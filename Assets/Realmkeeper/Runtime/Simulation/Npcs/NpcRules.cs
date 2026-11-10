using System;
using Realmkeeper.Domain.Randomness;

namespace Realmkeeper.Simulation.Npcs
{
    /// <summary>Tunable numbers of the NPC engine. Changing them changes behaviour, never saved data.</summary>
    public sealed class NpcRules
    {
        /// <summary>Known facts older than this become merely believed (they may be out of date).</summary>
        public int StaleAfterDays { get; set; } = 14;

        /// <summary>Rumours older than this are forgotten.</summary>
        public int RumourLifetimeDays { get; set; } = 45;

        /// <summary>Confidence lost each time a fact is passed on.</summary>
        public int RetellingConfidenceLoss { get; set; } = 20;

        /// <summary>Facts below this confidence are not passed on.</summary>
        public int MinShareConfidence { get; set; } = 30;

        /// <summary>A fact travels at most this many tellings from its observation.</summary>
        public int MaxHops { get; set; } = 3;

        /// <summary>Weekly salience loss of memories; memories below <see cref="ForgetBelowSalience"/> are forgotten.</summary>
        public int MemoryDecayPerWeek { get; set; } = 6;

        public int ForgetBelowSalience { get; set; } = 8;

        /// <summary>Conversations at one location per day (pairs), so cost grows with places, not people squared.</summary>
        public int ConversationsPerLocationPerDay { get; set; } = 2;

        /// <summary>Evening conversations between neighbours, per settlement per day (scales with people: one per six).</summary>
        public int EveningConversationsPerSettlement { get; set; } = 1;

        /// <summary>Weekly drift of affinity back toward neutral for ties that are not kept up.</summary>
        public int AffinityDriftPerWeek { get; set; } = 1;

        /// <summary>Affinity at which acquaintances become friends, and below which they become rivals.</summary>
        public int FriendAffinity { get; set; } = 40;

        public int RivalAffinity { get; set; } = -35;

        /// <summary>Coin earned per working day at skill 0 (plus skill / 25).</summary>
        public int BaseWage { get; set; } = 1;
    }

    /// <summary>
    /// Order-independent deterministic choices: every decision hashes (world seed, date, person,
    /// purpose), so no shared random stream is consumed and iteration order cannot change results.
    /// </summary>
    public static class NpcHash
    {
        public static ulong Of(long worldSeed, string purpose, string subject, long day = 0) =>
            SeedMixer.Combine(SeedMixer.Combine(SeedMixer.FromWorldSeed(worldSeed), purpose), SeedMixer.Combine(SeedMixer.HashString(subject), unchecked((ulong)day)));

        /// <summary>0 … max−1.</summary>
        public static int Roll(ulong seed, int max) => max <= 1 ? 0 : (int)(SeedMixer.Mix(seed) % (ulong)max);

        /// <summary>min … max inclusive.</summary>
        public static int Range(ulong seed, int min, int max) => min + Roll(seed, max - min + 1);

        public static ulong Next(ulong seed, int n) => SeedMixer.Combine(seed, unchecked((ulong)n));

        /// <summary>A stable GUID from a seed (for IDs created by the engine).</summary>
        public static Guid Guid(ulong seed)
        {
            var a = SeedMixer.Mix(seed);
            var b = SeedMixer.Mix(a ^ 0xA5A5A5A5A5A5A5A5UL);
            var bytes = new byte[16];
            BitConverter.GetBytes(a).CopyTo(bytes, 0);
            BitConverter.GetBytes(b).CopyTo(bytes, 8);
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40); // version 4 layout
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
            return new Guid(bytes);
        }
    }
}
