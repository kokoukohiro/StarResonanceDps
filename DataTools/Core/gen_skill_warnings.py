"""
戦闘画面の警告バーを出す技の一覧を生成する。

  Data/Generated/SkillWarnings.json

中身は技レベルID(技ID×100＋レベル)の配列。名前は持たない(`SkillNames.json`から引く)。

**`show_data`の技辞書で、スロット52に項目を持つ技レベルが警告バーを出す。**

`show_data`は`Bundles`の`<番号>.ab`に入ったテキストアセット。番号は版で変わるので、
アドレス一覧(`Unk/*.bin`)の`bin/datas/show_data`の行から引く。

アドレス一覧と`Bundles`はデータソースごとにある。**出力は新しい版の`Star`で作る。**
ほかのデータソース(`StarASIA`)も読み、`Star`に無い技レベルが1件でもあれば書かずに止まる
(`StarASIA`の版のほうが新しくなったときに取りこぼさないため)。
データソースのバンドルの一覧が、そのデータソースの`Bundles`に全部そろわなければ止まる。

読めない形、件数・スロット数・区切りの食い違いがあれば止まる。
"""
import glob
import os
import re
import struct
import sys

# _commonを読み込む前に置く(後ろだと__pycache__が作られる)
sys.dont_write_bytecode = True

from _common import GENERATED, SOURCES, bundles_dir, dump, unk_dir

# 出力を作るデータソース。ほかのデータソースの警告の技を全部含んでいることを実行のたびに確かめる。
PRIMARY_SOURCE = "Star"

SHOW_DATA_ADDRESS = "bin/datas/show_data"
SHOW_DATA_ASSET = "show_data"
ADDRESS_LIST_MARKER = b"->>>> bundleHash:"
WARNING_SLOT = 52
SLOT_COUNT = 68
TEXT_ASSET_CLASS_ID = 49


# ---------------------------------------------------------------------------
# アドレス一覧


def find_address_list(source):
    """データソースの`Unk/*.bin`から、中身でアドレス一覧を1本だけ探す。"""
    directory = unk_dir(source)
    paths = sorted(glob.glob(os.path.join(directory, "*.bin")))
    if not paths:
        raise FileNotFoundError("%sに*.binが無い" % directory)
    found = []
    for path in paths:
        with open(path, "rb") as fh:
            data = fh.read()
        if ADDRESS_LIST_MARKER in data:
            found.append((path, data))
    if len(found) != 1:
        raise ValueError("%sのアドレス一覧が1本に決まらない: %s" % (directory, [p for p, _ in found]))
    return found[0]


def header_count(path, data, name):
    counts = re.findall(rb"^(?:\xef\xbb\xbf)?" + re.escape(name.encode()) + rb":(\d+) ", data, re.MULTILINE)
    if len(counts) != 1:
        raise ValueError("%sに%sのヘッダー行が%d件ある" % (path, name, len(counts)))
    return int(counts[0])


def read_address_list(path, data):
    """
    アドレス一覧から、アドレス → バンドル番号 と、バンドルの一覧(番号の集合)を読む。

    アドレス行は`address:<アドレス> ->>>> hash:<数字> ->>>> bundleHash:<数字>`(アドレスは空白を含むことがある)で、
    件数はヘッダー行`AddressCount`。バンドルの一覧はヘッダー行`DepsDictCount`のあとの`bundleHash:<数字>`の行。
    どちらもヘッダー行の件数と行数が合わなければ止まる。
    """
    rows = re.findall(rb"^address:(.*) ->>>> hash:\d+ ->>>> bundleHash:(\d+)\r?$", data, re.MULTILINE)
    address_count = header_count(path, data, "AddressCount")
    if len(rows) != address_count:
        raise ValueError("%sのアドレス行が%d行で、AddressCountは%d" % (path, len(rows), address_count))
    addresses = {}
    for address, number in rows:
        address = address.decode("utf-8")
        if address in addresses:
            raise ValueError("%sにアドレス%sが重複" % (path, address))
        addresses[address] = int(number)

    bundles = [int(n) for n in re.findall(rb"^bundleHash:(\d+)\r?$", data, re.MULTILINE)]
    bundle_count = header_count(path, data, "DepsDictCount")
    if len(bundles) != bundle_count or len(set(bundles)) != bundle_count:
        raise ValueError("%sのバンドル一覧が%d行(異なる番号%d)で、DepsDictCountは%d"
                         % (path, len(bundles), len(set(bundles)), bundle_count))
    return addresses, set(bundles)


def present_bundles(source):
    """そのデータソースの`Bundles`にある`<番号>.ab`の番号。"""
    directory = bundles_dir(source)
    if not os.path.isdir(directory):
        raise FileNotFoundError("%sが無い" % directory)
    return {int(name[:-3]) for name in os.listdir(directory) if re.fullmatch(r"\d+\.ab", name)}


def show_data_bundle_path(source):
    """そのデータソースのアドレス一覧から`show_data`のバンドルを引く。一覧が同じデータソースの`Bundles`に全部そろわなければ止まる。"""
    path, data = find_address_list(source)
    addresses, bundles = read_address_list(path, data)
    if SHOW_DATA_ADDRESS not in addresses:
        raise ValueError("%sに%sの行が無い" % (path, SHOW_DATA_ADDRESS))
    missing = len(bundles - present_bundles(source))
    number = addresses[SHOW_DATA_ADDRESS]
    print("%-8s %sアドレス%d / バンドル%d(Bundlesに無い%d)/ %sは%d"
          % (source, os.path.basename(path), len(addresses), len(bundles), missing, SHOW_DATA_ADDRESS, number))
    if missing:
        raise ValueError("%sのバンドルの一覧が%sに%d個そろわない" % (source, bundles_dir(source), missing))
    return os.path.join(bundles_dir(source), "%d.ab" % number)


# ---------------------------------------------------------------------------
# UnityFS


def lz4_block(src, size):
    dst = bytearray()
    i = 0
    n = len(src)
    while i < n:
        token = src[i]
        i += 1
        literal = token >> 4
        if literal == 15:
            while True:
                b = src[i]
                i += 1
                literal += b
                if b != 255:
                    break
        dst += src[i:i + literal]
        i += literal
        if i >= n:
            break
        offset = src[i] | (src[i + 1] << 8)
        i += 2
        match = token & 15
        if match == 15:
            while True:
                b = src[i]
                i += 1
                match += b
                if b != 255:
                    break
        match += 4
        start = len(dst) - offset
        if offset == 0 or start < 0:
            raise ValueError("LZ4の参照先が不正")
        if offset >= match:
            dst += dst[start:start + match]
        else:
            for k in range(match):
                dst.append(dst[start + k])
    if len(dst) != size:
        raise ValueError("LZ4の展開後の長さが%dで、期待は%d" % (len(dst), size))
    return bytes(dst)


def decompress(data, flags, size):
    kind = flags & 0x3F
    if kind == 0:
        return data
    if kind in (2, 3):
        return lz4_block(data, size)
    raise ValueError("未対応の圧縮形式%d" % kind)


def cstring(data, i):
    end = data.index(0, i)
    return data[i:end].decode("utf-8"), end + 1


def read_unityfs(path):
    """UnityFSのバンドルから、ノードのパス → 中身 を返す。"""
    with open(path, "rb") as fh:
        ab = fh.read()
    signature, i = cstring(ab, 0)
    if signature != "UnityFS":
        raise ValueError("%sはUnityFSではない(%s)" % (path, signature))
    version = struct.unpack_from(">I", ab, i)[0]
    i += 4
    _, i = cstring(ab, i)
    _, i = cstring(ab, i)
    _, compressed_info_size, info_size, flags = struct.unpack_from(">qIII", ab, i)
    i += 20
    if version >= 7:
        i = (i + 15) // 16 * 16
    if flags & 0x80:
        raise ValueError("ブロック情報が末尾にある形は未対応")
    info = decompress(ab[i:i + compressed_info_size], flags, info_size)
    i += compressed_info_size
    if flags & 0x200:
        i = (i + 15) // 16 * 16

    j = 16
    block_count = struct.unpack_from(">i", info, j)[0]
    j += 4
    blocks = []
    for _ in range(block_count):
        blocks.append(struct.unpack_from(">IIH", info, j))
        j += 10
    node_count = struct.unpack_from(">i", info, j)[0]
    j += 4
    nodes = []
    for _ in range(node_count):
        offset, size, _ = struct.unpack_from(">qqI", info, j)
        j += 20
        name, j = cstring(info, j)
        nodes.append((offset, size, name))

    raw = bytearray()
    for size, compressed_size, block_flags in blocks:
        raw += decompress(ab[i:i + compressed_size], block_flags, size)
        i += compressed_size
    return {name: bytes(raw[offset:offset + size]) for offset, size, name in nodes}


def text_assets(serialized):
    """SerializedFile(版22以上)のTextAssetを 名前 → 中身 で返す。"""
    version = struct.unpack_from(">I", serialized, 8)[0]
    if version < 22:
        raise ValueError("SerializedFileの版%dは未対応" % version)
    endian = ">" if serialized[16] else "<"
    data_offset = struct.unpack_from(">q", serialized, 32)[0]
    k = 48
    _, k = cstring(serialized, k)
    k += 4
    has_type_tree = serialized[k]
    k += 1
    type_count = struct.unpack_from(endian + "i", serialized, k)[0]
    k += 4
    class_ids = []
    for _ in range(type_count):
        class_id = struct.unpack_from(endian + "i", serialized, k)[0]
        k += 4 + 1 + 2
        if class_id == 114:
            k += 16
        k += 16
        if has_type_tree:
            node_count, string_size = struct.unpack_from(endian + "ii", serialized, k)
            k += 8 + node_count * 32 + string_size
            dependency_count = struct.unpack_from(endian + "i", serialized, k)[0]
            k += 4 + dependency_count * 4
        class_ids.append(class_id)
    object_count = struct.unpack_from(endian + "i", serialized, k)[0]
    k += 4
    assets = {}
    for _ in range(object_count):
        k = (k + 3) // 4 * 4
        _, start, size, type_index = struct.unpack_from(endian + "qqIi", serialized, k)
        k += 24
        if class_ids[type_index] != TEXT_ASSET_CLASS_ID:
            continue
        body = serialized[data_offset + start:data_offset + start + size]
        name_length = struct.unpack_from(endian + "i", body, 0)[0]
        name = body[4:4 + name_length].decode("utf-8")
        q = (4 + name_length + 3) // 4 * 4
        script_length = struct.unpack_from(endian + "i", body, q)[0]
        if name in assets:
            raise ValueError("TextAsset %sが複数ある" % name)
        assets[name] = body[q + 4:q + 4 + script_length]
    return assets


# ---------------------------------------------------------------------------
# show_data
#
# 先頭06のあと、整数キーの辞書が 技 → バフ → 弾 の順に並ぶ。各辞書はint32件数 ＋ レコード。
# レコード: int32キー / int32 0 / 45 / byte中身のあるスロットの数 / スロット×68
# スロット: int32 -1(空)かint32件数 ＋ 項目。項目は型のbyteから始まる。
#   型4: ff uint16 uint16 ff  (中身を直接持つときはffの代わりに10で始まる50バイト)
#   型5: ff uint16 uint16文字列ff
#   型6: ff uint16 uint16文字列 文字列ff
#   型8: ff uint16 uint16文字列 文字列ff uint16 ff
# 文字列: int32が -1なら無し、0以上なら4バイトの値、それ以外は ~値 がバイト長で、int32文字数とUTF-8が続く。


class ShowDataReader:
    def __init__(self, data):
        self.data = data

    def int32(self, i):
        return struct.unpack_from("<i", self.data, i)[0]

    def string(self, i):
        head = self.int32(i)
        if head >= -1:
            return i + 4
        length = ~head
        chars = self.int32(i + 4)
        if not 0 <= chars <= length:
            raise ValueError("show_dataの%dの文字列の長さが不正" % i)
        self.data[i + 8:i + 8 + length].decode("utf-8")
        return i + 8 + length

    def expect_ff(self, i):
        if self.data[i] != 0xFF:
            raise ValueError("show_dataの%dにffが無い" % i)
        return i + 1

    def entry(self, i):
        kind = self.data[i]
        i += 1
        if kind == 4:
            if self.data[i] == 0x10:
                return i + 50
            i = self.expect_ff(i) + 4
            return self.expect_ff(i)
        if kind in (5, 6, 8):
            i = self.expect_ff(i) + 4
            i = self.string(i)
            if kind in (6, 8):
                i = self.string(i)
            if kind == 8:
                i = self.expect_ff(i) + 2
            return self.expect_ff(i)
        raise ValueError("show_dataの%dに未知の型%d" % (i - 1, kind))

    def record(self, i):
        key = self.int32(i)
        if self.int32(i + 4) != 0 or self.data[i + 8] != 0x45:
            raise ValueError("show_dataの%dがレコードの先頭の形でない" % i)
        filled = self.data[i + 9]
        i += 10
        slots = set()
        for slot in range(SLOT_COUNT):
            count = self.int32(i)
            i += 4
            if count == -1:
                continue
            if count < 0:
                raise ValueError("show_dataの%dの件数%dが不正" % (i - 4, count))
            for _ in range(count):
                i = self.entry(i)
            slots.add(slot)
        if len(slots) != filled:
            raise ValueError("show_dataのキー%dで中身のあるスロットが%d、レコードの先頭の値は%d" % (key, len(slots), filled))
        return key, slots, i

    def dictionary(self, i):
        count = self.int32(i)
        i += 4
        records = {}
        for _ in range(count):
            key, slots, i = self.record(i)
            if key in records:
                raise ValueError("show_dataのキー%dが重複" % key)
            records[key] = slots
        return records, i


def read_show_data(data):
    if data[0] != 6:
        raise ValueError("show_dataの先頭が06でない")
    reader = ShowDataReader(data)
    i = 1
    dictionaries = []
    for name in ("技", "バフ", "弾"):
        records, i = reader.dictionary(i)
        dictionaries.append((name, records))
    return dictionaries


# ---------------------------------------------------------------------------


def warning_levels_of(source):
    """そのデータソースの`show_data`で、スロット52に項目を持つ技レベル。"""
    bundle_path = show_data_bundle_path(source)
    assets = {}
    for serialized in read_unityfs(bundle_path).values():
        for name, body in text_assets(serialized).items():
            if name in assets:
                raise ValueError("TextAsset %sが複数ある" % name)
            assets[name] = body
    if SHOW_DATA_ASSET not in assets:
        raise ValueError("%sにTextAsset %sが無い(中身: %s)" % (bundle_path, SHOW_DATA_ASSET, sorted(assets)))
    show_data = assets[SHOW_DATA_ASSET]
    print("%s → %s %dバイト" % (os.path.basename(bundle_path), SHOW_DATA_ASSET, len(show_data)))

    dictionaries = read_show_data(show_data)
    for name, records in dictionaries:
        print("  %-3s %5d件 / スロット%dを持つ%d件"
              % (name, len(records), WARNING_SLOT, sum(1 for s in records.values() if WARNING_SLOT in s)))

    skills = dictionaries[0][1]
    return sorted(key for key, slots in skills.items() if WARNING_SLOT in slots)


def main():
    levels = {PRIMARY_SOURCE: warning_levels_of(PRIMARY_SOURCE)}
    for source in SOURCES:
        if source != PRIMARY_SOURCE:
            levels[source] = warning_levels_of(source)

    primary = set(levels[PRIMARY_SOURCE])
    for source, found in levels.items():
        extra = sorted(set(found) - primary)
        if extra:
            raise ValueError("%sにだけある警告の技レベルが%d件: %s(%sで作ると取りこぼす)"
                             % (source, len(extra), extra, PRIMARY_SOURCE))
        print("%-8s警告の技%d件(%sに無いもの0)" % (source, len(found), PRIMARY_SOURCE))

    warning_levels = levels[PRIMARY_SOURCE]
    path = os.path.join(GENERATED, "SkillWarnings.json")
    dump(path, warning_levels)
    print("→ %s %d件(%s)" % (os.path.relpath(path, GENERATED), len(warning_levels), PRIMARY_SOURCE))


if __name__ == "__main__":
    main()
