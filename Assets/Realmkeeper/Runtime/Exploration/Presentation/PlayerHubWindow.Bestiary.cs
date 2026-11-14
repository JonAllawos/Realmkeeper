using System;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.UI;
using UnityEngine.UIElements;
using static Realmkeeper.Exploration.Presentation.TradeWindow;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 25 (ADR-0116): the Bestiary section. Only creatures the player has met in a fight are listed, and each shows
    /// only what the player has learned:
    /// <list type="bullet">
    /// <item><b>Seen.</b> Its kind, habitats and behaviour.</item>
    /// <item><b>Defeated once.</b> Also its armour, hit points, speed, challenge, attacks and defences.</item>
    /// </list>
    /// What is not known yet is said in words, never shown as blanks or invented. A list with a category filter and a
    /// search sits beside the shared inspector.
    /// </summary>
    public sealed partial class PlayerHubWindow
    {
        /// <summary>The bestiary query (set by the controller).</summary>
        public Func<BestiaryView> BestiarySource { get; set; }

        private string _beast;
        private CreatureCategory? _beastCategory;
        private string _beastSearch = string.Empty;

        public string SelectedCreature => _beast;

        public void SelectCreature(string creatureId)
        {
            _beast = creatureId;
            Render();
        }

        /// <summary>The icon for a creature: its own when the family has one, otherwise its category's.</summary>
        public static string CreatureIcon(string creatureId, CreatureCategory category)
        {
            var id = creatureId ?? string.Empty;
            if (id.Contains("wolf")) return "wolf";
            if (id.Contains("spider")) return "spider";
            if (id.Contains("rat")) return "rat";
            if (id.Contains("bear")) return "bear";
            if (id.Contains("boar")) return "boar";
            if (id.Contains("horse") || id.Contains("pony")) return "horse";
            if (id.Contains("sheep")) return "sheep";
            if (id.Contains("goblin")) return "goblin";
            return category switch
            {
                CreatureCategory.Humanoid => "humanoid",
                CreatureCategory.Wildlife => "wildlife",
                CreatureCategory.Livestock => "livestock",
                CreatureCategory.CommonThreat => "threat",
                CreatureCategory.Dangerous => "dangerous",
                CreatureCategory.Undead => "undead",
                _ => "bestiary",
            };
        }

        private void Bestiary()
        {
            var v = BestiarySource?.Invoke();
            if (v == null || v.Entries.Count == 0)
            {
                _content.Add(Text("Your bestiary is empty. Creatures you face in a fight are written here: what you saw of them at once, and their strength once you have defeated one.", "rk-empty"));
                return;
            }

            var kinds = v.Known == 1 ? "kind" : "kinds";
            _content.Add(Text($"{v.Known} {kinds} met, {v.Studied} studied (defeated at least once). Only what you have seen is written here.", "xk-w-note"));
            var tools = Row("xk-inv-tools");
            var search = new TextField { name = "xk-beast-search", value = _beastSearch };
            search.AddToClassList("xk-inv-search");
            search.RegisterCallback<KeyUpEvent>(e => { _beastSearch = search.value; if (e.keyCode == UnityEngine.KeyCode.Return) Render(); });
            tools.Add(search);
            tools.Add(Act("Search", () => { _beastSearch = search.value; Render(); }));
            _content.Add(tools);

            var chips = Row("xk-inv-groups");
            var all = Act($"All ({v.Known})", () => { _beastCategory = null; Render(); });
            all.name = "xk-beast-cat-all";
            all.EnableInClassList("xk-w-choice--on", _beastCategory == null);
            chips.Add(all);
            foreach (CreatureCategory c in Enum.GetValues(typeof(CreatureCategory)))
            {
                var n = v.Entries.FindAll(e => e.Category == c).Count;
                if (n == 0) continue;
                var cat = c;
                var b = Act($"{WorldSession.CategoryName(c)} ({n})", () => { _beastCategory = cat; Render(); });
                b.name = "xk-beast-cat-" + c;
                b.EnableInClassList("xk-w-choice--on", _beastCategory == c);
                chips.Add(b);
            }

            _content.Add(chips);

            var md = Row("rk-md");
            var list = Column("rk-md-list");
            list.name = "xk-beast-list";
            BestiaryEntryView chosen = null;
            var shown = 0;
            foreach (var e in v.Entries)
            {
                if (_beastCategory.HasValue && e.Category != _beastCategory.Value) continue;
                if (!string.IsNullOrEmpty(_beastSearch) && e.Name.IndexOf(_beastSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                shown++;
                if (e.CreatureId == _beast) chosen = e;
                var row = Row("rk-row");
                row.name = "xk-beast-" + e.CreatureId;
                row.EnableInClassList("rk-row--selected", e.CreatureId == _beast);
                row.Add(IconFrame(CreatureIcon(e.CreatureId, e.Category), e.Studied ? "accent" : null));
                var main = Column("rk-row-main");
                main.Add(Text(e.Name, "rk-row-name"));
                main.Add(Text(e.CategoryName + (e.Studied ? " · studied" : " · seen"), "rk-row-sub"));
                row.Add(main);
                row.Add(Text("met " + e.Met, "rk-row-cell"));
                var beaten = Text("defeated " + e.Defeated, "rk-row-cell");
                beaten.AddToClassList("rk-row-cell--wide");
                row.Add(beaten);
                var id = e.CreatureId;
                row.RegisterCallback<ClickEvent>(_ => SelectCreature(id));
                list.Add(row);
            }

            if (shown == 0) list.Add(Text("Nothing matches.", "xk-w-empty"));
            md.Add(list);
            var detail = Column("rk-md-detail");
            detail.Add(chosen != null ? CreatureCard(chosen) : RkInspector.Empty("xk-beast-none", "Choose a creature to read what you know of it."));
            md.Add(detail);
            _content.Add(md);
        }

        private static VisualElement CreatureCard(BestiaryEntryView e)
        {
            var card = new RkInspector("xk-beast-card");
            card.Header(CreatureIcon(e.CreatureId, e.Category), e.Name, $"{e.Size} {e.Type.ToLowerInvariant()} · {e.CategoryName}", e.Studied ? "accent" : null);
            card.Badge(e.Studied ? "Studied" : "Seen", e.Studied ? "good" : "info");
            card.Stat("First met", e.FirstMet);
            card.Stat("Fights", e.Met + (e.Met == 1 ? " fight" : " fights"));
            card.Stat("Defeated", e.Defeated.ToString());
            card.Stat("Lives in", e.Habitats);
            card.Section("BEHAVIOUR");
            card.Text(e.Behaviour ?? "Not observed.");
            if (!string.IsNullOrEmpty(e.Description)) card.Text(e.Description, "rk-lore");
            card.Section("STRENGTH");
            if (!e.Studied)
            {
                card.Note("Unknown until you have defeated one: armour, hit points, attacks and defences are learned in victory.", "info");
                return card.Root;
            }

            card.Stat("Armour class", e.ArmourClass);
            card.Stat("Hit points", e.HitPoints);
            card.Stat("Speed", e.Speed);
            card.Stat("Challenge", e.Challenge);
            card.Stat("Senses", e.Senses);
            card.Stat("Defences", e.Defences);
            if (e.Attacks.Count > 0)
            {
                card.Section("ATTACKS");
                foreach (var a in e.Attacks) card.Text(a);
            }

            if (e.Traits.Count > 0)
            {
                card.Section("TRAITS");
                card.Text(string.Join(", ", e.Traits));
            }

            return card.Root;
        }
    }
}
