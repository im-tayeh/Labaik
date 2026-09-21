using Labaik.Api.Handlers;
using Scalar.AspNetCore;
using Serilog;
using System.Diagnostics;

// Stage 1: a "bootstrap" logger — captures anything that fails during startup,
// before the full configuration is loaded.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

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

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    builder.Services.AddOpenApi();

    var app = builder.Build();

    // One tidy structured line per HTTP request (method, path, status, elapsed ms).
    app.UseSerilogRequestLogging();

    app.UseExceptionHandler();   // catches unhandled exceptions -> ProblemDetails
    app.UseStatusCodePages();    // turns bare 404/401 into ProblemDetails too


    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.UseHttpsRedirection();

    app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
       .WithTags("System")
       .WithSummary("Liveness check");

    app.MapGet("/version", () => Results.Ok(new
    {
        service = "Labaik.Api",
        version = "1.0.0",
        environment = app.Environment.EnvironmentName
    }))
       .WithTags("System")
       .WithSummary("Service version and environment");

    // Temporary — remove after verifying the error pipeline.
    app.MapGet("/error-test", () =>
    {
        throw new InvalidOperationException("Test unhandled exception");
    });


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