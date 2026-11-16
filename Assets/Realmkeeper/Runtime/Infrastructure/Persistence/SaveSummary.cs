using System;
using System.Globalization;
using Realmkeeper.Application;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>M1.1 (ADR-0123): whether a save can be loaded, as the Load Game screen shows it.</summary>
    public enum SaveState
    {
        /// <summary>Written by this build's schema; loads as it is.</summary>
        Ready = 0,

        /// <summary>Older schema: loads, and is upgraded on load ("Will be updated").</summary>
        NeedsUpdate = 1,

        /// <summary>Written by a newer build: this build cannot read it.</summary>
        TooNew = 2,

        /// <summary>The header cannot be read or the checksum does not match.</summary>
        Damaged = 3,
    }

    /// <summary>
    /// Phase 19, extended in M1.1: what the save screens show about one save, read from the file's header only
    /// (<see cref="JsonWorldStore.ListSaves"/>); v28 files are also checked against their checksum. Nothing here is used
    /// to load the world.
    /// </summary>
    public sealed class SaveSummary
    {
        public string Slot { get; set; }
        public SaveSlotKind Kind => SaveSlots.KindOf(Slot);
        public string WorldName { get; set; }
        public string Seed { get; set; }

        /// <summary>The world's in-game date as stored ("0412-03-01").</summary>
        public string Date { get; set; }

        public string SavedAtUtc { get; set; }
        public DateTime LastWriteUtc { get; set; }
        public long Bytes { get; set; }
        public int SchemaVersion { get; set; }

        /// <summary>False when the header could not be read (the file may be damaged).</summary>
        public bool Readable { get; set; }

        /// <summary>False when the save is from a newer build than this one.</summary>
        public bool Compatible { get; set; }

        // ----- M1.1: the v28 summary (null or 0 for older saves) ---------------------------------------------

        public string CharacterName { get; set; }
        public string ClassName { get; set; }
        public int Level { get; set; }
        public string LocationText { get; set; }
        public int? MinuteOfDay { get; set; }
        public string CampaignChapter { get; set; }

        /// <summary>Seconds of active play; null when unknown (saves from before v28, until their next save).</summary>
        public double? PlaytimeSeconds { get; set; }

        public string PlayedAtUtc { get; set; }

        public SaveState State { get; set; }

        /// <summary>Why the save is <see cref="SaveState.Damaged"/>, or null.</summary>
        public string Problem { get; set; }

        /// <summary>A backup (<c>.bak</c>) exists and itself reads as Ready or Will be updated.</summary>
        public bool HasGoodBackup { get; set; }

        /// <summary>The thumbnail beside the save, or null when there is none.</summary>
        public string ThumbnailPath { get; set; }

        public bool CanLoad => State == SaveState.Ready || State == SaveState.NeedsUpdate;

        /// <summary>
        /// When it was last played, for ordering and Continue: the summary's playedAtUtc, else the header's savedAtUtc,
        /// else the file's write time.
        /// </summary>
        public DateTime LastPlayedUtc
        {
            get
            {
                if (TryParseUtc(PlayedAtUtc, out var played)) return played;
                if (TryParseUtc(SavedAtUtc, out var saved)) return saved;
                return LastWriteUtc;
            }
        }

        /// <summary>"Ready", "Will be updated", "Too new for this build", "Damaged (backup available)" or "Damaged".</summary>
        public string StateText =>
            State == SaveState.Ready ? "Ready" :
            State == SaveState.NeedsUpdate ? "Will be updated" :
            State == SaveState.TooNew ? "Too new for this build" :
            HasGoodBackup ? "Damaged (backup available)" : "Damaged";

        private static bool TryParseUtc(string text, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrEmpty(text)) return false;
            if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)) return false;
            utc = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            return true;
        }
    }
}
