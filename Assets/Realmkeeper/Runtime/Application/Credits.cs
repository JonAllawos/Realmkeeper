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
            ("KayKit Character Pack: Skeletons 1.1 (free version)", "Kay Lousberg (kaylousberg.com)", "CC0 1.0 Universal"),
            ("KayKit Character Animations 1.1 (free version)", "Kay Lousberg (kaylousberg.com)", "CC0 1.0 Universal"),
            // Phase 18
            ("KayKit Dungeon Remastered 1.0: dungeon pieces and props", "Kay Lousberg (kaylousberg.com)", "CC0 1.0 Universal"),
            ("Post Lantern (KayKit Halloween Bits)", "Kay Lousberg (kaylousberg.com)", "CC0 1.0 Universal"),
            ("Spider, Rat, Deer, Stag, Fox, Goblin, Giant, Orc, Bull, Chicken; Wolf, Husky, Horse, Cow (Ultimate Animated Animal Pack): creatures", "Quaternius (quaternius.com)", "CC0 1.0 Universal"),
            ("Farm Animals Pack: Sheep", "Quaternius (quaternius.com)", "CC0 1.0 Universal"),
            ("Well, Market Stalls, Market Stand, Street Light: settlement props", "Quaternius (quaternius.com)", "CC0 1.0 Universal"),
            ("Fantasy Props MegaKit (free Standard version): props", "Quaternius (quaternius.com)", "CC0 1.0 Universal"),
            ("Stylized Nature MegaKit (free Standard version): trees, plants and rocks", "Quaternius (quaternius.com)", "CC0 1.0 Universal"),
            ("\"Black bear\", via Poly Pizza (poly.pizza/m/56ym_pyVnel); converted to FBX and rescaled", "Poly by Google", "CC BY 3.0 (creativecommons.org/licenses/by/3.0)"),
            ("\"Boar\", via Poly Pizza (poly.pizza/m/57fSWum6F1P); converted to FBX and rescaled", "Poly by Google", "CC BY 3.0 (creativecommons.org/licenses/by/3.0)"),
            ("\"Goat\", via Poly Pizza (poly.pizza/m/d7dImmjtF8E); converted to FBX and rescaled", "Poly by Google", "CC BY 3.0 (creativecommons.org/licenses/by/3.0)"),
            // Phase 19: the interface
            ("Interface icons (142), available on game-icons.net; rendered white and tinted", "Icons made by Lorc, Delapouite, sbed, Skoll, Guard13007, Carl Olsen, Willdabeast, DarkZaitzev, Faithtoken, Lucas and Zeromancer", "CC BY 3.0 (creativecommons.org/licenses/by/3.0)"),
            ("Fantasy UI Borders: window frames and dividers", "Kenney (kenney.nl)", "CC0 1.0 Universal"),
            ("Cartography Pack: map symbols, compass rose and parchment", "Kenney (kenney.nl)", "CC0 1.0 Universal"),
            ("EB Garamond (headings and dialogue)", "The EB Garamond Project Authors (github.com/octaviopardo/EBGaramond12)", "SIL Open Font License 1.1 (openfontlicense.org)"),
            ("Alegreya Sans (text)", "The Alegreya Sans Project Authors (github.com/huertatipografica/Alegreya-Sans)", "SIL Open Font License 1.1 (openfontlicense.org)"),
        };
    }
}
