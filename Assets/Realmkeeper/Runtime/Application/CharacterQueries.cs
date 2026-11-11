using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>The player's character for the sheet and the creator (Phase 6): a copy, with the rules derived from it.</summary>
    public sealed class CharacterSnapshot
    {
        internal CharacterSnapshot(PlayerCharacterData copy)
        {
            Player = copy;
            Rules = new CharacterRules(copy.Rpg);
            ArmorClass = Rules.ArmorClass(copy);
        }

        /// <summary>A copy of the player (changing it changes nothing).</summary>
        public PlayerCharacterData Player { get; }

        public CharacterRules Rules { get; }
        public Breakdown ArmorClass { get; }
        public CreationState State => Player.Rpg.State;
        public bool NeedsCreation => State != CreationState.Complete;
        public string Name => Player.Name;
        public string Title => Player.Rpg.Title;
        public string History => Player.Rpg.History;
        public string FullName => string.IsNullOrEmpty(Title) ? Name : Name + " " + Title;
    }

    public sealed partial class WorldSession
    {
        /// <summary>The player's character, or null when there is no world or player.</summary>
        public CharacterSnapshot GetCharacter()
        {
            var p = _world?.Player;
            return p == null ? null : new CharacterSnapshot(p.Clone());
        }
    }
}
