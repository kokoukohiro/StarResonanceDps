using System.Globalization;

namespace StarResonanceDps.App.Models.Notifications;

/// <summary>
/// 通知タブの対象の名前とIDの欄に、区切りの文字(<see cref="Separator"/>)で並べて入れる複数の値。
/// 前後の空白は除き、空の区切りは無視する。IDの欄の入力は数字と区切りだけで、区切りのあいだの数は long に収まる桁まで(<see cref="Behaviors.DigitsOnlyInputBehavior"/>)。
/// </summary>
public static class NotificationTargetList
{
    public const string Separator = "|";

    public static List<string> SplitNames(string text)
    {
        return
        [
            .. text
                .Split(Separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        ];
    }

    /// <summary>IDの欄の文字(数字と区切りだけ)を数にする。</summary>
    public static List<long> ParseIds(string text)
    {
        return
        [
            .. text
                .Split(Separator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(id => long.Parse(id, NumberStyles.None, CultureInfo.InvariantCulture))
        ];
    }

    public static string JoinNames(IEnumerable<string> names)
    {
        return string.Join(Separator, names);
    }

    public static string JoinIds(IEnumerable<long> ids)
    {
        return string.Join(Separator, ids.Select(id => id.ToString(CultureInfo.InvariantCulture)));
    }
}
