using System;
using System.Linq;
using Realmkeeper.Application;
using Realmkeeper.Application.Party;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using UnityEngine.UIElements;
using static Realmkeeper.Exploration.Presentation.TradeWindow;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 15 (ADR-0073): the hub's Party section. The line-up and reserve, each member's sheet, loyalty and why it
    /// moved, their term and their own quest; their items and the player's pack side by side for giving and taking; the
    /// formation, leader, follow or wait, and parting ways. Every button is a command the controller dispatches; this
    /// window keeps no party state of its own.
    /// </summary>
    public sealed partial class PlayerHubWindow
    {
        /// <summary>The party snapshot (set by the controller).</summary>
        public Func<PartyView> PartySource { get; set; }

        /// <summary>Dispatches a party command and returns its result (the controller refreshes the hub after).</summary>
        public Func<ICommand, CommandResult> PartyDispatch { get; set; }

        /// <summary>The player asked to talk to a companion.</summary>
        public event Action<NpcId> TalkRequested;

        /// <summary>The member whose sheet and items are shown (default: the first).</summary>
        public NpcId SelectedCompanion { get; set; }

        public void ShowParty(NpcId? select = null)
        {
            if (select.HasValue) SelectedCompanion = select.Value;
            Select("Party");
        }

        private void Party()
        {
            var party = PartySource?.Invoke();
            if (party == null) { _content.Add(Text("No world.", "xk-w-note")); return; }
            var active = party.Members.Count(m => m.Role == PartyRole.Active);
            var reserve = party.Members.Count - active;
            var head = Row("xk-w-row");
            head.name = "xk-party-head";
            head.Add(Text($"Travelling {active}/{party.ActiveCapacity} · reserve {reserve}/{party.ReserveCapacity}", "xk-w-name"));
            head.Add(Act("Regroup", () => Run(new RegroupPartyCommand())));
            _content.Add(head);
            if (party.Members.Count == 0)
            {
                _content.Add(Text("Nobody travels with you. Ask someone who knows and trusts you to join (\"Travel with me\" in conversation). Some will come only for a few days; some not at all, and they will say why.", "xk-w-note"));
                Former(party);
                return;
            }

            if (party.Members.All(m => m.NpcId != SelectedCompanion)) SelectedCompanion = party.Members[0].NpcId;
            foreach (var m in party.Members) _content.Add(Card(m, party));
            var sel = party.Members.First(m => m.NpcId == SelectedCompanion);
            Detail(sel);
            Former(party);
        }

        private VisualElement Card(PartyMemberView m, PartyView party)
        {
            var card = Column("xk-party-card");
            card.name = "xk-party-card-" + m.NpcId;
            card.EnableInClassList("xk-party-card--selected", m.NpcId == SelectedCompanion);
            var top = Row("xk-w-row");
            var name = new Button(() => { SelectedCompanion = m.NpcId; Render(); }) { text = (m.IsLeader ? "★ " : string.Empty) + m.Name, name = "xk-party-select-" + m.NpcId };
            name.AddToClassList("xk-button");
            name.AddToClassList("xk-w-choice");
            top.Add(name);
            top.Add(Text($"{m.ClassName ?? "-"} {m.Level} · HP {m.Hp}/{m.MaxHp} · AC {m.ArmorClass} · {m.State}", "xk-w-desc"));
            card.Add(top);
            var status = m.Role == PartyRole.Reserve ? "in reserve (living their life until you call)" : m.Mode == FollowMode.Wait ? "waiting " + m.Where : "following";
            card.Add(Text($"{status} · loyalty {m.Loyalty} ({m.LoyaltyWord}){(string.IsNullOrEmpty(m.Term) ? string.Empty : " · " + m.Term)}", "xk-w-desc"));

            var acts = Row("xk-cc-wrap");
            if (m.Role == PartyRole.Active)
                acts.Add(m.Mode == FollowMode.Follow ? Act("Wait here", () => Run(new SetFollowModeCommand(m.NpcId, FollowMode.Wait))) : Act("Follow me", () => Run(new SetFollowModeCommand(m.NpcId, FollowMode.Follow))));
            acts.Add(m.Role == PartyRole.Active ? Act("To reserve", () => Run(new SetPartyRoleCommand(m.NpcId, PartyRole.Reserve))) : Act("Call up", () => Run(new SetPartyRoleCommand(m.NpcId, PartyRole.Active))));
            if (m.Order > 0) acts.Add(Act("Move up", () => Run(new MovePartyMemberCommand(m.NpcId, m.Order - 1))));
            if (!m.IsLeader) acts.Add(Act("Lead", () => Run(new SetPartyLeaderCommand(m.NpcId))));
            if (m.Present) acts.Add(Act("Talk", () => TalkRequested?.Invoke(m.NpcId)));
            acts.Add(Act("Part ways", () => Run(new DismissCompanionCommand(m.NpcId))));
            card.Add(acts);
            return card;
        }

        private void Detail(PartyMemberView m)
        {
            var d = Column("xk-party-detail");
            d.name = "xk-party-detail";
            d.Add(Text(m.Name, "xk-w-title"));
            var s = m.Sheet;
            if (s != null && s.HasSheet)
            {
                d.Add(Pair("Character", $"{s.Species} {s.ClassName}{(string.IsNullOrEmpty(s.Subclass) ? string.Empty : " (" + s.Subclass + ")")} {s.Level} · {s.Background} · {s.Experience} XP"));
                d.Add(Pair("Hit points", $"{s.Hp}/{s.MaxHp} ({s.State})"));
                d.Add(Pair("Armour class", $"{s.ArmorClass} — {s.ArmorClassText}"));
                d.Add(Pair("Speed", $"{s.Speed} ft · proficiency +{s.ProficiencyBonus}"));
                d.Add(Pair("Abilities", string.Join("  ", new[] { "Str", "Dex", "Con", "Int", "Wis", "Cha" }.Select((a, i) => a + " " + s.Scores[i]))));
                d.Add(Pair("Saves", string.Join(", ", s.Saves)));
                if (s.Skills.Count > 0) d.Add(Pair("Skills", string.Join(", ", s.Skills)));
                if (s.Features.Count > 0) d.Add(Pair("Features", string.Join(", ", s.Features)));
                if (s.Spells.Count > 0) d.Add(Pair("Spells", string.Join(", ", s.Spells)));
                if (s.Resources.Count > 0) d.Add(Pair("Resources", string.Join(", ", s.Resources)));
                if (s.CanAdvance) d.Add(Text("They have earned a new level; it is taken when they rest in a settlement (or through the person inspector).", "xk-w-note"));
            }

            d.Add(Pair("Loyalty", $"{m.Loyalty}/100 ({m.LoyaltyWord}). Below {PartyRules.LeaveBelow} they leave; only real deeds move it."));
            foreach (var why in m.RecentApprovals) d.Add(Text("· " + why, "xk-w-desc"));
            if (!string.IsNullOrEmpty(m.PersonalQuest)) d.Add(Pair("Their own quest", m.PersonalQuest));

            d.Add(Text($"What {m.Name} carries ({Kg(m.CarriedGrams)})", "xk-w-name"));
            if (m.Items.Count == 0) d.Add(Text("Nothing of their own.", "xk-w-note"));
            foreach (var it in m.Items)
            {
                var row = Row("xk-w-row");
                row.name = "xk-party-item-" + it.StackId;
                row.Add(Text((it.Slot.HasValue ? "[" + it.Slot.Value + "] " : string.Empty) + it.Name + (it.Quantity > 1 ? " ×" + it.Quantity : string.Empty) + (it.Bound ? " (bound)" : string.Empty), "xk-w-name"));
                if (it.Slot.HasValue) row.Add(Act("Take off", () => Run(new CompanionUnequipCommand(m.NpcId, it.Slot.Value))));
                else
                {
                    if (it.Equippable) row.Add(Act("Equip", () => Run(new CompanionEquipCommand(m.NpcId, it.StackId))));
                    row.Add(Act("Take", () => Run(new TransferItemCommand(m.NpcId, default, it.StackId))));
                }

                d.Add(row);
            }

            if (Hub != null && Hub.Inventory.Count > 0 && m.Present)
            {
                d.Add(Text("Your pack (give to " + m.Name + ")", "xk-w-name"));
                foreach (var it in Hub.Inventory)
                {
                    var row = Row("xk-w-row");
                    row.Add(Text(it.Name, "xk-w-name"));
                    row.Add(Act("Give", () => Run(new TransferItemCommand(default, m.NpcId, it.StackId))));
                    d.Add(row);
                }
            }

            _content.Add(d);
        }

        private void Former(PartyView party)
        {
            if (party.Former.Count == 0) return;
            _content.Add(Text("Travelled with you before", "xk-w-name"));
            foreach (var f in party.Former.Take(8)) _content.Add(Text("· " + f, "xk-w-desc"));
        }

        private void Run(ICommand command)
        {
            var r = PartyDispatch?.Invoke(command);
            if (r == null) return;
            _message.text = r.Message;
            _message.EnableInClassList("xk-message--error", !r.IsSuccess);
            Render();
        }
    }
}
