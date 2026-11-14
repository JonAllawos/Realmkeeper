using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Application.Atlas;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Atlas;
using Realmkeeper.Simulation.Encounters;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Overland
{
    /// <summary>Phase 24 (ADR-0114): a travelling group as the country shows it — where it is now on the region's road.</summary>
    public sealed class TravellerView
    {
        public string GroupId { get; set; }
        public TravelGroupKind Kind { get; set; }
        public TravelGroupState State { get; set; }
        public string Name { get; set; }
        public NpcId LeaderId { get; set; }
        public string Leader { get; set; }
        public string Faction { get; set; }
        public int Members { get; set; }
        public string From { get; set; }
        public string To { get; set; }

        /// <summary>Region metres, and the heading along the road (degrees, 0 = north).</summary>
        public float X { get; set; }

        public float Z { get; set; }
        public float Yaw { get; set; }

        /// <summary>Outlaws: met as an encounter, not a conversation.</summary>
        public bool Hostile => Kind == TravelGroupKind.Bandits;

        /// <summary>A short line ("Caravan of Mossholm, bound for Westerthorpe").</summary>
        public string Line { get; set; }
    }

    /// <summary>
    /// Phase 24 (ADR-0114): the world's own travelling groups (<see cref="TravelingGroupData"/>), placed in the country. Their
    /// position is the simulation's (route, departure and due time, the clock), mapped onto the region's road: nothing is
    /// spawned to fill the view, and leaving or reloading changes nothing about them.
    /// </summary>
    public static class Travellers
    {
        public static List<TravellerView> InRegion(WorldSession session, WorldData w, RegionId region)
        {
            var list = new List<TravellerView>();
            var now = QuestClock.Now(w);
            foreach (var g in w.Groups)
            {
                if (!g.IsActive) continue;
                if (!Where(w, g, now, out var seg, out var from, out var metres)) continue;
                if (seg.RegionA != region && seg.RegionB != region) continue; // never build another region's country to place a group elsewhere
                if (seg.IsCrossing)
                {
                    var half = metres * 2 < TravelRules.Km(seg) * 1000 ? from : seg.Other(from); // the half it is on lies in that end's region
                    if (w.FindSettlement(half)?.RegionId != region) continue;
                }

                if (!OverlandRoads.PointOf(session, w, seg, from, metres, out var r, out var x, out var z) || r != region) continue;
                // Heading: toward a point a little further along (stationed groups face along the road).
                var ahead = Math.Min((int)(TravelRules.Km(seg) * 1000), metres + 300);
                var yaw = 0f;
                if (OverlandRoads.PointOf(session, w, seg, from, ahead, out var r2, out var ax, out var az) && r2 == region && (ax != x || az != z))
                    yaw = (float)(Math.Atan2(ax - x, az - z) * 180 / Math.PI);
                var leader = w.FindNpc(g.LeaderNpcId);
                var v = new TravellerView
                {
                    GroupId = g.GroupId, Kind = g.Kind, State = g.State, Name = g.Name, LeaderId = g.LeaderNpcId, Leader = leader?.Name, Faction = w.FindFaction(g.FactionId)?.Name,
                    Members = Math.Max(1, g.Members), From = w.FindSettlement(g.OriginId)?.Name, To = w.FindSettlement(g.DestinationId)?.Name, X = x, Z = z, Yaw = yaw,
                };
                v.Line = g.Kind == TravelGroupKind.Bandits ? g.Name + ", holding the road"
                    : g.State == TravelGroupState.Stationed ? g.Name + ", keeping watch on the road"
                    : $"{g.Name}, bound for {v.To ?? "somewhere"}";
                list.Add(v);
            }

            return list;
        }

        /// <summary>A group's place on its route now: the segment, the settlement it set out from along it, and metres along it.</summary>
        public static bool Where(WorldData w, TravelingGroupData g, long now, out RouteSegmentData seg, out SettlementId from, out int metres)
        {
            seg = null;
            from = default;
            metres = 0;
            if (g.Segments.Count == 0) return false;
            if (g.State == TravelGroupState.Stationed || g.State == TravelGroupState.Halted || g.DueAt <= g.DepartedAt)
            {
                seg = w.FindRoute(g.StationRouteId ?? g.Segments[0]) ?? w.FindRoute(g.Segments[0]);
                if (seg == null) return false;
                from = seg.A;
                metres = (int)(TravelRules.Km(seg) * 1000 * (g.State == TravelGroupState.Halted ? 0.25 : 0.5));
                return true;
            }

            var t = Math.Max(0, Math.Min(1, (now - g.DepartedAt) / (double)(g.DueAt - g.DepartedAt)));
            double total = 0;
            foreach (var id in g.Segments) total += w.FindRoute(id) is RouteSegmentData s ? TravelRules.Km(s) : 0;
            var target = total * t;
            for (var i = 0; i < g.Segments.Count; i++)
            {
                var s = w.FindRoute(g.Segments[i]);
                if (s == null) continue;
                var km = TravelRules.Km(s);
                if (target <= km || i == g.Segments.Count - 1)
                {
                    seg = s;
                    from = g.Nodes.Count > i ? g.Nodes[i] : s.A;
                    metres = (int)(Math.Max(0, Math.Min(target, km)) * 1000);
                    return true;
                }

                target -= km;
            }

            return false;
        }
    }

    /// <summary>Phase 24: talk with a travelling group the player stands by: their news (real rumours of real events), and how they take the player.</summary>
    public sealed class TalkToTravellersCommand : ICommand
    {
        public TalkToTravellersCommand(string groupId) { GroupId = groupId; }
        public string GroupId { get; }
    }

    /// <summary>Phase 24: give a ration to refugees on the road (their home settlement and their leader remember it).</summary>
    public sealed class HelpTravellersCommand : ICommand
    {
        public HelpTravellersCommand(string groupId) { GroupId = groupId; }
        public string GroupId { get; }
    }

    public enum OverlandChoice
    {
        Fight = 0,

        /// <summary>Stealth against the band's or creature's notice; failing it, they attack.</summary>
        SlipAway = 1,

        /// <summary>Turn back the way you came (always possible; you lose the time and the way).</summary>
        BackOff = 2,

        /// <summary>Bandits only: pay their toll.</summary>
        Pay = 3,
    }

    /// <summary>Phase 24 (ADR-0114): answer the pending overland encounter.</summary>
    public sealed class ResolveOverlandEncounterCommand : ICommand
    {
        public ResolveOverlandEncounterCommand(string encounterId, OverlandChoice choice)
        {
            EncounterId = encounterId;
            Choice = choice;
        }

        public string EncounterId { get; }
        public OverlandChoice Choice { get; }
    }

    /// <summary>
    /// Phase 24 (ADR-0114): encounters in the country. Checked once per half-hour bucket of the clock while walking, never more
    /// (the bucket is the encounter's id, saved: loading never rerolls it), with a three-hour quiet after any encounter. Only a
    /// real source can be met: a creature population whose range covers the place, or a bandit band holding the road the player
    /// walks. The chance follows the country: remote ground and night raise it; roads, towns, watch posts and patrols lower it.
    /// </summary>
    public static class OverlandEncounters
    {
        public const int BucketMinutes = 30;
        public const int QuietMinutes = 180;
        public const float BanditRoadMetres = 160f;
        public const float TalkMetres = 30f;

        public static void Register(CommandRegistry commands)
        {
            commands.Register<TalkToTravellersCommand>(new TalkHandler());
            commands.Register<HelpTravellersCommand>(new HelpHandler());
            commands.Register<ResolveOverlandEncounterCommand>(new ResolveHandler());
        }

        /// <summary>The source an encounter could come from here, and how likely one is this bucket (0..1).</summary>
        public static (string kind, string source, double chance, string text) Odds(WorldSession session, WorldData w, PlayerCharacterData p)
        {
            if (!RealmIds.IsSurface(p.RealmId)) return (null, null, 0, null); // the ancient realm: nothing is invented
            var terrain = session.GetOverlandTerrain(RealmIds.Surface, p.RegionId);
            if (terrain == null) return (null, null, 0, null);
            var cell = terrain.CellAt(p.Pose.X, p.Pose.Z);
            var here = OverlandScale.CellAt(terrain.Map, p.Pose.X, p.Pose.Z);
            var night = TravelRules.IsNight(w.MinuteOfDay);

            // Bandits on the road the player walks.
            foreach (var g in w.Groups)
            {
                if (g.Kind != TravelGroupKind.Bandits || !g.IsActive || g.Strength <= 0) continue;
                var route = w.FindRoute(g.StationRouteId ?? (g.Segments.Count > 0 ? g.Segments[0] : null));
                if (route == null || route.IsCrossing && route.RegionA != p.RegionId && route.RegionB != p.RegionId) continue;
                if (!route.IsCrossing && w.FindSettlement(route.A)?.RegionId != p.RegionId) continue;
                var path = OverlandRoads.PathBetween(terrain.Map, route.A, route.B);
                if (path == null) continue;
                var near = float.MaxValue;
                foreach (var c in path)
                {
                    var (x, z) = OverlandScale.CellCentre(c);
                    near = Math.Min(near, (x - p.Pose.X) * (x - p.Pose.X) + (z - p.Pose.Z) * (z - p.Pose.Z));
                }

                if (near > BanditRoadMetres * BanditRoadMetres) continue;
                return ("bandits", g.GroupId, night ? 0.45 : 0.3, $"{g.Name} step out onto the road ahead. \"Toll's due, friend.\"");
            }

            // A creature population whose range covers this place.
            CreaturePopulationData best = null;
            CreatureDefinition bestDef = null;
            foreach (var pop in w.CreaturePopulations)
            {
                if (pop.RegionId != p.RegionId || pop.Count <= 0) continue;
                if (w.Groups.Exists(g => g.PopulationId == pop.PopulationId)) continue; // a road band is met as bandits
                if (Math.Max(Math.Abs(pop.Center.X - here.X), Math.Abs(pop.Center.Z - here.Z)) > pop.RangeCells + 2) continue;
                var def = CreatureCatalog.Find(pop.CreatureId);
                if (def == null || def.Attacks.Count == 0 || def.ChallengeRating == "0") continue; // a hare is not an encounter
                if (best == null || pop.Count > best.Count || (pop.Count == best.Count && string.CompareOrdinal(pop.PopulationId, best.PopulationId) < 0)) { best = pop; bestDef = def; }
            }

            if (best == null) return (null, null, 0, null);
            var chance = 0.08 + 0.025 * Math.Sqrt(best.Count);
            if (night) chance *= 1.6;
            if (cell.Road) chance *= 0.5;
            if (cell.Biome == Biome.DenseForest || cell.Biome == Biome.Marsh || cell.Biome == Biome.Rocky) chance *= 1.3;
            var nearestTown = int.MaxValue;
            foreach (var s in w.Settlements) if (s.RegionId == p.RegionId) nearestTown = Math.Min(nearestTown, Math.Max(Math.Abs(s.Position.X - here.X), Math.Abs(s.Position.Z - here.Z)));
            if (nearestTown <= 12) chance *= 0.3;
            foreach (var l in Landmarks.InRegion(session, w, RealmIds.Surface, p.RegionId))
                if (l.Kind == LandmarkKind.WatchPost && (l.X - p.Pose.X) * (l.X - p.Pose.X) + (l.Z - p.Pose.Z) * (l.Z - p.Pose.Z) < 700f * 700f) chance *= 0.4;
            if (w.Groups.Exists(g => g.IsActive && (g.Kind == TravelGroupKind.Patrol || g.Kind == TravelGroupKind.FactionPatrol) && Travellers.Where(w, g, QuestClock.Now(w), out var s2, out _, out _) && (s2.RegionA == p.RegionId || s2.RegionB == p.RegionId)))
                chance *= 0.7;
            var weather = GroundConditions.Of(w, p.RegionId, w.MinuteOfDay);
            if (weather.SightFactor < 0.7f) chance *= 0.8;
            var text = $"{(best.Count > 1 ? "Something is" : "Something is")} moving in the {Words(cell.Biome)}: {bestDef.Name.ToLowerInvariant()}{(best.Count > 1 ? "s" : string.Empty)} {(night ? "in the dark" : "nearby")}, and they have seen you.";
            return ("creature", best.PopulationId, Math.Min(0.6, chance), text);
        }

        /// <summary>
        /// Called as the player walks (inside the walk command): a new half-hour bucket may bring an encounter. Returns the new
        /// pending encounter, or null.
        /// </summary>
        internal static OverlandEncounterData Check(WorldSession session, WorldData w, PlayerCharacterData p)
        {
            var o = w.Overland;
            if (o == null || o.Pending != null || w.ActiveEncounter != null) return null;
            if (p.Rpg == null || p.Rpg.State != CreationState.Complete || p.Rpg.IsDead) return null;
            var now = QuestClock.Now(w);
            var bucket = now / BucketMinutes;
            if (bucket == o.LastCheckBucket) return null;
            o.LastCheckBucket = bucket;
            if (o.LastEncounterAt >= 0 && now - o.LastEncounterAt < QuietMinutes) return null;
            var (kind, source, chance, text) = Odds(session, w, p);
            if (kind == null) return null;
            var id = "encounter:" + p.RegionId + ":" + bucket.ToString(CultureInfo.InvariantCulture);
            if (o.FindEncounter(id) != null) return null;
            var roll = NpcHash.Roll(NpcHash.Of(w.Seed, "overland-encounter", id, 0), 1000) / 1000.0;
            if (roll >= chance) return null;
            var e = new OverlandEncounterData
            {
                Id = id, Kind = kind == "bandits" ? OverlandEncounterKind.Bandits : OverlandEncounterKind.Creature, State = OverlandEncounterState.Pending, RegionId = p.RegionId,
                X = p.Pose.X, Z = p.Pose.Z, At = now, Text = text,
            };
            if (e.Kind == OverlandEncounterKind.Bandits) { e.GroupId = source; e.PopulationId = w.FindGroup(source)?.PopulationId; }
            else e.PopulationId = source;
            o.Add(e);
            o.LastEncounterAt = now;
            return e;
        }

        /// <summary>
        /// Phase 24: whether a long rest here is broken in the night, and by what ("overland:{population}"), or null. A
        /// travellers' camp is safe; elsewhere, a population whose range covers the place may come (deterministic by night).
        /// </summary>
        public static string NightThreat(WorldSession session, WorldData w)
        {
            var p = w.Player;
            if (p == null || !p.IsOverland || !RealmIds.IsSurface(p.RealmId)) return null;
            foreach (var l in Landmarks.InRegion(session, w, RealmIds.Surface, p.RegionId))
                if (l.Kind == LandmarkKind.TravellersCamp && (l.X - p.Pose.X) * (l.X - p.Pose.X) + (l.Z - p.Pose.Z) * (l.Z - p.Pose.Z) < 40f * 40f) return null;
            var (kind, source, chance, _) = Odds(session, w, p);
            if (kind != "creature") return null;
            var night = w.CurrentSimulationDate.ToDayNumber();
            var roll = NpcHash.Roll(NpcHash.Of(w.Seed, "overland-night", p.RegionId + ":" + source, night), 1000) / 1000.0;
            return roll < Math.Min(0.75, chance * 2.5) ? "overland:" + source : null;
        }

        /// <summary>A night's rest broken by a population: the fight starts (recorded as an overland encounter).</summary>
        internal static CommandResult NightFight(CommandContext context, string populationId)
        {
            var w = context.Edit();
            var p = w.Player;
            var id = "encounter:" + p.RegionId + ":night:" + w.CurrentSimulationDate.ToDayNumber().ToString(CultureInfo.InvariantCulture);
            var e = w.Overland.FindEncounter(id);
            if (e == null)
            {
                e = new OverlandEncounterData { Id = id, Kind = OverlandEncounterKind.Creature, State = OverlandEncounterState.Pending, RegionId = p.RegionId, X = p.Pose.X, Z = p.Pose.Z, At = QuestClock.Now(w), PopulationId = populationId, Text = "Something found your camp in the night." };
                w.Overland.Add(e);
            }

            w.Overland.LastEncounterAt = QuestClock.Now(w);
            return Fight(context, w, e, "Woken in the dark — ");
        }

        /// <summary>The fight, through the one combat engine: individuals drawn from the real population (or band) near the nearest town.</summary>
        private static CommandResult Fight(CommandContext context, WorldData w, OverlandEncounterData e, string lead = "")
        {
            var p = w.Player;
            var map = context.Session.MapFor(p.RegionId);
            SettlementData near = null;
            var bestD = int.MaxValue;
            var terrain = context.Session.GetOverlandTerrain(RealmIds.Surface, p.RegionId);
            var here = terrain != null ? OverlandScale.CellAt(terrain.Map, p.Pose.X, p.Pose.Z) : default;
            foreach (var s in w.Settlements)
            {
                if (s.RegionId != p.RegionId) continue;
                var d = Math.Max(Math.Abs(s.Position.X - here.X), Math.Abs(s.Position.Z - here.Z));
                if (d < bestD) { bestD = d; near = s; }
            }

            var pop = w.CreaturePopulations.Find(x => x.PopulationId == e.PopulationId);
            if (map == null || near == null || pop == null || pop.Count <= 0)
            {
                e.State = OverlandEncounterState.Left;
                e.Outcome = "Nothing came of it.";
                return CommandResult.Ok("Whatever it was is gone.");
            }

            var attempt = 0;
            foreach (var x in w.Encounters) if (x.Origin != null) attempt++;
            var plan = EncounterGenerator.Plan(w, map, new EncounterRequest
            {
                Near = near.SettlementId, Purpose = e.Kind == OverlandEncounterKind.Bandits ? EncounterPurpose.Road : EncounterPurpose.Outskirts, Attempt = attempt, Seek = pop.CreatureId, Population = pop.PopulationId,
            });
            if (plan == null || !plan.IsValid)
            {
                e.State = OverlandEncounterState.Left;
                e.Outcome = "They melted away before a blow was struck.";
                return CommandResult.Ok("They melt away before you reach them.");
            }

            var r = WorldEncounters.StartPlan(context, plan);
            if (!r.IsSuccess) return r;
            e.State = OverlandEncounterState.Fought;
            e.EncounterId = w.ActiveEncounter?.EncounterId.ToString();
            e.Outcome = "Fought.";
            if (e.GroupId != null) w.FindGroup(e.GroupId)?.Note($"{TravelRules.Clock(QuestClock.Now(w))} fought a traveller on foot.");
            return CommandResult.Ok(lead + r.Message);
        }

        private sealed class ResolveHandler : ICommandHandler<ResolveOverlandEncounterCommand>
        {
            public CommandResult Handle(ResolveOverlandEncounterCommand c, CommandContext context)
            {
                var refused = OverlandCommandHandlers.Ready(context, out var player);
                if (refused != null) return refused;
                var pending = context.Committed.Overland?.FindEncounter(c.EncounterId);
                if (pending == null || pending.State != OverlandEncounterState.Pending) return CommandResult.InvalidState("Nothing waits on you.");
                var w = context.Edit();
                var e = w.Overland.FindEncounter(c.EncounterId);
                var p = w.Player;
                var band = e.GroupId != null ? w.FindGroup(e.GroupId) : null;
                switch (c.Choice)
                {
                    case OverlandChoice.Pay:
                    {
                        if (band == null) return CommandResult.Invalid("There is no one to pay.");
                        var toll = TravelCommandHandlers.Toll(band);
                        if (p.Purse < toll) return CommandResult.Invalid($"Their toll is {toll} coin; you have {p.Purse}.");
                        p.Purse -= toll;
                        e.State = OverlandEncounterState.Paid;
                        e.Outcome = $"Paid {toll} coin.";
                        band.Note($"{TravelRules.Clock(QuestClock.Now(w))} took {toll} coin from a traveller on foot.");
                        return CommandResult.Ok($"You pay {toll} coin and they let you pass.");
                    }

                    case OverlandChoice.SlipAway:
                    {
                        var bonus = 0;
                        if (p.Rpg != null && p.Rpg.State == CreationState.Complete) bonus = new CharacterRules(p.Rpg, CharacterRules.LoadoutOf(p)).SkillBonus(Skill.Stealth).Total;
                        var d20 = 1 + NpcHash.Roll(NpcHash.Of(w.Seed, "overland-slip", e.Id, 0), 20);
                        var cell = context.Session.GetOverlandTerrain(RealmIds.Surface, p.RegionId)?.CellAt(p.Pose.X, p.Pose.Z) ?? default;
                        var cover = cell.Concealment >= 0.5f ? 2 : 0; // the trees hide you
                        var dc = band != null ? 10 + band.Strength : 12 + (TravelRules.IsNight(w.MinuteOfDay) ? -2 : 0);
                        if (d20 + bonus + cover >= dc)
                        {
                            e.State = OverlandEncounterState.Avoided;
                            e.Outcome = "Slipped away unseen.";
                            return CommandResult.Ok($"Stealth {d20} + {bonus}{(cover > 0 ? " + 2 (cover)" : string.Empty)} = {d20 + bonus + cover} against {dc}: you slip away unseen.");
                        }

                        var fought = Fight(context, w, e);
                        return fought.IsSuccess ? CommandResult.Ok($"Stealth {d20} + {bonus}{(cover > 0 ? " + 2 (cover)" : string.Empty)} = {d20 + bonus + cover} against {dc}: they see you. " + fought.Message) : fought;
                    }

                    case OverlandChoice.BackOff:
                        e.State = OverlandEncounterState.Avoided;
                        e.Outcome = "Backed off the way they came.";
                        return CommandResult.Ok(band != null ? "You turn back. The road ahead is theirs for now: go round, or come back with help." : "You back away slowly, and they let you go.");
                    default:
                        return Fight(context, w, e);
                }
            }
        }

        private sealed class TalkHandler : ICommandHandler<TalkToTravellersCommand>
        {
            public CommandResult Handle(TalkToTravellersCommand c, CommandContext context)
            {
                var refused = OverlandCommandHandlers.Ready(context, out var player);
                if (refused != null) return refused;
                var world = context.Committed;
                var view = Near(context.Session, world, player, c.GroupId, out refused);
                if (view == null) return refused;
                if (view.Hostile) return CommandResult.InvalidState($"{view.Name} are not here to talk.");
                var w = context.Edit();
                var g = w.FindGroup(c.GroupId);
                var lines = new List<string>();
                var who = view.Leader ?? view.Name;
                if (g.Kind == TravelGroupKind.Patrol || g.Kind == TravelGroupKind.FactionPatrol)
                {
                    var standing = g.FactionId.IsEmpty ? 0 : Standings.Faction(w, g.FactionId);
                    var notorious = (w.Player.Social?.Notoriety ?? 0) >= 60;
                    if (standing <= -40 || notorious) lines.Add($"{who} looks you over and does not like what they see. \"Keep walking. We know your face.\"");
                    else if (standing >= 40) lines.Add($"{who} greets you by name. \"The road's safer with you on it.\"");
                    else lines.Add($"{who} asks your business on the road, hears you out, and waves you on.");
                }
                else lines.Add(g.Kind == TravelGroupKind.Refugees ? $"{who} speaks for the group: they left {view.From} with what they could carry." : $"{who} stops to pass the time of day.");

                // News: what the people they come from and go to know, told as rumour (the player can check it later).
                var told = 0;
                foreach (var e in w.WorldEvents)
                {
                    if (told >= 3 || e.State == WorldEventState.Resolved || e.State == WorldEventState.Failed) continue;
                    if (e.Known != EventKnowledge.Unknown && !e.KnowledgeOutdated) continue;
                    var touches = e.SettlementId == g.OriginId || e.SettlementId == g.DestinationId || (e.RouteId != null && g.Segments.Contains(e.RouteId)) || e.GroupId == g.GroupId;
                    if (!touches) continue;
                    WorldEvents.PlayerLearns(e, EventKnowledge.Rumored, QuestClock.Now(w), view.Name);
                    lines.Add("They tell you: " + WorldEvents.Telling(w, e) + ".");
                    told++;
                }

                if (told == 0) lines.Add("They have no news you have not heard.");
                Record(w, g, view, OverlandEncounterState.Settled, told > 0 ? "Shared their news." : "Passed the time of day.");
                return CommandResult.Ok(string.Join(" ", lines));
            }
        }

        private sealed class HelpHandler : ICommandHandler<HelpTravellersCommand>
        {
            public CommandResult Handle(HelpTravellersCommand c, CommandContext context)
            {
                var refused = OverlandCommandHandlers.Ready(context, out var player);
                if (refused != null) return refused;
                var world = context.Committed;
                var view = Near(context.Session, world, player, c.GroupId, out refused);
                if (view == null) return refused;
                if (view.Kind != TravelGroupKind.Refugees) return CommandResult.InvalidState($"{view.Name} need nothing from you.");
                var key = "helped:" + c.GroupId;
                if (world.Overland.FindEncounter(key) != null) return CommandResult.InvalidState("You have already given them what you could.");
                var ration = player.Inventory.Find(s => s.ItemId != null && s.ItemId.IndexOf("ration", StringComparison.OrdinalIgnoreCase) >= 0 && s.Quantity > 0);
                if (ration == null) return CommandResult.InvalidState("You have no rations to give.");
                var w = context.Edit();
                var p = w.Player;
                var stack = p.Inventory.Find(s => s.StackId.Equals(ration.StackId));
                stack.Quantity--;
                if (stack.Quantity <= 0) p.Inventory.Remove(stack);
                var g = w.FindGroup(c.GroupId);
                var report = QuestRuntime.Reporter(context);
                Reputation.Change(w, "settlement:" + g.OriginId, "Reputation", 4, key + ":home", "helped its people on the road", report);
                if (!g.LeaderNpcId.IsEmpty) Reputation.Change(w, "npc:" + g.LeaderNpcId, "Gratitude", 8, key + ":leader", "gave food on the road", report);
                g.Note($"{TravelRules.Clock(QuestClock.Now(w))} were given food by a traveller.");
                w.Overland.Add(new OverlandEncounterData { Id = key, Kind = OverlandEncounterKind.Travellers, State = OverlandEncounterState.Settled, RegionId = p.RegionId, X = p.Pose.X, Z = p.Pose.Z, At = QuestClock.Now(w), GroupId = g.GroupId, Outcome = "Gave a ration." });
                return CommandResult.Ok($"You give them a ration. {view.Leader ?? "Their leader"} thanks you; {view.From} will hear of it.");
            }
        }

        private static TravellerView Near(WorldSession session, WorldData w, PlayerCharacterData p, string groupId, out CommandResult refused)
        {
            refused = null;
            if (!p.IsOverland) { refused = CommandResult.InvalidState($"{p.Name} is not out in the country."); return null; }
            var view = Travellers.InRegion(session, w, p.RegionId).Find(t => t.GroupId == groupId);
            if (view == null) { refused = CommandResult.NotFound("They are not here."); return null; }
            var d = Math.Sqrt((view.X - p.Pose.X) * (view.X - p.Pose.X) + (view.Z - p.Pose.Z) * (view.Z - p.Pose.Z));
            if (d > TalkMetres * 2) { refused = CommandResult.InvalidState($"{view.Name} are {d:0} m away: walk up to them first."); return null; }
            return view;
        }

        private static void Record(WorldData w, TravelingGroupData g, TravellerView v, OverlandEncounterState state, string outcome)
        {
            var id = "travellers:" + g.GroupId + ":" + w.CurrentSimulationDate.ToDayNumber().ToString(CultureInfo.InvariantCulture);
            if (w.Overland.FindEncounter(id) != null) return;
            w.Overland.Add(new OverlandEncounterData { Id = id, Kind = OverlandEncounterKind.Travellers, State = state, RegionId = w.Player.RegionId, X = v.X, Z = v.Z, At = QuestClock.Now(w), GroupId = g.GroupId, Outcome = outcome, Text = v.Line });
        }

        private static string Words(Biome b)
        {
            switch (b)
            {
                case Biome.DenseForest: return "deep woods";
                case Biome.Forest: return "trees";
                case Biome.Marsh: return "marsh";
                case Biome.Hills: case Biome.Rocky: return "rocks";
                case Biome.Heath: return "heather";
                default: return "long grass";
            }
        }
    }
}
