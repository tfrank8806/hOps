using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace hOps.web.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailSender> _logger;

        public EmailSender(IConfiguration config, ILogger<EmailSender> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var smtpHost = _config["EmailSettings:SMTPHost"];
            var smtpPort = int.Parse(_config["EmailSettings:SMTPPort"] ?? "587");
            var smtpUser = _config["EmailSettings:SMTPUser"];
            var smtpPass = _config["EmailSettings:SMTPPass"];
            var fromEmail = _config["EmailSettings:FromEmail"];
            var fromName = _config["EmailSettings:FromName"];

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(smtpUser, smtpPass)
            };

            if (string.IsNullOrEmpty(fromEmail) || string.IsNullOrEmpty(email))
            {
                _logger.LogError("EmailSender: fromEmail or email is null/empty");
                return;
            }
            var mailAddr = new MailAddress(email, fromName);

            var mail = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            mail.To.Add(email);

            try
            {
                await client.SendMailAsync(mail);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", email);
            }
        }
    }
}
