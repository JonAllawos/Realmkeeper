using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Simulation.Campaigns;

namespace Realmkeeper.Tests.EditMode.Campaigns
{
    /// <summary>
    /// Phase 18 (ADR-0091): The Waystone Count, deepened without changing what it is — what the player found elsewhere in
    /// the world (the watch log of Cairn Watch, the ringers' verse) becomes a way to make the case for a shared count, and
    /// the toll-keeper can be brought to own the short count before both towns. Scenarios 27–29.
    /// </summary>
    public sealed class Phase18WaystoneTests : Phase17Fixture
    {
        private void ToTheQuestion()
        {
            Hero();
            Begin();
            ChapterOne(lookAtTally: false);
            About(Npc("counterpart"), "hold", "giver");
            Ok(Say("accept"));
        }

        [Test]
        public void WhatThePlayerFoundElsewhere_MakesTheCase_ForASharedCount()
        {
            ToTheQuestion();
            Assert.That(Options(), Does.Not.Contain("precedent_cairn"), "Not without having read the watch's log.");
            Assert.That(Options(), Does.Not.Contain("shared"), "And not shared, without a case made.");
            Ok(S.Dispatch(new EndConversationCommand()));
            Edit(w => w.Player.Social.Discoveries.Add(new DiscoveryData { Key = "cairn_watch_log", Text = "kept by turns", Category = DiscoveryCategory.Faction, Major = true }));
            About(Npc("counterpart"), "hold", "giver");
            Assert.That(Options(), Does.Contain("precedent_cairn"), "The watch log is precedent.");
            Ok(Say("precedent_cairn"));
            Assert.That(C.Flags["case_made"], Is.EqualTo("precedent"), "The case is made by what was found, not by a roll.");
            Ok(Say("shared"));
            Assert.That(C.Decision("road_claim").ChoiceId, Is.EqualTo("shared"));
        }

        [Test]
        public void TheRingersVerse_IsAlsoPrecedent()
        {
            ToTheQuestion();
            Ok(S.Dispatch(new EndConversationCommand()));
            Edit(w => w.Player.Social.Discoveries.Add(new DiscoveryData { Key = "ringers_order", Text = "three bells", Category = DiscoveryCategory.Cultural, Major = true }));
            About(Npc("counterpart"), "hold", "giver");
            Ok(Say("precedent_ringers"));
            Assert.That(C.Flags["case_made"], Is.EqualTo("precedent"));
        }

        [Test]
        public void TheTollKeeper_CanBeBroughtToOwnIt_BeforeBothTowns_WithTheEvidence()
        {
            ToTheQuestion();
            Ok(Say("apart"));
            About(Npc("counterpart"), "keeper", "giver");
            Ok(Say("accept"));
            About(Npc("tollkeeper"), "keeper", "tollkeeper");
            var witness = DialogueEngine.View(W).Responses.Single(r => r.Id == "witness");
            Assert.That(witness.Enabled, Is.False, "Without the evidence it is shown, but closed.");
            Assert.That(witness.Why, Does.Contain("can't show"));
            Ok(S.Dispatch(new EndConversationCommand()));
            Edit(w => CampaignEngine.Find(w, CampaignEngine.InstanceIdFor(Waystone)).Flags["evidence"] = "tally");
            About(Npc("tollkeeper"), "keeper", "tollkeeper");
            Assert.That(Options(), Does.Contain("witness"));
            var r = Say("witness"); // a Persuasion check: either way, the world's dice decide and the outcome is honest
            Ok(r);
            if (C.Decision("tollkeeper_fate") != null)
            {
                Assert.That(C.Decision("tollkeeper_fate").ChoiceId, Is.EqualTo("witness"));
                Assert.That(Arc("keeper").Outcome, Is.EqualTo("witnessed"));
                Assert.That(C.Flags["fate"], Is.EqualTo("witnessed"));
            }
            else
            {
                Assert.That(Options(), Does.Not.Contain("witness"), "A failed attempt is not offered twice.");
                Assert.That(Options(), Does.Contain("expose"), "The other ways remain.");
            }
        }
    }
}
