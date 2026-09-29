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

    [ObservableProperty]
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

    /// <summary>アイコンカラー。基底のコンストラクタから描き直しが呼ばれることがあるので、既定値で始める。</summary>
    private PlayerInfoWidgetSettingsConfig _settings = WidgetConfigDefaults.CreatePlayerInfoSettings();

    private bool _isDisposed;

    public PlayerInfoWidgetViewModel(
        WidgetListItemViewModel infoWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(infoWidget, requestedCharacterId)
    {
        _settings = infoWidget.GetPlayerInfoSettingsSnapshot();
        infoWidget.PlayerInfoSettingsChanged += InfoWidget_PlayerInfoSettingsChanged;
        _avatarSlot = new PhotoSlot(DefaultAvatarResourceKey, image => AvatarImage = image);
        _cardSlot = new PhotoSlot(DefaultCardResourceKey, image => CardImage = image);
        // 相手が決まる前から既定の絵を出す。
        RefreshPhotos(force: true);
        InitializePlayer(initialPlayer);
        // 相手がまだ一覧に載っていなくても、見出しと「不明」を出す。
        RenderPlayerInfo();
    }

    public WidgetListItemViewModel InfoWidget => PlayerWidget;

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

    /// <summary>アイコンカラーの設定(保存かプレビュー)が変わった。</summary>
    private void InfoWidget_PlayerInfoSettingsChanged(object? sender, EventArgs e)
    {
        _settings = InfoWidget.GetPlayerInfoSettingsSnapshot();
        RenderPlayerInfo();
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        // 相手が一覧から消えたら(パーティ外で周りから外れた)、最後に届いた値のまま残す。
        // 現在地だけは分からなくなるので「不明」にする。顔と名刺は同じ相手なので取り直さない。
        if (player is null && _player is not null)
        {
            player = _player with { IsNearby = false, PartySceneId = 0, PartyLineId = 0 };
        }

        // 窓を開いたとき(新しい窓は最初の相手が必ず「別の相手」になる)と相手が変わったときは、同じ URL でも取り直す。
        var isOtherPlayer = player?.CharacterId != _player?.CharacterId;
        _player = player;
        RenderPlayerInfo();
        RefreshPhotos(force: isOtherPlayer);
    }

    /// <summary>現在地とシーズン・シーズンランクは一覧の通知の場所とシーズンから決まるので、それが変わったら作り直す。</summary>
    protected override void OnRosterContextChanged()
    {
        RenderPlayerInfo();
    }

    /// <summary>写真の URL が変わり得る。写真は URL が変わったときだけ取り直す。</summary>
    protected override void OnSocialDataChanged()
    {
        RefreshPhotos(force: false);
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
        PlayerInfo = _player is null
            ? PlayerInfoEntry.CreateUnknown(
                LastKnownPlayerName,
                LastKnownPlayerUid,
                IsSelfTarget,
                LastKnownIsNpc,
                LastKnownProfessionId,
                RosterSeasonId,
                _settings)
            : PlayerInfoEntry.Create(_player, RosterMapName, RosterMapChannel, RosterSeasonId, _settings);
    }

    private void RefreshPhotos(bool force)
    {
        var characterId = _player?.CharacterId ?? 0;
        var avatar = characterId == 0 ? null : SocialDataStore.GetAvatar(characterId);
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
