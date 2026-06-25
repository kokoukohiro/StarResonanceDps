using System.IO;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using StarResonanceDps.Plugins.KeybindTool.Models;

namespace StarResonanceDps.Plugins.KeybindTool.Services;

internal sealed class BpsrKeybindSaveService
{
    private static readonly byte[] InputAnchor = Encoding.ASCII.GetBytes("BKRInputConfigData");
    private static readonly byte[] PresetAnchor = Encoding.ASCII.GetBytes("BKL_SETID_7001");
    private static readonly byte[] HelperPetWheelAnchor = Encoding.ASCII.GetBytes("PetWheel");

    private const int PresetRelativeOffset = 0x17;
    private const int Helper1FromPetWheelOffset = 0x1B;
    private const int Helper2FromPetWheelOffset = 0x1F;

    public KeybindSaveSession Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var raw = File.ReadAllBytes(filePath);
        var data = Decompress(raw);

        var inputAnchorOffset = FindAnchor(data, InputAnchor, "必須データが見つかりません。");
        var (helper1Offset, helper2Offset) = FindHelperOffsets(data);

        var presetAnchorOffset = FindAnchorOrNegative(data, PresetAnchor);
        var presetOffset = presetAnchorOffset >= 0
            ? presetAnchorOffset + PresetRelativeOffset
            : (int?)null;

        ValidateSessionLayout(
            data,
            inputAnchorOffset,
            helper1Offset,
            helper2Offset,
            presetOffset);

        return new KeybindSaveSession(
            filePath,
            data,
            inputAnchorOffset,
            helper1Offset,
            helper2Offset,
            presetOffset);
    }

    public IReadOnlyList<int> GetControllerOffsets(
        KeybindSaveSession session,
        ControllerActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(definition);

        var candidates = GetOffsets(
            session.InputAnchorOffset,
            definition.RelativeOffsets,
            KeybindCatalog.ControllerOffsetAliases,
            definition.Id);

        var resolved = candidates
            .Where(offset => IsControllerActionRecord(session.Data, offset))
            .ToArray();

        return resolved.Length > 0 ? resolved : candidates;
    }

    public IReadOnlyList<int> GetKeyMouseOffsets(
        KeybindSaveSession session,
        KeyMouseActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(definition);

        var candidates = GetOffsets(
            session.InputAnchorOffset,
            definition.RelativeOffsets,
            KeybindCatalog.KeyMouseOffsetAliases,
            definition.Id);

        var resolved = candidates
            .Where(offset => IsKeyMouseActionRecord(session.Data, offset))
            .ToArray();

        return resolved.Length > 0 ? resolved : candidates;
    }

    public IReadOnlyList<int> GetWritableControllerOffsets(
        KeybindSaveSession session,
        ControllerActionDefinition definition)
    {
        return GetControllerOffsets(session, definition)
            .Where(offset => IsControllerActionRecord(session.Data, offset))
            .ToArray();
    }

    public IReadOnlyList<int> GetWritableKeyMouseOffsets(
        KeybindSaveSession session,
        KeyMouseActionDefinition definition)
    {
        return GetKeyMouseOffsets(session, definition)
            .Where(offset => IsKeyMouseActionRecord(session.Data, offset))
            .ToArray();
    }

    public uint ReadUInt32(KeybindSaveSession session, int offset)
    {
        return ReadUInt32(session.Data, offset);
    }

    public uint ReadInputType(KeybindSaveSession session, int valueOffset)
    {
        return ReadUInt32(session.Data, valueOffset - sizeof(uint));
    }

    public void WriteUInt32(byte[] data, int offset, uint value)
    {
        EnsureRange(data, offset, sizeof(uint));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, sizeof(uint)), value);
    }

    public void Save(string filePath, byte[] data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(data);

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("設定ファイルの保存先を特定できません。");
        }

        var temporaryPath = $"{filePath}.tmp";
        try
        {
            var compressed = Compress(data);
            File.WriteAllBytes(temporaryPath, compressed);
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static byte[] Decompress(byte[] raw)
    {
        using var input = new MemoryStream(raw, writable: false);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            brotli.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    private static int FindAnchor(byte[] data, byte[] anchor, string errorMessage)
    {
        var offset = FindAnchorOrNegative(data, anchor);
        if (offset < 0)
        {
            throw new InvalidDataException(errorMessage);
        }

        return offset;
    }

    private static int FindAnchorOrNegative(byte[] data, byte[] anchor)
    {
        if (anchor.Length == 0 || data.Length < anchor.Length)
        {
            return -1;
        }

        for (var offset = 0; offset <= data.Length - anchor.Length; offset++)
        {
            if (data.AsSpan(offset, anchor.Length).SequenceEqual(anchor))
            {
                return offset;
            }
        }

        return -1;
    }

    private static (int Helper1Offset, int Helper2Offset) FindHelperOffsets(byte[] data)
    {
        var petWheelOffset = FindAnchor(data, HelperPetWheelAnchor, "補助キー設定が見つかりません。");
        var helper1Offset = petWheelOffset + Helper1FromPetWheelOffset;
        var helper2Offset = petWheelOffset + Helper2FromPetWheelOffset;

        EnsureRange(data, helper1Offset, sizeof(uint));
        EnsureRange(data, helper2Offset, sizeof(uint));

        var helper1Value = ReadUInt32(data, helper1Offset);
        var helper2Value = ReadUInt32(data, helper2Offset);

        if (!IsKnownHelperValue(helper1Value))
        {
            throw new InvalidDataException("補助キー1の保存位置を特定できません。");
        }

        if (!IsKnownHelperValue(helper2Value))
        {
            throw new InvalidDataException("補助キー2の保存位置を特定できません。");
        }

        return (helper1Offset, helper2Offset);
    }

    private static void ValidateSessionLayout(
        byte[] data,
        int inputAnchorOffset,
        int helper1Offset,
        int helper2Offset,
        int? presetOffset)
    {
        foreach (var action in KeybindCatalog.ControllerActions)
        {
            foreach (var offset in action.RelativeOffsets)
            {
                EnsureRange(data, inputAnchorOffset + offset - sizeof(uint), sizeof(uint) * 3);
            }
        }

        foreach (var action in KeybindCatalog.KeyMouseActions)
        {
            foreach (var offset in action.RelativeOffsets)
            {
                EnsureRange(data, inputAnchorOffset + offset - sizeof(uint), sizeof(uint) * 2);
            }
        }

        EnsureRange(data, helper1Offset, sizeof(uint));
        EnsureRange(data, helper2Offset, sizeof(uint));

        if (presetOffset is not null)
        {
            EnsureRange(data, presetOffset.Value, sizeof(byte));
        }
    }

    private static IReadOnlyList<int> GetOffsets(
        int inputAnchorOffset,
        IReadOnlyList<int> relativeOffsets,
        IReadOnlyDictionary<string, int[]> aliases,
        string actionName)
    {
        var values = new List<int>(relativeOffsets.Count + 2);
        foreach (var relativeOffset in relativeOffsets)
        {
            if (!values.Contains(relativeOffset))
            {
                values.Add(relativeOffset);
            }
        }

        if (aliases.TryGetValue(actionName, out var aliasOffsets))
        {
            foreach (var aliasOffset in aliasOffsets)
            {
                if (!values.Contains(aliasOffset))
                {
                    values.Add(aliasOffset);
                }
            }
        }

        return values
            .Select(relativeOffset => inputAnchorOffset + relativeOffset)
            .ToArray();
    }

    private static bool IsControllerActionRecord(byte[] data, int valueOffset)
    {
        if (!HasRange(data, valueOffset - sizeof(uint), sizeof(uint) * 3))
        {
            return false;
        }

        var inputType = ReadUInt32(data, valueOffset - sizeof(uint));
        var stateValue = ReadUInt32(data, valueOffset + sizeof(uint));

        return inputType == KeybindCatalog.InputTypeController
            && (stateValue is KeybindCatalog.ActionStateSingle
                or KeybindCatalog.ActionStateHelper1
                or KeybindCatalog.ActionStateHelper2);
    }

    private static bool IsKeyMouseActionRecord(byte[] data, int valueOffset)
    {
        if (!HasRange(data, valueOffset - sizeof(uint), sizeof(uint) * 2))
        {
            return false;
        }

        var inputType = ReadUInt32(data, valueOffset - sizeof(uint));
        return inputType is KeybindCatalog.InputTypeKeyboard or KeybindCatalog.InputTypeMouse;
    }

    private static bool IsKnownHelperValue(uint value)
    {
        return KeybindCatalog.HelperMainToActionValue.ContainsKey(value);
    }

    private static uint ReadUInt32(byte[] data, int offset)
    {
        EnsureRange(data, offset, sizeof(uint));
        return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, sizeof(uint)));
    }

    private static bool HasRange(byte[] data, int offset, int length)
    {
        return offset >= 0 && length >= 0 && offset <= data.Length - length;
    }

    private static void EnsureRange(byte[] data, int offset, int length)
    {
        if (!HasRange(data, offset, length))
        {
            throw new InvalidDataException("ファイルの形式が想定と異なります。");
        }
    }
}

internal sealed class KeybindSaveSession
{
    public KeybindSaveSession(
        string filePath,
        byte[] data,
        int inputAnchorOffset,
        int helper1Offset,
        int helper2Offset,
        int? presetOffset)
    {
        FilePath = filePath;
        Data = data;
        InputAnchorOffset = inputAnchorOffset;
        Helper1Offset = helper1Offset;
        Helper2Offset = helper2Offset;
        PresetOffset = presetOffset;
    }

    public string FilePath { get; }

    public byte[] Data { get; private set; }

    public int InputAnchorOffset { get; }

    public int Helper1Offset { get; }

    public int Helper2Offset { get; }

    public int? PresetOffset { get; }

    public bool IsPresetSupported => PresetOffset is not null;

    public void ReplaceData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
    }
}
