namespace Wiretap.Util;

public readonly record struct PropertyName(string Separator = ".", params string[] Parts) : IFormattable
{
    public PropertyName Append(params string[] parts)
    {
        return this with { Parts = [..Parts, ..parts] };
    }

    public override string ToString()
    {
        return string.Join(Separator, Parts);
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        var name = ToString();

        return format switch
        {
            null or "" => name,
            "_" => $"{{{name}}}",
            _ => $"{{{name}:{format}}}"
        };
    }

    public static implicit operator string(PropertyName value)
    {
        return value.ToString();
    }
}

public static class PropertyNameExtensions
{
    extension(PropertyName name)
    {
        public PropertyName Activity => name.Append("activity");

        public PropertyName State => name.Append("state");

        public PropertyName Status => name.Append("status");

        public PropertyName Name => name.Append("name");

        public PropertyName Tags => name.Append("tags");

        public PropertyName Role => name.Append("role");

        public PropertyName Code => name.Append("code");

        public PropertyName Depth => name.Append("depth");

        public PropertyName Path => name.Append("path");

        public PropertyName DurationMs => name.Append("duration_ms");
    }
}