namespace StarResonanceDps.PluginSdk;

/// <summary>
/// プラグインの版の決まり。本体の読み込み(<c>PluginManager</c>)と SDK の解析器が、このファイル1つを使う(解析器はソースを取り込む)。
/// 解析器は netstandard2.0 で動くので、新しい API を使わない。
/// </summary>
internal static class PluginVersionRules
{
    /// <summary>前後の空白を除いた版が、ASCII の数字だけの3つを「.」でつないだ形か。</summary>
    public static bool IsValid(string? version)
    {
        var parts = (version?.Trim() ?? string.Empty).Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (part.Length == 0)
            {
                return false;
            }

            foreach (var character in part)
            {
                if (character is < '0' or > '9')
                {
                    return false;
                }
            }
        }

        return true;
    }
}
