using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Phase 19: the one icon family (game-icons.net, CC BY 3.0; credits in Art/ThirdParty/GameIcons/License.txt).
    /// Icons are white PNGs loaded from Resources/RkIcons and tinted by USS (<c>.rk-icon--*</c>), so one texture
    /// serves every state colour. Screens ask for an icon by meaning ("food", "attack"), never by file.
    /// </summary>
    public static class RkIcons
    {
        public const string ResourceFolder = "RkIcons/";

        /// <summary>Meaning to game-icons.net file name. Generated from Tools/UI/icon-manifest.tsv.</summary>
        public static readonly IReadOnlyDictionary<string, string> Files = new Dictionary<string, string>
        {
            { "settings", "settings-knobs" },
            { "save", "save" },
            { "load", "scroll-unfurled" },
            { "close", "cancel" },
            { "info", "info" },
            { "help", "help" },
            { "search", "magnifying-glass" },
            { "lock", "padlock" },
            { "check", "check-mark" },
            { "warning", "hazard-sign" },
            { "play", "play-button" },
            { "pause", "pause-button" },
            { "fast", "fast-forward-button" },
            { "time", "hourglass" },
            { "calendar", "sundial" },
            { "exit", "exit-door" },
            { "enter", "entry-door" },
            { "world", "world" },
            { "newworld", "globe" },
            { "map", "treasure-map" },
            { "compass", "compass" },
            { "politics", "scales" },
            { "trade", "trade" },
            { "character", "person" },
            { "party", "three-friends" },
            { "spells", "spell-book" },
            { "appearance", "hood" },
            { "inventory", "knapsack" },
            { "equipment", "breastplate" },
            { "weapons", "crossed-swords" },
            { "clothing", "cloak" },
            { "crafting", "hammer-nails" },
            { "storage", "locked-chest" },
            { "journal", "quill-ink" },
            { "currency", "two-coins" },
            { "rest", "campfire" },
            { "people", "meeple" },
            { "population", "family-house" },
            { "prosperity", "coins-pile" },
            { "stability", "checked-shield" },
            { "food", "wheat" },
            { "production", "anvil-impact" },
            { "structures", "house" },
            { "faction", "rally-the-troops" },
            { "events", "papers" },
            { "settlement", "village" },
            { "castle", "castle" },
            { "place", "cave-entrance" },
            { "church", "church" },
            { "ruins", "castle-ruins" },
            { "mine", "mine-wagon" },
            { "graveyard", "graveyard" },
            { "terrain", "mountains" },
            { "forest", "forest" },
            { "road", "road" },
            { "river", "river" },
            { "threats", "skull-crossed-bones" },
            { "travel", "walking-boot" },
            { "caravan", "caravan" },
            { "patrol", "guards" },
            { "outlaws", "hood" },
            { "refugees", "three-friends" },
            { "attack", "broadsword" },
            { "action", "crossed-swords" },
            { "bonus", "star-swirl" },
            { "reaction", "shield-reflect" },
            { "move", "footprint" },
            { "dash", "sprint" },
            { "disengage", "dodging" },
            { "endturn", "hourglass" },
            { "hp", "health-normal" },
            { "heal", "heart-plus" },
            { "ac", "checked-shield" },
            { "ranged", "bow-arrow" },
            { "spell", "magic-swirl" },
            { "concentration", "meditation" },
            { "grid", "target-arrows" },
            { "dying", "death-skull" },
            { "withdraw", "exit-door" },
            { "target", "archery-target" },
            { "cover", "stone-wall" },
            { "sight", "eye-target" },
            { "dice", "dice-twenty-faces-twenty" },
            { "poisoned", "poison-bottle" },
            { "bleeding", "bleeding-wound" },
            { "exhausted", "tired-eye" },
            { "asleep", "sleepy" },
            { "blinded", "sight-disabled" },
            { "str", "biceps" },
            { "dex", "acrobatic" },
            { "con", "heart-tower" },
            { "int", "brain" },
            { "wis", "candle-light" },
            { "cha", "conversation" },
            { "talk", "talk" },
            { "persuasion", "shaking-hands" },
            { "insight", "eye-target" },
            { "intimidation", "angry-eyes" },
            { "decision", "scales" },
            { "quest", "flag-objective" },
            { "rumour", "brainstorm" },
            { "discovery", "magnifying-glass" },
            { "crime", "handcuffs" },
            { "reputation", "thumb-up" },
            { "disfavour", "thumb-down" },
            { "relationship", "hearts" },
            { "campaign", "book-cover" },
            { "weapon", "broadsword" },
            { "armor", "breastplate" },
            { "shield", "round-shield" },
            { "helmet", "visored-helm" },
            { "boots", "boots" },
            { "gloves", "gloves" },
            { "ring", "ring" },
            { "amulet", "gem-necklace" },
            { "potion", "potion-ball" },
            { "bread", "bread" },
            { "meat", "meat" },
            { "ore", "ore" },
            { "wood", "wood-pile" },
            { "herbs", "herbs-bundle" },
            { "stone", "stone-block" },
            { "gold", "gold-bar" },
            { "key", "key" },
            { "book", "open-book" },
            { "ammo", "arrow-cluster" },
            { "dagger", "plain-dagger" },
            { "axe", "battle-axe" },
            { "mace", "mace-head" },
            { "crossbow", "crossbow" },
            { "bow", "pocket-bow" },
            { "weight", "weight" },
            { "steal", "hood" },
            { "lockpick", "lockpicks" },
            { "open", "open-chest" },
            { "door", "door" },
            { "torch", "torch" },
            { "trap", "trap-mask" },
            { "mechanism", "gears" },
            { "sign", "wooden-sign" },
            { "sleep", "night-sleep" },
            { "hunger", "cooking-pot" },
            { "fire", "fire-ray" },
            { "cold", "ice-bolt" },
            { "lightning", "lightning-arc" },
            { "divine", "holy-symbol" },
            { "arcane", "pointy-hat" },
            { "wolf", "wolf-head" },
            { "spider", "spider-alt" },
            { "rat", "rat" },
            { "bear", "bear-head" },
            { "horse", "horse-head" },
            { "credits", "papers" },
            { "companion", "team-idea" },
            { "dm", "cog-lock" },
            // Phase 25: the bestiary, notifications, the pause menu, comparison and sound.
            { "bestiary", "paw-print" },
            { "undead", "skull-crossed-bones" },
            { "livestock", "cow" },
            { "wildlife", "deer-track" },
            { "humanoid", "person" },
            { "dangerous", "fanged-skull" },
            { "threat", "fangs-circle" },
            { "boar", "boar-tusks" },
            { "sheep", "sheep" },
            { "notify", "ringing-bell" },
            { "sound", "speaker" },
            { "compare", "scales" },
            { "menu", "hamburger-menu" },
            { "codex", "open-book" },
            { "goblin", "goblin-head" },
        };

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>True when the meaning has an icon and its texture loads.</summary>
        public static bool Has(string key) => Texture(key) != null;

        /// <summary>The icon's texture, or null for an unknown meaning (callers then show text only).</summary>
        public static Texture2D Texture(string key)
        {
            if (string.IsNullOrEmpty(key) || !Files.TryGetValue(key, out var file)) return null;
            if (Cache.TryGetValue(file, out var cached) && cached != null) return cached;
            var texture = Resources.Load<Texture2D>(ResourceFolder + file);
            Cache[file] = texture;
            return texture;
        }

        /// <summary>A tinted icon element. Size: "s", "m", "l", "xl" or null (18 px). Tone: "accent", "good", "warn", "bad", "info", "paper" or null.</summary>
        public static VisualElement Make(string key, string size = null, string tone = null)
        {
            var icon = new VisualElement { name = "icon-" + key, pickingMode = PickingMode.Ignore };
            icon.AddToClassList("rk-icon");
            if (!string.IsNullOrEmpty(size)) icon.AddToClassList("rk-icon--" + size);
            if (!string.IsNullOrEmpty(tone)) icon.AddToClassList("rk-icon--" + tone);
            var texture = Texture(key);
            if (texture != null) icon.style.backgroundImage = new StyleBackground(texture);
            else icon.style.display = DisplayStyle.None;
            return icon;
        }

        /// <summary>
        /// Puts an icon before a button's or label's text (once; a second call replaces it). The text stays, so the
        /// control still says what it does; the icon only helps scanning.
        /// </summary>
        public static T Lead<T>(T element, string key, string size = null, string tone = null) where T : TextElement
        {
            if (element == null) return null;
            var texture = Texture(key);
            if (texture == null) return element;
            // The icon is the element's own background image, placed in its left padding. A child element would stop
            // the text element from sizing itself to its text (a laid-out node with children has no text measure), and
            // text set later (Play / Pause) keeps working.
            var px = size == "s" ? 14 : size == "m" ? 22 : size == "l" ? 30 : 18;
            element.style.backgroundImage = new StyleBackground(texture);
            element.style.backgroundPositionX = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Left, new Length(9)));
            element.style.backgroundPositionY = new StyleBackgroundPosition(new BackgroundPosition(BackgroundPositionKeyword.Center));
            element.style.backgroundRepeat = new StyleBackgroundRepeat(new BackgroundRepeat(Repeat.NoRepeat, Repeat.NoRepeat));
            element.style.backgroundSize = new StyleBackgroundSize(new BackgroundSize(new Length(px), new Length(px)));
            element.style.unityBackgroundImageTintColor = Tone(tone);
            element.style.paddingLeft = 9 + px + 7;
            element.AddToClassList("rk-has-icon");
            LeadKeyTable.Remove(element);
            LeadKeyTable.Add(element, key);
            return element;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement, string> LeadKeyTable = new System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement, string>();

        /// <summary>The icon meaning a control was given with <see cref="Lead{T}"/>, or null (for tests and the audit).</summary>
        public static string LeadOf(VisualElement element) => element != null && LeadKeyTable.TryGetValue(element, out var k) ? k : null;

        /// <summary>The token colours, for icons drawn as a control's background (USS classes cannot reach them).</summary>
        public static Color Tone(string tone) => tone switch
        {
            "accent" => new Color(221 / 255f, 176 / 255f, 108 / 255f),
            "good" => new Color(140 / 255f, 182 / 255f, 112 / 255f),
            "warn" => new Color(222 / 255f, 163 / 255f, 70 / 255f),
            "bad" => new Color(226 / 255f, 122 / 255f, 100 / 255f),
            "info" => new Color(140 / 255f, 170 / 255f, 222 / 255f),
            "paper" => new Color(42 / 255f, 32 / 255f, 23 / 255f),
            "ink" => new Color(30 / 255f, 25 / 255f, 19 / 255f),
            _ => new Color(194 / 255f, 181 / 255f, 156 / 255f),
        };
    }
}
