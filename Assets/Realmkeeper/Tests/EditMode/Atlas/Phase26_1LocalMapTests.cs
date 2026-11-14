using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Tests.EditMode.Combat;

namespace Realmkeeper.Tests.EditMode.Atlas
{
    /// <summary>
    /// Phase 26.1 (ADR-0118): the local HUD map below the screen. Heading-up projection (the player at the centre, the
    /// heading at the top), stationary turning, diagonal and moving cases, edge pins, heading smoothing, label
    /// decluttering, and what the map may say about a person (a name only once met).
    /// </summary>
    public sealed class Phase26_1LocalMapTests : Phase16Fixture
    {
        private const float Ppm = 2f;

        private static (float x, float y) P(float px, float pz, float heading, float wx, float wz)
        {
            LocalMapGeometry.Project(px, pz, heading, wx, wz, Ppm, out var x, out var y);
            return (x, y);
        }

        private static void Near((float x, float y) got, float x, float y, string why)
        {
            Assert.That(got.x, Is.EqualTo(x).Within(1e-3), why + " (x)");
            Assert.That(got.y, Is.EqualTo(y).Within(1e-3), why + " (y)");
        }

        [Test]
        public void ThePlayerIsTheCentre_AndTheHeadingIsUp_FacingEachWay()
        {
            Near(P(100f, 200f, 0f, 100f, 200f), 0f, 0f, "the player's own place is the centre");
            // Facing north: north is up (negative y), east to the right.
            Near(P(0f, 0f, 0f, 0f, 10f), 0f, -20f, "north ahead");
            Near(P(0f, 0f, 0f, 10f, 0f), 20f, 0f, "east to the right");
            // Facing east: east is up, north to the left, south to the right.
            Near(P(0f, 0f, 90f, 10f, 0f), 0f, -20f, "east ahead");
            Near(P(0f, 0f, 90f, 0f, 10f), -20f, 0f, "north on the left");
            Near(P(0f, 0f, 90f, 0f, -10f), 20f, 0f, "south on the right");
            // Facing south: north is behind (down); facing west: north is to the right.
            Near(P(0f, 0f, 180f, 0f, 10f), 0f, 20f, "north behind");
            Near(P(0f, 0f, 270f, 0f, 10f), 20f, 0f, "north on the right, facing west");
            Near(P(0f, 0f, 270f, -10f, 0f), 0f, -20f, "west ahead");
        }

        [Test]
        public void Diagonals_AndAnyPosition_KeepTrueRelativePlaces()
        {
            // Facing north-east, a point to the north-east is straight ahead.
            var d = (float)Math.Sqrt(50.0);
            Near(P(5f, 5f, 45f, 10f, 10f), 0f, -d * Ppm, "north-east ahead when facing north-east");
            Near(P(5f, 5f, 45f, 0f, 10f), -d * Ppm, 0f, "north-west on the left when facing north-east");
            // Distances never change with the heading (the map only turns).
            var rnd = new Random(7);
            for (var i = 0; i < 200; i++)
            {
                float px = rnd.Next(-500, 500), pz = rnd.Next(-500, 500), wx = rnd.Next(-500, 500), wz = rnd.Next(-500, 500), h = rnd.Next(0, 360);
                var (x, y) = P(px, pz, h, wx, wz);
                var world = Math.Sqrt((wx - px) * (wx - px) + (wz - pz) * (wz - pz)) * Ppm;
                Assert.That(Math.Sqrt(x * x + y * y), Is.EqualTo(world).Within(1e-2), "turning keeps distance");
            }
        }

        [Test]
        public void TurningInPlace_TurnsTheMapTheOtherWay_AndWalkingMovesTheMarks()
        {
            // Standing still, turning right by 30 degrees turns every mark 30 degrees anticlockwise on screen.
            var before = P(0f, 0f, 10f, 30f, 40f);
            var after = P(0f, 0f, 40f, 30f, 40f);
            var a0 = Math.Atan2(before.y, before.x) * 180.0 / Math.PI;
            var a1 = Math.Atan2(after.y, after.x) * 180.0 / Math.PI;
            var turn = ((a1 - a0) % 360 + 540) % 360 - 180;
            Assert.That(turn, Is.EqualTo(-30.0).Within(1e-3), "screen angles grow clockwise (y down): the mark turns anticlockwise");

            // Walking forward (north, facing north) 10 m brings a mark ahead 10 m nearer, straight down the map.
            var far = P(0f, 0f, 0f, 0f, 40f);
            var near = P(0f, 10f, 0f, 0f, 40f);
            Assert.That(near.y - far.y, Is.EqualTo(10f * Ppm).Within(1e-3));
            Assert.That(near.x, Is.EqualTo(far.x).Within(1e-3));

            // Moving while turning: the result is the same as arriving and then turning.
            Near(P(3f, 4f, 123f, 50f, -20f), P(3f, 4f, 123f, 50f, -20f).x, P(3f, 4f, 123f, 50f, -20f).y, "pure function of position and heading");
        }

        [Test]
        public void Bounds_Pins_AndHeadingSmoothing()
        {
            Assert.That(LocalMapGeometry.Inside(80f, -80f, 90f, 4f), Is.True);
            Assert.That(LocalMapGeometry.Inside(88f, 0f, 90f, 4f), Is.False, "beyond the margin: not drawn (unless pinned)");
            float x = 300f, y = -150f;
            LocalMapGeometry.ClampToEdge(ref x, ref y, 90f, 6f);
            Assert.That(Math.Max(Math.Abs(x), Math.Abs(y)), Is.EqualTo(84f).Within(1e-3), "an edge pin sits on the edge");
            Assert.That(y / x, Is.EqualTo(-0.5f).Within(1e-4), "in the same direction");

            Assert.That(LocalMapGeometry.SmoothHeading(350f, 10f, 0.016f, true), Is.EqualTo(10f).Within(1e-4), "reduced motion: exact at once");
            var h = 350f;
            for (var i = 0; i < 6; i++) h = LocalMapGeometry.SmoothHeading(h, 10f, 0.016f, false);
            Assert.That(LocalMapGeometry.DeltaAngle(h, 10f), Is.InRange(0f, 10f), "turns the short way round, past north");
            for (var i = 0; i < 30; i++) h = LocalMapGeometry.SmoothHeading(h, 10f, 0.016f, false);
            Assert.That(Math.Abs(LocalMapGeometry.DeltaAngle(h, 10f)), Is.LessThan(0.06f), "and settles within half a second");
            var once = LocalMapGeometry.SmoothHeading(0f, 90f, 0.1f, false);
            Assert.That(once, Is.GreaterThan(81f), "a tenth of a second covers about 90 % of a turn: no noticeable lag");
        }

        [Test]
        public void Labels_NeverOverlap_NeverCoverThePlayer_AndTheImportantComeFirst()
        {
            var rnd = new Random(11);
            for (var trial = 0; trial < 300; trial++)
            {
                var marks = new List<(float x, float y, float labelWidth, int priority)>();
                var n = rnd.Next(1, 30);
                for (var i = 0; i < n; i++) marks.Add((rnd.Next(-85, 85), rnd.Next(-85, 85), rnd.Next(20, 110), rnd.Next(1, 10)));
                var sides = LocalMapGeometry.Declutter(marks, 90f, 16f, 5);
                var rects = new List<(float l, float t, float r, float b)>();
                for (var i = 0; i < n; i++)
                {
                    if (sides[i] == 0) continue;
                    var (x, y, w, _) = marks[i];
                    var l = sides[i] > 0 ? x + 7f : x - 7f - w;
                    var rect = (l, y - 8f, l + w, y + 8f);
                    Assert.That(rect.l >= -88f && rect.Item3 <= 88f && rect.Item2 >= -88f && rect.Item4 <= 88f, Is.True, "inside the map");
                    Assert.That(rect.l < 11f && rect.Item3 > -11f && rect.Item2 < 11f && rect.Item4 > -11f, Is.False, "off the player's mark");
                    foreach (var o in rects) Assert.That(rect.l < o.r && rect.Item3 > o.l && rect.Item2 < o.b && rect.Item4 > o.t, Is.False, "no two labels overlap");
                    rects.Add(rect);
                }

                Assert.That(rects.Count, Is.LessThanOrEqualTo(5));
            }

            // Two marks at the same spot: the hostile one is labelled, the person is not.
            var same = new List<(float, float, float, int)> { (30f, 30f, 60f, LocalMapGeometry.Priority(LocalMarkKind.Person)), (30f, 30f, 60f, LocalMapGeometry.Priority(LocalMarkKind.Hostile)) };
            var s2 = LocalMapGeometry.Declutter(same, 90f, 16f, 5);
            Assert.That(s2[1], Is.Not.EqualTo(0));
            Assert.That(s2[0] == 0 || s2[0] == -s2[1], Is.True, "the second label only where it does not collide");

            // The map's furniture (the range chip, the north letter) is kept clear, and does not count against the five.
            var clear = new List<(float, float, float, float)> { (-90f, -90f, 90f, 0f) };
            var spots = new List<(float, float, float, int)> { (-40f, -40f, 30f, 5), (-40f, 40f, 30f, 5) };
            var s3 = LocalMapGeometry.Declutter(spots, 90f, 16f, 1, null, clear);
            Assert.That(s3[0], Is.EqualTo(0), "no label in the kept-clear top half");
            Assert.That(s3[1], Is.Not.EqualTo(0), "the one allowed label still goes where it is clear");

            Assert.That(LocalMapGeometry.Shorten("The chapel on the rise above the mill", 18), Is.EqualTo("The chapel on the…"));
            Assert.That(LocalMapGeometry.Shorten("Gate", 18), Is.EqualTo("Gate"));
        }

        [Test]
        public void People_AreNamedOnlyOnceMet_OtherwiseByTheirTrade()
        {
            CombatTests.Create(S, "fighter", new[] { 15, 14, 13, 8, 10, 12 });
            var w = W;
            var stranger = w.Npcs.First(n => !n.IsDeceased && KnownPersonQueries.AcquaintanceOf(w, n) == Acquaintance.Seen && w.Shops.All(s => s.ShopkeeperNpcId != n.NpcId));
            var label = S.MapLabelFor(stranger.NpcId, out var known, out var merchant);
            Assert.That(known, Is.False);
            Assert.That(merchant, Is.False);
            Assert.That(label, Is.Not.EqualTo(stranger.Name), "a stranger's name is not shown");
            Assert.That(label, Is.EqualTo(WorldSession.RoleWord(stranger.Role, false)));

            Edit(x => x.FindNpc(stranger.NpcId).MetPlayerOn = x.CurrentSimulationDate);
            Assert.That(S.MapLabelFor(stranger.NpcId, out known, out _), Is.EqualTo(stranger.Name), "met: named");
            Assert.That(known, Is.True);

            var keeper = w.Shops.Select(s => w.FindNpc(s.ShopkeeperNpcId)).First(n => n != null && KnownPersonQueries.AcquaintanceOf(w, n) == Acquaintance.Seen);
            Assert.That(S.MapLabelFor(keeper.NpcId, out _, out merchant), Is.EqualTo("Merchant"));
            Assert.That(merchant, Is.True);
            Assert.That(S.MapLabelFor(default, out _, out _), Is.EqualTo("Someone"), "nobody: nothing revealed");
        }
    }
}
