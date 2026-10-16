using Labaik.Application.Common.Interfaces;
using Labaik.Infrastructure.Authentication;
using Labaik.Infrastructure.Email;
using Labaik.Infrastructure.Groups;
using Labaik.Infrastructure.Identity;
using Labaik.Infrastructure.Persistence;
using Labaik.Infrastructure.Persistence.Interceptors;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;


namespace Labaik.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        services.AddScoped<DomainEventDispatchInterceptor>();

        services.AddDbContext<AppDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString);
            options.AddInterceptors(sp.GetRequiredService<DomainEventDispatchInterceptor>());
        });

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddDataProtection();

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
            options.SignIn.RequireConfirmedEmail = true;
        })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IVerificationCodeService, VerificationCodeService>();

        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));

        var emailProvider = configuration.GetSection(EmailSettings.SectionName)["Provider"];
        if (string.Equals(emailProvider, "Brevo", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<IEmailSender, BrevoEmailSender>((sp, client) =>
            {
                var settings = sp.GetRequiredService<IOptions<EmailSettings>>().Value;
                client.BaseAddress = new Uri("https://api.brevo.com/");
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.Add("api-key", settings.ApiKey);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            });
        }
        else if (string.Equals(emailProvider, "Smtp", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            services.AddScoped<IEmailSender, LoggingEmailSender>();
        }

        services.AddSingleton(TimeProvider.System);

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName));

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddOptions<GoogleAuthSettings>()
            .Bind(configuration.GetSection(GoogleAuthSettings.SectionName));
        services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();

        services.AddScoped<IRefreshTokenService, RefreshTokenService>();

        services.AddScoped<IJoinCodeGenerator, JoinCodeGenerator>();
        services.AddScoped<IUserDirectory, UserDirectory>();

        return services;
    }
}