using System;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>
    /// Phase 19: what the world-selection screen shows about one save, read from the file's header only
    /// (<see cref="JsonWorldStore.ListSaves"/>). Nothing here is used to load the world.
    /// </summary>
    public sealed class SaveSummary
    {
        public string Slot { get; set; }
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
    }
}
