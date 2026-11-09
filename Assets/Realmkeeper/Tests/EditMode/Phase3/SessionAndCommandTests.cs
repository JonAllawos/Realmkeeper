using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Composition;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Phase3
{
    /// <summary>Prerequisites 2 and 3: one session host with an explicit lifecycle, and the typed command registry.</summary>
    public sealed class SessionAndCommandTests
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

        // ----- Session host -------------------------------------------------------------------

        [Test]
        public void Host_StartsExactlyOneSession_AndRefusesASecondWithoutReplacingTheWorld()
        {
            var host = Host();
            Assert.That(host.State, Is.EqualTo(SessionHostState.Idle));
            Assert.Throws<InvalidOperationException>(() => _ = host.Session, "No session before a world is loaded.");

            var started = 0;
            host.SessionStarted += _ => started++;
            Assert.That(host.StartNew(new CreateWorldCommand("Hosted", 7)).IsSuccess, Is.True);
            var session = host.Session;
            var worldId = session.GetOverview().WorldId;

            var second = host.StartNew(new CreateWorldCommand("Intruder", 8));
            Assert.That(second.Status, Is.EqualTo(CommandStatus.InvalidSessionState));
            Assert.That(host.Session, Is.SameAs(session));
            Assert.That(host.Session.GetOverview().WorldId, Is.EqualTo(worldId), "The active world was not replaced.");
            Assert.That(started, Is.EqualTo(1));
        }

        [Test]
        public void Shutdown_ClosesTheSession_AndStaleReferencesFailSafely()
        {
            var host = Host();
            host.StartNew(new CreateWorldCommand("Hosted", 7));
            var stale = host.Session;
            var ended = 0;
            host.SessionEnded += () => ended++;

            Assert.That(host.Shutdown().IsSuccess, Is.True);

            Assert.That(host.State, Is.EqualTo(SessionHostState.Idle));
            Assert.That(ended, Is.EqualTo(1));
            Assert.That(stale.IsClosed && !stale.HasWorld, Is.True);
            Assert.That(stale.Dispatch(new SaveWorldCommand("late")).Status, Is.EqualTo(CommandStatus.InvalidSessionState));
            Assert.That(stale.Execute(new AdvanceDayCommand()).IsSuccess, Is.False);
            Assert.That(host.Shutdown().Status, Is.EqualTo(CommandStatus.InvalidSessionState), "Shutting down twice is reported, not ignored.");

            Assert.That(host.StartNew(new CreateWorldCommand("Again", 9)).IsSuccess, Is.True);
            Assert.That(host.Session, Is.Not.SameAs(stale), "A restart builds a fresh session.");
        }

        [Test]
        public void StartFromSave_RestoresWorldDateAndCharacter_AndAFailedStartLeavesTheHostIdle()
        {
            var host = Host();
            host.StartNew(new CreateWorldCommand("Hosted", 123456789));
            var town = host.Session.GetSettlements().First(s => s.SettlementType == SettlementType.Town);
            host.Session.Execute(new AdvanceDaysCommand(12));
            host.Session.Dispatch(new EnterSettlementCommand(town.SettlementId));
            host.Session.Dispatch(new SetPlayerPoseCommand(town.SettlementId, new LocalPose(3.5f, 0f, -7.25f, 42f)));
            host.Session.Dispatch(new SetCameraPerspectiveCommand(CameraPerspective.FirstPerson));
            Assert.That(host.Session.Dispatch(new SaveWorldCommand("hosted")).IsSuccess, Is.True);
            var date = host.Session.GetOverview().Date;
            host.Shutdown();

            Assert.That(host.StartFromSave("missing-slot").IsSuccess, Is.False);
            Assert.That(host.State, Is.EqualTo(SessionHostState.Idle), "A failed load leaves no half-started session.");

            Assert.That(host.StartFromSave("hosted").IsSuccess, Is.True);
            var player = host.Session.GetPlayer();
            Assert.That(host.Session.GetOverview().Date, Is.EqualTo(date));
            Assert.That(player.IsInSettlement && player.SettlementId == town.SettlementId, Is.True);
            Assert.That(player.Pose, Is.EqualTo(new LocalPose(3.5f, 0f, -7.25f, 42f)));
            Assert.That(player.Perspective, Is.EqualTo(CameraPerspective.FirstPerson));
            Assert.That(host.Session.Commands.IsRegistered<EnterSettlementCommand>(), Is.True, "Module handlers are registered on every new session.");
        }

        [Test]
        public void DisposedHost_RefusesToStart()
        {
            var host = Host();
            host.StartNew(new CreateWorldCommand("Hosted", 7));
            var session = host.Session;
            host.Dispose();
            Assert.That(session.IsClosed, Is.True);
            Assert.That(host.State, Is.EqualTo(SessionHostState.Disposed));
            Assert.That(host.StartNew(new CreateWorldCommand("x", 1)).Status, Is.EqualTo(CommandStatus.InvalidSessionState));
        }

        // ----- Command registry ---------------------------------------------------------------

        private sealed class RenameCommand : ICommand
        {
            public RenameCommand(string name, bool throwAfterEdit = false, bool corrupt = false, bool refuse = false)
            {
                Name = name;
                ThrowAfterEdit = throwAfterEdit;
                Corrupt = corrupt;
                Refuse = refuse;
            }

            public string Name { get; }
            public bool ThrowAfterEdit { get; }
            public bool Corrupt { get; }
            public bool Refuse { get; }
        }

        private sealed class RenameHandler : ICommandHandler<RenameCommand>
        {
            public int Calls;

            public CommandResult Handle(RenameCommand command, CommandContext context)
            {
                Calls++;
                if (!context.HasWorld) return CommandResult.InvalidState("No world.");
                var world = context.Edit();
                world.Settlements[0].Name = command.Name;
                if (command.Corrupt) world.Settlements[0].Population = -1;
                if (command.ThrowAfterEdit) throw new InvalidOperationException("boom");
                if (command.Refuse) return CommandResult.Invalid("Refused after editing.");
                context.ReportEvent(HistoricalEventType.LabourNoticePosted, EventSeverity.Info, EventImportance.Minor,
                    new[] { world.Settlements[0].SettlementId }, null);
                return CommandResult.Ok("Renamed.");
            }
        }

        private WorldSession SessionWithRename(out RenameHandler handler)
        {
            var session = Phase2Worlds.Session(_dir);
            handler = new RenameHandler();
            session.Commands.Register(handler);
            return session;
        }

        [Test]
        public void Registry_ResolvesHandlersByType_AndRejectsDuplicates()
        {
            var session = SessionWithRename(out var handler);
            Assert.That(session.Commands.IsRegistered<RenameCommand>(), Is.True);
            Assert.That(session.Commands.IsRegistered<SaveWorldCommand>(), Is.True, "Built-in handlers are registered.");
            Assert.Throws<InvalidOperationException>(() => session.Commands.Register(new RenameHandler()));

            var changes = 0;
            session.WorldChanged += c => { if (c.Reason == WorldChangeReason.CommandApplied && c.CommandType == typeof(RenameCommand)) changes++; };
            var result = session.Dispatch(new RenameCommand("Newford"));

            Assert.That(result.Status, Is.EqualTo(CommandStatus.Success), result.ToString());
            Assert.That(handler.Calls, Is.EqualTo(1));
            Assert.That(session.GetSettlements()[0].Name, Is.EqualTo("Newford"));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(session.GetRecentEvents(1)[0].EventType, Is.EqualTo(HistoricalEventType.LabourNoticePosted), "Events reported by a command are committed with it.");
        }

        [Test]
        public void MissingHandler_AndNullCommand_FailClearly()
        {
            var session = Phase2Worlds.Session(_dir);
            var missing = session.Dispatch(new RenameCommand("x"));
            Assert.That(missing.Status, Is.EqualTo(CommandStatus.Unsupported));
            Assert.That(missing.Message, Does.Contain("RenameCommand"));
            Assert.That(session.Dispatch<RenameCommand>(null).Status, Is.EqualTo(CommandStatus.ValidationFailed));
        }

        [TestCase(true, false, false, CommandStatus.Failed)]
        [TestCase(false, true, false, CommandStatus.ValidationFailed)]
        [TestCase(false, false, true, CommandStatus.ValidationFailed)]
        public void FailingCommands_NeverPartiallyMutateTheWorld(bool throwAfterEdit, bool corrupt, bool refuse, CommandStatus expected)
        {
            var session = SessionWithRename(out _);
            var before = session.GetSettlements().Select(s => s.Name + s.Population).ToList();
            var events = session.GetOverview().EventCount;

            var result = session.Dispatch(new RenameCommand("Halfdone", throwAfterEdit, corrupt, refuse));

            Assert.That(result.Status, Is.EqualTo(expected), result.ToString());
            Assert.That(session.GetSettlements().Select(s => s.Name + s.Population), Is.EqualTo(before));
            Assert.That(session.GetOverview().EventCount, Is.EqualTo(events));
        }

        [Test]
        public void BuiltInSaveCommand_RunsThroughTheRegistry_AndValidatesItsInput()
        {
            var session = Phase2Worlds.Session(_dir);
            Assert.That(session.Dispatch(new SaveWorldCommand("  ")).Status, Is.EqualTo(CommandStatus.ValidationFailed));
            Assert.That(session.Dispatch(new SaveWorldCommand("registry")).IsSuccess, Is.True);
            Assert.That(File.Exists(Path.Combine(_dir, "registry.rkworld.json")), Is.True);
            Assert.That(session.Execute(new SetSimulationSpeedCommand(SimulationSpeed.Fast)).IsSuccess, Is.True);
            Assert.That(session.GetOverview().Speed, Is.EqualTo(SimulationSpeed.Fast));
        }
    }
}
