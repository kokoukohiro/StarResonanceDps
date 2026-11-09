using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.Diagnostics;

namespace StarResonanceDps.App.ViewModels;

/// <summary>フィードバックタブの検出された項目の1行。チェックは保存しない(既定は報告済みでない行だけ入れる)。報告済みの行はチェックできない。</summary>
public sealed partial class FeedbackItemViewModel : ObservableObject
{
    private readonly Action _selectionChanged;

    [ObservableProperty]
    private string _kindText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSelect))]
    private bool _isReported;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _showsDivider;

    public FeedbackItemViewModel(ResidentDetection detection, Action selectionChanged)
    {
        _selectionChanged = selectionChanged;
        Detection = detection;
        IsReported = detection.IsReported;
        IsSelected = !detection.IsReported;
        TimeText = detection.DetectedAt.ToLocalTime().ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
        RefreshText();
    }

    public ResidentDetection Detection { get; private set; }

    public string Id => Detection.Id;

    public string TimeText { get; }

    public bool CanSelect => !IsReported;

    public void Apply(ResidentDetection detection)
    {
        Detection = detection;
        if (detection.IsReported && !IsReported)
        {
            IsSelected = false;
        }

        IsReported = detection.IsReported;
    }

    public void RefreshText()
    {
        KindText = LocalizationManager.Instance.GetString(Detection.Kind switch
        {
            ResidentDetectionKind.BlankSourceName => "Feedback_Detection_BlankSourceName",
            ResidentDetectionKind.SourceLandingConflict => "Feedback_Detection_SourceLandingConflict",
            ResidentDetectionKind.SpecTalentConflict or ResidentDetectionKind.SpecSkillConflict => "Feedback_Detection_SpecConflict",
            _ => throw new ArgumentOutOfRangeException(nameof(Detection), Detection.Kind, null)
        });
    }

    partial void OnIsSelectedChanged(bool value)
    {
        _selectionChanged();
    }
}
