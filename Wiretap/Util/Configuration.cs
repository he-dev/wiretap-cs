using Wiretap.Meta;
using Wiretap.Util.Buzz;

namespace Wiretap.Util;

public record Configuration
{
    public bool AttachTraceContext { get; init; }

    public PropertyName PropertyName { get; init; } = new(Parts: ["wiretap"]);

    public IComposeMessage ComposeMessage { get; init; } = new ComposeMessageByAppending();

    public static Configuration Current => AmbientContext<Configuration>.Current ?? new();

    public static IDisposable Push(Configuration configuration) => AmbientContext<Configuration>.Push(configuration);
}
