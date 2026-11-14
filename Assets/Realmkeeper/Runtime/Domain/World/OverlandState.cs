using System.Collections.Generic;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    /// <summary>Phase 24 (ADR-0114): the kinds of landmark the country holds (an initial, coherent set).</summary>
    public enum LandmarkKind
    {
        /// <summary>A marker where roads meet or along a long road: its inscription names where the road goes and how far.</summary>
        Waystone = 0,

        /// <summary>A heap of stones on a hilltop: climbing it shows what lies around.</summary>
        Cairn = 1,

        /// <summary>A used camping place by a road: a safe place to rest.</summary>
        TravellersCamp = 2,

        /// <summary>A camp left in a hurry, off the road: searched once.</summary>
        AbandonedCamp = 3,

        /// <summary>A ruined farmstead in the open country: searched once.</summary>
        RuinedHomestead = 4,

        /// <summary>A faction's post where a road crosses into another region; patrols keep the road near it safer.</summary>
        WatchPost = 5,

        /// <summary>A wrecked wagon where a real caravan was lost (a world event); evidence of what happened.</summary>
        Wreck = 6,
    }

    /// <summary>
    /// Phase 24: a landmark as generated (from the region's seed and geography, or from a world event for a wreck). Derived
    /// data: never saved; only its <see cref="LandmarkStateData"/> is.
    /// </summary>
    public sealed class OverlandLandmark
    {
        /// <summary>Stable id: "landmark:{region}:{kind}:{n}" (or "landmark:wreck:{event}").</summary>
        public string Id { get; set; }

        public LandmarkKind Kind { get; set; }
        public string Name { get; set; }
        public string RealmId { get; set; }
        public RegionId RegionId { get; set; }
        public MapCoord Cell { get; set; }

        /// <summary>Region metres.</summary>
        public float X { get; set; }

        public float Z { get; set; }

        /// <summary>What it says or shows (an inscription, a description).</summary>
        public string Text { get; set; }

        /// <summary>The route it stands on (waystones, camps, wrecks, watch posts), or null.</summary>
        public string RouteId { get; set; }

        /// <summary>A wreck's world event; a watch post's faction.</summary>
        public string EventId { get; set; }

        public FactionId FactionId { get; set; }

        /// <summary>Whether it can be searched (once).</summary>
        public bool Searchable => Kind == LandmarkKind.AbandonedCamp || Kind == LandmarkKind.RuinedHomestead || Kind == LandmarkKind.Wreck;
    }

    /// <summary>Phase 24 (ADR-0114): what the player has done at a landmark. Saved; the landmark itself is regenerated.</summary>
    public sealed class LandmarkStateData
    {
        public string LandmarkId { get; set; }
        public bool Found { get; set; }
        public long FoundAt { get; set; }
        public bool Searched { get; set; }

        /// <summary>The last day the player rested here (a camp), or -1.</summary>
        public long RestedDay { get; set; } = -1;

        public LandmarkStateData Clone() => (LandmarkStateData)MemberwiseClone();
    }

    public enum OverlandEncounterKind
    {
        /// <summary>A creature of a population whose range covers the place (wolves on the moor).</summary>
        Creature = 0,

        /// <summary>A bandit band holding the road the player walks.</summary>
        Bandits = 1,

        /// <summary>A travelling group met on the road (merchants, pilgrims, refugees, a patrol): talk, help, pass.</summary>
        Travellers = 2,
    }

    public enum OverlandEncounterState
    {
        /// <summary>Met, waiting on the player's choice.</summary>
        Pending = 0,

        Fought = 1,

        /// <summary>Slipped past or backed off.</summary>
        Avoided = 2,

        /// <summary>Paid off (bandits).</summary>
        Paid = 3,

        /// <summary>Spoken with, helped or let pass (travellers).</summary>
        Settled = 4,

        /// <summary>The player walked away and it was left.</summary>
        Left = 5,
    }

    /// <summary>
    /// Phase 24 (ADR-0114): an encounter met in the country. Its id is its deterministic bucket, so loading never rolls it
    /// again; its outcome is kept (the 40 most recent).
    /// </summary>
    public sealed class OverlandEncounterData
    {
        /// <summary>"encounter:{region}:{bucket}" (or "travellers:{group}:{day}").</summary>
        public string Id { get; set; }

        public OverlandEncounterKind Kind { get; set; }
        public OverlandEncounterState State { get; set; }
        public RegionId RegionId { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public long At { get; set; }

        /// <summary>The creature population or the bandits' band (a population id).</summary>
        public string PopulationId { get; set; }

        /// <summary>The travelling group, when it is one.</summary>
        public string GroupId { get; set; }

        /// <summary>What it is, for the prompt ("Wolves have picked up your scent").</summary>
        public string Text { get; set; }

        /// <summary>How it ended, in a line.</summary>
        public string Outcome { get; set; }

        /// <summary>The fight it became, when fought.</summary>
        public string EncounterId { get; set; }

        public OverlandEncounterData Clone() => (OverlandEncounterData)MemberwiseClone();
    }

    /// <summary>Phase 24 (ADR-0114): the changes the player has made in the country, and the encounter clock.</summary>
    public sealed class OverlandStateData
    {
        public const int MaxEncounters = 40;

        public List<LandmarkStateData> Landmarks { get; set; } = new List<LandmarkStateData>();
        public List<OverlandEncounterData> Encounters { get; set; } = new List<OverlandEncounterData>();

        /// <summary>The last encounter bucket checked (absolute half-hours walked in), so a bucket is checked once.</summary>
        public long LastCheckBucket { get; set; } = -1;

        /// <summary>Absolute minutes of the last encounter met (for the cooldown), or -1.</summary>
        public long LastEncounterAt { get; set; } = -1;

        public LandmarkStateData FindLandmark(string id) => Landmarks.Find(l => l.LandmarkId == id);
        public OverlandEncounterData FindEncounter(string id) => Encounters.Find(e => e.Id == id);

        /// <summary>The pending encounter, if any (at most one at a time).</summary>
        public OverlandEncounterData Pending => Encounters.Find(e => e.State == OverlandEncounterState.Pending);

        public void Add(OverlandEncounterData e)
        {
            Encounters.Add(e);
            while (Encounters.Count > MaxEncounters)
            {
                var i = Encounters.FindIndex(x => x.State != OverlandEncounterState.Pending);
                if (i < 0) break;
                Encounters.RemoveAt(i);
            }
        }

        public OverlandStateData Clone()
        {
            var c = (OverlandStateData)MemberwiseClone();
            c.Landmarks = new List<LandmarkStateData>();
            foreach (var l in Landmarks) c.Landmarks.Add(l.Clone());
            c.Encounters = new List<OverlandEncounterData>();
            foreach (var e in Encounters) c.Encounters.Add(e.Clone());
            return c;
        }
    }
}
