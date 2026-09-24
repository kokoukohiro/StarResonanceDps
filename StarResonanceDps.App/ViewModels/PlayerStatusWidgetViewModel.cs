using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PlayerStatusWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    private PlayerStatusWidgetSettingsConfig _settings;
    private PlayerRosterEntry? _player;
    private bool _isDisposed;

    [ObservableProperty]
    private IReadOnlyList<PlayerStatusRow> _playerStatus = [];

    public PlayerStatusWidgetViewModel(
        WidgetListItemViewModel statusWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(statusWidget, requestedCharacterId, showPlayerIdentityInHeader: false)
    {
        _settings = statusWidget.GetPlayerStatusSettingsSnapshot();
        statusWidget.PlayerStatusSettingsChanged += Widget_PlayerStatusSettingsChanged;
        InitializePlayer(initialPlayer);
    }

    public WidgetListItemViewModel StatusWidget => PlayerWidget;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        StatusWidget.PlayerStatusSettingsChanged -= Widget_PlayerStatusSettingsChanged;
    }

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        _player = player;
        RefreshRows();
    }

    /// <summary>表示設定(保存かプレビュー)が変わった。行を作り直す。</summary>
    private void Widget_PlayerStatusSettingsChanged(object? sender, EventArgs e)
    {
        _settings = StatusWidget.GetPlayerStatusSettingsSnapshot();
        RefreshRows();
    }

    private void RefreshRows()
    {
        PlayerStatus = _player is null
            ? []
            : PlayerStatusEntry.Create(
                _player,
                _settings.HideInactiveStatusEffects
                    ?? WidgetConfigDefaults.DefaultHideInactiveStatusEffects,
                _settings.RowVisibility);
    }
}
