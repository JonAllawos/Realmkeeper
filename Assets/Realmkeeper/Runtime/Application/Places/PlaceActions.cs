using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Combat;
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
    public static partial class PlaceActions
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
                        if (box != null && !box.PlayerAccess && (box.Items.Count > 0 || box.Coin > 0))
                        {
                            // Phase 14 (ADR-0069): what is not yours can still be taken — at a price, if anyone sees.
                            Add(PlaceAction.Steal, "Take it anyway (theft)");
                            Add(PlaceAction.StealQuietly, "Take it quietly (theft)", null, "Sleight of Hand");
                        }

                        if (def?.Study != null) Add(PlaceAction.Inspect, "Examine", null, def.Study.Skill.ToString());
                        break;
                    case FeatureKind.Document:
                        Add(PlaceAction.Read, "Read", PlaceRules.Dark(w, room) ? "Too dark to read without a light." :
                            LastingConditions.Has(p.Rpg, ConditionType.Blinded, QuestClock.Now(w)) ? "You cannot see to read (blinded)." : null);
                        break;
                    case FeatureKind.Captive:
                        if (!s.Has(FeatureFlags.Freed)) Add(PlaceAction.Free, "Free them", HostileHere(place, room) ? "Not with enemies in the room." : null, "Athletics");
                        break;
                    case FeatureKind.Mechanism:
                        if (f.Get(Phase18Places.Mech) != null) { MechanismOptions(w, place, f, s, def, list); break; } // Phase 18 (ADR-0089)
                        Add(PlaceAction.Activate, s.Has(FeatureFlags.Used) ? "Pull it again" : "Pull it");
                        if (def?.Study != null) Add(PlaceAction.Inspect, "Examine", null, def.Study.Skill.ToString());
                        break;
                    default:
                        if (f.Get(Phase18Places.Mech) == Phase18Places.MechLight) { MechanismOptions(w, place, f, s, def, list); break; } // Phase 18: a sign read by raking light
                        Add(PlaceAction.Inspect, f.Kind == FeatureKind.Workstation || f.Kind == FeatureKind.Furniture ? "Look" : "Examine", null, def?.Study?.Skill.ToString());
                        if (f.DefinitionId == "webbing" && !s.Has(FeatureFlags.Broken)) // Phase 14 (ADR-0071): fire meets what burns
                            Add(PlaceAction.Burn, "Burn it away", PlaceRules.CarriesLight(p) ? null : "You need a flame: a lantern or a torch.");
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
            var gs = g == null ? GroupState.Defeated : place.GroupStateOf(g.Id);
            if (g != null && gs != GroupState.Defeated && gs != GroupState.Departed && (g.Room == room.Id || PlaceRules.InSight(place, g.Room, room.Id)))
            {
                if (g.Behaviour == GroupBehaviour.Parley && gs == GroupState.Present)
                {
                    // Phase 14 (ADR-0071): outlaws on a road talk before they fight.
                    Add(PlaceAction.PayToll, $"Pay their toll ({Toll} coin)", p.Purse < Toll ? $"You have only {p.Purse} coin." : null);
                    Add(PlaceAction.Persuade, "Talk them off the road", null, "Persuasion");
                    Add(PlaceAction.Intimidate, "Threaten them", null, "Intimidation");
                }

                Add(PlaceAction.Attack, "Attack " + g.Name);
            }

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
            if (group != null && (action == PlaceAction.PayToll || action == PlaceAction.Persuade || action == PlaceAction.Intimidate))
                return Parley(context, w, place, room, group, action);
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
            if (f.Get(Phase18Places.Mech) != null && action != PlaceAction.Inspect)
            {
                var worked = Mechanism(context, w, place, room, f, def, s, action); // Phase 18 (ADR-0089)
                if (worked != null) return worked;
            }

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
                    var woke = Wake(context, w, place, room, f, "As the lid shifts, ");
                    if (woke != null) return woke;
                    var count = 0;
                    foreach (var x in box.Items) count += x.Quantity;
                    return CommandResult.Ok(box.Items.Count == 0 && box.Coin == 0 ? $"{box.Name}: empty." : $"{box.Name}: {count} item{(count == 1 ? string.Empty : "s")}{(box.Coin > 0 ? $" and {box.Coin} coin" : string.Empty)}.");
                }
                case PlaceAction.Steal:
                case PlaceAction.StealQuietly:
                    return Steal(context, w, place, room, f, action == PlaceAction.StealQuietly);
                case PlaceAction.Burn:
                {
                    s.Flags |= FeatureFlags.Broken | FeatureFlags.Found;
                    s.ChangedAt = now;
                    place.Note(p.Name + " burned the webbing in the " + room.Name.ToLowerInvariant() + ".");
                    var said = "You put the flame to the webbing. It flares, shrivels and drops away in burning threads.";
                    foreach (var g in place.Layout.Groups)
                        if (g.Room == room.Id && (place.GroupStateOf(g.Id) == GroupState.Present || place.GroupStateOf(g.Id) == GroupState.Survived) && w.ActiveEncounter == null)
                            return PlaceEncounters.Start(context, w, place, new List<PlaceGroup> { g }, room, said + " Something above you screams — ");
                    return CommandResult.Ok(said + " The way is clear of it.");
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
                    if (f.DefinitionId == "sluice_wheel" && !place.Variables.ContainsKey("drained"))
                    {
                        // Phase 14: the culverts' sluice drops the water in the channels; the deep water there is gone for good.
                        place.Variables["drained"] = "yes";
                        said += " Far off, water roars through the sluice, and the channels begin to drop.";
                    }

                    var stirred = Wake(context, w, place, room, f, said + " ");
                    if (stirred != null) return stirred;
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
                    escort.PlaceId = place.PlaceId; // Phase 14 (ADR-0070): a follower inside a place keeps a room
                    escort.Room = room.Id;
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

        public const int Toll = 10;

        /// <summary>
        /// Phase 14 (ADR-0069): taking what is not the player's. Everything in the container moves to the pack. Taken quietly,
        /// the player's Sleight of Hand is set against each onlooker's passive Perception; taken openly, anyone who can see
        /// sees. The crime is recorded either way — undiscovered if nobody saw — and its consequences follow only from
        /// what the witnesses could tell.
        /// </summary>
        private static CommandResult Steal(CommandContext context, WorldData w, PlaceData place, PlaceRoom room, PlaceFeature f, bool quietly)
        {
            var p = w.Player;
            var box = w.FindContainer(PlaceContents.Key(place.PlaceId, f.Id));
            if (box == null || (box.Items.Count == 0 && box.Coin == 0)) return CommandResult.InvalidState("There is nothing in it to take.");
            var quiet = -1;
            var text = string.Empty;
            if (quietly)
            {
                var roll = PlaceRules.Check(w, Skill.SleightOfHand, 0, PlaceHarm.Key(place, "steal:" + f.Id) + ":" + w.NextCrimeNumber.ToString(CultureInfo.InvariantCulture));
                quiet = roll.Total;
                text = $"Sleight of Hand {roll.Total}. ";
            }

            var witnesses = CrimeRules.Witnesses(w, place, room, f.X, f.Z, false, quiet);
            var taken = new Dictionary<string, int>();
            var value = box.Coin;
            foreach (var stack in new List<ItemStackData>(box.Items))
            {
                taken[stack.ItemId] = (taken.TryGetValue(stack.ItemId, out var n) ? n : 0) + stack.Quantity;
                value += (ItemCatalog.Find(stack.ItemId)?.BaseValue ?? 0) * stack.Quantity;
                box.Items.Remove(stack);
                ItemCommandHandlers.Merge(p.Inventory, stack);
            }

            var coin = box.Coin;
            p.Purse += coin;
            box.Coin = 0;
            var owner = OwnerOf(w, place, f);
            var shop = place.Category == PlaceCategory.Interior && (f.Get("stock") == "yes");
            var deed = (shop ? "took goods from " : "took what was in ") + (owner?.Name is string who ? who + "'s " : "the ") + PlaceRules.Name(f).ToLowerInvariant();
            var group = place.Layout.Group(f.Get("owner_group"));
            var faction = group != null && !group.FactionId.IsEmpty ? group.FactionId : place.FactionId;
            var crime = CrimeRules.Record(context, w, shop ? CrimeKind.ShopTheft : CrimeKind.Theft, place, room, owner?.NpcId ?? default, faction, deed, witnesses, taken, coin, value);
            var got = taken.Count + (coin > 0 ? 1 : 0);
            text += $"You take {(got == 0 ? "nothing" : string.Join(", ", new List<string>(taken.Keys).ConvertAll(id => ItemCatalog.Find(id)?.Name?.ToLowerInvariant() ?? id)) + (coin > 0 ? $" and {coin} coin" : string.Empty))}.";
            if (witnesses.Count == 0) return CommandResult.Ok(text + " Nobody sees.");
            var first = witnesses[0];
            var shout = first.Recognized ? $"\"{p.Name}! Put that back!\"" : "\"Thief! Stop!\"";
            return CommandResult.Ok($"{text} {first.Npc.Name} sees you ({first.How}): {shout}" + (crime.Bounty > 0 ? $" (Bounty: {crime.Bounty} coin in {w.FindSettlement(crime.SettlementId)?.Name ?? "these parts"}.)" : " They could not tell who you were."));
        }

        /// <summary>The person a household container belongs to: someone who lives or works in the building.</summary>
        private static NpcData OwnerOf(WorldData w, PlaceData place, PlaceFeature f)
        {
            if (place.LocationId.IsEmpty) return null;
            NpcData best = null;
            foreach (var n in w.Npcs)
                if (!n.IsDeceased && (n.ResidenceId == place.LocationId || n.WorkplaceId == place.LocationId) && (best == null || n.NpcId.CompareTo(best.NpcId) < 0)) best = n;
            return best;
        }

        /// <summary>
        /// Phase 14 (ADR-0071): the outlaws' parley. Paying the toll buys passage (they stay on the road). Talking them off
        /// it (Persuasion 14) or frightening them off (Intimidation 15) sends every outlaw group away for good, ends their
        /// hold on the trade road, and settles the road quest; a failed threat, or talk that goes badly wrong, starts the fight.
        /// </summary>
        private static CommandResult Parley(CommandContext context, WorldData w, PlaceData place, PlaceRoom room, PlaceGroup g, PlaceAction action)
        {
            var p = w.Player;
            var name = PlaceRules.Capital(g.Name);
            if (action == PlaceAction.PayToll)
            {
                p.Purse -= Toll;
                place.GroupOf(g.Id).State = GroupState.Peaceful;
                place.Note(p.Name + " paid the outlaws' toll.");
                return CommandResult.Ok($"You count out {Toll} coin. {name} step aside, grinning: \"Pleasure doing business.\" They are still here — and still on the road.");
            }

            var skill = action == PlaceAction.Persuade ? Skill.Persuasion : Skill.Intimidation;
            var dc = action == PlaceAction.Persuade ? 14 : 15;
            var limit = PlaceRules.AttemptsLeft(place, g.Id, action.ToString(), out var key);
            if (limit != null) return CommandResult.InvalidState(limit);
            var roll = PlaceRules.Check(w, skill, dc, key);
            if (!roll.Success)
            {
                PlaceRules.Failed(place, g.Id, action.ToString());
                if (action == PlaceAction.Intimidate || roll.Dc - roll.Total >= 5)
                    return PlaceEncounters.Start(context, w, place, new List<PlaceGroup> { g }, room, $"{roll.Text}. They laugh, and draw — ");
                return CommandResult.Ok($"{roll.Text}. They hear you out, and do not move.");
            }

            var how = action == PlaceAction.Persuade ? "talked off it" : "frightened off it";
            foreach (var other in place.Layout.Groups)
            {
                var s = place.GroupStateOf(other.Id);
                if (other.WakesOn != null || s == GroupState.Defeated) continue;
                place.GroupOf(other.Id).State = GroupState.Departed;
            }

            place.Variables["road"] = "left";
            place.Note($"The outlaws left {place.Name}, {how} by {p.Name}.");
            context.ReportEvent(HistoricalEventType.OutlawsDeparted, EventSeverity.Positive, EventImportance.Minor, PlaceKnowledge.Settlements(place),
                new Dictionary<string, string> { ["place"] = place.Name, ["how"] = how, ["by"] = p.Name });
            context.Quest(new QuestEvent { Kind = QuestEventKind.Decision, Subject = "crowfoot_dealt" });
            PlaceRules.UpdateDiscovery(context, w, place);
            return CommandResult.Ok(action == PlaceAction.Persuade
                ? $"{roll.Text}. They look at each other, then at the road. \"Plenty of other roads.\" By nightfall the den is empty."
                : $"{roll.Text}. Their nerve breaks. They gather what they can carry and are gone before you can count them.");
        }

        /// <summary>
        /// Phase 14 (ADR-0071): whoever sleeps beside this feature wakes, once. If the player can see them (same room, or
        /// through an open way) the fight starts now; otherwise they are awake and fight on sight when found. Nobody wakes
        /// through a wall, and nobody wakes twice.
        /// </summary>
        private static CommandResult Wake(CommandContext context, WorldData w, PlaceData place, PlaceRoom room, PlaceFeature f, string prefix)
        {
            var near = new List<PlaceGroup>();
            var far = new List<string>();
            foreach (var g in place.Layout.Groups)
            {
                if (g.WakesOn != f.Id || place.GroupStateOf(g.Id) != GroupState.Present || place.GroupOf(g.Id).Woken) continue;
                place.GroupOf(g.Id).Woken = true;
                context.ReportEvent(HistoricalEventType.CreaturesWoke, EventSeverity.Warning, EventImportance.Minor, PlaceKnowledge.Settlements(place),
                    new Dictionary<string, string> { ["group"] = PlaceRules.Capital(g.Name), ["place"] = place.Name, ["by"] = w.Player.Name, ["what"] = PlaceRules.Name(f) });
                if (g.Room == room.Id || PlaceRules.InSight(place, g.Room, room.Id)) near.Add(g);
                else far.Add(g.Name);
            }

            if (near.Count > 0) return PlaceEncounters.Start(context, w, place, near, room, prefix + (prefix.TrimEnd().EndsWith(",", StringComparison.Ordinal) ? "something wakes! " : "Something wakes! "));
            return far.Count > 0 ? CommandResult.Ok(prefix + "Somewhere beyond the walls, something stirs.") : null;
        }

        private static void Learn(CommandContext context, WorldData w, PlaceData place, PlaceFeature f, FeatureDefinition def, QuestReporter report)
        {
            context.Quest(new QuestEvent { Kind = QuestEventKind.Investigated, Subject = place.PlaceId + "#" + f.Id });
            context.Quest(new QuestEvent { Kind = QuestEventKind.FeatureUsed, Subject = place.PlaceId, Detail = f.Id });
            if (def == null) return;

            // Phase 18 (ADR-0088): every clue that tells something is recorded, with its kind and certainty — the authored
            // keys as before, the generic clues under their place (so the claw marks of one cave are not those of another).
            var key = def.Discovery ?? (def.Insight != null && def.Category != Domain.Quests.DiscoveryCategory.None ? place.DefinitionId + ":" + def.Id : null);
            if (key == null) return;
            var d = new Domain.Quests.DiscoveryData
            {
                Key = key, Text = def.Insight, Source = place.Name, Category = def.Category, Major = def.Major, PlaceId = place.PlaceId,
                Certainty = def.Unverified ? Domain.Quests.DiscoveryCertainty.Unverified : Domain.Quests.DiscoveryCertainty.Confirmed,
                ConfirmedBy = def.Unverified && def.ConfirmedBy != null && !def.ConfirmedBy.StartsWith("group:", StringComparison.Ordinal) ? def.ConfirmedBy
                    : def.ConfirmedBy == null ? null : def.ConfirmedBy + "@" + place.PlaceId,
                Subject = def.AboutHolder && !place.FactionId.IsEmpty ? "faction:" + place.FactionId : null,
            };
            if (QuestRuntime.Discover(w, d, report))
                context.Quest(new QuestEvent { Kind = QuestEventKind.Discovered, Subject = key });
            if (def.RevealsPlace != null)
                foreach (var other in w.Places)
                    if (other.DefinitionId == def.RevealsPlace && other.Discovery < PlaceDiscovery.Discovered)
                        PlaceKnowledge.Raise(w, other, PlaceDiscovery.Discovered, report, "clue");
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
