using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using hOps.web.Controllers;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.Services;
using hOps.web.ViewModels.Messages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace hOps.web.Tests;

public class DirectMessageServiceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly DirectMessageService _service;

    public DirectMessageServiceTests()
    {
        _context = DirectMessageTestHelpers.CreateContext();
        _userManagerMock = DirectMessageTestHelpers.CreateUserManager(_context);
        _service = DirectMessageTestHelpers.CreateService(_context, _userManagerMock.Object);
    }

    [Fact]
    public async Task ArchiveConversationForUserAsync_OnlyArchivesRequestingParticipant()
    {
        var (userA, userB, conversation, _) = await DirectMessageTestHelpers.SeedConversationAsync(_context);

        await _service.ArchiveConversationForUserAsync(conversation.Id, userA.Id);

        var reloaded = await _context.DirectMessageConversations.FirstAsync(c => c.Id == conversation.Id);
        Assert.True(reloaded.ParticipantAArchived);
        Assert.False(reloaded.ParticipantBArchived);

        var userASummaries = await _service.GetConversationSummariesAsync(userA.Id);
        Assert.Empty(userASummaries);

        var userBSummaries = await _service.GetConversationSummariesAsync(userB.Id);
        var summary = Assert.Single(userBSummaries);
        Assert.Equal(conversation.Id, summary.ConversationId);
    }

    [Fact]
    public async Task GetConversationDetailAsync_UnarchivesAndMarksUnreadMessages()
    {
        var (userA, _, conversation, message) = await DirectMessageTestHelpers.SeedConversationAsync(_context, archivedForCurrent: true);

        var notification = new UserNotification
        {
            UserId = userA.Id,
            Type = "message",
            Title = "New message",
            Content = "Hello",
            LinkUrl = $"/DirectMessages?conversationId={conversation.Id}",
            DirectMessageId = message.Id,
            CreatedAt = message.SentAt,
            IsRead = false
        };
        _context.UserNotifications.Add(notification);
        await _context.SaveChangesAsync();

        var detail = await _service.GetConversationDetailAsync(conversation.Id, userA.Id);

        Assert.NotNull(detail);
        Assert.Equal(conversation.Id, detail.ConversationId);
        Assert.Single(detail.Messages);

        var reloadedConversation = await _context.DirectMessageConversations.FirstAsync(c => c.Id == conversation.Id);
        Assert.False(reloadedConversation.ParticipantAArchived);

        var reloadedMessage = await _context.DirectMessages.FirstAsync(m => m.Id == message.Id);
        Assert.True(reloadedMessage.IsRead);
        Assert.NotNull(reloadedMessage.ReadAt);

        var reloadedNotification = await _context.UserNotifications.FirstAsync(n => n.Id == notification.Id);
        Assert.True(reloadedNotification.IsRead);
        Assert.NotNull(reloadedNotification.ReadAt);
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

public class DirectMessagesControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly ApplicationUser _currentUser;
    private readonly ApplicationUser _otherUser;
    private readonly DirectMessageService _service;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IUserTimeZoneService> _timeZoneServiceMock;
    private readonly int _conversationId;

    public DirectMessagesControllerTests()
    {
        _context = DirectMessageTestHelpers.CreateContext();
        (_currentUser, _otherUser, var conversation, _) = DirectMessageTestHelpers.SeedConversationAsync(_context).GetAwaiter().GetResult();
        _conversationId = conversation.Id;

        _userManagerMock = DirectMessageTestHelpers.CreateUserManager(_context, _currentUser);
        _service = DirectMessageTestHelpers.CreateService(_context, _userManagerMock.Object);

        _timeZoneServiceMock = new Mock<IUserTimeZoneService>();
        _timeZoneServiceMock.Setup(t => t.ConvertToUserTime(It.IsAny<DateTime>())).Returns<DateTime>(dt => dt);
        _timeZoneServiceMock.Setup(t => t.GetTimeZone()).Returns(TimeZoneInfo.Utc);
        _timeZoneServiceMock.Setup(t => t.FormatLocal(It.IsAny<DateTime>(), It.IsAny<string>()))
            .Returns<DateTime, string>((dt, format) => dt.ToString(format));
    }

    [Fact]
    public async Task ConversationListPartial_ReturnsConversationListPartialView()
    {
        var controller = CreateController();

        var result = await controller.ConversationListPartial(_conversationId) as PartialViewResult;

        Assert.NotNull(result);
        Assert.Equal("_ConversationItems", result.ViewName);

        var model = Assert.IsType<DirectMessagePageViewModel>(result.Model);
        var item = Assert.Single(model.Conversations);
        Assert.Equal(_conversationId, item.ConversationId);
        Assert.Equal(_otherUser.Id, item.ParticipantId);
    }

    [Fact]
    public async Task ConversationMessagesPartial_ReturnsThreadPartialView()
    {
        var controller = CreateController();

        var result = await controller.ConversationMessagesPartial(_conversationId) as PartialViewResult;

        Assert.NotNull(result);
        Assert.Equal("_ConversationMessages", result.ViewName);

        var model = Assert.IsType<ConversationDetailViewModel>(result.Model);
        Assert.Equal(_conversationId, model.ConversationId);
        Assert.Single(model.Messages);
    }

    private DirectMessagesController CreateController()
    {
        var controller = new DirectMessagesController(_context, _userManagerMock.Object, _service, _timeZoneServiceMock.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = DirectMessageTestHelpers.CreatePrincipal(_currentUser.Id)
                }
            }
        };

        return controller;
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

internal static class DirectMessageTestHelpers
{
    internal static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    internal static Mock<UserManager<ApplicationUser>> CreateUserManager(ApplicationDbContext context, ApplicationUser? currentUser = null)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var manager = new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        manager.SetupGet(m => m.Users).Returns(context.Users);
        manager.Setup(m => m.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => context.Users.FirstOrDefault(u => u.Id == id));
        manager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync((ApplicationUser _) => (IList<string>)new List<string> { "Admin" });

        if (currentUser != null)
        {
            manager.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(currentUser);
        }

        return manager;
    }

    internal static DirectMessageService CreateService(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        var emailSender = new Mock<IEmailSender>();
        var logger = new Mock<ILogger<DirectMessageService>>();
        var realtimeNotifications = new Mock<IRealtimeNotificationService>();
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:BaseUrl"] = "https://example.test"
            })
            .Build();

        return new DirectMessageService(
            context,
            userManager,
            emailSender.Object,
            logger.Object,
            realtimeNotifications.Object,
            httpContextAccessor,
            configuration);
    }

    internal static ClaimsPrincipal CreatePrincipal(string userId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, $"user-{userId}")
        }, "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    internal static async Task<(ApplicationUser Current, ApplicationUser Other, DirectMessageConversation Conversation, DirectMessage Message)> SeedConversationAsync(
        ApplicationDbContext context,
        bool archivedForCurrent = false)
    {
        var currentUser = new ApplicationUser
        {
            Id = "user-a",
            UserName = "user.a",
            Email = "user.a@example.com",
            FirstName = "User",
            LastName = "A"
        };

        var otherUser = new ApplicationUser
        {
            Id = "user-b",
            UserName = "user.b",
            Email = "user.b@example.com",
            FirstName = "User",
            LastName = "B"
        };

        context.Users.AddRange(currentUser, otherUser);

        var conversation = new DirectMessageConversation
        {
            ParticipantAId = currentUser.Id,
            ParticipantBId = otherUser.Id,
            ParticipantAArchived = archivedForCurrent,
            ParticipantBArchived = false,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            UpdatedAt = DateTime.UtcNow.AddDays(-1)
        };

        context.DirectMessageConversations.Add(conversation);
        await context.SaveChangesAsync();

        var message = new DirectMessage
        {
            ConversationId = conversation.Id,
            SenderId = otherUser.Id,
            RecipientId = currentUser.Id,
            Body = "Hello from teammate",
            SentAt = DateTime.UtcNow.AddMinutes(-30),
            IsRead = false
        };

        context.DirectMessages.Add(message);
        await context.SaveChangesAsync();

        return (currentUser, otherUser, conversation, message);
    }
}
