namespace Wiretap.Util;

public interface IPropertyNameSchema
{
    string Render(params string[] parts);
}

public record PropertyNameSchema : IPropertyNameSchema
{
    public string[] Prefix { get; init; } = ["wiretap"];

    public string Separator { get; init; } = ".";

    public PropertyNameBuilder Root => new(this, []);

    public string Render(params string[] parts) => string.Join(Separator, [..Prefix, ..parts]);
}

public readonly record struct PropertyNameBuilder(IPropertyNameSchema Schema, string[] Parts)
{
    public PropertyNameBuilder Append(params string[] parts)
    {
        return this with { Parts = [..Parts, ..parts] };
    }

    public PropertyNamePlaceholder ToTemplate() => new(this);

    public static implicit operator string(PropertyNameBuilder value)
    {
        return value.Schema.Render(value.Parts);
    }
}

public readonly record struct PropertyNamePlaceholder(PropertyNameBuilder Name)
{
    public static implicit operator string(PropertyNamePlaceholder value)
    {
        return $"{{{value.Name}}}";
    }
}

public static class PropertyNameBuilderExtensions
{
    extension(PropertyNameBuilder name)
    {
        public PropertyNameBuilder Activity => name.Append("activity");

        public PropertyNameBuilder State => name.Append("state");

        public PropertyNameBuilder Status => name.Append("status");

        public PropertyNameBuilder Name => name.Append("name");

        public PropertyNameBuilder Role => name.Append("role");

        public PropertyNameBuilder Code => name.Append("code");

        public PropertyNameBuilder Depth => name.Append("depth");

        public PropertyNameBuilder Path => name.Append("path");

        public PropertyNameBuilder DurationMs => name.Append("duration_ms");
    }
}
