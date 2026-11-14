using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Combat;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// Phase 25 (ADR-0116): one kind of creature the player has met in a fight, for the bestiary. What the bestiary may
    /// show depends on it.
    /// <list type="bullet">
    /// <item><b>Met.</b> Its name, kind, habitats and behaviour.</item>
    /// <item><b>Defeated</b> at least once. Also its armour, hit points, challenge and attacks, from the creature catalogue.</item>
    /// </list>
    /// Nothing about a creature the player has never fought is shown.
    /// </summary>
    public sealed class KnownCreatureData
    {
        public string CreatureId { get; set; }

        /// <summary>The first meeting, in minutes since the calendar's start.</summary>
        public long FirstMetAt { get; set; } = -1;

        /// <summary>Fights in which the player faced it.</summary>
        public int Met { get; set; }

        /// <summary>Individuals of this kind that fell or fled in a fight the player was in.</summary>
        public int Defeated { get; set; }

        public KnownCreatureData Clone() => new KnownCreatureData { CreatureId = CreatureId, FirstMetAt = FirstMetAt, Met = Met, Defeated = Defeated };
    }

    /// <summary>
    /// Phase 25 (ADR-0116): records what the player learns of creatures by fighting them. It is called by the combat
    /// engine when a fight begins and when it ends. Only fights the player takes part in count, and only creatures on
    /// another side (allies' mounts and companions' beasts are not studied).
    /// </summary>
    public static class CreatureKnowledge
    {
        public const int MaxKinds = 500;

        public static long Now(WorldData w) => w == null ? -1 : w.CurrentSimulationDate.ToDayNumber() * 1440L + w.MinuteOfDay;

        /// <summary>A fight has begun: each creature kind facing the player is met once more.</summary>
        public static void Met(WorldData w, EncounterData e)
        {
            var p = w?.Player;
            if (p == null || e == null) return;
            var side = PlayerSide(e);
            if (side == null) return;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in e.Combatants)
            {
                var id = c.FormCreatureId ?? c.CreatureId;
                if (id == null || c.Side == side || !seen.Add(id)) continue;
                var k = Find(p, id) ?? Add(p, id, Now(w));
                if (k == null) continue;
                k.Met++;
            }
        }

        /// <summary>A fight has ended: each creature facing the player that fell or fled counts as defeated.</summary>
        public static void Ended(WorldData w, EncounterData e)
        {
            var p = w?.Player;
            if (p == null || e == null) return;
            var side = PlayerSide(e);
            if (side == null) return;
            foreach (var c in e.Combatants)
            {
                var id = c.FormCreatureId ?? c.CreatureId;
                if (id == null || c.Side == side || c.State == VitalState.Conscious) continue;
                var k = Find(p, id) ?? Add(p, id, Now(w));
                if (k != null) k.Defeated++;
            }
        }

        /// <summary>
        /// Seeds the record from the fights a save still holds (migration to v27 and older saves). It runs only when the
        /// record is empty, so it never counts a fight twice.
        /// </summary>
        public static void SeedFromHistory(WorldData w)
        {
            var p = w?.Player;
            if (p == null || p.KnownCreatures.Count > 0) return;
            foreach (var e in w.Encounters)
            {
                if (e == null) continue;
                Met(w, e);
                if (!e.IsActive) Ended(w, e);
            }
        }

        public static KnownCreatureData Find(PlayerCharacterData p, string creatureId)
        {
            if (p?.KnownCreatures == null || creatureId == null) return null;
            foreach (var k in p.KnownCreatures) if (k.CreatureId == creatureId) return k;
            return null;
        }

        private static KnownCreatureData Add(PlayerCharacterData p, string id, long at)
        {
            if (p.KnownCreatures.Count >= MaxKinds) return null;
            var k = new KnownCreatureData { CreatureId = id, FirstMetAt = at };
            p.KnownCreatures.Add(k);
            return k;
        }

        private static string PlayerSide(EncounterData e)
        {
            foreach (var c in e.Combatants) if (c.Kind == CombatantKind.Player) return c.Side;
            return null;
        }
    }
}
