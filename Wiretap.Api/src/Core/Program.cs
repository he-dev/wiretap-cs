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

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    using (var step = logger.Begin(new Output.Workflow.ExecuteStep.Now { StepIndex = 1 }))
    {
        logger.Output.Clue.LogInformation("This step has a note.");
        step.LogStatus(new Output.Workflow.ExecuteStep.Now.Okay { ItemsProcessed = 100 });
        //step.LogStatus(new Contracts.DeleteFile.Force.Ok("test.txt")); // note: Not assignable! Check!
    }

    using (var step = logger.Begin(new Output.Workflow.ExecuteStep.Now { StepIndex = 2 }))
    {
        // busy...
    }

    logger.Engine.News.LogDebug("This is a log text.");
    //logger.LogStatus(new Engine.DeleteFile.Ok("test.txt"));
    //logger.Engine.LogStatus(new Contracts.Workflow.ExecuteStep.Now.Ok(7)); // note: Not assignable! Check!

    using (logger.Begin(new Engine.CopyFile { Path = "test.txt" }))
    {
        // busy...
    }

    logger.Engine.Data.LogDebug("Logged without explicit last status.");
    logger.Engine.News.LogDebug("Logged without explicit last status.");
    logger.Engine.Clue.LogDebug("Logged without explicit last status.");

    using (var delete = logger.Begin(new Engine.DeleteFile { Path = "test.txt" }))
    {
        delete.LogStatus(new Engine.DeleteFile.Halt { Reason = "File not found." });
        delete.LogStatus(new Engine.DeleteFile.Fail());
        delete.LogDebug("Logged at busy status.");
        delete.LogTrace("Logged at busy status.");
        delete.LogStatus(new Engine.DeleteFile.Okay());
    }
}

app.MapGet("/", () => "Hello Wiretap!");

app.Run();