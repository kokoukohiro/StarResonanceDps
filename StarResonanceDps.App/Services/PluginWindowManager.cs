using System;
using System.Collections.Generic;
using System.Windows;
using StarResonanceDps.App.Models;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Views.Plugins;

namespace StarResonanceDps.App.Services;

public sealed class PluginWindowManager
{
    private readonly Dictionary<PluginKind, PluginWindow> _openWindows = new();

    private PluginWindowManager()
    {
    }

    public static PluginWindowManager Instance { get; } = new();

    public void Open(PluginListItemViewModel plugin)
    {
        if (plugin.IsAddItem || plugin.Kind is not { } kind)
        {
            return;
        }

        if (_openWindows.TryGetValue(kind, out var existingWindow))
        {
            if (existingWindow.WindowState == WindowState.Minimized)
            {
                existingWindow.WindowState = WindowState.Normal;
            }

            existingWindow.Activate();
            return;
        }

        var owner = Application.Current?.MainWindow;
        var window = new PluginWindow(plugin, owner);
        window.Closed += PluginWindow_Closed;

        _openWindows.Add(kind, window);
        window.Show();
    }

    private void PluginWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is not PluginWindow window)
        {
            return;
        }

        window.Closed -= PluginWindow_Closed;

        if (window.Plugin.Kind is { } kind)
        {
            _openWindows.Remove(kind);
        }
    }
}
