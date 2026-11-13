using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using UnityEngine.UIElements;
using static Realmkeeper.Exploration.Presentation.TradeWindow;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 11 (ADR-0055): the item screens of the Player Hub — the inventory (search, filter by group, sort, inspect,
    /// compare with what is worn through the rules engine), equipment with the rules' armour class and attack, crafting
    /// and work, and storage (the player's chest, a cache, loot). The window reads query snapshots through the sources
    /// the controller gives it, and asks for every change through <see cref="ItemActionRequested"/>: the controller runs
    /// it on the session and refreshes. Nothing here changes the world.
    /// </summary>
    public sealed partial class PlayerHubWindow
    {
        /// <summary>Inventory query: group, search, sort, descending.</summary>
        public Func<string, string, InventorySort, bool, InventoryPage> InventorySource { get; set; }
        public Func<ItemStackId, ItemInspection> InspectSource { get; set; }
        public Func<ItemStackId, EquipmentSlot, ItemComparison> CompareSource { get; set; }
        public Func<WorkshopView> WorkshopSource { get; set; }
        public Func<IReadOnlyList<ContainerView>> StorageSource { get; set; }

        /// <summary>A change the player asked for: the controller runs it on the session and shows the result.</summary>
        public event Action<Func<WorldSession, CommandResult>> ItemActionRequested;

        /// <summary>Waiting out the current work (time passes in the world).</summary>
        public event Action<string> FinishWorkRequested;

        private string _search = string.Empty;
        private string _group;
        private InventorySort _sort = InventorySort.Category;
        private bool _descending;
        private ItemStackId _selected;
        private EquipmentSlot _compareSlot = EquipmentSlot.None;

        /// <summary>The container the Storage section opens on (set when the player opens a chest in the street).</summary>
        public string FocusContainer { get; set; }

        public InventorySort Sort => _sort;
        public string GroupFilter => _group;
        public ItemStackId SelectedItem => _selected;

        /// <summary>Selects an item and shows its inspection and comparison (tests and captures use it too).</summary>
        public void SelectItem(ItemStackId id, EquipmentSlot compareWith = EquipmentSlot.None)
        {
            _selected = id;
            _compareSlot = compareWith;
            Render();
        }

        public void SetFilter(string group, string search = null, InventorySort? sort = null, bool? descending = null)
        {
            _group = group;
            if (search != null) _search = search;
            if (sort.HasValue) _sort = sort.Value;
            if (descending.HasValue) _descending = descending.Value;
            Render();
        }

        private void Run(Func<WorldSession, CommandResult> act) => ItemActionRequested?.Invoke(act);

        // Phase 21 (ADR-0107): crafting opened at a shop's work station (empty: from the hub, any open station here).
        private Realmkeeper.Domain.Identifiers.ShopId _stationShop;
        private string _stationNote;

        public void AtStation(Realmkeeper.Domain.Identifiers.ShopId shop, string note)
        {
            _stationShop = shop;
            _stationNote = note;
        }

        private static string W(int grams) => WorldSession.FormatWeight(grams);

        // ----- Inventory --------------------------------------------------------------------------------

        private void Inventory(PlayerHub h) => InventoryView(h, _group, true);

        private void InventoryView(PlayerHub h, string group, bool chips)
        {
            var page = InventorySource?.Invoke(group, _search, _sort, _descending);
            if (page == null)
            {
                _content.Add(Text("The inventory is not available.", "xk-w-empty"));
                return;
            }

            // Load and the rules' own numbers.
            var load = Row("xk-inv-load");
            load.name = "xk-inv-load";
            var bar = new VisualElement { name = "xk-inv-loadbar" };
            bar.AddToClassList("xk-inv-bar");
            var fill = new VisualElement();
            fill.AddToClassList("xk-inv-bar-fill");
            fill.EnableInClassList("xk-inv-bar-fill--over", page.Encumbered);
            fill.style.width = Length.Percent(Math.Min(100f, 100f * page.CarriedGrams / Math.Max(1, page.LimitGrams)));
            bar.Add(fill);
            load.Add(bar);
            load.Add(Text($"{W(page.CarriedGrams)} of {W(page.CapacityGrams)} capacity (most {W(page.LimitGrams)})" + (page.Encumbered ? " — over capacity: speed 5 ft" : string.Empty), "xk-w-note"));
            _content.Add(load);
            _content.Add(Text($"AC {page.ArmorClass}{(string.IsNullOrEmpty(page.ArmorClassText) ? string.Empty : " (" + page.ArmorClassText + ")")} · {page.AttackText ?? "no attack"} · attuned {page.Attuned}/{page.MaxAttuned} · {page.Purse} coin", "xk-w-note"));
            if (page.ActiveWork != null) _content.Add(WorkLine(page.ActiveWork));

            // Search, filter, sort.
            var tools = Row("xk-inv-tools");
            var search = new TextField { name = "xk-inv-search", value = _search };
            search.AddToClassList("xk-inv-search");
            search.RegisterCallback<KeyUpEvent>(e => { if (e.keyCode == UnityEngine.KeyCode.Return) { _search = search.value; Render(); } });
            tools.Add(search);
            tools.Add(Act("Search", () => { _search = search.value; Render(); }));
            if (!string.IsNullOrEmpty(_search)) tools.Add(Act("Clear", () => { _search = string.Empty; Render(); }));
            foreach (InventorySort s in Enum.GetValues(typeof(InventorySort)))
            {
                var sort = s;
                var b = Act(s.ToString(), () => { if (_sort == sort) _descending = !_descending; else { _sort = sort; _descending = sort == InventorySort.Value || sort == InventorySort.Recent || sort == InventorySort.Weight; } Render(); });
                b.name = "xk-inv-sort-" + s;
                b.EnableInClassList("xk-w-choice--on", _sort == s);
                if (_sort == s) b.text = s + (_descending ? " ↓" : " ↑");
                tools.Add(b);
            }

            _content.Add(tools);
            if (chips)
            {
                var row = Row("xk-inv-groups");
                var all = Act($"All ({page.TotalRows})", () => { _group = null; Render(); });
                all.name = "xk-inv-group-all";
                all.EnableInClassList("xk-w-choice--on", group == null);
                row.Add(all);
                foreach (var (g, n) in page.Groups)
                {
                    var gg = g;
                    var b = Act($"{g} ({n})", () => { _group = gg; Render(); });
                    b.name = "xk-inv-group-" + g;
                    b.EnableInClassList("xk-w-choice--on", group == g);
                    row.Add(b);
                }

                _content.Add(row);
            }

            Details(page);

            if (page.Rows.Count == 0) _content.Add(Text(page.TotalRows == 0 ? "Your pack is empty." : "Nothing matches.", "xk-w-empty"));
            var storage = StorageSource?.Invoke();
            ContainerView chest = null;
            if (storage != null) foreach (var c in storage) if (c.CanDeposit && c.Reachable) chest = c;
            foreach (var r in page.Rows) _content.Add(ItemRow(r, page, chest));
        }

        private VisualElement ItemRow(InventoryRow r, InventoryPage page, ContainerView chest)
        {
            var box = Column("xk-inv-item");
            var row = Row("xk-w-row");
            row.name = "xk-hub-item-" + r.ItemId;
            row.EnableInClassList("xk-w-row--selected", r.StackId == _selected);
            var name = Text(r.Name, "xk-w-name");
            name.EnableInClassList("xk-inv-magic", r.IsMagic);
            row.Add(name);
            row.Add(Text("×" + r.Quantity, "xk-w-qty"));
            row.Add(Text(W(r.TotalGrams), "xk-w-price"));
            row.Add(Text(r.UnitValue + " c", "xk-w-price"));
            row.Add(Act("Inspect", () => SelectItem(r.StackId)));
            if (r.CanEquip)
            {
                row.Add(Act("Equip", () => EquipRequested?.Invoke(r.StackId)));
                var def = ItemCatalog.Find(r.ItemId);
                if (def?.Slot == EquipmentSlot.RingLeft || def?.Slot == EquipmentSlot.RingRight)
                    row.Add(Act("Right hand", () => Run(s => s.Dispatch(new EquipItemCommand(r.StackId, EquipmentSlot.RingRight)))));
                if (def?.Weapon != null && def.Weapon.Has(WeaponProperty.Light) && !def.TwoHanded)
                    row.Add(Act("Off hand", () => Run(s => s.Dispatch(new EquipItemCommand(r.StackId, EquipmentSlot.OffHand)))));
            }

            if (r.UseVerb != null) row.Add(Act(r.UseVerb, () => UseRequested?.Invoke(r.StackId)));
            if (r.RequiresAttunement && !r.Attuned) row.Add(Act("Attune", () => Run(s => s.Dispatch(StartWorkCommand.Attune(r.StackId)))));
            if (r.Quantity > 1) row.Add(Act("Split", () => SplitRequested?.Invoke(r.StackId, r.Quantity / 2)));
            if (chest != null && !r.Bound && !r.Attuned)
            {
                var key = chest.Key;
                row.Add(Act("Store", () => Run(s => s.Dispatch(new PutInContainerCommand(key, r.StackId, r.Quantity)))));
            }

            if (!r.Bound) row.Add(Act("Drop 1", () => DiscardRequested?.Invoke(r.StackId, 1)));
            box.Add(row);
            if (!string.IsNullOrEmpty(r.Badges)) box.Add(Text(r.Category + " · " + r.Badges, "xk-w-desc"));
            else box.Add(Text(r.Category, "xk-w-desc"));
            return box;
        }

        /// <summary>The selected item: its inspection, and (when it can be worn) the rules' comparison with what is worn.</summary>
        private void Details(InventoryPage page)
        {
            if (_selected.IsEmpty) return;
            var i = InspectSource?.Invoke(_selected);
            if (i == null) return;
            var panel = Column("xk-inv-details");
            panel.name = "xk-inv-details";
            var head = Row("xk-w-head");
            var t = Text(i.Name, "xk-w-heading");
            t.style.flexGrow = 1;
            head.Add(t);
            head.Add(Act("Close", () => { _selected = default; Render(); }));
            panel.Add(head);
            panel.Add(Text(i.Subtitle, "xk-w-subtitle"));
            foreach (var w in i.Warnings) panel.Add(Text(w, "xk-inv-warning"));
            foreach (var (label, value) in i.Lines) panel.Add(Pair(label, value));
            if (!string.IsNullOrEmpty(i.Description)) panel.Add(Text(i.Description, "xk-w-desc"));
            if (!string.IsNullOrEmpty(i.Lore)) panel.Add(Text(i.Lore, "xk-w-desc"));
            panel.Add(Text("Source: " + i.Source, "xk-w-faint"));

            var cmp = CompareSource?.Invoke(_selected, _compareSlot);
            if (cmp != null)
            {
                var table = Column("xk-inv-compare");
                table.name = "xk-inv-compare";
                table.Add(Text($"Wearing it ({PlayerCommandHandlers.Slot(cmp.Slot)}) instead of {cmp.Current ?? "nothing"}:", "xk-w-heading"));
                foreach (var l in cmp.Lines)
                {
                    var line = Row("xk-w-pair");
                    line.Add(Text(l.Label, "xk-w-key"));
                    line.Add(Text(l.Before + "  →  " + l.After, "xk-w-value"));
                    var mark = Text(l.Better > 0 ? "better" : l.Better < 0 ? "worse" : "same", "xk-inv-delta");
                    mark.EnableInClassList("xk-inv-delta--up", l.Better > 0);
                    mark.EnableInClassList("xk-inv-delta--down", l.Better < 0);
                    line.Add(mark);
                    table.Add(line);
                }

                foreach (var n in cmp.Notes) table.Add(Text(n, "xk-w-note"));
                panel.Add(table);
            }

            _content.Add(panel);
        }

        // ----- Equipment --------------------------------------------------------------------------------

        private static readonly EquipmentSlot[] SlotOrder =
        {
            EquipmentSlot.MainHand, EquipmentSlot.OffHand, EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Legs, EquipmentSlot.Feet, EquipmentSlot.Hands,
            EquipmentSlot.Back, EquipmentSlot.Neck, EquipmentSlot.RingLeft, EquipmentSlot.RingRight,
        };

        private void Equipment(PlayerHub h)
        {
            var page = InventorySource?.Invoke(null, null, InventorySort.Category, false);
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                if (slot != EquipmentSlot.None && Array.IndexOf(SlotOrder, slot) < 0) SlotRow(slot, page);
            foreach (var slot in SlotOrder) SlotRow(slot, page);
            if (page == null) return;
            _content.Add(Text($"Armour class {page.ArmorClass}: {page.ArmorClassText}", "xk-w-note"));
            _content.Add(Text(page.AttackText ?? "No weapon attack.", "xk-w-note"));
            _content.Add(Text($"Attuned to {page.Attuned} of {page.MaxAttuned} items. Equipment shows on your character.", "xk-w-note"));
            Details(page);
        }

        private void SlotRow(EquipmentSlot slot, InventoryPage page)
        {
            var s = slot;
            var row = Row("xk-w-row");
            row.name = "xk-hub-slot-" + slot;
            row.Add(Text(PlayerCommandHandlers.Slot(slot), "xk-w-slot"));
            if (page != null && page.Equipped.TryGetValue(slot, out var item))
            {
                row.Add(Text(item.Name, "xk-w-name"));
                row.Add(Act("Unequip", () => UnequipRequested?.Invoke(s)));
                row.Add(Act("Inspect", () => SelectItem(item.StackId)));
                if (item.Attuned && !item.Bound) row.Add(Act("End attunement", () => Run(x => x.Dispatch(new EndAttunementCommand(item.StackId)))));
                if (item.RequiresAttunement && !item.Attuned) row.Add(Act("Attune", () => Run(x => x.Dispatch(StartWorkCommand.Attune(item.StackId)))));
                _content.Add(row);
                if (!string.IsNullOrEmpty(item.Badges)) _content.Add(Text(item.Badges, "xk-w-desc"));
                return;
            }

            var none = Text(slot == EquipmentSlot.MainHand ? "Bare hands" : "—", "xk-w-name");
            none.AddToClassList("xk-w-faint");
            row.Add(none);
            _content.Add(row);
        }

        private void Filtered(PlayerHub h, string group) => InventoryView(h, group, false);

        // ----- Crafting & work --------------------------------------------------------------------------

        private VisualElement WorkLine(WorkView w)
        {
            var row = Row("xk-w-row");
            row.name = "xk-work-active";
            row.Add(Text(w.Ready ? $"Ready: {w.What}" : $"Busy: {w.What} — {ItemCommandHandlers.Duration(w.MinutesLeft)} to go", "xk-w-name"));
            var key = w.Key;
            row.Add(Act(w.Ready ? "Collect" : "Wait until done", () => FinishWorkRequested?.Invoke(key)));
            if (!w.Ready) row.Add(Act("Abandon", () => Run(s => s.Dispatch(new CancelWorkCommand(key)))));
            return row;
        }

        private void Workshop()
        {
            var v = WorkshopSource?.Invoke();
            if (v == null) return;
            if (v.Active != null) _content.Add(WorkLine(v.Active));
            else _content.Add(Text("One piece of work at a time: it holds its materials until it is done, and time passes while you work.", "xk-w-note"));
            if (!v.InSettlement) _content.Add(Text("Crafting and gathering need a settlement.", "xk-w-empty"));

            if (_stationNote != null) _content.Add(Text(_stationNote, "xk-w-note")); // Phase 21: working at a shop's station
            _content.Add(Text("Recipes", "xk-w-heading"));
            string discipline = null;
            foreach (var r in v.Recipes)
            {
                if (r.Discipline != discipline)
                {
                    discipline = r.Discipline;
                    _content.Add(Text(CultureInfo.InvariantCulture.TextInfo.ToTitleCase(discipline), "xk-w-key"));
                }

                var row = Row("xk-w-row");
                row.name = "xk-craft-" + r.Id;
                row.Add(Text(r.Name, "xk-w-name"));
                row.Add(Text(r.Dc > 0 ? $"DC {r.Dc} · {r.ChancePercent}%" : "sure", "xk-w-price"));
                row.Add(Text(ItemCommandHandlers.Duration(r.Minutes), "xk-w-price"));
                var id = r.Id;
                var at = _stationShop;
                var start = Act("Start", () => Run(s => s.Dispatch(StartWorkCommand.Craft(id, at))));
                start.SetEnabled(r.CanStart && v.Active == null);
                row.Add(start);
                _content.Add(row);
                var inputs = new List<string>();
                foreach (var (name, need, have) in r.Inputs) inputs.Add($"{need} {name.ToLowerInvariant()} ({have})");
                _content.Add(Text($"→ {r.Output} · needs {string.Join(", ", inputs)}" + (r.Tool != null ? $" · {r.Tool.ToLowerInvariant()}{(r.HasTool ? string.Empty : " (missing)")}{(r.Proficient ? ", proficient" : string.Empty)}" : string.Empty)
                    + $" · at a {r.Station}{(r.HasStation ? string.Empty : " (none here)")} · {r.Ability} {(r.Bonus >= 0 ? "+" : string.Empty)}{r.Bonus}" + (r.Problem != null ? " — " + r.Problem : string.Empty), "xk-w-desc"));
            }

            _content.Add(Text("Gathering", "xk-w-heading"));
            if (v.Sites.Count == 0) _content.Add(Text("No places to gather here.", "xk-w-empty"));
            foreach (var site in v.Sites)
            {
                var row = Row("xk-w-row");
                row.name = "xk-gather-" + site.Key;
                row.Add(Text(site.Name, "xk-w-name"));
                row.Add(Text($"{site.Item}: {site.Available}/{site.Capacity}", "xk-w-price"));
                var key = site.Key;
                var go = Act($"Gather {site.Yield} ({ItemCommandHandlers.Duration(site.Minutes)})", () => Run(s => s.Dispatch(StartWorkCommand.Gather(key))));
                go.SetEnabled(site.Problem == null && v.Active == null);
                row.Add(go);
                _content.Add(row);
                if (site.Problem != null) _content.Add(Text(site.Problem, "xk-w-desc"));
            }

            _content.Add(Text("Mending", "xk-w-heading"));
            if (v.Repairs.Count == 0) _content.Add(Text("Nothing in your pack needs mending.", "xk-w-empty"));
            foreach (var (r, disc, dc, bonus, problem) in v.Repairs)
            {
                var row = Row("xk-w-row");
                row.Add(Text($"{r.Name} ({ItemConditions.Name(r.Condition)})", "xk-w-name"));
                row.Add(Text($"{disc} · DC {dc} · {(bonus >= 0 ? "+" : string.Empty)}{bonus}", "xk-w-price"));
                var id = r.StackId;
                var mend = Act("Mend", () => Run(s => s.Dispatch(StartWorkCommand.Repair(id))));
                mend.SetEnabled(problem == null && v.Active == null);
                row.Add(mend);
                _content.Add(row);
                if (problem != null) _content.Add(Text(problem, "xk-w-desc"));
            }

            _content.Add(Text("Attunement", "xk-w-heading"));
            if (v.Attunable.Count == 0) _content.Add(Text("No magic item you carry is waiting for attunement.", "xk-w-empty"));
            foreach (var (r, problem) in v.Attunable)
            {
                var row = Row("xk-w-row");
                row.Add(Text(r.Name, "xk-w-name"));
                var id = r.StackId;
                var attune = Act("Attune (short rest)", () => Run(s => s.Dispatch(StartWorkCommand.Attune(id))));
                attune.SetEnabled(problem == null && v.Active == null);
                row.Add(attune);
                _content.Add(row);
                if (problem != null) _content.Add(Text(problem, "xk-w-desc"));
            }

            if (v.RecentOutcomes.Count > 0)
            {
                _content.Add(Text("Recently", "xk-w-heading"));
                foreach (var o in v.RecentOutcomes) _content.Add(Text(o, "xk-w-ledger-line"));
            }
        }

        // ----- Storage ----------------------------------------------------------------------------------

        private void Storage()
        {
            var list = StorageSource?.Invoke();
            if (list == null || list.Count == 0)
            {
                _content.Add(Text("Nothing to open here. Your chest waits at each settlement's tavern; what a fight leaves behind is listed here until you take it.", "xk-w-empty"));
                return;
            }

            var ordered = new List<ContainerView>(list);
            ordered.Sort((a, b) => (a.Key == FocusContainer ? 0 : 1).CompareTo(b.Key == FocusContainer ? 0 : 1));
            foreach (var c in ordered)
            {
                var box = Column("xk-store");
                box.name = "xk-store-" + c.Key;
                var head = Row("xk-w-head");
                var t = Text(c.Name, "xk-w-heading");
                t.style.flexGrow = 1;
                head.Add(t);
                var key = c.Key;
                if (!c.Reachable) head.Add(Text(c.Problem, "xk-w-faint"));
                else if (c.Rows.Count > 0 || c.Coin > 0) head.Add(Act("Take all", () => Run(s => s.Dispatch(new TakeAllFromContainerCommand(key)))));
                box.Add(head);
                box.Add(Text($"{c.Coin} coin · {W(c.Grams)}{(c.CapacityGrams > 0 ? " of " + W(c.CapacityGrams) : string.Empty)}{(c.CanDeposit ? " · you may leave things here" : string.Empty)}", "xk-w-note"));
                if (c.Coin > 0 && c.Reachable) box.Add(Act($"Take {c.Coin} coin", () => Run(s => s.Dispatch(new MoveContainerCoinCommand(key, c.Coin)))));
                if (c.CanDeposit && c.Reachable && Hub != null && Hub.Purse >= 10) box.Add(Act("Leave 10 coin", () => Run(s => s.Dispatch(new MoveContainerCoinCommand(key, -10)))));
                if (c.Rows.Count == 0) box.Add(Text("Empty.", "xk-w-empty"));
                foreach (var r in c.Rows)
                {
                    var row = Row("xk-w-row");
                    row.name = "xk-store-item-" + r.ItemId;
                    row.Add(Text(r.Name, "xk-w-name"));
                    row.Add(Text("×" + r.Quantity, "xk-w-qty"));
                    row.Add(Text(W(r.TotalGrams), "xk-w-price"));
                    var id = r.StackId;
                    var qty = r.Quantity;
                    var take = Act("Take", () => Run(s => s.Dispatch(new TakeFromContainerCommand(key, id, qty))));
                    take.SetEnabled(c.Reachable);
                    row.Add(take);
                    box.Add(row);
                    if (!string.IsNullOrEmpty(r.Badges)) box.Add(Text(r.Category + " · " + r.Badges, "xk-w-desc"));
                }

                _content.Add(box);
            }
        }
    }
}
