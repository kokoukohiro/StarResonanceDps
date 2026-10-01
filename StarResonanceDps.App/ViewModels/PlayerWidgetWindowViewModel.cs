using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Services;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public abstract class PlayerWidgetWindowViewModel : ViewModelBase
{
    private readonly long? _requestedCharacterId;
    private readonly bool _showPlayerIdentityInHeader;
    private long? _representedCharacterId;
    private PlayerRosterEntry? _selectedPlayer;
    private string _headerText = string.Empty;

    /// <summary>
    /// 最後に確定した相手の素性。<b>ロスターから消えてもタイトルを保つために持つ。</b>
    ///
    /// <para>
    /// マップ移動の直後はロスターが作り直されるので、そのプレイヤーが一時的に居なくなる。
    /// これが無いと、その瞬間にタイトルがウィジェット名だけに戻ってしまう。
    /// </para>
    /// </summary>
    private string? _lastKnownPlayerName;

    private long _lastKnownPlayerUid;

    /// <summary>最後に確定した相手の NPC の印。タイトルに名前の規則(NPC は職業名)を当てるために持つ。</summary>
    private bool _lastKnownIsNpc;

    /// <summary>最後に確定した相手の職業。NPC のタイトルに出す職業名の元。</summary>
    private int _lastKnownProfessionId;

    protected PlayerWidgetWindowViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        bool showPlayerIdentityInHeader = true)
    {
        PlayerWidget = playerWidget;
        _requestedCharacterId = requestedCharacterId;
        _showPlayerIdentityInHeader = showPlayerIdentityInHeader;

        // 一覧の通知は中身が変わるまで来ないので、開いた時点の値から始める。
        var roster = PlayerRosterPresentationStore.Instance.Current;
        RosterMapName = roster.MapName;
        RosterMapChannel = roster.MapChannel;
        RosterSeasonId = roster.SeasonId;

        RefreshHeaderText();
    }

    /// <summary>一覧の通知のマップ名(自分のいる場所)。</summary>
    protected string RosterMapName { get; private set; }

    /// <summary>一覧の通知のチャンネル番号。チャンネルの無い場所では 0。</summary>
    protected uint RosterMapChannel { get; private set; }

    /// <summary>一覧の通知の今のシーズン番号。まだ分からなければ 0。</summary>
    protected int RosterSeasonId { get; private set; }

    /// <summary>一覧の通知の場所とシーズンを受け取る。変わったら <see cref="OnRosterContextChanged"/> を呼ぶ。</summary>
    public void UpdateRosterContext(string mapName, uint mapChannel, int seasonId)
    {
        if (string.Equals(RosterMapName, mapName, StringComparison.Ordinal)
            && RosterMapChannel == mapChannel
            && RosterSeasonId == seasonId)
        {
            return;
        }

        RosterMapName = mapName;
        RosterMapChannel = mapChannel;
        RosterSeasonId = seasonId;
        OnRosterContextChanged();
    }

    /// <summary>場所か今のシーズンが変わったとき。それを表示に使う窓だけが上書きする。</summary>
    protected virtual void OnRosterContextChanged()
    {
    }

    /// <summary>
    /// 名刺の値(顔写真・名刺)が変わった相手。その相手を映している窓(一覧から外れて最後の値を残している窓も含む)だけ
    /// <see cref="OnSocialDataChanged"/> を呼ぶ。
    /// </summary>
    public void NotifySocialDataChanged(long characterId)
    {
        if (RepresentsPlayer(characterId))
        {
            OnSocialDataChanged();
        }
    }

    /// <summary>名刺の値の控えが全部消えた(キャプチャの停止とログアウト)。相手を問わず <see cref="OnSocialDataChanged"/> を呼ぶ。</summary>
    public void NotifySocialDataCleared()
    {
        OnSocialDataChanged();
    }

    /// <summary>映している相手の名刺の値が変わったとき。それを表示に使う窓だけが上書きする。</summary>
    protected virtual void OnSocialDataChanged()
    {
    }

    /// <summary>
    /// ログアウトで起動直後の状態へ戻したとき。覚えている素性のうち、保存して次の起動へ持ち越す名前と UID だけを残し、
    /// NPC の印と職業は捨てる(起動時の復元と同じ)。
    /// </summary>
    public void ResetToStartup()
    {
        _lastKnownIsNpc = false;
        _lastKnownProfessionId = 0;
        RenderHeaderText();
        OnResetToStartup();
    }

    /// <summary>ログアウトで起動直後の状態へ戻したとき。一覧の行の写しを残している窓だけが上書きする。</summary>
    protected virtual void OnResetToStartup()
    {
    }

    public WidgetListItemViewModel PlayerWidget { get; }

    /// <summary>
    /// 窓を開いたときの指定。<c>null</c> は「自分」。
    /// <b>保存にはこれを使う。</b>解決後のIDを保存すると、キャラを変えたときに
    /// 前のキャラの窓として復元されてしまう。
    /// </summary>
    public long? RequestedCharacterId => _requestedCharacterId;

    /// <summary>最後に確定した相手の名前。保存して、次の起動でタイトルに使う。</summary>
    public string? LastKnownPlayerName => _lastKnownPlayerName;

    /// <summary>最後に確定した相手のUID。名前と対にして保存する。</summary>
    public long LastKnownPlayerUid => _lastKnownPlayerUid;

    /// <summary>最後に確定した相手の NPC の印。</summary>
    protected bool LastKnownIsNpc => _lastKnownIsNpc;

    /// <summary>最後に確定した相手の職業。</summary>
    protected int LastKnownProfessionId => _lastKnownProfessionId;

    /// <summary>自分を映す窓か。「自分」の指定で開いた窓と、覚えている UID が自分のものの窓。</summary>
    protected bool IsSelfTarget => _requestedCharacterId is null
        || (AppState.PlayerUID != 0 && _lastKnownPlayerUid == AppState.PlayerUID);

    /// <summary>
    /// 保存しておきたい素性が空から確定に変わったときに上がる。
    /// 対象一覧を書き直す契機になる(窓の開閉だけだと、開いた直後はまだ空のまま)。
    /// プレイヤー名だけでなく、派生側が持つ値(カードのバフ名など)からも上げる。
    /// </summary>
    public event EventHandler? SavedTargetInfoResolved;

    /// <summary>派生側が持つ保存対象の値が確定したときに呼ぶ。</summary>
    protected void RaiseSavedTargetInfoResolved()
    {
        SavedTargetInfoResolved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 設定から復元したときに、相手が見つかる前のタイトルを埋める。
    /// <b>実際に観測した値は上書きしない。</b>
    /// </summary>
    public void SeedLastKnownPlayer(string? playerName, long playerUid)
    {
        if (playerUid == 0
            || (!string.IsNullOrWhiteSpace(_lastKnownPlayerName) && _lastKnownPlayerUid != 0))
        {
            return;
        }

        // 名前が保存されていなくてもUIDは入れる。名前の無い相手でも素性を出せるように。
        if (string.IsNullOrWhiteSpace(_lastKnownPlayerName)
            && !string.IsNullOrWhiteSpace(playerName))
        {
            _lastKnownPlayerName = playerName;
        }

        _lastKnownPlayerUid = playerUid;
        RefreshHeaderText();
        OnLastKnownPlayerChanged();
    }

    /// <summary>
    /// 覚えている相手の素性(名前・UID・NPC の印・職業)を書いたとき。
    /// 相手が一覧に載る前から素性を表示に使う窓だけが上書きする。
    /// </summary>
    protected virtual void OnLastKnownPlayerChanged()
    {
    }

    protected long? SelectedCharacterId => _requestedCharacterId ?? _representedCharacterId;

    /// <summary>
    /// タイトルの前半。既定はウィジェット名。
    /// バフ・デバフカードのように、窓ごとに名乗りを変えるものだけが差し替える。
    /// </summary>
    protected virtual string HeaderPrefix => PlayerWidget.DisplayName;

    public string HeaderText
    {
        get => _headerText;
        private set => SetProperty(ref _headerText, value);
    }

    /// <summary>
    /// 観測した素性でタイトルを組み直す。
    ///
    /// <para>
    /// <b>空の名前は「新しい情報なし」として捨てる。</b>
    /// <c>PlayerDataSourceResolver</c> の名前は3経路(近傍・PT補完・メタデータ)が全部空だと
    /// 空文字を返す。フルコンテナが来ていない状態では実際にそうなるので、
    /// そのまま書き込むと<b>復元済みのタイトルが消える</b>。
    /// </para>
    /// </summary>
    protected void SetHeaderText(string playerName, long playerUid, bool isNpc, int professionId)
    {
        RememberPlayer(playerName, playerUid, isNpc, professionId);
        RenderHeaderText();
    }

    public bool RepresentsPlayer(long characterId)
    {
        return _requestedCharacterId == characterId
            || _representedCharacterId == characterId;
    }

    public void UpdatePlayerFromRoster(
        IReadOnlyDictionary<long, PlayerRosterEntry> playersByCharacterId,
        PlayerRosterEntry? selfPlayer)
    {
        PlayerRosterEntry? player = null;

        if (_requestedCharacterId is { } characterId)
        {
            playersByCharacterId.TryGetValue(characterId, out player);
        }
        else
        {
            player = selfPlayer;
        }

        UpdatePlayer(player);

        // ロスターに載っていない相手は UpdatePlayer が素通りする(null → null)。
        // 素性がまだ空のときだけ、ロスター以外の経路で引き直す機会を作る。
        if (player is null && string.IsNullOrWhiteSpace(_lastKnownPlayerName))
        {
            RefreshHeaderText();
        }
    }

    public void RefreshPlayerPresentation()
    {
        RefreshHeaderText();
        OnSelectedPlayerChanged(_selectedPlayer);
    }

    protected void InitializePlayer(PlayerRosterEntry? player)
    {
        UpdatePlayer(player);
    }

    protected abstract void OnSelectedPlayerChanged(PlayerRosterEntry? player);

    private void UpdatePlayer(PlayerRosterEntry? player)
    {
        if (player is not null)
        {
            if (_requestedCharacterId is null)
            {
                _representedCharacterId = player.CharacterId;
            }

            RememberPlayer(player.Name, player.CharacterId, player.IsNpc, player.ProfessionId);
        }

        if (EqualityComparer<PlayerRosterEntry?>.Default.Equals(_selectedPlayer, player))
        {
            return;
        }

        _selectedPlayer = player;
        RefreshHeaderText();
        OnSelectedPlayerChanged(player);
    }

    /// <summary>タイトルを組み直す。<see cref="HeaderPrefix"/> が変わった側から呼ぶ。</summary>
    protected void RefreshHeader()
    {
        RefreshHeaderText();
    }

    private void RefreshHeaderText()
    {
        if (_selectedPlayer is null)
        {
            ResolveIdentityWithoutRoster();
        }
        else
        {
            RememberPlayer(
                _selectedPlayer.Name,
                _selectedPlayer.CharacterId,
                _selectedPlayer.IsNpc,
                _selectedPlayer.ProfessionId);
        }

        RenderHeaderText();
    }

    /// <summary>
    /// タイトルを描き直す。素性は<b>記憶している値だけ</b>を使う。
    ///
    /// <para>
    /// 記憶が動くのは「不明 → 確定」と「確定 → 別の確定」だけで、
    /// <b>確定 → 不明には戻らない</b>(<see cref="RememberPlayer"/> が空を弾く)。
    /// 名前とUIDを必ず同じ組で扱うのもここに集約するため。
    /// </para>
    /// </summary>
    private void RenderHeaderText()
    {
        var hasName = !string.IsNullOrWhiteSpace(_lastKnownPlayerName);

        if (!_showPlayerIdentityInHeader || (!hasName && _lastKnownPlayerUid == 0))
        {
            HeaderText = HeaderPrefix;
            return;
        }

        var isSelf = IsSelfTarget;

        // 名前の出し方はプレイヤー一覧・メーターと同じ規則へ通す。
        // 名前がまだ取れていなければUID、伏せる設定なら伏せ字、NPC なら職業名になるので、
        // タイトル専用の分岐は要らない。
        HeaderText = $"{HeaderPrefix} - {PlayerInfoFormatFormatter.GetDisplayName(
            _lastKnownPlayerName,
            _lastKnownPlayerUid,
            isSelf,
            _lastKnownIsNpc,
            _lastKnownProfessionId,
            PlayerRosterPresentationStore.Instance.NameDisplayMode)}";
    }

    /// <summary>
    /// ロスターを待たずに素性を引く。
    ///
    /// <para>
    /// プレイヤー一覧の投影は街で待機中などに空になる。そこに載るのを待つ作りだと、
    /// <b>自分を対象にした窓のタイトルが状況次第で出たり出なかったりする</b>。
    /// エンカウンター側の実体から直接引けば、投影の有無に左右されない。
    /// </para>
    /// </summary>
    private void ResolveIdentityWithoutRoster()
    {
        var identity = _requestedCharacterId is { } characterId
            ? MeterSnapshotProvider.GetPlayerIdentity(characterId)
            : MeterSnapshotProvider.GetSelfPlayerIdentity();

        if (identity is null)
        {
            return;
        }

        if (_requestedCharacterId is null)
        {
            _representedCharacterId = identity.UserId;
        }

        RememberPlayer(identity.Name, identity.UserId, identity.IsNpc, identity.ProfessionId);
    }

    /// <summary>
    /// 素性を覚える。<b>名前とUIDは別々に扱う。</b>
    ///
    /// <para>
    /// 名前がまだ取れていない相手でもUIDは分かるので、UIDだけでも記憶する。
    /// そうしないと「名前の無い相手」でタイトルから素性が丸ごと消える。
    /// </para>
    ///
    /// <para>
    /// <b>伏せ字は名前として記憶しない。</b>ロスターは名前を伏せる設定のとき
    /// <c>Name</c> を伏せ字に差し替えて渡すので、そのまま覚えると
    /// 表示を戻したあとも伏せ字が残る。
    /// </para>
    ///
    /// <para>
    /// <b>NPC の印と職業は相手ごとの値。</b>相手(UID)が変わったら入れ替える。
    /// 同じ相手なら名前と同じく分かった値を消さない(NPC の印は true から戻さない、職業は 0 で上書きしない)。
    /// パーティを離れると NPC の印を運ぶ社交データが切れ、ロスターを通らない素性では false になるため。
    /// </para>
    /// </summary>
    private void RememberPlayer(string? playerName, long playerUid, bool isNpc, int professionId)
    {
        var name = string.Equals(
            playerName,
            PlayerRosterPresentationStore.HiddenPlayerName,
            StringComparison.Ordinal)
                ? null
                : playerName;

        var wasUnknown = string.IsNullOrWhiteSpace(_lastKnownPlayerName)
            && _lastKnownPlayerUid == 0;

        if (playerUid != 0 && playerUid != _lastKnownPlayerUid)
        {
            _lastKnownIsNpc = isNpc;
            _lastKnownProfessionId = professionId;
        }
        else
        {
            _lastKnownIsNpc |= isNpc;
            if (professionId > 0)
            {
                _lastKnownProfessionId = professionId;
            }
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            _lastKnownPlayerName = name;
        }

        if (playerUid != 0)
        {
            _lastKnownPlayerUid = playerUid;
        }

        if (wasUnknown
            && (!string.IsNullOrWhiteSpace(_lastKnownPlayerName) || _lastKnownPlayerUid != 0))
        {
            SavedTargetInfoResolved?.Invoke(this, EventArgs.Empty);
        }

        OnLastKnownPlayerChanged();
    }
}
