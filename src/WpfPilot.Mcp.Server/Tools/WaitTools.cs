using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Dsl;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class WaitTools
{
    private readonly UiaAdapter _uia;
    private readonly AuditLog _audit;

    public WaitTools(UiaAdapter uia, SessionManager session, AuditLog audit)
    {
        _uia = uia;
        _ = session;
        _audit = audit;
    }

    [McpServerTool(Name = "wpf_wait"),
     Description("Wait until a UI condition is met. Use wpf_capabilities for conditions. " +
        "Examples: wpf_wait(condition='exists', selector={automationId:'btnSave'}, timeoutMs=10000) | " +
        "wpf_wait(condition='has_text', selector={name:'lblStatus'}, value='Ready')")]
    public string Wait(WaitRequest request)
    {
        _audit.Record("wpf_wait", request.Selector, request.Path, new Dictionary<string, object?>
        {
            ["condition"] = request.Condition,
            ["value"] = request.Value,
            ["timeoutMs"] = request.TimeoutMs,
            ["pollIntervalMs"] = request.PollIntervalMs
        });

        if (!WaitConditionCatalog.TryParse(request.Condition, out var cond))
            return ToolJson.Error(ErrorCodes.InvalidArgs, $"Unknown condition: '{request.Condition}'. Use wpf_capabilities.");

        var met = PollUntil(() => CheckCondition(cond, request), request.TimeoutMs, request.PollIntervalMs);
        if (!met)
            return ToolJson.Error(ErrorCodes.Timeout, $"Condition '{request.Condition}' was not met within {request.TimeoutMs}ms.");

        return ToolJson.Ok(new
        {
            condition = request.Condition,
            met,
            timeoutMs = request.TimeoutMs,
            selector = request.Selector,
            path = request.Path
        });
    }

    private bool CheckCondition(WaitCondition cond, WaitRequest request)
    {
        var element = _uia.ResolveSelector(new ElementSelector
        {
            Element = request.Selector,
            Path = request.Path
        });
        return cond switch
        {
            WaitCondition.Exists => element is not null,
            WaitCondition.NotExists => element is null,
            WaitCondition.Enabled => element?.Properties.IsEnabled.ValueOrDefault == true,
            WaitCondition.Disabled => element is not null && element.Properties.IsEnabled.ValueOrDefault == false,
            WaitCondition.Visible => element is not null && !element.Properties.IsOffscreen.ValueOrDefault,
            WaitCondition.Hidden => element is null || element.Properties.IsOffscreen.ValueOrDefault,
            WaitCondition.Focused => element?.Properties.HasKeyboardFocus.ValueOrDefault == true,
            WaitCondition.Checked => element?.Patterns.Toggle.IsSupported == true &&
                                     element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault == FlaUI.Core.Definitions.ToggleState.On,
            WaitCondition.Unchecked => element?.Patterns.Toggle.IsSupported == true &&
                                       element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault == FlaUI.Core.Definitions.ToggleState.Off,
            WaitCondition.Selected => element?.Patterns.SelectionItem.IsSupported == true &&
                                      element.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault,
            WaitCondition.Expanded => element?.Patterns.ExpandCollapse.IsSupported == true &&
                                      element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.ValueOrDefault == FlaUI.Core.Definitions.ExpandCollapseState.Expanded,
            WaitCondition.Collapsed => element?.Patterns.ExpandCollapse.IsSupported == true &&
                                       element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.ValueOrDefault == FlaUI.Core.Definitions.ExpandCollapseState.Collapsed,
            WaitCondition.HasText => MatchText(element, request.Value, false),
            WaitCondition.HasValue => MatchValue(element, request.Value, false),
            _ => false
        };
    }

    private static bool MatchText(FlaUI.Core.AutomationElements.AutomationElement? element, string? expected, bool contains)
    {
        if (element is null || string.IsNullOrEmpty(expected)) return false;
        var actual = element.Properties.Name.ValueOrDefault ?? string.Empty;
        if (element.Patterns.Value.IsSupported)
        {
            try { actual = element.Patterns.Value.Pattern.Value.ValueOrDefault ?? actual; } catch { }
        }
        return contains
            ? actual.Contains(expected, StringComparison.OrdinalIgnoreCase)
            : actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchValue(FlaUI.Core.AutomationElements.AutomationElement? element, string? expected, bool contains)
    {
        if (element is null || string.IsNullOrEmpty(expected) || !element.Patterns.Value.IsSupported) return false;
        try
        {
            var actual = element.Patterns.Value.Pattern.Value.ValueOrDefault ?? string.Empty;
            return contains
                ? actual.Contains(expected, StringComparison.OrdinalIgnoreCase)
                : actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static bool PollUntil(Func<bool> condition, int timeoutMs, int pollIntervalMs = 200)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                if (condition()) return true;
            }
            catch { }
            Thread.Sleep(pollIntervalMs);
        }
        return false;
    }
}
