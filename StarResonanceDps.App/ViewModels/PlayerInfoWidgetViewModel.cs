using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PlayerInfoWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    /// <summary>顔の既定の絵。写真を取れたとき以外はこれを出す。</summary>
    private const string DefaultAvatarResourceKey = "Icon.Avatar.head_photo_000";

    /// <summary>名刺の既定の絵。写真を取れたとき以外はこれを出す。</summary>
    private const string DefaultCardResourceKey = "Icon.IdCard.idcard_common_01";

    /// <summary>表示値。相手も覚えている素性も無い間は null(画面ごと隠す)。値で比べ、前回と同じなら差し替えない(画面を作り直さない)。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlayerInfo))]
    private PlayerInfoEntry? _playerInfo;

    /// <summary>顔の絵。写真を取れたら写真、それ以外(照会がまだ届いていない・写真が無い・取得中・取得に失敗)は既定の絵。</summary>
    [ObservableProperty]
    private ImageSource? _avatarImage;

    /// <summary>名刺の半身の絵。出し方は顔と同じ。</summary>
    [ObservableProperty]
    private ImageSource? _cardImage;

    private readonly PhotoSlot _avatarSlot;
    private readonly PhotoSlot _cardSlot;
    private PlayerRosterEntry? _player;

    /// <summary>アイコンカラーの塗り。基底のコンストラクタから描き直しが呼ばれることがあるので、既定値で始める。</summary>
    private PlayerInfoIconPalette _iconPalette = new(WidgetConfigDefaults.CreatePlayerInfoSettings());

    private bool _isDisposed;

    public PlayerInfoWidgetViewModel(
        WidgetListItemViewModel infoWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(infoWidget, requestedCharacterId)
    {
        _iconPalette = new PlayerInfoIconPalette(infoWidget.GetPlayerInfoSettingsSnapshot());
        infoWidget.PlayerInfoSettingsChanged += InfoWidget_PlayerInfoSettingsChanged;
        _avatarSlot = new PhotoSlot(DefaultAvatarResourceKey, image => AvatarImage = image);
        _cardSlot = new PhotoSlot(DefaultCardResourceKey, image => CardImage = image);
        // 相手が決まる前から既定の絵を用意する(何も分からない間は画面ごと隠れる)。
        RefreshPhotos(force: true);
        InitializePlayer(initialPlayer);
        // 相手がまだ一覧に載っていなくても、覚えている素性があれば見出しと「不明」を出す。
        RenderPlayerInfo();
    }

    public WidgetListItemViewModel InfoWidget => PlayerWidget;

    /// <summary>何か出すものがあるか。無ければ画面ごと隠して、ほかのウィンドウと同じく何も出さない。</summary>
    public bool HasPlayerInfo => PlayerInfo is not null;

    /// <summary>自分の顔の写真を出しているときだけ、その写真。右クリックのメニュー(保存・コピー)はこれがあるときだけ出す。</summary>
    public PlayerPhoto? SavableAvatarPhoto => _player?.IsSelf == true ? _avatarSlot.Photo : null;

    /// <summary>自分の名刺の写真を出しているときだけ、その写真。扱いは顔と同じ。</summary>
    public PlayerPhoto? SavableCardPhoto => _player?.IsSelf == true ? _cardSlot.Photo : null;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        InfoWidget.PlayerInfoSettingsChanged -= InfoWidget_PlayerInfoSettingsChanged;
    }

    /// <summary>アイコンカラーの設定(保存かプレビュー)が変わった。塗りを作り直す。</summary>
    private void InfoWidget_PlayerInfoSettingsChanged(object? sender, EventArgs e)
    {
        _iconPalette = new PlayerInfoIconPalette(InfoWidget.GetPlayerInfoSettingsSnapshot());
        RenderPlayerInfo();
    }

    /// <summary>相手が一覧から消えても最後に届いた値のまま残す。現在地は「不明」になる。顔と名刺は同じ相手なので取り直さない。</summary>
    protected override bool KeepsLastPlayerWhenRemoved => true;

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        // 窓を開いたとき(新しい窓は最初の相手が必ず「別の相手」になる)と相手が変わったときは、同じ URL でも取り直す。
        var isOtherPlayer = player?.CharacterId != _player?.CharacterId;
        _player = player;
        RenderPlayerInfo();
        RefreshPhotos(force: isOtherPlayer);
    }

    /// <summary>
    /// 現在地と、ライブの行のシーズン・シーズン階級は一覧の通知の場所とシーズンから決まるので、それが変わったら作り直す
    /// (履歴の回から来た行は、行が持つその回のシーズン)。
    /// </summary>
    protected override void OnRosterContextChanged()
    {
        RenderPlayerInfo();
    }

    /// <summary>写真の URL が変わり得る。写真は URL が変わったときだけ取り直す。</summary>
    protected override void OnSocialDataChanged()
    {
        RefreshPhotos(force: false);
    }

    /// <summary>
    /// ログアウト。残していた最後の行は基底が捨てる(相手が変わったときと同じく既定の絵に戻る)。
    /// 行が無い表示は覚えている素性(捨てた NPC の印と職業を含む)から作るので出し直す。
    /// </summary>
    protected override void OnResetToStartup()
    {
        RenderPlayerInfo();
    }

    protected override WidgetDataStatus CreateDataStatus(GameReceptionSnapshot reception, PlayerRosterEntry? player)
    {
        var hasPhoto = player is not null
            && (player.RecordedAvatar ?? SocialDataStore.GetAvatar(player.CharacterId)) is not null;
        return WidgetDataStatusRules.ForPlayerInfo(
            reception,
            EncounterHistoryProvider.HasLoadFailed,
            IsSelfTarget,
            player,
            hasPhoto);
    }

    /// <summary>相手がまだ一覧に載っていない間は、覚えている素性(保存から戻した名前と UID)が入ったら出し直す。</summary>
    protected override void OnLastKnownPlayerChanged()
    {
        if (_player is null)
        {
            RenderPlayerInfo();
        }
    }

    private void RenderPlayerInfo()
    {
        // 相手も覚えている素性も無い(タイトルに名前が出ない)間は何も出さない。
        if (_player is null
            && string.IsNullOrWhiteSpace(LastKnownPlayerName)
            && LastKnownPlayerUid == 0)
        {
            PlayerInfo = null;
            return;
        }

        PlayerInfo = _player is null
            ? PlayerInfoEntry.CreateUnknown(
                LastKnownPlayerName,
                LastKnownPlayerUid,
                IsSelfTarget,
                LastKnownIsNpc,
                LastKnownProfessionId,
                RosterSeasonId,
                _iconPalette)
            : PlayerInfoEntry.Create(
                _player,
                RosterMapName,
                RosterMapChannel,
                _player.SeasonId ?? RosterSeasonId,
                _iconPalette,
                IsProfileUnknown(_player));
    }

    /// <summary>
    /// 冒険者レベル・能力スコアなどがまだ届いていないか。自分は途中起動(全体の値がまだ)、他の人は素性の欠け(周りでの出現も名刺も届いていない)。
    /// </summary>
    private static bool IsProfileUnknown(PlayerRosterEntry player)
    {
        return player.IsSelf
            ? GameReceptionStateStore.Current.IsMidLaunch
            : player.DataGaps.HasFlag(PlayerDataGaps.Identity);
    }

    /// <summary>
    /// 写真のリンクは、履歴の回から来た行ならその回に保存した値、無ければ(ライブの行・保存の時点で届いていなかった人・前の回)名刺の店の今の値。
    /// </summary>
    private void RefreshPhotos(bool force)
    {
        var characterId = _player?.CharacterId ?? 0;
        var avatar = characterId == 0 ? null : _player?.RecordedAvatar ?? SocialDataStore.GetAvatar(characterId);
        _avatarSlot.Show(GetAvatarPhotoUrl(avatar), characterId, force);
        _cardSlot.Show(GetCardPhotoUrl(avatar), characterId, force);
    }

    /// <summary>
    /// 顔の写真の URL。アップロードした写真の人(<c>avatarId</c> 0 か 1)で審査済みの写真があるときだけ。
    /// ゲームの用意したアイコンを選んだ人(それ以外の <c>avatarId</c>)の絵は持っていないので無し(既定の絵)。
    /// </summary>
    private static string? GetAvatarPhotoUrl(SocialAvatarState? avatar)
    {
        return avatar is { AvatarId: 0 or 1, IsProfileReviewed: true } && !string.IsNullOrEmpty(avatar.ProfileUrl)
            ? avatar.ProfileUrl
            : null;
    }

    /// <summary>名刺の半身の写真の URL。審査済みの写真があるときだけ(ゲームの名刺と同じ条件)。</summary>
    private static string? GetCardPhotoUrl(SocialAvatarState? avatar)
    {
        return avatar is { IsHalfBodyReviewed: true } && !string.IsNullOrEmpty(avatar.HalfBodyUrl)
            ? avatar.HalfBodyUrl
            : null;
    }

    /// <summary>
    /// 顔と名刺それぞれの絵。写真の URL があれば取っている間も既定の絵を出し、取れたら写真に替える。
    /// 取れなかったら既定の絵のまま(失敗は <see cref="PlayerPhotoLoader"/> がログに出す)。
    /// </summary>
    private sealed class PhotoSlot(string defaultResourceKey, Action<ImageSource?> apply)
    {
        private bool _hasShown;
        private string? _shownUrl;
        private CancellationTokenSource? _loading;

        /// <summary>いま出している写真。既定の絵を出している間(取得中も含む)は null。</summary>
        public PlayerPhoto? Photo { get; private set; }

        /// <param name="url">写真の URL。無ければ既定の絵。</param>
        /// <param name="force">同じ URL でも取り直す。</param>
        public void Show(string? url, long characterId, bool force)
        {
            if (!force && _hasShown && url == _shownUrl)
            {
                return;
            }

            _hasShown = true;
            _shownUrl = url;
            // 取り消しは取得中の要求が見ているので Dispose しない。
            _loading?.Cancel();
            _loading = null;

            Photo = null;
            apply(Application.Current?.TryFindResource(defaultResourceKey) as ImageSource);
            if (url is null)
            {
                return;
            }

            var loading = new CancellationTokenSource();
            _loading = loading;
            _ = LoadAsync(url, characterId, loading);
        }

        private async Task LoadAsync(string url, long characterId, CancellationTokenSource loading)
        {
            var photo = await PlayerPhotoLoader.LoadAsync(url, characterId, loading.Token);
            if (photo is not null && !loading.IsCancellationRequested)
            {
                Photo = photo;
                apply(photo.Image);
            }
        }
    }
}
