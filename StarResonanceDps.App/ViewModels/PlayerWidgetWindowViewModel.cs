using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.CombatRuntime;
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

    protected PlayerWidgetWindowViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId,
        bool showPlayerIdentityInHeader = true)
    {
        PlayerWidget = playerWidget;
        _requestedCharacterId = requestedCharacterId;
        _showPlayerIdentityInHeader = showPlayerIdentityInHeader;
        RefreshHeaderText();
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
        if (!string.IsNullOrWhiteSpace(_lastKnownPlayerName)
            || string.IsNullOrWhiteSpace(playerName)
            || playerUid == 0)
        {
            return;
        }

        _lastKnownPlayerName = playerName;
        _lastKnownPlayerUid = playerUid;
        RefreshHeaderText();
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
    protected void SetHeaderText(string playerName, long playerUid)
    {
        RememberPlayer(playerName, playerUid);
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

            RememberPlayer(player.Name, player.CharacterId);
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
            RememberPlayer(_selectedPlayer.Name, _selectedPlayer.CharacterId);
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
        if (!_showPlayerIdentityInHeader
            || string.IsNullOrWhiteSpace(_lastKnownPlayerName))
        {
            HeaderText = HeaderPrefix;
            return;
        }

        HeaderText = $"{HeaderPrefix} - {_lastKnownPlayerName}(UID:{_lastKnownPlayerUid})";
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

        RememberPlayer(identity.Name, identity.UserId);
    }

    private void RememberPlayer(string? playerName, long playerUid)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return;
        }

        var wasUnknown = string.IsNullOrWhiteSpace(_lastKnownPlayerName);

        _lastKnownPlayerName = playerName;
        _lastKnownPlayerUid = playerUid;

        if (wasUnknown)
        {
            SavedTargetInfoResolved?.Invoke(this, EventArgs.Empty);
        }
    }
}
