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

    /// <summary>
    /// どちらも <see cref="IsValid"/> を通る版を、番号ごとに数の大きさで比べる(2.0.10 は 2.0.9 より新しい)。
    /// 桁数に上限が無いので、数へ変換しない。
    /// </summary>
    public static int Compare(string left, string right)
    {
        var leftParts = left.Trim().Split('.');
        var rightParts = right.Trim().Split('.');
        for (var index = 0; index < leftParts.Length; index++)
        {
            var leftNumber = leftParts[index].TrimStart('0');
            var rightNumber = rightParts[index].TrimStart('0');
            var result = leftNumber.Length != rightNumber.Length
                ? leftNumber.Length.CompareTo(rightNumber.Length)
                : string.CompareOrdinal(leftNumber, rightNumber);
            if (result != 0)
            {
                return result;
            }
        }

        return 0;
    }
}
