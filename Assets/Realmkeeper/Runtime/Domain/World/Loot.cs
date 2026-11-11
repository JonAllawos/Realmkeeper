using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Randomness;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// One line of a loot table (ADR-0053): an item, another table, or coin; guaranteed, or by chance, or picked by
    /// weight among the table's weighted lines; a quantity range; a condition for used gear; a rarity band; and tags
    /// the context must carry for the line to apply (a creature category, a terrain, a wealth band).
    /// </summary>
    public sealed class LootTableEntry
    {
        public string ItemId { get; set; }
        public string TableId { get; set; }
        public bool Coin { get; set; }

        /// <summary>Always given (once per roll of the table).</summary>
        public bool Guaranteed { get; set; }

        /// <summary>Given by its own chance (0–100), independent of the weighted picks; 0: not a chance line.</summary>
        public int ChancePercent { get; set; }

        /// <summary>Weight among the table's weighted lines (picked <see cref="LootTable.Picks"/> times).</summary>
        public int Weight { get; set; }

        public int Min { get; set; } = 1;
        public int Max { get; set; } = 1;

        /// <summary>For used gear: the condition it is found in (Good when null).</summary>
        public ItemCondition? Condition { get; set; }

        /// <summary>Tags the context must carry (all of them), or empty.</summary>
        public IReadOnlyList<string> RequiresTags { get; set; } = Array.Empty<string>();
    }

    public sealed class LootTable
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>How many weighted picks (0: none).</summary>
        public int Picks { get; set; }

        public IReadOnlyList<LootTableEntry> Entries { get; set; } = Array.Empty<LootTableEntry>();
    }

    /// <summary>What a roll is for: the tags that switch lines on and the highest rarity that may appear.</summary>
    public sealed class LootContext
    {
        public HashSet<string> Tags { get; } = new HashSet<string>(StringComparer.Ordinal);
        public ItemRarity MaxRarity { get; set; } = ItemRarity.Uncommon;
    }

    public sealed class LootDrop
    {
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public ItemCondition Condition { get; set; } = ItemCondition.Good;
    }

    public sealed class LootResult
    {
        public int Coin { get; set; }
        public List<LootDrop> Items { get; } = new List<LootDrop>();
    }

    /// <summary>
    /// The loot tables and the deterministic roller (ADR-0053). The same seed and context always give the same loot.
    /// Creatures' own gear is separate: a humanoid leaves what it fought with (<see cref="Carried"/>); the table adds
    /// what it carried besides (coin, a trinket), and a beast's table gives materials.
    /// </summary>
    public static class LootTables
    {
        private static readonly Dictionary<string, LootTable> ById = new Dictionary<string, LootTable>(StringComparer.Ordinal);

        public static IEnumerable<LootTable> All => ById.Values;

        public static LootTable Find(string id) => id != null && ById.TryGetValue(id, out var t) ? t : null;

        private static LootTableEntry I(string id, int min = 1, int max = 1, int chance = 0, int weight = 0, bool guaranteed = false, ItemCondition? condition = null, params string[] tags) =>
            new LootTableEntry { ItemId = id, Min = min, Max = max, ChancePercent = chance, Weight = weight, Guaranteed = guaranteed, Condition = condition, RequiresTags = tags };

        private static LootTableEntry Coin(int min, int max) => new LootTableEntry { Coin = true, Min = min, Max = max, Guaranteed = true };

        private static void Add(string id, string name, int picks, params LootTableEntry[] entries) => ById[id] = new LootTable { Id = id, Name = name, Picks = picks, Entries = entries };

        static LootTables()
        {
            // People who live by the road.
            Add("outlaw", "An outlaw's purse", 1, Coin(2, 12), I("travel_rations", 1, 2, weight: 4), I("ale_jug", weight: 2), I("dice_set", weight: 2), I("silver_amulet", weight: 1),
                I("potion_healing", chance: 10), I("old_key", chance: 5));
            Add("outlaw_leader", "A road captain's purse", 1, Coin(15, 45), I("potion_healing", chance: 40), I("silver_amulet", weight: 3), I("gold_ring", weight: 2), I("almanac", weight: 1),
                I("sealed_letter", chance: 15));
            Add("cultist", "A cultist's things", 1, Coin(1, 6), I("almanac", weight: 1), I("silver_amulet", weight: 1), I("travel_rations", weight: 2));
            Add("goblin", "A goblin's pouch", 1, Coin(0, 4), I("dice_set", weight: 2), I("arrows", 2, 6, weight: 3), I("old_key", weight: 1));
            Add("soldier", "A soldier's kit", 1, Coin(5, 20), I("travel_rations", 1, 3, weight: 3), I("potion_healing", chance: 25));

            // Beasts: what a hunter takes from them.
            Add("game_deer", "A deer", 0, I("venison", 2, 3, guaranteed: true), I("raw_hide", guaranteed: true), I("antler", chance: 50));
            Add("game_boar", "A boar", 0, I("venison", 2, 4, guaranteed: true), I("raw_hide", guaranteed: true));
            Add("game_small", "Small game", 0, I("venison", guaranteed: true));
            Add("wolf", "A wolf", 0, I("wolf_pelt", guaranteed: true), I("venison", chance: 50));
            Add("bear", "A bear", 0, I("raw_hide", 2, 2, guaranteed: true), I("venison", 3, 5, guaranteed: true));
            Add("vermin", "Vermin", 0);
            Add("spider", "A giant spider", 0, I("linen_cloth", 1, 2, chance: 40)); // its silk is spun like flax

            // The dead and the large.
            Add("undead", "Grave goods", 1, Coin(0, 8), I("old_key", weight: 2), I("silver_amulet", weight: 1), I("gold_ring", weight: 1));
            Add("giant", "A giant's hoard", 1, Coin(20, 80), I("gold_ring", weight: 2), I("potion_greater_healing", weight: 1), I("raw_hide", 2, 3, weight: 2));

            // Places.
            Add("old_cache", "An old cache", 2, Coin(10, 40), I("potion_healing", 1, 2, weight: 4), I("almanac", weight: 2), I("gold_ring", weight: 2), I("silver_amulet", weight: 2),
                I("arrows", 6, 12, weight: 2), I("old_key", weight: 1), I("ring_of_protection", chance: 4, tags: new[] { "rare-allowed" }), I("dagger_plus1", chance: 8, tags: new[] { "rare-allowed" }),
                I("goggles_of_night", chance: 6, tags: new[] { "rare-allowed" }));

            // Phase 13: what explorable places hold (ADR-0064).
            Add("miners_stores", "Miners' stores", 2, Coin(2, 12), I("iron_ore", 1, 3, weight: 3), I("silver_ore", 1, 2, weight: 3), I("hooded_lantern", weight: 2),
                I("travel_rations", 1, 2, weight: 2), I("smiths_tools", weight: 1));
            Add("tomb_offerings", "Grave offerings", 2, Coin(5, 30), I("silver_amulet", weight: 2), I("gold_ring", weight: 2), I("holy_symbol", weight: 1), I("old_key", weight: 1),
                I("potion_healing", weight: 1));
            Add("garrison_stores", "Garrison stores", 2, Coin(5, 25), I("crossbow_bolts", 4, 10, weight: 2), I("spear_ash", weight: 2, condition: ItemCondition.Worn),
                I("shield_round", weight: 1, condition: ItemCondition.Worn), I("travel_rations", 1, 3, weight: 2), I("potion_healing", weight: 1));
            Add("vault_treasure", "Hidden treasure", 3, Coin(30, 90), I("gold_ring", weight: 2), I("silver_amulet", weight: 2), I("potion_greater_healing", weight: 1),
                I("ring_of_protection", chance: 15, tags: new[] { "rare-allowed" }), I("dagger_plus1", chance: 10, tags: new[] { "rare-allowed" }));
        }

        /// <summary>
        /// What a creature carries into a fight and so leaves behind (its weapons, as found: used), by creature ID. The
        /// SRD stat block's attacks name these (a bandit's scimitar and light crossbow).
        /// </summary>
        public static readonly IReadOnlyDictionary<string, LootTableEntry[]> Carried = new Dictionary<string, LootTableEntry[]>(StringComparer.Ordinal)
        {
            ["bandit"] = new[] { I("scimitar", guaranteed: true, condition: ItemCondition.Worn), I("light_crossbow", chance: 70, condition: ItemCondition.Worn), I("crossbow_bolts", 2, 8, chance: 70) },
            ["highwayman"] = new[] { I("mace_flanged", guaranteed: true, condition: ItemCondition.Worn), I("heavy_crossbow", chance: 60, condition: ItemCondition.Worn), I("crossbow_bolts", 3, 9, chance: 60) },
            ["bandit_captain"] = new[] { I("scimitar", guaranteed: true, condition: ItemCondition.Good), I("dagger_iron", guaranteed: true, condition: ItemCondition.Good) },
            ["goblin"] = new[] { I("scimitar", guaranteed: true, condition: ItemCondition.Damaged), I("bow_hunting", chance: 60, condition: ItemCondition.Worn) },
            ["cultist"] = new[] { I("scimitar", guaranteed: true, condition: ItemCondition.Worn) },
            ["veteran"] = new[] { I("sword_arming", guaranteed: true, condition: ItemCondition.Good), I("shortsword", guaranteed: true, condition: ItemCondition.Worn), I("heavy_crossbow", chance: 50, condition: ItemCondition.Worn) },
            ["guard"] = new[] { I("spear_ash", guaranteed: true, condition: ItemCondition.Worn) },
            ["skeleton"] = new[] { I("shortsword", guaranteed: true, condition: ItemCondition.Damaged), I("bow_hunting", chance: 50, condition: ItemCondition.Damaged), I("arrows", 1, 6, chance: 50) },
            ["ogre"] = new[] { I("club", guaranteed: true, condition: ItemCondition.Damaged) },
        };

        /// <summary>The table for what a creature carried besides its weapons, or the materials a beast yields.</summary>
        public static readonly IReadOnlyDictionary<string, string> ForCreature = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bandit"] = "outlaw", ["highwayman"] = "outlaw", ["bandit_captain"] = "outlaw_leader", ["thug"] = "outlaw", ["cultist"] = "cultist", ["cult_fanatic"] = "cultist",
            ["goblin"] = "goblin", ["veteran"] = "soldier", ["guard"] = "soldier", ["deer"] = "game_deer", ["boar"] = "game_boar", ["rabbit"] = "game_small", ["fox"] = "game_small",
            ["wolf"] = "wolf", ["dire_wolf"] = "wolf", ["black_bear"] = "bear", ["brown_bear"] = "bear", ["giant_rat"] = "vermin", ["giant_spider"] = "spider",
            ["skeleton"] = "undead", ["zombie"] = "undead", ["ogre"] = "giant", ["troll"] = "giant",
        };

        /// <summary>Rolls a table (and the tables it names) with a seed. Pure: the same seed and context, the same loot.</summary>
        public static LootResult Roll(string tableId, LootContext context, ulong seed)
        {
            var result = new LootResult();
            var n = 0UL;
            RollInto(result, Find(tableId), context, seed, ref n, 0);
            return result;
        }

        /// <summary>Rolls a list of lines as if it were a table with no weighted picks (carried gear).</summary>
        public static LootResult RollLines(IReadOnlyList<LootTableEntry> lines, LootContext context, ulong seed)
        {
            var result = new LootResult();
            var n = 0UL;
            RollInto(result, new LootTable { Id = "lines", Entries = lines }, context, seed, ref n, 0);
            return result;
        }

        private static void RollInto(LootResult result, LootTable table, LootContext context, ulong seed, ref ulong n, int depth)
        {
            if (table == null || depth > 4) return;
            ulong Next(ref ulong i) => SeedMixer.Mix(SeedMixer.Combine(seed, table.Id + ":" + (i++)));
            var weighted = new List<LootTableEntry>();
            foreach (var e in table.Entries)
            {
                if (!Applies(e, context)) continue;
                if (e.Guaranteed) Give(result, e, context, seed, ref n, depth);
                else if (e.ChancePercent > 0) { if ((int)(Next(ref n) % 100) < e.ChancePercent) Give(result, e, context, seed, ref n, depth); }
                else if (e.Weight > 0) weighted.Add(e);
            }

            var total = 0;
            foreach (var e in weighted) total += e.Weight;
            for (var p = 0; p < table.Picks && total > 0; p++)
            {
                var pick = (int)(Next(ref n) % (ulong)total);
                foreach (var e in weighted)
                {
                    pick -= e.Weight;
                    if (pick >= 0) continue;
                    Give(result, e, context, seed, ref n, depth);
                    break;
                }
            }

            void Give(LootResult r, LootTableEntry e, LootContext c, ulong s, ref ulong i, int d)
            {
                var qty = e.Min + (e.Max > e.Min ? (int)(Next(ref i) % (ulong)(e.Max - e.Min + 1)) : 0);
                if (e.Coin) { r.Coin += qty; return; }
                if (e.TableId != null) { RollInto(r, Find(e.TableId), c, s, ref i, d + 1); return; }
                if (qty <= 0) return;
                var def = ItemCatalog.Find(e.ItemId);
                if (def == null || def.Rarity > c.MaxRarity) return; // rarer than this place or fight allows
                r.Items.Add(new LootDrop { ItemId = e.ItemId, Quantity = qty, Condition = def.Wears ? e.Condition ?? ItemCondition.Good : ItemCondition.Good });
            }
        }

        private static bool Applies(LootTableEntry e, LootContext context)
        {
            foreach (var t in e.RequiresTags) if (!context.Tags.Contains(t)) return false;
            return true;
        }

        /// <summary>Checks the tables: every item and table they name exists; ranges are sensible; carried gear is gear.</summary>
        public static List<string> Validate()
        {
            var errors = new List<string>();
            void Check(string where, LootTableEntry e)
            {
                if (!e.Coin && e.TableId == null && ItemCatalog.Find(e.ItemId) == null) errors.Add($"{where}: unknown item '{e.ItemId}'.");
                if (e.TableId != null && Find(e.TableId) == null) errors.Add($"{where}: unknown table '{e.TableId}'.");
                if (e.Min < 0 || e.Max < e.Min) errors.Add($"{where}: quantity {e.Min}–{e.Max} is not a range.");
                if (e.ChancePercent < 0 || e.ChancePercent > 100) errors.Add($"{where}: chance {e.ChancePercent}% is impossible.");
                if (!e.Guaranteed && e.ChancePercent == 0 && e.Weight <= 0 && !e.Coin) errors.Add($"{where}: a line that can never be given.");
            }

            foreach (var t in All) foreach (var e in t.Entries) Check("Table " + t.Id, e);
            foreach (var pair in Carried) foreach (var e in pair.Value) Check("Carried by " + pair.Key, e);
            foreach (var pair in ForCreature) if (Find(pair.Value) == null) errors.Add($"Creature '{pair.Key}' names unknown table '{pair.Value}'.");
            return errors;
        }
    }
}
