using System.Collections.ObjectModel;
using StarResonanceDps.Plugins.KeybindTool.Models;

namespace StarResonanceDps.Plugins.KeybindTool.Services;

internal static class KeybindCatalog
{
    public const string DefaultControllerType = "PlayStation";
    public const string KeyMouseInputDeviceName = "キーボード/マウス";

    public const uint InputTypeKeyboard = 0x00000001u;
    public const uint InputTypeMouse = 0x00000002u;
    public const uint InputTypeController = 0x00000003u;

    public const uint ActionStateSingle = 0xFFFFFFFFu;
    public const uint ActionStateHelper1 = 0x00000000u;
    public const uint ActionStateHelper2 = 0x00000001u;

    public const string HelperNoneLabel = "割り当てなし";
    public const string ButtonLayoutFileName = "bpsr_controller_helper_config.json";

    public static readonly IReadOnlyList<string> ControllerTypes =
        new[] { "PlayStation", "Nintendo", "Xbox" };

    public static readonly IReadOnlyList<ControllerInputOption> ControllerInputOptions =
        new ControllerInputOption[]
        {
        new ControllerInputOption(0x1u, "L前後入力"),
        new ControllerInputOption(0x2u, "L左右入力"),
        new ControllerInputOption(0x3u, "R前後入力"),
        new ControllerInputOption(0x4u, "R左右入力"),
        new ControllerInputOption(0x5u, "L2"),
        new ControllerInputOption(0x6u, "R2"),
        new ControllerInputOption(0x7u, "×"),
        new ControllerInputOption(0x8u, "〇"),
        new ControllerInputOption(0xAu, "□"),
        new ControllerInputOption(0xBu, "△"),
        new ControllerInputOption(0xDu, "touchpad"),
        new ControllerInputOption(0xEu, "option"),
        new ControllerInputOption(0xFu, "share"),
        new ControllerInputOption(0x11u, "L1"),
        new ControllerInputOption(0x12u, "R1"),
        new ControllerInputOption(0x13u, "L3"),
        new ControllerInputOption(0x14u, "R3"),
        new ControllerInputOption(0x17u, "↑"),
        new ControllerInputOption(0x18u, "↓"),
        new ControllerInputOption(0x19u, "←"),
        new ControllerInputOption(0x1Au, "→"),
        };

    public static readonly IReadOnlyList<KeyMouseInputOption> KeyMouseInputOptions =
        new KeyMouseInputOption[]
        {
        new KeyMouseInputOption(0x1u, 0x9u, "Tab"),
        new KeyMouseInputOption(0x1u, 0xDu, "Enter"),
        new KeyMouseInputOption(0x1u, 0x1Bu, "Esc"),
        new KeyMouseInputOption(0x1u, 0x20u, "Space"),
        new KeyMouseInputOption(0x1u, 0x27u, ":"),
        new KeyMouseInputOption(0x1u, 0x2Cu, "<"),
        new KeyMouseInputOption(0x1u, 0x2Du, "-"),
        new KeyMouseInputOption(0x1u, 0x2Eu, ">"),
        new KeyMouseInputOption(0x1u, 0x2Fu, "/"),
        new KeyMouseInputOption(0x1u, 0x30u, "0"),
        new KeyMouseInputOption(0x1u, 0x31u, "1"),
        new KeyMouseInputOption(0x1u, 0x32u, "2"),
        new KeyMouseInputOption(0x1u, 0x33u, "3"),
        new KeyMouseInputOption(0x1u, 0x34u, "4"),
        new KeyMouseInputOption(0x1u, 0x35u, "5"),
        new KeyMouseInputOption(0x1u, 0x36u, "6"),
        new KeyMouseInputOption(0x1u, 0x37u, "7"),
        new KeyMouseInputOption(0x1u, 0x38u, "8"),
        new KeyMouseInputOption(0x1u, 0x39u, "9"),
        new KeyMouseInputOption(0x1u, 0x3Bu, ";"),
        new KeyMouseInputOption(0x1u, 0x3Du, "^"),
        new KeyMouseInputOption(0x1u, 0x5Bu, "@"),
        new KeyMouseInputOption(0x1u, 0x5Cu, "]"),
        new KeyMouseInputOption(0x1u, 0x5Du, "["),
        new KeyMouseInputOption(0x1u, 0x60u, "~"),
        new KeyMouseInputOption(0x1u, 0x61u, "A"),
        new KeyMouseInputOption(0x1u, 0x62u, "B"),
        new KeyMouseInputOption(0x1u, 0x63u, "C"),
        new KeyMouseInputOption(0x1u, 0x64u, "D"),
        new KeyMouseInputOption(0x1u, 0x65u, "E"),
        new KeyMouseInputOption(0x1u, 0x66u, "F"),
        new KeyMouseInputOption(0x1u, 0x67u, "G"),
        new KeyMouseInputOption(0x1u, 0x68u, "H"),
        new KeyMouseInputOption(0x1u, 0x69u, "I"),
        new KeyMouseInputOption(0x1u, 0x6Au, "J"),
        new KeyMouseInputOption(0x1u, 0x6Bu, "K"),
        new KeyMouseInputOption(0x1u, 0x6Cu, "L"),
        new KeyMouseInputOption(0x1u, 0x6Du, "M"),
        new KeyMouseInputOption(0x1u, 0x6Eu, "N"),
        new KeyMouseInputOption(0x1u, 0x6Fu, "O"),
        new KeyMouseInputOption(0x1u, 0x70u, "P"),
        new KeyMouseInputOption(0x1u, 0x71u, "Q"),
        new KeyMouseInputOption(0x1u, 0x72u, "R"),
        new KeyMouseInputOption(0x1u, 0x73u, "S"),
        new KeyMouseInputOption(0x1u, 0x74u, "T"),
        new KeyMouseInputOption(0x1u, 0x75u, "U"),
        new KeyMouseInputOption(0x1u, 0x76u, "V"),
        new KeyMouseInputOption(0x1u, 0x77u, "W"),
        new KeyMouseInputOption(0x1u, 0x78u, "X"),
        new KeyMouseInputOption(0x1u, 0x79u, "Y"),
        new KeyMouseInputOption(0x1u, 0x7Au, "Z"),
        new KeyMouseInputOption(0x1u, 0x100u, "Num0"),
        new KeyMouseInputOption(0x1u, 0x101u, "Num1"),
        new KeyMouseInputOption(0x1u, 0x102u, "Num2"),
        new KeyMouseInputOption(0x1u, 0x103u, "Num3"),
        new KeyMouseInputOption(0x1u, 0x104u, "Num4"),
        new KeyMouseInputOption(0x1u, 0x105u, "Num5"),
        new KeyMouseInputOption(0x1u, 0x106u, "Num6"),
        new KeyMouseInputOption(0x1u, 0x107u, "Num7"),
        new KeyMouseInputOption(0x1u, 0x108u, "Num8"),
        new KeyMouseInputOption(0x1u, 0x109u, "Num9"),
        new KeyMouseInputOption(0x1u, 0x111u, "↑"),
        new KeyMouseInputOption(0x1u, 0x112u, "↓"),
        new KeyMouseInputOption(0x1u, 0x113u, "→"),
        new KeyMouseInputOption(0x1u, 0x114u, "←"),
        new KeyMouseInputOption(0x1u, 0x11Au, "F1"),
        new KeyMouseInputOption(0x1u, 0x11Bu, "F2"),
        new KeyMouseInputOption(0x1u, 0x11Cu, "F3"),
        new KeyMouseInputOption(0x1u, 0x11Du, "F4"),
        new KeyMouseInputOption(0x1u, 0x11Eu, "F5"),
        new KeyMouseInputOption(0x1u, 0x11Fu, "F6"),
        new KeyMouseInputOption(0x1u, 0x120u, "F7"),
        new KeyMouseInputOption(0x1u, 0x121u, "F8"),
        new KeyMouseInputOption(0x1u, 0x122u, "F9"),
        new KeyMouseInputOption(0x1u, 0x123u, "F10"),
        new KeyMouseInputOption(0x1u, 0x124u, "F11"),
        new KeyMouseInputOption(0x1u, 0x125u, "F12"),
        new KeyMouseInputOption(0x1u, 0x12Fu, "R Shift"),
        new KeyMouseInputOption(0x1u, 0x130u, "L Shift"),
        new KeyMouseInputOption(0x1u, 0x131u, "R Ctrl"),
        new KeyMouseInputOption(0x1u, 0x132u, "L Ctrl"),
        new KeyMouseInputOption(0x1u, 0x133u, "R Alt"),
        new KeyMouseInputOption(0x1u, 0x134u, "L Alt"),
        new KeyMouseInputOption(0x2u, 0x0u, "マウス左クリック"),
        new KeyMouseInputOption(0x2u, 0x1u, "マウス右クリック"),
        new KeyMouseInputOption(0x2u, 0x2u, "マウス中央キー"),
        new KeyMouseInputOption(0x2u, 0x3u, "マウスボタン3"),
        new KeyMouseInputOption(0x2u, 0x4u, "マウスボタン4"),
        new KeyMouseInputOption(0x2u, 0x5u, "マウスボタン5"),
        new KeyMouseInputOption(0x2u, 0x6u, "マウスボタン6"),
        new KeyMouseInputOption(0x2u, 0x7u, "マウススクロール"),
        };


    public static readonly IReadOnlyList<ControllerActionDefinition> ControllerMainActions =
        new ControllerActionDefinition[]
        {
            CreateControllerAction(KeybindModeGroup.Main, "移動-前後", new[] { 0xB2 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Main, "移動-左右", new[] { 0xC7 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Main, "カメラ-前後", new[] { 0x60F }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Main, "カメラ-左右", new[] { 0x624 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Main, "ジャンプ", new[] { 0x133 }),
            CreateControllerAction(KeybindModeGroup.Main, "ダッシュ/回避", new[] { 0x1C7 }),
            CreateControllerAction(KeybindModeGroup.Main, "環境共鳴能力1", new[] { 0x204 }),
            CreateControllerAction(KeybindModeGroup.Main, "環境共鳴能力2", new[] { 0x227 }),
            CreateControllerAction(KeybindModeGroup.Main, "通常攻撃", new[] { 0x27E }),
            CreateControllerAction(KeybindModeGroup.Main, "特殊攻撃", new[] { 0x9E7 }),
            CreateControllerAction(KeybindModeGroup.Main, "マスタリースキル1", new[] { 0x2D5 }),
            CreateControllerAction(KeybindModeGroup.Main, "マスタリースキル2", new[] { 0x312 }),
            CreateControllerAction(KeybindModeGroup.Main, "マスタリースキル3", new[] { 0x34F }),
            CreateControllerAction(KeybindModeGroup.Main, "マスタリースキル4", new[] { 0x38C }),
            CreateControllerAction(KeybindModeGroup.Main, "究極スキル", new[] { 0x9AA }),
            CreateControllerAction(KeybindModeGroup.Main, "バトルイマジン1", new[] { 0xA24 }),
            CreateControllerAction(KeybindModeGroup.Main, "バトルイマジン2", new[] { 0xA61 }),
            CreateControllerAction(KeybindModeGroup.Main, "左でアイテム切り替え", new[] { 0x102D }),
            CreateControllerAction(KeybindModeGroup.Main, "アイテム使用", new[] { 0x3C9 }),
            CreateControllerAction(KeybindModeGroup.Main, "右でアイテム切り替え", new[] { 0x106A }),
            CreateControllerAction(KeybindModeGroup.Main, "アクション", new[] { 0x551, 0x158F }),
            CreateControllerAction(KeybindModeGroup.Main, "ロックオン/切り替え", new[] { 0x406 }),
            CreateControllerAction(KeybindModeGroup.Main, "エクストラスキル", new[] { 0xA9E }),
            CreateControllerAction(KeybindModeGroup.Main, "インタラクト解除", new[] { 0x45D }),
            CreateControllerAction(KeybindModeGroup.Main, "クエスト追跡", new[] { 0x514 }),
            CreateControllerAction(KeybindModeGroup.Main, "UI非表示", new[] { 0x4D7 }),
            CreateControllerAction(KeybindModeGroup.Main, "クエストアイテムのクイック使用", new[] { 0x49A }),
            CreateControllerAction(KeybindModeGroup.Main, "マップON/OFF", new[] { 0x690 }),
            CreateControllerAction(KeybindModeGroup.Main, "クエスト", new[] { 0x6CD }),
            CreateControllerAction(KeybindModeGroup.Main, "ソーシャルモード", new[] { 0x70A }),
            CreateControllerAction(KeybindModeGroup.Main, "メニューを開く", new[] { 0x84D }),
            CreateControllerAction(KeybindModeGroup.Main, "メニューを閉じる", new[] { 0x17CE }),
            CreateControllerAction(KeybindModeGroup.Main, "カーソル移動-上下", new[] { 0x1F40 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Main, "カーソル移動-左右", new[] { 0x1F63 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Main, "撮影", new[] { 0x76A }),
            CreateControllerAction(KeybindModeGroup.Main, "ダンジョン退出", new[] { 0x810 }),
            CreateControllerAction(KeybindModeGroup.Main, "アイテムを使用", new[] { 0x90D, 0x186B }),
            CreateControllerAction(KeybindModeGroup.Main, "クイック操作", new[] { 0xBA4 }),
            CreateControllerAction(KeybindModeGroup.Main, "乗り物召喚/解除", new[] { 0xB44 }),
            CreateControllerAction(KeybindModeGroup.Main, "招待承認", new[] { 0xBE1, 0x1A89 }),
            CreateControllerAction(KeybindModeGroup.Main, "招待拒否", new[] { 0xC1E, 0x1AC6 }),
            CreateControllerAction(KeybindModeGroup.Main, "オートバトル", new[] { 0xCBB }),
            CreateControllerAction(KeybindModeGroup.Main, "チャンネル", new[] { 0xC7E }),
            CreateControllerAction(KeybindModeGroup.Main, "イラストガイド", new[] { 0xCF8 }),
            CreateControllerAction(KeybindModeGroup.Main, "クイックホイール", new[] { 0xD35 }),
            CreateControllerAction(KeybindModeGroup.Main, "クエスト切り替え（左）", new[] { 0xFB3 }),
            CreateControllerAction(KeybindModeGroup.Main, "クエスト切り替え（右）", new[] { 0xFD6 }),
            CreateControllerAction(KeybindModeGroup.Main, "ズームアウト", new[] { 0x58E }),
            CreateControllerAction(KeybindModeGroup.Main, "ズームイン", new[] { 0x5A3 }),
            CreateControllerAction(KeybindModeGroup.Main, "スキルパレットを開く", new[] { 0x1227, 0x1F7D }),
            CreateControllerAction(KeybindModeGroup.Main, "ロールスキル1", new[] { 0x1133 }),
            CreateControllerAction(KeybindModeGroup.Main, "ロールスキル2", new[] { 0x1170 }),
            CreateControllerAction(KeybindModeGroup.Main, "ロールスキル3", new[] { 0x11AD }),
            CreateControllerAction(KeybindModeGroup.Main, "ロールスキル4", new[] { 0x11EA }),
            CreateControllerAction(KeybindModeGroup.Main, "ホーム設計図", new[] { 0x124A }),
        };
    public static readonly IReadOnlyList<ControllerActionDefinition> ControllerQuickWheelActions =
        new ControllerActionDefinition[]
        {
            CreateControllerAction(KeybindModeGroup.QuickWheel, "クイックホイール切替（左）", new[] { 0x289C }, usesHelper: false),
            CreateControllerAction(KeybindModeGroup.QuickWheel, "クイックホイール切替（右）", new[] { 0x28B1 }, usesHelper: false),
            CreateControllerAction(KeybindModeGroup.QuickWheel, "クイックホイール編集", new[] { 0x28EE }, usesHelper: false),
            CreateControllerAction(KeybindModeGroup.QuickWheel, "クイックホイールを閉じる", new[] { 0x292B }),
        };
    public static readonly IReadOnlyList<ControllerActionDefinition> ControllerPhotoActions =
        new ControllerActionDefinition[]
        {
            CreateControllerAction(KeybindModeGroup.Photo, "カメラ移動-上下", new[] { 0x230E }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "カメラ移動-左右", new[] { 0x2323 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "カメラパン-前後", new[] { 0x239F }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "カメラパン-左右", new[] { 0x23B4 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "画面を非表示にする", new[] { 0x21B8 }),
            CreateControllerAction(KeybindModeGroup.Photo, "撮影", new[] { 0x213E }),
            CreateControllerAction(KeybindModeGroup.Photo, "設定メニュー", new[] { 0x2688 }),
            CreateControllerAction(KeybindModeGroup.Photo, "参加者メニュー", new[] { 0x26C5 }),
            CreateControllerAction(KeybindModeGroup.Photo, "カーソル呼出し", new[] { 0x27B8 }),
            CreateControllerAction(KeybindModeGroup.Photo, "メニューを閉じる", new[] { 0x226F }),
            CreateControllerAction(KeybindModeGroup.Photo, "移動-前後", new[] { 0x2430 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "移動-左右", new[] { 0x2445 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "カメラ-前後", new[] { 0x273A }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "カメラ-左右", new[] { 0x274F }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "ジャンプ", new[] { 0x205D }),
            CreateControllerAction(KeybindModeGroup.Photo, "ダッシュ/回避", new[] { 0x217B }),
            CreateControllerAction(KeybindModeGroup.Photo, "特殊攻撃", new[] { 0x24B1 }),
            CreateControllerAction(KeybindModeGroup.Photo, "マスタリースキル1", new[] { 0x24EE }),
            CreateControllerAction(KeybindModeGroup.Photo, "マスタリースキル2", new[] { 0x252B }),
            CreateControllerAction(KeybindModeGroup.Photo, "マスタリースキル3", new[] { 0x2568 }),
            CreateControllerAction(KeybindModeGroup.Photo, "マスタリースキル4", new[] { 0x25A5 }),
            CreateControllerAction(KeybindModeGroup.Photo, "究極スキル", new[] { 0x209A }),
            CreateControllerAction(KeybindModeGroup.Photo, "乗り物召喚/解除", new[] { 0x25E2 }),
            CreateControllerAction(KeybindModeGroup.Photo, "カーソル移動-上下", new[] { 0x27FE }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "カーソル移動-左右", new[] { 0x2821 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "ズームアウト", new[] { 0x20EC }),
            CreateControllerAction(KeybindModeGroup.Photo, "ズームイン", new[] { 0x2101 }),
        };
    public static readonly IReadOnlyList<ControllerActionDefinition> ControllerFishingActions =
        new ControllerActionDefinition[]
        {
            CreateControllerAction(KeybindModeGroup.Fishing, "竿移動-前後", new[] { 0x2BB0 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Fishing, "竿移動-左右", new[] { 0x2BC5 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Fishing, "キャスト/竿を引く", new[] { 0x29A3 }),
            CreateControllerAction(KeybindModeGroup.Fishing, "釣り/図鑑", new[] { 0x29E0 }),
            CreateControllerAction(KeybindModeGroup.Fishing, "釣り/研究", new[] { 0x2A1D }),
            CreateControllerAction(KeybindModeGroup.Fishing, "釣り餌切替", new[] { 0x2A5A }),
            CreateControllerAction(KeybindModeGroup.Fishing, "竿切替", new[] { 0x2A97 }),
            CreateControllerAction(KeybindModeGroup.Fishing, "モード/ガイド", new[] { 0x2AD4 }),
            CreateControllerAction(KeybindModeGroup.Fishing, "設定", new[] { 0x2B11 }),
            CreateControllerAction(KeybindModeGroup.Fishing, "メニューを閉じる", new[] { 0x2C3F }),
        };

    public static readonly IReadOnlyList<KeyMouseActionDefinition> KeyMouseMainActions =
        new KeyMouseActionDefinition[]
        {
            CreateKeyMouseAction(KeybindModeGroup.Main, "移動-前", new[] { 0x59, 0x12BD }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "移動-後", new[] { 0x6E, 0x12D2 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "移動-左", new[] { 0x83, 0x12E7 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "移動-右", new[] { 0x98, 0x12FC }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "歩く/走る切替", new[] { 0x156, 0x1371 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ジャンプ", new[] { 0x119, 0x134E }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ダッシュ/回避1", new[] { 0x193, 0x13AE }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ダッシュ/回避2", new[] { 0x1AD }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "環境共鳴能力1", new[] { 0x1EA, 0x13D1 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "環境共鳴能力2", new[] { 0x241 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "通常攻撃", new[] { 0x264 }, allowedInputTypes: new[] { 0x2u }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "特殊攻撃", new[] { 0x9CD, 0x1934 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "マスタリースキル1", new[] { 0x2BB, 0x143A }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "マスタリースキル2", new[] { 0x2F8, 0x145D }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "マスタリースキル3", new[] { 0x335, 0x1480 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "マスタリースキル4", new[] { 0x372, 0x14A3 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "究極スキル", new[] { 0x990, 0x1911 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "バトルイマジン1", new[] { 0xA0A, 0x1957 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "バトルイマジン2", new[] { 0xA47, 0x197A }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "左でアイテム切り替え", new[] { 0x1013, 0x1E28 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "アイテム使用", new[] { 0x3AF, 0x14C6 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "右でアイテム切り替え", new[] { 0x1050, 0x1E4B }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "アクション", new[] { 0x537, 0x1598 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ロックオン/切り替え1", new[] { 0x3EC, 0x14E9 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ロックオン/切り替え2", new[] { 0x420 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "エクストラスキル", new[] { 0xA84, 0x199D }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "インタラクト解除", new[] { 0x443, 0x150C }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クエスト追跡", new[] { 0x4FA, 0x1575 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "UI非表示", new[] { 0x4BD, 0x1552 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クエストアイテムのクイック使用", new[] { 0x480, 0x152F }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "おすすめイベント", new[] { 0xB07, 0x1A06 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "マップON/OFF", new[] { 0x676, 0x1679 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クエスト", new[] { 0x6B3, 0x169C }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ソーシャルモード", new[] { 0x6F0, 0x16BF }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "トーク", new[] { 0xC41, 0x1B0C }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "キャラクター", new[] { 0x72D, 0x16E2 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ギルド", new[] { 0xE70, 0x1CD3 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "チャット画面チャンネル切り替え-上", new[] { 0x108D }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "チャット画面チャンネル切り替え-下", new[] { 0x10B0 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "チャット入力チャンネル切り替え-左", new[] { 0x10D3 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "チャット入力チャンネル切り替え-右", new[] { 0x10F6 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "メニューを開く", new[] { 0x833 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "メニューを閉じる", new[] { 0x17B4 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "マウス呼出し", new[] { 0x870, 0x17F1 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "撮影", new[] { 0x750, 0x1705 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "所持品", new[] { 0x78D, 0x1728 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "パーティ", new[] { 0x7B0, 0x174B }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "シーズンセンター", new[] { 0x7D3, 0x176E }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ダンジョン退出", new[] { 0x7F6, 0x1791 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "アビリティ", new[] { 0x8D0, 0x1851 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "アイテムを使用", new[] { 0x8F3, 0x1874 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クイック操作", new[] { 0xB8A, 0x1A6F }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "乗り物召喚/解除", new[] { 0xB2A, 0x1A29 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "パーティボイス切り替え", new[] { 0xB67, 0x1A4C }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "招待承認", new[] { 0xBC7, 0x1A92 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "招待拒否", new[] { 0xC04, 0x1ACF }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "オートバトル", new[] { 0xCA1, 0x1B52 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "チャンネル", new[] { 0xC64, 0x1B2F }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "イラストガイド", new[] { 0xCDE, 0x1B75 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クイックホイール", new[] { 0xD1B, 0x1B98 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "オートラン", new[] { 0xF39, 0x1D9C }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クエスト切り替え（左）", new[] { 0xF99, 0x1DE2 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クエスト切り替え（右）", new[] { 0xFF0 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ホーム編集", new[] { 0xF16, 0x1D79 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ズームアウト/ズームイン", new[] { 0x574, 0x893, 0x15D5, 0x1814 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "スキルパレットを開く", new[] { 0x120D, 0x1F86 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ロールスキル1", new[] { 0x1119 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ロールスキル2", new[] { 0x1156 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ロールスキル3", new[] { 0x1193 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ロールスキル4", new[] { 0x11D0 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ホーム設計図", new[] { 0x1264, 0x1FDD }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "アテンドイマジンを召喚する", new[] { 0x1287, 0x2000 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クイックホイール-スロット1", new[] { 0xD58, 0x1BBB }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クイックホイール-スロット2", new[] { 0xD7B, 0x1BDE }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クイックホイール-スロット3", new[] { 0xD9E, 0x1C01 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クイックホイール-スロット4", new[] { 0xDC1, 0x1C24 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クイックホイール-スロット5", new[] { 0xDE4, 0x1C47 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クイックホイール-スロット6", new[] { 0xE07, 0x1C6A }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クイックホイール-スロット7", new[] { 0xE2A, 0x1C8D }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "クイックホイール-スロット8", new[] { 0xE4D, 0x1CB0 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "スキル", new[] { 0xE93, 0x1CF6 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "装備", new[] { 0xEB6, 0x1D19 }),
        };
    public static readonly IReadOnlyList<KeyMouseActionDefinition> KeyMouseQuickWheelActions =
        new KeyMouseActionDefinition[]
        {
            CreateKeyMouseAction(KeybindModeGroup.QuickWheel, "クイックホイール切替", new[] { 0x2882 }),
            CreateKeyMouseAction(KeybindModeGroup.QuickWheel, "クイックホイール編集", new[] { 0x28D4 }),
            CreateKeyMouseAction(KeybindModeGroup.QuickWheel, "クイックホイールを閉じる", new[] { 0x2911 }),
        };
    public static readonly IReadOnlyList<KeyMouseActionDefinition> KeyMousePhotoActions =
        new KeyMouseActionDefinition[]
        {
            CreateKeyMouseAction(KeybindModeGroup.Photo, "カメラ移動-上", new[] { 0x22B5 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "カメラ移動-下", new[] { 0x22CA }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "カメラ移動-左", new[] { 0x22DF }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "カメラ移動-右", new[] { 0x22F4 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "カメラパン-前", new[] { 0x2346 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "カメラパン-後", new[] { 0x235B }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "カメラパン-左", new[] { 0x2370 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "カメラパン-右", new[] { 0x2385 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "ズームアウト", new[] { 0x20BD }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "ズームイン", new[] { 0x20D2 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "画面を非表示にする", new[] { 0x219E }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "撮影", new[] { 0x2124 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "設定メニュー", new[] { 0x266E }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "参加者メニュー", new[] { 0x26AB }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "移動-前", new[] { 0x23D7 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "移動-後", new[] { 0x23EC }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "移動-左", new[] { 0x2401 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "移動-右", new[] { 0x2416 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "ジャンプ", new[] { 0x2043 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "ダッシュ/回避", new[] { 0x2161 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "歩く/走る切替", new[] { 0x26E8 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "特殊攻撃", new[] { 0x2497 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "マスタリースキル1", new[] { 0x24D4 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "マスタリースキル2", new[] { 0x2511 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "マスタリースキル3", new[] { 0x254E }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "マスタリースキル4", new[] { 0x258B }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "究極スキル", new[] { 0x2080 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "乗り物召喚/解除", new[] { 0x25C8 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "終了", new[] { 0x2605 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "メニューを閉じる", new[] { 0x2255 }),
        };
    public static readonly IReadOnlyList<KeyMouseActionDefinition> KeyMouseFishingActions =
        new KeyMouseActionDefinition[]
        {
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "キャスト/竿を引く", new[] { 0x2989 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "竿移動-前", new[] { 0x2B57 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "竿移動-後", new[] { 0x2B6C }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "竿移動-左", new[] { 0x2B81 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "竿移動-右", new[] { 0x2B96 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "釣り/図鑑", new[] { 0x29C6 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "釣り/研究", new[] { 0x2A03 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "釣り餌切替", new[] { 0x2A40 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "竿切替", new[] { 0x2A7D }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "モード/ガイド", new[] { 0x2ABA }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "設定", new[] { 0x2AF7 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "マウス呼出し", new[] { 0x2B34 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "メニューを閉じる", new[] { 0x2C25 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "トーク", new[] { 0x2C62 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "チャット画面チャンネル切り替え-下", new[] { 0x2C85 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "チャット画面チャンネル切り替え-上", new[] { 0x2CA8 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "チャット入力チャンネル切り替え-左", new[] { 0x2CCB }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "チャット入力チャンネル切り替え-右", new[] { 0x2CEE }),
        };

    public static readonly IReadOnlyList<ControllerActionDefinition> ControllerActions =
        ControllerMainActions
            .Concat(ControllerQuickWheelActions)
            .Concat(ControllerPhotoActions)
            .Concat(ControllerFishingActions)
            .ToArray();

    public static readonly IReadOnlyList<KeyMouseActionDefinition> KeyMouseActions =
        KeyMouseMainActions
            .Concat(KeyMouseQuickWheelActions)
            .Concat(KeyMousePhotoActions)
            .Concat(KeyMouseFishingActions)
            .ToArray();

    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>> ControllerDisplayMaps =
        new ReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>>(
            new Dictionary<string, IReadOnlyDictionary<uint, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["PlayStation"] = new ReadOnlyDictionary<uint, string>(new Dictionary<uint, string>
                {
                    [1u] = "L前後入力", [2u] = "L左右入力", [3u] = "R前後入力", [4u] = "R左右入力",
                    [5u] = "L2", [6u] = "R2", [7u] = "×", [8u] = "〇", [10u] = "□", [11u] = "△",
                    [13u] = "touchpad", [14u] = "option", [15u] = "share", [17u] = "L1", [18u] = "R1",
                    [19u] = "L3", [20u] = "R3", [23u] = "↑", [24u] = "↓", [25u] = "←", [26u] = "→"
                }),
                ["Nintendo"] = new ReadOnlyDictionary<uint, string>(new Dictionary<uint, string>
                {
                    [1u] = "L前後入力", [2u] = "L左右入力", [3u] = "R前後入力", [4u] = "R左右入力",
                    [5u] = "ZL", [6u] = "ZR", [7u] = "B", [8u] = "A", [10u] = "Y", [11u] = "X",
                    [13u] = "-", [14u] = "+", [15u] = "capture", [17u] = "L", [18u] = "R",
                    [19u] = "LS", [20u] = "RS", [23u] = "↑", [24u] = "↓", [25u] = "←", [26u] = "→"
                }),
                ["Xbox"] = new ReadOnlyDictionary<uint, string>(new Dictionary<uint, string>
                {
                    [1u] = "L前後入力", [2u] = "L左右入力", [3u] = "R前後入力", [4u] = "R左右入力",
                    [5u] = "LT", [6u] = "RT", [7u] = "A", [8u] = "B", [10u] = "X", [11u] = "Y",
                    [13u] = "view", [14u] = "menu", [15u] = "xbox", [17u] = "LB", [18u] = "RB",
                    [19u] = "LS", [20u] = "RS", [23u] = "↑", [24u] = "↓", [25u] = "←", [26u] = "→"
                })
            });

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<PresetOption>> PresetOptions =
        new ReadOnlyDictionary<string, IReadOnlyList<PresetOption>>(
            new Dictionary<string, IReadOnlyList<PresetOption>>(StringComparer.OrdinalIgnoreCase)
            {
                ["PlayStation"] = new[] { new PresetOption(0x1u, "□ / ×"), new PresetOption(0x2u, "× / 〇"), new PresetOption(0x3u, "〇 / ×") },
                ["Nintendo"] = new[] { new PresetOption(0x1u, "Y / B"), new PresetOption(0x2u, "B / A"), new PresetOption(0x3u, "A / B") },
                ["Xbox"] = new[] { new PresetOption(0x1u, "X / A"), new PresetOption(0x2u, "A / B"), new PresetOption(0x3u, "B / A") }
            });

    public static readonly IReadOnlyDictionary<string, int[]> ControllerOffsetAliases =
        new ReadOnlyDictionary<string, int[]>(
            new Dictionary<string, int[]>(StringComparer.Ordinal)
            {
                [GetActionId(KeybindModeGroup.Main, "環境共鳴能力2")] = new[] { 0x241 },
                [GetActionId(KeybindModeGroup.Main, "クエスト切り替え（右）")] = new[] { 0xFF0 },
                [GetActionId(KeybindModeGroup.Main, "ホーム設計図")] = new[] { 0x1264 }
            });

    public static readonly IReadOnlyDictionary<string, int[]> KeyMouseOffsetAliases =
        new ReadOnlyDictionary<string, int[]>(
            new Dictionary<string, int[]>(StringComparer.Ordinal)
            {
                [GetActionId(KeybindModeGroup.Main, "環境共鳴能力2")] = new[] { 0x227 },
                [GetActionId(KeybindModeGroup.Main, "クエスト切り替え（右）")] = new[] { 0xFD6 },
                [GetActionId(KeybindModeGroup.Main, "ホーム設計図")] = new[] { 0x124A }
            });

    public static readonly IReadOnlyDictionary<uint, uint> HelperMainToActionValue =
        new ReadOnlyDictionary<uint, uint>(
            new Dictionary<uint, uint>
            {
                [0x01u] = 17u,
                [0x02u] = 18u,
                [0x04u] = 5u,
                [0x08u] = 6u
            });

    private static readonly HashSet<string> KeyMouseLControlPrefixActionIds = new(StringComparer.Ordinal)
    {
        GetActionId(KeybindModeGroup.Main, "UI非表示"),
        GetActionId(KeybindModeGroup.Main, "パーティボイス切り替え"),
        GetActionId(KeybindModeGroup.Main, "ロールスキル1"),
        GetActionId(KeybindModeGroup.Main, "ロールスキル2"),
        GetActionId(KeybindModeGroup.Main, "ロールスキル3"),
        GetActionId(KeybindModeGroup.Main, "ロールスキル4")
    };

    public static readonly IReadOnlyDictionary<string, string> ControllerQuickWheelLinks =
        CreateDirectLink(KeybindModeGroup.QuickWheel, "クイックホイールを閉じる", KeybindModeGroup.Main, "クイックホイール");

    public static readonly IReadOnlyDictionary<string, string> KeyMouseQuickWheelLinks =
        CreateDirectLink(KeybindModeGroup.QuickWheel, "クイックホイールを閉じる", KeybindModeGroup.Main, "クイックホイール");

    public static readonly IReadOnlyDictionary<string, string> ControllerPhotoModeLinks =
        BuildModeLinks(
            ControllerMainActions,
            ControllerPhotoActions,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal) { "撮影", "メニューを閉じる" });

    public static readonly IReadOnlyDictionary<string, string> ControllerFishingModeLinks =
        BuildModeLinks(
            ControllerMainActions,
            ControllerFishingActions,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal) { "メニューを閉じる" });

    public static readonly IReadOnlyDictionary<string, string> KeyMousePhotoModeLinks =
        BuildModeLinks(
            KeyMouseMainActions,
            KeyMousePhotoActions,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["カメラ-前"] = "カメラパン-前",
                ["カメラ-後"] = "カメラパン-後",
                ["カメラ-左"] = "カメラパン-左",
                ["カメラ-右"] = "カメラパン-右",
                ["ダッシュ/回避1"] = "ダッシュ/回避",
                ["撮影"] = "終了"
            },
            new HashSet<string>(StringComparer.Ordinal) { "撮影" });

    public static readonly IReadOnlyDictionary<string, string> KeyMouseFishingModeLinks =
        BuildModeLinks(
            KeyMouseMainActions,
            KeyMouseFishingActions,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));

    public static string GetActionId(KeybindModeGroup group, string name)
    {
        var prefix = group switch
        {
            KeybindModeGroup.Main => "main",
            KeybindModeGroup.QuickWheel => "quick_wheel",
            KeybindModeGroup.Photo => "photo",
            KeybindModeGroup.Fishing => "fishing",
            _ => throw new ArgumentOutOfRangeException(nameof(group))
        };

        return $"{prefix}:{name}";
    }

    public static string GetLegacyLayoutActionKey(IKeybindActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return definition.Group switch
        {
            KeybindModeGroup.Photo when definition.Name == "終了" => "撮影モード撮影モード終了",
            KeybindModeGroup.Photo => $"撮影モード{definition.Name}",
            KeybindModeGroup.Fishing => $"釣りモード{definition.Name}",
            _ => definition.Name
        };
    }

    public static IReadOnlyList<ControllerInputOption> GetControllerOptions(string? controllerType)
    {
        var labels = GetControllerDisplayMap(controllerType);
        return ControllerInputOptions
            .Where(option => labels.ContainsKey(option.Value))
            .Select(option => new ControllerInputOption(option.Value, labels[option.Value]))
            .ToArray();
    }

    public static IReadOnlyList<HelperBindingOption> GetHelperOptions(string? controllerType)
    {
        var labels = GetControllerDisplayMap(controllerType);
        return new[]
        {
            new HelperBindingOption(0x01u, labels[17u]),
            new HelperBindingOption(0x02u, labels[18u]),
            new HelperBindingOption(0x04u, labels[5u]),
            new HelperBindingOption(0x08u, labels[6u])
        };
    }

    public static IReadOnlyList<PresetOption> GetPresetOptions(string? controllerType)
    {
        return PresetOptions.TryGetValue(controllerType ?? string.Empty, out var options)
            ? options
            : PresetOptions[DefaultControllerType];
    }

    public static IReadOnlyList<ControllerInputOption> GetAllowedControllerOptions(
        ControllerActionDefinition definition,
        string? controllerType,
        ISet<uint>? blockedValues)
    {
        var allowedValues = definition.AllowedValues.Count > 0
            ? definition.AllowedValues
            : ControllerInputOptions.Select(option => option.Value).ToArray();

        var labels = GetControllerDisplayMap(controllerType);
        return allowedValues
            .Where(labels.ContainsKey)
            .Where(value => definition.HasExplicitAllowedValues
                || !definition.UsesHelper
                || blockedValues is null
                || !blockedValues.Contains(value))
            .Select(value => new ControllerInputOption(value, labels[value]))
            .ToArray();
    }

    public static IReadOnlyList<KeyMouseInputOption> GetAllowedKeyMouseOptions(KeyMouseActionDefinition definition)
    {
        if (definition.AllowedInputTypes.Count == 0)
        {
            return KeyMouseInputOptions;
        }

        var allowedTypes = new HashSet<uint>(definition.AllowedInputTypes);
        return KeyMouseInputOptions
            .Where(option => allowedTypes.Contains(option.InputType))
            .ToArray();
    }

    public static bool UsesLControlPrefix(KeyMouseActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return KeyMouseLControlPrefixActionIds.Contains(definition.Id);
    }

    public static IReadOnlyDictionary<uint, string> GetControllerDisplayMap(string? controllerType)
    {
        return ControllerDisplayMaps.TryGetValue(controllerType ?? string.Empty, out var map)
            ? map
            : ControllerDisplayMaps[DefaultControllerType];
    }

    public static IReadOnlyList<ControllerActionDefinition> GetControllerActions(KeybindModeGroup group)
    {
        return group switch
        {
            KeybindModeGroup.Main => ControllerMainActions,
            KeybindModeGroup.QuickWheel => ControllerQuickWheelActions,
            KeybindModeGroup.Photo => ControllerPhotoActions,
            KeybindModeGroup.Fishing => ControllerFishingActions,
            _ => throw new ArgumentOutOfRangeException(nameof(group))
        };
    }

    public static IReadOnlyList<KeyMouseActionDefinition> GetKeyMouseActions(KeybindModeGroup group)
    {
        return group switch
        {
            KeybindModeGroup.Main => KeyMouseMainActions,
            KeybindModeGroup.QuickWheel => KeyMouseQuickWheelActions,
            KeybindModeGroup.Photo => KeyMousePhotoActions,
            KeybindModeGroup.Fishing => KeyMouseFishingActions,
            _ => throw new ArgumentOutOfRangeException(nameof(group))
        };
    }

    private static ControllerActionDefinition CreateControllerAction(
        KeybindModeGroup group,
        string name,
        IReadOnlyList<int> relativeOffsets,
        IReadOnlyList<uint>? allowedValues = null,
        bool usesHelper = true)
    {
        return new ControllerActionDefinition(
            GetActionId(group, name),
            group,
            name,
            relativeOffsets,
            allowedValues ?? Array.Empty<uint>(),
            allowedValues is not null,
            usesHelper);
    }

    private static KeyMouseActionDefinition CreateKeyMouseAction(
        KeybindModeGroup group,
        string name,
        IReadOnlyList<int> relativeOffsets,
        IReadOnlyList<uint>? allowedInputTypes = null)
    {
        return new KeyMouseActionDefinition(
            GetActionId(group, name),
            group,
            name,
            relativeOffsets,
            allowedInputTypes ?? Array.Empty<uint>());
    }

    private static IReadOnlyDictionary<string, string> CreateDirectLink(
        KeybindModeGroup modeGroup,
        string modeName,
        KeybindModeGroup sourceGroup,
        string sourceName)
    {
        return new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GetActionId(modeGroup, modeName)] = GetActionId(sourceGroup, sourceName)
            });
    }

    private static IReadOnlyDictionary<string, string> BuildModeLinks<TDefinition>(
        IReadOnlyList<TDefinition> normalActions,
        IReadOnlyList<TDefinition> modeActions,
        IReadOnlyDictionary<string, string> exceptionNormalToMode,
        ISet<string> unlinkedModeNames)
        where TDefinition : IKeybindActionDefinition
    {
        var normalIdsByName = normalActions.ToDictionary(
            action => action.Name,
            action => action.Id,
            StringComparer.Ordinal);
        var modeIdsByName = modeActions.ToDictionary(
            action => action.Name,
            action => action.Id,
            StringComparer.Ordinal);

        var links = new Dictionary<string, string>(StringComparer.Ordinal);
        var exceptionModeNames = exceptionNormalToMode.Values.ToHashSet(StringComparer.Ordinal);

        foreach (var (modeName, modeId) in modeIdsByName)
        {
            if (unlinkedModeNames.Contains(modeName) || exceptionModeNames.Contains(modeName))
            {
                continue;
            }

            if (normalIdsByName.TryGetValue(modeName, out var sourceId))
            {
                links[modeId] = sourceId;
            }
        }

        foreach (var (normalName, modeName) in exceptionNormalToMode)
        {
            if (normalIdsByName.TryGetValue(normalName, out var sourceId)
                && modeIdsByName.TryGetValue(modeName, out var modeId))
            {
                links[modeId] = sourceId;
            }
        }

        return new ReadOnlyDictionary<string, string>(links);
    }
}
