using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;

namespace Realmkeeper.Simulation.Quests
{
    // Phase 12: the world side of quests (ADR-0056, ADR-0057, ADR-0059). Pure functions over WorldData, used by the
    // application's commands (inside their transaction) and by the daily quest system. Nothing here creates items or
    // awards experience: rewards are the application's, through its own validated paths.

    /// <summary>Reports a Chronicle event, from a command or from the day's simulation.</summary>
    public delegate void QuestReporter(HistoricalEventType type, EventSeverity severity, EventImportance importance, IReadOnlyList<SettlementId> settlements,
        Dictionary<string, string> facts, IReadOnlyList<NpcId> npcs);

    public static class QuestClock
    {
        public static long Now(WorldData w) => w.CurrentSimulationDate.ToDayNumber() * WorldTime.MinutesPerDay + w.MinuteOfDay;
    }

    /// <summary>Everything a quest event can say (ADR-0057). Commands emit them; the engine matches them to objectives.</summary>
    public enum QuestEventKind
    {
        Talked = 0,
        ReachedSettlement = 1,
        VisitedLocation = 2,
        Investigated = 3,
        Delivered = 4,
        Crafted = 5,
        Defeated = 6,
        DefeatedNamed = 7,
        Protected = 8,
        Escorted = 9,
        Rescued = 10,
        Discovered = 11,
        Decision = 12,
        QuestCompleted = 13,
        SurvivedEncounter = 14,
        LostEncounter = 15,
        ItemsChanged = 16,
        ObjectiveCompleted = 17,

        // Phase 13: places. Subject is the place id; Detail a room, floor or feature where it matters.
        PlaceDiscovered = 18,
        PlaceEntered = 19,
        RoomExplored = 20,
        FloorReached = 21,
        TrapDisarmed = 22,
        FeatureUsed = 23,
    }

    public sealed class QuestEvent
    {
        public QuestEventKind Kind { get; set; }

        /// <summary>What it is about: a person, item, creature, object, information, decision, quest or settlement ID.</summary>
        public string Subject { get; set; }

        /// <summary>To whom (a delivery's recipient), or the quest instance an explicit objective belongs to.</summary>
        public string Target { get; set; }

        /// <summary>Where (the settlement an encounter was near, the place of an object).</summary>
        public SettlementId Settlement { get; set; }

        public int Quantity { get; set; } = 1;

        /// <summary>A particular item instance (a delivered stack), or an objective ID for <see cref="QuestEventKind.ObjectiveCompleted"/>.</summary>
        public string Detail { get; set; }

        public override string ToString() => Kind + ":" + Subject + (Target != null ? "→" + Target : string.Empty) + "×" + Quantity;
    }

    /// <summary>Fills {role} and {variable} tokens with the bound entities' names (ADR-0056).</summary>
    public static class QuestText
    {
        public static string Name(WorldData w, string id)
        {
            if (string.IsNullOrEmpty(id)) return "someone";
            if (Guid.TryParse(id, out var g))
            {
                var n = w.FindNpc(new NpcId(g));
                if (n != null) return n.Name;
                var s = w.FindSettlement(new SettlementId(g));
                if (s != null) return s.Name;
                var f = w.FindFaction(new FactionId(g));
                if (f != null) return f.Name;
                var l = w.FindLocation(new LocationId(g));
                if (l != null) return l.Name;
            }

            var item = ItemCatalog.Find(id);
            if (item != null) return item.Name.ToLowerInvariant();
            var creature = CreatureCatalog.Find(id);
            if (creature != null) return creature.Name.ToLowerInvariant();
            var place = w.FindPlace(id.Split('#')[0]);
            if (place != null) return place.Name;
            if (id.StartsWith("ledger:", StringComparison.Ordinal)) return "the granary ledger";
            if (id.StartsWith("notice:", StringComparison.Ordinal)) return "the notice board";
            return id;
        }

        public static string Fill(WorldData w, QuestInstanceData q, string text, string player = null)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            var def = QuestCatalog.Find(q?.DefinitionId);
            var result = text;
            for (var pass = 0; pass < 2 && result.IndexOf('{') >= 0; pass++)
            {
                if (q != null)
                {
                    if (def != null && result.Contains("{introduction}")) result = result.Replace("{introduction}", def.Introduction ?? string.Empty);
                    foreach (var pair in q.Variables) result = result.Replace("{" + pair.Key + "}", pair.Value);
                    foreach (var pair in q.Bindings) result = result.Replace("{" + pair.Key + "}", Name(w, pair.Value));
                }

                result = result.Replace("{player}", player ?? w.Player?.Name ?? "the stranger");
            }

            return result;
        }
    }

    /// <summary>
    /// Fills a template's roles from the world (ADR-0056), generically from its <see cref="RoleSpec"/>s: people by their
    /// role and place, settlements and factions of a role, objects, items and creatures by value. Deterministic: the
    /// candidates are sorted by ID and picked with a seed. A role that cannot be filled means the quest cannot exist.
    /// </summary>
    public static class QuestBinder
    {
        public static Dictionary<string, string> Bind(WorldData w, QuestDefinition def, Dictionary<string, string> given, ulong seed, out string problem)
        {
            problem = null;
            var b = new Dictionary<string, string>(given ?? new Dictionary<string, string>(), StringComparer.Ordinal);
            var used = new HashSet<string>(b.Values, StringComparer.Ordinal);
            SettlementId Home(string role) => b.TryGetValue(role, out var id) && Guid.TryParse(id, out var g) ? (w.FindNpc(new NpcId(g))?.HomeSettlementId ?? default) : default;
            var n = 0;
            foreach (var spec in def.Roles)
            {
                n++;
                if (b.ContainsKey(spec.Name)) continue;
                switch (spec.Kind)
                {
                    case RoleKind.Npc:
                    {
                        var giverHome = spec.Name == "giver" ? default : Home("giver");
                        var candidates = new List<NpcData>();
                        foreach (var roleName in spec.NpcRoles.Count > 0 ? spec.NpcRoles : new[] { "" })
                        {
                            foreach (var npc in w.Npcs)
                            {
                                if (npc.IsDeceased || npc.Tier == NpcTier.Unassigned || used.Contains(npc.NpcId.ToString())) continue;
                                if (roleName.Length > 0 && npc.Role.ToString() != roleName) continue;
                                if (spec.NeedsFaction && npc.FactionId.IsEmpty) continue;
                                if (spec.Where == "same" && !giverHome.IsEmpty && npc.HomeSettlementId != giverHome) continue;
                                if (spec.Where == "other" && !giverHome.IsEmpty && npc.HomeSettlementId == giverHome) continue;
                                candidates.Add(npc);
                            }

                            if (candidates.Count > 0) break;
                        }

                        if (candidates.Count == 0 && spec.Where == "other") // nobody elsewhere: someone at home will do
                            foreach (var npc in w.Npcs)
                                if (!npc.IsDeceased && npc.Tier != NpcTier.Unassigned && !used.Contains(npc.NpcId.ToString()) && (!spec.NeedsFaction || !npc.FactionId.IsEmpty)) candidates.Add(npc);
                        if (candidates.Count == 0) { problem = $"Nobody can play '{spec.Name}'."; return null; }
                        candidates.Sort((x, y) => x.NpcId.CompareTo(y.NpcId));
                        var pick = candidates[(int)(SeedMixer.Mix(SeedMixer.Combine(seed, spec.Name)) % (ulong)candidates.Count)];
                        b[spec.Name] = pick.NpcId.ToString();
                        used.Add(pick.NpcId.ToString());
                        break;
                    }
                    case RoleKind.Settlement:
                    {
                        var home = Home(spec.Of ?? "giver");
                        if (home.IsEmpty) { problem = $"'{spec.Of}' has no home for '{spec.Name}'."; return null; }
                        b[spec.Name] = home.ToString();
                        break;
                    }
                    case RoleKind.Faction:
                    {
                        if (!b.TryGetValue(spec.Of ?? "giver", out var id) || !Guid.TryParse(id, out var g) || w.FindNpc(new NpcId(g))?.FactionId.IsEmpty != false)
                        {
                            problem = $"'{spec.Of}' belongs to no faction.";
                            return null;
                        }

                        b[spec.Name] = w.FindNpc(new NpcId(g)).FactionId.ToString();
                        break;
                    }
                    default:
                        b[spec.Name] = (spec.Value ?? string.Empty).Replace("{settlement}", b.TryGetValue("settlement", out var sid) ? sid : string.Empty);
                        break;
                }
            }

            return b;
        }
    }

    /// <summary>
    /// The player's reputation (ADR-0059): every change keyed in the ledger, so a repeated command, a replay or a reload
    /// never applies it twice; personal regard on the person, faction standing, settlement reputation and notoriety on the
    /// player. Changes are contextual: only the person, faction or place named is touched.
    /// </summary>
    public static class Reputation
    {
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>Applies a change once (by key). Returns false when the key was already applied or the target is unknown.</summary>
        public static bool Change(WorldData w, string target, string dimension, int amount, string key, string reason, QuestReporter report = null)
        {
            var social = w.Player?.Social;
            if (social == null || amount == 0) return false;
            if (key != null)
                foreach (var e in social.Ledger)
                    if (e.Key == key)
                        return false;
            var applied = false;
            var settlements = new List<SettlementId>();
            var npcs = new List<NpcId>();
            if (target.StartsWith("npc:", StringComparison.Ordinal) && Guid.TryParse(target.Substring(4), out var ng))
            {
                var npc = w.FindNpc(new NpcId(ng));
                if (npc == null) return false;
                var r = npc.PlayerRegard;
                switch (dimension)
                {
                    case "Familiarity": r.Familiarity += amount; break;
                    case "Affinity": r.Affinity += amount; break;
                    case "Trust": r.Trust += amount; break;
                    case "Respect": r.Respect += amount; break;
                    case "Fear": r.Fear += amount; break;
                    case "Gratitude": r.Gratitude += amount; break;
                    case "Hostility": r.Hostility += amount; break;
                    default: return false;
                }

                applied = true;
                npcs.Add(npc.NpcId);
                if (!npc.HomeSettlementId.IsEmpty) settlements.Add(npc.HomeSettlementId);
            }
            else if (target.StartsWith("faction:", StringComparison.Ordinal) && Guid.TryParse(target.Substring(8), out var fg))
            {
                var id = new FactionId(fg);
                if (w.FindFaction(id) == null) return false;
                FactionStandingData s = null;
                foreach (var x in social.FactionStandings) if (x.FactionId == id) s = x;
                if (s == null) social.FactionStandings.Add(s = new FactionStandingData { FactionId = id });
                s.Standing = Clamp(s.Standing + amount, -100, 100);
                applied = true;
            }
            else if (target.StartsWith("settlement:", StringComparison.Ordinal) && Guid.TryParse(target.Substring(11), out var sg))
            {
                var id = new SettlementId(sg);
                if (w.FindSettlement(id) == null) return false;
                SettlementStandingData s = null;
                foreach (var x in social.SettlementStandings) if (x.SettlementId == id) s = x;
                if (s == null) social.SettlementStandings.Add(s = new SettlementStandingData { SettlementId = id });
                s.Reputation = Clamp(s.Reputation + amount, -100, 100);
                settlements.Add(id);
                applied = true;
            }
            else if (target == "notoriety")
            {
                social.Notoriety = Clamp(social.Notoriety + amount, 0, 100);
                applied = true;
            }

            if (!applied) return false;
            social.Ledger.Add(new ReputationChangeData { Key = key ?? "rep:" + social.Ledger.Count, At = QuestClock.Now(w), Target = target, Dimension = dimension, Amount = amount, Reason = reason });
            while (social.Ledger.Count > PlayerSocialData.MaxLedger) social.Ledger.RemoveAt(0);
            if (report != null && (Math.Abs(amount) >= 10 || !target.StartsWith("npc:", StringComparison.Ordinal)))
                report(HistoricalEventType.ReputationChanged, amount > 0 ? EventSeverity.Positive : EventSeverity.Warning, EventImportance.Minor, settlements,
                    new Dictionary<string, string> { ["source"] = "player", ["target"] = TargetName(w, target), ["dimension"] = dimension, ["amount"] = amount.ToString(CultureInfo.InvariantCulture), ["reason"] = reason ?? string.Empty }, npcs);
            return true;
        }

        public static string TargetName(WorldData w, string target)
        {
            var colon = target.IndexOf(':');
            return colon < 0 ? target : QuestText.Name(w, target.Substring(colon + 1));
        }

        /// <summary>The presentation tier for a person (ADR-0059).</summary>
        public static ReputationTier Of(PlayerRegardData r) =>
            ReputationThresholds.Default.ForPerson(r.Familiarity, r.Affinity, r.Trust, r.Respect, r.Fear, r.Gratitude, r.Hostility);

        public static int Dimension(PlayerRegardData r, string dimension)
        {
            switch (dimension)
            {
                case "Familiarity": return r.Familiarity;
                case "Affinity": return r.Affinity;
                case "Trust": return r.Trust;
                case "Respect": return r.Respect;
                case "Fear": return r.Fear;
                case "Gratitude": return r.Gratitude;
                case "Hostility": return r.Hostility;
                default: return 0;
            }
        }

        /// <summary>
        /// How a person reacts on learning of a deed (ADR-0059): by the deed, their personality and their own ties. Applied
        /// once per person and deed (the knowledge record is unique per subject). Returns (dimension, amount) pairs.
        /// </summary>
        public static List<(string Dimension, int Amount)> Reaction(NpcData npc, string deed)
        {
            var list = new List<(string, int)>();
            var compassion = npc.Personality[NpcTrait.Compassion];
            var greed = npc.Personality[NpcTrait.Greed];
            switch (deed)
            {
                case "recovered_grain":
                case "fed_settlement":
                case "hunted_predators":
                case "supplied_shop":
                case "kept_seal":
                    list.Add(("Respect", 4 + compassion / 25));
                    list.Add(("Affinity", 2));
                    break;
                case "threatened_resident":
                    list.Add(("Fear", 3 + (100 - npc.Personality[NpcTrait.Courage]) / 25));
                    list.Add(("Affinity", -3));
                    break;
                case "lied_about_grain":
                case "opened_letter":
                    list.Add(("Trust", -(3 + (100 - npc.Personality[NpcTrait.Trustfulness]) / 25)));
                    break;
                case "took_silver":
                    list.Add(("Trust", -4));
                    if (greed < 50) list.Add(("Respect", -3));
                    break;
            }

            return list;
        }
    }

    /// <summary>Applies the world-only consequences of a quest (ADR-0056), each once (keyed on the instance).</summary>
    public static class QuestEffects
    {
        public static int Amount(QuestInstanceData q, ConsequenceDefinition c)
        {
            if (c.Value != null && c.Value.StartsWith("var:", StringComparison.Ordinal) && q.Variables.TryGetValue(c.Value.Substring(4), out var v) &&
                int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n))
                return n * (c.Amount == 0 ? 1 : c.Amount);
            return c.Amount;
        }

        public static void ApplyAll(WorldData w, QuestInstanceData q, ConsequenceTiming when, QuestReporter report)
        {
            var def = QuestCatalog.Find(q.DefinitionId);
            if (def == null) return;
            for (var i = 0; i < def.Consequences.Count; i++)
            {
                var c = def.Consequences[i];
                if (c.When != when) continue;
                if (c.Outcome != null && c.Outcome != q.Outcome) continue;
                var key = q.InstanceId + ":" + when + ":" + i.ToString(CultureInfo.InvariantCulture);
                if (q.AppliedConsequences.Contains(key)) continue;
                Apply(w, q, c, key, report);
                q.AppliedConsequences.Add(key);
            }
        }

        public static NpcData Person(WorldData w, QuestInstanceData q, string role) =>
            q.Bound(role) is string id && Guid.TryParse(id, out var g) ? w.FindNpc(new NpcId(g)) : null;

        public static SettlementData SettlementOf(WorldData w, QuestInstanceData q, string role)
        {
            var id = q.Bound(role);
            if (id == null || !Guid.TryParse(id, out var g)) return null;
            return w.FindSettlement(new SettlementId(g)) ?? (w.FindNpc(new NpcId(g)) is NpcData n ? w.FindSettlement(n.HomeSettlementId) : null);
        }

        public static void Apply(WorldData w, QuestInstanceData q, ConsequenceDefinition c, string key, QuestReporter report)
        {
            var today = w.CurrentSimulationDate;
            var amount = Amount(q, c);
            var player = w.Player?.Name;
            switch (c.Kind)
            {
                case ConsequenceKind.SettlementResource:
                {
                    var s = SettlementOf(w, q, c.Role ?? "settlement");
                    if (s == null) return;
                    var type = new ResourceTypeId(c.Key);
                    s.ResourceInventory.Set(type, Math.Max(0, s.ResourceInventory.Get(type) + amount));
                    break;
                }
                case ConsequenceKind.SettlementStability:
                {
                    var s = SettlementOf(w, q, c.Role ?? "settlement");
                    if (s != null) s.Stability = Reputation.Clamp(s.Stability + amount, 0, 100);
                    break;
                }
                case ConsequenceKind.SettlementProsperity:
                {
                    var s = SettlementOf(w, q, c.Role ?? "settlement");
                    if (s != null) s.Prosperity = Reputation.Clamp(s.Prosperity + amount, 0, 100);
                    break;
                }
                case ConsequenceKind.FactionInfluence:
                {
                    var npc = Person(w, q, c.Role);
                    var faction = npc != null ? w.FindFaction(npc.FactionId) : q.Bound(c.Role) is string fid && Guid.TryParse(fid, out var fg) ? w.FindFaction(new FactionId(fg)) : null;
                    if (faction != null) faction.Influence = Math.Max(0, faction.Influence + amount);
                    break;
                }
                case ConsequenceKind.NpcMemory:
                {
                    var npc = Person(w, q, c.Role);
                    if (npc == null || npc.IsDeceased) return;
                    NpcMind.Remember(npc, new NpcMemoryData
                    {
                        MemoryId = new MemoryId(NpcHash.Guid(SeedMixer.Combine(SeedMixer.FromWorldSeed(w.Seed), "quest-memory:" + key))),
                        Date = today, Category = MemoryCategory.Personal, Subject = QuestText.Fill(w, q, c.Key, player), SettlementId = npc.HomeSettlementId,
                        Salience = amount > 0 ? amount : 50, Source = MemorySource.Experienced,
                    });
                    break;
                }
                case ConsequenceKind.NpcRegard:
                {
                    var npc = Person(w, q, c.Role);
                    if (npc != null) Reputation.Change(w, "npc:" + npc.NpcId, c.Key, amount, key, QuestCatalog.Find(q.DefinitionId)?.Name, report);
                    break;
                }
                case ConsequenceKind.PlayerDeed:
                {
                    var npc = Person(w, q, c.Role);
                    if (npc != null) Deeds.Witness(w, npc, c.Key, q.InstanceId, key);
                    break;
                }
                case ConsequenceKind.NpcMorale:
                {
                    var npc = Person(w, q, c.Role);
                    if (npc != null) npc.Condition.Morale += amount;
                    break;
                }
                case ConsequenceKind.Notoriety:
                    Reputation.Change(w, "notoriety", "Notoriety", amount, key, QuestCatalog.Find(q.DefinitionId)?.Name, report);
                    break;
                case ConsequenceKind.ChronicleNote:
                {
                    var s = SettlementOf(w, q, c.Role ?? "settlement");
                    report?.Invoke(HistoricalEventType.QuestConsequence, EventSeverity.Info, EventImportance.Minor, s == null ? Array.Empty<SettlementId>() : new[] { s.SettlementId },
                        new Dictionary<string, string> { ["source"] = "quest", ["quest"] = QuestCatalog.Find(q.DefinitionId)?.Name ?? q.DefinitionId, ["instance"] = q.InstanceId, ["what"] = QuestText.Fill(w, q, c.Key, player) },
                        Array.Empty<NpcId>());
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Deeds (ADR-0059): what the player did becomes knowledge held by those who saw it, and then travels through the
    /// ordinary Phase 4 sharing (one item per meeting, hop and lifetime limits). People react when they learn of it.
    /// </summary>
    public static class Deeds
    {
        public static void Witness(WorldData w, NpcData npc, string deed, string about, string key)
        {
            var subject = "deed:" + about + ":" + deed;
            var seed = SeedMixer.Combine(SeedMixer.FromWorldSeed(w.Seed), "deed:" + key + ":" + npc.NpcId);
            NpcMind.Learn(npc, KnowledgeTopic.PlayerDeed, subject, deed, KnowledgeStatus.Known, 100, w.CurrentSimulationDate, default, 0, default,
                () => new KnowledgeId(NpcHash.Guid(seed)));
            React(w, npc, subject, deed);
        }

        /// <summary>Applies a person's reaction to a deed once (keyed by person and deed subject).</summary>
        public static void React(WorldData w, NpcData npc, string subject, string deed)
        {
            foreach (var (dimension, amount) in Reputation.Reaction(npc, deed))
                Reputation.Change(w, "npc:" + npc.NpcId, dimension, amount, "deed:" + npc.NpcId + ":" + subject + ":" + dimension, "heard: " + deed);
        }
    }

    /// <summary>The objective engine (ADR-0057): stages, alternatives, matching events, state objectives, completion.</summary>
    public static class QuestObjectives
    {
        public static ObjectiveDefinition Find(QuestDefinition def, string id)
        {
            foreach (var o in def.Objectives) if (o.Id == id) return o;
            return null;
        }

        /// <summary>A required group is satisfied when any member is done; optional objectives never block.</summary>
        public static bool StageDone(QuestDefinition def, QuestInstanceData q, int stage)
        {
            var groups = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var o in def.Objectives)
            {
                if (o.Stage != stage || o.Optional) continue;
                var p = q.Progress(o.Id);
                if (p != null && p.Unavailable) continue;
                var g = o.Group ?? "#" + o.Id;
                groups[g] = (groups.TryGetValue(g, out var done) && done) || (p != null && p.Done);
            }

            foreach (var pair in groups) if (!pair.Value) return false;
            return true;
        }

        public static bool StageOpen(QuestDefinition def, QuestInstanceData q, int stage)
        {
            for (var s = 0; s < stage; s++) if (!StageDone(def, q, s)) return false;
            return true;
        }

        public static int LastStage(QuestDefinition def)
        {
            var max = 0;
            foreach (var o in def.Objectives) if (o.Stage > max) max = o.Stage;
            return max;
        }

        public static bool AllDone(QuestDefinition def, QuestInstanceData q)
        {
            for (var s = 0; s <= LastStage(def); s++) if (!StageDone(def, q, s)) return false;
            return true;
        }

        /// <summary>Reveals what an opened stage shows (its ungrouped hidden objectives); grouped paths are revealed by what the player learns.</summary>
        public static void RevealOpenStages(QuestDefinition def, QuestInstanceData q)
        {
            foreach (var o in def.Objectives)
            {
                var p = q.Progress(o.Id);
                if (p == null || p.Revealed) continue;
                if (!o.Hidden) { p.Revealed = true; continue; }
                if (o.Group == null && StageOpen(def, q, o.Stage)) p.Revealed = true;
            }
        }

        private static string Resolve(QuestInstanceData q, string target)
        {
            if (target == null) return null;
            if (target.StartsWith("role:", StringComparison.Ordinal))
            {
                var hash = target.IndexOf('#');
                return hash < 0 ? q.Bound(target.Substring(5)) : q.Bound(target.Substring(5, hash - 5)) + target.Substring(hash); // Phase 13: "role:place#room"
            }

            if (target.StartsWith("var:", StringComparison.Ordinal)) return q.Variables.TryGetValue(target.Substring(4), out var v) ? v : null;
            var colon = target.IndexOf(':');
            return colon < 0 ? target : target.Substring(colon + 1);
        }

        public static bool IsFood(string itemId) => ItemCatalog.Find(itemId) is ItemDefinition d && (d.Category == ItemCategory.Food || d.Id == "grain_sack");

        private static bool Matches(QuestInstanceData q, ObjectiveDefinition o, QuestEvent e)
        {
            var target = Resolve(q, o.Target);
            switch (o.Kind)
            {
                case ObjectiveKind.TalkTo:
                case ObjectiveKind.Persuade:
                    return e.Kind == QuestEventKind.ObjectiveCompleted && e.Target == q.InstanceId && e.Detail == o.Id;
                case ObjectiveKind.Deliver:
                {
                    if (e.Kind != QuestEventKind.Delivered || e.Target != q.Bound(o.To)) return false;
                    if (o.Target == "item:@food") return IsFood(e.Subject);
                    return o.Target.StartsWith("var:", StringComparison.Ordinal) ? e.Detail == target : e.Subject == target;
                }
                case ObjectiveKind.DefeatCreature:
                    return e.Kind == QuestEventKind.Defeated && e.Subject == target && (q.Bound("settlement") == null || e.Settlement.ToString() == q.Bound("settlement"));
                case ObjectiveKind.DefeatNamed:
                    return e.Kind == QuestEventKind.DefeatedNamed && e.Subject == target;
                case ObjectiveKind.SurviveEncounter:
                    return e.Kind == QuestEventKind.SurvivedEncounter && (target == null || e.Subject == target);
                case ObjectiveKind.Protect:
                    return e.Kind == QuestEventKind.Protected && e.Subject == target;
                case ObjectiveKind.Escort:
                    return e.Kind == QuestEventKind.Escorted && e.Subject == target;
                case ObjectiveKind.Rescue:
                    return e.Kind == QuestEventKind.Rescued && e.Subject == target;
                case ObjectiveKind.Investigate:
                    return e.Kind == QuestEventKind.Investigated && e.Subject == target;
                case ObjectiveKind.DiscoverInfo:
                    return e.Kind == QuestEventKind.Discovered && e.Subject == target;
                case ObjectiveKind.DiscoverLocation:
                    return (e.Kind == QuestEventKind.VisitedLocation || e.Kind == QuestEventKind.ReachedSettlement || e.Kind == QuestEventKind.PlaceDiscovered || e.Kind == QuestEventKind.PlaceEntered) && e.Subject == target;
                case ObjectiveKind.VisitLocation:
                    return (e.Kind == QuestEventKind.VisitedLocation || e.Kind == QuestEventKind.ReachedSettlement || e.Kind == QuestEventKind.PlaceEntered) && e.Subject == target;
                case ObjectiveKind.ExploreRoom:
                    return e.Kind == QuestEventKind.RoomExplored && e.Subject + "#" + e.Detail == target;
                case ObjectiveKind.ReachFloor:
                    return e.Kind == QuestEventKind.FloorReached && e.Subject + "#" + e.Detail == target;
                case ObjectiveKind.DisarmTrap:
                    return e.Kind == QuestEventKind.TrapDisarmed && (e.Subject + "#" + e.Detail == target || e.Subject == target);
                case ObjectiveKind.UseFeature:
                    return e.Kind == QuestEventKind.FeatureUsed && e.Subject + "#" + e.Detail == target;
                case ObjectiveKind.ReachSettlement:
                    return e.Kind == QuestEventKind.ReachedSettlement && e.Subject == target;
                case ObjectiveKind.Craft:
                case ObjectiveKind.CraftingTask:
                    return e.Kind == QuestEventKind.Crafted && e.Subject == target;
                case ObjectiveKind.Decision:
                    return e.Kind == QuestEventKind.Decision && e.Subject == target && (e.Target == null || e.Target == q.InstanceId);
                case ObjectiveKind.CompleteQuest:
                    return e.Kind == QuestEventKind.QuestCompleted && e.Subject == target;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Applies one event to one open quest. Objectives in stages not yet open do not move (except an explicit
        /// completion, which the dialogue offers only when it makes sense). Returns true when anything changed.
        /// </summary>
        public static bool Observe(WorldData w, QuestInstanceData q, QuestEvent e)
        {
            if (!q.IsOpen) return false;
            var def = QuestCatalog.Find(q.DefinitionId);
            if (def == null) return false;
            var now = QuestClock.Now(w);
            var changed = false;
            foreach (var o in def.Objectives)
            {
                var p = q.Progress(o.Id);
                if (p == null || p.Done || p.Failed || p.Unavailable) continue;
                if (e.Kind == QuestEventKind.LostEncounter && o.Kind == ObjectiveKind.SurviveEncounter)
                {
                    p.Failed = true;
                    q.Note(now, "Objective failed: " + QuestText.Fill(w, q, o.Text));
                    changed = true;
                    continue;
                }

                if (!StageOpen(def, q, o.Stage) && e.Kind != QuestEventKind.ObjectiveCompleted) continue;
                if (!Matches(q, o, e)) continue;
                var add = o.Kind == ObjectiveKind.Deliver && o.Target == "item:@food" ? e.Quantity : Math.Max(1, e.Quantity);
                p.Count = Math.Min(o.Quantity, p.Count + add);
                if (o.Kind == ObjectiveKind.Deliver && o.Target == "item:@food")
                    q.Variables["delivered_food"] = ((q.Variables.TryGetValue("delivered_food", out var df) && int.TryParse(df, out var dn) ? dn : 0) + FoodUnits(e.Subject, e.Quantity)).ToString(CultureInfo.InvariantCulture);
                if (p.Count >= o.Quantity)
                {
                    p.Done = true;
                    p.Revealed = true;
                    p.DoneAt = now;
                    if (o.Outcome != null && q.Outcome == null) q.Outcome = o.Outcome;
                    q.Note(now, "Done: " + QuestText.Fill(w, q, o.Text));
                }

                changed = true;
            }

            if (changed) RevealOpenStages(def, q);
            return changed;
        }

        /// <summary>Settlement food a delivered item is worth: the food it is made of (its materials), at least one unit.</summary>
        public static int FoodUnits(string itemId, int quantity)
        {
            var d = ItemCatalog.Find(itemId);
            if (d == null) return 0;
            var units = 0;
            foreach (var m in d.Materials) if (m.ResourceTypeId == ResourceTypes.Food) units += m.Quantity;
            return Math.Max(1, units) * quantity;
        }

        /// <summary>Time limits on objectives and unavailability conditions; the daily system and every command call it.</summary>
        public static bool CheckTimers(WorldData w, QuestInstanceData q)
        {
            var def = QuestCatalog.Find(q.DefinitionId);
            if (def == null || !q.IsOpen || q.AcceptedAt < 0) return false;
            var now = QuestClock.Now(w);
            var changed = false;
            foreach (var o in def.Objectives)
            {
                var p = q.Progress(o.Id);
                if (p == null || p.Done || p.Failed) continue;
                if (o.TimeLimitMinutes > 0 && now - q.AcceptedAt > o.TimeLimitMinutes)
                {
                    p.Failed = true;
                    q.Note(now, "Too late: " + QuestText.Fill(w, q, o.Text));
                    changed = true;
                }
            }

            return changed;
        }
    }

    /// <summary>Creating instances, moving them between states, and failure and expiry (ADR-0056).</summary>
    public static class QuestLifecycle
    {
        public const int MaxInstances = 160;

        public static string InstanceId(QuestDefinition def, Dictionary<string, string> bindings, string sourceKey, int generation)
        {
            var giver = bindings.TryGetValue("giver", out var g) ? g : string.Empty;
            var key = "quest:" + def.Id + ":" + (sourceKey == "authored" ? giver : sourceKey);
            return generation > 0 ? key + "#" + generation.ToString(CultureInfo.InvariantCulture) : key;
        }

        /// <summary>Makes an available instance (its OnAvailable consequences happen now), or returns null with a reason.</summary>
        public static QuestInstanceData Create(WorldData w, QuestDefinition def, Dictionary<string, string> given, string sourceKey, Dictionary<string, string> variables,
            QuestReporter report, out string problem)
        {
            var seed = SeedMixer.Combine(SeedMixer.FromWorldSeed(w.Seed), "quest-bind:" + def.Id + ":" + sourceKey);
            var bindings = QuestBinder.Bind(w, def, given, seed, out problem);
            if (bindings == null) return null;
            var generation = 0;
            var id = InstanceId(def, bindings, sourceKey, 0);
            while (w.FindQuest(id) != null) id = InstanceId(def, bindings, sourceKey, ++generation);
            var now = QuestClock.Now(w);
            var q = new QuestInstanceData
            {
                InstanceId = id, DefinitionId = def.Id, DefinitionVersion = def.Version, State = QuestState.Available, Bindings = bindings, SourceKey = sourceKey,
                CreatedAt = now, ExpiresAt = def.ExpiresAfterDays > 0 ? now + def.ExpiresAfterDays * (long)WorldTime.MinutesPerDay : -1,
            };
            if (variables != null) foreach (var pair in variables) q.Variables[pair.Key] = pair.Value;
            foreach (var o in def.Objectives) q.Objectives.Add(new ObjectiveProgressData { Id = o.Id, Revealed = !o.Hidden });
            q.Note(now, "Became available.");
            w.Quests.Add(q);
            QuestEffects.ApplyAll(w, q, ConsequenceTiming.OnAvailable, report);
            Prune(w);
            return q;
        }

        /// <summary>Moves a quest to a new state if the state machine allows it, recording it and reporting it once.</summary>
        public static bool Move(WorldData w, QuestInstanceData q, QuestState to, string why, QuestReporter report)
        {
            var def = QuestCatalog.Find(q.DefinitionId);
            if (!QuestStates.CanMove(q.State, to, def?.Repeatable == true)) return false;
            var now = QuestClock.Now(w);
            q.State = to;
            switch (to)
            {
                case QuestState.Discovered: q.DiscoveredAt = now; break;
                case QuestState.Offered: q.OfferedAt = now; if (q.DiscoveredAt < 0) q.DiscoveredAt = now; break;
                case QuestState.Accepted: q.AcceptedAt = now; q.ExpiresAt = -1; break;
                case QuestState.Completed:
                case QuestState.Failed:
                case QuestState.Abandoned:
                    q.ClosedAt = now;
                    q.Tracked = false;
                    break;
            }

            if (to == QuestState.Failed) q.FailureReason = why;
            q.Note(now, Capital(QuestStates.Name(to)) + (string.IsNullOrEmpty(why) ? "." : ": " + why));
            var type = to == QuestState.Discovered ? HistoricalEventType.QuestDiscovered : to == QuestState.Offered ? HistoricalEventType.QuestOffered
                : to == QuestState.Active ? HistoricalEventType.QuestAccepted : to == QuestState.Completed ? HistoricalEventType.QuestCompleted
                : to == QuestState.Failed ? HistoricalEventType.QuestFailed : to == QuestState.Abandoned ? HistoricalEventType.QuestAbandoned : (HistoricalEventType?)null;
            if (type != null && report != null)
            {
                var s = QuestEffects.SettlementOf(w, q, "settlement");
                var giver = QuestEffects.Person(w, q, "giver");
                report(type.Value, to == QuestState.Completed ? EventSeverity.Positive : to == QuestState.Failed ? EventSeverity.Warning : EventSeverity.Info,
                    to == QuestState.Completed || to == QuestState.Failed ? EventImportance.Notable : EventImportance.Minor,
                    s == null ? Array.Empty<SettlementId>() : new[] { s.SettlementId },
                    new Dictionary<string, string>
                    {
                        ["source"] = "player", ["quest"] = def?.Name ?? q.DefinitionId, ["instance"] = q.InstanceId, ["giver"] = giver?.Name ?? string.Empty,
                        ["outcome"] = q.Outcome ?? string.Empty, ["reason"] = why ?? string.Empty,
                    },
                    giver == null ? Array.Empty<NpcId>() : new[] { giver.NpcId });
            }

            if (to == QuestState.Failed) QuestEffects.ApplyAll(w, q, ConsequenceTiming.OnFail, report);
            if (to == QuestState.Abandoned) QuestEffects.ApplyAll(w, q, ConsequenceTiming.OnAbandon, report);
            if (to == QuestState.Completed || to == QuestState.Failed || to == QuestState.Abandoned) Party.CompanionLoyalty.OnQuestClosed(w, q, to, report); // Phase 15
            Campaigns.CampaignEngine.OnQuestMoved(w, q, report); // Phase 17: the campaign it belongs to (if any) is re-read
            return true;
        }

        private static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);

        /// <summary>Why an open quest has failed now, or null (ADR-0056): a person it needs died, time ran out, a decision, or its cause ended.</summary>
        public static string FailureNow(WorldData w, QuestInstanceData q)
        {
            var def = QuestCatalog.Find(q.DefinitionId);
            if (def == null) return null;
            var now = QuestClock.Now(w);
            foreach (var f in def.Failures)
            {
                switch (f.Kind)
                {
                    case FailureKind.NpcDied:
                        var npc = QuestEffects.Person(w, q, f.Key);
                        if (npc == null || npc.IsDeceased) return QuestText.Fill(w, q, f.Text);
                        break;
                    case FailureKind.TimeLimit:
                        if (q.AcceptedAt >= 0 && now - q.AcceptedAt > f.Amount * (long)WorldTime.MinutesPerDay) return QuestText.Fill(w, q, f.Text);
                        break;
                    case FailureKind.Decision:
                        if (q.Decisions.Contains(f.Key)) return QuestText.Fill(w, q, f.Text);
                        break;
                    case FailureKind.CauseEnded:
                        if (!QuestCauses.Holds(w, q)) return QuestText.Fill(w, q, f.Text);
                        break;
                }
            }

            foreach (var o in def.Objectives)
                if (o.FailsQuest && q.Progress(o.Id)?.Failed == true) return "An objective could not be done: " + QuestText.Fill(w, q, o.Text);
            return null;
        }

        /// <summary>Keeps the quest list bounded: the oldest closed or lapsed emergent instances are forgotten first; authored ones and open ones never are.</summary>
        public static void Prune(WorldData w)
        {
            while (w.Quests.Count > MaxInstances)
            {
                var index = -1;
                for (var i = 0; i < w.Quests.Count; i++)
                {
                    var q = w.Quests[i];
                    if (q.SourceKey == "authored" || q.IsOpen || q.State == QuestState.Offered) continue;
                    if (q.SourceKey != null && q.SourceKey.StartsWith(Campaigns.CampaignEngine.QuestSourcePrefix, StringComparison.Ordinal)) continue; // Phase 17: a campaign's quests are its record
                    if (q.State == QuestState.Unavailable || q.IsClosed) { index = i; break; }
                }

                if (index < 0) return;
                w.Quests.RemoveAt(index);
            }
        }
    }

    /// <summary>Whether an emergent quest's cause still holds (ADR-0060): read from the world, never stored.</summary>
    public static class QuestCauses
    {
        public static bool Holds(WorldData w, QuestInstanceData q)
        {
            var key = q.SourceKey ?? "authored";
            var parts = key.Split(':');
            if (parts.Length < 2) return true;
            switch (parts[0])
            {
                case "shortage":
                    return Guid.TryParse(parts[parts.Length - 1], out var sg) && w.FindSettlement(new SettlementId(sg)) is SettlementData s && s.IsInFoodShortage;
                case "predators":
                    if (parts.Length < 3 || !Guid.TryParse(parts[1], out var pg)) return false;
                    return PredatorsNear(w, new SettlementId(pg), parts[2]) > 0;
                case "event": // Phase 16: a quest a world event called for holds while the event is open
                    return w.FindWorldEvent(key.Substring(6)) is WorldEventData ev && ev.IsOpen || q.Objectives.Exists(o => o.Done && o.Id != "report"); // done by the player: the cause ended by their hand
                case "story": // Phase 17 (ADR-0085): a story holds while its cause does
                    if (parts.Length >= 3 && parts[1] == "grain") return Guid.TryParse(parts[2], out var hg) && w.FindSettlement(new SettlementId(hg)) is SettlementData hs && hs.IsInFoodShortage;
                    return true; // a lost caravan stays lost: what became of it can always be asked
                case "supplies":
                    if (parts.Length < 3 || !Guid.TryParse(parts[1], out var shg)) return false;
                    var shop = w.FindShop(new ShopId(shg));
                    return shop != null && shop.QuantityOf(parts[2]) == 0;
                default:
                    return true;
            }
        }

        /// <summary>How many of a predator live within reach of a settlement (the same reach the encounter planner uses).</summary>
        public static int PredatorsNear(WorldData w, SettlementId id, string creatureId)
        {
            var s = w.FindSettlement(id);
            if (s == null) return 0;
            var count = 0;
            foreach (var p in w.CreaturePopulations)
            {
                if (p.CreatureId != creatureId || p.Count <= 0 || p.RegionId != s.RegionId) continue;
                var distance = Math.Max(0, Math.Max(Math.Abs(p.Center.X - s.Position.X), Math.Abs(p.Center.Z - s.Position.Z)) - p.RangeCells);
                if (distance <= 16) count += p.Count;
            }

            return count;
        }
    }
}
