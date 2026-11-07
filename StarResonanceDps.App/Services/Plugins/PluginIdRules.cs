namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグインが定義で渡す ID(ウィジェット・通知の項目・設定・選択肢)の決まり。SDK の各定義の説明と同じ。
/// </summary>
internal static class PluginIdRules
{
    private const int MaxLength = 128;

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
