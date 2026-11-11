using System;
using Realmkeeper.Domain.Calendar;

namespace Realmkeeper.Application
{
    public sealed partial class WorldSession
    {
        /// <summary>Phase 8: combat is turn-based and never moves the clock; time waits until the fight ends.</summary>
        public const string EncounterBlocksTime = "A fight is going on: time stands still until it ends.";

        /// <summary>Hunger: the player's fed level falls this much on each hour mark the clock passes.</summary>
        public const int FedLostPerHour = 2;

        /// <summary>
        /// Advances the clock. Each midnight crossed runs one simulated day (the same transactional
        /// day as <see cref="AdvanceDaysCommand"/>); then the minute of the day is set and the player
        /// grows hungrier by the hour marks passed (counted on absolute time, so many small steps
        /// equal one large one, and nothing hidden needs saving). If a day fails, the clock stops at
        /// that midnight. The minute and hunger change in place: neither can make the world invalid.
        /// </summary>
        public AdvanceResult Execute(AdvanceTimeCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (IsClosed || !HasWorld) return new AdvanceResult(false, "There is no active world.", null, 0, null);
            if (_world.ActiveEncounter != null) return new AdvanceResult(false, EncounterBlocksTime, null, 0, null);
            if (command.Minutes < 1 || command.Minutes > AdvanceTimeCommand.MaxMinutes)
                return new AdvanceResult(false, $"Advance between 1 and {AdvanceTimeCommand.MaxMinutes} minutes.", null, 0, null);

            var startAbsolute = Absolute();
            var end = _world.MinuteOfDay + command.Minutes;
            var days = end / WorldTime.MinutesPerDay;
            if (days > 0)
            {
                var run = Advance(days);
                if (!run.IsSuccess)
                {
                    if (run.DaysAdvanced > 0) SetClock(0, startAbsolute);
                    return run;
                }

                SetClock(end % WorldTime.MinutesPerDay, startAbsolute);
                return run;
            }

            SetClock(end, startAbsolute);
            return new AdvanceResult(true, $"Advanced {command.Minutes} minute(s).", null, 0, null);
        }

        private long Absolute() => (long)_world.CurrentSimulationDate.ToDayNumber() * WorldTime.MinutesPerDay + _world.MinuteOfDay;

        private void SetClock(int minute, long startAbsolute)
        {
            _world.MinuteOfDay = minute;
            var player = _world.Player;
            var hours = (int)(Absolute() / 60 - startAbsolute / 60);
            if (player != null && hours > 0) player.Fed = Math.Max(0, player.Fed - hours * FedLostPerHour);
            WorldChanged?.Invoke(new WorldChange(WorldChangeReason.TimeAdvanced, null));
        }
    }
}
