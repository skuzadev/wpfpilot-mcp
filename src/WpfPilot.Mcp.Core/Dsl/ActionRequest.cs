using System.Text.Json.Serialization;

namespace WpfPilot.Mcp.Core.Dsl;

public enum ActionVerb
{
    Click,
    DoubleClick,
    RightClick,
    Invoke,
    Hover,
    SetValue,
    ClearValue,
    Type,
    SendKeys,
    Select,
    SelectByText,
    SelectByIndex,
    Toggle,
    Check,
    Uncheck,
    Expand,
    Collapse,
    Scroll,
    ScrollIntoView,
    Focus,
    DragDrop,
    OpenMenu,
    OpenContextMenu,
    SetSlider,
    SetDate,
    AcceptDialog,
    CancelDialog
}

public static class ActionVerbCatalog
{
    public static readonly IReadOnlyDictionary<string, ActionVerb> ByName =
        new Dictionary<string, ActionVerb>(StringComparer.OrdinalIgnoreCase)
        {
            ["click"] = ActionVerb.Click,
            ["double_click"] = ActionVerb.DoubleClick,
            ["doubleclick"] = ActionVerb.DoubleClick,
            ["right_click"] = ActionVerb.RightClick,
            ["rightclick"] = ActionVerb.RightClick,
            ["invoke"] = ActionVerb.Invoke,
            ["hover"] = ActionVerb.Hover,
            ["set_value"] = ActionVerb.SetValue,
            ["setvalue"] = ActionVerb.SetValue,
            ["clear_value"] = ActionVerb.ClearValue,
            ["clearvalue"] = ActionVerb.ClearValue,
            ["type"] = ActionVerb.Type,
            ["type_text"] = ActionVerb.Type,
            ["send_keys"] = ActionVerb.SendKeys,
            ["sendkeys"] = ActionVerb.SendKeys,
            ["select"] = ActionVerb.Select,
            ["select_by_text"] = ActionVerb.SelectByText,
            ["select_by_index"] = ActionVerb.SelectByIndex,
            ["toggle"] = ActionVerb.Toggle,
            ["check"] = ActionVerb.Check,
            ["uncheck"] = ActionVerb.Uncheck,
            ["expand"] = ActionVerb.Expand,
            ["collapse"] = ActionVerb.Collapse,
            ["scroll"] = ActionVerb.Scroll,
            ["scroll_into_view"] = ActionVerb.ScrollIntoView,
            ["focus"] = ActionVerb.Focus,
            ["drag_drop"] = ActionVerb.DragDrop,
            ["open_menu"] = ActionVerb.OpenMenu,
            ["open_context_menu"] = ActionVerb.OpenContextMenu,
            ["set_slider"] = ActionVerb.SetSlider,
            ["set_date"] = ActionVerb.SetDate,
            ["accept_dialog"] = ActionVerb.AcceptDialog,
            ["cancel_dialog"] = ActionVerb.CancelDialog
        };

    public static bool TryParse(string? s, out ActionVerb verb)
        => ByName.TryGetValue(s ?? string.Empty, out verb);
}

public sealed class ActionRequest
{
    [JsonPropertyName("verb")]
    public string Verb { get; set; } = string.Empty;

    [JsonPropertyName("selector")]
    public ElementCriteria? Selector { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("parentSelector")]
    public ElementCriteria? ParentSelector { get; set; }

    [JsonPropertyName("parentPath")]
    public string? ParentPath { get; set; }

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    [JsonPropertyName("index")]
    public int? Index { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("menuPath")]
    public string? MenuPath { get; set; }

    [JsonPropertyName("menuItem")]
    public string? MenuItem { get; set; }

    [JsonPropertyName("sourceAutomationId")]
    public string? SourceAutomationId { get; set; }

    [JsonPropertyName("targetAutomationId")]
    public string? TargetAutomationId { get; set; }

    [JsonPropertyName("keys")]
    public string? Keys { get; set; }

    [JsonPropertyName("direction")]
    public string? Direction { get; set; }

    [JsonPropertyName("amount")]
    public double? Amount { get; set; }

    [JsonPropertyName("dryRun")]
    public bool DryRun { get; set; }

    [JsonPropertyName("timeoutMs")]
    public int? TimeoutMs { get; set; }
}
