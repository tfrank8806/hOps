using System.Threading;
using System.Threading.Tasks;

namespace hOps.web.Services
{
    public interface ICaptchaValidator
    {
        Task<bool> ValidateAsync(string token, string? remoteIp, CancellationToken cancellationToken);
    }
}
