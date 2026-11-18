// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.ViewModels.Plugins;

/// <summary>
/// プラグインのウィジェットの窓1つの中身。プラグインが作った本体・フッター・ヘッダーのボタンを持ち、窓を閉じたら後始末する
/// (窓が中身の <c>DataContext</c> の <see cref="IDisposable"/> を呼ぶ)。
/// 空の窓は中身なしで作り、相手が決まったら <see cref="SetContent"/> で入れる。
/// 状態の印は、定義の <see cref="PluginWidgetStatusRule"/> で本体が数える受信の事実と、プラグインが渡した中身の欠けを足す
/// (<see cref="WidgetDataStatusRules.ForPluginWidget"/>)。空の窓はプラグインを呼ばないので、本体の分だけ。
/// 窓が隠れた・出たことは文脈へ渡す(空の窓は今の状態を持ち、中身を入れたときに渡す)。
/// </summary>
public sealed class PluginWidgetWindowViewModel : IWidgetDataStatusSource, IWidgetWindowHiddenAware, INotifyPropertyChanged, IDisposable
{
    private readonly PluginCallGuard _guard;
    private readonly PluginWidgetStatusRule _statusRule;
    private readonly bool _usesWidgetColors;
    private readonly WidgetListItemViewModel _widget;
    private PluginWidgetContext? _context;
    private WidgetDataStatus _dataStatus = WidgetDataStatus.Complete;
    private bool _isWindowHidden;
    private bool _isDisposed;

    internal PluginWidgetWindowViewModel(PluginWidgetDefinition definition, PluginCallGuard guard, WidgetListItemViewModel widget)
    {
        ScrollsVertically = definition.ScrollsVertically;
        _statusRule = definition.StatusRule;
        _usesWidgetColors = definition.UsesWidgetColors;
        _guard = guard;
        _widget = widget;
        if (_usesWidgetColors)
        {
            _widget.PropertyChanged += Widget_PropertyChanged;
        }

        RefreshDataStatus();
    }

    /// <summary>窓が隠れているか。中身を作るときの文脈の最初の値にする。</summary>
    internal bool IsWindowHidden => _isWindowHidden;

    internal void SetContent(PluginWidgetContent content, PluginWidgetContext context)
    {
        Body = content.Body;
        Footer = content.Footer;
        HeaderButtons = [.. content.HeaderButtons.Select(button => new PluginWidgetHeaderButtonViewModel(button, _guard))];
        _context = context;
        context.StatusReasonsChanged += Context_StatusReasonsChanged;
        context.SetWindowHidden(_isWindowHidden);
        RefreshDataStatus();
    }

    public event EventHandler? DataStatusChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>本体。空の窓は相手が決まるまで null。</summary>
    public FrameworkElement? Body { get; private set; }

    public FrameworkElement? Footer { get; private set; }

    public bool ScrollsVertically { get; }

    public IReadOnlyList<PluginWidgetHeaderButtonViewModel> HeaderButtons { get; private set; } = [];

    /// <summary>
    /// 本体とフッターの包みが <c>Brush.*</c> に書くウィジェットの色。定義の <see cref="PluginWidgetDefinition.UsesWidgetColors"/> で、
    /// 背景画像でないときだけ(カードの選択リストと同じ。画像のときはメイン画面の色)。
    /// </summary>
    public ThemeColorPalette? WidgetPalette => _usesWidgetColors && !_widget.HasBackgroundImage ? _widget.ThemePalette : null;

    public WidgetDataStatus DataStatus => _dataStatus;

    public void RefreshDataStatus()
    {
        var pluginReasons = (WidgetDataStatusReasons)(int)(_context?.StatusReasons ?? PluginWidgetStatusReasons.None);
        var status = WidgetDataStatusRules.ForPluginWidget(
            GameReceptionStateStore.Current,
            _statusRule,
            EncounterHistoryProvider.SelectedEncounterId is not null,
            EncounterHistoryProvider.HasLoadFailed,
            pluginReasons);
        if (status == _dataStatus)
        {
            return;
        }

        _dataStatus = status;
        DataStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetWindowHidden(bool isHidden)
    {
        _isWindowHidden = isHidden;
        _context?.SetWindowHidden(isHidden);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        if (_usesWidgetColors)
        {
            _widget.PropertyChanged -= Widget_PropertyChanged;
        }

        if (_context is not null)
        {
            _context.StatusReasonsChanged -= Context_StatusReasonsChanged;
            _context.Detach();
        }

        if (Body is not null)
        {
            DisposeElement(Body);
        }

        if (Footer is not null)
        {
            DisposeElement(Footer);
        }
    }

    private void Context_StatusReasonsChanged(object? sender, EventArgs e)
    {
        RefreshDataStatus();
    }

    private void Widget_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WidgetListItemViewModel.ThemePalette) or nameof(WidgetListItemViewModel.HasBackgroundImage))
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WidgetPalette)));
        }
    }

    private void DisposeElement(FrameworkElement element)
    {
        if (element is IDisposable disposable)
        {
            _guard.TryInvoke("Widget content disposal", disposable.Dispose);
        }
    }
}

/// <summary>ヘッダーのボタン1つ。文字はプラグインのボタンの値をそのまま出し、押したら受け止めの中で知らせる。</summary>
public sealed class PluginWidgetHeaderButtonViewModel
{
    internal PluginWidgetHeaderButtonViewModel(PluginWidgetHeaderButton button, PluginCallGuard guard)
    {
        Button = button;
        ClickCommand = new RelayCommand(() => guard.TryInvoke("Widget header button", button.RaiseClick));
    }

    public PluginWidgetHeaderButton Button { get; }

    public ICommand ClickCommand { get; }
}
