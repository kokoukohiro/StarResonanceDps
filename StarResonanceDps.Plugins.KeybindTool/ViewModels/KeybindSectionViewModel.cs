using System.ComponentModel;

namespace StarResonanceDps.Plugins.KeybindTool.ViewModels;

/// <summary>一覧の節の種類。先頭の固定の節(ファイル・プリセット・入力方法・補助キー)と、4つのキー設定の節。</summary>
internal enum KeybindSectionKind
{
    Settings,
    Main,
    QuickWheel,
    Photo,
    Fishing,
}

/// <summary>
/// 一覧の節1つ。一覧はこの節でまとまり(グループ)に分け、見出し・カード・独立のチェックボックスをまとまりの枠で描く。
/// 独立のチェックボックスは、今の入力方法の値を読み書きする。
/// </summary>
internal sealed class KeybindSectionViewModel : ObservableObject
{
    private readonly KeybindToolViewModel _owner;
    private readonly string? _titleKey;
    private readonly string? _independentKey;

    public KeybindSectionViewModel(
        KeybindToolViewModel owner,
        KeybindSectionKind kind,
        string? titleKey,
        string? independentKey)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Kind = kind;
        _titleKey = titleKey;
        _independentKey = independentKey;
        _owner.PropertyChanged += Owner_PropertyChanged;
        _owner.Texts.PropertyChanged += Texts_PropertyChanged;
    }

    public KeybindSectionKind Kind { get; }

    public string Title => _titleKey is null ? string.Empty : _owner.Texts[_titleKey];

    public string IndependentText => _independentKey is null ? string.Empty : _owner.Texts[_independentKey];

    public bool HasIndependentCheckBox => _independentKey is not null;

    public bool IsIndependent
    {
        get => Kind switch
        {
            KeybindSectionKind.QuickWheel => _owner.IsControllerMode
                ? _owner.ControllerQuickWheelIndependent
                : _owner.KeyMouseQuickWheelIndependent,
            KeybindSectionKind.Photo => _owner.IsControllerMode
                ? _owner.ControllerPhotoModeIndependent
                : _owner.KeyMousePhotoModeIndependent,
            KeybindSectionKind.Fishing => _owner.IsControllerMode
                ? _owner.ControllerFishingModeIndependent
                : _owner.KeyMouseFishingModeIndependent,
            _ => false,
        };
        set
        {
            switch (Kind)
            {
                case KeybindSectionKind.QuickWheel when _owner.IsControllerMode:
                    _owner.ControllerQuickWheelIndependent = value;
                    break;
                case KeybindSectionKind.QuickWheel:
                    _owner.KeyMouseQuickWheelIndependent = value;
                    break;
                case KeybindSectionKind.Photo when _owner.IsControllerMode:
                    _owner.ControllerPhotoModeIndependent = value;
                    break;
                case KeybindSectionKind.Photo:
                    _owner.KeyMousePhotoModeIndependent = value;
                    break;
                case KeybindSectionKind.Fishing when _owner.IsControllerMode:
                    _owner.ControllerFishingModeIndependent = value;
                    break;
                case KeybindSectionKind.Fishing:
                    _owner.KeyMouseFishingModeIndependent = value;
                    break;
            }
        }
    }

    private void Owner_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(KeybindToolViewModel.IsControllerMode)
            or nameof(KeybindToolViewModel.ControllerQuickWheelIndependent)
            or nameof(KeybindToolViewModel.KeyMouseQuickWheelIndependent)
            or nameof(KeybindToolViewModel.ControllerPhotoModeIndependent)
            or nameof(KeybindToolViewModel.KeyMousePhotoModeIndependent)
            or nameof(KeybindToolViewModel.ControllerFishingModeIndependent)
            or nameof(KeybindToolViewModel.KeyMouseFishingModeIndependent))
        {
            OnPropertyChanged(nameof(IsIndependent));
        }
    }

    private void Texts_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IndependentText));
    }
}
