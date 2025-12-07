using System;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Areas.Identity.Pages.Account;
using hOps.web.Controllers;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Options;
using hOps.web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using Xunit;

namespace hOps.web.Tests;

public class RegisterModelTests
{
    [Fact]
    public async Task OnPostAsync_SendsManagerNotificationWithEncodedUrl()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new ApplicationDbContext(options);

        var managerRole = new IdentityRole { Id = "role-manager", Name = "Manager", NormalizedName = "MANAGER" };
        var managerUser = new ApplicationUser { Id = "user-manager", UserName = "manager", Email = "manager@example.com" };
        dbContext.Roles.Add(managerRole);
        dbContext.Users.Add(managerUser);
        dbContext.UserRoles.Add(new IdentityUserRole<string> { RoleId = managerRole.Id, UserId = managerUser.Id });
        var property = new Property { Id = 1, Code = "PROP", Name = "Test Property" };
        dbContext.Properties.Add(property);
        await dbContext.SaveChangesAsync();
        dbContext.UserPropertyAccesses.Add(new UserPropertyAccess
        {
            ApplicationUserId = managerUser.Id,
            PropertyId = property.Id
        });
        await dbContext.SaveChangesAsync();

        var propertyLookup = await dbContext.Properties.AsNoTracking().FirstOrDefaultAsync(p => p.Code.ToUpper() == "PROP");
        Assert.NotNull(propertyLookup);

        var accessibleManagers = await (from ur in dbContext.UserRoles
                                        join u in dbContext.Users on ur.UserId equals u.Id
                                        join access in dbContext.UserPropertyAccesses on u.Id equals access.ApplicationUserId
                                        where ur.RoleId == managerRole.Id && access.PropertyId == property.Id
                                        select u).ToListAsync();
        Assert.Single(accessibleManagers);

        var userStoreMock = new Mock<IUserEmailStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        userManagerMock
            .Setup(um => um.SupportsUserEmail)
            .Returns(true);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Method = "POST";
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        var formFeature = new TestFormFeature(new FormCollection(new Dictionary<string, StringValues>
        {
            ["g-recaptcha-response"] = "token"
        }));
        httpContext.Features.Set<IFormFeature>(formFeature);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(a => a.HttpContext).Returns(httpContext);

        var signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            userManagerMock.Object,
            httpContextAccessor.Object,
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null,
            null,
            null,
            null);

        signInManagerMock
            .Setup(s => s.GetExternalAuthenticationSchemesAsync())
            .ReturnsAsync(Enumerable.Empty<AuthenticationScheme>());

        var emailSender = new TestEmailSender();

        var loggerMock = new Mock<ILogger<RegisterModel>>();
        var logMessages = new List<string>();
        loggerMock
            .Setup(l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((_, __) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception, string>>()))
            .Callback<LogLevel, EventId, object, Exception, Delegate>((level, eventId, state, exception, formatter) =>
            {
                logMessages.Add(state?.ToString() ?? string.Empty);
            });
        var captchaValidatorMock = new Mock<ICaptchaValidator>();
        captchaValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(true);

        var captchaOptions = Microsoft.Extensions.Options.Options.Create(new CaptchaOptions
        {
            Enabled = true,
            SiteKey = "site-key",
            SecretKey = "secret-key"
        });
        var dataProtectionProvider = new EphemeralDataProtectionProvider();

        var urlHelperMock = new Mock<IUrlHelper>(MockBehavior.Strict);
        var encodedUrlTarget = "https://example.com/Admin/AccessRequests?token=abc&value=1";
        urlHelperMock
            .Setup(u => u.Content("~/"))
            .Returns("/");

        urlHelperMock
            .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
            .Returns(encodedUrlTarget);

        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor());
        var pageContext = new PageContext(actionContext);

        var registerModel = new RegisterModel(
            userManagerMock.Object,
            userStoreMock.Object,
            signInManagerMock.Object,
            loggerMock.Object,
            emailSender,
            dbContext,
            captchaValidatorMock.Object,
            captchaOptions,
            dataProtectionProvider)
        {
            PageContext = pageContext,
            Url = urlHelperMock.Object,
            Input = new RegisterModel.InputModel
            {
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                PropertyCode = "PROP",
                MobilePhone = "1234567890"
            }
        };

        Assert.Equal("PROP", registerModel.Input.PropertyCode);

        var privateMethod = typeof(RegisterModel).GetMethod("GetManagersForPropertyCodeAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(privateMethod);
        var tupleTask = (Task<ValueTuple<System.Collections.Generic.List<ApplicationUser>, bool>>)privateMethod!.Invoke(registerModel, new object?[] { "PROP" })!;
        var tupleResult = await tupleTask;
        Assert.True(tupleResult.Item2);
        Assert.Single(tupleResult.Item1);

        // Act
        var result = await registerModel.OnPostAsync();

        // Assert
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("./RegisterConfirmation", redirect.PageName);
        Assert.Equal("test@example.com", redirect.RouteValues?["email"]);
        var savedRequest = Assert.Single(dbContext.UserAccessRequests);
        Assert.Equal("test@example.com", savedRequest.Email);
        Assert.False(string.IsNullOrWhiteSpace(savedRequest.PasswordHash));
        Assert.Equal("PROP", savedRequest.PropertyCode);
        Assert.Contains(logMessages, msg => msg.Contains("Dispatching access request email", StringComparison.Ordinal));
        var routingLog = logMessages.LastOrDefault(msg => msg.Contains("Routing access request for property", StringComparison.Ordinal));
        Assert.NotNull(routingLog);
        Assert.Contains("1 managers", routingLog, StringComparison.Ordinal);
        var sentEmail = Assert.Single(emailSender.SentEmails);
        Assert.False(string.IsNullOrWhiteSpace(sentEmail.Body));
        Assert.Equal(managerUser.Email, sentEmail.Email);
        Assert.Contains("https://example.com/Admin/AccessRequests?token=abc&amp;value=1", sentEmail.Body, StringComparison.Ordinal);
        urlHelperMock.Verify(u => u.Action(It.IsAny<UrlActionContext>()), Times.AtLeastOnce());
    }

    [Fact]
    public async Task OnPostAsync_EncodesUserInputInManagerNotification()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new ApplicationDbContext(options);

        var managerRole = new IdentityRole { Id = "role-manager", Name = "Manager", NormalizedName = "MANAGER" };
        var managerUser = new ApplicationUser { Id = "user-manager", UserName = "<Manager>", Email = "manager@example.com" };
        dbContext.Roles.Add(managerRole);
        dbContext.Users.Add(managerUser);
        dbContext.UserRoles.Add(new IdentityUserRole<string> { RoleId = managerRole.Id, UserId = managerUser.Id });
        var property = new Property { Id = 1, Code = "PROP&1", Name = "Test Property" };
        dbContext.Properties.Add(property);
        await dbContext.SaveChangesAsync();
        dbContext.UserPropertyAccesses.Add(new UserPropertyAccess
        {
            ApplicationUserId = managerUser.Id,
            PropertyId = property.Id
        });
        await dbContext.SaveChangesAsync();

        var userStoreMock = new Mock<IUserEmailStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        userManagerMock
            .Setup(um => um.SupportsUserEmail)
            .Returns(true);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Method = "POST";
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        var formFeature = new TestFormFeature(new FormCollection(new Dictionary<string, StringValues>
        {
            ["g-recaptcha-response"] = "token"
        }));
        httpContext.Features.Set<IFormFeature>(formFeature);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(a => a.HttpContext).Returns(httpContext);

        var signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            userManagerMock.Object,
            httpContextAccessor.Object,
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null,
            null,
            null,
            null);

        signInManagerMock
            .Setup(s => s.GetExternalAuthenticationSchemesAsync())
            .ReturnsAsync(Enumerable.Empty<AuthenticationScheme>());

        var emailSender = new TestEmailSender();

        var loggerMock = new Mock<ILogger<RegisterModel>>();
        var captchaValidatorMock = new Mock<ICaptchaValidator>();
        captchaValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(true);

        var captchaOptions = Microsoft.Extensions.Options.Options.Create(new CaptchaOptions
        {
            Enabled = true,
            SiteKey = "site-key",
            SecretKey = "secret-key"
        });
        var dataProtectionProvider = new EphemeralDataProtectionProvider();

        var urlHelperMock = new Mock<IUrlHelper>(MockBehavior.Strict);
        var encodedUrlTarget = "https://example.com/Admin/AccessRequests?token=abc&value=1";
        urlHelperMock
            .Setup(u => u.Content("~/"))
            .Returns("/");

        urlHelperMock
            .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
            .Returns(encodedUrlTarget);

        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor());
        var pageContext = new PageContext(actionContext);

        var registerModel = new RegisterModel(
            userManagerMock.Object,
            userStoreMock.Object,
            signInManagerMock.Object,
            loggerMock.Object,
            emailSender,
            dbContext,
            captchaValidatorMock.Object,
            captchaOptions,
            dataProtectionProvider)
        {
            PageContext = pageContext,
            Url = urlHelperMock.Object,
            Input = new RegisterModel.InputModel
            {
                FirstName = "<Alice>",
                LastName = "User & Co",
                Email = "test@example.com",
                PropertyCode = "PROP&1",
                MobilePhone = "1234567890"
            }
        };

        var properties = await dbContext.Properties.AsNoTracking().Select(p => p.Code).ToListAsync();
        Assert.Contains("PROP&1", properties);

        var managerCheckMethod = typeof(RegisterModel).GetMethod("GetManagersForPropertyCodeAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(managerCheckMethod);
        var managerCheckTask = (Task<ValueTuple<System.Collections.Generic.List<ApplicationUser>, bool>>)managerCheckMethod!.Invoke(registerModel, new object?[] { "PROP&1" })!;
        var managerCheckResult = await managerCheckTask;
        Assert.True(managerCheckResult.Item2);
        Assert.Single(managerCheckResult.Item1);

        // Act
        var result = await registerModel.OnPostAsync();

        // Assert
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("./RegisterConfirmation", redirect.PageName);
        Assert.Equal("test@example.com", redirect.RouteValues?["email"]);
        var sentEmail = Assert.Single(emailSender.SentEmails);
        Assert.Equal(managerUser.Email, sentEmail.Email);
        Assert.Contains("Hello &lt;Manager&gt;,", sentEmail.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Hello <Manager>,", sentEmail.Body, StringComparison.Ordinal);
        Assert.Contains("Name: &lt;Alice&gt; User &amp; Co<br/>", sentEmail.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Name: <Alice> User & Co<br/>", sentEmail.Body, StringComparison.Ordinal);
        Assert.Contains("Property Code: PROP&amp;1<br/><br/>", sentEmail.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Property Code: PROP&1<br/><br/>", sentEmail.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnPostAsync_NotifiesAdminsWhenManagersDoNotMatchProperty()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var dbContext = new ApplicationDbContext(options);

        var adminRole = new IdentityRole { Id = "role-admin", Name = "Admin", NormalizedName = "ADMIN" };
        var managerRole = new IdentityRole { Id = "role-manager", Name = "Manager", NormalizedName = "MANAGER" };
        var adminUser = new ApplicationUser { Id = "user-admin", UserName = "admin", Email = "admin@example.com" };
        var managerUser = new ApplicationUser { Id = "user-manager", UserName = "manager", Email = "manager@example.com" };
        var propertyA = new Property { Id = 1, Code = "PROP-A", Name = "Property A" };
        var propertyB = new Property { Id = 2, Code = "PROP-B", Name = "Property B" };

        dbContext.Roles.AddRange(adminRole, managerRole);
        dbContext.Users.AddRange(adminUser, managerUser);
        dbContext.UserRoles.AddRange(
            new IdentityUserRole<string> { RoleId = adminRole.Id, UserId = adminUser.Id },
            new IdentityUserRole<string> { RoleId = managerRole.Id, UserId = managerUser.Id });
        dbContext.Properties.AddRange(propertyA, propertyB);
        await dbContext.SaveChangesAsync();

        dbContext.UserPropertyAccesses.Add(new UserPropertyAccess
        {
            ApplicationUserId = managerUser.Id,
            PropertyId = propertyA.Id
        });
        await dbContext.SaveChangesAsync();

        var userStoreMock = new Mock<IUserEmailStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

        userManagerMock
            .Setup(um => um.SupportsUserEmail)
            .Returns(true);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Method = "POST";
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        var formFeature = new TestFormFeature(new FormCollection(new Dictionary<string, StringValues>
        {
            ["g-recaptcha-response"] = "token"
        }));
        httpContext.Features.Set<IFormFeature>(formFeature);

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.SetupGet(a => a.HttpContext).Returns(httpContext);

        var signInManagerMock = new Mock<SignInManager<ApplicationUser>>(
            userManagerMock.Object,
            httpContextAccessor.Object,
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null,
            null,
            null,
            null);

        signInManagerMock
            .Setup(s => s.GetExternalAuthenticationSchemesAsync())
            .ReturnsAsync(Enumerable.Empty<AuthenticationScheme>());

        var emailSender = new TestEmailSender();

        var loggerMock = new Mock<ILogger<RegisterModel>>();
        var captchaValidatorMock = new Mock<ICaptchaValidator>();
        captchaValidatorMock
            .Setup(v => v.ValidateAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<System.Threading.CancellationToken>()))
            .ReturnsAsync(true);

        var captchaOptions = Microsoft.Extensions.Options.Options.Create(new CaptchaOptions
        {
            Enabled = true,
            SiteKey = "site-key",
            SecretKey = "secret-key"
        });
        var dataProtectionProvider = new EphemeralDataProtectionProvider();

        var urlHelperMock = new Mock<IUrlHelper>(MockBehavior.Strict);
        urlHelperMock
            .Setup(u => u.Content("~/"))
            .Returns("/");
        urlHelperMock
            .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
            .Returns("https://example.com/Admin/AccessRequests");

        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor());
        var pageContext = new PageContext(actionContext);

        var registerModel = new RegisterModel(
            userManagerMock.Object,
            userStoreMock.Object,
            signInManagerMock.Object,
            loggerMock.Object,
            emailSender,
            dbContext,
            captchaValidatorMock.Object,
            captchaOptions,
            dataProtectionProvider)
        {
            PageContext = pageContext,
            Url = urlHelperMock.Object,
            Input = new RegisterModel.InputModel
            {
                FirstName = "Test",
                LastName = "User",
                Email = "test@example.com",
                PropertyCode = propertyB.Code,
                MobilePhone = "1234567890"
            }
        };

        // Act
        var result = await registerModel.OnPostAsync();

        // Assert
        Assert.IsType<RedirectToPageResult>(result);
        var recipients = emailSender.SentEmails.Select(e => e.Email).ToList();
        Assert.Single(recipients);
        Assert.Contains(adminUser.Email, recipients);
        Assert.DoesNotContain(managerUser.Email, recipients);
    }

    private sealed class TestFormFeature : IFormFeature
    {
        public TestFormFeature(IFormCollection form)
        {
            Form = form;
        }

        public bool HasFormContentType => true;

        public IFormCollection Form { get; set; } = default!;

        public IFormCollection ReadForm()
        {
            return Form;
        }

        public Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Form);
        }
    }

    private sealed class TestEmailSender : IEmailSender
    {
        public List<SentEmail> SentEmails { get; } = new();

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            SentEmails.Add(new SentEmail(email, subject, htmlMessage));
            return Task.CompletedTask;
        }

        public readonly record struct SentEmail(string Email, string Subject, string Body);
    }
}
