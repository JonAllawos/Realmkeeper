using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;

namespace Realmkeeper.Application
{
    // Phase 4 read side: immutable snapshots of named people for the HUD and exploration, and the
    // dialogue that answers questions from a person's own knowledge. Nothing here changes the world.

    /// <summary>Filter for the NPC list. Null or empty fields match everything.</summary>
    public sealed class NpcFilter
    {
        public SettlementId Settlement { get; set; }
        public NpcRole? Role { get; set; }
        public NpcTier? Tier { get; set; }
        public FactionId Faction { get; set; }
        public string NameContains { get; set; }
        public bool IncludeDeceased { get; set; }
    }

    /// <summary>One line per person in the NPC list.</summary>
    public sealed class NpcListItem
    {
        internal NpcListItem(NpcData n, string settlement, string faction, string location, WorldDate today)
        {
            NpcId = n.NpcId;
            Name = n.Name;
            Role = n.Role;
            Tier = n.Tier;
            Sex = n.Sex;
            Age = n.AgeOn(today);
            IsAlive = n.IsAlive;
            SettlementId = n.HomeSettlementId;
            SettlementName = settlement ?? "-";
            FactionId = n.FactionId;
            FactionName = faction ?? "none";
            LocationName = location ?? "-";
            Morale = n.Condition.Morale;
        }

        public NpcId NpcId { get; }
        public string Name { get; }
        public NpcRole Role { get; }
        public NpcTier Tier { get; }
        public NpcSex Sex { get; }
        public int Age { get; }
        public bool IsAlive { get; }
        public SettlementId SettlementId { get; }
        public string SettlementName { get; }
        public FactionId FactionId { get; }
        public string FactionName { get; }

        /// <summary>Where they are today.</summary>
        public string LocationName { get; }

        public int Morale { get; }
    }

    /// <summary>A named line with a value and a short explanation, for any inspector section.</summary>
    public sealed class NpcLine
    {
        public NpcLine(string label, string value, string why = "")
        {
            Label = label;
            Value = value;
            Why = why ?? string.Empty;
        }

        public string Label { get; }
        public string Value { get; }
        public string Why { get; }
    }

    /// <summary>
    /// Everything the NPC inspector shows, in ten sections: identity, personality, skills,
    /// condition, goals, whereabouts and schedule, relationships, memories, knowledge, history.
    /// Each section has an explanation of where its values come from.
    /// </summary>
    public sealed class NpcDetail
    {
        public NpcId NpcId { get; internal set; }
        public string Name { get; internal set; }
        public string Subtitle { get; internal set; }
        public bool IsAlive { get; internal set; }

        public IReadOnlyList<NpcLine> Identity { get; internal set; }
        public IReadOnlyList<NpcLine> Personality { get; internal set; }
        public IReadOnlyList<NpcLine> Skills { get; internal set; }
        public IReadOnlyList<NpcLine> Condition { get; internal set; }
        public IReadOnlyList<NpcLine> Goals { get; internal set; }
        public IReadOnlyList<NpcLine> Whereabouts { get; internal set; }
        public IReadOnlyList<NpcLine> Relationships { get; internal set; }
        public IReadOnlyList<NpcLine> Memories { get; internal set; }
        public IReadOnlyList<NpcLine> Knowledge { get; internal set; }
        public IReadOnlyList<NpcLine> History { get; internal set; }

        /// <summary>Section name → explanation of the section.</summary>
        public static readonly IReadOnlyDictionary<string, string> Explanations = new Dictionary<string, string>
        {
            ["Identity"] = "Who they are. Fixed facts; only the DM can change them.",
            ["Personality"] = "Eight traits from 0 to 100 (50 is average). They shape whom they befriend, what they want and how they spend rest days.",
            ["Skills"] = "0 to 100. The skill their work uses sets their wage; finishing a learning goal raises a skill.",
            ["Condition"] = "Needs, from 0 (dire) to 100 (fine). Food follows their settlement's granary; safety follows its stability; morale is the average of the rest.",
            ["Goals"] = "What drives them. Needs open urgent goals; personality opens ambitions. The most urgent goal can send them somewhere other than their routine.",
            ["Whereabouts"] = "Where the world says they are today. Exploration shows them there. A weekly routine decides it unless an urgent goal overrides it.",
            ["Relationships"] = "Only people something connected them to: a shared home, a workplace, a faction, conversations. Each tie records its cause.",
            ["Memories"] = "What they remember, how much it matters (salience, fading weekly) and how they know it (experienced, witnessed or learned).",
            ["Knowledge"] = "What they hold to be true, kept apart from the world's truth: known first-hand, believed (possibly out of date) or rumour. Marked where it no longer matches the world.",
            ["History"] = "Chronicle entries that name them.",
        };
    }

    /// <summary>Conversation topics a player can raise. Answers come only from the person's own knowledge.</summary>
    public enum ConversationTopic
    {
        Greeting = 0,
        Themselves = 1,
        Settlement = 2,
        Governor = 3,
        FactionLeader = 4,
        News = 5,
        Rumours = 6,
        Person = 7,

        // Phase 5
        Shops = 8,
        Goods = 9,
        Prices = 10,
        Factions = 11,
        Work = 12,
    }

    /// <summary>Display text for NPC enums and facts.</summary>
    public static class NpcText
    {
        public static string Goal(string type)
        {
            switch (type)
            {
                case nameof(NpcGoalType.EarnLiving): return "earn a living";
                case nameof(NpcGoalType.SecureFood): return "find enough to eat";
                case nameof(NpcGoalType.Recover): return "recover their health";
                case nameof(NpcGoalType.GainStanding): return "rise in standing";
                case nameof(NpcGoalType.Socialise): return "make friends";
                case nameof(NpcGoalType.ServeFaction): return "serve their faction";
                case nameof(NpcGoalType.AcquireWealth): return "grow rich";
                case nameof(NpcGoalType.LearnSkill): return "learn a skill";
                case nameof(NpcGoalType.SeekSafety): return "find safety";
                default: return "pursue a goal";
            }
        }

        public static string Relation(string kind)
        {
            switch (kind)
            {
                case nameof(NpcRelationKind.Friend): return "friends";
                case nameof(NpcRelationKind.Rival): return "rivals";
                case nameof(NpcRelationKind.Household): return "one household";
                case nameof(NpcRelationKind.Colleague): return "colleagues";
                case nameof(NpcRelationKind.Superior): return "leader and follower";
                default: return "acquainted";
            }
        }

        public static string Cause(string cause)
        {
            switch (cause)
            {
                case nameof(NpcRelationCause.Conversation): return " after talking";
                case nameof(NpcRelationCause.Quarrel): return " after a quarrel";
                case nameof(NpcRelationCause.ManualIntervention): return string.Empty;
                case nameof(NpcRelationCause.SharedHardship): return " through shared hardship";
                default: return string.Empty;
            }
        }

        public static string Topic(string topic)
        {
            switch (topic)
            {
                case nameof(KnowledgeTopic.SettlementFood): return "food";
                case nameof(KnowledgeTopic.SettlementProsperity): return "how a settlement is doing";
                case nameof(KnowledgeTopic.SettlementGovernor): return "who governs";
                case nameof(KnowledgeTopic.FactionLeader): return "a faction's leader";
                case nameof(KnowledgeTopic.Whereabouts): return "someone's whereabouts";
                case nameof(KnowledgeTopic.RecentEvent): return "recent events";
                case nameof(ConversationTopic.Themselves): return "themselves";
                case nameof(ConversationTopic.Settlement): return "the settlement";
                case nameof(ConversationTopic.Governor): return "who governs";
                case nameof(ConversationTopic.News): return "the news";
                case nameof(ConversationTopic.Rumours): return "rumours";
                case nameof(ConversationTopic.Person): return "someone";
                case nameof(ConversationTopic.Shops): return "the shops";
                case nameof(ConversationTopic.Goods): return "what's for sale";
                case nameof(ConversationTopic.Prices): return "prices";
                case nameof(ConversationTopic.Factions): return "the factions";
                case nameof(ConversationTopic.Work): return "their work";
                default: return "this and that";
            }
        }

        public static string Trait(int value) => value >= 75 ? "very high" : value >= 60 ? "high" : value > 40 ? "average" : value > 25 ? "low" : "very low";

        public static string Need(int value) => value >= 75 ? "good" : value >= 50 ? "fair" : value >= 25 ? "poor" : "dire";

        public static string Food(string value) => value == "starving" ? "starving" : value == "short" ? "short of food" : "well fed";

        public static string Status(KnowledgeStatus s) => s == KnowledgeStatus.Known ? "known" : s == KnowledgeStatus.Believed ? "believed" : "rumour";

        public static string Source(MemorySource s) => s == MemorySource.Experienced ? "experienced" : s == MemorySource.Witnessed ? "witnessed" : "heard of";

        public static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        public static string Signed(int v) => v > 0 ? "+" + I(v) : I(v);
    }

    public sealed partial class WorldSession
    {
        /// <summary>A copy of the committed world, for tests only (exact-state assertions).</summary>
        internal WorldData CurrentWorldSnapshotForTests() => _world?.DeepClone();

        /// <summary>Named people matching a filter, in stable record order.</summary>
        public IReadOnlyList<NpcListItem> GetNpcs(NpcFilter filter = null)
        {
            var list = new List<NpcListItem>();
            if (!HasWorld) return list;
            filter = filter ?? new NpcFilter();
            foreach (var n in _world.Npcs)
            {
                if (!filter.IncludeDeceased && n.IsDeceased) continue;
                if (!filter.Settlement.IsEmpty && n.HomeSettlementId != filter.Settlement) continue;
                if (filter.Role.HasValue && n.Role != filter.Role.Value) continue;
                if (filter.Tier.HasValue && n.Tier != filter.Tier.Value) continue;
                if (!filter.Faction.IsEmpty && n.FactionId != filter.Faction) continue;
                if (!string.IsNullOrWhiteSpace(filter.NameContains) && n.Name.IndexOf(filter.NameContains.Trim(), StringComparison.OrdinalIgnoreCase) < 0) continue;
                list.Add(new NpcListItem(n, NameOf(n.HomeSettlementId), FactionNameOf(n.FactionId), PlaceName(n.CurrentLocationId), _world.CurrentSimulationDate));
            }

            return list;
        }

        /// <summary>Living named people physically in a settlement today (their current location is there).</summary>
        public IReadOnlyList<NpcSummary> GetPeoplePresent(SettlementId settlementId)
        {
            var list = new List<NpcSummary>();
            if (!HasWorld) return list;
            foreach (var n in _world.Npcs)
            {
                if (n.IsDeceased) continue;
                var here = PresentIn(n);
                if (here == settlementId) list.Add(new NpcSummary(n, _world.FindLocation(n.CurrentLocationId)));
            }

            return list;
        }

        /// <summary>The settlement a person is in today: their current location's, or their home's.</summary>
        internal SettlementId PresentIn(NpcData n)
        {
            var l = n.CurrentLocationId.IsEmpty ? null : _world.FindLocation(n.CurrentLocationId);
            return l?.SettlementId ?? n.HomeSettlementId;
        }

        /// <summary>The persistent places of a settlement.</summary>
        public IReadOnlyList<LocationSummary> GetLocations(SettlementId settlementId)
        {
            var list = new List<LocationSummary>();
            if (!HasWorld) return list;
            foreach (var l in _world.Locations) if (l.SettlementId == settlementId) list.Add(new LocationSummary(l));
            return list;
        }

        private string PlaceName(LocationId id)
        {
            var l = id.IsEmpty ? null : _world.FindLocation(id);
            if (l == null) return null;
            var s = _world.FindSettlement(l.SettlementId);
            return s == null ? l.Name : l.Name + ", " + s.Name;
        }

        private string NpcName(NpcId id) => _world?.FindNpc(id)?.Name ?? "someone";

        /// <summary>The full inspector snapshot, or null when no named person has the ID.</summary>
        public NpcDetail GetNpcDetail(NpcId id)
        {
            var n = _world?.FindNpc(id);
            if (n == null) return null;
            var today = _world.CurrentSimulationDate;
            var d = new NpcDetail
            {
                NpcId = n.NpcId,
                Name = n.Name,
                IsAlive = n.IsAlive,
                Subtitle = $"{n.Role} of {NameOf(n.HomeSettlementId) ?? "nowhere"}" + (n.IsDeceased ? $" — died {n.DiedOn}" : string.Empty),
            };

            var identity = new List<NpcLine>
            {
                new NpcLine("Sex", n.Sex == NpcSex.Unspecified ? "-" : n.Sex.ToString()),
                new NpcLine("Age", n.BirthDate.IsValidDate ? NpcText.I(n.AgeOn(today)) + " (born " + n.BirthDate + ")" : "unknown"),
                new NpcLine("Occupation", n.Role.ToString()),
                new NpcLine("Tier", n.Tier == NpcTier.Notable ? "1 — notable" : n.Tier == NpcTier.Resident ? "2 — resident" : "unassigned",
                    n.Tier == NpcTier.Notable ? "Simulated every day." : "Simulated once a week."),
                new NpcLine("Home", NameOf(n.HomeSettlementId) ?? "-"),
                new NpcLine("Faction", FactionNameOf(n.FactionId) ?? "none"),
                new NpcLine("Met you", n.HasMetPlayer ? n.MetPlayerOn + " (" + NpcText.I(n.ConversationCount) + " conversations)" : "not yet"),
            };
            d.Identity = identity;

            var traits = new List<NpcLine>();
            for (var t = 0; t < NpcPersonality.Count; t++)
            {
                var v = n.Personality[(NpcTrait)t];
                traits.Add(new NpcLine(((NpcTrait)t).ToString(), NpcText.I(v) + " (" + NpcText.Trait(v) + ")"));
            }

            d.Personality = traits;

            var skills = new List<NpcLine>();
            var primary = NpcGenesis.PrimarySkill(n.Role);
            for (var k = 0; k < NpcSkills.Count; k++)
            {
                var skill = (NpcSkill)k;
                skills.Add(new NpcLine(skill.ToString(), NpcText.I(n.Skills[skill]), skill == primary ? "Used in their work." : string.Empty));
            }

            d.Skills = skills;

            var c = n.Condition;
            var home = _world.FindSettlement(n.HomeSettlementId);
            var foodWhy = home == null ? string.Empty : "Food in " + home.Name + ": " + NpcText.Food(NpcMind.FoodState(home)) + ".";
            d.Condition = new List<NpcLine>
            {
                new NpcLine("Health", NpcText.I(c.Health) + " (" + NpcText.Need(c.Health) + ")", c.Satiation < 20 ? "Falling: they are going hungry." : string.Empty),
                new NpcLine("Fed", NpcText.I(c.Satiation) + " (" + NpcText.Need(c.Satiation) + ")", foodWhy),
                new NpcLine("Energy", NpcText.I(c.Energy) + " (" + NpcText.Need(c.Energy) + ")", "Work tires; a rest day restores."),
                new NpcLine("Safety", NpcText.I(c.Safety) + " (" + NpcText.Need(c.Safety) + ")", home == null ? string.Empty : "Follows " + home.Name + "'s stability (" + NpcText.I(home.Stability) + ")."),
                new NpcLine("Morale", NpcText.I(c.Morale) + " (" + NpcText.Need(c.Morale) + ")", "The average of their needs, lifted by goals near completion."),
                new NpcLine("Savings", NpcText.I(c.Wealth) + " coin", n.WorkplaceId.IsEmpty ? "No workplace: no wage." : "Wage from work, less living costs."),
            };

            var goals = new List<NpcLine>();
            var ordered = new List<NpcGoalData>(n.Goals);
            ordered.Sort((a, b) => a.IsActive != b.IsActive ? (a.IsActive ? -1 : 1) : b.Priority.CompareTo(a.Priority));
            foreach (var g in ordered)
            {
                var state = g.IsActive ? "priority " + NpcText.I(g.Priority) + ", " + NpcText.I(g.Progress) + "% done" : g.Status.ToString().ToLowerInvariant() + " " + g.ClosedOn;
                goals.Add(new NpcLine(NpcText.Goal(g.Type.ToString()), state, GoalWhy(n, g)));
            }

            d.Goals = goals;

            var where = new List<NpcLine>
            {
                new NpcLine("Today", PlaceName(n.CurrentLocationId) ?? "-", WhereWhy(n, today)),
                new NpcLine("Home", PlaceName(n.ResidenceId) ?? "-"),
                new NpcLine("Works at", PlaceName(n.WorkplaceId) ?? "-"),
            };
            for (var day = 0; day < NpcScheduleData.Days; day++)
            {
                var date = today.IsValidDate ? today.AddDays((day - NpcScheduleData.WeekdayOf(today) + NpcScheduleData.Days) % NpcScheduleData.Days) : today;
                where.Add(new NpcLine("Day " + NpcText.I(day + 1) + (day == NpcScheduleData.WeekdayOf(today) ? " (today)" : string.Empty),
                    _world.FindLocation(n.Schedule[day])?.Name ?? "-"));
            }

            d.Whereabouts = where;

            var relations = new List<NpcRelationData>();
            foreach (var r in _world.NpcRelations) if (r.Involves(n.NpcId)) relations.Add(r);
            relations.Sort((a, b) => Math.Abs(b.Affinity).CompareTo(Math.Abs(a.Affinity)));
            var rel = new List<NpcLine>();
            foreach (var r in relations)
            {
                rel.Add(new NpcLine(NpcName(r.Other(n.NpcId)),
                    r.Kind + ": affinity " + NpcText.Signed(r.Affinity) + ", trust " + NpcText.I(r.Trust) + ", respect " + NpcText.I(r.Respect) +
                    (r.Fear > 0 ? ", fear " + NpcText.I(r.Fear) : string.Empty) + (r.Rivalry > 0 ? ", rivalry " + NpcText.I(r.Rivalry) : string.Empty),
                    "Since " + r.Since + " (" + r.Origin + "); last changed by " + r.LastCause + ", " + NpcText.I(r.Interactions) + " conversations."));
            }

            d.Relationships = rel;

            var memories = new List<NpcMemoryData>(n.Memories);
            memories.Sort((a, b) => b.Salience != a.Salience ? b.Salience.CompareTo(a.Salience) : b.Date.CompareTo(a.Date));
            var mem = new List<NpcLine>();
            foreach (var m in memories)
            {
                mem.Add(new NpcLine(m.Date.ToString(), MemoryText(m),
                    m.Category + ", " + NpcText.Source(m.Source) + ", salience " + NpcText.I(m.Salience) + ", confidence " + NpcText.I(m.Confidence)));
            }

            d.Memories = mem;

            var know = new List<NpcLine>();
            foreach (var k in n.Knowledge)
            {
                var truth = TruthOf(k);
                var mismatch = truth != null && truth != k.Value;
                var from = k.SourceNpcId.IsEmpty ? "seen first-hand" : "heard from " + NpcName(k.SourceNpcId) + (k.Hops > 1 ? " (" + NpcText.I(k.Hops) + " tellings away)" : string.Empty);
                know.Add(new NpcLine(NpcText.Topic(k.Topic.ToString()) + " — " + SubjectName(k),
                    ValueText(k.Topic, k.Value) + (mismatch ? "  ✗ no longer true (now: " + ValueText(k.Topic, truth) + ")" : string.Empty),
                    NpcText.Status(k.Status) + ", confidence " + NpcText.I(k.Confidence) + ", " + from + ", " + k.LearnedOn));
            }

            d.Knowledge = know;

            var history = new List<NpcLine>();
            for (var i = _world.HistoricalEvents.Count - 1; i >= 0 && history.Count < 12; i--)
            {
                var e = _world.HistoricalEvents[i];
                var named = false;
                foreach (var who in e.NpcIds) if (who == n.NpcId) named = true;
                if (!named && e.Get("npc") != n.NpcId.ToString()) continue;
                history.Add(new NpcLine(e.Date.ToString(), EventFormatter.Format(e, NameOf, FactionNameOf), EventFormatter.IsManual(e) ? "Caused by a command." : string.Empty));
            }

            d.History = history;
            return d;
        }

        private string GoalWhy(NpcData n, NpcGoalData g)
        {
            var source = g.Source == ChangeSource.DungeonMaster ? "Set by the DM." : g.Source == ChangeSource.Player ? "Set by you." : string.Empty;
            switch (g.Type)
            {
                case NpcGoalType.SecureFood: return ("Opened because they were hungry (fed " + NpcText.I(n.Condition.Satiation) + "). " + source).Trim();
                case NpcGoalType.Recover: return ("Opened because their health fell to " + NpcText.I(n.Condition.Health) + ". " + source).Trim();
                case NpcGoalType.SeekSafety: return ("Opened because they feel unsafe. " + source).Trim();
                case NpcGoalType.GainStanding: return ("Ambition " + NpcText.I(n.Personality[NpcTrait.Ambition]) + ". " + source).Trim();
                case NpcGoalType.Socialise: return ("Sociability " + NpcText.I(n.Personality[NpcTrait.Sociability]) + "; achieved with three friends. " + source).Trim();
                case NpcGoalType.LearnSkill: return ("Curiosity " + NpcText.I(n.Personality[NpcTrait.Curiosity]) + (g.TargetSkill.HasValue ? "; learning " + g.TargetSkill.Value : string.Empty) + ". " + source).Trim();
                case NpcGoalType.AcquireWealth: return ("Greed " + NpcText.I(n.Personality[NpcTrait.Greed]) + " and savings over 100. " + source).Trim();
                case NpcGoalType.ServeFaction: return ("They lead " + (FactionNameOf(n.FactionId) ?? "a faction") + ". " + source).Trim();
                default: return ("Everyone with work earns a living. " + source).Trim();
            }
        }

        private string WhereWhy(NpcData n, WorldDate today)
        {
            if (n.IsDeceased) return "Dead.";
            var planned = n.Schedule.For(today);
            if (n.CurrentLocationId == planned) return planned == n.WorkplaceId ? "A working day." : "Their rest day.";
            NpcGoalData top = null;
            foreach (var g in n.Goals) if (g.IsActive && (top == null || g.Priority > top.Priority)) top = g;
            if (top != null && top.Priority >= 60) return "Their routine says " + (_world.FindLocation(planned)?.Name ?? "elsewhere") + ", but they need to " + NpcText.Goal(top.Type.ToString()) + ".";
            return "Moved here (by the DM or since their routine changed); their routine says " + (_world.FindLocation(planned)?.Name ?? "elsewhere") + ".";
        }

        private string MemoryText(NpcMemoryData m)
        {
            if (!m.EventId.IsEmpty)
            {
                foreach (var e in _world.HistoricalEvents)
                    if (e.EventId == m.EventId) return EventFormatter.Format(e, NameOf, FactionNameOf);
            }

            var who = m.NpcIds.Count > 0 ? NpcName(m.NpcIds[0]) : null;
            if (m.Category == MemoryCategory.Death && who != null) return who + " died.";
            if (m.Category == MemoryCategory.Conversation) return (m.Subject ?? "A conversation") + ".";
            return (m.Subject ?? m.Category.ToString()) + (who != null ? " (" + who + ")" : string.Empty) + ".";
        }

        private string SubjectName(NpcKnowledgeData k)
        {
            if (Guid.TryParse(k.SubjectId, out var g))
            {
                switch (k.Topic)
                {
                    case KnowledgeTopic.FactionLeader: return FactionNameOf(new FactionId(g)) ?? "a faction";
                    case KnowledgeTopic.Whereabouts: return NpcName(new NpcId(g));
                    case KnowledgeTopic.RecentEvent: return "an event";
                    default: return NameOf(new SettlementId(g)) ?? "a settlement";
                }
            }

            return k.SubjectId ?? "-";
        }

        internal string ValueText(KnowledgeTopic topic, string value)
        {
            if (value == null) return "-";
            switch (topic)
            {
                case KnowledgeTopic.SettlementFood: return NpcText.Food(value);
                case KnowledgeTopic.SettlementGovernor: return Guid.TryParse(value, out var f) ? FactionNameOf(new FactionId(f)) ?? "a faction" : "nobody";
                case KnowledgeTopic.FactionLeader: return Guid.TryParse(value, out var l) ? NpcName(new NpcId(l)) : "nobody";
                case KnowledgeTopic.Whereabouts: return Guid.TryParse(value, out var p) ? PlaceName(new LocationId(p)) ?? "somewhere" : "somewhere";
                default: return value;
            }
        }

        /// <summary>The world's truth for a piece of knowledge (null when it has no checkable truth).</summary>
        private string TruthOf(NpcKnowledgeData k)
        {
            if (!Guid.TryParse(k.SubjectId, out var g)) return null;
            switch (k.Topic)
            {
                case KnowledgeTopic.SettlementFood:
                    var s = _world.FindSettlement(new SettlementId(g));
                    return s == null ? null : NpcMind.FoodState(s);
                case KnowledgeTopic.SettlementProsperity:
                    var p = _world.FindSettlement(new SettlementId(g));
                    return p == null ? null : NpcMind.ProsperityState(p);
                case KnowledgeTopic.SettlementGovernor:
                    var gov = _world.FindSettlement(new SettlementId(g));
                    return gov == null ? null : gov.FactionId.IsEmpty ? string.Empty : gov.FactionId.ToString();
                case KnowledgeTopic.FactionLeader:
                    var f = _world.FindFaction(new FactionId(g));
                    return f == null ? null : f.LeaderNpcId.IsEmpty ? string.Empty : f.LeaderNpcId.ToString();
                case KnowledgeTopic.Whereabouts:
                    var who = _world.FindNpc(new NpcId(g));
                    return who == null ? null : who.CurrentLocationId.ToString();
                default:
                    return null;
            }
        }
    }

    public sealed class LocationSummary
    {
        internal LocationSummary(LocationData l)
        {
            LocationId = l.LocationId;
            SettlementId = l.SettlementId;
            Kind = l.Kind;
            Name = l.Name;
            LayoutKey = l.LayoutKey;
        }

        public LocationId LocationId { get; }
        public SettlementId SettlementId { get; }
        public LocationKind Kind { get; }
        public string Name { get; }
        public string LayoutKey { get; }
    }
}
