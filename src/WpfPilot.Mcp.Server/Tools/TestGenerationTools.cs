using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class TestGenerationTools
{
    private readonly UiaAdapter _uia;
    private readonly SessionManager _session;
    private readonly RecordingService _recording;
    private readonly AuditLog _audit;

    public TestGenerationTools(UiaAdapter uia, SessionManager session, RecordingService recording, AuditLog audit)
    {
        _uia = uia;
        _session = session;
        _recording = recording;
        _audit = audit;
    }

    [McpServerTool(Name = "wpf_export_page_object"), Description("Generate Page Object / Screen Object class from current window.")]
    public string ExportPageObject(string? className = null)
    {
        _audit.Record("wpf_export_page_object");
        var window = _session.ActiveWindow;
        if (window is null)
            return Error("No window attached.");

        var name = className ?? SanitizeName(window.Title ?? "MainPage");
        var allElements = _uia.QueryElements();
        var actionable = allElements.Where(e => !string.IsNullOrEmpty(e.AutomationId) && IsActionable(e.ControlType)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("using FlaUI.Core;");
        sb.AppendLine("using FlaUI.Core.AutomationElements;");
        sb.AppendLine("using FlaUI.Core.Conditions;");
        sb.AppendLine();
        sb.AppendLine($"public class {name}Page");
        sb.AppendLine("{");
        sb.AppendLine("    private readonly Window _window;");
        sb.AppendLine();
        sb.AppendLine($"    public {name}Page(Window window)");
        sb.AppendLine("    {");
        sb.AppendLine("        _window = window;");
        sb.AppendLine("    }");
        sb.AppendLine();

        foreach (var el in actionable)
        {
            var propName = SanitizeName(el.AutomationId!);
            var returnType = GetElementType(el.ControlType);
            sb.AppendLine($"    public {returnType} {propName} => _window.FindFirstDescendant(cf => cf.ByAutomationId(\"{el.AutomationId}\")).As<{returnType}>();");
        }

        sb.AppendLine("}");

        return JsonSerializer.Serialize(new { language = "csharp", className = $"{name}Page", code = sb.ToString() }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_export_selectors"), Description("Generate selector constants for all identifiable elements.")]
    public string ExportSelectors()
    {
        _audit.Record("wpf_export_selectors");
        var allElements = _uia.QueryElements();
        var withIds = allElements.Where(e => !string.IsNullOrEmpty(e.AutomationId)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("public static class Selectors");
        sb.AppendLine("{");

        foreach (var el in withIds)
        {
            var constName = SanitizeName(el.AutomationId!).ToUpperInvariant();
            sb.AppendLine($"    public const string {constName} = \"{el.AutomationId}\";");
        }

        sb.AppendLine("}");

        return JsonSerializer.Serialize(new { language = "csharp", elementCount = withIds.Count, code = sb.ToString() }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_export_assertions"), Description("Generate assertion helper methods for current form state.")]
    public string ExportAssertions()
    {
        _audit.Record("wpf_export_assertions");
        var allElements = _uia.QueryElements();
        var actionable = allElements.Where(e => !string.IsNullOrEmpty(e.AutomationId) && IsActionable(e.ControlType)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("public static class AssertionHelpers");
        sb.AppendLine("{");

        foreach (var el in actionable)
        {
            var methodName = $"Assert{SanitizeName(el.AutomationId!)}";
            sb.AppendLine($"    public static void {methodName}Exists(Window window)");
            sb.AppendLine("    {");
            sb.AppendLine($"        var element = window.FindFirstDescendant(cf => cf.ByAutomationId(\"{el.AutomationId}\"));");
            sb.AppendLine("        Assert.NotNull(element);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return JsonSerializer.Serialize(new { language = "csharp", code = sb.ToString() }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_export_test_project"), Description("Generate full test project structure with csproj and base classes.")]
    public string ExportTestProject(string projectName = "UiTests")
    {
        _audit.Record("wpf_export_test_project");

        var csproj = $@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=""FlaUI.UIA3"" Version=""4.0.0"" />
    <PackageReference Include=""xunit"" Version=""2.7.0"" />
    <PackageReference Include=""xunit.runner.visualstudio"" Version=""2.5.7"" />
    <PackageReference Include=""Microsoft.NET.Test.Sdk"" Version=""17.9.0"" />
  </ItemGroup>
</Project>";

        var baseClass = @"using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using System.Diagnostics;

public abstract class UiTestBase : IDisposable
{
    protected Application App { get; }
    protected UIA3Automation Automation { get; }
    protected Window MainWindow { get; }

    protected UiTestBase(string processName)
    {
        App = Application.Attach(Process.GetProcessesByName(processName).First());
        Automation = new UIA3Automation();
        MainWindow = App.GetMainWindow(Automation);
    }

    public void Dispose()
    {
        Automation?.Dispose();
    }
}";

        return JsonSerializer.Serialize(new
        {
            projectName,
            files = new[]
            {
                new { path = $"{projectName}.csproj", content = csproj },
                new { path = "UiTestBase.cs", content = baseClass }
            }
        }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_generate_smoke_test"), Description("Generate smoke test from current window structure.")]
    public string GenerateSmokeTest()
    {
        _audit.Record("wpf_generate_smoke_test");
        var window = _session.ActiveWindow;
        if (window is null)
            return Error("No window attached.");

        var allElements = _uia.QueryElements();
        var actionable = allElements.Where(e => !string.IsNullOrEmpty(e.AutomationId) && IsActionable(e.ControlType)).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("using FlaUI.Core.AutomationElements;");
        sb.AppendLine("using Xunit;");
        sb.AppendLine();
        sb.AppendLine("public class SmokeTests : UiTestBase");
        sb.AppendLine("{");
        sb.AppendLine($"    public SmokeTests() : base(\"{_session.GetStatus().ProcessName}\") {{ }}");
        sb.AppendLine();
        sb.AppendLine("    [Fact]");
        sb.AppendLine("    public void AllKeyElementsExist()");
        sb.AppendLine("    {");

        foreach (var el in actionable.Take(20))
        {
            sb.AppendLine($"        Assert.NotNull(MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(\"{el.AutomationId}\")));");
        }

        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    [Fact]");
        sb.AppendLine("    public void AllKeyElementsEnabled()");
        sb.AppendLine("    {");

        foreach (var el in actionable.Where(e => e.IsEnabled).Take(10))
        {
            sb.AppendLine($"        Assert.True(MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(\"{el.AutomationId}\")).IsEnabled);");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return JsonSerializer.Serialize(new { language = "csharp", framework = "xunit", code = sb.ToString() }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_generate_accessibility_test"), Description("Generate accessibility test checking AutomationProperties.")]
    public string GenerateAccessibilityTest()
    {
        _audit.Record("wpf_generate_accessibility_test");
        var window = _session.ActiveWindow;
        if (window is null)
            return Error("No window attached.");

        var sb = new StringBuilder();
        sb.AppendLine("using FlaUI.Core.AutomationElements;");
        sb.AppendLine("using FlaUI.Core.Definitions;");
        sb.AppendLine("using Xunit;");
        sb.AppendLine();
        sb.AppendLine("public class AccessibilityTests : UiTestBase");
        sb.AppendLine("{");
        sb.AppendLine($"    public AccessibilityTests() : base(\"{_session.GetStatus().ProcessName}\") {{ }}");
        sb.AppendLine();
        sb.AppendLine("    [Fact]");
        sb.AppendLine("    public void AllButtonsHaveNames()");
        sb.AppendLine("    {");
        sb.AppendLine("        var buttons = MainWindow.FindAll(FlaUI.Core.Definitions.TreeScope.Descendants,");
        sb.AppendLine("            MainWindow.Automation.ConditionFactory.ByControlType(ControlType.Button));");
        sb.AppendLine("        foreach (var button in buttons)");
        sb.AppendLine("        {");
        sb.AppendLine("            Assert.False(string.IsNullOrEmpty(button.Name) && string.IsNullOrEmpty(button.Properties.AutomationId.ValueOrDefault),");
        sb.AppendLine("                $\"Button at ({button.BoundingRectangle.X},{button.BoundingRectangle.Y}) has no name or AutomationId\");");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    [Fact]");
        sb.AppendLine("    public void AllEditFieldsHaveIdentifiers()");
        sb.AppendLine("    {");
        sb.AppendLine("        var edits = MainWindow.FindAll(FlaUI.Core.Definitions.TreeScope.Descendants,");
        sb.AppendLine("            MainWindow.Automation.ConditionFactory.ByControlType(ControlType.Edit));");
        sb.AppendLine("        foreach (var edit in edits)");
        sb.AppendLine("        {");
        sb.AppendLine("            Assert.False(string.IsNullOrEmpty(edit.Properties.AutomationId.ValueOrDefault),");
        sb.AppendLine("                $\"Edit field at ({edit.BoundingRectangle.X},{edit.BoundingRectangle.Y}) missing AutomationId\");");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    [Fact]");
        sb.AppendLine("    public void AllInteractiveElementsAreFocusable()");
        sb.AppendLine("    {");
        sb.AppendLine("        var elements = MainWindow.FindAll(FlaUI.Core.Definitions.TreeScope.Descendants,");
        sb.AppendLine("            MainWindow.Automation.ConditionFactory.ByControlType(ControlType.Button));");
        sb.AppendLine("        foreach (var el in elements)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (el.IsEnabled && !el.IsOffscreen)");
        sb.AppendLine("                Assert.True(el.Properties.IsKeyboardFocusable.ValueOrDefault,");
        sb.AppendLine("                    $\"Button '{el.Name ?? el.Properties.AutomationId.ValueOrDefault}' not keyboard focusable\");");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return JsonSerializer.Serialize(new { language = "csharp", framework = "xunit", code = sb.ToString() }, JsonOptions.Default);
    }

    [McpServerTool(Name = "wpf_generate_regression_test"), Description("Generate regression test from recording with assertions at checkpoints.")]
    public string GenerateRegressionTest(string workflowJson)
    {
        _audit.Record("wpf_generate_regression_test");
        try
        {
            var recording = JsonSerializer.Deserialize<RecordingModel>(workflowJson, JsonOptions.Default);
            if (recording is null)
                return Error("Invalid workflow JSON.");

            var code = _recording.GenerateTestCode(recording);
            return JsonSerializer.Serialize(new { language = "csharp", framework = "xunit", testType = "regression", code }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
    }

    private static string SanitizeName(string name)
    {
        var sanitized = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
            sanitized = "_" + sanitized;
        return sanitized;
    }

    private static string GetElementType(string? controlType) => controlType switch
    {
        "Button" => "Button",
        "TextBox" or "Edit" => "TextBox",
        "ComboBox" => "ComboBox",
        "CheckBox" => "CheckBox",
        "RadioButton" => "RadioButton",
        "Slider" => "Slider",
        "Tab" or "TabItem" => "TabItem",
        "ListItem" => "ListBoxItem",
        "DataItem" => "DataGridRow",
        "TreeItem" => "TreeItem",
        _ => "AutomationElement"
    };

    private static bool IsActionable(string? controlType)
    {
        if (string.IsNullOrEmpty(controlType)) return false;
        return controlType is "Button" or "TextBox" or "Edit" or "ComboBox" or "CheckBox"
            or "RadioButton" or "MenuItem" or "Tab" or "TabItem" or "ListItem"
            or "DataItem" or "TreeItem" or "Slider" or "Hyperlink";
    }

    [McpServerTool(Name = "wpf_export_test_roslyn"),
     Description("Export the current recording as a runnable C# test class using the Roslyn-based generator. " +
        "Returns the generated code plus any compiler diagnostics.")]
    public string ExportTestRoslyn(string? framework = null)
    {
        _audit.Record("wpf_export_test_roslyn");
        var recording = _recording.ActiveRecording;
        if (recording is null)
            return Error("No active recording. Start a recording first with wpf_record_start.");

        var code = _recording.GenerateTestCode(recording);
        var diagnostics = _recording.GetCodegenDiagnostics(recording);

        return JsonSerializer.Serialize(new
        {
            recording = recording.Name,
            framework = framework ?? "xunit",
            language = "csharp",
            stepCount = recording.Steps.Count,
            compiled = diagnostics.Count == 0,
            diagnostics,
            code
        }, JsonOptions.Default);
    }

    private static string Error(string message) =>
        JsonSerializer.Serialize(new { error = message }, JsonOptions.Default);
}
