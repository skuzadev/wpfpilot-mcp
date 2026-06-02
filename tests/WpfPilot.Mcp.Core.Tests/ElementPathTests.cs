using Xunit;
using WpfPilot.Mcp.Core.Selectors;

namespace WpfPilot.Mcp.Core.Tests;

public class ElementPathTests
{
    [Fact]
    public void Parses_Simple_Child_Path()
    {
        var path = ElementPath.Parse("Window/DataGrid/Row");
        Assert.Equal(3, path.Segments.Count);
        Assert.Equal("Window", path.Segments[0].TypeName);
        Assert.Equal("DataGrid", path.Segments[1].TypeName);
        Assert.Equal("Row", path.Segments[2].TypeName);
        Assert.All(path.Segments, s => Assert.Equal(SegmentMode.Child, s.Mode));
    }

    [Fact]
    public void Parses_Descendant_Axis()
    {
        var path = ElementPath.Parse("Window//TabItem");
        Assert.Equal(2, path.Segments.Count);
        Assert.Equal(SegmentMode.Child, path.Segments[0].Mode);
        Assert.Equal(SegmentMode.Descendant, path.Segments[1].Mode);
    }

    [Fact]
    public void Parses_Anchor_Id()
    {
        var path = ElementPath.Parse("Window/DataGrid#orders/Row[3]");
        Assert.Equal("DataGrid", path.Segments[1].TypeName);
        Assert.Equal("orders", path.Segments[1].AnchorId);
        Assert.Equal(3, path.Segments[2].Index);
    }

    [Fact]
    public void Parses_Property_Predicates()
    {
        var path = ElementPath.Parse("Window/DataGrid/Row/Cell[@Header='Name']");
        Assert.Equal("Cell", path.Segments[3].TypeName);
        Assert.True(path.Segments[3].Properties.ContainsKey("Header"));
        Assert.Equal("Name", path.Segments[3].Properties["Header"]);
    }

    [Fact]
    public void Parses_Property_Predicates_With_DoubleQuotes()
    {
        var path = ElementPath.Parse("Window/Button[@AutomationId=\"Save\"]");
        Assert.Equal("Save", path.Segments[1].Properties["AutomationId"]);
    }

    [Fact]
    public void Parses_Combined_Constraints()
    {
        var path = ElementPath.Parse("Window/DataGrid#orders/Row[3]/Cell[@Header='Name']");
        var cell = path.Segments[3];
        Assert.Equal("Cell", cell.TypeName);
        Assert.Null(cell.AnchorId);
        Assert.Null(cell.Index);
        Assert.Single(cell.Properties);
    }

    [Fact]
    public void ToString_Roundtrip()
    {
        var original = "Window/DataGrid#orders/Row[3]/Cell[@Header='Name']";
        var path = ElementPath.Parse(original);
        Assert.Equal(original, path.ToString());
    }

    [Fact]
    public void ToString_Roundtrip_With_Descendant()
    {
        var original = "Window//TabItem[@Name='Advanced']/Button#ok";
        var path = ElementPath.Parse(original);
        Assert.Equal(original, path.ToString());
    }

    [Fact]
    public void TryParse_Returns_Null_On_Empty()
    {
        Assert.Null(ElementPath.TryParse(null));
        Assert.Null(ElementPath.TryParse(""));
        Assert.Null(ElementPath.TryParse("   "));
    }

    [Fact]
    public void Parse_Throws_On_Empty()
    {
        Assert.Throws<ArgumentException>(() => ElementPath.Parse(""));
    }

    [Fact]
    public void Parse_Throws_On_Unclosed_Bracket()
    {
        Assert.Throws<FormatException>(() => ElementPath.Parse("Window/Row[3"));
    }

    [Fact]
    public void Parses_Leading_Slash_As_Child()
    {
        var path = ElementPath.Parse("/Window/Button");
        Assert.Equal(2, path.Segments.Count);
        Assert.Equal("Window", path.Segments[0].TypeName);
    }

    [Fact]
    public void Parses_Index_Zero()
    {
        var path = ElementPath.Parse("Window/Row[0]");
        Assert.Equal(0, path.Segments[1].Index);
    }

    [Fact]
    public void Multiple_Predicates_In_Same_Segment()
    {
        var path = ElementPath.Parse("Window/Row[@Header='A'][@Selected='true']");
        Assert.Equal(2, path.Segments[1].Properties.Count);
    }

    [Fact]
    public void Parse_Throws_On_Empty_Middle_Segment()
    {
        Assert.Throws<FormatException>(() => ElementPath.Parse("Window//"));
        Assert.Throws<FormatException>(() => ElementPath.Parse("Window/DataGrid//"));
        Assert.Throws<FormatException>(() => ElementPath.Parse("Window/ /Button"));
    }

    [Fact]
    public void Parse_Throws_On_Predicate_Only_Without_TypeName()
    {
        Assert.Throws<FormatException>(() => ElementPath.Parse("Window/[@Header='Name']"));
    }

    [Fact]
    public void Parse_Allows_Anchor_Only_Segment()
    {
        var path = ElementPath.Parse("Window/#orders");
        Assert.Equal("orders", path.Segments[1].AnchorId);
    }
}
