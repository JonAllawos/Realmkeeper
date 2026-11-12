using System.Collections.Generic;
using NUnit.Framework;
using Realmkeeper.Domain.Combat;

namespace Realmkeeper.Tests.EditMode.Combat
{
    /// <summary>Phase 14 (ADR-0067): the shared line-of-sight and cover function, on hand-drawn grids.</summary>
    public sealed class GridSightTests
    {
        /// <summary>A grid drawn as text: '#' wall, 'D' closed door, 'o' low obstacle, 'c' a creature, '.' floor. Row 0 is y = 0.</summary>
        private sealed class Drawn : ISightGrid
        {
            private readonly string[] _rows;
            public Drawn(params string[] rows) { _rows = rows; }
            private char At(int x, int y) => y < 0 || y >= _rows.Length || x < 0 || x >= _rows[y].Length ? '#' : _rows[y][x];
            public bool IsWall(int x, int y) => At(x, y) == '#' || At(x, y) == 'D';
            public string WallName(int x, int y) => At(x, y) == 'D' ? "a closed door" : "a wall";
            public bool IsLowCover(int x, int y) => At(x, y) == 'o';
            public string LowCoverName(int x, int y) => "a table";
            public string CreatureAt(int x, int y) => At(x, y) == 'c' ? "Goblin" : null;
        }

        private static GridPos P(int x, int y) => new GridPos(x, y);

        [Test]
        public void AClearCorridor_GivesNoCover_BothWays()
        {
            var g = new Drawn(
                "##########",
                "..........",
                "##########");
            Assert.That(GridSight.Cover(g, P(0, 1), P(9, 1)).Level, Is.EqualTo(CoverLevel.None));
            Assert.That(GridSight.Cover(g, P(9, 1), P(0, 1)).Level, Is.EqualTo(CoverLevel.None));
        }

        [Test]
        public void AClosedDoor_IsTotalCover_AndOpeningItClearsTheLine()
        {
            var shut = new Drawn(
                "#####",
                "..D..",
                "#####");
            var open = new Drawn(
                "#####",
                ".....",
                "#####");
            var r = GridSight.Cover(shut, P(0, 1), P(4, 1));
            Assert.That(r.Level, Is.EqualTo(CoverLevel.Total));
            Assert.That(r.By, Is.EqualTo("a closed door"));
            Assert.That(GridSight.Cover(open, P(0, 1), P(4, 1)).Level, Is.EqualTo(CoverLevel.None));
        }

        [Test]
        public void ASolidWall_BetweenThem_IsTotalCover()
        {
            var g = new Drawn(
                "...#...",
                "...#...",
                "...#...");
            Assert.That(GridSight.Cover(g, P(0, 1), P(6, 1)).Level, Is.EqualTo(CoverLevel.Total));
            Assert.That(GridSight.CanSee(g, P(0, 1), P(6, 1)), Is.False);
        }

        [Test]
        public void ACorner_HidesWhatIsBehindIt_ButNotWhatIsPastIt()
        {
            var g = new Drawn(
                "....#...",
                "....#...",
                "....#...",
                "........");
            Assert.That(GridSight.Cover(g, P(0, 0), P(7, 0)).Level, Is.EqualTo(CoverLevel.Total), "behind the wall");
            Assert.That(GridSight.Cover(g, P(0, 3), P(7, 3)).Level, Is.EqualTo(CoverLevel.None), "along the open row below it");
            Assert.That(GridSight.Cover(g, P(3, 0), P(3, 3)).Level, Is.EqualTo(CoverLevel.None), "beside the wall's face");
        }

        [Test]
        public void GrazingAWallsEdge_IsClear_ButSqueezingBetweenDiagonalWalls_IsNot()
        {
            // Two walls touching only at a corner: no line passes between them.
            var g = new Drawn(
                ".#",
                "#.");
            Assert.That(GridSight.Cover(g, P(0, 0), P(1, 1)).Level, Is.EqualTo(CoverLevel.Total), "a diagonal through two walls touching at a corner");
            // A straight wall alongside: a line along its face is not blocked.
            var h = new Drawn(
                "......",
                "######");
            Assert.That(GridSight.Cover(h, P(0, 0), P(5, 0)).Level, Is.EqualTo(CoverLevel.None));
        }

        [Test]
        public void ATargetInARoomCorner_HasNoCover_FromInsideTheRoom()
        {
            var g = new Drawn(
                "######",
                "#....#",
                "#....#",
                "######");
            Assert.That(GridSight.Cover(g, P(4, 2), P(1, 1)).Level, Is.EqualTo(CoverLevel.None));
            Assert.That(GridSight.Cover(g, P(1, 1), P(4, 2)).Level, Is.EqualTo(CoverLevel.None));
        }

        [Test]
        public void HalfAndThreeQuartersCover_ComeFromHowManyCornerLinesAWallBlocks()
        {
            // The target stands just past the end of a wall: part of it is hidden.
            var half = new Drawn(
                ".....",
                ".....",
                "..##.",
                ".....");
            var r = GridSight.Cover(half, P(2, 0), P(4, 3));
            Assert.That(r.Level, Is.EqualTo(CoverLevel.Half), "two of the best corner's four lines are blocked");
            Assert.That(r.AcBonus, Is.EqualTo(2));
            Assert.That(GridSight.Cover(half, P(2, 0), P(3, 3)).Level, Is.EqualTo(CoverLevel.Total), "straight behind it");

            // A pillar between them hides three of the best corner's four lines.
            var pillar = new Drawn(
                ".....",
                "..#..",
                ".....");
            var q = GridSight.Cover(pillar, P(0, 0), P(4, 2));
            Assert.That(q.Level, Is.EqualTo(CoverLevel.ThreeQuarters));
            Assert.That(q.AcBonus, Is.EqualTo(5));
            Assert.That(q.LinesBlocked, Is.EqualTo(3));

            // An arrow-slit-like gap: only one corner line gets through.
            var slit = new Drawn(
                "....",
                "##.#",
                "....");
            var s = GridSight.Cover(slit, P(0, 0), P(0, 2));
            Assert.That(s.Level, Is.Not.EqualTo(CoverLevel.None));
        }

        [Test]
        public void CreaturesAndLowObstacles_GiveHalfCover_NeverMore()
        {
            var g = new Drawn(
                ".....",
                ".c...",
                ".....");
            var r = GridSight.Cover(g, P(0, 1), P(3, 1));
            Assert.That(r.Level, Is.EqualTo(CoverLevel.Half));
            Assert.That(r.By, Is.EqualTo("Goblin"));
            var t = new Drawn(".o..");
            Assert.That(GridSight.Cover(t, P(0, 0), P(3, 0)).Level, Is.EqualTo(CoverLevel.Half));
        }

        [Test]
        public void AnAreasSpread_StopsAtWalls_ButNotAtCreatures()
        {
            var g = new Drawn(
                ".......",
                ".c.#...",
                ".......");
            Assert.That(GridSight.CentreLineClear(g, P(0, 1), P(6, 1)), Is.False, "the wall stops the spread");
            Assert.That(GridSight.CentreLineClear(g, P(0, 1), P(2, 1)), Is.True, "a creature does not");
            Assert.That(GridSight.CentreLineClear(g, P(0, 0), P(6, 0)), Is.True);
        }

        [Test]
        public void DifferentFloors_AreTotalCover()
        {
            var g = new Drawn("....");
            Assert.That(GridSight.Cover(g, new GridPos(0, 0, 0), new GridPos(2, 0, 1)).Level, Is.EqualTo(CoverLevel.Total));
        }

        [Test]
        public void TheSameSquares_AlwaysGiveTheSameAnswer_BothWays()
        {
            var g = new Drawn(
                "..#.....",
                "..#..#..",
                ".....#..",
                "##...#..",
                "........");
            for (var ax = 0; ax < 8; ax++)
                for (var ay = 0; ay < 5; ay++)
                    for (var bx = 0; bx < 8; bx++)
                        for (var by = 0; by < 5; by++)
                        {
                            if (g.IsWall(ax, ay) || g.IsWall(bx, by)) continue;
                            var there = GridSight.CanSee(g, P(ax, ay), P(bx, by));
                            var back = GridSight.CanSee(g, P(bx, by), P(ax, ay));
                            Assert.That(back, Is.EqualTo(there), $"({ax},{ay}) ↔ ({bx},{by})");
                            Assert.That(GridSight.CentreLineClear(g, P(ax, ay), P(bx, by)), Is.EqualTo(GridSight.CentreLineClear(g, P(bx, by), P(ax, ay))));
                        }
        }
    }
}
