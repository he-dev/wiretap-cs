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
    using (var batch = logger.BeginBatch(new Wires.DeleteFolder { Path = "batch" }))
    {
        for (var i = 0; i < loopCount; i++)
        {
            //logger.LogStatus(new Wires.ValidateRecord { RecordId = "record-001" }, new Wires.ValidateRecord.Okay());

            using (var item = batch.BeginWatch(new Wires.DeleteFile { Path = "batch/a.txt" }))
            {
                item.LogEvent(new Wires.DeleteFile.Okay());
                // core: This should not work!
                //item.LogEvent(new Wires.DeleteFolder.Okay());
            }

            using (var item = batch.BeginWatch(new Wires.DeleteFile { Path = "batch/missing.txt" }))
            {
                item.LogEvent(new Wires.DeleteFile.Noop.NotFound());
            }

            using (var item = batch.BeginWatch(new Wires.DeleteFile { Path = "batch/error.txt" }))
            {
                item.LogEvent(new Wires.DeleteFile.Fail());
            }
        }

        // core: This should not work!
        //bulk.SetStatus(new Wires.DeleteFile.Okay());
        batch.LogEvent(new Wires.DeleteFolder.Okay());

        //step.SetStatus(new Contracts.DeleteFile.Force.Ok("test.txt")); // note: Not assignable! Check!
    }

    using (var step = logger.BeginWatch(new Wires.Workflow.ExecuteStep { StepIndex = 2 }))
    {
        // work...
    }

    //logger.SetStatus(new Engine.DeleteFile.Ok("test.txt"));
    //logger.Engine.SetStatus(new Contracts.Workflow.ExecuteStep.Now.Ok(7)); // note: Not assignable! Check!

    using (logger.BeginWatch(new Wires.CopyFile { Path = "test.txt" }))
    {
        // work...
    }


    using (var delete = logger.BeginWatch(new Wires.DeleteFile { Path = "test.txt" }))
    {
        delete.LogEvent(new Wires.DeleteFile.Noop { Reason = "File not found." });
        delete.LogEvent(new Wires.DeleteFile.Noop());
        delete.LogEvent(new Wires.DeleteFile.Fail());
        delete.LogEvent(new Wires.DeleteFile.Okay());
        delete.LogEvent(new Wires.DeleteFile.Noop.NotFound());
    }

    using (var quick = logger.BeginWatch(new WatchLite("WarmIndex", "Index: {IndexName}", "documents")))
    {
        quick.LogEvent(new WatchLite.Okay("Segments: {SegmentCount}", 7));
    }

    using (var quickBulk = logger.BeginBatch(new BatchLite("ImportRows", "Source: {Source}", "rows.csv")))
    {
        using (var item = quickBulk.BeginWatch(new BulkLiteItem("ImportRow", "Row: {RowNumber}", 1)))
        {
            item.LogEvent(new BulkLiteItem.Okay("Record: {RecordId}", "A-001"));
        }

        using (var item = quickBulk.BeginWatch(new BulkLiteItem("ImportRow", "Row: {RowNumber}", 2)))
        {
            item.LogEvent(new BulkLiteItem.Noop("Skipped duplicate record."));
        }

        quickBulk.LogEvent(new BatchLite.Okay("Imported quick rows."));
    }
}

app.MapGet("/", () => "Hello Wiretap!");

app.Run();
