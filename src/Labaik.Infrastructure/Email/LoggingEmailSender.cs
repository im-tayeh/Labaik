using Labaik.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Labaik.Infrastructure.Email;

internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "DEV EMAIL -> To: {To} | Subject: {Subject} | Body: {Body}",
            to, subject, body);

        return Task.CompletedTask;
    }
}