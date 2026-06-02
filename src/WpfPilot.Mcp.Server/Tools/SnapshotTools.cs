using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class SnapshotTools
{
    private readonly UiaAdapter _uia;
    private readonly AuditLog _audit;

    public SnapshotTools(SessionManager session, UiaAdapter uia, AuditLog audit)
    {
        _ = session;
        _uia = uia;
        _audit = audit;
    }

    [McpServerTool(Name = "wpf_snapshot"), Description("Return compact UI tree for the active window.")]
    public string Snapshot(int maxDepth = 5)
    {
        _audit.Record("wpf_snapshot");
        var snapshot = _uia.CaptureSnapshot(maxDepth: maxDepth);
        return JsonSerializer.Serialize(snapshot, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_snapshot_element"), Description("Return UI subtree rooted at an element (selector or path).")]
    public string SnapshotElement(string? automationId = null, string? name = null, string? path = null, int maxDepth = 3)
    {
        _audit.Record("wpf_snapshot_element", new ElementCriteria { AutomationId = automationId, Name = name });
        var element = _uia.ResolveSelector(new ElementSelector
        {
            Element = new ElementCriteria { AutomationId = automationId, Name = name },
            Path = path
        });
        if (element is null)
            return ToolJson.Error(ErrorCodes.ElementNotFound, "Element not found.");

        var snapshot = _uia.CaptureSnapshot(element, maxDepth);
        return JsonSerializer.Serialize(snapshot, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_diff_snapshot"), Description("Compare two snapshots (JSON) and report added/removed/changed elements.")]
    public string DiffSnapshot(string beforeJson, string afterJson)
    {
        _audit.Record("wpf_diff_snapshot");
        try
        {
            var before = JsonSerializer.Deserialize<UiSnapshot>(beforeJson, JsonOptions.Default);
            var after = JsonSerializer.Deserialize<UiSnapshot>(afterJson, JsonOptions.Default);
            if (before is null || after is null)
                return ToolJson.Error(ErrorCodes.InvalidArgs, "Could not parse snapshots.");

            var beforeElements = FlattenElements(before.Tree);
            var afterElements = FlattenElements(after.Tree);
            var beforeIds = beforeElements.Select(e => e.AutomationId ?? e.Id).ToHashSet();
            var afterIds = afterElements.Select(e => e.AutomationId ?? e.Id).ToHashSet();
            var added = afterIds.Except(beforeIds).ToList();
            var removed = beforeIds.Except(afterIds).ToList();
            var common = beforeIds.Intersect(afterIds).ToList();
            var changed = new List<object>();
            foreach (var id in common)
            {
                var b = beforeElements.First(e => (e.AutomationId ?? e.Id) == id);
                var a = afterElements.First(e => (e.AutomationId ?? e.Id) == id);
                if (b.IsEnabled != a.IsEnabled || b.Value != a.Value || b.Name != a.Name)
                    changed.Add(new { id, before = new { b.IsEnabled, b.Value, b.Name }, after = new { a.IsEnabled, a.Value, a.Name } });
            }

            return ToolJson.Ok(new { added, removed, changed, addedCount = added.Count, removedCount = removed.Count, changedCount = changed.Count });
        }
        catch (Exception ex)
        {
            return ToolJson.Error(ErrorCodes.Internal, ex.Message, ex.ToString());
        }
    }

    [McpServerTool(Name = "wpf_watch_ui_changes"), Description("Poll the UI tree for changes over durationMs.")]
    public string WatchUiChanges(int durationMs = 3000, int pollIntervalMs = 500)
    {
        _audit.Record("wpf_watch_ui_changes");
        var changes = new List<object>();
        var sw = Stopwatch.StartNew();
        string? lastSnapshot = null;
        int iteration = 0;

        while (sw.ElapsedMilliseconds < durationMs)
        {
            try
            {
                var snapshot = _uia.CaptureSnapshot(maxDepth: 3);
                var currentJson = JsonSerializer.Serialize(snapshot.Tree, JsonOptions.Default);
                if (lastSnapshot is not null && currentJson != lastSnapshot)
                    changes.Add(new { timestampMs = sw.ElapsedMilliseconds, iteration, changeDetected = true });
                lastSnapshot = currentJson;
                iteration++;
            }
            catch { }
            Thread.Sleep(pollIntervalMs);
        }

        return ToolJson.Ok(new { durationMs, totalIterations = iteration, changesDetected = changes.Count, changes });
    }

    private static List<UiElement> FlattenElements(List<UiElement> tree)
    {
        var result = new List<UiElement>();
        foreach (var el in tree)
        {
            result.Add(el);
            if (el.Children.Count > 0)
                result.AddRange(FlattenElements(el.Children));
        }
        return result;
    }
}
