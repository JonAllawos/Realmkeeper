namespace Realmkeeper.Domain.World
{
    public static partial class ItemCatalog
    {
        /// <summary>
        /// Phase 13 (ADR-0064): the few items exploration needs — a key with a lock in the world, a light that answers
        /// darkness, ore a mine still holds and a relic a crypt keeps. Descriptions are Realmkeeper's own.
        /// </summary>
        private static void AddPhase13()
        {
            Add(new ItemDefinition { Id = "fort_key", Name = "Captain's key", Category = ItemCategory.Key, Subcategory = "key", BaseValue = 2, WeightGrams = 80,
                Visual = "like:dagger_iron", Description = "A long key on a leather thong, stamped with a garrison mark.", Tags = new[] { "key" } });
            Add(new ItemDefinition { Id = "hooded_lantern", Name = "Hooded lantern", Category = ItemCategory.Tool, Subcategory = "light", BaseValue = 5, WeightGrams = 900,
                SoldBy = new[] { ShopCategory.General }, Visual = "like:grain_sack", Description = "A tin lantern with a shutter. Carried, it lights the way in dark places.", Tags = new[] { "light" } });
            Add(new ItemDefinition { Id = "silver_ore", Name = "Silver ore", Category = ItemCategory.Material, Subcategory = "ore", BaseValue = 6, WeightGrams = 1500, Stackable = true, MaxStack = 50, MaterialFor = new[] { "blacksmithing" },
                Visual = "like:iron_ore", Description = "Grey rock threaded with bright metal: what the seam was worked for.", Tags = new[] { "metal", "ore" } });
            Add(new ItemDefinition { Id = "fordkeeper_seal", Name = "Fordkeeper's seal", Category = ItemCategory.Jewelry, Subcategory = "seal", BaseValue = 60, WeightGrams = 120,
                Visual = "like:silver_amulet", Description = "A bronze seal ring, its face cut with a ford and a tower. Old, and not forgotten by everyone.", Tags = new[] { "valuable", "relic" } });
        }
    }
}
