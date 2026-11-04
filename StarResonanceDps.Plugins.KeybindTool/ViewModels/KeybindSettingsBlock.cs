namespace StarResonanceDps.Plugins.KeybindTool.ViewModels;

/// <summary>一覧の先頭の固定の節(ファイル・プリセット・入力方法・補助キー)を1項目として並べる入れ物。中身はビューモデル本体に結ぶ。</summary>
internal sealed class KeybindSettingsBlock
{
    public KeybindSettingsBlock(KeybindToolViewModel owner, KeybindSectionViewModel section)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Section = section ?? throw new ArgumentNullException(nameof(section));
    }

    public KeybindToolViewModel Owner { get; }

    public KeybindSectionViewModel Section { get; }
}
