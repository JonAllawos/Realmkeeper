using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Party;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Party
{
    /// <summary>One item a companion holds, for the party screen (Phase 15).</summary>
    public sealed class CompanionItemView
    {
        public ItemStackId StackId { get; set; }
        public string ItemId { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }

        /// <summary>The slot it is worn in; null when it is in the pack.</summary>
        public EquipmentSlot? Slot { get; set; }

        public bool Equippable { get; set; }
        public bool Bound { get; set; }
    }

    /// <summary>One party member as the HUD and the party screen show them (Phase 15).</summary>
    public sealed class PartyMemberView
    {
        public NpcId NpcId { get; set; }
        public string Name { get; set; }
        public PartyRole Role { get; set; }
        public FollowMode Mode { get; set; }
        public int Order { get; set; }
        public bool IsLeader { get; set; }

        /// <summary>With the player now (following, or waiting where the player stands).</summary>
        public bool Present { get; set; }

        /// <summary>Where they wait, in words (empty when following).</summary>
        public string Where { get; set; }

        /// <summary>Phase 16: where they stand while waiting in a settlement, if known.</summary>
        public LocalPose? WaitPose { get; set; }

        public string ClassName { get; set; }
        public int Level { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int ArmorClass { get; set; }

        /// <summary>"unhurt", "injured", "dying", "unconscious, stable" or "dead".</summary>
        public string State { get; set; }

        public int Loyalty { get; set; }

        /// <summary>Loyalty in a word: devoted (80+), loyal (60+), steady (40+), uneasy (25+), wavering.</summary>
        public string LoyaltyWord { get; set; }

        /// <summary>The most recent reasons their loyalty moved, newest first (at most five).</summary>
        public List<string> RecentApprovals { get; } = new List<string>();

        /// <summary>A temporary arrangement's time left in words ("2 days left"); empty when permanent.</summary>
        public string Term { get; set; }

        public string PersonalQuest { get; set; }
        public NpcCharacterView Sheet { get; set; }
        public List<CompanionItemView> Items { get; } = new List<CompanionItemView>();
        public int CarriedGrams { get; set; }
    }

    public sealed class PartyView
    {
        public List<PartyMemberView> Members { get; } = new List<PartyMemberView>();
        public int ActiveCapacity { get; set; }
        public int ReserveCapacity { get; set; }

        /// <summary>People who travelled with the player before and left (name and why), most recent first.</summary>
        public List<string> Former { get; } = new List<string>();
    }

    public static class PartyQueries
    {
        public static string LoyaltyWord(int loyalty) => loyalty >= 80 ? "devoted" : loyalty >= 60 ? "loyal" : loyalty >= 40 ? "steady" : loyalty >= 25 ? "uneasy" : "wavering";

        public static PartyView View(WorldData w)
        {
            var v = new PartyView { ActiveCapacity = PartyRules.ActiveCapacity, ReserveCapacity = PartyRules.ReserveCapacity };
            if (w?.Party == null) return v;
            var now = QuestClock.Now(w);
            var members = new List<PartyMemberData>(w.Party.Members);
            members.Sort((a, b) => a.Order.CompareTo(b.Order));
            foreach (var m in members)
            {
                var npc = w.FindNpc(m.NpcId);
                if (npc == null) continue;
                var record = w.FindCompanion(m.NpcId);
                var sheet = NpcCharacterCommandHandlers.View(w, npc);
                var mv = new PartyMemberView
                {
                    NpcId = m.NpcId, Name = npc.Name, Role = m.Role, Mode = m.Mode, Order = m.Order, IsLeader = w.Party.Leader == m.NpcId,
                    Present = m.Role == PartyRole.Active && (m.Mode == FollowMode.Follow || CompanionLoyalty.SameSpot(w, m)),
                    Where = m.Mode == FollowMode.Wait ? PartyCommandHandlers.WhereWaiting(w, m) : string.Empty,
                    WaitPose = m.WaitPose,
                    ClassName = sheet?.ClassName, Level = sheet?.Level ?? 0, Hp = sheet?.Hp ?? 0, MaxHp = sheet?.MaxHp ?? 0, ArmorClass = sheet?.ArmorClass ?? 0,
                    State = sheet?.State ?? "unknown", Loyalty = record?.Loyalty ?? 50, Sheet = sheet,
                };
                mv.LoyaltyWord = LoyaltyWord(mv.Loyalty);
                if (record != null)
                    for (var i = record.Approvals.Count - 1; i >= 0 && mv.RecentApprovals.Count < 5; i--)
                    {
                        var a = record.Approvals[i];
                        mv.RecentApprovals.Add((a.Delta > 0 ? "+" : string.Empty) + a.Delta.ToString(CultureInfo.InvariantCulture) + ": " + a.Reason);
                    }

                if (m.Temporary)
                {
                    var left = Math.Max(0, m.Until - now);
                    mv.Term = left >= 1440 ? $"{(left + 1439) / 1440} day{((left + 1439) / 1440 == 1 ? string.Empty : "s")} left" : $"{left / 60} h left";
                }

                if (record?.PersonalQuestId != null && w.FindQuest(record.PersonalQuestId) is Domain.Quests.QuestInstanceData q)
                    mv.PersonalQuest = (Domain.Quests.QuestCatalog.Find(q.DefinitionId)?.Name ?? q.DefinitionId) + " (" + q.State.ToString().ToLowerInvariant() + ")";

                var ch = npc.Character;
                if (ch != null && ch.OwnsItems)
                {
                    foreach (var pair in ch.Equipment) mv.Items.Add(Item(pair.Value, pair.Key));
                    foreach (var s in ch.Inventory) mv.Items.Add(Item(s, null));
                    mv.CarriedGrams = ItemWeights.CarriedGrams(ch);
                }

                v.Members.Add(mv);
            }

            var former = new List<CompanionRecordData>(w.Companions.FindAll(c => c.Standing != CompanionStanding.InParty));
            former.Sort((a, b) => b.LeftAt.CompareTo(a.LeftAt));
            foreach (var c in former) v.Former.Add($"{w.FindNpc(c.NpcId)?.Name ?? "someone"}: {c.LeftReason ?? c.Standing.ToString().ToLowerInvariant()}");
            return v;
        }

        private static CompanionItemView Item(ItemStackData s, EquipmentSlot? slot)
        {
            var def = ItemCatalog.Find(s.ItemId);
            return new CompanionItemView
            {
                StackId = s.StackId, ItemId = s.ItemId, Name = def?.Name ?? s.ItemId, Quantity = s.Quantity, Slot = slot, Equippable = def?.IsEquippable == true, Bound = s.Bound,
            };
        }
    }

}

namespace Realmkeeper.Application
{
    public sealed partial class WorldSession
    {
        /// <summary>Phase 15: the player's party (members, loyalty, items, sheets) for the HUD and the party screen.</summary>
        public Party.PartyView GetParty() => HasWorld ? Party.PartyQueries.View(_world) : new Party.PartyView();

        /// <summary>Phase 15: why a person would refuse to join now (null: they would come).</summary>
        public string WhyNotRecruit(Domain.Identifiers.NpcId id, bool temporary = false) => HasWorld ? Party.RecruitRules.Problem(_world, _world.FindNpc(id), temporary) : "There is no active world.";
    }
}
