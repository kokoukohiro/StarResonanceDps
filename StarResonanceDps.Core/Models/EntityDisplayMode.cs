namespace StarResonanceDps.Core.Models;

/// <summary>エンティティリストのフィルター。</summary>
public enum EntityDisplayMode
{
    All = 0,

    /// <summary>ゲーム内で HP バーが出る実体だけ(<see cref="NearbyEntityEntry.HasHpBar"/>)。</summary>
    HideObjects = 1
}
