using System.Collections;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>Phase 2 completion on screen: explained ratings, food trend, route toggles and map markers.</summary>
    public sealed class Phase2HudPlayModeTests : RealmkeeperPlayModeFixture
    {
        private static int Bits(SettlementIndicators flags)
        {
            var n = 0;
            for (var v = (int)flags; v != 0; v &= v - 1) n++;
            return n;
        }

        [UnityTest]
        public IEnumerator Inspector_ExplainsRatings_AndShowsTheFoodTrend()
        {
            Bootstrap.Session.Execute(new AdvanceDaysCommand(20));
            var s = Bootstrap.Session.GetSettlements().First();
            Bootstrap.Selection.Select(s.SettlementId);
            yield return null;

            var root = Bootstrap.Hud.Root;
            StringAssert.StartsWith("Why: ", root.Q<Label>("insp-prosperity-why").text);
            StringAssert.StartsWith("Why: ", root.Q<Label>("insp-stability-why").text);
            StringAssert.Contains("Food", root.Q<Label>("insp-stability-why").text, "Food is always a stability factor.");
            Assert.That(root.Q<Label>("insp-food-trend").text, Does.StartWith("rising").Or.StartWith("falling").Or.StartWith("steady"));
            Assert.That(root.Q<Label>("insp-type").text, Does.Not.Contain("None"), "No specialization reads as Generalist.");
        }

        [UnityTest]
        public IEnumerator RouteToggle_SuspendsAndResumesThroughTheRegistry()
        {
            var s = Bootstrap.Session.GetSettlements().First();
            Bootstrap.Selection.Select(s.SettlementId);
            yield return null;
            var route = Bootstrap.Session.GetTradeRoutes(s.SettlementId).First();
            var toggle = Bootstrap.Hud.TradeList.Q<Button>("route-toggle-" + route.TradeRouteId);
            Assert.That(toggle, Is.Not.Null);
            Assert.That(toggle.text, Is.EqualTo("Suspend"));

            Click(toggle);
            yield return null;
            Assert.That(Bootstrap.Session.GetTradeRoutes(s.SettlementId).Single(r => r.TradeRouteId == route.TradeRouteId).IsSuspended, Is.True);
            toggle = Bootstrap.Hud.TradeList.Q<Button>("route-toggle-" + route.TradeRouteId);
            Assert.That(toggle.text, Is.EqualTo("Resume"));
            StringAssert.Contains("suspended", Bootstrap.Hud.MessageLabel.text);

            Click(toggle);
            yield return null;
            Assert.That(Bootstrap.Session.GetTradeRoutes(s.SettlementId).Single(r => r.TradeRouteId == route.TradeRouteId).IsSuspended, Is.False);
        }

        [UnityTest]
        public IEnumerator MapMarkers_MatchTheSimulationEveryDay()
        {
            for (var day = 0; day < 15; day++)
            {
                Bootstrap.Session.Execute(new AdvanceDayCommand());
                yield return null;
                var expected = 0;
                foreach (var s in Bootstrap.Session.GetSettlements())
                {
                    Assert.That(Bootstrap.Indicators.ShownFor(s.SettlementId), Is.EqualTo(s.Indicators), $"Day {day + 1}, {s.Name}");
                    expected += Bits(s.Indicators);
                }

                Assert.That(Bootstrap.Indicators.MarkerCount, Is.EqualTo(expected));
            }

            Assert.That(Bootstrap.Indicators.GetComponentsInChildren<Collider>(), Is.Empty, "Markers never block selection.");
        }
    }
}
