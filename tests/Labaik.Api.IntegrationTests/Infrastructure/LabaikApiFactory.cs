using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Labaik.Application.Common.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace Labaik.Api.IntegrationTests.Infrastructure;

// One PostgreSQL container and one app host for the whole test run: the Serilog bootstrap logger
// can only be frozen once per process, and startup (migrations + seeding) is the slow part.
public sealed class LabaikApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminEmail = "admin@labaik.test";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17") // same image as docker-compose
        .WithDatabase("labaik")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public CapturingEmailSender Emails { get; } = new();

    public async Task InitializeAsync()
    {
        await _db.StartAsync();
        _ = Server; // start the host now: runs migrations and seeders once
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _db.GetConnectionString());
        builder.UseSetting("jwt:SigningKey", "integration-tests-signing-key-at-least-32-bytes-long!");
        builder.UseSetting("Email:Provider", "Logging");
        builder.UseSetting("Admin:Emails:0", AdminEmail);
        builder.UseSetting("OpenApi:Enabled", "true");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
        });
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<LabaikApiFactory>
{
    public const string Name = "api";
}

// Captures outgoing emails so tests can read the OTP.
public sealed partial class CapturingEmailSender : IEmailSender
{
    private readonly ConcurrentDictionary<string, string> _lastBodyByRecipient = new(StringComparer.OrdinalIgnoreCase);

    public Task SendAsync(string to, string subject, string body, CancellationToken cancellationToken = default)
    {
        _lastBodyByRecipient[to] = body;
        return Task.CompletedTask;
    }

    public string LastCodeFor(string email)
    {
        var body = _lastBodyByRecipient.TryGetValue(email, out var b)
            ? b
            : throw new InvalidOperationException($"No email was sent to {email}.");
        return SixDigits().Match(body) is { Success: true } m
            ? m.Value
            : throw new InvalidOperationException($"No code in the email to {email}.");
    }

    public string LastBodyFor(string email) => _lastBodyByRecipient.TryGetValue(email, out var b)
        ? b
        : throw new InvalidOperationException($"No email was sent to {email}.");

    // Temporary password from a provisioning email (null when the email carried none).
    public string? TemporaryPasswordFor(string email) =>
        TemporaryPassword().Match(LastBodyFor(email)) is { Success: true } m ? m.Groups[1].Value : null;

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigits();

    [GeneratedRegex(@"Temporary password: (\S+)")]
    private static partial Regex TemporaryPassword();
}

internal static class ServiceCollectionExtensions
{
    public static void RemoveAll<T>(this IServiceCollection services)
    {
        foreach (var d in services.Where(d => d.ServiceType == typeof(T)).ToList())
        {
            services.Remove(d);
        }
    }
}
