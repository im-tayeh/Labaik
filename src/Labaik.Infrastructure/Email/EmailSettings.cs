namespace Labaik.Infrastructure.Email;

public sealed class EmailSettings
{
    public const string SectionName = "Email";

    public string Provider { get; init; } = "Logging"; // Logging | Smtp | Brevo
    public string SenderName { get; init; } = "Labaik";
    public string SenderEmail { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;

    // SMTP (kept for fallback)
    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 587;
    public string SmtpUser { get; init; } = string.Empty;
    public string SmtpPassword { get; init; } = string.Empty;
}