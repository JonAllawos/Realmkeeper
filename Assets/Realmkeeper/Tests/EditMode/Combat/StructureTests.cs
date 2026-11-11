using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Commerce;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Combat
{
    /// <summary>Phase 8 settlement structures: the catalog, generation from existing places and shops, distribution by size, determinism.</summary>
    public sealed class StructureTests
    {
        [Test]
        public void TheCatalog_IsModest_Classified_AndHonest()
        {
            var all = StructureCatalog.All;
            Assert.That(all.Select(d => d.Id).Distinct().Count(), Is.EqualTo(all.Count));
            Assert.That(all.Count, Is.InRange(12, 30), "A modest catalog.");
            foreach (var d in all)
            {
                Assert.That(d.Name, Is.Not.Empty);
                Assert.That(d.Description, Is.Not.Empty, d.Id);
                Assert.That(d.Participates, Is.Not.Empty, d.Id + " states what it does.");
                Assert.That(d.Sign, Is.Not.Empty, d.Id);
                if (d.Role == StructureRole.Functional) Assert.That(d.OnLocation, Is.Not.Null, d.Id + " is functional because it is an existing place.");
                else Assert.That(d.OnLocation, Is.Null, d.Id);
            }

            Assert.That(all.Count(d => d.Role == StructureRole.Functional), Is.GreaterThan(0));
            Assert.That(all.Count(d => d.Role == StructureRole.Informational), Is.GreaterThan(0));
            Assert.That(all.Count(d => d.Role == StructureRole.Decorative), Is.GreaterThan(0));
        }

        [Test]
        public void EveryPlace_IsOneStructure_AndEveryShopIsHousedOnce()
        {
            var w = Phase2Worlds.Generated(424242);
            Assert.That(w.Structures, Is.Not.Empty);
            foreach (var l in w.Locations)
            {
                var on = w.Structures.Where(s => s.LocationId == l.LocationId).ToList();
                Assert.That(on.Count, Is.EqualTo(1), l.Name);
                Assert.That(on[0].SettlementId, Is.EqualTo(l.SettlementId));
                Assert.That(StructureCatalog.Find(on[0].DefinitionId).Role, Is.EqualTo(StructureRole.Functional));
                Assert.That(on[0].LayoutKey, Is.EqualTo(l.LayoutKey ?? string.Empty));
            }

            foreach (var shop in w.Shops)
            {
                var housing = w.Structures.Where(s => s.ShopId == shop.ShopId).ToList();
                Assert.That(housing.Count, Is.EqualTo(1), shop.Name);
                Assert.That(housing[0].LocationId, Is.EqualTo(shop.LocationId));
                Assert.That(housing[0].Name, Is.EqualTo(shop.Name), "Existing shops keep their names.");
                Assert.That(housing[0].OwnerNpcId, Is.EqualTo(shop.ShopkeeperNpcId));
            }

            Assert.That(WorldValidator.Validate(w).IsValid, Is.True);
        }

        [Test]
        public void Distribution_FollowsSettlementSize()
        {
            var w = Phase2Worlds.Generated(424242);
            foreach (var s in w.Settlements)
            {
                var ids = w.Structures.Where(x => x.SettlementId == s.SettlementId).Select(x => x.DefinitionId).ToList();
                Assert.That(ids, Does.Contain("well"), s.Name);
                if (s.SettlementType == SettlementType.Hamlet)
                {
                    Assert.That(ids, Does.Contain("barn"));
                    Assert.That(ids, Does.Not.Contain("bakery").And.Not.Contain("warehouse").And.Not.Contain("market_stalls"));
                }
                else if (s.SettlementType == SettlementType.Village)
                {
                    Assert.That(ids, Does.Contain("bakery").And.Contain("stable").And.Contain("barn"));
                    Assert.That(ids, Does.Not.Contain("butcher").And.Not.Contain("warehouse"));
                }
                else
                {
                    Assert.That(ids, Does.Contain("warehouse").And.Contain("butcher").And.Contain("mason"));
                    Assert.That(ids, Does.Not.Contain("barn"), "Towns have no barns.");
                }

                Assert.That(ids.Count(i => i == "well"), Is.EqualTo(1), "Informational and decorative structures appear once each.");
            }

            Assert.That(w.Settlements.Select(s => s.SettlementType).Distinct().Count(), Is.GreaterThan(1), "The test world has settlements of more than one size.");
        }

        [Test]
        public void Generation_IsDeterministic_AndOnceOnly()
        {
            var a = Phase2Worlds.Generated(31337);
            var b = Phase2Worlds.Generated(31337);
            Assert.That(a.Structures.Select(s => s.StructureId + s.DefinitionId + s.Name), Is.EqualTo(b.Structures.Select(s => s.StructureId + s.DefinitionId + s.Name)));
            var count = a.Structures.Count;
            Assert.That(StructureGenesis.Ensure(a), Is.Zero);
            Assert.That(a.Structures.Count, Is.EqualTo(count));
            var c = Phase2Worlds.Generated(31338);
            Assert.That(c.Structures.Select(s => s.StructureId).Intersect(a.Structures.Select(s => s.StructureId)), Is.Empty, "Another world has its own IDs.");
        }

        [Test]
        public void StructuresOf_ListsFunctionalFirst()
        {
            var w = Phase2Worlds.Generated(424242);
            var s = w.Settlements[0];
            var list = StructureGenesis.Of(w, s.SettlementId);
            var roles = list.Select(x => StructureCatalog.Find(x.DefinitionId).Role).ToList();
            Assert.That(roles, Is.Ordered);
        }
    }
}
