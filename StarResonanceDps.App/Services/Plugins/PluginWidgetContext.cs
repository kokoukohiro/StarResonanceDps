// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 開いているプラグインのウィジェットの窓1つに渡す口。設定はカード(<see cref="WidgetListItemViewModel"/>)が持つ今の値(設定の窓のプレビューを含む)から作る。
/// 値はどのスレッドからでも読める写しにし、カードの知らせのたびに作り直して <see cref="SettingsChanged"/> を受け止めの中で出す。
/// 状態の印の理由はどのスレッドからでも受け、窓の値(<see cref="StatusReasonsChanged"/>)へは画面のスレッドで知らせる。
/// 窓が隠れた・出たことは窓の値から受けて、受け止めの中で知らせる。窓を閉じたら外す。
/// </summary>
internal sealed class PluginWidgetContext : IPluginWidgetContext
{
    private const PluginWidgetStatusReasons DefinedReasons =
        PluginWidgetStatusReasons.MapOrRelogin
        | PluginWidgetStatusReasons.PartyScreen
        | PluginWidgetStatusReasons.PlayerClick
        | PluginWidgetStatusReasons.PlayerApproach
        | PluginWidgetStatusReasons.EnemyApproach
        | PluginWidgetStatusReasons.Unreadable;

    private readonly PluginWidgetRegistration _registration;
    private readonly WidgetListItemViewModel _widget;
    private readonly PluginCallGuard _guard;
    private PluginSettingValues _settings;
    private int _statusReasons;
    private volatile bool _isWindowHidden;
    private volatile bool _isDetached;

    public PluginWidgetContext(
        string widgetId,
        PluginWidgetRegistration registration,
        WidgetListItemViewModel widget,
        PluginCallGuard guard,
        bool isWindowHidden)
    {
        WidgetId = widgetId;
        _registration = registration;
        _widget = widget;
        _guard = guard;
        _isWindowHidden = isWindowHidden;
        _settings = CreateSettings();
        _widget.PluginSettingsChanged += Widget_PluginSettingsChanged;
    }

    public string WidgetId { get; }

    public PluginSettingValues Settings => Volatile.Read(ref _settings);

    public event EventHandler? SettingsChanged;

    public PluginWidgetStatusReasons StatusReasons => (PluginWidgetStatusReasons)Volatile.Read(ref _statusReasons);

    public bool IsWindowHidden => _isWindowHidden;

    public event EventHandler? WindowHiddenChanged;

    /// <summary>プラグインが渡した理由が変わった(画面のスレッドで)。窓の値が印を計算し直す。</summary>
    public event EventHandler? StatusReasonsChanged;

    public void SetStatusReasons(PluginWidgetStatusReasons reasons)
    {
        if ((reasons & ~DefinedReasons) != PluginWidgetStatusReasons.None)
        {
            throw new ArgumentException($"{(int)reasons} has a value that PluginWidgetStatusReasons does not define.", nameof(reasons));
        }

        if (Interlocked.Exchange(ref _statusReasons, (int)reasons) == (int)reasons
            || _isDetached
            || Application.Current?.Dispatcher is not { HasShutdownStarted: false } dispatcher)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            StatusReasonsChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            if (!_isDetached)
            {
                StatusReasonsChanged?.Invoke(this, EventArgs.Empty);
            }
        });
    }

    /// <summary>窓が隠れた・出た(画面のスレッドで)。変わったときだけ受け止めの中で知らせる。</summary>
    public void SetWindowHidden(bool isHidden)
    {
        if (_isWindowHidden == isHidden || _isDetached)
        {
            return;
        }

        _isWindowHidden = isHidden;
        _guard.TryInvoke("Widget hidden notification", () => WindowHiddenChanged?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>窓を閉じた(作るのに失敗した)。カードの知らせから外し、知らせも出さない。</summary>
    public void Detach()
    {
        if (_isDetached)
        {
            return;
        }

        _isDetached = true;
        _widget.PluginSettingsChanged -= Widget_PluginSettingsChanged;
    }

    private PluginSettingValues CreateSettings()
    {
        return new PluginSettingValues(PluginSettingValueCodec.ReadAll(
            _registration.Settings,
            _widget.GetPluginSettingsSnapshot().Values));
    }

    private void Widget_PluginSettingsChanged(object? sender, EventArgs e)
    {
        Volatile.Write(ref _settings, CreateSettings());
        _guard.TryInvoke("Widget settings notification", () => SettingsChanged?.Invoke(this, EventArgs.Empty));
    }
}
