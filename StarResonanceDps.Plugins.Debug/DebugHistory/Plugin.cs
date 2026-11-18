// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.PluginSdk;

[assembly: PluginRegistration(typeof(DebugHistory.Plugin), PluginSdkVersion.Current)]
[assembly: PluginDisplayName("ja-JP", "デバッグ: 履歴と戦闘への保存")]
[assembly: PluginGenre("ja-JP", "デバッグ")]
[assembly: PluginAuthor("kokoukohiro")]
[assembly: PluginVersion("1.0.0")]

namespace DebugHistory;

public sealed class Plugin : IStarResonancePlugin, IPluginWindowProvider
{
    private IPluginContext? _context;

    public void Initialize(IPluginContext context)
    {
        _context = context;
    }

    public FrameworkElement CreateContent()
    {
        var panel = new StackPanel { Margin = new Thickness(24, 8, 24, 24) };
        panel.Children.Add(CreateButton("履歴を保ったまま例外(止まる)", ThrowWhileKeepingHistory));
        panel.Children.Add(CreateButton("上限を越える長さ(ArgumentException で止まる)", StoreTooLongData));
        panel.Children.Add(CreateButton("33個目のキー(InvalidOperationException で止まる)", StoreTooManyKeys));
        panel.Children.Add(CreateButton("決まりに合わないキー(ArgumentException で止まる)", StoreInvalidKey));
        panel.Children.Add(CreateButton("保存済みの戦闘へ上限を越える(止まるかを確かめる)", StoreTooManyKeysToSavedEncounter));
        return panel;
    }

    /// <summary>止めた後とアプリを閉じるときは、戦闘への保存が false、履歴の一覧が Unavailable になる。結果はログに書く。</summary>
    public void Shutdown()
    {
        var context = _context;
        _context = null;
        if (context is null)
        {
            return;
        }

        var live = context.GameData.GetLiveEncounter();
        var stored = live is not null && context.History.TrySetEncounterData(live.EncounterKey, "debug.shutdown", new byte[] { 1 });
        context.Logger.Info($"TrySetEncounterData in Shutdown: encounter key {live?.EncounterKey.ToString() ?? "(none)"}, stored {stored}");
        _ = LogEntriesStatusAsync(context);
    }

    private static Button CreateButton(string text, Action action)
    {
        var button = new Button { Content = text, Width = 380, Margin = new Thickness(0, 0, 0, 8), HorizontalAlignment = HorizontalAlignment.Left };
        button.SetResourceReference(FrameworkElement.StyleProperty, PluginResourceKeys.Buttons.Text);
        button.Click += (_, _) => action();
        return button;
    }

    private static async Task LogEntriesStatusAsync(IPluginContext context)
    {
        var result = await context.History.GetEntriesAsync().ConfigureAwait(false);
        context.Logger.Info($"GetEntriesAsync in Shutdown: {result.Status}");
    }

    /// <summary>
    /// 集計で履歴を開いてから押す。止まった後、全体設定「履歴表示を自動解除」がオンなら、今の戦闘のできごとで今の戦闘に戻る
    /// (止めたときに本体が保持を外している)。
    /// </summary>
    private void ThrowWhileKeepingHistory()
    {
        if (_context is null)
        {
            return;
        }

        // 外さずに捨てる。外すのは止めたときの本体だけ。
        _ = _context.History.KeepHistoryOpen();
        throw new InvalidOperationException("Debug exception while keeping the history encounter open.");
    }

    /// <summary>長さは回を探す前に確かめるので、今の戦闘が無くても例外になる。</summary>
    private void StoreTooLongData()
    {
        if (_context is null)
        {
            return;
        }

        var encounterKey = _context.GameData.GetLiveEncounter()?.EncounterKey ?? 0;
        _context.History.TrySetEncounterData(encounterKey, "debug.too-long", new byte[PluginEncounterDataLimits.MaxTotalBytes + 1]);
    }

    /// <summary>1バイトずつキーを足す。上限の数までは入り、次のキーで例外になる。</summary>
    private void StoreTooManyKeys()
    {
        if (_context is null)
        {
            return;
        }

        var live = _context.GameData.GetLiveEncounter();
        if (live is null)
        {
            _context.Logger.Warning("There is no live encounter to store data with.");
            return;
        }

        for (var i = 0; i <= PluginEncounterDataLimits.MaxKeys; i++)
        {
            if (!_context.History.TrySetEncounterData(live.EncounterKey, $"debug.key-{i}", new byte[] { 1 }))
            {
                _context.Logger.Warning($"The live encounter did not take key {i}: it was saved meanwhile.");
                return;
            }
        }
    }

    private void StoreInvalidKey()
    {
        if (_context is null)
        {
            return;
        }

        var encounterKey = _context.GameData.GetLiveEncounter()?.EncounterKey ?? 0;
        _context.History.TrySetEncounterData(encounterKey, "履歴", new byte[] { 1 });
    }

    /// <summary>
    /// 例外は本体の中で投げられ、async void から画面のスレッドへ出る。本体がこのプラグインの例外と見分けて止めるかを確かめる
    /// (見分けられなければアプリの未処理の例外になる)。上限までのキーは履歴の先頭の戦闘に残る(その戦闘が履歴から消えるまで)。
    /// </summary>
    private async void StoreTooManyKeysToSavedEncounter()
    {
        var context = _context;
        if (context is null)
        {
            return;
        }

        var list = await context.History.GetEntriesAsync();
        if (list.Status != PluginHistoryStatus.Succeeded || list.Entries.Count == 0)
        {
            context.Logger.Warning($"There is no saved encounter to store data with: {list.Status}, {list.Entries.Count} entries.");
            return;
        }

        var historyId = list.Entries[0].HistoryId;
        for (var i = 0; i <= PluginEncounterDataLimits.MaxKeys; i++)
        {
            var status = await context.History.SetSavedEncounterDataAsync(historyId, $"debug.key-{i}", new byte[] { 1 });
            if (status != PluginHistoryStatus.Succeeded)
            {
                context.Logger.Warning($"History ID {historyId} did not take key {i}: {status}");
                return;
            }
        }
    }
}
