using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Core.Selectors;

namespace WpfPilot.Mcp.Server.Services;

public sealed class UiaAdapter
{
    private readonly SessionManager _session;
    private readonly PathResolver _pathResolver = new();
    private int _elementCounter;
    private string? _lastResolveStrategy;

    public UiaAdapter(SessionManager session)
    {
        _session = session;
    }

    public string? LastResolveStrategy => _lastResolveStrategy;

    public UiSnapshot CaptureSnapshot(AutomationElement? root = null, int maxDepth = 10)
    {
        EnsureAttached();
        Interlocked.Exchange(ref _elementCounter, 0);

        var window = _session.ActiveWindow
            ?? throw new InvalidOperationException("Could not get main window. The application may be busy or not responding.");
        var startElement = root ?? window;

        var tree = BuildTree(startElement, maxDepth, 0);
        var allElements = FlattenTree(tree);

        var automationIds = allElements
            .Where(e => !string.IsNullOrEmpty(e.AutomationId))
            .GroupBy(e => e.AutomationId)
            .Where(g => g.Count() > 1)
            .Count();

        var missingIds = allElements.Count(e =>
            string.IsNullOrEmpty(e.AutomationId) &&
            IsActionableControlType(e.ControlType));

        var missingNames = allElements.Count(e =>
            string.IsNullOrEmpty(e.Name) &&
            string.IsNullOrEmpty(e.AutomationId) &&
            IsActionableControlType(e.ControlType));

        return new UiSnapshot
        {
            SessionId = _session.SessionId,
            TimestampUtc = DateTime.UtcNow,
            Window = new WindowInfo
            {
                Title = window.Title,
                ProcessId = _session.Application!.ProcessId,
                Handle = window.Properties.NativeWindowHandle.ValueOrDefault.ToString()
            },
            Tree = tree,
            Diagnostics = new SnapshotDiagnostics
            {
                MissingAutomationIds = missingIds,
                DuplicateAutomationIds = automationIds,
                MissingNames = missingNames,
                TotalElements = allElements.Count
            }
        };
    }

    public List<AutomationElement> FindElements(ElementCriteria criteria, AutomationElement? root = null)
    {
        EnsureAttached();
        var searchRoot = root ?? _session.ActiveWindow
            ?? throw new InvalidOperationException("Could not get main window.");
        var condition = BuildCondition(criteria);
        return searchRoot.FindAll(FlaUI.Core.Definitions.TreeScope.Descendants, condition).ToList();
    }

    public AutomationElement? FindElement(ElementCriteria criteria, AutomationElement? root = null)
    {
        EnsureAttached();
        var searchRoot = root ?? _session.ActiveWindow
            ?? throw new InvalidOperationException("Could not get main window.");
        var condition = BuildCondition(criteria);
        return searchRoot.FindFirst(FlaUI.Core.Definitions.TreeScope.Descendants, condition);
    }

    public AutomationElement? ResolveSelector(ElementSelector selector)
    {
        EnsureAttached();
        _lastResolveStrategy = null;

        if (!string.IsNullOrEmpty(selector.Path))
        {
            var parsed = ElementPath.TryParse(selector.Path);
            if (parsed is null)
            {
                _lastResolveStrategy = "path.parse_error";
            }
            else
            {
                var root = _session.ActiveWindow;
                var resolved = _pathResolver.Resolve(parsed, root!);
                if (resolved is not null)
                {
                    _lastResolveStrategy = $"path(depth={resolved.Depth + 1},siblings={resolved.SiblingCount})";
                    return resolved.Element;
                }
                _lastResolveStrategy = "path.miss";
            }
        }

        if (selector.Element is not null)
        {
            var element = FindElement(selector.Element);
            if (element is not null)
            {
                _lastResolveStrategy = "criteria";
                return element;
            }
        }

        if (selector.Fallbacks is not null)
        {
            foreach (var fallback in selector.Fallbacks)
            {
                var element = FindElement(fallback);
                if (element is not null)
                {
                    _lastResolveStrategy = "fallback";
                    return element;
                }
            }
        }

        return null;
    }

    public List<UiElement> QueryElements(string? automationId = null, string? name = null, string? controlType = null, string? className = null)
    {
        EnsureAttached();
        var window = _session.ActiveWindow
            ?? throw new InvalidOperationException("Could not get main window.");
        var cf = _session.Automation!.ConditionFactory;
        var conditions = new List<ConditionBase>();

        if (!string.IsNullOrEmpty(automationId))
            conditions.Add(cf.ByAutomationId(automationId));
        if (!string.IsNullOrEmpty(name))
            conditions.Add(cf.ByName(name));
        if (!string.IsNullOrEmpty(controlType) && Enum.TryParse<ControlType>(controlType, true, out var ct))
            conditions.Add(cf.ByControlType(ct));
        if (!string.IsNullOrEmpty(className))
            conditions.Add(cf.ByClassName(className));

        ConditionBase finalCondition = conditions.Count switch
        {
            0 => TrueCondition.Default,
            1 => conditions[0],
            _ => new AndCondition(conditions.ToArray())
        };

        var elements = window.FindAll(TreeScope.Descendants, finalCondition);
        return elements.Select(MapElement).ToList();
    }

    public UiElement MapElement(AutomationElement element)
    {
        var patterns = new List<string>();
        try
        {
            if (element.Patterns.Invoke.IsSupported) patterns.Add("Invoke");
            if (element.Patterns.Value.IsSupported) patterns.Add("Value");
            if (element.Patterns.Toggle.IsSupported) patterns.Add("Toggle");
            if (element.Patterns.Selection.IsSupported) patterns.Add("Selection");
            if (element.Patterns.SelectionItem.IsSupported) patterns.Add("SelectionItem");
            if (element.Patterns.ExpandCollapse.IsSupported) patterns.Add("ExpandCollapse");
            if (element.Patterns.ScrollItem.IsSupported) patterns.Add("ScrollItem");
            if (element.Patterns.Grid.IsSupported) patterns.Add("Grid");
            if (element.Patterns.RangeValue.IsSupported) patterns.Add("RangeValue");
        }
        catch { }

        string? value = null;
        try
        {
            if (element.Patterns.Value.IsSupported)
                value = element.Patterns.Value.Pattern.Value.ValueOrDefault;
        }
        catch { }

        var bounds = element.BoundingRectangle;

        return new UiElement
        {
            Id = $"e{Interlocked.Increment(ref _elementCounter)}",
            AutomationId = element.Properties.AutomationId.ValueOrDefault,
            Name = element.Properties.Name.ValueOrDefault,
            ControlType = element.Properties.ControlType.ValueOrDefault.ToString(),
            ClassName = element.Properties.ClassName.ValueOrDefault,
            IsEnabled = element.Properties.IsEnabled.ValueOrDefault,
            IsOffscreen = element.Properties.IsOffscreen.ValueOrDefault,
            Bounds = new ElementBounds
            {
                X = bounds.X,
                Y = bounds.Y,
                Width = bounds.Width,
                Height = bounds.Height
            },
            Patterns = patterns,
            Value = value
        };
    }

    private List<UiElement> BuildTree(AutomationElement element, int maxDepth, int currentDepth)
    {
        var result = new List<UiElement>();
        if (currentDepth > maxDepth) return result;

        var uiElement = MapElement(element);

        try
        {
            var children = element.FindAll(TreeScope.Children, TrueCondition.Default);
            foreach (var child in children)
            {
                var childElements = BuildTree(child, maxDepth, currentDepth + 1);
                if (childElements.Count == 1)
                {
                    uiElement.Children.Add(childElements[0]);
                }
                else
                {
                    uiElement.Children.AddRange(childElements);
                }
            }
        }
        catch { }

        result.Add(uiElement);
        return result;
    }

    private ConditionBase BuildCondition(ElementCriteria criteria)
    {
        var cf = _session.Automation!.ConditionFactory;
        var conditions = new List<ConditionBase>();

        if (!string.IsNullOrEmpty(criteria.AutomationId))
            conditions.Add(cf.ByAutomationId(criteria.AutomationId));
        if (!string.IsNullOrEmpty(criteria.Name))
            conditions.Add(cf.ByName(criteria.Name));
        if (!string.IsNullOrEmpty(criteria.ControlType) && Enum.TryParse<ControlType>(criteria.ControlType, true, out var ct))
            conditions.Add(cf.ByControlType(ct));
        if (!string.IsNullOrEmpty(criteria.ClassName))
            conditions.Add(cf.ByClassName(criteria.ClassName));

        return conditions.Count switch
        {
            0 => TrueCondition.Default,
            1 => conditions[0],
            _ => new AndCondition(conditions.ToArray())
        };
    }

    private static List<UiElement> FlattenTree(List<UiElement> tree)
    {
        var result = new List<UiElement>();
        foreach (var element in tree)
        {
            result.Add(element);
            result.AddRange(FlattenTree(element.Children));
        }
        return result;
    }

    private static bool IsActionableControlType(string? controlType)
    {
        if (string.IsNullOrEmpty(controlType)) return false;
        return controlType is "Button" or "TextBox" or "ComboBox" or "CheckBox"
            or "RadioButton" or "MenuItem" or "Tab" or "TabItem" or "ListItem"
            or "DataItem" or "TreeItem" or "Slider" or "Hyperlink";
    }

    private void EnsureAttached()
    {
        if (!_session.IsAttached)
            throw new InvalidOperationException("No app attached. Use wpf_attach or wpf_launch_app first.");
    }
}
