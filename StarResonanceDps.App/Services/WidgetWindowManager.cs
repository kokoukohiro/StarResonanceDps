using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.Views.Widgets;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Services;

public sealed class WidgetWindowManager
{
    private readonly Dictionary<WidgetKind, WidgetWindow> _openWindows = new();
    private Window? _managerWindow;
    private bool _isManagerClosing;

    private WidgetWindowManager()
    {
    }

    public static WidgetWindowManager Instance { get; } = new();

    public void ApplyWidgetState(WidgetListItemViewModel widget)
    {
        if (widget.State == WidgetState.Running)
        {
            Open(widget);
            return;
        }

        Close(widget.Kind);
    }

    private void Open(WidgetListItemViewModel widget)
    {
        if (_openWindows.TryGetValue(widget.Kind, out var existingWindow))
        {
            if (existingWindow.WindowState == WindowState.Minimized)
            {
                existingWindow.WindowState = WindowState.Normal;
            }

            existingWindow.Activate();
            return;
        }

        var owner = Application.Current?.MainWindow;
        TrackManagerWindow(owner);

        var savedBounds = WidgetStateManager.Instance.GetWidgetSnapshot(widget.Kind).Window;
        var window = new WidgetWindow(widget, savedBounds, owner);
        window.Closed += WidgetWindow_Closed;

        _openWindows.Add(widget.Kind, window);
        window.Show();
    }

    private void Close(WidgetKind kind)
    {
        if (!_openWindows.TryGetValue(kind, out var window))
        {
            return;
        }

        window.Close();
    }

    private void TrackManagerWindow(Window? managerWindow)
    {
        if (managerWindow is null || ReferenceEquals(_managerWindow, managerWindow))
        {
            return;
        }

        if (_managerWindow is not null)
        {
            _managerWindow.Closing -= ManagerWindow_Closing;
        }

        _managerWindow = managerWindow;
        _managerWindow.Closing += ManagerWindow_Closing;
    }

    private void ManagerWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (!e.Cancel)
        {
            _isManagerClosing = true;
        }
    }

    private void WidgetWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is not WidgetWindow window)
        {
            return;
        }

        window.Closed -= WidgetWindow_Closed;
        _openWindows.Remove(window.Widget.Kind);

        // Closing the manager also closes its owned widget windows. Keep their saved
        // running state so the same windows are restored on the next app launch.
        if (!_isManagerClosing && window.Widget.State == WidgetState.Running)
        {
            window.Widget.State = WidgetState.Stopped;
        }
    }
}
