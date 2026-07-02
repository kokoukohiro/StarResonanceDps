using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using StarResonanceDps.Plugins.KeybindTool.Models;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.Plugins.KeybindTool.Services;

internal sealed class KeybindSaveService
{
    private readonly PluginLocalizer _texts;

    private static readonly byte[] InputAnchor = Encoding.ASCII.GetBytes("BKRInputConfigData");
    private static readonly byte[] PresetAnchor = Encoding.ASCII.GetBytes("BKL_SETID_7001");
    private static readonly byte[] HelperPetWheelAnchor = Encoding.ASCII.GetBytes("PetWheel");

    private const int PresetRelativeOffset = 0x17;
    private const int Helper1FromPetWheelOffset = 0x1B;
    private const int Helper2FromPetWheelOffset = 0x1F;

    public KeybindSaveService(PluginLocalizer texts)
    {
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
    }

    public KeybindSaveSession Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var raw = File.ReadAllBytes(filePath);
        var data = Decompress(raw);

        var inputAnchorOffset = FindAnchor(data, InputAnchor, _texts["Keybind.Error.RequiredInputDataNotFound"]);
        if (!KeybindCatalog.TryResolveServerProfile(filePath, out var serverProfile))
        {
            throw new InvalidDataException(_texts["Keybind.Error.ServerProfileNotFound"]);
        }

        var (helper1Offset, helper2Offset) = FindHelperOffsets(data);

        var presetAnchorOffset = FindAnchorOrNegative(data, PresetAnchor);
        var presetOffset = presetAnchorOffset >= 0
            ? presetAnchorOffset + PresetRelativeOffset
            : (int?)null;

        ValidateSessionLayout(
            data,
            inputAnchorOffset,
            serverProfile,
            helper1Offset,
            helper2Offset,
            presetOffset);

        return new KeybindSaveSession(
            filePath,
            data,
            inputAnchorOffset,
            serverProfile,
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

        return GetOffsets(
            session.InputAnchorOffset,
            KeybindCatalog.GetControllerRelativeOffsets(
                definition,
                session.ServerProfile));
    }

    public IReadOnlyList<int> GetKeyMouseOffsets(
        KeybindSaveSession session,
        KeyMouseActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(definition);

        return GetOffsets(
            session.InputAnchorOffset,
            KeybindCatalog.GetKeyMouseRelativeOffsets(
                definition,
                session.ServerProfile));
    }

    public KeybindWriteTargetValidation ValidateWriteTargets(KeybindSaveSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var invalidTargets = new List<KeybindInvalidWriteTarget>();
        var controllerActionIdsToSkip = new HashSet<string>(StringComparer.Ordinal);
        var keyMouseActionIdsToSkip = new HashSet<string>(StringComparer.Ordinal);

        foreach (var definition in KeybindCatalog.ControllerActions)
        {
            var invalidOffsets = GetControllerInvalidWriteOffsets(session, definition);
            if (invalidOffsets.Count == 0)
            {
                continue;
            }

            invalidTargets.Add(new KeybindInvalidWriteTarget(
                KeybindSaveTargetDevice.Controller,
                definition.Id,
                definition.Group,
                definition.Key,
                invalidOffsets));
            controllerActionIdsToSkip.Add(definition.Id);
        }

        foreach (var definition in KeybindCatalog.KeyMouseActions)
        {
            var invalidOffsets = GetKeyMouseInvalidWriteOffsets(session, definition);
            if (invalidOffsets.Count == 0)
            {
                continue;
            }

            invalidTargets.Add(new KeybindInvalidWriteTarget(
                KeybindSaveTargetDevice.KeyMouse,
                definition.Id,
                definition.Group,
                definition.Key,
                invalidOffsets));
            keyMouseActionIdsToSkip.Add(definition.Id);
        }

        return new KeybindWriteTargetValidation(
            invalidTargets,
            controllerActionIdsToSkip,
            keyMouseActionIdsToSkip);
    }

    public uint ReadUInt32(KeybindSaveSession session, int offset)
    {
        ArgumentNullException.ThrowIfNull(session);
        return ReadUInt32(session.Data, offset);
    }

    public uint ReadInputType(KeybindSaveSession session, int valueOffset)
    {
        ArgumentNullException.ThrowIfNull(session);
        return ReadUInt32(session.Data, valueOffset - sizeof(uint));
    }

    public void WriteUInt32(byte[] data, int offset, uint value)
    {
        ArgumentNullException.ThrowIfNull(data);

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
            throw new InvalidOperationException(_texts["Keybind.Error.SaveDirectoryUnavailable"]);
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

    private IReadOnlyList<KeybindInvalidWriteOffset> GetControllerInvalidWriteOffsets(
        KeybindSaveSession session,
        ControllerActionDefinition definition)
    {
        var invalidOffsets = new List<KeybindInvalidWriteOffset>();
        foreach (var offset in GetControllerOffsets(session, definition))
        {
            if (!IsControllerActionRecord(session.Data, offset))
            {
                invalidOffsets.Add(CreateInvalidWriteOffset(
                    session,
                    offset,
                    controller: true));
            }
        }

        return invalidOffsets;
    }

    private IReadOnlyList<KeybindInvalidWriteOffset> GetKeyMouseInvalidWriteOffsets(
        KeybindSaveSession session,
        KeyMouseActionDefinition definition)
    {
        var invalidOffsets = new List<KeybindInvalidWriteOffset>();
        foreach (var offset in GetKeyMouseOffsets(session, definition))
        {
            if (!IsKeyMouseActionRecord(session.Data, offset))
            {
                invalidOffsets.Add(CreateInvalidWriteOffset(
                    session,
                    offset,
                    controller: false));
            }
        }

        return invalidOffsets;
    }

    private static bool IsControllerActionRecord(byte[] data, int valueOffset)
    {
        if (!HasRange(data, valueOffset - sizeof(uint), sizeof(uint) * 3))
        {
            return false;
        }

        var inputType = ReadUInt32Unchecked(data, valueOffset - sizeof(uint));
        var stateValue = ReadUInt32Unchecked(data, valueOffset + sizeof(uint));
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

        var inputType = ReadUInt32Unchecked(data, valueOffset - sizeof(uint));
        return inputType is KeybindCatalog.InputTypeKeyboard
            or KeybindCatalog.InputTypeMouse;
    }

    private static KeybindInvalidWriteOffset CreateInvalidWriteOffset(
        KeybindSaveSession session,
        int valueOffset,
        bool controller)
    {
        var relativeOffset = valueOffset - session.InputAnchorOffset;
        var inputType = HasRange(session.Data, valueOffset - sizeof(uint), sizeof(uint))
            ? ReadUInt32Unchecked(session.Data, valueOffset - sizeof(uint))
            : 0u;
        uint? stateValue = controller
            && HasRange(session.Data, valueOffset + sizeof(uint), sizeof(uint))
            ? ReadUInt32Unchecked(session.Data, valueOffset + sizeof(uint))
            : null;

        return new KeybindInvalidWriteOffset(relativeOffset, inputType, stateValue);
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

    private (int Helper1Offset, int Helper2Offset) FindHelperOffsets(byte[] data)
    {
        var petWheelOffset = FindAnchor(data, HelperPetWheelAnchor, _texts["Keybind.Error.HelperKeysNotFound"]);
        var helper1Offset = petWheelOffset + Helper1FromPetWheelOffset;
        var helper2Offset = petWheelOffset + Helper2FromPetWheelOffset;

        EnsureRange(data, helper1Offset, sizeof(uint));
        EnsureRange(data, helper2Offset, sizeof(uint));

        var helper1Value = ReadUInt32(data, helper1Offset);
        var helper2Value = ReadUInt32(data, helper2Offset);

        if (!IsKnownHelperValue(helper1Value))
        {
            throw new InvalidDataException(_texts["Keybind.Error.Helper1OffsetNotFound"]);
        }

        if (!IsKnownHelperValue(helper2Value))
        {
            throw new InvalidDataException(_texts["Keybind.Error.Helper2OffsetNotFound"]);
        }

        return (helper1Offset, helper2Offset);
    }

    private void ValidateSessionLayout(
        byte[] data,
        int inputAnchorOffset,
        KeybindServerProfile serverProfile,
        int helper1Offset,
        int helper2Offset,
        int? presetOffset)
    {
        foreach (var action in KeybindCatalog.ControllerActions)
        {
            foreach (var relativeOffset in KeybindCatalog.GetControllerRelativeOffsets(
                action,
                serverProfile))
            {
                EnsureRange(
                    data,
                    inputAnchorOffset + relativeOffset - sizeof(uint),
                    sizeof(uint) * 3);
            }
        }

        foreach (var action in KeybindCatalog.KeyMouseActions)
        {
            foreach (var relativeOffset in KeybindCatalog.GetKeyMouseRelativeOffsets(
                action,
                serverProfile))
            {
                EnsureRange(
                    data,
                    inputAnchorOffset + relativeOffset - sizeof(uint),
                    sizeof(uint) * 2);
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
        IReadOnlyList<int> relativeOffsets)
    {
        return relativeOffsets
            .Select(relativeOffset => inputAnchorOffset + relativeOffset)
            .ToArray();
    }

    private static bool IsKnownHelperValue(uint value)
    {
        return KeybindCatalog.HelperMainToActionValue.ContainsKey(value);
    }

    private uint ReadUInt32(byte[] data, int offset)
    {
        EnsureRange(data, offset, sizeof(uint));
        return ReadUInt32Unchecked(data, offset);
    }

    private static uint ReadUInt32Unchecked(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, sizeof(uint)));
    }

    private static bool HasRange(byte[] data, int offset, int length)
    {
        return offset >= 0 && length >= 0 && offset <= data.Length - length;
    }

    private void EnsureRange(byte[] data, int offset, int length)
    {
        if (!HasRange(data, offset, length))
        {
            throw new InvalidDataException(_texts["Keybind.Error.UnexpectedFileFormat"]);
        }
    }
}

internal sealed class KeybindSaveSession
{
    public KeybindSaveSession(
        string filePath,
        byte[] data,
        int inputAnchorOffset,
        KeybindServerProfile serverProfile,
        int helper1Offset,
        int helper2Offset,
        int? presetOffset)
    {
        FilePath = filePath;
        Data = data;
        InputAnchorOffset = inputAnchorOffset;
        ServerProfile = serverProfile;
        Helper1Offset = helper1Offset;
        Helper2Offset = helper2Offset;
        PresetOffset = presetOffset;
    }

    public string FilePath { get; }

    public byte[] Data { get; private set; }

    public int InputAnchorOffset { get; }

    public KeybindServerProfile ServerProfile { get; }

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
