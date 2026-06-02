using WpfPilot.Mcp.Core.Models;
using WpfPilot.Mcp.Core.Primitives;

namespace WpfPilot.Mcp.Core.Abstractions;

public interface IElement
{
    string Id { get; }
    string? AutomationId { get; }
    string? Name { get; }
    string? ControlType { get; }
    string? ClassName { get; }
    bool IsEnabled { get; }
    bool IsOffscreen { get; }
    ElementBounds? Bounds { get; }
    IReadOnlyList<string> Patterns { get; }
    string? Value { get; }
    IReadOnlyList<IElement> Children { get; }
}

public interface IUiAdapter
{
    bool IsAttached { get; }

    Result<UiSnapshot> CaptureSnapshot(IElement? root = null, int maxDepth = 10);
    Result<IElement> FindElement(ElementSelector selector);
    Result<IReadOnlyList<IElement>> FindElements(ElementSelector selector);
    Result<IReadOnlyList<IElement>> Query(ElementCriteria criteria, IElement? root = null);
    Result<UiElement> MapElement(IElement element);
    Result<bool> Click(IElement element);
    Result<bool> DoubleClick(IElement element);
    Result<bool> RightClick(IElement element);
    Result<bool> Invoke(IElement element);
    Result<bool> Hover(IElement element);
    Result<bool> SetValue(IElement element, string value);
    Result<bool> ClearValue(IElement element);
    Result<bool> Type(string text, IElement? target = null);
    Result<bool> SendKeys(string keys);
    Result<bool> Focus(IElement element);
    Result<bool> Select(IElement element);
    Result<bool> SelectByText(string text, IElement? parent = null);
    Result<bool> SelectByIndex(int index, IElement? parent = null);
    Result<bool> Toggle(IElement element);
    Result<bool> SetChecked(IElement element, bool desired);
    Result<bool> Expand(IElement element);
    Result<bool> Collapse(IElement element);
    Result<bool> Scroll(IElement element, string direction, double amount);
    Result<bool> ScrollIntoView(IElement element);
    Result<bool> DragDrop(IElement source, IElement target);
    Result<bool> SetSlider(IElement element, double value);
    Result<bool> SetDate(IElement element, string date);
    Result<bool> OpenMenuPath(string menuPath);
    Result<bool> OpenContextMenuItem(IElement element, string menuItem);
    Result<bool> AcceptDialog();
    Result<bool> CancelDialog();
    Result<string?> GetText(IElement element);
    Result<string?> GetValue(IElement element);
    Result<ElementState> GetState(IElement element);
    Result<ElementBounds> GetBounds(IElement element);
    Result<IReadOnlyList<string>> GetPatterns(IElement element);
    Result<IReadOnlyList<IElement>> GetSelectedItems(IElement element);
    Result<bool> Exists(ElementSelector selector);
    Result<int> Count(ElementSelector selector);
    Result<IReadOnlyList<IElement>> GetAncestors(IElement element, int maxDepth = 10);
    Result<IReadOnlyList<IElement>> GetSiblings(IElement element);
}

public sealed class ElementState
{
    public bool IsEnabled { get; set; }
    public bool IsOffscreen { get; set; }
    public bool HasFocus { get; set; }
    public bool IsKeyboardFocusable { get; set; }
    public bool? IsChecked { get; set; }
    public string? ExpandCollapseState { get; set; }
    public bool? IsSelected { get; set; }
    public string? WindowVisualState { get; set; }
}
