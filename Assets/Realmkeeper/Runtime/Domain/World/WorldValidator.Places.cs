using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;

namespace Realmkeeper.Domain.World
{
    /// <summary>
    /// Phase 13 invariants (ADR-0062 … ADR-0066): each place is unique, with a valid saved layout and state that names
    /// only its own rooms, doors and features; the player in a place stands on one of its floors; each person held,
    /// following or posted is one living identity in one place; a place's containers name its own features.
    /// </summary>
    public static partial class WorldValidator
    {
        public const int MaxFollowers = 3;

        /// <summary>Phase 13: a creature that lives in a place ("place:{placeId}:{groupId}") is accounted for by that place's group.</summary>
        private static bool FromPlace(WorldData world, string origin)
        {
            if (!origin.StartsWith("place:", StringComparison.Ordinal)) return false;
            var rest = origin.Substring(6);
            var colon = rest.LastIndexOf(':');
            if (colon < 0) return false;
            var place = world.FindPlace(rest.Substring(0, colon));
            return place?.Layout.Group(rest.Substring(colon + 1)) != null;
        }

        private static void ValidatePlaces(WorldData world, ValidationResult result)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var posted = new HashSet<NpcId>();
            foreach (var p in world.Places)
            {
                var label = $"Place '{p?.PlaceId}'";
                if (p == null || string.IsNullOrEmpty(p.PlaceId) || !ids.Add(p.PlaceId)) { result.AddError($"{label} is missing or duplicated."); continue; }
                if (p.Layout == null) { result.AddError($"{label} has no layout."); continue; }
                foreach (var e in PlaceValidator.Validate(p.Layout)) result.AddError($"{label}: {e}");
                if (p.ParentKind == PlaceParentKind.Settlement || p.Category == PlaceCategory.Interior)
                    if (world.FindSettlement(p.SettlementId) == null) result.AddError($"{label} belongs to an unknown settlement.");
                foreach (var r in p.ExploredRooms) if (p.Layout.Room(r) == null) result.AddError($"{label} records an unknown room '{r}' as explored.");
                foreach (var c in p.FoundConnections) if (p.Layout.Connection(c) == null) result.AddError($"{label} records an unknown passage '{c}' as found.");
                foreach (var pair in p.Doors)
                {
                    var c = p.Layout.Connection(pair.Key);
                    if (c == null) result.AddError($"{label} has a door state for unknown passage '{pair.Key}'.");
                    else if (c.Kind == ConnectionKind.Opening || c.IsVertical) result.AddError($"{label}: '{pair.Key}' has no door to be {pair.Value}.");
                }

                foreach (var pair in p.Features) if (p.Layout.Feature(pair.Key) == null) result.AddError($"{label} has state for unknown feature '{pair.Key}'.");
                foreach (var pair in p.Groups) if (p.Layout.Group(pair.Key) == null) result.AddError($"{label} has state for unknown group '{pair.Key}'.");
                foreach (var g in p.Layout.Groups)
                    foreach (var m in g.Members)
                    {
                        if (m.NpcId.IsEmpty) continue;
                        var npc = world.FindNpc(m.NpcId);
                        if (npc == null) result.AddError($"{label}: {g.Id} names an unknown person.");
                        if (p.GroupStateOf(g.Id) != GroupState.Defeated && !posted.Add(m.NpcId)) result.AddError($"{label}: {npc?.Name ?? m.NpcId.ToString()} is posted in two places at once.");
                    }

                if (p.Threat < 0 || p.Threat > 100) result.AddError($"{label} has a threat out of range.");
            }

            foreach (var c in world.Containers)
            {
                if (c?.PlaceId == null) continue;
                var p = world.FindPlace(c.PlaceId);
                if (p == null) result.AddError($"Container '{c.Key}' stands in unknown place '{c.PlaceId}'.");
                else if (c.FeatureId != null && p.Layout.Feature(c.FeatureId) == null && !c.FeatureId.StartsWith("loot:", StringComparison.Ordinal))
                    result.AddError($"Container '{c.Key}' stands at unknown feature '{c.FeatureId}'.");
            }

            var player = world.Player;
            if (player != null && player.IsInPlace)
            {
                var p = world.FindPlace(player.PlaceId);
                if (p == null) result.AddError($"The player is in unknown place '{player.PlaceId}'.");
                else if (p.Layout.Floors.Find(f => f.Index == player.PlaceFloor) == null) result.AddError($"The player is on floor {player.PlaceFloor} of {p.Name}, which it does not have.");
            }
            else if (player != null && player.PlaceId != null) result.AddError("The player names a place without being in one.");

            var escorted = new HashSet<NpcId>();
            var following = 0;
            foreach (var e in world.Escorts)
            {
                if (e == null) { result.AddError("Null escort entry."); continue; }
                var npc = world.FindNpc(e.NpcId);
                if (npc == null) { result.AddError($"An escort names unknown person {e.NpcId}."); continue; }
                if (!escorted.Add(e.NpcId)) result.AddError($"{npc.Name} is escorted twice.");
                if (posted.Contains(e.NpcId) && e.OverridesSchedule) result.AddError($"{npc.Name} is both posted to a garrison and held or following.");
                if ((e.State == EscortState.Captive || e.State == EscortState.Waiting) && world.FindPlace(e.PlaceId) == null) result.AddError($"{npc.Name} is held in an unknown place.");
                if (e.State == EscortState.Following) following++;
                if (e.OverridesSchedule && npc.IsDeceased) result.AddError($"{npc.Name} is dead but still held or following.");
                if (e.State == EscortState.Lost && !npc.IsDeceased) result.AddError($"{npc.Name} is recorded lost but alive.");
            }

            if (following > MaxFollowers) result.AddError($"{following} people follow the player (at most {MaxFollowers}).");

            // ----- Phase 14 (ADR-0068 … ADR-0071) -----
            foreach (var e in world.Escorts)
            {
                if (e == null || e.State != EscortState.Following || e.PlaceId == null) continue;
                var p = world.FindPlace(e.PlaceId);
                if (p == null || p.Layout.Room(e.Room) == null) result.AddError($"A follower stands in an unknown room ('{e.PlaceId}' / '{e.Room}').");
            }

            foreach (var p in world.Places)
            {
                if (p == null) continue;
                foreach (var pair in p.Bridges)
                    if (p.Layout.Connection(pair.Key)?.Kind != ConnectionKind.Bridge) result.AddError($"{p.Name} records a bridge state for '{pair.Key}', which is not a bridge.");
            }

            var crimeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in world.Crimes)
            {
                if (c == null || string.IsNullOrEmpty(c.CrimeId)) { result.AddError("A crime record has no id."); continue; }
                if (!crimeIds.Add(c.CrimeId)) result.AddError($"Crime '{c.CrimeId}' is recorded twice.");
                if (c.Bounty < 0) result.AddError($"Crime '{c.CrimeId}' has a negative bounty.");
                if (c.Status == CrimeStatus.Settled && c.Bounty != 0) result.AddError($"Crime '{c.CrimeId}' is settled but still has a bounty.");
                if (c.Status == CrimeStatus.Undiscovered && c.Witnesses.Count > 0) result.AddError($"Crime '{c.CrimeId}' was seen but is recorded undiscovered.");
                var seen = new HashSet<NpcId>();
                foreach (var x in c.Witnesses)
                {
                    if (world.FindNpc(x.NpcId) == null) result.AddError($"Crime '{c.CrimeId}' names an unknown witness.");
                    if (!seen.Add(x.NpcId)) result.AddError($"Crime '{c.CrimeId}' names a witness twice.");
                }
            }

            if (world.NextCrimeNumber < 1) result.AddError("The next crime number must be at least 1.");
            ValidateConditions(world.Player?.Rpg, world.Player?.Name ?? "The player", result);
            foreach (var n in world.Npcs) if (n?.Character?.Sheet != null) ValidateConditions(n.Character.Sheet, n.Name, result);
        }

        private static void ValidateConditions(Realmkeeper.Domain.Rpg.CharacterSheetData sheet, string who, ValidationResult result)
        {
            if (sheet?.Conditions == null) return;
            var types = new HashSet<Realmkeeper.Domain.Combat.ConditionType>();
            foreach (var k in sheet.Conditions)
            {
                if (k == null) { result.AddError($"{who} has a null lasting condition."); continue; }
                if (!types.Add(k.Type)) result.AddError($"{who} has {k.Type} twice (conditions do not stack).");
                if (Realmkeeper.Domain.Rpg.LastingConditions.FightOnly(k.Type)) result.AddError($"{who} keeps {k.Type} outside a fight.");
                if (k.Type == Realmkeeper.Domain.Combat.ConditionType.Exhaustion && (k.Level < 1 || k.Level > 6)) result.AddError($"{who}'s exhaustion level {k.Level} is out of range.");
                if (k.ExpiresAt >= 0 && k.ExpiresAt < k.AppliedAt) result.AddError($"{who}'s {k.Type} ends before it began.");
            }
        }
    }
}
