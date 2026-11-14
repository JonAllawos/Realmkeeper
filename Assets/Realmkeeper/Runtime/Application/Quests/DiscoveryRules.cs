using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application
{
    /// <summary>
    /// Phase 18 (ADR-0088): what a discovery is worth to the people of the world. Each kind of discovery matters to some
    /// kinds of people — history to an elder, danger to a guard, trade news to a merchant, signs of beasts to a farmer, a
    /// faction's business to those who watch it — and they will hear it from the player, once per settlement. What they
    /// give back is what such a person has: standing in their settlement, their faction's regard, a little coin for news
    /// that pays, their own thanks. Major discoveries are worth more. Nothing is invented: the player tells what they found.
    /// </summary>
    public static class DiscoveryRules
    {
        /// <summary>The most findings one person is offered in a conversation's opening.</summary>
        public const int MaxOffered = 2;

        /// <summary>How much this person cares to hear it (0: not at all).</summary>
        public static int Interest(WorldData w, NpcData npc, DiscoveryData d) =>
            npc == null ? 0 : Interest(npc, d, npc.HomeSettlementId.ToString());

        /// <summary>As <see cref="Interest(WorldData, NpcData, DiscoveryData)"/>, with the person's home already formatted (called per discovery).</summary>
        private static int Interest(NpcData npc, DiscoveryData d, string home)
        {
            if (npc == null || d == null || npc.IsDeceased || npc.HomeSettlementId.IsEmpty) return 0;
            if (d.SharedIn.Contains(home)) return 0;
            if (d.Certainty == DiscoveryCertainty.Unverified && !d.Major) return 0; // a guess is not worth passing on
            switch (d.Category)
            {
                case DiscoveryCategory.Historical:
                case DiscoveryCategory.Cultural:
                    return npc.Role == NpcRole.Elder ? 3 : npc.Role == NpcRole.Innkeeper ? 2 : 0;
                case DiscoveryCategory.Religious:
                case DiscoveryCategory.Arcane:
                    return npc.Role == NpcRole.Elder ? 3 : 0;
                case DiscoveryCategory.Faction:
                    if (npc.FactionId.IsEmpty || d.Subject == "faction:" + npc.FactionId) return 0; // their own faction's business is no news to them
                    return npc.Role == NpcRole.Guard ? 3 : npc.Role == NpcRole.Elder ? 2 : 0;
                case DiscoveryCategory.Economic:
                    return npc.Role == NpcRole.Merchant ? 3 : npc.Role == NpcRole.Innkeeper ? 2 : npc.Role == NpcRole.Smith ? 1 : 0;
                case DiscoveryCategory.Geographic:
                    return npc.Role == NpcRole.Merchant || npc.Role == NpcRole.Guard ? 2 : 0;
                case DiscoveryCategory.Natural:
                    return npc.Role == NpcRole.Farmer ? 3 : npc.Role == NpcRole.Labourer ? 2 : 0;
                case DiscoveryCategory.Dangerous:
                    return npc.Role == NpcRole.Guard ? 3 : npc.Role == NpcRole.Elder ? 2 : npc.Role == NpcRole.Farmer ? 1 : 0;
                case DiscoveryCategory.Personal:
                    return npc.Role == NpcRole.Elder ? 1 : 0;
                default:
                    return 0;
            }
        }

        /// <summary>The findings this person would hear, most interesting (then major, then newest) first.</summary>
        public static List<DiscoveryData> Offerable(WorldData w, NpcData npc)
        {
            var list = new List<DiscoveryData>();
            var social = w.Player?.Social;
            if (social == null || npc == null || npc.HomeSettlementId != w.Player.SettlementId) return list;
            // Each discovery is scored once (the sort compared them again and again, formatting the home each time: Phase 23's
            // larger world made that measurable).
            var home = npc.HomeSettlementId.ToString();
            var score = new Dictionary<DiscoveryData, int>();
            foreach (var d in social.Discoveries)
            {
                var s = Interest(npc, d, home);
                if (s <= 0) continue;
                score[d] = s;
                list.Add(d);
            }

            list.Sort((a, b) =>
            {
                var i = score[b].CompareTo(score[a]);
                if (i != 0) return i;
                if (a.Major != b.Major) return a.Major ? -1 : 1;
                return b.At != a.At ? b.At.CompareTo(a.At) : string.CompareOrdinal(a.Key, b.Key);
            });
            if (list.Count > MaxOffered) list.RemoveRange(MaxOffered, list.Count - MaxOffered);
            return list;
        }

        /// <summary>The player's line offering it.</summary>
        public static string Offer(DiscoveryData d)
        {
            var what = d.Text ?? d.Key;
            if (what.Length > 70) what = what.Substring(0, 67).TrimEnd() + "…";
            return "I found something you should hear: " + what;
        }

        /// <summary>Tells it (inside the choose command). Returns the person's answer.</summary>
        public static string Share(WorldData w, NpcData npc, string key, string turnKey, QuestReporter report)
        {
            var social = w.Player.Social;
            var d = social.FindDiscovery(key);
            if (d == null || Interest(w, npc, d) <= 0) return "\"I've heard it already, or it's no business of mine.\"";
            var settlement = npc.HomeSettlementId.ToString();
            d.SharedIn.Add(settlement);
            var big = d.Major;
            var rk = "shared:" + d.Key + ":" + settlement;
            Reputation.Change(w, "npc:" + npc.NpcId, "Respect", big ? 6 : 3, rk + ":respect", "told them what they found", report);
            switch (d.Category)
            {
                case DiscoveryCategory.Historical:
                case DiscoveryCategory.Cultural:
                    Reputation.Change(w, "settlement:" + settlement, "Reputation", big ? 5 : 2, rk, "brought back the town's history", report);
                    return big ? "\"So that's how it was. People will want to hear this — the old ones especially. Thank you for bringing it home.\"" : "\"Huh. I never knew that. I'll pass it on.\"";
                case DiscoveryCategory.Religious:
                case DiscoveryCategory.Arcane:
                    Reputation.Change(w, "settlement:" + settlement, "Reputation", big ? 4 : 2, rk, "told what lies in the old places", report);
                    return "\"Some things are better known than whispered about. I'm grateful you told me plainly.\"";
                case DiscoveryCategory.Faction:
                    Reputation.Change(w, "faction:" + npc.FactionId, "Standing", big ? 6 : 3, rk, "brought word of another faction's business", report);
                    return big ? "\"That's worth knowing — worth a great deal. I'll see it reaches the right ears.\"" : "\"Noted. We like to know who's doing what.\"";
                case DiscoveryCategory.Economic:
                {
                    var coin = big ? 15 : 5;
                    w.Player.Purse += coin;
                    Reputation.Change(w, "npc:" + npc.NpcId, "Gratitude", 4, rk + ":gratitude", "trade news", report);
                    return $"\"News like that is money, if you hear it first.\" They press {coin} coin into your hand.";
                }

                case DiscoveryCategory.Geographic:
                {
                    var coin = big ? 10 : 5;
                    w.Player.Purse += coin;
                    return $"\"A way through, or a way not to go — either's worth knowing on the road.\" They give you {coin} coin for it.";
                }

                case DiscoveryCategory.Natural:
                    Reputation.Change(w, "npc:" + npc.NpcId, "Gratitude", big ? 8 : 5, rk + ":gratitude", "warned of what's in the wild", report);
                    return "\"I'll keep the beasts in closer, then. You've maybe saved me a lamb or two.\"";
                case DiscoveryCategory.Dangerous:
                    Reputation.Change(w, "settlement:" + settlement, "Reputation", big ? 6 : 3, rk, "warned the town of a danger", report);
                    return "\"Then we'll keep folk away from there. Better a warning than a burial.\"";
                case DiscoveryCategory.Personal:
                    Reputation.Change(w, "npc:" + npc.NpcId, "Trust", big ? 6 : 3, rk + ":trust", "brought word of someone's last days", report);
                    return "\"I knew them, a little. It's a kindness to know what became of them.\"";
            }

            return "\"I'll remember it.\"";
        }
    }
}
