using System.Collections;

namespace Wiretap.Meta;

internal sealed class AmbientContext<T> : IDisposable, IEnumerable<AmbientContext<T>> where T : class
{
    private static readonly AsyncLocal<AmbientContext<T>?> CurrentScope = new();

    private bool _disposed;

    private AmbientContext(T value, AmbientContext<T>? parent)
    {
        Value = value;
        Parent = parent;
    }

    public T Value { get; }

    public AmbientContext<T>? Parent { get; }

    public static T? Current => CurrentScope.Value?.Value;

    public static AmbientContext<T> Push(T value)
    {
        var scope = new AmbientContext<T>(value, CurrentScope.Value);
        CurrentScope.Value = scope;
        return scope;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CurrentScope.Value = Parent;
        _disposed = true;
    }

    public IEnumerator<AmbientContext<T>> GetEnumerator()
    {
        for (var current = this; current is not null; current = current.Parent)
        {
            yield return current;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
