using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.TestTools;

namespace Realmkeeper.Tests.PlayMode
{
    public sealed class WorldPresentationTests : RealmkeeperPlayModeFixture
    {
        [UnityTest]
        public IEnumerator Bootstrap_CreatesWorld_WithOneSettlementViewPerSettlementId()
        {
            Assert.That(Bootstrap.LastCreateResult.IsSuccess, Is.True, Bootstrap.LastCreateResult.Message);
            Assert.That(Bootstrap.Session.HasWorld, Is.True);

            var settlements = Bootstrap.Session.GetSettlementsInView();
            Assert.That(settlements.Count, Is.EqualTo(3));

            var expected = new HashSet<SettlementId>();
            foreach (var s in settlements) expected.Add(s.SettlementId);
            Assert.That(new HashSet<SettlementId>(Bootstrap.WorldView.Settlements.Keys), Is.EquivalentTo(expected));

            foreach (var pair in Bootstrap.WorldView.Settlements)
            {
                Assert.That(pair.Value, Is.Not.Null);
                Assert.That(pair.Value.SettlementId, Is.EqualTo(pair.Key), "View is keyed by the ID it displays.");
                Assert.That(pair.Value.BuildingCount, Is.GreaterThan(0));
            }

            Assert.That(Object.FindObjectsByType<SettlementView>(FindObjectsSortMode.None).Length, Is.EqualTo(3));
            yield break;
        }

        [UnityTest]
        public IEnumerator TerrainMesh_VertexCountAndBounds_MatchRegionMap()
        {
            var map = Bootstrap.Session.Map;
            var view = Bootstrap.WorldView;

            var colliderMesh = view.TerrainCollider.sharedMesh;
            Assert.That(colliderMesh.vertexCount, Is.EqualTo(map.Width * map.Depth));

            var bounds = colliderMesh.bounds;
            var width = (map.Width - 1) * map.CellSize;
            var depth = (map.Depth - 1) * map.CellSize;
            Assert.That(bounds.min.x, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(bounds.min.z, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(bounds.size.x, Is.EqualTo(width).Within(1e-3f));
            Assert.That(bounds.size.z, Is.EqualTo(depth).Within(1e-3f));
            Assert.That(bounds.max.y, Is.LessThanOrEqualTo(map.MaxElevation + 1e-3f));

            var chunksPerSide = Mathf.CeilToInt((map.Width - 1) / (float)TerrainMeshBuilder.ChunkCells);
            Assert.That(view.TerrainChunkCount, Is.EqualTo(chunksPerSide * chunksPerSide));
            Assert.That(view.Bounds.width, Is.EqualTo(width).Within(1e-3f));
            yield break;
        }

        [UnityTest]
        public IEnumerator Forests_AreInstanced_WithinTargetRange()
        {
            Assert.That(Bootstrap.WorldView.TreeCount, Is.InRange(8000, 20000));
            yield break;
        }
    }
}
