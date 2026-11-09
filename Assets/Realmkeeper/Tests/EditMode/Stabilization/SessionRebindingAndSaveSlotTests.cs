using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Composition;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Stabilization
{
    /// <summary>
    /// Pre-Phase-2 stabilization: presentation rebinding across session restarts (through
    /// <see cref="WorldSessionBinding"/>) and explicit save slots (<see cref="SaveSlot"/>).
    /// </summary>
    public sealed class SessionRebindingAndSaveSlotTests
    {
        private string _dir;

        [SetUp]
        public void SetUp() => _dir = Phase2Worlds.TempDirectory();

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private WorldSessionHost Host() => RealmkeeperComposition.CreateHost(_dir, ExplorationModule.Register);

        private int SaveFileCount() => Directory.Exists(_dir) ? Directory.GetFiles(_dir, "*" + JsonWorldStore.Extension).Length : 0;

        // ----- Rebinding ------------------------------------------------------------------------

        [Test]
        public void Binding_FollowsEveryRestart_WithExactlyOneSubscriptionAndNoClosedSessionKept()
        {
            var host = Host();
            var changes = new List<WorldChangeReason>();
            var binding = new WorldSessionBinding(c => changes.Add(c.Reason));
            var previous = new List<WorldSession>();

            for (var cycle = 0; cycle < 3; cycle++)
            {
                Assert.That(host.StartNew(new CreateWorldCommand("Cycle " + cycle, 100 + cycle)).IsSuccess, Is.True);
                var session = host.Session;
                var before = session.WorldChangedSubscriberCount;

                Assert.That(binding.Bind(session), Is.True, $"Cycle {cycle}: binds the new session.");
                Assert.That(binding.Bind(session), Is.False, "Binding the same session again is a no-op.");
                Assert.That(session.WorldChangedSubscriberCount, Is.EqualTo(before + 1), "Subscribed exactly once.");

                changes.Clear();
                session.Execute(new AdvanceDayCommand());
                Assert.That(changes, Is.EqualTo(new[] { WorldChangeReason.Advanced }), "One notification per change, not one per bind.");

                host.Shutdown();
                Assert.That(binding.Session, Is.Null, "A closed session is not kept.");
                Assert.That(binding.IsBound, Is.False);
                Assert.That(changes.Last(), Is.EqualTo(WorldChangeReason.Closed), "The listener hears the session close.");
                Assert.That(session.WorldChangedSubscriberCount, Is.EqualTo(0));
                Assert.That(binding.Bind(session), Is.False, "A closed session cannot be bound again.");
                previous.Add(session);
            }

            Assert.That(previous.Distinct().Count(), Is.EqualTo(3), "Each restart built a fresh session.");
        }

        [Test]
        public void Rebinding_ToAnotherSession_RemovesTheOldSubscription()
        {
            var hostA = Host();
            var hostB = RealmkeeperComposition.CreateHost(Path.Combine(_dir, "b"), ExplorationModule.Register);
            hostA.StartNew(new CreateWorldCommand("A", 11));
            hostB.StartNew(new CreateWorldCommand("B", 12));
            var a = hostA.Session;
            var b = hostB.Session;
            var calls = 0;
            var binding = new WorldSessionBinding(_ => calls++);

            binding.Bind(a);
            Assert.That(a.WorldChangedSubscriberCount, Is.EqualTo(1));
            binding.Bind(b);

            Assert.That(a.WorldChangedSubscriberCount, Is.EqualTo(0), "Old subscription removed.");
            Assert.That(b.WorldChangedSubscriberCount, Is.EqualTo(1));
            a.Execute(new AdvanceDayCommand());
            Assert.That(calls, Is.EqualTo(0), "Changes of the old session no longer arrive.");
            b.Execute(new AdvanceDayCommand());
            Assert.That(calls, Is.EqualTo(1));

            binding.Dispose();
            b.Execute(new AdvanceDayCommand());
            Assert.That(calls, Is.EqualTo(1), "Disposing stops notifications.");
            Assert.That(b.WorldChangedSubscriberCount, Is.EqualTo(0));
        }
        // ----- Save slots -----------------------------------------------------------------------

        [TestCase("autosave")]
        [TestCase("slot-2")]
        [TestCase("Kingdom_of_Ash")]
        [TestCase("012345678901234567890123456789012345678901234567")] // 48 characters
        public void ValidSlotNames_AreAccepted(string name)
        {
            Assert.That(SaveSlot.TryCreate(name, out var slot, out var error), Is.True, error);
            Assert.That(slot.Name, Is.EqualTo(name));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("../escape")]
        [TestCase("a/b")]
        [TestCase("C:")]
        [TestCase("slot with spaces")]
        [TestCase("0123456789012345678901234567890123456789012345678")] // 49 characters
        public void InvalidSlotNames_AreRejectedWithAReason(string name)
        {
            Assert.That(SaveSlot.TryCreate(name, out var slot, out var error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(slot, Is.EqualTo(SaveSlot.Default), "A failed parse never yields a half-valid slot.");
            Assert.That(JsonWorldStore.IsValidSlotName(name), Is.False, "The store applies the same rule.");
        }

        [Test]
        public void DefaultSlot_IsAutosave_SoEarlierSavesStillLoad()
        {
            Assert.That(SaveSlot.Default.Name, Is.EqualTo("autosave"));
            Assert.That(default(SaveSlot).Name, Is.EqualTo("autosave"));

            var host = Host();
            host.StartNew(new CreateWorldCommand("Default", 21));
            Assert.That(host.Session.Dispatch(new SaveWorldCommand(SaveSlot.Default)).IsSuccess, Is.True);
            Assert.That(File.Exists(Path.Combine(_dir, "autosave" + JsonWorldStore.Extension)), Is.True);
            Assert.That(host.Session.Execute(new LoadWorldCommand("autosave")).IsSuccess, Is.True, "Loading by the plain name still works.");
        }

        [Test]
        public void Slots_HoldIndependentWorlds()
        {
            var host = Host();
            host.StartNew(new CreateWorldCommand("Slots", 31));
            var s = host.Session;
            var day0 = s.GetOverview().Date;

            Assert.That(s.Dispatch(new SaveWorldCommand(SaveSlot.Create("slot-a"))).IsSuccess, Is.True);
            s.Execute(new AdvanceDaysCommand(10));
            var day10 = s.GetOverview().Date;
            Assert.That(s.Dispatch(new SaveWorldCommand(SaveSlot.Create("slot-b"))).IsSuccess, Is.True);
            s.Execute(new AdvanceDaysCommand(5));

            Assert.That(s.Execute(new LoadWorldCommand(SaveSlot.Create("slot-a"))).IsSuccess, Is.True);
            Assert.That(s.GetOverview().Date, Is.EqualTo(day0));
            Assert.That(s.Execute(new LoadWorldCommand(SaveSlot.Create("slot-b"))).IsSuccess, Is.True);
            Assert.That(s.GetOverview().Date, Is.EqualTo(day10));

            // Overwriting b leaves a untouched.
            s.Execute(new AdvanceDaysCommand(3));
            var day13 = s.GetOverview().Date;
            s.Dispatch(new SaveWorldCommand("slot-b"));
            s.Execute(new LoadWorldCommand("slot-a"));
            Assert.That(s.GetOverview().Date, Is.EqualTo(day0), "Slot a is independent of later saves to b.");
            s.Execute(new LoadWorldCommand("slot-b"));
            Assert.That(s.GetOverview().Date, Is.EqualTo(day13));
            Assert.That(SaveFileCount(), Is.EqualTo(2));
        }

        [TestCase("")]
        [TestCase("../escape")]
        [TestCase("0123456789012345678901234567890123456789012345678")]
        public void InvalidSlots_AreRejectedForSaveAndLoad_AndChangeNothing(string name)
        {
            var host = Host();
            host.StartNew(new CreateWorldCommand("Invalid", 41));
            var s = host.Session;
            s.Execute(new AdvanceDaysCommand(4));
            var date = s.GetOverview().Date;

            Assert.That(s.Dispatch(new SaveWorldCommand(name)).Status, Is.EqualTo(CommandStatus.ValidationFailed));
            Assert.That(s.Execute(new LoadWorldCommand(name)).Status, Is.EqualTo(CommandStatus.ValidationFailed));
            Assert.That(s.GetOverview().Date, Is.EqualTo(date));
            Assert.That(SaveFileCount(), Is.EqualTo(0), "Nothing was written.");

            host.Shutdown();
            Assert.That(host.StartFromSave(name).Status, Is.EqualTo(CommandStatus.ValidationFailed));
            Assert.That(host.State, Is.EqualTo(SessionHostState.Idle));
        }

        [Test]
        public void MissingSlot_IsNotFound_AndTheCurrentWorldStays()
        {
            var host = Host();
            host.StartNew(new CreateWorldCommand("Missing", 51));
            var s = host.Session;
            var id = s.GetOverview().WorldId;

            var result = s.Execute(new LoadWorldCommand("never-saved"));

            Assert.That(result.Status, Is.EqualTo(CommandStatus.NotFound));
            StringAssert.Contains("never-saved", result.Message);
            Assert.That(s.GetOverview().WorldId, Is.EqualTo(id));
        }
    }
}
