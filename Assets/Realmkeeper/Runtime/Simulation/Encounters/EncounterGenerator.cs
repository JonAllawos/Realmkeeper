using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Simulation.Encounters
{
    /// <summary>Where the player goes looking for trouble (Phase 10).</summary>
    public enum EncounterPurpose
    {
        /// <summary>The settlement's outskirts: its woods, fields and hills.</summary>
        Outskirts = 0,

        /// <summary>The road out of the settlement.</summary>
        Road = 1,

        /// <summary>The farms around the settlement (predators come for the herds).</summary>
        Farms = 2,
    }

    public sealed class EncounterRequest
    {
        public SettlementId Near { get; set; }
        public EncounterPurpose Purpose { get; set; }

        /// <summary>Distinguishes repeated outings at the same hour (the count of generated encounters so far).</summary>
        public int Attempt { get; set; }
    }

    /// <summary>A generated encounter before it starts: the setup the engine builds, its origin, and the individuals it draws.</summary>
    public sealed class EncounterPlan
    {
        public EncounterSetup Setup { get; set; }
        public EncounterOriginData Origin { get; set; }

        /// <summary>For each foe participant (by index in the setup): the population it is drawn from.</summary>
        public Dictionary<int, string> Draws { get; } = new Dictionary<int, string>();

        public string Description { get; set; }
        public List<string> Problems { get; } = new List<string>();

        public bool IsValid => Problems.Count == 0 && Setup != null;
    }

    /// <summary>
    /// Phase 10 (ADR-0046): encounters arise from the world. What is near (the populations around the settlement), the
    /// ground (the region map), the hour, the road and its bandits, the settlement's guard, and the player's level
    /// decide who turns up, how many, where they stand and what the fight is for. Deterministic: the same world state
    /// and request give the same plan. The plan is checked before any fight starts.
    /// </summary>
    public static class EncounterGenerator
    {
        public const int FieldRadius = 10;

        public static EncounterPlan Plan(WorldData world, RegionMap map, EncounterRequest request)
        {
            var plan = new EncounterPlan();
            var s = world.FindSettlement(request.Near);
            if (s == null) { plan.Problems.Add("No such settlement."); return plan; }
            var player = world.Player;
            var level = Math.Max(1, player?.Rpg?.Level ?? 1);
            var seed = SeedMixer.Combine(SeedMixer.Combine(SeedMixer.FromWorldSeed(world.Seed), "world-encounter:" + s.SettlementId),
                (ulong)(world.CurrentSimulationDate.ToDayNumber() * 1440L + world.MinuteOfDay) * 31UL + (ulong)request.Attempt * 7UL + (ulong)request.Purpose);
            var rng = new Rng(seed);

            // The ground around the settlement.
            var terrain = DominantTerrain(map, s.Position, request.Purpose);
            var night = world.MinuteOfDay < 6 * 60 || world.MinuteOfDay >= 20 * 60;
            var dusk = !night && (world.MinuteOfDay < 8 * 60 || world.MinuteOfDay >= 18 * 60);
            var guards = new List<NpcData>();
            foreach (var n in world.Npcs) if (!n.IsDeceased && n.HomeSettlementId == s.SettlementId && n.Role == NpcRole.Guard) guards.Add(n);

            // Who could be out there, and how likely.
            var candidates = new List<(CreaturePopulationData pop, CreatureDefinition def, double weight)>();
            foreach (var p in world.CreaturePopulations)
            {
                if (p.Count <= 0 || p.RegionId != s.RegionId) continue;
                // How far the settlement is from the edge of the population's range (a road band works its whole road).
                var distance = Math.Max(0, Math.Max(Math.Abs(p.Center.X - s.Position.X), Math.Abs(p.Center.Z - s.Position.Z)) - p.RangeCells);
                if (distance > 16) continue;
                var def = CreatureCatalog.Find(p.CreatureId);
                if (def == null) continue;
                var w = 1 + Math.Sqrt(p.Count);
                if (Lives(def, terrain)) w *= 2.5;
                w *= TimeFactor(def, night, dusk);
                w *= PurposeFactor(def, request.Purpose, p);
                if (p.CreatureId == "bandit" || p.CreatureId == "highwayman") w /= 1 + guards.Count / 2.0; // a watched road is quieter
                if (ChallengeRatings.Value(def.ChallengeRating) > level + 1) w *= 0.15; // the truly dangerous are rare near people
                if (p.CreatureId == "highwayman") w *= 0.2; // the leader comes with the band, not alone
                w /= 1 + distance / 12.0;
                if (w > 0) candidates.Add((p, def, w));
            }

            if (candidates.Count == 0) { plan.Problems.Add("Nothing stirs out there: no creature population is within reach."); return plan; }
            candidates.Sort((a, b) => string.CompareOrdinal(a.pop.PopulationId, b.pop.PopulationId));
            var total = 0.0;
            foreach (var c in candidates) total += c.weight;
            var pick = rng.Next01() * total;
            var chosen = candidates[candidates.Count - 1];
            foreach (var c in candidates)
            {
                pick -= c.weight;
                if (pick <= 0) { chosen = c; break; }
            }

            // The party's budget: SRD 5.2.1's per-level XP (moderate 75, high 100 per level at the low levels the game reaches).
            var budget = 75 * level;
            var high = 100 * level;
            var origin = new EncounterOriginData { NearSettlementId = s.SettlementId, Terrain = terrain.ToString(), XpBudget = budget, PlanSeed = seed };
            var setup = new EncounterSetup { Kind = EncounterKind.Other, Lethality = Lethality.Lethal, SettlementId = s.SettlementId, Origin = origin };
            plan.Origin = origin;
            setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(0, 0), Control = CombatControl.Player });

            // How many: the social form, the population left, and the budget.
            var def0 = chosen.def;
            var (min, max) = GroupSize(def0.Social);
            var count = Math.Min(chosen.pop.Count, min + rng.Int(max - min + 1));
            var xp = 0;
            var picked = 0;
            for (var i = 0; i < count; i++)
            {
                if (picked > 0 && xp + def0.Xp > budget) break; // a group within the moderate budget
                xp += def0.Xp;
                picked++;
            }

            var leader = default(CreaturePopulationData);
            if (def0.Id == "bandit")
                foreach (var p in world.CreaturePopulations)
                    if (p.CreatureId == "highwayman" && p.Count > 0 && p.Center.Equals(chosen.pop.Center) && xp + CreatureCatalog.Find("highwayman").Xp <= high) leader = p; // a leader only while it stays within the high budget

            // What kind of fight, and what for.
            var behaviour = def0.Behavior.Kind;
            origin.Type = def0.Category == CreatureCategory.Livestock || behaviour == CreatureBehavior.Skittish || behaviour == CreatureBehavior.Passive ? EncounterType.Wildlife
                : def0.Id == "bandit" || def0.Id == "highwayman" ? EncounterType.BanditAmbush
                : def0.Id == "giant_rat" ? EncounterType.Vermin
                : def0.Category == CreatureCategory.Wildlife || def0.Category == CreatureCategory.Dangerous && def0.Type == "Beast" ? EncounterType.Predators
                : EncounterType.HostileCreatures;
            origin.Objective = origin.Type == EncounterType.Wildlife ? EncounterObjective.Hunt : EncounterObjective.Defeat;
            NpcData farmer = null;
            if (request.Purpose == EncounterPurpose.Farms && origin.Type == EncounterType.Predators)
                foreach (var n in world.Npcs)
                    if (!n.IsDeceased && n.HomeSettlementId == s.SettlementId && n.Role == NpcRole.Farmer && (farmer == null || n.NpcId.CompareTo(farmer.NpcId) < 0)) farmer = n;
            if (farmer != null)
            {
                origin.Type = EncounterType.DefendFarm;
                origin.Objective = EncounterObjective.Protect;
                origin.ProtectedNpcId = farmer.NpcId;
                setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Npc, NpcId = farmer.NpcId, Side = "player", Position = new GridPos(1, 1) });
            }

            if (origin.Type == EncounterType.BanditAmbush && guards.Count > 0 && !night && request.Purpose == EncounterPurpose.Road)
            {
                // The road watch is out: the first guard on duty comes along.
                guards.Sort((a, b) => a.NpcId.CompareTo(b.NpcId));
                setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Npc, NpcId = guards[0].NpcId, Side = "player", Position = new GridPos(-1, 1) });
                origin.Type = EncounterType.Patrol;
            }

            var totalXp = xp + (leader != null ? CreatureCatalog.Find("highwayman").Xp : 0);
            if (totalXp > budget * 2 && origin.Objective == EncounterObjective.Defeat) origin.Objective = EncounterObjective.Escape;
            origin.XpTotal = totalXp;
            origin.Difficulty = totalXp <= budget * 2 / 3 ? "easy" : totalXp <= budget ? "moderate" : totalXp <= high ? "hard" : "deadly";

            // Where everyone stands: an ambush closes around; predators and hunters come from one side; game is found grazing.
            var taken = new HashSet<GridPos>();
            foreach (var p in setup.Participants) taken.Add(p.Position);
            var radius = origin.Type == EncounterType.BanditAmbush || origin.Type == EncounterType.Patrol ? 4 : origin.Type == EncounterType.Wildlife ? 5 : 6;
            var arc = origin.Type == EncounterType.BanditAmbush || origin.Type == EncounterType.Patrol ? Math.PI * 2 : Math.PI * 0.8;
            var facing = rng.Next01() * Math.PI * 2;
            var foes = new List<(CreaturePopulationData pop, CreatureDefinition def)>();
            for (var i = 0; i < picked; i++) foes.Add((chosen.pop, def0));
            if (leader != null) foes.Add((leader, CreatureCatalog.Find("highwayman")));
            for (var i = 0; i < foes.Count; i++)
            {
                var angle = facing + (foes.Count == 1 ? 0 : arc * i / Math.Max(1, foes.Count - (arc >= Math.PI * 2 ? 0 : 1))) - (arc >= Math.PI * 2 ? 0 : arc / 2);
                var at = new GridPos((int)Math.Round(Math.Cos(angle) * radius), (int)Math.Round(Math.Sin(angle) * radius));
                for (var guard = 0; guard < 12 && taken.Contains(at); guard++) at = new GridPos(at.X + 1, at.Y);
                taken.Add(at);
                plan.Draws[setup.Participants.Count] = foes[i].pop.PopulationId;
                setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = foes[i].def.Id, Side = Side(foes[i].def), Position = at });
            }

            if (!origin.SourceIds.Contains(chosen.pop.PopulationId)) origin.SourceIds.Add(chosen.pop.PopulationId);
            if (leader != null) origin.SourceIds.Add(leader.PopulationId);

            // Rough ground: woods and hills leave some squares difficult.
            if (terrain == TerrainType.Forest || terrain == TerrainType.Hills)
                for (var i = 0; i < 10; i++)
                {
                    var g = new GridPos(rng.Int(2 * FieldRadius + 1) - FieldRadius, rng.Int(2 * FieldRadius + 1) - FieldRadius);
                    if (!taken.Contains(g) && !setup.DifficultTerrain.Contains(g)) setup.DifficultTerrain.Add(g);
                }

            var when = night ? "at night" : dusk ? "at dusk" : "by day";
            var where = request.Purpose == EncounterPurpose.Road ? "on the road out of " + s.Name : request.Purpose == EncounterPurpose.Farms ? "among the farms of " + s.Name : "on the " + Describe(terrain) + " edge of " + s.Name;
            origin.Context = where + ", " + when;
            setup.Name = Title(origin, def0, foes.Count) + " — " + origin.Context;
            plan.Description = $"{setup.Name}: {foes.Count} {(foes.Count == 1 ? def0.Name : Plural(def0.Name))}{(leader != null ? " led by a highwayman" : string.Empty)} ({origin.Difficulty}, {totalXp} XP against a budget of {budget}); objective: {Objective(origin.Objective)}.";
            plan.Setup = setup;
            Check(world, plan);
            return plan;
        }

        /// <summary>Refuses impossible plans: unknown creatures, more drawn than live, people who cannot come, two on a square, outside the field, nobody hostile.</summary>
        public static void Check(WorldData world, EncounterPlan plan)
        {
            var setup = plan.Setup;
            if (setup == null) { plan.Problems.Add("No setup."); return; }
            var squares = new HashSet<GridPos>();
            var drawn = new Dictionary<string, int>(StringComparer.Ordinal);
            var sides = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < setup.Participants.Count; i++)
            {
                var p = setup.Participants[i];
                sides.Add(p.Side);
                if (!squares.Add(p.Position)) plan.Problems.Add($"Two participants stand on {p.Position}.");
                if (Math.Abs(p.Position.X) > FieldRadius || Math.Abs(p.Position.Y) > FieldRadius) plan.Problems.Add($"{p.Position} is outside the field.");
                if (p.Kind == CombatantKind.Creature && CreatureCatalog.Find(p.CreatureId) == null) plan.Problems.Add($"Unknown creature '{p.CreatureId}'.");
                if (p.Kind == CombatantKind.Npc && (world.FindNpc(p.NpcId) == null || world.FindNpc(p.NpcId).IsDeceased)) plan.Problems.Add("A person in the plan cannot come.");
            }

            foreach (var pop in plan.Draws.Values) drawn[pop] = (drawn.TryGetValue(pop, out var n) ? n : 0) + 1;
            foreach (var index in plan.Draws.Keys)
                if (index < 0 || index >= setup.Participants.Count || setup.Participants[index].Kind != CombatantKind.Creature) plan.Problems.Add($"Draw {index} names no creature in the plan.");

            foreach (var pair in drawn)
            {
                CreaturePopulationData found = null;
                foreach (var p in world.CreaturePopulations) if (p.PopulationId == pair.Key) found = p;
                if (found == null) plan.Problems.Add($"No population '{pair.Key}'.");
                else if (pair.Value > found.Count) plan.Problems.Add($"{pair.Value} drawn from {pair.Key}, which has {found.Count}.");
            }

            if (sides.Count < 2) plan.Problems.Add("Nobody is hostile.");
            if (plan.Origin != null && plan.Origin.Objective == EncounterObjective.Protect && plan.Origin.ProtectedNpcId.IsEmpty) plan.Problems.Add("Protect whom?");
        }

        private static string Side(CreatureDefinition d) => d.Category == CreatureCategory.Wildlife || d.Category == CreatureCategory.Livestock ? "wild" : d.Id == "bandit" || d.Id == "highwayman" ? "bandits" : d.Category == CreatureCategory.Undead ? "undead" : d.Id;

        private static string Title(EncounterOriginData o, CreatureDefinition d, int n)
        {
            switch (o.Type)
            {
                case EncounterType.Wildlife: return (n == 1 ? "A " + d.Name.ToLowerInvariant() : Plural(d.Name)) + " in the open";
                case EncounterType.Predators: return d.Social == SocialForm.Pack ? "A hunting pack" : "A " + d.Name.ToLowerInvariant() + " on the prowl";
                case EncounterType.DefendFarm: return Plural(d.Name) + " at the farm";
                case EncounterType.BanditAmbush: return "Ambush";
                case EncounterType.Patrol: return "The road watch meets bandits";
                case EncounterType.Vermin: return "Giant rats in the middens";
                default: return Plural(d.Name);
            }
        }

        private static string Objective(EncounterObjective o) =>
            o == EncounterObjective.Hunt ? "bring down game before it flees" : o == EncounterObjective.Protect ? "keep the farmer standing" : o == EncounterObjective.Escape ? "get away alive" : "defeat or drive them off";

        private static string Plural(string name) => name.EndsWith("f") ? name.Substring(0, name.Length - 1) + "ves" : name.EndsWith("s") || name == "Cattle" || name == "Sheep" || name == "Deer" ? name : name + "s";

        private static string Describe(TerrainType t) => t == TerrainType.Forest ? "wooded" : t == TerrainType.Hills ? "hilly" : t == TerrainType.Mountain ? "mountain" : t == TerrainType.Shore || t == TerrainType.River ? "riverside" : "open";

        private static (int min, int max) GroupSize(SocialForm f)
        {
            switch (f)
            {
                case SocialForm.Solitary: return (1, 1);
                case SocialForm.Pair: return (1, 2);
                case SocialForm.Herd: return (2, 4);
                case SocialForm.Pack: return (2, 4);
                case SocialForm.Band: return (2, 5);
                case SocialForm.Flock: return (3, 5);
                default: return (1, 2);
            }
        }

        private static bool Lives(CreatureDefinition d, TerrainType t)
        {
            foreach (var h in d.Habitats)
            {
                if (t == TerrainType.Forest && h == Habitat.Forest) return true;
                if (t == TerrainType.Plains && (h == Habitat.Grassland || h == Habitat.Farmland || h == Habitat.Road)) return true;
                if (t == TerrainType.Hills && h == Habitat.Hills) return true;
                if (t == TerrainType.Mountain && h == Habitat.Mountains) return true;
                if ((t == TerrainType.Shore || t == TerrainType.River) && (h == Habitat.Riverbank || h == Habitat.Wetland)) return true;
            }

            return false;
        }

        private static double TimeFactor(CreatureDefinition d, bool night, bool dusk)
        {
            var nocturnal = d.Id == "wolf" || d.Id == "dire_wolf" || d.Id == "giant_spider" || d.Id == "troll" || d.Id == "goblin" || d.Id == "giant_rat" || d.Category == CreatureCategory.Undead;
            if (nocturnal) return night ? 2.5 : dusk ? 1.5 : 0.5;
            if (d.Id == "bandit" || d.Id == "highwayman") return night ? 0.6 : 1.5;
            if (d.Behavior.Kind == CreatureBehavior.Skittish) return dusk ? 2 : night ? 0.7 : 1.4;
            return 1;
        }

        private static double PurposeFactor(CreatureDefinition d, EncounterPurpose purpose, CreaturePopulationData p)
        {
            var outlaw = d.Id == "bandit" || d.Id == "highwayman";
            switch (purpose)
            {
                case EncounterPurpose.Road: return outlaw ? 6 : p.Habitat == "Settlement" ? 0.1 : 0.25;
                case EncounterPurpose.Farms: return d.Diet == Diet.Carnivore || d.Id == "fox" || d.Id == "boar" ? 2.5 : outlaw ? 0.3 : 1;
                default: return outlaw ? 0.6 : p.Habitat == "Settlement" ? 0.6 : 1;
            }
        }

        /// <summary>The ground the fight is on: the commonest open terrain a few cells out from the settlement (roads for the road).</summary>
        private static TerrainType DominantTerrain(RegionMap map, MapCoord at, EncounterPurpose purpose)
        {
            if (purpose == EncounterPurpose.Farms) return TerrainType.Plains;
            var counts = new Dictionary<TerrainType, int>();
            for (var dx = -8; dx <= 8; dx++)
                for (var dz = -8; dz <= 8; dz++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dz)) < 3 || !map.InBounds(at.X + dx, at.Z + dz)) continue;
                    var t = map.GetTerrain(at.X + dx, at.Z + dz);
                    if (t == TerrainType.DeepWater || t == TerrainType.ShallowWater) continue;
                    counts[t] = (counts.TryGetValue(t, out var n) ? n : 0) + 1;
                }

            var best = TerrainType.Plains;
            var most = -1;
            foreach (var t in new[] { TerrainType.Forest, TerrainType.Plains, TerrainType.Hills, TerrainType.Mountain, TerrainType.Shore, TerrainType.River })
                if (counts.TryGetValue(t, out var n) && n > most) { best = t; most = n; }
            return best;
        }

        /// <summary>A small deterministic stream (the plan's own; never touches simulation streams).</summary>
        private sealed class Rng
        {
            private ulong _state;

            public Rng(ulong seed) { _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed; }

            private ulong Next()
            {
                _state = SeedMixer.Mix(_state + 0x9E3779B97F4A7C15UL);
                return _state;
            }

            public double Next01() => (Next() >> 11) * (1.0 / 9007199254740992.0);

            public int Int(int n) => n <= 1 ? 0 : (int)(Next() % (ulong)n);
        }
    }
}
