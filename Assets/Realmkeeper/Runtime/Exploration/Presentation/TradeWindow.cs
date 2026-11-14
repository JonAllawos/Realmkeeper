using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.UI;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The trade window (Phase 5; Phase 26 rebuilt on the shared list-and-inspector pattern), opened at a shop's counter
    /// or from its keeper. It shows the shop as world data has it:
    /// <list type="bullet">
    /// <item><b>Four pages:</b> Buy (the stock), Sell (what the player carries, with the shop's offer or its reason for
    /// refusing), Particular goods (single worn, fine or part-charged items) and Mending.</item>
    /// <item><b>The selected line's inspector:</b> the price made factor by factor; the item's rules; a comparison with
    /// what is worn (the rules engine, on a copy); a quantity bounded by stock, purse and what is carried; the total and
    /// the purse after; one confirm button that says why when it cannot be used.</item>
    /// <item><b>The shop:</b> its trade, what it lacks and why, its latest supplies, and the recent trades with the player.</item>
    /// </list>
    /// Buying and selling raise events; the controller dispatches the existing commands and shows the result. Nothing here
    /// changes the world. All four lists stay in the tree (only the current page shows), so a row can always be found by
    /// its name.
    /// </summary>
    public sealed class TradeWindow
    {
        public static readonly string[] Pages = { "Buy", "Sell", "Particular goods", "Mending" };

        private readonly VisualElement _forSale;
        private readonly VisualElement _offers;
        private readonly VisualElement _ledger;
        private readonly Label _supply;
        private readonly VisualElement _goods;
        private readonly VisualElement _repairs;
        private readonly VisualElement _tabs;
        private readonly VisualElement _inspector;
        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly Label _status;
        private readonly Label _purse;
        private readonly Label _detail;
        private readonly Label _message;
        private string _page = "Buy";
        private string _selectedItem;
        private ItemStackId _selectedStack;
        private string _selectedKind;
        private int _quantity = 1;

        public TradeWindow(VisualElement hudRoot)
        {
            Root = new VisualElement { name = "xk-trade" };
            Root.AddToClassList("xk-window");
            Root.AddToClassList("xk-trade");
            Root.AddToClassList("xk-hidden");

            var head = Row("xk-w-head");
            var titles = new VisualElement();
            titles.style.flexGrow = 1;
            _title = Text("-", "xk-w-title");
            _title.AddToClassList("rk-serif");
            _subtitle = Text("-", "xk-w-subtitle");
            titles.Add(_title);
            titles.Add(_subtitle);
            head.Add(titles);
            _purse = Text("-", "xk-w-purse");
            _purse.name = "xk-trade-purse";
            RkIcons.Lead(_purse, "currency", "m", "accent");
            _purse.tooltip = "Your purse.";
            head.Add(_purse);
            var close = new Button(() => CloseRequested?.Invoke()) { text = RkText.Get("common.close.esc"), name = "xk-trade-close" };
            close.AddToClassList("xk-button");
            head.Add(close);
            Root.Add(head);

            _status = Text("-", "xk-w-status");
            _status.name = "xk-trade-status";
            Root.Add(_status);

            var md = Row("rk-md");
            md.AddToClassList("xk-trade-body");
            var left = Column("rk-md-list");
            _tabs = Row("rk-tabs");
            _tabs.name = "xk-trade-tabs";
            left.Add(_tabs);
            _forSale = List(left, "xk-trade-forsale");
            _offers = List(left, "xk-trade-offers");
            _goods = List(left, "xk-trade-goods");
            _repairs = List(left, "xk-trade-repairs");
            md.Add(left);

            // The inspector and the shop scroll together, so a long inspector never runs under the shop's notes.
            var right = new ScrollView(ScrollViewMode.Vertical) { name = "xk-trade-side" };
            right.AddToClassList("rk-md-detail");
            right.AddToClassList("xk-trade-side");
            right.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            _inspector = new VisualElement { name = "xk-trade-inspector" };
            _inspector.style.flexShrink = 0;
            right.Add(_inspector);
            _detail = Text(RkText.Get("trade.pick"), "rk-inspector-text");
            _detail.name = "xk-trade-detail";
            var shop = Column("rk-inspector");
            shop.name = "xk-trade-shop";
            shop.AddToClassList("xk-trade-shop");
            shop.Add(Text(RkText.Get("trade.shop"), "rk-inspector-section"));
            _supply = Text(string.Empty, "rk-inspector-text");
            _supply.style.whiteSpace = WhiteSpace.Normal;
            shop.Add(_supply);
            shop.Add(Text(RkText.Get("trade.recent"), "rk-inspector-section"));
            _ledger = Column("xk-w-ledger");
            shop.Add(_ledger);
            right.Add(shop);
            md.Add(right);
            Root.Add(md);

            _message = Text(string.Empty, "xk-w-message");
            _message.name = "xk-trade-message";
            Root.Add(_message);
            hudRoot.Add(Root);
        }

        private static VisualElement List(VisualElement parent, string name)
        {
            var list = new ScrollView { name = name };
            list.AddToClassList("xk-w-list");
            list.AddToClassList("xk-trade-list");
            parent.Add(list);
            return list;
        }

        public VisualElement Root { get; }
        public bool IsOpen => !Root.ClassListContains("xk-hidden");
        public ShopDetail Detail { get; private set; }
        public string LastMessage => _message.text;
        public string Page => _page;
        public int Quantity => _quantity;

        /// <summary>Phase 26: the rules engine's comparison for a shop's item (catalogue id) with what is worn.</summary>
        public Func<string, ItemComparison> CompareCatalogSource { get; set; }

        /// <summary>Phase 26: the same for an item the player carries (an offer).</summary>
        public Func<ItemStackId, ItemComparison> CompareOwnedSource { get; set; }

        public event Action<string, int> BuyRequested;
        public event Action<ItemStackId, int> SellRequested;
        public event Action CloseRequested;

        /// <summary>Phase 26: buy several of a stock line (item, quantity, unit price shown).</summary>
        public event Action<string, int, int> BuyQuantityRequested;

        /// <summary>Phase 26: sell several of a stack (stack, quantity, unit price shown).</summary>
        public event Action<ItemStackId, int, int> SellQuantityRequested;

        /// <summary>Phase 11: buy one particular item (stack, price shown).</summary>
        public event Action<ItemStackId, int> BuyGoodsRequested;

        /// <summary>Phase 11: have the armourer mend an item (stack, price shown).</summary>
        public event Action<ItemStackId, int> RepairRequested;

        public void SelectPage(string page)
        {
            if (Array.IndexOf(Pages, page) < 0) return;
            _page = page;
            Show(Detail);
        }

        public void Show(ShopDetail detail, string message = null, bool failed = false)
        {
            Detail = detail;
            Root.RemoveFromClassList("xk-hidden");
            if (detail == null) return;
            var s = detail.Summary;
            _title.text = s.Name;
            _subtitle.text = RkText.Format("trade.subtitle", s.KeeperName, s.Hours, detail.Familiarity > 0 ? RkText.Format("trade.knows", detail.Familiarity) : RkText.Get("trade.stranger"));
            _status.text = s.IsOpen ? s.Status : RkText.Get("trade.closed") + s.Status;
            _status.EnableInClassList("xk-w-status--closed", !s.IsOpen);
            _purse.text = RkText.Format("common.coin", detail.PlayerPurse);
            if (message != null)
            {
                _message.text = message;
                _message.EnableInClassList("xk-message--error", failed);
            }

            // Pages with their counts.
            _tabs.Clear();
            var counts = new[] { detail.ForSale.Count, detail.Offers.Count, detail.Goods.Count, detail.Repairs.Count };
            for (var i = 0; i < Pages.Length; i++)
            {
                var page = Pages[i];
                var tab = new Button(() => SelectPage(page)) { text = $"{page} ({counts[i]})", name = "xk-trade-tab-" + i };
                tab.AddToClassList("rk-tab");
                tab.EnableInClassList("rk-tab--active", _page == page);
                RkIcons.Lead(tab, i == 0 ? "trade" : i == 1 ? "currency" : i == 2 ? "inventory" : "crafting", "s");
                _tabs.Add(tab);
            }

            _forSale.EnableInClassList("xk-hidden", _page != Pages[0]);
            _offers.EnableInClassList("xk-hidden", _page != Pages[1]);
            _goods.EnableInClassList("xk-hidden", _page != Pages[2]);
            _repairs.EnableInClassList("xk-hidden", _page != Pages[3]);

            _forSale.Clear();
            if (detail.ForSale.Count == 0) _forSale.Add(Text(RkText.Get("trade.nostock"), "rk-empty"));
            foreach (var line in detail.ForSale)
            {
                var l = line;
                var canPay = l.UnitPrice <= detail.PlayerPurse;
                var row = ItemRow(l.ItemId, l.Name, CategoryOf(l.Item), "×" + l.Quantity, l.UnitPrice + " c", s.IsOpen && canPay, RkText.Get("trade.buy"),
                    () => BuyRequested?.Invoke(l.ItemId, l.UnitPrice), () => Select("sale", l.ItemId, l.StackId),
                    !s.IsOpen ? RkText.Get("trade.notnow") : !canPay ? RkText.Format("trade.short", l.UnitPrice - detail.PlayerPurse) : null);
                row.name = "xk-trade-sale-" + l.ItemId;
                row.EnableInClassList("rk-row--selected", _selectedKind == "sale" && l.ItemId == _selectedItem);
                _forSale.Add(row);
            }

            _offers.Clear();
            if (detail.Offers.Count == 0) _offers.Add(Text(RkText.Get("trade.nothingtosell"), "rk-empty"));
            foreach (var offer in detail.Offers)
            {
                var o = offer;
                var row = ItemRow(o.ItemId, o.Name, CategoryOf(o.Item), "×" + o.Quantity, o.Accepted ? o.UnitPrice + " c" : "—", s.IsOpen && o.Accepted, RkText.Get("trade.sell"),
                    () => SellRequested?.Invoke(o.StackId, o.UnitPrice), () => Select("offer", o.ItemId, o.StackId),
                    o.Refusal ?? (!s.IsOpen ? RkText.Get("trade.notnow") : null));
                row.name = "xk-trade-offer-" + o.ItemId;
                if (!o.Accepted) row.AddToClassList("xk-w-row--refused");
                row.EnableInClassList("rk-row--selected", _selectedKind == "offer" && o.StackId == _selectedStack);
                _offers.Add(row);
            }

            _goods.Clear();
            if (detail.Goods.Count == 0) _goods.Add(Text(RkText.Get("trade.noparticular"), "rk-empty"));
            foreach (var (g, price) in detail.Goods)
            {
                var id = g.StackId;
                var p = price;
                var gg = g;
                var row = ItemRow(g.ItemId, g.Name + (string.IsNullOrEmpty(g.Badges) ? string.Empty : " (" + g.Badges + ")"), g.Category, "×" + g.Quantity, p + " c", s.IsOpen && p <= detail.PlayerPurse, RkText.Get("trade.buy"),
                    () => BuyGoodsRequested?.Invoke(id, p), () => SelectGoods(gg, p), p > detail.PlayerPurse ? RkText.Format("trade.short", p - detail.PlayerPurse) : null);
                row.name = "xk-trade-goods-" + g.ItemId;
                _goods.Add(row);
            }

            _repairs.Clear();
            if (detail.Repairs.Count == 0) _repairs.Add(Text(RkText.Format("trade.norepairs", detail.Summary.Name), "rk-empty"));
            foreach (var (r, price) in detail.Repairs)
            {
                var id = r.StackId;
                var p = price;
                var rr = r;
                var row = ItemRow(r.ItemId, r.Name + " (" + ItemConditions.Name(r.Condition) + ")", r.Category, string.Empty, p + " c", s.IsOpen && p <= detail.PlayerPurse, RkText.Get("trade.mend"),
                    () => RepairRequested?.Invoke(id, p), () => SelectRepair(rr, p), p > detail.PlayerPurse ? RkText.Format("trade.short", p - detail.PlayerPurse) : null);
                row.name = "xk-trade-repair-" + r.ItemId;
                _repairs.Add(row);
            }

            _ledger.Clear();
            if (detail.Ledger.Count == 0) _ledger.Add(Text(RkText.Get("trade.notrades"), "rk-hint"));
            for (var i = 0; i < detail.Ledger.Count && i < 4; i++) _ledger.Add(Text(detail.Ledger[i].Text, "rk-inspector-text"));

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(detail.Trade)) lines.Add(detail.Trade);
            for (var i = 0; i < detail.Shortfalls.Count && i < 3; i++) lines.Add("No " + detail.Shortfalls[i].Name.ToLowerInvariant() + ": " + detail.Shortfalls[i].Why);
            if (detail.Shortfalls.Count > 3) lines.Add($"… and {detail.Shortfalls.Count - 3} more out of stock.");
            if (detail.Supply.Count > 0) lines.Add("Latest in: " + detail.Supply[0]);
            _supply.text = string.Join("\n", lines);
            _supply.tooltip = detail.Supply.Count == 0 ? null : string.Join("\n", detail.Supply);
            RenderInspector();
        }

        public void Hide()
        {
            Root.AddToClassList("xk-hidden");
            Detail = null;
            _message.text = string.Empty;
        }

        /// <summary>Shows how an item's price is made (as selecting its row does).</summary>
        public void Explain(string itemId)
        {
            if (Detail == null) return;
            foreach (var l in Detail.ForSale) if (l.ItemId == itemId) { Select("sale", l.ItemId, l.StackId); return; }
            foreach (var l in Detail.Offers) if (l.ItemId == itemId) { Select("offer", l.ItemId, l.StackId); return; }
        }

        public string Explanation => _detail.text;

        /// <summary>Phase 26: selects a stock line or an offer and shows it in the inspector.</summary>
        public void Select(string kind, string itemId, ItemStackId stack)
        {
            if (kind != _selectedKind || itemId != _selectedItem || stack != _selectedStack) _quantity = 1;
            _selectedKind = kind;
            _selectedItem = itemId;
            _selectedStack = stack;
            if (kind == "sale") _page = Pages[0];
            else if (kind == "offer") _page = Pages[1];
            Show(Detail);
        }

        public void SetQuantity(int quantity)
        {
            _quantity = Math.Max(1, quantity);
            RenderInspector();
        }

        private (InventoryRow Row, int Price) _goodsPick;
        private (InventoryRow Row, int Price) _repairPick;

        private void SelectGoods((InventoryRow, int) pick)
        {
            _selectedKind = "goods";
            _goodsPick = pick;
            _selectedItem = pick.Item1.ItemId;
            _detail.text = pick.Item1.Name + ": " + pick.Item1.Badges + " — " + pick.Item2 + " coin.";
            RenderInspector();
        }

        private void SelectGoods(InventoryRow row, int price) => SelectGoods((row, price));

        private void SelectRepair(InventoryRow row, int price)
        {
            _selectedKind = "repair";
            _repairPick = (row, price);
            _selectedItem = row.ItemId;
            _detail.text = row.Name + ": mended to good condition for " + price + " coin.";
            RenderInspector();
        }

        private PriceLine CurrentLine()
        {
            if (Detail == null) return null;
            if (_selectedKind == "sale") foreach (var l in Detail.ForSale) if (l.ItemId == _selectedItem) return l;
            if (_selectedKind == "offer") foreach (var l in Detail.Offers) if (l.StackId == _selectedStack) return l;
            return null;
        }

        private void RenderInspector()
        {
            _inspector.Clear();
            var d = Detail;
            if (d == null) return;
            var open = d.Summary.IsOpen;
            if (_selectedKind == "goods" && _goodsPick.Row != null)
            {
                var (g, price) = _goodsPick;
                var card = new RkInspector("xk-trade-card").Header(PlayerHubWindow.ItemIcon(g.ItemId), g.Name, g.Category, g.IsMagic ? "info" : "accent");
                if (!string.IsNullOrEmpty(g.Badges)) card.Badge(g.Badges, "warn");
                card.Stat(RkText.Get("trade.price"), price + " coin").Stat(RkText.Get("trade.purseafter"), (d.PlayerPurse - price) + " coin");
                card.Root.Add(_detail);
                card.Action(RkText.Get("trade.buy"), "xk-trade-confirm", () => BuyGoodsRequested?.Invoke(g.StackId, price), open && price <= d.PlayerPurse,
                    !open ? RkText.Get("trade.notnow") : RkText.Format("trade.short", price - d.PlayerPurse), true);
                _inspector.Add(card.Root);
                return;
            }

            if (_selectedKind == "repair" && _repairPick.Row != null)
            {
                var (r, price) = _repairPick;
                var card = new RkInspector("xk-trade-card").Header(PlayerHubWindow.ItemIcon(r.ItemId), r.Name, ItemConditions.Name(r.Condition), "warn");
                card.Stat(RkText.Get("trade.price"), price + " coin").Stat(RkText.Get("trade.purseafter"), (d.PlayerPurse - price) + " coin");
                card.Root.Add(_detail);
                card.Action(RkText.Get("trade.mend"), "xk-trade-confirm", () => RepairRequested?.Invoke(r.StackId, price), open && price <= d.PlayerPurse,
                    !open ? RkText.Get("trade.notnow") : RkText.Format("trade.short", price - d.PlayerPurse), true);
                _inspector.Add(card.Root);
                return;
            }

            var line = CurrentLine();
            if (line == null)
            {
                _detail.text = RkText.Get("trade.pick");
                var empty = RkInspector.Empty("xk-trade-none", RkText.Get("trade.pick"));
                _inspector.Add(empty);
                return;
            }

            var buying = _selectedKind == "sale";
            var item = line.Item;
            var stats = item == null ? string.Empty
                : item.Damage > 0 ? $" Damage {item.Damage}." : item.Protection > 0 ? $" Protection {item.Protection}." : item.Nourishment > 0 ? $" Nourishes {item.Nourishment}." : string.Empty;
            _detail.text = $"{line.Name}: {line.Explanation} → {(line.Accepted ? line.UnitPrice + " coin" : "no offer")}.{stats} {item?.Description}";

            var c = new RkInspector("xk-trade-card");
            c.Header(PlayerHubWindow.ItemIcon(line.ItemId), line.Name, CategoryOf(item), "accent");
            c.Badge(buying ? RkText.Format("trade.instock", line.Quantity) : RkText.Format("trade.youhave", line.Quantity), "info");
            if (!line.Accepted) c.Note(RkText.Get("trade.refused") + line.Refusal, "warn");
            c.Section(RkText.Get("trade.pricesection"));
            c.Stat(RkText.Get("trade.base"), line.BaseValue + " coin");
            if (line.Factors != null) foreach (var f in line.Factors) c.Text("· " + f, "rk-hint");
            if (line.Accepted) c.Stat(RkText.Get(buying ? "trade.unitbuy" : "trade.unitsell"), line.UnitPrice + " coin");
            if (item != null)
            {
                if (item.Damage > 0) c.Stat(RkText.Get("trade.damage"), item.Damage.ToString());
                if (item.Protection > 0) c.Stat(RkText.Get("trade.protection"), item.Protection.ToString());
                if (item.Nourishment > 0) c.Stat(RkText.Get("trade.nourishes"), item.Nourishment.ToString());
            }

            var cmp = buying ? CompareCatalogSource?.Invoke(line.ItemId) : CompareOwnedSource?.Invoke(line.StackId);
            if (cmp != null && cmp.Lines.Count > 0)
            {
                c.Section(RkText.Format("trade.ifworn", PlayerCommandHandlers.Slot(cmp.Slot).ToUpperInvariant(), (cmp.Current ?? "nothing").ToUpperInvariant()));
                var table = new VisualElement { name = "xk-trade-compare" };
                foreach (var l in cmp.Lines)
                {
                    var one = new RkInspector("xk-trade-compare-line");
                    one.Compare(l.Label, l.Before, l.After, l.Better);
                    foreach (var child in new List<VisualElement>(one.Root.Children())) table.Add(child);
                }

                foreach (var n in cmp.Notes) table.Add(Text(n, "rk-inspector-note"));
                c.Root.Add(table);
            }
            else if (cmp != null) foreach (var n in cmp.Notes) c.Note(n, null);

            if (!string.IsNullOrEmpty(item?.Description)) { c.Section(RkText.Get("trade.description")); c.Text(item.Description); }

            // Quantity, total, purse after.
            var max = !line.Accepted ? 0 : buying ? Math.Min(line.Quantity, line.UnitPrice <= 0 ? line.Quantity : d.PlayerPurse / line.UnitPrice) : line.Quantity;
            _quantity = Math.Max(1, Math.Min(_quantity, Math.Max(1, max)));
            var total = _quantity * line.UnitPrice;
            if (line.Accepted)
            {
                c.Section(RkText.Get("trade.quantity"));
                var stepper = Row("xk-trade-stepper");
                stepper.name = "xk-trade-stepper";
                var minus = new Button(() => SetQuantity(_quantity - 1)) { text = "−", name = "xk-trade-qty-minus" };
                minus.AddToClassList("rk-btn");
                minus.SetEnabled(_quantity > 1);
                var count = Text(_quantity.ToString(), "xk-trade-qty");
                count.name = "xk-trade-qty";
                var plus = new Button(() => SetQuantity(_quantity + 1)) { text = "+", name = "xk-trade-qty-plus" };
                plus.AddToClassList("rk-btn");
                plus.SetEnabled(_quantity < max);
                var all = new Button(() => SetQuantity(max)) { text = RkText.Format("trade.max", Math.Max(1, max)), name = "xk-trade-qty-max" };
                all.AddToClassList("rk-btn");
                all.AddToClassList("rk-btn--quiet");
                all.SetEnabled(max > 1 && _quantity < max);
                stepper.Add(minus);
                stepper.Add(count);
                stepper.Add(plus);
                stepper.Add(all);
                c.Root.Add(stepper);
                c.Stat(RkText.Get("trade.total"), total + " coin");
                c.Stat(RkText.Get("trade.purseafter"), (buying ? d.PlayerPurse - total : d.PlayerPurse + total) + " coin");
            }

            c.Root.Add(_detail);
            _detail.style.display = DisplayStyle.None; // kept for Explanation; the inspector says it in rows
            string why = null;
            if (!open) why = RkText.Get("trade.notnow");
            else if (!line.Accepted) why = line.Refusal;
            else if (buying && total > d.PlayerPurse) why = RkText.Format("trade.short", total - d.PlayerPurse);
            var qty = _quantity;
            var unitPrice = line.UnitPrice;
            var itemId = line.ItemId;
            var stackId = line.StackId;
            if (buying)
                c.Action(RkText.Format("trade.buyfor", qty, total), "xk-trade-confirm", () => BuyQuantityRequested?.Invoke(itemId, qty, unitPrice), why == null, why, true);
            else
                c.Action(RkText.Format("trade.sellfor", qty, total), "xk-trade-confirm", () => SellQuantityRequested?.Invoke(stackId, qty, unitPrice), why == null, why, true);
            _inspector.Add(c.Root);
        }

        private static string CategoryOf(ItemView item) => item == null ? null : ItemCategories.Of(item.Category)?.Name;

        private static VisualElement ItemRow(string itemId, string name, string category, string quantity, string price, bool enabled, string action, Action act, Action select, string why)
        {
            var row = Row("rk-row");
            row.AddToClassList("xk-w-row");
            row.Add(PlayerHubWindow.IconFrame(PlayerHubWindow.ItemIcon(itemId)));
            var main = Column("rk-row-main");
            var n = Text(name, "rk-row-name");
            n.AddToClassList("xk-w-name");
            main.Add(n);
            var sub = why ?? category;
            if (!string.IsNullOrEmpty(sub))
            {
                var s = Text(sub, "rk-row-sub");
                if (why != null) s.AddToClassList("xk-trade-why");
                main.Add(s);
            }

            row.Add(main);
            row.Add(Text(quantity, "rk-row-cell"));
            row.Add(Text(price, "rk-row-cell"));
            var b = new Button(act) { text = action };
            b.AddToClassList("xk-button");
            b.AddToClassList("xk-w-act");
            b.SetEnabled(enabled);
            if (!enabled && why != null) b.tooltip = why;
            row.Add(b);
            row.RegisterCallback<ClickEvent>(e => { if (!(e.target is Button)) select(); });
            return row;
        }

        internal static Label Text(string text, string cls)
        {
            var l = new Label(text);
            l.AddToClassList(cls);
            return l;
        }

        internal static VisualElement Row(string cls)
        {
            var v = new VisualElement();
            v.AddToClassList(cls);
            v.style.flexDirection = FlexDirection.Row;
            return v;
        }

        internal static VisualElement Column(string cls)
        {
            var v = new VisualElement();
            v.AddToClassList(cls);
            return v;
        }
    }
}
