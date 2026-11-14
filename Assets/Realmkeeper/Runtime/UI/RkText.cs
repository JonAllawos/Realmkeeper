using System;
using System.Collections.Generic;
using System.Globalization;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Phase 26: the interface's string table (translation readiness). Player-facing interface text is looked up by a
    /// stable key; the table below is the English text. A later translation replaces or overlays this table
    /// (<see cref="Overlay"/>) without touching the screens. Rules:
    /// <list type="bullet">
    /// <item>Keys are lower-case and dotted by screen (<c>trade.buy</c>, <c>codex.people</c>).</item>
    /// <item>Dynamic values go through <see cref="Format"/> with numbered placeholders, never string concatenation in the
    /// screen, so a translation can reorder them.</item>
    /// <item>Game data (item, creature and place names, quest text, dialogue lines) is <b>not</b> in this table: it is
    /// data, translated with its catalogues later. Internal ids, logs and save keys are never localised.</item>
    /// <item>An unknown key returns the key itself, so a missing string is visible instead of blank.</item>
    /// </list>
    /// </summary>
    public static class RkText
    {
        private static readonly Dictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ----- Common ----------------------------------------------------------------------------------
            ["common.close"] = "Close",
            ["common.close.esc"] = "Close (Esc)",
            ["common.cancel"] = "Cancel",
            ["common.coin"] = "{0} coin",
            ["common.search"] = "Search",
            ["common.all"] = "All ({0})",
            ["common.none"] = "None",
            ["common.unknown"] = "Unknown",
            ["common.nothingmatches"] = "Nothing matches.",

            // ----- Pause menu, settings, notices (Phase 25 components) --------------------------------------
            ["pause.title"] = "Paused",
            ["pause.clock"] = "{0} · the world's clock is stopped while this menu is open.",
            ["pause.resume"] = "Resume",
            ["pause.save"] = "Save (F5)",
            ["pause.settings"] = "Settings",
            ["pause.map"] = "Return to the world map (M)",
            ["pause.main"] = "Main menu",
            ["pause.exit"] = "Exit to desktop",
            ["pause.notinfight"] = "Not during a fight: finish it or withdraw first.",
            ["pause.nomainmenu"] = "There is no main menu in this scene.",
            ["pause.recent"] = "Recent notices",
            ["pause.norecent"] = "Nothing yet this session.",
            ["pause.mainconfirm.title"] = "Leave for the main menu?",
            ["pause.mainconfirm.body"] = "You will return to the world map, and the main menu opens over it. Progress since your last save is kept in the running world, but is only on disk once you save.",
            ["pause.exitconfirm.title"] = "Exit to desktop?",
            ["pause.exitconfirm.body"] = "Realmkeeper will close. Anything since your last save will be lost: save first if you want to keep it.",
            ["toast.more"] = "{0} more notices",
            ["toast.more.body"] = "See the recent notices in the menu (Esc).",
            ["toast.dismiss"] = "Click to dismiss.",

            // ----- Trade (Phase 26) ---------------------------------------------------------------------------
            ["trade.subtitle"] = "Kept by {0} · open {1} · {2}",
            ["trade.knows"] = "knows you ({0})",
            ["trade.stranger"] = "a stranger to you",
            ["trade.closed"] = "Not trading: ",
            ["trade.buy"] = "Buy",
            ["trade.sell"] = "Sell",
            ["trade.mend"] = "Mend",
            ["trade.notnow"] = "The shop is not trading now.",
            ["trade.short"] = "You are {0} coin short.",
            ["trade.nostock"] = "Nothing in stock.",
            ["trade.nothingtosell"] = "You carry nothing to sell.",
            ["trade.noparticular"] = "Nothing out of the ordinary.",
            ["trade.norepairs"] = "{0} has nothing of yours to mend (an armourer mends arms and armour).",
            ["trade.notrades"] = "No trades with you yet.",
            ["trade.pick"] = "Choose a line to see how its price is made, what it is, and how it compares with what you wear.",
            ["trade.shop"] = "THE SHOP",
            ["trade.recent"] = "RECENT TRADES WITH YOU",
            ["trade.instock"] = "{0} in stock",
            ["trade.youhave"] = "You have {0}",
            ["trade.refused"] = "Not bought here: ",
            ["trade.pricesection"] = "PRICE",
            ["trade.base"] = "Base value",
            ["trade.unitbuy"] = "Each, to you",
            ["trade.unitsell"] = "Each, they pay",
            ["trade.price"] = "Price",
            ["trade.damage"] = "Damage",
            ["trade.protection"] = "Protection",
            ["trade.nourishes"] = "Nourishes",
            ["trade.ifworn"] = "IF WORN ({0}) INSTEAD OF {1}",
            ["trade.description"] = "DESCRIPTION",
            ["trade.quantity"] = "QUANTITY",
            ["trade.max"] = "Max ({0})",
            ["trade.total"] = "Total",
            ["trade.purseafter"] = "Purse after",
            ["trade.buyfor"] = "Buy {0} for {1} coin",
            ["trade.sellfor"] = "Sell {0} for {1} coin",

            // ----- Crafting (Phase 26) -----------------------------------------------------------------------
            ["craft.intro"] = "One piece of work at a time: it holds its materials until it is done, and time passes while you work.",
            ["craft.recipes"] = "Recipes",
            ["craft.canmake"] = "Can make now ({0})",
            ["craft.needssettlement"] = "Crafting and gathering need a settlement.",
            ["craft.pick"] = "Choose a recipe to see what it makes, what it needs, and whether you can start it.",
            ["craft.makes"] = "Makes",
            ["craft.materials"] = "MATERIALS",
            ["craft.tool"] = "Tool",
            ["craft.station"] = "Station",
            ["craft.check"] = "Check",
            ["craft.chance"] = "{0} {1} · DC {2} · {3} % to succeed",
            ["craft.sure"] = "{0} {1} · no check (sure)",
            ["craft.time"] = "Time",
            ["craft.start"] = "Start",
            ["craft.busy"] = "You are busy with other work: collect or abandon it first.",
            ["craft.ready"] = "Ready to start.",
            ["craft.missingtool"] = "missing",
            ["craft.nostation"] = "none here",
            ["craft.proficient"] = "proficient",
            ["craft.gathering"] = "Gathering",
            ["craft.mending"] = "Mending",
            ["craft.attunement"] = "Attunement",
            ["craft.recently"] = "Recently",

            // ----- Party (Phase 26) ---------------------------------------------------------------------------
            ["party.you"] = "You",
            ["party.pick"] = "Choose a member of the party to see them.",
            ["party.empty"] = "Nobody travels with you. Ask someone who knows and trusts you to join (\"Travel with me\" in conversation). Some will come only for a few days; some not at all, and they will say why.",
            ["party.stats"] = "STATE",
            ["party.equipment"] = "EQUIPMENT",
            ["party.abilities"] = "ABILITIES",
            ["party.bond"] = "BOND",

            // ----- Codex (Phase 26) ---------------------------------------------------------------------------
            ["codex.intro"] = "Everything you have learned, gathered from your journal, your travels and your fights. Only what you know is written here.",
            ["codex.empty"] = "Your codex is empty: meet people, visit places and hear of the world, and it fills.",
            ["codex.pick"] = "Choose an entry to read what you know.",
            ["codex.sort.name"] = "By name",
            ["codex.sort.recent"] = "Most recent",
            ["codex.known"] = "WHAT YOU KNOW",
            ["codex.links"] = "CONNECTED",
            ["codex.source"] = "Learned from",
            ["codex.status"] = "Known as",

            // ----- Dialogue (Phase 26) ------------------------------------------------------------------------
            ["dialogue.leave"] = "Leave (Esc)",
            ["dialogue.about"] = "About them",
            ["dialogue.check"] = "Check",
            ["dialogue.party"] = "Party",
            ["dialogue.quest"] = "Quest",
            ["dialogue.trade"] = "Trade",
            ["dialogue.ends"] = "Ends",

            // ----- Region map (Phase 26) ----------------------------------------------------------------------
            ["map.legend"] = "Key",
            ["map.you"] = "You",
            ["map.settlement"] = "Settlement",
            ["map.place"] = "Place",
            ["map.landmark"] = "Landmark",
            ["map.waypoint"] = "Waypoint",
            ["map.gateway"] = "Way to another region",
            ["map.road"] = "Road",
            ["map.scale"] = "One square = 40 m · the region is {0} × {1} km",

            // ----- Combat (Phase 26) --------------------------------------------------------------------------
            ["combat.won"] = "Victory",
            ["combat.lost"] = "Defeat",
            ["combat.withdrawn"] = "Withdrawn",
            ["combat.stalemate"] = "Nobody left standing",
            ["combat.calledoff"] = "Called off",
        };

        private static Dictionary<string, string> _overlay;

        /// <summary>How many strings the table holds (the audit and tests).</summary>
        public static int Count => English.Count;

        public static IEnumerable<string> Keys => English.Keys;

        public static bool Has(string key) => key != null && (English.ContainsKey(key) || _overlay != null && _overlay.ContainsKey(key));

        /// <summary>The text for a key, or the key itself when it is unknown (so a gap is visible).</summary>
        public static string Get(string key)
        {
            if (key == null) return string.Empty;
            if (_overlay != null && _overlay.TryGetValue(key, out var o)) return o;
            return English.TryGetValue(key, out var e) ? e : key;
        }

        /// <summary>The text with its numbered placeholders filled ({0}, {1} …), in the invariant culture.</summary>
        public static string Format(string key, params object[] args) => string.Format(CultureInfo.InvariantCulture, Get(key), args);

        /// <summary>Replaces some strings (a future translation, or a test). Null clears the overlay.</summary>
        public static void Overlay(IDictionary<string, string> strings) => _overlay = strings == null ? null : new Dictionary<string, string>(strings, StringComparer.Ordinal);
    }
}
