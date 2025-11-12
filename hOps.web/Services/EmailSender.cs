using System;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using hOps.web.Models;
using hOps.web.Utilities;

namespace hOps.web.Services
{
    public class EmailSender : IEmailSender, IExtendedEmailSender
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
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("EmailSender: attempted to send email without a recipient address");
                return;
            }

            await SendEmailAsync(email, subject, htmlMessage, attachments: null);
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage, IEnumerable<EmailAttachment>? attachments)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.LogWarning("EmailSender: attempted to send email without a recipient address");
                return;
            }

            var smtpHost = _config["EmailSettings:SMTPHost"];
            var smtpPortValue = _config["EmailSettings:SMTPPort"];
            var smtpUser = _config["EmailSettings:SMTPUser"];
            var smtpPass = _config["EmailSettings:SMTPPass"];
            var fromEmail = _config["EmailSettings:FromEmail"];
            var fromName = _config["EmailSettings:FromName"];
            var enableSsl = _config.GetValue("EmailSettings:EnableSsl", true);

            if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogError("EmailSender: SMTP host or from email configuration is missing.");
                return;
            }

            if (!int.TryParse(smtpPortValue, out var smtpPort))
            {
                smtpPort = 587;
            }

            using var client = new SmtpClient(smtpHost, smtpPort)
            {
                EnableSsl = enableSsl
            };

            if (!string.IsNullOrWhiteSpace(smtpUser))
            {
                client.Credentials = new NetworkCredential(smtpUser, smtpPass ?? string.Empty);
            }
            else
            {
                client.UseDefaultCredentials = true;
            }

            if (SensitiveContentGuard.ContainsSensitiveData(htmlMessage))
            {
                _logger.LogWarning("EmailSender: blocked email to {Email} because body contained restricted content.", email);
                return;
            }

            var safeBody = SensitiveContentGuard.Sanitize(htmlMessage);

            using var mail = new MailMessage
            {
                From = new MailAddress(fromEmail, string.IsNullOrWhiteSpace(fromName) ? fromEmail : fromName),
                Subject = subject ?? string.Empty,
                Body = safeBody,
                IsBodyHtml = true
            };

            mail.To.Add(new MailAddress(email));

            if (attachments != null)
            {
                foreach (var attachment in attachments)
                {
                    if (attachment == null || attachment.Content.Length == 0)
                    {
                        continue;
                    }

                    var stream = new MemoryStream(attachment.Content);
                    var mailAttachment = new Attachment(stream, attachment.ContentType)
                    {
                        Name = attachment.FileName
                    };
                    if (mailAttachment.ContentDisposition != null)
                    {
                        mailAttachment.ContentDisposition.FileName = attachment.FileName;
                    }
                    mail.Attachments.Add(mailAttachment);
            }
            }

            try
            {
                await client.SendMailAsync(mail);
            }
            catch (SmtpException ex)
            {
                _logger.LogError(ex, "Failed to send email to {Email}", email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while sending email to {Email}", email);
            }
        }
    }
}

