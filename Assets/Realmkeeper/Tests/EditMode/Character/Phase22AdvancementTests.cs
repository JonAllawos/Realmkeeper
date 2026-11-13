using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Character
{
    /// <summary>
    /// Phase 22 (ADR-0109): the player-driven level-up on top of Phase 7's explicit advancement. Experience makes the
    /// character eligible; the notice is shown once per level and recorded; picks made so far are kept on the sheet (and
    /// survive saving) without applying anything; confirming applies the whole level once; nothing is chosen for the player.
    /// </summary>
    public sealed class Phase22AdvancementTests
    {
        private string _dir;
        private WorldSession _s;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = Phase2Worlds.Session(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private WorldData W => _s.CurrentWorldSnapshotForTests();
        private CharacterSheetData Sheet => W.Player.Rpg;

        private void Create(string cls = "fighter")
        {
            var r = _s.Dispatch(new CreateCharacterCommand(Phase22LevelOneTests.Full(cls, "human", "soldier").Clean()));
            Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));
        }

        private void Award(int toLevel, string key)
        {
            var need = ProgressionRules.ExperienceFor(toLevel) - Sheet.Experience;
            Assert.That(_s.Dispatch(new AwardExperienceCommand(need, ExperienceSource.Milestone, key)).IsSuccess, Is.True);
        }

        private void Advance()
        {
            var r = _s.Dispatch(new AdvanceCharacterCommand(CharacterAdvancement.SuggestChoices(Sheet, Sheet.Level + 1)));
            Assert.That(r.IsSuccess, Is.True, r.Message + " " + string.Join("; ", r.Errors));
        }

        /// <summary>Saves the world to a slot and loads it back into the session (the real save path).</summary>
        private void SaveAndLoad(string slot)
        {
            Assert.That(_s.Execute(new SaveWorldCommand(slot)).IsSuccess, Is.True);
            Assert.That(_s.Execute(new LoadWorldCommand(slot)).IsSuccess, Is.True);
        }

        // ----- The notice ---------------------------------------------------------------------------------

        [Test]
        public void ANewLevel_IsAnnouncedOnce_Recorded_AndTheAnnouncementSurvivesSaving()
        {
            Create();
            Assert.That(_s.GetCharacter().LevelUpUnannounced, Is.False, "Nothing to tell at level 1 with no experience.");
            Assert.That(_s.Dispatch(new AcknowledgeLevelUpCommand()).IsSuccess, Is.False);

            Award(2, "first");
            var c = _s.GetCharacter();
            Assert.That(c.CanAdvance && c.LevelUpUnannounced, Is.True);
            Assert.That(c.Level, Is.EqualTo(1), "Experience never levels by itself.");
            SaveAndLoad("before-notice");
            Assert.That(_s.GetCharacter().LevelUpUnannounced, Is.True, "Still to be told after loading.");

            var ack = _s.Dispatch(new AcknowledgeLevelUpCommand());
            Assert.That(ack.IsSuccess, Is.True, ack.Message);
            Assert.That(Sheet.LevelUpNoticed, Is.EqualTo(2));
            Assert.That(_s.GetCharacter().LevelUpUnannounced, Is.False, "Told once.");
            Assert.That(_s.Dispatch(new AcknowledgeLevelUpCommand()).IsSuccess, Is.False, "Not twice.");
            Assert.That(W.HistoricalEvents.Any(e => e.EventType == HistoricalEventType.PlayerLevelReady && e.Get("to") == "2"), Is.True, "In the chronicle.");
            Assert.That(_s.GetCharacter().CanAdvance, Is.True, "The level still waits for the player.");

            SaveAndLoad("after-notice");
            Assert.That(_s.GetCharacter().LevelUpUnannounced, Is.False, "The notice is not repeated after loading.");
            Assert.That(Sheet.LevelUpNoticed, Is.EqualTo(2));
        }

        [Test]
        public void SeveralLevels_AreAnnouncedTogether_AndAMoreDistantLevel_IsAnnouncedAgain()
        {
            Create();
            Award(4, "big");
            Assert.That(_s.GetCharacter().EligibleLevel, Is.EqualTo(4));
            Assert.That(_s.Dispatch(new AcknowledgeLevelUpCommand()).IsSuccess, Is.True);
            Assert.That(Sheet.LevelUpNoticed, Is.EqualTo(4));
            Advance();
            Assert.That(_s.GetCharacter().LevelUpUnannounced, Is.False, "Levels 3 and 4 were already announced.");
            Assert.That(_s.GetCharacter().CanAdvance, Is.True);
            Award(5, "more");
            Assert.That(_s.GetCharacter().LevelUpUnannounced, Is.True, "Level 5 is new.");
        }

        // ----- Picks kept, nothing applied ----------------------------------------------------------------

        [Test]
        public void PartialPicks_AreKept_ApplyNothing_SurviveSaving_AndResume()
        {
            Create("fighter");
            Award(3, "to3");
            Advance(); // level 2 (no choices for a fighter)
            Assert.That(Sheet.Level, Is.EqualTo(2));
            var before = Sheet.Clone();

            // Level 3 asks for the fighter's Martial Archetype. Nothing is chosen for the player.
            var draft = new AdvancementDraft(Sheet);
            Assert.That(draft.Resumed, Is.False);
            Assert.That(draft.OpenChoices, Is.GreaterThan(0));
            Assert.That(draft.Choices.SubclassId, Is.Null, "No subclass is picked for the player.");
            var subclass = draft.Required.First(c => c.Choice.Kind == ChoiceKind.Subclass).Choice;
            Assert.That(draft.Toggle(subclass, subclass.Options[0].Id), Is.True);
            var kept = _s.Dispatch(draft.ToSaveCommand());
            Assert.That(kept.IsSuccess, Is.True, kept.Message + " " + string.Join("; ", kept.Errors));

            Assert.That(Sheet.Level, Is.EqualTo(2), "Keeping picks takes no level.");
            Assert.That(Sheet.SubclassId, Is.Null, "Nor the subclass.");
            Assert.That(Sheet.CurrentHp, Is.EqualTo(before.CurrentHp));
            Assert.That(Sheet.Advancements.Count, Is.EqualTo(before.Advancements.Count));
            Assert.That(Sheet.PendingAdvancement.SubclassId, Is.EqualTo(subclass.Options[0].Id));

            SaveAndLoad("pending");
            Assert.That(Sheet.PendingAdvancement, Is.Not.Null, "Kept through saving and loading.");
            Assert.That(Sheet.PendingAdvancement.Level, Is.EqualTo(3));
            Assert.That(Sheet.PendingAdvancement.SubclassId, Is.EqualTo(subclass.Options[0].Id));

            var resumed = new AdvancementDraft(Sheet);
            Assert.That(resumed.Resumed, Is.True);
            Assert.That(resumed.Choices.SubclassId, Is.EqualTo(subclass.Options[0].Id), "The window picks up where the player left off.");
            Assert.That(resumed.OpenChoices, Is.EqualTo(_s.GetCharacter().OpenChoices));

            // Complete the rest and confirm: the level applies once, the draft is gone.
            foreach (var slot in resumed.Required)
                foreach (var o in slot.Choice.Options)
                    if (!slot.Choice.CountAllowed(resumed.Picked(slot.Choice.Id).Count) && slot.Choice.Kind != ChoiceKind.Subclass) resumed.Toggle(slot.Choice, o.Id);
            Assert.That(resumed.Problems(), Is.Empty, string.Join("; ", resumed.Problems()));
            var done = _s.Dispatch(resumed.ToCommand());
            Assert.That(done.IsSuccess, Is.True, done.Message + " " + string.Join("; ", done.Errors));
            Assert.That(Sheet.Level, Is.EqualTo(3));
            Assert.That(Sheet.SubclassId, Is.EqualTo(subclass.Options[0].Id));
            Assert.That(Sheet.PendingAdvancement, Is.Null, "The draft is consumed.");
            Assert.That(Sheet.Advancements.Count, Is.EqualTo(2));

            // Duplicate prevention: the same level cannot be taken again, nor the same picks re-sent.
            Assert.That(_s.Dispatch(resumed.ToCommand()).IsSuccess, Is.False);
            Assert.That(Sheet.Level, Is.EqualTo(3));
            SaveAndLoad("after");
            Assert.That(Sheet.Level, Is.EqualTo(3));
            Assert.That(Sheet.Advancements.Count, Is.EqualTo(2), "Nothing doubled by loading.");
        }

        [Test]
        public void KeptPicks_AreValidated_AndCanBeCleared()
        {
            Create("fighter");
            Assert.That(_s.Dispatch(new SaveAdvancementDraftCommand(new LevelChoices { Level = 2, SubclassId = "champion" })).IsSuccess, Is.False, "No level is available.");
            Award(3, "to3");
            Advance();

            var wrongLevel = _s.Dispatch(new SaveAdvancementDraftCommand(new LevelChoices { Level = 4, SubclassId = "champion" }));
            Assert.That(wrongLevel.IsSuccess, Is.False, "Only the next level.");
            var unlocked = new LevelChoices { Level = 3 };
            unlocked.Choices["lv4:feat"] = new List<string> { "alert" };
            Assert.That(_s.Dispatch(new SaveAdvancementDraftCommand(unlocked)).IsSuccess, Is.False, "A choice not yet unlocked.");
            Assert.That(_s.Dispatch(new SaveAdvancementDraftCommand(new LevelChoices { Level = 3, SubclassId = "evoker" })).IsSuccess, Is.False, "Another class's subclass.");
            Assert.That(Sheet.PendingAdvancement, Is.Null, "Nothing invalid is kept.");

            Assert.That(_s.Dispatch(new SaveAdvancementDraftCommand(new LevelChoices { Level = 3, SubclassId = "champion" })).IsSuccess, Is.True);
            Assert.That(Sheet.PendingAdvancement, Is.Not.Null);
            Assert.That(_s.Dispatch(new SaveAdvancementDraftCommand(null)).IsSuccess, Is.True);
            Assert.That(Sheet.PendingAdvancement, Is.Null, "Cleared.");
        }

        [Test]
        public void ATamperedSave_WithAnInvalidLevelUpInProgress_IsRefused()
        {
            Create("fighter");
            Award(2, "to2");
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(W));
            doc["world"]["player"]["rpg"]["pendingAdvancement"] = new JObject { ["level"] = 5, ["choices"] = new JObject() };
            Assert.That(serializer.Deserialize(doc.ToString()).IsSuccess, Is.False, "A level-up to 5 at level 1 with 300 experience.");
            doc["world"]["player"]["rpg"]["pendingAdvancement"] = new JObject { ["level"] = 2, ["choices"] = new JObject { ["lv9:nope"] = new JArray("x") } };
            Assert.That(serializer.Deserialize(doc.ToString()).IsSuccess, Is.False, "A choice that does not exist.");
            doc["world"]["player"]["rpg"]["pendingAdvancement"] = null;
            doc["world"]["player"]["rpg"]["levelUpNoticed"] = 99;
            Assert.That(serializer.Deserialize(doc.ToString()).IsSuccess, Is.False);
        }

        [Test]
        public void AVersion23Save_LoadsWithNothingPending_AndAWaitingLevelIsAnnouncedOnce()
        {
            Create("fighter");
            Award(2, "to2");
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(W));
            doc["schemaVersion"] = 23;
            var rpg = (JObject)doc["world"]["player"]["rpg"];
            rpg.Remove("pendingAdvancement");
            rpg.Remove("levelUpNoticed");
            var loaded = serializer.Deserialize(doc.ToString());
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            var s = loaded.World.Player.Rpg;
            Assert.That(s.Level, Is.EqualTo(1));
            Assert.That(s.Experience, Is.EqualTo(300), "Levels and experience untouched.");
            Assert.That(s.PendingAdvancement, Is.Null);
            Assert.That(s.LevelUpNoticed, Is.EqualTo(0));
            Assert.That(ProgressionCommandHandlers.LevelUpUnannounced(s), Is.True, "The waiting level is announced once after loading.");
            Assert.That(s.RulesVersion, Is.EqualTo(ProgressionRules.CurrentRulesVersion), "A character made at rules 10 stays at 10.");
        }

        // ----- The overview: automatic apart from chosen ------------------------------------------------

        [Test]
        public void TheOverview_ShowsWhatTheLevelGivesByItself_AndTheChoicesOpen()
        {
            Create("fighter");
            Award(3, "to3");
            Advance();
            var c = _s.GetCharacter();
            var p = c.NextLevelPreview;
            Assert.That(p, Is.Not.Null);
            Assert.That(p.ToLevel, Is.EqualTo(3));
            Assert.That(p.HitPointGain, Is.GreaterThan(0), "Hit points: automatic.");
            Assert.That(p.Choices.Any(s => s.Choice.Kind == ChoiceKind.Subclass), Is.True, "The archetype: chosen.");
            Assert.That(c.OpenChoices, Is.GreaterThanOrEqualTo(1));
            Assert.That(c.LockedFeatures.All(f => f.Level > c.Level), Is.True);
            Assert.That(c.LockedFeatures.Any(f => f.Level == 5 && f.Feature.Name.Contains("Extra Attack")), Is.True, "Extra Attack is shown locked until 5.");
        }
    }
}
