using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;

namespace Realmkeeper.Tests.EditMode.Graphics
{
    /// <summary>
    /// The engine-free half of the graphics pass: modular building plans and resident appearance.
    /// Both are derived presentation data, so the tests check determinism, coverage of the kit, that
    /// plans stay inside their layout footprints, and that appearance never depends on anything but
    /// the resident's stable identity, role and name.
    /// </summary>
    public sealed class BuildingKitTests
    {
        private static readonly SettlementId Town = new SettlementId(Guid.Parse("0f3c2a8e-56b1-4f6d-9d2e-3b7f0c1a9e44"));
        private static readonly RegionId Region = new RegionId(Guid.Parse("7a1b9c2d-3e4f-4a5b-8c6d-9e0f1a2b3c4d"));

        private static LocalEnvironmentLayout Layout(SettlementType type, long seed = 12345L, SettlementId? id = null) =>
            LocalEnvironmentGenerator.Generate(seed, id ?? Town, Region, type, Array.Empty<ResidentInput>());

        private static IEnumerable<SettlementType> Types => new[] { SettlementType.Hamlet, SettlementType.Village, SettlementType.Town };

        [Test]
        public void SameLayout_GivesIdenticalPlans()
        {
            foreach (var type in Types)
            {
                var a = Layout(type).Buildings.Select(b => BuildingKitPlanner.Plan(b, type, Town.ToString())).ToList();
                var b2 = Layout(type).Buildings.Select(b => BuildingKitPlanner.Plan(b, type, Town.ToString())).ToList();
                Assert.That(a.Count, Is.EqualTo(b2.Count));
                for (var i = 0; i < a.Count; i++)
                {
                    Assert.That(a[i].Archetype, Is.EqualTo(b2[i].Archetype));
                    Assert.That(a[i].Pieces, Is.EqualTo(b2[i].Pieces), $"{type} {a[i].Key}: the same layout assembles the same way.");
                }
            }
        }

        [Test]
        public void Plans_DoNotDependOnOrder()
        {
            var layout = Layout(SettlementType.Town);
            var forward = layout.Buildings.ToDictionary(b => b.Key, b => BuildingKitPlanner.Plan(b, layout.Type, Town.ToString()));
            var backward = Enumerable.Reverse(layout.Buildings).ToDictionary(b => b.Key, b => BuildingKitPlanner.Plan(b, layout.Type, Town.ToString()));
            foreach (var key in forward.Keys) Assert.That(backward[key].Pieces, Is.EqualTo(forward[key].Pieces), key);
        }

        [Test]
        public void DifferentSettlements_LookDifferent()
        {
            var other = new SettlementId(Guid.Parse("11111111-2222-4333-8444-555555555555"));
            var a = Layout(SettlementType.Town);
            var signatures = a.Buildings.Select(b => Signature(BuildingKitPlanner.Plan(b, SettlementType.Town, Town.ToString()))).ToList();
            var b2 = a.Buildings.Select(b => Signature(BuildingKitPlanner.Plan(b, SettlementType.Town, other.ToString()))).ToList();
            Assert.That(signatures, Is.Not.EqualTo(b2), "The variation key (settlement ID) changes the buildings.");
            Assert.That(signatures.Distinct().Count(), Is.GreaterThan(signatures.Count / 2), "Houses in one town are not all alike.");
        }

        [Test]
        public void EveryPiece_IsAKnownKitModuleOrProceduralPiece()
        {
            var known = new HashSet<string>(BuildingKitPlanner.KitModuleIds.Concat(BuildingKitPlanner.ProceduralIds), StringComparer.Ordinal);
            foreach (var type in Types)
            foreach (var seed in new[] { 1L, 77L, 9001L })
            {
                var layout = Layout(type, seed);
                foreach (var b in layout.Buildings)
                foreach (var p in BuildingKitPlanner.Plan(b, type, Town.ToString()).Pieces)
                    Assert.That(known, Has.Member(p.ModuleId), $"{type} {b.Key}: {p.ModuleId}");
            }

            foreach (var p in BuildingKitPlanner.Gatehouse(6f, -50f).Pieces) Assert.That(known, Has.Member(p.ModuleId));
        }

        [Test]
        public void Plans_StayInsideTheirLayoutFootprint()
        {
            foreach (var type in Types)
            foreach (var seed in new[] { 3L, 42L, 2024L })
            foreach (var b in Layout(type, seed).Buildings)
            {
                var plan = BuildingKitPlanner.Plan(b, type, Town.ToString());
                if (plan.Archetype == BuildingArchetype.Tower)
                {
                    Assert.That(plan.Width, Is.LessThanOrEqualTo(b.Width + 0.25f));
                    continue;
                }

                Assert.That(plan.Width, Is.LessThanOrEqualTo(b.Width + 0.5f), $"{b.Key} width");
                Assert.That(plan.Depth, Is.LessThanOrEqualTo(b.Depth + 0.5f), $"{b.Key} depth");
                Assert.That(plan.Width % BuildingKitPlanner.ModuleSize, Is.EqualTo(0f), "Footprints are whole modules.");
                Assert.That(plan.Storeys, Is.InRange(1, 3));
                Assert.That(plan.RidgeHeight, Is.GreaterThan(plan.WallHeight));
            }
        }

        [Test]
        public void NoPieceIsPlacedTwice()
        {
            foreach (var type in Types)
            foreach (var b in Layout(type).Buildings)
            {
                var plan = BuildingKitPlanner.Plan(b, type, Town.ToString());
                var duplicates = plan.Pieces.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key.ToString()).ToList();
                Assert.That(duplicates, Is.Empty, $"{type} {b.Key}");
            }
        }

        [Test]
        public void EveryHouse_HasAFrontDoor_AndTheTavernAnOpenDoorway()
        {
            foreach (var type in Types)
            foreach (var b in Layout(type).Buildings)
            {
                var plan = BuildingKitPlanner.Plan(b, type, Town.ToString());
                var front = -plan.Depth / 2f;
                if (plan.Archetype == BuildingArchetype.Tavern)
                {
                    Assert.That(plan.DoorwayWidth, Is.GreaterThan(1.2f));
                    Assert.That(plan.Pieces.Any(p => p.ModuleId == "Wall_Arch" && Math.Abs(p.X) < 0.01f && Math.Abs(p.Z - front) < 0.01f && p.Y == 0f),
                        "The tavern's arch is centred on its front wall.");
                    continue;
                }

                Assert.That(plan.Pieces.Any(p => p.ModuleId.Contains("_Door_") && p.Y == 0f && Math.Abs(p.Z - front) < 0.6f),
                    $"{type} {b.Key} ({plan.Archetype}) has a ground-floor door on its front.");
            }
        }

        [Test]
        public void Profiles_SelectArchetypesAndHeightsBySettlementType()
        {
            var hamlet = Layout(SettlementType.Hamlet).Buildings.Where(b => b.Kind == BuildingKind.House)
                .Select(b => BuildingKitPlanner.Plan(b, SettlementType.Hamlet, Town.ToString())).ToList();
            Assert.That(hamlet.All(p => p.Storeys == 1), "Hamlet houses have one storey.");
            Assert.That(hamlet.All(p => p.Archetype == BuildingArchetype.Cottage || p.Archetype == BuildingArchetype.Workshop));

            var towns = new[] { 1L, 2L, 3L, 4L }.SelectMany(s => Layout(SettlementType.Town, s).Buildings.Where(b => b.Kind == BuildingKind.House)
                .Select(b => BuildingKitPlanner.Plan(b, SettlementType.Town, Town.ToString()))).ToList();
            Assert.That(towns.Any(p => p.Storeys >= 2), "Towns have taller houses.");
            Assert.That(towns.Select(p => p.Archetype).Distinct().Count(), Is.GreaterThanOrEqualTo(3), "Towns mix house archetypes.");
            Assert.That(towns.SelectMany(p => p.Pieces).Any(p => p.ModuleId.StartsWith("Wall_UnevenBrick", StringComparison.Ordinal)), "Town ground floors are brick.");
        }

        [Test]
        public void EightArchetypes_AreAllReachable()
        {
            var seen = new HashSet<BuildingArchetype> { BuildingKitPlanner.Gatehouse(6f, -40f).Archetype };
            foreach (var type in Types)
            foreach (var seed in Enumerable.Range(1, 12))
            foreach (var b in Layout(type, seed).Buildings)
                seen.Add(BuildingKitPlanner.Plan(b, type, "s" + seed).Archetype);
            Assert.That(seen, Is.EquivalentTo(Enum.GetValues(typeof(BuildingArchetype)).Cast<BuildingArchetype>()));
        }

        [Test]
        public void Snap_RoundsToTheTwoMetreGrid()
        {
            Assert.That(BuildingKitPlanner.Snap(6.5f), Is.EqualTo(3));
            Assert.That(BuildingKitPlanner.Snap(7.49f), Is.EqualTo(3));
            Assert.That(BuildingKitPlanner.Snap(7.5f), Is.EqualTo(4));
            Assert.That(BuildingKitPlanner.Snap(12f), Is.EqualTo(6));
            Assert.That(BuildingKitPlanner.Snap(1f), Is.EqualTo(2), "At least two modules.");
        }

        [Test]
        public void LayoutCarriesTheSettlementType_ForTheVisualProfile()
        {
            foreach (var type in Types) Assert.That(Layout(type).Type, Is.EqualTo(type));
        }

        private static string Signature(BuildingPlan plan) =>
            plan.Archetype + ":" + plan.Storeys + ":" + string.Join(",", plan.Pieces.Select(p => p.ModuleId));
    }
}
