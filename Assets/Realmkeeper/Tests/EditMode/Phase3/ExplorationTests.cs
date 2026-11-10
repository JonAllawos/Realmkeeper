using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Infrastructure.Composition;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Phase3
{
    /// <summary>The exploration vertical slice, engine-free: commands, layout and world continuity.</summary>
    public sealed class ExplorationTests
    {
        private string _dir;
        private WorldSession _session;
        private SettlementSummary _town;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _session = Phase2Worlds.Session(_dir);
            ExplorationModule.Register(_session);
            _town = _session.GetSettlements().First(s => s.SettlementType == SettlementType.Town);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        /// <summary>Everything except the player: settlements, residents' identities, date, events.</summary>
        private string WorldWithoutPlayer() =>
            _session.GetOverview().Date + "|" + _session.GetOverview().EventCount + "|" +
            string.Join(";", _session.GetSettlements().Select(s => $"{s.SettlementId}:{s.Population}:{s.Prosperity}:{s.Stability}:{string.Join(",", s.Economy.Select(r => r.Stock))}")) + "|" +
            string.Join(";", _session.GetSettlements().SelectMany(s => _session.GetResidents(s.SettlementId)).Select(n => n.NpcId));

        [Test]
        public void EnterSettlement_ValidatesReferences_AndPlacesTheCharacterAtTheGate()
        {
            Assert.That(_session.Dispatch(new EnterSettlementCommand(default)).Status, Is.EqualTo(CommandStatus.ValidationFailed));
            Assert.That(_session.Dispatch(new EnterSettlementCommand(SettlementId.NewRandom())).Status, Is.EqualTo(CommandStatus.NotFound));

            var result = _session.Dispatch(new EnterSettlementCommand(_town.SettlementId));
            Assert.That(result.IsSuccess, Is.True, result.ToString());

            var player = _session.GetPlayer();
            var layout = ExplorationModule.LayoutFor(_session, _town);
            Assert.That(player.IsInSettlement, Is.True);
            Assert.That(player.SettlementId, Is.EqualTo(_town.SettlementId));
            Assert.That(player.RegionId, Is.EqualTo(_town.RegionId), "Same region identity as the map's settlement.");
            Assert.That(_session.FindRegion(player.RegionId), Is.Not.Null);
            Assert.That(player.Pose, Is.EqualTo(layout.ArrivalPose));
            Assert.That(player.LastSettlementId, Is.EqualTo(_town.SettlementId));

            Assert.That(_session.Dispatch(new EnterSettlementCommand(_town.SettlementId)).Status, Is.EqualTo(CommandStatus.InvalidSessionState));
            var village = _session.GetSettlements().First(s => s.SettlementType == SettlementType.Village);
            Assert.That(_session.Dispatch(new EnterSettlementCommand(village.SettlementId)).Status, Is.EqualTo(CommandStatus.InvalidSessionState), "Leave first.");
        }

        [Test]
        public void EnteringAndLeaving_ChangeOnlyTheCharacter_NeverTheDateEconomyPopulationOrResidents()
        {
            var before = WorldWithoutPlayer();
            for (var i = 0; i < 3; i++)
            {
                Assert.That(_session.Dispatch(new EnterSettlementCommand(_town.SettlementId)).IsSuccess, Is.True);
                Assert.That(_session.Dispatch(new SetCameraPerspectiveCommand(i % 2 == 0 ? CameraPerspective.FirstPerson : CameraPerspective.ThirdPerson)).IsSuccess, Is.True);
                Assert.That(_session.Dispatch(new ExitSettlementCommand()).IsSuccess, Is.True);
            }

            Assert.That(WorldWithoutPlayer(), Is.EqualTo(before));
            Assert.That(_session.GetPlayer().IsInSettlement, Is.False);
            Assert.That(_session.Dispatch(new ExitSettlementCommand()).Status, Is.EqualTo(CommandStatus.InvalidSessionState), "Cannot leave twice.");
        }

        [Test]
        public void PlayerPose_PersistsInsideTheSettlement_AndInvalidPositionsAreRejected()
        {
            _session.Dispatch(new EnterSettlementCommand(_town.SettlementId));
            var pose = new LocalPose(4f, 0.02f, -10f, 135f);

            Assert.That(_session.Dispatch(new SetPlayerPoseCommand(_town.SettlementId, pose)).IsSuccess, Is.True);
            Assert.That(_session.GetPlayer().Pose, Is.EqualTo(pose));

            Assert.That(_session.Dispatch(new SetPlayerPoseCommand(_town.SettlementId, new LocalPose(900f, 0f, 0f, 0f))).Status, Is.EqualTo(CommandStatus.ValidationFailed));
            Assert.That(_session.Dispatch(new SetPlayerPoseCommand(_town.SettlementId, new LocalPose(float.NaN, 0f, 0f, 0f))).Status, Is.EqualTo(CommandStatus.ValidationFailed));
            var other = _session.GetSettlements().First(s => s.SettlementType == SettlementType.Hamlet);
            Assert.That(_session.Dispatch(new SetPlayerPoseCommand(other.SettlementId, pose)).Status, Is.EqualTo(CommandStatus.ValidationFailed));
            Assert.That(_session.GetPlayer().Pose, Is.EqualTo(pose), "Rejected positions changed nothing.");
        }

        [Test]
        public void SwitchingPerspective_KeepsThePosition()
        {
            _session.Dispatch(new EnterSettlementCommand(_town.SettlementId));
            var pose = new LocalPose(-6f, 0f, 12f, 270f);
            _session.Dispatch(new SetPlayerPoseCommand(_town.SettlementId, pose));

            Assert.That(_session.Dispatch(new SetCameraPerspectiveCommand(CameraPerspective.FirstPerson)).IsSuccess, Is.True);
            Assert.That(_session.GetPlayer().Perspective, Is.EqualTo(CameraPerspective.FirstPerson));
            Assert.That(_session.GetPlayer().Pose, Is.EqualTo(pose));
            Assert.That(_session.Dispatch(new SetCameraPerspectiveCommand(CameraPerspective.ThirdPerson)).IsSuccess, Is.True);
            Assert.That(_session.GetPlayer().Pose, Is.EqualTo(pose));
        }

        [Test]
        public void TalkingToAResident_RemembersTheMeetingOnce_AndRecordsItInTheChronicle()
        {
            var resident = _session.GetResidents(_town.SettlementId)[0];
            Assert.That(_session.Dispatch(new TalkToResidentCommand(resident.NpcId)).Status, Is.EqualTo(CommandStatus.InvalidSessionState), "Must be in the settlement.");

            _session.Dispatch(new EnterSettlementCommand(_town.SettlementId));
            var first = _session.Dispatch(new TalkToResidentCommand(resident.NpcId));
            var second = _session.Dispatch(new TalkToResidentCommand(resident.NpcId));

            Assert.That(first.IsSuccess && second.IsSuccess, Is.True);
            Assert.That(first.Message, Does.Contain(resident.Name));
            var after = _session.FindNpc(resident.NpcId);
            Assert.That(after.HasMetPlayer, Is.True);
            Assert.That(after.MetPlayerOn, Is.EqualTo(_session.GetOverview().Date));
            Assert.That(after.ConversationCount, Is.EqualTo(2));
            var meetings = _session.GetRecentEvents(50).Where(e => e.EventType == HistoricalEventType.PlayerMetResident).ToList();
            Assert.That(meetings.Count, Is.EqualTo(1), "Only the first meeting is chronicled.");
            Assert.That(meetings[0].Summary, Does.Contain(resident.Name).And.Contain(_town.Name));

            var outsider = _session.GetResidents(_session.GetSettlements().First(s => s.SettlementType == SettlementType.Hamlet).SettlementId)[0];
            Assert.That(_session.Dispatch(new TalkToResidentCommand(outsider.NpcId)).Status, Is.EqualTo(CommandStatus.ValidationFailed));
            Assert.That(_session.Dispatch(new TalkToResidentCommand(NpcId.NewRandom())).Status, Is.EqualTo(CommandStatus.NotFound));
        }

        [Test]
        public void LabourNotice_ChangesTheSettlementSeenOnTheMap_AffectsProduction_AndSurvivesSaveLoad()
        {
            _session.Dispatch(new EnterSettlementCommand(_town.SettlementId));
            var focus = ProductionSourceType.Logging;
            var loggersBefore = _town.Production.First(p => p.SourceType == focus).Workers;

            var result = _session.Dispatch(new PostLabourNoticeCommand(_town.SettlementId, focus));
            Assert.That(result.IsSuccess, Is.True, result.ToString());
            Assert.That(_session.FindSettlement(_town.SettlementId).LabourFocus, Is.EqualTo(focus), "The strategic inspector's snapshot shows it.");
            Assert.That(_session.Dispatch(new PostLabourNoticeCommand(_town.SettlementId, focus)).Status, Is.EqualTo(CommandStatus.ValidationFailed), "Already posted.");
            Assert.That(_session.Dispatch(new PostLabourNoticeCommand(_town.SettlementId, ProductionSourceType.Mining)).Status,
                Is.EqualTo(_town.Production.Any(p => p.SourceType == ProductionSourceType.Mining) ? CommandStatus.Success : CommandStatus.ValidationFailed));
            _session.Dispatch(new PostLabourNoticeCommand(_town.SettlementId, focus));

            _session.Execute(new AdvanceDayCommand());
            var loggersAfter = _session.FindSettlement(_town.SettlementId).Production.First(p => p.SourceType == focus).Workers;
            Assert.That(loggersAfter, Is.GreaterThanOrEqualTo(loggersBefore), "Labour follows the notice from the next day.");
            Assert.That(_session.GetRecentEvents(50).Any(e => e.EventType == HistoricalEventType.LabourNoticePosted && e.SettlementIds.Contains(_town.SettlementId)), Is.True);

            _session.Dispatch(new SaveWorldCommand("notice"));
            var reloaded = RealmkeeperComposition.CreateSession(_dir);
            Assert.That(reloaded.Execute(new LoadWorldCommand("notice")).IsSuccess, Is.True);
            Assert.That(reloaded.FindSettlement(_town.SettlementId).LabourFocus, Is.EqualTo(focus));
        }

        [Test]
        public void Layout_IsDeterministic_KeyedByTheSameIds_WithOneSpotPerResident()
        {
            var a = ExplorationModule.LayoutFor(_session, _town);
            var b = ExplorationModule.LayoutFor(_session, _session.FindSettlement(_town.SettlementId));

            Assert.That(a.SettlementId, Is.EqualTo(_town.SettlementId));
            Assert.That(a.RegionId, Is.EqualTo(_town.RegionId));
            Assert.That(a.EnvironmentKey, Is.EqualTo("settlement:" + _town.SettlementId));
            Assert.That(a.Buildings.Select(x => $"{x.Key}{x.Center.X}{x.Center.Z}{x.Height}"), Is.EqualTo(b.Buildings.Select(x => $"{x.Key}{x.Center.X}{x.Center.Z}{x.Height}")));
            // The hall, and (Phase 5) each house that is a shop.
            var shopHouses = _session.GetShops(_town.SettlementId).Count(s => s.LayoutKey != null && s.LayoutKey.StartsWith("house-"));
            Assert.That(shopHouses, Is.GreaterThanOrEqualTo(2), "A town has a general merchant and a smithy in its houses.");
            Assert.That(a.Buildings.Count(x => x.IsEnterable), Is.EqualTo(1 + shopHouses));
            Assert.That(a.Buildings.Count(x => x.Fit != ShopFit.None), Is.EqualTo(shopHouses));
            Assert.That(a.FindInteractable(InteractableKind.LabourNoticeBoard), Is.Not.Null);
            Assert.That(a.FindInteractable(InteractableKind.GranaryLedger), Is.Not.Null);
            var residents = _session.GetResidents(_town.SettlementId);
            Assert.That(a.Residents.Select(r => r.NpcId), Is.EquivalentTo(residents.Select(r => r.NpcId)));
            Assert.That(a.Residents.All(r => a.Contains(r.Position.X, r.Position.Z)), Is.True);
            Assert.That(a.Contains(a.ArrivalPose.X, a.ArrivalPose.Z), Is.True);

            var village = _session.GetSettlements().First(s => s.SettlementType == SettlementType.Village);
            Assert.That(ExplorationModule.LayoutFor(_session, village).HalfExtent, Is.LessThan(a.HalfExtent));
        }

        [Test]
        public void SaveAndReload_InsideASettlement_RestoresCharacterResidentsAndWorld()
        {
            _session.Execute(new AdvanceDaysCommand(5));
            _session.Dispatch(new EnterSettlementCommand(_town.SettlementId));
            var resident = _session.GetResidents(_town.SettlementId)[1];
            _session.Dispatch(new TalkToResidentCommand(resident.NpcId));
            _session.Dispatch(new SetPlayerPoseCommand(_town.SettlementId, new LocalPose(2f, 0f, 3f, 90f)));
            _session.Dispatch(new SetCameraPerspectiveCommand(CameraPerspective.FirstPerson));
            var expectedWorld = WorldWithoutPlayer();
            Assert.That(_session.Dispatch(new SaveWorldCommand("inside")).IsSuccess, Is.True);

            var reloaded = RealmkeeperComposition.CreateSession(_dir);
            ExplorationModule.Register(reloaded);
            Assert.That(reloaded.Execute(new LoadWorldCommand("inside")).IsSuccess, Is.True);
            _session = reloaded;

            var player = _session.GetPlayer();
            Assert.That(player.SettlementId, Is.EqualTo(_town.SettlementId));
            Assert.That(player.RegionId, Is.EqualTo(_town.RegionId));
            Assert.That(player.Pose, Is.EqualTo(new LocalPose(2f, 0f, 3f, 90f)));
            Assert.That(player.Perspective, Is.EqualTo(CameraPerspective.FirstPerson));
            Assert.That(_session.FindNpc(resident.NpcId).HasMetPlayer, Is.True);
            Assert.That(WorldWithoutPlayer(), Is.EqualTo(expectedWorld));
            Assert.That(_session.Dispatch(new ExitSettlementCommand()).IsSuccess, Is.True, "Exploration continues on the reloaded session.");
        }
    }
}
