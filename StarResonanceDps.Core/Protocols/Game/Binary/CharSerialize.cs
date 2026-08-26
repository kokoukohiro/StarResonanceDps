using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarResonanceDps.Core.Protocols.Game.Binary;

public class CharSerialize(BlobReader blob) : BlobType(ref blob)
{
    public override string? DebugName => "CharSerialize";

    public int? CharId;
    public CharBaseInfo? CharBaseInfo;
    public SceneData? SceneData;
    public BuffDBInfo? BuffInfo;
    public UserFightAttr? Attr;
    public SeasonRankList? SeasonRankList;
    public ProfessionList? ProfessionList;
    public CurrentProfessionProjectIdInfo? CurrentProjectIdInfo;
    public DutyList? DutyList;
    public FightPoint? FightPoint;
    public SeasonRoleLevelData? SeasonRoleLevelData;

    public override bool ParseField(int index, ref BlobReader blob)
    {
        switch (index)
        {
            case Zproto.CharSerialize.CharIdFieldNumber:
                CharId = blob.ReadInt();
                return true;
            case Zproto.CharSerialize.CharBaseFieldNumber:
                CharBaseInfo = new(blob);
                return true;
            case Zproto.CharSerialize.SceneDataFieldNumber:
                SceneData = new(blob);
                return true;
            case Zproto.CharSerialize.BuffInfoFieldNumber:
                ReadBuffInfoWithoutDisturbingStream(blob);
                return true;
            case Zproto.CharSerialize.AttrFieldNumber:
                Attr = new(blob);
                return true;
            case Zproto.CharSerialize.SeasonRankListFieldNumber:
                SeasonRankList = new(blob);
                return true;
            case Zproto.CharSerialize.ProfessionListFieldNumber:
                ProfessionList = new(blob);
                return true;
            case Zproto.CharSerialize.CurProjectIdInfoFieldNumber:
                CurrentProjectIdInfo = new(blob);
                return true;
            case Zproto.CharSerialize.DutyListFieldNumber:
                DutyList = new(blob);
                return true;
            case Zproto.CharSerialize.FightPointFieldNumber:
                FightPoint = new(blob);
                return true;
            case Zproto.CharSerialize.SeasonRoleLevelDataFieldNumber:
                SeasonRoleLevelData = new(blob);
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// buffInfo(field 6)を読む。<b>外側のストリーム位置は必ず従来と同じところで終える。</b>
    ///
    /// <para>
    /// このフィールドは従来 <see cref="BlobReader.TrySkipNestedBlob"/> で読み飛ばされていた。
    /// 素直にパースすると、こちらの解釈が1バイトでもずれた時点で、後続の
    /// professionList(61) や dutyList(107) が丸ごと失われる。
    /// そこで先に読み飛ばして正しい終端を確定させ、その範囲だけを読み直し、
    /// 結果によらず終端へ戻す。パースに失敗しても <see cref="BuffInfo"/> が null になるだけで済む。
    /// </para>
    /// </summary>
    private void ReadBuffInfoWithoutDisturbingStream(BlobReader blob)
    {
        var start = blob.Offset;
        if (!blob.TrySkipNestedBlob())
        {
            // 読み飛ばせない形。従来はここで解析打ち切りになっていたので、挙動を変えずに戻す。
            blob.Offset = start;
            return;
        }

        var end = blob.Offset;
        try
        {
            blob.Offset = start;
            BuffInfo = new(blob);
        }
        catch
        {
            BuffInfo = null;
        }
        finally
        {
            blob.Offset = end;
        }
    }
}
