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

        private void SetPlaying(bool playing)
        {
            if (IsPlaying == playing) return;
            IsPlaying = playing;
            if (!playing) _accumulator = 0;
            PlayStateChanged?.Invoke(playing);
        }
    }
}
