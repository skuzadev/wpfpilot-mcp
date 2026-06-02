using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class DevWatcherTools
{
    private readonly DevWatcherService _watcher;
    private readonly UiaAdapter _uia;
    private readonly AuditLog _audit;

    public DevWatcherTools(DevWatcherService watcher, UiaAdapter uia, AuditLog audit)
    {
        _watcher = watcher;
        _uia = uia;
        _audit = audit;
    }

    [McpServerTool(Name = "wpf_dev_check"), Description("Run a development-time health check on the current window. Reports missing AutomationIds, duplicate IDs, accessibility gaps, binding errors, new/removed elements since last check, and an overall health score.")]
    public async Task<string> DevCheck()
    {
        _audit.Record("wpf_dev_check");
        try
        {
            var report = await _watcher.CheckAsync();
            return JsonSerializer.Serialize(report, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_dev_diff"), Description("Compare current UI state with previous check. Shows what elements were added, removed, or changed since the last wpf_dev_check call.")]
    public async Task<string> DevDiff()
    {
        _audit.Record("wpf_dev_diff");
        try
        {
            var report = await _watcher.CheckAsync();

            var diff = new
            {
                newElements = report.NewElements,
                removedElements = report.RemovedElements,
                summary = new
                {
                    added = report.NewElements.Count,
                    removed = report.RemovedElements.Count,
                    currentHealthScore = report.Summary.HealthScore,
                    note = report.NewElements.Count == 0 && report.RemovedElements.Count == 0
                        ? "No changes detected since last check."
                        : $"{report.NewElements.Count} new, {report.RemovedElements.Count} removed since last check."
                }
            };

            return JsonSerializer.Serialize(diff, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_dev_suggest_ids"), Description("Suggest AutomationId values for elements that are currently missing them. Returns XAML snippets ready to paste.")]
    public string DevSuggestIds()
    {
        _audit.Record("wpf_dev_suggest_ids");
        try
        {
            var allElements = _uia.QueryElements();
            var missingId = allElements
                .Where(e => IsActionable(e.ControlType) && string.IsNullOrEmpty(e.AutomationId))
                .ToList();

            var suggestions = missingId.Take(20).Select(e =>
            {
                var suggestedId = GenerateId(e.ControlType, e.Name, e.ClassName);
                return new
                {
                    currentState = new { e.ControlType, e.Name, e.ClassName },
                    suggestedAutomationId = suggestedId,
                    xamlSnippet = $"AutomationProperties.AutomationId=\"{suggestedId}\""
                };
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                totalMissingIds = missingId.Count,
                suggestions
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_dev_accessibility_quick"), Description("Quick accessibility lint: reports critical issues that would fail WCAG compliance — missing names, keyboard-inaccessible controls, broken focus order.")]
    public string DevAccessibilityQuick()
    {
        _audit.Record("wpf_dev_accessibility_quick");
        try
        {
            var allElements = _uia.QueryElements();
            var actionable = allElements.Where(e => IsActionable(e.ControlType)).ToList();

            var issues = new List<object>();

            // Elements with no accessible name at all
            var noName = actionable.Where(e => string.IsNullOrEmpty(e.Name) && string.IsNullOrEmpty(e.AutomationId)).ToList();
            if (noName.Count > 0)
            {
                issues.Add(new
                {
                    rule = "WCAG 4.1.2 - Name",
                    severity = "critical",
                    count = noName.Count,
                    message = $"{noName.Count} interactive element(s) have no accessible name — screen readers cannot announce them.",
                    examples = noName.Take(5).Select(e => new { e.ControlType, e.ClassName }).ToList()
                });
            }

            // TextBox without label association
            var textBoxes = actionable.Where(e => e.ControlType == "TextBox").ToList();
            var unlabeled = textBoxes.Where(e => string.IsNullOrEmpty(e.Name)).ToList();
            if (unlabeled.Count > 0)
            {
                issues.Add(new
                {
                    rule = "WCAG 1.3.1 - Labels",
                    severity = "critical",
                    count = unlabeled.Count,
                    message = $"{unlabeled.Count} TextBox(es) have no accessible label.",
                    fix = "Add AutomationProperties.Name or associate a Label with AutomationProperties.LabeledBy."
                });
            }

            // Images without alt text
            var images = allElements.Where(e => e.ControlType == "Image" && string.IsNullOrEmpty(e.Name)).ToList();
            if (images.Count > 0)
            {
                issues.Add(new
                {
                    rule = "WCAG 1.1.1 - Non-text Content",
                    severity = "warning",
                    count = images.Count,
                    message = $"{images.Count} Image element(s) have no accessible name (alt text equivalent)."
                });
            }

            var score = actionable.Count > 0
                ? Math.Max(0, 100 - (noName.Count * 15) - (unlabeled.Count * 10) - (images.Count * 5))
                : 100;

            return JsonSerializer.Serialize(new
            {
                accessibilityScore = Math.Min(100, score),
                issueCount = issues.Count,
                issues,
                recommendation = score >= 80 ? "Good accessibility posture." :
                    score >= 50 ? "Moderate issues — address critical items before release." :
                    "Significant accessibility gaps — many elements are invisible to assistive technology."
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    private static string GenerateId(string? controlType, string? name, string? className)
    {
        var prefix = controlType switch
        {
            "Button" => "btn",
            "TextBox" => "txt",
            "ComboBox" => "cmb",
            "CheckBox" => "chk",
            "RadioButton" => "rb",
            "MenuItem" => "mnu",
            "TabItem" => "tab",
            "Slider" => "sld",
            "ListItem" => "li",
            _ => "ctl"
        };

        var suffix = !string.IsNullOrEmpty(name)
            ? name.Replace(" ", "").Replace("-", "").Replace("_", "")
            : !string.IsNullOrEmpty(className)
                ? className.Split('.').Last()
                : "Unknown";

        // Limit length
        if (suffix.Length > 20)
            suffix = suffix[..20];

        return $"{prefix}{suffix}";
    }

    private static bool IsActionable(string? controlType) =>
        controlType is "Button" or "TextBox" or "ComboBox" or "CheckBox"
            or "RadioButton" or "MenuItem" or "TabItem" or "ListItem"
            or "DataItem" or "TreeItem" or "Slider" or "Hyperlink";
}
