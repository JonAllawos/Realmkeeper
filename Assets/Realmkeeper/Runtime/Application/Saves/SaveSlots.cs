using System;
using System.Collections.Generic;

namespace Realmkeeper.Application
{
    /// <summary>M1.1 (ADR-0123): what a save slot is for.</summary>
    public enum SaveSlotKind
    {
        /// <summary>slot-1 … slot-3: written only when the player chooses Save Game.</summary>
        Manual = 0,

        /// <summary>auto-1 … auto-3: the autosave rotation (the oldest is replaced).</summary>
        Auto = 1,

        /// <summary>Any other name (developer and pre-M1.1 saves): listed in development builds only.</summary>
        Other = 2,
    }

    /// <summary>
    /// M1.1 (ADR-0123): the release save slots. Three manual slots the player chooses, three rotating autosaves. Any
    /// other slot name is an older or developer save: it still loads, but release menus do not offer it.
    /// </summary>
    public static class SaveSlots
    {
        public static readonly IReadOnlyList<string> Manual = new[] { "slot-1", "slot-2", "slot-3" };
        public static readonly IReadOnlyList<string> Auto = new[] { "auto-1", "auto-2", "auto-3" };

        public static SaveSlotKind KindOf(string slot)
        {
            foreach (var m in Manual) if (string.Equals(m, slot, StringComparison.Ordinal)) return SaveSlotKind.Manual;
            foreach (var a in Auto) if (string.Equals(a, slot, StringComparison.Ordinal)) return SaveSlotKind.Auto;
            return SaveSlotKind.Other;
        }

        public static bool IsManual(string slot) => KindOf(slot) == SaveSlotKind.Manual;
        public static bool IsAuto(string slot) => KindOf(slot) == SaveSlotKind.Auto;

        /// <summary>"Slot 1", "Autosave 2", or the slot name itself.</summary>
        public static string DisplayName(string slot)
        {
            for (var i = 0; i < Manual.Count; i++) if (Manual[i] == slot) return "Slot " + (i + 1);
            for (var i = 0; i < Auto.Count; i++) if (Auto[i] == slot) return "Autosave " + (i + 1);
            return slot;
        }

        /// <summary>
        /// The autosave slot to write next: the first that has no save, otherwise the one played longest ago
        /// (<paramref name="playedAt"/> gives each existing autosave's time; null counts as oldest).
        /// </summary>
        public static string NextAutosave(Func<string, bool> exists, Func<string, DateTime?> playedAt)
        {
            if (exists == null) throw new ArgumentNullException(nameof(exists));
            foreach (var a in Auto) if (!exists(a)) return a;
            string oldest = null;
            var oldestAt = DateTime.MaxValue;
            foreach (var a in Auto)
            {
                var at = playedAt?.Invoke(a) ?? DateTime.MinValue;
                if (oldest == null || at < oldestAt)
                {
                    oldest = a;
                    oldestAt = at;
                }
            }

            return oldest;
        }
    }
}
