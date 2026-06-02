using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class RecordingTools
{
    private readonly RecordingService _recording;
    private readonly UiaAdapter _uia;
    private readonly AuditLog _audit;

    public RecordingTools(RecordingService recording, UiaAdapter uia, AuditLog audit)
    {
        _recording = recording;
        _uia = uia;
        _audit = audit;
    }

    [McpServerTool(Name = "wpf_record_start"), Description("Start recording UI actions.")]
    public string RecordStart(string name = "Untitled Recording")
    {
        _audit.Record("wpf_record_start");
        try
        {
            _recording.Start(name);
            return JsonSerializer.Serialize(new { result = "recording_started", name }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_record_stop"), Description("Stop recording and return workflow JSON.")]
    public string RecordStop()
    {
        _audit.Record("wpf_record_stop");
        try
        {
            var recording = _recording.Stop();
            return JsonSerializer.Serialize(recording, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_replay"), Description("Replay recorded workflow JSON.")]
    public string Replay(string workflowJson)
    {
        _audit.Record("wpf_replay");
        try
        {
            var recording = JsonSerializer.Deserialize<RecordingModel>(workflowJson, JsonOptions.Default);
            if (recording is null || recording.Steps.Count == 0)
                return JsonSerializer.Serialize(new { error = "Invalid or empty workflow." }, JsonOptions.Default);

            var results = new List<StepResult>();
            foreach (var step in recording.Steps)
            {
                try
                {
                    if (step.Action is not null)
                    {
                        ExecuteAction(step);
                        results.Add(new StepResult { Step = step.Name ?? step.Action, Result = "success" });
                    }
                    else if (step.Assert is not null)
                    {
                        var passed = ExecuteAssertion(step);
                        results.Add(new StepResult { Step = step.Name ?? step.Assert, Result = passed ? "pass" : "fail" });
                    }
                }
                catch (Exception ex)
                {
                    results.Add(new StepResult { Step = step.Name ?? step.Action ?? step.Assert ?? "unknown", Result = "error", Error = ex.Message });
                }
            }

            var allPassed = results.All(r => r.Result != "error" && r.Result != "fail");
            return JsonSerializer.Serialize(new { result = allPassed ? "success" : "partial_failure", steps = results }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_validate_recording"), Description("Check recording for brittle selectors and missing waits.")]
    public string ValidateRecording(string workflowJson)
    {
        _audit.Record("wpf_validate_recording");
        try
        {
            var recording = JsonSerializer.Deserialize<RecordingModel>(workflowJson, JsonOptions.Default);
            if (recording is null)
                return JsonSerializer.Serialize(new { error = "Invalid workflow JSON." }, JsonOptions.Default);

            var issues = new List<object>();

            for (int i = 0; i < recording.Steps.Count; i++)
            {
                var step = recording.Steps[i];
                if (step.Selector is null)
                {
                    issues.Add(new { stepIndex = i, issue = "Missing selector", severity = "warning" });
                    continue;
                }

                if (string.IsNullOrEmpty(step.Selector.AutomationId) && !string.IsNullOrEmpty(step.Selector.Name))
                {
                    issues.Add(new { stepIndex = i, issue = "Selector uses Name without AutomationId — may break with localization", severity = "warning" });
                }

                if (step.Selector.IndexPath is not null)
                {
                    issues.Add(new { stepIndex = i, issue = "Selector uses index path — brittle", severity = "error" });
                }

                if (string.IsNullOrEmpty(step.Selector.AutomationId) && string.IsNullOrEmpty(step.Selector.Name))
                {
                    issues.Add(new { stepIndex = i, issue = "Selector has no unique identifier", severity = "error" });
                }
            }

            return JsonSerializer.Serialize(new
            {
                isValid = !issues.Any(i => ((dynamic)i).severity == "error"),
                issueCount = issues.Count,
                issues
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_export_test"), Description("Generate xUnit + FlaUI test code from recording JSON.")]
    public string ExportTest(string workflowJson)
    {
        _audit.Record("wpf_export_test");
        try
        {
            var recording = JsonSerializer.Deserialize<RecordingModel>(workflowJson, JsonOptions.Default);
            if (recording is null)
                return JsonSerializer.Serialize(new { error = "Invalid workflow JSON." }, JsonOptions.Default);

            var testCode = _recording.GenerateTestCode(recording);
            return JsonSerializer.Serialize(new { language = "csharp", framework = "xunit", code = testCode }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_export_recording"), Description("Export current or provided workflow as JSON.")]
    public string ExportRecording(string? workflowJson = null)
    {
        _audit.Record("wpf_export_recording");

        if (!string.IsNullOrEmpty(workflowJson))
            return workflowJson;

        if (_recording.IsRecording)
            return JsonSerializer.Serialize(new { error = "Recording still in progress. Stop it first." }, JsonOptions.Default);

        return JsonSerializer.Serialize(new { error = "No recording available. Start and stop a recording first." }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_import_recording"), Description("Load and validate workflow JSON.")]
    public string ImportRecording(string workflowJson)
    {
        _audit.Record("wpf_import_recording");
        try
        {
            var recording = JsonSerializer.Deserialize<RecordingModel>(workflowJson, JsonOptions.Default);
            if (recording is null)
                return JsonSerializer.Serialize(new { error = "Invalid workflow JSON." }, JsonOptions.Default);

            return JsonSerializer.Serialize(new
            {
                result = "imported",
                name = recording.Name,
                stepCount = recording.Steps.Count,
                schemaVersion = recording.SchemaVersion
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_record_pause"), Description("Pause the current recording.")]
    public string RecordPause()
    {
        _audit.Record("wpf_record_pause");
        if (!_recording.IsRecording)
            return JsonSerializer.Serialize(new { error = "No recording in progress." }, JsonOptions.Default);

        _recording.Pause();
        return JsonSerializer.Serialize(new { result = "recording_paused" }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_record_resume"), Description("Resume a paused recording.")]
    public string RecordResume()
    {
        _audit.Record("wpf_record_resume");
        _recording.Resume();
        return JsonSerializer.Serialize(new { result = "recording_resumed" }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_record_step"), Description("Manually add a named step/checkpoint to the recording.")]
    public string RecordStep(string stepName, string? notes = null)
    {
        _audit.Record("wpf_record_step");
        if (!_recording.IsRecording)
            return JsonSerializer.Serialize(new { error = "No recording in progress." }, JsonOptions.Default);

        _recording.AddStep(new RecordingStep
        {
            Name = stepName,
            Action = "checkpoint",
            Value = notes,
            TimestampUtc = DateTime.UtcNow
        });

        return JsonSerializer.Serialize(new { result = "step_added", name = stepName }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_record_assertion"), Description("Add assertion from current UI state to the recording.")]
    public string RecordAssertion(string assertType, string? automationId = null, string? name = null, string? expectedValue = null)
    {
        _audit.Record("wpf_record_assertion");
        if (!_recording.IsRecording)
            return JsonSerializer.Serialize(new { error = "No recording in progress." }, JsonOptions.Default);

        _recording.AddStep(new RecordingStep
        {
            Assert = assertType,
            Selector = new ElementCriteria { AutomationId = automationId, Name = name },
            Value = expectedValue,
            TimestampUtc = DateTime.UtcNow
        });

        return JsonSerializer.Serialize(new { result = "assertion_added", type = assertType }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_replay_step"), Description("Replay one step from a recording by index or name.")]
    public string ReplayStep(string workflowJson, int? stepIndex = null, string? stepName = null)
    {
        _audit.Record("wpf_replay_step");
        try
        {
            var recording = JsonSerializer.Deserialize<RecordingModel>(workflowJson, JsonOptions.Default);
            if (recording is null)
                return JsonSerializer.Serialize(new { error = "Invalid workflow JSON." }, JsonOptions.Default);

            RecordingStep? step = null;
            if (stepIndex.HasValue && stepIndex.Value < recording.Steps.Count)
                step = recording.Steps[stepIndex.Value];
            else if (!string.IsNullOrEmpty(stepName))
                step = recording.Steps.FirstOrDefault(s => s.Name == stepName);

            if (step is null)
                return JsonSerializer.Serialize(new { error = "Step not found." }, JsonOptions.Default);

            if (step.Action is not null)
            {
                ExecuteAction(step);
                return JsonSerializer.Serialize(new { result = "step_replayed", step = step.Name ?? step.Action }, JsonOptions.Default);
            }
            else if (step.Assert is not null)
            {
                var passed = ExecuteAssertion(step);
                return JsonSerializer.Serialize(new { result = passed ? "pass" : "fail", step = step.Name ?? step.Assert }, JsonOptions.Default);
            }

            return JsonSerializer.Serialize(new { error = "Step has no action or assertion." }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_optimize_recording"), Description("Replace sleeps/coordinates with waits/semantic selectors in a recording.")]
    public string OptimizeRecording(string workflowJson)
    {
        _audit.Record("wpf_optimize_recording");
        try
        {
            var recording = JsonSerializer.Deserialize<RecordingModel>(workflowJson, JsonOptions.Default);
            if (recording is null)
                return JsonSerializer.Serialize(new { error = "Invalid workflow JSON." }, JsonOptions.Default);

            int optimized = 0;
            foreach (var step in recording.Steps)
            {
                if (step.Selector is not null)
                {
                    // Try to enhance selectors with AutomationId if missing
                    if (string.IsNullOrEmpty(step.Selector.AutomationId) && !string.IsNullOrEmpty(step.Selector.Name))
                    {
                        var element = _uia.FindElement(step.Selector);
                        if (element is not null)
                        {
                            var automationId = element.Properties.AutomationId.ValueOrDefault;
                            if (!string.IsNullOrEmpty(automationId))
                            {
                                step.Selector.AutomationId = automationId;
                                optimized++;
                            }
                        }
                    }
                }
            }

            recording.Policy ??= new RecordingPolicy();
            recording.Policy.AllowCoordinateFallback = false;

            return JsonSerializer.Serialize(new
            {
                result = "optimized",
                optimizedSteps = optimized,
                recording
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_explain_replay_failure"), Description("Diagnose why a replay step failed.")]
    public string ExplainReplayFailure(string workflowJson, int failedStepIndex)
    {
        _audit.Record("wpf_explain_replay_failure");
        try
        {
            var recording = JsonSerializer.Deserialize<RecordingModel>(workflowJson, JsonOptions.Default);
            if (recording is null)
                return JsonSerializer.Serialize(new { error = "Invalid workflow JSON." }, JsonOptions.Default);

            if (failedStepIndex >= recording.Steps.Count)
                return JsonSerializer.Serialize(new { error = "Step index out of range." }, JsonOptions.Default);

            var step = recording.Steps[failedStepIndex];
            var diagnosis = new List<string>();

            if (step.Selector is null)
            {
                diagnosis.Add("Step has no selector — cannot locate element.");
            }
            else
            {
                var element = _uia.FindElement(step.Selector);
                if (element is null)
                {
                    diagnosis.Add("Selector does not match any element in current window.");
                    var allElements = _uia.QueryElements();

                    if (!string.IsNullOrEmpty(step.Selector.AutomationId))
                    {
                        var similar = allElements.Where(e => e.AutomationId?.Contains(step.Selector.AutomationId, StringComparison.OrdinalIgnoreCase) == true).ToList();
                        if (similar.Count > 0)
                            diagnosis.Add($"Found {similar.Count} elements with similar AutomationId: {string.Join(", ", similar.Select(s => s.AutomationId).Take(3))}");
                    }
                    if (!string.IsNullOrEmpty(step.Selector.Name))
                    {
                        var similar = allElements.Where(e => e.Name?.Contains(step.Selector.Name, StringComparison.OrdinalIgnoreCase) == true).ToList();
                        if (similar.Count > 0)
                            diagnosis.Add($"Found {similar.Count} elements with similar Name: {string.Join(", ", similar.Select(s => s.Name).Take(3))}");
                    }
                }
                else
                {
                    if (!element.Properties.IsEnabled.ValueOrDefault)
                        diagnosis.Add("Element found but is disabled.");
                    if (element.Properties.IsOffscreen.ValueOrDefault)
                        diagnosis.Add("Element found but is offscreen/not visible.");
                    if (step.Action == "invoke" && !element.Patterns.Invoke.IsSupported)
                        diagnosis.Add("Element does not support Invoke pattern.");
                    if (step.Action == "set_value" && !element.Patterns.Value.IsSupported)
                        diagnosis.Add("Element does not support Value pattern.");
                }
            }

            if (diagnosis.Count == 0)
                diagnosis.Add("No obvious issue detected. The step may have failed due to timing.");

            return JsonSerializer.Serialize(new
            {
                stepIndex = failedStepIndex,
                stepName = step.Name ?? step.Action ?? step.Assert,
                diagnosis,
                suggestion = diagnosis.Count > 0 ? "Try adding a wait step before this action." : null
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    [McpServerTool(Name = "wpf_snapshot_checkpoint"), Description("Capture named UI state checkpoint for comparison.")]
    public string SnapshotCheckpoint(string checkpointName)
    {
        _audit.Record("wpf_snapshot_checkpoint");
        try
        {
            var snapshot = _uia.CaptureSnapshot(maxDepth: 4);
            var json = JsonSerializer.Serialize(snapshot, JsonOptions.Default);
            return JsonSerializer.Serialize(new { result = "checkpoint_captured", name = checkpointName, snapshot = json }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = ex.Message }, JsonOptions.Default);
        }
    }

    private void ExecuteAction(RecordingStep step)
    {
        if (step.Selector is null) return;

        var element = _uia.FindElement(step.Selector)
            ?? throw new InvalidOperationException($"Element not found for step: {step.Name ?? step.Action}");

        switch (step.Action)
        {
            case "invoke":
                if (element.Patterns.Invoke.IsSupported)
                    element.Patterns.Invoke.Pattern.Invoke();
                else
                    element.Click();
                break;
            case "click":
                element.Click();
                break;
            case "double_click":
                element.DoubleClick();
                break;
            case "right_click":
                element.RightClick();
                break;
            case "set_value":
                if (element.Patterns.Value.IsSupported && step.Value is not null)
                    element.Patterns.Value.Pattern.SetValue(step.Value);
                break;
            case "type_text":
                element.Focus();
                if (step.Value is not null)
                    FlaUI.Core.Input.Keyboard.Type(step.Value);
                break;
            case "send_keys":
                element.Focus();
                if (step.Value is not null)
                    FlaUI.Core.Input.Keyboard.Type(step.Value);
                break;
            case "toggle":
                if (element.Patterns.Toggle.IsSupported)
                    element.Patterns.Toggle.Pattern.Toggle();
                break;
            case "check":
                if (element.Patterns.Toggle.IsSupported)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault == FlaUI.Core.Definitions.ToggleState.On) break;
                        element.Patterns.Toggle.Pattern.Toggle();
                    }
                }
                break;
            case "uncheck":
                if (element.Patterns.Toggle.IsSupported)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (element.Patterns.Toggle.Pattern.ToggleState.ValueOrDefault == FlaUI.Core.Definitions.ToggleState.Off) break;
                        element.Patterns.Toggle.Pattern.Toggle();
                    }
                }
                break;
            case "select":
                if (element.Patterns.SelectionItem.IsSupported)
                    element.Patterns.SelectionItem.Pattern.Select();
                break;
            case "expand":
                if (element.Patterns.ExpandCollapse.IsSupported)
                    element.Patterns.ExpandCollapse.Pattern.Expand();
                break;
            case "collapse":
                if (element.Patterns.ExpandCollapse.IsSupported)
                    element.Patterns.ExpandCollapse.Pattern.Collapse();
                break;
            case "scroll_into_view":
                if (element.Patterns.ScrollItem.IsSupported)
                    element.Patterns.ScrollItem.Pattern.ScrollIntoView();
                break;
            default:
                throw new InvalidOperationException($"Unknown action '{step.Action}' in recording step.");
        }
    }

    private bool ExecuteAssertion(RecordingStep step)
    {
        if (step.Selector is null) return false;

        var element = _uia.FindElement(step.Selector);

        return step.Assert switch
        {
            "exists" => element is not null,
            "not_exists" => element is null,
            "enabled" => element?.Properties.IsEnabled.ValueOrDefault == true,
            "disabled" => element?.Properties.IsEnabled.ValueOrDefault == false,
            "visible" => element?.Properties.IsOffscreen.ValueOrDefault == false,
            _ => false
        };
    }
}

internal sealed class StepResult
{
    public string Step { get; set; } = "";
    public string Result { get; set; } = "";
    public string? Error { get; set; }
}
