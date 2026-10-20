using System.Windows.Threading;
using System.Windows.Media;
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

    /// <summary>右クリックの「グループに追加」の行き先。</summary>
    private readonly IBuffDebuffGroupEntryAdder? _groupEntryAdder;

    /// <summary>通知テキスト(効果時間切れ・薬剤・料理バフ2分以下)の判定。カードを開いている間だけ動く。</summary>
    private readonly BuffCardNotificationTracker _notificationTracker;

    private PlayerRosterEntry? _player;
    private string _scaleKey = string.Empty;

    /// <summary>
    /// 最後に分かったバフ名。<b>失効してもタイトルは維持する</b>ので持っておく。
    /// 一度も分かっていないうち(料理バフの自動選択で対象が無い等)は空。
    /// </summary>
    private string _lastKnownBuffName = string.Empty;

    /// <summary>
    /// 最後に出していた実体の付与者(プレイヤーでなければ null)と、その対象。失効した後の付与者プレイヤー名に使う。
    /// 対象が替わったら前の対象の付与者は使わない。
    /// </summary>
    private TakenDamageLogParty? _lastKnownCaster;
    private long _lastKnownCasterCharacterId;

    private bool _isDisposed;

    [ObservableProperty]
    private string _displayText = string.Empty;

    [ObservableProperty]
    private string _durationText = string.Empty;

    [ObservableProperty]
    private string _layerText = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    /// <summary>絵が Icons.xaml の絵(装備効果)なら型抜き(<see cref="BuffDebuffCardContent.IconMask"/>)。</summary>
    [ObservableProperty]
    private ImageBrush? _iconMask;

    /// <summary>絵がモジュール効果の絵か(<see cref="BuffDebuffCardContent.IsModuleEffectIcon"/>)。絵の大きさを切り替える。</summary>
    [ObservableProperty]
    private bool _isModuleEffectIcon;

    /// <summary>絵が因子の絵か(<see cref="BuffDebuffCardContent.IsFactorIcon"/>)。絵の位置を切り替える。</summary>
    [ObservableProperty]
    private bool _isFactorIcon;

    /// <summary>絵のパスはあるのにファイルが見つからないか(<see cref="BuffDebuffCardContent.ShowsUnknownIcon"/>)。はてなを出す。</summary>
    [ObservableProperty]
    private bool _showsUnknownIcon;

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
        BuffGroup group,
        IBuffDebuffGroupEntryAdder? groupEntryAdder = null)
        : base(playerWidget, requestedCharacterId)
    {
        _kind = kind;
        _group = group;
        _groupEntryAdder = groupEntryAdder;
        _requestedBuffKey = group == BuffGroup.None ? requestedBuffKey : null;
        _notificationTracker = new BuffCardNotificationTracker(playerWidget.GetBuffCardSettingsSnapshot);
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

    /// <summary>右クリックのメニューの色を引くウィジェット。</summary>
    public WidgetListItemViewModel Widget => PlayerWidget;

    /// <summary>右クリックのメニューの項目「グループに追加」。</summary>
    public string AddToGroupMenuText => LocalizationManager.Instance.GetString("BuffList_Menu_AddToGroup");

    /// <summary>
    /// このカードのバフをバフ・デバフグループに足せるか。自分のカードだけで、グループにまだオンで入っていないとき真。
    /// 偽なら右クリックのメニューを開かない。バフが今付いていなくても、追っているバフ(まとまり)で決まる。
    /// </summary>
    public bool CanAddToGroup()
    {
        return IsSelfTarget
            && _groupEntryAdder is not null
            && ResolveGroupEntryKey() is { } key
            && _groupEntryAdder.CanAdd(key);
    }

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

    [RelayCommand]
    private void AddToGroup()
    {
        if (CanAddToGroup() && ResolveGroupEntryKey() is { } key)
        {
            _groupEntryAdder!.Add(key);
        }
    }

    /// <summary>グループの行の鍵。まとまりを追うカードはまとまり、個別のバフのカードは追っているカードの鍵の番号。</summary>
    private BuffEntryKey? ResolveGroupEntryKey()
    {
        if (_group != BuffGroup.None)
        {
            return BuffEntryKey.ForGroup(_group);
        }

        return MeterSnapshotProvider.TryGetBuffIdFromCardKey(_requestedBuffKey, out var buffId)
            ? BuffEntryKey.ForBuffDebuffGroup(buffId)
            : null;
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
        OnPropertyChanged(nameof(AddToGroupMenuText));
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
            _notificationTracker.Reset();
            ApplyContent(BuffDebuffCardContent.Empty, string.Empty, characterId: 0, hasBuff: false);

            // 対象が分からなくても、まとまり(料理・薬剤)の名前は出せる。
            // ここで組み直さないと、直起動のあいだ既定名のまま固定される。
            RefreshHeader();
            return;
        }

        var playerIdentity = MeterSnapshotProvider.GetPlayerIdentity(characterId);
        // カードは番号ごとに1枚で、出すのは残り時間の一番長い実体。一覧の「同種バフの表示」は効かない。
        var snapshots = MeterSnapshotProvider.GetPlayerBuffs(characterId, _kind, groupSameBuffs: true);
        var snapshot = _group != BuffGroup.None
            ? BuffDebuffCardContent.FindByGroup(snapshots, _group)
            : BuffDebuffCardContent.FindByKey(snapshots, _requestedBuffKey ?? string.Empty);

        // タイトルはバフ名を含むので、バフを解決してから組む。
        //
        // 控えるのは内部ID注記を付けない名前。snapshot.Name は注記込みのことがあり、
        // それを保存すると ID 表示を切ったあともタイトルに残る。
        var rememberedName = snapshot is null
            ? string.Empty
            : BuffDebuffCardContent.GetCardBuffName(snapshot);
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
            SetHeaderText(playerIdentity.Name, playerIdentity.UserId, playerIdentity.IsNpc, playerIdentity.ProfessionId);
        }
        else
        {
            RefreshHeader();
        }

        // 名前の出し方はプレイヤー一覧・メーターと同じ規則へ通す。
        // 名前がまだ取れていなければUID、伏せる設定なら伏せ字、NPC なら職業名。
        var targetName = PlayerInfoFormatFormatter.GetDisplayName(
            _player?.Name ?? playerIdentity?.Name,
            playerIdentity?.UserId ?? characterId,
            _player?.IsSelf ?? (AppState.PlayerUID != 0 && characterId == AppState.PlayerUID),
            _player?.IsNpc ?? playerIdentity?.IsNpc ?? false,
            _player?.ProfessionId ?? playerIdentity?.ProfessionId ?? 0,
            PlayerRosterPresentationStore.Instance.NameDisplayMode);

        _notificationTracker.Observe(characterId, snapshot, targetName);

        if (snapshot is null)
        {
            // 失効。アイコンと残り時間は消すが、名前の行は残す。
            ApplyContent(
                BuffDebuffCardContent.CreateNameOnly(
                    _lastKnownBuffName,
                    targetName,
                    _player?.Level ?? 0,
                    PlayerWidget.BuffInfoFormatString,
                    _lastKnownCasterCharacterId == characterId ? _lastKnownCaster : null),
                string.Empty,
                characterId,
                hasBuff: false);
            return;
        }

        _lastKnownCaster = snapshot.Caster;
        _lastKnownCasterCharacterId = characterId;

        var content = BuffDebuffCardContent.Create(
            snapshot,
            targetName,
            _player?.Level ?? 0,
            PlayerWidget.BuffInfoFormatString);

        ApplyContent(content, snapshot.CardKey, characterId, hasBuff: true);
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
        IconMask = content.IconMask;
        IsModuleEffectIcon = content.IsModuleEffectIcon;
        IsFactorIcon = content.IsFactorIcon;
        ShowsUnknownIcon = content.ShowsUnknownIcon;
        HasBuff = hasBuff;
        HasDisplayText = !string.IsNullOrEmpty(content.DisplayText);

        SynchronizeScaleKey(BuffDebuffCardContent.CreateScaleKey(characterId, _group, _requestedBuffKey));
    }

    /// <summary>
    /// 倍率の保存キーが変わったら、その組み合わせの保存値を読み直す。
    /// 対象が分からない間はキーが空になるので、そのときは今の倍率を保つ。
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
