using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    // Phase 8: settlement structures. Definitions (data, never saved) describe kinds of building; instances (saved)
    // are the buildings a settlement owns. A structure built on an existing location or shop links to it rather than
    // duplicating it, and every definition states honestly what it does in the simulation.

    public enum StructureCategory
    {
        Trade = 0,
        Food = 1,
        Hospitality = 2,
        Civic = 3,
        Military = 4,
        Worship = 5,
        Storage = 6,
        Agriculture = 7,
        Transport = 8,
        Utility = 9,
        Housing = 10,
    }

    /// <summary>What a structure does in the simulation (shown everywhere it is listed).</summary>
    public enum StructureRole
    {
        /// <summary>Takes part in an existing system: a shop, a workplace, a home or a gathering place people go to.</summary>
        Functional = 0,

        /// <summary>Has an identity and a description, and is shown in the town, but no simulation behaviour of its own yet.</summary>
        Informational = 1,

        /// <summary>Part of the town's look only.</summary>
        Decorative = 2,
    }

    public enum StructureCondition
    {
        Good = 0,
        Worn = 1,
        Damaged = 2,
    }

    public sealed class StructureDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public StructureCategory Category { get; set; }
        public StructureRole Role { get; set; }

        /// <summary>For functional structures: the system it takes part in, in words ("the Smithy shop", "residents' homes").</summary>
        public string Participates { get; set; }

        /// <summary>The smallest settlement that has one (informational and decorative structures).</summary>
        public SettlementType SmallestSettlement { get; set; }

        /// <summary>The largest settlement that has one (barns belong to hamlets and villages).</summary>
        public SettlementType LargestSettlement { get; set; } = SettlementType.Fortress;

        /// <summary>The location kind a functional structure is built on (it is created from that location).</summary>
        public LocationKind? OnLocation { get; set; }

        /// <summary>A sign or marker word for the presentation.</summary>
        public string Sign { get; set; }
    }

    /// <summary>One building in a settlement.</summary>
    public sealed class StructureData
    {
        public StructureId StructureId { get; set; }
        public string DefinitionId { get; set; }
        public string Name { get; set; }
        public SettlementId SettlementId { get; set; }

        /// <summary>The existing place it is (functional structures); empty otherwise.</summary>
        public LocationId LocationId { get; set; }

        /// <summary>The existing shop it houses, if any.</summary>
        public ShopId ShopId { get; set; }

        public NpcId OwnerNpcId { get; set; }
        public FactionId FactionId { get; set; }

        /// <summary>The site in the settlement's local layout it stands on ("hall", "house-4", "plaza" …), or empty.</summary>
        public string LayoutKey { get; set; } = string.Empty;

        public StructureCondition Condition { get; set; }
        public WorldDate EstablishedOn { get; set; }

        public StructureData Clone() => (StructureData)MemberwiseClone();
    }

    public static class StructureCatalog
    {
        private static readonly Lazy<IReadOnlyList<StructureDefinition>> List = new Lazy<IReadOnlyList<StructureDefinition>>(Build);

        public static IReadOnlyList<StructureDefinition> All => List.Value;

        public static StructureDefinition Find(string id)
        {
            if (id == null) return null;
            foreach (var d in All) if (d.Id == id) return d;
            return null;
        }

        /// <summary>The definition a location of this kind becomes (functional), by kind and, for workshops, name.</summary>
        public static string ForLocation(LocationData l)
        {
            switch (l.Kind)
            {
                case LocationKind.Residence: return "home";
                case LocationKind.Shop: return "shopfront";
                case LocationKind.Workshop: return l.Name == "Smithy" ? "smithy" : "granary";
                case LocationKind.Tavern: return "tavern";
                case LocationKind.Market: return "market";
                case LocationKind.Administration: return "hall_of_governance";
                case LocationKind.Worship: return "shrine";
                case LocationKind.GuardPost: return "guard_post";
                default: return "square";
            }
        }

        private static IReadOnlyList<StructureDefinition> Build() => new[]
        {
            // ----- Functional: built on the settlement's existing places and shops -----
            F("home", "Home", StructureCategory.Housing, LocationKind.Residence, "residents' homes (where people sleep and spend their evenings)", "Home",
                "A family house of timber and plaster."),
            F("shopfront", "Shopfront", StructureCategory.Trade, LocationKind.Shop, "its shop (General Store or Outfitters): buying and selling, stock from the settlement's stores", "Shop",
                "A house with a counter and shelves at the front."),
            F("smithy", "Smithy", StructureCategory.Trade, LocationKind.Workshop, "the Smithy shop and the smith's workplace", "Smithy",
                "A forge, an anvil and a rack of finished arms."),
            F("granary", "Granary", StructureCategory.Storage, LocationKind.Workshop, "the labourers' workplace and, where there is one, the Granary Yard or Farm Stall", "Granary",
                "Raised stores for grain and the settlement's goods."),
            F("tavern", "Tavern", StructureCategory.Hospitality, LocationKind.Tavern, "the Tavern Kitchen and the innkeeper's workplace; where people gather in the evening", "Tavern",
                "The common hall: hearth, tables and a kitchen."),
            F("market", "Market", StructureCategory.Trade, LocationKind.Market, "the merchants' and residents' market (routines and talk)", "Market",
                "Stalls and awnings on the plaza."),
            F("square", "Square", StructureCategory.Civic, LocationKind.Square, "the gathering place in residents' routines", "Square",
                "The open heart of the settlement."),
            F("hall_of_governance", "Keep", StructureCategory.Civic, LocationKind.Administration, "the elders' workplace and the seat of the governing faction", "Keep",
                "Where the settlement is governed and its records kept."),
            F("shrine", "Shrine", StructureCategory.Worship, LocationKind.Worship, "a place of worship in residents' routines", "Shrine",
                "A small house of prayer."),
            F("guard_post", "Guard post", StructureCategory.Military, LocationKind.GuardPost, "the guards' post in their routines; the practice encounter's guard comes from here", "Guard",
                "A watch station on the approach."),

            // ----- Informational: real trades the settlement has, with no simulation of their own yet -----
            I("bakery", "Bakery", StructureCategory.Food, SettlementType.Village, "Bakery", "Ovens that turn the granary's flour into the town's bread."),
            I("butcher", "Butcher", StructureCategory.Food, SettlementType.Town, "Butcher", "A cold room and a block; meat from the farms around."),
            I("carpenter", "Carpenter's workshop", StructureCategory.Trade, SettlementType.Town, "Carpenter", "Benches, saws and stacked timber."),
            I("mason", "Mason's yard", StructureCategory.Trade, SettlementType.Town, "Mason", "Dressed stone and the tools to shape it."),
            I("stable", "Stable", StructureCategory.Transport, SettlementType.Village, "Stable", "Stalls for travellers' horses and the carters' teams."),
            I("warehouse", "Warehouse", StructureCategory.Storage, SettlementType.Town, "Warehouse", "Where merchants keep goods between caravans."),
            I("barn", "Barn", StructureCategory.Agriculture, SettlementType.Hamlet, "Barn", "Hay, tools and animals from the fields outside.", SettlementType.Village),

            // ----- Decorative -----
            D("well", "Well", StructureCategory.Utility, SettlementType.Hamlet, "Well", "The public well."),
            D("market_stalls", "Market stalls", StructureCategory.Trade, SettlementType.Village, "Stalls", "Covered stalls along the plaza."),
        };

        private static StructureDefinition F(string id, string name, StructureCategory c, LocationKind on, string participates, string sign, string text) => new StructureDefinition
        {
            Id = id, Name = name, Category = c, Role = StructureRole.Functional, OnLocation = on, Participates = participates, Sign = sign, Description = text,
            SmallestSettlement = SettlementType.Hamlet,
        };

        private static StructureDefinition I(string id, string name, StructureCategory c, SettlementType smallest, string sign, string text, SettlementType largest = SettlementType.Fortress) => new StructureDefinition
        {
            Id = id, Name = name, Category = c, Role = StructureRole.Informational, SmallestSettlement = smallest, LargestSettlement = largest, Sign = sign, Description = text,
            Participates = "nothing yet: informational (no production, storage or trade is simulated for it)",
        };

        private static StructureDefinition D(string id, string name, StructureCategory c, SettlementType smallest, string sign, string text) => new StructureDefinition
        {
            Id = id, Name = name, Category = c, Role = StructureRole.Decorative, SmallestSettlement = smallest, Sign = sign, Description = text,
            Participates = "nothing: decorative",
        };

        /// <summary>Whether a settlement of this type has an informational or decorative structure of this kind.</summary>
        public static bool Suits(StructureDefinition d, SettlementType type) => d.Role != StructureRole.Functional && type >= d.SmallestSettlement && type <= d.LargestSettlement;
    }
}
