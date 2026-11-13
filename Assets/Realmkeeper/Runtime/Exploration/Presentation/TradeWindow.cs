using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// The trade window (Phase 5), opened at a shop's counter or from its keeper. It shows the shop as
    /// world data has it: whether it trades now (and why not), every item for sale with its stock and its
    /// price explained factor by factor, what the shop would pay for each thing the player carries (or why
    /// it will not buy it), the player's purse, how well the keeper knows them, and the shop's recent
    /// trades. Buying and selling raise events; the controller dispatches the commands and shows the
    /// result. Nothing here changes the world.
    /// </summary>
    public sealed class TradeWindow
    {
        private readonly VisualElement _forSale;
        private readonly VisualElement _offers;
        private readonly VisualElement _ledger;
        private readonly Label _supply; // Phase 21: what is missing and why; where the stock came from
        private readonly VisualElement _goods;
        private readonly VisualElement _repairs;
        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly Label _status;
        private readonly Label _purse;
        private readonly Label _detail;
        private readonly Label _message;
        private string _selectedItem;
        private ItemStackId _selectedStack;

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
            _subtitle = Text("-", "xk-w-subtitle");
            titles.Add(_title);
            titles.Add(_subtitle);
            head.Add(titles);
            _purse = Text("-", "xk-w-purse");
            head.Add(_purse);
            var close = new Button(() => CloseRequested?.Invoke()) { text = "Close (Esc)", name = "xk-trade-close" };
            close.AddToClassList("xk-button");
            head.Add(close);
            Root.Add(head);

            _status = Text("-", "xk-w-status");
            _status.name = "xk-trade-status";
            Root.Add(_status);

            var columns = Row("xk-w-columns");
            var left = Column("xk-w-col");
            left.Add(Text("For sale", "xk-w-heading"));
            _forSale = new ScrollView { name = "xk-trade-forsale" };
            _forSale.AddToClassList("xk-w-list");
            left.Add(_forSale);
            var right = Column("xk-w-col");
            right.Add(Text("Your goods: their offer", "xk-w-heading"));
            _offers = new ScrollView { name = "xk-trade-offers" };
            _offers.AddToClassList("xk-w-list");
            right.Add(_offers);
            columns.Add(left);
            columns.Add(right);
            Root.Add(columns);

            // Phase 11: particular items (worn, fine, part-charged) and an armourer's mending.
            var extra = Row("xk-w-columns");
            var goodsCol = Column("xk-w-col");
            goodsCol.Add(Text("Particular goods", "xk-w-heading"));
            _goods = new ScrollView { name = "xk-trade-goods" };
            _goods.AddToClassList("xk-w-list");
            goodsCol.Add(_goods);
            var repairCol = Column("xk-w-col");
            repairCol.Add(Text("Mending", "xk-w-heading"));
            _repairs = new ScrollView { name = "xk-trade-repairs" };
            _repairs.AddToClassList("xk-w-list");
            repairCol.Add(_repairs);
            extra.Add(goodsCol);
            extra.Add(repairCol);
            Root.Add(extra);

            _detail = Text("Select an item to see how its price is made.", "xk-w-detail");
            _detail.name = "xk-trade-detail";
            Root.Add(_detail);
            _message = Text(string.Empty, "xk-w-message");
            _message.name = "xk-trade-message";
            Root.Add(_message);
            Root.Add(Text("Recent trades here", "xk-w-heading"));
            _ledger = Column("xk-w-ledger");
            Root.Add(_ledger);
            _supply = Text(string.Empty, "xk-w-ledger-line");
            _supply.style.whiteSpace = WhiteSpace.Normal;
            Root.Add(_supply);
            hudRoot.Add(Root);
        }

        public VisualElement Root { get; }
        public bool IsOpen => !Root.ClassListContains("xk-hidden");
        public ShopDetail Detail { get; private set; }
        public string LastMessage => _message.text;

        public event Action<string, int> BuyRequested;
        public event Action<ItemStackId, int> SellRequested;
        public event Action CloseRequested;

        /// <summary>Phase 11: buy one particular item (stack, price shown).</summary>
        public event Action<ItemStackId, int> BuyGoodsRequested;

        /// <summary>Phase 11: have the armourer mend an item (stack, price shown).</summary>
        public event Action<ItemStackId, int> RepairRequested;

        public void Show(ShopDetail detail, string message = null, bool failed = false)
        {
            Detail = detail;
            Root.RemoveFromClassList("xk-hidden");
            if (detail == null) return;
            var s = detail.Summary;
            _title.text = s.Name;
            _subtitle.text = $"Kept by {s.KeeperName} · open {s.Hours} · {(detail.Familiarity > 0 ? "knows you (" + detail.Familiarity + ")" : "a stranger to you")}";
            _status.text = s.IsOpen ? s.Status : "Not trading: " + s.Status;
            _status.EnableInClassList("xk-w-status--closed", !s.IsOpen);
            _purse.text = detail.PlayerPurse + " coin";
            if (message != null)
            {
                _message.text = message;
                _message.EnableInClassList("xk-message--error", failed);
            }

            _forSale.Clear();
            if (detail.ForSale.Count == 0) _forSale.Add(Text("Nothing in stock.", "xk-w-empty"));
            foreach (var line in detail.ForSale)
            {
                var l = line;
                var row = ItemRow(l.Name, "×" + l.Quantity, l.UnitPrice + " c", s.IsOpen && l.UnitPrice <= detail.PlayerPurse, "Buy",
                    () => BuyRequested?.Invoke(l.ItemId, l.UnitPrice), () => Select(l));
                row.name = "xk-trade-sale-" + l.ItemId;
                if (l.ItemId == _selectedItem) row.AddToClassList("xk-w-row--selected");
                _forSale.Add(row);
            }

            _offers.Clear();
            if (detail.Offers.Count == 0) _offers.Add(Text("You carry nothing to sell.", "xk-w-empty"));
            foreach (var offer in detail.Offers)
            {
                var o = offer;
                var row = ItemRow(o.Name, "×" + o.Quantity, o.Accepted ? o.UnitPrice + " c" : "—", s.IsOpen && o.Accepted, "Sell",
                    () => SellRequested?.Invoke(o.StackId, o.UnitPrice), () => Select(o));
                row.name = "xk-trade-offer-" + o.ItemId;
                if (!o.Accepted) row.AddToClassList("xk-w-row--refused");
                _offers.Add(row);
            }

            _goods.Clear();
            if (detail.Goods.Count == 0) _goods.Add(Text("Nothing out of the ordinary.", "xk-w-empty"));
            foreach (var (g, price) in detail.Goods)
            {
                var id = g.StackId;
                var p = price;
                var row = ItemRow(g.Name + (string.IsNullOrEmpty(g.Badges) ? string.Empty : " (" + g.Badges + ")"), "×" + g.Quantity, p + " c", s.IsOpen && p <= detail.PlayerPurse, "Buy",
                    () => BuyGoodsRequested?.Invoke(id, p), () => _detail.text = g.Name + ": " + g.Badges + " — " + p + " coin.");
                row.name = "xk-trade-goods-" + g.ItemId;
                _goods.Add(row);
            }

            _repairs.Clear();
            if (detail.Repairs.Count == 0) _repairs.Add(Text(detail.Summary.Name + " has nothing of yours to mend (an armourer mends arms and armour).", "xk-w-empty"));
            foreach (var (r, price) in detail.Repairs)
            {
                var id = r.StackId;
                var p = price;
                var row = ItemRow(r.Name + " (" + Realmkeeper.Domain.World.ItemConditions.Name(r.Condition) + ")", string.Empty, p + " c", s.IsOpen && p <= detail.PlayerPurse, "Mend",
                    () => RepairRequested?.Invoke(id, p), () => _detail.text = r.Name + ": mended to good condition for " + p + " coin.");
                row.name = "xk-trade-repair-" + r.ItemId;
                _repairs.Add(row);
            }

            _ledger.Clear();
            if (detail.Ledger.Count == 0) _ledger.Add(Text("No trades with you yet.", "xk-w-empty"));
            for (var i = 0; i < detail.Ledger.Count && i < 4; i++) _ledger.Add(Text(detail.Ledger[i].Text, "xk-w-ledger-line"));

            // Phase 21 (ADR-0106): the trade, what it lacks and why (at most three), and its latest supplies (on hover, all).
            var lines = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(detail.Trade)) lines.Add(detail.Trade);
            for (var i = 0; i < detail.Shortfalls.Count && i < 3; i++) lines.Add("No " + detail.Shortfalls[i].Name.ToLowerInvariant() + ": " + detail.Shortfalls[i].Why);
            if (detail.Shortfalls.Count > 3) lines.Add($"… and {detail.Shortfalls.Count - 3} more out of stock.");
            if (detail.Supply.Count > 0) lines.Add("Latest in: " + detail.Supply[0]);
            _supply.text = string.Join("\n", lines);
            _supply.tooltip = detail.Supply.Count == 0 ? null : string.Join("\n", detail.Supply);
        }

        public void Hide()
        {
            Root.AddToClassList("xk-hidden");
            Detail = null;
            _message.text = string.Empty;
        }

        /// <summary>Shows how an item's price is made (as hovering its row does).</summary>
        public void Explain(string itemId)
        {
            if (Detail == null) return;
            foreach (var l in Detail.ForSale) if (l.ItemId == itemId) { Select(l); return; }
            foreach (var l in Detail.Offers) if (l.ItemId == itemId) { Select(l); return; }
        }

        public string Explanation => _detail.text;

        private void Select(PriceLine line)
        {
            _selectedItem = line.ItemId;
            _selectedStack = line.StackId;
            var item = line.Item;
            var stats = item == null ? string.Empty
                : item.Damage > 0 ? $" Damage {item.Damage}." : item.Protection > 0 ? $" Protection {item.Protection}." : item.Nourishment > 0 ? $" Nourishes {item.Nourishment}." : string.Empty;
            _detail.text = $"{line.Name}: {line.Explanation} → {(line.Accepted ? line.UnitPrice + " coin" : "no offer")}.{stats} {item?.Description}";
        }

        private static VisualElement ItemRow(string name, string quantity, string price, bool enabled, string action, Action act, Action select)
        {
            var row = Row("xk-w-row");
            var n = Text(name, "xk-w-name");
            var q = Text(quantity, "xk-w-qty");
            var p = Text(price, "xk-w-price");
            var b = new Button(act) { text = action };
            b.AddToClassList("xk-button");
            b.AddToClassList("xk-w-act");
            b.SetEnabled(enabled);
            row.Add(n);
            row.Add(q);
            row.Add(p);
            row.Add(b);
            row.RegisterCallback<ClickEvent>(_ => select());
            row.RegisterCallback<MouseEnterEvent>(_ => select());
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
