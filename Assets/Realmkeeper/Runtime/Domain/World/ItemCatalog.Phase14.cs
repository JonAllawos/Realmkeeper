namespace Realmkeeper.Domain.World
{
    public static partial class ItemCatalog
    {
        /// <summary>
        /// Phase 14 (ADR-0071): the keys of the new places' locks, the fane's relic and the goods the culverts and the
        /// hideout hold. Descriptions are Realmkeeper's own.
        /// </summary>
        private static void AddPhase14()
        {
            Add(new ItemDefinition { Id = "ashgrave_vault_key", Name = "Vault key", Category = ItemCategory.Key, Subcategory = "key", BaseValue = 2, WeightGrams = 90,
                Visual = "like:dagger_iron", Description = "A heavy black key, green at the bow, tied to a sexton's tally stick.", Tags = new[] { "key" } });
            Add(new ItemDefinition { Id = "crowfoot_strongbox_key", Name = "Strongroom key", Category = ItemCategory.Key, Subcategory = "key", BaseValue = 2, WeightGrams = 60,
                Visual = "like:dagger_iron", Description = "A bright new key on a bootlace — whoever cut it had money.", Tags = new[] { "key" } });
            Add(new ItemDefinition { Id = "fane_lantern", Name = "Fane lantern", Category = ItemCategory.Jewelry, Subcategory = "relic", BaseValue = 80, WeightGrams = 1200,
                Visual = "like:silver_amulet", Description = "A bronze lantern, green with age, its panes of thin horn. The faction that built the fane carried one like it.", Tags = new[] { "valuable", "relic", "light" } });
            Add(new ItemDefinition { Id = "merchants_cloth", Name = "Bolt of fine cloth", Category = ItemCategory.TradeGood, Subcategory = "cloth", BaseValue = 12, WeightGrams = 2500, Stackable = true, MaxStack = 10,
                Visual = "like:grain_sack", Description = "Dyed wool with a trader's mark sewn into the selvedge.", Tags = new[] { "trade", "stolen_goods" } });
        }
    }
}
