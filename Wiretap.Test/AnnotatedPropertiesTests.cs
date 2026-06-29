using Wiretap.Util;
using Wiretap.Util.Buzz;

namespace Wiretap.Test;

public sealed class AnnotatedPropertiesTests
{
    [Fact]
    public void ReportsPublicAnnotatedProperties()
    {
        var source = new Source();
        var details = new List<(string Name, string? DetailName, object? Value)>();

        AnnotatedProperties.For<Detail>(
            source,
            (name, detail, value) => details.Add((name, detail.Name, value))
        );

        Assert.Equal(
            [
                ("PublicValue", "custom", "visible"),
                ("CascadingValue", "cascading", "root"),
                ("OptionalValue", "optional", null),
            ],
            details
        );
    }

    [Fact]
    public void ReportsDifferentAnnotationTypesIndependently()
    {
        var source = new Source();
        var remarks = new List<(string Name, string? Label, QuoteMode QuoteMode, object? Value)>();

        AnnotatedProperties.For<Remark>(
            source,
            (name, remark, value) => remarks.Add((name, remark.Label, remark.QuoteMode, value))
        );

        Assert.Equal([("RemarkedValue", "Remark", QuoteMode.Always, "quoted")], remarks);
    }

    [Fact]
    public void CollectsAnnotatedDetails()
    {
        var source = new Source();
        var details = new Dictionary<string, object?>();

        GetAnnotatedDetails.From(
            new PropertyName("wiretap"),
            [source]
        ).ToList().ForEach(pair => details[pair.Key] = pair.Value);

        Assert.Equal("interface", details["wiretap.activity.state.by_interface"]);
        Assert.Equal("visible", details["wiretap.activity.state.custom"]);
        Assert.Equal("root", details["wiretap.activity.state.cascading"]);
        Assert.True(details.ContainsKey("wiretap.activity.state.optional"));
        Assert.Null(details["wiretap.activity.state.optional"]);
    }

    [Fact]
    public void CollectsOnlyCascadingDetailsWhenRequested()
    {
        var source = new Source();
        var details = new Dictionary<string, object?>();

        GetAnnotatedDetails.From(
            new PropertyName("wiretap"),
            [new object(), source]
        ).ToList().ForEach(pair => details[pair.Key] = pair.Value);

        Assert.Equal(["wiretap.activity.state.cascading"], details.Keys);
    }

    [Fact]
    public void CollectsAnnotatedRemarks()
    {
        var source = new Source();
        var parts = GetAnnotatedRemarks.From(new PropertyName("wiretap"), new Dictionary<string, object?>(), source);

        var messages = parts.Select(x => x.Value).ToList();
        Assert.Equal("Interface: {wiretap.activity.state.by_interface_remark}", messages[0].Template);
        Assert.Equal(["interface"], messages[0].Args);
        Assert.Equal("Remark: '{wiretap.activity.state.RemarkedValue}'", messages[1].Template);
        Assert.Equal(["quoted"], messages[1].Args);
    }

    private sealed class Source : IDetailSource, IRemarkSource
    {
        public void Details(DetailBuilder details)
        {
            details.Add(new PropertyName("by_interface"), "interface");
        }

        public void Remarks(RemarkBuilder remarks)
        {
            remarks.Add(new PropertyName("by_interface_remark"), "interface", options => options.Label = "Interface");
        }

        [Detail("custom")]
        public string PublicValue => "visible";

        [Detail("cascading", Cascade = true)]
        public string CascadingValue => "root";

        [Detail("optional")]
        public string? OptionalValue => null;

        [Remark("Remark", QuoteStyle = QuoteStyle.Single, QuoteMode = QuoteMode.Always)]
        public string RemarkedValue => "quoted";

        [Detail("private")]
        private string PrivateValue => "hidden";
    }
}
