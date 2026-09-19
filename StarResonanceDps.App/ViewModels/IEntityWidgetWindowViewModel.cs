using System.ComponentModel;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// エンティティ(モンスター)に紐づくウィジェットウィンドウのViewModel。
///
/// <para>
/// バフ/デバフ一覧とバフ・デバフカードの2種類があり、
/// <see cref="Services.WidgetWindowManager"/> は同じ一覧で扱う。
/// </para>
/// </summary>
public interface IEntityWidgetWindowViewModel : INotifyPropertyChanged, IDisposable
{
    /// <summary>種別ID。復元時に対象を捕まえ直すのに使う。</summary>
    long EntityId { get; }

    /// <summary>実体の種類。<see cref="EntityId"/> がどの表の番号かを決めるので、捕まえ直すときは組で比べる。</summary>
    Zproto.EEntityType? EntityType { get; }

    /// <summary>捕まえている実体のID。<b>0 は未捕獲</b>(復元直後)。</summary>
    long EntityUuid { get; }

    bool IsEntityAcquired { get; }

    /// <summary>保存に使う名前。未捕獲なら復元時に読み込んだ名前。</summary>
    string TargetName { get; }

    string HeaderText { get; }

    bool RepresentsEntity(long entityUuid);

    /// <summary>個体を採用する。未捕獲なら種別一致で捕まえる。</summary>
    bool TryApplyEntity(EntityListEntry entity);

    void RefreshPresentation();
}
