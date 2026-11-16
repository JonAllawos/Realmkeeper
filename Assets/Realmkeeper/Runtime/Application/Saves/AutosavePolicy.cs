namespace Realmkeeper.Application
{
    /// <summary>M1.1: the state an autosave is judged against (filled by the presentation at the moment of the trigger).</summary>
    public struct AutosaveConditions
    {
        /// <summary>The player's Autosave setting.</summary>
        public bool Enabled;

        public bool HasWorld;
        public bool InFight;
        public bool InConversation;

        /// <summary>A loading transition is under way and has not completed.</summary>
        public bool InTransition;

        /// <summary>A command is being applied to the world.</summary>
        public bool CommandApplying;

        /// <summary>Real seconds since the last save of any kind (manual or auto); null when nothing was saved this session.</summary>
        public double? SecondsSinceLastSave;
    }

    /// <summary>
    /// M1.1 (ADR-0123): when an autosave may be written. Autosaves happen at arrivals, after campaign decisions and
    /// chapter changes, and on leaving the game; never during a fight, an open conversation, an unfinished transition or a
    /// command, nor within <see cref="MinSecondsBetweenSaves"/> of any save. Pure, so it is tested without Unity.
    /// </summary>
    public static class AutosavePolicy
    {
        public const double MinSecondsBetweenSaves = 60.0;

        /// <summary>Null when an autosave may be written now, otherwise the reason it may not.</summary>
        public static string Check(AutosaveConditions c)
        {
            if (!c.Enabled) return "Autosave is turned off in Settings.";
            if (!c.HasWorld) return "There is no world to save.";
            if (c.InFight) return "Not during a fight.";
            if (c.InConversation) return "Not while a conversation is open.";
            if (c.InTransition) return "Not during a transition.";
            if (c.CommandApplying) return "Not while the world is changing.";
            if (c.SecondsSinceLastSave.HasValue && c.SecondsSinceLastSave.Value < MinSecondsBetweenSaves)
                return $"Saved {c.SecondsSinceLastSave.Value:0} s ago (autosaves wait {MinSecondsBetweenSaves:0} s after any save).";
            return null;
        }

        /// <summary>True when the reason will pass by itself (wait and try again), false when the trigger should be dropped.</summary>
        public static bool IsTransient(AutosaveConditions c) => c.Enabled && c.HasWorld;
    }
}
