using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Wires;
using Wiretap.Core;
using Wiretap.Util;

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

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var loopCount = 1;

    Configuration.Default = new Configuration
    {
        DiagnosticLogger = DiagnosticLogger.Create(loggerFactory)
    };

    using var _ = Wiretap.Util.Configuration.Default.TraceContext.Listen();
    using (var bulk = logger.BeginBulk(new Wires.DeleteFolder { Path = "batch" }))
    {
        for (var i = 0; i < loopCount; i++)
        {
            //logger.LogStatus(new Wires.ValidateRecord { RecordId = "record-001" }, new Wires.ValidateRecord.Okay());

            using (var item = bulk.BeginItem(new Wires.DeleteFile { Path = "batch/a.txt" }))
            {
                item.SetStatus(new Wires.DeleteFile.Okay());
                // core: This should not work!
                //item.SetStatus(new Wires.DeleteFolder.Okay());
            }

            using (var item = bulk.BeginItem(new Wires.DeleteFile { Path = "batch/missing.txt" }))
            {
                item.SetStatus(new Wires.DeleteFile.Noop.NotFound());
            }

            using (var item = bulk.BeginItem(new Wires.DeleteFile { Path = "batch/error.txt" }))
            {
                item.SetStatus(new Wires.DeleteFile.Fail());
            }
        }

        // core: This should not work!
        //bulk.SetStatus(new Wires.DeleteFile.Okay());
        bulk.SetStatus(new Wires.DeleteFolder.Okay());

        //step.SetStatus(new Contracts.DeleteFile.Force.Ok("test.txt")); // note: Not assignable! Check!
    }

    using (var step = logger.BeginBuzz(new Wires.Workflow.ExecuteStep { StepIndex = 2 }))
    {
        // work...
    }

    //logger.SetStatus(new Engine.DeleteFile.Ok("test.txt"));
    //logger.Engine.SetStatus(new Contracts.Workflow.ExecuteStep.Now.Ok(7)); // note: Not assignable! Check!

    using (logger.BeginBuzz(new Wires.CopyFile { Path = "test.txt" }))
    {
        // work...
    }


    using (var delete = logger.BeginBuzz(new Wires.DeleteFile { Path = "test.txt" }))
    {
        delete.SetStatus(new Wires.DeleteFile.Noop { Reason = "File not found." });
        delete.SetStatus(new Wires.DeleteFile.Noop());
        delete.SetStatus(new Wires.DeleteFile.Fail());
        delete.SetStatus(new Wires.DeleteFile.Okay());
        delete.SetStatus(new Wires.DeleteFile.Noop.NotFound());
    }

    using (var quick = logger.BeginBuzz(new Buzz("WarmIndex")))
    {
        quick.SetStatus(new Status.Okay());
    }

    using (var quickBulk = logger.BeginBulk(new Buzz.Bulk("ImportRows")))
    {
        using (var item = quickBulk.BeginItem(new Buzz("ImportRow")))
        {
            item.SetStatus(new Status.Okay());
        }

        using (var item = quickBulk.BeginItem(new Buzz("ImportRow")))
        {
            item.SetStatus(new Status.Noop());
        }

        quickBulk.SetStatus(new Status.Okay());
    }
}

app.MapGet("/", () => "Hello Wiretap!");

app.Run();
