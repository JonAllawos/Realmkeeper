using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;

namespace Realmkeeper.Domain.Combat
{
    /// <summary>The broad group a creature belongs to (Phase 10 bestiary; drives encounters and the inspector).</summary>
    public enum CreatureCategory
    {
        Humanoid = 0,
        Wildlife = 1,
        Livestock = 2,
        CommonThreat = 3,
        Dangerous = 4,
        Undead = 5,
    }

    /// <summary>
    /// How a creature behaves in a fight and in the world (Phase 10, ADR-0044). Read by the combat AI; deterministic
    /// like everything else it decides.
    /// </summary>
    public enum CreatureBehavior
    {
        /// <summary>Thinks: closes in, picks off the weakest, falls back when badly hurt (the Phase 8 baseline).</summary>
        Intelligent = 0,

        /// <summary>Runs from any threat; fights only when cornered (deer, rabbits, foxes).</summary>
        Skittish = 1,

        /// <summary>Kept animals: move away from trouble, kick or butt only when cornered (sheep, cattle, horses).</summary>
        Passive = 2,

        /// <summary>Holds ground near its home and attacks whatever comes close; does not chase far (bears, boars).</summary>
        Territorial = 3,

        /// <summary>Hunts with its pack: everyone on one quarry, flanking; the pack breaks when half of it falls (wolves).</summary>
        Pack = 4,

        /// <summary>A lone hunter that goes for the weakest and gives up a losing fight (spiders, trolls).</summary>
        Predatory = 5,

        /// <summary>Attacks the nearest living thing and never flees (skeletons, zombies).</summary>
        Mindless = 6,

        /// <summary>Stays by what it guards and strikes what threatens it (guard dogs, watchmen).</summary>
        Guarding = 7,

        /// <summary>Picks at the weak and the fallen; scatters when the fight turns (giant rats, goblins).</summary>
        Scavenging = 8,
    }

    public enum Habitat
    {
        Forest = 0,
        Grassland = 1,
        Hills = 2,
        Mountains = 3,
        Wetland = 4,
        Farmland = 5,
        Road = 6,
        Settlement = 7,
        Ruins = 8,
        Riverbank = 9,
    }

    public enum Diet
    {
        None = 0,
        Herbivore = 1,
        Carnivore = 2,
        Omnivore = 3,
        Scavenger = 4,
    }

    public enum SocialForm
    {
        Solitary = 0,
        Pair = 1,
        Herd = 2,
        Pack = 3,
        Band = 4,
        Flock = 5,
    }

    /// <summary>Typed special traits the engine resolves (no creature-specific code elsewhere).</summary>
    public enum CreatureTraitKind
    {
        /// <summary>After moving 20 ft or more toward a target this turn, a hit adds dice and may knock it prone (boar, goat).</summary>
        Charge = 0,

        /// <summary>Regains hit points at the start of its turn unless fire or acid hurt it since its last turn; at 0 it only dies if it cannot regenerate (troll).</summary>
        Regeneration = 1,

        /// <summary>Damage that would drop it to 0 (not radiant, not a critical) allows a Constitution save (DC 5 + damage) to stay at 1 (zombie).</summary>
        UndeadFortitude = 2,

        /// <summary>Once per fight, damage of the value or less that would drop it to 0 leaves it at 1 (boar).</summary>
        Relentless = 3,

        /// <summary>It can Disengage as a bonus action (goblin).</summary>
        NimbleEscape = 4,

        /// <summary>Senses and climbing (keen smell, spider climb, web sense): shown, not simulated (no sight or terrain model).</summary>
        Descriptive = 5,
    }

    public sealed class CreatureTrait
    {
        public CreatureTraitKind Kind { get; set; }
        public string Name { get; set; }
        public string Text { get; set; }

        /// <summary>Kind-specific: Charge's extra dice count (with <see cref="DiceSides"/>), Regeneration's hit points, Relentless's damage limit.</summary>
        public int Value { get; set; }
        public int DiceSides { get; set; }

        /// <summary>Charge: the Strength save DC against being knocked prone (0 = none).</summary>
        public int SaveDc { get; set; }

        /// <summary>Regeneration: damage types that stop it.</summary>
        public IReadOnlyList<World.DamageType> StoppedBy { get; set; } = Array.Empty<World.DamageType>();
    }

    /// <summary>A creature's reaction (Phase 10, offered through the reaction system).</summary>
    public enum CreatureReactionKind
    {
        /// <summary>+Value AC against one melee hit it can see, while holding a melee weapon (bandit captain).</summary>
        Parry = 0,
    }

    public sealed class CreatureReaction
    {
        public CreatureReactionKind Kind { get; set; }
        public string Name { get; set; }
        public int Value { get; set; }
    }

    /// <summary>One line of what a creature leaves (item, how many, how likely). Coin is separate.</summary>
    public sealed class LootEntry
    {
        public string ItemId { get; set; }
        public int Min { get; set; } = 1;
        public int Max { get; set; } = 1;
        public int ChancePercent { get; set; } = 100;
    }

    public sealed class LootProfile
    {
        public int CoinMin { get; set; }
        public int CoinMax { get; set; }
        public IReadOnlyList<LootEntry> Items { get; set; } = Array.Empty<LootEntry>();

        /// <summary>Hide, meat and the like a hunter takes (shown; turned into goods when butchering exists).</summary>
        public string Remains { get; set; }

        public static readonly LootProfile None = new LootProfile();
    }

    /// <summary>How the AI weighs its options for a behaviour (data, tunable per creature).</summary>
    public sealed class BehaviorProfile
    {
        public CreatureBehavior Kind { get; set; }

        /// <summary>Tries to get away below this share of its hit points (0 = never).</summary>
        public double FleeBelow { get; set; } = 0.25;

        /// <summary>Territorial: how far from its home square it will fight, in feet.</summary>
        public int TerritoryFeet { get; set; } = 30;

        public string Summary { get; set; }
    }

    /// <summary>Experience by challenge rating (SRD 5.2.1 / 5.1 tables; CR 0 with no damaging attack gives 0).</summary>
    public static class ChallengeRatings
    {
        public static int Xp(string cr)
        {
            switch (cr)
            {
                case "0": return 10;
                case "1/8": return 25;
                case "1/4": return 50;
                case "1/2": return 100;
                case "1": return 200;
                case "2": return 450;
                case "3": return 700;
                case "4": return 1100;
                case "5": return 1800;
                case "6": return 2300;
                default: return 0;
            }
        }

        /// <summary>The rating as a number (for budgets).</summary>
        public static double Value(string cr)
        {
            if (string.IsNullOrEmpty(cr)) return 0;
            if (cr.Contains("/"))
            {
                var parts = cr.Split('/');
                return double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture) / double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
            }

            return double.Parse(cr, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
