using Wiretap.Util.Data;

namespace Wiretap.Util.Buzz;

public static class CollectDetails
{
    public static void From(
        DetailBuilder builder,
        object source
    )
    {
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
}
