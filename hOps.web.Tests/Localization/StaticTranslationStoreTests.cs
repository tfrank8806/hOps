using System;
using System.IO;
using hOps.web.Localization;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace hOps.web.Tests.Localization
{
    public class StaticTranslationStoreTests
    {
        [Fact]
        public void GetTranslations_DuplicateKeys_DoesNotThrowAndReturnsTranslations()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var localizationDirectory = Path.Combine(tempRoot, "Localization");
            Directory.CreateDirectory(localizationDirectory);

            var payload = """
{
  "Alpha": "Uno",
  "alpha": "Duplicado",
  "Work Orders": "Órdenes de trabajo"
}
""";
            var spanishFile = Path.Combine(localizationDirectory, "static.es.json");
            File.WriteAllText(spanishFile, payload);

            try
            {
                var environment = new TestHostEnvironment(tempRoot);
                var store = new StaticTranslationStore(environment, NullLogger<StaticTranslationStore>.Instance);

                var translations = store.GetTranslations("es");

                Assert.NotNull(translations);
                Assert.NotEmpty(translations);

                Assert.Equal("Uno", translations["Alpha"]);
                Assert.Equal("Órdenes de trabajo", translations["Work Orders"]);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
        }

        private sealed class TestHostEnvironment : IHostEnvironment
        {
            public TestHostEnvironment(string contentRoot)
            {
                ContentRootPath = contentRoot;
                ContentRootFileProvider = new PhysicalFileProvider(contentRoot);
            }

            public string ApplicationName { get; set; } = "Tests";

            public IFileProvider ContentRootFileProvider { get; set; }

            public string ContentRootPath { get; set; }

            public string EnvironmentName { get; set; } = Environments.Development;
        }
    }
}
