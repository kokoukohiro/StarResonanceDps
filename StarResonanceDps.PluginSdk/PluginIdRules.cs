namespace StarResonanceDps.PluginSdk;

/// <summary>
/// プラグイン ID の決まり。本体の読み込み(<c>PluginManager</c>)と SDK の解析器が、このファイル1つを使う(解析器はソースを取り込む)。
/// 解析器は netstandard2.0 で動くので、新しい API(<c>char.IsAsciiLetterOrDigit</c> など)を使わない。
/// </summary>
internal static class PluginIdRules
{
    public const int MaxLength = 128;

    /// <summary>前後の空白を除いた ID が、1〜<see cref="MaxLength"/> 文字の ASCII の英数字と「.」「-」「_」だけか。</summary>
    public static bool IsValid(string? id)
    {
        var trimmed = id?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || trimmed.Length > MaxLength)
        {
            return false;
        }

        foreach (var character in trimmed)
        {
            var isAllowed = character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '.' or '-' or '_';
            if (!isAllowed)
            {
                return false;
            }
        }

        return true;
    }
}
