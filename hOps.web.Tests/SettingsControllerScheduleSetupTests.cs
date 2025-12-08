using System;
using System.Collections.Generic;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Moq;
using Xunit;

namespace hOps.web.Tests;

public class SettingsControllerScheduleSetupTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task ScheduleSetup_AllowsSavingShift_WhenColorMissing(string? postedColor)
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var property = new Property { Id = 1, Name = "Test Property", Code = "TP" };
        db.Properties.Add(property);

        var user = new ApplicationUser { Id = "manager-1", UserName = "manager" };
        db.Users.Add(user);

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

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id)
            }, "Test"))
        };

        var controller = new SettingsController(db, userManagerMock.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>())
        };

        var model = new ScheduleSetupViewModel
        {
            SelectedPropertyId = property.Id,
            StartDayOfWeek = DayOfWeek.Monday,
            ShiftTemplates = new List<ScheduleShiftTemplateInputModel>
            {
                new()
                {
                    Name = "Day",
                    ShiftName = "Morning",
                    StartTime = "08:00",
                    EndTime = "16:00",
                    ColorHex = postedColor,
                    SortOrder = 0
                }
            }
        };

        var result = await controller.ScheduleSetup(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(SettingsController.ScheduleSetup), redirect.ActionName);

        var savedTemplates = await db.ScheduleShiftTemplates.ToListAsync();
        Assert.Single(savedTemplates);
        Assert.Equal("#3b82f6", savedTemplates[0].ColorHex);
    }
}
