using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging;
using Serilog;
using Wiretap.Core;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Wiretap.Api.Util;

[MemoryDiagnoser]
public class Benchmarks
{
    private ILoggerFactory _factory = null!;
    private CountingLoggerProvider _provider = null!;
    private ILogger<Benchmarks> _logger = null!;
    private string _logFilePath = null!;

    // [GlobalSetup]
    // public void Setup()
    // {
    //     _provider = new CountingLoggerProvider();
    //     _factory = LoggerFactory.Create(builder =>
    //     {
    //         builder.ClearProviders();
    //         builder.SetMinimumLevel(LogLevel.Trace);
    //         builder.AddProvider(_provider);
    //     });
    //
    //     _logger = _factory.CreateLogger<Benchmarks>();
    // }
    //
    // [GlobalCleanup]
    // public void Cleanup()
    // {
    //     _factory.Dispose();
    // }

    [GlobalSetup]
    public void Setup()
    {
        _logFilePath = Path.Combine(Path.GetTempPath(), $"wiretap-bench-{Guid.NewGuid():N}.log");

        var serilog = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(
                path: _logFilePath,
                rollingInterval: RollingInterval.Infinite,
                buffered: true,
                shared: false,
                outputTemplate: "{Timestamp:O} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .CreateLogger();

        _factory = LoggerFactory.Create(builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddSerilog(serilog, dispose: true);
        });

        _logger = _factory.CreateLogger<Benchmarks>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _factory.Dispose();

        if (File.Exists(_logFilePath))
        {
            File.Delete(_logFilePath);
        }
    }

    [Benchmark(Baseline = true)]
    public void Plain_TwoStatus_Okay()
    {
        var activity = "Workflow.ExecuteStep.Now";
        var stepIndex = 1;

        var stateZero = new List<KeyValuePair<string, object?>>
        {
            new("Activity", activity),
            new("ActivityRole", "Core"),
            new("Status", "Zero"),
            new("MessageRole", "Data"),
            new("ElapsedMs", 0L),
            new("StepIndex", stepIndex)
        };

        using (_logger.BeginScope(stateZero))
        {
            _logger.LogTrace(
                "ActivityRole: {ActivityRole}; Activity: {Activity}[{Status}]; Elapsed: {ElapsedMs} ms",
                "Core", activity, "Zero", 0L);
        }

        var stateOkay = new List<KeyValuePair<string, object?>>
        {
            new("Activity", activity),
            new("ActivityRole", "Core"),
            new("Status", "Okay"),
            new("MessageRole", "Data"),
            new("ElapsedMs", 0L),
            new("StepIndex", stepIndex),
            new("ItemsProcessed", 100)
        };

        using (_logger.BeginScope(stateOkay))
        {
            _logger.LogInformation(
                "ActivityRole: {ActivityRole}; Activity: {Activity}[{Status}]; Elapsed: {ElapsedMs} ms; ItemsProcessed: {ItemsProcessed}",
                "Core", activity, "Okay", 0L, 100);
        }
    }

    [Benchmark]
    public void Plain_TwoStatus_VoidFallback()
    {
        var activity = "CopyFile";

        var stateZero = new List<KeyValuePair<string, object?>>
        {
            new("Activity", activity),
            new("ActivityRole", "Buzz"),
            new("Status", "Zero"),
            new("MessageRole", "Data"),
            new("ElapsedMs", 0L),
            new("Path", "test.txt"),
            new("CustomItem", "CustomValue")
        };

        using (_logger.BeginScope(stateZero))
        {
            _logger.LogTrace(
                "ActivityRole: {ActivityRole}; Activity: {Activity}[{Status}]; Elapsed: {ElapsedMs} ms",
                "Buzz", activity, "Zero", 0L);
        }

        var stateVoid = new List<KeyValuePair<string, object?>>
        {
            new("Activity", activity),
            new("ActivityRole", "Buzz"),
            new("Status", "Void"),
            new("MessageRole", "Data"),
            new("ElapsedMs", 0L),
            new("Path", "test.txt"),
            new("CustomItem", "CustomValue")
        };

        using (_logger.BeginScope(stateVoid))
        {
            _logger.LogInformation(
                "ActivityRole: {ActivityRole}; Activity: {Activity}[{Status}]; Elapsed: {ElapsedMs} ms; {Message}",
                "Buzz", activity, "Void", 0L,
                "CanBeVoid policy is set; it allows omitting an explicit last status.");
        }
    }

    [Benchmark]
    public void Wiretap_Okay()
    {
        using var step = _logger.Begin(new Wires.Workflow.ExecuteStep.Now { StepIndex = 1 });
        step.LogStatus(new Wires.Workflow.ExecuteStep.Now.Okay { ItemsProcessed = 100 });
    }

    [Benchmark]
    public void Wiretap_VoidFallback()
    {
        using var _ = _logger.Begin(new Wires.CopyFile { Path = "test.txt" });
    }
}

public sealed class CountingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();
    private long _checksum;

    public ILogger CreateLogger(string categoryName) => new CountingLogger(this);

    public void Dispose() { }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    private sealed class CountingLogger(CountingLoggerProvider owner) : ILogger
    {
        public IDisposable BeginScope<TState>(TState state) where TState : notnull => owner._scopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>
        (
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            var score = 0;

            var message = formatter(state, exception);
            score += message.Length;
            score += (int)logLevel;
            score += eventId.Id;

            if (state is IEnumerable<KeyValuePair<string, object?>> kvps)
            {
                foreach (var kv in kvps)
                {
                    score += kv.Key.Length;
                    score += kv.Value?.GetHashCode() ?? 0;
                }
            }

            owner._scopeProvider.ForEachScope((scopeObj, acc) =>
            {
                if (scopeObj is IEnumerable scopeEnumerable)
                {
                    foreach (var item in scopeEnumerable)
                    {
                        acc += item?.GetHashCode() ?? 0;
                    }
                }
                else
                {
                    acc += scopeObj?.GetHashCode() ?? 0;
                }
            }, score);

            Interlocked.Add(ref owner._checksum, score);
        }
    }
}