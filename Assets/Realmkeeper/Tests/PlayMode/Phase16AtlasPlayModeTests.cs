using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Atlas;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Presentation;
using Realmkeeper.Hosting;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>Phase 16 helpers shared by the atlas PlayMode tests and captures.</summary>
    internal static class AtlasDriver
    {
        /// <summary>Clicks a named button on the atlas (a submit event, as a player's click would send).</summary>
        public static void Click(RealmkeeperHud hud, string name)
        {
            var b = hud.AtlasRoot.Q<Button>(name);
            Assert.That(b, Is.Not.Null, "No atlas button '" + name + "'. Buttons: " + string.Join(", ", hud.AtlasRoot.Query<Button>().ToList().Select(x => x.name)));
            using (var click = NavigationSubmitEvent.GetPooled())
            {
                click.target = b;
                b.SendEvent(click);
            }
        }

        public static bool Has(RealmkeeperHud hud, string name) => hud.AtlasRoot.Q<Button>(name) != null;

        /// <summary>From the town to the world map, the atlas open.</summary>
        public static IEnumerator ToAtlas(RealmkeeperBootstrap bootstrap, ExplorationController c)
        {
            if (c.IsExploring) Assert.That(c.ReturnToWorldMap().IsSuccess, Is.True, "Back to the world map.");
            for (var i = 0; i < 6; i++) yield return null;
            bootstrap.Hud.ToggleAtlas(true);
            yield return null;
            Assert.That(bootstrap.Hud.AtlasOpen, Is.True);
        }

        /// <summary>The nearest other settlement of the region the player is in.</summary>
        public static AtlasSettlementView Near(RealmkeeperHud hud, WorldSession s)
        {
            var v = hud.AtlasSnapshot;
            var here = v.Settlements.First(x => x.SettlementId == s.GetPlayer().LastSettlementId || x.IsHere);
            return v.Settlements.Where(x => x.RegionId == here.RegionId && x.SettlementId != here.SettlementId)
                .OrderBy(x => (x.X - here.X) * (x.X - here.X) + (x.Z - here.Z) * (x.Z - here.Z)).First();
        }

        /// <summary>A settlement of another charted region (the farthest charted one when asked).</summary>
        public static AtlasSettlementView Beyond(RealmkeeperHud hud, bool far = false)
        {
            var v = hud.AtlasSnapshot;
            var home = v.Regions.First(r => r.RegionId == v.PlayerRegion);
            var target = far ? v.Regions.Where(r => v.Settlements.Any(x => x.RegionId == r.RegionId)).Where(r => r.RegionId != home.RegionId).OrderByDescending(r => System.Math.Abs(r.AtlasX - home.AtlasX) + System.Math.Abs(r.AtlasZ - home.AtlasZ)).First()
                             : v.Regions.Where(r => v.Settlements.Any(x => x.RegionId == r.RegionId)).Where(r => r.RegionId != home.RegionId).OrderBy(r => System.Math.Abs(r.AtlasX - home.AtlasX) + System.Math.Abs(r.AtlasZ - home.AtlasZ)).First();
            return v.Settlements.First(x => x.RegionId == target.RegionId);
        }

        /// <summary>
        /// Travels on from the atlas's strip until the journey ends: outlaws barring the way are paid (or the choice made by
        /// <paramref name="onThreat"/>), halts resumed through the night. Returns when arrived (or after a fortnight's tries).
        /// </summary>
        public static IEnumerator TravelOn(RealmkeeperHud hud, string onThreat = "rk-atlas-threat-pay", List<string> seen = null)
        {
            for (var i = 0; i < 60; i++)
            {
                var j = hud.AtlasSnapshot?.Journey;
                if (j == null || j.Status == JourneyStatus.Arrived) yield break;
                if (j.ThreatPending)
                {
                    seen?.Add("threat");
                    Click(hud, onThreat);
                }
                else if (Has(hud, "rk-atlas-fight-out"))
                {
                    seen?.Add("fight");
                    Click(hud, "rk-atlas-fight-out");
                }
                else if (Has(hud, "rk-atlas-night")) Click(hud, "rk-atlas-night");
                else if (Has(hud, "rk-atlas-travel-on")) Click(hud, "rk-atlas-travel-on");
                else yield break;
                yield return null;
                yield return null;
            }
        }

        /// <summary>The road outlaws made bold (the test's licence: their numbers, through a save edit).</summary>
        public static IEnumerator ArmTheRoads(RealmkeeperRuntime runtime, ExplorationController c, WorldSession s, int count)
        {
            yield return ItemDriver.Give(runtime, c, s, w =>
            {
                var bands = new HashSet<string>();
                foreach (JObject g in (JArray)w["groups"])
                {
                    if ((string)g["kind"] != "Bandits" || string.IsNullOrEmpty((string)g["populationId"])) continue;
                    g["strength"] = count;
                    g["members"] = count;
                    bands.Add((string)g["populationId"]);
                }

                foreach (JObject p in (JArray)w["creaturePopulations"])
                {
                    if (!bands.Contains((string)p["populationId"])) continue;
                    p["count"] = count;
                    if ((int)p["capacity"] < count) p["capacity"] = count;
                }
            });
        }
    }

    /// <summary>
    /// Phase 16 (ADR-0076–0081) through the real scene: the world atlas over six regions, the ways to a settlement read
    /// from the route data, a journey travelled from the atlas to its end in the town, the party shown with the player,
    /// and outlaws on the road met with each choice the strip offers.
    /// </summary>
    public sealed class Phase16AtlasPlayModeTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private InputSettings.BackgroundBehavior _background;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        [UnityTest]
        public IEnumerator TheAtlas_ShowsTheConnectedWorld_AndAJourney_EndsInTheTown()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            var hud = Bootstrap.Hud;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return AtlasDriver.ToAtlas(Bootstrap, c);

            // The connected world: six regions, every settlement and road drawn, layers that switch.
            var v = hud.AtlasSnapshot;
            Assert.That(v.Regions.Count, Is.EqualTo(6));
            Assert.That(v.Routes.Any(r => r.Crossing), Is.True, "Roads cross the borders.");
            foreach (var x in v.Settlements) Assert.That(AtlasDriver.Has(hud, "rk-atlas-mark-settlement:" + x.SettlementId), Is.True, x.Name + " is on the atlas.");
            Assert.That(AtlasDriver.Has(hud, "rk-atlas-mark-player"), Is.True);
            hud.SetAtlasLayer("Settlements", false);
            yield return null;
            Assert.That(AtlasDriver.Has(hud, "rk-atlas-mark-settlement:" + v.Settlements[0].SettlementId), Is.False, "A layer hides what it draws.");
            hud.SetAtlasLayer("Settlements", true);
            yield return null;

            // A region inspected: its name, its people, its influence.
            hud.SelectOnAtlas("region:" + v.PlayerRegion);
            yield return null;
            Assert.That(hud.AtlasRoot.Q("rk-atlas-side").Query<Label>().ToList().Any(l => l.ClassListContains("rk-atlas-faction")), Is.True, "Faction influence in the region.");

            // A settlement chosen: the ways there, from the route data; the best one taken.
            var to = AtlasDriver.Near(hud, s);
            AtlasDriver.Click(hud, "rk-atlas-mark-settlement:" + to.SettlementId);
            yield return null;
            Assert.That(hud.AtlasSelection, Is.EqualTo("settlement:" + to.SettlementId));
            var options = s.GetTravelOptions(to.SettlementId);
            Assert.That(options.Count, Is.GreaterThan(0));
            Assert.That(AtlasDriver.Has(hud, "rk-atlas-go-0"), Is.True, "The best way offered.");
            var before = s.GetAtlas().Clock;
            AtlasDriver.Click(hud, "rk-atlas-go-0");
            yield return null;
            Assert.That(hud.AtlasSnapshot.Journey, Is.Not.Null, "On the road.");
            Assert.That(hud.AtlasSnapshot.Journey.Destination, Is.EqualTo(to.Name));

            // An hour on the road: the clock moves, the journey with it.
            AtlasDriver.Click(hud, "rk-atlas-travel-hour");
            yield return null;
            Assert.That(s.GetAtlas().Clock, Is.Not.EqualTo(before), "The world's clock moved.");

            // On to the end: the journey ends in the town, walking.
            yield return AtlasDriver.TravelOn(hud);
            for (var i = 0; i < 30 && !c.IsExploring; i++) yield return null;
            Assert.That(s.GetPlayer().IsInSettlement, Is.True, "Arrived in the town.");
            Assert.That(s.GetPlayer().SettlementId, Is.EqualTo(to.SettlementId));
            Assert.That(c.IsExploring, Is.True, "The town is entered on arrival.");
        }

        [UnityTest]
        public IEnumerator ThePartyTravels_WithThePlayer_AndTheMapSaysSo()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            var hud = Bootstrap.Hud;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            var ids = new List<NpcId>();
            yield return PartyDriver.Willing(Runtime, c, s, 1, ids);
            yield return PartyDriver.Recruit(c, s, ids[0]);
            yield return AtlasDriver.ToAtlas(Bootstrap, c);
            Assert.That(hud.AtlasSnapshot.Party.Count, Is.EqualTo(1), "The companion is with the player on the map.");
            Assert.That(hud.AtlasRoot.Q<Button>("rk-atlas-mark-player").text, Does.Contain("+ 1"));

            var to = AtlasDriver.Near(hud, s);
            AtlasDriver.Click(hud, "rk-atlas-mark-settlement:" + to.SettlementId);
            yield return null;
            AtlasDriver.Click(hud, "rk-atlas-go-0");
            yield return null;
            yield return AtlasDriver.TravelOn(hud);
            for (var i = 0; i < 30 && !c.IsExploring; i++) yield return null;
            Assert.That(s.GetPlayer().SettlementId, Is.EqualTo(to.SettlementId));
            for (var i = 0; i < 90 && !c.Companions.ContainsKey(ids[0]); i++) yield return null;
            Assert.That(c.Companions.ContainsKey(ids[0]), Is.True, "The companion arrived with the player and walks with them in the new town.");
        }

        [UnityTest]
        public IEnumerator OutlawsOnTheRoad_AreMetWithAChoice_AndTheJourneyGoesOn()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            var hud = Bootstrap.Hud;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return AtlasDriver.ArmTheRoads(Runtime, c, s, 8);
            yield return ItemDriver.Give(Runtime, c, s, w => w["player"]["purse"] = 2000);
            yield return AtlasDriver.ToAtlas(Bootstrap, c);

            var to = AtlasDriver.Beyond(hud, far: true);
            AtlasDriver.Click(hud, "rk-atlas-mark-settlement:" + to.SettlementId);
            yield return null;
            AtlasDriver.Click(hud, "rk-atlas-go-0");
            yield return null;

            // Hour by hour until outlaws bar the way (bold ones, on a long road).
            for (var i = 0; i < 200 && hud.AtlasSnapshot.Journey != null && !hud.AtlasSnapshot.Journey.ThreatPending; i++)
            {
                if (AtlasDriver.Has(hud, "rk-atlas-night")) AtlasDriver.Click(hud, "rk-atlas-night");
                else AtlasDriver.Click(hud, "rk-atlas-travel-hour");
                yield return null;
            }

            var j = hud.AtlasSnapshot.Journey;
            Assume.That(j != null && j.ThreatPending, Is.True, "No outlaws met on this road.");
            Assert.That(AtlasDriver.Has(hud, "rk-atlas-threat-fight"), Is.True);
            Assert.That(AtlasDriver.Has(hud, "rk-atlas-threat-slip"), Is.True);
            Assert.That(AtlasDriver.Has(hud, "rk-atlas-threat-pay"), Is.True);
            var purse = s.GetCharacter().Player.Purse;
            AtlasDriver.Click(hud, "rk-atlas-threat-pay");
            yield return null;
            Assert.That(s.GetCharacter().Player.Purse, Is.EqualTo(purse - j.Toll), "The toll is paid from the purse.");
            Assert.That(hud.AtlasSnapshot.Journey.ThreatPending, Is.False, "Paid, the way is open.");

            // The next band is fought (the fight is the engine's, drawn from the band).
            var seen = new List<string>();
            yield return AtlasDriver.TravelOn(hud, "rk-atlas-threat-fight", seen);
            if (seen.Contains("threat")) Assert.That(seen, Does.Contain("fight"), "Choosing to fight starts a fight.");
            Assert.That(hud.AtlasSnapshot.Journey == null || hud.AtlasSnapshot.Journey.Status != JourneyStatus.Travelling || s.GetPlayer().IsInSettlement, Is.True);
        }

        [UnityTest]
        public IEnumerator EveryAtlasControl_WorksThroughTheWindow()
        {
            var c = Controller;
            var s = Bootstrap.Session;
            var hud = Bootstrap.Hud;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, Runtime, c);
            yield return AtlasDriver.ArmTheRoads(Runtime, c, s, 8);
            yield return ItemDriver.Give(Runtime, c, s, w => w["player"]["purse"] = 2000);
            Assert.That(c.ReturnToWorldMap().IsSuccess, Is.True);
            for (var i = 0; i < 6; i++) yield return null;

            // The nav button opens the atlas; Close closes it.
            ItemDriver.ClickText(hud.Root, "World atlas (A)");
            yield return null;
            Assert.That(hud.AtlasOpen, Is.True, "The nav button opens the atlas.");
            AtlasDriver.Click(hud, "rk-atlas-close");
            yield return null;
            Assert.That(hud.AtlasOpen, Is.False, "Close closes it.");
            ItemDriver.ClickText(hud.Root, "World atlas (A)");
            yield return null;

            // Each layer button switches its layer.
            foreach (var layer in RealmkeeperHud.AtlasLayers)
            {
                AtlasDriver.Click(hud, "rk-atlas-layer-" + layer);
                yield return null;
                Assert.That(hud.AtlasLayersOn.Contains(layer), Is.False, layer + " off.");
                AtlasDriver.Click(hud, "rk-atlas-layer-" + layer);
                yield return null;
                Assert.That(hud.AtlasLayersOn.Contains(layer), Is.True, layer + " on.");
            }

            // A region mark, a settlement mark and a road from the settlement's inspector.
            AtlasDriver.Click(hud, "rk-atlas-mark-region:" + hud.AtlasSnapshot.PlayerRegion);
            yield return null;
            Assert.That(hud.AtlasSelection, Is.EqualTo("region:" + hud.AtlasSnapshot.PlayerRegion));
            var here = hud.AtlasSnapshot.Settlements.First(x => x.IsHere);
            AtlasDriver.Click(hud, "rk-atlas-mark-settlement:" + here.SettlementId);
            yield return null;
            var road = hud.AtlasRoot.Query<Button>().ToList().First(b => b.name.StartsWith("rk-atlas-road-"));
            AtlasDriver.Click(hud, road.name);
            yield return null;
            Assert.That(hud.AtlasSelection, Does.StartWith("route:"), "A road in the inspector selects the road.");

            // An alternative way to a town beyond the border.
            var to = AtlasDriver.Beyond(hud, far: true);
            AtlasDriver.Click(hud, "rk-atlas-mark-settlement:" + to.SettlementId);
            yield return null;
            Assume.That(AtlasDriver.Has(hud, "rk-atlas-go-1"), Is.True, "Only one way there.");
            AtlasDriver.Click(hud, "rk-atlas-go-1");
            yield return null;
            Assert.That(hud.AtlasSnapshot.Journey, Is.Not.Null);

            // Halt and resume.
            AtlasDriver.Click(hud, "rk-atlas-travel-hour");
            yield return null;
            if (!hud.AtlasSnapshot.Journey.ThreatPending)
            {
                AtlasDriver.Click(hud, "rk-atlas-halt");
                yield return null;
                Assert.That(hud.AtlasSnapshot.Journey.Status, Is.EqualTo(JourneyStatus.Halted));
                AtlasDriver.Click(hud, "rk-atlas-resume");
                yield return null;
                Assert.That(hud.AtlasSnapshot.Journey.Status, Is.EqualTo(JourneyStatus.Travelling));
            }

            // Outlaws: slip past them; the next ones, fight and withdraw.
            var slipped = false;
            var withdrew = false;
            for (var i = 0; i < 300 && hud.AtlasSnapshot.Journey != null && hud.AtlasSnapshot.Journey.Status != JourneyStatus.Arrived && !(slipped && withdrew); i++)
            {
                var j = hud.AtlasSnapshot.Journey;
                if (j.ThreatPending && !slipped)
                {
                    AtlasDriver.Click(hud, "rk-atlas-threat-slip");
                    slipped = true;
                }
                else if (j.ThreatPending) AtlasDriver.Click(hud, "rk-atlas-threat-fight");
                else if (s.GetRoadFight() != null)
                {
                    // Phase 17 (ADR-0086): the fight opens in the combat scene by itself, and the outlaws now act. The atlas's
                    // two fight controls are still there: Withdraw (refused, by the existing rule, while an outlaw stands next
                    // to the player) and the labelled "Let it play out". Closing the panel puts the party back on the road.
                    for (var k = 0; k < 120 && !c.InFight; k++) yield return null;
                    Assert.That(c.InFight, Is.True, "The fight opened in the combat scene.");
                    c.AutoAi = false;
                    hud.ToggleAtlas(true);
                    yield return null;
                    var withdraw = hud.AtlasRoot.Q<Button>("rk-atlas-withdraw");
                    Assert.That(withdraw, Is.Not.Null, "The atlas offers Withdraw during a road fight.");
                    AtlasDriver.Click(hud, "rk-atlas-withdraw");
                    yield return null;
                    if (s.GetEncounter() != null && s.GetEncounter().Status == Realmkeeper.Domain.Combat.EncounterStatus.Active)
                    {
                        AtlasDriver.Click(hud, "rk-atlas-fight-out"); // the explicit, labelled choice
                        yield return null;
                    }

                    for (var k = 0; k < 10; k++) yield return null;
                    Assert.That(s.GetEncounter() == null || s.GetEncounter().Status != Realmkeeper.Domain.Combat.EncounterStatus.Active, Is.True, "The fight is over (withdrawn, or played out).");
                    if (s.GetCharacter().IsDead) Assert.Inconclusive("The hero fell.");
                    LevelUpDriver.Click(c.Combat.Root, "xk-combat-close");
                    for (var k = 0; k < 40 && c.IsExploring; k++) yield return null;
                    Assert.That(s.GetRoadFight(), Is.Null, "Back on the road.");
                    hud.ToggleAtlas(true);
                    withdrew = true;
                }
                else if (AtlasDriver.Has(hud, "rk-atlas-night")) AtlasDriver.Click(hud, "rk-atlas-night");
                else if (AtlasDriver.Has(hud, "rk-atlas-travel-hour")) AtlasDriver.Click(hud, "rk-atlas-travel-hour");
                else break;
                yield return null;
            }

            Assert.That(slipped, Is.True, "Outlaws met and slipped past (or the attempt failed into a fight).");

            // Give up the journey: the party stands on the road.
            if (hud.AtlasSnapshot.Journey != null && hud.AtlasSnapshot.Journey.Status != JourneyStatus.Arrived && AtlasDriver.Has(hud, "rk-atlas-abandon"))
            {
                AtlasDriver.Click(hud, "rk-atlas-abandon");
                yield return null;
                Assert.That(hud.AtlasSnapshot.Journey.Status, Is.EqualTo(JourneyStatus.Abandoned));
                Assert.That(s.GetPlayer().IsInSettlement, Is.False, "Out on the road.");
            }
        }
    }
}
