using System;
using Realmkeeper.Application;
using Realmkeeper.UI;
using UnityEngine.UIElements;
using static Realmkeeper.Exploration.Presentation.TradeWindow;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 26 (ADR-0117): the Codex section, which brings together what the player has learned: people, companions,
    /// factions, settlements, places, creatures, discoveries and events.
    /// <list type="bullet">
    /// <item><b>Controls:</b> category chips with counts, a search across names and known facts, and a sort (by name, or
    /// most recent first).</item>
    /// <item><b>The inspector:</b> the selected entry with each fact and how it is known (its tooltip), what it connects to,
    /// and what knowing more would add.</item>
    /// </list>
    /// Everything comes from <see cref="WorldSession.GetCodex"/>, which reads only existing knowledge records.
    /// </summary>
    public sealed partial class PlayerHubWindow
    {
        public Func<string, CodexCategory?, bool, CodexView> CodexSource { get; set; }

        private CodexCategory? _codexCategory;
        private string _codexSearch = string.Empty;
        private bool _codexRecent;
        private string _codexEntry;

        public string SelectedCodexEntry => _codexEntry;

        public void SelectCodex(string entryId)
        {
            _codexEntry = entryId;
            Render();
        }

        public void SetCodexFilter(CodexCategory? category, string search = null, bool? recentFirst = null)
        {
            _codexCategory = category;
            if (search != null) _codexSearch = search;
            if (recentFirst.HasValue) _codexRecent = recentFirst.Value;
            Render();
        }

        public static string CodexIcon(CodexCategory c) => c switch
        {
            CodexCategory.People => "people",
            CodexCategory.Companions => "companion",
            CodexCategory.Factions => "faction",
            CodexCategory.Settlements => "settlement",
            CodexCategory.Places => "place",
            CodexCategory.Creatures => "bestiary",
            CodexCategory.Discoveries => "discovery",
            _ => "events",
        };

        private void Codex()
        {
            var v = CodexSource?.Invoke(_codexSearch, _codexCategory, _codexRecent);
            if (v == null) { _content.Add(Text(RkText.Get("codex.empty"), "rk-empty")); return; }
            _content.Add(Text(RkText.Get("codex.intro"), "xk-w-note"));
            if (v.Total == 0) { _content.Add(Text(RkText.Get("codex.empty"), "rk-empty")); return; }

            var tools = Row("xk-inv-tools");
            var search = new TextField { name = "xk-codex-search", value = _codexSearch };
            search.AddToClassList("xk-inv-search");
            search.RegisterCallback<KeyUpEvent>(e => { _codexSearch = search.value; if (e.keyCode == UnityEngine.KeyCode.Return) Render(); });
            tools.Add(search);
            tools.Add(Act(RkText.Get("common.search"), () => { _codexSearch = search.value; Render(); }));
            var sort = Act(_codexRecent ? RkText.Get("codex.sort.recent") : RkText.Get("codex.sort.name"), () => { _codexRecent = !_codexRecent; Render(); });
            sort.name = "xk-codex-sort";
            tools.Add(sort);
            _content.Add(tools);

            var chips = Row("xk-chips");
            var all = Act(RkText.Format("common.all", v.Total), () => { _codexCategory = null; Render(); });
            all.name = "xk-codex-cat-all";
            all.EnableInClassList("xk-w-choice--on", _codexCategory == null);
            chips.Add(all);
            foreach (CodexCategory c in Enum.GetValues(typeof(CodexCategory)))
            {
                if (!v.Counts.TryGetValue(c, out var n) || n == 0) continue; // a category with nothing known is not offered
                var cat = c;
                var b = Act($"{c} ({n})", () => { _codexCategory = cat; Render(); });
                b.name = "xk-codex-cat-" + c;
                b.EnableInClassList("xk-w-choice--on", _codexCategory == c);
                RkIcons.Lead(b, CodexIcon(c), "s");
                chips.Add(b);
            }

            _content.Add(chips);

            var md = Row("rk-md");
            var list = Column("rk-md-list");
            list.name = "xk-codex-list";
            CodexEntryView chosen = null;
            CodexCategory? group = null;
            foreach (var e in v.Entries)
            {
                if (e.Id == _codexEntry) chosen = e;
                if (!_codexRecent && group != e.Category)
                {
                    group = e.Category;
                    list.Add(Text(e.Category.ToString().ToUpperInvariant(), "xk-codex-section"));
                }

                var row = Row("rk-row");
                row.name = "xk-codex-" + e.Id;
                row.EnableInClassList("rk-row--selected", e.Id == _codexEntry);
                row.Add(IconFrame(CodexIcon(e.Category), e.Id == _codexEntry ? "accent" : null));
                var main = Column("rk-row-main");
                main.Add(Text(e.Name, "rk-row-name"));
                main.Add(Text(string.IsNullOrEmpty(e.Subtitle) ? e.Status : e.Subtitle + " · " + e.Status, "rk-row-sub"));
                row.Add(main);
                var id = e.Id;
                row.RegisterCallback<ClickEvent>(_ => SelectCodex(id));
                list.Add(row);
            }

            if (v.Entries.Count == 0) list.Add(Text(RkText.Get("common.nothingmatches"), "rk-empty"));
            md.Add(list);
            var detail = Column("rk-md-detail");
            detail.Add(chosen != null ? CodexCard(chosen) : RkInspector.Empty("xk-codex-none", RkText.Get("codex.pick")));
            md.Add(detail);
            _content.Add(md);
        }

        private static VisualElement CodexCard(CodexEntryView e)
        {
            var card = new RkInspector("xk-codex-card");
            card.Header(CodexIcon(e.Category), e.Name, e.Subtitle, "accent");
            card.Badge(e.Status, e.Status == "Dead" ? "bad" : "info");
            card.Badge(e.Category.ToString(), null);
            if (!string.IsNullOrEmpty(e.Text)) card.Text(e.Text, e.Category == CodexCategory.Places || e.Category == CodexCategory.Factions ? "rk-lore" : null);
            if (e.Lines.Count > 0)
            {
                card.Section(RkText.Get("codex.known"));
                foreach (var (label, value, how) in e.Lines) card.Stat(label, value, null, how == null ? null : "Known from: " + how);
            }

            if (e.Links.Count > 0)
            {
                card.Section(RkText.Get("codex.links"));
                foreach (var l in e.Links) card.Text("· " + l);
            }

            if (!string.IsNullOrEmpty(e.Withheld)) card.Note(e.Withheld, "info");
            card.Stat(RkText.Get("codex.source"), e.Source);
            return card.Root;
        }
    }
}
