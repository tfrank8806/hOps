namespace hOps.web.ViewModels
{
    public class UserAvatarViewModel
    {
        public string? ImageUrl { get; set; }
        public string Initials { get; set; } = "UU";
        public string? Name { get; set; }
        public string Size { get; set; } = "md";
        public string AdditionalCssClasses { get; set; } = string.Empty;
    }
}
