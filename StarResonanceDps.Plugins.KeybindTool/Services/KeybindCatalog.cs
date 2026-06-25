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

    public static readonly IReadOnlyList<ControllerActionDefinition> ControllerActions =
        new ControllerActionDefinition[]
        {
        new ControllerActionDefinition("移動-前後", new[] { 0xB2 }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("移動-左右", new[] { 0xC7 }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("カメラ-前後", new[] { 0x60F }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("カメラ-左右", new[] { 0x624 }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("ジャンプ", new[] { 0x133 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("ダッシュ/回避", new[] { 0x1C7 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("環境共鳴能力1", new[] { 0x204 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("環境共鳴能力2", new[] { 0x227 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("通常攻撃", new[] { 0x27E }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("特殊攻撃", new[] { 0x9E7 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("マスタリースキル1", new[] { 0x2D5 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("マスタリースキル2", new[] { 0x312 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("マスタリースキル3", new[] { 0x34F }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("マスタリースキル4", new[] { 0x38C }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("究極スキル", new[] { 0x9AA }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("バトルイマジン1", new[] { 0xA24 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("バトルイマジン2", new[] { 0xA61 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("左でアイテム切り替え", new[] { 0x102D }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("アイテム使用", new[] { 0x3C9 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("右でアイテム切り替え", new[] { 0x106A }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("アクション", new[] { 0x551, 0x158F }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("ロックオン/切り替え", new[] { 0x406 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("エクストラスキル", new[] { 0xA9E }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("インタラクト解除", new[] { 0x45D }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("クエスト追跡", new[] { 0x514 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("UI非表示", new[] { 0x4D7 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("クエストアイテムのクイック使用", new[] { 0x49A }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("マップON/OFF", new[] { 0x690 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("クエスト", new[] { 0x6CD }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("ソーシャルモード", new[] { 0x70A }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("メニューを開く", new[] { 0x84D }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("メニューを閉じる", new[] { 0x17CE }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("カーソル移動-上下", new[] { 0x1F40 }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("カーソル移動-左右", new[] { 0x1F63 }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("撮影", new[] { 0x76A }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("ダンジョン退出", new[] { 0x810 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("アイテムを使用", new[] { 0x90D, 0x186B }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("クイック操作", new[] { 0xBA4 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("乗り物召喚/解除", new[] { 0xB44 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("招待承認", new[] { 0xBE1, 0x1A89 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("招待拒否", new[] { 0xC1E, 0x1AC6 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("オートバトル", new[] { 0xCBB }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("チャンネル", new[] { 0xC7E }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("イラストガイド", new[] { 0xCF8 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("クイックホイール", new[] { 0xD35 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("クイックホイール切替（左）", new[] { 0x289C }, Array.Empty<uint>(), false, false),
        new ControllerActionDefinition("クイックホイール切替（右）", new[] { 0x28B1 }, Array.Empty<uint>(), false, false),
        new ControllerActionDefinition("クイックホイール編集", new[] { 0x28EE }, Array.Empty<uint>(), false, false),
        new ControllerActionDefinition("クエスト切り替え（左）", new[] { 0xFB3 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("クエスト切り替え（右）", new[] { 0xFD6 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("ズームアウト", new[] { 0x58E }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("ズームイン", new[] { 0x5A3 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("スキルパレットを開く", new[] { 0x1227, 0x1F7D }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("ロールスキル1", new[] { 0x1133 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("ロールスキル2", new[] { 0x1170 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("ロールスキル3", new[] { 0x11AD }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("ロールスキル4", new[] { 0x11EA }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("ホーム設計図", new[] { 0x124A }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モードカメラ移動-上下", new[] { 0x230E }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("撮影モードカメラ移動-左右", new[] { 0x2323 }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("撮影モードカメラパン-前後", new[] { 0x239F }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("撮影モードカメラパン-左右", new[] { 0x23B4 }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("撮影モード画面を非表示にする", new[] { 0x21B8 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モード撮影", new[] { 0x213E }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モード設定メニュー", new[] { 0x2688 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モード参加者メニュー", new[] { 0x26C5 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モードカーソル呼出し", new[] { 0x27B8 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モードメニューを閉じる", new[] { 0x226F }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モード移動-前後", new[] { 0x2430 }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("撮影モード移動-左右", new[] { 0x2445 }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("撮影モードカメラ-前後", new[] { 0x273A }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("撮影モードカメラ-左右", new[] { 0x274F }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("撮影モードジャンプ", new[] { 0x205D }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モードダッシュ/回避", new[] { 0x217B }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モード特殊攻撃", new[] { 0x24B1 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モードマスタリースキル1", new[] { 0x24EE }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モードマスタリースキル2", new[] { 0x252B }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モードマスタリースキル3", new[] { 0x2568 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モードマスタリースキル4", new[] { 0x25A5 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モード究極スキル", new[] { 0x209A }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モード乗り物召喚/解除", new[] { 0x25E2 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モードカーソル移動-上下", new[] { 0x27FE }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("撮影モードカーソル移動-左右", new[] { 0x2821 }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("撮影モードズームアウト", new[] { 0x20EC }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("撮影モードズームイン", new[] { 0x2101 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("釣りモード竿移動-前後", new[] { 0x2BB0 }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("釣りモード竿移動-左右", new[] { 0x2BC5 }, new uint[] { 0x1u, 0x2u, 0x3u, 0x4u }, true, true),
        new ControllerActionDefinition("釣りモードキャスト/竿を引く", new[] { 0x29A3 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("釣りモード釣り/図鑑", new[] { 0x29E0 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("釣りモード釣り/研究", new[] { 0x2A1D }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("釣りモード釣り餌切替", new[] { 0x2A5A }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("釣りモード竿切替", new[] { 0x2A97 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("釣りモードモード/ガイド", new[] { 0x2AD4 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("釣りモード設定", new[] { 0x2B11 }, Array.Empty<uint>(), false, true),
        new ControllerActionDefinition("釣りモードメニューを閉じる", new[] { 0x2C3F }, Array.Empty<uint>(), false, true),
        };

    public static readonly IReadOnlyList<KeyMouseActionDefinition> KeyMouseActions =
        new KeyMouseActionDefinition[]
        {
        new KeyMouseActionDefinition("移動-前", new[] { 0x59, 0x12BD }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("移動-後", new[] { 0x6E, 0x12D2 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("移動-左", new[] { 0x83, 0x12E7 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("移動-右", new[] { 0x98, 0x12FC }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("歩く/走る切替", new[] { 0x156, 0x1371 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ジャンプ", new[] { 0x119, 0x134E }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ダッシュ/回避1", new[] { 0x193, 0x13AE }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ダッシュ/回避2", new[] { 0x1AD }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("環境共鳴能力1", new[] { 0x1EA, 0x13D1 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("環境共鳴能力2", new[] { 0x241 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("通常攻撃", new[] { 0x264 }, new uint[] { 0x2u }),
        new KeyMouseActionDefinition("特殊攻撃", new[] { 0x9CD, 0x1934 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("マスタリースキル1", new[] { 0x2BB, 0x143A }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("マスタリースキル2", new[] { 0x2F8, 0x145D }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("マスタリースキル3", new[] { 0x335, 0x1480 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("マスタリースキル4", new[] { 0x372, 0x14A3 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("究極スキル", new[] { 0x990, 0x1911 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("バトルイマジン1", new[] { 0xA0A, 0x1957 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("バトルイマジン2", new[] { 0xA47, 0x197A }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("左でアイテム切り替え", new[] { 0x1013, 0x1E28 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("アイテム使用", new[] { 0x3AF, 0x14C6 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("右でアイテム切り替え", new[] { 0x1050, 0x1E4B }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("アクション", new[] { 0x537, 0x1598 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ロックオン/切り替え1", new[] { 0x3EC, 0x14E9 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ロックオン/切り替え2", new[] { 0x420 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("エクストラスキル", new[] { 0xA84, 0x199D }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("インタラクト解除", new[] { 0x443, 0x150C }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クエスト追跡", new[] { 0x4FA, 0x1575 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("UI非表示", new[] { 0x4BD, 0x1552 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クエストアイテムのクイック使用", new[] { 0x480, 0x152F }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("おすすめイベント", new[] { 0xB07, 0x1A06 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("マップON/OFF", new[] { 0x676, 0x1679 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クエスト", new[] { 0x6B3, 0x169C }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ソーシャルモード", new[] { 0x6F0, 0x16BF }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("トーク", new[] { 0xC41, 0x1B0C }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("キャラクター", new[] { 0x72D, 0x16E2 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ギルド", new[] { 0xE70, 0x1CD3 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("チャット画面チャンネル切り替え-上", new[] { 0x108D }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("チャット画面チャンネル切り替え-下", new[] { 0x10B0 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("チャット入力チャンネル切り替え-左", new[] { 0x10D3 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("チャット入力チャンネル切り替え-右", new[] { 0x10F6 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("メニューを開く", new[] { 0x833 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("メニューを閉じる", new[] { 0x17B4 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("マウス呼出し", new[] { 0x870, 0x17F1 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影", new[] { 0x750, 0x1705 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("所持品", new[] { 0x78D, 0x1728 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("パーティ", new[] { 0x7B0, 0x174B }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("シーズンセンター", new[] { 0x7D3, 0x176E }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ダンジョン退出", new[] { 0x7F6, 0x1791 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("アビリティ", new[] { 0x8D0, 0x1851 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("アイテムを使用", new[] { 0x8F3, 0x1874 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クイック操作", new[] { 0xB8A, 0x1A6F }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("乗り物召喚/解除", new[] { 0xB2A, 0x1A29 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("パーティボイス切り替え", new[] { 0xB67, 0x1A4C }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("招待承認", new[] { 0xBC7, 0x1A92 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("招待拒否", new[] { 0xC04, 0x1ACF }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("オートバトル", new[] { 0xCA1, 0x1B52 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("チャンネル", new[] { 0xC64, 0x1B2F }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("イラストガイド", new[] { 0xCDE, 0x1B75 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クイックホイール", new[] { 0xD1B, 0x1B98 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クイックホイール切替", new[] { 0x2882 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クイックホイール編集", new[] { 0x28D4 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("オートラン", new[] { 0xF39, 0x1D9C }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クエスト切り替え（左）", new[] { 0xF99, 0x1DE2 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クエスト切り替え（右）", new[] { 0xFF0 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ホーム編集", new[] { 0xF16, 0x1D79 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ズームアウト/ズームイン", new[] { 0x574, 0x893, 0x15D5, 0x1814 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("スキルパレットを開く", new[] { 0x120D, 0x1F86 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ロールスキル1", new[] { 0x1119 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ロールスキル2", new[] { 0x1156 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ロールスキル3", new[] { 0x1193 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ロールスキル4", new[] { 0x11D0 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("ホーム設計図", new[] { 0x1264, 0x1FDD }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("アテンドイマジンを召喚する", new[] { 0x1287, 0x2000 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クイックホイール-スロット1", new[] { 0xD58, 0x1BBB }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クイックホイール-スロット2", new[] { 0xD7B, 0x1BDE }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クイックホイール-スロット3", new[] { 0xD9E, 0x1C01 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クイックホイール-スロット4", new[] { 0xDC1, 0x1C24 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クイックホイール-スロット5", new[] { 0xDE4, 0x1C47 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クイックホイール-スロット6", new[] { 0xE07, 0x1C6A }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クイックホイール-スロット7", new[] { 0xE2A, 0x1C8D }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("クイックホイール-スロット8", new[] { 0xE4D, 0x1CB0 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("スキル", new[] { 0xE93, 0x1CF6 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("装備", new[] { 0xEB6, 0x1D19 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードカメラ移動-上", new[] { 0x22B5 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードカメラ移動-下", new[] { 0x22CA }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードカメラ移動-左", new[] { 0x22DF }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードカメラ移動-右", new[] { 0x22F4 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードカメラパン-前", new[] { 0x2346 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードカメラパン-後", new[] { 0x235B }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードカメラパン-左", new[] { 0x2370 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードカメラパン-右", new[] { 0x2385 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードズームアウト", new[] { 0x20BD }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードズームイン", new[] { 0x20D2 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モード画面を非表示にする", new[] { 0x219E }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モード撮影", new[] { 0x2124 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モード設定メニュー", new[] { 0x266E }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モード参加者メニュー", new[] { 0x26AB }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モード移動-前", new[] { 0x23D7 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モード移動-後", new[] { 0x23EC }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モード移動-左", new[] { 0x2401 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モード移動-右", new[] { 0x2416 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードジャンプ", new[] { 0x2043 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードダッシュ/回避", new[] { 0x2161 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モード歩く/走る切替", new[] { 0x26E8 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モード特殊攻撃", new[] { 0x2497 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードマスタリースキル1", new[] { 0x24D4 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードマスタリースキル2", new[] { 0x2511 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードマスタリースキル3", new[] { 0x254E }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードマスタリースキル4", new[] { 0x258B }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モード究極スキル", new[] { 0x2080 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モード乗り物召喚/解除", new[] { 0x25C8 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モード撮影モード終了", new[] { 0x2605 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("撮影モードメニューを閉じる", new[] { 0x2255 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("釣りモードキャスト/竿を引く", new[] { 0x2989 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("釣りモード竿移動-前", new[] { 0x2B57 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("釣りモード竿移動-後", new[] { 0x2B6C }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("釣りモード竿移動-左", new[] { 0x2B81 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("釣りモード竿移動-右", new[] { 0x2B96 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("釣りモード釣り/図鑑", new[] { 0x29C6 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("釣りモード釣り/研究", new[] { 0x2A03 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("釣りモード釣り餌切替", new[] { 0x2A40 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("釣りモード竿切替", new[] { 0x2A7D }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("釣りモードモード/ガイド", new[] { 0x2ABA }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("釣りモード設定", new[] { 0x2AF7 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("釣りモードマウス呼出し", new[] { 0x2B34 }, Array.Empty<uint>()),
        new KeyMouseActionDefinition("釣りモードメニューを閉じる", new[] { 0x2C25 }, Array.Empty<uint>()),
        };

    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>> ControllerDisplayMaps =
        new ReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>>(
            new Dictionary<string, IReadOnlyDictionary<uint, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["PlayStation"] = new ReadOnlyDictionary<uint, string>(new Dictionary<uint, string> { [0x1u] = "L前後入力", [0x2u] = "L左右入力", [0x3u] = "R前後入力", [0x4u] = "R左右入力", [0x5u] = "L2", [0x6u] = "R2", [0x7u] = "×", [0x8u] = "〇", [0xAu] = "□", [0xBu] = "△", [0xDu] = "touchpad", [0xEu] = "option", [0xFu] = "share", [0x11u] = "L1", [0x12u] = "R1", [0x13u] = "L3", [0x14u] = "R3", [0x17u] = "↑", [0x18u] = "↓", [0x19u] = "←", [0x1Au] = "→" }),
                ["Nintendo"] = new ReadOnlyDictionary<uint, string>(new Dictionary<uint, string> { [0x1u] = "L前後入力", [0x2u] = "L左右入力", [0x3u] = "R前後入力", [0x4u] = "R左右入力", [0x5u] = "ZL", [0x6u] = "ZR", [0x7u] = "B", [0x8u] = "A", [0xAu] = "Y", [0xBu] = "X", [0xDu] = "-", [0xEu] = "+", [0xFu] = "capture", [0x11u] = "L", [0x12u] = "R", [0x13u] = "LS", [0x14u] = "RS", [0x17u] = "↑", [0x18u] = "↓", [0x19u] = "←", [0x1Au] = "→" }),
                ["Xbox"] = new ReadOnlyDictionary<uint, string>(new Dictionary<uint, string> { [0x1u] = "L前後入力", [0x2u] = "L左右入力", [0x3u] = "R前後入力", [0x4u] = "R左右入力", [0x5u] = "LT", [0x6u] = "RT", [0x7u] = "A", [0x8u] = "B", [0xAu] = "X", [0xBu] = "Y", [0xDu] = "view", [0xEu] = "menu", [0xFu] = "xbox", [0x11u] = "LB", [0x12u] = "RB", [0x13u] = "LS", [0x14u] = "RS", [0x17u] = "↑", [0x18u] = "↓", [0x19u] = "←", [0x1Au] = "→" }),
            });

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<PresetOption>> PresetOptions =
        new ReadOnlyDictionary<string, IReadOnlyList<PresetOption>>(
            new Dictionary<string, IReadOnlyList<PresetOption>>(StringComparer.OrdinalIgnoreCase)
            {
                ["PlayStation"] = new[] { new PresetOption(0x1u, "□ / ×"), new PresetOption(0x2u, "× / 〇"), new PresetOption(0x3u, "〇 / ×") },
                ["Nintendo"] = new[] { new PresetOption(0x1u, "Y / B"), new PresetOption(0x2u, "B / A"), new PresetOption(0x3u, "A / B") },
                ["Xbox"] = new[] { new PresetOption(0x1u, "X / A"), new PresetOption(0x2u, "A / B"), new PresetOption(0x3u, "B / A") },
            });

    public static readonly IReadOnlyDictionary<string, int[]> ControllerOffsetAliases =
        new ReadOnlyDictionary<string, int[]>(
            new Dictionary<string, int[]>(StringComparer.Ordinal)
            {
                ["環境共鳴能力2"] = new[] { 0x241 },
                ["クエスト切り替え（右）"] = new[] { 0xFF0 },
                ["ホーム設計図"] = new[] { 0x1264 },
            });

    public static readonly IReadOnlyDictionary<string, int[]> KeyMouseOffsetAliases =
        new ReadOnlyDictionary<string, int[]>(
            new Dictionary<string, int[]>(StringComparer.Ordinal)
            {
                ["環境共鳴能力2"] = new[] { 0x227 },
                ["クエスト切り替え（右）"] = new[] { 0xFD6 },
                ["ホーム設計図"] = new[] { 0x124A },
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

    private static readonly HashSet<string> KeyMouseLControlPrefixActionNames = new(StringComparer.Ordinal)
    {
        "UI非表示", "パーティボイス切り替え", "ロールスキル1", "ロールスキル2", "ロールスキル3", "ロールスキル4"
    };

    private static readonly HashSet<string> ControllerPhotoModeUnlinkedActionNames = new(StringComparer.Ordinal)
    {
        "撮影モードメニューを閉じる", "撮影モード撮影"
    };

    private static readonly HashSet<string> ControllerFishingModeUnlinkedActionNames = new(StringComparer.Ordinal)
    {
        "釣りモードメニューを閉じる"
    };

    private static readonly HashSet<string> KeyMousePhotoModeUnlinkedActionNames = new(StringComparer.Ordinal)
    {
        "撮影モード撮影"
    };

    private static readonly HashSet<string> KeyMouseFishingModeUnlinkedActionNames = new(StringComparer.Ordinal)
    {
        
    };

    private static readonly IReadOnlyDictionary<string, string> KeyMousePhotoModeExceptions =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["カメラ-前"] = "撮影モードカメラパン-前", ["カメラ-後"] = "撮影モードカメラパン-後", ["カメラ-左"] = "撮影モードカメラパン-左", ["カメラ-右"] = "撮影モードカメラパン-右", ["ダッシュ/回避1"] = "撮影モードダッシュ/回避", ["撮影"] = "撮影モード撮影モード終了"
            });

    public static readonly IReadOnlyDictionary<string, string> ControllerPhotoModeLinks =
        BuildModeLinks(ControllerActions, "撮影モード", new Dictionary<string, string>(), ControllerPhotoModeUnlinkedActionNames);

    public static readonly IReadOnlyDictionary<string, string> ControllerFishingModeLinks =
        BuildModeLinks(ControllerActions, "釣りモード", new Dictionary<string, string>(), ControllerFishingModeUnlinkedActionNames);

    public static readonly IReadOnlyDictionary<string, string> KeyMousePhotoModeLinks =
        BuildModeLinks(KeyMouseActions, "撮影モード", KeyMousePhotoModeExceptions, KeyMousePhotoModeUnlinkedActionNames);

    public static readonly IReadOnlyDictionary<string, string> KeyMouseFishingModeLinks =
        BuildModeLinks(KeyMouseActions, "釣りモード", new Dictionary<string, string>(), KeyMouseFishingModeUnlinkedActionNames);

    public static IReadOnlyList<ControllerInputOption> GetControllerOptions(string? controllerType)
    {
        var labels = GetControllerDisplayMap(controllerType);
        return ControllerInputOptions
            .Where(option => labels.TryGetValue(option.Value, out _))
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
            .Where(value => labels.ContainsKey(value))
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

    public static bool UsesLControlPrefix(string actionName)
    {
        return KeyMouseLControlPrefixActionNames.Contains(actionName);
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
            KeybindModeGroup.Photo => ControllerActions.Where(action => action.Name.StartsWith("撮影モード", StringComparison.Ordinal)).ToArray(),
            KeybindModeGroup.Fishing => ControllerActions.Where(action => action.Name.StartsWith("釣りモード", StringComparison.Ordinal)).ToArray(),
            _ => ControllerActions.Where(action => !IsModeAction(action.Name)).ToArray()
        };
    }

    public static IReadOnlyList<KeyMouseActionDefinition> GetKeyMouseActions(KeybindModeGroup group)
    {
        return group switch
        {
            KeybindModeGroup.Photo => KeyMouseActions.Where(action => action.Name.StartsWith("撮影モード", StringComparison.Ordinal)).ToArray(),
            KeybindModeGroup.Fishing => KeyMouseActions.Where(action => action.Name.StartsWith("釣りモード", StringComparison.Ordinal)).ToArray(),
            _ => KeyMouseActions.Where(action => !IsModeAction(action.Name)).ToArray()
        };
    }

    public static string GetDisplayActionName(string actionName, KeybindModeGroup group)
    {
        return group switch
        {
            KeybindModeGroup.Photo when actionName.StartsWith("撮影モード", StringComparison.Ordinal) => actionName["撮影モード".Length..],
            KeybindModeGroup.Fishing when actionName.StartsWith("釣りモード", StringComparison.Ordinal) => actionName["釣りモード".Length..],
            _ => actionName
        };
    }

    private static bool IsModeAction(string actionName)
    {
        return actionName.StartsWith("撮影モード", StringComparison.Ordinal)
            || actionName.StartsWith("釣りモード", StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, string> BuildModeLinks<TDefinition>(
        IReadOnlyList<TDefinition> actions,
        string modePrefix,
        IReadOnlyDictionary<string, string> exceptionNormalToMode,
        ISet<string> unlinkedActionNames)
        where TDefinition : IKeybindActionDefinition
    {
        var normalNames = actions
            .Select(action => action.Name)
            .Where(name => !IsModeAction(name))
            .ToHashSet(StringComparer.Ordinal);

        var modeNames = actions
            .Select(action => action.Name)
            .Where(name => name.StartsWith(modePrefix, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

        var resolvedExceptions = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in exceptionNormalToMode)
        {
            if (normalNames.Contains(pair.Key) && modeNames.Contains(pair.Value))
            {
                resolvedExceptions[pair.Value] = pair.Key;
            }
        }

        var exceptionNormalNames = resolvedExceptions.Values.ToHashSet(StringComparer.Ordinal);
        var links = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var modeName in modeNames)
        {
            if (unlinkedActionNames.Contains(modeName))
            {
                continue;
            }

            var suffix = modeName[modePrefix.Length..];
            if (normalNames.Contains(suffix) && !exceptionNormalNames.Contains(suffix))
            {
                links[modeName] = suffix;
            }
        }

        foreach (var pair in resolvedExceptions)
        {
            links[pair.Key] = pair.Value;
        }

        return new ReadOnlyDictionary<string, string>(links);
    }
}
