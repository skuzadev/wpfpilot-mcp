using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Dsl;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class ActTools
{
    private readonly UiaAdapter _uia;
    private readonly AuditLog _audit;
    private readonly RecordingService _recording;
    private readonly ErrorMapper _errors;
    private readonly UiActionEngine _actions;

    public ActTools(UiaAdapter uia, AuditLog audit, RecordingService recording, ErrorMapper errors, UiActionEngine actions)
    {
        _uia = uia;
        _audit = audit;
        _recording = recording;
        _errors = errors;
        _actions = actions;
    }

    [McpServerTool(Name = "wpf_act"),
     Description("Generic verb-driven action. Use wpf_capabilities to list verbs. Examples: " +
        "wpf_act(verb='click', selector={automationId:'btnSave'}) | " +
        "wpf_act(verb='set_value', selector={name:'Username'}, value='alice') | " +
        "wpf_act(verb='select_by_text', parentSelector={automationId:'cmbRole'}, text='Admin') | " +
        "wpf_act(verb='open_menu', menuPath='File>Recent>Doc1') | " +
        "wpf_act(verb='drag_drop', sourceAutomationId='a', targetAutomationId='b') | " +
        "wpf_act(verb='set_slider', selector={automationId:'vol'}, value='75')")]
    public string Act(ActionRequest request)
    {
        var stopwatch = Stopwatch.StartNew();
        var auditParams = new Dictionary<string, object?>
        {
            ["verb"] = request.Verb,
            ["selector"] = request.Selector,
            ["path"] = request.Path,
            ["value"] = request.Value,
            ["text"] = request.Text,
            ["index"] = request.Index,
            ["menuPath"] = request.MenuPath,
            ["menuItem"] = request.MenuItem,
            ["keys"] = request.Keys,
            ["direction"] = request.Direction,
            ["sourceAutomationId"] = request.SourceAutomationId,
            ["targetAutomationId"] = request.TargetAutomationId,
            ["dryRun"] = request.DryRun
        };
        _audit.Record("wpf_act", request.Selector, request.Path, auditParams);

        if (request.DryRun)
        {
            return ToolJson.Ok(new
            {
                dryRun = true,
                verb = request.Verb,
                selector = request.Selector,
                path = request.Path,
                wouldDo = "preview only - no action taken"
            });
        }

        if (!ActionVerbCatalog.TryParse(request.Verb, out var verb))
            return ToolJson.Error(ErrorCodes.InvalidArgs, $"Unknown verb: '{request.Verb}'. Use wpf_capabilities to list valid verbs.");

        try
        {
            return verb switch
            {
                ActionVerb.Click => DoClick(request),
                ActionVerb.DoubleClick => DoDoubleClick(request),
                ActionVerb.RightClick => DoRightClick(request),
                ActionVerb.Invoke => DoInvokePattern(request),
                ActionVerb.Hover => DoHover(request),
                ActionVerb.SetValue => DoWithElement(request, SetValue, request.Value, "set_value"),
                ActionVerb.ClearValue => DoWithElement(request, (e, _) => { e.Patterns.Value.Pattern.SetValue(string.Empty); return true; }, null, "clear_value"),
                ActionVerb.Type => DoType(request),
                ActionVerb.SendKeys => DoSendKeys(request.Keys ?? string.Empty),
                ActionVerb.Focus => DoWithElement(request, (e, _) => { e.Focus(); return true; }, null, "focus"),
                ActionVerb.Select => DoWithElement(request, (e, _) => { e.Patterns.SelectionItem.Pattern.Select(); return true; }, null, "select"),
                ActionVerb.SelectByText => DoSelectByText(request),
                ActionVerb.SelectByIndex => DoSelectByIndex(request),
                ActionVerb.Toggle => DoWithElement(request, (e, _) => { e.Patterns.Toggle.Pattern.Toggle(); return true; }, null, "toggle"),
                ActionVerb.Check => DoSetChecked(request, true),
                ActionVerb.Uncheck => DoSetChecked(request, false),
                ActionVerb.Expand => DoWithElement(request, (e, _) => { e.Patterns.ExpandCollapse.Pattern.Expand(); return true; }, null, "expand"),
                ActionVerb.Collapse => DoWithElement(request, (e, _) => { e.Patterns.ExpandCollapse.Pattern.Collapse(); return true; }, null, "collapse"),
                ActionVerb.Scroll => DoScroll(request),
                ActionVerb.ScrollIntoView => DoWithElement(request, (e, _) => { e.Patterns.ScrollItem.Pattern.ScrollIntoView(); return true; }, null, "scroll_into_view"),
                ActionVerb.DragDrop => DoDragDrop(request),
                ActionVerb.OpenMenu => DoOpenMenuPath(request.MenuPath ?? string.Empty),
                ActionVerb.OpenContextMenu => DoOpenContextMenu(request),
                ActionVerb.SetSlider => DoWithElement(request, (e, v) => { e.Patterns.RangeValue.Pattern.SetValue(double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)); return true; }, request.Value, "set_slider"),
                ActionVerb.SetDate => DoWithElement(request, SetValue, request.Value, "set_date"),
                ActionVerb.AcceptDialog => _actions.AcceptDialog(),
                ActionVerb.CancelDialog => _actions.CancelDialog(),
                _ => ToolJson.Error(ErrorCodes.InvalidArgs, $"Verb '{verb}' not implemented yet")
            };
        }
        catch (Exception ex)
        {
            var info = _errors.FromUiaException($"wpf_act({verb})", ex);
            _audit.Record("wpf_act", result: "error", error: info.Message, durationMs: stopwatch.ElapsedMilliseconds);
            return ToolJson.Error(info);
        }
    }

    private string DoClick(ActionRequest request)
    {
        var element = ResolveElement(request);
        if (element is null) return ElementNotFound();
        if (element.Patterns.Invoke.IsSupported) element.Patterns.Invoke.Pattern.Invoke();
        else element.Click();
        RecordAction("click", request);
        return ToolJson.OkResult("clicked");
    }

    private string DoDoubleClick(ActionRequest request)
    {
        var element = ResolveElement(request);
        if (element is null) return ElementNotFound();
        element.DoubleClick();
        RecordAction("double_click", request);
        return ToolJson.OkResult("double_clicked");
    }

    private string DoRightClick(ActionRequest request)
    {
        var element = ResolveElement(request);
        if (element is null) return ElementNotFound();
        element.RightClick();
        RecordAction("right_click", request);
        return ToolJson.OkResult("right_clicked");
    }

    private string DoInvokePattern(ActionRequest request)
    {
        var element = ResolveElement(request);
        if (element is null) return ElementNotFound();
        if (!element.Patterns.Invoke.IsSupported)
            return ToolJson.Error(ErrorCodes.PatternNotSupported, "Element does not support Invoke pattern");
        element.Patterns.Invoke.Pattern.Invoke();
        RecordAction("invoke", request);
        return ToolJson.OkResult("invoked");
    }

    private string DoHover(ActionRequest request)
    {
        var element = ResolveElement(request);
        if (element is null) return ElementNotFound();
        var rect = element.BoundingRectangle;
        if (rect.IsEmpty)
            return ToolJson.Error(ErrorCodes.PatternNotSupported, "Element has no bounding rectangle");
        var point = new System.Drawing.Point(
            (int)(rect.X + rect.Width / 2),
            (int)(rect.Y + rect.Height / 2));
        Mouse.MoveTo(point);
        RecordAction("hover", request);
        return ToolJson.OkResult("hovered");
    }

    private string DoWithElement(ActionRequest request,
        Func<AutomationElement, string, bool> action, string? value, string? actionName)
    {
        var element = ResolveElement(request);
        if (element is null) return ElementNotFound();
        action(element, value ?? string.Empty);
        if (actionName is not null) RecordAction(actionName, request, value);
        return ToolJson.OkResult(actionName ?? "ok");
    }

    private string DoType(ActionRequest request)
    {
        if (request.Selector is not null || request.Path is not null)
        {
            var element = ResolveElement(request);
            if (element is null) return ElementNotFound();
            element.Focus();
        }
        if (string.IsNullOrEmpty(request.Text))
            return ToolJson.Error(ErrorCodes.InvalidArgs, "text is required for type verb");
        Keyboard.Type(request.Text);
        RecordAction("type", request, request.Text);
        return ToolJson.OkResult("typed");
    }

    private string DoSendKeys(string keys)
    {
        if (string.IsNullOrWhiteSpace(keys))
            return ToolJson.Error(ErrorCodes.InvalidArgs, "keys is required");
        return _actions.SendKeys(keys);
    }

    private string DoSelectByText(ActionRequest request)
    {
        if (string.IsNullOrEmpty(request.Text))
            return ToolJson.Error(ErrorCodes.InvalidArgs, "text is required");
        var parent = ResolveParent(request);
        var element = _uia.FindElement(new ElementCriteria { Name = request.Text }, parent);
        if (element is null)
            return ToolJson.Error(ErrorCodes.ElementNotFound, $"Item with text '{request.Text}' not found");
        if (element.Patterns.SelectionItem.IsSupported) element.Patterns.SelectionItem.Pattern.Select();
        else element.Click();
        RecordAction("select_by_text", request, request.Text);
        return ToolJson.OkResult("selected");
    }

    private string DoSelectByIndex(ActionRequest request)
    {
        if (!request.Index.HasValue)
            return ToolJson.Error(ErrorCodes.InvalidArgs, "index is required");
        var parent = ResolveParent(request);
        var items = _uia.FindElements(new ElementCriteria { ControlType = ControlTypes.ListItem }, parent);
        if (items.Count == 0) items = _uia.FindElements(new ElementCriteria { ControlType = ControlTypes.TreeItem }, parent);
        if (items.Count == 0) items = _uia.FindElements(new ElementCriteria { ControlType = ControlTypes.DataItem }, parent);
        if (request.Index.Value < 0 || request.Index.Value >= items.Count)
            return ToolJson.Error(ErrorCodes.InvalidArgs, $"Index {request.Index} out of range. Found {items.Count} items.");
        var target = items[request.Index.Value];
        if (target.Patterns.SelectionItem.IsSupported) target.Patterns.SelectionItem.Pattern.Select();
        else target.Click();
        RecordAction("select_by_index", request, request.Index.Value.ToString());
        return ToolJson.OkResult($"selected_index_{request.Index}");
    }

    private string DoSetChecked(ActionRequest request, bool desired)
    {
        var element = ResolveElement(request);
        if (element is null) return ElementNotFound();
        if (!element.Patterns.Toggle.IsSupported)
            return ToolJson.Error(ErrorCodes.PatternNotSupported, "Element does not support Toggle pattern");
        for (int i = 0; i < 3; i++)
        {
            var state = element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault;
            var isOn = state == FlaUI.Core.Definitions.ToggleState.On;
            if (isOn == desired) break;
            element.Patterns.Toggle.Pattern.Toggle();
        }
        RecordAction(desired ? "check" : "uncheck", request);
        return ToolJson.OkResult(desired ? "checked" : "unchecked");
    }

    private string DoScroll(ActionRequest request)
    {
        var element = ResolveElement(request);
        if (element is null) return ElementNotFound();
        if (!element.Patterns.Scroll.IsSupported)
            return ToolJson.Error(ErrorCodes.PatternNotSupported, "Element does not support Scroll pattern");
        var dir = (request.Direction ?? "down").ToLowerInvariant();
        var sp = element.Patterns.Scroll.Pattern;
        switch (dir)
        {
            case "up": sp.Scroll(FlaUI.Core.Definitions.ScrollAmount.NoAmount, FlaUI.Core.Definitions.ScrollAmount.SmallDecrement); break;
            case "down": sp.Scroll(FlaUI.Core.Definitions.ScrollAmount.NoAmount, FlaUI.Core.Definitions.ScrollAmount.SmallIncrement); break;
            case "left": sp.Scroll(FlaUI.Core.Definitions.ScrollAmount.SmallDecrement, FlaUI.Core.Definitions.ScrollAmount.NoAmount); break;
            case "right": sp.Scroll(FlaUI.Core.Definitions.ScrollAmount.SmallIncrement, FlaUI.Core.Definitions.ScrollAmount.NoAmount); break;
            default: return ToolJson.Error(ErrorCodes.InvalidArgs, $"Unknown direction: {dir}");
        }
        RecordAction("scroll", request, dir);
        return ToolJson.OkResult("scrolled");
    }

    private string DoDragDrop(ActionRequest request)
    {
        if (string.IsNullOrEmpty(request.SourceAutomationId) || string.IsNullOrEmpty(request.TargetAutomationId))
            return ToolJson.Error(ErrorCodes.InvalidArgs, "sourceAutomationId and targetAutomationId are required for drag_drop");
        var source = _uia.FindElement(new ElementCriteria { AutomationId = request.SourceAutomationId });
        var target = _uia.FindElement(new ElementCriteria { AutomationId = request.TargetAutomationId });
        if (source is null) return ToolJson.Error(ErrorCodes.ElementNotFound, "Source not found");
        if (target is null) return ToolJson.Error(ErrorCodes.ElementNotFound, "Target not found");
        var sb = source.BoundingRectangle;
        var tb = target.BoundingRectangle;
        var s = new System.Drawing.Point((int)(sb.X + sb.Width / 2), (int)(sb.Y + sb.Height / 2));
        var t = new System.Drawing.Point((int)(tb.X + tb.Width / 2), (int)(tb.Y + tb.Height / 2));
        Mouse.MoveTo(s);
        Mouse.Down(MouseButton.Left);
        Thread.Sleep(100);
        Mouse.MoveTo(t);
        Thread.Sleep(100);
        Mouse.Up(MouseButton.Left);
        return ToolJson.OkResult("drag_dropped");
    }

    private string DoOpenMenuPath(string menuPath)
    {
        if (string.IsNullOrEmpty(menuPath))
            return ToolJson.Error(ErrorCodes.InvalidArgs, "menuPath is required");
        return _actions.OpenMenuPath(menuPath);
    }

    private string DoOpenContextMenu(ActionRequest request)
    {
        if (string.IsNullOrEmpty(request.MenuItem))
            return ToolJson.Error(ErrorCodes.InvalidArgs, "menuItem is required");
        return _actions.OpenContextMenuItem(
            request.MenuItem, request.Selector?.AutomationId, request.Selector?.Name);
    }

    private AutomationElement? ResolveElement(ActionRequest request)
    {
        if (request.Selector is null && string.IsNullOrEmpty(request.Path)) return null;
        return _uia.ResolveSelector(new ElementSelector
        {
            Element = request.Selector,
            Path = request.Path
        });
    }

    private AutomationElement? ResolveParent(ActionRequest request)
    {
        if (request.ParentSelector is null && string.IsNullOrEmpty(request.ParentPath)) return null;
        return _uia.ResolveSelector(new ElementSelector
        {
            Element = request.ParentSelector,
            Path = request.ParentPath
        });
    }

    private void RecordAction(string action, ActionRequest request, string? value = null) =>
        _actions.RecordStep(action, request.Selector, request.Path, value ?? request.Value ?? request.Text);

    private static bool SetValue(AutomationElement e, string v)
    {
        e.Patterns.Value.Pattern.SetValue(v);
        return true;
    }

    private static string ElementNotFound() =>
        ToolJson.Error(ErrorCodes.ElementNotFound, "Element not found");
}
