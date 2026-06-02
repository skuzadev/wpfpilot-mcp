using System.Text;
using FlaUI.Core.AutomationElements;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Core.Selectors;

namespace WpfPilot.Mcp.Server.Services;

public sealed class SelectorBuilder
{
    private readonly UiaAdapter _uiaAdapter;

    public SelectorBuilder(UiaAdapter uiaAdapter)
    {
        _uiaAdapter = uiaAdapter;
    }

    public ElementSelector BuildSelector(AutomationElement element)
    {
        var automationId = element.Properties.AutomationId.ValueOrDefault;
        var name = element.Properties.Name.ValueOrDefault;
        var controlType = element.Properties.ControlType.ValueOrDefault.ToString();
        var className = element.Properties.ClassName.ValueOrDefault;

        var primary = new ElementCriteria();
        var fallbacks = new List<ElementCriteria>();

        if (!string.IsNullOrEmpty(automationId))
        {
            primary.AutomationId = automationId;
            primary.ControlType = controlType;

            if (!string.IsNullOrEmpty(name))
            {
                fallbacks.Add(new ElementCriteria
                {
                    Name = name,
                    ControlType = controlType
                });
            }
        }
        else if (!string.IsNullOrEmpty(name))
        {
            primary.Name = name;
            primary.ControlType = controlType;

            if (!string.IsNullOrEmpty(className))
            {
                fallbacks.Add(new ElementCriteria
                {
                    ClassName = className,
                    ControlType = controlType
                });
            }
        }
        else
        {
            primary.ClassName = className;
            primary.ControlType = controlType;
        }

        var path = TryBuildPath(element);
        return new ElementSelector
        {
            Element = primary,
            Path = path,
            Fallbacks = fallbacks.Count > 0 ? fallbacks : null
        };
    }

    /// <summary>
    /// Builds an xpath-like path to the element by walking up its ancestor chain.
    /// Each ancestor contributes a TypeName or TypeName#AutomationId segment. The path is
    /// only returned if every ancestor contributes a stable identifier; otherwise null.
    /// </summary>
    public string? TryBuildPath(AutomationElement element, int maxDepth = 8)
    {
        var segments = new List<string>();
        var current = element;
        int depth = 0;
        while (current is not null && depth < maxDepth)
        {
            var type = current.Properties.ControlType.ValueOrDefault.ToString();
            if (string.IsNullOrEmpty(type)) return null;
            var id = current.Properties.AutomationId.ValueOrDefault;
            if (string.IsNullOrEmpty(id)) return null;
            segments.Insert(0, $"{type}#{id}");
            current = current.Parent;
            depth++;
        }
        if (segments.Count == 0) return null;
        return string.Join("/", segments);
    }

    public bool ValidateSelector(ElementSelector selector)
    {
        var element = _uiaAdapter.ResolveSelector(selector);
        return element is not null;
    }

    public (bool isUnique, int matchCount) ValidateSelectorUniqueness(ElementCriteria criteria)
    {
        var elements = _uiaAdapter.FindElements(criteria);
        return (elements.Count == 1, elements.Count);
    }

    public List<(ElementSelector selector, string strategy, int stability)> RankSelectors(AutomationElement element)
    {
        var results = new List<(ElementSelector selector, string strategy, int stability)>();
        var automationId = element.Properties.AutomationId.ValueOrDefault;
        var name = element.Properties.Name.ValueOrDefault;
        var controlType = element.Properties.ControlType.ValueOrDefault.ToString();
        var className = element.Properties.ClassName.ValueOrDefault;

        var path = TryBuildPath(element);
        if (!string.IsNullOrEmpty(path))
        {
            results.Add((new ElementSelector { Path = path }, "Path", 99));
        }

        if (!string.IsNullOrEmpty(automationId) && !string.IsNullOrEmpty(controlType))
        {
            var sel = new ElementSelector { Element = new ElementCriteria { AutomationId = automationId, ControlType = controlType } };
            results.Add((sel, "AutomationId+ControlType", 98));
        }

        if (!string.IsNullOrEmpty(automationId))
        {
            var sel = new ElementSelector { Element = new ElementCriteria { AutomationId = automationId } };
            results.Add((sel, "AutomationId", 95));
        }

        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(controlType))
        {
            var sel = new ElementSelector { Element = new ElementCriteria { Name = name, ControlType = controlType } };
            results.Add((sel, "Name+ControlType", 70));
        }

        if (!string.IsNullOrEmpty(className) && !string.IsNullOrEmpty(controlType))
        {
            var sel = new ElementSelector { Element = new ElementCriteria { ClassName = className, ControlType = controlType } };
            results.Add((sel, "ClassName+ControlType", 40));
        }

        return results.OrderByDescending(r => r.stability).ToList();
    }
}
