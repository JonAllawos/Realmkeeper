using Realmkeeper.Application;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Exploration.Application
{
    /// <summary>Move the player character from the world map into a settlement's local environment.</summary>
    public sealed class EnterSettlementCommand : ICommand
    {
        public EnterSettlementCommand(SettlementId settlementId) { SettlementId = settlementId; }
        public SettlementId SettlementId { get; }
    }

    /// <summary>Return the player character from a settlement to the world map.</summary>
    public sealed class ExitSettlementCommand : ICommand
    {
    }

    /// <summary>Record where the character is inside the current settlement (sent on perspective switch, exit and save, not every frame).</summary>
    public sealed class SetPlayerPoseCommand : ICommand
    {
        public SetPlayerPoseCommand(SettlementId settlementId, LocalPose pose)
        {
            SettlementId = settlementId;
            Pose = pose;
        }

        public SettlementId SettlementId { get; }
        public LocalPose Pose { get; }
    }

    /// <summary>Switch between first- and third-person. The character's position is untouched.</summary>
    public sealed class SetCameraPerspectiveCommand : ICommand
    {
        public SetCameraPerspectiveCommand(CameraPerspective perspective) { Perspective = perspective; }
        public CameraPerspective Perspective { get; }
    }

    /// <summary>
    /// Talk to a named resident of the settlement the character is in. Persistent consequence:
    /// the resident remembers the player (first meeting date, conversation count), and a first
    /// meeting is recorded in the chronicle.
    /// </summary>
    public sealed class TalkToResidentCommand : ICommand
    {
        public TalkToResidentCommand(NpcId npcId) { NpcId = npcId; }
        public NpcId NpcId { get; }
    }

    /// <summary>
    /// Post a labour notice on the settlement's notice board. Persistent consequence: the labour
    /// system doubles the chosen source's allocation weight from the next day, the inspector
    /// shows the focus, and the chronicle records the notice.
    /// </summary>
    public sealed class PostLabourNoticeCommand : ICommand
    {
        public PostLabourNoticeCommand(SettlementId settlementId, ProductionSourceType focus)
        {
            SettlementId = settlementId;
            Focus = focus;
        }

        public SettlementId SettlementId { get; }
        public ProductionSourceType Focus { get; }
    }
}
