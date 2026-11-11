using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    // Phase 11 (ADR-0051, ADR-0054): where items rest besides the player's pack — containers with stable keys and
    // persistent contents — and the player's work in progress (crafting, gathering, repairing), and the places
    // materials are gathered from. All saved; none regenerated.

    public enum ContainerKind
    {
        /// <summary>The player's own chest (one in each settlement, at the tavern).</summary>
        Personal = 0,

        /// <summary>A chest that belongs to someone or somewhere.</summary>
        Chest = 1,

        /// <summary>A settlement's store (the granary's goods); the player may only look.</summary>
        SettlementStore = 2,

        /// <summary>A hidden or abandoned cache, filled once from a loot table.</summary>
        Cache = 3,

        /// <summary>What a fight left behind, filled once from the fallen (keyed by the encounter).</summary>
        Loot = 4,

        /// <summary>Storage at a camp (data only: there are no camps yet).</summary>
        Camp = 5,
    }

    public sealed class ContainerData
    {
        /// <summary>Stable key ("chest:{settlement}", "cache:{settlement}", "loot:encounter:{id}").</summary>
        public string Key { get; set; }

        public ContainerKind Kind { get; set; }
        public string Name { get; set; }

        /// <summary>Where it is: a settlement (and the plan building it stands at), or empty for a loot pile out of town.</summary>
        public SettlementId SettlementId { get; set; }
        public string LayoutKey { get; set; }

        /// <summary>Phase 13: the place and feature it stands at (a chest in a mine, a loot pile in a crypt's tomb), or null.</summary>
        public string PlaceId { get; set; }
        public string FeatureId { get; set; }

        /// <summary>True when the player may take from it and put into it (their chest, loot they won, a cache they found).</summary>
        public bool PlayerAccess { get; set; }

        /// <summary>True when the player may put things into it (their chest; not a loot pile or a cache).</summary>
        public bool AcceptsDeposits { get; set; }

        public List<ItemStackData> Items { get; set; } = new List<ItemStackData>();
        public int Coin { get; set; }

        /// <summary>Most grams it holds (0: no limit).</summary>
        public int CapacityGrams { get; set; }

        /// <summary>What filled it, so it is filled once ("encounter:{id}", "table:old_cache").</summary>
        public string SourceKey { get; set; }

        /// <summary>Absolute world minute it was made, or -1.</summary>
        public long CreatedAt { get; set; } = -1;

        /// <summary>The player has looked inside.</summary>
        public bool Opened { get; set; }

        public bool IsEmpty => Items.Count == 0 && Coin == 0;

        public ContainerData Clone()
        {
            var c = (ContainerData)MemberwiseClone();
            c.Items = Items.ConvertAll(x => x.Clone());
            return c;
        }
    }

    public enum WorkKind
    {
        Craft = 0,
        Gather = 1,
        Repair = 2,

        /// <summary>Attuning to a magic item: a short rest (an hour) spent with it (SRD 5.2.1).</summary>
        Attune = 3,
    }

    /// <summary>
    /// The player's work in progress (ADR-0054): materials are held when it starts, the outcome is settled then
    /// (deterministic), and the result is collected once the world clock has passed <see cref="ReadyAt"/>.
    /// </summary>
    public sealed class WorkJobData
    {
        public string Key { get; set; }
        public WorkKind Kind { get; set; }
        public SettlementId SettlementId { get; set; }

        /// <summary>The recipe crafted, the site gathered at, or the stack repaired.</summary>
        public string RecipeId { get; set; }
        public string SiteKey { get; set; }
        public ItemStackId StackId { get; set; }

        /// <summary>What the work holds (materials consumed, or the item under repair), returned if it is cancelled.</summary>
        public List<ItemStackData> Held { get; set; } = new List<ItemStackData>();

        public long StartedAt { get; set; }
        public long ReadyAt { get; set; }

        /// <summary>The check: the d20, the total and the DC (0 when there is none).</summary>
        public int Roll { get; set; }
        public int Total { get; set; }
        public int Dc { get; set; }
        public bool Succeeded { get; set; }

        public string OutputItemId { get; set; }
        public int OutputQuantity { get; set; }
        public ItemQuality OutputQuality { get; set; }

        /// <summary>The outcome in words (set when it is settled).</summary>
        public string Outcome { get; set; } = string.Empty;

        public bool Done { get; set; }

        public WorkJobData Clone()
        {
            var c = (WorkJobData)MemberwiseClone();
            c.Held = Held.ConvertAll(x => x.Clone());
            return c;
        }
    }

    /// <summary>A place to gather a material in a settlement's land: limited, replenished weekly (no endless supply).</summary>
    public sealed class GatheringSiteData
    {
        public string Key { get; set; }
        public SettlementId SettlementId { get; set; }

        /// <summary>"mining", "forestry", "foraging", "fishing".</summary>
        public string Kind { get; set; }

        public string Name { get; set; }
        public string ItemId { get; set; }

        /// <summary>The tool discipline needed ("mining" → a miner's pick), or null.</summary>
        public string ToolDiscipline { get; set; }

        public int Available { get; set; }
        public int Capacity { get; set; }
        public int PerWeek { get; set; }

        /// <summary>Units one stint of work yields, and how long a stint takes.</summary>
        public int Yield { get; set; } = 1;
        public int Minutes { get; set; } = 60;

        public GatheringSiteData Clone() => (GatheringSiteData)MemberwiseClone();
    }
}
