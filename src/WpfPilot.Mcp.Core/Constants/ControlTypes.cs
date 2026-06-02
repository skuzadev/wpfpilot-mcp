namespace WpfPilot.Mcp.Core.Constants;

public static class ControlTypes
{
    public const string Button = "Button";
    public const string TextBox = "TextBox";
    public const string Edit = "Edit";
    public const string ComboBox = "ComboBox";
    public const string CheckBox = "CheckBox";
    public const string RadioButton = "RadioButton";
    public const string MenuItem = "MenuItem";
    public const string Menu = "Menu";
    public const string MenuBar = "MenuBar";
    public const string Tab = "Tab";
    public const string TabItem = "TabItem";
    public const string List = "List";
    public const string ListItem = "ListItem";
    public const string DataItem = "DataItem";
    public const string Tree = "Tree";
    public const string TreeItem = "TreeItem";
    public const string Slider = "Slider";
    public const string Hyperlink = "Hyperlink";
    public const string Window = "Window";
    public const string Pane = "Pane";
    public const string Group = "Group";
    public const string Document = "Document";
    public const string Image = "Image";
    public const string Table = "Table";
    public const string ProgressBar = "ProgressBar";

    public static readonly IReadOnlySet<string> Actionable = new HashSet<string>(StringComparer.Ordinal)
    {
        Button, TextBox, Edit, ComboBox, CheckBox, RadioButton, MenuItem, Tab, TabItem,
        ListItem, DataItem, TreeItem, Slider, Hyperlink
    };

    public static readonly IReadOnlySet<string> Items = new HashSet<string>(StringComparer.Ordinal)
    {
        ListItem, TreeItem, DataItem
    };
}
