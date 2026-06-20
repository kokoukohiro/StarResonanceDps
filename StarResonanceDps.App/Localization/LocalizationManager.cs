using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace StarResonanceDps.App.Localization;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    public const int SystemLanguageIndex = 0;
    public const int JapaneseLanguageIndex = 1;
    public const int KoreanLanguageIndex = 2;
    public const int ChineseLanguageIndex = 3;
    public const int EnglishLanguageIndex = 4;

    private static readonly ResourceManager ResourceManager = new(
        "StarResonanceDps.App.Properties.Resources",
        typeof(LocalizationManager).Assembly);

    private readonly CultureInfo _systemDefaultCulture;
    private CultureInfo _currentCulture;

    private LocalizationManager()
    {
        _systemDefaultCulture = CultureInfo.CurrentUICulture;
        _currentCulture = ResolveSystemCulture(_systemDefaultCulture);
    }

    public static LocalizationManager Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? CultureChanged;

    public CultureInfo CurrentCulture => _currentCulture;

    public string this[string key] => GetString(key);

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return ResourceManager.GetString(key, _currentCulture) ?? key;
    }

    public string Format(string key, params object[] arguments)
    {
        return string.Format(_currentCulture, GetString(key), arguments);
    }

    public void ApplyLanguageIndex(int languageIndex)
    {
        var culture = ResolveCulture(languageIndex);
        if (string.Equals(_currentCulture.Name, culture.Name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _currentCulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }

    private CultureInfo ResolveCulture(int languageIndex)
    {
        return languageIndex switch
        {
            JapaneseLanguageIndex => CultureInfo.GetCultureInfo("ja-JP"),
            KoreanLanguageIndex => CultureInfo.GetCultureInfo("ko-KR"),
            ChineseLanguageIndex => CultureInfo.GetCultureInfo("zh-CN"),
            EnglishLanguageIndex => CultureInfo.GetCultureInfo("en-US"),
            _ => ResolveSystemCulture(_systemDefaultCulture)
        };
    }

    private static CultureInfo ResolveSystemCulture(CultureInfo culture)
    {
        if (culture.Name.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("ja-JP");
        }

        if (culture.Name.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("ko-KR");
        }

        if (culture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.GetCultureInfo("zh-CN");
        }

        return CultureInfo.GetCultureInfo("en-US");
    }
}
