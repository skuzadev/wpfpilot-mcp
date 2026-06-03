using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Dsl;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class QueryTools
{
    private readonly UiaAdapter _uia;
    private readonly AuditLog _audit;

    public QueryTools(UiaAdapter uia, AuditLog audit)
    {
        _uia = uia;
        _audit = audit;
    }

    [McpServerTool(Name = "wpf_query"),
     Description("Generic verb-driven read. Pass selector fields flat (automationId, name, controlType, className, path). Examples: " +
        "kind=text, automationId=lblStatus | kind=bounds, name=Submit | kind=children, controlType=DataGrid, maxDepth=2")]
    public string Query(
        string kind,
        string? automationId = null,
        string? name = null,
        string? controlType = null,
        string? className = null,
        string? path = null,
        int? maxDepth = null,
        int? limit = null,
        string? nearAutomationId = null,
        string? nearName = null)
    {
        var request = McpSelectorParams.ToQueryRequest(
            kind, automationId, name, controlType, className, path, maxDepth, limit, nearAutomationId, nearName);
        var sw = Stopwatch.StartNew();
        _audit.Record("wpf_query", request.Selector, request.Path, new Dictionary<string, object?>
        {
            ["kind"] = request.Kind,
            ["maxDepth"] = request.MaxDepth,
            ["limit"] = request.Limit
        });

        if (!QueryKindCatalog.TryParse(request.Kind, out var queryKind))
            return ToolJson.Error(ErrorCodes.InvalidArgs, $"Unknown kind: '{request.Kind}'. Use wpf_capabilities to list valid kinds.");

        try
        {
            var element = ResolveElement(request);
            return queryKind switch
            {
                QueryKind.Text => QueryText(element),
                QueryKind.Value => QueryValue(element),
                QueryKind.State => QueryState(element),
                QueryKind.Bounds => QueryBounds(element),
                QueryKind.Patterns => QueryPatterns(element),
                QueryKind.Properties => QueryProperties(element),
                QueryKind.Selection => QuerySelection(element),
                QueryKind.Children => QueryChildren(request, element),
                QueryKind.Ancestors => QueryAncestors(element, request.Limit ?? 32),
                QueryKind.Siblings => QuerySiblings(element, request.Limit ?? 64),
                QueryKind.Existence => Existence(element),
                QueryKind.Count => Count(request, element),
                QueryKind.Find => FindFirst(request),
                QueryKind.FindAll => FindAll(request),
                _ => ToolJson.Error(ErrorCodes.InvalidArgs, $"Kind '{queryKind}' not implemented yet")
            };
        }
        catch (Exception ex)
        {
            return ToolJson.Error(ErrorCodes.Internal, $"wpf_query({queryKind}) failed: {ex.Message}", ex.ToString());
        }
    }

    private AutomationElement? ResolveElement(QueryRequest request)
    {
        if (request.Selector is null && string.IsNullOrEmpty(request.Path)) return null;
        return _uia.ResolveSelector(new ElementSelector
        {
            Element = request.Selector,
            Path = request.Path
        });
    }

    private string QueryText(AutomationElement? element)
    {
        if (element is null) return Error("Element not found");
        string? value = null;
        try { if (element.Patterns.Value.IsSupported) value = element.Patterns.Value.Pattern.Value.ValueOrDefault; } catch { }
        var name = element.Properties.Name.ValueOrDefault;
        return Ok(new { text = value ?? name ?? string.Empty });
    }

    private string QueryValue(AutomationElement? element)
    {
        if (element is null) return Error("Element not found");
        if (!element.Patterns.Value.IsSupported) return Error("Element does not support Value pattern");
        var v = element.Patterns.Value.Pattern.Value.ValueOrDefault ?? string.Empty;
        return Ok(new { value = v });
    }

    private string QueryState(AutomationElement? element)
    {
        if (element is null) return Error("Element not found");
        var result = new Dictionary<string, object?>
        {
            ["enabled"] = element.Properties.IsEnabled.ValueOrDefault,
            ["visible"] = !element.Properties.IsOffscreen.ValueOrDefault,
            ["focused"] = element.Properties.HasKeyboardFocus.ValueOrDefault
        };
        try { if (element.Patterns.Toggle.IsSupported) result["toggleState"] = element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault.ToString(); } catch { }
        try { if (element.Patterns.ExpandCollapse.IsSupported) result["expandState"] = element.Patterns.ExpandCollapse.Pattern.ExpandCollapseState.ValueOrDefault.ToString(); } catch { }
        try { if (element.Patterns.SelectionItem.IsSupported) result["isSelected"] = element.Patterns.SelectionItem.Pattern.IsSelected.ValueOrDefault; } catch { }
        return Ok(result);
    }

    private string QueryBounds(AutomationElement? element)
    {
        if (element is null) return Error("Element not found");
        var r = element.BoundingRectangle;
        return Ok(new
        {
            x = r.X,
            y = r.Y,
            width = r.Width,
            height = r.Height,
            centerX = r.X + r.Width / 2,
            centerY = r.Y + r.Height / 2,
            empty = r.IsEmpty
        });
    }

    private string QueryPatterns(AutomationElement? element)
    {
        if (element is null) return Error("Element not found");
        var supported = new List<string>();
        CheckPattern(element.Patterns.Invoke, "Invoke", supported);
        CheckPattern(element.Patterns.Value, "Value", supported);
        CheckPattern(element.Patterns.Toggle, "Toggle", supported);
        CheckPattern(element.Patterns.Selection, "Selection", supported);
        CheckPattern(element.Patterns.SelectionItem, "SelectionItem", supported);
        CheckPattern(element.Patterns.ExpandCollapse, "ExpandCollapse", supported);
        CheckPattern(element.Patterns.Scroll, "Scroll", supported);
        CheckPattern(element.Patterns.ScrollItem, "ScrollItem", supported);
        CheckPattern(element.Patterns.RangeValue, "RangeValue", supported);
        CheckPattern(element.Patterns.Window, "Window", supported);
        CheckPattern(element.Patterns.Text, "Text", supported);
        CheckPattern(element.Patterns.Table, "Table", supported);
        CheckPattern(element.Patterns.Grid, "Grid", supported);
        return Ok(new { supported });
    }

    private static void CheckPattern(dynamic p, string name, List<string> bucket)
    {
        try { if (p.IsSupported) bucket.Add(name); } catch { }
    }

    private string QueryProperties(AutomationElement? element)
    {
        if (element is null) return Error("Element not found");
        var p = element.Properties;
        return Ok(new
        {
            automationId = p.AutomationId.ValueOrDefault,
            name = p.Name.ValueOrDefault,
            controlType = p.ControlType.ValueOrDefault.ToString(),
            className = p.ClassName.ValueOrDefault,
            frameworkId = p.FrameworkId.ValueOrDefault,
            processId = p.ProcessId.ValueOrDefault,
            isEnabled = p.IsEnabled.ValueOrDefault,
            isOffscreen = p.IsOffscreen.ValueOrDefault,
            hasKeyboardFocus = p.HasKeyboardFocus.ValueOrDefault,
            isPassword = p.IsPassword.ValueOrDefault,
            isContentElement = p.IsContentElement.ValueOrDefault,
            isControlElement = p.IsControlElement.ValueOrDefault,
            helpText = p.HelpText.ValueOrDefault,
            acceleratedKey = p.AcceleratorKey.ValueOrDefault,
            accessKey = p.AccessKey.ValueOrDefault
        });
    }

    private string QuerySelection(AutomationElement? element)
    {
        if (element is null) return Error("Element not found");
        if (!element.Patterns.Selection.IsSupported) return Error("Element does not support Selection pattern");
        var sel = element.Patterns.Selection.Pattern.Selection.ValueOrDefault;
        if (sel is null) return Ok(new { items = Array.Empty<object>() });
        var items = sel.Select(s => new
        {
            name = s.Properties.Name.ValueOrDefault,
            automationId = s.Properties.AutomationId.ValueOrDefault,
            controlType = s.Properties.ControlType.ValueOrDefault.ToString()
        });
        return Ok(new { items });
    }

    private string QueryChildren(QueryRequest request, AutomationElement? element)
    {
        if (element is null) return Error("Element not found");
        var depth = request.MaxDepth ?? 2;
        var limit = request.Limit ?? 100;
        var snapshot = _uia.CaptureSnapshot(element, depth);
        var flat = Flatten(snapshot.Tree, limit);
        return Ok(new { count = flat.Count, children = flat });
    }

    private string QueryAncestors(AutomationElement? element, int limit)
    {
        if (element is null) return Error("Element not found");
        var ancestors = new List<object>();
        var current = element.Parent;
        int n = 0;
        while (current is not null && n < limit)
        {
            ancestors.Add(new
            {
                controlType = current.Properties.ControlType.ValueOrDefault.ToString(),
                name = current.Properties.Name.ValueOrDefault,
                automationId = current.Properties.AutomationId.ValueOrDefault
            });
            current = current.Parent;
            n++;
        }
        return Ok(new { count = ancestors.Count, ancestors });
    }

    private string QuerySiblings(AutomationElement? element, int limit)
    {
        if (element is null) return Error("Element not found");
        var parent = element.Parent;
        if (parent is null) return Ok(new { count = 0, siblings = Array.Empty<object>() });
        var siblings = new List<object>();
        foreach (var child in parent.FindAllChildren())
        {
            if (siblings.Count >= limit) break;
            siblings.Add(new
            {
                controlType = child.Properties.ControlType.ValueOrDefault.ToString(),
                name = child.Properties.Name.ValueOrDefault,
                automationId = child.Properties.AutomationId.ValueOrDefault
            });
        }
        return Ok(new { count = siblings.Count, siblings });
    }

    private string Existence(AutomationElement? element)
    {
        return Ok(new { exists = element is not null });
    }

    private string FindFirst(QueryRequest request)
    {
        if (request.Selector is null)
            return ToolJson.Error(ErrorCodes.InvalidArgs, "selector is required for find");
        var matches = _uia.QueryElements(
            request.Selector.AutomationId,
            request.Selector.Name,
            request.Selector.ControlType,
            request.Selector.ClassName);
        var first = matches.FirstOrDefault();
        if (first is null)
            return ToolJson.Error(ErrorCodes.ElementNotFound, "No matching element found.");
        return Ok(first);
    }

    private string FindAll(QueryRequest request)
    {
        if (request.Selector is null)
            return ToolJson.Error(ErrorCodes.InvalidArgs, "selector is required for find_all");
        var matches = _uia.QueryElements(
            request.Selector.AutomationId,
            request.Selector.Name,
            request.Selector.ControlType,
            request.Selector.ClassName);
        var limit = request.Limit ?? 200;
        if (matches.Count > limit)
            matches = matches.Take(limit).ToList();
        return Ok(new { count = matches.Count, elements = matches });
    }

    private string Count(QueryRequest request, AutomationElement? element)
    {
        AutomationElement? root = element;
        if (root is null && (request.Selector is not null || !string.IsNullOrEmpty(request.Path)))
            return Error("Element not found");
        if (root is null)
        {
            var snap = _uia.CaptureSnapshot(maxDepth: 1);
            return Ok(new { count = snap.Tree.Count });
        }
        var count = 0;
        foreach (var _ in root.FindAllChildren()) count++;
        return Ok(new { count });
    }

    private static List<object> Flatten(List<UiElement> tree, int limit)
    {
        var result = new List<object>();
        void Walk(IReadOnlyList<UiElement> nodes)
        {
            foreach (var n in nodes)
            {
                if (result.Count >= limit) return;
                result.Add(new
                {
                    controlType = n.ControlType,
                    name = n.Name,
                    automationId = n.AutomationId
                });
                if (n.Children is { Count: > 0 }) Walk(n.Children);
                if (result.Count >= limit) return;
            }
        }
        Walk(tree);
        return result;
    }

    private static string Ok(object body) => ToolJson.Ok(body);

    private static string Error(string message) =>
        ToolJson.Error(ErrorCodes.ElementNotFound, message);
}
