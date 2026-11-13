using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// A recipe (ADR-0054): materials and quantities, the tool and its proficiency, the station (a kind of place in the
    /// settlement), the time, a check (an ability with the tool's proficiency against a DC), the output, and what
    /// failure and great success mean. A recipe either works with the rules here or does not exist.
    /// </summary>
    /// <summary>Phase 21 (ADR-0107): a physical work station, kept in a shop of a kind that has one (<see cref="ShopKindDefinition.Stations"/>).</summary>
    public enum StationKind
    {
        None = 0,
        Forge = 1,
        Workbench = 2,
        LeatherBench = 3,
        SewingTable = 4,
        Oven = 5,
        Hearth = 6,
        Still = 7,
    }

    public static class StationWords
    {
        public static string Name(StationKind k)
        {
            switch (k)
            {
                case StationKind.Forge: return "forge and anvil";
                case StationKind.Workbench: return "woodworker's bench";
                case StationKind.LeatherBench: return "leatherworker's bench";
                case StationKind.SewingTable: return "sewing table";
                case StationKind.Oven: return "bread oven";
                case StationKind.Hearth: return "kitchen hearth";
                case StationKind.Still: return "apothecary's still";
                default: return "work place";
            }
        }
    }

    public sealed class RecipeDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Phase 21: the physical station it is made at (a forge, a bench, an oven …), found in a shop of a kind that keeps
        /// one; None for work that needs only a settlement. The older <see cref="Station"/> place kind, when set, still counts.
        /// </summary>
        public StationKind StationKind { get; set; }

        /// <summary>"blacksmithing", "armorsmithing", "leatherworking", "woodworking", "tailoring", "alchemy", "cooking", "enchanting", "general".</summary>
        public string Discipline { get; set; }

        public IReadOnlyList<(string ItemId, int Quantity)> Inputs { get; set; } = Array.Empty<(string, int)>();

        /// <summary>The tool used (and the proficiency it carries), or null.</summary>
        public string ToolId { get; set; }

        /// <summary>The kind of place it is made at (a smithy is a workshop), or null for anywhere in a settlement.</summary>
        public LocationKind? Station { get; set; }

        public int Minutes { get; set; }

        /// <summary>The check: ability and DC (0: no check, it always works).</summary>
        public Ability CheckAbility { get; set; } = Ability.Intelligence;
        public int Dc { get; set; }

        public string OutputItemId { get; set; }
        public int OutputQuantity { get; set; } = 1;

        /// <summary>Beating the DC by 5 or more makes it Fine (only for things that are made well or badly: gear).</summary>
        public bool CanBeFine { get; set; }

        /// <summary>On a failed check, the fraction of materials lost (percent); the rest come back.</summary>
        public int FailureLossPercent { get; set; } = 50;
    }

    public static class RecipeCatalog
    {
        private static readonly List<RecipeDefinition> All = new List<RecipeDefinition>();

        public static IReadOnlyList<RecipeDefinition> Recipes => All;

        public static RecipeDefinition Find(string id)
        {
            foreach (var r in All) if (r.Id == id) return r;
            return null;
        }

        /// <summary>Every discipline the data knows, including those with no recipes yet (prepared, not built).</summary>
        public static readonly IReadOnlyList<string> Disciplines = new[] { "blacksmithing", "armorsmithing", "leatherworking", "woodworking", "tailoring", "alchemy", "cooking", "enchanting", "general" };

        private static void Add(RecipeDefinition r) => All.Add(r);

        static RecipeCatalog()
        {
            // Blacksmithing (at a smithy: a workshop place).
            Add(new RecipeDefinition { Id = "smelt_iron", Name = "Smelt an iron bar", Discipline = "blacksmithing", StationKind = StationKind.Forge, Inputs = new[] { ("iron_ore", 2), ("charcoal", 1) }, ToolId = "smiths_tools",
                Station = LocationKind.Workshop, Minutes = 120, CheckAbility = Ability.Strength, Dc = 10, OutputItemId = "iron_bar", FailureLossPercent = 50 });
            Add(new RecipeDefinition { Id = "forge_dagger", Name = "Forge a dagger", Discipline = "blacksmithing", StationKind = StationKind.Forge, Inputs = new[] { ("iron_bar", 1), ("timber_plank", 1) }, ToolId = "smiths_tools",
                Station = LocationKind.Workshop, Minutes = 240, CheckAbility = Ability.Strength, Dc = 12, OutputItemId = "dagger_iron", CanBeFine = true, FailureLossPercent = 0 });
            Add(new RecipeDefinition { Id = "forge_arrowheads", Name = "Make arrows", Discipline = "blacksmithing", StationKind = StationKind.Forge, Inputs = new[] { ("iron_bar", 1), ("timber_plank", 1) }, ToolId = "smiths_tools",
                Station = LocationKind.Workshop, Minutes = 180, CheckAbility = Ability.Dexterity, Dc = 10, OutputItemId = "arrows", OutputQuantity = 20, FailureLossPercent = 50 });

            // Leatherworking.
            Add(new RecipeDefinition { Id = "cure_hide", Name = "Cure a hide", Discipline = "leatherworking", Inputs = new[] { ("raw_hide", 1) }, ToolId = "leatherworkers_tools",
                Minutes = 240, CheckAbility = Ability.Wisdom, Dc = 8, OutputItemId = "cured_leather", OutputQuantity = 2, FailureLossPercent = 100 });
            Add(new RecipeDefinition { Id = "stitch_jerkin", Name = "Stitch a leather jerkin", Discipline = "leatherworking", Inputs = new[] { ("cured_leather", 3) }, ToolId = "leatherworkers_tools",
                Minutes = 360, CheckAbility = Ability.Dexterity, Dc = 12, OutputItemId = "jerkin_leather", CanBeFine = true, FailureLossPercent = 34 });
            Add(new RecipeDefinition { Id = "make_sling", Name = "Make a sling", Discipline = "general", Inputs = new[] { ("cured_leather", 1) }, Minutes = 60,
                CheckAbility = Ability.Dexterity, Dc = 5, OutputItemId = "sling", FailureLossPercent = 0 });

            // Woodworking.
            Add(new RecipeDefinition { Id = "carve_staff", Name = "Carve a quarterstaff", Discipline = "woodworking", Inputs = new[] { ("timber_plank", 2) }, ToolId = "carpenters_tools",
                Minutes = 120, CheckAbility = Ability.Dexterity, Dc = 8, OutputItemId = "staff_quarter", CanBeFine = true, FailureLossPercent = 50 });

            // Tailoring.
            Add(new RecipeDefinition { Id = "sew_cloak", Name = "Sew a traveller's cloak", Discipline = "tailoring", Inputs = new[] { ("linen_cloth", 3) }, ToolId = "weavers_tools",
                Minutes = 240, CheckAbility = Ability.Dexterity, Dc = 10, OutputItemId = "travellers_cloak", FailureLossPercent = 34 });

            // Alchemy (at a shrine, where the herbs are dried and the stills kept).
            Add(new RecipeDefinition { Id = "brew_healing", Name = "Brew a potion of healing", Discipline = "alchemy", StationKind = StationKind.Still, Inputs = new[] { ("silverleaf", 3), ("glass_vial", 1) }, ToolId = "alchemists_supplies",
                Station = LocationKind.Worship, Minutes = 240, CheckAbility = Ability.Intelligence, Dc = 13, OutputItemId = "potion_healing", FailureLossPercent = 100 });

            // Cooking (in a tavern's kitchen).
            Add(new RecipeDefinition { Id = "cook_stew", Name = "Cook a hunter's stew", Discipline = "cooking",  StationKind = StationKind.Hearth, Inputs = new[] { ("venison", 1) }, ToolId = "cooks_utensils",
                Station = LocationKind.Tavern, Minutes = 90, CheckAbility = Ability.Wisdom, Dc = 8, OutputItemId = "hunters_stew", OutputQuantity = 2, FailureLossPercent = 100 });
            Add(new RecipeDefinition { Id = "smoke_fish", Name = "Smoke fish", Discipline = "cooking", Inputs = new[] { ("fresh_fish", 2), ("charcoal", 1) }, ToolId = "cooks_utensils",
                Minutes = 120, CheckAbility = Ability.Wisdom, Dc = 8, OutputItemId = "smoked_fish", OutputQuantity = 2, FailureLossPercent = 50 });

            // ----- Phase 21 (ADR-0107): work at the stations the specialist shops keep. Each makes a real catalog item from
            // real inputs; the keepers make them too, from their own shop's stock (NPC production).

            // At the forge.
            Add(new RecipeDefinition { Id = "forge_shortsword", Name = "Forge a shortsword", Discipline = "blacksmithing", StationKind = StationKind.Forge, Inputs = new[] { ("iron_bar", 2), ("timber_plank", 1) },
                ToolId = "smiths_tools", Minutes = 300, CheckAbility = Ability.Strength, Dc = 13, OutputItemId = "shortsword", CanBeFine = true, FailureLossPercent = 0 });
            Add(new RecipeDefinition { Id = "forge_hand_axe", Name = "Forge a hand axe", Discipline = "blacksmithing", StationKind = StationKind.Forge, Inputs = new[] { ("iron_bar", 2), ("timber_plank", 1) },
                ToolId = "smiths_tools", Minutes = 240, CheckAbility = Ability.Strength, Dc = 12, OutputItemId = "axe_hand", CanBeFine = true, FailureLossPercent = 0 });
            Add(new RecipeDefinition { Id = "forge_iron_cap", Name = "Forge an iron cap", Discipline = "armorsmithing", StationKind = StationKind.Forge, Inputs = new[] { ("iron_bar", 2) },
                ToolId = "smiths_tools", Minutes = 300, CheckAbility = Ability.Strength, Dc = 13, OutputItemId = "cap_iron", FailureLossPercent = 0 });
            Add(new RecipeDefinition { Id = "forge_chain_shirt", Name = "Link a chain shirt", Discipline = "armorsmithing", StationKind = StationKind.Forge, Inputs = new[] { ("iron_bar", 5) },
                ToolId = "smiths_tools", Minutes = 900, CheckAbility = Ability.Strength, Dc = 15, OutputItemId = "chain_shirt", CanBeFine = true, FailureLossPercent = 20 });

            // At the leatherworker's bench.
            Add(new RecipeDefinition { Id = "stitch_bracers", Name = "Stitch leather bracers", Discipline = "leatherworking", StationKind = StationKind.LeatherBench, Inputs = new[] { ("cured_leather", 1) },
                ToolId = "leatherworkers_tools", Minutes = 180, CheckAbility = Ability.Dexterity, Dc = 10, OutputItemId = "bracers_leather", FailureLossPercent = 50 });
            Add(new RecipeDefinition { Id = "stitch_leggings", Name = "Stitch leather leggings", Discipline = "leatherworking", StationKind = StationKind.LeatherBench, Inputs = new[] { ("cured_leather", 2) },
                ToolId = "leatherworkers_tools", Minutes = 300, CheckAbility = Ability.Dexterity, Dc = 12, OutputItemId = "leggings_leather", FailureLossPercent = 50 });
            Add(new RecipeDefinition { Id = "make_riding_boots", Name = "Make riding boots", Discipline = "leatherworking", StationKind = StationKind.LeatherBench, Inputs = new[] { ("cured_leather", 2) },
                ToolId = "leatherworkers_tools", Minutes = 300, CheckAbility = Ability.Dexterity, Dc = 12, OutputItemId = "boots_riding", FailureLossPercent = 50 });
            Add(new RecipeDefinition { Id = "make_backpack", Name = "Make a backpack", Discipline = "leatherworking", StationKind = StationKind.LeatherBench, Inputs = new[] { ("cured_leather", 2) },
                ToolId = "leatherworkers_tools", Minutes = 240, CheckAbility = Ability.Dexterity, Dc = 10, OutputItemId = "backpack", FailureLossPercent = 50 });

            // At the sewing table.
            Add(new RecipeDefinition { Id = "sew_tunic", Name = "Sew a linen tunic", Discipline = "tailoring", StationKind = StationKind.SewingTable, Inputs = new[] { ("linen_cloth", 2) },
                ToolId = "weavers_tools", Minutes = 180, CheckAbility = Ability.Dexterity, Dc = 8, OutputItemId = "tunic_linen", FailureLossPercent = 50 });
            Add(new RecipeDefinition { Id = "sew_padded", Name = "Quilt a padded coat", Discipline = "tailoring", StationKind = StationKind.SewingTable, Inputs = new[] { ("linen_cloth", 4) },
                ToolId = "weavers_tools", Minutes = 420, CheckAbility = Ability.Dexterity, Dc = 11, OutputItemId = "padded_armour", FailureLossPercent = 34 });

            // At the woodworker's bench (bowyer and carpenter).
            Add(new RecipeDefinition { Id = "carve_bow", Name = "Shape a hunting bow", Discipline = "woodworking", StationKind = StationKind.Workbench, Inputs = new[] { ("timber_plank", 2) },
                ToolId = "carpenters_tools", Minutes = 360, CheckAbility = Ability.Dexterity, Dc = 13, OutputItemId = "bow_hunting", CanBeFine = true, FailureLossPercent = 50 });
            Add(new RecipeDefinition { Id = "fletch_arrows", Name = "Fletch arrows", Discipline = "woodworking", StationKind = StationKind.Workbench, Inputs = new[] { ("timber_plank", 1) },
                ToolId = "carpenters_tools", Minutes = 180, CheckAbility = Ability.Dexterity, Dc = 10, OutputItemId = "arrows", OutputQuantity = 10, FailureLossPercent = 50 });
            Add(new RecipeDefinition { Id = "make_round_shield", Name = "Make a round shield", Discipline = "woodworking", StationKind = StationKind.Workbench, Inputs = new[] { ("timber_plank", 3), ("iron_bar", 1) },
                ToolId = "carpenters_tools", Minutes = 300, CheckAbility = Ability.Strength, Dc = 11, OutputItemId = "shield_round", CanBeFine = true, FailureLossPercent = 34 });

            // At the bread oven.
            Add(new RecipeDefinition { Id = "bake_bread", Name = "Bake bread", Discipline = "cooking", StationKind = StationKind.Oven, Inputs = new[] { ("grain_sack", 1) },
                ToolId = "cooks_utensils", Minutes = 120, CheckAbility = Ability.Wisdom, Dc = 6, OutputItemId = "bread_loaf", OutputQuantity = 4, FailureLossPercent = 100 });
            Add(new RecipeDefinition { Id = "pack_rations", Name = "Pack travel rations", Discipline = "cooking", StationKind = StationKind.Oven, Inputs = new[] { ("bread_loaf", 2), ("smoked_fish", 1) },
                ToolId = "cooks_utensils", Minutes = 60, CheckAbility = Ability.Wisdom, Dc = 5, OutputItemId = "travel_rations", OutputQuantity = 2, FailureLossPercent = 0 });
        }

        /// <summary>Checks the recipes: every item and tool exists, the tool's discipline fits, quantities and times are sensible.</summary>
        public static List<string> Validate()
        {
            var errors = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in All)
            {
                if (!seen.Add(r.Id)) errors.Add($"Recipe '{r.Id}' appears twice.");
                foreach (var (item, qty) in r.Inputs)
                {
                    if (ItemCatalog.Find(item) == null) errors.Add($"Recipe '{r.Id}' needs unknown item '{item}'.");
                    if (qty < 1) errors.Add($"Recipe '{r.Id}' needs {qty} of '{item}'.");
                }

                if (ItemCatalog.Find(r.OutputItemId) == null || r.OutputQuantity < 1) errors.Add($"Recipe '{r.Id}' makes nothing known.");
                if (r.ToolId != null && ItemCatalog.Find(r.ToolId)?.ToolDiscipline == null) errors.Add($"Recipe '{r.Id}' uses '{r.ToolId}', which is not a crafting tool.");
                if (r.Minutes <= 0) errors.Add($"Recipe '{r.Id}' takes no time.");
                if (r.FailureLossPercent < 0 || r.FailureLossPercent > 100) errors.Add($"Recipe '{r.Id}' loses {r.FailureLossPercent}% on failure.");
                if (r.CanBeFine && ItemCatalog.Find(r.OutputItemId)?.Wears != true) errors.Add($"Recipe '{r.Id}' can make a Fine thing that cannot be made well or badly.");
                var known = false;
                foreach (var d in Disciplines) if (d == r.Discipline) known = true;
                if (!known) errors.Add($"Recipe '{r.Id}' has an unknown discipline '{r.Discipline}'.");
            }

            return errors;
        }
    }
}
