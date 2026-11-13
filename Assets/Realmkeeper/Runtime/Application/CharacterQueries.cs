using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>The player's character for the sheet, the creator and the level-up window: a copy, with the rules derived from it.</summary>
    public sealed class CharacterSnapshot
    {
        internal CharacterSnapshot(PlayerCharacterData copy)
        {
            Player = copy;
            Rules = new CharacterRules(copy.Rpg, CharacterRules.LoadoutOf(copy));
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

        // ----- Phase 8: the dying state -----

        public bool IsDead => Player.Rpg.IsDead;
        public bool IsStable => Player.Rpg.IsStable;

        /// <summary>At 0 hit points, neither stable nor dead (only during a fight: a fight's end stabilizes the player).</summary>
        public bool IsDying => State == CreationState.Complete && Player.Rpg.CurrentHp == 0 && !IsDead && !IsStable;

        /// <summary>"Dead", "Unconscious, stable", "Dying (1 success, 2 failures)" or null when conscious.</summary>
        public string VitalText =>
            IsDead ? "Dead" : IsStable ? "Unconscious, stable (heal to wake)" :
            IsDying ? $"Dying ({Player.Rpg.DeathSaveSuccesses} successes, {Player.Rpg.DeathSaveFailures} failures)" : null;

        // ----- Phase 7: progression -----

        public int Level => Player.Rpg.Level;
        public int Experience => Player.Rpg.Experience;

        /// <summary>Experience the next level needs, or null at level 20.</summary>
        public int? NextLevelExperience => ProgressionRules.NextThreshold(Level);

        /// <summary>Experience the current level needed.</summary>
        public int LevelExperience => ProgressionRules.ExperienceFor(Level);

        /// <summary>Levels the character can take now.</summary>
        public int LevelsAvailable => CharacterAdvancement.LevelsAvailable(Player.Rpg);

        public bool CanAdvance => LevelsAvailable > 0;

        /// <summary>Choices from earlier rules versions still to make (made with the next level-up).</summary>
        public IReadOnlyList<ChoiceSlot> Outstanding => CharacterProgression.Outstanding(Player.Rpg);

        // ----- Phase 22 (ADR-0109): the level-up as the player sees it -----

        /// <summary>The highest level the experience allows now (the current level when none is available).</summary>
        public int EligibleLevel => ProgressionCommandHandlers.EligibleLevel(Player.Rpg);

        /// <summary>A level is available that the player has not been told about yet: show the notice (once).</summary>
        public bool LevelUpUnannounced => ProgressionCommandHandlers.LevelUpUnannounced(Player.Rpg);

        /// <summary>The next level-up's picks so far (null when none are kept).</summary>
        public LevelChoices PendingAdvancement => Player.Rpg.PendingAdvancement;

        /// <summary>Choices the next level-up still needs (0 when it can be confirmed, or when no level is available).</summary>
        public int OpenChoices => CanAdvance ? CharacterAdvancement.OpenChoices(Player.Rpg, PendingAdvancement) : 0;

        /// <summary>Everything the next level gives on its own (hit points, proficiency, features, resources), before any choice.</summary>
        public AdvancementPreview NextLevelPreview => CanAdvance ? CharacterAdvancement.Preview(Player.Rpg, PendingAdvancement ?? new LevelChoices { Level = Level + 1 }) : null;

        /// <summary>Features still to come, with the level each arrives at.</summary>
        public IReadOnlyList<FeatureEntry> LockedFeatures => Rules.LockedFeatures;

        /// <summary>Progress towards the next level, 0–1 (1 at level 20).</summary>
        public float Progress
        {
            get
            {
                var next = NextLevelExperience;
                if (next == null) return 1f;
                var span = next.Value - LevelExperience;
                return span <= 0 ? 1f : System.Math.Max(0f, System.Math.Min(1f, (Experience - LevelExperience) / (float)span));
            }
        }
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
