using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.World;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>Phase 2: the HUD shows the simulated economy, trade and chronicle.</summary>
    public sealed class EconomyHudTests : RealmkeeperPlayModeFixture
    {
        private const string TestSlot = "playmode-economy-test";

        private SettlementSummary Town => Bootstrap.Session.GetSettlementsInView().First(s => s.SettlementType == SettlementType.Town);

        private IEnumerator SelectTown()
        {
            Bootstrap.Selection.Select(Town.SettlementId);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Inspector_ShowsOneEconomyRowPerResource_WithSimulatedValues()
        {
            yield return SelectTown();

            var rows = Bootstrap.Hud.EconomyTable.Children().ToList();
            Assert.That(rows.Count, Is.EqualTo(ResourceTypes.All.Count));
            var foodCells = rows[0].Children().OfType<Label>().ToList();
            Assert.That(foodCells[0].text, Is.EqualTo("Food"));
            Assert.That(foodCells[2].text, Is.EqualTo("+" + Town.Food.Produced.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)),
                "The 'Made' column is the simulation's ledger value.");
            Assert.That(Bootstrap.Hud.InspectorStatus.text, Is.Not.Empty);
            Assert.That(Bootstrap.Hud.Root.Q<Label>("insp-type").text, Does.Contain(Town.Specialization.ToString()));
        }

        [UnityTest]
        public IEnumerator AdvanceMonthButton_Advances30Days_AndTradeMovesGoods()
        {
            var start = Bootstrap.Session.GetOverview().Date;

            Click(Bootstrap.Hud.AdvanceMonthButton);
            yield return null;

            Assert.That(Bootstrap.Session.GetOverview().Date, Is.EqualTo(start.AddDays(WorldDate.DaysPerMonth)));
            var routes = Bootstrap.Session.GetTradeRoutes();
            Assert.That(routes.Sum(r => r.ShipmentsDelivered), Is.GreaterThan(0), "Goods were delivered along the roads during the month.");
            Assert.That(routes.SelectMany(r => r.Delivered).Sum(d => d.Quantity), Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator Inspector_ListsTheSelectedSettlementsTradeRoutes()
        {
            Bootstrap.Session.Execute(new AdvanceDaysCommand(20));
            yield return SelectTown();

            var expected = Bootstrap.Session.GetTradeRoutes(Town.SettlementId).Count;
            var crossings = Bootstrap.Session.GetTradeRoutes(Town.SettlementId).Count(r => Bootstrap.Session.FindSettlement(r.OriginId).RegionId != Bootstrap.Session.FindSettlement(r.DestinationId).RegionId);
            Assert.That(expected - crossings, Is.EqualTo(4), "The town has a route to and from each of the other two settlements of its vale (and more across a border, Phase 16).");
            // Each route line sits in a row with its Suspend/Resume toggle, so look at every depth.
            var lines = Bootstrap.Hud.TradeList.Query<Label>(className: "rk-trade-line").ToList().Select(l => l.text).ToList();
            Assert.That(Bootstrap.Hud.TradeList.Query<Button>(className: "rk-route-toggle").ToList().Count, Is.EqualTo(expected), "One toggle per route.");
            Assert.That(lines.Count, Is.EqualTo(expected + 1), "One line per route plus the trade activity line.");
            foreach (var other in Bootstrap.Session.GetSettlementsInView().Where(s => s.SettlementId != Town.SettlementId))
                Assert.That(lines.Any(l => l.Contains(other.Name)), Is.True, other.Name);
        }

        [UnityTest]
        public IEnumerator RecentEvents_ShowTheChroniclesFormattedEntries()
        {
            Bootstrap.Session.Execute(new AdvanceDaysCommand(90));
            yield return null;

            var expected = Bootstrap.Session.GetRecentEvents(RealmkeeperHud.EventCount).Select(e => e.Summary).ToList();
            var shown = Bootstrap.Hud.EventList.Query<Label>(className: "rk-event-text").ToList().Select(l => l.text).ToList();
            Assert.That(expected.Count, Is.GreaterThan(3));
            Assert.That(shown, Is.EqualTo(expected), "The panel shows simulation facts formatted by the application layer.");
        }

        [UnityTest]
        public IEnumerator ShortageWarnings_AppearForASettlementThatRunsShort()
        {
            // Seed 42's town runs short of some goods within months (see TestResults.md, balance runs).
            Assert.That(Bootstrap.Session.Execute(new CreateWorldCommand("Shortland", 42)).IsSuccess, Is.True);
            Bootstrap.Session.Execute(new AdvanceDaysCommand(120));
            yield return null;

            var short_ = Bootstrap.Session.GetSettlementsInView().FirstOrDefault(s => s.Economy.Any(r => r.InShortage));
            Assert.That(short_, Is.Not.Null, "Fixture: some settlement is short of something.");
            Bootstrap.Selection.Select(short_.SettlementId);
            yield return null;

            Assert.That(Bootstrap.Hud.WarningList.childCount, Is.GreaterThan(0));
            Assert.That(Bootstrap.Hud.EconomyTable.Children().Count(r => r.ClassListContains("rk-table-row--short")),
                Is.EqualTo(short_.Economy.Count(r => r.InShortage)));
        }

        [UnityTest]
        public IEnumerator SaveAndLoad_RestoreTheWorldAndTheChronicleShownInTheHud()
        {
            var path = Path.Combine(UnityEngine.Application.persistentDataPath, "Saves", TestSlot + ".rkworld.json");
            try
            {
                Bootstrap.Session.Execute(new AdvanceDaysCommand(40));
                Assert.That(Bootstrap.Session.Execute(new SaveWorldCommand(TestSlot)).IsSuccess, Is.True);
                yield return null;
                var savedDate = Bootstrap.Session.GetOverview().Date;
                var savedEvents = Bootstrap.Hud.EventList.Query<Label>(className: "rk-event-text").ToList().Select(l => l.text).ToList();
                var savedTown = Town;

                Bootstrap.Session.Execute(new AdvanceDaysCommand(30));
                yield return null;
                Assert.That(Bootstrap.Session.GetOverview().Date, Is.Not.EqualTo(savedDate));

                Assert.That(Bootstrap.Session.Execute(new LoadWorldCommand(TestSlot)).IsSuccess, Is.True);
                yield return null;

                Assert.That(Bootstrap.Session.GetOverview().Date, Is.EqualTo(savedDate));
                Assert.That(Bootstrap.Hud.DateLabel.text, Is.EqualTo(savedDate.ToString()));
                Assert.That(Bootstrap.Hud.EventList.Query<Label>(className: "rk-event-text").ToList().Select(l => l.text), Is.EqualTo(savedEvents));
                Assert.That(Town.Economy.Select(r => r.Stock), Is.EqualTo(savedTown.Economy.Select(r => r.Stock)));
                Assert.That(Bootstrap.WorldView.Settlements.Count, Is.EqualTo(3), "Visuals rebuilt for the loaded world.");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
