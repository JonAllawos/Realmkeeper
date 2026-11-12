using System;
using System.Collections.Generic;
using Realmkeeper.Application.Atlas;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    public sealed partial class WorldSession
    {
        /// <summary>Phase 16: raised when a journey reaches its settlement (the exploration module enters it).</summary>
        public event Action<SettlementId> Arrived;

        /// <summary>The words of the last travel run, hour by hour (for the travel panel).</summary>
        public IReadOnlyList<string> LastTravel { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// Phase 16 (ADR-0078): travels for up to <paramref name="maxHours"/> hours. Each hour is one
        /// <see cref="TravelHourCommand"/> (the walking, the road's checks) and then one hour of the world's clock
        /// (<see cref="AdvanceTimeCommand"/>: a simulated day at each midnight). It stops on arrival, at anything that bars
        /// the way, when a fight begins, and (unless <paramref name="throughTheNight"/>) at nightfall.
        /// </summary>
        public CommandResult Travel(int maxHours = 24, bool throughTheNight = false)
        {
            if (!HasWorld) return CommandResult.InvalidState("There is no active world.");
            var lines = new List<string>();
            CommandResult last = CommandResult.InvalidState("You are not travelling.");
            for (var h = 0; h < Math.Max(1, maxHours); h++)
            {
                var j = _world.Journey;
                if (j == null || j.Status != JourneyStatus.Travelling) break;
                if (!throughTheNight && h > 0 && TravelRules.IsNight(_world.MinuteOfDay))
                {
                    Dispatch(new HaltJourneyCommand());
                    lines.Add("Night falls: you make camp by the road (rest, or press on at half pace).");
                    break;
                }

                last = Dispatch(new TravelHourCommand());
                if (!last.IsSuccess) break;
                lines.Add(last.Message);
                var arrived = _world.Journey?.Status == JourneyStatus.Arrived;
                var stopped = _world.Journey?.Status != JourneyStatus.Travelling;
                var time = Execute(new AdvanceTimeCommand(60));
                if (!time.IsSuccess) { lines.Add(time.Message); break; }
                FollowPlayersRegion();
                if (arrived)
                {
                    Arrived?.Invoke(_world.Journey.DestinationId);
                    break;
                }

                if (stopped) break;
            }

            LastTravel = lines;
            return lines.Count == 0 ? last : CommandResult.Ok(string.Join("\n", lines));
        }
    }
}

namespace Realmkeeper.Application
{
    public sealed partial class WorldSession
    {
        /// <summary>
        /// Phase 16: plays a fight met on the world map to its end through the one engine and its commands (the AI chooses
        /// the player's actions as it does any combatant's; reactions are accepted). Fights met in a settlement or a place
        /// are played in the scene with full tactical control; on the map there is no scene, so this is how the road's
        /// fights are fought there. Returns the last lines of the fight's log.
        /// </summary>
        public CommandResult FightItOut(int maxSteps = 800)
        {
            if (!HasWorld) return CommandResult.InvalidState("There is no active world.");
            for (var i = 0; i < maxSteps; i++)
            {
                var snap = GetEncounter();
                if (snap == null || snap.Status != Domain.Combat.EncounterStatus.Active) break;
                if (snap.Prompt != null) { Dispatch(new RespondToReactionCommand(snap.Id, snap.Prompt.Id, true)); continue; }
                if (snap.IsPlayersTurn)
                {
                    var copy = _world.DeepClone();
                    var engine = new Domain.Combat.CombatEngine(copy, copy.ActiveEncounter);
                    var act = Domain.Combat.CombatAi.Next(engine, engine.Encounter.Find(snap.Player.Id));
                    if (!Dispatch(new CombatActionCommand(snap.Id, snap.Player.Id, act)).IsSuccess) Dispatch(new CombatActionCommand(snap.Id, snap.Player.Id, new Domain.Combat.EndTurnAction()));
                    continue;
                }

                Dispatch(new RunAiTurnCommand(snap.Id));
            }

            var last = GetEncounter(true);
            if (last == null) return CommandResult.InvalidState("There was no fight.");
            var lines = new List<string>();
            for (var i = Math.Max(0, last.Log.Count - 8); i < last.Log.Count; i++) lines.Add(last.Log[i].Text);
            return CommandResult.Ok(string.Join("\n", lines));
        }
    }
}
