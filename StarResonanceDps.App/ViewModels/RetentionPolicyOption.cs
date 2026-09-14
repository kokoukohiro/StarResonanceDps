namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 集計設定「履歴の保持期間」の選択肢1つ。
///
/// <para>
/// <see cref="Days"/> は保存値そのもの(<c>AppConfig.DatabaseRetentionPolicyDays</c>)で、
/// <b>0 が無期限。</b>ComboBox は <c>SelectedValue</c> でこれを直接束縛するので、
/// 表示の並び順と保存値の対応表を別に持たなくてよい。
/// </para>
///
/// <para>
/// <b>文言は作り直しで差し替える。</b>言語が変わると <see cref="DisplayName"/> も変わるが、
/// 通知を持たせずに <c>SettingsViewModel.RebuildRetentionPolicyOptions</c> が
/// コレクションごと作り直す(選択肢が5件しかないので、変更通知を足す価値がない)。
/// </para>
/// </summary>
public sealed class RetentionPolicyOption
{
    public RetentionPolicyOption(int days, string displayName)
    {
        Days = days;
        DisplayName = displayName;
    }

    public int Days { get; }

    public string DisplayName { get; }
}
