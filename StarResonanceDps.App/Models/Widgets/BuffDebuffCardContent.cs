using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// バフ・デバフカードが1件のバフから作る表示値。
///
/// <para>
/// プレイヤー用とモンスター用でViewModelが分かれるので、<b>組み立てはここに集める</b>。
/// 分けて書くと片方だけ直して見た目がずれる。
/// </para>
/// </summary>
public readonly record struct BuffDebuffCardContent(
    string DisplayText,
    string DurationText,
    string LayerText,
    string? IconPath)
{
    public static BuffDebuffCardContent Empty { get; } = new(
        string.Empty,
        string.Empty,
        string.Empty,
        null);

    public static BuffDebuffCardContent Create(
        PlayerBuffSnapshot snapshot,
        string targetName,
        int level,
        string? formatString)
    {
        var entry = new PlayerBuffEntry(snapshot);

        return new BuffDebuffCardContent(
            BuffInfoFormatFormatter.Format(entry.Name, targetName, level, formatString),
            entry.DurationWithUnitText,
            entry.LayerText,
            entry.IconPath);
    }

    /// <summary>
    /// 倍率の保存キー。まとまりを追う窓は <c>{対象ID}:group:{まとまり}</c>、
    /// 個別のバフを追う窓は <c>{対象ID}:{バフキー}</c>。
    ///
    /// <para>
    /// まとまりで個別のバフキーを使わないのは、料理や薬剤が<b>食べ直すたびに別のIDへ入れ替わる</b>
    /// ため。個別キーのままだと入れ替わりのたびに倍率が既定へ戻ってしまう。
    /// </para>
    ///
    /// <para>
    /// 必要な値が欠けている間は空を返し、呼び出し側が保存も読み出しもしない。
    /// </para>
    /// </summary>
    public static string CreateScaleKey(long targetId, BuffGroup group, string? buffKey)
    {
        if (targetId == 0)
        {
            return string.Empty;
        }

        if (group != BuffGroup.None)
        {
            return $"{targetId}:group:{group}";
        }

        return string.IsNullOrWhiteSpace(buffKey)
            ? string.Empty
            : $"{targetId}:{buffKey}";
    }

    /// <summary>
    /// そのまとまりに属するバフを探す。入れ替わっても同じまとまりなら拾い直せる。
    /// 複数付いていることはゲーム仕様上ないが、あれば一覧順の先頭を採る。
    /// </summary>
    public static PlayerBuffSnapshot? FindByGroup(
        IReadOnlyList<PlayerBuffSnapshot> snapshots,
        BuffGroup group)
    {
        if (group == BuffGroup.None)
        {
            return null;
        }

        foreach (var snapshot in snapshots)
        {
            if (CombatDataCatalog.GetBuffGroup(snapshot.BaseId) == group)
            {
                return snapshot;
            }
        }

        return null;
    }

    /// <summary>
    /// タイトルの前半。「バフ(名前)」「デバフ(名前)」の形にする。
    ///
    /// <para>
    /// まとまりを追う窓は個別のバフ名ではなく<b>まとまりの名前</b>(料理・薬剤)を出す。
    /// まとまりは対象が居なくても名乗れるので、起動直後にウィジェット名のままになることがない。
    /// </para>
    ///
    /// <para>
    /// 個別のバフを追う窓では、失効しても<b>最後に分かった名前を出し続ける</b>(呼び出し側が保持する)。
    /// 一度も分かっていないときだけウィジェット名に落ちる。
    /// </para>
    /// </summary>
    public static string CreateHeaderPrefix(
        string widgetDisplayName,
        PlayerBuffListKind kind,
        BuffGroup group,
        string lastKnownBuffName)
    {
        var name = group != BuffGroup.None
            ? LocalizationManager.Instance.GetString($"Widget_BuffDebuffCard_Group_{group}")
            : lastKnownBuffName;

        if (string.IsNullOrWhiteSpace(name))
        {
            return widgetDisplayName;
        }

        return LocalizationManager.Instance.Format(
            kind == PlayerBuffListKind.Debuff
                ? "Widget_BuffDebuffCard_TitleDebuff"
                : "Widget_BuffDebuffCard_TitleBuff",
            name);
    }

    public static PlayerBuffSnapshot? FindByKey(
        IReadOnlyList<PlayerBuffSnapshot> snapshots,
        string buffKey)
    {
        foreach (var snapshot in snapshots)
        {
            if (string.Equals(snapshot.Key, buffKey, StringComparison.Ordinal))
            {
                return snapshot;
            }
        }

        return null;
    }
}
