using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Commerce;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Simulation.Quests
{
    /// <summary>
    /// Emergent activities (ADR-0060): quests that exist because something in the world is wrong — a settlement short of
    /// food, predators in reach of a settlement with herds, a merchant out of a material they deal in. Each cause has a
    /// deterministic key; one open instance per cause at most; an offer lapses when its cause ends. The authored quests
    /// of the vertical slice are made once, bound to the first town's real people.
    /// </summary>
    public static class QuestGenesis
    {
        /// <summary>Predators a settlement's herds fear, and how many must be in reach before anyone asks for help.</summary>
        public static readonly IReadOnlyList<string> Predators = new[] { "wolf", "dire_wolf" };
        public const int PredatorThreshold = 3;

        private static readonly IReadOnlyList<string> ShopMaterials = new[] { "timber_plank", "iron_bar", "cured_leather", "linen_cloth" };

        /// <summary>The authored quests (once per world) and today's emergent ones. True when anything was added.</summary>
        public static bool Ensure(WorldData w, QuestReporter report = null)
        {
            if (w == null) throw new ArgumentNullException(nameof(w));
            report = report ?? Direct(w);
            var changed = false;
            var fresh = w.Quests.Count == 0;
            var town = FirstTown(w);
            if (town != null)
            {
                if (!HasAuthored(w, QuestCatalog.GranaryShortfall))
                {
                    var food = town.ResourceInventory.Get(Domain.Economy.ResourceTypes.Food);
                    var missing = Math.Max(20, Math.Min(300, food * 8 / 100));
                    var given = Giver(w, town.SettlementId, "Elder", "Guard", "Merchant");
                    if (given != null && Make(w, QuestCatalog.GranaryShortfall, given, "authored", new Dictionary<string, string> { ["missing"] = I(missing) }, report) != null) changed = true;
                }

                if (!HasAuthored(w, QuestCatalog.SealedLetter))
                {
                    var given = Giver(w, town.SettlementId, "Merchant", "Innkeeper", "Smith", "Elder");
                    if (given != null && Make(w, QuestCatalog.SealedLetter, given, "authored", null, report) != null) changed = true;
                }
            }

            // Phase 21: emergent quests come when the daily system would offer them (a new world or quest log; the week's
            // turn; a settlement short of food), so loading a save mid-week offers nothing a continuous game would not.
            var shortage = false;
            foreach (var s in w.Settlements) if (s.IsInFoodShortage) shortage = true;
            var day = w.CurrentSimulationDate.IsValidDate ? w.CurrentSimulationDate.ToDayNumber() : 0;
            if (fresh || shortage || day % Domain.Calendar.WorldDate.DaysPerWeek == 0)
                if (Emerge(w, report)) changed = true;
            return changed;
        }

        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// A reporter for when no command or simulated day is running (world creation, an old save gaining its quests): it
        /// appends the Chronicle entry directly, with an ID from the world seed and the entry count (deterministic).
        /// </summary>
        public static QuestReporter Direct(WorldData w) => (type, severity, importance, settlements, facts, npcs) =>
        {
            var seed = Domain.Randomness.SeedMixer.Combine(Domain.Randomness.SeedMixer.FromWorldSeed(w.Seed), "quest-genesis-event:" + w.HistoricalEvents.Count.ToString(CultureInfo.InvariantCulture));
            w.HistoricalEvents.Add(new HistoricalEvent(new EventId(Npcs.NpcHash.Guid(seed)), w.CurrentSimulationDate, type, severity, importance, settlements, facts, null, null, npcs));
        };

        public static SettlementData FirstTown(WorldData w)
        {
            // Phase 16: the opening town is in the home region (the first), not whichever region's town sorts first.
            var home = w.Regions.Count > 0 ? w.Regions[0].RegionId : default;
            SettlementData best = null;
            foreach (var s in w.Settlements)
                if (s.SettlementType == SettlementType.Town && (w.Regions.Count == 0 || s.RegionId == home) && (best == null || s.SettlementId.CompareTo(best.SettlementId) < 0)) best = s;
            if (best != null) return best;
            foreach (var s in w.Settlements)
                if ((w.Regions.Count == 0 || s.RegionId == home) && (best == null || s.Population > best.Population)) best = s;
            if (best != null) return best;
            foreach (var s in w.Settlements) if (best == null || s.Population > best.Population) best = s;
            return best;
        }

        private static bool HasAuthored(WorldData w, string def)
        {
            foreach (var q in w.Quests) if (q.DefinitionId == def && q.SourceKey == "authored") return true;
            return false;
        }

        private static bool HasOpen(WorldData w, string sourceKey)
        {
            foreach (var q in w.Quests)
                if (q.SourceKey == sourceKey && (q.State == QuestState.Available || q.State == QuestState.Discovered || q.State == QuestState.Offered || q.IsOpen)) return true;
            return false;
        }

        /// <summary>The settlement's first living person in one of these roles (by ID), as the giver binding.</summary>
        private static Dictionary<string, string> Giver(WorldData w, SettlementId s, params string[] roles)
        {
            foreach (var role in roles)
            {
                NpcData best = null;
                foreach (var n in w.Npcs)
                    if (!n.IsDeceased && n.Tier != NpcTier.Unassigned && n.HomeSettlementId == s && n.Role.ToString() == role && (best == null || n.NpcId.CompareTo(best.NpcId) < 0)) best = n;
                if (best != null) return new Dictionary<string, string> { ["giver"] = best.NpcId.ToString() };
            }

            // A small place may have none of those: then whoever speaks for it (its first living person).
            NpcData anyone = null;
            foreach (var n in w.Npcs)
                if (!n.IsDeceased && n.Tier != NpcTier.Unassigned && n.HomeSettlementId == s && (anyone == null || n.NpcId.CompareTo(anyone.NpcId) < 0)) anyone = n;
            if (anyone != null) return new Dictionary<string, string> { ["giver"] = anyone.NpcId.ToString() };
            return null;
        }

        private static QuestInstanceData Make(WorldData w, string defId, Dictionary<string, string> given, string source, Dictionary<string, string> vars, QuestReporter report)
        {
            var def = QuestCatalog.Find(defId);
            return def == null ? null : QuestLifecycle.Create(w, def, given, source, vars, report, out _);
        }

        /// <summary>Emergent quests from today's world. Deterministic: settlements and shops in ID order, keys from their causes.</summary>
        public static bool Emerge(WorldData w, QuestReporter report)
        {
            var changed = false;
            var settlements = new List<SettlementData>(w.Settlements);
            settlements.Sort((a, b) => a.SettlementId.CompareTo(b.SettlementId));
            foreach (var s in settlements)
            {
                // A settlement short of food asks for food.
                var shortage = "shortage:food:" + s.SettlementId;
                if (s.IsInFoodShortage && !HasOpen(w, shortage))
                {
                    var given = Giver(w, s.SettlementId, "Elder", "Farmer", "Merchant");
                    if (given != null && Make(w, QuestCatalog.FoodRelief, given, shortage, null, report) != null) changed = true;
                }

                // Predators in reach of a settlement that keeps herds.
                var herds = false;
                foreach (var h in w.Livestock) if (h.SettlementId == s.SettlementId && h.Count > 0) herds = true;
                if (!herds) continue;
                foreach (var creature in Predators)
                {
                    var key = "predators:" + s.SettlementId + ":" + creature;
                    if (QuestCauses.PredatorsNear(w, s.SettlementId, creature) < PredatorThreshold || HasOpen(w, key)) continue;
                    var given = Giver(w, s.SettlementId, "Guard", "Farmer", "Elder");
                    if (given == null) continue;
                    given["creature"] = creature;
                    var def = CreatureCatalog.Find(creature);
                    var vars = new Dictionary<string, string> { ["count"] = "2", ["creature_plural"] = Plural(def?.Name ?? creature) };
                    if (Make(w, QuestCatalog.WolvesAtTheFolds, given, key, vars, report) != null) changed = true;
                    break; // one hunt per settlement at a time
                }
            }

            // A merchant out of a material they deal in.
            var shops = new List<ShopData>(w.Shops);
            shops.Sort((a, b) => a.ShopId.CompareTo(b.ShopId));
            foreach (var shop in shops)
            {
                var keeper = w.FindNpc(shop.ShopkeeperNpcId);
                if (keeper == null || keeper.IsDeceased) continue;
                foreach (var item in ShopMaterials)
                {
                    var def = ItemCatalog.Find(item);
                    if (def == null || !CommerceMath.Sells(shop, def) || shop.QuantityOf(item) > 0) continue;
                    var key = "supplies:" + shop.ShopId + ":" + item;
                    if (HasOpen(w, key)) break;
                    const int quantity = 4;
                    var price = CommerceMath.SellingPrice(w, shop, def).UnitPrice;
                    var vars = new Dictionary<string, string> { ["quantity"] = I(quantity), ["item_name"] = def.Name.ToLowerInvariant(), ["payment"] = I(Math.Max(quantity, price * quantity * 5 / 4)) };
                    if (Make(w, QuestCatalog.ShopSupplies, new Dictionary<string, string> { ["giver"] = keeper.NpcId.ToString(), ["item"] = item }, key, vars, report) != null) changed = true;
                    break; // one request per shop at a time
                }
            }

            return changed;
        }

        public static string Plural(string name)
        {
            var n = name.ToLowerInvariant();
            if (n.EndsWith("wolf", StringComparison.Ordinal)) return n.Substring(0, n.Length - 1) + "ves";
            return n.EndsWith("s", StringComparison.Ordinal) ? n : n + "s";
        }
    }

    /// <summary>
    /// The daily quest pass (order 480, after gathering and before the chronicle): offers lapse, open quests fail when
    /// their failure conditions hold, timed objectives run out, emergent quests arise from the day's world, and people
    /// who heard of the player's deeds today react. It reads only the quests and the people's new knowledge: no scan of
    /// the world per frame, and nothing it does depends on the order of evaluation beyond IDs.
    /// </summary>
    public sealed class QuestSystem : ISimulationSystem
    {
        public const string Id = "realmkeeper.quests";

        public string SystemId => Id;
        public int ExecutionOrder => 480;

        public void Execute(SimulationDayContext context)
        {
            var w = context.World;
            QuestReporter report = (type, severity, importance, settlements, facts, npcs) =>
            {
                var data = new EventData();
                foreach (var pair in facts) data.Set(pair.Key, pair.Value);
                context.ReportEvent(type, severity, importance, settlements, data, npcs);
            };
            var now = QuestClock.Now(w);
            foreach (var q in new List<QuestInstanceData>(w.Quests))
            {
                if (q.State == QuestState.Available || q.State == QuestState.Discovered || q.State == QuestState.Offered)
                {
                    if (q.ExpiresAt >= 0 && now >= q.ExpiresAt) QuestLifecycle.Move(w, q, QuestState.Unavailable, "the offer lapsed", report);
                    else if (!QuestCauses.Holds(w, q)) QuestLifecycle.Move(w, q, QuestState.Unavailable, "no longer needed", report);
                    continue;
                }

                if (!q.IsOpen) continue;
                QuestObjectives.CheckTimers(w, q);
                var why = QuestLifecycle.FailureNow(w, q);
                if (why != null) QuestLifecycle.Move(w, q, QuestState.Failed, why, report);
            }

            if (context.Date.ToDayNumber() % Domain.Calendar.WorldDate.DaysPerWeek == 0 || w.Quests.Count == 0) QuestGenesis.Emerge(w, report);
            else
            {
                // Food shortages are urgent: checked daily; predators and stock weekly.
                foreach (var s in w.Settlements)
                    if (s.IsInFoodShortage)
                    {
                        QuestGenesis.Emerge(w, report);
                        break;
                    }
            }

            // Deeds heard today (the Phase 4 sharing carried them): each listener reacts once.
            var today = w.CurrentSimulationDate;
            foreach (var npc in w.Npcs)
            {
                if (npc.IsDeceased) continue;
                foreach (var k in npc.Knowledge)
                    if (k.Topic == KnowledgeTopic.PlayerDeed && k.Hops > 0 && k.LearnedOn == today) Deeds.React(w, npc, k.SubjectId, k.Value);
            }
        }
    }
}
