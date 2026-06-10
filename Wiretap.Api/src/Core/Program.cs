using System;
using System.Diagnostics;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Wiretap.Api.Util;
using Wiretap.Core;
using Wiretap.Meta;

if (args.Contains("--benchmark", StringComparer.OrdinalIgnoreCase))
{
    BenchmarkRunner.Run<Benchmarks>();
    return;
}

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

var listener = CreateActivityListener.Default();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var loopCount = args.Contains("--benchmark", StringComparer.OrdinalIgnoreCase) ? 10_000 : 1 ;

    for (var i = 0; i < loopCount; i++)
    {
        using (var step = logger.Begin(new Wires.Workflow.ExecuteStep.Now { StepIndex = 1 }))
        {
            logger.Note.LogInformation("This step has a note.");
            step.SetStatus(new Wires.Workflow.ExecuteStep.Now.Okay { ItemsProcessed = 100 }, "This is the end of this step.");
            //step.SetStatus(new Contracts.DeleteFile.Force.Ok("test.txt")); // note: Not assignable! Check!
        }

        using (var step = logger.Begin(new Wires.Workflow.ExecuteStep.Now { StepIndex = 2 }))
        {
            // busy...
        }

        logger.Echo.LogDebug("This is a log text.");
        //logger.SetStatus(new Engine.DeleteFile.Ok("test.txt"));
        //logger.Engine.SetStatus(new Contracts.Workflow.ExecuteStep.Now.Ok(7)); // note: Not assignable! Check!

        using (logger.Begin(new Wires.CopyFile { Path = "test.txt" }))
        {
            // busy...
        }

        logger.Data.LogDebug("Logged without explicit last status.");
        logger.Echo.LogDebug("Logged without explicit last status.");
        logger.Note.LogDebug("Logged without explicit last status.");

        using (var delete = logger.Begin(new Wires.DeleteFile { Path = "test.txt" }))
        {
            delete.SetStatus(new Wires.DeleteFile.Halt { Reason = "File not found." });
            delete.SetStatus(new Wires.DeleteFile.Halt());
            delete.SetStatus(new Wires.DeleteFile.Fail());
            delete.LogDebug("Logged at busy status.");
            delete.LogTrace("Logged at busy status.");
            delete.SetStatus(new Wires.DeleteFile.Okay());
            delete.SetStatus(new Wires.DeleteFile.Halt.NotFound());
        }
    }
}

app.MapGet("/", () => "Hello Wiretap!");

app.Run();
