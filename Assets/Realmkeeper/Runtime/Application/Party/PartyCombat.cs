using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Party;

namespace Realmkeeper.Application.Party
{
    /// <summary>
    /// Phase 15 (ADR-0075): the party in fights. Active members who are with the player join every encounter the player
    /// starts or is drawn into, as ordinary <see cref="CombatantKind.Npc"/> participants on the player's side. The engine
    /// then treats them like any person with a sheet: their own HP, slots, conditions and equipment, used live, so what
    /// happens in the fight stays with them. Experience is awarded by the existing keyed encounter awards.
    /// </summary>
    public static class PartyCombat
    {
        /// <summary>Squares around the player, nearest and behind first (foes are usually ahead, at +y).</summary>
        private static readonly (int, int)[] Ring =
        {
            (-1, 0), (1, 0), (0, -1), (-1, -1), (1, -1), (-2, 0), (2, 0), (0, -2), (-2, -1), (2, -1), (-1, -2), (1, -2), (-2, -2), (2, -2), (-3, 0), (3, 0),
        };

        /// <summary>The members who fight now: active, with the player, alive and with a sheet that can stand.</summary>
        public static List<NpcData> Fighters(WorldData w)
        {
            var list = new List<NpcData>();
            foreach (var npc in CompanionLoyalty.Present(w))
            {
                var sheet = npc.Character?.Sheet;
                if (sheet == null || sheet.IsDead) continue;
                list.Add(npc);
            }

            return list;
        }

        /// <summary>
        /// Adds the fighting members to a setup beside the player's participant (once each: someone already in the setup is
        /// left where they are). <paramref name="free"/> picks a square; without it, the nearest free square on the ring.
        /// Returns how many joined.
        /// </summary>
        public static int Join(WorldData w, EncounterSetup setup, Func<GridPos, GridPos?> free = null)
        {
            if (w?.Party == null || w.Party.Members.Count == 0 || setup == null) return 0;
            var player = setup.Participants.Find(p => p.Kind == CombatantKind.Player);
            if (player == null) return 0;
            var taken = new HashSet<GridPos>(setup.Blocked);
            foreach (var p in setup.Participants) taken.Add(p.Position);
            var joined = 0;
            foreach (var npc in Fighters(w))
            {
                if (setup.Participants.Exists(p => p.Kind == CombatantKind.Npc && p.NpcId == npc.NpcId)) continue;
                var at = free != null ? free(player.Position) : Near(player.Position, taken);
                if (at == null) continue;
                taken.Add(at.Value);
                setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Npc, NpcId = npc.NpcId, Side = player.Side, Position = at.Value });
                joined++;
            }

            return joined;
        }

        private static GridPos? Near(GridPos origin, HashSet<GridPos> taken)
        {
            foreach (var (dx, dz) in Ring)
            {
                var g = new GridPos(origin.X + dx, origin.Y + dz);
                if (!taken.Contains(g)) return g;
            }

            return null;
        }
    }
}

namespace Realmkeeper.Application.Party
{
    using Realmkeeper.Domain.Combat;
    using Realmkeeper.Domain.World;
    using Realmkeeper.Simulation.Party;
    using Realmkeeper.Simulation.Quests;

    /// <summary>
    /// Phase 15: what a finished fight means to each companion in it, once per fight (the approval keys carry the encounter
    /// ID). Left dying when the player withdrew: a heavy blow. Pulled back from death by the player's own hands: gratitude.
    /// Killed: they leave the party for good.
    /// </summary>
    public static class PartyAftermath
    {
        public static void Apply(WorldData world, EncounterData e, QuestReporter report)
        {
            if (e == null || e.IsActive || world.Party == null || world.Companions.Count == 0) return;
            var player = e.Combatants.Find(c => c.Kind == CombatantKind.Player);
            if (player == null) return;
            var key = e.EncounterId.ToString();
            foreach (var c in e.Combatants)
            {
                if (c.Kind != CombatantKind.Npc || c.CreatureId != null || c.Side != player.Side || !System.Guid.TryParse(c.SourceId, out var g)) continue;
                var id = new Domain.Identifiers.NpcId(g);
                var record = world.FindCompanion(id);
                var npc = world.FindNpc(id);
                if (record == null || npc == null || !world.Party.Contains(id)) continue;
                if (npc.IsDeceased || c.State == VitalState.Dead)
                {
                    CompanionLoyalty.Leave(world, npc, CompanionStanding.Departed, "fell in battle (" + e.Name + ")", report);
                    continue;
                }

                // The engine stabilises the fallen as any fight ends: anyone on the ground (not fled with the player) was left behind.
                if (e.Outcome == EncounterOutcome.Withdrawn && (c.State == VitalState.Dying || c.State == VitalState.Stable))
                    CompanionLoyalty.Apply(world, id, "abandoned:" + key, -20, "you withdrew and left them dying", report);
                foreach (var l in e.Log)
                    if (l.Kind == CombatLogKind.Stabilized && l.ActorId == player.CombatantId && l.TargetId == c.CombatantId)
                    {
                        CompanionLoyalty.Apply(world, id, "saved:" + key, 5, "you kept them from dying", report);
                        break;
                    }
            }
        }
    }
}
