using System;
using System.Collections.Generic;
using System.Linq;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>Phase 26.1: what a mark on the local HUD map stands for. Each has its own shape and colour.</summary>
    public enum LocalMarkKind
    {
        Companion = 0,
        Person = 1,
        Merchant = 2,
        Shop = 3,
        Service = 4,
        Building = 5,
        Gate = 6,
        Settlement = 7,
        Landmark = 8,
        Place = 9,
        Waypoint = 10,
        Hostile = 11,
        Traveller = 12,
    }

    /// <summary>
    /// Phase 26.1: one mark on the local HUD map. It is a view of something the game already holds (a resident, a shop, a
    /// found landmark, a known site), at that thing's own position; the map keeps no record of its own.
    /// </summary>
    public sealed class LocalMapMark
    {
        public string Key { get; set; }
        public LocalMarkKind Kind { get; set; }

        /// <summary>The label as the player may know it (a name only when known; otherwise what anyone can see).</summary>
        public string Label { get; set; }

        /// <summary>World position in metres (x east, z north).</summary>
        public float X { get; set; }

        public float Z { get; set; }

        /// <summary>Kept at the map's edge when out of range, as a direction (settlements, the waypoint, gates).</summary>
        public bool PinAtEdge { get; set; }
    }

    /// <summary>
    /// Phase 26.1 (ADR-0118): the local HUD map's geometry. Heading-up: the player is at the centre, facing the top, and
    /// the world turns beneath. Headings are Unity yaw in degrees: 0 = north (+z), 90 = east (+x). Screen offsets are in
    /// pixels from the map's centre, x to the right and y downward (UI Toolkit's axes).
    /// </summary>
    public static class LocalMapGeometry
    {
        /// <summary>Where a world point falls on the map, from the player's position and heading.</summary>
        public static void Project(float playerX, float playerZ, float headingDegrees, float worldX, float worldZ, float pixelsPerMetre, out float x, out float y)
        {
            var dx = worldX - playerX;
            var dz = worldZ - playerZ;
            var h = headingDegrees * (Math.PI / 180.0);
            var sin = (float)Math.Sin(h);
            var cos = (float)Math.Cos(h);
            var right = dx * cos - dz * sin; // along the player's right hand: (cos h, −sin h)
            var forward = dx * sin + dz * cos; // along the heading: (sin h, cos h)
            x = right * pixelsPerMetre;
            y = -forward * pixelsPerMetre;
        }

        /// <summary>Inside the square map, keeping <paramref name="margin"/> pixels from its edge.</summary>
        public static bool Inside(float x, float y, float half, float margin) => Math.Abs(x) <= half - margin && Math.Abs(y) <= half - margin;

        /// <summary>A point beyond the edge brought back onto it along the same direction (for edge pins).</summary>
        public static void ClampToEdge(ref float x, ref float y, float half, float margin)
        {
            var limit = half - margin;
            var m = Math.Max(Math.Abs(x), Math.Abs(y));
            if (m <= limit || m <= 0f) return;
            var k = limit / m;
            x *= k;
            y *= k;
        }

        /// <summary>The shortest signed turn from one heading to another, in degrees (−180 to 180).</summary>
        public static float DeltaAngle(float from, float to)
        {
            var d = (to - from) % 360f;
            if (d > 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }

        /// <summary>
        /// Turns the shown heading toward the true one: fast enough not to lag (about 90 % in a tenth of a second), smooth
        /// enough not to jitter. With reduced motion (or a long frame) it is exact.
        /// </summary>
        public static float SmoothHeading(float shown, float target, float deltaSeconds, bool reducedMotion)
        {
            if (reducedMotion || deltaSeconds <= 0f || deltaSeconds > 0.25f) return Normalise(target);
            var d = DeltaAngle(shown, target);
            if (Math.Abs(d) < 0.05f || Math.Abs(d) > 120f) return Normalise(target);
            var k = 1f - (float)Math.Exp(-deltaSeconds * 24f);
            return Normalise(shown + d * k);
        }

        public static float Normalise(float degrees)
        {
            var d = degrees % 360f;
            return d < 0f ? d + 360f : d;
        }

        /// <summary>A label's estimated width in pixels at a font size (proportional sans serif, a little generous).</summary>
        public static float LabelWidth(string text, float fontPixels) => string.IsNullOrEmpty(text) ? 0f : text.Length * fontPixels * 0.56f + 8f;

        /// <summary>A label shortened to <paramref name="maxChars"/>, at a word if possible, with an ellipsis.</summary>
        public static string Shorten(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxChars || maxChars < 2) return text;
            var cut = text.Substring(0, maxChars - 1);
            var space = cut.LastIndexOf(' ');
            if (text[maxChars - 1] != ' ' && space >= maxChars / 2) cut = cut.Substring(0, space); // end at a word
            return cut.TrimEnd(' ', ',', '·', '-') + "…";
        }

        /// <summary>How much a kind matters on the small map: the higher, the sooner its label is shown.</summary>
        public static int Priority(LocalMarkKind kind)
        {
            switch (kind)
            {
                case LocalMarkKind.Hostile: return 9;
                case LocalMarkKind.Waypoint: return 8;
                case LocalMarkKind.Companion: return 7;
                case LocalMarkKind.Merchant:
                case LocalMarkKind.Shop: return 6;
                case LocalMarkKind.Settlement: return 6;
                case LocalMarkKind.Landmark: return 5;
                case LocalMarkKind.Service: return 5;
                case LocalMarkKind.Gate: return 4;
                case LocalMarkKind.Place: return 4;
                case LocalMarkKind.Traveller: return 3;
                case LocalMarkKind.Building: return 2;
                default: return 1;
            }
        }

        /// <summary>
        /// Chooses which marks get a label: by priority, then nearness to the player; each label goes to the right of its
        /// mark, or to the left if the right is taken. A label is left off when it would leave the map, cover the player's
        /// mark, or overlap a label already placed, and never more than <paramref name="maxLabels"/>. The mark itself
        /// always stays, so nothing becomes unreadable: its shape and colour still say what it is.
        /// </summary>
        /// <returns>For each mark: 0 no label, 1 label to the right, −1 label to the left.</returns>
        public static int[] Declutter(IReadOnlyList<(float x, float y, float labelWidth, int priority)> marks, float half, float labelHeight, int maxLabels, int[] into = null,
            IReadOnlyList<(float l, float t, float r, float b)> keepClear = null)
        {
            var n = marks.Count;
            var result = into != null && into.Length >= n ? into : new int[n];
            for (var i = 0; i < n; i++) result[i] = 0;
            var order = new int[n];
            for (var i = 0; i < n; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                var pa = marks[a].priority;
                var pb = marks[b].priority;
                if (pa != pb) return pb.CompareTo(pa);
                var da = marks[a].x * marks[a].x + marks[a].y * marks[a].y;
                var db = marks[b].x * marks[b].x + marks[b].y * marks[b].y;
                return da.CompareTo(db);
            });

            var placed = new List<(float l, float t, float r, float b)>();
            var reserved = 0;
            if (keepClear != null) placed.AddRange(keepClear); // the map's own furniture: the range, the north letter
            reserved = placed.Count;
            const float gap = 7f; // from the mark's centre to the label
            const float player = 11f; // the player's mark, as a square half-size
            foreach (var i in order)
            {
                if (placed.Count - reserved >= maxLabels) break;
                var (x, y, w, _) = marks[i];
                if (w <= 0f) continue;
                foreach (var side in new[] { 1, -1 })
                {
                    var l = side > 0 ? x + gap : x - gap - w;
                    var r = l + w;
                    var t = y - labelHeight / 2f;
                    var b = y + labelHeight / 2f;
                    if (l < -half + 2f || r > half - 2f || t < -half + 2f || b > half - 2f) continue;
                    if (l < player && r > -player && t < player && b > -player) continue;
                    var clash = false;
                    foreach (var p in placed)
                        if (l < p.r && r > p.l && t < p.b && b > p.t) { clash = true; break; }
                    if (clash) continue;
                    placed.Add((l, t, r, b));
                    result[i] = side;
                    break;
                }
            }

            return result;
        }
    }

    /// <summary>Phase 26.1: what the local HUD map may say about a person (known people by name, others by what they do).</summary>
    public sealed partial class WorldSession
    {
        /// <summary>
        /// The label for a person on the local map. A name only once the player has spoken with them (the codex's rule);
        /// before that, what anyone can see: their trade ("Smith", "Guard"). <paramref name="isMerchant"/> is true for a
        /// shopkeeper.
        /// </summary>
        public string MapLabelFor(NpcId id, out bool known, out bool isMerchant)
        {
            known = false;
            isMerchant = false;
            var w = _world;
            var n = w?.Npcs.FirstOrDefault(x => x.NpcId == id);
            if (n == null) return "Someone";
            isMerchant = w.Shops.Any(s => s.ShopkeeperNpcId == id);
            known = KnownPersonQueries.AcquaintanceOf(w, n) != Acquaintance.Seen;
            return known ? n.Name : RoleWord(n.Role, isMerchant);
        }

        public static string RoleWord(NpcRole role, bool merchant)
        {
            if (merchant) return "Merchant";
            switch (role)
            {
                case NpcRole.Elder: return "Elder";
                case NpcRole.Merchant: return "Merchant";
                case NpcRole.Farmer: return "Farmer";
                case NpcRole.Guard: return "Guard";
                case NpcRole.Smith: return "Smith";
                case NpcRole.Innkeeper: return "Innkeeper";
                case NpcRole.Labourer: return "Labourer";
                default: return "Resident";
            }
        }
    }
}
