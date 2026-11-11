using System.Collections.Generic;

namespace Realmkeeper.Application
{
    /// <summary>
    /// The credits and attributions shipped with the game (Phase 8): shown in the Player Hub's Info section and kept
    /// identical to CREDITS.md at the repository root. The SRD notice is required by CC BY 4.0; the CC0 assets need no
    /// credit, and are credited anyway.
    /// </summary>
    public static class Credits
    {
        public const string SrdNotice =
            "This work includes material from the System Reference Document 5.2.1 (\"SRD 5.2.1\") by Wizards of the Coast LLC, " +
            "available at https://www.dndbeyond.com/srd. The SRD 5.2.1 is licensed under the Creative Commons Attribution 4.0 " +
            "International License, available at https://creativecommons.org/licenses/by/4.0/legalcode.";

        public const string Srd51Notice =
            "This work includes material from the System Reference Document 5.1 (\"SRD 5.1\") by Wizards of the Coast LLC, " +
            "available at https://www.dndbeyond.com/srd. The SRD 5.1 is licensed under the Creative Commons Attribution 4.0 " +
            "International License, available at https://creativecommons.org/licenses/by/4.0/legalcode.";

        public const string RulesUse =
            "Rules mechanics (species, classes, progression, armour, weapons, ammunition, tools, conditions, combat, spells and spellcasting, class " +
            "features, rests, carrying capacity, attunement, the SRD 5.2.1 magic items used (potions of healing, +1 weapons, armour and shield, ring and cloak of protection, " +
            "gauntlets of ogre power, goggles of night, boots of elvenkind, wand of magic missiles, Berserker Axe), " +
            "the bandit, guard, commoner and wolf statistics, and from SRD 5.1 the acolyte and cult fanatic and the " +
            "bestiary: deer, boar, black and brown bears, dire wolf, giant rat, giant spider, goat, riding and draft horses, mastiff, goblin, " +
            "thug (the game's highwayman), bandit captain, cultist, veteran, skeleton, zombie, ogre and troll, with their traits) follow the SRD; " +
            "the rabbit, fox, cattle, sheep and chicken are Realmkeeper originals, labelled as such; spell names are the SRD's, and every description, name and setting text is Realmkeeper's own. Realmkeeper does " +
            "not use the \"Dungeons & Dragons\" trademark and is not affiliated with or endorsed by Wizards of the Coast.";

        /// <summary>Third-party art: (what, by whom, licence).</summary>
        public static readonly IReadOnlyList<(string Work, string Author, string Licence)> Assets = new[]
        {
            ("Medieval Village MegaKit (free version): buildings and props", "Quaternius (quaternius.com)", "CC0 1.0 Universal"),
            ("Universal Base Characters (free version): bodies and heads", "Quaternius (quaternius.com)", "CC0 1.0 Universal"),
            ("Modular Character Outfits – Fantasy (free version): clothing and armour", "Quaternius (quaternius.com)", "CC0 1.0 Universal"),
            ("Universal Animation Library: character animation", "Quaternius (quaternius.com)", "CC0 1.0 Universal"),
            ("Grass004, Ground037, Ground054, PavingStones070, Planks021, Wood049: ground and wood materials", "ambientCG (ambientcg.com)", "CC0 1.0 Universal"),
        };
    }
}
