using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class SelectorTools
{
    private readonly UiaAdapter _uia;
    private readonly SelectorBuilder _selectors;
    private readonly AuditLog _audit;

    public SelectorTools(UiaAdapter uia, SelectorBuilder selectors, AuditLog audit)
    {
        _uia = uia;
        _selectors = selectors;
        _audit = audit;
    }

    [McpServerTool(Name = "wpf_build_selector"), Description("Generate stable selector for an element found by automation id or name. The returned selector includes a Path if every ancestor has a stable AutomationId.")]
    public string BuildSelector(string? automationId = null, string? name = null, string? controlType = null)
    {
        _audit.Record("wpf_build_selector");
        var criteria = new ElementCriteria { AutomationId = automationId, Name = name, ControlType = controlType };
        var element = _uia.FindElement(criteria);
        if (element is null)
            return JsonSerializer.Serialize(new { error = "Element not found." }, JsonOptions.Default);

        var selector = _selectors.BuildSelector(element);
        return JsonSerializer.Serialize(selector, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_resolve_with_strategy"),
     Description("Resolve a selector (path or criteria) and report which strategy was used: " +
        "'path(depth=N,siblings=M)' | 'criteria' | 'fallback' | 'path.miss' | 'path.parse_error'. " +
        "Use this to verify a path-based selector works as expected.")]
    public string ResolveWithStrategy(ElementSelector selector)
    {
        _audit.Record("wpf_resolve_with_strategy", selector.Element, selector.Path);
        var element = _uia.ResolveSelector(selector);
        return JsonSerializer.Serialize(new
        {
            found = element is not null,
            strategy = _uia.LastResolveStrategy ?? "none",
            selector
        }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_validate_selector"), Description("Test whether a selector resolves uniquely.")]
    public string ValidateSelector(string? automationId = null, string? name = null, string? controlType = null, string? className = null)
    {
        _audit.Record("wpf_validate_selector");
        var criteria = new ElementCriteria { AutomationId = automationId, Name = name, ControlType = controlType, ClassName = className };
        var (isUnique, matchCount) = _selectors.ValidateSelectorUniqueness(criteria);

        return JsonSerializer.Serialize(new
        {
            isValid = matchCount > 0,
            isUnique,
            matchCount
        }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_detect_missing_ids"), Description("Find actionable elements missing AutomationId in current window.")]
    public string DetectMissingIds()
    {
        _audit.Record("wpf_detect_missing_ids");
        var allElements = _uia.QueryElements();
        var missing = allElements.Where(e =>
            string.IsNullOrEmpty(e.AutomationId) &&
            IsActionable(e.ControlType)).ToList();

        var result = new
        {
            totalElements = allElements.Count,
            missingCount = missing.Count,
            elements = missing.Select(e => new
            {
                e.Name,
                e.ControlType,
                e.ClassName,
                e.Bounds
            }).Take(50).ToList()
        };

        return JsonSerializer.Serialize(result, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_detect_duplicate_ids"), Description("Find duplicate AutomationIds in current window.")]
    public string DetectDuplicateIds()
    {
        _audit.Record("wpf_detect_duplicate_ids");
        var allElements = _uia.QueryElements();
        var duplicates = allElements
            .Where(e => !string.IsNullOrEmpty(e.AutomationId))
            .GroupBy(e => e.AutomationId)
            .Where(g => g.Count() > 1)
            .Select(g => new
            {
                automationId = g.Key,
                count = g.Count(),
                controlTypes = g.Select(e => e.ControlType).Distinct().ToList()
            }).ToList();

        return JsonSerializer.Serialize(new { duplicateGroups = duplicates.Count, duplicates }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_explain_selector"), Description("Explain how selector is resolved and why it may be brittle.")]
    public string ExplainSelector(string? automationId = null, string? name = null, string? controlType = null, string? className = null)
    {
        _audit.Record("wpf_explain_selector");
        var criteria = new ElementCriteria { AutomationId = automationId, Name = name, ControlType = controlType, ClassName = className };
        var elements = _uia.FindElements(criteria);

        var explanation = new List<string>();
        var stability = "high";

        if (elements.Count == 0)
        {
            explanation.Add("Selector does not match any element.");
            stability = "broken";
        }
        else if (elements.Count > 1)
        {
            explanation.Add($"Selector matches {elements.Count} elements (not unique).");
            stability = "low";
        }
        else
        {
            explanation.Add("Selector resolves to exactly one element.");
        }

        if (!string.IsNullOrEmpty(automationId))
            explanation.Add("Uses AutomationId — most stable strategy.");
        else if (!string.IsNullOrEmpty(name))
        {
            explanation.Add("Uses Name — may change with localization or dynamic text.");
            if (stability == "high") stability = "medium";
        }
        else if (!string.IsNullOrEmpty(className))
        {
            explanation.Add("Uses ClassName only — may match many elements.");
            if (stability != "broken") stability = "low";
        }

        return JsonSerializer.Serialize(new { matchCount = elements.Count, stability, explanation }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_rank_selectors"), Description("Generate multiple selectors ranked by stability for an element.")]
    public string RankSelectors(string? automationId = null, string? name = null)
    {
        _audit.Record("wpf_rank_selectors");
        var criteria = new ElementCriteria { AutomationId = automationId, Name = name };
        var element = _uia.FindElement(criteria);
        if (element is null)
            return JsonSerializer.Serialize(new { error = "Element not found." }, JsonOptions.Default);

        var ranked = _selectors.RankSelectors(element);
        var result = ranked.Select(r => new
        {
            selector = r.selector,
            strategy = r.strategy,
            stabilityScore = r.stability
        }).ToList();

        return JsonSerializer.Serialize(new { selectors = result }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_heal_selector"), Description("Find likely replacement when a selector no longer resolves.")]
    public string HealSelector(string? automationId = null, string? name = null, string? controlType = null, string? className = null)
    {
        _audit.Record("wpf_heal_selector");
        var criteria = new ElementCriteria { AutomationId = automationId, Name = name, ControlType = controlType, ClassName = className };

        // Check if original works
        var element = _uia.FindElement(criteria);
        if (element is not null)
            return JsonSerializer.Serialize(new { status = "selector_still_valid", healed = false }, JsonOptions.Default);

        // Try healing strategies
        var candidates = new List<object>();

        // Strategy 1: Try by controlType + partial name
        if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(controlType))
        {
            var byType = _uia.QueryElements(controlType: controlType);
            var similar = byType.Where(e => e.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true).ToList();
            foreach (var s in similar.Take(3))
            {
                candidates.Add(new { strategy = "partial_name_match", automationId = s.AutomationId, name = s.Name, controlType = s.ControlType });
            }
        }

        // Strategy 2: Try by className
        if (!string.IsNullOrEmpty(className))
        {
            var byClass = _uia.QueryElements(className: className);
            foreach (var s in byClass.Take(3))
            {
                candidates.Add(new { strategy = "className_match", automationId = s.AutomationId, name = s.Name, controlType = s.ControlType });
            }
        }

        // Strategy 3: Similar automation id
        if (!string.IsNullOrEmpty(automationId))
        {
            var allElements = _uia.QueryElements();
            var similar = allElements.Where(e =>
                !string.IsNullOrEmpty(e.AutomationId) &&
                (e.AutomationId.Contains(automationId, StringComparison.OrdinalIgnoreCase) ||
                 automationId.Contains(e.AutomationId, StringComparison.OrdinalIgnoreCase))).Take(3);
            foreach (var s in similar)
            {
                candidates.Add(new { strategy = "similar_automationId", automationId = s.AutomationId, name = s.Name, controlType = s.ControlType });
            }
        }

        return JsonSerializer.Serialize(new { status = "selector_broken", healed = candidates.Count > 0, candidates }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_find_similar_element"), Description("Locate element by previous metadata: text, control type, position, siblings.")]
    public string FindSimilarElement(string? controlType = null, string? partialName = null, string? nearAutomationId = null)
    {
        _audit.Record("wpf_find_similar_element");
        var allElements = _uia.QueryElements();
        var candidates = allElements.AsEnumerable();

        if (!string.IsNullOrEmpty(controlType))
            candidates = candidates.Where(e => e.ControlType == controlType);

        if (!string.IsNullOrEmpty(partialName))
            candidates = candidates.Where(e => e.Name?.Contains(partialName, StringComparison.OrdinalIgnoreCase) == true);

        var results = candidates.Take(10).ToList();

        if (!string.IsNullOrEmpty(nearAutomationId))
        {
            var nearElement = allElements.FirstOrDefault(e => e.AutomationId == nearAutomationId);
            if (nearElement?.Bounds is not null)
            {
                results = results
                    .Where(e => e.Bounds is not null)
                    .OrderBy(e => Math.Abs(e.Bounds!.X - nearElement.Bounds.X) + Math.Abs(e.Bounds!.Y - nearElement.Bounds.Y))
                    .Take(5)
                    .ToList();
            }
        }

        return JsonSerializer.Serialize(new { count = results.Count, elements = results }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_get_selector_candidates"), Description("Return all possible selector strategies for an element.")]
    public string GetSelectorCandidates(string? automationId = null, string? name = null)
    {
        _audit.Record("wpf_get_selector_candidates");
        var criteria = new ElementCriteria { AutomationId = automationId, Name = name };
        var element = _uia.FindElement(criteria);
        if (element is null)
            return JsonSerializer.Serialize(new { error = "Element not found." }, JsonOptions.Default);

        var candidates = new List<object>();
        var elemAutomationId = element.Properties.AutomationId.ValueOrDefault;
        var elemName = element.Properties.Name.ValueOrDefault;
        var elemControlType = element.Properties.ControlType.ValueOrDefault.ToString();
        var elemClassName = element.Properties.ClassName.ValueOrDefault;

        if (!string.IsNullOrEmpty(elemAutomationId))
            candidates.Add(new { strategy = "AutomationId", selector = new { automationId = elemAutomationId }, stability = 95 });
        if (!string.IsNullOrEmpty(elemAutomationId) && !string.IsNullOrEmpty(elemControlType))
            candidates.Add(new { strategy = "AutomationId+ControlType", selector = new { automationId = elemAutomationId, controlType = elemControlType }, stability = 98 });
        if (!string.IsNullOrEmpty(elemName))
            candidates.Add(new { strategy = "Name", selector = new { name = elemName }, stability = 60 });
        if (!string.IsNullOrEmpty(elemName) && !string.IsNullOrEmpty(elemControlType))
            candidates.Add(new { strategy = "Name+ControlType", selector = new { name = elemName, controlType = elemControlType }, stability = 70 });
        if (!string.IsNullOrEmpty(elemClassName) && !string.IsNullOrEmpty(elemControlType))
            candidates.Add(new { strategy = "ClassName+ControlType", selector = new { className = elemClassName, controlType = elemControlType }, stability = 40 });

        return JsonSerializer.Serialize(new { candidates }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_detect_brittle_selectors"), Description("Flag selectors that rely on index, coordinates, generated names, or unstable text.")]
    public string DetectBrittleSelectors()
    {
        _audit.Record("wpf_detect_brittle_selectors");
        var allElements = _uia.QueryElements();
        var brittle = new List<object>();

        foreach (var el in allElements)
        {
            if (IsActionable(el.ControlType))
            {
                var issues = new List<string>();
                if (string.IsNullOrEmpty(el.AutomationId))
                    issues.Add("missing_automation_id");
                if (string.IsNullOrEmpty(el.Name) && string.IsNullOrEmpty(el.AutomationId))
                    issues.Add("no_stable_identifier");
                if (el.Name?.Contains("System.") == true || el.Name?.Contains("Window") == true)
                    issues.Add("generated_name");

                if (issues.Count > 0)
                {
                    brittle.Add(new { controlType = el.ControlType, name = el.Name, automationId = el.AutomationId, className = el.ClassName, issues });
                }
            }
        }

        return JsonSerializer.Serialize(new { totalActionable = allElements.Count(e => IsActionable(e.ControlType)), brittleCount = brittle.Count, brittle = brittle.Take(50).ToList() }, JsonOptions.Default);
    }

    private static bool IsActionable(string? controlType)
    {
        if (string.IsNullOrEmpty(controlType)) return false;
        return controlType is "Button" or "TextBox" or "ComboBox" or "CheckBox"
            or "RadioButton" or "MenuItem" or "Tab" or "TabItem" or "ListItem"
            or "DataItem" or "TreeItem" or "Slider" or "Hyperlink";
    }
}
