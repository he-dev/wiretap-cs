using Wiretap.Util;

namespace Wiretap.Test;

public class PropertyNameTests
{
    [Fact]
    public void StringPartsAreParsedAsDottedPaths()
    {
        Assert.Equal(
            "wiretap.activity.state.bulk.item_count",
            new PropertyName("wiretap.activity", "state", "bulk.item_count").ToString()
        );
    }

    [Fact]
    public void PlusCombinesParsedPropertyNames()
    {
        Assert.Equal(
            "wiretap.activity.state.bulk.item_count",
            (new PropertyName("wiretap.activity.state") + PropertyName.Parse("bulk.item_count")).ToString()
        );
    }
}
