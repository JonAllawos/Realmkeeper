using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Generation;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Phase3
{
    /// <summary>Prerequisite 1: persistent region identities.</summary>
    public sealed class RegionIdentityTests
    {
        [Test]
        public void GeneratedWorld_HasAStableRegion_AndEverySettlementReferencesIt()
        {
            var a = Phase2Worlds.Generated(123456789);
            var b = Phase2Worlds.Generated(123456789);
            var other = Phase2Worlds.Generated(42);

            Assert.That(a.Regions.Count, Is.EqualTo(1));
            var region = a.Regions[0];
            Assert.That(region.RegionId.IsEmpty, Is.False);
            Assert.That(region.Name, Does.StartWith("Vale of "));
            Assert.That(a.Settlements.All(s => s.RegionId == region.RegionId && region.Contains(s.Position)), Is.True);
            Assert.That(b.Regions[0].RegionId, Is.EqualTo(region.RegionId), "Same seed, same region identity.");
            Assert.That(other.Regions[0].RegionId, Is.Not.EqualTo(region.RegionId), "Different worlds, different regions.");
            Assert.That(WorldValidator.Validate(a).IsValid, Is.True);
        }

        [Test]
        public void RegionId_DoesNotDependOnNamesBoundsOrOrder()
        {
            var settings = new WorldSettings();
            var generator = new RegionGenerator(new Xoshiro256RandomFactory());
            var x = generator.Generate(new WorldGenerationRequest("First Name", 77, settings)).World;
            var y = generator.Generate(new WorldGenerationRequest("Completely Different", 77, settings)).World;
            Assert.That(y.Regions[0].RegionId, Is.EqualTo(x.Regions[0].RegionId), "World and region names play no part in identity.");

            // Renaming or resizing a region never changes which settlements reference it.
            var region = x.Regions[0];
            region.Name = "Renamed Vale";
            region.Width += 1;
            Assert.That(x.Settlements.All(s => s.RegionId == region.RegionId), Is.True);
            Assert.That(x.FindRegion(region.RegionId), Is.SameAs(region));
        }

        [Test]
        public void SaveLoad_PreservesRegionLinks_AndRepeatedLoadsNeverDuplicateRegions()
        {
            var world = Phase2Worlds.Generated(2718);
            var serializer = new JsonWorldSerializer();

            var once = serializer.Deserialize(serializer.Serialize(world)).World;
            var twice = serializer.Deserialize(serializer.Serialize(once)).World;

            foreach (var loaded in new[] { once, twice })
            {
                Assert.That(loaded.Regions.Select(r => r.RegionId), Is.EqualTo(world.Regions.Select(r => r.RegionId)));
                Assert.That(loaded.Settlements.Select(s => s.RegionId), Is.EqualTo(world.Settlements.Select(s => s.RegionId)));
            }

            Assert.That(WorldFingerprint.Of(twice), Is.EqualTo(WorldFingerprint.Of(world)));
        }

        [Test]
        public void Validator_ExplainsMissingUnknownAndOutOfBoundsRegionReferences()
        {
            var world = TestWorlds.SingleSettlement(100, 100, 10);
            var s = world.Settlements[0];

            s.RegionId = default;
            Assert.That(WorldValidator.Validate(world).Errors, Has.Some.Contains("'Ashford' has no region"));

            s.RegionId = new RegionId(new Guid("00000000-0000-4000-8000-00000000abcd"));
            Assert.That(WorldValidator.Validate(world).Errors, Has.Some.Contains("references unknown region"));

            s.RegionId = TestWorlds.RegionId;
            s.Position = new MapCoord(90, 90);
            Assert.That(WorldValidator.Validate(world).Errors, Has.Some.Contains("lies outside its region"));

            world.Regions.Add(world.Regions[0].Clone());
            Assert.That(WorldValidator.Validate(world).Errors, Has.Some.Contains("Duplicate RegionId"));
        }

        [Test]
        public void LocationsResolveToTheirRegion_AndUnassignableSettlementsFailLoudly()
        {
            var world = Phase2Worlds.Generated(1337);
            foreach (var s in world.Settlements)
                Assert.That(world.FindRegionContaining(s.Position).RegionId, Is.EqualTo(s.RegionId));
            Assert.That(world.FindRegionContaining(new MapCoord(5000, 5000)), Is.Null);

            // A settlement no region contains is never silently attached to the wrong region.
            var orphan = TestWorlds.SingleSettlement(100, 100, 10);
            orphan.Regions.Clear();
            orphan.Settlements[0].RegionId = default;
            orphan.Settlements[0].Position = new MapCoord(500, 500);
            var ex = Assert.Throws<InvalidOperationException>(() =>
                WorldPopulationInitializer.EnsureRegions(orphan, 128, 128, new Xoshiro256RandomFactory()));
            Assert.That(ex.Message, Does.Contain("outside every region"));
        }

        [Test]
        public void Version2Save_UpgradesToRegionsPlayerAndResidents_WithoutChangingExistingData()
        {
            var fresh = Phase2Worlds.GeneratedBeforeLivingWorld(555); // Phase 10: the v11 world (its living-world additions arrive when a session loads a save; see the Phase 10 tests).
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(fresh));

            // Reconstruct the v2 layout: no regions, no regionId, no player, no named residents, no labour focus.
            doc["schemaVersion"] = 2;
            var w = (JObject)doc["world"];
            w.Remove("regions");
            w.Remove("player");
            w["npcs"] = new JArray();
            foreach (JObject s in (JArray)w["settlements"])
            {
                s.Remove("regionId");
                ((JObject)s["economy"]).Remove("labourFocus");
            }

            var result = serializer.Deserialize(doc.ToString());

            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Errors));
            var upgraded = result.World;
            Assert.That(upgraded.Regions.Single().RegionId, Is.EqualTo(fresh.Regions.Single().RegionId), "Same deterministic region as a fresh world.");
            Assert.That(upgraded.Settlements.Select(s => s.SettlementId), Is.EqualTo(fresh.Settlements.Select(s => s.SettlementId)), "Existing identities kept.");
            Assert.That(upgraded.Settlements.Select(s => s.Population), Is.EqualTo(fresh.Settlements.Select(s => s.Population)), "Existing data kept.");
            Assert.That(upgraded.Player.CharacterId, Is.EqualTo(fresh.Player.CharacterId));
            Assert.That(upgraded.Npcs.Select(n => n.NpcId), Is.EqualTo(fresh.Npcs.Select(n => n.NpcId)));
            Assert.That(upgraded.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
        }

        [Test]
        public void NamedResidents_AreAStableSubsetOfThePopulation()
        {
            var world = Phase2Worlds.Generated(123456789);
            Assert.That(world.Npcs.Count, Is.EqualTo((6 + 3) + 5 + (4 + 1)), "Town, village and hamlet residents (Phase 10: the town's captain, guard and priest, the hamlet's warden).");
            Assert.That(world.Npcs.Select(n => n.NpcId).Distinct().Count(), Is.EqualTo(world.Npcs.Count));
            foreach (var s in world.Settlements)
                Assert.That(world.Npcs.Count(n => n.HomeSettlementId == s.SettlementId), Is.LessThanOrEqualTo(s.Population));
            Assert.That(world.Npcs.All(n => !n.HasMetPlayer && n.ConversationCount == 0), Is.True);
            Assert.That(Phase2Worlds.Generated(123456789).Npcs.Select(n => n.NpcId), Is.EqualTo(world.Npcs.Select(n => n.NpcId)));
        }
    }
}
