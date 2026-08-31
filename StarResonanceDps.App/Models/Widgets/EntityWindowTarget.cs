using System.ComponentModel;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// エンティティ(モンスター)用ウィンドウが追う対象。
///
/// <para>
/// 通常は<b>実体1つ</b>に固定される。エンティティリストから選んで開いた窓は、
/// その個体を追い続ける。
/// </para>
///
/// <para>
/// 例外は<b>設定からの復元直後</b>。実体ID(<c>EntityUuid</c>)は再起動で消えるため、
/// 保存できるのは種別ID(<see cref="EntityId"/>)だけ。復元した窓は未捕獲の状態で開き、
/// その種別の個体が現れた時点で1体だけ捕まえる。捕まえた後は通常と同じで、その個体に固定される。
/// </para>
/// </summary>
public sealed class EntityWindowTarget
{
    private readonly string _savedName;
    private EntityListEntry? _entity;

    /// <summary>実体が分かっている状態で作る。エンティティリストからの経路。</summary>
    public EntityWindowTarget(EntityListEntry entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        EntityId = entity.EntityId;
        _savedName = entity.Name;
        Attach(entity);
    }

    /// <summary>種別だけ分かっている状態で作る。設定からの復元経路。</summary>
    public EntityWindowTarget(long entityId, string? savedName)
    {
        EntityId = entityId;
        _savedName = savedName ?? string.Empty;
    }

    /// <summary>対象が差し替わったとき(捕獲・実体の作り直し・表示名の変化)に上がる。</summary>
    public event EventHandler? Changed;

    public long EntityId { get; }

    /// <summary>捕まえている実体のID。<b>0 は未捕獲</b>。</summary>
    public long EntityUuid => _entity?.EntityUuid ?? 0;

    public bool IsAcquired => _entity is not null;

    /// <summary>捕まえている実体。未捕獲なら <c>null</c>。</summary>
    public EntityListEntry? Entity => _entity;

    /// <summary>ヘッダーに出す名前。未捕獲なら保存しておいた名前。</summary>
    public string DisplayName => _entity?.DisplayName ?? _savedName;

    /// <summary>書式を通していない素の名前。</summary>
    public string Name => _entity?.Name ?? _savedName;

    public int Level => _entity?.Level ?? 0;

    public bool Represents(long entityUuid)
    {
        return IsAcquired && EntityUuid == entityUuid;
    }

    /// <summary>
    /// 与えられた個体を採用する。未捕獲なら種別が一致するものを捕まえ、
    /// 捕獲済みなら同じ実体の新しいエントリにだけ差し替える。
    /// </summary>
    /// <returns>採用したら <c>true</c>。</returns>
    public bool TryApply(EntityListEntry entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (_entity is null)
        {
            if (entity.EntityId != EntityId)
            {
                return false;
            }

            Attach(entity);
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }

        if (entity.EntityUuid != EntityUuid || ReferenceEquals(_entity, entity))
        {
            return false;
        }

        Detach();
        Attach(entity);
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void Dispose()
    {
        Detach();
    }

    private void Attach(EntityListEntry entity)
    {
        _entity = entity;
        _entity.PropertyChanged += Entity_PropertyChanged;
    }

    private void Detach()
    {
        if (_entity is null)
        {
            return;
        }

        _entity.PropertyChanged -= Entity_PropertyChanged;
        _entity = null;
    }

    private void Entity_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EntityListEntry.DisplayName))
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
