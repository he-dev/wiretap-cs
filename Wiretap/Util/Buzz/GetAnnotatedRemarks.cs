using Wiretap.Util.Data;

namespace Wiretap.Util.Buzz;

public static class GetAnnotatedRemarks
{
    public static RemarkCollection From(
        PropertyName root,
        DetailCollection details,
        params object?[] sources
    )
    {
        var remarks = new RemarkCollection();

        foreach (var source in sources)
        {
            if (source is null)
            {
                continue;
            }

            var builder = new RemarkBuilder(root, details, remarks);

            if (source is IRemarkSource remarkSource)
            {
                remarkSource.Remarks(builder);
            }

            AnnotatedProperties.For<Remark>(
                source,
                (propertyName, remark, value) =>
                {
                    builder.Add(new PropertyName(propertyName), value, options =>
                    {
                        options.Label = remark.Label;
                        options.Separator = remark.Separator ?? ": ";
                        options.QuoteStyle = remark.QuoteStyle;
                        options.QuoteMode = remark.QuoteMode;
                    });
                }
            );
        }

        return remarks;
    }
}
