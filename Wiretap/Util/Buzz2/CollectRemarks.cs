using Wiretap.Util.Data;

namespace Wiretap.Util.Buzz2;

public static class CollectRemarks
{
    public static void From(RemarkBuilder builder, object source)
    {
        if (source is IRemarkSource remarkSource)
        {
            remarkSource.Remarks(builder);
        }

        GetPropertiesByAttribute.Where<Remark>(
            source,
            (propertyName, remark, value) =>
            {
                builder.Add(new PropertyName(propertyName), value, options =>
                {
                    options.Label = remark.Label;
                    options.Separator = remark.Separator ?? ": ";
                    options.Format = remark.Format;
                    options.QuoteStyle = remark.QuoteStyle;
                    options.QuoteMode = remark.QuoteMode;
                });
            }
        );
    }
}
