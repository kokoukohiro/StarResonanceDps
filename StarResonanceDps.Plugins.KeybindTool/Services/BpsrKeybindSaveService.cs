using System.IO;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using StarResonanceDps.Plugins.KeybindTool.Models;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.Plugins.KeybindTool.Services;

internal sealed class BpsrKeybindSaveService
{
    private readonly PluginLocalizer _texts;

    private static readonly byte[] InputAnchor = Encoding.ASCII.GetBytes("BKRInputConfigData");
    private static readonly byte[] PresetAnchor = Encoding.ASCII.GetBytes("BKL_SETID_7001");
    private static readonly byte[] HelperPetWheelAnchor = Encoding.ASCII.GetBytes("PetWheel");

    private const int PresetRelativeOffset = 0x17;
    private const int Helper1FromPetWheelOffset = 0x1B;
    private const int Helper2FromPetWheelOffset = 0x1F;

    public BpsrKeybindSaveService(PluginLocalizer texts)
    {
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
    }

    public KeybindSaveSession Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var raw = File.ReadAllBytes(filePath);
        var data = Decompress(raw);

        var inputAnchorOffset = FindAnchor(data, InputAnchor, _texts["Keybind.Error.RequiredInputDataNotFound"]);
        var bindingDataLayout = DetectBindingDataLayout(data, inputAnchorOffset);
        var (helper1Offset, helper2Offset) = FindHelperOffsets(data);

        var presetAnchorOffset = FindAnchorOrNegative(data, PresetAnchor);
        var presetOffset = presetAnchorOffset >= 0
            ? presetAnchorOffset + PresetRelativeOffset
            : (int?)null;

        ValidateSessionLayout(
            data,
            inputAnchorOffset,
            bindingDataLayout,
            helper1Offset,
            helper2Offset,
            presetOffset);

        return new KeybindSaveSession(
            filePath,
            data,
            inputAnchorOffset,
            bindingDataLayout,
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
                session.BindingDataLayout));
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
                session.BindingDataLayout));
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

    private static KeybindBindingDataLayout DetectBindingDataLayout(
        byte[] data,
        int inputAnchorOffset)
    {
        var beforeUpdateScore = ScoreBindingDataLayout(
            data,
            inputAnchorOffset,
            KeybindBindingDataLayout.BeforeUpdate);
        var afterUpdateScore = ScoreBindingDataLayout(
            data,
            inputAnchorOffset,
            KeybindBindingDataLayout.AfterUpdate);

        return afterUpdateScore > beforeUpdateScore
            ? KeybindBindingDataLayout.AfterUpdate
            : KeybindBindingDataLayout.BeforeUpdate;
    }

    private static int ScoreBindingDataLayout(
        byte[] data,
        int inputAnchorOffset,
        KeybindBindingDataLayout layout)
    {
        var score = 0;

        foreach (var action in KeybindCatalog.ControllerActions)
        {
            foreach (var relativeOffset in KeybindCatalog.GetControllerRelativeOffsets(action, layout))
            {
                var valueOffset = inputAnchorOffset + relativeOffset;
                if (HasExpectedInputType(
                    data,
                    valueOffset,
                    KeybindCatalog.InputTypeController))
                {
                    score++;
                }
            }
        }

        foreach (var action in KeybindCatalog.KeyMouseActions)
        {
            foreach (var relativeOffset in KeybindCatalog.GetKeyMouseRelativeOffsets(action, layout))
            {
                var valueOffset = inputAnchorOffset + relativeOffset;
                if (HasExpectedInputType(
                    data,
                    valueOffset,
                    KeybindCatalog.InputTypeKeyboard,
                    KeybindCatalog.InputTypeMouse))
                {
                    score++;
                }
            }
        }

        return score;
    }

    private static bool HasExpectedInputType(
        byte[] data,
        int valueOffset,
        params uint[] expectedTypes)
    {
        if (!HasRange(data, valueOffset - sizeof(uint), sizeof(uint) * 2))
        {
            return false;
        }

        var inputType = BinaryPrimitives.ReadUInt32LittleEndian(
            data.AsSpan(valueOffset - sizeof(uint), sizeof(uint)));
        return expectedTypes.Contains(inputType);
    }

    private void ValidateSessionLayout(
        byte[] data,
        int inputAnchorOffset,
        KeybindBindingDataLayout bindingDataLayout,
        int helper1Offset,
        int helper2Offset,
        int? presetOffset)
    {
        foreach (var action in KeybindCatalog.ControllerActions)
        {
            foreach (var relativeOffset in KeybindCatalog.GetControllerRelativeOffsets(
                action,
                bindingDataLayout))
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
                bindingDataLayout))
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
        KeybindBindingDataLayout bindingDataLayout,
        int helper1Offset,
        int helper2Offset,
        int? presetOffset)
    {
        FilePath = filePath;
        Data = data;
        InputAnchorOffset = inputAnchorOffset;
        BindingDataLayout = bindingDataLayout;
        Helper1Offset = helper1Offset;
        Helper2Offset = helper2Offset;
        PresetOffset = presetOffset;
    }

    public string FilePath { get; }

    public byte[] Data { get; private set; }

    public int InputAnchorOffset { get; }

    public KeybindBindingDataLayout BindingDataLayout { get; }

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
