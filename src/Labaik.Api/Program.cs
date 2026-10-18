using Labaik.Api.Endpoints;
using Labaik.Api.Extensions;
using Labaik.Api.Handlers;
using Labaik.Api.Realtime;
using Labaik.Api.Services;
using Labaik.Application;
using Labaik.Application.Common.Interfaces;
using Labaik.Infrastructure;
using Labaik.Infrastructure.Authentication;
using Labaik.Infrastructure.Persistence;
using Labaik.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;


// Stage 1: a "bootstrap" logger — captures anything that fails during startup,
// before the full configuration is loaded.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Render (and most PaaS) inject the port to listen on via the PORT env var.
    var port = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrEmpty(port))
    {
        builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
    }

    // Stage 2: the real logger, configured from appsettings.json + DI services.
    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    // Standardized RFC 9457 error responses, enriched with a correlation id.
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Instance =
                $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}";
            context.ProblemDetails.Extensions["traceId"] =
                Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
        };
    });

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApiVersioningConfiguration();

    var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                RoleClaimType = ClaimTypes.Role,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey))
            };

            // WebSockets cannot send an Authorization header: SignalR clients pass the JWT as ?access_token=.
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var token = context.Request.Query["access_token"];
                    if (!string.IsNullOrEmpty(token) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    {
                        context.Token = token;
                    }

                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAppAuthorization();

    builder.Services.AddExceptionHandler<ValidationExceptionHandler>(); // specific first
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();     // catch-all second


    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();

    builder.Services.AddSignalR();
    builder.Services.AddScoped<ILocationBroadcaster, SignalRLocationBroadcaster>();
    builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
    builder.Services.AddSingleton<IUserIdProvider, SubClaimUserIdProvider>();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();

        var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Seed");
        await RitualStepSeeder.SeedAsync(dbContext, seedLogger);
        await GuideArticleSeeder.SeedAsync(dbContext, seedLogger);
        await MapPlaceSeeder.SeedAsync(dbContext, seedLogger);
        await RoleSeeder.SeedAsync(scope.ServiceProvider, seedLogger);
    }

    // One tidy structured line per HTTP request (method, path, status, elapsed ms).
    app.UseSerilogRequestLogging();

    app.UseExceptionHandler();   // catches unhandled exceptions -> ProblemDetails
    app.UseStatusCodePages();    // turns bare 404/401 into ProblemDetails too


    // Always in Development; elsewhere opt-in (env OpenApi__Enabled=true) so the dashboard team can
    // read /openapi/v1.json and Scalar from a deployed instance. Endpoints stay protected by auth.
    if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("OpenApi:Enabled"))
    {
        app.MapOpenApi().WithDocumentPerVersion();
        app.MapScalarApiReference();
    }

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapAuthEndpoints();
    app.MapJourneyEndpoints();
    app.MapGroupEndpoints();
    app.MapRitualEndpoints();
    app.MapHomeEndpoints();
    app.MapGuideEndpoints();
    app.MapAnnouncementEndpoints();
    app.MapLocationEndpoints();
    app.MapNotificationEndpoints();
    app.MapAdminEndpoints();
    app.MapCompanyEndpoints();
    app.MapProfileEndpoints();
    app.MapSupervisorEndpoints();
    app.MapTripEndpoints();
    app.MapMapEndpoints();
    app.MapHelpCenterEndpoints();
    app.MapFamilyCircleEndpoints();
    app.MapHub<LocationHub>(LocationHub.Path);
    app.MapHub<EventsHub>(EventsHub.Path);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
public partial class Program; // exposes the entry point to WebApplicationFactory in integration tests
