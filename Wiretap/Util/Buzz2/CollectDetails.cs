using Wiretap.Util.Data;

namespace Wiretap.Util.Buzz2;

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

        GetPropertiesByAttribute.Where<Detail>(
            source,
            (propertyName, detail, value) =>
            {
                builder.Add(new PropertyName(detail.Name ?? propertyName), value, options => options.Cascade = detail.Cascade);
            }
        );
    }
}
