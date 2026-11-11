using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Rpg;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// The mechanical side of a named person (Phase 10, ADR-0043): the archetype that explains them, what they
    /// carry, and — for those who fight, heal or hunt — an ordinary character sheet run by the same rules engine,
    /// advancement, spellcasting and validation as the player's. Lightweight residents have no sheet: in a fight
    /// they use the SRD profile their archetype names. Promotion adds a sheet without touching anything else.
    /// </summary>
    public sealed class NpcCharacterData
    {
        /// <summary>The archetype the person was generated from (<see cref="NpcArchetypeCatalog"/>).</summary>
        public string ArchetypeId { get; set; }

        /// <summary>What they do, in words ("town guard", "shrine priest").</summary>
        public string Occupation { get; set; }

        /// <summary>The character sheet; null for a lightweight resident.</summary>
        public CharacterSheetData Sheet { get; set; }

        public NpcGearData Gear { get; set; } = new NpcGearData();

        /// <summary>Coin carried, in the same unit as the player's purse.</summary>
        public int Purse { get; set; }

        /// <summary>When the sheet was made (generation or promotion).</summary>
        public Calendar.WorldDate SheetSince { get; set; }

        public bool HasSheet => Sheet != null && Sheet.State == CreationState.Complete;

        public NpcCharacterData Clone()
        {
            var c = (NpcCharacterData)MemberwiseClone();
            c.Sheet = Sheet?.Clone();
            c.Gear = (Gear ?? new NpcGearData()).Clone();
            return c;
        }
    }

    /// <summary>What a person wears and carries, by item ID (<see cref="ItemCatalog"/>). Null slots are empty.</summary>
    public sealed class NpcGearData
    {
        public string Body { get; set; }
        public string Shield { get; set; }
        public string MainHand { get; set; }

        /// <summary>A second weapon (not a shield), or null.</summary>
        public string OffHand { get; set; }

        /// <summary>Tools, foci and other things carried (not worn).</summary>
        public List<string> Carried { get; set; } = new List<string>();

        public NpcGearData Clone()
        {
            var c = (NpcGearData)MemberwiseClone();
            c.Carried = new List<string>(Carried ?? new List<string>());
            return c;
        }

        public IEnumerable<string> All()
        {
            if (Body != null) yield return Body;
            if (Shield != null) yield return Shield;
            if (MainHand != null) yield return MainHand;
            if (OffHand != null) yield return OffHand;
            foreach (var x in Carried ?? new List<string>()) yield return x;
        }

        /// <summary>The rules' view of the gear: worn armour and shield.</summary>
        public Loadout Loadout => new Loadout(Body, Shield);
    }

    /// <summary>
    /// A kind of person the world produces (data-driven, ADR-0043). Roles and classes differ: a guard is usually a
    /// fighter; a priest a cleric; a merchant no combat class at all. Level ranges grow with the settlement.
    /// </summary>
    public sealed class NpcArchetypeDefinition
    {
        public string Id { get; set; }
        public string Occupation { get; set; }

        /// <summary>The class of a sheet-holder; null for a lightweight (civilian) archetype.</summary>
        public string ClassId { get; set; }

        /// <summary>The class a lightweight person is promoted into when nothing else is asked for.</summary>
        public string PromotionClassId { get; set; }

        public string SubclassId { get; set; }
        public string BackgroundId { get; set; }
        public string SpeciesId { get; set; } = "human";

        public int MinLevel { get; set; } = 1;
        public int MaxLevel { get; set; } = 1;

        /// <summary>Extra levels in a town or larger (seasoned people gather where there is more to defend).</summary>
        public int TownBonus { get; set; }

        /// <summary>Standard-array order: these abilities get the highest scores, in order.</summary>
        public Ability[] ScoreOrder { get; set; } = Array.Empty<Ability>();

        /// <summary>Preferred picks for any choice (skills, options, spells), tried before catalog order.</summary>
        public string[] Prefer { get; set; } = Array.Empty<string>();

        /// <summary>Spells to prepare, in order, as far as the limit allows (prepared casters).</summary>
        public string[] Prepare { get; set; } = Array.Empty<string>();

        public NpcGearData Gear { get; set; } = new NpcGearData();

        /// <summary>The SRD profile a lightweight person fights with (no sheet).</summary>
        public string CombatProfile { get; set; } = "commoner";

        /// <summary>Coin carried at generation.</summary>
        public int Purse { get; set; }
    }

    /// <summary>The archetypes, and how a person's role, workplace and settlement select one (deterministic).</summary>
    public static class NpcArchetypeCatalog
    {
        public static readonly IReadOnlyList<NpcArchetypeDefinition> All = new[]
        {
            new NpcArchetypeDefinition
            {
                Id = "town_guard", Occupation = "town guard", ClassId = "fighter", BackgroundId = "soldier", MinLevel = 1, MaxLevel = 2,
                ScoreOrder = new[] { Ability.Strength, Ability.Constitution, Ability.Dexterity, Ability.Wisdom },
                Prefer = new[] { "defense", "Athletics", "Perception", "Intimidation" },
                Gear = new NpcGearData { Body = "chain_shirt", Shield = "shield_round", MainHand = "spear_ash" }, Purse = 18,
            },
            new NpcArchetypeDefinition
            {
                Id = "guard_captain", Occupation = "captain of the guard", ClassId = "fighter", SubclassId = "champion", BackgroundId = "soldier", MinLevel = 3, MaxLevel = 4, TownBonus = 1,
                ScoreOrder = new[] { Ability.Strength, Ability.Constitution, Ability.Wisdom, Ability.Dexterity },
                Prefer = new[] { "defense", "Athletics", "Perception", "Insight" },
                Gear = new NpcGearData { Body = "chain_mail", Shield = "shield_round", MainHand = "sword_arming" }, Purse = 60,
            },
            new NpcArchetypeDefinition
            {
                Id = "watch_warden", Occupation = "warden of the watchtower", ClassId = "ranger", BackgroundId = "outlander", MinLevel = 1, MaxLevel = 2,
                ScoreOrder = new[] { Ability.Dexterity, Ability.Wisdom, Ability.Constitution, Ability.Strength },
                Prefer = new[] { "archery", "Perception", "Survival", "Stealth", "hunters_mark", "cure_wounds" },
                Gear = new NpcGearData { Body = "jerkin_leather", MainHand = "bow_hunting", OffHand = "shortsword" }, Purse = 12,
            },
            new NpcArchetypeDefinition
            {
                Id = "woodsman", Occupation = "woodsman and hunter", ClassId = "ranger", BackgroundId = "outlander", MinLevel = 1, MaxLevel = 2,
                ScoreOrder = new[] { Ability.Dexterity, Ability.Wisdom, Ability.Constitution, Ability.Strength },
                Prefer = new[] { "archery", "Survival", "Nature", "Stealth", "hunters_mark" },
                Gear = new NpcGearData { Body = "jerkin_forester", MainHand = "bow_hunting", OffHand = "axe_hand" }, Purse = 9,
            },
            new NpcArchetypeDefinition
            {
                Id = "priest", Occupation = "shrine priest", ClassId = "cleric", SubclassId = "life", BackgroundId = "acolyte", MinLevel = 1, MaxLevel = 3,
                ScoreOrder = new[] { Ability.Wisdom, Ability.Constitution, Ability.Strength, Ability.Charisma },
                Prefer = new[] { "protector", "Medicine", "Insight", "Religion", "sacred_flame", "guidance", "light" },
                Prepare = new[] { "cure_wounds", "bless", "healing_word", "guiding_bolt", "lesser_restoration", "aid", "shield_of_faith" },
                Gear = new NpcGearData { Body = "chain_shirt", Shield = "shield_round", MainHand = "mace_flanged", Carried = new List<string> { "holy_symbol" } }, Purse = 25,
            },
            new NpcArchetypeDefinition
            {
                Id = "priest_elder", Occupation = "village elder and priest", ClassId = "cleric", SubclassId = "life", BackgroundId = "acolyte", MinLevel = 3, MaxLevel = 3,
                ScoreOrder = new[] { Ability.Wisdom, Ability.Charisma, Ability.Constitution, Ability.Intelligence },
                Prefer = new[] { "thaumaturge", "Medicine", "Insight", "Religion", "Persuasion", "sacred_flame", "guidance", "light", "thaumaturgy" },
                Prepare = new[] { "cure_wounds", "bless", "healing_word", "lesser_restoration", "aid", "shield_of_faith", "guiding_bolt" },
                Gear = new NpcGearData { MainHand = "staff_quarter", Carried = new List<string> { "holy_symbol", "herbalism_kit" } }, Purse = 30,
            },
            new NpcArchetypeDefinition
            {
                Id = "smith", Occupation = "blacksmith", ClassId = "fighter", BackgroundId = "artisan", MinLevel = 1, MaxLevel = 2,
                ScoreOrder = new[] { Ability.Strength, Ability.Constitution, Ability.Wisdom, Ability.Dexterity },
                Prefer = new[] { "dueling", "Athletics", "Insight" },
                Gear = new NpcGearData { Body = "jerkin_leather", MainHand = "axe_hand", Carried = new List<string> { "smiths_tools" } }, Purse = 40,
            },
            new NpcArchetypeDefinition { Id = "town_elder", Occupation = "town elder", PromotionClassId = "fighter", BackgroundId = "noble", Purse = 80, Gear = new NpcGearData { MainHand = "dagger_iron" } },
            new NpcArchetypeDefinition { Id = "merchant", Occupation = "merchant", PromotionClassId = "rogue", BackgroundId = "merchant", Purse = 120, Gear = new NpcGearData { MainHand = "dagger_iron" } },
            new NpcArchetypeDefinition { Id = "innkeeper", Occupation = "innkeeper", PromotionClassId = "fighter", BackgroundId = "folk_hero", Purse = 50, Gear = new NpcGearData { MainHand = "staff_quarter" } },
            new NpcArchetypeDefinition { Id = "farmer", Occupation = "farmer", PromotionClassId = "ranger", BackgroundId = "folk_hero", Purse = 6, Gear = new NpcGearData { MainHand = "spear_ash" } },
            new NpcArchetypeDefinition { Id = "labourer", Occupation = "labourer", PromotionClassId = "fighter", BackgroundId = "folk_hero", Purse = 4 },
            new NpcArchetypeDefinition { Id = "townsfolk", Occupation = "townsperson", PromotionClassId = "fighter", BackgroundId = "folk_hero", Purse = 5 },
        };

        public static NpcArchetypeDefinition Find(string id)
        {
            foreach (var a in All) if (a.Id == id) return a;
            return null;
        }

        /// <summary>
        /// The archetype a person's role, workplace and settlement point to. Guards who are notable lead; those who
        /// tend a shrine are priests; a labourer of a forestry or hunting settlement is a woodsman; the rest follow
        /// their role.
        /// </summary>
        public static NpcArchetypeDefinition For(WorldData world, NpcData npc)
        {
            var workplace = world.FindLocation(npc.WorkplaceId);
            var settlement = world.FindSettlement(npc.HomeSettlementId);
            var smallPlace = settlement != null && (settlement.SettlementType == SettlementType.Hamlet || settlement.SettlementType == SettlementType.Village);
            if (workplace?.Kind == LocationKind.Worship) return Find(npc.Role == NpcRole.Elder ? "priest_elder" : "priest");
            switch (npc.Role)
            {
                case NpcRole.Guard:
                    if (smallPlace && workplace?.Kind == LocationKind.GuardPost) return Find("watch_warden");
                    return Find(npc.Tier == NpcTier.Notable ? "guard_captain" : "town_guard");
                case NpcRole.Smith: return Find("smith");
                case NpcRole.Elder: return Find("town_elder");
                case NpcRole.Merchant: return Find("merchant");
                case NpcRole.Innkeeper: return Find("innkeeper");
                case NpcRole.Farmer: return Find("farmer");
                case NpcRole.Labourer:
                    var e = settlement?.Economy;
                    var woods = e != null && (e.Specialization == Economy.SettlementSpecialization.Forestry || e.SecondarySpecialization == Economy.SettlementSpecialization.Forestry);
                    return Find(woods ? "woodsman" : "labourer");
                default: return Find("townsfolk");
            }
        }

        /// <summary>The level an archetype is generated at for a person (deterministic per person).</summary>
        public static int LevelFor(NpcArchetypeDefinition a, SettlementData settlement, ulong personHash)
        {
            var span = Math.Max(0, a.MaxLevel - a.MinLevel);
            var level = a.MinLevel + (span == 0 ? 0 : (int)(personHash % (ulong)(span + 1)));
            if (settlement != null && settlement.SettlementType >= SettlementType.Town) level += a.TownBonus;
            return Math.Max(1, Math.Min(Rpg.AbilityRules.MaxLevel, level));
        }

        /// <summary>Problems with the catalog itself: unknown classes, backgrounds, subclasses or items.</summary>
        public static List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var a in All)
            {
                if (!ids.Add(a.Id)) errors.Add($"Archetype '{a.Id}' is listed twice.");
                if (a.ClassId != null && RpgCatalog.FindClass(a.ClassId) == null) errors.Add($"{a.Id}: unknown class '{a.ClassId}'.");
                if (a.PromotionClassId != null && RpgCatalog.FindClass(a.PromotionClassId) == null) errors.Add($"{a.Id}: unknown promotion class '{a.PromotionClassId}'.");
                if (a.ClassId == null && a.PromotionClassId == null) errors.Add($"{a.Id}: needs a class or a promotion class.");
                if (a.SubclassId != null && RpgCatalog.FindSubclass(a.SubclassId) == null) errors.Add($"{a.Id}: unknown subclass '{a.SubclassId}'.");
                if (RpgCatalog.FindBackground(a.BackgroundId) == null) errors.Add($"{a.Id}: unknown background '{a.BackgroundId}'.");
                if (CreatureCatalog.Find(a.CombatProfile) == null) errors.Add($"{a.Id}: unknown combat profile '{a.CombatProfile}'.");
                if (a.MinLevel < 1 || a.MaxLevel < a.MinLevel) errors.Add($"{a.Id}: bad level range.");
                foreach (var item in a.Gear.All()) if (ItemCatalog.Find(item) == null) errors.Add($"{a.Id}: unknown item '{item}'.");
            }

            return errors;
        }
    }
}
