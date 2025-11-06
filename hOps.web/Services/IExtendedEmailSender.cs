using System.Collections.Generic;
using System.Threading.Tasks;
using hOps.web.Models;

namespace hOps.web.Services
{
    public interface IExtendedEmailSender
    {
        Task SendEmailAsync(string email, string subject, string htmlMessage, IEnumerable<EmailAttachment>? attachments);
    }
}
