using System.Net.Http.Json;
using Labaik.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Labaik.Infrastructure.Email;

internal sealed class BrevoEmailSender(
    HttpClient httpClient,
    IOptions<EmailSettings> options,
    ILogger<BrevoEmailSender> logger) : IEmailSender
{
    private readonly EmailSettings _settings = options.Value;

    public async Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            sender = new { name = _settings.SenderName, email = _settings.SenderEmail },
            to = new[] { new { email = to } },
            subject,
            textContent = body
        };

        using var response = await httpClient.PostAsJsonAsync("v3/smtp/email", payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Brevo email send failed ({Status}): {Error}", (int)response.StatusCode, error);
            throw new InvalidOperationException($"Email provider returned status {(int)response.StatusCode}.");
        }

        logger.LogInformation("Email sent to {To} via Brevo.", to);
    }
}