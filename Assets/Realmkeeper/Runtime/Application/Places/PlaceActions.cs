using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Simulation.Quests;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;
using FeatureDefinition = Realmkeeper.Domain.Places.FeatureDefinition;

namespace Realmkeeper.Application.Places
{
    /// <summary>One action the player could take on a target now, with the reason when they cannot.</summary>
    public sealed class ActionOption
    {
        public PlaceAction Action { get; set; }
        public string Label { get; set; }
        public bool Enabled { get; set; }
        public string Reason { get; set; }
        public string Check { get; set; }
    }

    /// <summary>
    /// Phase 13 (ADR-0064): the interaction framework. Every target in a place (a feature, a door or passage, the room
    /// itself, a group) offers the actions its kind and state allow; each is validated and applied here, inside the
    /// command, with the character rules' checks and the world's dice. The Phase 12 ledger and notice board keep their
    /// own inspect command; inside places this is the one framework.
    /// </summary>
    public static class PlaceActions
    {
        /// <summary>What can be done to a target from where the player stands (for prompts; the command re-checks everything).</summary>
        public static List<ActionOption> Options(WorldData w, PlaceData place, PlaceRoom room, string targetId)
        {
            var list = new List<ActionOption>();
            void Add(PlaceAction a, string label, string reason = null, string check = null) => list.Add(new ActionOption { Action = a, Label = label, Enabled = reason == null, Reason = reason, Check = check });
            if (place == null || room == null) return list;
            var p = w.Player;
            if (targetId == room.Id)
            {
                Add(PlaceAction.Search, "Search the room", null, "Investigation");
                return list;
            }

            var f = place.Layout.Feature(targetId);
            if (f != null)
            {
                if (f.Room != room.Id) return list;
                var s = place.Features.TryGetValue(f.Id, out var st) ? st : new FeatureStateData();
                var def = FeatureCatalog.Find(f.DefinitionId);
                switch (f.Kind)
                {
                    case FeatureKind.Trap:
                        var trap = TrapCatalog.Find(f.Get("trap"));
                        if (s.Has(FeatureFlags.Disarmed)) Add(PlaceAction.Inspect, "Look at the disarmed trap");
                        else
                        {
                            Add(PlaceAction.Inspect, "Look at it");
                            var tool = trap?.DisarmTool;
                            Add(PlaceAction.Disarm, "Disarm", tool != null && !PlaceCommandHandlers.HasItem(p, tool) ? "You need " + ItemCatalog.Find(tool)?.Name.ToLowerInvariant() + "." : null,
                                PlaceRules.SkillName(trap?.DisarmSkill ?? Skill.SleightOfHand));
                        }

                        break;
                    case FeatureKind.Container:
                        var key = PlaceContents.Key(place.PlaceId, f.Id);
                        var box = w.FindContainer(key);
                        Add(PlaceAction.Open, s.Has(FeatureFlags.Opened) ? "Look inside" : "Open", box == null ? "It is empty and broken." : !box.PlayerAccess ? OwnerReason(place, f) : null);
                        if (def?.Study != null) Add(PlaceAction.Inspect, "Examine", null, def.Study.Skill.ToString());
                        break;
                    case FeatureKind.Document:
                        Add(PlaceAction.Read, "Read", PlaceRules.Dark(w, room) ? "Too dark to read without a light." : null);
                        break;
                    case FeatureKind.Captive:
                        if (!s.Has(FeatureFlags.Freed)) Add(PlaceAction.Free, "Free them", HostileHere(place, room) ? "Not with enemies in the room." : null, "Athletics");
                        break;
                    case FeatureKind.Mechanism:
                        Add(PlaceAction.Activate, s.Has(FeatureFlags.Used) ? "Pull it again" : "Pull it");
                        if (def?.Study != null) Add(PlaceAction.Inspect, "Examine", null, def.Study.Skill.ToString());
                        break;
                    default:
                        Add(PlaceAction.Inspect, f.Kind == FeatureKind.Workstation || f.Kind == FeatureKind.Furniture ? "Look" : "Examine", null, def?.Study?.Skill.ToString());
                        break;
                }

                return list;
            }

            var c = place.Layout.Connection(targetId);
            if (c != null && (c.Joins(room.Id) || (c.Kind == ConnectionKind.Entrance && c.A == room.Id)))
            {
                if (c.Kind == ConnectionKind.Entrance)
                {
                    list.Add(new ActionOption { Action = PlaceAction.Open, Label = "Leave", Enabled = true });
                    return list;
                }

                if (c.IsVertical) return list; // stairs and ladders are taken with the traverse command
                if (c.Kind == ConnectionKind.Opening) return list;
                if (c.Kind == ConnectionKind.Secret && !place.Found(c)) return list;
                var state = place.DoorOf(c);
                var guard = c.GuardedBy != null && !PlaceRules.HasPassage(place, c.GuardedBy) && place.GroupStateOf(c.GuardedBy) != GroupState.Peaceful
                    ? PlaceRules.Capital(place.Layout.Group(c.GuardedBy)?.Name ?? "The guards") + " are watching this door." : null;
                switch (state)
                {
                    case DoorState.Closed:
                        Add(PlaceAction.Open, "Open", guard);
                        if (c.KeyItem != null && PlaceCommandHandlers.HasItem(p, c.KeyItem)) Add(PlaceAction.Lock, "Lock");
                        Add(PlaceAction.Listen, "Listen at the door", null, "Perception");
                        break;
                    case DoorState.Open:
                        Add(PlaceAction.Close, "Close");
                        break;
                    case DoorState.Locked:
                        Add(PlaceAction.Unlock, "Unlock", c.KeyItem == null ? "There is no key for it here." : !PlaceCommandHandlers.HasItem(p, c.KeyItem) ? "You do not have its key." : null);
                        Add(PlaceAction.Pick, "Pick the lock", c.LockDc <= 0 ? "The lock cannot be picked." : !PlaceCommandHandlers.HasItem(p, "thieves_tools") ? "You need thieves' tools." : guard,
                            "Sleight of Hand");
                        Add(PlaceAction.Force, "Force it", c.LockDc <= 0 ? "It will not give." : guard, "Athletics");
                        Add(PlaceAction.Listen, "Listen at the door", null, "Perception");
                        break;
                    case DoorState.Barred:
                        if (room.Id == c.A) Add(PlaceAction.Open, "Lift the bar");
                        else Add(PlaceAction.Force, "Force it", c.LockDc <= 0 ? "It will not give." : null, "Athletics");
                        Add(PlaceAction.Listen, "Listen at the door", null, "Perception");
                        break;
                }

                return list;
            }

            var g = place.Layout.Group(targetId);
            if (g != null && place.GroupStateOf(g.Id) != GroupState.Defeated && (g.Room == room.Id || PlaceRules.InSight(place, g.Room, room.Id)))
                Add(PlaceAction.Attack, "Attack " + g.Name);
            return list;
        }

        private static string OwnerReason(PlaceData place, PlaceFeature f)
        {
            if (f.Get("owned") == "yes") return "It belongs to the household.";
            var owner = place.Layout.Group(f.Get("owner_group"));
            return owner != null ? "It belongs to " + owner.Name + "." : "It is not yours.";
        }

        private static bool HostileHere(PlaceData place, PlaceRoom room)
        {
            foreach (var g in place.Layout.Groups)
            {
                if (g.Room != room.Id || g.Behaviour == GroupBehaviour.Guard) continue;
                var s = place.GroupStateOf(g.Id);
                if (s == GroupState.Present || s == GroupState.Survived || s == GroupState.Fighting) return true;
            }

            return false;
        }

        public static CommandResult Do(CommandContext context, WorldData w, PlaceData place, PlaceRoom room, string targetId, PlaceAction action)
        {
            var options = Options(w, place, room, targetId);
            var option = options.Find(o => o.Action == action);
            if (option == null) return CommandResult.InvalidState("That cannot be done here.");
            if (!option.Enabled) return CommandResult.InvalidState(option.Reason);
            var p = w.Player;
            var report = QuestRuntime.Reporter(context);
            var dark = PlaceRules.Dark(w, room);
            var now = QuestClock.Now(w);

            if (action == PlaceAction.Search)
            {
                var limit = PlaceRules.AttemptsLeft(place, room.Id, "search", out var key);
                if (limit != null) return CommandResult.InvalidState(limit);
                // One roll, against the hardest thing hidden here; easier things are found with it.
                var roll = PlaceRules.Check(w, Skill.Investigation, 0, key, dark);
                var found = new List<string>();
                foreach (var f in place.Layout.Features)
                {
                    if (f.Room != room.Id || !f.Hidden || place.Has(f.Id, FeatureFlags.Found)) continue;
                    if (roll.Die != 1 && (roll.Die == 20 || roll.Total >= f.FindDc))
                    {
                        var s = place.StateOf(f.Id);
                        s.Flags |= FeatureFlags.Found;
                        s.ChangedAt = now;
                        found.Add(f.Kind == FeatureKind.Trap ? (TrapCatalog.Find(f.Get("trap"))?.Seen ?? "a trap") : PlaceRules.Name(f).ToLowerInvariant());
                    }
                }

                foreach (var c in place.Layout.Connections)
                    if (c.Kind == ConnectionKind.Secret && c.Joins(room.Id) && !place.Found(c) && roll.Die != 1 && (roll.Die == 20 || roll.Total >= c.FindDc))
                    {
                        PlaceRules.Find(context, place, c);
                        found.Add("a hidden way through the wall");
                    }

                var text = $"You search the {room.Name.ToLowerInvariant()} (Investigation {roll.Total}{(dark ? ", in the dark" : string.Empty)}).";
                if (found.Count == 0)
                {
                    PlaceRules.Failed(place, room.Id, "search");
                    return CommandResult.Ok(text + " You find nothing more" + (dark ? " — a light might show more." : "."));
                }

                context.Quest(new QuestEvent { Kind = QuestEventKind.Investigated, Subject = place.PlaceId + "#" + room.Id });
                return CommandResult.Ok(text + " You find: " + string.Join("; ", found) + ".");
            }

            var feature = place.Layout.Feature(targetId);
            if (feature != null) return OnFeature(context, w, place, room, feature, action, dark);
            var conn = place.Layout.Connection(targetId);
            if (conn != null) return OnPassage(context, w, place, room, conn, action);
            var group = place.Layout.Group(targetId);
            if (group != null && action == PlaceAction.Attack)
            {
                if (group.Behaviour == GroupBehaviour.Guard)
                {
                    var faction = group.FactionId.IsEmpty ? place.FactionId : group.FactionId;
                    if (!faction.IsEmpty) Reputation.Change(w, "faction:" + faction, "Standing", -15, "attacked:" + place.PlaceId + ":" + group.Id, "attacked the garrison of " + place.Name, report);
                    Reputation.Change(w, "notoriety", "Notoriety", 5, "attacked:" + place.PlaceId + ":" + group.Id + ":notoriety", "attacked the garrison of " + place.Name, report);
                }

                return PlaceEncounters.Start(context, w, place, new List<PlaceGroup> { group }, place.Layout.Room(group.Room) ?? room, "You attack: ");
            }

            return CommandResult.InvalidState("That cannot be done here.");
        }

        private static CommandResult OnFeature(CommandContext context, WorldData w, PlaceData place, PlaceRoom room, PlaceFeature f, PlaceAction action, bool dark)
        {
            var p = w.Player;
            var report = QuestRuntime.Reporter(context);
            var def = FeatureCatalog.Find(f.DefinitionId);
            var s = place.StateOf(f.Id);
            var now = QuestClock.Now(w);
            switch (action)
            {
                case PlaceAction.Inspect:
                {
                    if (f.Kind == FeatureKind.Trap)
                    {
                        var trap = TrapCatalog.Find(f.Get("trap"));
                        s.Flags |= FeatureFlags.Examined;
                        return CommandResult.Ok(trap.Seen + (s.Has(FeatureFlags.Disarmed) ? " It has been made safe." : $" ({trap.Name}: disarming needs {PlaceRules.SkillName(trap.DisarmSkill)}{(trap.DisarmTool != null ? " and " + ItemCatalog.Find(trap.DisarmTool)?.Name.ToLowerInvariant() : string.Empty)}.)"));
                    }

                    var look = def?.Look ?? PlaceRules.Name(f);
                    if (def?.Study == null || s.Has(FeatureFlags.Examined))
                    {
                        s.Flags |= FeatureFlags.Examined;
                        return CommandResult.Ok(look + (s.Has(FeatureFlags.Examined) && def?.Insight != null && s.Attempts == 0 ? " " + def.Insight : string.Empty));
                    }

                    var limit = PlaceRules.AttemptsLeft(place, f.Id, "study", out var key);
                    if (limit != null) return CommandResult.InvalidState(look + " " + limit);
                    var roll = PlaceRules.Check(w, def.Study.Skill, def.Study.Dc, key, dark);
                    if (!roll.Success)
                    {
                        PlaceRules.Failed(place, f.Id, "study");
                        s.Attempts++;
                        return CommandResult.Ok($"{look} ({roll.Text}.) {def.Missed}" + (dark ? " A light would help." : string.Empty));
                    }

                    s.Flags |= FeatureFlags.Examined;
                    s.Attempts = 0;
                    s.ChangedAt = now;
                    Learn(context, w, place, f, def, report);
                    return CommandResult.Ok($"{look} ({roll.Text}.) {def.Insight}");
                }
                case PlaceAction.Read:
                {
                    s.Flags |= FeatureFlags.Read | FeatureFlags.Examined;
                    s.ChangedAt = now;
                    Learn(context, w, place, f, def, report);
                    return CommandResult.Ok(def.Look + " " + (def.Insight ?? string.Empty));
                }
                case PlaceAction.Open:
                {
                    var key = PlaceContents.Key(place.PlaceId, f.Id);
                    var box = w.FindContainer(key);
                    s.Flags |= FeatureFlags.Opened;
                    s.ChangedAt = now;
                    box.Opened = true;
                    foreach (var g in place.Layout.Groups)
                        if (g.WakesOn == f.Id && place.GroupStateOf(g.Id) == GroupState.Present)
                            return PlaceEncounters.Start(context, w, place, new List<PlaceGroup> { g }, place.Layout.Room(g.Room) ?? room, "As the lid shifts, ");
                    var count = 0;
                    foreach (var x in box.Items) count += x.Quantity;
                    return CommandResult.Ok(box.Items.Count == 0 && box.Coin == 0 ? $"{box.Name}: empty." : $"{box.Name}: {count} item{(count == 1 ? string.Empty : "s")}{(box.Coin > 0 ? $" and {box.Coin} coin" : string.Empty)}.");
                }
                case PlaceAction.Disarm:
                {
                    var trap = TrapCatalog.Find(f.Get("trap"));
                    var limit = PlaceRules.AttemptsLeft(place, f.Id, "disarm", out var key);
                    if (limit != null) return CommandResult.InvalidState(limit);
                    var roll = PlaceRules.Check(w, trap.DisarmSkill, trap.DisarmDc, key, dark);
                    if (!roll.Success)
                    {
                        PlaceRules.Failed(place, f.Id, "disarm");
                        if (roll.Dc - roll.Total >= trap.SpringsOnFailBy || roll.Die == 1)
                        {
                            var sprung = PlaceCommandHandlers.Spring(context, w, place, f, trap, "a slip while disarming it");
                            return CommandResult.Ok($"{roll.Text}. Your hand slips. " + sprung.Message);
                        }

                        return CommandResult.Ok($"{roll.Text}. You cannot work it loose, but nothing goes off.");
                    }

                    s.Flags |= FeatureFlags.Disarmed | FeatureFlags.Found;
                    s.ChangedAt = now;
                    var salvage = "";
                    if (trap.Salvage != null && ItemCatalog.Find(trap.Salvage) is ItemDefinition item)
                    {
                        var stack = ItemStacks.Create(ItemCommandHandlers.NewStackId(context), item, item.Stackable ? 4 : 1, "Salvaged from a trap in " + place.Name);
                        ItemCommandHandlers.Merge(p.Inventory, stack);
                        salvage = $" You keep the {item.Name.ToLowerInvariant()}.";
                    }

                    place.Note(p.Name + " disarmed the " + trap.Name.ToLowerInvariant() + ".");
                    context.Quest(new QuestEvent { Kind = QuestEventKind.TrapDisarmed, Subject = place.PlaceId, Detail = f.Id });
                    return CommandResult.Ok($"{roll.Text}. The {trap.Name.ToLowerInvariant()} is safe.{salvage}");
                }
                case PlaceAction.Activate:
                {
                    s.Flags |= FeatureFlags.Used;
                    s.ChangedAt = now;
                    var said = def?.Name + " moves with a groan.";
                    var opens = place.Layout.Connection(f.Get("opens"));
                    if (opens != null)
                    {
                        place.Doors[opens.Id] = DoorState.Open;
                        said += " Somewhere near, something heavy grinds open.";
                    }

                    context.Quest(new QuestEvent { Kind = QuestEventKind.FeatureUsed, Subject = place.PlaceId, Detail = f.Id });
                    foreach (var g in place.Layout.Groups)
                        if (g.WakesOn == f.Id && place.GroupStateOf(g.Id) == GroupState.Present)
                            return PlaceEncounters.Start(context, w, place, new List<PlaceGroup> { g }, place.Layout.Room(g.Room) ?? room, said + " ");
                    return CommandResult.Ok(said);
                }
                case PlaceAction.Free:
                {
                    var npc = w.FindNpc(new Domain.Identifiers.NpcId(Guid.TryParse(f.Get("npc"), out var gid) ? gid : Guid.Empty));
                    var escort = npc == null ? null : w.FindEscort(npc.NpcId);
                    if (npc == null || npc.IsDeceased || escort == null) return CommandResult.InvalidState("There is nobody left to free.");
                    if (PlacePresence.Followers(w).Count >= WorldValidator.MaxFollowers) return CommandResult.InvalidState("You cannot look after anyone else as well.");
                    var limit = PlaceRules.AttemptsLeft(place, f.Id, "free", out var key);
                    if (limit != null) return CommandResult.InvalidState(limit);
                    var roll = PlaceRules.Check(w, Skill.Athletics, 10, key);
                    if (!roll.Success)
                    {
                        PlaceRules.Failed(place, f.Id, "free");
                        return CommandResult.Ok($"{roll.Text}. The timber will not shift yet. {npc.Name} groans.");
                    }

                    s.Flags |= FeatureFlags.Freed | FeatureFlags.Used;
                    s.ChangedAt = now;
                    escort.State = EscortState.Following;
                    escort.PlaceId = null;
                    escort.Room = null;
                    escort.ChangedAt = now;
                    npc.PlayerRegard.Gratitude += 10;
                    place.Note(p.Name + " freed " + npc.Name + ".");
                    context.Quest(new QuestEvent { Kind = QuestEventKind.Rescued, Subject = npc.NpcId.ToString(), Settlement = place.SettlementId });
                    context.ReportEvent(HistoricalEventType.CaptiveFreed, EventSeverity.Positive, EventImportance.Minor, PlaceKnowledge.Settlements(place),
                        new Dictionary<string, string> { ["npc"] = npc.Name, ["place"] = place.Name, ["by"] = p.Name }, npcIds: new[] { npc.NpcId });
                    return CommandResult.Ok($"{roll.Text}. You heave the timber aside and cut the webbing. {npc.Name} stands, shaking: \"Get me out of here. Please.\" ({npc.Name} follows you.)");
                }
            }

            return CommandResult.InvalidState("That cannot be done here.");
        }

        private static void Learn(CommandContext context, WorldData w, PlaceData place, PlaceFeature f, FeatureDefinition def, QuestReporter report)
        {
            context.Quest(new QuestEvent { Kind = QuestEventKind.Investigated, Subject = place.PlaceId + "#" + f.Id });
            context.Quest(new QuestEvent { Kind = QuestEventKind.FeatureUsed, Subject = place.PlaceId, Detail = f.Id });
            if (def?.Discovery == null) return;
            if (QuestRuntime.Discover(w, def.Discovery, def.Insight, place.Name, report))
                context.Quest(new QuestEvent { Kind = QuestEventKind.Discovered, Subject = def.Discovery });
            if (def.Discovery == "fort_side_door")
                foreach (var other in w.Places)
                    if (other.DefinitionId == PlaceCatalog.Fort) other.Variables["entrance:side_door"] = "known";
        }

        private static CommandResult OnPassage(CommandContext context, WorldData w, PlaceData place, PlaceRoom room, PlaceConnection c, PlaceAction action)
        {
            var p = w.Player;
            var name = c.Kind == ConnectionKind.Secret ? "the hidden way" : "the door";
            switch (action)
            {
                case PlaceAction.Open:
                    if (c.Kind == ConnectionKind.Entrance) return CommandResult.InvalidState("Use the way out to leave.");
                    place.Doors[c.Id] = DoorState.Open;
                    return CommandResult.Ok(place.DoorOf(c) == DoorState.Open && c.Door == DoorState.Barred ? "You lift the bar and swing it open." : $"You open {name}.");
                case PlaceAction.Close:
                    place.Doors[c.Id] = DoorState.Closed;
                    return CommandResult.Ok($"You close {name}.");
                case PlaceAction.Lock:
                    place.Doors[c.Id] = DoorState.Locked;
                    return CommandResult.Ok("You lock it. The key stays with you.");
                case PlaceAction.Unlock:
                    place.Doors[c.Id] = DoorState.Closed;
                    return CommandResult.Ok($"The {ItemCatalog.Find(c.KeyItem)?.Name.ToLowerInvariant() ?? "key"} turns. It is unlocked (the key stays with you).");
                case PlaceAction.Pick:
                case PlaceAction.Force:
                {
                    var limit = PlaceRules.AttemptsLeft(place, c.Id, action.ToString(), out var key);
                    if (limit != null) return CommandResult.InvalidState(limit);
                    var roll = action == PlaceAction.Pick ? PlaceRules.Check(w, Skill.SleightOfHand, c.LockDc, key, PlaceRules.Dark(w, room)) : PlaceRules.Check(w, Skill.Athletics, c.LockDc + 2, key);
                    if (!roll.Success)
                    {
                        PlaceRules.Failed(place, c.Id, action.ToString());
                        return CommandResult.Ok($"{roll.Text}. It holds.");
                    }

                    place.Doors[c.Id] = action == PlaceAction.Pick ? DoorState.Closed : DoorState.Broken;
                    if (action == PlaceAction.Force)
                    {
                        // Noise: whoever is near hears it.
                        foreach (var g in place.Layout.Groups)
                            if (g.Room == c.A || g.Room == c.B || PlaceRules.InSight(place, g.Room, room.Id)) place.GroupOf(g.Id).Alerted = true;
                        place.Note(p.Name + " broke a door in " + place.Name + ".");
                        return CommandResult.Ok($"{roll.Text}. The door gives with a crash — anyone near will have heard.");
                    }

                    return CommandResult.Ok($"{roll.Text}. The lock clicks back.");
                }
                case PlaceAction.Listen:
                {
                    var limit = PlaceRules.AttemptsLeft(place, c.Id, "listen", out var key);
                    if (limit != null) return CommandResult.InvalidState(limit);
                    var roll = PlaceRules.Check(w, Skill.Perception, 10, key);
                    PlaceRules.Failed(place, c.Id, "listen");
                    if (!roll.Success) return CommandResult.Ok($"{roll.Text}. You hear nothing you can be sure of.");
                    var beyond = c.Other(room.Id);
                    foreach (var g in place.Layout.Groups)
                    {
                        var s = place.GroupStateOf(g.Id);
                        if (g.Room == beyond && s != GroupState.Defeated)
                            return CommandResult.Ok($"{roll.Text}. Movement beyond the door — " + (g.Members.Exists(m => m.NpcId.IsEmpty && NotHuman(m.CreatureId)) ? "something not human." : "voices, low."));
                    }

                    return CommandResult.Ok($"{roll.Text}. Silence beyond.");
                }
            }

            return CommandResult.InvalidState("That cannot be done here.");
        }

        private static bool NotHuman(string id) => Domain.Combat.CreatureCatalog.Find(id)?.Type != "Humanoid";
    }
}
