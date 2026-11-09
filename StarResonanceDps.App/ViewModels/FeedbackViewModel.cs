using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.Views;
using StarResonanceDps.Core.Diagnostics;

namespace StarResonanceDps.App.ViewModels;

/// <summary>フィードバックタブ。投稿画面を開くことと、検出された項目の一覧と報告。</summary>
public sealed partial class FeedbackViewModel : ObservableObject
{
    private readonly Dictionary<(ResidentDetectionKind Kind, string Id), FeedbackItemViewModel> _itemsByDetection = [];
    private readonly List<FeedbackItemViewModel> _orderedItems = [];

    /// <summary>確認の途中だけある。</summary>
    private CancellationTokenSource? _checkCancellation;

    [ObservableProperty]
    private bool _hasItems;

    /// <summary>報告のボタンの文字。確認の途中は「キャンセル」。</summary>
    [ObservableProperty]
    private string _reportButtonText = string.Empty;

    public FeedbackViewModel()
    {
        UpdateReportButton();
    }

    public ObservableCollection<FeedbackItemViewModel> Items { get; } = [];

    /// <summary>控えから行を読み直す。行は作り直さず値だけ入れ替える。並びは未報告 → 報告済みで、それぞれ検知した順。</summary>
    public void Refresh()
    {
        var detections = ResidentDetectionStore.GetSnapshot();
        CollectRows(detections, isReported: false);
        CollectRows(detections, isReported: true);
        CollectionOrder.Apply(Items, _orderedItems);
        _orderedItems.Clear();

        for (var index = 0; index < Items.Count; index++)
        {
            Items[index].ShowsDivider = index < Items.Count - 1;
        }

        HasItems = Items.Count > 0;
        ReportSelectedCommand.NotifyCanExecuteChanged();
    }

    public void RefreshTexts()
    {
        foreach (var item in Items)
        {
            item.RefreshText();
        }

        UpdateReportButton();
    }

    private void CollectRows(IReadOnlyList<ResidentDetection> detections, bool isReported)
    {
        foreach (var detection in detections)
        {
            if (detection.IsReported != isReported)
            {
                continue;
            }

            var key = (detection.Kind, detection.Id);
            if (_itemsByDetection.TryGetValue(key, out var item))
            {
                item.Apply(detection);
            }
            else
            {
                item = new FeedbackItemViewModel(detection, ReportSelectedCommand.NotifyCanExecuteChanged);
                _itemsByDetection.Add(key, item);
            }

            _orderedItems.Add(item);
        }
    }

    [RelayCommand]
    private void OpenIssue()
    {
        ExternalLinkOpener.Open(FeedbackIssueLinks.CreateNewIssueUrl());
    }

    private bool CanReportSelected()
    {
        return _checkCancellation is not null || Items.Any(item => item.IsSelected);
    }

    /// <summary>
    /// 同じ報告がIssueにある項目を「報告済み」にして、無い項目だけで投稿画面を開く。確認の途中はキャンセルになるので、途中でも押せる。
    /// 確認できなければ投稿画面を開かない。投稿画面に入れた行は、投稿したか分からないので印を付けず、続きの報告に入らないようチェックだけ外す。
    /// 入りきらない行はチェックを付けたまま残し、次に押したときに確かめる。
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanReportSelected), AllowConcurrentExecutions = true)]
    private async Task ReportSelectedAsync()
    {
        if (_checkCancellation is { } running)
        {
            running.Cancel();
            return;
        }

        var selected = Items.Where(item => item.IsSelected).Select(item => item.Detection).ToList();
        if (await FindReportedAsync(selected) is not { } lookup)
        {
            return;
        }

        if (lookup.Reported.Count > 0)
        {
            ResidentDetectionStore.MarkReported(lookup.Reported);
            Refresh();
        }

        var owner = Application.Current?.MainWindow;
        if (lookup.Failure is { } failure)
        {
            DetectionReportMessage.ShowCheckFailed(owner, failure);
            return;
        }

        if (lookup.NotReported.Count == 0)
        {
            DetectionReportMessage.ShowAlreadyReported(owner);
            return;
        }

        var link = FeedbackIssueLinks.CreateDetectionReportLink(lookup.NotReported);
        if (!ExternalLinkOpener.Open(link.Url))
        {
            return;
        }

        ClearSelection(lookup.NotReported.Take(link.ReportedCount));

        var remainingCount = selected.Count - lookup.Reported.Count - link.ReportedCount;
        if (remainingCount > 0)
        {
            DetectionReportMessage.ShowLimit(owner, remainingCount);
        }
    }

    /// <summary>キャンセルされたら null。</summary>
    private async Task<FeedbackIssueLookupResult?> FindReportedAsync(IReadOnlyList<ResidentDetection> detections)
    {
        using var cancellation = new CancellationTokenSource();
        _checkCancellation = cancellation;
        UpdateReportButton();
        try
        {
            return await FeedbackIssueLookup.FindReportedAsync(detections, cancellation.Token);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            Log.Information("The report check was cancelled");
            return null;
        }
        finally
        {
            _checkCancellation = null;
            UpdateReportButton();
        }
    }

    private void ClearSelection(IEnumerable<ResidentDetection> detections)
    {
        foreach (var detection in detections)
        {
            if (_itemsByDetection.TryGetValue((detection.Kind, detection.Id), out var item))
            {
                item.IsSelected = false;
            }
        }
    }

    private void UpdateReportButton()
    {
        ReportButtonText = LocalizationManager.Instance.GetString(
            _checkCancellation is null ? "Feedback_Detections_Report" : "Common_Cancel");
        ReportSelectedCommand.NotifyCanExecuteChanged();
    }
}
