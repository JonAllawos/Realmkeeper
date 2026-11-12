using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Campaigns;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Simulation.Campaigns
{
    /// <summary>
    /// Phase 17 (ADR-0082): fills a campaign's roles from the world as it stands — the opening town, a road out of the
    /// home region, the people living at either end. Deterministic (world seed, IDs in order). Nothing is created: if the
    /// world has no one to play a part, the campaign says so and does not start.
    /// </summary>
    public static class CampaignBinder
    {
        public static Dictionary<string, string> Bind(WorldData w, CampaignDefinition def, out string problem)
        {
            problem = null;
            var b = new Dictionary<string, string>(StringComparer.Ordinal);
            var cast = new HashSet<string>(StringComparer.Ordinal);
            var seed = SeedMixer.Combine(SeedMixer.FromWorldSeed(w.Seed), "campaign-bind:" + def.Id);
            foreach (var spec in def.Roles)
            {
                if (spec.Kind == CampaignRoleKind.FactionLeader || spec.Kind == CampaignRoleKind.FirstAvailable) continue; // read from the world when asked
                var id = Pick(w, def, spec, b, cast, seed);
                if (id == null)
                {
                    if (spec.Optional) continue;
                    problem = Missing(w, spec, b);
                    return null;
                }

                b[spec.Name] = id;
                if (spec.Kind == CampaignRoleKind.Npc) cast.Add(id);
            }

            return b;
        }

        private static string Missing(WorldData w, CampaignRoleSpec spec, Dictionary<string, string> b)
        {
            switch (spec.Rule)
            {
                case CampaignRoleRule.HomeTown: return "This world has no settlement to begin in.";
                case CampaignRoleRule.HomeCrossing: return "No road leads out of the home region yet: the story crosses into a neighbouring land.";
                case CampaignRoleRule.FactionOf: return $"{Name(w, b, spec.Of)} answers to no faction.";
                default: return $"Nobody in {Name(w, b, spec.Of)} can play {spec.Part ?? spec.Name}.";
            }
        }

        private static string Name(WorldData w, Dictionary<string, string> b, string role) => role != null && b.TryGetValue(role, out var id) ? CampaignEngine.NameOf(w, id) : "the place";

        private static string Pick(WorldData w, CampaignDefinition def, CampaignRoleSpec spec, Dictionary<string, string> b, HashSet<string> cast, ulong seed)
        {
            string of = spec.Of != null && b.TryGetValue(spec.Of, out var o) ? o : null;
            switch (spec.Rule)
            {
                case CampaignRoleRule.HomeTown:
                    return QuestGenesis.FirstTown(w)?.SettlementId.ToString();
                case CampaignRoleRule.HomeCrossing:
                {
                    var home = of != null && Guid.TryParse(of, out var hg) ? w.FindSettlement(new SettlementId(hg)) : QuestGenesis.FirstTown(w);
                    if (home == null) return null;
                    RouteSegmentData best = null;
                    var bestScore = int.MinValue;
                    foreach (var r in w.Routes)
                    {
                        if (!r.IsCrossing || (r.RegionA != home.RegionId && r.RegionB != home.RegionId)) continue;
                        var aEnd = w.FindSettlement(r.A);
                        var far = aEnd != null && aEnd.RegionId == home.RegionId ? w.FindSettlement(r.B) : aEnd;
                        if (far == null) continue;
                        var score = (far.FactionId != home.FactionId && !far.FactionId.IsEmpty ? 4 : 0) + (r.Passable ? 2 : 0) + (far.SettlementType == SettlementType.Town ? 1 : 0);
                        if (score > bestScore || (score == bestScore && string.CompareOrdinal(r.RouteId, best.RouteId) < 0)) { best = r; bestScore = score; }
                    }

                    return best?.RouteId;
                }
                case CampaignRoleRule.FarEnd:
                {
                    var r = w.FindRoute(of);
                    if (r == null) return null;
                    var home = QuestGenesis.FirstTown(w);
                    var a = w.FindSettlement(r.A);
                    return (a != null && home != null && a.RegionId == home.RegionId ? r.B : r.A).ToString();
                }
                case CampaignRoleRule.PersonIn:
                {
                    if (of == null || !Guid.TryParse(of, out var sg)) return null;
                    var s = new SettlementId(sg);
                    var trades = new List<string>(spec.NpcRoles) { string.Empty }; // the preferred trades first, then anyone who lives there
                    foreach (var trade in trades)
                    {
                        var candidates = new List<NpcData>();
                        foreach (var n in w.Npcs)
                        {
                            if (n.IsDeceased || n.Tier == NpcTier.Unassigned || n.HomeSettlementId != s || cast.Contains(n.NpcId.ToString())) continue;
                            if (trade.Length > 0 && n.Role.ToString() != trade) continue;
                            if (w.Party.Contains(n.NpcId)) continue; // a companion is the player's, not the story's
                            candidates.Add(n);
                        }

                        if (candidates.Count == 0) continue;
                        candidates.Sort((x, y) => x.NpcId.CompareTo(y.NpcId));
                        return candidates[(int)(SeedMixer.Mix(SeedMixer.Combine(seed, spec.Name)) % (ulong)candidates.Count)].NpcId.ToString();
                    }

                    return null;
                }
                case CampaignRoleRule.FactionOf:
                {
                    if (of == null || !Guid.TryParse(of, out var g)) return null;
                    var s = w.FindSettlement(new SettlementId(g));
                    if (s != null) return s.FactionId.IsEmpty ? null : s.FactionId.ToString();
                    var n = w.FindNpc(new NpcId(g));
                    return n == null || n.FactionId.IsEmpty ? null : n.FactionId.ToString();
                }
                case CampaignRoleRule.HouseholdOf:
                {
                    if (of == null || !Guid.TryParse(of, out var g)) return null;
                    var who = new NpcId(g);
                    NpcData best = null;
                    foreach (var r in w.NpcRelations)
                    {
                        if (r.Kind != NpcRelationKind.Household || (r.NpcA != who && r.NpcB != who)) continue;
                        var other = w.FindNpc(r.NpcA == who ? r.NpcB : r.NpcA);
                        if (other == null || other.IsDeceased || cast.Contains(other.NpcId.ToString())) continue;
                        if (best == null || other.NpcId.CompareTo(best.NpcId) < 0) best = other;
                    }

                    return best?.NpcId.ToString();
                }
                case CampaignRoleRule.RegionOf:
                {
                    if (of == null || !Guid.TryParse(of, out var g)) return null;
                    return w.FindSettlement(new SettlementId(g))?.RegionId.ToString();
                }
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// What has become of a person (ADR-0084), read from the systems that decide it. A campaign asks; it never sets.
    /// </summary>
    public static class CampaignCharacters
    {
        /// <summary>Hostility at which a person will no longer deal with the player (the same line shops use).</summary>
        public const int HostileAt = 70;

        public static CharacterStatus StatusOf(WorldData w, NpcData n)
        {
            if (n == null) return CharacterStatus.Missing;
            if (n.IsDeceased) return CharacterStatus.Dead;
            var escort = w.FindEscort(n.NpcId);
            if (escort != null && escort.State == EscortState.Captive) return CharacterStatus.Captured;
            if (w.Party != null && w.Party.Contains(n.NpcId)) return CharacterStatus.Allied;
            if (w.FindCompanion(n.NpcId) is CompanionRecordData rec && rec.Standing == CompanionStanding.Departed) return CharacterStatus.Departed;
            if (n.PlayerRegard != null && n.PlayerRegard.Hostility >= HostileAt) return CharacterStatus.Hostile;
            if (n.HomeSettlementId.IsEmpty || w.FindSettlement(n.HomeSettlementId) == null) return CharacterStatus.Missing;
            if (Places.PlacePresence.IsAway(w, n)) return CharacterStatus.Unavailable;
            if (n.Condition != null && n.Condition.Health < 40) return CharacterStatus.Injured;
            return CharacterStatus.Available;
        }

        /// <summary>How the journal puts a status.</summary>
        public static string Words(CharacterStatus s)
        {
            switch (s)
            {
                case CharacterStatus.Unavailable: return "away from home";
                case CharacterStatus.Missing: return "missing";
                case CharacterStatus.Captured: return "held captive";
                case CharacterStatus.Injured: return "hurt";
                case CharacterStatus.Dead: return "dead";
                case CharacterStatus.Departed: return "left your company";
                case CharacterStatus.Hostile: return "hostile to you";
                case CharacterStatus.Allied: return "travelling with you";
                default: return "can be found at home";
            }
        }
    }

    /// <summary>
    /// The campaign side of the shared condition evaluator (ADR-0083): campaign arcs, decisions and flags; what has
    /// become of a person; world events, roads, settlements and companions as roles name them. The application's
    /// evaluator hands these kinds here, and campaign arcs use them directly in the day's simulation.
    /// </summary>
    public static class CampaignConditions
    {
        public static bool All(WorldData w, CampaignInstanceData c, QuestInstanceData q, NpcData npc, IReadOnlyList<QuestCondition> conditions)
        {
            if (conditions == null) return true;
            foreach (var x in conditions) if (!Holds(w, c, q, npc, x)) return false;
            return true;
        }

        public static bool Holds(WorldData w, CampaignInstanceData c, QuestInstanceData q, NpcData npc, QuestCondition x)
        {
            var result = Raw(w, c, q, npc, x);
            return x.Negate ? !result : result;
        }

        /// <summary>True for the kinds this evaluator answers (the application's evaluator asks before handing over).</summary>
        public static bool Handles(ConditionKind kind) => kind >= ConditionKind.CampaignArc && kind <= ConditionKind.CampaignStanding;

        private static bool In(string values, string value)
        {
            if (values == null) return true;
            foreach (var v in values.Split('|')) if (v == value) return true;
            return false;
        }

        private static bool InRange(QuestCondition x, int v) => v >= x.Min && v <= x.Max;

        /// <summary>A role's person: a campaign role, else the quest's, else (for "npc" or none) the person spoken to.</summary>
        private static NpcData Person(WorldData w, CampaignInstanceData c, QuestInstanceData q, NpcData npc, string role)
        {
            if (role == null || role == "npc") return npc;
            if (c != null && CampaignEngine.Definition(c)?.Role(role) != null) return CampaignEngine.Npc(w, c, role);
            return q == null ? null : QuestEffects.Person(w, q, role);
        }

        private static SettlementData Place(WorldData w, CampaignInstanceData c, QuestInstanceData q, string role)
        {
            if (role == null || role == "here") return w.Player != null && w.Player.IsInSettlement ? w.FindSettlement(w.Player.SettlementId) : null;
            if (c != null && CampaignEngine.Definition(c)?.Role(role) != null) return CampaignEngine.Settlement(w, c, role);
            return q == null ? null : QuestEffects.SettlementOf(w, q, role);
        }

        private static bool Raw(WorldData w, CampaignInstanceData c, QuestInstanceData q, NpcData npc, QuestCondition x)
        {
            var now = QuestClock.Now(w);
            switch (x.Kind)
            {
                case ConditionKind.CampaignArc:
                {
                    var a = c?.Arc(x.Key);
                    return a != null && In(x.Value, a.State.ToString());
                }
                case ConditionKind.CampaignDecision:
                {
                    var d = c?.Decision(x.Key);
                    return d != null && In(x.Value, d.ChoiceId);
                }
                case ConditionKind.CampaignFlag:
                    return c != null && c.Flags.TryGetValue(x.Key ?? string.Empty, out var flag) && (x.Value == null ? !string.IsNullOrEmpty(flag) : In(x.Value, flag));
                case ConditionKind.CampaignState:
                    return c != null && In(x.Value, c.State.ToString());
                case ConditionKind.CharacterStatus:
                {
                    var who = Person(w, c, q, npc, x.Key);
                    var status = who == null ? CharacterStatus.Missing : CampaignCharacters.StatusOf(w, who);
                    return In(x.Value, status.ToString());
                }
                case ConditionKind.WorldEventOpen:
                {
                    if (!Enum.TryParse<WorldEventType>(x.Key, out var type)) return false;
                    var route = x.Value != null && c != null ? CampaignEngine.Route(w, c, x.Value) : null;
                    var place = x.Value != null && route == null ? Place(w, c, q, x.Value) : null;
                    foreach (var e in w.WorldEvents)
                    {
                        if (!e.IsOpen || e.Type != type) continue;
                        if (x.Value == null) return true;
                        if (route != null && e.RouteId == route.RouteId) return true;
                        if (place != null && (e.SettlementId == place.SettlementId || (w.FindRoute(e.RouteId) is RouteSegmentData er && er.Joins(place.SettlementId)))) return true;
                    }

                    return false;
                }
                case ConditionKind.CompanionLoyalty:
                {
                    var who = Person(w, c, q, npc, x.Key);
                    var rec = who == null ? null : w.FindCompanion(who.NpcId);
                    return rec != null && InRange(x, rec.Loyalty);
                }
                case ConditionKind.SettlementStability:
                {
                    var s = Place(w, c, q, x.Key);
                    return s != null && InRange(x, s.Stability);
                }
                case ConditionKind.RouteOpen:
                {
                    var r = c == null ? null : CampaignEngine.Route(w, c, x.Key);
                    return r != null && r.Passable;
                }
                case ConditionKind.NpcRelation:
                {
                    var parts = (x.Key ?? string.Empty).Split(',');
                    if (parts.Length != 2) return false;
                    var a = Person(w, c, q, npc, parts[0]);
                    var b = Person(w, c, q, npc, parts[1]);
                    if (a == null || b == null) return false;
                    return InRange(x, Relations.Read(w.FindNpcRelation(a.NpcId, b.NpcId), x.Value));
                }
                case ConditionKind.HasRemark:
                {
                    var rec = npc == null ? null : w.FindCompanion(npc.NpcId);
                    if (rec == null) return false;
                    foreach (var r in rec.Remarks) if (!r.Heard) return true;
                    return false;
                }
                case ConditionKind.CampaignArcDays:
                {
                    var a = c?.Arc(x.Key);
                    return a != null && a.StartedAt >= 0 && InRange(x, (int)((now - a.StartedAt) / Domain.Calendar.WorldTime.MinutesPerDay));
                }
                case ConditionKind.CampaignQuest:
                {
                    var qq = c != null && c.Quests.TryGetValue(x.Key ?? string.Empty, out var id) ? w.FindQuest(id) : null;
                    return In(x.Value, qq?.State.ToString() ?? "None");
                }
                case ConditionKind.CampaignStanding:
                {
                    var f = c == null ? null : CampaignEngine.Faction(w, c, x.Key);
                    if (f == null && Person(w, c, q, npc, x.Key) is NpcData who && !who.FactionId.IsEmpty) f = w.FindFaction(who.FactionId);
                    return f != null && w.Player != null && InRange(x, w.Player.Social.StandingWith(f.FactionId));
                }
                default:
                    return false;
            }
        }
    }
}
