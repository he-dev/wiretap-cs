using System;

namespace Reusable.Wiretap.Data;

public static class ConsoleStyleExtensions
{
    public static IDisposable Apply(this IConsoleStyle style)
    {
        // Backup the current style.
        var (foregroundColor, backgroundColor) = ConsoleStyle.Current;

        // Apply a new stile.
        (Console.ForegroundColor, Console.BackgroundColor) = (style.ForegroundColor, style.BackgroundColor);

        return new RestoreStyle(() => (Console.ForegroundColor, Console.BackgroundColor) = (foregroundColor, backgroundColor));
    }

    private class RestoreStyle(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}