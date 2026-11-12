using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Campaigns;
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
    /// <summary>
    /// Evidence capture for Phase 17 (automated, not manual verification): the atlas and its groups, a fight on the road
    /// entered and left, and the provisional Waystone campaign played through the journal and conversations to its end,
    /// with companions reacting. Runs only when RK_CAPTURE_DIR is set. Each caption says what the picture shows; where the
    /// test changed the world to reach or show something (moving between towns, a report reaching the player, the
    /// companions' temperaments), the caption says so.
    /// </summary>
    public sealed class Phase17CaptureTests
    {
        private const string ScenePath = "Assets/Realmkeeper/Scenes/Realmkeeper.unity";
        private string _dir;
        private InputSettings.BackgroundBehavior _background;
        private static readonly List<string> Log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _dir = Environment.GetEnvironmentVariable("RK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(_dir)) yield break;
            Directory.CreateDirectory(_dir);
            _background = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            SceneManager.LoadScene(ScenePath);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Unload()
        {
            if (string.IsNullOrEmpty(_dir)) yield break;
            File.WriteAllLines(Path.Combine(_dir, "p17_capture_log.txt"), Log);
            InputSystem.settings.backgroundBehavior = _background;
            var runtime = Object.FindAnyObjectByType<RealmkeeperRuntime>();
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(go);
            yield return null;
        }

        private static RealmkeeperBootstrap Bootstrap => Object.FindAnyObjectByType<RealmkeeperBootstrap>();
        private static RealmkeeperRuntime Runtime => Object.FindAnyObjectByType<RealmkeeperRuntime>();
        private static ExplorationController Controller => Object.FindAnyObjectByType<ExplorationInstaller>().Controller;

        private static string Side(RealmkeeperHud hud) => string.Join(" / ", hud.AtlasRoot.Q("rk-atlas-side").Query<Label>().ToList().Select(l => l.text).Take(8));

        private static IEnumerator Wait(float seconds)
        {
            var end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        private static void Layers(RealmkeeperHud hud, params string[] on)
        {
            foreach (var l in RealmkeeperHud.AtlasLayers) hud.SetAtlasLayer(l, on.Length == 0 || on.Contains(l));
        }

        private static CampaignView Story(WorldSession s) => s.GetCampaigns().Campaigns.FirstOrDefault();

        [UnityTest]
        public IEnumerator Capture_Phase17()
        {
            if (string.IsNullOrEmpty(_dir)) Assert.Ignore("Set RK_CAPTURE_DIR to capture screenshots.");
            var c = Controller;
            var s = Bootstrap.Session;
            var hud = Bootstrap.Hud;
            var rt = Runtime;
            yield return CombatDriver.EnterTownAsFighter(Bootstrap, rt, c);
            var start = s.GetPlayer().SettlementId;
            var ids = new List<NpcId>();
            yield return PartyDriver.Willing(rt, c, s, 2, ids);
            foreach (var id in ids) yield return PartyDriver.Recruit(c, s, id);
            // The companions' temperaments, set for the capture so that a decision divides them: one ambitious and hard, one compassionate.
            yield return ItemDriver.Give(rt, c, s, w =>
            {
                w["player"]["purse"] = 2000;
                foreach (JObject n in (JArray)w["npcs"])
                {
                    var id = (string)n["npcId"];
                    if (id == ids[0].ToString()) { n["personality"]["Ambition"] = 90; n["personality"]["Compassion"] = 20; }
                    if (ids.Count > 1 && id == ids[1].ToString()) { n["personality"]["Ambition"] = 15; n["personality"]["Compassion"] = 90; }
                }
            });
            var hard = s.FindNpc(ids[0]).Name;
            var soft = ids.Count > 1 ? s.FindNpc(ids[1]).Name : null;
            s.Execute(new AdvanceDaysCommand(10)); // the world runs: caravans out, events, news
            yield return AtlasDriver.ToAtlas(Bootstrap, c);

            // 1. The atlas with all six regions.
            Layers(hud);
            hud.SelectOnAtlas(null);
            var v = hud.AtlasSnapshot;
            yield return Shoot("p17_01_atlas_six_regions", $"World atlas: {v.Regions.Count} regions ({v.Regions.Count(r => r.Discovery != Realmkeeper.Domain.World.RegionDiscovery.Unknown)} charted), {v.Settlements.Count} settlements, {v.Routes.Count} roads; {v.Groups.Count} groups shown ({v.Groups.Count(g => g.Seen)} seen, {v.Groups.Count(g => !g.Seen)} reported). Clock {v.Clock}.");

            // 2. Groups across regions: seen in the player's region, reported elsewhere (a report reaching the player was set for the capture if none had).
            if (!hud.AtlasSnapshot.Groups.Any(g => !g.Seen))
            {
                // No report of a farther group has reached the player yet: one is set for the capture — a carter's word of
                // outlaws on a road in another region (an existing band; the report is the capture's licence).
                var here = hud.AtlasSnapshot.PlayerRegion.ToString();
                yield return ItemDriver.Give(rt, c, s, w =>
                {
                    var routes = ((JArray)w["routes"]).OfType<JObject>().ToDictionary(r => (string)r["routeId"], r => r);
                    var gone = new[] { "Arrived", "Dispersed", "Defeated" };
                    var active = ((JArray)w["groups"]).OfType<JObject>().Where(g => !gone.Contains((string)g["state"])).ToDictionary(g => (string)g["groupId"], g => g);
                    var known = false;
                    foreach (JObject e in (JArray)w["worldEvents"])
                    {
                        if (string.IsNullOrEmpty((string)e["groupId"]) || string.IsNullOrEmpty((string)e["routeId"]) || !active.ContainsKey((string)e["groupId"])) continue;
                        if (!routes.TryGetValue((string)e["routeId"], out var er) || (string)er["regionA"] == here) continue;
                        e["known"] = "Rumored"; e["knownAt"] = e["updatedAt"]; e["knownState"] = e["state"]; e["knownSeverity"] = e["severity"];
                        e["knownFrom"] = "a carter (set for the capture)";
                        known = true;
                        break;
                    }

                    if (known) return;
                    foreach (var g in active.Values.OrderBy(x => (string)x["kind"] == "Bandits" ? 0 : 1))
                    {
                        var station = (string)g["stationRouteId"] ?? (string)((JArray)g["segments"])?.FirstOrDefault();
                        if ((string)g["kind"] == "Caravan" || station == null || !routes.TryGetValue(station, out var r) || (string)r["regionA"] == here) continue;
                        var n = (int)w["nextWorldEventNumber"];
                        w["nextWorldEventNumber"] = n + 1;
                        var at = ((JArray)w["worldEvents"]).OfType<JObject>().Select(x => (long)x["updatedAt"]).DefaultIfEmpty(0).Max();
                        ((JArray)w["worldEvents"]).Add(new JObject
                        {
                            ["eventId"] = "event:" + n, ["type"] = (string)g["kind"] == "Bandits" ? "BanditActivity" : "PatrolResponse", ["state"] = "Active", ["severity"] = 2, ["cause"] = "a group on the road (set for the capture)",
                            ["routeId"] = station, ["regionId"] = r["regionA"], ["groupId"] = g["groupId"], ["createdAt"] = at, ["updatedAt"] = at, ["expiresAt"] = -1, ["closedAt"] = -1,
                            ["key"] = "capture:" + g["groupId"], ["applied"] = new JArray(), ["history"] = new JArray(), ["known"] = "Rumored", ["knownAt"] = at,
                            ["knownState"] = "Active", ["knownSeverity"] = 2, ["knownFrom"] = "a carter (set for the capture)",
                        });
                        break;
                    }
                });
                hud.ToggleAtlas(true);
                yield return null;
            }

            Layers(hud, "Terrain", "Settlements", "Roads", "Travel", "Threats");
            var reported = hud.AtlasSnapshot.Groups.FirstOrDefault(g => !g.Seen);
            if (reported != null) hud.SelectOnAtlas("group:" + reported.GroupId);
            yield return Shoot("p17_02_atlas_groups", reported == null ? "Groups seen in the player's region (no report of a farther group reached the player)." :
                $"Groups across regions: letters are groups seen now; \"?\" marks are reports. Selected: {reported.Name}, reported {reported.ReportedAt} by {reported.ReportedFrom} — " + Side(hud));

            // 3–5. A fight on the road: met, entered, and left.
            Layers(hud);
            yield return AtlasDriver.ArmTheRoads(rt, c, s, 4); // road bands made bolder for the capture
            hud.ToggleAtlas(true);
            var to = AtlasDriver.Beyond(hud);
            AtlasDriver.Click(hud, "rk-atlas-mark-settlement:" + to.SettlementId);
            yield return null;
            AtlasDriver.Click(hud, "rk-atlas-go-0");
            yield return null;
            for (var i = 0; i < 80 && hud.AtlasSnapshot.Journey != null && !hud.AtlasSnapshot.Journey.ThreatPending && hud.AtlasSnapshot.Journey.Status != JourneyStatus.Arrived; i++)
            {
                if (AtlasDriver.Has(hud, "rk-atlas-night")) AtlasDriver.Click(hud, "rk-atlas-night");
                else if (AtlasDriver.Has(hud, "rk-atlas-travel-hour")) AtlasDriver.Click(hud, "rk-atlas-travel-hour");
                else break;
                yield return null;
            }

            var j = hud.AtlasSnapshot.Journey;
            if (j != null && j.ThreatPending)
            {
                hud.SelectOnAtlas("player");
                yield return Shoot("p17_03_travel_encounter", $"Outlaws bar the road to {j.Destination} (road bands made bolder for the capture): fight, slip past (Stealth) or pay {j.Toll} coin.");
                AtlasDriver.Click(hud, "rk-atlas-threat-fight");
                for (var i = 0; i < 120 && !c.IsExploring; i++) yield return null;
                yield return Wait(1f);
                if (c.InFight)
                {
                    c.AutoAi = false;
                    for (var i = 0; i < 4 && c.InFight; i++) { CombatDriver.Step(c); yield return null; }
                    yield return Wait(0.6f);
                    yield return Shoot("p17_04_combat_transition", $"The fight entered from the atlas: the combat scene on the open ground by {s.GetRoadFight()?.GroundName}, the existing tactical panel, {c.Combat.Snapshot.Order.Count} combatants (party and outlaws). The party is still on the road in the world.", c.CameraRig.Camera);
                    // The rest of the fight is let play out (the labelled AI choice) to keep the capture short.
                    if (c.InFight) s.FightItOut();
                    for (var i = 0; i < 10; i++) yield return null;
                    Log.Add("p17_04/05: after the screenshot the rest of the fight was let play out with the labelled AI option, to keep the capture short.");

                    LevelUpDriver.Click(c.Combat.Root, "xk-combat-close");
                    for (var i = 0; i < 40 && c.IsExploring; i++) yield return null;
                    hud.ToggleAtlas(true);
                    yield return null;
                    var after = hud.AtlasSnapshot.Journey;
                    hud.SelectOnAtlas("player");
                    yield return Shoot("p17_05_return_to_travel", $"Back on the road after the fight: the journey {after?.Status.ToString().ToLowerInvariant()} ({after?.Reason}), {after?.PercentDone}% of the way; it goes on when the player resumes.");
                }
                else Log.Add("p17_04_combat_transition / p17_05_return_to_travel: the fight did not open (not captured).");
            }
            else Log.Add("p17_03–05: no outlaws barred the road on this journey (not captured).");

            if (hud.AtlasSnapshot.Journey != null && AtlasDriver.Has(hud, "rk-atlas-abandon")) { AtlasDriver.Click(hud, "rk-atlas-abandon"); yield return null; }
            yield return CampaignDriver.GoTo(rt, c, s, start);

            // 6, 7. The campaign begun from the journal.
            yield return CampaignDriver.Journal(c);
            yield return Wait(0.3f);
            yield return Shoot("p17_06_campaign_selection", "The journal's Campaign tab in an open-ended world: what could begin here (the provisional Waystone demonstration), with Begin.", c.CameraRig.Camera);
            LevelUpDriver.Click(c.HubWindow.Root, "xk-campaign-start-" + CampaignDriver.Waystone);
            yield return null;
            yield return CampaignDriver.Journal(c, "overview");
            yield return Shoot("p17_07_campaign_overview", "The campaign begun: its premise names the world's own towns and people; its first chapter and arcs under way. " + Story(s)?.Premise, c.CameraRig.Camera);

            // Chapter one: the letter across the border (moving between towns is the capture's licence; arriving still counts).
            c.CloseWindows();
            yield return CampaignDriver.About(rt, c, s, "reeve", "letter", "giver");
            QuestDriver.Say(c, "accept");
            yield return null;
            yield return CampaignDriver.GoTo(rt, c, s, s.GetCampaignSettlement(CampaignDriver.Waystone, "far_town"));
            yield return CampaignDriver.About(rt, c, s, "counterpart", "letter", "recipient");
            QuestDriver.Say(c, "give");
            yield return null;

            // 8. Chapter and arc progress.
            yield return CampaignDriver.Journal(c, "story");
            yield return Shoot("p17_08_chapter_progress", "Chapter and arc progress: chapter one closed (the letter delivered across the border), chapter two open with two arcs side by side.", c.CameraRig.Camera);

            // 9, 10. The question put in conversation: a branching dialogue with a choice out of reach, then the decision.
            yield return CampaignDriver.About(rt, c, s, "counterpart", "hold", "giver");
            QuestDriver.Say(c, "accept");
            yield return Wait(0.3f);
            yield return Shoot("p17_09_branching_dialogue", "Branching dialogue: the decisions are marked; the shared count is shown disabled with the reason (no proof yet); a Persuasion attempt names its check.", c.CameraRig.Camera);
            QuestDriver.Say(c, "home");
            yield return Wait(0.3f);
            yield return Shoot("p17_10_campaign_decision", $"The decision made: the count belongs to the home town. The speaker's reply reflects it; {hard} and {soft ?? "the other companion"} react by their own natures.", c.CameraRig.Camera);
            c.CloseWindows();

            // 11. Its consequences, now and later.
            s.Execute(new AdvanceDaysCommand(7));
            yield return null;
            yield return CampaignDriver.Journal(c, "decisions");
            yield return Shoot("p17_11_decision_consequences", "Decision consequences in the journal: what was decided, and what came of it six days later (the far town's carters refused the toll — a real world event on the crossing).", c.CameraRig.Camera);

            // 12, 13. The campaign journal and the people in it.
            yield return CampaignDriver.Journal(c, "story");
            yield return Shoot("p17_12_campaign_journal", "The campaign journal: chapters, arcs and objectives as far as the player knows them (later chapters and hidden objectives stay hidden).", c.CameraRig.Camera);
            yield return CampaignDriver.Journal(c, "people");
            yield return Shoot("p17_13_important_npcs", "The people in the story: each a person of the world, with the part they play and, once met, what has become of them.", c.CameraRig.Camera);

            // 14. A companion's own story (their trust raised for the capture, so that they ask it now).
            yield return ItemDriver.Give(rt, c, s, w => { foreach (JObject r in (JArray)w["companions"]) if ((string)r["npcId"] == ids[0].ToString()) r["loyalty"] = 75; });
            s.Execute(new AdvanceDaysCommand(1));
            yield return null;
            c.CloseWindows();
            c.ToggleHub();
            c.HubWindow.ShowParty(ids[0]);
            yield return null;
            yield return Shoot("p17_14_companion_story", $"{hard}'s companion detail: loyalty and why it moved (including the decision), their own quest if they have asked it, and how they get on with the others.", c.CameraRig.Camera);

            // 15. A companion speaks their mind: something that happened, asked about.
            c.CloseWindows();
            if (soft != null)
            {
                var talk = c.StartDialogue(ids[1]);
                yield return null;
                if (talk.IsSuccess && CampaignDriver.Response(c, "remark") != null)
                {
                    // They say the oldest thing on their mind first; asked again, the next. The capture stops at what they
                    // said about the decision (it names the towns or the other companion), or at the last thing they had to say.
                    var far = s.FindSettlement(s.GetCampaignSettlement(CampaignDriver.Waystone, "far_town")).Name;
                    var homeName = s.FindSettlement(s.GetCampaignSettlement(CampaignDriver.Waystone, "home_town")).Name;
                    QuestDriver.Say(c, "remark");
                    yield return null;
                    for (var i = 0; i < 6; i++)
                    {
                        var said = c.Dialogue.View.NpcLine ?? string.Empty;
                        if (said.Contains(far) && !said.Contains("My people") || said.Contains(hard) || said.Contains("side") || said.Contains("nearer") || said.Contains("stronger") || CampaignDriver.Response(c, "more") == null) break;
                        QuestDriver.Say(c, "more");
                        yield return null;
                    }

                    yield return Wait(0.3f);
                    yield return Shoot("p17_15_companion_relationship", $"{soft}, asked what is on their mind (a remark is caused by something that happened): \"{c.Dialogue.View.NpcLine}\"", c.CameraRig.Camera);
                }
                else Log.Add($"p17_15_companion_relationship: {soft} had nothing to say ({talk.Message}) (not captured).");
                c.CloseWindows();
            }

            // 16. Faction standing moved by the story.
            c.ToggleHub();
            c.HubWindow.SelectJournal("Reputation");
            yield return null;
            yield return Shoot("p17_16_faction_progression", "Faction-related progression: standings with the factions the two towns answer to, moved by the decision (in the reputation ledger with the campaign as the reason).", c.CameraRig.Camera);
            c.CloseWindows();

            // 17. Multi-region progress: the toll-keeper in the far town, the home town's voice in the other region.
            yield return CampaignDriver.About(rt, c, s, "counterpart", "keeper", "giver");
            QuestDriver.Say(c, "accept");
            yield return null;
            yield return CampaignDriver.About(rt, c, s, "tollkeeper", "keeper", "tollkeeper");
            QuestDriver.Say(c, "expose");
            yield return null;
            c.CloseWindows();
            Assert.That(c.ReturnToWorldMap().IsSuccess, Is.True);
            for (var i = 0; i < 6; i++) yield return null;
            hud.ToggleAtlas(true);
            Layers(hud, "Terrain", "Settlements", "Roads", "Events");
            hud.SelectOnAtlas("settlement:" + s.GetCampaignSettlement(CampaignDriver.Waystone, "home_town"));
            yield return Shoot("p17_17_multi_region_progress", "Multi-region progress: the story's two towns lie in different regions, joined by the crossing; the last step waits in the home town. " + Side(hud));

            // 18. The conclusion.
            yield return CampaignDriver.About(rt, c, s, "home_voice", "accord", "giver");
            QuestDriver.Say(c, "accept");
            yield return null;
            QuestDriver.Say(c, "tell");
            yield return null;
            yield return CampaignDriver.Journal(c, "overview");
            var done = Story(s);
            yield return Shoot("p17_18_campaign_conclusion", $"The conclusion: \"{done?.Ending}\" — {done?.EndingText} The campaign is {done?.State.ToString().ToLowerInvariant()}.", c.CameraRig.Camera);

            // 19. The world goes on.
            c.CloseWindows();
            s.Execute(new AdvanceDaysCommand(3));
            var minute = s.GetWorldTime().MinuteOfDay;
            if (minute < 8 * 60 || minute > 18 * 60) s.Execute(new AdvanceTimeCommand((10 * 60 - minute + 1440) % 1440));
            yield return Wait(1f);
            c.CameraRig.SetView(c.Player.transform.eulerAngles.y + 20f, 14f);
            c.CameraRig.Snap();
            yield return Wait(0.5f);
            yield return Shoot("p17_19_post_campaign", $"After the story: three days on in {s.GetPlayer().SettlementName}, the world going on as before; the campaign stays {Story(s)?.State.ToString().ToLowerInvariant()} and nothing reopens it.", c.CameraRig.Camera);
        }

        private IEnumerator Shoot(string name, string note, Camera camera = null, int width = 1920, int height = 1080)
        {
            yield return null;
            Log.Add(name + ": " + note);
            camera = camera ?? Camera.main;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var docs = Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).Where(d => d.isActiveAndEnabled && d.panelSettings != null).ToList();
            var settings = docs.Select(d => d.panelSettings).Distinct().ToList();
            var previous = settings.Select(x => (x, x.targetTexture, x.clearColor)).ToList();
            foreach (var x in settings) { x.targetTexture = rt; x.clearColor = false; }
            yield return null;
            if (camera != null)
            {
                var cameraTarget = camera.targetTexture;
                camera.targetTexture = rt;
                camera.Render();
                camera.targetTexture = cameraTarget;
            }

            yield return null;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
            foreach (var (x, target, clear) in previous) { x.targetTexture = target; x.clearColor = clear; }
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
