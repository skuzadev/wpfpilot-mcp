using WpfPilot.Mcp.Core.Dsl;
using WpfPilot.Mcp.Core.Models;

namespace WpfPilot.Mcp.Server.Services;

/// <summary>
/// Builds <see cref="ElementCriteria"/> from flat MCP tool parameters (no recursive JSON Schema).
/// </summary>
internal static class McpSelectorParams
{
    public static ElementCriteria? ToCriteria(
        string? automationId = null,
        string? name = null,
        string? controlType = null,
        string? className = null,
        string? windowTitle = null,
        string? nearAutomationId = null,
        string? nearName = null)
    {
        ElementCriteria? near = null;
        if (!string.IsNullOrWhiteSpace(nearAutomationId) || !string.IsNullOrWhiteSpace(nearName))
        {
            near = new ElementCriteria
            {
                AutomationId = nearAutomationId,
                Name = nearName
            };
        }

        var criteria = new ElementCriteria
        {
            AutomationId = automationId,
            Name = name,
            ControlType = controlType,
            ClassName = className,
            WindowTitle = windowTitle,
            Near = near
        };

        return criteria.IsEmpty ? null : criteria;
    }

    public static ActionRequest ToActionRequest(
        string verb,
        string? automationId = null,
        string? name = null,
        string? controlType = null,
        string? className = null,
        string? path = null,
        string? parentAutomationId = null,
        string? parentName = null,
        string? parentControlType = null,
        string? parentPath = null,
        string? value = null,
        int? index = null,
        string? text = null,
        string? menuPath = null,
        string? menuItem = null,
        string? sourceAutomationId = null,
        string? targetAutomationId = null,
        string? keys = null,
        string? direction = null,
        double? amount = null,
        bool dryRun = false,
        int? timeoutMs = null,
        string? nearAutomationId = null,
        string? nearName = null) =>
        new()
        {
            Verb = verb,
            Selector = ToCriteria(automationId, name, controlType, className, nearAutomationId: nearAutomationId, nearName: nearName),
            Path = path,
            ParentSelector = ToCriteria(parentAutomationId, parentName, parentControlType),
            ParentPath = parentPath,
            Value = value,
            Index = index,
            Text = text,
            MenuPath = menuPath,
            MenuItem = menuItem,
            SourceAutomationId = sourceAutomationId,
            TargetAutomationId = targetAutomationId,
            Keys = keys,
            Direction = direction,
            Amount = amount,
            DryRun = dryRun,
            TimeoutMs = timeoutMs
        };

    public static QueryRequest ToQueryRequest(
        string kind,
        string? automationId = null,
        string? name = null,
        string? controlType = null,
        string? className = null,
        string? path = null,
        int? maxDepth = null,
        int? limit = null,
        string? nearAutomationId = null,
        string? nearName = null) =>
        new()
        {
            Kind = kind,
            Selector = ToCriteria(automationId, name, controlType, className, nearAutomationId: nearAutomationId, nearName: nearName),
            Path = path,
            MaxDepth = maxDepth,
            Limit = limit
        };

    public static WaitRequest ToWaitRequest(
        string condition,
        string? automationId = null,
        string? name = null,
        string? controlType = null,
        string? className = null,
        string? path = null,
        string? value = null,
        int timeoutMs = 5000,
        int pollIntervalMs = 100,
        string? nearAutomationId = null,
        string? nearName = null) =>
        new()
        {
            Condition = condition,
            Selector = ToCriteria(automationId, name, controlType, className, nearAutomationId: nearAutomationId, nearName: nearName),
            Path = path,
            Value = value,
            TimeoutMs = timeoutMs,
            PollIntervalMs = pollIntervalMs
        };

    public static AssertRequest ToAssertRequest(
        string condition,
        string? automationId = null,
        string? name = null,
        string? controlType = null,
        string? className = null,
        string? path = null,
        string? expected = null,
        string? value = null,
        string? nearAutomationId = null,
        string? nearName = null) =>
        new()
        {
            Condition = condition,
            Selector = ToCriteria(automationId, name, controlType, className, nearAutomationId: nearAutomationId, nearName: nearName),
            Path = path,
            Expected = expected,
            Value = value
        };
}
