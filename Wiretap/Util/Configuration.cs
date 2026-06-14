using Microsoft.Extensions.Logging;
using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public record Configuration
{
    public ILogger Logger { get; init; } = null!;

    public bool AttachTraceContext { get; init; }

    public PropertyName PropertyName { get; init; } = new(Parts: ["wiretap"]);

    public IComposeMessage ComposeMessage { get; init; } = new ComposeMessageByAppending();

    public static Configuration Current => AmbientContext<Configuration>.Current ?? new();

    public static IDisposable Push(ILoggerFactory loggerFactory, Func<Configuration, Configuration>? configure = null)
    {
        var configuration = new Configuration { Logger = loggerFactory.CreateLogger("Wiretap") };
        return AmbientContext<Configuration>.Push(configure is null ? configuration : configure(configuration));
    }
}