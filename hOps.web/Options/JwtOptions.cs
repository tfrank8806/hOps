namespace hOps.web.Options
{
    public class JwtOptions
    {
        public string Issuer { get; set; } = "GuestQuest-hOps";
        public string Audience { get; set; } = "GuestQuest-hOps-Mobile";
        public string SigningKey { get; set; } = "change-me-please-GuestQuest-hOps-mobile";
        public int AccessTokenMinutes { get; set; } = 120;
    }
}
