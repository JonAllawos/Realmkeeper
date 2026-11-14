using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;

namespace Realmkeeper.Domain.World
{
    public sealed class ValidationResult
    {
        private readonly List<string> _errors = new List<string>();

        public bool IsValid => _errors.Count == 0;
        public IReadOnlyList<string> Errors => _errors;

        public void AddError(string error) => _errors.Add(error);

        public override string ToString() => IsValid ? "Valid" : string.Join("; ", _errors);
    }

    /// <summary>
    /// Structural and invariant checks for world state. Used before committing a simulated
    /// day, after loading a save, and after generation. Collects all errors instead of
    /// stopping at the first one.
    /// </summary>
    public static partial class WorldValidator
    {
        public static ValidationResult Validate(WorldData world)
        {
            var result = new ValidationResult();
            if (world == null)
            {
                result.AddError("World is null.");
                return result;
            }

            if (world.WorldId.IsEmpty) result.AddError("WorldId is empty.");
            if (string.IsNullOrWhiteSpace(world.WorldName)) result.AddError("WorldName is empty.");
            else if (world.WorldName.Length > WorldData.MaxNameLength) result.AddError("WorldName is too long.");
            if (world.GeneratorVersion <= 0) result.AddError("GeneratorVersion must be positive.");
            if (world.SchemaVersion != WorldData.CurrentSchemaVersion)
                result.AddError($"SchemaVersion {world.SchemaVersion} is not the current schema ({WorldData.CurrentSchemaVersion}).");
            if (!world.CurrentSimulationDate.IsValidDate) result.AddError("CurrentSimulationDate is invalid.");

            if (world.WorldSettings == null)
            {
                result.AddError("WorldSettings is missing.");
            }
            else
            {
                if (!world.WorldSettings.StartingDate.IsValidDate) result.AddError("StartingDate is invalid.");
                else if (world.CurrentSimulationDate.IsValidDate && world.CurrentSimulationDate < world.WorldSettings.StartingDate)
                    result.AddError("CurrentSimulationDate precedes StartingDate.");
                if (world.WorldSettings.EnabledSimulationSystems == null) result.AddError("EnabledSimulationSystems is null.");
            }

            ValidateRealms(world, result); // Phase 23

            if (world.Settlements == null || world.Factions == null || world.Npcs == null || world.HistoricalEvents == null)
            {
                result.AddError("World collections must not be null.");
                return result;
            }

            var factionIds = new HashSet<FactionId>();
            foreach (var faction in world.Factions)
            {
                if (faction == null) { result.AddError("Null faction entry."); continue; }
                if (faction.FactionId.IsEmpty) result.AddError("Faction has empty ID.");
                else if (!factionIds.Add(faction.FactionId)) result.AddError($"Duplicate FactionId {faction.FactionId}.");
                if (string.IsNullOrWhiteSpace(faction.Name)) result.AddError($"Faction {faction.FactionId} has no name.");
            }

            if (world.Regions == null)
            {
                result.AddError("Regions collection must not be null.");
                return result;
            }

            var regions = new Dictionary<RegionId, RegionData>();
            foreach (var region in world.Regions)
            {
                if (region == null) { result.AddError("Null region entry."); continue; }
                if (region.RegionId.IsEmpty) result.AddError($"Region '{region.Name}' has an empty ID.");
                else if (regions.ContainsKey(region.RegionId)) result.AddError($"Duplicate RegionId {region.RegionId}.");
                else regions.Add(region.RegionId, region);
                if (string.IsNullOrWhiteSpace(region.Name)) result.AddError($"Region {region.RegionId} has no name.");
                else if (region.Name.Length > RegionData.MaxNameLength) result.AddError($"Region {region.RegionId} name is too long.");
                if (region.Width < 1 || region.Depth < 1) result.AddError($"Region '{region.Name}' must cover at least one cell.");
            }

            var settlementIds = new HashSet<SettlementId>();
            foreach (var settlement in world.Settlements)
            {
                if (settlement == null) { result.AddError("Null settlement entry."); continue; }
                ValidateSettlement(settlement, result);
                if (!settlement.SettlementId.IsEmpty && !settlementIds.Add(settlement.SettlementId))
                    result.AddError($"Duplicate SettlementId {settlement.SettlementId}.");
                if (!settlement.FactionId.IsEmpty && !factionIds.Contains(settlement.FactionId))
                    result.AddError($"Settlement '{settlement.Name}' references unknown faction {settlement.FactionId}.");
                if (settlement.RegionId.IsEmpty)
                    result.AddError($"Settlement '{settlement.Name}' has no region.");
                else if (!regions.TryGetValue(settlement.RegionId, out var region))
                    result.AddError($"Settlement '{settlement.Name}' references unknown region {settlement.RegionId}.");
                else if (!region.Contains(settlement.Position))
                    result.AddError($"Settlement '{settlement.Name}' at {settlement.Position} lies outside its region '{region.Name}'.");
            }

            if (world.Player != null) ValidatePlayer(world, settlementIds, regions, result);

            var npcIds = new HashSet<NpcId>();
            var namedPerSettlement = new Dictionary<SettlementId, int>();
            foreach (var npc in world.Npcs)
            {
                if (npc == null) { result.AddError("Null NPC entry."); continue; }
                if (npc.NpcId.IsEmpty) result.AddError("NPC has empty ID.");
                else if (!npcIds.Add(npc.NpcId)) result.AddError($"Duplicate NpcId {npc.NpcId}.");
                if (string.IsNullOrWhiteSpace(npc.Name)) result.AddError($"NPC {npc.NpcId} has no name.");
                if (npc.ConversationCount < 0) result.AddError($"NPC {npc.NpcId} has a negative conversation count.");
                if (!npc.HomeSettlementId.IsEmpty && !settlementIds.Contains(npc.HomeSettlementId))
                    result.AddError($"NPC {npc.NpcId} references unknown settlement.");
                if (!npc.HomeSettlementId.IsEmpty && npc.IsAlive)
                {
                    namedPerSettlement.TryGetValue(npc.HomeSettlementId, out var count);
                    namedPerSettlement[npc.HomeSettlementId] = count + 1;
                }
            }

            // Named (living) people are part of the population, never extra to it: tier 3 is the rest.
            foreach (var settlement in world.Settlements)
            {
                if (settlement != null && namedPerSettlement.TryGetValue(settlement.SettlementId, out var named) && named > settlement.Population)
                    result.AddError($"Settlement '{settlement.Name}' has more named residents ({named}) than people ({settlement.Population}).");
            }

            var eventIds = new HashSet<EventId>();
            foreach (var e in world.HistoricalEvents)
            {
                if (e == null) { result.AddError("Null event entry."); continue; }
                if (e.EventId.IsEmpty) result.AddError("Event has empty ID.");
                else if (!eventIds.Add(e.EventId)) result.AddError($"Duplicate EventId {e.EventId}.");
                if (!e.Date.IsValidDate) result.AddError($"Event {e.EventId} has an invalid date.");
                else if (world.CurrentSimulationDate.IsValidDate && e.Date > world.CurrentSimulationDate)
                    result.AddError($"Event {e.EventId} is dated in the future.");
                foreach (var id in e.SettlementIds)
                {
                    if (id.IsEmpty || !settlementIds.Contains(id)) result.AddError($"Event {e.EventId} references unknown settlement.");
                }
            }

            ValidateTrade(world, settlementIds, result);
            ValidatePolitics(world, settlementIds, npcIds, result);
            ValidateNpcs(world, settlementIds, npcIds, result);
            ValidateCommerce(world, settlementIds, npcIds, result);
            ValidateCombat(world, settlementIds, npcIds, result);
            ValidateLiving(world, settlementIds, npcIds, result); // Phase 10
            ValidateQuests(world, result); // Phase 12
            ValidatePlaces(world, result); // Phase 13
            ValidateAtlas(world, settlementIds, result); // Phase 16
            ValidateCampaigns(world, result); // Phase 17
            if (world.Player != null) foreach (var e in Rpg.CharacterValidation.Check(world.Player)) result.AddError("Character: " + e);
            return result;
        }

        /// <summary>Phase 4: locations, whereabouts, inner-life bounds and relationships.</summary>
        private static void ValidateNpcs(WorldData world, HashSet<SettlementId> settlementIds, HashSet<NpcId> npcIds, ValidationResult result)
        {
            var locations = new Dictionary<LocationId, LocationData>();
            foreach (var l in world.Locations)
            {
                if (l == null) { result.AddError("Null location entry."); continue; }
                if (l.LocationId.IsEmpty) result.AddError("Location has empty ID.");
                else if (locations.ContainsKey(l.LocationId)) result.AddError($"Duplicate LocationId {l.LocationId}.");
                else locations.Add(l.LocationId, l);
                if (!settlementIds.Contains(l.SettlementId)) result.AddError($"Location {l.Name} references unknown settlement.");
                if (!Enum.IsDefined(typeof(LocationKind), l.Kind)) result.AddError($"Location {l.Name} has an unknown kind.");
            }

            void CheckLocation(NpcData n, LocationId id, string what)
            {
                if (!id.IsEmpty && !locations.ContainsKey(id)) result.AddError($"NPC {n.Name} has an unknown {what}.");
            }

            foreach (var n in world.Npcs)
            {
                if (n == null) continue;
                CheckLocation(n, n.ResidenceId, "residence");
                CheckLocation(n, n.WorkplaceId, "workplace");
                CheckLocation(n, n.CurrentLocationId, "current location");
                for (var d = 0; d < NpcScheduleData.Days; d++) CheckLocation(n, n.Schedule[d], "scheduled location");
                if (n.Memories.Count > NpcBounds.MaxMemories) result.AddError($"NPC {n.Name} remembers more than {NpcBounds.MaxMemories} things.");
                if (n.Knowledge.Count > NpcBounds.MaxKnowledge) result.AddError($"NPC {n.Name} holds more than {NpcBounds.MaxKnowledge} pieces of knowledge.");
                var goalIds = new HashSet<GoalId>();
                foreach (var g in n.Goals) if (g.GoalId.IsEmpty || !goalIds.Add(g.GoalId)) result.AddError($"NPC {n.Name} has a goal with a missing or duplicate ID.");
                if (n.IsDeceased && !n.DiedOn.IsValidDate) result.AddError($"NPC {n.Name} is dead but has no date of death.");
            }

            var pairs = new HashSet<(NpcId, NpcId)>();
            var perNpc = new Dictionary<NpcId, int>();
            foreach (var r in world.NpcRelations)
            {
                if (r == null) { result.AddError("Null NPC relationship entry."); continue; }
                if (r.NpcA == r.NpcB) result.AddError("An NPC relationship links a person to themselves.");
                if (r.NpcA.CompareTo(r.NpcB) > 0) result.AddError("An NPC relationship is not stored in ID order.");
                if (!npcIds.Contains(r.NpcA) || !npcIds.Contains(r.NpcB)) result.AddError("An NPC relationship references an unknown person.");
                if (!pairs.Add((r.NpcA, r.NpcB))) result.AddError("Duplicate NPC relationship.");
                foreach (var id in new[] { r.NpcA, r.NpcB })
                {
                    perNpc.TryGetValue(id, out var c);
                    perNpc[id] = c + 1;
                }
            }

            foreach (var pair in perNpc)
            {
                if (pair.Value > NpcBounds.MaxRelationsPerNpc) result.AddError($"An NPC has more than {NpcBounds.MaxRelationsPerNpc} relationships.");
            }

            foreach (var f in world.Factions)
            {
                if (f == null || f.LeaderNpcId.IsEmpty) continue;
                var leader = world.FindNpc(f.LeaderNpcId);
                if (leader != null && leader.IsDeceased) result.AddError($"Faction {f.Name} is led by a dead person.");
            }
        }

        /// <summary>Phase 3: factions, presence, governance, relations, claims and goals.</summary>
        private static void ValidatePolitics(WorldData world, HashSet<SettlementId> settlementIds, HashSet<NpcId> npcIds, ValidationResult result)
        {
            if (world.Relations == null || world.Claims == null)
            {
                result.AddError("Political collections must not be null.");
                return;
            }

            var factions = new Dictionary<FactionId, FactionData>();
            foreach (var f in world.Factions)
            {
                if (f != null && !f.FactionId.IsEmpty && !factions.ContainsKey(f.FactionId)) factions.Add(f.FactionId, f);
            }

            var population = new Dictionary<SettlementId, int>();
            foreach (var s in world.Settlements)
            {
                if (s != null && !population.ContainsKey(s.SettlementId)) population.Add(s.SettlementId, s.Population);
            }

            var members = new Dictionary<SettlementId, long>();
            var goalIds = new HashSet<GoalId>();
            foreach (var f in world.Factions)
            {
                if (f == null) continue;
                var label = $"Faction '{f.Name}'";
                if (f.Name != null && f.Name.Length > FactionData.MaxNameLength) result.AddError($"{label} name is too long.");
                if (f.Description != null && f.Description.Length > FactionRules.MaxDescriptionLength) result.AddError($"{label} description is too long.");
                if (!Enum.IsDefined(typeof(FactionType), f.Type)) result.AddError($"{label} has an unknown faction type.");
                if (!Enum.IsDefined(typeof(FactionStatus), f.Status)) result.AddError($"{label} has an unknown status.");
                if (!f.FoundedOn.IsValidDate) result.AddError($"{label} has no valid founding date.");
                if (f.Influence < FactionRules.MinInfluence || f.Influence > FactionRules.MaxInfluence) result.AddError($"{label} influence {f.Influence} is outside 0..100.");
                if (f.InfluenceTarget < FactionRules.MinInfluence || f.InfluenceTarget > FactionRules.MaxInfluence) result.AddError($"{label} influence target is outside 0..100.");
                if (f.Treasury < 0) result.AddError($"{label} has a negative treasury.");
                if (f.DormantTicks < 0) result.AddError($"{label} has negative dormant ticks.");
                if (!f.LeaderNpcId.IsEmpty && !npcIds.Contains(f.LeaderNpcId)) result.AddError($"{label} names a leader who does not exist.");
                if (f.IsDissolved && !f.DissolvedOn.IsValidDate) result.AddError($"{label} is dissolved but has no dissolution date.");
                if (f.Presence == null || f.Goals == null) { result.AddError($"{label} has null presence or goals."); continue; }
                if (f.IsDissolved && f.Presence.Count > 0) result.AddError($"{label} is dissolved but still has a presence.");

                var seen = new HashSet<SettlementId>();
                foreach (var p in f.Presence)
                {
                    if (p == null) { result.AddError($"{label} has a null presence entry."); continue; }
                    if (!population.TryGetValue(p.SettlementId, out var pop)) { result.AddError($"{label} has a presence in an unknown settlement."); continue; }
                    if (!seen.Add(p.SettlementId)) result.AddError($"{label} has two presence records in one settlement.");
                    if (p.Strength < 1 || p.Strength > 100) result.AddError($"{label} presence strength {p.Strength} is outside 1..100.");
                    if (p.Members < 0 || p.Members > pop) result.AddError($"{label} has {p.Members} members in a settlement of {pop}.");
                    if (!Enum.IsDefined(typeof(PresenceStatus), p.Status)) result.AddError($"{label} has an unknown presence status.");
                    if (!p.EstablishedOn.IsValidDate) result.AddError($"{label} has a presence with no start date.");
                    members.TryGetValue(p.SettlementId, out var total);
                    members[p.SettlementId] = total + Math.Max(0, p.Members);
                }

                foreach (var g in f.Goals)
                {
                    if (g == null) { result.AddError($"{label} has a null goal."); continue; }
                    if (g.GoalId.IsEmpty) result.AddError($"{label} has a goal with an empty ID.");
                    else if (!goalIds.Add(g.GoalId)) result.AddError($"Duplicate GoalId {g.GoalId}.");
                    if (!Enum.IsDefined(typeof(GoalType), g.Type)) result.AddError($"{label} has a goal of unknown type.");
                    if (!Enum.IsDefined(typeof(GoalStatus), g.Status)) result.AddError($"{label} has a goal with unknown status.");
                    if (g.Priority < FactionRules.MinPriority || g.Priority > FactionRules.MaxPriority) result.AddError($"{label} has a goal with priority {g.Priority} outside 1..5.");
                    if (g.Progress < 0 || g.Progress > 100) result.AddError($"{label} has a goal with progress outside 0..100.");
                    if (!g.CreatedOn.IsValidDate) result.AddError($"{label} has a goal with no creation date.");
                    if (!g.TargetSettlementId.IsEmpty && !settlementIds.Contains(g.TargetSettlementId)) result.AddError($"{label} has a goal targeting an unknown settlement.");
                    if (!g.TargetFactionId.IsEmpty && !factions.ContainsKey(g.TargetFactionId)) result.AddError($"{label} has a goal targeting an unknown faction.");
                    if (g.Status == GoalStatus.Active)
                    {
                        if (g.CloseReason != GoalCloseReason.None) result.AddError($"{label} has an active goal with a close reason.");
                        if (GoalNeedsSettlement(g.Type) && g.TargetSettlementId.IsEmpty) result.AddError($"{label} has a {g.Type} goal without a target settlement.");
                        if (g.Type == GoalType.ImproveRelationship && (g.TargetFactionId.IsEmpty || g.TargetFactionId == f.FactionId))
                            result.AddError($"{label} has an ImproveRelationship goal without another faction as target.");
                        if (f.IsDissolved) result.AddError($"{label} is dissolved but still has an active goal.");
                    }
                    else if (g.CloseReason == GoalCloseReason.None || !g.ClosedOn.IsValidDate)
                    {
                        result.AddError($"{label} has a closed goal without a reason or date.");
                    }
                }
            }

            foreach (var pair in members)
            {
                if (pair.Value > population[pair.Key]) result.AddError($"Faction members in settlement {pair.Key} ({pair.Value}) exceed its population ({population[pair.Key]}).");
            }

            foreach (var s in world.Settlements)
            {
                if (s == null) continue;
                if (!s.FactionId.IsEmpty && factions.TryGetValue(s.FactionId, out var governor))
                {
                    if (governor.IsDissolved) result.AddError($"Settlement '{s.Name}' is governed by dissolved faction '{governor.Name}'.");
                    else if (!FactionRules.CanGovern(governor.Type)) result.AddError($"Settlement '{s.Name}' is governed by '{governor.Name}', but a {governor.Type} cannot govern.");
                }

                if (s.AdministratorFactionId.IsEmpty) continue;
                if (s.FactionId.IsEmpty) result.AddError($"Settlement '{s.Name}' has an administrator but no governing authority.");
                if (s.AdministratorFactionId == s.FactionId) result.AddError($"Settlement '{s.Name}' is administered by its own governor.");
                if (!factions.TryGetValue(s.AdministratorFactionId, out var admin)) result.AddError($"Settlement '{s.Name}' references an unknown administrator.");
                else if (admin.IsDissolved) result.AddError($"Settlement '{s.Name}' is administered by dissolved faction '{admin.Name}'.");
                else if (!FactionRules.CanAdminister(admin.Type)) result.AddError($"Settlement '{s.Name}' is administered by '{admin.Name}', but a {admin.Type} cannot administer.");
            }

            var pairs = new HashSet<(FactionId, FactionId)>();
            foreach (var r in world.Relations)
            {
                if (r == null) { result.AddError("Null relation entry."); continue; }
                if (!factions.TryGetValue(r.FactionA, out var a) || !factions.TryGetValue(r.FactionB, out var b)) { result.AddError("A relation references an unknown faction."); continue; }
                var label = $"Relation {a.Name} / {b.Name}";
                if (r.FactionA == r.FactionB) result.AddError($"{label} relates a faction to itself.");
                else if (r.FactionA.CompareTo(r.FactionB) > 0) result.AddError($"{label} is stored in the wrong order.");
                if (!pairs.Add((r.FactionA, r.FactionB))) result.AddError($"{label} is recorded twice.");
                if (r.Opinion < FactionRelationData.MinOpinion || r.Opinion > FactionRelationData.MaxOpinion) result.AddError($"{label} opinion {r.Opinion} is outside -100..100.");
                if (r.Trust < 0 || r.Trust > FactionRelationData.MaxScore) result.AddError($"{label} trust is outside 0..100.");
                if (r.Rivalry < 0 || r.Rivalry > FactionRelationData.MaxScore) result.AddError($"{label} rivalry is outside 0..100.");
                if (!Enum.IsDefined(typeof(AgreementType), r.Agreement) || !Enum.IsDefined(typeof(RelationCause), r.LastCause) || !Enum.IsDefined(typeof(RelationState), r.LastReportedState))
                    result.AddError($"{label} has an unknown agreement, cause or state.");
                if (r.Agreement != AgreementType.None)
                {
                    if (!r.AgreementSince.IsValidDate) result.AddError($"{label} has an agreement with no start date.");
                    if (a.IsDissolved || b.IsDissolved) result.AddError($"{label} has an agreement with a dissolved faction.");
                }
            }

            var claimIds = new HashSet<ClaimId>();
            var open = new HashSet<(FactionId, SettlementId)>();
            foreach (var c in world.Claims)
            {
                if (c == null) { result.AddError("Null claim entry."); continue; }
                if (c.ClaimId.IsEmpty) result.AddError("Claim has an empty ID.");
                else if (!claimIds.Add(c.ClaimId)) result.AddError($"Duplicate ClaimId {c.ClaimId}.");
                if (!Enum.IsDefined(typeof(ClaimStatus), c.Status)) result.AddError($"Claim {c.ClaimId} has an unknown status.");
                if (!c.FiledOn.IsValidDate || !c.UpdatedOn.IsValidDate) result.AddError($"Claim {c.ClaimId} has an invalid date.");
                if (!settlementIds.Contains(c.SettlementId)) result.AddError($"Claim {c.ClaimId} references an unknown settlement.");
                if (!factions.TryGetValue(c.ClaimantId, out var claimant)) { result.AddError($"Claim {c.ClaimId} references an unknown faction."); continue; }
                if (!c.IsOpen) continue;
                if (claimant.IsDissolved) result.AddError($"Dissolved faction '{claimant.Name}' still has an open claim.");
                if (!FactionRules.CanClaimTerritory(claimant.Type)) result.AddError($"'{claimant.Name}' has a claim, but a {claimant.Type} cannot claim territory.");
                if (!open.Add((c.ClaimantId, c.SettlementId))) result.AddError($"'{claimant.Name}' has two open claims on the same settlement.");
            }

            foreach (var e in world.HistoricalEvents)
            {
                if (e == null) continue;
                foreach (var id in e.FactionIds)
                {
                    if (id.IsEmpty || !factions.ContainsKey(id)) result.AddError($"Event {e.EventId} references unknown faction.");
                }
            }
        }

        public static bool GoalNeedsSettlement(GoalType type) => type == GoalType.EstablishPresence || type == GoalType.SecureClaim;

        private static void ValidatePlayer(WorldData world, HashSet<SettlementId> settlementIds, Dictionary<RegionId, RegionData> regions, ValidationResult result)
        {
            var p = world.Player;
            if (p.CharacterId.IsEmpty) result.AddError("Player character has an empty ID.");
            if (string.IsNullOrWhiteSpace(p.Name)) result.AddError("Player character has no name.");
            else if (p.Name.Length > PlayerCharacterData.MaxNameLength) result.AddError("Player character name is too long.");
            if (!System.Enum.IsDefined(typeof(CharacterLocationKind), p.LocationKind)) result.AddError("Player character location kind is unknown.");
            if (!System.Enum.IsDefined(typeof(CameraPerspective), p.Perspective)) result.AddError("Player character perspective is unknown.");
            if (!p.Pose.IsFinite) result.AddError("Player character position is not a finite number.");
            if (!p.LastSettlementId.IsEmpty && !settlementIds.Contains(p.LastSettlementId))
                result.AddError("Player character's last settlement does not exist.");

            if (p.LocationKind == CharacterLocationKind.Settlement)
            {
                var settlement = world.FindSettlement(p.SettlementId);
                if (settlement == null) result.AddError("Player character is in a settlement that does not exist.");
                else if (p.RegionId != settlement.RegionId) result.AddError("Player character's region does not match their settlement's region.");
                if (!regions.ContainsKey(p.RegionId)) result.AddError("Player character is in a region that does not exist.");
            }
            else if (p.LocationKind == CharacterLocationKind.Place)
            {
                // Phase 13: in a place, the region is the place's, and a settlement only for an interior (checked with the places).
                var place = world.FindPlace(p.PlaceId);
                if (place != null && p.RegionId != place.RegionId) result.AddError("Player character's region does not match their place's region.");
                if (place != null && p.SettlementId != (place.Category == Realmkeeper.Domain.Places.PlaceCategory.Interior ? place.SettlementId : default))
                    result.AddError("Player character in a place names the wrong settlement.");
            }
            else if (p.LocationKind == CharacterLocationKind.Overland)
            {
                // Phase 23 (ADR-0110, ADR-0111): walking a region of the surface or of another realm; never in a settlement.
                if (!p.SettlementId.IsEmpty) result.AddError("Player character in the country must not be in a settlement.");
                if (RealmIds.IsSurface(p.RealmId))
                {
                    if (world.FindRegion(p.RegionId) == null) result.AddError("Player character walks a region that does not exist.");
                }
                else if (world.FindRealm(p.RealmId)?.FindRegion(p.RegionId) == null) result.AddError("Player character walks a realm's region that does not exist.");
            }
            else if (!p.SettlementId.IsEmpty || !p.RegionId.IsEmpty && world.FindRegion(p.RegionId) == null)
            {
                // Phase 16: on the map (on a journey) the player is in a region of the connected world, but never in a settlement.
                result.AddError("Player character on the world map must not reference a settlement or an unknown region.");
            }
        }

        private static void ValidateTrade(WorldData world, HashSet<SettlementId> settlementIds, ValidationResult result)
        {
            if (world.TradeRoutes == null || world.Shipments == null)
            {
                result.AddError("Trade collections must not be null.");
                return;
            }

            var routes = new Dictionary<TradeRouteId, TradeRouteData>();
            foreach (var r in world.TradeRoutes)
            {
                if (r == null) { result.AddError("Null trade route entry."); continue; }
                var label = $"Trade route {r.TradeRouteId}";
                if (r.TradeRouteId.IsEmpty) result.AddError("Trade route has empty ID.");
                else if (routes.ContainsKey(r.TradeRouteId)) result.AddError($"Duplicate TradeRouteId {r.TradeRouteId}.");
                else routes.Add(r.TradeRouteId, r);
                if (!settlementIds.Contains(r.OriginId)) result.AddError($"{label} has an unknown origin settlement.");
                if (!settlementIds.Contains(r.DestinationId)) result.AddError($"{label} has an unknown destination settlement.");
                if (r.OriginId == r.DestinationId) result.AddError($"{label} starts and ends at the same settlement.");
                if (r.CapacityPerShipment < 1) result.AddError($"{label} capacity must be at least 1.");
                if (r.TravelDays < 1) result.AddError($"{label} travel time must be at least 1 day.");
                if (r.FrequencyDays < 1) result.AddError($"{label} frequency must be at least 1 day.");
                if (r.EfficiencyPercent < 1 || r.EfficiencyPercent > 100) result.AddError($"{label} efficiency must be 1..100.");
                if (r.ShipmentsDelivered < 0) result.AddError($"{label} has a negative delivery count.");
                if (r.Delivered == null) result.AddError($"{label} has no delivery totals.");
            }

            var shipmentIds = new HashSet<ShipmentId>();
            var routesInUse = new HashSet<TradeRouteId>();
            foreach (var s in world.Shipments)
            {
                if (s == null) { result.AddError("Null shipment entry."); continue; }
                var label = $"Shipment {s.ShipmentId}";
                if (s.ShipmentId.IsEmpty) result.AddError("Shipment has empty ID.");
                else if (!shipmentIds.Add(s.ShipmentId)) result.AddError($"Duplicate ShipmentId {s.ShipmentId}.");
                if (!routes.TryGetValue(s.TradeRouteId, out var route)) result.AddError($"{label} references an unknown trade route.");
                else if (!routesInUse.Add(s.TradeRouteId)) result.AddError($"Trade route {s.TradeRouteId} has more than one shipment on the road.");
                else if (s.OriginId != route.OriginId) result.AddError($"{label} origin does not match its route.");
                if (!settlementIds.Contains(s.OriginId) || !settlementIds.Contains(s.DestinationId)) result.AddError($"{label} references an unknown settlement.");
                if (s.ResourceTypeId.IsEmpty || !Economy.ResourceTypes.IsKnown(s.ResourceTypeId) || s.ResourceTypeId == Economy.ResourceTypes.Coin)
                    result.AddError($"{label} carries an unknown or non-tradeable resource.");
                if (s.Quantity < 1) result.AddError($"{label} quantity must be positive.");
                if (s.ArrivalDay < s.DepartureDay) result.AddError($"{label} arrives before it departs.");
                if (s.DaysWaiting < 0) result.AddError($"{label} has negative waiting days.");
            }
        }

        public static void ValidateSettlement(SettlementData s, ValidationResult result)
        {
            var label = string.IsNullOrWhiteSpace(s.Name) ? s.SettlementId.ToString() : s.Name;
            if (s.SettlementId.IsEmpty) result.AddError($"Settlement '{label}' has empty ID.");
            if (string.IsNullOrWhiteSpace(s.Name)) result.AddError($"Settlement {s.SettlementId} has no name.");
            else if (s.Name.Length > SettlementData.MaxNameLength) result.AddError($"Settlement '{label}' name is too long.");
            if (s.Population < 0) result.AddError($"Settlement '{label}' has negative population.");
            if (s.Prosperity < SettlementData.MinRating || s.Prosperity > SettlementData.MaxRating)
                result.AddError($"Settlement '{label}' prosperity {s.Prosperity} is outside 0..100.");
            if (s.Stability < SettlementData.MinRating || s.Stability > SettlementData.MaxRating)
                result.AddError($"Settlement '{label}' stability {s.Stability} is outside 0..100.");
            if (s.Position.X < 0 || s.Position.Z < 0) result.AddError($"Settlement '{label}' has a negative map position.");
            if (s.Economy == null)
            {
                result.AddError($"Settlement '{label}' has no economy.");
            }

            if (s.ResourceInventory == null)
            {
                result.AddError($"Settlement '{label}' has no resource inventory.");
            }
            else
            {
                foreach (var stack in s.ResourceInventory.Stacks)
                {
                    if (stack.Quantity < 0) result.AddError($"Settlement '{label}' has negative {stack.ResourceTypeId}.");
                    if (!Economy.ResourceTypes.IsKnown(stack.ResourceTypeId)) result.AddError($"Settlement '{label}' stores unknown resource '{stack.ResourceTypeId}'.");
                    if (s.Economy != null && stack.Quantity > s.Economy.CapacityOf(stack.ResourceTypeId))
                        result.AddError($"Settlement '{label}' stores more {stack.ResourceTypeId} than its capacity.");
                }
            }

            if (s.Economy != null) ValidateEconomy(s, label, result);
        }

        private static void ValidateEconomy(SettlementData s, string label, ValidationResult result)
        {
            var e = s.Economy;
            if (e.Production == null || e.StorageCapacity == null || e.LastDay == null || e.LastDaySources == null ||
                e.ResourceStatuses == null || e.LifetimeProduced == null || e.Markers == null)
            {
                result.AddError($"Settlement '{label}' economy has missing sections.");
                return;
            }

            var seen = new HashSet<Economy.ProductionSourceType>();
            var workers = 0L;
            foreach (var p in e.Production)
            {
                if (p == null) { result.AddError($"Settlement '{label}' has a null production entry."); continue; }
                if (!seen.Add(p.SourceType)) result.AddError($"Settlement '{label}' lists {p.SourceType} twice.");
                if (p.AllocationWeight < 0 || p.WorkerCapacity < 0 || p.Workers < 0)
                    result.AddError($"Settlement '{label}' {p.SourceType} has negative labour values.");
                if (p.Workers > p.WorkerCapacity) result.AddError($"Settlement '{label}' {p.SourceType} employs more workers than its capacity.");
                if (p.EfficiencyPercent < 1 || p.EfficiencyPercent > 300) result.AddError($"Settlement '{label}' {p.SourceType} efficiency must be 1..300.");
                workers += p.Workers;
            }

            if (s.Population >= 0 && workers > s.Population) result.AddError($"Settlement '{label}' employs more workers than it has people.");
            if (e.DaysWithoutFood < 0) result.AddError($"Settlement '{label}' has negative days without food.");
            if (e.TradeActivity < 0) result.AddError($"Settlement '{label}' has negative trade activity.");
            if (e.ProsperityTarget < SettlementData.MinRating || e.ProsperityTarget > SettlementData.MaxRating ||
                e.StabilityTarget < SettlementData.MinRating || e.StabilityTarget > SettlementData.MaxRating)
                result.AddError($"Settlement '{label}' rating targets are outside 0..100.");
            foreach (var status in e.ResourceStatuses)
            {
                if (status == null || status.DaysBelowThreshold < 0) result.AddError($"Settlement '{label}' has an invalid resource status.");
            }
        }

        public static ValidationResult ValidateSettlement(SettlementData settlement)
        {
            var result = new ValidationResult();
            if (settlement == null) result.AddError("Settlement is null.");
            else ValidateSettlement(settlement, result);
            return result;
        }
    }
}
