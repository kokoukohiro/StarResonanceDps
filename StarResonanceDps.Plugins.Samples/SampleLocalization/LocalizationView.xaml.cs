using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.PluginSdk;

namespace SampleLocalization;

/// <summary>The app disposes the view after the window closes, because it implements <see cref="IDisposable"/>.</summary>
public partial class LocalizationView : UserControl, IDisposable
{
    private const string ResourceBaseName = "SampleLocalization.Properties.Resources";
    private const string SettingsFileName = "sample_settings.json";

    private readonly IPluginContext _context;
    private readonly SampleSettings _settings;

    public LocalizationView(IPluginContext context)
    {
        _context = context;
        Texts = new PluginLocalizer(context.Localization, typeof(LocalizationView).Assembly, ResourceBaseName);
        _settings = context.Settings.Load<SampleSettings>(SettingsFileName);

        DataContext = this;
        InitializeComponent();

        Texts.CultureChanged += Texts_CultureChanged;
        SettingsFolderText.Text = context.Settings.GetFilePath(SettingsFileName);
        ShowSavedCount();
    }

    public PluginLocalizer Texts { get; }

    public void Dispose()
    {
        Texts.CultureChanged -= Texts_CultureChanged;
        Texts.Dispose();
    }

    private void Texts_CultureChanged(object? sender, EventArgs e)
    {
        ShowSavedCount();
    }

    private void CountUp_Click(object sender, RoutedEventArgs e)
    {
        _settings.Count++;
        _context.Settings.Save(SettingsFileName, _settings);
        ShowSavedCount();
    }

    private void ShowMessage_Click(object sender, RoutedEventArgs e)
    {
        _context.Messages.Show(Texts["MessageTitle"], Texts["MessageText"], Texts["MessageDetail"]);
    }

    private void ShowSavedCount()
    {
        SavedCountText.Text = Texts.Format("SavedCount", _settings.Count);
    }
}

/// <summary>Saved as JSON in the plugin's folder. Uninstalling the plugin removes it with the folder.</summary>
public sealed class SampleSettings
{
    public int Count { get; set; }
}
