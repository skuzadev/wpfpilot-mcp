using FlaUI.Core.AutomationElements;
using FlaUI.Core.Input;
using FlaUI.Core.WindowsAPI;
using WpfPilot.Mcp.Core.Constants;
using WpfPilot.Mcp.Core.Models;

namespace WpfPilot.Mcp.Server.Services;

/// <summary>
/// Shared UIA action implementation used by <c>wpf_act</c> and legacy action tools.
/// </summary>
public sealed class UiActionEngine
{
    private readonly UiaAdapter _uia;
    private readonly RecordingService _recording;

    public UiActionEngine(UiaAdapter uia, RecordingService recording)
    {
        _uia = uia;
        _recording = recording;
    }

    public string SendKeys(string keys)
    {
        if (string.IsNullOrWhiteSpace(keys))
            return ToolJson.Error(ErrorCodes.InvalidArgs, "keys is required");

        var keyParts = keys.Split('+').Select(k => k.Trim().ToLowerInvariant()).ToArray();
        var modifiers = new List<VirtualKeyShort>();
        VirtualKeyShort? mainKey = null;

        foreach (var part in keyParts)
        {
            switch (part)
            {
                case "ctrl" or "control": modifiers.Add(VirtualKeyShort.CONTROL); break;
                case "alt": modifiers.Add(VirtualKeyShort.ALT); break;
                case "shift": modifiers.Add(VirtualKeyShort.SHIFT); break;
                case "enter" or "return": mainKey = VirtualKeyShort.ENTER; break;
                case "tab": mainKey = VirtualKeyShort.TAB; break;
                case "escape" or "esc": mainKey = VirtualKeyShort.ESCAPE; break;
                case "delete" or "del": mainKey = VirtualKeyShort.DELETE; break;
                case "backspace": mainKey = VirtualKeyShort.BACK; break;
                case "space": mainKey = VirtualKeyShort.SPACE; break;
                case "home": mainKey = VirtualKeyShort.HOME; break;
                case "end": mainKey = VirtualKeyShort.END; break;
                case "up": mainKey = VirtualKeyShort.UP; break;
                case "down": mainKey = VirtualKeyShort.DOWN; break;
                case "left": mainKey = VirtualKeyShort.LEFT; break;
                case "right": mainKey = VirtualKeyShort.RIGHT; break;
                default:
                    if (part.Length == 1 && char.IsLetterOrDigit(part[0]))
                        mainKey = (VirtualKeyShort)char.ToUpperInvariant(part[0]);
                    else if (part.StartsWith("f") && int.TryParse(part[1..], out var fNum) && fNum is >= 1 and <= 12)
                        mainKey = (VirtualKeyShort)((int)VirtualKeyShort.F1 + fNum - 1);
                    break;
            }
        }

        if (mainKey is null)
            return ToolJson.Error(ErrorCodes.InvalidArgs, "Could not parse key combination.");

        foreach (var mod in modifiers) Keyboard.Press(mod);
        Keyboard.Press(mainKey.Value);
        Keyboard.Release(mainKey.Value);
        foreach (var mod in modifiers) Keyboard.Release(mod);

        return ToolJson.OkResult("keys_sent");
    }

    public string OpenMenuPath(string menuPath)
    {
        if (string.IsNullOrWhiteSpace(menuPath))
            return ToolJson.Error(ErrorCodes.InvalidArgs, "Menu path is empty.");

        var parts = menuPath.Split(new[] { ">", " > ", " → " }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return ToolJson.Error(ErrorCodes.InvalidArgs, "Menu path is empty.");

        foreach (var part in parts)
        {
            var criteria = new ElementCriteria { Name = part, ControlType = ControlTypes.MenuItem };
            var menuItem = _uia.FindElement(criteria);
            if (menuItem is null)
            {
                criteria = new ElementCriteria { AutomationId = part };
                menuItem = _uia.FindElement(criteria);
            }

            if (menuItem is null)
                return ToolJson.Error(ErrorCodes.ElementNotFound, $"Menu item '{part}' not found.");

            if (menuItem.Patterns.ExpandCollapse.IsSupported)
                menuItem.Patterns.ExpandCollapse.Pattern.Expand();
            else if (menuItem.Patterns.Invoke.IsSupported)
                menuItem.Patterns.Invoke.Pattern.Invoke();
            else
                menuItem.Click();

            Thread.Sleep(100);
        }

        RecordLegacy("open_menu_path", null, menuPath);
        return ToolJson.OkResult("menu_opened");
    }

    public string OpenContextMenuItem(string menuItemName, string? automationId = null, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(menuItemName))
            return ToolJson.Error(ErrorCodes.InvalidArgs, "menuItem is required");

        var criteria = new ElementCriteria { AutomationId = automationId, Name = name };
        var element = _uia.FindElement(criteria);
        if (element is null)
            return ToolJson.Error(ErrorCodes.ElementNotFound, "Element not found.");

        element.RightClick();
        Thread.Sleep(200);

        var menuCriteria = new ElementCriteria { Name = menuItemName, ControlType = ControlTypes.MenuItem };
        var menuItem = _uia.FindElement(menuCriteria);
        if (menuItem is null)
            return ToolJson.Error(ErrorCodes.ElementNotFound, $"Context menu item '{menuItemName}' not found.");

        if (menuItem.Patterns.Invoke.IsSupported)
            menuItem.Patterns.Invoke.Pattern.Invoke();
        else
            menuItem.Click();

        return ToolJson.OkResult("context_menu_item_invoked");
    }

    public string AcceptDialog()
    {
        string[] acceptNames = ["OK", "Ok", "Yes", "Accept", "Confirm", "Save"];
        foreach (var buttonName in acceptNames)
        {
            var criteria = new ElementCriteria { Name = buttonName, ControlType = ControlTypes.Button };
            var element = _uia.FindElement(criteria);
            if (element is null) continue;

            if (element.Patterns.Invoke.IsSupported)
                element.Patterns.Invoke.Pattern.Invoke();
            else
                element.Click();

            return ToolJson.OkResult($"accepted_via_{buttonName}");
        }

        return ToolJson.Error(ErrorCodes.ElementNotFound, "No accept/OK button found in current window.");
    }

    public string CancelDialog()
    {
        string[] cancelNames = ["Cancel", "No", "Close", "Abort"];
        foreach (var buttonName in cancelNames)
        {
            var criteria = new ElementCriteria { Name = buttonName, ControlType = ControlTypes.Button };
            var element = _uia.FindElement(criteria);
            if (element is null) continue;

            if (element.Patterns.Invoke.IsSupported)
                element.Patterns.Invoke.Pattern.Invoke();
            else
                element.Click();

            return ToolJson.OkResult($"cancelled_via_{buttonName}");
        }

        return ToolJson.Error(ErrorCodes.ElementNotFound, "No cancel/close button found in current window.");
    }

    public void RecordStep(string action, ElementCriteria? selector, string? path = null, string? value = null)
    {
        if (!_recording.IsRecording) return;
        _recording.AddStep(new RecordingStep
        {
            Action = action,
            Selector = selector,
            Path = path,
            Value = value
        });
    }

    private void RecordLegacy(string action, ElementCriteria? selector, string? value = null) =>
        RecordStep(action, selector, value: value);
}
