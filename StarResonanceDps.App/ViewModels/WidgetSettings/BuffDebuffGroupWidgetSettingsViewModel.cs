using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels.WidgetSettings;

/// <summary>
/// バフ・デバフグループの設定。ゲージの長さ、ゲージの色(バフカラーとデバフカラーの2節、バフ・デバフ一覧と共有の部品)、
/// ウィジェットに出すバフ・デバフの行(「バフ・デバフ」の一覧)。
///
/// <para>
/// 行はスイッチでオン/オフし、掴んで並べ替える(<see cref="IRowReorderTarget"/>)。どちらも保存前のプレビューで、確定は「保存」。
/// 右クリックで足した行はオフのまま保存すると消え、既定の行(<see cref="WidgetConfigDefaults.DefaultBuffDebuffGroupEntries"/>)は消えない。
/// </para>
/// </summary>
public sealed partial class BuffDebuffGroupWidgetSettingsViewModel : ObservableObject, IDisposable, IRowReorderTarget
{
    private readonly ObservableCollection<BuffSettingRowViewModel> _entries = [];
    private BuffDebuffGroupWidgetSettingsConfig _lastSaved;

    /// <summary>「バフ・デバフ」の一覧で掴んでいる行の鍵と、掴んだときの並び。掴んでいなければ null。</summary>
    private BuffEntryKey? _draggingKey;
    private List<BuffEntryKey>? _orderBeforeDrag;
    private bool _isLoading;

    [ObservableProperty]
    private int _gaugeLengthIndex = WidgetConfigDefaults.DefaultBuffDebuffGroupGaugeLengthIndex;

    public BuffDebuffGroupWidgetSettingsViewModel(BuffDebuffGroupWidgetSettingsConfig? config)
    {
        BuffGaugeColors = new BuffGaugeColorSettingsViewModel(PlayerBuffListKind.Buff);
        BuffGaugeColors.Changed += NotifyChanged;
        DebuffGaugeColors = new BuffGaugeColorSettingsViewModel(PlayerBuffListKind.Debuff);
        DebuffGaugeColors.Changed += NotifyChanged;
        Entries = new ReadOnlyObservableCollection<BuffSettingRowViewModel>(_entries);
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffDebuffGroup(config);
        Load(_lastSaved);
    }

    public event Action<BuffDebuffGroupWidgetSettingsConfig>? PreviewChanged;

    /// <summary>「バフカラー」の節。バフの行のゲージの色。</summary>
    public BuffGaugeColorSettingsViewModel BuffGaugeColors { get; }

    /// <summary>「デバフカラー」の節。デバフの行のゲージの色。</summary>
    public BuffGaugeColorSettingsViewModel DebuffGaugeColors { get; }

    /// <summary>「バフ・デバフ」の一覧の行。並びはウィジェットの行の並び。</summary>
    public ReadOnlyObservableCollection<BuffSettingRowViewModel> Entries { get; }

    public string EntrySectionTitle => LocalizationManager.Instance.GetString("Settings_Section_BuffDebuffGroupEntries_Title");

    public bool HasUnsavedChanges => !SettingsEqual(CreateConfig(), _lastSaved);

    public void Dispose()
    {
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        BuffGaugeColors.Changed -= NotifyChanged;
        BuffGaugeColors.Dispose();
        DebuffGaugeColors.Changed -= NotifyChanged;
        DebuffGaugeColors.Dispose();
    }

    /// <summary>右クリックで足した行は、スイッチがオフなら落とす(保存すると行が消える)。既定の行はオフでも残す。</summary>
    public BuffDebuffGroupWidgetSettingsConfig CreateConfig()
    {
        var config = new BuffDebuffGroupWidgetSettingsConfig
        {
            GaugeLengthIndex = GaugeLengthIndex,
            BuffGaugeColors = BuffGaugeColors.CreateConfig(),
            DebuffGaugeColors = DebuffGaugeColors.CreateConfig(),
            Entries =
            [
                .. _entries
                    .Where(row => row.IsEnabled || row.IsDefault)
                    .Select(row => new BuffDebuffGroupEntryConfig
                    {
                        BuffId = row.Key.BuffId,
                        Group = row.Key.Group,
                        IsEnabled = row.IsEnabled
                    })
            ]
        };

        return WidgetConfigDefaults.CloneNormalizedBuffDebuffGroup(config);
    }

    /// <summary>既定に戻す。行も既定の7行と既定の並び・オン/オフに戻す(ほかの項目と同じく保存前の値。確定は「保存」)。</summary>
    public void ResetToDefaults()
    {
        Load(WidgetConfigDefaults.CreateBuffDebuffGroupSettings());
        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    public void RestoreSavedPreview()
    {
        PreviewChanged?.Invoke(_lastSaved.Clone());
    }

    /// <summary>保存した。保存した値で行を作り直す(オフのまま保存した足した行が消える)。</summary>
    public void MarkSaved(BuffDebuffGroupWidgetSettingsConfig? config)
    {
        _lastSaved = WidgetConfigDefaults.CloneNormalizedBuffDebuffGroup(config);
        Load(_lastSaved);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// 開いている間に、右クリックで行がオン/オフになった(一覧・カードの「グループに追加」、グループの窓の「グループから削除」)。
    /// その分は既に保存されているので、保存済みの値と行の両方に同じ決まり(<see cref="WidgetConfigDefaults.SetBuffDebuffGroupEntryEnabled"/>)で入れる
    /// (「保存」で消えず、未保存の印も付かない)。プレビューは鳴らさない(ウィジェット側は既に変えている)。
    /// </summary>
    public void SetEntryEnabledFromWidget(BuffEntryKey key, bool isEnabled)
    {
        if (!key.IsValid)
        {
            return;
        }

        WidgetConfigDefaults.SetBuffDebuffGroupEntryEnabled(_lastSaved.Entries ??= [], key, isEnabled);

        var row = _entries.FirstOrDefault(candidate => candidate.Key == key);
        if (isEnabled)
        {
            if (row is not null)
            {
                row.LoadEnabled(true);
            }
            else
            {
                _entries.Add(CreateRow(key, isEnabled: true));
            }
        }
        else if (row is not null)
        {
            if (row.IsDefault)
            {
                row.LoadEnabled(false);
            }
            else
            {
                _entries.Remove(row);
            }
        }

        UpdateIsLast();
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>
    /// 開いている間に、バフ・デバフグループの窓で行が並べ替えられた。行を同じ順に並べ直す(作り直さない)。
    /// 保存前の並びになるので未保存の印は付くが、プレビューは鳴らさない(ウィジェット側は既にその並び)。
    /// </summary>
    public void ApplyEntryOrderFromWidget(IReadOnlyList<BuffEntryKey> order)
    {
        MoveToOrder(order);
        OnPropertyChanged(nameof(HasUnsavedChanges));
    }

    /// <summary>「バフ・デバフ」の一覧の行の並べ替えの単位(行1つずつ)。</summary>
    public object? GetRowReorderKey(object item)
    {
        return item is BuffSettingRowViewModel row ? row.Key : null;
    }

    public void BeginRowReorder(object key)
    {
        if (key is BuffEntryKey entryKey)
        {
            _draggingKey = entryKey;
            _orderBeforeDrag = [.. _entries.Select(row => row.Key)];
        }
    }

    /// <summary>掴んでいる間の見た目の並び。掴んだ行を <paramref name="beforeKey"/> の行の前(null なら末尾)へ動かす。プレビューは離したときに出す。</summary>
    public void ShowRowReorderAt(object? beforeKey)
    {
        if (_draggingKey is not { } draggingKey || Equals(beforeKey, draggingKey))
        {
            return;
        }

        var currentIndex = IndexOf(draggingKey);
        if (currentIndex < 0)
        {
            return;
        }

        var row = _entries[currentIndex];
        var order = _entries.Where(candidate => candidate.Key != draggingKey).ToList();
        var insertAt = beforeKey is BuffEntryKey before
            ? order.FindIndex(candidate => candidate.Key == before)
            : order.Count;
        if (insertAt < 0)
        {
            return;
        }

        order.Insert(insertAt, row);
        MoveToOrder(order.Select(candidate => candidate.Key).ToList());
    }

    /// <summary>掴んでいる間に、掴んだ行を元の位置(掴んだときの並び)へ戻す。設定の窓の外に出たときに使う。</summary>
    public void ShowRowReorderAtOriginalPosition()
    {
        if (_draggingKey is null || _orderBeforeDrag is null)
        {
            return;
        }

        MoveToOrder(_orderBeforeDrag);
    }

    /// <summary>掴み終わり。<paramref name="commit"/> で並びが変わっていればプレビューを出し(保存は「保存」)、取り消しなら元の並びへ戻す。</summary>
    public void EndRowReorder(bool commit)
    {
        if (_draggingKey is null || _orderBeforeDrag is null)
        {
            return;
        }

        var orderBeforeDrag = _orderBeforeDrag;
        _draggingKey = null;
        _orderBeforeDrag = null;

        if (!commit)
        {
            MoveToOrder(orderBeforeDrag);
            return;
        }

        if (!_entries.Select(row => row.Key).SequenceEqual(orderBeforeDrag))
        {
            NotifyChanged();
        }
    }

    private void Load(BuffDebuffGroupWidgetSettingsConfig config)
    {
        var normalized = WidgetConfigDefaults.CloneNormalizedBuffDebuffGroup(config);

        _isLoading = true;
        try
        {
            GaugeLengthIndex = normalized.GaugeLengthIndex;
            BuffGaugeColors.Load(normalized.BuffGaugeColors);
            DebuffGaugeColors.Load(normalized.DebuffGaugeColors);

            _entries.Clear();
            foreach (var entry in normalized.Entries ?? [])
            {
                _entries.Add(CreateRow(entry.GetKey(), entry.IsEnabled));
            }

            UpdateIsLast();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private BuffSettingRowViewModel CreateRow(BuffEntryKey key, bool isEnabled)
    {
        return new BuffSettingRowViewModel(
            key,
            WidgetConfigDefaults.IsDefaultBuffDebuffGroupEntry(key),
            isEnabled,
            NotifyChanged);
    }

    private int IndexOf(BuffEntryKey key)
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            if (_entries[index].Key == key)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>行を鍵の並びのとおりの位置へ動かす(作り直さない)。最後の行の印も付け直す。</summary>
    private void MoveToOrder(IReadOnlyList<BuffEntryKey> orderedKeys)
    {
        for (var target = 0; target < orderedKeys.Count && target < _entries.Count; target++)
        {
            var current = IndexOf(orderedKeys[target]);
            if (current >= 0 && current != target)
            {
                _entries.Move(current, target);
            }
        }

        UpdateIsLast();
    }

    private void UpdateIsLast()
    {
        for (var index = 0; index < _entries.Count; index++)
        {
            _entries[index].IsLast = index == _entries.Count - 1;
        }
    }

    partial void OnGaugeLengthIndexChanged(int value)
    {
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        if (_isLoading)
        {
            return;
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        RaisePreviewChanged();
    }

    private void RaisePreviewChanged()
    {
        PreviewChanged?.Invoke(CreateConfig());
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var row in _entries)
        {
            row.RefreshMetadata();
        }

        OnPropertyChanged(nameof(EntrySectionTitle));
    }

    private static bool SettingsEqual(
        BuffDebuffGroupWidgetSettingsConfig left,
        BuffDebuffGroupWidgetSettingsConfig right)
    {
        if (left.GaugeLengthIndex != right.GaugeLengthIndex
            || left.BuffGaugeColors is null
            || right.BuffGaugeColors is null
            || left.DebuffGaugeColors is null
            || right.DebuffGaugeColors is null
            || !BuffGaugeColorSettingsViewModel.ConfigEquals(left.BuffGaugeColors, right.BuffGaugeColors)
            || !BuffGaugeColorSettingsViewModel.ConfigEquals(left.DebuffGaugeColors, right.DebuffGaugeColors))
        {
            return false;
        }

        var leftEntries = left.Entries ?? [];
        var rightEntries = right.Entries ?? [];
        return leftEntries.Count == rightEntries.Count
            && leftEntries
                .Zip(rightEntries)
                .All(pair => pair.First.GetKey() == pair.Second.GetKey() && pair.First.IsEnabled == pair.Second.IsEnabled);
    }
}
