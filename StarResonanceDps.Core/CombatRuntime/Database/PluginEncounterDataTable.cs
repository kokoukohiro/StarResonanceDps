// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime
{
    /// <summary>表 <c>PluginEncounterData</c> の1行(<see cref="DBSchema.PluginEncounterData"/>)。</summary>
    public class PluginEncounterDataTable
    {
        public ulong EncounterId { get; set; }
        public string PluginKey { get; set; } = null!;
        public string DataKey { get; set; } = null!;

        /// <summary>縮める前の長さ。上限の確かめに使う(展開しないで済む)。</summary>
        public long RawLength { get; set; }

        /// <summary>zstd で縮めた値。</summary>
        public byte[] Data { get; set; } = null!;
    }
}
