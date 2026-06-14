using System;
using BenchmarkDotNet.Running;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Wiretap.Api.Util;
using Wiretap.Core;
using Wiretap.Meta;
using Wiretap.Util;
using Wiretap.Util.Buzz;

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

var listener = ActivityCast.Listen();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var loopCount = args.Contains("--benchmark", StringComparer.OrdinalIgnoreCase) ? 10_000 : 1;

    using var _ = Wiretap.Util.Configuration.Push(loggerFactory, c => c with { AttachTraceContext = true, ComposeMessage = new ComposeMessageByAppending() });
    using (var bulk = logger.BeginBulk(new Wires.DeleteFolder { Path = "batch" }))
    {
        for (var i = 0; i < loopCount; i++)
        {
            logger.LogSnap(new Wires.ValidateRecord { RecordId = "record-001" }, new Wires.ValidateRecord.Okay());

            using (var item = bulk.BeginItem(new Wires.DeleteFile { Path = "batch/a.txt" }))
            {
                item.SetStatus(new Wires.DeleteFile.Okay());
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

        bulk.SetStatus(new Wires.DeleteFolder.Okay(), "This is the end of this bulk delete.");
        //step.SetStatus(new Contracts.DeleteFile.Force.Ok("test.txt")); // note: Not assignable! Check!
    }

    using (var step = logger.BeginBuzz(new Wires.Workflow.ExecuteStep.Now { StepIndex = 2 }))
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

    logger.LogSnap(
        new QuickSnap("InspectCache", "Key: {CacheKey}", "users:active"),
        new QuickSnap.Okay("Entries: {EntryCount}", 42)
    );

    using (var quick = logger.BeginBuzz(new QuickBuzz("WarmIndex", "Index: {IndexName}", "documents")))
    {
        quick.SetStatus(new QuickBuzz.Okay("Segments: {SegmentCount}", 7));
    }

    using (var quickBulk = logger.BeginBulk(new QuickBulk("ImportRows", "Source: {Source}", "rows.csv")))
    {
        using (var item = quickBulk.BeginItem(new QuickBuzz("ImportRow", "Row: {RowNumber}", 1)))
        {
            item.SetStatus(new QuickBuzz.Okay("Record: {RecordId}", "A-001"));
        }

        using (var item = quickBulk.BeginItem(new QuickBuzz("ImportRow", "Row: {RowNumber}", 2)))
        {
            item.SetStatus(new QuickBuzz.Noop("Skipped duplicate record."));
        }

        quickBulk.SetStatus(new QuickBulk.Okay("Imported quick rows."));
    }
}

app.MapGet("/", () => "Hello Wiretap!");

app.Run();