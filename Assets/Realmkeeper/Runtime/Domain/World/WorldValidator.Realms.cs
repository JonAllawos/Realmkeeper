using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    public static partial class WorldValidator
    {
        /// <summary>
        /// Phase 23 (ADR-0111): realms have unique ids, at most one surface; another realm's regions have unique ids, seeds
        /// and names; gateways join two existing ends in different realms, with a known state; the player outside the surface
        /// is overland in one of that realm's regions.
        /// </summary>
        private static void ValidateRealms(WorldData world, ValidationResult result)
        {
            if (world.Realms == null || world.Gateways == null)
            {
                result.AddError("Realms and gateways must not be null.");
                return;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var regionIds = new HashSet<RegionId>();
            var surfaces = 0;
            foreach (var r in world.Realms)
            {
                if (r == null) { result.AddError("Null realm entry."); continue; }
                if (string.IsNullOrWhiteSpace(r.RealmId) || !ids.Add(r.RealmId)) result.AddError($"Realm id '{r.RealmId}' is empty or repeated.");
                if (string.IsNullOrWhiteSpace(r.Name)) result.AddError($"Realm {r.RealmId} has no name.");
                if (!Enum.IsDefined(typeof(RealmKind), r.Kind)) result.AddError($"Realm {r.RealmId} has an unknown kind.");
                if (r.Kind == RealmKind.Surface) surfaces++;
                if (r.Regions == null) { result.AddError($"Realm {r.RealmId} has no region list."); continue; }
                if (r.Kind == RealmKind.Surface && r.Regions.Count > 0) result.AddError("The surface realm's regions are the atlas's, not its own.");
                foreach (var g in r.Regions)
                {
                    if (g == null || g.RegionId.IsEmpty || !regionIds.Add(g.RegionId) || world.FindRegion(g.RegionId) != null)
                        result.AddError($"Realm {r.RealmId} has a region with an empty, repeated or surface id.");
                    else if (g.Seed == 0 || string.IsNullOrWhiteSpace(g.Name)) result.AddError($"Realm region {g.RegionId} has no seed or name.");
                }
            }

            if (surfaces > 1) result.AddError("There is more than one surface realm.");

            var gateways = new HashSet<string>(StringComparer.Ordinal);
            foreach (var g in world.Gateways)
            {
                if (g == null) { result.AddError("Null gateway entry."); continue; }
                if (string.IsNullOrWhiteSpace(g.GatewayId) || !gateways.Add(g.GatewayId)) result.AddError($"Gateway id '{g.GatewayId}' is empty or repeated.");
                if (!Enum.IsDefined(typeof(GatewayState), g.State)) result.AddError($"Gateway {g.GatewayId} has an unknown state.");
                if (!EndExists(world, g.A) || !EndExists(world, g.B)) result.AddError($"Gateway {g.GatewayId} has an end in a realm or region that does not exist.");
                if (string.Equals(g.A.RealmId, g.B.RealmId, StringComparison.Ordinal)) result.AddError($"Gateway {g.GatewayId} joins a realm to itself.");
            }

            var p = world.Player;
            if (p != null && !RealmIds.IsSurface(p.RealmId) && p.LocationKind != CharacterLocationKind.Overland)
                result.AddError("Outside the surface realm the player can only be in the country.");
        }

        private static bool EndExists(WorldData world, GatewayEnd end)
        {
            if (RealmIds.IsSurface(end.RealmId)) return world.FindRegion(end.RegionId) != null;
            return world.FindRealm(end.RealmId)?.FindRegion(end.RegionId) != null;
        }
    }
}
