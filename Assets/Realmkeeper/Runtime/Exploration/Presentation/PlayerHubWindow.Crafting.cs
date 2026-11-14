using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Application;
using Realmkeeper.UI;
using UnityEngine.UIElements;
using static Realmkeeper.Exploration.Presentation.TradeWindow;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 26: the recipe browser of Crafting &amp; work, on the shared list-and-inspector pattern.
    /// <list type="bullet">
    /// <item><b>Filters:</b> a category (discipline) chip row with counts, "Can make now", and a search over names and outputs.</item>
    /// <item><b>The list:</b> each recipe with its output, and its check or the one thing stopping it.</item>
    /// <item><b>The inspector:</b> what it makes; every material as have/need with a tick or a cross; tool, station, check
    /// and chance, time; and Start, or the exact reason it cannot start.</item>
    /// </list>
    /// The data is the existing <see cref="WorkshopView"/> and the action the existing <c>StartWorkCommand.Craft</c>:
    /// no new mechanics, no changed recipes. The Start button stays on each row too, so routine work is one click.
    /// </summary>
    public sealed partial class PlayerHubWindow
    {
        private string _recipe;
        private string _recipeDiscipline;
        private bool _recipeCanMake;
        private string _recipeSearch = string.Empty;

        public string SelectedRecipe => _recipe;

        public void SelectRecipe(string id)
        {
            _recipe = id;
            Render();
        }

        public void SetRecipeFilter(string discipline, bool? canMake = null, string search = null)
        {
            _recipeDiscipline = discipline;
            if (canMake.HasValue) _recipeCanMake = canMake.Value;
            if (search != null) _recipeSearch = search;
            Render();
        }

        /// <summary>An icon for each craft (the one family; the discipline is also written).</summary>
        public static string DisciplineIcon(string discipline) => (discipline ?? string.Empty).ToLowerInvariant() switch
        {
            "blacksmithing" => "production",
            "leatherworking" => "armor",
            "woodworking" => "wood",
            "tailoring" => "clothing",
            "alchemy" => "potion",
            "cooking" => "food",
            _ => "crafting",
        };

        private static string Title(string s) => string.IsNullOrEmpty(s) ? s : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s);

        private void RecipeBrowser(WorkshopView v)
        {
            var canNow = 0;
            var disciplines = new List<(string Name, int Count)>();
            foreach (var r in v.Recipes)
            {
                if (r.CanStart) canNow++;
                var i = disciplines.FindIndex(d => d.Name == r.Discipline);
                if (i < 0) disciplines.Add((r.Discipline, 1));
                else disciplines[i] = (r.Discipline, disciplines[i].Count + 1);
            }

            _content.Add(Text(RkText.Get("craft.recipes"), "xk-w-heading"));
            var tools = Row("xk-inv-tools");
            var search = new TextField { name = "xk-craft-search", value = _recipeSearch };
            search.AddToClassList("xk-inv-search");
            search.RegisterCallback<KeyUpEvent>(e => { _recipeSearch = search.value; if (e.keyCode == UnityEngine.KeyCode.Return) Render(); });
            tools.Add(search);
            tools.Add(Act(RkText.Get("common.search"), () => { _recipeSearch = search.value; Render(); }));
            var can = Act(RkText.Format("craft.canmake", canNow), () => { _recipeCanMake = !_recipeCanMake; Render(); });
            can.name = "xk-craft-canmake";
            can.EnableInClassList("xk-w-choice--on", _recipeCanMake);
            tools.Add(can);
            _content.Add(tools);

            var chips = Row("xk-chips");
            var all = Act(RkText.Format("common.all", v.Recipes.Count), () => { _recipeDiscipline = null; Render(); });
            all.name = "xk-craft-cat-all";
            all.EnableInClassList("xk-w-choice--on", _recipeDiscipline == null);
            chips.Add(all);
            foreach (var (name, count) in disciplines)
            {
                var d = name;
                var b = Act($"{Title(name)} ({count})", () => { _recipeDiscipline = d; Render(); });
                b.name = "xk-craft-cat-" + name;
                b.EnableInClassList("xk-w-choice--on", _recipeDiscipline == name);
                RkIcons.Lead(b, DisciplineIcon(name), "s");
                chips.Add(b);
            }

            _content.Add(chips);

            var md = Row("rk-md");
            var list = Column("rk-md-list");
            list.name = "xk-craft-list";
            RecipeView chosen = null;
            var shown = 0;
            foreach (var r in v.Recipes)
            {
                if (_recipeDiscipline != null && r.Discipline != _recipeDiscipline) continue;
                if (_recipeCanMake && !r.CanStart) continue;
                if (_recipeSearch.Length > 0 && r.Name.IndexOf(_recipeSearch, StringComparison.OrdinalIgnoreCase) < 0 && (r.Output ?? string.Empty).IndexOf(_recipeSearch, StringComparison.OrdinalIgnoreCase) < 0) continue;
                shown++;
                if (r.Id == _recipe) chosen = r;
                var row = Row("rk-row");
                row.AddToClassList("xk-w-row");
                row.name = "xk-craft-" + r.Id;
                row.EnableInClassList("rk-row--selected", r.Id == _recipe);
                row.Add(IconFrame(DisciplineIcon(r.Discipline), r.CanStart ? "accent" : null));
                var main = Column("rk-row-main");
                main.Add(Text(r.Name, "rk-row-name"));
                var sub = Text(r.Problem ?? (Title(r.Discipline) + " · → " + r.Output), "rk-row-sub");
                if (r.Problem != null) sub.AddToClassList("xk-trade-why");
                main.Add(sub);
                row.Add(main);
                var check = Text(r.Dc > 0 ? $"DC {r.Dc} · {r.ChancePercent}%" : "sure", "rk-row-cell");
                check.AddToClassList("rk-row-cell--wide");
                row.Add(check);
                row.Add(Text(ItemCommandHandlers.Duration(r.Minutes), "rk-row-cell"));
                var id = r.Id;
                var at = _stationShop;
                var start = Act(RkText.Get("craft.start"), () => Run(s => s.Dispatch(StartWorkCommand.Craft(id, at))));
                start.SetEnabled(r.CanStart && v.Active == null);
                start.tooltip = v.Active != null ? RkText.Get("craft.busy") : r.Problem;
                row.Add(start);
                row.RegisterCallback<ClickEvent>(e => { if (!(e.target is Button)) SelectRecipe(id); });
                list.Add(row);
            }

            if (shown == 0) list.Add(Text(RkText.Get("common.nothingmatches"), "rk-empty"));
            md.Add(list);
            var detail = Column("rk-md-detail");
            detail.Add(chosen != null ? RecipeCard(chosen, v) : RkInspector.Empty("xk-craft-none", RkText.Get("craft.pick")));
            md.Add(detail);
            _content.Add(md);
        }

        private VisualElement RecipeCard(RecipeView r, WorkshopView v)
        {
            var card = new RkInspector("xk-craft-card");
            card.Header(DisciplineIcon(r.Discipline), r.Name, Title(r.Discipline), r.CanStart ? "accent" : null);
            card.Badge(r.CanStart && v.Active == null ? RkText.Get("craft.ready") : v.Active != null ? RkText.Get("craft.busy") : r.Problem, r.CanStart && v.Active == null ? "good" : "warn");
            card.Stat(RkText.Get("craft.makes"), r.Output);
            card.Section(RkText.Get("craft.materials"));
            foreach (var (name, need, have) in r.Inputs)
            {
                var line = Row("xk-craft-mat");
                line.name = "xk-craft-mat-" + name;
                var enough = have >= need;
                line.EnableInClassList("xk-craft-mat--missing", !enough);
                line.Add(RkIcons.Make(enough ? "check" : "close", "s", enough ? "good" : "bad"));
                line.Add(Text($"{name}: {have} / {need}" + (enough ? string.Empty : $" ({need - have} more)"), "rk-inspector-text"));
                card.Root.Add(line);
            }

            if (r.Tool != null)
                card.Stat(RkText.Get("craft.tool"), r.Tool + (r.HasTool ? string.Empty : " (" + RkText.Get("craft.missingtool") + ")") + (r.Proficient ? " · " + RkText.Get("craft.proficient") : string.Empty));
            card.Stat(RkText.Get("craft.station"), r.Station + (r.HasStation ? string.Empty : " (" + RkText.Get("craft.nostation") + ")"));
            var bonus = (r.Bonus >= 0 ? "+" : string.Empty) + r.Bonus;
            card.Stat(RkText.Get("craft.check"), r.Dc > 0 ? RkText.Format("craft.chance", r.Ability, bonus, r.Dc, r.ChancePercent) : RkText.Format("craft.sure", r.Ability, bonus));
            card.Stat(RkText.Get("craft.time"), ItemCommandHandlers.Duration(r.Minutes));
            if (r.Problem != null) card.Note(r.Problem, "warn");
            var id = r.Id;
            var at = _stationShop;
            card.Action(RkText.Get("craft.start"), "xk-craft-start", () => Run(s => s.Dispatch(StartWorkCommand.Craft(id, at))), r.CanStart && v.Active == null,
                v.Active != null ? RkText.Get("craft.busy") : r.Problem, true);
            return card.Root;
        }
    }
}
