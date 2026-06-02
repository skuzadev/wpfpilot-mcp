using System.ComponentModel;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Dsl;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class AssertionTools
{
    private readonly UiaAdapter _uia;
    private readonly AuditLog _audit;

    public AssertionTools(UiaAdapter uia, AuditLog audit)
    {
        _uia = uia;
        _audit = audit;
    }

    [McpServerTool(Name = "wpf_assert"),
     Description("Assert a UI condition. Returns {assertion:'pass'|'fail', message}. Use wpf_capabilities for conditions.")]
    public string Assert(AssertRequest request)
    {
        _audit.Record("wpf_assert", request.Selector, request.Path, new Dictionary<string, object?>
        {
            ["condition"] = request.Condition,
            ["expected"] = request.Expected,
            ["value"] = request.Value
        });

        if (!TryParseCondition(request.Condition, out var cond, out var mode))
            return Fail($"Unknown condition: '{request.Condition}'. Use wpf_capabilities.");

        var element = _uia.ResolveSelector(new ElementSelector
        {
            Element = request.Selector,
            Path = request.Path
        });

        return (cond, mode) switch
        {
            (AssertCondition.Exists, _) => element is not null ? Pass("Element exists.") : Fail("Element not found."),
            (AssertCondition.NotExists, _) => element is null ? Pass("Element does not exist.") : Fail("Element unexpectedly exists."),
            (AssertCondition.Visible, _) => element is null ? Fail("Element not found.") : (!element.Properties.IsOffscreen.ValueOrDefault ? Pass("Element is visible.") : Fail("Element is offscreen.")),
            (AssertCondition.Hidden, _) => element is null ? Pass("Element is hidden.") : (element.Properties.IsOffscreen.ValueOrDefault ? Pass("Element is hidden.") : Fail("Element is visible.")),
            (AssertCondition.Enabled, _) => element is null ? Fail("Element not found.") : (element.Properties.IsEnabled.ValueOrDefault ? Pass("Element is enabled.") : Fail("Element is disabled.")),
            (AssertCondition.Disabled, _) => element is null ? Fail("Element not found.") : (!element.Properties.IsEnabled.ValueOrDefault ? Pass("Element is disabled.") : Fail("Element is enabled.")),
            (AssertCondition.Focused, _) => element is null ? Fail("Element not found.") : (element.Properties.HasKeyboardFocus.ValueOrDefault ? Pass("Element is focused.") : Fail("Element is not focused.")),
            (AssertCondition.Checked, _) => ToggleCheck(element, true),
            (AssertCondition.Unchecked, _) => ToggleCheck(element, false),
            (AssertCondition.Selected, _) => SelectionCheck(element, true),
            (AssertCondition.NotSelected, _) => SelectionCheck(element, false),
            (AssertCondition.Expanded, _) => ExpandCheck(element, true),
            (AssertCondition.Collapsed, _) => ExpandCheck(element, false),
            (AssertCondition.TextEquals, _) => TextCheck(element, request.Expected, false),
            (AssertCondition.TextContains, _) => TextCheck(element, request.Expected ?? request.Value, true),
            (AssertCondition.TextMatches, _) => TextCheck(element, request.Expected ?? request.Value, true),
            (AssertCondition.ValueEquals, _) => ValueCheck(element, request.Expected, false),
            (AssertCondition.ValueContains, _) => ValueCheck(element, request.Expected ?? request.Value, true),
            _ => Fail($"Condition '{request.Condition}' not supported")
        };
    }

    private enum AssertCondition
    {
        Exists, NotExists, Visible, Hidden, Enabled, Disabled, Focused,
        Checked, Unchecked, Selected, NotSelected, Expanded, Collapsed,
        TextEquals, TextContains, TextMatches, ValueEquals, ValueContains
    }

    private enum AssertMode { Exact, Contains }

    private static bool TryParseCondition(string s, out AssertCondition cond, out AssertMode mode)
    {
        mode = AssertMode.Exact;
        var key = (s ?? string.Empty).Trim().ToLowerInvariant();
        switch (key)
        {
            case "text_contains": mode = AssertMode.Contains; key = "text_contains"; break;
            case "text_matches": mode = AssertMode.Contains; key = "text_matches"; break;
            case "text_equals":
            case "has_text": key = "text_equals"; break;
            case "value_contains": mode = AssertMode.Contains; key = "value_contains"; break;
            case "value_equals":
            case "has_value": key = "value_equals"; break;
        }

        if (key is not (
            "exists" or "present" or "not_exists" or "absent" or "visible" or "hidden" or "invisible" or
            "enabled" or "disabled" or "focused" or "has_focus" or "checked" or "unchecked" or
            "selected" or "not_selected" or "expanded" or "collapsed" or
            "text_equals" or "text_contains" or "text_matches" or "value_equals" or "value_contains"))
        {
            cond = default;
            return false;
        }

        cond = key switch
        {
            "exists" or "present" => AssertCondition.Exists,
            "not_exists" or "absent" => AssertCondition.NotExists,
            "visible" => AssertCondition.Visible,
            "hidden" or "invisible" => AssertCondition.Hidden,
            "enabled" => AssertCondition.Enabled,
            "disabled" => AssertCondition.Disabled,
            "focused" or "has_focus" => AssertCondition.Focused,
            "checked" => AssertCondition.Checked,
            "unchecked" => AssertCondition.Unchecked,
            "selected" => AssertCondition.Selected,
            "not_selected" => AssertCondition.NotSelected,
            "expanded" => AssertCondition.Expanded,
            "collapsed" => AssertCondition.Collapsed,
            "text_equals" => AssertCondition.TextEquals,
            "text_contains" => AssertCondition.TextContains,
            "text_matches" => AssertCondition.TextMatches,
            "value_equals" => AssertCondition.ValueEquals,
            "value_contains" => AssertCondition.ValueContains,
            _ => AssertCondition.Exists
        };
        return true;
    }

    private static string Pass(string message) => ToolJson.Ok(new { assertion = "pass", message });
    private static string Fail(string message) => ToolJson.Ok(new { assertion = "fail", message });

    private static string ToggleCheck(FlaUI.Core.AutomationElements.AutomationElement? el, bool desired)
    {
        if (el is null) return Fail("Element not found");
        if (!el.Patterns.Toggle.IsSupported) return Fail("Element does not support Toggle pattern");
        var state = el.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault;
        var isOn = state == FlaUI.Core.Definitions.ToggleState.On;
        return isOn == desired
            ? Pass(desired ? "Element is checked." : "Element is unchecked.")
            : Fail(desired ? "Element is not checked." : "Element is checked.");
    }

    private static string SelectionCheck(FlaUI.Core.AutomationElements.AutomationElement? el, bool desired)
    {
        if (el is null) return Fail("Element not found");
        if (!el.Patterns.SelectionItem.IsSupported) return Fail("Element does not support SelectionItem pattern");
        var isSel = el.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault;
        return isSel == desired
            ? Pass(desired ? "Element is selected." : "Element is not selected.")
            : Fail(desired ? "Element is not selected." : "Element is selected.");
    }

    private static string ExpandCheck(FlaUI.Core.AutomationElements.AutomationElement? el, bool desired)
    {
        if (el is null) return Fail("Element not found");
        if (!el.Patterns.ExpandCollapse.IsSupported) return Fail("Element does not support ExpandCollapse pattern");
        var state = el.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.ValueOrDefault;
        var isExp = state == FlaUI.Core.Definitions.ExpandCollapseState.Expanded;
        return isExp == desired
            ? Pass(desired ? "Element is expanded." : "Element is collapsed.")
            : Fail(desired ? "Element is not expanded." : "Element is not collapsed.");
    }

    private static string TextCheck(FlaUI.Core.AutomationElements.AutomationElement? el, string? expected, bool contains)
    {
        if (el is null) return Fail("Element not found");
        if (string.IsNullOrEmpty(expected)) return Fail("expected is required for text_* conditions");
        var actual = el.Properties.Name.ValueOrDefault ?? string.Empty;
        if (el.Patterns.Value.IsSupported)
        {
            try { actual = el.Patterns.Value.Pattern.Value.ValueOrDefault ?? actual; } catch { }
        }
        var ok = contains
            ? actual.Contains(expected, StringComparison.OrdinalIgnoreCase)
            : actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
        return ok ? Pass($"Text matches: {actual}") : Fail($"Text '{actual}' does not match expected '{expected}'");
    }

    private static string ValueCheck(FlaUI.Core.AutomationElements.AutomationElement? el, string? expected, bool contains)
    {
        if (el is null) return Fail("Element not found");
        if (string.IsNullOrEmpty(expected)) return Fail("expected is required for value_* conditions");
        if (!el.Patterns.Value.IsSupported) return Fail("Element does not support Value pattern");
        try
        {
            var actual = el.Patterns.Value.Pattern.Value.ValueOrDefault ?? string.Empty;
            var ok = contains
                ? actual.Contains(expected, StringComparison.OrdinalIgnoreCase)
                : actual.Equals(expected, StringComparison.OrdinalIgnoreCase);
            return ok ? Pass($"Value matches: {actual}") : Fail($"Value '{actual}' does not match expected '{expected}'");
        }
        catch (Exception ex) { return Fail(ex.Message); }
    }
}
