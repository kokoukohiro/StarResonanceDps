namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Resource keys of the app's look that plugins may use, for example
/// <c>Background="{DynamicResource Brush.PanelBackground}"</c> or <c>Style="{StaticResource Button.Text}"</c>.
/// These names do not change. Keys not listed here belong to the app and may change at any time.
/// Use <c>DynamicResource</c> for brushes so that the plugin follows the app's theme.
/// </summary>
public static class PluginResourceKeys
{
    public static class Brushes
    {
        public const string WindowSurface = "Brush.WindowSurface";
        public const string PanelBackground = "Brush.PanelBackground";
        public const string Border = "Brush.Border";
        public const string BorderSoft = "Brush.BorderSoft";
        public const string ControlBackground = "Brush.ControlBackground";
        public const string ControlActiveBackground = "Brush.ControlActiveBackground";
        public const string InputBackground = "Brush.InputBackground";
        public const string InputHoverBackground = "Brush.InputHoverBackground";
        public const string TextPrimary = "Brush.TextPrimary";
        public const string TextSecondary = "Brush.TextSecondary";
        public const string TextMuted = "Brush.TextMuted";
        public const string Success = "Brush.Success";
    }

    public static class Text
    {
        public const string ProjectDefaultFont = "Font.ProjectDefault";
        public const string WidgetWindow = "Text.WidgetWindow";
        public const string WidgetWindowSubtitle = "Text.WidgetWindowSubtitle";
        public const string WidgetWindowTextShadow = "Effect.WidgetWindowTextShadow";
    }

    public static class Buttons
    {
        public const string Text = "Button.Text";
        public const string Icon = "Button.Icon";
        public const string HyperlinkBase = "Hyperlink.Base";
        public const string HyperlinkInline = "Hyperlink.Inline";
    }

    public static class Inputs
    {
        /// <summary>A single-line text box.</summary>
        public const string PathBox = "Input.PathBox";
        public const string MultilineTextBox = "Input.MultilineTextBox";
        public const string SearchBox = "Input.SearchBox";
        public const string SettingsComboBox = "Input.SettingsComboBox";
        public const string SettingsComboBoxItem = "Input.SettingsComboBoxItem";
        public const string SelectBox = "Input.SelectBox";
        public const string Switch = "Input.Switch";
        public const string ValueSlider = "Input.ValueSlider";
    }

    public static class Panels
    {
        public const string RoundedOutline = "Surface.RoundedOutline";
        public const string RoundedButtonOutline = "Surface.RoundedButtonOutline";
        public const string Root = "Panel.Root";
        public const string SettingSection = "Panel.SettingSection";
        public const string SettingField = "Panel.SettingField";
        public const string SettingFieldLast = "Panel.SettingFieldLast";
    }

    public static class Scrolling
    {
        public const string ScrollViewer = "ScrollViewer.WidgetList";
        public const string WidgetWindowScrollViewer = "ScrollViewer.WidgetWindow";
        public const string ScrollBar = "ScrollBar.WidgetList";
        public const string WidgetWindowScrollBar = "ScrollBar.WidgetWindow";
    }

    public static class ToolTips
    {
        public const string Info = "ToolTip.Info";
        public const string WidgetWindowInfo = "ToolTip.WidgetWindowInfo";

        /// <summary>The style of an element that shows a tooltip and does something when clicked.</summary>
        public const string Owner = "ToolTipOwner.Default";

        /// <summary>The style of an element that shows a tooltip and does nothing when clicked: the tooltip stays open on click.</summary>
        public const string OwnerKeepsOnClick = "ToolTipOwner.KeepsOnClick";
    }

    public static class Sizes
    {
        public const string DefaultRadius = "Radius.Default";
        public const string ButtonRadius = "Radius.Button";
        public const string CardPadding = "Padding.Card";
    }

    public static class Icons
    {
        /// <summary>The style of a <c>Path</c> that draws an icon geometry with strokes.</summary>
        public const string StrokeStyle = "Icon.Stroke";

        /// <summary>The style of a <c>Path</c> that draws an icon geometry filled.</summary>
        public const string FillStyle = "Icon.Fill";

        public const string Search = "Icon.Search";
        public const string Add = "Icon.Add";
        public const string ChevronDown = "Icon.ChevronDown";
        public const string ChevronRight = "Icon.ChevronRight";
        public const string Copy = "Icon.Copy";
        public const string Settings = "Icon.Settings";
        public const string Filter = "Icon.Filter";
        public const string Sort = "Icon.Sort";
        public const string EyeOff = "Icon.EyeOff";
        public const string MoreVertical = "Icon.MoreVertical";
        public const string Help = "Icon.Help";
        public const string Wrench = "Icon.Wrench";
        public const string Sliders = "Icon.Sliders";
    }
}
