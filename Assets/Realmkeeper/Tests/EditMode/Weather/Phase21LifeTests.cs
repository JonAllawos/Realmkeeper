using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Npcs;
using Realmkeeper.Tests.EditMode.Atlas;

namespace Realmkeeper.Tests.EditMode.Weather
{
    /// <summary>Phase 21 (ADR-0108): settlement life — people run errands to their town's shops on free days.</summary>
    public sealed class Phase21LifeTests : Phase16Fixture
    {
        [Test]
        public void On_free_days_people_go_to_their_towns_shops_within_opening_hours()
        {
            var w = W;
            var errands = 0;
            var start = w.CurrentSimulationDate.ToDayNumber();
            foreach (var npc in w.Npcs.Where(n => !n.IsDeceased && n.Role != NpcRole.Guard))
            for (var d = 0; d < 14; d++)
            {
                var date = Realmkeeper.Domain.Calendar.WorldDate.FromDayNumber(start + d);
                var (shop, at) = NpcActivityPlan.ErrandAt(w, npc, date, false);
                if (shop == null) continue;
                errands++;
                Assert.That(shop.SettlementId, Is.EqualTo(npc.HomeSettlementId), "their own town's shop");
                Assert.That(shop.ShopkeeperNpcId, Is.Not.EqualTo(npc.NpcId), "not their own");
                Assert.That(at, Is.GreaterThanOrEqualTo(shop.OpensAt));
                Assert.That(at + 60, Is.LessThanOrEqualTo(shop.ClosesAt));
                Assert.That(NpcActivityPlan.ErrandAt(w, npc, date, false), Is.EqualTo((shop, at)), "the same day gives the same errand");
                Assert.That(NpcActivityPlan.ErrandAt(w, npc, date, true).shop, Is.Null, "bad weather keeps them in");
            }

            Assert.That(errands, Is.GreaterThan(20), "the towns' people shop");
        }

        [Test]
        public void An_errand_puts_someone_in_the_shop_trading()
        {
            var w = W;
            foreach (var npc in w.Npcs.Where(n => !n.IsDeceased && n.Role != NpcRole.Guard && !n.ResidenceId.IsEmpty))
            {
                npc.CurrentLocationId = npc.ResidenceId; // a day at home
                npc.Goals.Clear();
                var (shop, at) = NpcActivityPlan.ErrandAt(w, npc, w.CurrentSimulationDate, false);
                if (shop == null) continue;
                foreach (var region in w.Environment.Regions) foreach (var p in region.Periods) p.Kind = WeatherKind.Clear;
                var now = NpcActivityPlan.At(w, npc, w.CurrentSimulationDate, at + 10);
                if (now.Location == npc.ResidenceId && now.Reason != "At home today.") continue; // another part of their day came first
                Assert.That(now.Location, Is.EqualTo(shop.LocationId), now.Reason);
                Assert.That(now.Activity, Is.EqualTo(ActivityKind.Trading));
                Assert.That(now.Reason, Does.Contain(shop.Name));
                return;
            }

            Assert.Inconclusive("No one had an errand today in this world.");
        }
    }
}
