using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using hOps.web.Controllers;
using hOps.web.Data;
using hOps.web.Models;
using hOps.web.ViewModels.Settings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace hOps.web.Tests;

public class SettingsControllerRoomsDeletionTests
{
    [Fact]
    public async Task DeleteRoom_RemovesOnlyTargetRoom()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var propertyA = new Property { Name = "North Tower", Code = "NT" };
        var propertyB = new Property { Name = "South Tower", Code = "ST" };
        db.Properties.AddRange(propertyA, propertyB);

        var user = new ApplicationUser { Id = "user-1", UserName = "manager@example.com" };
        db.Users.Add(user);

        await db.SaveChangesAsync();

        db.UserPropertyAccesses.Add(new UserPropertyAccess
        {
            ApplicationUserId = user.Id,
            PropertyId = propertyA.Id
        });

        await db.SaveChangesAsync();

        var roomA1 = new Room
        {
            PropertyId = propertyA.Id,
            RoomNumber = "101",
            Abbreviation = "A1",
            Floor = 1,
            RoomType = "King",
            Description = "North 101"
        };

        var roomA2 = new Room
        {
            PropertyId = propertyA.Id,
            RoomNumber = "101",
            Abbreviation = "A2",
            Floor = 2,
            RoomType = "Queen",
            Description = "North 201"
        };

        var roomB1 = new Room
        {
            PropertyId = propertyB.Id,
            RoomNumber = "101",
            Abbreviation = "B1",
            Floor = 1,
            RoomType = "King",
            Description = "South 101"
        };

        db.Rooms.AddRange(roomA1, roomA2, roomB1);
        await db.SaveChangesAsync();

        db.RoomLayouts.AddRange(
            new RoomLayout
            {
                PropertyId = propertyA.Id,
                RoomId = roomA1.Id,
                Floor = roomA1.Floor,
                X = 10,
                Y = 20,
                Width = 30,
                Height = 40
            },
            new RoomLayout
            {
                PropertyId = propertyA.Id,
                RoomId = roomA2.Id,
                Floor = roomA2.Floor,
                X = 50,
                Y = 60,
                Width = 30,
                Height = 40
            });

        await db.SaveChangesAsync();

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
            .ReturnsAsync(new List<string> { "Manager" });

        var logger = Mock.Of<ILogger<SettingsController>>();

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            }, "TestAuth"))
        };

        var controller = new SettingsController(db, userManagerMock.Object, logger)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };

        var request = new DeleteRoomRequest
        {
            RoomId = roomA1.Id,
            PropertyId = propertyA.Id
        };

        var result = await controller.DeleteRoom(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        Assert.Equal(2, await db.Rooms.AsNoTracking().CountAsync());
        Assert.Single(await db.Rooms.AsNoTracking().Where(r => r.PropertyId == propertyA.Id).ToListAsync());
        Assert.Single(await db.Rooms.AsNoTracking().Where(r => r.PropertyId == propertyB.Id).ToListAsync());

        Assert.False(await db.RoomLayouts.AsNoTracking().AnyAsync(rl => rl.RoomId == roomA1.Id));
        Assert.True(await db.RoomLayouts.AsNoTracking().AnyAsync(rl => rl.RoomId == roomA2.Id));
    }

    [Fact]
    public async Task DeleteRoom_ReturnsNotFound_WhenRoomDoesNotExist()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var property = new Property { Name = "North Tower", Code = "NT" };
        db.Properties.Add(property);

        var user = new ApplicationUser { Id = "user-1", UserName = "manager@example.com" };
        db.Users.Add(user);

        await db.SaveChangesAsync();

        db.UserPropertyAccesses.Add(new UserPropertyAccess
        {
            ApplicationUserId = user.Id,
            PropertyId = property.Id
        });

        await db.SaveChangesAsync();

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
            .ReturnsAsync(new List<string> { "Manager" });

        var logger = Mock.Of<ILogger<SettingsController>>();

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            }, "TestAuth"))
        };

        var controller = new SettingsController(db, userManagerMock.Object, logger)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };

        var request = new DeleteRoomRequest
        {
            RoomId = 999,
            PropertyId = property.Id
        };

        var result = await controller.DeleteRoom(request);

        Assert.IsType<NotFoundResult>(result);
    }
}
