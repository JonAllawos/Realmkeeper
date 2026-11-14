using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

            // Phase 25: a list on the left and the selected item's inspector on the right (stacked on narrow screens).
            var storage = StorageSource?.Invoke();
            ContainerView chest = null;
            if (storage != null) foreach (var c in storage) if (c.CanDeposit && c.Reachable) chest = c;
            var md = Row("rk-md");
            md.name = "xk-inv-md";
            var list = Column("rk-md-list");
            list.name = "xk-inv-list";
            if (page.Rows.Count == 0) list.Add(Text(page.TotalRows == 0 ? "Your pack is empty." : "Nothing matches.", "xk-w-empty"));
            foreach (var r in page.Rows) list.Add(ItemRow(r));
            md.Add(list);
            var detail = Column("rk-md-detail");
            detail.Add(Details(page, chest) ?? Realmkeeper.UI.RkInspector.Empty("xk-inv-none", "Choose an item to see what it is, what it does, and how it compares with what you wear."));
            md.Add(detail);
            _content.Add(md);
        }

        /// <summary>Phase 25: an icon in the list's square frame.</summary>
        internal static VisualElement IconFrame(string icon, string tone = null)
        {
            var frame = new VisualElement();
            frame.AddToClassList("rk-row-icon");
            frame.Add(Realmkeeper.UI.RkIcons.Make(icon, "m", tone));
            return frame;
        }

        /// <summary>Phase 25: the icon for an item's category (one family, tinted; the category is also written under the name).</summary>
        public static string ItemIcon(string itemId)
        {
            var def = ItemCatalog.Find(itemId);
            if (def == null) return "inventory";
            if (def.Weapon != null && def.Weapon.Has(WeaponProperty.Ammunition)) return "bow";
            return def.Category switch
            {
                ItemCategory.Weapon => "weapon", ItemCategory.Shield => "shield", ItemCategory.Clothing => "clothing", ItemCategory.Armour => "armor",
                ItemCategory.Headwear => "helmet", ItemCategory.Food => "bread", ItemCategory.TradeGood => "trade", ItemCategory.Accessory => "amulet",
                ItemCategory.Tool => "crafting", ItemCategory.Focus => "arcane", ItemCategory.Ammunition => "ammo", ItemCategory.Potion => "potion",
                ItemCategory.Jewelry => "ring", ItemCategory.Material => "ore", ItemCategory.Quest => "quest", ItemCategory.Book => "book",
                ItemCategory.Key => "key", ItemCategory.Container => "storage", ItemCategory.Wondrous => "spell", _ => "inventory",
            };
        }

        /// <summary>
        /// One row: icon, name with its category and badges, quantity, weight and value, then the quick actions that keep
        /// routine use to one click: Inspect, and the item's main use (Equip, or its verb: Eat, Drink, Read …). Every
        /// other action is in the inspector.
        /// </summary>
        private VisualElement ItemRow(InventoryRow r)
        {
            var row = Row("rk-row");
            row.AddToClassList("xk-w-row");
            row.name = "xk-hub-item-" + r.ItemId;
            row.EnableInClassList("rk-row--selected", r.StackId == _selected);
            row.EnableInClassList("xk-w-row--selected", r.StackId == _selected);
            var frame = new VisualElement();
            frame.AddToClassList("rk-row-icon");
            frame.Add(Realmkeeper.UI.RkIcons.Make(ItemIcon(r.ItemId), "m", r.IsMagic ? "info" : null));
            row.Add(frame);
            var main = Column("rk-row-main");
            var name = Text(r.Name, "rk-row-name");
            name.AddToClassList("xk-w-name");
            name.EnableInClassList("xk-inv-magic", r.IsMagic);
            main.Add(name);
            main.Add(Text(string.IsNullOrEmpty(r.Badges) ? r.Category : r.Category + " · " + r.Badges, "rk-row-sub"));
            row.Add(main);
            row.Add(Text("×" + r.Quantity, "rk-row-cell"));
            var weight = Text(W(r.TotalGrams), "rk-row-cell");
            weight.AddToClassList("rk-row-cell--wide");
            row.Add(weight);
            row.Add(Text(r.UnitValue + " c", "rk-row-cell"));
            var inspect = Act("Inspect", () => SelectItem(r.StackId));
            inspect.AddToClassList("rk-row-tag");
            row.Add(inspect);
            if (r.CanEquip) row.Add(Act("Equip", () => EquipRequested?.Invoke(r.StackId)));
            else if (r.UseVerb != null) row.Add(Act(r.UseVerb, () => UseRequested?.Invoke(r.StackId)));
            row.RegisterCallback<ClickEvent>(e => { if (!(e.target is Button)) SelectItem(r.StackId); });
            return row;
        }

        /// <summary>
        /// The selected item in the shared inspector (Phase 25). It shows:
        /// <list type="bullet">
        /// <item>its inspection lines;</item>
        /// <item>the rules engine's comparison with what is worn, in words ("better" or "worse" as well as colour);</item>
        /// <item>every action that applies to it now. Each is a real command; actions that do not apply are not offered.</item>
        /// </list>
        /// </summary>
        private VisualElement Details(InventoryPage page, ContainerView chest = null)
        {
            if (_selected.IsEmpty) return null;
            var i = InspectSource?.Invoke(_selected);
            if (i == null) return null;
            InventoryRow r = null;
            if (page != null)
            {
                foreach (var x in page.Rows) if (x.StackId == _selected) r = x;
                if (r == null) foreach (var x in page.Equipped.Values) if (x.StackId == _selected) r = x;
            }

            var card = new Realmkeeper.UI.RkInspector("xk-inv-details");
            card.Header(r != null ? ItemIcon(r.ItemId) : "inventory", i.Name, i.Subtitle, r != null && r.IsMagic ? "info" : "accent");
            if (r != null)
            {
                if (r.IsMagic) card.Badge(r.Rarity.ToString(), "info");
                if (r.EquippedIn != EquipmentSlot.None) card.Badge("Worn: " + PlayerCommandHandlers.Slot(r.EquippedIn), "good");
                if (r.Attuned) card.Badge("Attuned", "info");
                if (r.Bound) card.Badge("Bound", "warn");
            }

            foreach (var w in i.Warnings) card.Note(w, "warn");
            foreach (var (label, value) in i.Lines) card.Stat(label, value);
            var cmp = CompareSource?.Invoke(_selected, _compareSlot);
            if (cmp != null)
            {
                card.Section($"IF WORN ({PlayerCommandHandlers.Slot(cmp.Slot).ToUpperInvariant()}) INSTEAD OF {(cmp.Current ?? "nothing").ToUpperInvariant()}");
                var table = new VisualElement { name = "xk-inv-compare" };
                foreach (var l in cmp.Lines)
                {
                    var c = new Realmkeeper.UI.RkInspector("xk-inv-compare-line");
                    c.Root.RemoveFromClassList("rk-inspector");
                    c.Compare(l.Label, l.Before, l.After, l.Better);
                    foreach (var child in c.Root.Children().ToList()) table.Add(child);
                }

                foreach (var n in cmp.Notes)
                {
                    var note = Text(n, "rk-inspector-note");
                    table.Add(note);
                }

                card.Root.Add(table);
            }

            if (!string.IsNullOrEmpty(i.Description)) { card.Section("DESCRIPTION"); card.Text(i.Description); }
            if (!string.IsNullOrEmpty(i.Lore)) card.Text(i.Lore, "rk-lore");
            card.Text("Source: " + i.Source, "rk-hint");

            if (r != null)
            {
                var def = ItemCatalog.Find(r.ItemId);
                var id = r.StackId;
                if (r.EquippedIn != EquipmentSlot.None)
                {
                    var slot = r.EquippedIn;
                    card.Action("Unequip", "xk-inv-act-unequip", () => UnequipRequested?.Invoke(slot));
                }
                else if (r.CanEquip)
                {
                    card.Action("Equip", "xk-inv-act-equip", () => EquipRequested?.Invoke(id), primary: true);
                    if (def?.Slot == EquipmentSlot.RingLeft || def?.Slot == EquipmentSlot.RingRight)
                        card.Action("Right hand", "xk-inv-act-right", () => Run(s => s.Dispatch(new EquipItemCommand(id, EquipmentSlot.RingRight))));
                    if (def?.Weapon != null && def.Weapon.Has(WeaponProperty.Light) && !def.TwoHanded)
                        card.Action("Off hand", "xk-inv-act-offhand", () => Run(s => s.Dispatch(new EquipItemCommand(id, EquipmentSlot.OffHand))));
                }

                if (r.UseVerb != null) card.Action(r.UseVerb, "xk-inv-act-use", () => UseRequested?.Invoke(id), primary: !r.CanEquip);
                if (r.RequiresAttunement && !r.Attuned) card.Action("Attune", "xk-inv-act-attune", () => Run(s => s.Dispatch(StartWorkCommand.Attune(id))));
                if (r.Attuned && !r.Bound) card.Action("End attunement", "xk-inv-act-unattune", () => Run(s => s.Dispatch(new EndAttunementCommand(id))));
                if (r.Quantity > 1) { var half = r.Quantity / 2; card.Action("Split", "xk-inv-act-split", () => SplitRequested?.Invoke(id, half)); }
                if (r.EquippedIn == EquipmentSlot.None)
                {
                    if (chest != null && !r.Bound && !r.Attuned)
                    {
                        var key = chest.Key;
                        var qty = r.Quantity;
                        card.Action("Store", "xk-inv-act-store", () => Run(s => s.Dispatch(new PutInContainerCommand(key, id, qty))));
                    }

                    if (!r.Bound) card.Action("Drop 1", "xk-inv-act-drop", () => DiscardRequested?.Invoke(id, 1), danger: true);
                }
            }

            card.Action("Close", "xk-inv-act-close", () => { _selected = default; Render(); });
            return card.Root;
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
            // Phase 25: the rules' totals first, then the slots beside the selected item's inspector.
            if (page != null)
            {
                var totals = Row("xk-cc-wrap");
                totals.name = "xk-eq-totals";
                totals.Add(StatTile("Armour class", page.ArmorClass.ToString(CultureInfo.InvariantCulture), page.ArmorClassText));
                totals.Add(StatTile("Attuned", page.Attuned + " / " + page.MaxAttuned, "Magic items you are attuned to, of the most you can be."));
                _content.Add(totals);
                _content.Add(Text(page.AttackText ?? "No weapon attack.", "xk-w-note"));
            }

            var md = Row("rk-md");
            var list = Column("rk-md-list");
            list.name = "xk-eq-list";
            foreach (EquipmentSlot slot in Enum.GetValues(typeof(EquipmentSlot)))
                if (slot != EquipmentSlot.None && Array.IndexOf(SlotOrder, slot) < 0) SlotRow(list, slot, page);
            foreach (var slot in SlotOrder) SlotRow(list, slot, page);
            list.Add(Text("What you wear shows on your character.", "xk-w-note"));
            md.Add(list);
            var detail = Column("rk-md-detail");
            detail.Add(Details(page) ?? Realmkeeper.UI.RkInspector.Empty("xk-eq-none", "Choose something you wear to inspect it."));
            md.Add(detail);
            _content.Add(md);
        }

        /// <summary>Phase 25: a large number with its label (and its explanation on hover).</summary>
        private static VisualElement StatTile(string label, string value, string tip)
        {
            var tile = Column("xk-cc-card");
            tile.Add(Text(value, "xk-cc-card-value"));
            tile.Add(Text(label, "xk-cc-card-label"));
            tile.tooltip = tip;
            return tile;
        }

        /// <summary>Phase 25: the icon of an equipment slot, shown when it is empty.</summary>
        private static string SlotIcon(EquipmentSlot slot) => slot switch
        {
            EquipmentSlot.MainHand => "weapon", EquipmentSlot.OffHand => "shield", EquipmentSlot.Head => "helmet", EquipmentSlot.Chest => "armor",
            EquipmentSlot.Feet => "boots", EquipmentSlot.Hands => "gloves", EquipmentSlot.Back => "clothing", EquipmentSlot.Neck => "amulet",
            EquipmentSlot.RingLeft => "ring", EquipmentSlot.RingRight => "ring", _ => "equipment",
        };

        private void SlotRow(VisualElement into, EquipmentSlot slot, InventoryPage page)
        {
            var s = slot;
            var row = Row("rk-row");
            row.AddToClassList("xk-w-row");
            row.name = "xk-hub-slot-" + slot;
            var frame = new VisualElement();
            frame.AddToClassList("rk-row-icon");
            var worn = page != null && page.Equipped.TryGetValue(slot, out var item0) ? item0 : null;
            frame.Add(Realmkeeper.UI.RkIcons.Make(worn != null ? ItemIcon(worn.ItemId) : SlotIcon(slot), "m", worn == null ? null : worn.IsMagic ? "info" : "accent"));
            row.Add(frame);
            var main = Column("rk-row-main");
            main.Add(Text(PlayerCommandHandlers.Slot(slot), "rk-row-sub"));
            if (worn != null)
            {
                var item = worn;
                row.EnableInClassList("rk-row--selected", item.StackId == _selected);
                var name = Text(item.Name, "rk-row-name");
                name.EnableInClassList("xk-inv-magic", item.IsMagic);
                main.Add(name);
                if (!string.IsNullOrEmpty(item.Badges)) main.Add(Text(item.Badges, "rk-row-sub"));
                row.Add(main);
                row.Add(Act("Unequip", () => UnequipRequested?.Invoke(s)));
                row.Add(Act("Inspect", () => SelectItem(item.StackId)));
                if (item.Attuned && !item.Bound) row.Add(Act("End attunement", () => Run(x => x.Dispatch(new EndAttunementCommand(item.StackId)))));
                if (item.RequiresAttunement && !item.Attuned) row.Add(Act("Attune", () => Run(x => x.Dispatch(StartWorkCommand.Attune(item.StackId)))));
                row.RegisterCallback<ClickEvent>(e => { if (!(e.target is Button)) SelectItem(item.StackId); });
                into.Add(row);
                return;
            }

            var none = Text(slot == EquipmentSlot.MainHand ? "Bare hands" : "Nothing worn", "rk-row-name");
            none.AddToClassList("xk-w-faint");
            main.Add(none);
            row.Add(main);
            into.Add(row);
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
                row.AddToClassList("rk-row");
                row.Add(IconFrame("crafting", r.CanStart ? "accent" : null)); // Phase 25
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
                    row.AddToClassList("rk-row");
                    row.Add(IconFrame(ItemIcon(r.ItemId), r.IsMagic ? "info" : null)); // Phase 25
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
