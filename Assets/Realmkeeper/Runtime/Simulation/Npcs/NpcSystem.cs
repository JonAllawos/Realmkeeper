using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Simulation.Npcs
{
    /// <summary>
    /// The NPC Intelligence Engine's daily system (order 425: after population, before politics).
    /// It runs inside the day's transaction, so a failed day leaves no person half-updated.
    ///
    /// Each day:
    /// 1. Named people and the population are reconciled. Named people are members of it, never
    ///    extra, and tier 3 is the rest. When the population falls below the living named count,
    ///    the least notable named residents die.
    /// 2. People who are due are updated: tier 1 (notable) every day, tier 2 once a week on a day
    ///    fixed by their ID. An update covers condition, what they see, goals and where they go.
    /// 3. People who share a location talk (a few pairs per place). Ties form or change, and one
    ///    fact may pass on as hearsay.
    /// 4. They react to today's notable events: memories, knowledge, morale.
    /// 5. On the weekly tick, memories fade, first-hand knowledge goes stale, rumours expire and
    ///    unused ties drift back toward neutral.
    ///
    /// Choices hash (seed, date, person), so the outcome does not depend on order or shared randomness.
    /// </summary>
    public sealed class NpcSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.npcs";
        public const string SimulationSource = "simulation";

        private readonly NpcRules _rules;

        public NpcSystem(NpcRules rules = null)
        {
            _rules = rules ?? new NpcRules();
        }

        public string SystemId => Id;
        public int ExecutionOrder => 425;

        public NpcRules Rules => _rules;

        /// <summary>True when a tier-2 person is due on this date (their weekday, fixed by ID).</summary>
        public static bool IsDue(WorldData world, NpcData npc, WorldDate date) =>
            npc.Tier == NpcTier.Notable || NpcHash.Roll(NpcHash.Of(world.Seed, "tier2-day", npc.NpcId.ToString()), NpcScheduleData.Days) == NpcScheduleData.WeekdayOf(date);

        public void Execute(SimulationDayContext context)
        {
            var world = context.World;
            if (world.Npcs.Count == 0 || world.Locations.Count == 0) return; // not an NPC-engine world yet
            var today = context.Date;
            var dayNumber = today.ToDayNumber();

            Reconcile(context);

            var settlements = new Dictionary<SettlementId, SettlementData>();
            foreach (var s in world.Settlements) settlements[s.SettlementId] = s;

            foreach (var npc in world.Npcs)
            {
                if (npc.IsDeceased || npc.Tier == NpcTier.Unassigned) continue;
                RestOvernight(world, npc, today); // Phase 10: sheet-holders sleep and recover, every night
                if (!IsDue(world, npc, today))
                {
                    // Not due for a full update, but everyone keeps to their routine every day (cheap).
                    Move(world, npc, null, today);
                    continue;
                }

                var days = npc.LastUpdatedOn.IsValidDate ? Math.Max(1, Math.Min(7, npc.LastUpdatedOn.DaysUntil(today))) : 1;
                settlements.TryGetValue(npc.HomeSettlementId, out var home);
                UpdateCondition(npc, home, days, today);
                UpdateGoals(context, npc);
                Move(world, npc, home, today);
                var here = NpcMind.SettlementAt(world, npc.CurrentLocationId) ?? home;
                NpcMind.Observe(world, npc, here, today, KnowledgeIds(world, npc, today));
                npc.LastUpdatedOn = today;
            }

            Converse(context);
            React(context);
            DiscoverCrimes(context); // Phase 14 (ADR-0069)
            if (dayNumber % WorldDate.DaysPerWeek == 0) Weekly(context);
        }

        /// <summary>
        /// Phase 10: a person with a character sheet sleeps each night — an SRD long rest (hit points, hit dice, resources,
        /// spell preparation) when anything needs recovering. Someone unconscious but stable comes round with 1 hit point
        /// first. Not while they are in a fight; the dead stay dead.
        /// </summary>
        public static void RestOvernight(WorldData world, NpcData npc, WorldDate today)
        {
            var sheet = npc.Character?.Sheet;
            if (sheet == null || sheet.State != Domain.Rpg.CreationState.Complete || sheet.IsDead || !today.IsValidDate) return;
            var fight = world.ActiveEncounter;
            if (fight != null) foreach (var c in fight.Combatants) if (c.SourceId == npc.NpcId.ToString()) return;
            if (world.Party?.Find(npc.NpcId) is PartyMemberData member && member.Role == PartyRole.Active) return; // Phase 15: they rest when the party rests
            var rules = new Domain.Rpg.CharacterRules(sheet);
            var tired = sheet.CurrentHp < rules.MaxHp;
            foreach (var pair in rules.ResourceMaximums()) if (!sheet.Resources.TryGetValue(pair.Key, out var left) || left < pair.Value) tired = true;
            if (!tired) return;
            if (sheet.CurrentHp <= 0)
            {
                sheet.CurrentHp = 1;
                sheet.IsStable = false;
                sheet.DeathSaveSuccesses = sheet.DeathSaveFailures = 0;
            }

            var endedAt = today.ToDayNumber() * 1440L + 360L; // they wake at six
            if (Domain.Rpg.CharacterRest.LongRestProblem(sheet, endedAt - Domain.Rpg.CharacterRest.LongRestMinutes) != null) return;
            Domain.Rpg.CharacterRest.LongRest(sheet, endedAt, "night:" + today.ToDayNumber());
        }

        // ----- 1. Population ---------------------------------------------------------------------

        private void Reconcile(SimulationDayContext context)
        {
            var world = context.World;
            foreach (var s in world.Settlements)
            {
                var living = new List<NpcData>();
                foreach (var n in world.Npcs) if (n.IsAlive && n.HomeSettlementId == s.SettlementId) living.Add(n);
                var excess = living.Count - Math.Max(0, s.Population);
                if (excess <= 0) continue;

                // The least notable go first; among equals, a stable order by ID.
                living.Sort((a, b) =>
                {
                    var t = ((int)b.Tier).CompareTo((int)a.Tier);
                    return t != 0 ? t : a.NpcId.CompareTo(b.NpcId);
                });
                for (var i = 0; i < excess && i < living.Count; i++) Die(context, living[i], s);
            }
        }

        private void Die(SimulationDayContext context, NpcData npc, SettlementData s)
        {
            context.ReportEvent(HistoricalEventType.NpcDied, EventSeverity.Critical, EventImportance.Notable, new[] { s.SettlementId },
                new EventData().Set("source", SimulationSource).Set("npc", npc.NpcId.ToString()).Set("name", npc.Name).Set("role", npc.Role.ToString()),
                npcIds: new[] { npc.NpcId });
            RecordDeath(context.World, npc, s, context.Date);
        }

        /// <summary>
        /// The world consequences of a resident's death, shared by every cause (Phase 8: old age and population in the daily
        /// simulation, and combat): dead with a date, goals abandoned, faction leadership passed to the most notable living
        /// member, and those close to them remembering it. The caller reports the chronicle event.
        /// </summary>
        public static void RecordDeath(WorldData world, NpcData npc, SettlementData s, WorldDate date)
        {
            npc.IsDeceased = true;
            npc.DiedOn = date;
            npc.CurrentLocationId = default;
            foreach (var g in npc.Goals)
            {
                if (!g.IsActive) continue;
                g.Status = NpcGoalStatus.Abandoned;
                g.ClosedOn = date;
            }

            // A faction never stays led by the dead: the most notable living member takes over, if any.
            foreach (var f in world.Factions)
            {
                if (f.LeaderNpcId != npc.NpcId) continue;
                NpcData successor = null;
                foreach (var n in world.Npcs)
                {
                    if (n.IsDeceased || n.FactionId != f.FactionId) continue;
                    if (successor == null || (int)n.Tier < (int)successor.Tier || n.Tier == successor.Tier && n.NpcId.CompareTo(successor.NpcId) < 0) successor = n;
                }

                f.LeaderNpcId = successor?.NpcId ?? default;
                if (successor != null) successor.Tier = NpcTier.Notable;
            }

            // Those close to them remember.
            foreach (var r in world.NpcRelations)
            {
                if (!r.Involves(npc.NpcId) || r.Affinity < 10) continue;
                var other = world.FindNpc(r.Other(npc.NpcId));
                if (other == null || other.IsDeceased) continue;
                NpcMind.Remember(other, new NpcMemoryData
                {
                    MemoryId = MemoryIdFor(world, other, date, "death:" + npc.NpcId),
                    Date = date,
                    Category = MemoryCategory.Death,
                    Subject = HistoricalEventType.NpcDied.ToString(),
                    SettlementId = s?.SettlementId ?? npc.HomeSettlementId,
                    NpcIds = new List<NpcId> { npc.NpcId },
                    Salience = Math.Min(100, 50 + r.Affinity / 2),
                    Source = r.Kind == NpcRelationKind.Household ? MemorySource.Experienced : MemorySource.Learned,
                });
                other.Condition.Morale -= 10 + r.Affinity / 10;
            }
        }

        // ----- 2. Condition, goals, movement -----------------------------------------------------------

        private void UpdateCondition(NpcData npc, SettlementData home, int days, WorldDate today)
        {
            var c = npc.Condition;
            var food = home == null ? "plenty" : NpcMind.FoodState(home);
            var satiationStep = food == "starving" ? -15 : food == "short" ? -6 : 10;
            c.Satiation = food == "plenty" ? Math.Min(85, c.Satiation + satiationStep * days) : c.Satiation + satiationStep * days;
            c.Health += c.Satiation < 20 ? -5 * days : c.Health < 100 ? 2 * days : 0;

            var resting = npc.Schedule.For(today) != npc.WorkplaceId || npc.WorkplaceId.IsEmpty;
            // A day's work tires but a night's sleep restores part of it (a working day settles near 55);
            // a rest day restores more. A tier-2 week settles in between.
            if (days == 1) c.Energy += resting ? 15 : -3 + (80 - c.Energy) / 10;
            else c.Energy += (70 - c.Energy) / 2;
            if (home != null)
            {
                var target = NpcBounds.Clamp(home.Stability, 0, 100);
                c.Safety += Math.Sign(target - c.Safety) * Math.Min(Math.Abs(target - c.Safety), 5 * days);
            }

            // Wages for days worked (a week counts six), and living costs.
            if (!npc.WorkplaceId.IsEmpty)
            {
                var worked = days == 1 ? (resting ? 0 : 1) : days * 6 / 7;
                var wage = _rules.BaseWage + npc.Skills[NpcGenesis.PrimarySkill(npc.Role)] / 25 + (home != null ? home.Prosperity / 50 : 0);
                c.Wealth += worked * wage;
            }

            var spending = (1 + npc.Personality[NpcTrait.Greed] / 60) * days / 2;
            c.Wealth -= spending;

            var social = 0;
            foreach (var g in npc.Goals) if (g.IsActive && g.Progress >= 75) social += 2;
            c.Morale = (c.Health + c.Satiation + c.Energy + c.Safety) / 4 + social;
        }

        private void UpdateGoals(SimulationDayContext context, NpcData npc)
        {
            var c = npc.Condition;
            var today = context.Date;
            var active = 0;
            foreach (var g in npc.Goals) if (g.IsActive) active++;

            void Open(NpcGoalType type, int priority, NpcSkill? skill = null, bool need = false)
            {
                foreach (var g in npc.Goals)
                {
                    if (g.Type != type) continue;
                    if (g.IsActive) { g.Priority = Math.Max(g.Priority, priority); return; }
                    if (g.ClosedOn.IsValidDate && g.ClosedOn.DaysUntil(today) < GoalCooldownDays) return; // not straight back to it
                }

                if (!need && active >= NpcBounds.MaxActiveGoals) return; // urgent needs are never crowded out by ambitions
                var goal = new NpcGoalData
                {
                    GoalId = new GoalId(NpcHash.Guid(NpcHash.Of(context.World.Seed, "goal-" + type, npc.NpcId.ToString(), today.ToDayNumber()))),
                    Type = type,
                    Priority = priority,
                    Status = NpcGoalStatus.Active,
                    CreatedOn = today,
                    Source = ChangeSource.Simulation,
                    TargetSkill = skill,
                };
                npc.Goals.Add(goal);
                active++;
                if (npc.Tier == NpcTier.Notable && IsAmbition(type)) ReportGoal(context, npc, goal, HistoricalEventType.NpcGoalSet);
            }

            // Needs open goals.
            if (c.Satiation < 40) Open(NpcGoalType.SecureFood, 90 - c.Satiation, need: true);
            if (c.Health < 50) Open(NpcGoalType.Recover, 100 - c.Health, need: true);
            if (c.Safety < 40) Open(NpcGoalType.SeekSafety, 80 - c.Safety, need: true);

            foreach (var g in npc.Goals)
            {
                if (!g.IsActive) continue;
                switch (g.Type)
                {
                    case NpcGoalType.SecureFood: g.Progress = c.Satiation * 100 / 70; break;
                    case NpcGoalType.Recover: g.Progress = c.Health * 100 / 80; break;
                    case NpcGoalType.SeekSafety: g.Progress = c.Safety * 100 / 60; break;
                    case NpcGoalType.EarnLiving: g.Progress = Math.Min(100, c.Wealth); break;
                    case NpcGoalType.AcquireWealth: g.Progress = Math.Min(100, c.Wealth / 3); break;
                    case NpcGoalType.GainStanding: g.Progress = Math.Min(100, g.Progress + npc.Personality[NpcTrait.Ambition] / 25); break;
                    case NpcGoalType.Socialise: g.Progress = Math.Min(100, Friends(context.World, npc) * 34); break;
                    case NpcGoalType.LearnSkill: g.Progress = Math.Min(100, g.Progress + 1 + npc.Personality[NpcTrait.Curiosity] / 30); break;
                    case NpcGoalType.ServeFaction: g.Progress = npc.FactionId.IsEmpty ? 0 : 50; break;
                }

                g.Progress = NpcBounds.Clamp(g.Progress, 0, 100);
                if (g.Progress < 100 || IsOngoing(g.Type)) continue;
                g.Status = NpcGoalStatus.Achieved;
                g.ClosedOn = today;
                active--;
                if (g.Type == NpcGoalType.LearnSkill && g.TargetSkill.HasValue) npc.Skills[g.TargetSkill.Value] += 5;
                if (npc.Tier == NpcTier.Notable && IsAmbition(g.Type)) ReportGoal(context, npc, g, HistoricalEventType.NpcGoalAchieved);
            }

            // Achieved goals give way to the next motivation.
            if (c.Wealth >= 100 && npc.Personality[NpcTrait.Greed] >= 55) Open(NpcGoalType.AcquireWealth, 40);
            if (!npc.WorkplaceId.IsEmpty) Open(NpcGoalType.EarnLiving, 50);

            // Keep the record bounded: closed goals older than a season are dropped.
            npc.Goals.RemoveAll(g => !g.IsActive && g.ClosedOn.IsValidDate && g.ClosedOn.DaysUntil(today) > 90);
        }

        /// <summary>Days before a goal of the same type can be taken up again after it closed.</summary>
        public const int GoalCooldownDays = 30;

        /// <summary>Goals that are a way of life rather than something finished: earning a living, serving a faction.</summary>
        public static bool IsOngoing(NpcGoalType type) => type == NpcGoalType.EarnLiving || type == NpcGoalType.ServeFaction;

        /// <summary>Goals worth a chronicle entry for notable people (needs come and go every week and are not).</summary>
        public static bool IsAmbition(NpcGoalType type) =>
            type == NpcGoalType.GainStanding || type == NpcGoalType.AcquireWealth || type == NpcGoalType.LearnSkill || type == NpcGoalType.Socialise;

        private static int Friends(WorldData world, NpcData npc)
        {
            var count = 0;
            foreach (var r in world.NpcRelations) if (r.Involves(npc.NpcId) && r.Kind == NpcRelationKind.Friend) count++;
            return count;
        }

        private void ReportGoal(SimulationDayContext context, NpcData npc, NpcGoalData goal, HistoricalEventType type)
        {
            context.ReportEvent(type, type == HistoricalEventType.NpcGoalAchieved ? EventSeverity.Positive : EventSeverity.Info, EventImportance.Minor,
                npc.HomeSettlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { npc.HomeSettlementId },
                new EventData().Set("source", SimulationSource).Set("npc", npc.NpcId.ToString()).Set("name", npc.Name).Set("goal", goal.Type.ToString()),
                npcIds: new[] { npc.NpcId });
        }

        /// <summary>Where a person spends today: their schedule, unless a pressing need sends them elsewhere.</summary>
        public static LocationId PlaceFor(WorldData world, NpcData npc, WorldDate today)
        {
            var settlement = npc.HomeSettlementId;
            NpcGoalData top = null;
            foreach (var g in npc.Goals) if (g.IsActive && (top == null || g.Priority > top.Priority)) top = g;
            if (top != null && top.Priority >= 60)
            {
                switch (top.Type)
                {
                    case NpcGoalType.Recover: if (!npc.ResidenceId.IsEmpty) return npc.ResidenceId; break;
                    case NpcGoalType.SecureFood:
                        var market = NpcGenesis.FirstOf(world, settlement, LocationKind.Market);
                        if (market != null) return market.LocationId;
                        break;
                    case NpcGoalType.SeekSafety:
                        var keep = NpcGenesis.FirstOf(world, settlement, LocationKind.Administration, LocationKind.Worship);
                        if (keep != null) return keep.LocationId;
                        break;
                }
            }

            var planned = npc.Schedule.For(today);
            return planned.IsEmpty ? npc.ResidenceId : planned;
        }

        private static void Move(WorldData world, NpcData npc, SettlementData home, WorldDate today)
        {
            var place = PlaceFor(world, npc, today);
            if (!place.IsEmpty) npc.CurrentLocationId = place;
        }

        // ----- 3. Conversations ----------------------------------------------------------------------

        private void Converse(SimulationDayContext context)
        {
            var world = context.World;
            var today = context.Date;
            var byPlace = new SortedDictionary<LocationId, List<NpcData>>();
            foreach (var n in world.Npcs)
            {
                if (n.IsDeceased || n.Tier == NpcTier.Unassigned || n.CurrentLocationId.IsEmpty) continue;
                if (!byPlace.TryGetValue(n.CurrentLocationId, out var list)) byPlace[n.CurrentLocationId] = list = new List<NpcData>();
                list.Add(n);
            }

            foreach (var pair in byPlace)
            {
                var people = pair.Value;
                if (people.Count < 2) continue;
                people.Sort((a, b) => a.NpcId.CompareTo(b.NpcId));
                var seed = NpcHash.Of(world.Seed, "converse", pair.Key.ToString(), today.ToDayNumber());
                for (var t = 0; t < _rules.ConversationsPerLocationPerDay; t++)
                {
                    var i = NpcHash.Roll(NpcHash.Next(seed, 2 * t), people.Count);
                    var j = NpcHash.Roll(NpcHash.Next(seed, 2 * t + 1), people.Count - 1);
                    if (j >= i) j++;
                    Talk(context, people[i], people[j], NpcHash.Next(seed, 100 + t));
                }
            }

            // Evenings: neighbours meet (one conversation per settlement), so word gets around even
            // between people who work apart.
            var bySettlement = new SortedDictionary<SettlementId, List<NpcData>>();
            foreach (var n in world.Npcs)
            {
                if (n.IsDeceased || n.Tier == NpcTier.Unassigned || n.HomeSettlementId.IsEmpty) continue;
                if (!bySettlement.TryGetValue(n.HomeSettlementId, out var list)) bySettlement[n.HomeSettlementId] = list = new List<NpcData>();
                list.Add(n);
            }

            foreach (var pair in bySettlement)
            {
                var people = pair.Value;
                if (people.Count < 2) continue;
                people.Sort((a, b) => a.NpcId.CompareTo(b.NpcId));
                var seed = NpcHash.Of(world.Seed, "evening", pair.Key.ToString(), today.ToDayNumber());
                var evenings = Math.Max(_rules.EveningConversationsPerSettlement, people.Count / 6);
                for (var t = 0; t < evenings; t++)
                {
                    var i = NpcHash.Roll(NpcHash.Next(seed, 2 * t), people.Count);
                    var j = NpcHash.Roll(NpcHash.Next(seed, 2 * t + 1), people.Count - 1);
                    if (j >= i) j++;
                    Talk(context, people[i], people[j], NpcHash.Next(seed, 100 + t));
                }
            }
        }

        private void Talk(SimulationDayContext context, NpcData a, NpcData b, ulong seed)
        {
            var world = context.World;
            var today = context.Date;
            // The more sociable of the two has to want to talk.
            var willing = Math.Max(a.Personality[NpcTrait.Sociability], b.Personality[NpcTrait.Sociability]);
            if (NpcHash.Roll(seed, 100) >= willing + 20) return;

            var r = NpcMind.Relate(world, a.NpcId, b.NpcId, NpcRelationCause.Conversation, NpcRelationKind.Acquaintance, today);
            if (r == null) return; // no room for another acquaintance
            var before = r.Kind;
            var compatibility = 100 - Math.Abs(a.Personality[NpcTrait.Sociability] - b.Personality[NpcTrait.Sociability])
                                - Math.Abs(a.Personality[NpcTrait.Greed] - b.Personality[NpcTrait.Greed]) / 2;
            var warmth = (a.Personality[NpcTrait.Compassion] + b.Personality[NpcTrait.Compassion]) / 40;
            var delta = compatibility >= 60 ? 2 + warmth : compatibility >= 35 ? 1 : -3;
            r.Affinity += delta;
            r.Trust += delta > 0 ? 1 : -2;
            if (delta < 0) r.Rivalry += 2;
            r.Interactions++;
            r.LastCause = delta < 0 ? NpcRelationCause.Quarrel : NpcRelationCause.Conversation;
            r.LastChanged = today;
            r.Kind = NpcMind.KindFor(r, _rules);
            if (r.Kind != before && (r.Kind == NpcRelationKind.Friend || r.Kind == NpcRelationKind.Rival))
            {
                context.ReportEvent(HistoricalEventType.NpcRelationshipChanged, r.Kind == NpcRelationKind.Friend ? EventSeverity.Positive : EventSeverity.Warning,
                    EventImportance.Minor, a.HomeSettlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { a.HomeSettlementId },
                    new EventData().Set("source", SimulationSource).Set("from", before.ToString()).Set("to", r.Kind.ToString())
                        .Set("cause", r.LastCause.ToString()).Set("nameA", a.Name).Set("nameB", b.Name),
                    npcIds: new[] { r.NpcA, r.NpcB });
            }

            // They now know where the other can be found (first-hand).
            NpcMind.Learn(a, KnowledgeTopic.Whereabouts, b.NpcId.ToString(), b.CurrentLocationId.ToString(), KnowledgeStatus.Known, 100, today, default, 0, default,
                KnowledgeIds(world, a, today, "met"));
            NpcMind.Learn(b, KnowledgeTopic.Whereabouts, a.NpcId.ToString(), a.CurrentLocationId.ToString(), KnowledgeStatus.Known, 100, today, default, 0, default,
                KnowledgeIds(world, b, today, "met"));

            // One fact passes from the better-informed to the other, as hearsay.
            Share(world, a, b, r, today, NpcHash.Next(seed, 7));
            Share(world, b, a, r, today, NpcHash.Next(seed, 8));
        }

        private void Share(WorldData world, NpcData from, NpcData to, NpcRelationData tie, WorldDate today, ulong seed)
        {
            if (from.Knowledge.Count == 0 || NpcHash.Roll(seed, 2) == 1) return;
            NpcKnowledgeData best = null;
            foreach (var k in from.Knowledge)
            {
                if (k.Confidence < _rules.MinShareConfidence || k.Hops >= _rules.MaxHops) continue;
                var theirs = NpcMind.Find(to, k.Topic, k.SubjectId);
                if (theirs != null && (theirs.Status == KnowledgeStatus.Known || theirs.Value == k.Value)) continue; // nothing new to them
                if (best == null || Interest(k) > Interest(best) || Interest(k) == Interest(best) && k.KnowledgeId.CompareTo(best.KnowledgeId) < 0) best = k;
            }

            if (best == null) return;
            var confidence = best.Confidence - _rules.RetellingConfidenceLoss + (tie.Trust - 50) / 5 + (to.Personality[NpcTrait.Trustfulness] - 50) / 10;
            if (confidence < 10) return;
            var status = confidence >= 60 && tie.Trust >= 60 ? KnowledgeStatus.Believed : KnowledgeStatus.Rumor;
            NpcMind.Learn(to, best.Topic, best.SubjectId, best.Value, status, confidence, today, from.NpcId, best.Hops + 1, best.EventId,
                KnowledgeIds(world, to, today, "heard"));
        }

        /// <summary>What people would rather talk about: news of places, factions and events before who is where.</summary>
        private static int Interest(NpcKnowledgeData k) => k.Confidence + (k.Topic == KnowledgeTopic.Whereabouts ? -60 : k.Topic == KnowledgeTopic.RecentEvent ? 20 : k.Topic == KnowledgeTopic.PlayerDeed ? 30 : 0) - k.Hops * 5;

        // ----- 4. Reactions to events ----------------------------------------------------------------------

        private void React(SimulationDayContext context)
        {
            var world = context.World;
            var events = context.Events;
            if (events.Count == 0) return;
            for (var e = 0; e < events.Count; e++)
            {
                var ev = events[e];
                if (ev.EventType == HistoricalEventType.NpcDied) continue; // handled where the death happens
                var minor = ev.Importance == EventImportance.Minor;
                var category = CategoryOf(ev);
                if (category == null) continue;
                foreach (var npc in world.Npcs)
                {
                    if (npc.IsDeceased || npc.Tier == NpcTier.Unassigned) continue;
                    var inPlace = false;
                    foreach (var s in ev.SettlementIds) if (s == npc.HomeSettlementId) inPlace = true;
                    var inFaction = false;
                    foreach (var f in ev.FactionIds) if (f == npc.FactionId) inFaction = true;
                    if (!inPlace && !inFaction) continue;

                    var source = inPlace && (category == MemoryCategory.Hardship || category == MemoryCategory.Prosperity) ? MemorySource.Experienced
                        : inPlace ? MemorySource.Witnessed : MemorySource.Learned;
                    var salience = (ev.Importance == EventImportance.Major ? 70 : minor ? 25 : 45) + (source == MemorySource.Experienced ? 15 : 0);
                    NpcMind.Remember(npc, new NpcMemoryData
                    {
                        MemoryId = MemoryIdFor(world, npc, context.Date, ev.EventId.ToString()),
                        EventId = ev.EventId,
                        Date = ev.Date,
                        Category = category.Value,
                        Subject = ev.EventType.ToString(),
                        SettlementId = ev.SettlementId,
                        FactionId = ev.FactionIds.Count > 0 ? ev.FactionIds[0] : default,
                        NpcIds = new List<NpcId>(ev.NpcIds),
                        Salience = salience,
                        Confidence = source == MemorySource.Learned ? 80 : 100,
                        Source = source,
                    });
                    NpcMind.Learn(npc, KnowledgeTopic.RecentEvent, ev.EventId.ToString(), ev.EventType.ToString(),
                        source == MemorySource.Learned ? KnowledgeStatus.Believed : KnowledgeStatus.Known, source == MemorySource.Learned ? 80 : 100,
                        context.Date, default, 0, ev.EventId, KnowledgeIds(world, npc, context.Date, "event"));
                    if (category == MemoryCategory.Hardship) npc.Condition.Morale -= 6;
                    if (category == MemoryCategory.Prosperity) npc.Condition.Morale += 4;
                }
            }
        }

        /// <summary>How people file an event in memory (null: not memorable), by type and, for changes, direction.</summary>
        public static MemoryCategory? CategoryOf(HistoricalEvent e)
        {
            switch (e.EventType)
            {
                case HistoricalEventType.ProsperityChanged:
                case HistoricalEventType.StabilityChanged:
                    return e.GetInt("to") >= e.GetInt("from") ? MemoryCategory.Prosperity : MemoryCategory.Hardship;
                case HistoricalEventType.SettlementGrew:
                case HistoricalEventType.EconomicMilestone:
                    return MemoryCategory.Prosperity;
            }

            return CategoryOf(e.EventType);
        }

        public static MemoryCategory? CategoryOf(HistoricalEventType type)
        {
            switch (type)
            {
                case HistoricalEventType.FoodShortageBegan:
                case HistoricalEventType.FoodReservesExhausted:
                case HistoricalEventType.StarvationBegan:
                case HistoricalEventType.ResourceShortageBegan:
                    return MemoryCategory.Hardship;
                case HistoricalEventType.FoodShortageEnded:
                case HistoricalEventType.ResourceShortageEnded:
                case HistoricalEventType.TradeShipmentDelivered:
                    return MemoryCategory.Prosperity;
                case HistoricalEventType.RelationshipChanged:
                case HistoricalEventType.AgreementEstablished:
                case HistoricalEventType.AgreementBroken:
                case HistoricalEventType.TerritorialClaimResolved:
                case HistoricalEventType.AffiliationShifted:
                case HistoricalEventType.FactionPresenceEstablished:
                case HistoricalEventType.FactionPresenceWithdrawn:
                case HistoricalEventType.FactionInfluenceChanged:
                case HistoricalEventType.FactionGoalCompleted:
                    return MemoryCategory.Politics;
                default:
                    return null;
            }
        }

        // ----- 5. Weekly ------------------------------------------------------------------------------

        private void Weekly(SimulationDayContext context)
        {
            var world = context.World;
            var today = context.Date;
            foreach (var npc in world.Npcs)
            {
                if (npc.IsDeceased) continue;
                foreach (var m in npc.Memories) m.Salience -= _rules.MemoryDecayPerWeek;
                npc.Memories.RemoveAll(m => m.Salience < _rules.ForgetBelowSalience);
                foreach (var k in npc.Knowledge)
                {
                    if (k.Status == KnowledgeStatus.Known && k.LearnedOn.DaysUntil(today) > _rules.StaleAfterDays)
                    {
                        k.Status = KnowledgeStatus.Believed;
                        k.Confidence -= 15;
                    }
                }

                npc.Knowledge.RemoveAll(k => k.Status == KnowledgeStatus.Rumor && k.LearnedOn.DaysUntil(today) > _rules.RumourLifetimeDays);
            }

            foreach (var r in world.NpcRelations)
            {
                if (r.Kind == NpcRelationKind.Household || r.LastChanged.IsValidDate && r.LastChanged.DaysUntil(today) < 7) continue;
                if (r.Affinity > 0) r.Affinity = Math.Max(0, r.Affinity - _rules.AffinityDriftPerWeek);
                else if (r.Affinity < 0) r.Affinity = Math.Min(0, r.Affinity + _rules.AffinityDriftPerWeek);
                r.Kind = NpcMind.KindFor(r, _rules);
            }
        }

        // ----- IDs --------------------------------------------------------------------------------------

        private static Func<KnowledgeId> KnowledgeIds(WorldData world, NpcData npc, WorldDate today, string purpose = "observe")
        {
            var n = 0;
            return () => new KnowledgeId(NpcHash.Guid(NpcHash.Of(world.Seed, "knowledge-" + purpose, npc.NpcId + ":" + npc.Knowledge.Count + ":" + n++, today.ToDayNumber())));
        }

        /// <summary>
        /// Phase 14 (ADR-0069): a theft, a break-in or a smashed door nobody saw is found out the next day by the person
        /// it was done to — they know it happened, not who did it. It travels as news, and costs the player nothing.
        /// </summary>
        private static void DiscoverCrimes(SimulationDayContext context)
        {
            var world = context.World;
            var today = context.Date;
            foreach (var c in world.Crimes)
            {
                if (c.Status != CrimeStatus.Undiscovered) continue;
                if (c.Kind != CrimeKind.Theft && c.Kind != CrimeKind.ShopTheft && c.Kind != CrimeKind.UnauthorizedEntry && c.Kind != CrimeKind.PropertyDamage) continue;
                if (c.At / 1440 >= today.ToDayNumber()) continue; // found the next day
                var victim = world.FindNpc(c.VictimNpcId);
                c.Status = CrimeStatus.Discovered;
                c.DiscoveredAt = today.ToDayNumber() * 1440L;
                var deed = c.Kind == CrimeKind.Theft || c.Kind == CrimeKind.ShopTheft ? "they had been robbed" : "someone had broken in";
                if (victim != null && !victim.IsDeceased)
                {
                    NpcMind.Remember(victim, new NpcMemoryData
                    {
                        MemoryId = MemoryIdFor(world, victim, today, c.CrimeId), Date = today, Category = MemoryCategory.Hardship, Subject = "found that " + deed + " — nobody knows who",
                        SettlementId = c.SettlementId, Salience = 60, Source = MemorySource.Experienced,
                    });
                    NpcMind.Learn(victim, KnowledgeTopic.RecentEvent, c.CrimeId, deed + " (culprit unknown)", KnowledgeStatus.Known, 100, today, default, 0, default, KnowledgeIds(world, victim, today, "crime"));
                }

                context.ReportEvent(HistoricalEventType.CrimeDiscovered, EventSeverity.Warning, EventImportance.Minor, c.SettlementId.IsEmpty ? new SettlementId[0] : new[] { c.SettlementId },
                    new EventData().Set("source", SimulationSource).Set("victim", victim?.Name ?? "Someone").Set("deed", deed));
            }
        }

        private static MemoryId MemoryIdFor(WorldData world, NpcData npc, WorldDate today, string what) =>
            new MemoryId(NpcHash.Guid(NpcHash.Of(world.Seed, "memory", npc.NpcId + ":" + what, today.ToDayNumber())));
    }
}
