using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Places
{
    /// <summary>Where the player is, as far as resting goes.</summary>
    public enum RestSite
    {
        /// <summary>In a settlement: lodging, or a corner of the street. Safe.</summary>
        Lodging = 0,

        /// <summary>On the map, in open country: a camp.</summary>
        Camp = 1,

        /// <summary>In a place, in a room with no hazard, no foes in sight and nobody's private room.</summary>
        PlaceRoom = 2,
    }

    /// <summary>What resting here would mean: allowed or why not; and, for a long rest, who would break it.</summary>
    public sealed class RestCheck
    {
        public RestSite Site { get; set; }
        public string Problem { get; set; }

        /// <summary>A long rest here is broken by this group of the place's inhabitants (null: an undisturbed night).</summary>
        public string Interrupter { get; set; }

        public string Describe => Problem ?? (Site == RestSite.Lodging ? "Safe to rest here." : Site == RestSite.Camp ? "You can make camp here." :
            Interrupter != null ? "You can rest here, but not unseen: something still lives in this place." : "A quiet corner: you can rest here.");
    }

    /// <summary>
    /// Phase 14 (ADR-0068): where a rest is allowed. A settlement is safe and the open country is a camp. In a place,
    /// a room is refused while it holds a hazard (unless the fire has burnt out), foes in sight, someone's private room,
    /// or a garrison's restricted ground. An interior that is not the player's is refused: you do not sleep in a stranger's
    /// house. A long rest in a place where inhabitants still stand is interrupted: the nearest group finds the player.
    /// Deterministic: the same place, visit and room give the same answer.
    /// </summary>
    public static class RestRules
    {
        public static RestCheck Check(WorldData w, RestKind kind)
        {
            var p = w.Player;
            var check = new RestCheck();
            if (p == null) { check.Problem = "There is no player."; return check; }
            if (p.Rpg != null && LastingConditions.Has(p.Rpg, ConditionType.Restrained, QuestClock.Now(w)))
            {
                check.Problem = "You cannot rest while restrained: break free first.";
                return check;
            }

            if (!p.IsInPlace)
            {
                check.Site = p.IsInSettlement ? RestSite.Lodging : RestSite.Camp;
                return check;
            }

            check.Site = RestSite.PlaceRoom;
            var place = PlaceRules.Current(w);
            var room = PlaceRules.CurrentRoom(w);
            if (place == null || room == null) { check.Problem = "You are nowhere you can rest."; return check; }
            if (place.Category == PlaceCategory.Interior)
            {
                check.Problem = "This is someone's house: you cannot make yourself at home here. Find lodging, or rest outside.";
                return check;
            }

            var hazard = PlaceRules.HazardOf(place, room);
            if (hazard != null && !hazard.Dark && !(hazard.BurnsMinutes > 0 && PlaceRules.FireState(w, place, room) == "burnt out"))
            {
                check.Problem = $"Not here: {hazard.Name.ToLowerInvariant()}. Find a safer room.";
                return check;
            }

            if (room.Restricted && !PlaceRules.HasLeave(place))
            {
                check.Problem = "Not here: you have no business in this room.";
                return check;
            }

            foreach (var g in place.Layout.Groups)
            {
                var s = place.GroupStateOf(g.Id);
                if (s == GroupState.Defeated || s == GroupState.Departed || s == GroupState.Peaceful) continue;
                if (g.WakesOn != null && !place.GroupOf(g.Id).Woken) continue; // the sleeping dead stay asleep unless disturbed
                if (g.Room == room.Id || PlaceRules.InSight(place, g.Room, room.Id))
                {
                    check.Problem = $"Not with {g.Name} so close.";
                    return check;
                }

                if (kind == RestKind.Long && check.Interrupter == null && g.Behaviour != GroupBehaviour.Guard) check.Interrupter = g.Id;
            }

            return check;
        }

        public static string Key(WorldData w) => (w.Player.PlaceId ?? "out") + ":" + (w.Player.PlaceRoom ?? string.Empty) + ":" + (PlaceRules.Current(w)?.Visits ?? 0).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Phase 14: the night is broken — the group that found the sleeping player attacks in the player's room.</summary>
    public sealed class InterruptRestCommand : ICommand
    {
        public InterruptRestCommand(string groupId) { GroupId = groupId; }
        public string GroupId { get; }
    }

    /// <summary>Phase 14 (ADR-0068): try to break free of a lasting restraint (a net): Athletics or Acrobatics against its DC.</summary>
    public sealed class BreakFreeCommand : ICommand
    {
    }

    public static class RestCommandHandlers
    {
        public static void Register(CommandRegistry commands)
        {
            commands.Register<InterruptRestCommand>(new InterruptHandler());
            commands.Register<BreakFreeCommand>(new BreakFreeHandler());
        }

        private sealed class InterruptHandler : ICommandHandler<InterruptRestCommand>
        {
            public CommandResult Handle(InterruptRestCommand c, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                if (context.Committed.ActiveEncounter != null) return CommandResult.InvalidState("A fight is already going on.");
                var w = context.Edit();
                var place = PlaceRules.Current(w);
                var room = PlaceRules.CurrentRoom(w);
                var g = place?.Layout.Group(c.GroupId);
                if (g == null || room == null) return CommandResult.NotFound("Nothing comes.");
                var s = place.GroupStateOf(g.Id);
                if (s == GroupState.Defeated || s == GroupState.Departed) return CommandResult.Ok("The night passes undisturbed.");
                place.GroupOf(g.Id).Alerted = true;
                place.Note(PlaceRules.Capital(g.Name) + " found " + w.Player.Name + " resting.");
                return PlaceEncounters.Start(context, w, place, new List<PlaceGroup> { g }, room, "Woken in the dark — ");
            }
        }

        private sealed class BreakFreeHandler : ICommandHandler<BreakFreeCommand>
        {
            public CommandResult Handle(BreakFreeCommand c, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                var p = context.Committed.Player;
                if (p?.Rpg == null) return CommandResult.InvalidState("Create your character first.");
                if (context.Committed.ActiveEncounter != null) return CommandResult.InvalidState("In a fight, use Escape.");
                var now = QuestClock.Now(context.Committed);
                var held = LastingConditions.Find(p.Rpg, ConditionType.Restrained, now);
                if (held == null) return CommandResult.InvalidState("You are not held by anything.");
                var w = context.Edit();
                var k = LastingConditions.Find(w.Player.Rpg, ConditionType.Restrained, now);
                if (k.EscapeTries >= LastingConditions.EscapeTries)
                {
                    LastingConditions.Remove(w.Player.Rpg, ConditionType.Restrained);
                    return CommandResult.Ok($"You work yourself free of {(string.IsNullOrEmpty(k.Reason) ? "it" : k.Reason)}, slowly ({LastingConditions.WorkFreeMinutes} minutes).");
                }

                var rules = PlaceRules.Rules(w);
                var skill = rules.SkillBonus(Skill.Athletics).Total >= rules.SkillBonus(Skill.Acrobatics).Total ? Skill.Athletics : Skill.Acrobatics;
                var roll = PlaceRules.Check(w, skill, k.EscapeDc, "escape:" + k.Source + ":" + k.AppliedAt.ToString(CultureInfo.InvariantCulture) + ":" + k.EscapeTries.ToString(CultureInfo.InvariantCulture));
                if (roll.Success)
                {
                    LastingConditions.Remove(w.Player.Rpg, ConditionType.Restrained);
                    return CommandResult.Ok($"{roll.Text}. You tear free.");
                }

                k.EscapeTries++;
                return CommandResult.Ok($"{roll.Text}. Still held{(k.EscapeTries >= LastingConditions.EscapeTries ? " — but it is giving; one more try and you will work free, given time" : string.Empty)}.");
            }
        }
    }
}
