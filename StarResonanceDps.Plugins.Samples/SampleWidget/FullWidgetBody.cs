using System.Windows.Controls;
using System.Windows.Media;
using StarResonanceDps.PluginSdk;

namespace SampleWidget;

/// <summary>
/// Shows the widget's own settings. The app disposes the body after the window closes, because it implements <see cref="IDisposable"/>.
/// </summary>
internal sealed class FullWidgetBody : StackPanel, IDisposable
{
    private const string SwitchId = "switch";
    private const string ChoiceId = "choice";
    private const string NumberId = "number";
    private const string TextId = "text";
    private const string SliderId = "slider";
    private const string ColorId = "color";

    private readonly IPluginWidgetContext _context;
    private readonly TextBlock _switchText = SampleWidgetPlugin.CreateWindowText(string.Empty);
    private readonly TextBlock _choiceText = SampleWidgetPlugin.CreateWindowText(string.Empty);
    private readonly TextBlock _numberText = SampleWidgetPlugin.CreateWindowText(string.Empty);
    private readonly TextBlock _textText = SampleWidgetPlugin.CreateWindowText(string.Empty);
    private readonly TextBlock _sliderText = SampleWidgetPlugin.CreateWindowText(string.Empty);
    private readonly TextBlock _colorText = SampleWidgetPlugin.CreateWindowText("This text uses the color setting.");

    public FullWidgetBody(IPluginWidgetContext context)
    {
        _context = context;
        Children.Add(_switchText);
        Children.Add(_choiceText);
        Children.Add(_numberText);
        Children.Add(_textText);
        Children.Add(_sliderText);
        Children.Add(_colorText);

        ApplySettings();
        _context.SettingsChanged += Context_SettingsChanged;
    }

    public static IReadOnlyList<PluginSettingDefinition> CreateSettings()
    {
        return
        [
            new PluginSwitchSetting
            {
                Id = SwitchId,
                Names = SampleWidgetPlugin.Texts("Switch", "スイッチ"),
                Notes = SampleWidgetPlugin.Texts("Note: A note below the setting's name.", "※項目名の下に出る注意書き"),
                DefaultValue = true
            },
            new PluginChoiceSetting
            {
                Id = ChoiceId,
                Names = SampleWidgetPlugin.Texts("Choice", "選択"),
                Options =
                [
                    new PluginChoiceOption { Id = "first", Names = SampleWidgetPlugin.Texts("First", "1つ目") },
                    new PluginChoiceOption { Id = "second", Names = SampleWidgetPlugin.Texts("Second", "2つ目") }
                ],
                DefaultOptionId = "first"
            },
            new PluginNumberSetting
            {
                Id = NumberId,
                Names = SampleWidgetPlugin.Texts("Number", "数"),
                Minimum = 0,
                Maximum = 100,
                DefaultValue = 10
            },
            new PluginTextSetting
            {
                Id = TextId,
                Names = SampleWidgetPlugin.Texts("Text", "文字"),
                DefaultValue = "Hello"
            },
            new PluginSliderSetting
            {
                Id = SliderId,
                Names = SampleWidgetPlugin.Texts("Slider", "スライダー"),
                Minimum = 0,
                Maximum = 1,
                DefaultValue = 0.5,
                Step = 0.1
            }
        ];
    }

    public static IReadOnlyList<PluginSettingDefinition> CreateColorSettings()
    {
        return
        [
            new PluginColorSetting
            {
                Id = ColorId,
                Names = SampleWidgetPlugin.Texts("Color", "色"),
                DefaultColors = ["#FFFFFF", "#4C8BF5", "#F5A623"]
            }
        ];
    }

    public void Dispose()
    {
        _context.SettingsChanged -= Context_SettingsChanged;
    }

    private void Context_SettingsChanged(object? sender, EventArgs e)
    {
        ApplySettings();
    }

    /// <summary>While the settings window is open, these are the values the user is trying.</summary>
    private void ApplySettings()
    {
        var settings = _context.Settings;
        _switchText.Text = $"Switch: {settings.GetSwitch(SwitchId)}";
        _choiceText.Text = $"Choice: {settings.GetChoice(ChoiceId)}";
        _numberText.Text = $"Number: {settings.GetNumber(NumberId)}";
        _textText.Text = $"Text: {settings.GetText(TextId)}";
        _sliderText.Text = $"Slider: {settings.GetSlider(SliderId):0.0}";
        _colorText.Foreground = new SolidColorBrush(settings.GetColor(ColorId));
    }
}
