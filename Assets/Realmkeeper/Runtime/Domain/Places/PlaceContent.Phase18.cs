using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.Rpg;

namespace Realmkeeper.Domain.Places
{
    /// <summary>
    /// Phase 18 (ADR-0089): four more places, built from the existing archetypes (no new kinds), each with a reason to
    /// be as it is and a working puzzle at its heart:
    /// <list type="bullet">
    /// <item>Greywater Drift (a mine): a flooded seam and the pump that drains it, in stages, if it is worked as its drill board says.</item>
    /// <item>The Ringers' Vault (a crypt): three bells rung in the order a painted verse gives; the wrong order costs blood.</item>
    /// <item>Hollin Tower (a ruin): a lintel whose scoring shows only by a flame held close, pointing to a hidden room.</item>
    /// <item>Cairn Watch (a keep): a watchtower two settlements once kept together, and the evidence of who stopped.</item>
    /// </list>
    /// Every text is Realmkeeper's own; the histories are local and fix no campaign canon.
    /// </summary>
    public static class Phase18Places
    {
        public const string Drift = "greywater_drift";
        public const string Vault = "ringers_vault";
        public const string Hollin = "hollin_tower";
        public const string Cairn = "cairn_watch";

        public static readonly string[] All = { Drift, Vault, Hollin, Cairn };

        // Mechanism data keys (on PlaceFeature.Data).
        public const string Mech = "mech";
        public const string MechSequence = "sequence";
        public const string MechIntake = "intake";
        public const string MechPump = "pump";
        public const string MechLight = "light";

        /// <summary>The bells in the order the verse gives.</summary>
        public static readonly string[] BellOrder = { "bell_ferry", "bell_mill", "bell_forge" };

        public static IEnumerable<FeatureDefinition> Features()
        {
            FeatureDefinition F(string id, string name, FeatureKind kind, string look, Skill? skill = null, int dc = 0, string insight = null, string discovery = null, string missed = null) =>
                new FeatureDefinition
                {
                    Id = id, Name = name, Kind = kind, Look = look, Study = skill.HasValue ? new FeatureCheck { Skill = skill.Value, Dc = dc } : null, Insight = insight,
                    Missed = missed ?? "Nothing more comes of it — not yet, anyway.", Discovery = discovery,
                };

            // Greywater Drift: the silver below the water.
            yield return F("pump_drill", "Pump drill", FeatureKind.Document, "A board nailed up by the shaft head, the paint gone grey with damp.", insight:
                "\"PUMP DRILL. One: clear the intake grate. Two: work the handle, a turn of the glass at a time, till the gauge reads DRY. Never work the pump with the grate choked — the barrel will crack.\"",
                discovery: "drift_pump_drill");
            yield return F("intake_grate", "Intake grate", FeatureKind.Mechanism, "An iron grate over the pump's intake, choked with silt and splintered prop timber.");
            yield return F("drift_pump", "Drainage pump", FeatureKind.Mechanism, "A cast-iron pump with a long handle and a brass gauge. The needle sits hard against FULL.", Skill.Investigation, 10,
                "A good pump, well made. The barrel is sound — for now. The gauge has three marks: FULL, HALF and DRY.", "drift_pump_gauge");
            yield return F("silver_vein", "Silver seam", FeatureKind.Clue, "A seam of grey rock glittering where the last picks struck.", Skill.Nature, 11,
                "Silver, and a rich seam — better than anything worked above. This is why they kept digging after the water came.", "drift_silver");
            yield return F("drift_tally", "Shift tally", FeatureKind.Document, "A slate of names and marks hung on a nail.", insight:
                "The last shift's marks: \"Water at the third rung. Grate choked again. Owner says dig on.\" Nine names went down. Nobody chalked them out.", discovery: "drift_last_shift");
            yield return F("drowned_strongbox", "Miners' strongbox", FeatureKind.Container, "An iron box wedged on a ledge, its lid rusted half shut.");

            // The Ringers' Vault: three trades, three bells.
            yield return F("bell_ferry", "Ferry bell", FeatureKind.Mechanism, "A small bronze bell on an iron bracket. A ferryman's pole is cast into its shoulder.", Skill.History, 10,
                "The oldest casting of the three: the crossing was here before anything else.");
            yield return F("bell_mill", "Mill bell", FeatureKind.Mechanism, "A bronze bell on an iron bracket, a millwheel cast into its shoulder.", Skill.History, 10,
                "Younger than the ferry bell. The mill came after the crossing, as the fields came after the ford.");
            yield return F("bell_forge", "Forge bell", FeatureKind.Mechanism, "A bronze bell on an iron bracket, hammer and tongs cast into its shoulder.", Skill.History, 10,
                "The youngest casting. Its clapper is iron, not bronze — a smith's own work.");
            yield return F("ringers_verse", "Ringers' verse", FeatureKind.Document, "Lines painted on the wall above the bells, flaking but legible in a good light.", insight:
                "\"Ring first for the water that carried us over, then for the wheel that fed us after, last for the fire that made our tools — and the ringers' door opens to the true.\"",
                discovery: "ringers_order");
            yield return F("ringers_tomb", "Ringers' tomb", FeatureKind.Container, "A broad stone chest carved with three bells, the lid lettered with names worn past reading.");

            // Hollin Tower: what the reeve hid.
            yield return F("scored_lintel", "Scored lintel", FeatureKind.Clue, "Over a bricked alcove, a lintel scored with fine lines too faint to make out.", insight:
                "Held close, the flame throws the scoring into relief: a reeve's staff, and below it an arrow pointing down — \"beneath the reeve's step\". The floor stone under the alcove rings hollow.",
                discovery: "hollin_reeves_step");
            yield return F("ash_hearth", "Ash in the hearth", FeatureKind.Clue, "A cold hearth, heaped with grey ash.", Skill.Investigation, 12,
                "Paper ash, a fortnight old at most, and the corner of a ledger page that did not burn. Someone burned a copy here — and nobody burns a copy unless the original is hidden somewhere.",
                "hollin_burned_copy");
            yield return F("fresh_bootprints", "Boot prints", FeatureKind.Clue, "Prints in the dust of the stair.", Skill.Survival, 11,
                "One pair of good boots, coming and going, more than once. Someone visits this ruin — and wants nobody to know.", "hollin_visitor");
            yield return F("reeves_strongbox", "Reeve's strongbox", FeatureKind.Container, "A strongbox in a hollow under the floor, its hasp new.");

            // Cairn Watch: two settlements' tower.
            yield return F("boundary_stone", "Boundary stone", FeatureKind.Clue, "A worn stone in the yard, cut with two marks: one old and shallow, one newer and deeper.", Skill.History, 11,
                "The older mark is the line both settlements once agreed; the newer was cut over it. Whoever holds the watch moved the line — or put it back.", "cairn_boundary");
            yield return F("watch_log", "Watch log", FeatureKind.Document, "A thick log-book on the watch-room table.", insight:
                "Years of entries in two hands. The watch was built by both settlements and kept by turns, until a hard winter when one side stopped sending its people. The log never says which.",
                discovery: "cairn_watch_log");
            yield return F("torn_banner", "Folded banner", FeatureKind.Clue, "A banner, torn from its pole and folded away in a chest.", Skill.History, 9,
                "The other claimant's colours, kept with care rather than burned. They held this place once, and someone here still respects it.", "cairn_two_banners");

            // Bramble Cave and the Broken Hall gain a story (new worlds).
            yield return F("hunters_cache", "Hunter's cache", FeatureKind.Container, "A dry hollow under a rock shelf, lined with birch bark.");
            yield return F("bear_wallow", "Wallow", FeatureKind.Clue, "Churned earth matted with coarse dark hair.", Skill.Nature, 10,
                "A bear's day-bed, used every day. It leaves at dusk and comes back before dawn — down towards the road.", "cave_bear_habits");
            yield return F("hall_hearthstone", "Hearthstone", FeatureKind.Clue, "A great hearthstone carved with a running pattern of oak leaves.", Skill.History, 12,
                "A hall-keeper's hearth: the oak leaves are a household's mark, the kind cut when a hall was given to a family to keep in trust.", "hall_kept_in_trust");
            yield return F("roof_survey", "Mason's survey", FeatureKind.Document, "A slate of figures and a sketch of the roof beams.", insight:
                "\"North beams rotten through. Do not stand under the gallery.\" The mason's warning is older than the collapse that proved it right.", discovery: "hall_roof_warning");
        }

        /// <summary>What each Phase 18 feature's insight is (ADR-0088), which matter, and what confirms the guesses.</summary>
        public static IEnumerable<(string Id, DiscoveryCategory Category, bool Major, string ConfirmedBy, string Reveals)> Categories()
        {
            yield return ("pump_drill", DiscoveryCategory.Geographic, true, null, null);
            yield return ("drift_pump", DiscoveryCategory.Geographic, false, null, null);
            yield return ("silver_vein", DiscoveryCategory.Economic, true, null, null);
            yield return ("drift_tally", DiscoveryCategory.Historical, true, null, null);
            yield return ("bell_ferry", DiscoveryCategory.Historical, false, null, null);
            yield return ("bell_mill", DiscoveryCategory.Historical, false, null, null);
            yield return ("bell_forge", DiscoveryCategory.Historical, false, null, null);
            yield return ("ringers_verse", DiscoveryCategory.Cultural, true, null, null);
            yield return ("scored_lintel", DiscoveryCategory.Arcane, true, null, null);
            yield return ("ash_hearth", DiscoveryCategory.Personal, true, "hollin_reeves_step", null);
            yield return ("fresh_bootprints", DiscoveryCategory.Dangerous, false, null, null);
            yield return ("boundary_stone", DiscoveryCategory.Historical, true, "cairn_watch_log", null);
            yield return ("watch_log", DiscoveryCategory.Faction, true, null, null);
            yield return ("torn_banner", DiscoveryCategory.Faction, false, null, null);
            yield return ("bear_wallow", DiscoveryCategory.Natural, false, null, null);
            yield return ("hall_hearthstone", DiscoveryCategory.Historical, true, null, null);
            yield return ("roof_survey", DiscoveryCategory.Dangerous, false, null, null);
        }

        public static IEnumerable<PlaceDefinition> Definitions()
        {
            yield return new PlaceDefinition
            {
                Id = Drift, Name = "Greywater Drift", ArchetypeId = "mine",
                Description = "A silver drift in the hills, worked until the water rose and nine miners did not come up. The pump that kept it dry still stands at the shaft head.",
                Story =
                {
                    ("pump_drill", RoomKind.Gallery, true),
                    ("drift_pump", RoomKind.Gallery, true),
                    ("intake_grate", RoomKind.Gallery, true),
                    ("drift_tally", RoomKind.Storage, false),
                    ("silver_vein", RoomKind.Den, false),
                    ("drowned_strongbox", RoomKind.Den, false),
                },
                StoryLoot = { ["drowned_strongbox"] = "miners_stores" },
                Finish = FinishDrift,
            };
            yield return new PlaceDefinition
            {
                Id = Vault, Name = "The Ringers' Vault", ArchetypeId = "crypt",
                Description = "A burial vault where the first ringers of the old crossing were laid down, with the bells of their three trades still hung at its heart.",
                Story =
                {
                    ("ringers_verse", RoomKind.Shrine, true),
                    ("bell_ferry", RoomKind.Shrine, true),
                    ("bell_mill", RoomKind.Shrine, true),
                    ("bell_forge", RoomKind.Shrine, true),
                    ("ringers_tomb", RoomKind.Tomb, false),
                },
                StoryLoot = { ["ringers_tomb"] = "tomb_offerings" },
                StoryItem = { ["ringers_tomb"] = "old_charter" },
                Waking = { ("ringers_tomb", "the ringers' dead", new[] { "skeleton" }, 2) },
                Finish = FinishVault,
            };
            yield return new PlaceDefinition
            {
                Id = Hollin, Name = "Hollin Tower", ArchetypeId = "ruin",
                Description = "The shell of a reeve's tower on a low rise, abandoned since the reeves' court moved to the town. Lately there are fresh tracks on the stair.",
                Story =
                {
                    ("scored_lintel", RoomKind.Hall, true),
                    ("ash_hearth", RoomKind.Hall, false),
                    ("fresh_bootprints", RoomKind.Chamber, false),
                    ("reeves_strongbox", RoomKind.Chamber, false),
                },
                StoryLoot = { ["reeves_strongbox"] = "old_cache" },
                StoryItem = { ["reeves_strongbox"] = "true_ledger" },
                Finish = FinishHollin,
            };
            yield return new PlaceDefinition
            {
                Id = Cairn, Name = "Cairn Watch", ArchetypeId = "keep",
                Description = "A stone watchtower on a cairned rise between two settlements' lands, built by both and now held by one.",
                Story =
                {
                    ("boundary_stone", RoomKind.Yard, false),
                    ("watch_log", RoomKind.Quarters, true),
                    ("torn_banner", RoomKind.Storage, false),
                },
            };
        }

        // ----- Finishing (deterministic; returns false when this layout cannot carry the puzzle) -----

        /// <summary>The flooded seam: the room of the silver lies under water; the pump, its grate and the drill board are on dry ground.</summary>
        private static bool FinishDrift(PlaceLayout l)
        {
            var silver = l.Feature("story:silver_vein");
            var pump = l.Feature("story:drift_pump");
            var grate = l.Feature("story:intake_grate");
            if (silver == null || pump == null || grate == null) return false;
            var flooded = l.Room(silver.Room);
            if (flooded == null || flooded.Secret) return false;
            var entrance = l.Connections.Find(c => c.Kind == ConnectionKind.Entrance);
            if (entrance == null || entrance.A == flooded.Id) return false;
            foreach (var id in new[] { "story:drift_pump", "story:intake_grate", "story:pump_drill" })
            {
                var f = l.Feature(id);
                if (f != null && f.Room == flooded.Id && !MoveToward(l, f, entrance.A)) return false;
            }

            flooded.Hazard = "deep_water";
            flooded.Tags.Add("flooded");
            pump.Data[Mech] = MechPump;
            grate.Data[Mech] = MechIntake;
            return true;
        }

        /// <summary>The bells and the verse on the near side; the tomb behind a door the bells open (and that can still be picked or forced, hard).</summary>
        private static bool FinishVault(PlaceLayout l)
        {
            var tomb = l.Feature("story:ringers_tomb");
            var entrance = l.Connections.Find(c => c.Kind == ConnectionKind.Entrance);
            if (tomb == null || entrance == null || tomb.Room == entrance.A) return false;
            var seal = Separating(l, entrance.A, tomb.Room);
            if (seal == null) return false;
            var near = Reach(l, entrance.A, seal);
            foreach (var id in new[] { "story:ringers_verse", "story:bell_ferry", "story:bell_mill", "story:bell_forge" })
            {
                var f = l.Feature(id);
                if (f == null) return false;
                if (!near.Contains(f.Room) && !MoveInto(l, f, Deepest(l, near, entrance.A))) return false;
            }

            if (seal.Kind == ConnectionKind.Opening) seal.Kind = ConnectionKind.Door;
            seal.Door = DoorState.Locked;
            seal.KeyItem = null;
            seal.LockDc = 22; // the ringers' door: the bells open it; picking or forcing it is very hard
            seal.GuardedBy = null;
            for (var i = 0; i < BellOrder.Length; i++)
            {
                var bell = l.Feature("story:" + BellOrder[i]);
                bell.Data[Mech] = MechSequence;
                bell.Data["seq"] = "bells";
                bell.Data["order"] = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                bell.Data["opens"] = seal.Id;
            }

            return true;
        }

        /// <summary>The lintel beside the hidden way; the strongbox in the hidden room beyond it.</summary>
        private static bool FinishHollin(PlaceLayout l)
        {
            var secret = l.Connections.Find(c => c.Kind == ConnectionKind.Secret);
            var lintel = l.Feature("story:scored_lintel");
            var box = l.Feature("story:reeves_strongbox");
            if (secret == null || lintel == null || box == null) return false;
            var a = l.Room(secret.A);
            var b = l.Room(secret.B);
            if (a == null || b == null) return false;
            var hidden = b.Secret ? b : a.Secret ? a : null;
            var open = hidden == b ? a : b;
            if (hidden == null || open.Secret) return false;
            if (lintel.Room != open.Id && !MoveInto(l, lintel, open)) return false;
            if (box.Room != hidden.Id && !MoveInto(l, box, hidden)) return false;
            lintel.Data[Mech] = MechLight;
            lintel.Data["reveals"] = secret.Id;
            return true;
        }

        // ----- Layout helpers -----

        /// <summary>Rooms reached from a room without passing a given connection or any secret (doors count as passable).</summary>
        public static HashSet<string> Reach(PlaceLayout l, string from, PlaceConnection without)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { from };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                foreach (var c in l.Connections)
                {
                    if (c == without || c.Kind == ConnectionKind.Secret || c.Kind == ConnectionKind.Entrance || !c.Joins(r)) continue;
                    var o = c.Other(r);
                    if (o != null && seen.Add(o)) queue.Enqueue(o);
                }
            }

            return seen;
        }

        /// <summary>The level passage nearest the target that, closed, cuts it off from the entrance — leaving more than the entrance room on the near side.</summary>
        private static PlaceConnection Separating(PlaceLayout l, string entranceRoom, string target)
        {
            PlaceConnection best = null;
            var bestNear = -1;
            foreach (var c in l.Connections)
            {
                if (c.IsVertical || c.Kind == ConnectionKind.Entrance || c.Kind == ConnectionKind.Secret || c.Kind == ConnectionKind.Bridge || c.KeyItem != null) continue;
                var near = Reach(l, entranceRoom, c);
                if (near.Contains(target) || near.Count < 2) continue;
                if (near.Count > bestNear || (near.Count == bestNear && string.CompareOrdinal(c.Id, best.Id) < 0))
                {
                    best = c;
                    bestNear = near.Count;
                }
            }

            return best;
        }

        private static PlaceRoom Deepest(PlaceLayout l, HashSet<string> rooms, string entranceRoom)
        {
            PlaceRoom best = null;
            var bestDepth = -1;
            var depth = new Dictionary<string, int>(StringComparer.Ordinal) { [entranceRoom] = 0 };
            var queue = new Queue<string>();
            queue.Enqueue(entranceRoom);
            while (queue.Count > 0)
            {
                var r = queue.Dequeue();
                foreach (var c in l.Connections)
                {
                    if (c.Kind == ConnectionKind.Secret || c.Kind == ConnectionKind.Entrance || !c.Joins(r)) continue;
                    var o = c.Other(r);
                    if (o == null || depth.ContainsKey(o) || !rooms.Contains(o)) continue;
                    depth[o] = depth[r] + 1;
                    queue.Enqueue(o);
                }
            }

            foreach (var pair in depth)
            {
                var room = l.Room(pair.Key);
                if (room == null || room.Secret) continue;
                if (pair.Value > bestDepth || (pair.Value == bestDepth && string.CompareOrdinal(room.Id, best.Id) < 0))
                {
                    best = room;
                    bestDepth = pair.Value;
                }
            }

            return best;
        }

        /// <summary>Moves a feature one room nearer the entrance (to a neighbouring room on the way out).</summary>
        private static bool MoveToward(PlaceLayout l, PlaceFeature f, string entranceRoom)
        {
            var path = Reach(l, entranceRoom, null);
            foreach (var c in l.Connections)
            {
                if (c.Kind == ConnectionKind.Secret || c.Kind == ConnectionKind.Entrance || !c.Joins(f.Room)) continue;
                var o = l.Room(c.Other(f.Room));
                if (o != null && !o.Secret && path.Contains(o.Id) && MoveInto(l, f, o)) return true;
            }

            return false;
        }

        /// <summary>Puts a feature on a free cell of a room (not on a wall, a way through, another feature or a creature).</summary>
        public static bool MoveInto(PlaceLayout l, PlaceFeature f, PlaceRoom room)
        {
            if (room == null) return false;
            var r = room.Rect;
            for (var x = r.X + 1; x < r.MaxX - 1; x++)
                for (var z = r.Z + 1; z < r.MaxZ - 1; z++)
                {
                    if (l.Features.Exists(o => o != f && l.Room(o.Room)?.Floor == room.Floor && o.X == x && o.Z == z)) continue;
                    var busy = false;
                    foreach (var g in l.Groups) foreach (var m in g.Members) if (l.Room(g.Room)?.Floor == room.Floor && m.X == x && m.Z == z) busy = true;
                    foreach (var c in l.Connections)
                        if (c.Rect.X - 1 <= x && x <= c.Rect.MaxX && c.Rect.Z - 1 <= z && z <= c.Rect.MaxZ && (c.Joins(room.Id) || c.A == room.Id)) busy = true;
                    if (busy) continue;
                    f.Room = room.Id;
                    f.X = x;
                    f.Z = z;
                    return true;
                }

            return false;
        }
    }
}
