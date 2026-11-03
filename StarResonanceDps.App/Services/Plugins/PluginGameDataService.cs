using System.Threading;
using System.Windows;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 本体が通信から控えた値をプラグインへ渡す。値は Core の控えを SDK の型へ写したもので、画面のスレッドで置き換えて知らせる。
/// </summary>
internal sealed class PluginGameDataService : IPluginGameDataService
{
    private int _isSelfModulesRefreshQueued;

    private PluginGameDataService()
    {
        SelfModules = ToPluginInventory(SelfModuleStore.GetSnapshot());
        SelfModuleStore.Changed += SelfModuleStore_Changed;
    }

    public static PluginGameDataService Instance { get; } = new();

    public PluginGameServer CurrentServer => ToPluginServer(GameServerStore.Current);

    public PluginModuleInventory? SelfModules { get; private set; }

    public event EventHandler? SelfModulesChanged;

    /// <summary>受信のスレッドから来る。画面のスレッドへの予約は1回にまとめ、予約が走った時点の控えを写す。</summary>
    private void SelfModuleStore_Changed()
    {
        if (Interlocked.Exchange(ref _isSelfModulesRefreshQueued, 1) == 1)
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            // 終了の途中で画面が無い。知らせる相手も無い。
            Interlocked.Exchange(ref _isSelfModulesRefreshQueued, 0);
            return;
        }

        dispatcher.BeginInvoke(RefreshSelfModules);
    }

    private void RefreshSelfModules()
    {
        Interlocked.Exchange(ref _isSelfModulesRefreshQueued, 0);
        SelfModules = ToPluginInventory(SelfModuleStore.GetSnapshot());
        SelfModulesChanged?.Invoke(this, EventArgs.Empty);
    }

    private static PluginModuleInventory? ToPluginInventory(SelfModuleInventory? inventory)
    {
        if (inventory is null)
        {
            return null;
        }

        var modules = inventory.Modules
            .Select(module => new PluginModuleItem(
                module.Uuid,
                module.ConfigId,
                module.Quality,
                module.Effects.Select(effect => new PluginModuleEffect(effect.EffectId, effect.LinkCount)).ToArray()))
            .ToArray();
        return new PluginModuleInventory(modules, inventory.EquippedUuidBySlot);
    }

    private static PluginGameServer ToPluginServer(GameServer server)
    {
        return server switch
        {
            GameServer.Unknown => PluginGameServer.Unknown,
            GameServer.Asia => PluginGameServer.Asia,
            GameServer.China => PluginGameServer.China,
            GameServer.Global => PluginGameServer.Global,
            GameServer.SoutheastAsia => PluginGameServer.SoutheastAsia,
            _ => throw new ArgumentOutOfRangeException(nameof(server), server, "The game server has no plugin equivalent."),
        };
    }
}
