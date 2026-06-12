using System.Collections;

namespace Wiretap.Meta;

internal sealed class ActivityScopeStack<T> : IDisposable, IEnumerable<ActivityScopeStack<T>> where T : class
{
    private static readonly AsyncLocal<ActivityScopeStack<T>?> CurrentScope = new();

    private bool _disposed;

    private ActivityScopeStack(T value, ActivityScopeStack<T>? parent)
    {
        Value = value;
        Parent = parent;
    }

    public T Value { get; }

    public ActivityScopeStack<T>? Parent { get; }

    public static T? Current => CurrentScope.Value?.Value;

    public static ActivityScopeStack<T> Push(T value)
    {
        var scope = new ActivityScopeStack<T>(value, CurrentScope.Value);
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

    public IEnumerator<ActivityScopeStack<T>> GetEnumerator()
    {
        for (var current = this; current is not null; current = current.Parent)
        {
            yield return current;
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal static class ActivityScopeStackExtensions
{
    extension<T>(ActivityScopeStack<T>? scope) where T : class
    {
        public int Depth => scope?.Skip(1).Count() ?? 0;

        public string PathOf(Func<T, string> select)
        {
            return scope is null ? string.Empty : string.Join("/", scope.Reverse().Select(x => select(x.Value)));
        }
    }
}
