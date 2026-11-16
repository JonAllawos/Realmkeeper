using System;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Phase 25: actions one interface layer offers another without referencing it. The strategic bootstrap sets
    /// <see cref="OpenMainMenu"/> (its title screen); the exploration pause menu calls it. When nothing set it (a
    /// test scene without a title screen), the menu entry is shown disabled with the reason.
    /// <para>
    /// M1.1 (ADR-0123): the save screens (<see cref="OpenSaveGame"/>, <see cref="OpenLoadGame"/>), the moments autosave
    /// listens for (<see cref="TransitionArrived"/>, <see cref="BeforeLeavingGame"/>), and the states it must respect
    /// (<see cref="TransitionInProgress"/>, <see cref="PauseMenuOpen"/>).
    /// </para>
    /// </summary>
    public static class RkHooks
    {
        public static Action OpenMainMenu;

        /// <summary>M1.1: opens the Save Game screen (null when the scene has no title screen).</summary>
        public static Action OpenSaveGame;

        /// <summary>M1.1: opens the Load Game screen (null when the scene has no title screen).</summary>
        public static Action OpenLoadGame;

        /// <summary>M1.1: true from the moment a loading transition starts until it has completed or failed.</summary>
        public static bool TransitionInProgress;

        /// <summary>M1.1: true while the exploration pause menu is open (play time is not counted).</summary>
        public static bool PauseMenuOpen;

        /// <summary>M1.1: a loading transition has completed (the destination's name); raised while the loading screen still shows.</summary>
        public static event Action<string> TransitionArrived;

        /// <summary>M1.1: the player confirmed leaving the game ("exit" or "title"); raised before anything closes.</summary>
        public static event Action<string> BeforeLeavingGame;

        public static void RaiseTransitionArrived(string destination)
        {
            TransitionInProgress = false;
            TransitionArrived?.Invoke(destination);
        }

        public static void RaiseBeforeLeavingGame(string why) => BeforeLeavingGame?.Invoke(why);
    }
}
