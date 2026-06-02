using System.Text.Json.Serialization;
using WpfPilot.Mcp.Core.Serialization;

namespace WpfPilot.Mcp.Core.Models;

public sealed class ElementCriteria
{
    [JsonPropertyName("automationId")]
    public string? AutomationId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("controlType")]
    public string? ControlType { get; set; }

    [JsonPropertyName("className")]
    public string? ClassName { get; set; }

    [JsonPropertyName("nameMatch")]
    public NameMatchMode NameMatch { get; set; } = NameMatchMode.Equals;

    [JsonPropertyName("near")]
    public ElementCriteria? Near { get; set; }

    [JsonPropertyName("indexPath")]
    public int[]? IndexPath { get; set; }

    [JsonPropertyName("windowTitle")]
    public string? WindowTitle { get; set; }

    public bool IsEmpty =>
        string.IsNullOrEmpty(AutomationId) &&
        string.IsNullOrEmpty(Name) &&
        string.IsNullOrEmpty(ControlType) &&
        string.IsNullOrEmpty(ClassName) &&
        string.IsNullOrEmpty(WindowTitle) &&
        NameMatch == NameMatchMode.Equals &&
        (IndexPath is null || IndexPath.Length == 0) &&
        Near is null;
}

public enum NameMatchMode
{
    Equals = 0,
    Contains = 1,
    Regex = 2
}

[JsonConverter(typeof(ElementSelectorJsonConverter))]
public sealed class ElementSelector
{
    public ElementCriteria? Criteria { get; set; }

    /// <summary>Alias for <see cref="Criteria"/> (not serialized; use <c>criteria</c> in JSON).</summary>
    [JsonIgnore]
    public ElementCriteria? Element
    {
        get => Criteria;
        set => Criteria = value;
    }

    [JsonPropertyName("window")]
    public WindowSelector? Window { get; set; }

    [JsonPropertyName("probe")]
    public ProbeCriteria? Probe { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("fallbacks")]
    public List<ElementCriteria>? Fallbacks { get; set; }

    [JsonPropertyName("matchIndex")]
    public int MatchIndex { get; set; } = 0;

    public bool IsEmpty =>
        (Criteria is null || Criteria.IsEmpty) &&
        string.IsNullOrEmpty(Path) &&
        Window is null &&
        Probe is null &&
        (Fallbacks is null || Fallbacks.Count == 0);
}

public sealed class WindowSelector
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("process")]
    public string? Process { get; set; }

    [JsonPropertyName("automationId")]
    public string? AutomationId { get; set; }
}

public sealed class ProbeCriteria
{
    [JsonPropertyName("bindingPath")]
    public string? BindingPath { get; set; }

    [JsonPropertyName("dataContextType")]
    public string? DataContextType { get; set; }
}
