using WpfPilot.Mcp.Codegen;
using WpfPilot.Mcp.Core.Models;

namespace WpfPilot.Mcp.Server.Services;

public sealed class RecordingService
{
    private readonly SessionManager _session;
    private readonly RoslynTestCodeGenerator _generator = new();
    private readonly object _lock = new();
    private RecordingModel? _activeRecording;
    private bool _isRecording;
    private bool _isPaused;

    public bool IsRecording { get { lock (_lock) return _isRecording; } }
    public bool IsPaused { get { lock (_lock) return _isPaused; } }
    public RecordingModel? ActiveRecording { get { lock (_lock) return _activeRecording; } }

    public RecordingService(SessionManager session)
    {
        _session = session;
    }

    public void Start(string name)
    {
        lock (_lock)
        {
            if (_isRecording)
                throw new InvalidOperationException("Recording already in progress. Stop the current recording first.");

            _activeRecording = new RecordingModel
            {
                Name = name,
                App = new RecordingAppInfo
                {
                    Process = _session.GetStatus().ProcessName
                },
                Policy = new RecordingPolicy
                {
                    AllowCoordinateFallback = false,
                    RedactValues = true
                }
            };
            _isRecording = true;
        }
    }

    public void AddStep(RecordingStep step)
    {
        lock (_lock)
        {
            if (!_isRecording || _activeRecording is null || _isPaused)
                return;

            step.TimestampUtc = DateTime.UtcNow;
            _activeRecording.Steps.Add(step);
        }
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (!_isRecording)
                throw new InvalidOperationException("No recording in progress.");
            _isPaused = true;
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (!_isRecording)
                throw new InvalidOperationException("No recording in progress.");
            _isPaused = false;
        }
    }

    public RecordingModel Stop()
    {
        lock (_lock)
        {
            if (!_isRecording || _activeRecording is null)
                throw new InvalidOperationException("No recording in progress.");

            _isRecording = false;
            var recording = _activeRecording;
            _activeRecording = null;
            return recording;
        }
    }

    public string GenerateTestCode(RecordingModel recording)
    {
        return GenerateTestCode(recording, useRoslyn: true);
    }

    public string GenerateTestCode(RecordingModel recording, bool useRoslyn)
    {
        if (useRoslyn)
        {
            try
            {
                var result = _generator.Generate(recording, new CodegenOptions
                {
                    Framework = TestFramework.XUnit,
                    ValidateCompiles = true
                });
                if (result.Compiled) return result.Code;
            }
            catch
            {
            }
        }
        return GenerateTestCodeStringBuilder(recording);
    }

    public IReadOnlyList<string> GetCodegenDiagnostics(RecordingModel recording)
    {
        try
        {
            var result = _generator.Generate(recording, new CodegenOptions
            {
                Framework = TestFramework.XUnit,
                ValidateCompiles = true
            });
            return result.Diagnostics
                .Select(d => $"{d.Severity} {d.Id} @ {d.Location.GetLineSpan().StartLinePosition}: {d.GetMessage()}")
                .ToList();
        }
        catch (Exception ex)
        {
            return new[] { $"Error: {ex.Message}" };
        }
    }

    private static string GenerateTestCodeStringBuilder(RecordingModel recording)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("using FlaUI.Core;");
        sb.AppendLine("using FlaUI.Core.AutomationElements;");
        sb.AppendLine("using FlaUI.UIA3;");
        sb.AppendLine("using Xunit;");
        sb.AppendLine();
        sb.AppendLine($"public class {SanitizeName(recording.Name)}Tests : IDisposable");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly Application _app;");
        sb.AppendLine("    private readonly UIA3Automation _automation;");
        sb.AppendLine("    private readonly Window _window;");
        sb.AppendLine();
        sb.AppendLine($"    public {SanitizeName(recording.Name)}Tests()");
        sb.AppendLine("    {");
        if (!string.IsNullOrEmpty(recording.App?.Process))
        {
            sb.AppendLine($"        _app = Application.AttachOrLaunch(new System.Diagnostics.ProcessStartInfo(\"{recording.App.Process}\"));");
        }
        sb.AppendLine("        _automation = new UIA3Automation();");
        sb.AppendLine("        _window = _app.GetMainWindow(_automation);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    [Fact]");
        sb.AppendLine($"    public void {SanitizeName(recording.Name)}_Workflow()");
        sb.AppendLine("    {");

        foreach (var step in recording.Steps)
        {
            if (step.Action is not null)
            {
                sb.AppendLine($"        // {step.Name ?? step.Action}");
                var selectorCode = GenerateSelectorCode(step.Selector);

                switch (step.Action)
                {
                    case "set_value":
                        sb.AppendLine($"        {selectorCode}.Patterns.Value.Pattern.SetValue(\"{step.Value}\");");
                        break;
                    case "invoke":
                        sb.AppendLine($"        {selectorCode}.Patterns.Invoke.Pattern.Invoke();");
                        break;
                    case "toggle":
                        sb.AppendLine($"        {selectorCode}.Patterns.Toggle.Pattern.Toggle();");
                        break;
                    case "select":
                        sb.AppendLine($"        {selectorCode}.Patterns.SelectionItem.Pattern.Select();");
                        break;
                    case "expand":
                        sb.AppendLine($"        {selectorCode}.Patterns.ExpandCollapse.Pattern.Expand();");
                        break;
                    case "collapse":
                        sb.AppendLine($"        {selectorCode}.Patterns.ExpandCollapse.Pattern.Collapse();");
                        break;
                    default:
                        sb.AppendLine($"        // TODO: Implement action '{step.Action}'");
                        break;
                }
                sb.AppendLine();
            }
            else if (step.Assert is not null)
            {
                sb.AppendLine($"        // Assert: {step.Name ?? step.Assert}");
                var selectorCode = GenerateSelectorCode(step.Selector);

                switch (step.Assert)
                {
                    case "exists":
                        sb.AppendLine($"        Assert.NotNull({selectorCode});");
                        break;
                    case "not_exists":
                        sb.AppendLine($"        Assert.Null({selectorCode});");
                        break;
                    case "enabled":
                        sb.AppendLine($"        Assert.True({selectorCode}.IsEnabled);");
                        break;
                    case "text":
                        sb.AppendLine($"        Assert.Equal(\"{step.Value}\", {selectorCode}.Properties.Name.ValueOrDefault);");
                        break;
                    default:
                        sb.AppendLine($"        // TODO: Implement assertion '{step.Assert}'");
                        break;
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    public void Dispose()");
        sb.AppendLine("    {");
        sb.AppendLine("        _automation?.Dispose();");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateSelectorCode(ElementCriteria? selector)
    {
        if (selector is null) return "_window";

        if (!string.IsNullOrEmpty(selector.AutomationId))
        {
            return $"_window.FindFirstDescendant(cf => cf.ByAutomationId(\"{selector.AutomationId}\"))";
        }
        if (!string.IsNullOrEmpty(selector.Name))
        {
            return $"_window.FindFirstDescendant(cf => cf.ByName(\"{selector.Name}\"))";
        }
        return "_window";
    }

    private static string SanitizeName(string name)
    {
        return new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
    }
}
