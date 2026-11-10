using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Commerce;
using Realmkeeper.Simulation.Npcs;

namespace Realmkeeper.Application
{
    /// <summary>
    /// Phase 5 conversation topics. Answers are bound to what the speaker can know: residents know the
    /// shops of their own settlement (where they are and when they open, as anyone living there
    /// would); only a shopkeeper knows their own stock and exact prices; others speak of prices only
    /// from what they know of food and fortunes; faction talk comes from their knowledge and
    /// memories. Fixed templates only: no generated dialogue.
    /// </summary>
    internal static class TradeTalk
    {
        private static string I(int v) => v.ToString(CultureInfo.InvariantCulture);

        private static ShopData Kept(WorldData world, NpcData npc)
        {
            foreach (var s in world.Shops) if (s.ShopkeeperNpcId == npc.NpcId) return s;
            return null;
        }

        private static bool Local(NpcData npc, SettlementData here) => here != null && npc.HomeSettlementId == here.SettlementId;

        public static string Shops(WorldData world, NpcData npc, SettlementData here)
        {
            if (here == null) return "There's no trade out here.";
            if (!Local(npc, here)) return "I'm not from " + here.Name + "; I couldn't tell you where to buy anything.";
            var parts = new List<string>();
            foreach (var s in world.Shops)
            {
                if (s.SettlementId != here.SettlementId) continue;
                var keeper = world.FindNpc(s.ShopkeeperNpcId);
                var mine = keeper != null && keeper.NpcId == npc.NpcId;
                parts.Add((mine ? "my own " + s.Name : s.Name + (keeper != null && keeper.IsDeceased ? ", shut since " + keeper.Name + " died" : string.Empty)) +
                          " (" + WorldTime.Clock(s.OpensAt) + "–" + WorldTime.Clock(s.ClosesAt) + ")");
            }

            if (parts.Count == 0) return "Nobody keeps a shop in " + here.Name + ". You'd do better in a town.";
            return "In " + here.Name + " there's " + string.Join("; ", parts) + ".";
        }

        public static string Goods(WorldData world, NpcData npc, SettlementData here)
        {
            var mine = Kept(world, npc);
            if (mine != null)
            {
                if (mine.Stock.Count == 0) return "My shelves are bare just now. Come back after I've restocked.";
                var lines = new List<(int q, string name)>();
                foreach (var line in mine.Stock) lines.Add((line.Quantity, ItemCatalog.Find(line.ItemId)?.Name ?? line.ItemId));
                lines.Sort((a, b) => b.q != a.q ? b.q.CompareTo(a.q) : string.CompareOrdinal(a.name, b.name));
                var shown = new List<string>();
                for (var i = 0; i < lines.Count && i < 5; i++) shown.Add(I(lines[i].q) + " " + Plural(lines[i].name.ToLowerInvariant(), lines[i].q));
                return "I have " + string.Join(", ", shown) + (lines.Count > 5 ? ", and a few other things" : string.Empty) + ". Have a look.";
            }

            if (here == null || !Local(npc, here)) return "I don't know what's to be had here.";
            var dealers = new List<string>();
            foreach (var s in world.Shops)
            {
                if (s.SettlementId != here.SettlementId) continue;
                var kinds = new List<string>();
                foreach (var c in s.Categories) kinds.Add(Kind(c));
                dealers.Add(s.Name + " for " + string.Join(" and ", kinds));
            }

            if (dealers.Count == 0) return "There's nothing for sale here, not in any shop.";
            return "Try " + string.Join("; ", dealers) + ". What they have in today, you'd have to ask them.";
        }

        public static string Prices(WorldData world, NpcData npc, SettlementData here)
        {
            var mine = Kept(world, npc);
            if (mine != null)
            {
                var quotes = new List<string>();
                foreach (var line in mine.Stock)
                {
                    var d = ItemCatalog.Find(line.ItemId);
                    if (d == null) continue;
                    quotes.Add(d.Name.ToLowerInvariant() + " " + I(CommerceMath.SellingPrice(world, mine, d).UnitPrice));
                    if (quotes.Count == 4) break;
                }

                if (quotes.Count == 0) return "Nothing to price; I'm sold out.";
                return "Fair prices: " + string.Join(", ", quotes) + " coin. Ask about anything else at the counter.";
            }

            if (here == null) return "Prices? Depends where you are.";
            var food = NpcMind.Find(npc, KnowledgeTopic.SettlementFood, here.SettlementId.ToString());
            var state = NpcMind.Find(npc, KnowledgeTopic.SettlementProsperity, here.SettlementId.ToString());
            if (food == null && state == null) return "I couldn't tell you what things cost here.";
            var said = new List<string>();
            if (food != null) said.Add(NpcDialogue.Hedge(food) + (food.Value == "plenty" ? "bread is cheap enough" : "bread's dear, with food so short"));
            if (state != null)
                said.Add(state.Value == "thriving" ? "folk here can afford to pay more for things"
                    : state.Value == "struggling" ? "merchants here have to sell cheap" : "prices are about what you'd expect");
            return NpcDialogue.Capital(string.Join("; ", said)) + ".";
        }

        public static string Factions(WorldData world, NpcData npc, SettlementData here)
        {
            NpcMemoryData best = null;
            foreach (var m in npc.Memories)
                if (m.Category == MemoryCategory.Politics && (best == null || m.Salience > best.Salience || m.Salience == best.Salience && m.Date > best.Date)) best = m;
            var said = new List<string>();
            if (here != null)
            {
                var gov = NpcMind.Find(npc, KnowledgeTopic.SettlementGovernor, here.SettlementId.ToString());
                if (gov != null && !string.IsNullOrEmpty(gov.Value)) said.Add(NpcDialogue.Hedge(gov) + NpcDialogue.FactionName(world, gov.Value) + " holds " + here.Name);
            }

            if (!npc.FactionId.IsEmpty) said.Add("I'm with " + NpcDialogue.FactionName(world, npc.FactionId.ToString()) + " myself");
            if (best != null) said.Add("the last I heard, " + NpcDialogue.NewsText(world, best));
            if (said.Count == 0) return "Politics is for others. I don't follow the factions.";
            return NpcDialogue.Capital(string.Join("; ", said)) + ".";
        }

        public static string Work(WorldData world, NpcData npc)
        {
            var role = npc.Role.ToString().ToLowerInvariant();
            var work = world.FindLocation(npc.WorkplaceId);
            var mine = Kept(world, npc);
            var (start, end) = NpcActivityPlan.WorkHours(world, npc);
            var now = NpcActivityPlan.Now(world, npc);
            var where = work == null ? "I've no fixed place of work" : mine != null ? "I keep " + mine.Name : "I work at the " + work.Name;
            var doing = now.Activity == ActivityKind.Working || now.Activity == ActivityKind.Trading || now.Activity == ActivityKind.Guarding
                ? " I'm at it now." : now.Activity == ActivityKind.Eating ? " Just stopped to eat." : string.Empty;
            return "I'm a " + role + ". " + where + (work == null ? "." : ", " + WorldTime.Clock(start) + " to " + WorldTime.Clock(end) + " most days.") + doing;
        }

        /// <summary>"2 felt hats", "3 loaves of bread", "2 leather bracers".</summary>
        internal static string Plural(string name, int count)
        {
            if (count == 1 || string.IsNullOrEmpty(name)) return name;
            var of = name.IndexOf(" of ", System.StringComparison.Ordinal);
            var head = of > 0 ? name.Substring(0, of) : name;
            var tail = of > 0 ? name.Substring(of) : string.Empty;
            if (head.EndsWith("s")) return name;
            head = head.EndsWith("f") ? head.Substring(0, head.Length - 1) + "ves" : head.EndsWith("ch") || head.EndsWith("sh") ? head + "es" : head + "s";
            return head + tail;
        }

        private static string Kind(ShopCategory c)
        {
            switch (c)
            {
                case ShopCategory.General: return "general goods";
                case ShopCategory.WeaponsAndArmour: return "arms and armour";
                case ShopCategory.Clothing: return "clothing";
                case ShopCategory.Provisions: return "food";
                case ShopCategory.Materials: return "building materials";
                default: return "goods";
            }
        }
    }
}
