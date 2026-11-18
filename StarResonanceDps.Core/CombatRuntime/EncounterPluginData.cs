// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// プラグインが回1つに残した値(プラグインの鍵 → 値のキー → バイト列)。回の中に持ち、回と一緒に保存して、回と一緒に手放す。
///
/// <para>
/// 書けるのは保存の前だけ。保存の写しを取るのと同時に封をし(<see cref="SealForSave"/>)、封の後の <see cref="TrySet"/> は書かない
/// (写すのと封を別にすると、その間に書いた値が DB に入らないまま成功になる)。保存に失敗したら封を戻す(回も保存されないので値を残す)。
/// DB から読んだ回は封をしたまま。保存した回の値を置き換えるのは <see cref="ReplaceSaved"/> だけ(DB へ書いた後に、本体が持っている写しへ入れる)。
/// </para>
///
/// <para>
/// プラグインの鍵(アセンブリ名)も値のキーも、大文字と小文字を区別しない(本体のプラグインの見分け方と同じ。DB の列も同じ照合)。
/// 上限の数は持たない。Core は SDK を参照しないので、呼ぶ側が渡す。配列は受け取ったものをそのまま持ち、書き換えない(書き込みのたびに別の配列に替わる)。
/// </para>
/// </summary>
public sealed class EncounterPluginDataStore
{
    private readonly object _gate = new();
    private Dictionary<string, Dictionary<string, byte[]>>? _byPlugin;
    private bool _isSealed;

    /// <summary>
    /// 保存の前の回に値を書く。長さ0はキーを消す。置き換えた後のそのプラグインのキーの数か長さの合計が上限を越えるなら書かない。
    /// </summary>
    public EncounterPluginDataSetResult TrySet(string pluginKey, string dataKey, byte[] data, int maxKeys, int maxTotalBytes)
    {
        ArgumentNullException.ThrowIfNull(pluginKey);
        ArgumentNullException.ThrowIfNull(dataKey);
        ArgumentNullException.ThrowIfNull(data);

        lock (_gate)
        {
            if (_isSealed)
            {
                return EncounterPluginDataSetResult.Sealed;
            }

            if (data.Length == 0)
            {
                RemoveLocked(pluginKey, dataKey);
                return EncounterPluginDataSetResult.Stored;
            }

            var keyCount = 1;
            long totalBytes = data.Length;
            if (_byPlugin?.TryGetValue(pluginKey, out var current) == true)
            {
                foreach (var (key, value) in current)
                {
                    if (!string.Equals(key, dataKey, StringComparison.OrdinalIgnoreCase))
                    {
                        keyCount++;
                        totalBytes += value.Length;
                    }
                }
            }

            if (keyCount > maxKeys)
            {
                return EncounterPluginDataSetResult.TooManyKeys;
            }

            if (totalBytes > maxTotalBytes)
            {
                return EncounterPluginDataSetResult.TooLarge;
            }

            SetLocked(pluginKey, dataKey, data);
            return EncounterPluginDataSetResult.Stored;
        }
    }

    /// <summary>そのプラグインのそのキーの値。無ければ null。</summary>
    public byte[]? Get(string pluginKey, string dataKey)
    {
        lock (_gate)
        {
            return _byPlugin is not null
                && _byPlugin.TryGetValue(pluginKey, out var values)
                && values.TryGetValue(dataKey, out var data)
                    ? data
                    : null;
        }
    }

    /// <summary>保存のトランザクションに入れる全部の行を写し、同じロックの中で封をする。</summary>
    internal IReadOnlyList<EncounterPluginDataRow> SealForSave()
    {
        lock (_gate)
        {
            _isSealed = true;
            if (_byPlugin is null)
            {
                return [];
            }

            var rows = new List<EncounterPluginDataRow>();
            foreach (var (pluginKey, values) in _byPlugin)
            {
                foreach (var (dataKey, data) in values)
                {
                    rows.Add(new EncounterPluginDataRow(pluginKey, dataKey, data));
                }
            }

            return rows;
        }
    }

    /// <summary>保存のトランザクションが失敗した(回も保存されていない)。封を戻して、保存の前の回として書けるようにする。</summary>
    internal void UnsealAfterFailedSave()
    {
        lock (_gate)
        {
            _isSealed = false;
        }
    }

    /// <summary>DB から読んだ回に、保存した値を入れて封をする。</summary>
    internal void LoadSaved(IEnumerable<EncounterPluginDataRow> rows)
    {
        lock (_gate)
        {
            _isSealed = true;
            foreach (var row in rows)
            {
                SetLocked(row.PluginKey, row.DataKey, row.Data);
            }
        }
    }

    /// <summary>
    /// 保存した回の写し(開いている履歴の回・保持している前の回)の値を、DB へ書いた後に置き換える。封に関係なく書く。長さ0はキーを消す。
    /// 上限は DB に書く前に確かめてある。
    /// </summary>
    internal void ReplaceSaved(string pluginKey, string dataKey, byte[] data)
    {
        lock (_gate)
        {
            if (data.Length == 0)
            {
                RemoveLocked(pluginKey, dataKey);
            }
            else
            {
                SetLocked(pluginKey, dataKey, data);
            }
        }
    }

    private void SetLocked(string pluginKey, string dataKey, byte[] data)
    {
        _byPlugin ??= new Dictionary<string, Dictionary<string, byte[]>>(StringComparer.OrdinalIgnoreCase);
        if (!_byPlugin.TryGetValue(pluginKey, out var values))
        {
            values = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            _byPlugin.Add(pluginKey, values);
        }

        // 大文字と小文字だけ違うキーは同じキー。書いたときの書き方で持ち直す。
        values.Remove(dataKey);
        values.Add(dataKey, data);
    }

    private void RemoveLocked(string pluginKey, string dataKey)
    {
        if (_byPlugin is null || !_byPlugin.TryGetValue(pluginKey, out var values))
        {
            return;
        }

        values.Remove(dataKey);
        if (values.Count == 0)
        {
            _byPlugin.Remove(pluginKey);
        }
    }
}

/// <summary>プラグインが回に残した値の1行(保存と読み込みの形)。</summary>
/// <param name="PluginKey">プラグインの鍵(アセンブリ名)。</param>
/// <param name="DataKey">プラグインが付けたキー。</param>
/// <param name="Data">値(縮める前)。</param>
public sealed record EncounterPluginDataRow(string PluginKey, string DataKey, byte[] Data);

/// <summary><see cref="EncounterPluginDataStore.TrySet"/> の結果。</summary>
public enum EncounterPluginDataSetResult
{
    /// <summary>書いた(長さ0なら消した)。</summary>
    Stored,

    /// <summary>保存の写しを取った後なので書かなかった。</summary>
    Sealed,

    /// <summary>そのプラグインのキーの数が上限を越えるので書かなかった。</summary>
    TooManyKeys,

    /// <summary>そのプラグインの値の長さの合計が上限を越えるので書かなかった。</summary>
    TooLarge
}
