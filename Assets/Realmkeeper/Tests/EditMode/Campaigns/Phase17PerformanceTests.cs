using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Campaigns;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Campaigns;

namespace Realmkeeper.Tests.EditMode.Campaigns
{
    /// <summary>
    /// Phase 17: measured costs of the campaign layer on the connected world — re-reading a campaign (it is event-driven:
    /// these are the costs of the re-reads that happen), a decision with its consequences, the dialogue's condition
    /// checks, the day's story pass, a simulated day with a campaign under way against one without, and saving and
    /// loading with a campaign. Generous bounds catch pathological regressions; numbers go to the test output.
    /// </summary>
    public sealed class Phase17PerformanceTests : Phase17Fixture
    {
        [Test]
        public void TheCampaignLayer_IsMeasured()
        {
            Hero();
            Days(2); // warm up
            var sw = Stopwatch.StartNew();
            Days(10);
            sw.Stop();
            var sandboxDayMs = sw.Elapsed.TotalMilliseconds / 10;

            Begin();
            ChapterOne(lookAtTally: false);
            var w = W;
            var c = CampaignEngine.Find(w, CampaignEngine.InstanceIdFor(Waystone));
            sw.Restart();
            for (var i = 0; i < 200; i++) CampaignEngine.Evaluate(w, c, null);
            sw.Stop();
            var evaluateMs = sw.Elapsed.TotalMilliseconds / 200;

            sw.Restart();
            for (var i = 0; i < 200; i++) StorySynthesis.Run(W, null); // on a fresh snapshot each time: what a day's pass costs
            sw.Stop();
            var storyMs = sw.Elapsed.TotalMilliseconds / 200;

            About(Npc("counterpart"), "hold", "giver");
            Ok(Say("accept"));
            sw.Restart();
            for (var i = 0; i < 100; i++) Realmkeeper.Application.DialogueEngine.View(W);
            sw.Stop();
            var dialogueMs = sw.Elapsed.TotalMilliseconds / 100;

            sw.Restart();
            Ok(Say("home"));
            sw.Stop();
            var decideMs = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            Days(10);
            sw.Stop();
            var campaignDayMs = sw.Elapsed.TotalMilliseconds / 10;

            w = W;
            var json = new JsonWorldSerializer().Serialize(w);
            sw.Restart();
            for (var i = 0; i < 3; i++) new JsonWorldSerializer().Serialize(w);
            sw.Stop();
            var saveMs = sw.Elapsed.TotalMilliseconds / 3;
            sw.Restart();
            for (var i = 0; i < 3; i++) new JsonWorldSerializer().Deserialize(json);
            sw.Stop();
            var loadMs = sw.Elapsed.TotalMilliseconds / 3;
            var campaignKb = Newtonsoft.Json.Linq.JObject.Parse(json)["world"]["campaigns"].ToString().Length / 1024.0;

            TestContext.WriteLine($"Campaign: {c.Arcs.Count} arcs, {C.Decisions.Count} decision(s), {C.Pending.Count} pending consequence(s), {C.History.Count} history lines, {W.Quests.Count} quests in the world.");
            TestContext.WriteLine($"Re-reading the campaign: {evaluateMs:0.000} ms. Story pass: {storyMs:0.000} ms. Dialogue view (conditions included): {dialogueMs:0.00} ms. " +
                                  $"A decision with its consequences (the whole command): {decideMs:0.0} ms.");
            TestContext.WriteLine($"Simulated day: {sandboxDayMs:0.0} ms without a campaign, {campaignDayMs:0.0} ms with one. Save {saveMs:0} ms, load {loadMs:0} ms, " +
                                  $"{json.Length / 1024} KB (campaign section {campaignKb:0.0} KB).");
            Assert.That(evaluateMs, Is.LessThan(5));
            Assert.That(storyMs, Is.LessThan(20));
            Assert.That(dialogueMs, Is.LessThan(20));
            Assert.That(campaignDayMs, Is.LessThan(sandboxDayMs * 2 + 50), "A campaign adds little to a day.");
            Assert.That(C.History.Count, Is.LessThanOrEqualTo(Realmkeeper.Domain.Campaigns.CampaignInstanceData.MaxHistory), "Bounded history.");
        }
    }
}
