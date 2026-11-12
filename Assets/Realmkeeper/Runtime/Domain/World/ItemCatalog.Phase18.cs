namespace Realmkeeper.Domain.World
{
    public static partial class ItemCatalog
    {
        /// <summary>
        /// Phase 18 (ADR-0089, ADR-0092): the evidence the new places hold, and a few distinctive tools that work with the
        /// place mechanics. Descriptions are Realmkeeper's own.
        /// </summary>
        private static void AddPhase18()
        {
            Add(new ItemDefinition { Id = "old_charter", Name = "Old watch charter", Category = ItemCategory.Jewelry, Subcategory = "document", BaseValue = 30, WeightGrams = 200,
                Visual = "like:silver_amulet", Description = "A charter on vellum, sealed by two settlements: the watch on the cairn to be built and kept by both, by turns, for ever.",
                Tags = new[] { "document", "evidence", "valuable" } });
            Add(new ItemDefinition { Id = "true_ledger", Name = "Reeve's true ledger", Category = ItemCategory.Jewelry, Subcategory = "document", BaseValue = 10, WeightGrams = 600,
                Visual = "like:silver_amulet", Description = "The record-house ledger the fire was meant to destroy. The measures in it do not match the ones the town was told.",
                Tags = new[] { "document", "evidence" } });

            // Distinctive tools (Phase 18 rewards; each has a use in the places' mechanics).
            Add(new ItemDefinition { Id = "pump_wrench", Name = "Pump-wright's wrench", Category = ItemCategory.Tool, Subcategory = "tool", BaseValue = 18, WeightGrams = 1400,
                Visual = "like:dagger_iron", Description = "A long iron wrench for pump barrels and sluice gear. With it, a cracked pump can be braced and worked again.",
                Tags = new[] { "tool", "repair" } });
            Add(new ItemDefinition { Id = "shuttered_lantern", Name = "Shuttered lantern", Category = ItemCategory.Tool, Subcategory = "light", BaseValue = 20, WeightGrams = 900,
                Visual = "like:silver_amulet", Description = "A lantern with a sliding shutter that throws a narrow, raking beam — the way masons read old cuts in stone.",
                Tags = new[] { "tool", "light", "raking_light" } });
        }
    }
}
