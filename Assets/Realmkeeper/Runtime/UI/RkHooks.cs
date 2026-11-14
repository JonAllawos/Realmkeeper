using System;

namespace Realmkeeper.UI
{
    /// <summary>
    /// Phase 25: actions one interface layer offers another without referencing it. The strategic bootstrap sets
    /// <see cref="OpenMainMenu"/> (its title screen); the exploration pause menu calls it. When nothing set it (a
    /// test scene without a title screen), the menu entry is shown disabled with the reason.
    /// </summary>
    public static class RkHooks
    {
        public static Action OpenMainMenu;
    }
}
