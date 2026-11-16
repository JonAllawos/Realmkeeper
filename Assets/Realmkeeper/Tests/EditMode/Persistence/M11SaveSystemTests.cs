using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Persistence
{
    /// <summary>
    /// M1.1 (ADR-0123): the release save system's store. Three manual slots and three autosaves, each with a backup; a
    /// checksum that finds a cut-off or altered file; a slot that always keeps a good save or a good backup whatever
    /// fails during a write; Continue's order; and the v27 → v28 step.
    /// </summary>
    public sealed class M11SaveSystemTests
    {
        private string _dir;
        private static WorldData _world;

        [SetUp]
        public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "realmkeeper-m11-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static WorldData World()
        {
            if (_world == null) _world = Phase2Worlds.Generated(1101);
            return _world.DeepClone();
        }

        private static WorldData Named(string name, int day = 1)
        {
            var w = World();
            w.Player.Name = name;
            w.MinuteOfDay = 9 * 60 + day;
            return w;
        }

        // ----- Slots ---------------------------------------------------------------------------------

        [Test]
        public void Slots_ThreeManual_ThreeAuto_EverythingElseIsOther()
        {
            Assert.That(SaveSlots.Manual, Is.EqualTo(new[] { "slot-1", "slot-2", "slot-3" }));
            Assert.That(SaveSlots.Auto, Is.EqualTo(new[] { "auto-1", "auto-2", "auto-3" }));
            Assert.That(SaveSlots.KindOf("slot-2"), Is.EqualTo(SaveSlotKind.Manual));
            Assert.That(SaveSlots.KindOf("auto-3"), Is.EqualTo(SaveSlotKind.Auto));
            Assert.That(SaveSlots.KindOf(SaveSlot.DefaultName), Is.EqualTo(SaveSlotKind.Other), "The pre-M1.1 'autosave' slot is an older save, not the rotation.");
            Assert.That(SaveSlots.KindOf("slot-4"), Is.EqualTo(SaveSlotKind.Other));
            Assert.That(SaveSlots.DisplayName("slot-1"), Is.EqualTo("Slot 1"));
            Assert.That(SaveSlots.DisplayName("auto-2"), Is.EqualTo("Autosave 2"));
            foreach (var s in SaveSlots.Manual.Concat(SaveSlots.Auto)) Assert.That(SaveSlot.IsValidName(s), Is.True, s);
        }

        [Test]
        public void TheRotation_FillsEmptySlotsFirst_ThenReplacesTheOldest()
        {
            var t0 = new DateTime(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);
            Assert.That(SaveSlots.NextAutosave(_ => false, _ => null), Is.EqualTo("auto-1"));
            Assert.That(SaveSlots.NextAutosave(s => s == "auto-1", _ => t0), Is.EqualTo("auto-2"));
            Assert.That(SaveSlots.NextAutosave(s => s != "auto-3", _ => t0), Is.EqualTo("auto-3"));
            var times = new System.Collections.Generic.Dictionary<string, DateTime?> { ["auto-1"] = t0.AddMinutes(30), ["auto-2"] = t0, ["auto-3"] = t0.AddMinutes(10) };
            Assert.That(SaveSlots.NextAutosave(_ => true, s => times[s]), Is.EqualTo("auto-2"), "All three full: the oldest is replaced.");
        }

        [Test]
        public void ThePolicy_RefusesEveryForbiddenState_AndWaitsSixtySecondsAfterAnySave()
        {
            var ok = new AutosaveConditions { Enabled = true, HasWorld = true, SecondsSinceLastSave = null };
            Assert.That(AutosavePolicy.Check(ok), Is.Null);
            void Refused(AutosaveConditions c, string why) => Assert.That(AutosavePolicy.Check(c), Is.Not.Null, why);
            var c1 = ok; c1.Enabled = false; Refused(c1, "Setting off");
            var c2 = ok; c2.HasWorld = false; Refused(c2, "No world");
            var c3 = ok; c3.InFight = true; Refused(c3, "Fight");
            var c4 = ok; c4.InConversation = true; Refused(c4, "Conversation");
            var c5 = ok; c5.InTransition = true; Refused(c5, "Transition");
            var c6 = ok; c6.CommandApplying = true; Refused(c6, "Command");
            var c7 = ok; c7.SecondsSinceLastSave = 59.9; Refused(c7, "Within 60 s");
            var c8 = ok; c8.SecondsSinceLastSave = 60.0;
            Assert.That(AutosavePolicy.Check(c8), Is.Null, "60 s after a save it may write again.");
            Assert.That(AutosavePolicy.IsTransient(c3), Is.True, "A fight ends: the trigger waits.");
            Assert.That(AutosavePolicy.IsTransient(c1), Is.False, "Autosave off: the trigger is dropped.");
        }

        [Test]
        public void SavingIsRefused_InAFight_AndInAConversation()
        {
            var w = World();
            Assert.That(WorldSession.SaveBlockerFor(w), Is.Null);
            w.Conversation = new Realmkeeper.Domain.Quests.ConversationData { Key = "test" };
            Assert.That(WorldSession.SaveBlockerFor(w), Is.EqualTo(WorldSession.ConversationBlocker));
            w.Conversation = null;
            w.Encounters.Add(new Realmkeeper.Domain.Combat.EncounterData { Status = Realmkeeper.Domain.Combat.EncounterStatus.Active });
            Assert.That(WorldSession.SaveBlockerFor(w), Is.EqualTo(WorldSession.FightBlocker));
            Assert.That(WorldSession.SaveBlockerFor(null), Is.Not.Null);
        }

        // ----- Save, load, overwrite, delete ---------------------------------------------------------

        [Test]
        public void EachManualSlot_Saves_Loads_KeepsABackupWhenOverwritten_AndDeletesCompletely()
        {
            var store = new JsonWorldStore(_dir);
            foreach (var slot in SaveSlots.Manual)
            {
                Assert.That(store.Save(Named("First " + slot), slot).IsSuccess, Is.True, slot);
                Assert.That(store.HasBackup(slot), Is.False, "A new slot has no backup until its second save.");
                var first = File.ReadAllText(store.PathFor(slot));
                Assert.That(store.Load(slot).World.Player.Name, Is.EqualTo("First " + slot));

                Assert.That(store.Save(Named("Second " + slot), slot).IsSuccess, Is.True);
                Assert.That(store.Load(slot).World.Player.Name, Is.EqualTo("Second " + slot));
                Assert.That(store.HasBackup(slot), Is.True);
                Assert.That(File.ReadAllText(store.BackupPathFor(slot)), Is.EqualTo(first), "The replaced save is the backup, byte for byte.");
                Assert.That(store.LoadBackup(slot).World.Player.Name, Is.EqualTo("First " + slot));
            }

            File.WriteAllBytes(store.ThumbnailPathFor("slot-2"), new byte[] { 1, 2, 3 });
            Assert.That(store.Delete("slot-2").IsSuccess, Is.True);
            Assert.That(store.Exists("slot-2") || store.HasBackup("slot-2") || File.Exists(store.ThumbnailPathFor("slot-2")), Is.False, "Delete removes the save, its backup and its picture.");
            Assert.That(store.Exists("slot-1") && store.Exists("slot-3"), Is.True, "Only that slot.");
        }

        [Test]
        public void TheV28Envelope_CarriesTheSummary_AndAChecksum_ReadWithoutTheWorld()
        {
            var store = new JsonWorldStore(_dir);
            var w = Named("Ysolde");
            w.PlaytimeSeconds = 3725;
            Assert.That(store.Save(w, "slot-1").IsSuccess, Is.True);
            var text = File.ReadAllText(store.PathFor("slot-1"));
            Assert.That(text.IndexOf("\"summary\"", StringComparison.Ordinal), Is.LessThan(text.IndexOf("\"world\"", StringComparison.Ordinal)), "The summary comes before the world.");
            Assert.That(SaveEnvelope.Verify(text), Is.Null);
            var doc = JObject.Parse(text);
            Assert.That((int)doc["schemaVersion"], Is.EqualTo(28));
            Assert.That((string)doc["integrity"]["algorithm"], Is.EqualTo("sha256"));

            var s = store.Inspect("slot-1");
            Assert.That(s.State, Is.EqualTo(SaveState.Ready));
            Assert.That(s.Kind, Is.EqualTo(SaveSlotKind.Manual));
            Assert.That(s.CharacterName, Is.EqualTo("Ysolde"));
            Assert.That(s.Level, Is.GreaterThanOrEqualTo(1));
            Assert.That(s.LocationText, Is.Not.Empty);
            Assert.That(s.MinuteOfDay, Is.EqualTo(w.MinuteOfDay));
            Assert.That(s.Date, Is.EqualTo(w.CurrentSimulationDate.ToIsoString()));
            Assert.That(s.PlaytimeSeconds, Is.EqualTo(3725));
            Assert.That(s.PlayedAtUtc, Is.Not.Null);
            Assert.That((string)doc["summary"]["slotKind"], Is.EqualTo("manual"));
            Assert.That(store.Save(Named("A"), "auto-1").IsSuccess, Is.True);
            Assert.That((string)JObject.Parse(File.ReadAllText(store.PathFor("auto-1")))["summary"]["slotKind"], Is.EqualTo("auto"));
        }

        [Test]
        public void PlayTime_SurvivesSaveAndLoad()
        {
            var store = new JsonWorldStore(_dir);
            var w = World();
            w.PlaytimeSeconds = 12.5;
            store.Save(w, "slot-1");
            Assert.That(store.Load("slot-1").World.PlaytimeSeconds, Is.EqualTo(12.5));
            Assert.That(w.DeepClone().PlaytimeSeconds, Is.EqualTo(12.5), "Every command's copy keeps it.");
        }

        // ----- The checksum and the backup -----------------------------------------------------------

        [Test]
        public void ACutOffFile_IsDamaged_AndItsBackupRestoresIt()
        {
            var store = new JsonWorldStore(_dir);
            store.Save(Named("Before"), "slot-1");
            store.Save(Named("After"), "slot-1");
            var path = store.PathFor("slot-1");
            var full = File.ReadAllText(path);
            File.WriteAllText(path, full.Substring(0, full.Length * 2 / 3));

            var s = store.Inspect("slot-1");
            Assert.That(s.State, Is.EqualTo(SaveState.Damaged), "The header still reads; the checksum finds the cut.");
            Assert.That(s.HasGoodBackup, Is.True);
            Assert.That(s.StateText, Is.EqualTo("Damaged (backup available)"));
            Assert.That(store.Load("slot-1").Status, Is.EqualTo(LoadStatus.Corrupt));
            Assert.That(store.ContinueOrder(false).Select(x => x.Slot), Does.Not.Contain("slot-1"), "Continue skips a damaged save.");

            Assert.That(store.RestoreBackup("slot-1").IsSuccess, Is.True);
            Assert.That(store.Load("slot-1").World.Player.Name, Is.EqualTo("Before"));
            Assert.That(File.Exists(path + JsonWorldStore.DamagedSuffix), Is.True, "The damaged file is kept beside it, not deleted.");
        }

        [Test]
        public void AOneByteEdit_IsDamaged()
        {
            var store = new JsonWorldStore(_dir);
            store.Save(Named("Edda"), "slot-2");
            var path = store.PathFor("slot-2");
            var text = File.ReadAllText(path);
            var at = text.IndexOf("\"Edda\"", text.IndexOf("\"world\": ", StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThan(0));
            File.WriteAllText(path, text.Substring(0, at + 1) + "F" + text.Substring(at + 2));

            Assert.That(store.Inspect("slot-2").State, Is.EqualTo(SaveState.Damaged));
            Assert.That(store.Inspect("slot-2").HasGoodBackup, Is.False, "No backup yet: plain Damaged.");
            var load = store.Load("slot-2");
            Assert.That(load.Status, Is.EqualTo(LoadStatus.Corrupt));
            Assert.That(string.Join(" ", load.Errors), Does.Contain("checksum"));
            Assert.That(store.RestoreBackup("slot-2").IsSuccess, Is.False, "Nothing to restore from.");
        }

        // ----- Failures while writing ----------------------------------------------------------------

        public enum Failure { ThrowMidWrite, Truncate, Deny, ReplaceFails }

        private sealed class FailingFiles : ISaveFileSystem
        {
            public Failure Mode;

            public void WriteAllText(string path, string text)
            {
                switch (Mode)
                {
                    case Failure.ThrowMidWrite:
                        File.WriteAllText(path, text.Substring(0, text.Length / 2));
                        throw new IOException("The disk is full.");
                    case Failure.Truncate:
                        File.WriteAllText(path, text.Substring(0, text.Length - 40));
                        return;
                    case Failure.Deny:
                        throw new UnauthorizedAccessException("Access to the path is denied.");
                    default:
                        SaveFileSystem.Instance.WriteAllText(path, text);
                        return;
                }
            }

            public string ReadAllText(string path) => SaveFileSystem.Instance.ReadAllText(path);

            public void Replace(string source, string destination, string backup)
            {
                if (Mode == Failure.ReplaceFails) throw new IOException("The file is in use.");
                SaveFileSystem.Instance.Replace(source, destination, backup);
            }

            public void Move(string source, string destination) => SaveFileSystem.Instance.Move(source, destination);
        }

        [Test]
        public void EveryInjectedWriteFailure_LeavesTheOldSaveAndItsBackup_AndNoTemporaryFile([Values] Failure mode)
        {
            var good = new JsonWorldStore(_dir);
            good.Save(Named("Old backup"), "slot-3");
            good.Save(Named("Old save"), "slot-3");
            var save = File.ReadAllText(good.PathFor("slot-3"));
            var bak = File.ReadAllText(good.BackupPathFor("slot-3"));

            var failing = new JsonWorldStore(_dir, null, new FailingFiles { Mode = mode });
            var result = failing.Save(Named("New"), "slot-3");
            Assert.That(result.IsSuccess, Is.False, mode.ToString());
            Assert.That(result.Error, Does.Contain("previous save is unchanged"), "The player is told plainly.");
            Assert.That(File.ReadAllText(good.PathFor("slot-3")), Is.EqualTo(save), "The save is untouched.");
            Assert.That(File.ReadAllText(good.BackupPathFor("slot-3")), Is.EqualTo(bak), "The backup is untouched.");
            Assert.That(File.Exists(good.PathFor("slot-3") + JsonWorldStore.TempSuffix), Is.False, "No temporary file is left.");
            Assert.That(good.Load("slot-3").World.Player.Name, Is.EqualTo("Old save"));
        }

        [Test]
        public void AStaleTemporaryFile_IsRemovedAtStartUp_AndTheSaveBesideItIsNot()
        {
            var store = new JsonWorldStore(_dir);
            store.Save(Named("Kept"), "slot-1");
            store.Save(Named("Kept 2"), "slot-1");
            File.WriteAllText(store.PathFor("slot-1") + JsonWorldStore.TempSuffix, "{ \"format\": \"realmkeeper.world\", half a sav");
            File.WriteAllText(store.PathFor("auto-2") + JsonWorldStore.TempSuffix, "x");
            var save = File.ReadAllText(store.PathFor("slot-1"));

            var removed = store.CleanStaleTemps();
            Assert.That(removed, Is.EquivalentTo(new[] { "slot-1" + JsonWorldStore.Extension + JsonWorldStore.TempSuffix, "auto-2" + JsonWorldStore.Extension + JsonWorldStore.TempSuffix }));
            Assert.That(Directory.GetFiles(_dir, "*" + JsonWorldStore.TempSuffix), Is.Empty);
            Assert.That(File.ReadAllText(store.PathFor("slot-1")), Is.EqualTo(save));
            Assert.That(store.HasBackup("slot-1"), Is.True);
            Assert.That(store.ListSaves().Select(s => s.Slot), Is.EqualTo(new[] { "slot-1" }), "A temporary file is never listed as a save.");
        }

        // ----- Continue ------------------------------------------------------------------------------

        [Test]
        public void Continue_PicksTheMostRecentlyPlayed_NotTheNewestFile_AndSkipsTooNewAndDamaged()
        {
            var t = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
            var clock = t;
            var store = new JsonWorldStore(_dir, new JsonWorldSerializer(utcNow: () => clock));
            clock = t.AddMinutes(30); store.Save(Named("Newest played"), "auto-1");
            clock = t.AddMinutes(10); store.Save(Named("Middle"), "slot-2");
            clock = t; store.Save(Named("Oldest"), "slot-1");
            Assert.That(File.GetLastWriteTimeUtc(store.PathFor("slot-1")), Is.GreaterThanOrEqualTo(File.GetLastWriteTimeUtc(store.PathFor("auto-1"))), "slot-1 is the newest file …");
            Assert.That(store.ContinueOrder(false).Select(s => s.Slot), Is.EqualTo(new[] { "auto-1", "slot-2", "slot-1" }), "… but Continue follows when each was played.");

            File.WriteAllText(store.PathFor("slot-3"), "{\n  \"format\": \"realmkeeper.world\",\n  \"schemaVersion\": 999,\n  \"world\": { \"worldName\": \"Tomorrow\", \"seed\": 1, \"currentSimulationDate\": \"0500-01-01\" }\n}");
            var text = File.ReadAllText(store.PathFor("auto-1"));
            File.WriteAllText(store.PathFor("auto-1"), text.Substring(0, text.Length - 100));
            Assert.That(store.Inspect("slot-3").State, Is.EqualTo(SaveState.TooNew));
            Assert.That(store.ContinueOrder(false).Select(s => s.Slot), Is.EqualTo(new[] { "slot-2", "slot-1" }), "Too new and damaged are skipped.");

            store.Save(Named("Developer"), "my-world");
            Assert.That(store.ContinueOrder(false).Select(s => s.Slot), Does.Not.Contain("my-world"), "Release builds: older and developer saves are not offered.");
            Assert.That(store.ContinueOrder(true).Select(s => s.Slot), Does.Contain("my-world"), "Development builds: they are.");
        }

        [Test]
        public void ListingAndContinue_NeverChangeAFile()
        {
            var store = new JsonWorldStore(_dir);
            store.Save(Named("One"), "slot-1");
            store.Save(Named("Two"), "slot-1");
            store.Save(Named("Three"), "auto-1");
            var before = Directory.GetFiles(_dir).ToDictionary(f => f, f => File.ReadAllText(f) + File.GetLastWriteTimeUtc(f).Ticks);
            store.ListSaves();
            store.ContinueOrder(true);
            store.Inspect("slot-1");
            var after = Directory.GetFiles(_dir).ToDictionary(f => f, f => File.ReadAllText(f) + File.GetLastWriteTimeUtc(f).Ticks);
            Assert.That(after, Is.EqualTo(before));
        }

        // ----- Migration -----------------------------------------------------------------------------

        [Test]
        public void ARealV27Save_LoadsAsV28_IdenticalApartFromTheNewFields()
        {
            var path = Path.Combine(UnityEngine.Application.dataPath, "Realmkeeper", "Tests", "EditMode", "Persistence", "Fixtures", "v27-world.rkworld.json.txt");
            Assert.That(File.Exists(path), Is.True, "The v27 fixture written by the pre-M1.1 build is present.");
            var v27Text = File.ReadAllText(path);
            var v27 = JObject.Parse(v27Text);
            Assert.That((int)v27["schemaVersion"], Is.EqualTo(27), "A real v27 file, written before M1.1.");
            Assert.That(v27["summary"], Is.Null);

            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "old" + JsonWorldStore.Extension), v27Text);
            var store = new JsonWorldStore(_dir);
            Assert.That(store.Inspect("old").State, Is.EqualTo(SaveState.NeedsUpdate), "Listed as Will be updated.");
            Assert.That(store.Inspect("old").PlaytimeSeconds, Is.Null, "Playtime unknown (shown as —).");
            var load = store.Load("old");
            Assert.That(load.IsSuccess, Is.True, string.Join("; ", load.Errors));
            Assert.That(load.World.PlaytimeSeconds, Is.EqualTo(0));
            Assert.That(File.ReadAllText(Path.Combine(_dir, "old" + JsonWorldStore.Extension)), Is.EqualTo(v27Text), "Loading never rewrites the file.");

            var v28 = JObject.Parse(new JsonWorldSerializer().Serialize(load.World));
            Assert.That((int)v28["schemaVersion"], Is.EqualTo(28));
            Assert.That(v28["summary"], Is.Not.Null);
            Assert.That(v28["integrity"], Is.Not.Null);
            var newWorld = (JObject)v28["world"];
            Assert.That((double)newWorld["playtimeSeconds"], Is.EqualTo(0));
            newWorld.Remove("playtimeSeconds");
            Assert.That(JToken.DeepEquals(newWorld, v27["world"]), Is.True, "Apart from the play-time counter, the world is exactly the v27 world.");
        }
    }
}
