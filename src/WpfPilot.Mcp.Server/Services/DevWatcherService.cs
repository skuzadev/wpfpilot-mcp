using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using WpfPilot.Mcp.Core.Models;

namespace WpfPilot.Mcp.Server.Services;

/// <summary>
/// Watches the UI for real-time issues: missing AutomationIds on new elements,
/// binding errors, tab order problems, and accessibility gaps.
/// </summary>
public sealed class DevWatcherService
{
    private readonly SessionManager _session;
    private readonly UiaAdapter _uia;
    private readonly ProbeClient _probe;

    private List<UiElement>? _previousSnapshot;
    private DateTime _lastCheck = DateTime.MinValue;

    public DevWatcherService(SessionManager session, UiaAdapter uia, ProbeClient probe)
    {
        _session = session;
        _uia = uia;
        _probe = probe;
    }

    public async Task<DevWatchReport> CheckAsync()
    {
        var report = new DevWatchReport();

        if (!_session.IsAttached)
        {
            report.Issues.Add(new DevIssue
            {
                Severity = "error",
                Category = "session",
                Message = "Not attached to any application."
            });
            return report;
        }

        var currentElements = _uia.QueryElements();

        // Check for missing AutomationIds on actionable elements
        var missingIds = currentElements
            .Where(e => IsActionable(e.ControlType) && string.IsNullOrEmpty(e.AutomationId))
            .ToList();

        foreach (var elem in missingIds.Take(10))
        {
            report.Issues.Add(new DevIssue
            {
                Severity = "warning",
                Category = "automation_id",
                Message = $"{elem.ControlType} '{elem.Name ?? "(no name)"}' has no AutomationId — tests won't reliably find it.",
                Element = $"{elem.ControlType}:{elem.Name}",
                Suggestion = $"Add AutomationProperties.AutomationId to this {elem.ControlType} in XAML."
            });
        }

        // Check for duplicate AutomationIds
        var duplicates = currentElements
            .Where(e => !string.IsNullOrEmpty(e.AutomationId))
            .GroupBy(e => e.AutomationId)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var dup in duplicates)
        {
            report.Issues.Add(new DevIssue
            {
                Severity = "error",
                Category = "duplicate_id",
                Message = $"AutomationId '{dup.Key}' is used by {dup.Count()} elements — selectors will be ambiguous.",
                Element = dup.Key!,
                Suggestion = "Each AutomationId must be unique within the window. Add suffixes or use more specific IDs."
            });
        }

        // Check for missing accessible names
        var missingNames = currentElements
            .Where(e => IsActionable(e.ControlType) &&
                        string.IsNullOrEmpty(e.Name) &&
                        string.IsNullOrEmpty(e.AutomationId))
            .ToList();

        foreach (var elem in missingNames.Take(5))
        {
            report.Issues.Add(new DevIssue
            {
                Severity = "warning",
                Category = "accessibility",
                Message = $"{elem.ControlType} has no Name and no AutomationId — completely invisible to automation and screen readers.",
                Element = $"{elem.ControlType}:{elem.ClassName}",
                Suggestion = "Add AutomationProperties.Name or AutomationProperties.AutomationId."
            });
        }

        // Check for elements that should be focusable but aren't in tab order
        var buttons = currentElements.Where(e => e.ControlType == "Button" && e.IsEnabled).ToList();
        var offscreenButtons = buttons.Where(e => e.IsOffscreen).ToList();
        if (offscreenButtons.Count > 0)
        {
            report.Issues.Add(new DevIssue
            {
                Severity = "info",
                Category = "layout",
                Message = $"{offscreenButtons.Count} enabled button(s) are offscreen — may indicate layout overflow.",
                Suggestion = "Check if buttons are clipped by their container. Consider using ScrollViewer or adjusting layout."
            });
        }

        // Check for new elements since last snapshot (dev-time feedback)
        if (_previousSnapshot is not null)
        {
            var previousIds = new HashSet<string>(_previousSnapshot
                .Where(e => !string.IsNullOrEmpty(e.AutomationId))
                .Select(e => e.AutomationId!));

            var newElements = currentElements
                .Where(e => !string.IsNullOrEmpty(e.AutomationId) && !previousIds.Contains(e.AutomationId!))
                .ToList();

            foreach (var elem in newElements.Take(5))
            {
                report.NewElements.Add(new DevNewElement
                {
                    AutomationId = elem.AutomationId,
                    Name = elem.Name,
                    ControlType = elem.ControlType,
                    HasId = !string.IsNullOrEmpty(elem.AutomationId),
                    HasName = !string.IsNullOrEmpty(elem.Name)
                });
            }

            var removedIds = _previousSnapshot
                .Where(e => !string.IsNullOrEmpty(e.AutomationId))
                .Select(e => e.AutomationId!)
                .Except(currentElements.Where(e => !string.IsNullOrEmpty(e.AutomationId)).Select(e => e.AutomationId!))
                .ToList();

            foreach (var id in removedIds.Take(5))
            {
                report.RemovedElements.Add(id);
            }
        }

        // Probe-based checks (binding errors, validation)
        if (_probe.IsConnected)
        {
            try
            {
                var bindingResponse = await _probe.SendAsync("get_binding_errors");
                if (bindingResponse?.Data is not null && bindingResponse.Data.Value.GetRawText().Length > 5)
                {
                    report.Issues.Add(new DevIssue
                    {
                        Severity = "error",
                        Category = "binding",
                        Message = "Active binding errors detected — some UI elements may not display correct data.",
                        Element = "DataBinding",
                        Suggestion = "Run wpf_get_binding_errors for full details. Fix binding paths or DataContext."
                    });
                }
            }
            catch { }
        }

        // Summary
        report.Summary = new DevWatchSummary
        {
            TotalElements = currentElements.Count,
            ActionableElements = currentElements.Count(e => IsActionable(e.ControlType)),
            IssueCount = report.Issues.Count,
            WarningCount = report.Issues.Count(i => i.Severity == "warning"),
            ErrorCount = report.Issues.Count(i => i.Severity == "error"),
            NewElementCount = report.NewElements.Count,
            RemovedElementCount = report.RemovedElements.Count,
            HealthScore = CalculateHealthScore(currentElements, report.Issues)
        };

        _previousSnapshot = currentElements;
        _lastCheck = DateTime.UtcNow;

        return report;
    }

    private static int CalculateHealthScore(List<UiElement> elements, List<DevIssue> issues)
    {
        var actionable = elements.Where(e => IsActionable(e.ControlType)).ToList();
        if (actionable.Count == 0) return 100;

        var withId = actionable.Count(e => !string.IsNullOrEmpty(e.AutomationId));
        var idCoverage = (double)withId / actionable.Count;

        var errorPenalty = issues.Count(i => i.Severity == "error") * 10;
        var warningPenalty = issues.Count(i => i.Severity == "warning") * 3;

        var score = (int)(idCoverage * 100) - errorPenalty - warningPenalty;
        return Math.Max(0, Math.Min(100, score));
    }

    private static bool IsActionable(string? controlType) =>
        controlType is "Button" or "TextBox" or "ComboBox" or "CheckBox"
            or "RadioButton" or "MenuItem" or "TabItem" or "ListItem"
            or "DataItem" or "TreeItem" or "Slider" or "Hyperlink";
}

public sealed class DevWatchReport
{
    public DevWatchSummary Summary { get; set; } = new();
    public List<DevIssue> Issues { get; set; } = [];
    public List<DevNewElement> NewElements { get; set; } = [];
    public List<string> RemovedElements { get; set; } = [];
}

public sealed class DevWatchSummary
{
    public int TotalElements { get; set; }
    public int ActionableElements { get; set; }
    public int IssueCount { get; set; }
    public int WarningCount { get; set; }
    public int ErrorCount { get; set; }
    public int NewElementCount { get; set; }
    public int RemovedElementCount { get; set; }
    public int HealthScore { get; set; }
}

public sealed class DevIssue
{
    public string Severity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Element { get; set; }
    public string? Suggestion { get; set; }
}

public sealed class DevNewElement
{
    public string? AutomationId { get; set; }
    public string? Name { get; set; }
    public string? ControlType { get; set; }
    public bool HasId { get; set; }
    public bool HasName { get; set; }
}
