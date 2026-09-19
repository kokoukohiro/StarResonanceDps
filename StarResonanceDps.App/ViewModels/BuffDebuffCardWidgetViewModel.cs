using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// プレイヤーのバフ・デバフを1件だけ大きく出すカード。
///
/// <para>
/// <b>失効したら空になるのが正しい挙動。</b>同じ人に同じバフが付き直せば
/// <c>base:{BaseId}</c> のキーが一致するのでそのまま戻る。
/// </para>
/// </summary>
public sealed partial class BuffDebuffCardWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    private readonly PlayerBuffListKind _kind;

    /// <summary>
    /// 追う対象のバフキー。まとまり(<see cref="_group"/>)を追うときは使わない。
    /// </summary>
    private readonly string? _requestedBuffKey;

    /// <summary>
    /// 追うまとまり。<see cref="BuffGroup.None"/> なら個別のバフを追う。
    /// まとまりは対象が居なくても名乗れるので、タイトルが空になることがない。
    /// </summary>
    private readonly BuffGroup _group;

    private readonly DispatcherTimer _refreshTimer;
    private PlayerRosterEntry? _player;
    private string _scaleKey = string.Empty;

    /// <summary>
    /// 最後に分かったバフ名。<b>失効してもタイトルは維持する</b>ので持っておく。
    /// 一度も分かっていないうち(料理バフの自動選択で対象が無い等)は空。
    /// </summary>
    private string _lastKnownBuffName = string.Empty;

    private bool _isDisposed;

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

    public BuffDebuffCardWidgetViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer,
        PlayerBuffListKind kind,
        string? requestedBuffKey,
        BuffGroup group)
        : base(playerWidget, requestedCharacterId)
    {
        _kind = kind;
        _group = group;
        _requestedBuffKey = group == BuffGroup.None ? requestedBuffKey : null;
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
        InitializePlayer(initialPlayer);

        // 基底のコンストラクタが先にタイトルを組むが、そのとき _group も _kind もまだ既定値。
        // フィールドが揃ってから組み直す。
        RefreshHeader();
        Refresh();
        _refreshTimer.Start();
    }

    /// <summary>いま追っているバフのキー。料理バフ自動選択では実際に見つかったものになる。</summary>
    public string ResolvedBuffKey { get; private set; } = string.Empty;

    public PlayerBuffListKind BuffListKind => _kind;

    /// <summary>開いたときに指定されたバフキー。まとまりを追う窓では <c>null</c>。</summary>
    public string? RequestedBuffKey => _requestedBuffKey;

    public BuffGroup Group => _group;

    /// <summary>最後に分かったバフ名。保存して、次の起動でタイトルに使う。</summary>
    public string? LastKnownBuffName => _lastKnownBuffName;

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
        RefreshHeader();
    }

    /// <summary>拡大率。<c>ScaleTransform</c> がそのまま使う倍率。</summary>
    public double Scale => ScalePercent / 100d;

    /// <summary>
    /// タイトルの前半を「バフ(名前)」「デバフ(名前)」にする。
    /// バフ名が一度も分かっていないときだけウィジェット名のまま。
    /// </summary>
    protected override string HeaderPrefix =>
        BuffDebuffCardContent.CreateHeaderPrefix(
            PlayerWidget.DisplayName,
            _kind,
            _group,
            _lastKnownBuffName);

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

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        _player = player;
        Refresh();
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
        PlayerWidget.SaveBuffCardScale(_scaleKey, normalized);
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        if (SelectedCharacterId is not { } characterId)
        {
            ApplyContent(BuffDebuffCardContent.Empty, string.Empty, characterId: 0, hasBuff: false);

            // 対象が分からなくても、まとまり(料理・薬剤)の名前は出せる。
            // ここで組み直さないと、直起動のあいだ既定名のまま固定される。
            RefreshHeader();
            return;
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        var snapshots = MeterSnapshotProvider.GetPlayerBuffs(characterId, _kind);
        var snapshot = _group != BuffGroup.None
            ? BuffDebuffCardContent.FindByGroup(snapshots, _group)
            : BuffDebuffCardContent.FindByKey(snapshots, _requestedBuffKey ?? string.Empty);

        // タイトルはバフ名を含むので、バフを解決してから組む。
        //
        // 控えるのは内部ID注記を付けない名前。snapshot.Name は注記込みのことがあり、
        // それを保存すると ID 表示を切ったあともタイトルに残る。
        var rememberedName = snapshot is null
            ? string.Empty
            : BuffDebuffCardContent.GetCardBuffName(snapshot.BaseId);
        if (!string.IsNullOrWhiteSpace(rememberedName))
        {
            var wasUnknown = string.IsNullOrWhiteSpace(_lastKnownBuffName);
            _lastKnownBuffName = rememberedName;

            if (wasUnknown)
            {
                RaiseSavedTargetInfoResolved();
            }
        }

        if (playerIdentity is not null)
        {
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId);
        }
        else
        {
            RefreshHeader();
        }

        // 名前の出し方はプレイヤー一覧・メーターと同じ規則へ通す。
        // 名前がまだ取れていなければUID、伏せる設定なら伏せ字。
        var targetName = PlayerInfoFormatFormatter.GetDisplayName(
            _player?.Name ?? playerIdentity?.Name,
            playerIdentity?.UserId ?? characterId,
            _player?.IsSelf ?? (AppState.PlayerUID != 0 && characterId == AppState.PlayerUID),
            PlayerRosterPresentationStore.Instance.NameDisplayMode);

        if (snapshot is null)
        {
            // 失効。アイコンと残り時間は消すが、名前の行は残す。
            ApplyContent(
                BuffDebuffCardContent.CreateNameOnly(
                    _lastKnownBuffName,
                    targetName,
                    _player?.Level ?? 0,
                    PlayerWidget.BuffInfoFormatString),
                string.Empty,
                characterId,
                hasBuff: false);
            return;
        }

        var content = BuffDebuffCardContent.Create(
            snapshot,
            targetName,
            _player?.Level ?? 0,
            PlayerWidget.BuffInfoFormatString);

        ApplyContent(content, snapshot.Key, characterId, hasBuff: true);
    }

    /// <summary>
    /// <paramref name="hasBuff"/> は<b>バフが見つかったかどうか</b>で、アイコンが引けたかではない。
    /// アイコンだけ欠けている状態を「バフ無し」に丸めると、名前も残り時間も出なくなって
    /// 失効と見分けがつかなくなる。
    /// </summary>
    private void ApplyContent(
        BuffDebuffCardContent content,
        string buffKey,
        long characterId,
        bool hasBuff)
    {
        ResolvedBuffKey = buffKey;
        DisplayText = content.DisplayText;
        DurationText = content.DurationText;
        LayerText = content.LayerText;
        IconPath = content.IconPath;
        HasBuff = hasBuff;
        HasDisplayText = !string.IsNullOrEmpty(content.DisplayText);

        SynchronizeScaleKey(BuffDebuffCardContent.CreateScaleKey(characterId, _group, buffKey));
    }

    /// <summary>
    /// 倍率の保存キーが変わったら、その組み合わせの保存値を読み直す。
    /// バフを見失っている間はキーが空になるので、そのときは今の倍率を保つ。
    /// </summary>
    private void SynchronizeScaleKey(string scaleKey)
    {
        if (string.IsNullOrEmpty(scaleKey)
            || string.Equals(_scaleKey, scaleKey, StringComparison.Ordinal))
        {
            return;
        }

        _scaleKey = scaleKey;
        ScalePercent = PlayerWidget.GetBuffCardScale(scaleKey);
    }
}
