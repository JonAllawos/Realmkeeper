using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Simulation.Party;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Party
{
    /// <summary>Ask a person to travel with the player (Phase 15). Temporary: for <see cref="PartyRules.TemporaryDays"/> days.</summary>
    public sealed class RecruitCompanionCommand : ICommand
    {
        public RecruitCompanionCommand(NpcId npcId, bool temporary = false) { NpcId = npcId; Temporary = temporary; }
        public NpcId NpcId { get; }
        public bool Temporary { get; }
    }

    /// <summary>Send a companion home (Phase 15). They keep everything they carry and go back to their life.</summary>
    public sealed class DismissCompanionCommand : ICommand
    {
        public DismissCompanionCommand(NpcId npcId) { NpcId = npcId; }
        public NpcId NpcId { get; }
    }

    /// <summary>Follow, or wait where the player stands now (Phase 15).</summary>
    public sealed class SetFollowModeCommand : ICommand
    {
        public SetFollowModeCommand(NpcId npcId, FollowMode mode, LocalPose? at = null) { NpcId = npcId; Mode = mode; At = at; }
        public NpcId NpcId { get; }
        public FollowMode Mode { get; }

        /// <summary>Phase 16: where their body stands when told to wait in a settlement (kept, so they are there on return).</summary>
        public LocalPose? At { get; }
    }

    /// <summary>Everyone waiting where the player is follows again (Phase 15).</summary>
    public sealed class RegroupPartyCommand : ICommand { }

    /// <summary>Active line-up or reserve (Phase 15); capacities are <see cref="PartyRules"/>.</summary>
    public sealed class SetPartyRoleCommand : ICommand
    {
        public SetPartyRoleCommand(NpcId npcId, PartyRole role) { NpcId = npcId; Role = role; }
        public NpcId NpcId { get; }
        public PartyRole Role { get; }
    }

    /// <summary>Moves a member to a place in the formation order (Phase 15).</summary>
    public sealed class MovePartyMemberCommand : ICommand
    {
        public MovePartyMemberCommand(NpcId npcId, int order) { NpcId = npcId; Order = order; }
        public NpcId NpcId { get; }
        public int Order { get; }
    }

    /// <summary>Who leads the formation (Phase 15): a member, or the player (an empty ID).</summary>
    public sealed class SetPartyLeaderCommand : ICommand
    {
        public SetPartyLeaderCommand(NpcId leader) { Leader = leader; }
        public NpcId Leader { get; }
    }

    /// <summary>
    /// Phase 15: hands an item between the player and a companion (or between companions). The instance moves: one owner
    /// before, one owner after, with its state and provenance. An empty ID is the player.
    /// </summary>
    public sealed class TransferItemCommand : ICommand
    {
        public TransferItemCommand(NpcId from, NpcId to, ItemStackId stackId, int quantity = 0) { From = from; To = to; StackId = stackId; Quantity = quantity; }
        public NpcId From { get; }
        public NpcId To { get; }
        public ItemStackId StackId { get; }

        /// <summary>0: the whole stack.</summary>
        public int Quantity { get; }
    }

    /// <summary>A companion puts on or takes up an item from their own pack (Phase 15), by the same slot rules as the player.</summary>
    public sealed class CompanionEquipCommand : ICommand
    {
        public CompanionEquipCommand(NpcId npcId, ItemStackId stackId) { NpcId = npcId; StackId = stackId; }
        public NpcId NpcId { get; }
        public ItemStackId StackId { get; }
    }

    /// <summary>A companion takes off what is in a slot (Phase 15); a curse may refuse.</summary>
    public sealed class CompanionUnequipCommand : ICommand
    {
        public CompanionUnequipCommand(NpcId npcId, EquipmentSlot slot) { NpcId = npcId; Slot = slot; }
        public NpcId NpcId { get; }
        public EquipmentSlot Slot { get; }
    }

    /// <summary>
    /// Phase 15 (ADR-0073): who will travel with the player, and why someone will not. Every refusal names its cause:
    /// what they think of the player, what they know the player did, their post, their nerve, their faction, an
    /// unfinished matter, or a grievance from leaving before.
    /// </summary>
    public static class RecruitRules
    {
        /// <summary>Roles that hold a post: they will come for a few days, not for good.</summary>
        public static bool HoldsPost(NpcRole r) => r == NpcRole.Elder || r == NpcRole.Merchant || r == NpcRole.Innkeeper || r == NpcRole.Smith || r == NpcRole.Guard;

        public static string Problem(WorldData w, NpcData npc, bool temporary)
        {
            if (npc == null || npc.IsDeceased) return "They are not alive to ask.";
            var p = w.Player;
            if (p == null || p.Rpg == null || p.Rpg.State != CreationState.Complete) return "Create your character first.";
            if (w.ActiveEncounter != null) return "Not in the middle of a fight.";
            if (w.Party.Contains(npc.NpcId)) return $"{npc.Name} is already with you.";
            if (npc.BirthDate.IsValidDate && npc.AgeOn(w.CurrentSimulationDate) < 16) return $"{npc.Name} is too young to go adventuring.";
            var escort = w.FindEscort(npc.NpcId);
            if (escort != null && escort.OverridesSchedule)
                return $"{npc.Name} is not free to choose: they are being taken home.";
            if (PlacePresence.IsAway(w, npc)) return $"{npc.Name} is not free to go: they have a post elsewhere.";
            if (!Places.PlaceRules.Beside(w, npc) && (p.IsInPlace || !p.IsInSettlement || npc.HomeSettlementId != p.SettlementId)) return $"{npc.Name} is not here.";
            var record = w.FindCompanion(npc.NpcId);
            if (record != null && record.Standing == CompanionStanding.Departed && record.Loyalty < PartyRules.ReturnAt)
                return $"{npc.Name} will not come back yet: {record.LeftReason ?? "they left over a grievance"}.";
            var r = npc.PlayerRegard;
            if (r.Familiarity < 15) return $"{npc.Name} barely knows you. Talk to them first.";
            if (r.Hostility >= 30 || r.Affinity < 0) return $"{npc.Name} does not like you enough to share the road.";
            if (r.Trust < 20) return $"{npc.Name} does not trust you yet.";
            if (CrimeRules.KnownTo(w, npc).Count > 0) return $"{npc.Name} knows what you did and will not travel with you.";
            if (!npc.FactionId.IsEmpty)
                foreach (var s in p.Social?.FactionStandings ?? new List<FactionStandingData>())
                    if (s.FactionId == npc.FactionId && s.Standing <= -20)
                        return $"{npc.Name}'s people count you an enemy ({w.FindFaction(npc.FactionId)?.Name}).";
            if (!temporary && HoldsPost(npc.Role)) return $"{npc.Name} has a post to keep ({npc.Character?.Occupation ?? npc.Role.ToString().ToLowerInvariant()}). A few days, perhaps; not for good.";
            if (!temporary && npc.Personality[NpcTrait.Courage] < 25) return $"{npc.Name} has no stomach for the road: not for good, maybe for a few days.";
            foreach (var q in w.Quests)
                if (q.IsOpen && q.State == QuestState.Active && q.Bound("giver") == npc.NpcId.ToString() && !temporary)
                    return $"{npc.Name} waits here for news of what they asked of you ({QuestCatalog.Find(q.DefinitionId)?.Name ?? "their request"}).";
            var active = w.Party.Count(PartyRole.Active);
            var reserve = w.Party.Count(PartyRole.Reserve);
            if (active >= PartyRules.ActiveCapacity && reserve >= PartyRules.ReserveCapacity) return $"Your party is full ({active} travelling, {reserve} in reserve).";
            return null;
        }
    }

    public static class PartyCommandHandlers
    {
        public static void Register(CommandRegistry commands)
        {
            commands.Register<RecruitCompanionCommand>(new RecruitHandler());
            commands.Register<DismissCompanionCommand>(new DismissHandler());
            commands.Register<SetFollowModeCommand>(new FollowHandler());
            commands.Register<RegroupPartyCommand>(new RegroupHandler());
            commands.Register<SetPartyRoleCommand>(new RoleHandler());
            commands.Register<MovePartyMemberCommand>(new MoveHandler());
            commands.Register<SetPartyLeaderCommand>(new LeaderHandler());
            commands.Register<TransferItemCommand>(new TransferHandler());
            commands.Register<CompanionEquipCommand>(new EquipHandler());
            commands.Register<CompanionUnequipCommand>(new UnequipHandler());
        }

        private static Dictionary<string, string> Facts(WorldData w, NpcData npc, params (string, string)[] more)
        {
            var d = new Dictionary<string, string> { ["npc"] = npc.NpcId.ToString(), ["name"] = npc.Name, ["player"] = w.Player?.Name ?? "the player" };
            foreach (var (k, v) in more) d[k] = v;
            return d;
        }

        internal static void Report(CommandContext context, WorldData w, NpcData npc, HistoricalEventType type, params (string, string)[] facts) =>
            context.ReportEvent(type, EventSeverity.Info, EventImportance.Minor, npc.HomeSettlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { npc.HomeSettlementId }, Facts(w, npc, facts), null, new[] { npc.NpcId });

        /// <summary>Renumbers the formation so orders run 0, 1, 2 … without gaps.</summary>
        internal static void Renumber(PartyData party) => CompanionLoyalty.Renumber(party);

        /// <summary>
        /// Recruits a person whom <see cref="RecruitRules.Problem"/> has already accepted, in the world being edited: their
        /// sheet if they have none, their companion record, their own items (once), their place in the party, a memory and
        /// the chronicle line. Returns what the player is told, or null if no sheet could be made. Shared by the command
        /// and the dialogue consequence, so both recruit the same way.
        /// </summary>
        internal static string Recruit(CommandContext context, WorldData edit, NpcId npcId, bool temporary)
        {
            var e = edit.FindNpc(npcId);
            var now = QuestClock.Now(edit);

            // A full character, made from who they already are (identity, home, gear and relationships kept).
            if (e.Character == null || !e.Character.HasSheet)
            {
                var archetype = NpcArchetypeCatalog.Find(e.Character?.ArchetypeId) ?? NpcArchetypeCatalog.For(edit, e);
                var classId = archetype.ClassId ?? archetype.PromotionClassId ?? "fighter";
                var made = NpcCharacterGenesis.Make(edit, e, archetype, classId, out _);
                if (made.Sheet == null) return null;
                if (e.Character != null) { made.Gear = e.Character.Gear?.Clone() ?? made.Gear; made.Purse = e.Character.Purse; }
                e.Character = made;
                if (e.Tier < NpcTier.Notable) e.Tier = NpcTier.Notable;
            }

            var record = edit.FindCompanion(e.NpcId);
            if (record == null) edit.Companions.Add(record = new CompanionRecordData { NpcId = e.NpcId, Loyalty = 40 + Math.Max(0, Math.Min(30, e.PlayerRegard.Affinity / 3)) });
            record.Standing = CompanionStanding.InParty;
            record.TimesRecruited++;
            record.LeftAt = -1;
            record.LeftReason = null;
            RecruitHandler.GrantItems(context, e, record);

            var role = edit.Party.Count(PartyRole.Active) < PartyRules.ActiveCapacity ? PartyRole.Active : PartyRole.Reserve;
            edit.Party.Members.Add(new PartyMemberData
            {
                NpcId = e.NpcId, Role = role, Mode = FollowMode.Follow, Order = edit.Party.Members.Count, JoinedAt = now,
                Until = temporary ? now + PartyRules.TemporaryDays * 1440L : -1, FollowDistance = 2.5f + 1.2f * edit.Party.Members.Count,
            });
            Realmkeeper.Simulation.Npcs.NpcMind.Remember(e, new NpcMemoryData
            {
                MemoryId = new MemoryId(context.NextGuid("npc-memory")), Date = edit.CurrentSimulationDate, Category = MemoryCategory.Personal,
                Subject = "set out with " + edit.Player.Name, SettlementId = e.HomeSettlementId, Salience = 60, Source = MemorySource.Witnessed,
            });
            var terms = temporary ? $"for {PartyRules.TemporaryDays} days" : string.Empty;
            Report(context, edit, e, HistoricalEventType.CompanionJoined, ("terms", terms));
            return $"{e.Name} joins you{(temporary ? " " + terms : string.Empty)}{(role == PartyRole.Reserve ? " (in reserve: your line-up is full)" : string.Empty)}.";
        }

        private sealed class RecruitHandler : ICommandHandler<RecruitCompanionCommand>
        {
            public CommandResult Handle(RecruitCompanionCommand c, CommandContext context)
            {
                var w = context.Committed;
                if (w == null) return CommandResult.InvalidState("There is no active world.");
                var why = RecruitRules.Problem(w, w.FindNpc(c.NpcId), c.Temporary);
                if (why != null) return CommandResult.Invalid(why);
                var said = Recruit(context, context.Edit(), c.NpcId, c.Temporary);
                return said == null ? CommandResult.Invalid("They could not be made ready for the road.") : CommandResult.Ok(said);
            }

            /// <summary>
            /// Once in a person's life: what they wear and carry by name becomes real items they own (equipped where worn).
            /// Their gear record is cleared so nothing exists twice; a second recruitment finds them already owning it.
            /// </summary>
            internal static void GrantItems(CommandContext context, NpcData e, CompanionRecordData record)
            {
                if (record.ItemsGranted) return;
                record.ItemsGranted = true;
                var ch = e.Character;
                if (ch.OwnsItems) return;
                var gear = ch.Gear ?? new NpcGearData();
                ItemStackData Make(string itemId) => ItemCatalog.Find(itemId) == null ? null : new ItemStackData
                {
                    StackId = ItemCommandHandlers.NewStackId(context), ItemId = itemId, Quantity = 1, Provenance = "carried by " + e.Name,
                };

                void Wear(EquipmentSlot slot, string itemId)
                {
                    var s = itemId == null ? null : Make(itemId);
                    if (s != null) ch.Equipment[slot] = s;
                }

                Wear(EquipmentSlot.Chest, gear.Body);
                Wear(EquipmentSlot.MainHand, gear.MainHand);
                Wear(EquipmentSlot.OffHand, gear.Shield ?? gear.OffHand);
                foreach (var id in gear.Carried) { var s = Make(id); if (s != null) ch.Inventory.Add(s); }
                ch.OwnsItems = true;
                ch.Gear = new NpcGearData();
            }
        }

        private sealed class DismissHandler : ICommandHandler<DismissCompanionCommand>
        {
            public CommandResult Handle(DismissCompanionCommand c, CommandContext context)
            {
                var w = context.Committed;
                if (w == null) return CommandResult.InvalidState("There is no active world.");
                if (w.ActiveEncounter != null) return CommandResult.InvalidState("Not in the middle of a fight.");
                if (!w.Party.Contains(c.NpcId)) return CommandResult.Invalid("They are not in your party.");
                var edit = context.Edit();
                var npc = edit.FindNpc(c.NpcId);
                Leave(context, edit, npc, CompanionStanding.Dismissed, "sent home");
                return CommandResult.Ok($"{npc.Name} heads home. They take everything they carry with them.");
            }
        }

        /// <summary>Takes a member out of the party (dismissed, departed or their term ended) and records why.</summary>
        internal static void Leave(CommandContext context, WorldData edit, NpcData npc, CompanionStanding standing, string why) =>
            CompanionLoyalty.Leave(edit, npc, standing, why, QuestRuntime.Reporter(context));

        private sealed class FollowHandler : ICommandHandler<SetFollowModeCommand>
        {
            public CommandResult Handle(SetFollowModeCommand c, CommandContext context)
            {
                var w = context.Committed;
                var m = w?.Party.Find(c.NpcId);
                if (m == null) return CommandResult.Invalid("They are not in your party.");
                var npc = w.FindNpc(c.NpcId);
                if (c.Mode == FollowMode.Follow && m.Mode == FollowMode.Wait && !SameSpot(w, m))
                    return CommandResult.Invalid($"{npc.Name} is waiting {WhereWaiting(w, m)}. Go back for them.");
                var edit = context.Edit().Party.Find(c.NpcId);
                edit.Mode = c.Mode;
                var p = context.Edit().Player;
                edit.WaitSettlement = c.Mode == FollowMode.Wait ? p.SettlementId : default;
                edit.WaitPlaceId = c.Mode == FollowMode.Wait ? p.PlaceId : null;
                edit.WaitRoom = c.Mode == FollowMode.Wait ? p.PlaceRoom : null;
                edit.WaitPose = c.Mode == FollowMode.Wait && p.IsInSettlement ? c.At ?? p.Pose : (LocalPose?)null;
                return CommandResult.Ok(c.Mode == FollowMode.Wait ? $"{npc.Name} waits here." : $"{npc.Name} follows you.");
            }
        }

        /// <summary>A waiting member is where the player is now (same settlement, or same place and room).</summary>
        internal static bool SameSpot(WorldData w, PartyMemberData m) => CompanionLoyalty.SameSpot(w, m);

        internal static string WhereWaiting(WorldData w, PartyMemberData m)
        {
            if (!string.IsNullOrEmpty(m.WaitPlaceId))
            {
                var place = w.FindPlace(m.WaitPlaceId);
                return $"in {place?.Name ?? "a place"}{(place?.Layout.Room(m.WaitRoom) is PlaceRoom r ? ", " + r.Name.ToLowerInvariant() : string.Empty)}";
            }

            return m.WaitSettlement.IsEmpty ? "on the road" : "in " + (w.FindSettlement(m.WaitSettlement)?.Name ?? "a settlement");
        }

        private sealed class RegroupHandler : ICommandHandler<RegroupPartyCommand>
        {
            public CommandResult Handle(RegroupPartyCommand c, CommandContext context)
            {
                var w = context.Committed;
                if (w == null) return CommandResult.InvalidState("There is no active world.");
                var here = w.Party.Members.Where(m => m.Role == PartyRole.Active && m.Mode == FollowMode.Wait && SameSpot(w, m)).Select(m => m.NpcId).ToList();
                var away = w.Party.Members.Where(m => m.Role == PartyRole.Active && m.Mode == FollowMode.Wait && !SameSpot(w, m)).ToList();
                if (here.Count == 0 && away.Count == 0) return CommandResult.Ok("Everyone is already with you.");
                var edit = context.Edit();
                foreach (var id in here)
                {
                    var m = edit.Party.Find(id);
                    m.Mode = FollowMode.Follow;
                    m.WaitSettlement = default;
                    m.WaitPlaceId = null;
                    m.WaitRoom = null;
                    m.WaitPose = null;
                }

                var said = here.Count > 0 ? string.Join(", ", here.Select(id => w.FindNpc(id).Name)) + " fall in behind you." : string.Empty;
                if (away.Count > 0) said += (said.Length > 0 ? " " : string.Empty) + string.Join("; ", away.Select(m => $"{w.FindNpc(m.NpcId).Name} is waiting {WhereWaiting(w, m)}")) + ".";
                return CommandResult.Ok(said);
            }
        }

        private sealed class RoleHandler : ICommandHandler<SetPartyRoleCommand>
        {
            public CommandResult Handle(SetPartyRoleCommand c, CommandContext context)
            {
                var w = context.Committed;
                var m = w?.Party.Find(c.NpcId);
                if (m == null) return CommandResult.Invalid("They are not in your party.");
                if (m.Role == c.Role) return CommandResult.Ok("No change.");
                if (w.ActiveEncounter != null) return CommandResult.InvalidState("Not in the middle of a fight.");
                var cap = c.Role == PartyRole.Active ? PartyRules.ActiveCapacity : PartyRules.ReserveCapacity;
                if (w.Party.Count(c.Role) >= cap) return CommandResult.Invalid($"The {(c.Role == PartyRole.Active ? "line-up" : "reserve")} is full ({cap}).");
                context.Edit().Party.Find(c.NpcId).Role = c.Role;
                var name = w.FindNpc(c.NpcId).Name;
                return CommandResult.Ok(c.Role == PartyRole.Active ? $"{name} travels with you." : $"{name} stays in reserve and goes about their life until you call.");
            }
        }

        private sealed class MoveHandler : ICommandHandler<MovePartyMemberCommand>
        {
            public CommandResult Handle(MovePartyMemberCommand c, CommandContext context)
            {
                var w = context.Committed;
                if (w?.Party.Find(c.NpcId) == null) return CommandResult.Invalid("They are not in your party.");
                var edit = context.Edit().Party;
                var list = edit.Members.OrderBy(m => m.Order).ToList();
                var moving = list.First(m => m.NpcId == c.NpcId);
                list.Remove(moving);
                list.Insert(Math.Max(0, Math.Min(list.Count, c.Order)), moving);
                for (var i = 0; i < list.Count; i++) list[i].Order = i;
                return CommandResult.Ok($"{w.FindNpc(c.NpcId).Name} is now {Ordinal(moving.Order + 1)} in the formation.");
            }

            private static string Ordinal(int n) => n + (n == 1 ? "st" : n == 2 ? "nd" : n == 3 ? "rd" : "th");
        }

        private sealed class LeaderHandler : ICommandHandler<SetPartyLeaderCommand>
        {
            public CommandResult Handle(SetPartyLeaderCommand c, CommandContext context)
            {
                var w = context.Committed;
                if (w == null) return CommandResult.InvalidState("There is no active world.");
                if (!c.Leader.IsEmpty && !w.Party.Contains(c.Leader)) return CommandResult.Invalid("Only a party member can lead.");
                context.Edit().Party.Leader = c.Leader;
                return CommandResult.Ok(c.Leader.IsEmpty ? "You lead the party." : $"{w.FindNpc(c.Leader).Name} leads the formation.");
            }
        }

        // ----- Items -----------------------------------------------------------------------------------------

        /// <summary>The pack of an owner (the player when the ID is empty; a companion who owns items otherwise), or the reason.</summary>
        internal static List<ItemStackData> Pack(WorldData w, NpcId owner, out string name, out string why)
        {
            why = null;
            if (owner.IsEmpty) { name = "you"; return w.Player.Inventory; }
            name = null;
            if (!w.Party.Contains(owner)) { why = "Only party members can take or give items."; return null; }
            var ch = w.FindNpc(owner)?.Character;
            if (ch == null || !ch.OwnsItems) { why = "They carry nothing of yours."; return null; }
            name = w.FindNpc(owner).Name;
            return ch.Inventory;
        }

        private sealed class TransferHandler : ICommandHandler<TransferItemCommand>
        {
            public CommandResult Handle(TransferItemCommand c, CommandContext context)
            {
                var w = context.Committed;
                if (w?.Player == null) return CommandResult.InvalidState("There is no active world.");
                if (c.From == c.To) return CommandResult.Invalid("That is already theirs.");
                if (w.ActiveEncounter != null) return CommandResult.InvalidState("Not in the middle of a fight (use an item instead).");
                var edit = context.Edit();
                var from = Pack(edit, c.From, out var fromName, out var why);
                if (from == null) return CommandResult.Invalid(why);
                var to = Pack(edit, c.To, out var toName, out why);
                if (to == null) return CommandResult.Invalid(why);
                var stack = from.FirstOrDefault(s => s.StackId == c.StackId);
                if (stack == null) return CommandResult.Invalid("That item is not in the pack it is meant to come from (equipped items must be taken off first).");
                if (stack.Bound) return CommandResult.InvalidState("A curse binds it to its bearer.");
                var def = ItemCatalog.Find(stack.ItemId);
                var qty = c.Quantity <= 0 ? stack.Quantity : Math.Min(c.Quantity, stack.Quantity);
                var moved = ItemCommandHandlers.TakeFrom(from, stack, qty, context);
                if (c.To.IsEmpty) ItemCommandHandlers.AddStack(edit.Player, moved);
                else to.Add(moved);
                var who = c.From.IsEmpty ? $"You give {toName}" : c.To.IsEmpty ? $"{fromName} hands you" : $"{fromName} gives {toName}";
                return CommandResult.Ok($"{who} {(qty > 1 ? qty + " × " : string.Empty)}{def?.Name ?? stack.ItemId}.");
            }
        }

        private sealed class EquipHandler : ICommandHandler<CompanionEquipCommand>
        {
            public CommandResult Handle(CompanionEquipCommand c, CommandContext context)
            {
                var w = context.Committed;
                if (w == null) return CommandResult.InvalidState("There is no active world.");
                if (w.ActiveEncounter != null) return CommandResult.InvalidState("Not in the middle of a fight.");
                var pack = Pack(w, c.NpcId, out var name, out var why);
                if (pack == null) return CommandResult.Invalid(why);
                var stack = pack.FirstOrDefault(s => s.StackId == c.StackId);
                if (stack == null) return CommandResult.Invalid("That is not in their pack.");
                var def = ItemCatalog.Find(stack.ItemId);
                if (def == null || !def.IsEquippable) return CommandResult.Invalid($"{def?.Name ?? stack.ItemId} cannot be worn or wielded.");
                var ch = w.FindNpc(c.NpcId).Character;
                var slot = def.Slot;
                if (slot == EquipmentSlot.RingLeft || slot == EquipmentSlot.RingRight)
                    slot = ch.Equipped(EquipmentSlot.RingLeft) == null ? EquipmentSlot.RingLeft : EquipmentSlot.RingRight;
                var displaced = new List<EquipmentSlot> { slot };
                if (def.TwoHanded) displaced.Add(EquipmentSlot.OffHand);
                if (slot == EquipmentSlot.OffHand && ch.Equipped(EquipmentSlot.MainHand) is ItemStackData main && ItemCatalog.Find(main.ItemId)?.TwoHanded == true) displaced.Add(EquipmentSlot.MainHand);
                foreach (var d in displaced)
                    if (ch.Equipped(d) is ItemStackData worn && worn.Bound) return CommandResult.InvalidState($"A curse binds the {ItemCatalog.Find(worn.ItemId)?.Name ?? "item"} to {name}.");

                var e = context.Edit().FindNpc(c.NpcId).Character;
                var off = new List<string>();
                foreach (var d in displaced)
                    if (e.Equipment.TryGetValue(d, out var worn))
                    {
                        e.Equipment.Remove(d);
                        e.Inventory.Add(worn);
                        off.Add(ItemCatalog.Find(worn.ItemId)?.Name ?? worn.ItemId);
                    }

                var taken = ItemCommandHandlers.TakeFrom(e.Inventory, e.Inventory.First(s => s.StackId == c.StackId), 1, context);
                e.Equipment[slot] = taken;
                var rules = new CharacterRules(e.Sheet, CharacterRules.LoadoutOf(e));
                var note = rules.UntrainedArmor ? " They are not trained in it: disadvantage on Strength and Dexterity checks and no spells." : rules.ArmorTooHeavy ? " It is too heavy for them: −10 ft speed." : string.Empty;
                return CommandResult.Ok($"{name} equips {def.Name}.{(off.Count > 0 ? " Put away: " + string.Join(", ", off) + "." : string.Empty)}{note}");
            }
        }

        private sealed class UnequipHandler : ICommandHandler<CompanionUnequipCommand>
        {
            public CommandResult Handle(CompanionUnequipCommand c, CommandContext context)
            {
                var w = context.Committed;
                if (w == null) return CommandResult.InvalidState("There is no active world.");
                if (w.ActiveEncounter != null) return CommandResult.InvalidState("Not in the middle of a fight.");
                var pack = Pack(w, c.NpcId, out var name, out var why);
                if (pack == null) return CommandResult.Invalid(why);
                var worn = w.FindNpc(c.NpcId).Character.Equipped(c.Slot);
                if (worn == null) return CommandResult.Invalid("Nothing is there.");
                if (worn.Bound) return CommandResult.InvalidState($"A curse binds it to {name}.");
                var e = context.Edit().FindNpc(c.NpcId).Character;
                var s = e.Equipment[c.Slot];
                e.Equipment.Remove(c.Slot);
                e.Inventory.Add(s);
                return CommandResult.Ok($"{name} puts away {ItemCatalog.Find(s.ItemId)?.Name ?? s.ItemId}.");
            }
        }
    }
}
