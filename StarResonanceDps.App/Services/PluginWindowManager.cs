using System;
using System.Collections.Generic;
using System.Windows;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Views.Plugins;

namespace StarResonanceDps.App.Services;

public sealed class PluginWindowManager
{
    private readonly Dictionary<string, PluginWindow> _openWindows = new(StringComparer.OrdinalIgnoreCase);

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

        var owner = Application.Current?.MainWindow;
        var window = new PluginWindow(plugin, content, owner);
        window.Closed += PluginWindow_Closed;

        _openWindows.Add(plugin.PluginId, window);
        window.Show();
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
