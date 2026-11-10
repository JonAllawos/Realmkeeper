using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Npcs
{
    /// <summary>
    /// Creates the Phase 4 state that a world, or a pre-Phase-4 save, does not have yet:
    /// settlement locations and each named person's identity, personality, skills, condition,
    /// home, workplace, weekly schedule, starting goals, household and workplace ties, and what
    /// they currently see of their own settlement.
    ///
    /// Everything is derived from the world seed and stable IDs, so it is deterministic and
    /// independent of order. It invents no history: no memories, no past events, and relationships
    /// only where the present facts imply them (sharing a home, a workplace, a faction).
    /// </summary>
    public static class NpcGenesis
    {
        /// <summary>Given names in the world generator's list that are conventionally feminine.</summary>
        private static readonly HashSet<string> FeminineNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Cwen", "Edda", "Fenna", "Hild", "Isolde", "Kenna", "Maud", "Petra", "Quenby", "Rowena", "Tilda", "Wenna",
            "Agnes", "Merry", "Ottilie", "Sabine",
        };

        public static NpcSex SexFromName(string name)
        {
            var given = name?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (given == null || given.Length == 0) return NpcSex.Unspecified;
            return FeminineNames.Contains(given[0]) ? NpcSex.Female : NpcSex.Male;
        }

        // ----- Locations -------------------------------------------------------------------------

        /// <summary>The places a settlement has, by type, each on a layout site (see <see cref="SettlementSites"/>).</summary>
        public static IReadOnlyList<(LocationKind kind, string name, string key)> PlanLocations(SettlementType type)
        {
            var list = new List<(LocationKind, string, string)>
            {
                (LocationKind.Square, "The square", SettlementSites.Plaza),
                (LocationKind.Market, "Market", SettlementSites.Plaza),
                (LocationKind.Tavern, "Tavern", SettlementSites.Hall),
                (LocationKind.Workshop, "Granary", SettlementSites.Granary),
            };
            var houses = SettlementSites.HouseCount(type);
            var next = 0;
            switch (type)
            {
                case SettlementType.Town:
                case SettlementType.City:
                case SettlementType.Fortress:
                    list.Add((LocationKind.Administration, "Keep", SettlementSites.Landmark));
                    list.Add((LocationKind.GuardPost, "Gatehouse", SettlementSites.Gate));
                    list.Add((LocationKind.Shop, "Shop", SettlementSites.House(next++)));
                    list.Add((LocationKind.Workshop, "Smithy", SettlementSites.House(next++)));
                    list.Add((LocationKind.Worship, "Shrine", SettlementSites.House(next++)));
                    break;
                case SettlementType.Village:
                    list.Add((LocationKind.Worship, "Chapel", SettlementSites.Landmark));
                    list.Add((LocationKind.Shop, "Shop", SettlementSites.House(next++)));
                    list.Add((LocationKind.Workshop, "Smithy", SettlementSites.House(next++)));
                    break;
                default:
                    list.Add((LocationKind.GuardPost, "Watchtower", SettlementSites.Landmark));
                    break;
            }

            for (var i = next; i < houses; i++) list.Add((LocationKind.Residence, "House " + (i + 1), SettlementSites.House(i)));
            return list;
        }

        /// <summary>Adds the planned locations of every settlement that has none. Returns how many were added.</summary>
        public static int EnsureLocations(WorldData world)
        {
            var added = 0;
            foreach (var s in world.Settlements)
            {
                var has = false;
                foreach (var l in world.Locations) if (l.SettlementId == s.SettlementId) { has = true; break; }
                if (has) continue;
                foreach (var (kind, name, key) in PlanLocations(s.SettlementType))
                {
                    world.Locations.Add(new LocationData
                    {
                        LocationId = new LocationId(NpcHash.Guid(NpcHash.Of(world.Seed, "location", s.SettlementId + ":" + kind + ":" + key))),
                        SettlementId = s.SettlementId,
                        Kind = kind,
                        Name = name,
                        LayoutKey = key,
                    });
                    added++;
                }
            }

            return added;
        }

        public static List<LocationData> LocationsOf(WorldData world, SettlementId settlement, LocationKind kind)
        {
            var list = new List<LocationData>();
            foreach (var l in world.Locations) if (l.SettlementId == settlement && l.Kind == kind) list.Add(l);
            return list;
        }

        public static LocationData FirstOf(WorldData world, SettlementId settlement, params LocationKind[] kinds)
        {
            foreach (var kind in kinds)
            {
                var found = LocationsOf(world, settlement, kind);
                if (found.Count > 0) return found[0];
            }

            return null;
        }

        /// <summary>Where a role works in a settlement (first available of its preferred places).</summary>
        public static LocationData WorkplaceFor(WorldData world, SettlementId settlement, NpcRole role)
        {
            switch (role)
            {
                case NpcRole.Innkeeper: return FirstOf(world, settlement, LocationKind.Tavern);
                case NpcRole.Merchant: return FirstOf(world, settlement, LocationKind.Shop, LocationKind.Market);
                case NpcRole.Smith: return SmithyOr(world, settlement);
                case NpcRole.Farmer:
                case NpcRole.Labourer: return FirstOf(world, settlement, LocationKind.Workshop);
                case NpcRole.Guard: return FirstOf(world, settlement, LocationKind.GuardPost, LocationKind.Square);
                case NpcRole.Elder: return FirstOf(world, settlement, LocationKind.Administration, LocationKind.Worship, LocationKind.Tavern);
                default: return FirstOf(world, settlement, LocationKind.Market, LocationKind.Square);
            }
        }

        private static LocationData SmithyOr(WorldData world, SettlementId settlement)
        {
            foreach (var l in LocationsOf(world, settlement, LocationKind.Workshop)) if (l.LayoutKey != SettlementSites.Granary) return l;
            return FirstOf(world, settlement, LocationKind.Workshop);
        }

        /// <summary>A weekly pattern: the workplace on six days, a rest day at the tavern, a place of worship or home.</summary>
        public static void PlanSchedule(WorldData world, NpcData npc, SettlementId settlement)
        {
            var rest = NpcHash.Roll(NpcHash.Of(world.Seed, "rest-day", npc.NpcId.ToString()), NpcScheduleData.Days);
            var work = npc.WorkplaceId.IsEmpty ? FirstOf(world, settlement, LocationKind.Market, LocationKind.Square)?.LocationId ?? npc.ResidenceId : npc.WorkplaceId;
            LocationId restPlace;
            if (npc.Personality[NpcTrait.Sociability] >= 55) restPlace = FirstOf(world, settlement, LocationKind.Tavern, LocationKind.Square)?.LocationId ?? npc.ResidenceId;
            else restPlace = FirstOf(world, settlement, LocationKind.Worship)?.LocationId ?? npc.ResidenceId;
            for (var d = 0; d < NpcScheduleData.Days; d++) npc.Schedule[d] = d == rest ? restPlace : work;
        }

        // ----- People ----------------------------------------------------------------------------

        /// <summary>
        /// Initializes every named person whose tier is still unassigned. Locations are created first
        /// when missing. Returns the number of people initialized. Running it again changes nothing.
        /// </summary>
        public static int EnsurePeople(WorldData world)
        {
            EnsureLocations(world);
            var today = world.CurrentSimulationDate;
            var pending = new List<NpcData>();
            foreach (var n in world.Npcs) if (n.Tier == NpcTier.Unassigned) pending.Add(n);
            if (pending.Count == 0) return 0;

            var leaders = new HashSet<NpcId>();
            foreach (var f in world.Factions) if (!f.LeaderNpcId.IsEmpty && !f.IsDissolved) leaders.Add(f.LeaderNpcId);

            // Homes are shared when a settlement has more named people than houses (households).
            var bySettlement = new Dictionary<SettlementId, List<NpcData>>();
            foreach (var n in world.Npcs)
            {
                if (n.HomeSettlementId.IsEmpty || n.IsDeceased) continue;
                if (!bySettlement.TryGetValue(n.HomeSettlementId, out var list)) bySettlement[n.HomeSettlementId] = list = new List<NpcData>();
                list.Add(n);
            }

            foreach (var npc in pending)
            {
                var seed = NpcHash.Of(world.Seed, "genesis", npc.NpcId.ToString());
                int Roll(int n, int min, int max) => NpcHash.Range(NpcHash.Next(seed, n), min, max);
                npc.Sex = SexFromName(npc.Name);
                npc.Tier = leaders.Contains(npc.NpcId) || npc.Role == NpcRole.Elder || npc.Role == NpcRole.Innkeeper ? NpcTier.Notable : NpcTier.Resident;
                var age = npc.Role == NpcRole.Elder ? Roll(1, 55, 74) : Roll(1, 19, 52);
                if (today.IsValidDate) npc.BirthDate = today.AddDays(-(age * WorldDate.DaysPerYear + Roll(2, 0, WorldDate.DaysPerYear - 1)));

                for (var t = 0; t < NpcPersonality.Count; t++) npc.Personality[(NpcTrait)t] = Roll(10 + t, 22, 78);
                Bias(npc);
                for (var k = 0; k < NpcSkills.Count; k++) npc.Skills[(NpcSkill)k] = Roll(20 + k, 5, 35);
                foreach (var (skill, min, max) in SkillsOf(npc.Role)) npc.Skills[skill] = Roll(30 + (int)skill, min, max);

                var settlement = world.FindSettlement(npc.HomeSettlementId);
                npc.Condition = new NpcCondition
                {
                    Health = Roll(40, 80, 100),
                    Satiation = settlement == null ? 70 : settlement.IsInFoodShortage ? 40 : Roll(41, 65, 90),
                    Energy = Roll(42, 60, 90),
                    Safety = settlement == null ? 60 : NpcBounds.Clamp(settlement.Stability, 20, 95),
                    Wealth = WealthFor(npc.Role, Roll(43, 0, 100)),
                };
                npc.Condition.Morale = (npc.Condition.Health + npc.Condition.Satiation + npc.Condition.Energy + npc.Condition.Safety) / 4;

                if (settlement != null)
                {
                    var residents = LocationsOf(world, settlement.SettlementId, LocationKind.Residence);
                    var people = bySettlement.TryGetValue(settlement.SettlementId, out var p) ? p : new List<NpcData>();
                    var index = Math.Max(0, people.IndexOf(npc));
                    if (residents.Count > 0) npc.ResidenceId = residents[index % residents.Count].LocationId;
                    npc.WorkplaceId = WorkplaceFor(world, settlement.SettlementId, npc.Role)?.LocationId ?? default;
                    PlanSchedule(world, npc, settlement.SettlementId);
                    npc.CurrentLocationId = npc.Schedule.For(today);
                }

                var counter = 0;
                GoalId NewGoal() => new GoalId(NpcHash.Guid(NpcHash.Of(world.Seed, "goal", npc.NpcId.ToString(), counter++)));
                foreach (var g in StartingGoals(npc, leaders.Contains(npc.NpcId), today, NewGoal)) npc.Goals.Add(g);

                var known = 0;
                KnowledgeId NewKnowledge() => new KnowledgeId(NpcHash.Guid(NpcHash.Of(world.Seed, "knowledge", npc.NpcId.ToString(), known++ + 1000L * today.ToDayNumber())));
                if (today.IsValidDate) NpcMind.Observe(world, npc, settlement, today, NewKnowledge);
                npc.LastUpdatedOn = today;
            }

            TieTogether(world, pending, today);
            return pending.Count;
        }

        private static void Bias(NpcData npc)
        {
            var p = npc.Personality;
            switch (npc.Role)
            {
                case NpcRole.Guard: p[NpcTrait.Courage] += 15; p[NpcTrait.Discipline] += 10; break;
                case NpcRole.Merchant: p[NpcTrait.Greed] += 12; p[NpcTrait.Ambition] += 10; p[NpcTrait.Sociability] += 5; break;
                case NpcRole.Elder: p[NpcTrait.Discipline] += 12; p[NpcTrait.Compassion] += 6; break;
                case NpcRole.Innkeeper: p[NpcTrait.Sociability] += 18; p[NpcTrait.Curiosity] += 6; break;
                case NpcRole.Smith: p[NpcTrait.Discipline] += 8; break;
                case NpcRole.Farmer: p[NpcTrait.Compassion] += 5; break;
            }
        }

        private static IEnumerable<(NpcSkill, int, int)> SkillsOf(NpcRole role)
        {
            switch (role)
            {
                case NpcRole.Farmer: yield return (NpcSkill.Farming, 55, 85); yield return (NpcSkill.Labour, 35, 55); break;
                case NpcRole.Smith: yield return (NpcSkill.Crafting, 60, 88); yield return (NpcSkill.Labour, 35, 55); break;
                case NpcRole.Merchant: yield return (NpcSkill.Trading, 60, 88); yield return (NpcSkill.Lore, 25, 50); break;
                case NpcRole.Guard: yield return (NpcSkill.Combat, 55, 85); yield return (NpcSkill.Labour, 30, 50); break;
                case NpcRole.Elder: yield return (NpcSkill.Leadership, 55, 85); yield return (NpcSkill.Lore, 45, 75); break;
                case NpcRole.Innkeeper: yield return (NpcSkill.Hospitality, 60, 88); yield return (NpcSkill.Trading, 30, 55); break;
                default: yield return (NpcSkill.Labour, 45, 75); yield return (NpcSkill.Farming, 25, 50); break;
            }
        }

        public static NpcSkill PrimarySkill(NpcRole role)
        {
            foreach (var (skill, _, _) in SkillsOf(role)) return skill;
            return NpcSkill.Labour;
        }

        private static int WealthFor(NpcRole role, int roll)
        {
            switch (role)
            {
                case NpcRole.Merchant: return 60 + roll * 60 / 100;
                case NpcRole.Elder: return 50 + roll * 40 / 100;
                case NpcRole.Innkeeper: return 40 + roll * 40 / 100;
                case NpcRole.Smith: return 30 + roll * 30 / 100;
                case NpcRole.Guard: return 20 + roll * 20 / 100;
                default: return 5 + roll * 25 / 100;
            }
        }

        private static IEnumerable<NpcGoalData> StartingGoals(NpcData npc, bool leader, WorldDate today, Func<GoalId> newId)
        {
            NpcGoalData Goal(NpcGoalType type, int priority, NpcSkill? skill = null) => new NpcGoalData
            {
                GoalId = newId(), Type = type, Priority = priority, Status = NpcGoalStatus.Active, CreatedOn = today, Source = ChangeSource.Simulation, TargetSkill = skill,
            };

            var count = 0;
            if (leader) { yield return Goal(NpcGoalType.ServeFaction, 70); count++; }
            yield return Goal(NpcGoalType.EarnLiving, 50);
            count++;
            if (npc.Personality[NpcTrait.Ambition] >= 65 && count < NpcBounds.MaxActiveGoals) { yield return Goal(NpcGoalType.GainStanding, 45); count++; }
            if (npc.Personality[NpcTrait.Sociability] >= 65 && count < NpcBounds.MaxActiveGoals) { yield return Goal(NpcGoalType.Socialise, 35); count++; }
            if (npc.Personality[NpcTrait.Curiosity] >= 65 && count < NpcBounds.MaxActiveGoals) yield return Goal(NpcGoalType.LearnSkill, 30, Weakest(npc));
        }

        public static NpcSkill Weakest(NpcData npc)
        {
            var best = NpcSkill.Farming;
            for (var k = 1; k < NpcSkills.Count; k++) if (npc.Skills[(NpcSkill)k] < npc.Skills[best]) best = (NpcSkill)k;
            return best;
        }

        /// <summary>Ties implied by present facts: a shared home, a shared workplace, a faction's leader.</summary>
        private static void TieTogether(WorldData world, List<NpcData> people, WorldDate today)
        {
            var ordered = new List<NpcData>(world.Npcs);
            ordered.Sort((a, b) => a.NpcId.CompareTo(b.NpcId));
            var involved = new HashSet<NpcId>();
            foreach (var n in people) involved.Add(n.NpcId);
            for (var i = 0; i < ordered.Count; i++)
            {
                var a = ordered[i];
                if (a.IsDeceased) continue;
                for (var j = i + 1; j < ordered.Count; j++)
                {
                    var b = ordered[j];
                    if (b.IsDeceased || !involved.Contains(a.NpcId) && !involved.Contains(b.NpcId)) continue;
                    if (!a.ResidenceId.IsEmpty && a.ResidenceId == b.ResidenceId)
                        NpcMind.Relate(world, a.NpcId, b.NpcId, NpcRelationCause.SharedHousehold, NpcRelationKind.Household, today);
                    else if (!a.WorkplaceId.IsEmpty && a.WorkplaceId == b.WorkplaceId)
                        NpcMind.Relate(world, a.NpcId, b.NpcId, NpcRelationCause.SharedWorkplace, NpcRelationKind.Colleague, today);
                }
            }

            foreach (var f in world.Factions)
            {
                if (f.IsDissolved || f.LeaderNpcId.IsEmpty) continue;
                var leader = world.FindNpc(f.LeaderNpcId);
                if (leader == null || leader.IsDeceased) continue;
                foreach (var n in ordered)
                {
                    if (n.NpcId == leader.NpcId || n.IsDeceased || n.FactionId != f.FactionId || n.Tier != NpcTier.Notable) continue;
                    if (!involved.Contains(n.NpcId) && !involved.Contains(leader.NpcId)) continue;
                    NpcMind.Relate(world, leader.NpcId, n.NpcId, NpcRelationCause.FactionHierarchy, NpcRelationKind.Superior, today);
                }
            }
        }
    }
}
