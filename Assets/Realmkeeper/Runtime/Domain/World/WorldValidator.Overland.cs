using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.World
{
    public static partial class WorldValidator
    {
        /// <summary>Phase 25 (ADR-0116): the bestiary record names catalogued creatures, each once, with counts that make sense.</summary>
        private static void ValidateKnownCreatures(WorldData world, ValidationResult result)
        {
            var p = world.Player;
            if (p == null) return;
            if (p.KnownCreatures == null) { result.AddError("The player's known creatures must not be null."); return; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var k in p.KnownCreatures)
            {
                if (k == null || string.IsNullOrWhiteSpace(k.CreatureId) || !ids.Add(k.CreatureId)) { result.AddError($"Known creature '{k?.CreatureId}' is empty or repeated."); continue; }
                if (Combat.CreatureCatalog.Find(k.CreatureId) == null) result.AddError($"Known creature '{k.CreatureId}' is not in the creature catalogue.");
                if (k.Met < 0 || k.Defeated < 0) result.AddError($"Known creature '{k.CreatureId}' has a negative count.");
            }

            if (p.KnownCreatures.Count > CreatureKnowledge.MaxKinds) result.AddError("Too many known creatures.");
        }

        /// <summary>
        /// Phase 24 (ADR-0114): the country's saved changes are well formed: landmark states and overland encounters have
        /// unique ids, encounters a known kind and state, at most one is waiting on the player, and the list is bounded.
        /// </summary>
        private static void ValidateOverland(WorldData world, ValidationResult result)
        {
            ValidateKnownCreatures(world, result); // Phase 25
            var o = world.Overland;
            if (o == null) { result.AddError("The overland state must not be null."); return; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var l in o.Landmarks)
                if (l == null || string.IsNullOrWhiteSpace(l.LandmarkId) || !ids.Add(l.LandmarkId)) result.AddError($"Landmark state '{l?.LandmarkId}' is empty or repeated.");
            ids.Clear();
            var pending = 0;
            foreach (var e in o.Encounters)
            {
                if (e == null || string.IsNullOrWhiteSpace(e.Id) || !ids.Add(e.Id)) { result.AddError($"Overland encounter '{e?.Id}' is empty or repeated."); continue; }
                if (!Enum.IsDefined(typeof(OverlandEncounterKind), e.Kind) || !Enum.IsDefined(typeof(OverlandEncounterState), e.State)) result.AddError($"Overland encounter {e.Id} has an unknown kind or state.");
                if (e.State == OverlandEncounterState.Pending) pending++;
            }

            if (pending > 1) result.AddError("More than one overland encounter is waiting on the player.");
            if (o.Encounters.Count > OverlandStateData.MaxEncounters + 1) result.AddError("Too many overland encounters are kept.");
        }
    }
}
