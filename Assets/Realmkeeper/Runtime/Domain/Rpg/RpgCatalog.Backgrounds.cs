using System.Collections.Generic;

namespace Realmkeeper.Domain.Rpg
{
    public static partial class RpgCatalog
    {
        private static KitItem K(string item, int quantity = 1, bool equip = false) => new KitItem(item, quantity, equip);

        // Acolyte, Criminal, Scholar (Sage) and Soldier follow SRD 5.2.1's proficiencies; the other eight are
        // Realmkeeper's own. All descriptions, lore, features and kits are Realmkeeper's own.
        private static IReadOnlyList<BackgroundDefinition> BuildBackgrounds() => new[]
        {
            new BackgroundDefinition
            {
                Id = "soldier", Name = "Soldier", Source = ContentSource.Srd521,
                Description = "You served in a company, a garrison or a lord's levy, and you know how armed people behave.",
                Lore = "Drilled, fed badly and paid late: the realm's soldiers learn discipline and dice in equal measure.",
                Skills = new[] { Skill.Athletics, Skill.Intimidation }, Tools = new[] { "dice_set" },
                Equipment = new[] { K("dice_set"), K("travel_rations", 2) }, Coin = 14,
                Feature = F("military_rank", "Military Rank", "Guards and soldiers recognise your bearing and speak to you as one of their own.", E(EffectKind.SocialFeature, "military-rank")),
                Suggestions = new[] { "I keep my gear in order; it kept me alive.", "I owe a dead comrade a promise." },
            },
            new BackgroundDefinition
            {
                Id = "noble", Name = "Noble", Source = ContentSource.RealmkeeperOriginal,
                Description = "You were born to a house with a name, lands or debts, and the manners that go with them.",
                Lore = "A noble name opens doors and paints a target. Whether yours is honoured or merely remembered depends on what your family did with it.",
                Skills = new[] { Skill.History, Skill.Persuasion }, Tools = new[] { "dice_set" }, Languages = new[] { "elvish" },
                Equipment = new[] { K("dice_set") }, Coin = 25,
                Feature = F("position_of_privilege", "Position of Privilege", "People of standing receive you; common folk assume you have authority.", E(EffectKind.SocialFeature, "position-of-privilege")),
                Suggestions = new[] { "I expect courtesy, and I give it.", "My family's honour is mine to restore." },
            },
            new BackgroundDefinition
            {
                Id = "criminal", Name = "Criminal", Source = ContentSource.Srd521,
                Description = "You made a living on the wrong side of the law: theft, smuggling, fencing or worse.",
                Lore = "Every town has an economy that never reaches the ledgers. You know its rules, its prices and who to avoid.",
                Skills = new[] { Skill.SleightOfHand, Skill.Stealth }, Tools = new[] { "thieves_tools" },
                Equipment = new[] { K("thieves_tools") }, Coin = 16,
                Feature = F("criminal_contact", "Criminal Contact", "You know how to send word through the underworld and find someone who will buy without questions.", E(EffectKind.SocialFeature, "criminal-contact")),
                Suggestions = new[] { "I always have a way out.", "I took the fall for someone once." },
            },
            new BackgroundDefinition
            {
                Id = "acolyte", Name = "Acolyte", Source = ContentSource.Srd521,
                Description = "You served at a shrine or temple, learning its rites and the needs of those who came to it.",
                Lore = "Shrines are the realm's hospitals, schools and archives as much as its houses of worship. An acolyte learns all three.",
                Skills = new[] { Skill.Insight, Skill.Religion }, Tools = new[] { "calligraphers_supplies" }, Languages = new[] { "celestial" },
                Equipment = new[] { K("holy_symbol"), K("calligraphers_supplies") }, Coin = 8,
                Feature = F("shelter_of_the_faithful", "Shelter of the Faithful", "Shrines and their keepers give you and your companions a meal and a place to sleep.", E(EffectKind.SocialFeature, "shelter-of-the-faithful")),
                Suggestions = new[] { "I see a lesson in everything.", "I left my order with a question it could not answer." },
            },
            new BackgroundDefinition
            {
                Id = "artisan", Name = "Artisan", Source = ContentSource.RealmkeeperOriginal,
                Description = "You learned a craft at a master's bench and can judge good work at a glance.",
                Lore = "Smiths, joiners and weavers hold the realm together. A guild mark on your hand is worth more than a letter of introduction in any market town.",
                Skills = new[] { Skill.Investigation, Skill.Persuasion }, Tools = new[] { "smiths_tools" },
                Equipment = new[] { K("smiths_tools") }, Coin = 15,
                Feature = F("guild_standing", "Guild Standing", "Craftspeople treat you as a peer and will share what they know of their town.", E(EffectKind.SocialFeature, "guild-standing")),
                Suggestions = new[] { "Bad work offends me.", "I am saving for my own workshop." },
            },
            new BackgroundDefinition
            {
                Id = "merchant", Name = "Merchant", Source = ContentSource.RealmkeeperOriginal,
                Description = "You bought and sold along the roads and know what things are worth and what people will pay.",
                Lore = "Trade is the realm's bloodstream. A merchant learns prices before names, and names before faces.",
                Skills = new[] { Skill.Insight, Skill.Persuasion }, Tools = new[] { "navigators_tools" }, Languages = new[] { "dwarvish" },
                Equipment = new[] { K("travel_rations", 2) }, Coin = 30,
                Feature = F("trade_connections", "Trade Connections", "You bargain like one of the trade: you pay 5% less in shops and are offered 5% more when you sell.",
                    E(EffectKind.ShopPricePercent, value: -5)),
                Suggestions = new[] { "Everything has a price, including me.", "A deal is a deal." },
            },
            new BackgroundDefinition
            {
                Id = "outlander", Name = "Outlander", Source = ContentSource.RealmkeeperOriginal,
                Description = "You grew up far from the towns, among hills, forests or moors, and you know how to live off them.",
                Lore = "Beyond the roads the realm is wide and empty, and the people who live there trust land more than law.",
                Skills = new[] { Skill.Athletics, Skill.Survival }, Tools = new[] { "herbalism_kit" }, Languages = new[] { "sylvan" },
                Equipment = new[] { K("travel_rations", 5) }, Coin = 8,
                Feature = F("wanderer", "Wanderer", "You never lose your way in the wild and can find food and water for yourself and a few others.", E(EffectKind.SocialFeature, "wanderer")),
                Suggestions = new[] { "Towns make me restless.", "I am the last of my band." },
            },
            new BackgroundDefinition
            {
                Id = "scholar", Name = "Scholar", Source = ContentSource.Srd521,
                Description = "You spent years among books and teachers, and you know how to find things out.",
                Lore = "Knowledge in the realm is scattered across archives, shrines and private collections. A scholar learns the catalogues before the contents.",
                Skills = new[] { Skill.Arcana, Skill.History }, Tools = new[] { "calligraphers_supplies" }, Languages = new[] { "draconic" },
                Equipment = new[] { K("calligraphers_supplies") }, Coin = 10,
                Feature = F("researcher", "Researcher", "When you do not know something, you usually know where and from whom to learn it.", E(EffectKind.SocialFeature, "researcher")),
                Suggestions = new[] { "I cannot leave a question alone.", "My teacher's last work was stolen." },
            },
            new BackgroundDefinition
            {
                Id = "sailor", Name = "Sailor", Source = ContentSource.RealmkeeperOriginal,
                Description = "You crewed river barges or coastal ships and learned ropes, weather and a sailor's superstitions.",
                Lore = "The realm's rivers carry more trade than its roads. Sailors know every port and every rumour that travels between them.",
                Skills = new[] { Skill.Athletics, Skill.Perception }, Tools = new[] { "navigators_tools" },
                Equipment = new[] { K("navigators_tools") }, Coin = 10,
                Feature = F("ships_passage", "Ship's Passage", "You can find working passage on river and coastal craft for yourself and companions.", E(EffectKind.SocialFeature, "ships-passage")),
                Suggestions = new[] { "I can read the weather in a sky.", "I jumped ship and someone is still owed." },
            },
            new BackgroundDefinition
            {
                Id = "entertainer", Name = "Entertainer", Source = ContentSource.RealmkeeperOriginal,
                Description = "You performed for your supper: music, tales, tumbling or masks.",
                Lore = "A good performer is welcome in any tavern in the realm, and hears more gossip in a night than a magistrate in a month.",
                Skills = new[] { Skill.Acrobatics, Skill.Performance }, Tools = new[] { "lute", "disguise_kit" },
                Equipment = new[] { K("lute") }, Coin = 15,
                Feature = F("by_popular_demand", "By Popular Demand", "Taverns give you a bed and a meal in return for performing.", E(EffectKind.SocialFeature, "by-popular-demand")),
                Suggestions = new[] { "I live for an audience.", "My best song is about someone I wronged." },
            },
            new BackgroundDefinition
            {
                Id = "hermit", Name = "Hermit", Source = ContentSource.RealmkeeperOriginal,
                Description = "You lived apart, in a cell, a cave or a wood, for reasons of faith, grief or study.",
                Lore = "Solitude teaches patience and herb lore, and sometimes something stranger that the hermit carries back into the world.",
                Skills = new[] { Skill.Medicine, Skill.Religion }, Tools = new[] { "herbalism_kit" }, Languages = new[] { "primordial" },
                Equipment = new[] { K("herbalism_kit") }, Coin = 5,
                Feature = F("discovery", "Discovery", "In your solitude you uncovered a truth, place or secret (to be revealed by a later story).", E(EffectKind.SocialFeature, "discovery")),
                Suggestions = new[] { "I am at ease with silence.", "I left my solitude because of a vision." },
            },
            new BackgroundDefinition
            {
                Id = "folk_hero", Name = "Folk Hero", Source = ContentSource.RealmkeeperOriginal,
                Description = "Ordinary folk once looked to you in a crisis, and you did not let them down.",
                Lore = "Every village has a story about the day someone stood up. In yours, that someone was you.",
                Skills = new[] { Skill.AnimalHandling, Skill.Survival }, Tools = new[] { "carpenters_tools" },
                Equipment = new[] { K("carpenters_tools") }, Coin = 10,
                Feature = F("rustic_hospitality", "Rustic Hospitality", "Common folk will shelter you and hide you from those who seek you.", E(EffectKind.SocialFeature, "rustic-hospitality")),
                Suggestions = new[] { "I judge people by their actions.", "I fear I cannot live up to the story." },
            },
        };

        private static IReadOnlyList<ToolDefinition> BuildTools() => new[]
        {
            new ToolDefinition("thieves_tools", "Thieves' tools", "thieves_tools"),
            new ToolDefinition("smiths_tools", "Smith's tools", "smiths_tools"),
            new ToolDefinition("carpenters_tools", "Carpenter's tools", "carpenters_tools"),
            new ToolDefinition("tinkers_tools", "Tinker's tools", null),
            new ToolDefinition("herbalism_kit", "Herbalism kit", "herbalism_kit"),
            new ToolDefinition("navigators_tools", "Navigator's tools", "navigators_tools"),
            new ToolDefinition("calligraphers_supplies", "Calligrapher's supplies", "calligraphers_supplies"),
            new ToolDefinition("disguise_kit", "Disguise kit", null),
            new ToolDefinition("lute", "Lute", "lute"),
            new ToolDefinition("dice_set", "Dice set", "dice_set"),
        };

        private static IReadOnlyList<LanguageDefinition> BuildLanguages() => new[]
        {
            new LanguageDefinition("common", "Common", "The trade tongue of the realm."),
            new LanguageDefinition("elvish", "Elvish", "Fluid and old; the language of elven scholarship."),
            new LanguageDefinition("dwarvish", "Dwarvish", "Hard consonants; carved in stone as often as spoken."),
            new LanguageDefinition("halfling", "Halfling", "Spoken at home, rarely written."),
            new LanguageDefinition("gnomish", "Gnomish", "Precise, full of technical words."),
            new LanguageDefinition("orc", "Orc", "Spoken across the high moors' clans."),
            new LanguageDefinition("giant", "Giant", "The tongue of the mountain peoples."),
            new LanguageDefinition("draconic", "Draconic", "Ancient; used in old learning and by dragonborn."),
            new LanguageDefinition("infernal", "Infernal", "Precise and contractual."),
            new LanguageDefinition("celestial", "Celestial", "Used in liturgy and in old shrine texts."),
            new LanguageDefinition("sylvan", "Sylvan", "The speech of the deep woods."),
            new LanguageDefinition("primordial", "Primordial", "Elemental and raw; heard in remote places."),
            new LanguageDefinition("druidic", "Druidic", "A secret language of druids."),
            new LanguageDefinition("thieves_cant", "Thieves' Cant", "Slang and signs among the underworld."),
        };
    }
}
