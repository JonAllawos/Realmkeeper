using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Campaigns;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Campaigns;
using Realmkeeper.Simulation.Quests;

namespace Realmkeeper.Application.Campaigns
{
    /// <summary>Phase 17: begins a campaign in this world, if its prerequisites hold (the reason is given when they do not).</summary>
    public sealed class StartCampaignCommand : ICommand
    {
        public StartCampaignCommand(string campaignId) { CampaignId = campaignId; }
        public string CampaignId { get; }
    }

    public enum CampaignAction
    {
        Suspend = 0,
        Resume = 1,
        Abandon = 2,
    }

    /// <summary>Phase 17: sets a campaign aside, takes it up again, or gives it up (checked transitions).</summary>
    public sealed class SetCampaignStateCommand : ICommand
    {
        public SetCampaignStateCommand(string campaignId, CampaignAction action) { CampaignId = campaignId; Action = action; }
        public string CampaignId { get; }
        public CampaignAction Action { get; }
    }

    /// <summary>Phase 17: the player refuses a part of the story (an optional arc, or one that allows it).</summary>
    public sealed class RefuseArcCommand : ICommand
    {
        public RefuseArcCommand(string campaignId, string arcId) { CampaignId = campaignId; ArcId = arcId; }
        public string CampaignId { get; }
        public string ArcId { get; }
    }

    /// <summary>Phase 17: a decision the player makes alone, from the journal (one put by a person is made in conversation).</summary>
    public sealed class MakeCampaignDecisionCommand : ICommand
    {
        public MakeCampaignDecisionCommand(string campaignId, string decisionId, string choiceId) { CampaignId = campaignId; DecisionId = decisionId; ChoiceId = choiceId; }
        public string CampaignId { get; }
        public string DecisionId { get; }
        public string ChoiceId { get; }
    }

    /// <summary>
    /// Phase 17 (ADR-0082, ADR-0083): campaign commands. Each works on the command's copy of the world through the
    /// simulation's campaign engine, so a campaign starts, moves and decides inside one transaction with its quests,
    /// reputation, settlements and Chronicle.
    /// </summary>
    public static class CampaignCommandHandlers
    {
        public static void Register(CommandRegistry commands)
        {
            commands.Register<StartCampaignCommand>(new StartHandler());
            commands.Register<SetCampaignStateCommand>(new StateHandler());
            commands.Register<RefuseArcCommand>(new RefuseHandler());
            commands.Register<MakeCampaignDecisionCommand>(new DecideHandler());
        }

        private sealed class StartHandler : ICommandHandler<StartCampaignCommand>
        {
            public CommandResult Handle(StartCampaignCommand c, CommandContext context)
            {
                if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
                var def = CampaignCatalog.Find(c.CampaignId);
                if (def == null)
                    return CampaignCatalog.Rejected.TryGetValue(c.CampaignId ?? string.Empty, out var errors)
                        ? CommandResult.Invalid("That campaign's content is malformed and cannot be played.", errors)
                        : CommandResult.NotFound("No such campaign.");
                var why = CampaignEngine.Compatibility(context.Committed, def);
                if (why != null) return CommandResult.InvalidState(why);
                var w = context.Edit();
                var instance = CampaignEngine.Start(w, def, QuestRuntime.Reporter(context), out var problem);
                if (instance == null) return CommandResult.InvalidState(problem);
                return CommandResult.Ok($"\"{def.Title}\" begins. Its premise and first steps are in the journal (Campaign).");
            }
        }

        private sealed class StateHandler : ICommandHandler<SetCampaignStateCommand>
        {
            public CommandResult Handle(SetCampaignStateCommand c, CommandContext context)
            {
                var existing = CampaignEngine.Find(context.Committed, c.CampaignId);
                if (existing == null) return CommandResult.NotFound("No such campaign in this world.");
                var w = context.Edit();
                var instance = CampaignEngine.Find(w, c.CampaignId);
                var to = c.Action == CampaignAction.Suspend ? CampaignState.Suspended : c.Action == CampaignAction.Resume ? CampaignState.Active : CampaignState.Abandoned;
                var why = CampaignEngine.Move(w, instance, to, c.Action == CampaignAction.Abandon ? "you gave it up" : null, QuestRuntime.Reporter(context));
                if (why != null) return CommandResult.InvalidState(why);
                var title = CampaignEngine.Definition(instance)?.Title ?? instance.CampaignId;
                return CommandResult.Ok(c.Action == CampaignAction.Suspend ? $"\"{title}\" is set aside. Nothing is lost, and the world goes on."
                    : c.Action == CampaignAction.Resume ? $"You take up \"{title}\" again." : $"You give up \"{title}\". What was done stays done.");
            }
        }

        private sealed class RefuseHandler : ICommandHandler<RefuseArcCommand>
        {
            public CommandResult Handle(RefuseArcCommand c, CommandContext context)
            {
                if (CampaignEngine.Find(context.Committed, c.CampaignId) == null) return CommandResult.NotFound("No such campaign in this world.");
                var w = context.Edit();
                var instance = CampaignEngine.Find(w, c.CampaignId);
                var why = CampaignEngine.Refuse(w, instance, c.ArcId, QuestRuntime.Reporter(context));
                return why != null ? CommandResult.InvalidState(why) : CommandResult.Ok($"You refuse \"{CampaignEngine.Definition(instance)?.Arc(c.ArcId)?.Title}\".");
            }
        }

        private sealed class DecideHandler : ICommandHandler<MakeCampaignDecisionCommand>
        {
            public CommandResult Handle(MakeCampaignDecisionCommand c, CommandContext context)
            {
                var committed = CampaignEngine.Find(context.Committed, c.CampaignId);
                if (committed == null) return CommandResult.NotFound("No such campaign in this world.");
                var d = CampaignEngine.Definition(committed)?.Decision(c.DecisionId);
                if (d != null && d.SpeakerRole != null)
                    return CommandResult.InvalidState($"That is for {CampaignEngine.Text(context.Committed, committed, "{" + d.SpeakerRole + "}")} to ask you, face to face.");
                var pre = CampaignEngine.CanDecide(context.Committed, committed, c.DecisionId, c.ChoiceId);
                if (pre != null) return CommandResult.InvalidState(pre);
                var w = context.Edit();
                var why = Decide(context, w, CampaignEngine.Find(w, c.CampaignId), c.DecisionId, c.ChoiceId, "the journal");
                return why != null ? CommandResult.InvalidState(why) : CommandResult.Ok("Decided: " + CampaignEngine.Text(w, CampaignEngine.Find(w, c.CampaignId), d.Choice(c.ChoiceId).Summary) + ".");
            }
        }

        /// <summary>
        /// Makes a decision inside a command (the journal's, or a conversation's): the engine records and applies it, the
        /// player's decision log gets it, and the quest event lets a quest objective waiting on it finish.
        /// </summary>
        public static string Decide(CommandContext context, WorldData w, CampaignInstanceData instance, string decisionId, string choiceId, string source)
        {
            var why = CampaignEngine.Decide(w, instance, decisionId, choiceId, source, QuestRuntime.Reporter(context));
            if (why != null) return why;
            var choice = CampaignEngine.Definition(instance).Decision(decisionId).Choice(choiceId);
            var social = w.Player.Social;
            social.Decisions.Add(new DecisionData { Key = "campaign:" + instance.CampaignId + ":" + decisionId, Text = CampaignEngine.Text(w, instance, choice.Summary), At = QuestClock.Now(w) });
            while (social.Decisions.Count > PlayerSocialData.MaxDecisions) social.Decisions.RemoveAt(0);
            context.Quest(new QuestEvent { Kind = QuestEventKind.Decision, Subject = decisionId });
            return null;
        }
    }
}
