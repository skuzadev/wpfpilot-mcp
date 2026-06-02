using System.Text.Json;
using WpfPilot.Mcp.Core;
using WpfPilot.Mcp.Core.Models;
using Xunit;

namespace WpfPilot.Mcp.Core.Tests;

public class ElementSelectorTests
{
    [Fact]
    public void ElementCriteria_IsEmpty_When_Only_NameMatchMode_Set()
    {
        var criteria = new ElementCriteria { NameMatch = NameMatchMode.Contains };
        Assert.False(criteria.IsEmpty);
    }

    [Fact]
    public void ElementCriteria_IsEmpty_When_Only_WindowTitle_Set()
    {
        var criteria = new ElementCriteria { WindowTitle = "Main" };
        Assert.False(criteria.IsEmpty);
    }

    [Fact]
    public void ElementCriteria_IsEmpty_When_All_Fields_Default()
    {
        Assert.True(new ElementCriteria().IsEmpty);
    }

    [Fact]
    public void ElementSelector_IsEmpty_When_Only_Probe_Set()
    {
        var selector = new ElementSelector
        {
            Probe = new ProbeCriteria { BindingPath = "SaveCommand" }
        };
        Assert.False(selector.IsEmpty);
    }

    [Fact]
    public void ElementSelector_IsEmpty_When_All_Fields_Default()
    {
        Assert.True(new ElementSelector().IsEmpty);
    }

    [Fact]
    public void Deserialize_Criteria_Json_Property()
    {
        const string json = """{"criteria":{"automationId":"btnSave"}}""";
        var selector = JsonSerializer.Deserialize<ElementSelector>(json, JsonDefaults.Options);
        Assert.NotNull(selector);
        Assert.Equal("btnSave", selector!.Criteria!.AutomationId);
    }

    [Fact]
    public void Deserialize_Legacy_Element_Json_Property()
    {
        const string json = """{"element":{"name":"Submit"}}""";
        var selector = JsonSerializer.Deserialize<ElementSelector>(json, JsonDefaults.Options);
        Assert.NotNull(selector);
        Assert.Equal("Submit", selector!.Criteria!.Name);
        Assert.Equal("Submit", selector.Element!.Name);
    }

    [Fact]
    public void Deserialize_Prefers_Criteria_When_Both_Keys_Present()
    {
        const string json = """{"criteria":{"automationId":"fromCriteria"},"element":{"automationId":"fromElement"}}""";
        var selector = JsonSerializer.Deserialize<ElementSelector>(json, JsonDefaults.Options);
        Assert.Equal("fromCriteria", selector!.Criteria!.AutomationId);
    }

    [Fact]
    public void Serialize_Emits_Criteria_Not_Element()
    {
        var selector = new ElementSelector
        {
            Criteria = new ElementCriteria { AutomationId = "btnOk" }
        };
        var json = JsonSerializer.Serialize(selector, JsonDefaults.Options);
        Assert.Contains("\"criteria\"", json);
        Assert.DoesNotContain("\"element\"", json);
    }
}
