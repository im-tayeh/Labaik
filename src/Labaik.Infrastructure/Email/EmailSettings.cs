namespace Labaik.Infrastructure.Email;

public sealed class EmailSettings
{
    public const string SectionName = "Email";

    public string Provider { get; init; } = "Logging";
    public string SenderName { get; init; } = "Labaik";
    public string SenderEmail { get; init; } = string.Empty;
    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 587;
    public string SmtpUser { get; init; } = string.Empty;
    public string SmtpPassword { get; init; } = string.Empty;
}