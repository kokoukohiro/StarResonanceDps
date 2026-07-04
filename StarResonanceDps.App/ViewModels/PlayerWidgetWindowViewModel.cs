using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public abstract class PlayerWidgetWindowViewModel : ViewModelBase
{
    private readonly long? _requestedCharacterId;
    private long? _representedCharacterId;
    private PlayerRosterEntry? _selectedPlayer;
    private string _headerText = string.Empty;

    protected PlayerWidgetWindowViewModel(
        WidgetListItemViewModel playerWidget,
        long? requestedCharacterId)
    {
        PlayerWidget = playerWidget;
        _requestedCharacterId = requestedCharacterId;
        RefreshHeaderText();
    }

    public WidgetListItemViewModel PlayerWidget { get; }

    public string HeaderText
    {
        get => _headerText;
        private set => SetProperty(ref _headerText, value);
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
        if (_requestedCharacterId is null && player is not null)
        {
            _representedCharacterId = player.CharacterId;
        }

        if (EqualityComparer<PlayerRosterEntry?>.Default.Equals(_selectedPlayer, player))
        {
            return;
        }

        _selectedPlayer = player;
        RefreshHeaderText();
        OnSelectedPlayerChanged(player);
    }

    private void RefreshHeaderText()
    {
        HeaderText = _selectedPlayer is null
            ? PlayerWidget.DisplayName
            : $"{PlayerWidget.DisplayName} - {_selectedPlayer.Name}(UID:{_selectedPlayer.CharacterId})";
    }
}
