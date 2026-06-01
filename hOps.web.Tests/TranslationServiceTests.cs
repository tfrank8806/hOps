using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using hOps.web.Data;
using hOps.web.Localization;
using hOps.web.Models;
using hOps.web.Services.Localization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace hOps.web.Tests
{
    public sealed class TranslationServiceTests : IDisposable
    {
        private readonly string _contentRoot;
        private readonly SqliteConnection _connection;
        private readonly ApplicationDbContext _context;
        private readonly StaticTranslationStore _staticStore;
        private readonly StubTranslationProvider _provider;
        private readonly IMemoryCache _memoryCache;
        private readonly TranslationService _translationService;

        public TranslationServiceTests()
        {
            _contentRoot = Path.Combine(Path.GetTempPath(), "hops-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_contentRoot);
            Directory.CreateDirectory(Path.Combine(_contentRoot, "Localization"));

            File.WriteAllText(
                Path.Combine(_contentRoot, "Localization", "static.es.json"),
                """
                {
                  "Hello": "Hola",
                  "Lost & Found": "Objetos perdidos",
                  "Package & Mail Log": "Registro de paquetes y correo"
                }
                """);

            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            _context = new ApplicationDbContext(options);
            _context.Database.EnsureCreated();

            var hostEnvironment = new TestHostEnvironment(_contentRoot);
            _staticStore = new StaticTranslationStore(hostEnvironment, NullLogger<StaticTranslationStore>.Instance);
            _provider = new StubTranslationProvider();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _translationService = new TranslationService(
                _context,
                _staticStore,
                _provider,
                _memoryCache,
                NullLogger<TranslationService>.Instance);
        }

        [Fact]
        public void Translate_UsesStaticDictionary()
        {
            var result = _translationService.Translate("Hello", LanguageConstants.Spanish, "Hello");
            Assert.Equal("Hola", result);
        }

        [Fact]
        public void Translate_HandlesAmpersandsInKeysAndFallbacks()
        {
            var spanishLostFound = _translationService.Translate("Lost & Found", LanguageConstants.Spanish, "Lost & Found");
            Assert.Equal("Objetos perdidos", spanishLostFound);

            var englishPackageMail = _translationService.Translate("Package & Mail Log", LanguageConstants.English, "Package & Mail Log");
            Assert.Equal("Package & Mail Log", englishPackageMail);

            var encoded = System.Text.Encodings.Web.HtmlEncoder.Default.Encode(englishPackageMail);
            Assert.Equal("Package &amp; Mail Log", encoded);
        }

        [Fact]
        public async Task TranslateDynamicAsync_CachesTranslations()
        {
            const string entityType = "TestEntity";
            const string entityId = "1";
            const string field = "Body";
            const string sourceText = "Hello world";

            _provider.Results.Enqueue("Hola mundo");

            var first = await _translationService.TranslateDynamicAsync(
                entityType,
                entityId,
                field,
                sourceText,
                LanguageConstants.English,
                LanguageConstants.Spanish);

            Assert.Equal("Hola mundo", first);

            var stored = await _context.TranslatedTexts.FirstOrDefaultAsync();
            Assert.NotNull(stored);
            Assert.Equal("Hola mundo", stored!.TranslatedTextValue);

            var second = await _translationService.TranslateDynamicAsync(
                entityType,
                entityId,
                field,
                sourceText,
                LanguageConstants.English,
                LanguageConstants.Spanish);

            Assert.Equal("Hola mundo", second);
        }

        [Fact]
        public async Task TranslateDynamicAsync_PreservesCurlyColorTokens()
        {
            const string entityType = "Announcement";
            const string entityId = "5";
            const string field = "Content";
            const string sourceText = "{{color:blue}}GSS:{{/color}} Testing the translation now.";

            _provider.Translator = text => text
                .Replace("Testing", "Probando")
                .Replace("the translation now", "la traducción ahora");

            var translated = await _translationService.TranslateDynamicAsync(
                entityType,
                entityId,
                field,
                sourceText,
                LanguageConstants.English,
                LanguageConstants.Spanish);

            Assert.Equal("{{color:blue}}GSS:{{/color}} Probando la traducción ahora.", translated);
        }

        [Fact]
        public async Task TranslateDynamicAsync_PreservesUrls()
        {
            const string entityType = "Announcement";
            const string entityId = "6";
            const string field = "Content";
            const string sourceText = "Visit https://example.com for details.";

            _provider.Translator = text => text
                .Replace("Visit", "Visita")
                .Replace("for details", "para más detalles");

            var translated = await _translationService.TranslateDynamicAsync(
                entityType,
                entityId,
                field,
                sourceText,
                LanguageConstants.English,
                LanguageConstants.Spanish);

            Assert.Equal("Visita https://example.com para más detalles.", translated);
        }

        public void Dispose()
        {
            _context.Dispose();
            _connection.Dispose();
            _memoryCache.Dispose();
            if (Directory.Exists(_contentRoot))
            {
                Directory.Delete(_contentRoot, recursive: true);
            }
        }

        private sealed class StubTranslationProvider : IExternalTranslationProvider
        {
            public Queue<string?> Results { get; } = new();
            public Func<string, string> Translator { get; set; } = text => text;

            public Task<string?> TranslateAsync(string text, string sourceLanguage, string targetLanguage, System.Threading.CancellationToken cancellationToken = default)
            {
                var result = Results.Count > 0 ? Results.Dequeue() : Translator(text);
                return Task.FromResult(result);
            }
        }

        private sealed class TestHostEnvironment : IHostEnvironment
        {
            public TestHostEnvironment(string contentRoot)
            {
                ContentRootPath = contentRoot;
                EnvironmentName = "Test";
                ApplicationName = "hOps.web.Tests";
            }

            public string ApplicationName { get; set; }
            public IFileProvider? ContentRootFileProvider { get; set; }
            public string ContentRootPath { get; set; }
            public string EnvironmentName { get; set; }
        }
    }
}
