using Wiretap.Util.Data;

namespace Wiretap.Util.Buzz;

public static class GetAnnotatedDetails
{
    public static DetailCollection From(
        PropertyName root,
        IEnumerable<object?> sources
    )
    {
        var details = new DetailCollection();

        foreach (var (source, level) in sources.Where(source => source is not null).Select((source, level) => (source!, level)))
        {
            var builder = new DetailBuilder(root.Activity.State, level, details);

            if (source is IDetailSource detailSource)
            {
                detailSource.Details(builder);
            }

            AnnotatedProperties.For<Detail>(
                source,
                (propertyName, detail, value) =>
                {
                    builder.Add(new PropertyName(detail.Name ?? propertyName), value, options => options.Cascade = detail.Cascade);
                }
            );
        }

        return details;
    }
}
