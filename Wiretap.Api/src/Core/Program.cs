using System;
using Aion.Meta.Logging.Wiretap;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

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

    using (var scope = logger.BeginScopeWith(("loopCount", loopCount)))
    {
        scope.System.Zero.LogInformation("Hello Wiretap!");
    }
}

//app.MapGet("/", () => "Hello Wiretap!");

//app.Run();