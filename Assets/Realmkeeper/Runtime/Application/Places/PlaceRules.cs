using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Simulation.Quests;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;

namespace Realmkeeper.Application.Places
{
    /// <summary>
    /// Phase 13 (ADR-0062 … ADR-0066): the rules of being in a place, used by its commands. Every function here edits the
    /// command's working copy, so a refused or failed action changes nothing (the command returns before commit).
    /// Checks roll the world's dice (keyed by place, target and attempt) through the character rules.
    /// </summary>
    public static class PlaceRules
    {
        public const float Cell = 1.5f;

        /// <summary>Failed attempts at one check allowed per visit; a new visit (or another approach) allows more.</summary>
        public const int AttemptsPerVisit = 3;

        // ----- Where the player is -------------------------------------------------------------

        public static PlaceData Current(WorldData w) => w?.Player != null && w.Player.IsInPlace ? w.FindPlace(w.Player.PlaceId) : null;

        public static PlaceRoom CurrentRoom(WorldData w)
        {
            var p = Current(w);
            return p?.Layout.Room(w.Player.PlaceRoom);
        }

        /// <summary>A pose standing in a cell, facing a direction (place space: x and z in metres, y the floor's height).</summary>
        public static LocalPose PoseAt(PlaceData place, int floor, int x, int z, float yaw)
        {
            var elevation = place.Layout.Floors.Find(f => f.Index == floor)?.Elevation ?? 0f;
            return new LocalPose((x + 0.5f) * Cell, elevation, (z + 0.5f) * Cell, yaw);
        }

        public static (int X, int Z) CellOf(LocalPose pose) => ((int)Math.Floor(pose.X / Cell), (int)Math.Floor(pose.Z / Cell));

        /// <summary>The pose inside an entrance: one cell in from the threshold, facing in.</summary>
        public static LocalPose EntrancePose(PlaceData place, PlaceConnection entrance)
        {
            var room = place.Layout.Room(entrance.A);
            var x = entrance.Rect.X + entrance.Rect.W / 2;
            var z = Math.Min(room.Rect.MaxZ - 1, entrance.Rect.Z + 1);
            return PoseAt(place, room.Floor, Math.Min(Math.Max(x, room.Rect.X), room.Rect.MaxX - 1), Math.Max(z, room.Rect.Z), 0f);
        }

        // ----- People in places ----------------------------------------------------------------

        /// <summary>True when a person is with the player in the place they are in (held there, following, posted there, or at home inside).</summary>
        public static bool Beside(WorldData w, NpcData npc)
        {
            var p = w.Player;
            if (p == null || npc == null || npc.IsDeceased) return false;
            var e = w.FindEscort(npc.NpcId);
            if (e != null && e.State == EscortState.Following) return true; // a follower is always beside the player
            if (!p.IsInPlace) return false;
            if (e != null && (e.State == EscortState.Captive || e.State == EscortState.Waiting) && e.PlaceId == p.PlaceId) return true;
            if (PlacePresence.PostedAt(w, npc.NpcId)?.PlaceId == p.PlaceId) return true;
            var place = w.FindPlace(p.PlaceId);
            if (place != null && !place.LocationId.IsEmpty && !PlacePresence.IsAway(w, npc))
            {
                var now = NpcActivityPlan.Now(w, npc);
                if (now.IsKnown && now.Location == place.LocationId && now.Activity != ActivityKind.Sleeping) return true;
            }

            return false;
        }

        /// <summary>The place and group a guard holds a gate for, when the player has not been let through: "placeId:groupId", or null.</summary>
        public static string GateHeldBy(WorldData w, NpcId npc)
        {
            var p = Current(w);
            if (p == null) return null;
            foreach (var g in p.Layout.Groups)
            {
                if (!g.Members.Exists(m => m.NpcId == npc)) continue;
                if (p.GroupStateOf(g.Id) != GroupState.Present) return null;
                foreach (var c in p.Layout.Connections)
                    if (c.GuardedBy == g.Id && !HasPassage(p, g.Id)) return p.PlaceId + ":" + g.Id;
            }

            return null;
        }

        public static bool HasPassage(PlaceData p, string groupId) =>
            p.Variables.TryGetValue("passage:" + groupId, out var v) && v == "granted" || p.GroupStateOf(groupId) == GroupState.Defeated;

        public static bool HasLeave(PlaceData p)
        {
            foreach (var pair in p.Variables) if (pair.Key.StartsWith("passage:", StringComparison.Ordinal) && pair.Value == "granted") return true;
            return false;
        }

        // ----- Passages ------------------------------------------------------------------------

        /// <summary>Why the player cannot pass a connection from a room, or null when they can.</summary>
        public static string Blocks(PlaceData p, PlaceConnection c, string fromRoom)
        {
            if (c.Kind == ConnectionKind.Secret && !p.Found(c)) return "There is no way through there.";
            if (c.GuardedBy != null && !HasPassage(p, c.GuardedBy) && p.GroupStateOf(c.GuardedBy) != GroupState.Peaceful)
                return (p.Layout.Group(c.GuardedBy)?.Name is string n ? Capital(n) : "The guards") + " bar the way. Talk to them, or find another way in.";
            if (c.Kind == ConnectionKind.Opening || c.IsVertical) return null;
            var state = p.DoorOf(c);
            switch (state)
            {
                case DoorState.Open:
                case DoorState.Broken:
                    return null;
                case DoorState.Locked:
                    return "It is locked.";
                case DoorState.Barred:
                    return fromRoom == c.A ? "It is barred on this side: lift the bar first." : "It is barred from the other side.";
                default:
                    return "It is shut.";
            }
        }

        // ----- Checks ---------------------------------------------------------------------------

        public static CharacterRules Rules(WorldData w) => new CharacterRules(w.Player.Rpg, CharacterRules.LoadoutOf(w.Player));

        public static bool CarriesLight(PlayerCharacterData p)
        {
            foreach (var s in p.Inventory)
                if (ItemCatalog.Find(s.ItemId)?.Tags is IReadOnlyList<string> tags && Contains(tags, "light")) return true;
            return false;
        }

        private static bool Contains(IReadOnlyList<string> list, string x)
        {
            foreach (var s in list) if (s == x) return true;
            return false;
        }

        /// <summary>Too dark to see well: an unlit room the player carries no light into and cannot see in the dark.</summary>
        public static bool Dark(WorldData w, PlaceRoom room) =>
            room != null && !room.Lit && room.Hazard == "darkness" && !CarriesLight(w.Player) && Rules(w).Darkvision <= 0;

        public sealed class Roll
        {
            public int Die;
            public int Total;
            public int Dc;
            public bool Success;
            public string Text;
        }

        /// <summary>A skill check against a DC with the world's dice; at a disadvantage two dice and the lower counts.</summary>
        public static Roll Check(WorldData w, Skill skill, int dc, string key, bool disadvantage = false)
        {
            var bonus = w.Player.Rpg != null && w.Player.Rpg.State == CreationState.Complete ? Rules(w).SkillBonus(skill).Total : 0;
            var die = SpellCommandHandlers.Dice(w, "place:" + key);
            var d = die(20);
            if (disadvantage) d = Math.Min(d, die(20));
            var total = d + bonus;
            var ok = d == 20 || (d != 1 && total >= dc);
            return new Roll { Die = d, Total = total, Dc = dc, Success = ok, Text = $"{SkillName(skill)} {total} ({d}{(bonus >= 0 ? "+" : string.Empty)}{bonus}{(disadvantage ? ", in the dark" : string.Empty)}) against {dc}: {(ok ? "success" : "failure")}" };
        }

        public static Roll Save(WorldData w, Ability ability, int dc, string key)
        {
            var bonus = w.Player.Rpg != null && w.Player.Rpg.State == CreationState.Complete ? Rules(w).SaveBonus(ability).Total : 0;
            var d = SpellCommandHandlers.Dice(w, "place-save:" + key)(20);
            var total = d + bonus;
            var ok = d == 20 || (d != 1 && total >= dc);
            return new Roll { Die = d, Total = total, Dc = dc, Success = ok, Text = $"{ability} save {total} against {dc}: {(ok ? "success" : "failure")}" };
        }

        public static int PassivePerception(WorldData w, PlaceRoom room)
        {
            var bonus = w.Player.Rpg != null && w.Player.Rpg.State == CreationState.Complete ? Rules(w).SkillBonus(Skill.Perception).Total : 0;
            return 10 + bonus - (Dark(w, room) ? 5 : 0);
        }

        public static string SkillName(Skill s) => s == Skill.SleightOfHand ? "Sleight of Hand" : s.ToString();

        /// <summary>The attempt counter for a check this visit: refused once the limit is reached.</summary>
        public static string AttemptsLeft(PlaceData p, string target, string what, out string key)
        {
            var counter = "attempts:" + target + ":" + what + ":" + p.Visits.ToString(CultureInfo.InvariantCulture);
            p.Variables.TryGetValue(counter, out var v);
            var n = int.TryParse(v, out var x) ? x : 0;
            key = p.PlaceId + ":" + target + ":" + what + ":" + p.Visits.ToString(CultureInfo.InvariantCulture) + ":" + n.ToString(CultureInfo.InvariantCulture);
            return n >= AttemptsPerVisit ? "You have tried that as far as you can for now. Come back another time, or try another way." : null;
        }

        public static void Failed(PlaceData p, string target, string what)
        {
            var counter = "attempts:" + target + ":" + what + ":" + p.Visits.ToString(CultureInfo.InvariantCulture);
            p.Variables.TryGetValue(counter, out var v);
            p.Variables[counter] = ((int.TryParse(v, out var x) ? x : 0) + 1).ToString(CultureInfo.InvariantCulture);
        }

        // ----- Harm outside a fight -------------------------------------------------------------

        /// <summary>
        /// Damage from a trap or hazard: rolled with the world's dice, halved on a successful save, taken from the sheet's hit
        /// points. Outside a fight it never takes the last hit point (ADR-0064: fair challenges, no instant death).
        /// </summary>
        public static int Hurt(WorldData w, Dice dice, bool halved, string key)
        {
            if (dice.IsEmpty || w.Player.Rpg == null) return 0;
            var die = SpellCommandHandlers.Dice(w, "place-harm:" + key);
            var total = dice.Bonus;
            for (var i = 0; i < dice.Count; i++) total += die(dice.Sides);
            if (halved) total /= 2;
            var sheet = w.Player.Rpg;
            var before = sheet.CurrentHp;
            sheet.CurrentHp = Math.Max(Math.Min(1, before), before - Math.Max(0, total));
            return before - sheet.CurrentHp;
        }

        // ----- Arriving in a room ---------------------------------------------------------------

        /// <summary>
        /// Everything that happens on stepping into a room: it is explored; the player notices what their passive Perception
        /// finds; a hazard is met (once a visit); trespass is seen; inhabitants who fight on sight start a fight.
        /// Returns what the player learns, in words.
        /// </summary>
        public static string Arrive(CommandContext context, WorldData w, PlaceData place, PlaceRoom room, string via)
        {
            var p = w.Player;
            var lines = new List<string>();
            var report = QuestRuntime.Reporter(context);
            p.PlaceRoom = room.Id;
            p.PlaceFloor = room.Floor;
            var first = !place.Explored(room.Id);
            if (first)
            {
                place.ExploredRooms.Add(room.Id);
                context.Quest(new QuestEvent { Kind = QuestEventKind.RoomExplored, Subject = place.PlaceId, Detail = room.Id });
            }

            context.Quest(new QuestEvent { Kind = QuestEventKind.FloorReached, Subject = place.PlaceId, Detail = room.Floor.ToString(CultureInfo.InvariantCulture) });

            // What a watchful eye catches on entering: traps and hidden things in the room, secret ways out of it.
            var passive = PassivePerception(w, room);
            foreach (var f in place.Layout.Features)
            {
                if (f.Room != room.Id || !f.Hidden || place.Has(f.Id, FeatureFlags.Found)) continue;
                if (passive >= f.FindDc)
                {
                    var s = place.StateOf(f.Id);
                    s.Flags |= FeatureFlags.Found;
                    s.ChangedAt = QuestClock.Now(w);
                    lines.Add(f.Kind == FeatureKind.Trap ? "You notice something: " + (TrapCatalog.Find(f.Get("trap"))?.Seen ?? "a trap.") : "Something catches your eye: " + Name(f) + ".");
                }
            }

            foreach (var c in place.Layout.Connections)
                if (c.Kind == ConnectionKind.Secret && c.Joins(room.Id) && !place.Found(c) && passive >= c.FindDc)
                {
                    Find(context, place, c);
                    lines.Add("A draught where there should be none: there is a hidden way here.");
                }

            // The room's hazard, once a visit.
            var hazard = HazardCatalog.Find(room.Hazard);
            if (hazard != null)
            {
                var met = "hazard:" + room.Id;
                var visit = place.Visits.ToString(CultureInfo.InvariantCulture);
                if (!place.Variables.TryGetValue(met, out var last) || last != visit)
                {
                    place.Variables[met] = visit;
                    lines.Add(Meet(w, place, room, hazard));
                }
            }

            // Trespass: in a restricted room, or anywhere inside without leave, in sight of a garrison that has not let the player pass.
            var trespass = Trespass(w, place, room, via);
            if (trespass != null)
            {
                lines.Add(trespass.Item2);
                var group = trespass.Item1;
                if (!group.FactionId.IsEmpty || !place.FactionId.IsEmpty)
                    Reputation.Change(w, "faction:" + (group.FactionId.IsEmpty ? place.FactionId : group.FactionId), "Standing", -10,
                        "trespass:" + place.PlaceId + ":" + room.Id + ":" + place.Visits.ToString(CultureInfo.InvariantCulture), "caught trespassing in " + place.Name, report);
                context.ReportEvent(HistoricalEventType.Trespass, EventSeverity.Warning, EventImportance.Minor, PlaceKnowledge.Settlements(place),
                    new Dictionary<string, string> { ["place"] = place.Name, ["room"] = room.Name, ["by"] = p.Name });
                var r = PlaceEncounters.Start(context, w, place, new List<PlaceGroup> { group }, room, "Caught trespassing — ");
                if (!r.IsSuccess) lines.Add(r.Message);
                return string.Join(" ", lines);
            }

            // Inhabitants who fight on sight (a den, an ambush). Garrisons that have let the player in stay calm.
            var hostile = new List<PlaceGroup>();
            foreach (var g in place.Layout.Groups)
            {
                if (g.Room != room.Id || g.Behaviour == GroupBehaviour.Guard || g.WakesOn != null) continue;
                var s = place.GroupStateOf(g.Id);
                if (s == GroupState.Present || s == GroupState.Survived) hostile.Add(g);
            }

            if (hostile.Count > 0 && w.ActiveEncounter == null)
            {
                var r = PlaceEncounters.Start(context, w, place, hostile, room, null);
                lines.Add(r.IsSuccess ? r.Message : r.Message);
            }

            UpdateDiscovery(context, w, place);
            return string.Join(" ", lines);
        }

        private static string Meet(WorldData w, PlaceData place, PlaceRoom room, HazardDefinition h)
        {
            var key = place.PlaceId + ":" + room.Id + ":" + place.Visits.ToString(CultureInfo.InvariantCulture);
            if (h.Dark) return h.Describe + (Dark(w, room) ? " Without a light you will miss things here." : string.Empty);
            if (h.SwimDc > 0)
            {
                var swim = Check(w, Skill.Athletics, h.SwimDc, "swim:" + key);
                if (swim.Success) return h.Describe + " You swim across (" + swim.Text + ").";
                var lost = Hurt(w, h.Damage, false, "swim:" + key);
                place.Note(w.Player.Name + " floundered in the water in " + room.Name + ".");
                return h.Describe + $" The cold takes your breath; you drag yourself across ({swim.Text}): {lost} damage.";
            }

            if (h.Save.HasValue)
            {
                var save = Save(w, h.Save.Value, h.SaveDc, "hazard:" + key);
                var lost = Hurt(w, h.Damage, save.Success, "hazard:" + key);
                var cond = !save.Success && h.Condition != null ? $" You are left {h.Condition.ToLowerInvariant()} for a while." : string.Empty;
                return $"{h.Describe} {save.Text}: {lost} {h.DamageType.ToString().ToLowerInvariant()} damage.{cond}";
            }

            return h.Describe;
        }

        /// <summary>The group that catches the player trespassing on entering a room, with what happens, or null.</summary>
        public static Tuple<PlaceGroup, string> Trespass(WorldData w, PlaceData place, PlaceRoom room, string via)
        {
            if (place.Layout.Entrance?.A == room.Id && !room.Restricted) return null; // the way in is public ground
            var leave = HasLeave(place);
            if (!room.Restricted && (leave || place.FactionId.IsEmpty)) return null;
            foreach (var g in place.Layout.Groups)
            {
                if (g.Behaviour != GroupBehaviour.Guard) continue;
                var s = place.GroupStateOf(g.Id);
                if (s == GroupState.Defeated || s == GroupState.Fighting) continue;
                if (g.Room != room.Id && !InSight(place, g.Room, room.Id)) continue;
                var why = room.Restricted ? $"{Capital(g.Name)} see you in the {room.Name.ToLowerInvariant()}, where you have no business." : $"{Capital(g.Name)} see a stranger inside the walls.";
                return Tuple.Create(g, why);
            }

            return null;
        }

        /// <summary>True when two rooms see each other: joined by an opening, or by a door standing open.</summary>
        public static bool InSight(PlaceData p, string a, string b)
        {
            foreach (var c in p.Layout.Connections)
            {
                if (c.IsVertical || c.Kind == ConnectionKind.Entrance) continue;
                if (!((c.A == a && c.B == b) || (c.A == b && c.B == a))) continue;
                if (c.Kind == ConnectionKind.Opening) return true;
                var s = p.DoorOf(c);
                if (s == DoorState.Open || s == DoorState.Broken) return true;
            }

            return false;
        }

        public static void Find(CommandContext context, PlaceData place, PlaceConnection c)
        {
            if (place.FoundConnections.Contains(c.Id)) return;
            place.FoundConnections.Add(c.Id);
            place.Note("A hidden way was found in " + place.Name + ".");
            context.ReportEvent(HistoricalEventType.PlaceSecretFound, EventSeverity.Positive, EventImportance.Minor, PlaceKnowledge.Settlements(place),
                new Dictionary<string, string> { ["place"] = place.Name });
        }

        /// <summary>Explored (every room that is not secret seen) and Cleared (no hostile inhabitants left).</summary>
        public static void UpdateDiscovery(CommandContext context, WorldData w, PlaceData place)
        {
            var report = QuestRuntime.Reporter(context);
            var all = true;
            foreach (var r in place.Layout.Rooms) if (!r.Secret && !place.Explored(r.Id)) all = false;
            if (all) PlaceKnowledge.Raise(w, place, PlaceDiscovery.Explored, report, "explored");
            place.Threat = PlaceThreat.Of(place);
            if (PlaceThreat.Cleared(place) && place.Discovery < PlaceDiscovery.Cleared)
            {
                place.Discovery = PlaceDiscovery.Cleared;
                place.Note(place.Name + " was cleared.");
                context.ReportEvent(HistoricalEventType.PlaceCleared, EventSeverity.Positive, EventImportance.Minor, PlaceKnowledge.Settlements(place),
                    new Dictionary<string, string> { ["place"] = place.Name, ["by"] = w.Player.Name });
            }
        }

        // ----- Escorts ---------------------------------------------------------------------------

        /// <summary>People following the player home: those whose destination is this settlement arrive and go back to their lives.</summary>
        public static List<string> Arrive(CommandContext context, WorldData w, SettlementId settlement)
        {
            var arrived = new List<string>();
            foreach (var e in w.Escorts)
            {
                if (e.State != EscortState.Following || e.Destination != settlement) continue;
                var npc = w.FindNpc(e.NpcId);
                if (npc == null || npc.IsDeceased) continue;
                e.State = EscortState.Arrived;
                e.ChangedAt = QuestClock.Now(w);
                arrived.Add(npc.Name);
                npc.CurrentLocationId = npc.ResidenceId;
                context.Quest(new QuestEvent { Kind = QuestEventKind.Escorted, Subject = npc.NpcId.ToString(), Settlement = settlement });
                context.ReportEvent(HistoricalEventType.EscortArrived, EventSeverity.Positive, EventImportance.Minor, new[] { settlement },
                    new Dictionary<string, string> { ["npc"] = npc.Name, ["by"] = w.Player.Name }, npcIds: new[] { npc.NpcId });
            }

            return arrived;
        }

        public static string Name(PlaceFeature f)
        {
            if (f.Kind == FeatureKind.Trap) return TrapCatalog.Find(f.Get("trap"))?.Name ?? "Trap";
            return FeatureCatalog.Find(f.DefinitionId)?.Name ?? f.DefinitionId;
        }

        public static string Capital(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
