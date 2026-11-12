using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.Quests
{
    // Phase 12 (ADR-0059, ADR-0060): what the player is to people, factions and places, and what the player knows.
    // Personal regard lives on each person (PlayerRegardData); faction standing, settlement reputation, notoriety,
    // the reputation ledger, discoveries, rumours and decisions live here, on the player.

    /// <summary>The dimensions of one person's regard for the player (ADR-0059). Familiarity and affinity date from Phase 4.</summary>
    public enum RegardDimension
    {
        Familiarity = 0,

        /// <summary>Liking: −100..100.</summary>
        Affinity = 1,

        Trust = 2,
        Respect = 3,
        Fear = 4,
        Gratitude = 5,
        Hostility = 6,
    }

    /// <summary>Presentation tiers (ADR-0059): read from the dimensions, never stored, never one universal number.</summary>
    public enum ReputationTier
    {
        Unknown = 0,
        Recognized = 1,
        Respected = 2,
        Trusted = 3,
        Honored = 4,
        Feared = 5,
        Hostile = 6,
    }

    public sealed class FactionStandingData
    {
        public FactionId FactionId { get; set; }

        /// <summary>−100..100.</summary>
        public int Standing { get; set; }

        public FactionStandingData Clone() => (FactionStandingData)MemberwiseClone();
    }

    public sealed class SettlementStandingData
    {
        public SettlementId SettlementId { get; set; }

        /// <summary>−100..100: how the settlement's people regard the player.</summary>
        public int Reputation { get; set; }

        public SettlementStandingData Clone() => (SettlementStandingData)MemberwiseClone();
    }

    /// <summary>One change to the player's reputation, keyed so it is applied once (ADR-0059).</summary>
    public sealed class ReputationChangeData
    {
        public string Key { get; set; }
        public long At { get; set; }

        /// <summary>"npc:{id}", "faction:{id}", "settlement:{id}" or "notoriety".</summary>
        public string Target { get; set; }

        public string Dimension { get; set; }
        public int Amount { get; set; }
        public string Reason { get; set; }

        public ReputationChangeData Clone() => (ReputationChangeData)MemberwiseClone();
    }

    /// <summary>
    /// Phase 18 (ADR-0088): what kind of thing a discovery is. Each kind has a use: the journal groups by it, it decides
    /// who in the world cares to hear it (an elder for history, a guard for danger, a merchant for trade), and companions
    /// with a matching interest remark on it. <see cref="None"/> is for facts that only a quest reads.
    /// </summary>
    public enum DiscoveryCategory
    {
        None = 0,
        Historical = 1,
        Geographic = 2,
        Cultural = 3,
        Faction = 4,
        Religious = 5,
        Natural = 6,
        Economic = 7,
        Personal = 8,
        Dangerous = 9,
        Arcane = 10,
    }

    /// <summary>
    /// Phase 18 (ADR-0088): how sure the player is. Rumours are a separate list (heard, not seen). An unverified
    /// observation is something the player saw but could not be sure of; it becomes confirmed when what confirms it is
    /// found. Old saves load as confirmed (the default), as everything they recorded was.
    /// </summary>
    public enum DiscoveryCertainty
    {
        Confirmed = 0,
        Unverified = 1,
    }

    /// <summary>Something the player has found out (a fact, a clue, a place).</summary>
    public sealed class DiscoveryData
    {
        public string Key { get; set; }
        public string Text { get; set; }
        public long At { get; set; }

        /// <summary>Where it came from ("npc:{id}", "object:{key}", "item:{id}", "visit").</summary>
        public string Source { get; set; }

        /// <summary>Phase 18: the kind of discovery (None for older or quest-only facts).</summary>
        public DiscoveryCategory Category { get; set; }

        /// <summary>Phase 18: whether the player has confirmed it.</summary>
        public DiscoveryCertainty Certainty { get; set; }

        /// <summary>Phase 18: the place it was found in, or null.</summary>
        public string PlaceId { get; set; }

        /// <summary>Phase 18: who it concerns ("faction:{id}", "npc:{id}"), or null.</summary>
        public string Subject { get; set; }

        /// <summary>Phase 18: a discovery that matters (an authored story fact): it gives experience once, and is kept when the list is full.</summary>
        public bool Major { get; set; }

        /// <summary>Phase 18: for an unverified observation, what confirms it (a discovery key, or "group:{name}" for the creatures themselves seen).</summary>
        public string ConfirmedBy { get; set; }

        /// <summary>Phase 18: the settlements whose people have been told it (each tells once).</summary>
        public List<string> SharedIn { get; set; } = new List<string>();

        public DiscoveryData Clone()
        {
            var c = (DiscoveryData)MemberwiseClone();
            c.SharedIn = new List<string>(SharedIn);
            return c;
        }
    }

    /// <summary>Something the player has heard, unverified (ADR-0060).</summary>
    public sealed class RumorData
    {
        public string Key { get; set; }
        public string Text { get; set; }
        public long At { get; set; }
        public NpcId SourceNpcId { get; set; }

        /// <summary>0–100.</summary>
        public int Confidence { get; set; }

        /// <summary>Confirmed true, or shown false, by what the player found.</summary>
        public bool Verified { get; set; }

        public RumorData Clone() => (RumorData)MemberwiseClone();
    }

    /// <summary>A choice the player made that matters (shown under "Recent decisions").</summary>
    public sealed class DecisionData
    {
        public string Key { get; set; }
        public string Text { get; set; }
        public long At { get; set; }
        public string QuestInstanceId { get; set; }

        public DecisionData Clone() => (DecisionData)MemberwiseClone();
    }

    /// <summary>The player's social and discovery state (saved with the player; ADR-0059, ADR-0060).</summary>
    public sealed class PlayerSocialData
    {
        public const int MaxLedger = 300;
        public const int MaxDiscoveries = 400; // Phase 18: clues now record what they tell (was 200)
        public const int MaxRumors = 60;
        public const int MaxDecisions = 80;
        public const int MaxAppliedKeys = 600;

        public List<FactionStandingData> FactionStandings { get; set; } = new List<FactionStandingData>();
        public List<SettlementStandingData> SettlementStandings { get; set; } = new List<SettlementStandingData>();

        /// <summary>0–100: how widely the player's darker deeds are known.</summary>
        public int Notoriety { get; set; }

        public List<ReputationChangeData> Ledger { get; set; } = new List<ReputationChangeData>();

        /// <summary>Location and settlement IDs the player knows of (told, read, or seen).</summary>
        public List<string> KnownPlaces { get; set; } = new List<string>();

        /// <summary>Settlements the player has been to.</summary>
        public List<SettlementId> VisitedSettlements { get; set; } = new List<SettlementId>();

        public List<DiscoveryData> Discoveries { get; set; } = new List<DiscoveryData>();
        public List<RumorData> Rumors { get; set; } = new List<RumorData>();
        public List<DecisionData> Decisions { get; set; } = new List<DecisionData>();

        /// <summary>Keys of one-time dialogue consequences already applied (bounded; the oldest fall away first).</summary>
        public List<string> AppliedKeys { get; set; } = new List<string>();

        public int StandingWith(FactionId f)
        {
            foreach (var s in FactionStandings) if (s.FactionId == f) return s.Standing;
            return 0;
        }

        public int ReputationIn(SettlementId s)
        {
            foreach (var r in SettlementStandings) if (r.SettlementId == s) return r.Reputation;
            return 0;
        }

        public bool Knows(string key)
        {
            foreach (var d in Discoveries) if (d.Key == key) return true;
            return false;
        }

        public DiscoveryData FindDiscovery(string key)
        {
            foreach (var d in Discoveries) if (d.Key == key) return d;
            return null;
        }

        /// <summary>Phase 18: keeps the list in bounds, dropping the oldest minor discovery first (major ones stay while any minor one is left).</summary>
        public void TrimDiscoveries()
        {
            while (Discoveries.Count > MaxDiscoveries)
            {
                var drop = Discoveries.FindIndex(d => !d.Major);
                Discoveries.RemoveAt(drop >= 0 ? drop : 0);
            }
        }

        public bool Applied(string key) => key != null && AppliedKeys.Contains(key);

        public PlayerSocialData Clone()
        {
            var c = (PlayerSocialData)MemberwiseClone();
            c.FactionStandings = FactionStandings.ConvertAll(x => x.Clone());
            c.SettlementStandings = SettlementStandings.ConvertAll(x => x.Clone());
            c.Ledger = Ledger.ConvertAll(x => x.Clone());
            c.KnownPlaces = new List<string>(KnownPlaces);
            c.VisitedSettlements = new List<SettlementId>(VisitedSettlements);
            c.Discoveries = Discoveries.ConvertAll(x => x.Clone());
            c.Rumors = Rumors.ConvertAll(x => x.Clone());
            c.Decisions = Decisions.ConvertAll(x => x.Clone());
            c.AppliedKeys = new List<string>(AppliedKeys);
            return c;
        }
    }

    /// <summary>The tiers as words, from the dimensions (configurable thresholds; ADR-0059).</summary>
    public sealed class ReputationThresholds
    {
        public int Recognized { get; set; } = 10;
        public int Respected { get; set; } = 40;
        public int Trusted { get; set; } = 55;
        public int Honored { get; set; } = 80;
        public int Feared { get; set; } = 50;
        public int Hostile { get; set; } = 50;

        public static readonly ReputationThresholds Default = new ReputationThresholds();

        /// <summary>A person's view of the player. Hostility and fear outrank warmth: someone who hates you is not "respected".</summary>
        public ReputationTier ForPerson(int familiarity, int affinity, int trust, int respect, int fear, int gratitude, int hostility)
        {
            if (hostility >= Hostile || affinity <= -Hostile) return ReputationTier.Hostile;
            if (fear >= Feared && fear > respect) return ReputationTier.Feared;
            if (familiarity < Recognized && gratitude == 0 && respect == 0) return ReputationTier.Unknown;
            if (trust >= Honored && respect >= Honored - 10 || gratitude >= Honored) return ReputationTier.Honored;
            if (trust >= Trusted) return ReputationTier.Trusted;
            if (respect >= Respected || gratitude >= Respected) return ReputationTier.Respected;
            return ReputationTier.Recognized;
        }

        /// <summary>A faction's or settlement's view (one number each, −100..100).</summary>
        public ReputationTier ForStanding(int standing, int notoriety = 0)
        {
            if (standing <= -Hostile) return ReputationTier.Hostile;
            if (notoriety >= Feared && standing < Respected) return ReputationTier.Feared;
            if (standing >= Honored) return ReputationTier.Honored;
            if (standing >= Trusted) return ReputationTier.Trusted;
            if (standing >= Respected) return ReputationTier.Respected;
            if (standing >= Recognized || standing <= -Recognized) return ReputationTier.Recognized;
            return ReputationTier.Unknown;
        }
    }

    /// <summary>How tagged content is presented (ADR-0061): shown as written, softened (its gentler variant), or left out.</summary>
    public enum ContentPresentation
    {
        Show = 0,
        Soften = 1,
        Hide = 2,
    }

    public sealed class ContentSettingsData
    {
        /// <summary>Per tag; tags not listed are shown. Defaults keep everything shown except what the owner later restricts.</summary>
        public Dictionary<ContentTags, ContentPresentation> Rules { get; set; } = new Dictionary<ContentTags, ContentPresentation>();

        public ContentPresentation For(ContentTags tags)
        {
            var result = ContentPresentation.Show;
            foreach (ContentTags t in Enum.GetValues(typeof(ContentTags)))
            {
                if (t == ContentTags.None || (tags & t) == 0 || !Rules.TryGetValue(t, out var p)) continue;
                if (p > result) result = p;
            }

            return result;
        }

        public ContentSettingsData Clone() => new ContentSettingsData { Rules = new Dictionary<ContentTags, ContentPresentation>(Rules) };
    }
}
