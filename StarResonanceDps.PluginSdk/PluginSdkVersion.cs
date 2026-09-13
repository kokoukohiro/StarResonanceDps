namespace StarResonanceDps.PluginSdk;

/// <summary>
/// プラグインSDKのAPI版。<b>開発段階の間は 0 のまま上げない。</b>
///
/// <para>
/// これは<b>互換性の関門</b>で、<c>PluginManager</c> が
/// <c>registrationAttribute.ApiVersion != Current</c> のプラグインを読み込み時に弾く。
/// 値はプラグインDLLへ<b>コンパイル時に焼き込まれる</b>ので、上げた瞬間に
/// 「古い数字で作られたDLL」が全部読めなくなる。
/// </para>
///
/// <para>
/// <b>上げてはいけない理由。</b> まだ配布していないので、守るべき互換性の境界が存在しない。
/// 同梱プラグインはソリューションごと再ビルドされて常に一致するため、上げても何も検出しない。
/// 一方で外部に渡ったDLLがあれば、それを黙って締め出す。**得るものが無く、失うものだけある。**
/// しかも弾かれたときの表示は「Unsupported plugin API version」で、
/// プラグイン側の不具合のように見える。
/// </para>
///
/// <para>
/// <b>上げるのは最初のリリース以降、SDKの公開面に破壊的変更を入れたときだけ。</b>
/// そのとき初めてこの数字が意味を持つ。開発中にAPIをいくら変えても 0 のままでよい。
/// </para>
/// </summary>
public static class PluginSdkVersion
{
    public const int Current = 0;
}
