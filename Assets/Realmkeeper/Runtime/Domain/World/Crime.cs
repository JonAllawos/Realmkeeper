using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>Phase 14 (ADR-0069): the crimes the world keeps track of. Not a legal code: local, witnessed acts.</summary>
    public enum CrimeKind
    {
        /// <summary>Taking from a container that belongs to someone.</summary>
        Theft = 0,

        /// <summary>Taking from a business's stock.</summary>
        ShopTheft = 1,

        /// <summary>Getting into a locked home or building without leave (picked or forced).</summary>
        UnauthorizedEntry = 2,

        /// <summary>Being where one has been told not to be (a restricted room, after a warning).</summary>
        Trespass = 3,

        /// <summary>Attacking someone who was not threatening the player.</summary>
        Assault = 4,

        /// <summary>Killing them.</summary>
        Murder = 5,

        /// <summary>Breaking a door or other property that is not the player's.</summary>
        PropertyDamage = 6,
    }

    public enum CrimeStatus
    {
        /// <summary>Nobody knows it happened.</summary>
        Undiscovered = 0,

        /// <summary>Someone found out it happened, but not who did it.</summary>
        Discovered = 1,

        /// <summary>Someone saw the player do it (and recognised them, or described them).</summary>
        Witnessed = 2,

        /// <summary>Set right: the goods returned or the fine paid. Memories stay; the bounty is gone.</summary>
        Settled = 3,
    }

    /// <summary>One person who perceived the act, with how well.</summary>
    public sealed class CrimeWitnessData
    {
        public NpcId NpcId { get; set; }

        /// <summary>They knew who it was (they had met the player, or the player is well known).</summary>
        public bool Recognized { get; set; }

        /// <summary>What they could tell of an unrecognised culprit ("a stranger, an elf, armoured").</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Why they noticed ("in the same room, lit"; "through the open door").</summary>
        public string How { get; set; } = string.Empty;

        public CrimeWitnessData Clone() => (CrimeWitnessData)MemberwiseClone();
    }

    /// <summary>
    /// A crime, kept whether or not anyone knows of it (the world's truth). What people know lives in their own
    /// memories and knowledge, so knowledge spreads by the existing hearsay and stays local; this record only says what
    /// happened and what has been applied, so nothing is applied twice.
    /// </summary>
    public sealed class CrimeRecordData
    {
        public string CrimeId { get; set; }
        public CrimeKind Kind { get; set; }
        public CrimeStatus Status { get; set; }

        /// <summary>Absolute world minute.</summary>
        public long At { get; set; }

        public SettlementId SettlementId { get; set; }
        public string PlaceId { get; set; }
        public string Room { get; set; }

        /// <summary>The person wronged (an owner, the one attacked), or empty.</summary>
        public NpcId VictimNpcId { get; set; }

        /// <summary>The faction wronged (a garrison's), or empty.</summary>
        public FactionId VictimFactionId { get; set; }

        /// <summary>What was done, in words ("took a silver ring from the Thornes' chest").</summary>
        public string Detail { get; set; } = string.Empty;

        /// <summary>The goods taken (item IDs and quantities), so they can be given back.</summary>
        public Dictionary<string, int> Taken { get; set; } = new Dictionary<string, int>();

        public int Coin { get; set; }

        /// <summary>Coin owed to settle it (0 until the player is known to have done it).</summary>
        public int Bounty { get; set; }

        public List<CrimeWitnessData> Witnesses { get; set; } = new List<CrimeWitnessData>();

        /// <summary>When it was found out (−1 while undiscovered).</summary>
        public long DiscoveredAt { get; set; } = -1;

        public CrimeRecordData Clone()
        {
            var c = (CrimeRecordData)MemberwiseClone();
            c.Taken = new Dictionary<string, int>(Taken);
            c.Witnesses = new List<CrimeWitnessData>();
            foreach (var w in Witnesses) c.Witnesses.Add(w.Clone());
            return c;
        }

        public bool KnownToBeThePlayer => Status == CrimeStatus.Witnessed && Witnesses.Exists(w => w.Recognized);
    }
}
