using System.ComponentModel;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using ModelContextProtocol.Server;
using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Server.Services;

namespace WpfPilot.Mcp.Server.Tools;

[McpServerToolType]
public sealed class WhyTools
{
    private readonly UiaAdapter _uia;
    private readonly ProbeClient _probe;
    private readonly SessionManager _session;
    private readonly AuditLog _audit;

    public WhyTools(UiaAdapter uia, ProbeClient probe, SessionManager session, AuditLog audit)
    {
        _uia = uia;
        _probe = probe;
        _session = session;
        _audit = audit;
    }

    [McpServerTool(Name = "wpf_why_disabled"), Description("Explain WHY an element is disabled. Checks CanExecute, bindings, DataContext, and ancestor state to find the root cause.")]
    public async Task<string> WhyDisabled(string? automationId = null, string? name = null)
    {
        _audit.Record("wpf_why_disabled");
        try
        {
            var criteria = new ElementCriteria { AutomationId = automationId, Name = name };
            var element = _uia.FindElement(criteria);
            if (element is null)
                return Error("Element not found.");

            var isEnabled = element.Properties.IsEnabled.ValueOrDefault;
            if (isEnabled)
                return JsonSerializer.Serialize(new { result = "Element is currently ENABLED — not disabled." }, JsonOptions.Default);

            var analysis = new DisabledAnalysis
            {
                Element = new ElementInfo
                {
                    AutomationId = element.Properties.AutomationId.ValueOrDefault,
                    Name = element.Properties.Name.ValueOrDefault,
                    ControlType = element.Properties.ControlType.ValueOrDefault.ToString(),
                    IsEnabled = false
                }
            };

            // Check if parent is disabled (inherited disable)
            var parent = element.Parent;
            if (parent is not null && !parent.Properties.IsEnabled.ValueOrDefault)
            {
                analysis.Reasons.Add(new DisabledReason
                {
                    Category = "inherited",
                    Description = $"Parent element '{parent.Properties.Name.ValueOrDefault ?? parent.Properties.AutomationId.ValueOrDefault}' ({parent.Properties.ControlType.ValueOrDefault}) is also disabled. The disabled state is inherited.",
                    Suggestion = "Enable the parent container/group first."
                });
            }

            // Check if it's a button with a command (via probe)
            if (_probe.IsConnected && element.Properties.ControlType.ValueOrDefault == ControlType.Button)
            {
                try
                {
                    var commandResponse = await _probe.SendAsync("get_command_state");
                    if (commandResponse?.Data is not null)
                    {
                        var commands = JsonSerializer.Deserialize<List<CommandInfo>>(commandResponse.Data!.Value.GetRawText());
                        var elementName = element.Properties.Name.ValueOrDefault ?? element.Properties.AutomationId.ValueOrDefault;
                        var matchedCommand = commands?.FirstOrDefault(c =>
                            c.Name.Contains(elementName ?? "", StringComparison.OrdinalIgnoreCase) ||
                            (automationId is not null && c.Name.Contains(automationId, StringComparison.OrdinalIgnoreCase)));

                        if (matchedCommand is not null && !matchedCommand.CanExecute)
                        {
                            analysis.Reasons.Add(new DisabledReason
                            {
                                Category = "command_canexecute",
                                Description = $"Command '{matchedCommand.Name}' has CanExecute = false.",
                                Suggestion = "Check the CanExecute logic — typically depends on required fields being filled or valid state."
                            });
                        }
                    }

                    // Check for binding errors
                    var bindingResponse = await _probe.SendAsync("get_binding_errors");
                    if (bindingResponse?.Data is not null)
                    {
                        analysis.Reasons.Add(new DisabledReason
                        {
                            Category = "binding_errors",
                            Description = "Binding errors detected that may prevent the element from enabling.",
                            Suggestion = "Check DataContext and binding paths — a broken binding means the ViewModel property isn't reaching the control."
                        });
                    }

                    // Check ViewModel state
                    var vmResponse = await _probe.SendAsync("get_viewmodel_properties");
                    if (vmResponse?.Data is not null)
                    {
                        analysis.ViewModelState = vmResponse.Data!.Value.GetRawText();
                    }
                }
                catch { }
            }

            // If no specific reason found, provide general guidance
            if (analysis.Reasons.Count == 0)
            {
                analysis.Reasons.Add(new DisabledReason
                {
                    Category = "unknown",
                    Description = "Could not determine specific cause via UIA inspection alone.",
                    Suggestion = "Connect the in-process probe for deeper CanExecute/binding analysis. Without the probe, check: (1) required fields may be empty, (2) a prerequisite action hasn't been performed, (3) the element's IsEnabled is bound to a ViewModel property that is false."
                });
            }

            return JsonSerializer.Serialize(new { analysis }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
    }

    [McpServerTool(Name = "wpf_why_hidden"), Description("Explain WHY an element is not visible. Checks Visibility, Offscreen, collapsed state, and parent visibility.")]
    public string WhyHidden(string? automationId = null, string? name = null)
    {
        _audit.Record("wpf_why_hidden");
        try
        {
            var criteria = new ElementCriteria { AutomationId = automationId, Name = name };
            var element = _uia.FindElement(criteria);

            if (element is null)
            {
                // Element doesn't exist at all — check if it might be collapsed
                return JsonSerializer.Serialize(new
                {
                    analysis = new
                    {
                        elementFound = false,
                        reasons = new[]
                        {
                            new
                            {
                                category = "not_in_tree",
                                description = "Element not found in the automation tree. It may be: (1) Visibility=Collapsed (removed from tree), (2) not yet loaded, (3) in a different tab/page, or (4) dynamically created on demand.",
                                suggestion = "Check if a tab/expander needs to be activated first, or if the element appears after a specific user action."
                            }
                        }
                    }
                }, JsonOptions.Default);
            }

            var isOffscreen = element.Properties.IsOffscreen.ValueOrDefault;
            if (!isOffscreen)
                return JsonSerializer.Serialize(new { result = "Element IS visible and not offscreen." }, JsonOptions.Default);

            var reasons = new List<object>();

            // Check bounds
            var bounds = element.BoundingRectangle;
            if (bounds.Width == 0 || bounds.Height == 0)
            {
                reasons.Add(new
                {
                    category = "zero_size",
                    description = "Element has zero width or height — likely Visibility=Hidden or constrained by layout.",
                    suggestion = "Check if Visibility is bound to a ViewModel property. Set it to Visible or check the binding."
                });
            }

            // Check if scrolled out of view
            var parent = element.Parent;
            if (parent?.Patterns.Scroll.IsSupported == true)
            {
                reasons.Add(new
                {
                    category = "scrolled_out",
                    description = "Element is inside a scrollable container and is currently scrolled out of view.",
                    suggestion = "Use wpf_scroll_into_view to bring it into view."
                });
            }

            // Check parent visibility
            if (parent is not null && parent.Properties.IsOffscreen.ValueOrDefault)
            {
                reasons.Add(new
                {
                    category = "parent_hidden",
                    description = $"Parent '{parent.Properties.Name.ValueOrDefault}' is also offscreen. Hidden state is inherited.",
                    suggestion = "Make the parent visible first."
                });
            }

            if (reasons.Count == 0)
            {
                reasons.Add(new
                {
                    category = "offscreen",
                    description = "Element is marked offscreen by UIA. May be behind another window, in an inactive tab, or scrolled out.",
                    suggestion = "Try wpf_focus or wpf_scroll_into_view. Check if the element's tab/page is active."
                });
            }

            return JsonSerializer.Serialize(new
            {
                analysis = new
                {
                    elementFound = true,
                    isOffscreen = true,
                    bounds = new { bounds.X, bounds.Y, bounds.Width, bounds.Height },
                    reasons
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
    }

    [McpServerTool(Name = "wpf_why_validation_failed"), Description("Explain validation errors on the current window. Shows which fields have errors, error messages, and ViewModel validation state.")]
    public async Task<string> WhyValidationFailed()
    {
        _audit.Record("wpf_why_validation_failed");
        try
        {
            var allElements = _uia.QueryElements();

            // Look for elements that might indicate validation errors
            var errorElements = allElements.Where(e =>
                (e.Name?.Contains("error", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.Name?.Contains("invalid", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.Name?.Contains("required", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.AutomationId?.Contains("Error", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (e.AutomationId?.Contains("Validation", StringComparison.OrdinalIgnoreCase) ?? false))
                .Select(e => new { e.AutomationId, e.Name, e.ControlType, e.Value })
                .ToList();

            string? probeValidation = null;
            if (_probe.IsConnected)
            {
                try
                {
                    var response = await _probe.SendAsync("get_validation_state");
                    probeValidation = response?.Data?.GetRawText();
                }
                catch { }
            }

            return JsonSerializer.Serialize(new
            {
                analysis = new
                {
                    visibleErrorElements = errorElements,
                    viewModelValidation = probeValidation,
                    suggestion = errorElements.Count == 0 && probeValidation is null
                        ? "No visible validation errors detected. If you expect errors, they may appear after a submit action."
                        : "Review the fields listed above and correct their values."
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
    }

    [McpServerTool(Name = "wpf_why_empty"), Description("Explain WHY a field is empty when it should have a value. Traces bindings and DataContext.")]
    public async Task<string> WhyEmpty(string? automationId = null, string? name = null)
    {
        _audit.Record("wpf_why_empty");
        try
        {
            var criteria = new ElementCriteria { AutomationId = automationId, Name = name };
            var element = _uia.FindElement(criteria);
            if (element is null)
                return Error("Element not found.");

            string? currentValue = null;
            if (element.Patterns.Value.IsSupported)
                currentValue = element.Patterns.Value.Pattern.Value.ValueOrDefault;

            if (!string.IsNullOrEmpty(currentValue))
                return JsonSerializer.Serialize(new { result = $"Element has value: '{currentValue}' — it is NOT empty." }, JsonOptions.Default);

            var reasons = new List<object>();

            if (_probe.IsConnected)
            {
                try
                {
                    var bindingResponse = await _probe.SendAsync("get_bindings",
                        new Dictionary<string, object?> { ["automationId"] = automationId ?? name ?? "" });

                    if (bindingResponse?.Data is not null)
                    {
                        reasons.Add(new
                        {
                            category = "binding_info",
                            description = $"Binding data: {bindingResponse.Data!.Value.GetRawText()}",
                            suggestion = "Check if the bound property on the ViewModel has been set."
                        });
                    }

                    var errResponse = await _probe.SendAsync("get_binding_errors");
                    if (errResponse?.Data is not null && errResponse.Data.Value.GetRawText().Contains(automationId ?? name ?? ""))
                    {
                        reasons.Add(new
                        {
                            category = "binding_error",
                            description = "Binding error detected for this element — the data is not reaching the control.",
                            suggestion = "Verify the binding Path matches the ViewModel property name. Check DataContext is set."
                        });
                    }
                }
                catch { }
            }

            if (reasons.Count == 0)
            {
                reasons.Add(new
                {
                    category = "no_probe",
                    description = "Without the in-process probe, binding inspection is limited. Common causes: (1) ViewModel property not set, (2) binding path typo, (3) DataContext is null, (4) value converter returning empty.",
                    suggestion = "Connect the probe for detailed binding tracing. Or check the ViewModel property that this field is bound to."
                });
            }

            return JsonSerializer.Serialize(new
            {
                analysis = new
                {
                    element = new { automationId, name, currentValue = (string?)null },
                    reasons
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
    }

    [McpServerTool(Name = "wpf_explain_screen"), Description("AI-friendly explanation of the current screen: what it does, available actions, current state, and potential issues.")]
    public async Task<string> ExplainScreen()
    {
        _audit.Record("wpf_explain_screen");
        try
        {
            var window = _session.ActiveWindow;
            var allElements = _uia.QueryElements();
            var actionable = allElements.Where(e => IsActionable(e.ControlType)).ToList();

            var inputs = actionable.Where(e => e.ControlType is "TextBox" or "ComboBox" or "CheckBox" or "RadioButton" or "Slider").ToList();
            var buttons = actionable.Where(e => e.ControlType is "Button").ToList();
            var tabs = actionable.Where(e => e.ControlType is "TabItem").ToList();
            var grids = allElements.Where(e => e.ControlType is "DataGrid" or "Table" or "List").ToList();

            // Determine screen purpose
            var purpose = DetermineScreenPurpose(inputs, buttons, grids, tabs, window?.Title);

            // Find issues
            var issues = new List<string>();
            var disabledButtons = buttons.Where(b => !b.IsEnabled).ToList();
            if (disabledButtons.Count > 0)
                issues.Add($"{disabledButtons.Count} button(s) are disabled: {string.Join(", ", disabledButtons.Select(b => b.Name ?? b.AutomationId))}");

            var emptyRequiredInputs = inputs.Where(i => string.IsNullOrEmpty(i.Value) && i.IsEnabled).ToList();
            if (emptyRequiredInputs.Count > 0)
                issues.Add($"{emptyRequiredInputs.Count} input field(s) are empty");

            string? vmInfo = null;
            if (_probe.IsConnected)
            {
                try
                {
                    var response = await _probe.SendAsync("get_datacontext");
                    vmInfo = response?.Data?.GetRawText();
                }
                catch { }
            }

            return JsonSerializer.Serialize(new
            {
                screen = new
                {
                    windowTitle = window?.Title,
                    purpose,
                    currentState = new
                    {
                        inputFields = inputs.Select(i => new { i.AutomationId, i.Name, i.Value, i.IsEnabled }).ToList(),
                        availableActions = buttons.Select(b => new { b.AutomationId, b.Name, b.IsEnabled }).ToList(),
                        tabs = tabs.Select(t => new { t.Name, t.AutomationId }).ToList(),
                        dataGrids = grids.Select(g => new { g.AutomationId, g.Name }).ToList()
                    },
                    issues,
                    viewModel = vmInfo
                }
            }, JsonOptions.Default);
        }
        catch (Exception ex)
        {
            return Error(ex.Message);
        }
    }

    private static string DetermineScreenPurpose(List<UiElement> inputs, List<UiElement> buttons, List<UiElement> grids, List<UiElement> tabs, string? title)
    {
        if (inputs.Count > 3 && buttons.Any(b =>
            (b.Name?.Contains("Save", StringComparison.OrdinalIgnoreCase) ?? false) ||
            (b.Name?.Contains("Submit", StringComparison.OrdinalIgnoreCase) ?? false)))
            return "Data entry form";

        if (grids.Count > 0 && inputs.Count <= 2)
            return "Data listing/grid view";

        if (tabs.Count > 2)
            return "Tabbed navigation interface";

        if (inputs.Count == 1 && buttons.Count <= 2)
            return "Search or filter interface";

        if (title?.Contains("Login", StringComparison.OrdinalIgnoreCase) ?? false)
            return "Authentication screen";

        if (title?.Contains("Settings", StringComparison.OrdinalIgnoreCase) ?? false)
            return "Settings/configuration screen";

        return "General application screen";
    }

    private static bool IsActionable(string? controlType) =>
        controlType is "Button" or "TextBox" or "ComboBox" or "CheckBox"
            or "RadioButton" or "MenuItem" or "TabItem" or "ListItem"
            or "DataItem" or "TreeItem" or "Slider" or "Hyperlink";

    private static string Error(string message) =>
        JsonSerializer.Serialize(new { error = message }, JsonOptions.Default);

    private sealed class DisabledAnalysis
    {
        public ElementInfo Element { get; set; } = new();
        public List<DisabledReason> Reasons { get; set; } = [];
        public string? ViewModelState { get; set; }
    }

    private sealed class ElementInfo
    {
        public string? AutomationId { get; set; }
        public string? Name { get; set; }
        public string? ControlType { get; set; }
        public bool IsEnabled { get; set; }
    }

    private sealed class DisabledReason
    {
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
    }

    private sealed class CommandInfo
    {
        public string Name { get; set; } = string.Empty;
        public bool CanExecute { get; set; }
    }
}
