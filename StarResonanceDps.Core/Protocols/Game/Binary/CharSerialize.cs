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
}
