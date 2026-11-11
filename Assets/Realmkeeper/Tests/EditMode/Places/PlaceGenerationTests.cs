using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Settlements;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Tests.EditMode.Places
{
    /// <summary>
    /// Phase 13 (ADR-0063): generation across many seeds and every archetype, interiors for every building function
    /// and size, the authored fort, determinism, and fault injection on the validator.
    /// </summary>
    public sealed class PlaceGenerationTests
    {
        public const int SeedsPerArchetype = 250;

        [Test]
        public void TheCatalogs_AreConsistent()
        {
            Assert.That(ArchetypeCatalog.Validate(), Is.Empty);
            Assert.That(FeatureCatalog.Validate(), Is.Empty);
            Assert.That(TrapCatalog.Validate(), Is.Empty);
            Assert.That(LootTables.Validate(), Is.Empty);
            Assert.That(ItemCatalog.Validate().ToList(), Is.Empty);
            Assert.That(ArchetypeCatalog.All.Count(), Is.EqualTo(10));
            foreach (var d in PlaceCatalog.All) Assert.That(ArchetypeCatalog.Find(d.ArchetypeId), Is.Not.Null, d.Id);
        }

        [Test]
        public void EveryArchetype_GeneratesValidConnectedLayouts_AcrossManySeeds()
        {
            var failures = new List<string>();
            var generated = 0;
            var stats = new StringBuilder();
            foreach (var a in ArchetypeCatalog.All)
            {
                int rooms = 0, secrets = 0, floors = 0, groups = 0, traps = 0, containers = 0, locked = 0;
                for (var seed = 1UL; seed <= SeedsPerArchetype; seed++)
                {
                    PlaceLayout l;
                    try
                    {
                        l = PlaceGenerator.Generate(a, seed * 7919UL);
                    }
                    catch (InvalidOperationException e)
                    {
                        failures.Add(a.Id + "/" + seed + ": " + e.Message);
                        continue;
                    }

                    generated++;
                    var errors = PlaceValidator.Validate(l);
                    if (errors.Count > 0) failures.Add(a.Id + "/" + seed + ": " + string.Join("; ", errors));
                    rooms += l.Rooms.Count;
                    secrets += l.Rooms.Count(r => r.Secret);
                    floors += l.Floors.Count;
                    groups += l.Groups.Count;
                    traps += l.Features.Count(f => f.Kind == FeatureKind.Trap);
                    containers += l.Features.Count(f => f.Kind == FeatureKind.Container);
                    locked += l.Connections.Count(c => c.Door == DoorState.Locked);
                    Assert.That(l.Floors.Count, Is.InRange(a.MinFloors, a.MaxFloors), a.Id);
                    Assert.That(l.Rooms.Count(r => !r.Secret), Is.GreaterThanOrEqualTo(a.MinRooms), a.Id + ": enough rooms");
                }

                stats.AppendLine($"{a.Id}: {SeedsPerArchetype} layouts, avg {rooms / (double)SeedsPerArchetype:0.0} rooms, {floors / (double)SeedsPerArchetype:0.0} floors, " +
                                 $"{secrets} secret rooms, {groups} groups, {traps} traps, {containers} containers, {locked} locked doors");
            }

            TestContext.WriteLine($"Generated {generated} layouts over {ArchetypeCatalog.All.Count()} archetypes; validation failures: {failures.Count}");
            TestContext.WriteLine(stats.ToString());
            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(10)));
            Assert.That(generated, Is.EqualTo(SeedsPerArchetype * ArchetypeCatalog.All.Count()));
        }

        [Test]
        public void TheSameSeed_GivesTheSameLayout_AndADifferentSeed_ADifferentOne()
        {
            foreach (var a in ArchetypeCatalog.All)
            {
                var one = Signature(PlaceGenerator.Generate(a, 4242UL));
                var two = Signature(PlaceGenerator.Generate(a, 4242UL));
                var other = Signature(PlaceGenerator.Generate(a, 4243UL));
                Assert.That(two, Is.EqualTo(one), a.Id + ": deterministic");
                Assert.That(other, Is.Not.EqualTo(one), a.Id + ": seeds vary");
            }
        }

        [Test]
        public void Archetypes_AreDistinct_InRoomsMaterialsAndInhabitants()
        {
            var mine = PlaceGenerator.Generate(ArchetypeCatalog.Find("mine"), 11UL);
            var crypt = PlaceGenerator.Generate(ArchetypeCatalog.Find("crypt"), 11UL);
            var cave = PlaceGenerator.Generate(ArchetypeCatalog.Find("cave"), 11UL);
            Assert.That(mine.Floors.Count, Is.EqualTo(2), "A mine has a lower level.");
            Assert.That(mine.Connections.Any(c => c.Kind == ConnectionKind.Ladder), Is.True, "Mine levels are joined by a ladder in a shaft.");
            Assert.That(mine.Groups.SelectMany(g => g.Members).Select(m => m.CreatureId), Has.Some.EqualTo("giant_spider").Or.Some.EqualTo("giant_rat"));
            Assert.That(crypt.Groups.SelectMany(g => g.Members).Select(m => m.CreatureId).All(c => c == "skeleton" || c == "zombie"), Is.True, "Crypts hold the dead.");
            Assert.That(crypt.Rooms.Any(r => r.Kind == RoomKind.Tomb), Is.True);
            Assert.That(cave.Connections.Any(c => c.Kind == ConnectionKind.Door), Is.False, "Caves have no doors.");
            Assert.That(ArchetypeCatalog.Find("mine").Material, Is.Not.EqualTo(ArchetypeCatalog.Find("crypt").Material));
        }

        [Test]
        public void StoryFeatures_GoIntoTheirRooms_AndRequiredOnesAreNeverBehindASecret()
        {
            var def = PlaceCatalog.Find(PlaceCatalog.Mine);
            for (var seed = 1UL; seed <= 60; seed++)
            {
                var l = PlaceGenerator.Generate(ArchetypeCatalog.Find(def.ArchetypeId), seed, def);
                var captive = l.Features.Single(f => f.DefinitionId == "captive_chains");
                Assert.That(captive.Required, Is.True);
                Assert.That(l.Room(captive.Room).Secret, Is.False);
                Assert.That(l.Features.Any(f => f.DefinitionId == "foremans_ledger"), Is.True, "Why the mine was abandoned is written down.");
                Assert.That(PlaceValidator.Validate(l), Is.Empty);
            }
        }

        [Test]
        public void Interiors_AreValid_ForEveryFunctionSizeAndWealth()
        {
            var count = 0;
            foreach (BuildingFunction f in Enum.GetValues(typeof(BuildingFunction)))
            {
                if (!InteriorGenerator.HasInterior(f)) continue;
                foreach (var (w, d) in new[] { (6f, 6f), (9f, 8f), (12f, 10f), (18f, 14f), (24f, 24f) })
                    foreach (var storeys in new[] { 1, 2, 3 })
                        foreach (var wealth in new[] { 20, 60, 90 })
                        {
                            var l = InteriorGenerator.Generate(new InteriorRequest { Function = f, WidthMetres = w, DepthMetres = d, Storeys = storeys, Wealth = wealth }, (ulong)(count + 1));
                            var errors = PlaceValidator.Validate(l);
                            Assert.That(errors, Is.Empty, $"{f} {w}x{d} storeys {storeys} wealth {wealth}: {string.Join("; ", errors)}");
                            count++;
                        }
            }

            TestContext.WriteLine($"{count} interiors validated.");
            var home = InteriorGenerator.Generate(new InteriorRequest { Function = BuildingFunction.Home, WidthMetres = 12f, DepthMetres = 10f, Storeys = 2, Wealth = 70 }, 5UL);
            Assert.That(home.Rooms.Select(r => r.Kind), Has.Member(RoomKind.Bedroom));
            Assert.That(home.Floors.Count, Is.EqualTo(3), "Ground floor, upper floor and a cellar for a well-off home.");
            Assert.That(home.Features.Any(x => x.DefinitionId == "hearth"), Is.True);
            var guard = InteriorGenerator.Generate(new InteriorRequest { Function = BuildingFunction.GuardPost, WidthMetres = 12f, DepthMetres = 10f }, 5UL);
            Assert.That(guard.Rooms.Where(r => r.Restricted).Select(r => r.Kind), Is.EquivalentTo(new[] { RoomKind.Cell, RoomKind.Armoury }));
        }

        [Test]
        public void TheAuthoredFort_IsValid_AndBuiltAsDesigned()
        {
            var l = AuthoredPlaces.WatchFort();
            Assert.That(PlaceValidator.Validate(l), Is.Empty);
            Assert.That(l.Connections.Count(c => c.Kind == ConnectionKind.Entrance), Is.EqualTo(2), "The gate and a side door.");
            Assert.That(l.Connection("c_gate").GuardedBy, Is.EqualTo("watch"));
            Assert.That(l.Rooms.Where(r => r.Restricted).Select(r => r.Id), Is.EquivalentTo(new[] { "armoury", "quarters" }));
            Assert.That(l.Connection("c_quarters").KeyItem, Is.EqualTo("fort_key"));
            Assert.That(l.Features.Single(f => f.Get("key") == "fort_key").Room, Is.EqualTo("armoury"), "The key lies where the side door leads.");
        }

        [Test]
        public void TheValidator_CatchesBrokenLayouts()
        {
            string Broken(Action<PlaceLayout> breakIt)
            {
                var l = PlaceGenerator.Generate(ArchetypeCatalog.Find("crypt"), 99UL);
                breakIt(l);
                return string.Join("; ", PlaceValidator.Validate(l));
            }

            Assert.That(Broken(l => l.Rooms[1].Rect = l.Rooms[0].Rect), Does.Contain("overlap"));
            Assert.That(Broken(l => l.Connections.RemoveAll(c => c.Kind == ConnectionKind.Entrance)), Does.Contain("no way in"));
            Assert.That(Broken(l => l.Connections.RemoveAll(c => c.Kind != ConnectionKind.Entrance && c.Joins(l.Rooms.Last(r => !r.Secret).Id))), Does.Contain("cannot be reached"));
            Assert.That(Broken(l => l.Rooms[0].Ceiling = 1.8f), Does.Contain("below head height"));
            Assert.That(Broken(l =>
            {
                var c = l.Connections.First(x => x.Kind == ConnectionKind.Opening || x.Kind == ConnectionKind.Door);
                c.Door = DoorState.Locked;
                c.LockDc = 0;
                c.KeyItem = null;
            }), Does.Contain("no key"));
            Assert.That(Broken(l =>
            {
                var f = l.Features.First();
                f.X = -50;
            }), Does.Contain("outside"));
            Assert.That(Broken(l =>
            {
                var secret = l.Rooms.First(r => r.Secret);
                l.Features.Add(new PlaceFeature { Id = "must", Room = secret.Id, Kind = FeatureKind.Clue, DefinitionId = "epitaph", X = secret.Rect.X + 1, Z = secret.Rect.Z + 1, Required = true });
            }), Does.Contain("not reachable without a secret"));
            Assert.That(Broken(l =>
            {
                var c = l.Connections.First(x => x.IsVertical || x.Kind == ConnectionKind.Opening || x.Kind == ConnectionKind.Door);
                c.Kind = ConnectionKind.Stairs;
                c.B = c.A;
            }), Does.Contain("neighbouring floors"));
            Assert.That(Broken(l => l.Groups.Add(new PlaceGroup { Id = "ghost", Room = l.Rooms[0].Id, Members = { new GroupMember { CreatureId = "skeleton", X = -3, Z = -3 } } })), Does.Contain("outside"));
        }

        public static string Signature(PlaceLayout l)
        {
            var sb = new StringBuilder();
            foreach (var f in l.Floors) sb.Append('F').Append(f.Index).Append(f.Name).Append(';');
            foreach (var r in l.Rooms) sb.Append(r.Id).Append(r.Rect).Append(r.Kind).Append(r.Lit).Append(r.Secret).Append(r.Hazard).Append(';');
            foreach (var c in l.Connections) sb.Append(c.Id).Append(c.Kind).Append(c.A).Append(c.B).Append(c.Rect).Append(c.Door).Append(c.LockDc).Append(';');
            foreach (var f in l.Features) sb.Append(f.Id).Append(f.DefinitionId).Append(f.Room).Append(f.X).Append(',').Append(f.Z).Append(';');
            foreach (var g in l.Groups)
            {
                sb.Append(g.Id).Append(g.Room);
                foreach (var m in g.Members) sb.Append(m.CreatureId).Append(m.X).Append(',').Append(m.Z);
                sb.Append(';');
            }

            return sb.ToString();
        }
    }
}
