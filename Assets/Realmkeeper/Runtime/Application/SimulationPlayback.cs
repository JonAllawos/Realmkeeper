using System;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>
    /// Real-time pacing for the simulation. Plain C#: the presentation layer feeds it frame
    /// time (Tick), and it issues AdvanceDays commands at whole-day boundaries. Simulation
    /// results do not depend on frame rate, only on how many days are advanced.
    /// </summary>
    public sealed class SimulationPlayback
    {
        private readonly WorldSession _session;
        private readonly int _maxDaysPerTick;
        private double _accumulator;

        public SimulationPlayback(WorldSession session, int maxDaysPerTick = 4)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (maxDaysPerTick < 1) throw new ArgumentOutOfRangeException(nameof(maxDaysPerTick));
            _maxDaysPerTick = maxDaysPerTick;
        }

        public event Action<bool> PlayStateChanged;

        public bool IsPlaying { get; private set; }

        /// <summary>
        /// Phase 5: true while the player walks a settlement. Time then runs minute by minute at a pace
        /// people can be watched in (<see cref="LivingMinutesPerSecond"/>); false (the realm map) keeps
        /// whole days per tick, as before, leaving the time of day unchanged.
        /// </summary>
        public bool LivingWorld
        {
            get => _living;
            set
            {
                if (_living == value) return;
                _living = value;
                _accumulator = 0;
            }
        }

        private bool _living;

        /// <summary>Most game minutes one tick may advance in the living world (hitches are clamped, not replayed).</summary>
        public const int MaxLivingMinutesPerTick = 120;

        /// <summary>
        /// Game minutes per real second in the living world. Normal is two minutes a second (an hour in 30 s),
        /// slow enough that a resident walking across town at a brisk 1.8 m/s arrives within the hour the world
        /// says they are there; faster speeds are for waiting.
        /// </summary>
        public static double LivingMinutesPerSecond(SimulationSpeed speed)
        {
            switch (speed)
            {
                case SimulationSpeed.Slow: return 0.5;
                case SimulationSpeed.Normal: return 2.0;
                case SimulationSpeed.Fast: return 10.0;
                case SimulationSpeed.VeryFast: return 60.0;
                default: throw new ArgumentOutOfRangeException(nameof(speed));
            }
        }

        public static double SecondsPerDay(SimulationSpeed speed)
        {
            switch (speed)
            {
                case SimulationSpeed.Slow: return 2.0;
                case SimulationSpeed.Normal: return 1.0;
                case SimulationSpeed.Fast: return 0.4;
                case SimulationSpeed.VeryFast: return 0.1;
                default: throw new ArgumentOutOfRangeException(nameof(speed));
            }
        }

        public void Play() => SetPlaying(true);

        public void Pause() => SetPlaying(false);

        public void Toggle() => SetPlaying(!IsPlaying);

        /// <summary>Advances whole days due for the elapsed time. Returns days advanced.</summary>
        public int Tick(double deltaSeconds)
        {
            if (!IsPlaying || !_session.HasWorld || deltaSeconds <= 0 || double.IsNaN(deltaSeconds)) return 0;
            if (_living) return TickLiving(deltaSeconds);

            var secondsPerDay = SecondsPerDay(_session.GetOverview().Speed);
            _accumulator += deltaSeconds;
            var due = (int)Math.Floor(_accumulator / secondsPerDay);
            if (due <= 0) return 0;

            // Clamp after hitches (e.g. editor pause) instead of fast-forwarding a backlog.
            var days = Math.Min(due, _maxDaysPerTick);
            _accumulator = due > _maxDaysPerTick ? 0 : _accumulator - days * secondsPerDay;

            var result = _session.Execute(new AdvanceDaysCommand(days));
            if (!result.IsSuccess) Pause();
            return result.DaysAdvanced;
        }

        private int TickLiving(double deltaSeconds)
        {
            _accumulator += deltaSeconds * LivingMinutesPerSecond(_session.GetOverview().Speed);
            var due = (int)Math.Floor(_accumulator);
            if (due <= 0) return 0;
            var minutes = Math.Min(due, MaxLivingMinutesPerTick);
            _accumulator = due > MaxLivingMinutesPerTick ? 0 : _accumulator - minutes;
            var result = _session.Execute(new AdvanceTimeCommand(minutes));
            if (!result.IsSuccess) Pause();
            return result.DaysAdvanced;
        }

        private void SetPlaying(bool playing)
        {
            if (IsPlaying == playing) return;
            IsPlaying = playing;
            if (!playing) _accumulator = 0;
            PlayStateChanged?.Invoke(playing);
        }
    }
}
