using System;
using Aion.Meta.Logging.Wiretap;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Wiretap.Core;

var builder = WebApplication.CreateBuilder(args);

builder
    .Configuration
    .AddJsonFile("appsettings.Serilog.json", optional: true, reloadOnChange: true)
    .AddCommandLine(args);

builder
    .Logging
    .ClearProviders();

builder
    .Services
    .AddSerilog((services, configuration) =>
        configuration
            .ReadFrom.Services(services)
            .ReadFrom.Configuration(builder.Configuration)
    );

var app = builder.Build();

using (var services = app.Services.CreateScope())
{
    var logger = services.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var loggerFactory = services.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var loopCount = 1;

    using (var scope = logger.BeginScopeAbout("example", [("loopCount", loopCount)]).LogEnvelope(EnvelopeMode.LastOnly))
    {
        scope.Zero.System.LogInformation("Hello Wiretap!");

        var stats = new Benchmark();
        foreach (var found in new[] { true, false, true })
        {
            // core: The sample is recorded when the using block exits.
            using var sample = stats.Measure();
            System.Threading.Thread.Sleep(10); // Simulate the work being measured.
            sample.CountAs(found ? "found" : "not-found");
        }

        scope.Tick.System.LogInformation(
            "Processed {FilesTotal} files at {FilesPerSecond:F2}/s; found {FilesFound}, not found {FilesNotFound}, found rate {FoundRate:P0}.",
            stats.Total.Count,
            stats.Total.ThroughputMs * 1000,
            stats["found"].Count,
            stats["not-found"].Count,
            stats.Ratio("found"));
    }
}

//app.MapGet("/", () => "Hello Wiretap!");

//app.Run();