using System;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Geography;
using Realmkeeper.Domain.World;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.World
{
    /// <summary>
    /// Phase 29 (ADR-0122): the guarantee the overland terrain cache rests on. An <see cref="OverlandTerrain"/> is an immutable
    /// snapshot: its heights and cells are fixed when it is made, so even an edit to its region map afterwards does not change
    /// any answer it gives (heights, biomes, size). The cache matches a terrain by object identity; this test is what makes
    /// that identity a sufficient key, and the cache's sparse fingerprint only an integrity check.
    /// </summary>
    public sealed class Phase29OverlandSnapshotTests
    {
        [Test]
        public void ATerrain_IsASnapshot_EditingItsMapAfterwardsChangesNothingItAnswers()
        {
            var g = Phase2Worlds.Generate(123456789);
            var map = g.Map;
            var terrain = new OverlandTerrain(map, RegionKind.Vale, 123456789);

            // Every corner and cell centre of the region, and a fine line across it.
            float[] Heights() => Enumerable.Range(0, (map.Width + 1) * (map.Depth + 1))
                .Select(i => terrain.HeightAt(i % (map.Width + 1) * OverlandScale.MetresPerCell, i / (map.Width + 1) * OverlandScale.MetresPerCell))
                .Concat(Enumerable.Range(0, 4000).Select(i => terrain.HeightAt(i * 1.27f, i * 0.93f))).ToArray();
            Biome[] Biomes() => Enumerable.Range(0, map.Width * map.Depth).Select(i => terrain.Cell(i % map.Width, i / map.Width).Biome).ToArray();
            var heights = Heights();
            var biomes = Biomes();
            var (sx, sz) = (terrain.SizeX, terrain.SizeZ);

            // Edit the map everywhere after the terrain was made.
            for (var z = 0; z < map.Depth; z++)
                for (var x = 0; x < map.Width; x++)
                {
                    map.SetElevation(x, z, 1f - map.GetElevation(x, z));
                    map.SetTerrain(x, z, TerrainType.Mountain);
                }

            Assert.That(Heights(), Is.EqualTo(heights), "Heights are the snapshot's, not the map's.");
            Assert.That(Biomes(), Is.EqualTo(biomes), "Cells are the snapshot's.");
            Assert.That((terrain.SizeX, terrain.SizeZ), Is.EqualTo((sx, sz)));
            Assert.That(new OverlandTerrain(map, RegionKind.Vale, 123456789).HeightAt(1000f, 1000f), Is.Not.EqualTo(terrain.HeightAt(1000f, 1000f)),
                "A terrain made after the edit does differ: only a new object carries new ground.");
        }

        [Test]
        public void ATerrain_HasNoWayToChange_AndItsMapsSizeIsFixed()
        {
            var t = typeof(OverlandTerrain);
            Assert.That(t.GetProperties().Where(p => p.CanWrite).Select(p => p.Name), Is.Empty, "No settable property.");
            Assert.That(t.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                .Where(f => !f.IsInitOnly).Select(f => f.Name), Is.Empty, "Every field is readonly.");
            var m = typeof(RegionMap);
            Assert.That(m.GetProperty(nameof(RegionMap.Width)).CanWrite || m.GetProperty(nameof(RegionMap.Depth)).CanWrite, Is.False,
                "The map's size, the one thing a terrain reads live, cannot change.");
        }
    }
}
