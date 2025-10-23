using System;
using System.Linq;
using System.Threading.Tasks;
using hOps.web.Areas.Identity.Pages.Account;
using hOps.web.Controllers;
using hOps.web.Data;
using hOps.web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
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

        var emailSenderMock = new Mock<IEmailSender>();
        string? capturedBody = null;
        emailSenderMock
            .Setup(e => e.SendEmailAsync(managerUser.Email, "New Access Request", It.IsAny<string>()))
            .Callback<string, string, string>((_, _, body) => capturedBody = body)
            .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<RegisterModel>>();

        var urlHelperMock = new Mock<IUrlHelper>(MockBehavior.Strict);
        var encodedUrlTarget = "https://example.com/Admin/AccessRequests?token=abc&value=1";
        urlHelperMock
            .Setup(u => u.Content("~/"))
            .Returns("/");

        urlHelperMock
            .Setup(u => u.Action(It.Is<UrlActionContext>(ctx =>
                ctx.Action == nameof(AdminController.AccessRequests) &&
                ctx.Controller == "Admin" &&
                ctx.Protocol == httpContext.Request.Scheme)))
            .Returns(encodedUrlTarget);

        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor());
        var pageContext = new PageContext(actionContext);

        var registerModel = new RegisterModel(
            userManagerMock.Object,
            userStoreMock.Object,
            signInManagerMock.Object,
            loggerMock.Object,
            emailSenderMock.Object,
            dbContext)
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

        // Act
        var result = await registerModel.OnPostAsync();

        // Assert
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("./RegisterConfirmation", redirect.PageName);
        Assert.Equal("test@example.com", redirect.RouteValues?["email"]);
        var savedRequest = Assert.Single(dbContext.UserAccessRequests);
        Assert.Equal("test@example.com", savedRequest.Email);
        Assert.False(string.IsNullOrWhiteSpace(savedRequest.PasswordHash));
        Assert.NotNull(capturedBody);
        Assert.Contains("https://example.com/Admin/AccessRequests?token=abc&amp;value=1", capturedBody, StringComparison.Ordinal);

        emailSenderMock.Verify(e => e.SendEmailAsync(managerUser.Email, "New Access Request", It.IsAny<string>()), Times.Once);
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

        var emailSenderMock = new Mock<IEmailSender>();
        string? capturedBody = null;
        emailSenderMock
            .Setup(e => e.SendEmailAsync(managerUser.Email, "New Access Request", It.IsAny<string>()))
            .Callback<string, string, string>((_, _, body) => capturedBody = body)
            .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<RegisterModel>>();

        var urlHelperMock = new Mock<IUrlHelper>(MockBehavior.Strict);
        var encodedUrlTarget = "https://example.com/Admin/AccessRequests?token=abc&value=1";
        urlHelperMock
            .Setup(u => u.Content("~/"))
            .Returns("/");

        urlHelperMock
            .Setup(u => u.Action(It.Is<UrlActionContext>(ctx =>
                ctx.Action == nameof(AdminController.AccessRequests) &&
                ctx.Controller == "Admin" &&
                ctx.Protocol == httpContext.Request.Scheme)))
            .Returns(encodedUrlTarget);

        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor());
        var pageContext = new PageContext(actionContext);

        var registerModel = new RegisterModel(
            userManagerMock.Object,
            userStoreMock.Object,
            signInManagerMock.Object,
            loggerMock.Object,
            emailSenderMock.Object,
            dbContext)
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

        // Act
        var result = await registerModel.OnPostAsync();

        // Assert
        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("./RegisterConfirmation", redirect.PageName);
        Assert.Equal("test@example.com", redirect.RouteValues?["email"]);
        Assert.NotNull(capturedBody);
        Assert.Contains("Hello &lt;Manager&gt;,", capturedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Hello <Manager>,", capturedBody, StringComparison.Ordinal);
        Assert.Contains("Name: &lt;Alice&gt; User &amp; Co<br/>", capturedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Name: <Alice> User & Co<br/>", capturedBody, StringComparison.Ordinal);
        Assert.Contains("Property Code: PROP&amp;1<br/><br/>", capturedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("Property Code: PROP&1<br/><br/>", capturedBody, StringComparison.Ordinal);

        emailSenderMock.Verify(e => e.SendEmailAsync(managerUser.Email, "New Access Request", It.IsAny<string>()), Times.Once);
    }
}
