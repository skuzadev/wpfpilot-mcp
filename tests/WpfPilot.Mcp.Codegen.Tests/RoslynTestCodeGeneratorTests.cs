using Xunit;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Codegen;

namespace WpfPilot.Mcp.Codegen.Tests;

public class RoslynTestCodeGeneratorTests
{
    [Fact]
    public void Generates_XUnit_Test_Class()
    {
        var rec = NewRecording("Sample", new RecordingStep
        {
            Action = "click",
            Name = "Click Save",
            Selector = new ElementCriteria { AutomationId = "btnSave" }
        });
        var gen = new RoslynTestCodeGenerator();
        var result = gen.Generate(rec);
        Assert.Contains("public class SampleTests", result.Code);
        Assert.Contains("[Fact]", result.Code);
        Assert.Contains("btnSave", result.Code);
        Assert.Contains("IDisposable", result.Code);
    }

    [Fact]
    public void Generates_NUnit_Test_Class()
    {
        var rec = NewRecording("Sample", new RecordingStep { Action = "click" });
        var gen = new RoslynTestCodeGenerator();
        var result = gen.Generate(rec, new CodegenOptions { Framework = TestFramework.NUnit });
        Assert.Contains("[Test]", result.Code);
    }

    [Fact]
    public void Generates_MSTest_Test_Class()
    {
        var rec = NewRecording("Sample", new RecordingStep { Action = "click" });
        var gen = new RoslynTestCodeGenerator();
        var result = gen.Generate(rec, new CodegenOptions { Framework = TestFramework.MSTest });
        Assert.Contains("[TestMethod]", result.Code);
    }

    [Fact]
    public void Emits_Path_Based_Selector()
    {
        var rec = NewRecording("Sample", new RecordingStep
        {
            Action = "click",
            Path = "Window#Main/Button#Save"
        });
        var gen = new RoslynTestCodeGenerator();
        var result = gen.Generate(rec);
        Assert.Contains("Page.Path(\"Window#Main/Button#Save\")", result.Code);
    }

    [Fact]
    public void Emits_PageObject_When_Enabled()
    {
        var rec = NewRecording("Sample", new RecordingStep
        {
            Action = "click",
            Selector = new ElementCriteria { AutomationId = "btnSave" }
        });
        var gen = new RoslynTestCodeGenerator();
        var result = gen.Generate(rec, new CodegenOptions { EmitPageObject = true });
        Assert.Contains("public class SamplePage", result.Code);
        Assert.Contains("btnSaveElement", result.Code);
    }

    [Fact]
    public void Skips_PageObject_When_Disabled()
    {
        var rec = NewRecording("Sample", new RecordingStep { Action = "click" });
        var gen = new RoslynTestCodeGenerator();
        var result = gen.Generate(rec, new CodegenOptions { EmitPageObject = false });
        Assert.DoesNotContain("public class SamplePage", result.Code);
    }

    [Fact]
    public void Returns_Compilation_Unit_Tree()
    {
        var rec = NewRecording("Sample", new RecordingStep { Action = "click" });
        var gen = new RoslynTestCodeGenerator();
        var result = gen.Generate(rec);
        Assert.NotNull(result.Tree);
        Assert.NotEmpty(result.Code);
    }

    [Fact]
    public void Empty_Recording_Generates_Valid_Skeleton()
    {
        var rec = NewRecording("Empty", null);
        var gen = new RoslynTestCodeGenerator();
        var result = gen.Generate(rec);
        Assert.Contains("public class EmptyTests", result.Code);
        Assert.Contains("[Fact]", result.Code);
    }

    [Fact]
    public void Sanitizes_Special_Characters_In_Name()
    {
        var rec = NewRecording("Test Workflow - 1.0!", new RecordingStep { Action = "click" });
        var gen = new RoslynTestCodeGenerator();
        var result = gen.Generate(rec);
        Assert.Contains("Test_Workflow___1_0Tests", result.Code);
    }

    [Fact]
    public void Emits_All_Action_Verbs()
    {
        var rec = NewRecording("All", new[]
        {
            new RecordingStep { Action = "click", Selector = new ElementCriteria { AutomationId = "a1" } },
            new RecordingStep { Action = "set_value", Selector = new ElementCriteria { AutomationId = "a2" }, Value = "x" },
            new RecordingStep { Action = "toggle", Selector = new ElementCriteria { AutomationId = "a3" } },
            new RecordingStep { Action = "select", Selector = new ElementCriteria { AutomationId = "a4" } },
            new RecordingStep { Action = "expand", Selector = new ElementCriteria { AutomationId = "a5" } },
            new RecordingStep { Action = "collapse", Selector = new ElementCriteria { AutomationId = "a6" } },
            new RecordingStep { Action = "focus", Selector = new ElementCriteria { AutomationId = "a7" } },
        });
        var gen = new RoslynTestCodeGenerator();
        var result = gen.Generate(rec);
        Assert.Contains("ByAutomationId(\"a1\")).Click()", result.Code);
        Assert.Contains("Value.Pattern.SetValue", result.Code);
        Assert.Contains("Toggle.Pattern.Toggle()", result.Code);
        Assert.Contains("SelectionItem.Pattern.Select()", result.Code);
        Assert.Contains("ExpandCollapse.Pattern.Expand()", result.Code);
        Assert.Contains("ExpandCollapse.Pattern.Collapse()", result.Code);
        Assert.Contains("ByAutomationId(\"a7\")).Focus()", result.Code);
    }

    [Fact]
    public void Invoke_Action_Emits_Invoke_Pattern()
    {
        var rec = NewRecording("Invoke", new RecordingStep
        {
            Action = "invoke",
            Selector = new ElementCriteria { AutomationId = "btnRun" }
        });
        var gen = new RoslynTestCodeGenerator();
        var result = gen.Generate(rec);
        Assert.Contains("Invoke.Pattern.Invoke()", result.Code);
        Assert.DoesNotContain(".Click()", result.Code);
    }

    [Fact]
    public void Emits_Assert_Verbs()
    {
        var rec = NewRecording("Asserts", new[]
        {
            new RecordingStep { Assert = "exists", Selector = new ElementCriteria { AutomationId = "x1" } },
            new RecordingStep { Assert = "not_exists", Selector = new ElementCriteria { AutomationId = "x2" } },
            new RecordingStep { Assert = "enabled", Selector = new ElementCriteria { AutomationId = "x3" } },
            new RecordingStep { Assert = "text", Selector = new ElementCriteria { AutomationId = "x4" }, Value = "Hello" },
        });
        var gen = new RoslynTestCodeGenerator();
        var result = gen.Generate(rec);
        Assert.Contains("Assert.True", result.Code);
        Assert.Contains("Assert.Equal", result.Code);
        Assert.Contains("\"Hello\"", result.Code);
    }

    [Fact]
    public void Uses_Custom_Namespace()
    {
        var rec = NewRecording("Sample", new RecordingStep { Action = "click" });
        var gen = new RoslynTestCodeGenerator();
        var result = gen.Generate(rec, new CodegenOptions { Namespace = "MyCompany.Tests" });
        Assert.Contains("namespace MyCompany.Tests", result.Code);
    }

    private static RecordingModel NewRecording(string name, params RecordingStep[]? steps)
    {
        return new RecordingModel
        {
            Name = name,
            App = new RecordingAppInfo { Process = "mspaint.exe" },
            Steps = steps?.ToList() ?? new List<RecordingStep>()
        };
    }
}
