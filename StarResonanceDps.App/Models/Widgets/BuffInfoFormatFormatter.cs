using System.Text.RegularExpressions;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// バフ・デバフカードの表示テキストを組み立てる。
///
/// <para>
/// <b>プレイヤーとモンスターで同じ関数を通す。</b>どちらも名前とレベルを持つので、
/// 呼ぶ側が値を取り出して渡すだけでよい。
/// </para>
/// </summary>
public static partial class BuffInfoFormatFormatter
{
    public static string Format(string buffName, string targetName, int level, string? formatString)
    {
        var result = string.IsNullOrEmpty(formatString)
            ? buffName
            : formatString;

        result = GetBuffNameRegex().Replace(result, buffName);
        result = GetNameRegex().Replace(result, targetName);
        result = GetLevelRegex().Replace(result, level.ToString());
        result = GetNewLineRegex().Replace(result, Environment.NewLine);
        return result;
    }

    public static string FormatPreview(string? formatString)
    {
        return Format(
            LocalizationManager.Instance.GetString("Settings_BuffInfo_PreviewBuffName"),
            LocalizationManager.Instance.GetString("Settings_BuffInfo_PreviewName"),
            50,
            formatString);
    }

    [GeneratedRegex(@"\{BuffName\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetBuffNameRegex();

    [GeneratedRegex(@"\{Name\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetNameRegex();

    [GeneratedRegex(@"\{Level\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetLevelRegex();

    [GeneratedRegex(@"\{NewLine\}", RegexOptions.IgnoreCase)]
    private static partial Regex GetNewLineRegex();
}
