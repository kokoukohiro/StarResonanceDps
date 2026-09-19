using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// モンスターのバフ・デバフを1件だけ大きく出すカード。
///
/// <para>
/// 倍率の保存キーには <see cref="EntityListEntry.EntityId"/>(種別ID)を使う。
/// <c>EntityUuid</c> は実体ごとの値なので、再起動をまたぐと二度と当たらない。
/// </para>
/// </summary>
public sealed partial class EntityBuffDebuffCardWidgetViewModel
    : ViewModelBase, IEntityWidgetWindowViewModel, IDisposable
{
    private readonly WidgetListItemViewModel _widget;
    private readonly EntityWindowTarget _target;
    private readonly PlayerBuffListKind _kind;
    private readonly string? _requestedBuffKey;

    /// <summary>追うまとまり。<see cref="BuffGroup.None"/> なら個別のバフを追う。</summary>
    private readonly BuffGroup _group;
    private readonly DispatcherTimer _refreshTimer;
    private string _scaleKey = string.Empty;

    /// <summary>最後に分かったバフ名。失効してもタイトルは維持する。</summary>
    private string _lastKnownBuffName = string.Empty;

    private bool _isDisposed;

    [ObservableProperty]
    private string _headerText = string.Empty;

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private string _durationText = string.Empty;

    [ObservableProperty]
    private string _layerText = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    [ObservableProperty]
    private bool _hasBuff;

    /// <summary>名前の行を出すか。<b>失効しても名前だけは残す</b>ので、<see cref="HasBuff"/> とは別。</summary>
    [ObservableProperty]
    private bool _hasDisplayText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Scale))]
    [NotifyCanExecuteChangedFor(nameof(ZoomInCommand))]
    [NotifyCanExecuteChangedFor(nameof(ZoomOutCommand))]
    private int _scalePercent = WidgetConfigDefaults.DefaultBuffCardScale;

    public EntityBuffDebuffCardWidgetViewModel(
        WidgetListItemViewModel widget,
        EntityWindowTarget target,
        PlayerBuffListKind kind,
        string? requestedBuffKey,
        BuffGroup group)
    {
        _widget = widget;
        _target = target;
        _kind = kind;
        _group = group;
        _requestedBuffKey = group == BuffGroup.None ? requestedBuffKey : null;
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _target.Changed += Target_Changed;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        RefreshPresentation();
        Refresh();
        _refreshTimer.Start();
    }

    public long EntityId => _target.EntityId;

    public Zproto.EEntityType? EntityType => _target.EntityType;

    public long EntityUuid => _target.EntityUuid;

    public bool IsEntityAcquired => _target.IsAcquired;

    public string TargetName => _target.Name;

    public PlayerBuffListKind BuffListKind => _kind;

    public string? RequestedBuffKey => _requestedBuffKey;

    public BuffGroup Group => _group;

    /// <summary>最後に分かったバフ名。保存して、次の起動でタイトルに使う。</summary>
    public string? LastKnownBuffName => _lastKnownBuffName;

    /// <summary>
    /// バフ名が空から確定に変わったときに上がる。対象一覧を書き直す契機。
    /// プレイヤー用カードの <c>SavedTargetInfoResolved</c> と同じ役目。
    /// </summary>
    public event EventHandler? SavedTargetInfoResolved;

    /// <summary>
    /// 設定から復元したときに、バフを観測する前のタイトルを埋める。
    /// <b>実際に観測した値は上書きしない。</b>
    /// </summary>
    public void SeedLastKnownBuffName(string? buffName)
    {
        if (!string.IsNullOrWhiteSpace(_lastKnownBuffName)
            || string.IsNullOrWhiteSpace(buffName))
        {
            return;
        }

        _lastKnownBuffName = buffName;

        // 種を入れただけではタイトルに出ない。窓を作る前にここで組み直す。
        RefreshHeaderText();
    }

    public double Scale => ScalePercent / 100d;

    public bool RepresentsEntity(long entityUuid)
    {
        return _target.Represents(entityUuid);
    }

    public bool RepresentsBuff(PlayerBuffListKind kind, string? buffKey, BuffGroup group)
    {
        if (_kind != kind || _group != group)
        {
            return false;
        }

        // まとまりを追う窓は、個別のバフキーが違っても同じ窓。
        return group != BuffGroup.None
            || string.Equals(_requestedBuffKey, buffKey, StringComparison.Ordinal);
    }

    public bool TryApplyEntity(EntityListEntry entity)
    {
        return _target.TryApply(entity);
    }

    public void RefreshPresentation()
    {
        Refresh();
        RefreshHeaderText();
    }

    /// <summary>
    /// タイトルの前半を「バフ(名前)」「デバフ(名前)」にする。
    /// バフ名が一度も分かっていないときだけウィジェット名のまま。
    /// </summary>
    private void RefreshHeaderText()
    {
        var prefix = BuffDebuffCardContent.CreateHeaderPrefix(
            _widget.DisplayName,
            _kind,
            _group,
            _lastKnownBuffName);

        HeaderText = string.IsNullOrWhiteSpace(_target.DisplayName)
            ? prefix
            : $"{prefix} - {_target.DisplayName}";
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _target.Changed -= Target_Changed;
        _target.Dispose();
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    [RelayCommand(CanExecute = nameof(CanZoomIn))]
    private void ZoomIn()
    {
        ApplyScale(ScalePercent + WidgetConfigDefaults.BuffCardScaleStep);
    }

    [RelayCommand(CanExecute = nameof(CanZoomOut))]
    private void ZoomOut()
    {
        ApplyScale(ScalePercent - WidgetConfigDefaults.BuffCardScaleStep);
    }

    private bool CanZoomIn() => ScalePercent < WidgetConfigDefaults.MaxBuffCardScale;

    private bool CanZoomOut() => ScalePercent > WidgetConfigDefaults.MinBuffCardScale;

    private void ApplyScale(int scalePercent)
    {
        var normalized = WidgetConfigDefaults.ClampBuffCardScale(scalePercent);
        if (normalized == ScalePercent)
        {
            return;
        }

        ScalePercent = normalized;
        _widget.SaveBuffCardScale(_scaleKey, normalized);
    }

    private void Target_Changed(object? sender, EventArgs e)
    {
        RefreshPresentation();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        // タイトルの「バフ/デバフ」もカルチャ依存なので、中身だけでなく組み直す。
        RefreshPresentation();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        var snapshots = MeterSnapshotProvider.GetEntityBuffs(_target.EntityUuid, _kind);
        var snapshot = _group != BuffGroup.None
            ? BuffDebuffCardContent.FindByGroup(snapshots, _group)
            : BuffDebuffCardContent.FindByKey(snapshots, _requestedBuffKey ?? string.Empty);

        // 控えるのは内部ID注記を付けない名前。snapshot.Name は注記込みのことがあり、
        // それを保存すると ID 表示を切ったあともタイトルに残る。
        var rememberedName = snapshot is null
            ? string.Empty
            : BuffDebuffCardContent.GetCardBuffName(snapshot.BaseId);
        if (!string.IsNullOrWhiteSpace(rememberedName))
        {
            var wasUnknown = string.IsNullOrWhiteSpace(_lastKnownBuffName);
            _lastKnownBuffName = rememberedName;
            RefreshHeaderText();

            if (wasUnknown)
            {
                SavedTargetInfoResolved?.Invoke(this, EventArgs.Empty);
            }
        }

        if (snapshot is null)
        {
            // 失効。アイコンと残り時間は消すが、名前の行は残す。
            ApplyContent(
                BuffDebuffCardContent.CreateNameOnly(
                    _lastKnownBuffName,
                    _target.Name,
                    _target.Level,
                    _widget.BuffInfoFormatString),
                hasBuff: false);
            return;
        }

        ApplyContent(
            BuffDebuffCardContent.Create(
                snapshot,
                _target.Name,
                _target.Level,
                _widget.BuffInfoFormatString),
            hasBuff: true);
    }

    /// <summary>
    /// <paramref name="hasBuff"/> は<b>バフが見つかったかどうか</b>で、アイコンが引けたかではない。
    /// </summary>
    private void ApplyContent(BuffDebuffCardContent content, bool hasBuff)
    {
        DisplayText = content.DisplayText;
        DurationText = content.DurationText;
        LayerText = content.LayerText;
        IconPath = content.IconPath;
        HasBuff = hasBuff;
        HasDisplayText = !string.IsNullOrEmpty(content.DisplayText);

        SynchronizeScaleKey(
            BuffDebuffCardContent.CreateScaleKey(_target.EntityId, _group, _requestedBuffKey));
    }

    private void SynchronizeScaleKey(string scaleKey)
    {
        if (string.IsNullOrEmpty(scaleKey)
            || string.Equals(_scaleKey, scaleKey, StringComparison.Ordinal))
        {
            return;
        }

        _scaleKey = scaleKey;
        ScalePercent = _widget.GetBuffCardScale(scaleKey);
    }
}
