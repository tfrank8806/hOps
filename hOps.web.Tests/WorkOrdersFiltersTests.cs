using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using hOps.web.Controllers;
using hOps.web.Data;
using hOps.web.Localization;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.Services.Localization;
using hOps.web.ViewModels.WorkOrders;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace hOps.web.Tests;

public sealed class WorkOrdersFiltersTests
{
    [Fact]
    public async Task IndexFilters_DepartmentsAndTypesAreUniqueForSelectedProperty()
    {
        await using var scope = await WorkOrdersFiltersTestScope.CreateAsync();

        var filters = new WorkOrderFilterInput
        {
            PropertyIds = new List<int> { scope.Property1.Id }
        };

        var viewModel = await scope.BuildViewModelAsync(filters, scope.Property1);

        Assert.All(
            viewModel.Departments.Where(d => d.PropertyId.HasValue),
            d => Assert.Contains(d.PropertyId!.Value, filters.PropertyIds));

        Assert.Equal(
            viewModel.Departments
                .Select(d => (d.Name ?? string.Empty).Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            viewModel.Departments.Count);

        Assert.All(
            viewModel.WorkOrderTypes.Where(t => t.PropertyId.HasValue),
            t => Assert.Contains(t.PropertyId!.Value, filters.PropertyIds));

        Assert.Equal(
            viewModel.WorkOrderTypes
                .Select(t => (t.Name ?? string.Empty).Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            viewModel.WorkOrderTypes.Count);
    }

    [Fact]
    public async Task IndexFilters_ChangeWhenSelectedPropertyChanges()
    {
        await using var scope = await WorkOrdersFiltersTestScope.CreateAsync();

        var firstFilters = new WorkOrderFilterInput
        {
            PropertyIds = new List<int> { scope.Property1.Id }
        };

        var firstViewModel = await scope.BuildViewModelAsync(firstFilters, scope.Property1);

        var secondFilters = new WorkOrderFilterInput
        {
            PropertyIds = new List<int> { scope.Property2.Id }
        };

        var secondViewModel = await scope.BuildViewModelAsync(secondFilters, scope.Property2);

        var firstDepartmentNames = firstViewModel.Departments.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var secondDepartmentNames = secondViewModel.Departments.Select(d => d.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Security", firstDepartmentNames);
        Assert.Contains("Security", secondDepartmentNames);

        var firstTypeNames = firstViewModel.WorkOrderTypes.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var secondTypeNames = secondViewModel.WorkOrderTypes.Select(t => t.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("Electrical", firstTypeNames);
        Assert.Contains("Electrical", secondTypeNames);
    }

    [Fact]
    public async Task CreateFormOptions_ReturnsUniqueValuesForRequestedProperties()
    {
        await using var scope = await WorkOrdersFiltersTestScope.CreateAsync();

        var result = await scope.Controller.CreateFormOptions(new[] { scope.Property1.Id, scope.Property2.Id });
        var json = Assert.IsType<JsonResult>(result);

        var departmentObjects = ExtractAnonymousList(json.Value!, "departments");
        var departmentNames = departmentObjects
            .Select(item => item.GetType().GetProperty("name")!.GetValue(item) as string ?? string.Empty)
            .ToList();

        Assert.Equal(
            departmentNames.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            departmentNames.Count);

        var typeObjects = ExtractAnonymousList(json.Value!, "workOrderTypes");
        var typeNames = typeObjects
            .Select(item => item.GetType().GetProperty("name")!.GetValue(item) as string ?? string.Empty)
            .ToList();

        Assert.Equal(
            typeNames.Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            typeNames.Count);
    }

    private static IEnumerable<object> ExtractAnonymousList(object source, string propertyName)
    {
        var property = source.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        var value = property!.GetValue(source);
        Assert.NotNull(value);

        if (value is IEnumerable enumerable)
        {
            foreach (var item in enumerable)
            {
                yield return item!;
            }
        }
        else
        {
            throw new InvalidOperationException($"Property '{propertyName}' was not enumerable.");
        }
    }

    private sealed class WorkOrdersFiltersTestScope : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MethodInfo _buildViewModelMethod;

        private WorkOrdersFiltersTestScope(
            SqliteConnection connection,
            ApplicationDbContext dbContext,
            WorkOrdersController controller,
            ApplicationUser user,
            Property property1,
            Property property2)
        {
            _connection = connection;
            DbContext = dbContext;
            Controller = controller;
            User = user;
            Property1 = property1;
            Property2 = property2;
            _buildViewModelMethod = typeof(WorkOrdersController)
                .GetMethod("BuildViewModelAsync", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Unable to locate BuildViewModelAsync.");
        }

        public ApplicationDbContext DbContext { get; }
        public WorkOrdersController Controller { get; }
        public ApplicationUser User { get; }
        public Property Property1 { get; }
        public Property Property2 { get; }

        public static async Task<WorkOrdersFiltersTestScope> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new ApplicationDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var property1 = new Property { Name = "Alpha Suites", Code = "ALP" };
            var property2 = new Property { Name = "Beacon Tower", Code = "BCT" };
            dbContext.Properties.AddRange(property1, property2);
            await dbContext.SaveChangesAsync();

            var globalDepartment = new Department { Name = "Front Desk" };
            var property1DepartmentA = new Department { Name = "Maintenance", PropertyId = property1.Id };
            var property1DepartmentB = new Department { Name = "Housekeeping", PropertyId = property1.Id };
            var property2DepartmentA = new Department { Name = "Maintenance", PropertyId = property2.Id };
            var property2DepartmentB = new Department { Name = "Security", PropertyId = property2.Id };
            dbContext.Departments.AddRange(globalDepartment, property1DepartmentA, property1DepartmentB, property2DepartmentA, property2DepartmentB);

            var globalType = new WorkOrderType { Name = "General" };
            var property1TypeA = new WorkOrderType { Name = "Plumbing", PropertyId = property1.Id };
            var property1TypeB = new WorkOrderType { Name = "HVAC", PropertyId = property1.Id };
            var property2TypeA = new WorkOrderType { Name = "Plumbing", PropertyId = property2.Id };
            var property2TypeB = new WorkOrderType { Name = "Electrical", PropertyId = property2.Id };
            dbContext.WorkOrderTypes.AddRange(globalType, property1TypeA, property1TypeB, property2TypeA, property2TypeB);

            var user = new ApplicationUser
            {
                Id = "user-1",
                Email = "user1@example.com",
                UserName = "user1@example.com",
                FirstName = "Case",
                LastName = "Tester"
            };

            dbContext.Users.Add(user);

            dbContext.UserPropertyAccesses.AddRange(
                new UserPropertyAccess { ApplicationUserId = user.Id, ApplicationUser = user, PropertyId = property1.Id, Property = property1 },
                new UserPropertyAccess { ApplicationUserId = user.Id, ApplicationUser = user, PropertyId = property2.Id, Property = property2 });

            await dbContext.SaveChangesAsync();

            var userStore = new Mock<IUserStore<ApplicationUser>>();
            var userManagerMock = new Mock<UserManager<ApplicationUser>>(
                userStore.Object,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);

            userManagerMock
                .Setup(um => um.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            userManagerMock
                .Setup(um => um.GetRolesAsync(user))
                .ReturnsAsync(new List<string>());

            userManagerMock
                .SetupGet(um => um.Users)
                .Returns(dbContext.Users);

            var emailSender = Mock.Of<IEmailSender>();
            var mentionService = new MentionService(
                dbContext,
                userManagerMock.Object,
                emailSender,
                Mock.Of<ILogger<MentionService>>());

            var environment = Mock.Of<IWebHostEnvironment>(env => env.WebRootPath == AppContext.BaseDirectory);

            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["WorkOrders:DefaultStatus"] = "New"
                })
                .Build();

            var translationService = new StubTranslationService();
            var timeZoneService = new StubUserTimeZoneService();
            var logger = Mock.Of<ILogger<WorkOrdersController>>();

            var controller = new WorkOrdersController(
                dbContext,
                userManagerMock.Object,
                environment,
                configuration,
                logger,
                mentionService,
                emailSender,
                timeZoneService,
                translationService);

            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id)
                }, "TestAuth"))
            };

            httpContext.Items["ActiveLanguage"] = LanguageConstants.English;
            httpContext.RequestServices = new ServiceCollection()
                .BuildServiceProvider();

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

            return new WorkOrdersFiltersTestScope(connection, dbContext, controller, user, property1, property2);
        }

        public async Task<WorkOrdersViewModel> BuildViewModelAsync(WorkOrderFilterInput filters, Property? currentProperty)
        {
            if (currentProperty != null)
            {
                Controller.ViewBag.CurrentProperty = currentProperty;
            }

            var task = (Task<WorkOrdersViewModel>)_buildViewModelMethod.Invoke(Controller, new object?[] { filters, null })!;
            return await task.ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class StubTranslationService : ITranslationService
    {
        public string DefaultLanguage => LanguageConstants.English;
        public IReadOnlyList<LanguageOption> SupportedLanguages { get; } = LanguageConstants.SupportedLanguages;

        public string Translate(string key, string targetLanguage, string? fallback = null) =>
            fallback ?? key;

        public bool TryTranslate(string key, string targetLanguage, out string translation)
        {
            translation = key;
            return false;
        }

        public Task<string> TranslateDynamicAsync(
            string entityType,
            string entityId,
            string field,
            string sourceText,
            string sourceLanguage,
            string targetLanguage,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(sourceText);
        }
    }

    private sealed class StubUserTimeZoneService : IUserTimeZoneService
    {
        public TimeZoneInfo GetTimeZone() => TimeZoneInfo.Utc;

        public DateTime ConvertToUserTime(DateTime utcDateTime) =>
            utcDateTime.Kind == DateTimeKind.Utc
                ? utcDateTime
                : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        public string FormatLocal(DateTime utcDateTime, string format) =>
            ConvertToUserTime(utcDateTime).ToString(format, CultureInfo.InvariantCulture);
    }
}
