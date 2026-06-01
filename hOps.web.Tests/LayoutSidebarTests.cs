using System;
using System.IO;
using Xunit;

namespace hOps.web.Tests
{
    public sealed class LayoutSidebarTests
    {
        [Fact]
        public void SidebarMenuLabelsUseUnencodedAmpersands()
        {
            var layoutPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "..",
                "..",
                "..",
                "hOps.web",
                "Views",
                "Shared",
                "_Layout.cshtml"));

            Assert.True(File.Exists(layoutPath), $"Expected layout view at {layoutPath}.");

            var content = File.ReadAllText(layoutPath);

            Assert.Contains("@Html.Localize(\"Lost & Found\")", content);
            Assert.Contains("@Html.Localize(\"Package & Mail Log\")", content);
            Assert.Contains("@Html.Localize(\"Your assignments & reminders\")", content);

            Assert.DoesNotContain("@Html.Localize(\"Lost &amp; Found\")", content);
            Assert.DoesNotContain("@Html.Localize(\"Package &amp; Mail Log\")", content);
            Assert.DoesNotContain("@Html.Localize(\"Your assignments &amp; reminders\")", content);
        }
    }
}
