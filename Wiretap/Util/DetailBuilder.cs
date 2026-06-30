namespace Wiretap.Util;

public sealed class DetailOptions
{
    public bool Cascade { get; set; }
}

public interface IDetailSource
{
    void Details(DetailBuilder details);
}

public sealed class DetailBuilder(
    PropertyName root,
    int level,
    IDictionary<string, object?> details
)
{
    public void Add(PropertyName name, object? value, Action<DetailOptions>? configure = null)
    {
        var options = new DetailOptions();
        configure?.Invoke(options);

        if (level == 0 || options.Cascade)
        {
            var key = (root + name).ToString();
            if (!details.TryGetValue(key, out var current))
            {
                details[key] = value;
            }
            else
            {
                if (current is null && value is not null)
                {
                    details[key] = value;
                }
            }
        }
    }
}
