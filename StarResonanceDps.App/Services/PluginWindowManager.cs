using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Views.Plugins;

namespace StarResonanceDps.App.Services;

public sealed class PluginWindowManager
{
    private readonly Dictionary<string, PluginWindow> _openWindows = new(StringComparer.OrdinalIgnoreCase);
    private Window? _managerWindow;

    private PluginWindowManager()
    {
    }

    public static PluginWindowManager Instance { get; } = new();

    public bool ActivateExisting(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId)
            || !_openWindows.TryGetValue(pluginId, out var existingWindow))
        {
            return false;
        }

        if (existingWindow.WindowState == WindowState.Minimized)
        {
            existingWindow.WindowState = WindowState.Normal;
        }

        existingWindow.Activate();
        return true;
    }

    public void Open(PluginListItemViewModel plugin, FrameworkElement content)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        ArgumentNullException.ThrowIfNull(content);

        if (plugin.IsAddItem || string.IsNullOrWhiteSpace(plugin.PluginId))
        {
            return;
        }

        if (ActivateExisting(plugin.PluginId))
        {
            return;
        }

        var managerWindow = Application.Current?.MainWindow;
        TrackManagerWindow(managerWindow);

        var window = new PluginWindow(plugin, content, managerWindow);
        window.Closed += PluginWindow_Closed;

        _openWindows.Add(plugin.PluginId, window);
        window.Show();
    }

    private void TrackManagerWindow(Window? managerWindow)
    {
        if (managerWindow is null || ReferenceEquals(_managerWindow, managerWindow))
        {
            return;
        }

        if (_managerWindow is not null)
        {
            _managerWindow.Closed -= ManagerWindow_Closed;
        }

        _managerWindow = managerWindow;
        _managerWindow.Closed += ManagerWindow_Closed;
    }

    private void ManagerWindow_Closed(object? sender, EventArgs eventArgs)
    {
        foreach (var window in _openWindows.Values.ToArray())
        {
            window.Close();
        }

        _managerWindow = null;
    }

    private void PluginWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is not PluginWindow window)
        {
            return;
        }

        window.Closed -= PluginWindow_Closed;

        if (!string.IsNullOrWhiteSpace(window.Plugin.PluginId))
        {
            _openWindows.Remove(window.Plugin.PluginId);
        }
    }
}
