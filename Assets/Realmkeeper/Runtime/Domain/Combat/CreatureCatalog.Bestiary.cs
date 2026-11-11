using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>
    /// The Phase 10 bestiary (ADR-0044). Statistics follow the SRD stat blocks named in each creature's provenance
    /// (SRD 5.2.1 for the Phase 8 four; SRD 5.1 for the rest, both CC BY 4.0). Five kept and small animals that no SRD
    /// covers are Realmkeeper originals, sized to similar SRD beasts and labelled as such. Every description is
    /// Realmkeeper's own. Senses, climbing and smell are shown but not simulated (there is no sight or terrain model).
    /// </summary>
    public static partial class CreatureCatalog
    {
        private const string Srd51Block = "SRD 5.1 stat block (CC BY 4.0)";
        private const string Srd521Block = "SRD 5.2.1 stat block (CC BY 4.0)";
        private const string Original = "Realmkeeper original (no SRD statistics exist; sized to comparable SRD beasts)";

        private static CreatureAttack Melee(string id, string name, int bonus, Dice dice, DamageType type, int reach = 5) =>
            new CreatureAttack { Id = id, Name = name, AttackBonus = bonus, Damage = dice, DamageType = type, Reach = reach };

        private static CreatureAttack Ranged(string id, string name, int bonus, Dice dice, DamageType type, int range, int longRange) =>
            new CreatureAttack { Id = id, Name = name, AttackBonus = bonus, Damage = dice, DamageType = type, Range = range, LongRange = longRange };

        private static BehaviorProfile B(CreatureBehavior kind, string summary, double flee = 0.25, int territory = 30) =>
            new BehaviorProfile { Kind = kind, Summary = summary, FleeBelow = flee, TerritoryFeet = territory };

        private static CreatureTrait T(CreatureTraitKind kind, string name, string text, int value = 0, int sides = 0, int dc = 0, params DamageType[] stoppedBy) =>
            new CreatureTrait { Kind = kind, Name = name, Text = text, Value = value, DiceSides = sides, SaveDc = dc, StoppedBy = stoppedBy };

        private static LootProfile Loot(int coinMin, int coinMax, string remains, params LootEntry[] items) =>
            new LootProfile { CoinMin = coinMin, CoinMax = coinMax, Remains = remains, Items = items };

        private static LootEntry L(string item, int chance, int min = 1, int max = 1) => new LootEntry { ItemId = item, ChancePercent = chance, Min = min, Max = max };

        /// <summary>Bestiary data for the Phase 8 and 9 creatures (statistics unchanged).</summary>
        private static void Annotate(List<CreatureDefinition> list)
        {
            foreach (var c in list)
            {
                switch (c.Id)
                {
                    case "commoner":
                        c.ChallengeRating = "0"; c.Habitats = new[] { Habitat.Settlement, Habitat.Farmland }; c.Social = SocialForm.Band; c.Temperament = "wary";
                        c.Provenance = Srd521Block; c.Languages = "Common";
                        break;
                    case "bandit":
                        c.ChallengeRating = "1/8"; c.Category = CreatureCategory.CommonThreat; c.Habitats = new[] { Habitat.Road, Habitat.Forest, Habitat.Hills };
                        c.Social = SocialForm.Band; c.Temperament = "greedy, opportunistic"; c.Provenance = Srd521Block; c.Languages = "Common";
                        c.Loot = Loot(2, 12, null, L("scimitar", 25), L("light_crossbow", 15));
                        c.Behavior = B(CreatureBehavior.Intelligent, "Ambushes from cover, shoots before closing, runs when the fight turns.", 0.4);
                        break;
                    case "guard":
                        c.ChallengeRating = "1/8"; c.Habitats = new[] { Habitat.Settlement, Habitat.Road }; c.Social = SocialForm.Band; c.Temperament = "dutiful";
                        c.Provenance = Srd521Block; c.Languages = "Common"; c.Senses = "passive Perception 12";
                        c.Behavior = B(CreatureBehavior.Guarding, "Holds the line by what it protects; strikes whatever threatens it.", 0.2);
                        break;
                    case "wolf":
                        c.ChallengeRating = "1/4"; c.Category = CreatureCategory.Wildlife; c.Habitats = new[] { Habitat.Forest, Habitat.Hills, Habitat.Grassland };
                        c.Diet = Diet.Carnivore; c.Social = SocialForm.Pack; c.Temperament = "cautious hunter"; c.Provenance = Srd521Block;
                        c.Senses = "darkvision 60 ft, keen hearing and smell"; c.Traits = new[] { T(CreatureTraitKind.Descriptive, "Keen Hearing and Smell", "Hard to surprise.") };
                        c.Loot = Loot(0, 0, "a wolf pelt and some meat");
                        c.Behavior = B(CreatureBehavior.Pack, "Hunts with its pack: everyone on one quarry; the pack breaks when half of it falls.", 0.3);
                        break;
                    case "acolyte":
                        c.ChallengeRating = "1/4"; c.Habitats = new[] { Habitat.Settlement }; c.Temperament = "devout"; c.Provenance = Srd51Block; c.Languages = "Common";
                        break;
                    case "cult_fanatic":
                        c.ChallengeRating = "2"; c.Category = CreatureCategory.CommonThreat; c.Habitats = new[] { Habitat.Ruins, Habitat.Settlement };
                        c.Social = SocialForm.Band; c.Temperament = "zealous"; c.Provenance = Srd51Block; c.Languages = "Common";
                        c.Loot = Loot(5, 20, null, L("dagger_iron", 60));
                        c.Behavior = B(CreatureBehavior.Intelligent, "Fights to the end for its faith; spends spells freely.", 0);
                        break;
                }
            }
        }

        private static IReadOnlyList<CreatureDefinition> Bestiary() => new[]
        {
            // ===== Wildlife =================================================================================
            new CreatureDefinition
            {
                Id = "deer", Name = "Deer", Description = "A russet-flanked deer, all ears and nerves.", Type = "Beast", Category = CreatureCategory.Wildlife, Source = ContentSource.Srd51, Provenance = Srd51Block,
                ChallengeRating = "0", ArmorClass = 13, ArmorNote = "natural", HitDice = new Dice(1, 8), MaxHp = 4, Speed = 50, Scores = new[] { 11, 16, 11, 2, 14, 5 },
                Senses = "passive Perception 12", Habitats = new[] { Habitat.Forest, Habitat.Grassland }, Diet = Diet.Herbivore, Social = SocialForm.Herd, Temperament = "skittish",
                Attacks = new[] { Melee("bite", "Bite", 2, new Dice(1, 4), DamageType.Piercing) }, Loot = Loot(0, 0, "venison and a hide"),
                Behavior = B(CreatureBehavior.Skittish, "Bolts from anything that comes near; fights only when cornered.", 1),
            },
            new CreatureDefinition
            {
                Id = "rabbit", Name = "Rabbit", Description = "A brown hare of the field margins.", Type = "Beast", Category = CreatureCategory.Wildlife, Size = CreatureSize.Tiny,
                Source = ContentSource.RealmkeeperOriginal, Provenance = Original, ChallengeRating = "0", ArmorClass = 13, ArmorNote = "natural", HitDice = new Dice(1, 4), MaxHp = 2, Speed = 40,
                Scores = new[] { 2, 16, 9, 2, 12, 4 }, Habitats = new[] { Habitat.Grassland, Habitat.Farmland }, Diet = Diet.Herbivore, Social = SocialForm.Herd, Temperament = "timid",
                Attacks = new[] { Melee("kick", "Kick", 0, new Dice(0, 0, 1), DamageType.Bludgeoning) }, Loot = Loot(0, 0, "a rabbit for the pot"),
                Behavior = B(CreatureBehavior.Skittish, "Runs for cover at once.", 1),
            },
            new CreatureDefinition
            {
                Id = "fox", Name = "Fox", Description = "A red fox, quick and bold around henhouses.", Type = "Beast", Category = CreatureCategory.Wildlife, Size = CreatureSize.Small,
                Source = ContentSource.RealmkeeperOriginal, Provenance = Original, ChallengeRating = "0", ArmorClass = 13, ArmorNote = "natural", HitDice = new Dice(1, 6), MaxHp = 3, Speed = 40,
                Scores = new[] { 5, 16, 10, 3, 12, 6 }, Senses = "keen smell", Habitats = new[] { Habitat.Forest, Habitat.Farmland, Habitat.Grassland }, Diet = Diet.Omnivore, Social = SocialForm.Solitary,
                Temperament = "wily", Attacks = new[] { Melee("bite", "Bite", 3, new Dice(1, 3), DamageType.Piercing) }, Loot = Loot(0, 0, "a fox pelt"),
                Behavior = B(CreatureBehavior.Skittish, "Slips away from a fair fight; snaps when cornered.", 1),
            },
            new CreatureDefinition
            {
                Id = "boar", Name = "Boar", Description = "A bristling wild boar with yellowed tusks.", Type = "Beast", Category = CreatureCategory.Wildlife, Source = ContentSource.Srd51, Provenance = Srd51Block,
                ChallengeRating = "1/4", ArmorClass = 11, ArmorNote = "natural", HitDice = new Dice(2, 8, 2), MaxHp = 11, Speed = 40, Scores = new[] { 13, 11, 12, 2, 9, 5 },
                Habitats = new[] { Habitat.Forest, Habitat.Farmland }, Diet = Diet.Omnivore, Social = SocialForm.Herd, Temperament = "short-tempered",
                Attacks = new[] { Melee("tusk", "Tusk", 3, new Dice(1, 6, 1), DamageType.Slashing) }, Loot = Loot(0, 0, "pork and a bristled hide"),
                Traits = new[]
                {
                    T(CreatureTraitKind.Charge, "Charge", "After running 20 ft or more at a target, a tusk hit adds 1d6 slashing; the target makes a DC 11 Strength save or falls prone.", 1, 6, 11),
                    T(CreatureTraitKind.Relentless, "Relentless", "Once per fight, 7 damage or less that would drop it to 0 leaves it at 1.", 7),
                },
                Behavior = B(CreatureBehavior.Territorial, "Charges whatever comes close; does not chase far.", 0, 40),
            },
            new CreatureDefinition
            {
                Id = "black_bear", Name = "Black Bear", Description = "A black bear, heavy-shouldered and wary of people.", Type = "Beast", Category = CreatureCategory.Wildlife,
                Source = ContentSource.Srd51, Provenance = Srd51Block, ChallengeRating = "1/2", ArmorClass = 11, ArmorNote = "natural", HitDice = new Dice(3, 8, 6), MaxHp = 19, Speed = 40, ClimbSpeed = 30,
                Scores = new[] { 15, 10, 14, 2, 12, 7 }, Skills = "Perception +3", Senses = "keen smell", Multiattack = 2, Habitats = new[] { Habitat.Forest, Habitat.Hills },
                Diet = Diet.Omnivore, Social = SocialForm.Solitary, Temperament = "wary", Traits = new[] { T(CreatureTraitKind.Descriptive, "Keen Smell", "Hard to surprise.") },
                Attacks = new[] { Melee("claws", "Claws", 3, new Dice(2, 4, 2), DamageType.Slashing), Melee("bite", "Bite", 3, new Dice(1, 6, 2), DamageType.Piercing) },
                Loot = Loot(0, 0, "a bear pelt and meat"), Behavior = B(CreatureBehavior.Territorial, "Drives intruders off its ground, then lets them go.", 0.3, 30),
            },
            new CreatureDefinition
            {
                Id = "brown_bear", Name = "Brown Bear", Description = "A great brown bear, taller than a man when it rises.", Type = "Beast", Category = CreatureCategory.Dangerous, Size = CreatureSize.Large,
                Source = ContentSource.Srd51, Provenance = Srd51Block, ChallengeRating = "1", ArmorClass = 11, ArmorNote = "natural", HitDice = new Dice(4, 10, 12), MaxHp = 34, Speed = 40, ClimbSpeed = 30,
                Scores = new[] { 19, 10, 16, 2, 13, 7 }, Skills = "Perception +3", Senses = "keen smell", Multiattack = 2, Habitats = new[] { Habitat.Forest, Habitat.Hills, Habitat.Mountains, Habitat.Riverbank },
                Diet = Diet.Omnivore, Social = SocialForm.Solitary, Temperament = "territorial", Traits = new[] { T(CreatureTraitKind.Descriptive, "Keen Smell", "Hard to surprise.") },
                Attacks = new[] { Melee("claws", "Claws", 6, new Dice(2, 6, 4), DamageType.Slashing), Melee("bite", "Bite", 6, new Dice(1, 8, 4), DamageType.Piercing) },
                Loot = Loot(0, 0, "a heavy bear pelt and meat"), Behavior = B(CreatureBehavior.Territorial, "Defends its ground ferociously; rarely follows far.", 0.2, 40),
            },
            new CreatureDefinition
            {
                Id = "dire_wolf", Name = "Dire Wolf", Description = "A wolf the size of a pony, grey-muzzled and scarred.", Type = "Beast", Category = CreatureCategory.Dangerous, Size = CreatureSize.Large,
                Source = ContentSource.Srd51, Provenance = Srd51Block, ChallengeRating = "1", ArmorClass = 14, ArmorNote = "natural", HitDice = new Dice(5, 10, 10), MaxHp = 37, Speed = 50,
                Scores = new[] { 17, 15, 15, 3, 12, 7 }, Skills = "Perception +3, Stealth +4", Senses = "keen hearing and smell", PackTactics = true, Habitats = new[] { Habitat.Forest, Habitat.Hills },
                Diet = Diet.Carnivore, Social = SocialForm.Pack, Temperament = "bold hunter",
                Attacks = new[] { new CreatureAttack { Id = "bite", Name = "Bite", AttackBonus = 5, Damage = new Dice(2, 6, 3), DamageType = DamageType.Piercing, Reach = 5, OnHit = ConditionType.Prone, OnHitSave = Ability.Strength, OnHitDc = 13 } },
                Loot = Loot(0, 0, "a great grey pelt"), Behavior = B(CreatureBehavior.Pack, "Leads the pack onto one quarry.", 0.25),
            },
            new CreatureDefinition
            {
                Id = "giant_rat", Name = "Giant Rat", Description = "A rat the size of a dog, bold in the dark.", Type = "Beast", Category = CreatureCategory.CommonThreat, Size = CreatureSize.Small,
                Source = ContentSource.Srd51, Provenance = Srd51Block, ChallengeRating = "1/8", ArmorClass = 12, HitDice = new Dice(2, 6), MaxHp = 7, Speed = 30, Scores = new[] { 7, 15, 11, 2, 10, 4 },
                Darkvision = 60, Senses = "darkvision 60 ft, keen smell", PackTactics = true, Habitats = new[] { Habitat.Settlement, Habitat.Ruins, Habitat.Farmland },
                Diet = Diet.Scavenger, Social = SocialForm.Pack, Temperament = "bold in numbers",
                Attacks = new[] { Melee("bite", "Bite", 4, new Dice(1, 4, 2), DamageType.Piercing) }, Behavior = B(CreatureBehavior.Scavenging, "Swarms the weak; scatters when the pack thins.", 0.5),
            },
            new CreatureDefinition
            {
                Id = "giant_spider", Name = "Giant Spider", Description = "A spider as large as a cart, its web strung between old trees.", Type = "Beast", Category = CreatureCategory.Dangerous, Size = CreatureSize.Large,
                Source = ContentSource.Srd51, Provenance = Srd51Block, ChallengeRating = "1", ArmorClass = 14, ArmorNote = "natural", HitDice = new Dice(4, 10, 4), MaxHp = 26, Speed = 30, ClimbSpeed = 30,
                Scores = new[] { 14, 16, 12, 2, 11, 4 }, Skills = "Stealth +7", Darkvision = 60, Senses = "blindsight 10 ft, darkvision 60 ft",
                Habitats = new[] { Habitat.Forest, Habitat.Ruins }, Diet = Diet.Carnivore, Social = SocialForm.Solitary, Temperament = "patient ambusher",
                Traits = new[] { T(CreatureTraitKind.Descriptive, "Spider Climb, Web Sense, Web Walker", "Climbs anything and feels its web (not simulated: no terrain model).") },
                Attacks = new[]
                {
                    new CreatureAttack { Id = "bite", Name = "Bite", AttackBonus = 5, Damage = new Dice(1, 8, 3), DamageType = DamageType.Piercing, Reach = 5,
                        SaveDamage = new Dice(2, 8), SaveDamageType = DamageType.Poison, SaveDamageAbility = Ability.Constitution, SaveDamageDc = 11 },
                    new CreatureAttack { Id = "web", Name = "Web", AttackBonus = 5, Damage = new Dice(0, 0), DamageType = DamageType.Bludgeoning, Range = 30, LongRange = 60,
                        OnHit = ConditionType.Restrained, EscapeDc = 12, Recharge = 5 },
                },
                Behavior = B(CreatureBehavior.Predatory, "Webs its prey from a distance, then closes in on the helpless.", 0.3),
            },

            // ===== Livestock and working animals ===========================================================
            new CreatureDefinition
            {
                Id = "cattle", Name = "Cattle", Description = "A broad-backed dun cow of the common byre.", Type = "Beast", Category = CreatureCategory.Livestock, Size = CreatureSize.Large,
                Source = ContentSource.RealmkeeperOriginal, Provenance = Original, ChallengeRating = "1/4", ArmorClass = 10, HitDice = new Dice(2, 10, 4), MaxHp = 15, Speed = 30,
                Scores = new[] { 18, 8, 14, 2, 9, 4 }, Habitats = new[] { Habitat.Farmland, Habitat.Grassland }, Diet = Diet.Herbivore, Social = SocialForm.Herd, Temperament = "placid",
                Attacks = new[] { Melee("gore", "Gore", 6, new Dice(1, 6, 4), DamageType.Piercing) }, Loot = Loot(0, 0, "beef and a hide"),
                Behavior = B(CreatureBehavior.Passive, "Moves away from trouble; butts only when cornered.", 1),
            },
            new CreatureDefinition
            {
                Id = "sheep", Name = "Sheep", Description = "A shaggy hill sheep.", Type = "Beast", Category = CreatureCategory.Livestock, Source = ContentSource.RealmkeeperOriginal, Provenance = Original,
                ChallengeRating = "0", ArmorClass = 10, HitDice = new Dice(1, 8, 1), MaxHp = 5, Speed = 30, Scores = new[] { 10, 10, 12, 2, 9, 5 },
                Habitats = new[] { Habitat.Farmland, Habitat.Hills, Habitat.Grassland }, Diet = Diet.Herbivore, Social = SocialForm.Flock, Temperament = "flighty",
                Attacks = new[] { Melee("ram", "Ram", 2, new Dice(1, 4), DamageType.Bludgeoning) }, Loot = Loot(0, 0, "mutton and a fleece"),
                Behavior = B(CreatureBehavior.Passive, "Bunches with the flock and runs.", 1),
            },
            new CreatureDefinition
            {
                Id = "goat", Name = "Goat", Description = "A nanny goat with a wicked eye.", Type = "Beast", Category = CreatureCategory.Livestock, Source = ContentSource.Srd51, Provenance = Srd51Block,
                ChallengeRating = "0", ArmorClass = 10, HitDice = new Dice(1, 8), MaxHp = 4, Speed = 40, Scores = new[] { 12, 10, 11, 2, 10, 5 },
                Habitats = new[] { Habitat.Farmland, Habitat.Hills, Habitat.Mountains }, Diet = Diet.Herbivore, Social = SocialForm.Herd, Temperament = "contrary",
                Attacks = new[] { Melee("ram", "Ram", 3, new Dice(1, 4, 1), DamageType.Bludgeoning) },
                Traits = new[] { T(CreatureTraitKind.Charge, "Charge", "After running 20 ft or more at a target, a ram hit adds 1d4 bludgeoning; DC 10 Strength save or prone.", 1, 4, 10) },
                Loot = Loot(0, 0, "goat meat and a hide"), Behavior = B(CreatureBehavior.Passive, "Keeps its distance; rams what corners it.", 1),
            },
            new CreatureDefinition
            {
                Id = "riding_horse", Name = "Riding Horse", Description = "A sound saddle horse.", Type = "Beast", Category = CreatureCategory.Livestock, Size = CreatureSize.Large,
                Source = ContentSource.Srd51, Provenance = Srd51Block, ChallengeRating = "1/4", ArmorClass = 10, HitDice = new Dice(2, 10, 2), MaxHp = 13, Speed = 60, Scores = new[] { 16, 10, 12, 2, 11, 7 },
                Habitats = new[] { Habitat.Farmland, Habitat.Grassland, Habitat.Road }, Diet = Diet.Herbivore, Social = SocialForm.Herd, Temperament = "nervous",
                Attacks = new[] { Melee("hooves", "Hooves", 5, new Dice(2, 4, 3), DamageType.Bludgeoning) }, Behavior = B(CreatureBehavior.Passive, "Shies and bolts; kicks when hemmed in.", 1),
            },
            new CreatureDefinition
            {
                Id = "draft_horse", Name = "Draft Horse", Description = "A heavy plough horse with feathered hooves.", Type = "Beast", Category = CreatureCategory.Livestock, Size = CreatureSize.Large,
                Source = ContentSource.Srd51, Provenance = Srd51Block, ChallengeRating = "1/4", ArmorClass = 10, HitDice = new Dice(3, 10, 3), MaxHp = 19, Speed = 40, Scores = new[] { 18, 10, 12, 2, 11, 7 },
                Habitats = new[] { Habitat.Farmland }, Diet = Diet.Herbivore, Social = SocialForm.Herd, Temperament = "steady",
                Attacks = new[] { Melee("hooves", "Hooves", 6, new Dice(2, 4, 4), DamageType.Bludgeoning) }, Behavior = B(CreatureBehavior.Passive, "Plods away from trouble.", 1),
            },
            new CreatureDefinition
            {
                Id = "chicken", Name = "Chicken", Description = "A speckled hen, loud about everything.", Type = "Beast", Category = CreatureCategory.Livestock, Size = CreatureSize.Tiny,
                Source = ContentSource.RealmkeeperOriginal, Provenance = Original, ChallengeRating = "0", ArmorClass = 11, HitDice = new Dice(1, 4), MaxHp = 1, Speed = 20, Scores = new[] { 2, 12, 9, 1, 10, 3 },
                Habitats = new[] { Habitat.Farmland, Habitat.Settlement }, Diet = Diet.Omnivore, Social = SocialForm.Flock, Temperament = "excitable",
                Attacks = new[] { Melee("peck", "Peck", 2, new Dice(0, 0, 1), DamageType.Piercing) }, Behavior = B(CreatureBehavior.Skittish, "Flaps away squawking.", 1),
            },
            new CreatureDefinition
            {
                Id = "mastiff", Name = "Mastiff", Description = "A heavy farm dog that minds the yard.", Type = "Beast", Category = CreatureCategory.Livestock, Source = ContentSource.Srd51, Provenance = Srd51Block,
                ChallengeRating = "1/8", ArmorClass = 12, HitDice = new Dice(1, 8, 1), MaxHp = 5, Speed = 40, Scores = new[] { 13, 14, 12, 3, 12, 7 }, Skills = "Perception +3", Senses = "keen hearing and smell",
                Habitats = new[] { Habitat.Farmland, Habitat.Settlement }, Diet = Diet.Carnivore, Social = SocialForm.Pair, Temperament = "loyal",
                Attacks = new[] { new CreatureAttack { Id = "bite", Name = "Bite", AttackBonus = 3, Damage = new Dice(1, 6, 1), DamageType = DamageType.Piercing, Reach = 5, OnHit = ConditionType.Prone, OnHitSave = Ability.Strength, OnHitDc = 11 } },
                Behavior = B(CreatureBehavior.Guarding, "Stays by its people and holds off what threatens them.", 0.2),
            },

            // ===== Common threats ============================================================================
            new CreatureDefinition
            {
                Id = "goblin", Name = "Goblin", Description = "A small, sharp-eared raider who fights dirty and runs early.", Category = CreatureCategory.CommonThreat, Size = CreatureSize.Small,
                Source = ContentSource.Srd51, Provenance = Srd51Block, ChallengeRating = "1/4", ArmorClass = 15, ArmorNote = "leather armour, shield", HitDice = new Dice(2, 6), MaxHp = 7, Speed = 30,
                Scores = new[] { 8, 14, 10, 10, 8, 8 }, Skills = "Stealth +6", Darkvision = 60, Senses = "darkvision 60 ft", Languages = "Common, Goblin",
                Habitats = new[] { Habitat.Forest, Habitat.Hills, Habitat.Ruins }, Social = SocialForm.Band, Temperament = "cowardly, cruel",
                Traits = new[] { T(CreatureTraitKind.NimbleEscape, "Nimble Escape", "Disengages (or hides) as a bonus action.") },
                Attacks = new[] { Melee("scimitar", "Scimitar", 4, new Dice(1, 6, 2), DamageType.Slashing), Ranged("shortbow", "Shortbow", 4, new Dice(1, 6, 2), DamageType.Piercing, 80, 320) },
                Loot = Loot(0, 6, null, L("scimitar", 20)), Behavior = B(CreatureBehavior.Scavenging, "Shoots from range, gangs up on the hurt, slips away when losing.", 0.5),
            },
            new CreatureDefinition
            {
                Id = "highwayman", Name = "Highwayman", Description = "A hardened road thief who leads by fear.", Category = CreatureCategory.CommonThreat, Source = ContentSource.Srd51,
                Provenance = "SRD 5.1 Thug stat block (CC BY 4.0), named for the role it plays on Realmkeeper's roads", ChallengeRating = "1/2", ArmorClass = 11, ArmorNote = "leather armour",
                HitDice = new Dice(5, 8, 10), MaxHp = 32, Speed = 30, Scores = new[] { 15, 11, 14, 10, 10, 11 }, Skills = "Intimidation +2", Languages = "Common", PackTactics = true, Multiattack = 2,
                Habitats = new[] { Habitat.Road, Habitat.Forest }, Social = SocialForm.Band, Temperament = "ruthless",
                Attacks = new[] { Melee("mace", "Mace", 4, new Dice(1, 6, 2), DamageType.Bludgeoning), Ranged("heavy_crossbow", "Heavy crossbow", 2, new Dice(1, 10), DamageType.Piercing, 100, 400) },
                Loot = Loot(5, 25, null, L("dagger_iron", 30)), Behavior = B(CreatureBehavior.Intelligent, "Leads the ambush; gives up once the band is broken.", 0.3),
            },
            new CreatureDefinition
            {
                Id = "bandit_captain", Name = "Bandit Captain", Description = "A road captain in studded leather, quick with blade and tongue.", Category = CreatureCategory.CommonThreat,
                Source = ContentSource.Srd51, Provenance = Srd51Block, ChallengeRating = "2", ArmorClass = 15, ArmorNote = "studded leather", HitDice = new Dice(10, 8, 20), MaxHp = 65, Speed = 30,
                Scores = new[] { 15, 16, 14, 14, 11, 14 }, SaveProficiencies = new[] { Ability.Strength, Ability.Dexterity, Ability.Wisdom }, Skills = "Athletics +4, Deception +4", Languages = "Common", Multiattack = 3,
                Habitats = new[] { Habitat.Road, Habitat.Forest, Habitat.Hills }, Social = SocialForm.Band, Temperament = "calculating",
                Reactions = new[] { new CreatureReaction { Kind = CreatureReactionKind.Parry, Name = "Parry", Value = 2 } },
                Attacks = new[] { Melee("scimitar", "Scimitar", 5, new Dice(1, 6, 3), DamageType.Slashing), new CreatureAttack { Id = "dagger", Name = "Dagger", AttackBonus = 5, Damage = new Dice(1, 4, 3), DamageType = DamageType.Piercing, Reach = 5, Range = 20, LongRange = 60 } },
                Loot = Loot(20, 60, null, L("scimitar", 70), L("dagger_iron", 70)), Behavior = B(CreatureBehavior.Intelligent, "Parries, presses the weak, and withdraws to fight another day.", 0.35),
            },
            new CreatureDefinition
            {
                Id = "cultist", Name = "Cultist", Description = "A hooded believer with a curved blade.", Category = CreatureCategory.CommonThreat, Source = ContentSource.Srd51, Provenance = Srd51Block,
                ChallengeRating = "1/8", ArmorClass = 12, ArmorNote = "leather armour", HitDice = new Dice(2, 8), MaxHp = 9, Speed = 30, Scores = new[] { 11, 12, 10, 10, 11, 10 },
                Skills = "Deception +2, Religion +2", Languages = "Common", Habitats = new[] { Habitat.Ruins, Habitat.Settlement }, Social = SocialForm.Band, Temperament = "devoted",
                Traits = new[] { T(CreatureTraitKind.Descriptive, "Dark Devotion", "Advantage against being charmed or frightened (no fear effects are simulated yet).") },
                Attacks = new[] { Melee("scimitar", "Scimitar", 3, new Dice(1, 6, 1), DamageType.Slashing) }, Loot = Loot(1, 8, null, L("scimitar", 30)),
                Behavior = B(CreatureBehavior.Intelligent, "Stands with its fellows; does not run.", 0),
            },
            new CreatureDefinition
            {
                Id = "veteran", Name = "Veteran", Description = "An old soldier in splint who has seen every trick.", Source = ContentSource.Srd51, Provenance = Srd51Block,
                ChallengeRating = "3", ArmorClass = 17, ArmorNote = "splint", HitDice = new Dice(9, 8, 18), MaxHp = 58, Speed = 30, Scores = new[] { 16, 13, 14, 10, 11, 10 },
                Skills = "Athletics +5, Perception +2", Languages = "Common", Multiattack = 2, Habitats = new[] { Habitat.Settlement, Habitat.Road }, Social = SocialForm.Band, Temperament = "steady",
                Attacks = new[] { Melee("longsword", "Longsword", 5, new Dice(1, 8, 3), DamageType.Slashing), Ranged("heavy_crossbow", "Heavy crossbow", 3, new Dice(1, 10, 1), DamageType.Piercing, 100, 400) },
                Loot = Loot(10, 40, null, L("sword_arming", 80)), Behavior = B(CreatureBehavior.Intelligent, "Holds formation, guards the hurt, falls back in good order.", 0.25),
            },

            // ===== Undead ======================================================================================
            new CreatureDefinition
            {
                Id = "skeleton", Name = "Skeleton", Description = "Old bones bound to a last order, armour scraps rattling.", Type = "Undead", Category = CreatureCategory.Undead, Source = ContentSource.Srd51, Provenance = Srd51Block,
                ChallengeRating = "1/4", ArmorClass = 13, ArmorNote = "armour scraps", HitDice = new Dice(2, 8, 4), MaxHp = 13, Speed = 30, Scores = new[] { 10, 14, 15, 6, 8, 5 },
                Darkvision = 60, Senses = "darkvision 60 ft", Vulnerabilities = new[] { DamageType.Bludgeoning }, Immunities = new[] { DamageType.Poison },
                ConditionImmunities = new[] { ConditionType.Exhaustion, ConditionType.Poisoned }, Habitats = new[] { Habitat.Ruins }, Diet = Diet.None, Social = SocialForm.Band, Temperament = "relentless",
                Attacks = new[] { Melee("shortsword", "Shortsword", 4, new Dice(1, 6, 2), DamageType.Piercing), Ranged("shortbow", "Shortbow", 4, new Dice(1, 6, 2), DamageType.Piercing, 80, 320) },
                Loot = Loot(0, 3, null, L("shortsword", 30)), Behavior = B(CreatureBehavior.Mindless, "Advances on the nearest living thing and never flees.", 0),
            },
            new CreatureDefinition
            {
                Id = "zombie", Name = "Zombie", Description = "A shambling corpse that will not stay down.", Type = "Undead", Category = CreatureCategory.Undead, Source = ContentSource.Srd51, Provenance = Srd51Block,
                ChallengeRating = "1/4", ArmorClass = 8, HitDice = new Dice(3, 8, 9), MaxHp = 22, Speed = 20, Scores = new[] { 13, 6, 16, 3, 6, 5 }, SaveProficiencies = new[] { Ability.Wisdom },
                Darkvision = 60, Senses = "darkvision 60 ft", Immunities = new[] { DamageType.Poison }, ConditionImmunities = new[] { ConditionType.Poisoned },
                Habitats = new[] { Habitat.Ruins, Habitat.Wetland }, Diet = Diet.None, Social = SocialForm.Band, Temperament = "relentless",
                Traits = new[] { T(CreatureTraitKind.UndeadFortitude, "Undead Fortitude", "Damage that would drop it to 0 (not radiant, not a critical hit) allows a Constitution save, DC 5 + the damage, to stay at 1.") },
                Attacks = new[] { Melee("slam", "Slam", 3, new Dice(1, 6, 1), DamageType.Bludgeoning) }, Behavior = B(CreatureBehavior.Mindless, "Shambles at the nearest living thing; never flees.", 0),
            },

            // ===== Dangerous creatures =======================================================================
            new CreatureDefinition
            {
                Id = "ogre", Name = "Ogre", Description = "A hulking, dim brute with a tree-trunk club.", Type = "Giant", Category = CreatureCategory.Dangerous, Size = CreatureSize.Large,
                Source = ContentSource.Srd51, Provenance = Srd51Block, ChallengeRating = "2", ArmorClass = 11, ArmorNote = "hide armour", HitDice = new Dice(7, 10, 21), MaxHp = 59, Speed = 40,
                Scores = new[] { 19, 8, 16, 5, 7, 7 }, Darkvision = 60, Senses = "darkvision 60 ft", Languages = "Common, Giant", Habitats = new[] { Habitat.Hills, Habitat.Forest, Habitat.Mountains },
                Diet = Diet.Carnivore, Social = SocialForm.Solitary, Temperament = "greedy, quick to anger",
                Attacks = new[] { Melee("greatclub", "Greatclub", 6, new Dice(2, 8, 4), DamageType.Bludgeoning), Ranged("javelin", "Javelin", 6, new Dice(2, 6, 4), DamageType.Piercing, 30, 120) },
                Loot = Loot(5, 30, null, L("javelin", 50, 1, 3)), Behavior = B(CreatureBehavior.Predatory, "Smashes the nearest threat; lumbers off when badly hurt.", 0.2),
            },
            new CreatureDefinition
            {
                Id = "troll", Name = "Troll", Description = "A rangy green-grey giant whose wounds close as you watch.", Type = "Giant", Category = CreatureCategory.Dangerous, Size = CreatureSize.Large,
                Source = ContentSource.Srd51, Provenance = Srd51Block, ChallengeRating = "5", ProficiencyBonus = 3, ArmorClass = 15, ArmorNote = "natural", HitDice = new Dice(8, 10, 40), MaxHp = 84, Speed = 30,
                Scores = new[] { 18, 13, 20, 7, 9, 7 }, Skills = "Perception +2", Darkvision = 60, Senses = "darkvision 60 ft, keen smell", Languages = "Giant", Multiattack = 3,
                Habitats = new[] { Habitat.Forest, Habitat.Wetland, Habitat.Mountains }, Diet = Diet.Carnivore, Social = SocialForm.Solitary, Temperament = "ravenous",
                Traits = new[]
                {
                    T(CreatureTraitKind.Regeneration, "Regeneration", "Regains 10 hit points at the start of its turn unless acid or fire hurt it since its last turn; it dies only if it starts its turn at 0 and cannot regenerate.", 10, 0, 0, DamageType.Acid, DamageType.Fire),
                    T(CreatureTraitKind.Descriptive, "Keen Smell", "Hard to surprise."),
                },
                Attacks = new[] { Melee("claw", "Claw", 7, new Dice(2, 6, 4), DamageType.Slashing), Melee("bite", "Bite", 7, new Dice(1, 6, 4), DamageType.Piercing) },
                Loot = Loot(10, 50, null), Behavior = B(CreatureBehavior.Predatory, "Hunts the weakest; trusts its regeneration and rarely retreats.", 0.1),
            },
        };

        /// <summary>Problems with the catalog itself (duplicate IDs, impossible numbers, unsupported data). Run by the tests.</summary>
        public static List<string> Validate()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in All)
            {
                if (!ids.Add(c.Id)) errors.Add($"Creature '{c.Id}' is listed twice.");
                if (string.IsNullOrWhiteSpace(c.Description)) errors.Add($"{c.Id}: no description.");
                if (c.ArmorClass < 1 || c.MaxHp < 1 || c.Speed < 0) errors.Add($"{c.Id}: impossible statistics.");
                if (c.Scores == null || c.Scores.Length != 6) errors.Add($"{c.Id}: needs six ability scores.");
                if (ChallengeRatings.Xp(c.ChallengeRating) == 0 && c.ChallengeRating != "0") errors.Add($"{c.Id}: unknown challenge rating '{c.ChallengeRating}'.");
                if (string.IsNullOrWhiteSpace(c.Provenance)) errors.Add($"{c.Id}: no provenance.");
                if (c.Source == ContentSource.RealmkeeperOriginal && !c.Provenance.Contains("Realmkeeper")) errors.Add($"{c.Id}: an original must say so.");
                if (c.Multiattack < 1) errors.Add($"{c.Id}: multiattack below 1.");
                foreach (var a in c.Attacks)
                {
                    if (a.Reach <= 0 && a.Range <= 0) errors.Add($"{c.Id}: attack '{a.Id}' reaches nothing.");
                    if (a.OnHitSave.HasValue && a.OnHitDc < 1) errors.Add($"{c.Id}: attack '{a.Id}' has a save without a DC.");
                    if (a.SaveDamage.Count > 0 && a.SaveDamageDc < 1) errors.Add($"{c.Id}: attack '{a.Id}' has save damage without a DC.");
                    if (a.Recharge != 0 && (a.Recharge < 2 || a.Recharge > 6)) errors.Add($"{c.Id}: attack '{a.Id}' has an impossible recharge.");
                    if (a.EscapeDc > 0 && a.OnHit != ConditionType.Restrained && a.OnHit != ConditionType.Grappled) errors.Add($"{c.Id}: attack '{a.Id}' has an escape DC but holds nothing.");
                }

                foreach (var t in c.Traits)
                {
                    if (t.Kind == CreatureTraitKind.Charge && (t.Value < 1 || t.DiceSides < 2)) errors.Add($"{c.Id}: Charge without dice.");
                    if (t.Kind == CreatureTraitKind.Regeneration && t.Value < 1) errors.Add($"{c.Id}: Regeneration of nothing.");
                    if (t.Kind == CreatureTraitKind.Relentless && t.Value < 1) errors.Add($"{c.Id}: Relentless without a limit.");
                }

                foreach (var l in c.Loot.Items) if (ItemCatalog.Find(l.ItemId) == null) errors.Add($"{c.Id}: loot item '{l.ItemId}' is unknown.");
                if (c.Loot.CoinMin < 0 || c.Loot.CoinMax < c.Loot.CoinMin) errors.Add($"{c.Id}: impossible coin range.");
                if (c.Habitats.Count == 0) errors.Add($"{c.Id}: lives nowhere.");
            }

            return errors;
        }
    }
}
