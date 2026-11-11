using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Rpg;

namespace Realmkeeper.Domain.World
{
    public static partial class WorldValidator
    {
        /// <summary>
        /// A plan's check, remembered for the plan instance while its content stamp is unchanged (plans are immutable in
        /// play, and the scheduler validates the world twice a day: re-checking unchanged plans would dominate the day).
        /// </summary>
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Settlements.SettlementLayoutData, PlanCheck> PlanChecks =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Settlements.SettlementLayoutData, PlanCheck>();

        private sealed class PlanCheck
        {
            public long Stamp;
            public SettlementType Type;
            public List<string> Errors;
        }

        private static List<string> CheckPlan(Settlements.SettlementLayoutData plan, SettlementType type)
        {
            var stamp = Stamp(plan);
            lock (PlanChecks)
            {
                if (PlanChecks.TryGetValue(plan, out var cached) && cached.Stamp == stamp && cached.Type == type) return cached.Errors;
            }

            var errors = Settlements.SettlementLayoutValidator.Check(plan, type);
            lock (PlanChecks)
            {
                PlanChecks.Remove(plan);
                PlanChecks.Add(plan, new PlanCheck { Stamp = stamp, Type = type, Errors = errors });
            }

            return errors;
        }

        /// <summary>A content stamp over everything the plan check reads (linear in the plan's size).</summary>
        private static long Stamp(Settlements.SettlementLayoutData p)
        {
            unchecked
            {
                long h = 1469598103934665603L;
                void Mix(double v) => h = (h ^ System.BitConverter.DoubleToInt64Bits(v)) * 1099511628211L;
                void Text(string s) => h = (h ^ (s?.GetHashCode() ?? 7)) * 1099511628211L;
                void Point(Settlements.PlanPoint q) { Mix(q.X); Mix(q.Z); }
                Mix(p.GeneratorVersion); Mix(p.HalfExtent); Mix(p.PlazaRadius); Mix(p.CoreRadius); Mix(p.HasWalls ? 1 : 0);
                var t = p.Terrain;
                if (t != null)
                {
                    Mix(t.NoiseSeed); Mix(t.NoiseAmplitude); Mix(t.SlopePer100); Mix(t.SlopeDirection); Mix(t.LevelRadius);
                    foreach (var hill in t.Hills) { Point(hill.Center); Mix(hill.Radius); Mix(hill.Height); Mix(hill.Plateau); }
                }

                foreach (var w in p.Waterways) { Text(w.Key); Mix(w.Width); Mix(w.Depth); foreach (var q in w.Line) Point(q); }
                foreach (var d in p.Districts) { Text(d.Key); Point(d.Center); Mix(d.Radius); }
                foreach (var r in p.Roads) { Text(r.Key); Mix(r.Width); Mix(r.Line.Count); foreach (var q in r.Line) Point(q); }
                foreach (var b in p.Bridges) { Text(b.Key); Text(b.RoadKey); Point(b.From); Point(b.To); Mix(b.Width); }
                foreach (var b in p.Buildings) { Text(b.Key); Mix((int)b.Function); Text(b.DistrictKey); Point(b.Center); Mix(b.Yaw); Mix(b.Width); Mix(b.Depth); Mix(b.Height); Mix(b.Foundation); }
                foreach (var f in p.Fields) { Text(f.Key); Mix((int)f.Kind); Point(f.Center); Mix(f.Width); Mix(f.Depth); Mix(f.Yaw); }
                foreach (var l in p.Landmarks) { Text(l.Key); Text(l.FeatureKey); }
                return h;
            }
        }

        /// <summary>
        /// Phase 10: the living-world data — named people's archetypes, gear and sheets (each sheet by the same rules
        /// as the player's).
        /// </summary>
        private static void ValidateLiving(WorldData world, HashSet<SettlementId> settlementIds, HashSet<NpcId> npcIds, ValidationResult result)
        {
            foreach (var n in world.Npcs)
            {
                var c = n?.Character;
                if (c == null) continue;
                var who = $"{n.Name}'s character record";
                if (NpcArchetypeCatalog.Find(c.ArchetypeId) == null) result.AddError($"{who} names an unknown archetype '{c.ArchetypeId}'.");
                if (c.Purse < 0) result.AddError($"{who} has a negative purse.");
                if (c.Gear == null) { result.AddError($"{who} has no gear record."); continue; }
                foreach (var item in c.Gear.All()) if (ItemCatalog.Find(item) == null) result.AddError($"{who} carries an unknown item '{item}'.");
                if (c.Gear.Shield != null && ItemCatalog.Find(c.Gear.Shield)?.Armor?.Category != ArmorCategory.Shield) result.AddError($"{who} holds '{c.Gear.Shield}' as a shield.");
                if (c.Gear.Body != null && (ItemCatalog.Find(c.Gear.Body)?.Armor == null || ItemCatalog.Find(c.Gear.Body).Armor.Category == ArmorCategory.Shield))
                    result.AddError($"{who} wears '{c.Gear.Body}' as armour.");
                if (c.Sheet == null) continue;
                if (c.Sheet.State != CreationState.Complete) result.AddError($"{who} holds a sheet that is not complete.");
                foreach (var e in CharacterValidation.CheckSheet(c.Sheet)) result.AddError($"{who}: {e}");
                if (n.IsDeceased && !c.Sheet.IsDead && c.Sheet.CurrentHp > 0) result.AddError($"{n.Name} is dead, but their sheet has hit points.");
            }

            // Creature populations, creatures and herds.
            var populations = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var p in world.CreaturePopulations)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.PopulationId) || !populations.Add(p.PopulationId)) { result.AddError("A creature population has a missing or duplicate ID."); continue; }
                if (Combat.CreatureCatalog.Find(p.CreatureId) == null) result.AddError($"Population {p.PopulationId} is of an unknown creature.");
                if (p.Count < 0 || p.Capacity < 0 || p.Count > FaunaBounds.MaxPopulation) result.AddError($"Population {p.PopulationId} has an impossible count.");
                if (p.Deaths < 0 || p.Drawn < 0) result.AddError($"Population {p.PopulationId} has negative records.");
            }

            var herds = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var h in world.Livestock)
            {
                if (h == null || string.IsNullOrWhiteSpace(h.HerdId) || !herds.Add(h.HerdId)) { result.AddError("A herd has a missing or duplicate ID."); continue; }
                if (!settlementIds.Contains(h.SettlementId)) result.AddError($"Herd {h.HerdId} belongs to no settlement.");
                if (Combat.CreatureCatalog.Find(h.CreatureId) == null) result.AddError($"Herd {h.HerdId} is of an unknown creature.");
                if (h.Count < 0 || h.Count > FaunaBounds.MaxHerd) result.AddError($"Herd {h.HerdId} has an impossible count.");
                if (!h.KeeperNpcId.IsEmpty && !npcIds.Contains(h.KeeperNpcId)) result.AddError($"Herd {h.HerdId} has an unknown keeper.");
                if (!string.IsNullOrEmpty(h.PastureKey))
                {
                    var plan = world.FindLayout(h.SettlementId);
                    var found = false;
                    if (plan != null) foreach (var f in plan.Fields) if (f.Key == h.PastureKey) { found = true; break; }
                    if (!found) result.AddError($"Herd {h.HerdId} grazes a pasture '{h.PastureKey}' its settlement does not have.");
                }
            }

            // Settlement plans: one per settlement, each sound (ADR-0047).
            var planned = new HashSet<SettlementId>();
            foreach (var plan in world.SettlementLayouts)
            {
                if (plan == null) { result.AddError("A settlement layout is empty."); continue; }
                var settlement = world.FindSettlement(plan.SettlementId);
                if (settlement == null) { result.AddError("A settlement layout belongs to no settlement."); continue; }
                if (!planned.Add(plan.SettlementId)) { result.AddError($"{settlement.Name} has two layouts."); continue; }
                foreach (var e in CheckPlan(plan, settlement.SettlementType)) result.AddError($"{settlement.Name}'s layout: {e}");
            }

            var creatures = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var c in world.Creatures)
            {
                if (c == null || string.IsNullOrWhiteSpace(c.InstanceId) || !creatures.Add(c.InstanceId)) { result.AddError("A creature has a missing or duplicate ID."); continue; }
                if (Combat.CreatureCatalog.Find(c.CreatureId) == null) result.AddError($"Creature {c.InstanceId} is of an unknown kind.");
                if (c.State == CreatureInstanceState.Dead && c.CurrentHp != 0) result.AddError($"Creature {c.InstanceId} is dead with hit points.");
                if (c.CurrentHp < 0) result.AddError($"Creature {c.InstanceId} has negative hit points.");
                if (!string.IsNullOrEmpty(c.OriginId) && !populations.Contains(c.OriginId) && !herds.Contains(c.OriginId)) result.AddError($"Creature {c.InstanceId} comes from no population or herd.");
            }
        }
    }
}
