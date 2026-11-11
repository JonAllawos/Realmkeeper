using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>Why an encounter arose (Phase 10, ADR-0046).</summary>
    public enum EncounterType
    {
        Wildlife = 0,
        Predators = 1,
        BanditAmbush = 2,
        Patrol = 3,
        HostileCreatures = 4,
        DefendFarm = 5,
        Vermin = 6,
    }

    /// <summary>What counts as success.</summary>
    public enum EncounterObjective
    {
        /// <summary>Defeat or drive off every hostile.</summary>
        Defeat = 0,

        /// <summary>Keep a named person standing until the hostiles are gone.</summary>
        Protect = 1,

        /// <summary>Get away alive (withdraw) — the opposition is beyond the party.</summary>
        Escape = 2,

        /// <summary>Bring down at least one of the game before it flees.</summary>
        Hunt = 3,
    }

    /// <summary>
    /// Where a generated encounter came from and what it is for (persisted with the encounter): its type, the world
    /// context it was drawn from, the populations its creatures belong to, its objective and difficulty, and — once it
    /// is over — what it did to the world, applied exactly once.
    /// </summary>
    public sealed class EncounterOriginData
    {
        public EncounterType Type { get; set; }
        public EncounterObjective Objective { get; set; }

        /// <summary>The person to protect (Protect), or empty.</summary>
        public NpcId ProtectedNpcId { get; set; }

        public SettlementId NearSettlementId { get; set; }

        /// <summary>The place, in words ("the forested edge of Westerthorpe, at dusk").</summary>
        public string Context { get; set; }

        /// <summary>The terrain the fight is on ("Forest", "Plains", "Road" …).</summary>
        public string Terrain { get; set; }

        /// <summary>Populations (or herds) the creatures were drawn from.</summary>
        public List<string> SourceIds { get; set; } = new List<string>();

        public int XpBudget { get; set; }
        public int XpTotal { get; set; }

        /// <summary>"easy", "moderate", "hard", "deadly" against the budget.</summary>
        public string Difficulty { get; set; }

        /// <summary>The seed the plan came from (the same world state gives the same plan).</summary>
        public ulong PlanSeed { get; set; }

        /// <summary>True once the consequences have been applied (they never are twice).</summary>
        public bool Resolved { get; set; }

        /// <summary>The consequences, in words, once resolved.</summary>
        public string Result { get; set; }

        public bool ObjectiveMet { get; set; }

        /// <summary>Phase 13 (ADR-0064): the place and inhabitant group the fight is with, and the room it is in (null for outings).</summary>
        public string PlaceId { get; set; }
        public string GroupId { get; set; }
        public string Room { get; set; }

        public EncounterOriginData Clone()
        {
            var c = (EncounterOriginData)MemberwiseClone();
            c.SourceIds = new List<string>(SourceIds);
            return c;
        }
    }
}
