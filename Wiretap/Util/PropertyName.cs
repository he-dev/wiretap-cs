namespace Wiretap.Util;

public readonly record struct PropertyName : IFormattable
{
    public PropertyName(params string[] parts)
    {
        Parts = [.. parts.SelectMany(part => part.Split('.'))];
    }

    public string[] Parts { get; }

    public bool Equals(PropertyName other) => Parts.AsSpan().SequenceEqual(other.Parts);


    public PropertyName Append(params string[] parts)
    {
        return new PropertyName([..Parts, ..parts.SelectMany(part => part.Split('.'))]);
    }

    public static PropertyName Parse(string value)
    {
        return new PropertyName(value);
    }

    public static PropertyName operator +(PropertyName left, PropertyName right)
    {
        return new PropertyName([..left.Parts, ..right.Parts]);
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        var name = string.Join(".", Parts);
        return format switch { null => name, "." => $"{{{name}}}", _ => $"{{{name}:{format}}}" };
    }

    public override string ToString() => ToString(null, null);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var part in Parts)
        {
            hash.Add(part, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
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
        public PropertyName Buzz => name.Append("buzz");

        public PropertyName State => name.Append("state");

        public PropertyName Status => name.Append("status");

        public PropertyName Name => name.Append("name");

        public PropertyName Tags => name.Append("tags");

        public PropertyName Role => name.Append("role");

        public PropertyName Code => name.Append("code");

        public PropertyName Depth => name.Append("depth");

        public PropertyName Path => name.Append("path");

        public PropertyName DurationMs => name.Append("duration_ms");

        public PropertyName TraceId => name.Append("trace_id");

        public PropertyName SpanId => name.Append("span_id");

        public PropertyName ParentSpanId => name.Append("parent_span_id");
    }
}
