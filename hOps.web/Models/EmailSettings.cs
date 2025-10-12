namespace hOps.web.Models
{
    public class EmailSettings
    {
        public string SenderEmail { get; set; } = "";
        public string Password { get; set; } = "";
        public string SmtpServer { get; set; } = "smtp.gmail.com";
        public int Port { get; set; } = 587;
        public bool UseSSL { get; set; } = true;
    }
}
