using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Threading;

namespace StarResonanceDps.App.Localization;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    public const int SystemLanguageIndex = 0;
    public const int JapaneseLanguageIndex = 1;
    public const int KoreanLanguageIndex = 2;
    public const int ChineseLanguageIndex = 3;
    public const int EnglishLanguageIndex = 4;

    private const string EnglishCultureName = "en-US";
    private const string NeutralResourceName = "StarResonanceDps.App.Properties.Resources.resources";
    private const string JapaneseResourceName = "StarResonanceDps.App.Properties.Resources.ja-JP.resources";
    private const string KoreanResourceName = "StarResonanceDps.App.Properties.Resources.ko-KR.resources";
    private const string ChineseResourceName = "StarResonanceDps.App.Properties.Resources.zh-CN.resources";

    private static readonly IReadOnlyDictionary<string, string> ResourceNamesByCulture =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ja-JP"] = JapaneseResourceName,
            ["ko-KR"] = KoreanResourceName,
            ["zh-CN"] = ChineseResourceName,
            [EnglishCultureName] = NeutralResourceName
        };

    private static readonly ConcurrentDictionary<string, Lazy<IReadOnlyDictionary<string, string>>> ResourceSets =
        new(StringComparer.Ordinal);

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

        var localizedResources = GetResourceSet(_currentCulture);
        if (localizedResources.TryGetValue(key, out var localizedValue))
        {
            return localizedValue;
        }

        if (!string.Equals(_currentCulture.Name, EnglishCultureName, StringComparison.OrdinalIgnoreCase))
        {
            var neutralResources = GetResourceSet(CultureInfo.GetCultureInfo(EnglishCultureName));
            if (neutralResources.TryGetValue(key, out var neutralValue))
            {
                return neutralValue;
            }
        }

        return key;
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

    private static IReadOnlyDictionary<string, string> GetResourceSet(CultureInfo culture)
    {
        var resourceName = ResourceNamesByCulture.TryGetValue(culture.Name, out var localizedResourceName)
            ? localizedResourceName
            : NeutralResourceName;

        return ResourceSets.GetOrAdd(
            resourceName,
            static name => new Lazy<IReadOnlyDictionary<string, string>>(
                () => LoadResourceSet(name),
                LazyThreadSafetyMode.ExecutionAndPublication)).Value;
    }

    private static IReadOnlyDictionary<string, string> LoadResourceSet(string resourceName)
    {
        var assembly = typeof(LocalizationManager).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new MissingManifestResourceException(
                $"Embedded localization resource '{resourceName}' was not found in '{assembly.GetName().Name}'.");
        using var reader = new ResourceReader(stream);

        var resources = new Dictionary<string, string>(StringComparer.Ordinal);
        IDictionaryEnumerator enumerator = reader.GetEnumerator();

        while (enumerator.MoveNext())
        {
            if (enumerator.Key is not string key || enumerator.Value is not string value)
            {
                throw new InvalidDataException(
                    $"Embedded localization resource '{resourceName}' contains a non-string entry.");
            }

            resources.Add(key, value);
        }

        return resources;
    }

    private CultureInfo ResolveCulture(int languageIndex)
    {
        return languageIndex switch
        {
            JapaneseLanguageIndex => CultureInfo.GetCultureInfo("ja-JP"),
            KoreanLanguageIndex => CultureInfo.GetCultureInfo("ko-KR"),
            ChineseLanguageIndex => CultureInfo.GetCultureInfo("zh-CN"),
            EnglishLanguageIndex => CultureInfo.GetCultureInfo(EnglishCultureName),
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

        return CultureInfo.GetCultureInfo(EnglishCultureName);
    }
}
