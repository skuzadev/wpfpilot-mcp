using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using WpfPilot.Mcp.Core.Models;

namespace WpfPilot.Mcp.Codegen;

public enum TestFramework { XUnit, NUnit, MSTest }

public sealed class CodegenOptions
{
    public TestFramework Framework { get; set; } = TestFramework.XUnit;
    public string Namespace { get; set; } = "GeneratedTests";
    public bool EmitPageObject { get; set; } = true;
    public bool ValidateCompiles { get; set; } = true;
    public List<MetadataReference>? ExtraReferences { get; set; }
}

public sealed class CodegenResult
{
    public CompilationUnitSyntax Tree { get; init; } = null!;
    public string Code { get; init; } = string.Empty;
    public IReadOnlyList<Diagnostic> Diagnostics { get; init; } = Array.Empty<Diagnostic>();
    public bool Compiled { get; init; }
}

/// <summary>
/// Roslyn-based test code generator. Emits a CompilationUnitSyntax for a test class
/// and an optional page object class derived from a RecordingModel.
/// </summary>
public sealed class RoslynTestCodeGenerator
{
    public CodegenResult Generate(RecordingModel recording, CodegenOptions? options = null)
    {
        options ??= new CodegenOptions();
        var className = SanitizeIdentifier(recording.Name) + "Tests";
        var pageObjectName = SanitizeIdentifier(recording.Name) + "Page";

        var testClass = GenerateTestClass(className, pageObjectName, recording, options);
        var pageObject = options.EmitPageObject
            ? GeneratePageObject(pageObjectName, recording)
            : null;

        var members = new List<MemberDeclarationSyntax> { testClass };
        if (pageObject is not null) members.Add(pageObject);

        var unit = SyntaxFactory.CompilationUnit()
            .WithUsings(GenerateUsings(options))
            .WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(
                SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName(options.Namespace))
                    .WithMembers(SyntaxFactory.List(members))))
            .NormalizeWhitespace();

        var code = unit.ToFullString();
        var diagnostics = new List<Diagnostic>();
        bool compiled = true;

        if (options.ValidateCompiles)
        {
            var (ok, diags) = TryCompile(unit, options);
            compiled = ok;
            diagnostics.AddRange(diags);
        }

        return new CodegenResult
        {
            Tree = unit,
            Code = code,
            Diagnostics = diagnostics,
            Compiled = compiled
        };
    }

    private static SyntaxList<UsingDirectiveSyntax> GenerateUsings(CodegenOptions options)
    {
        var usings = new List<UsingDirectiveSyntax>
        {
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Collections.Generic")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Linq")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("System.Threading.Tasks")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("FlaUI.Core")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("FlaUI.Core.AutomationElements")),
            SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("FlaUI.UIA3")),
        };
        usings.Add(options.Framework switch
        {
            TestFramework.XUnit => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Xunit")),
            TestFramework.NUnit => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("NUnit.Framework")),
            TestFramework.MSTest => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Microsoft.VisualStudio.TestTools.UnitTesting")),
            _ => SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("Xunit"))
        });
        return SyntaxFactory.List(usings);
    }

    private ClassDeclarationSyntax GenerateTestClass(string className, string pageObjectName, RecordingModel recording, CodegenOptions options)
    {
        var fieldDecls = new List<MemberDeclarationSyntax>
        {
            FieldDecl("Application", "_app"),
            FieldDecl("UIA3Automation", "_automation"),
            FieldDecl("Window", "_window"),
        };

        var ctor = SyntaxFactory.ConstructorDeclaration(className)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithBody(SyntaxFactory.Block(
                string.IsNullOrEmpty(recording.App?.Process)
                    ? AssignStatement("_app", "null!")
                    : ParseStmt($"_app = Application.AttachOrLaunch(new System.Diagnostics.ProcessStartInfo(\"{Escape(recording.App.Process)}\"));"),
                ParseStmt("_automation = new UIA3Automation();"),
                ParseStmt("_window = _app.GetMainWindow(_automation);")));

        var pageProp = PropertyDecl(pageObjectName, "Page");

        var testMethod = GenerateTestMethod(className, recording, pageObjectName, options);

        var disposeMethod = SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("void"), "Dispose")
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithBody(SyntaxFactory.Block(ParseStmt("_automation?.Dispose();")));

        return SyntaxFactory.ClassDeclaration(className)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddBaseListTypes(SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseName("IDisposable")))
            .AddMembers(fieldDecls.Concat(new MemberDeclarationSyntax[] { ctor, pageProp, testMethod, disposeMethod }).ToArray());
    }

    private PropertyDeclarationSyntax PropertyDecl(string typeName, string name)
    {
        return SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName(typeName), name)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithAccessorList(SyntaxFactory.AccessorList(SyntaxFactory.List(new[]
            {
                SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
                SyntaxFactory.AccessorDeclaration(SyntaxKind.SetAccessorDeclaration)
                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
            })));
    }

    private FieldDeclarationSyntax FieldDecl(string typeName, string name)
    {
        return SyntaxFactory.FieldDeclaration(SyntaxFactory.VariableDeclaration(SyntaxFactory.ParseTypeName(typeName))
            .AddVariables(SyntaxFactory.VariableDeclarator(name)));
    }

    private MethodDeclarationSyntax GenerateTestMethod(string className, RecordingModel recording, string pageObjectName, CodegenOptions options)
    {
        var statements = new List<StatementSyntax>();
        foreach (var step in recording.Steps)
        {
            if (string.IsNullOrEmpty(step.Action) && string.IsNullOrEmpty(step.Assert))
                continue;
            var (selectorExpr, isPath) = BuildSelectorExpression(step);
            var commentText = step.Name ?? step.Action ?? step.Assert ?? "step";
            statements.Add(ParseStmt($"// {EscapeComment(commentText)}"));
            if (!string.IsNullOrEmpty(step.Action))
            {
                var act = TranslateAction(step, selectorExpr, isPath);
                if (act is not null) statements.Add(act);
            }
            else if (!string.IsNullOrEmpty(step.Assert))
            {
                var ass = TranslateAssert(step, selectorExpr, isPath, options);
                if (ass is not null) statements.Add(ass);
            }
        }
        var attrName = options.Framework switch
        {
            TestFramework.NUnit => "Test",
            TestFramework.MSTest => "TestMethod",
            _ => "Fact"
        };
        var attrList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Attribute(SyntaxFactory.ParseName(attrName))));

        var methodName = SanitizeIdentifier(recording.Name) + "_Workflow";
        return SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("void"), methodName)
            .AddAttributeLists(attrList)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .WithBody(SyntaxFactory.Block(statements));
    }

    private ClassDeclarationSyntax GeneratePageObject(string pageObjectName, RecordingModel recording)
    {
        var properties = new List<MemberDeclarationSyntax>();
        var seen = new HashSet<string>();
        foreach (var step in recording.Steps)
        {
            if (step.Selector is null) continue;
            var key = step.Selector.AutomationId ?? step.Selector.Name;
            if (string.IsNullOrEmpty(key) || !seen.Add(key)) continue;
            var name = SanitizeIdentifier(key) + "Element";
            var init = GenerateSelectorInitializer(step.Selector);
            properties.Add(SyntaxFactory.PropertyDeclaration(SyntaxFactory.ParseTypeName("AutomationElement"), name)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithExpressionBody(SyntaxFactory.ArrowExpressionClause(init))
                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken)));
        }

        return SyntaxFactory.ClassDeclaration(pageObjectName)
            .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
            .AddMembers(properties.ToArray());
    }

    private static ExpressionSyntax GenerateSelectorInitializer(ElementCriteria? selector)
    {
        if (selector is null) return SyntaxFactory.IdentifierName("_root");
        if (!string.IsNullOrEmpty(selector.AutomationId))
            return SyntaxFactory.ParseExpression(
                $"_root.FindFirstDescendant(cf => cf.ByAutomationId(\"{Escape(selector.AutomationId)}\"))");
        if (!string.IsNullOrEmpty(selector.Name))
            return SyntaxFactory.ParseExpression(
                $"_root.FindFirstDescendant(cf => cf.ByName(\"{Escape(selector.Name)}\"))");
        return SyntaxFactory.IdentifierName("_root");
    }

    private static (string expr, bool isPath) BuildSelectorExpression(RecordingStep step)
    {
        if (!string.IsNullOrEmpty(step.Path))
            return ($"Page.Path(\"{Escape(step.Path)}\")", true);
        if (step.Selector is not null)
        {
            if (!string.IsNullOrEmpty(step.Selector.AutomationId))
                return ($"_window.FindFirstDescendant(cf => cf.ByAutomationId(\"{Escape(step.Selector.AutomationId)}\"))", false);
            if (!string.IsNullOrEmpty(step.Selector.Name))
                return ($"_window.FindFirstDescendant(cf => cf.ByName(\"{Escape(step.Selector.Name)}\"))", false);
        }
        return ("_window", false);
    }

    private StatementSyntax? TranslateAction(RecordingStep step, string selectorExpr, bool isPath)
    {
        if (string.IsNullOrEmpty(step.Action)) return null;
        return step.Action switch
        {
            "click" => ParseStmt($"{selectorExpr}.Click();"),
            "invoke" => ParseStmt($"{selectorExpr}.Patterns.Invoke.Pattern.Invoke();"),
            "set_value" => ParseStmt($"{selectorExpr}.Patterns.Value.Pattern.SetValue(\"{Escape(step.Value ?? string.Empty)}\");"),
            "toggle" => ParseStmt($"{selectorExpr}.Patterns.Toggle.Pattern.Toggle();"),
            "select" => ParseStmt($"{selectorExpr}.Patterns.SelectionItem.Pattern.Select();"),
            "expand" => ParseStmt($"{selectorExpr}.Patterns.ExpandCollapse.Pattern.Expand();"),
            "collapse" => ParseStmt($"{selectorExpr}.Patterns.ExpandCollapse.Pattern.Collapse();"),
            "focus" => ParseStmt($"{selectorExpr}.Focus();"),
            "double_click" => ParseStmt($"{selectorExpr}.DoubleClick();"),
            "right_click" => ParseStmt($"{selectorExpr}.RightClick();"),
            "scroll_into_view" => ParseStmt($"{selectorExpr}.Patterns.ScrollItem.Pattern.ScrollIntoView();"),
            _ => ParseStmt($"// TODO: action '{step.Action}' not implemented in codegen")
        };
    }

    private StatementSyntax? TranslateAssert(RecordingStep step, string selectorExpr, bool isPath, CodegenOptions options)
    {
        if (string.IsNullOrEmpty(step.Assert)) return null;
        var assertClass = "Assert";
        return step.Assert switch
        {
            "exists" => ParseStmt($"{assertClass}.True({selectorExpr} != null, \"Element should exist\");"),
            "not_exists" => ParseStmt($"{assertClass}.True({selectorExpr} == null, \"Element should not exist\");"),
            "enabled" => ParseStmt($"{assertClass}.True(({selectorExpr})?.IsEnabled ?? false, \"Element should be enabled\");"),
            "disabled" => ParseStmt($"{assertClass}.False(({selectorExpr})?.IsEnabled ?? true, \"Element should be disabled\");"),
            "text" => ParseStmt($"{assertClass}.Equal(\"{Escape(step.Value ?? string.Empty)}\", ({selectorExpr})?.Properties.Name.ValueOrDefault);"),
            "value" => ParseStmt($"{assertClass}.Equal(\"{Escape(step.Value ?? string.Empty)}\", ({selectorExpr})?.Patterns.Value.Pattern.Value.ValueOrDefault);"),
            _ => ParseStmt($"// TODO: assertion '{step.Assert}' not implemented in codegen")
        };
    }

    private static string SanitizeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Generated";
        var sb = new System.Text.StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
            else if (c == ' ' || c == '-' || c == '.') sb.Append('_');
        }
        if (sb.Length == 0) return "Generated";
        if (char.IsDigit(sb[0])) sb.Insert(0, '_');
        return sb.ToString();
    }

    private static string Escape(string s) => s?.Replace("\\", "\\\\").Replace("\"", "\\\"") ?? string.Empty;
    private static string EscapeComment(string s) => s?.Replace("*/", "* /") ?? string.Empty;

    private static StatementSyntax ParseStmt(string text) => SyntaxFactory.ParseStatement(text);
    private static StatementSyntax AssignStatement(string name, string value)
        => ParseStmt($"{name} = {value};");

    private static (bool ok, IReadOnlyList<Diagnostic> diagnostics) TryCompile(CompilationUnitSyntax unit, CodegenOptions options)
    {
        var references = new List<MetadataReference>();
        var trustedAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        foreach (var path in trustedAssemblies.Split(Path.PathSeparator))
        {
            if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                references.Add(MetadataReference.CreateFromFile(path));
        }
        references.AddRange(options.ExtraReferences ?? new List<MetadataReference>());

        var tree = CSharpSyntaxTree.ParseText(unit.SyntaxTree.GetText().ToString());
        var compilation = CSharpCompilation.Create(
            assemblyName: "GeneratedTest",
            syntaxTrees: new[] { tree },
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var ms = new MemoryStream();
        var result = compilation.Emit(ms);
        if (!result.Success)
        {
            return (false, result.Diagnostics.ToList());
        }
        return (true, Array.Empty<Diagnostic>());
    }
}
