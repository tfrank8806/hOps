namespace hOps.web.Options
{
    /// <summary>
    /// Configuration values for Google reCAPTCHA.
    /// </summary>
    public class CaptchaOptions
    {
        public bool Enabled { get; set; } = false;
        public string SiteKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
    }
}
