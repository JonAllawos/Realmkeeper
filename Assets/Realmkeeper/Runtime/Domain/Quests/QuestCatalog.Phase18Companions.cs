namespace Realmkeeper.Domain.Quests
{
    public static partial class QuestCatalog
    {
        public const string CompanionGoingHome = "companion_going_home";
        public const string CompanionGrave = "companion_grave";

        /// <summary>
        /// Phase 18 (ADR-0095): two more personal stories a trusted companion may ask about, each bound to a real person in
        /// the companion's life — family living elsewhere they have not seen since joining the road, or a friend who died
        /// while they were away. Each has two endings the player's words decide.
        /// </summary>
        private static void AddPhase18Companions()
        {
            Add(new QuestDefinition
            {
                Id = CompanionGoingHome, Name = "Going Home", Category = QuestCategory.Personal, DialogueId = "dlg_going_home",
                Description = "{giver} has not seen {kin} in {kin_home} since taking to the road with you. They would like to, with you there.",
                Introduction = "My {kin} — {kin}, in {kin_home}. I left without much of a goodbye. I'd like to go back and see them, just for a day. I'd like you to meet them, if you'll come.",
                Roles = new[] { Given("giver", RoleKind.Npc), Given("kin", RoleKind.Npc), Given("kin_home", RoleKind.Settlement) },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "go", Stage = 0, Kind = ObjectiveKind.ReachSettlement, Target = "role:kin_home", Text = "Go with {giver} to {kin_home}." },
                    new ObjectiveDefinition { Id = "meet", Stage = 1, Kind = ObjectiveKind.TalkTo, Target = "role:kin", Text = "Meet {kin} with {giver}." },
                },
                Failures = new[]
                {
                    new FailureDefinition { Kind = FailureKind.NpcDied, Key = "kin", Text = "{kin} died before {giver} could see them again." },
                    new FailureDefinition { Kind = FailureKind.NpcDied, Key = "giver", Text = "{giver} is dead." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 90, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "kin", Key = "Gratitude", Amount = 15, Outcome = "vouched" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Affinity", Amount = 10, Outcome = "left_alone" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMorale, Role = "giver", Amount = 15 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "giver", Key = "went home to {kin_home} with {player}", Amount = 70 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "kin", Key = "{player} told me {giver} is doing good work on the road", Amount = 60, Outcome = "vouched" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "kin", Key = "{giver} came home for a day, and we talked as we used to", Amount = 60, Outcome = "left_alone" },
                },
            });

            Add(new QuestDefinition
            {
                Id = CompanionGrave, Name = "Words at the Grave", Category = QuestCategory.Personal, DialogueId = "dlg_companion_grave",
                Description = "{lost_name}, a friend of {giver}'s, died in {grave_town} while {giver} was on the road with you. {giver} wants to go and say goodbye.",
                Introduction = "I heard — {lost_name} died, in {grave_town}, weeks back. I wasn't there. I'd like to go and stand at the grave, at least. Will you come?",
                Roles = new[] { Given("giver", RoleKind.Npc), Given("grave_town", RoleKind.Settlement) },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "go", Stage = 0, Kind = ObjectiveKind.ReachSettlement, Target = "role:grave_town", Text = "Go with {giver} to {grave_town}." },
                    new ObjectiveDefinition { Id = "words", Stage = 1, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Stand with {giver} at {lost_name}'s grave." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.NpcDied, Key = "giver", Text = "{giver} is dead." } },
                Rewards = new[] { new RewardDefinition { Kind = RewardKind.Experience, Amount = 80, Shown = "Experience" } },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMorale, Role = "giver", Amount = 10, Outcome = "spoke" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMorale, Role = "giver", Amount = 5, Outcome = "silent" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "giver", Key = "said goodbye to {lost_name} in {grave_town}, with {player}", Amount = 80 },
                },
            });
        }
    }

    public static partial class DialogueCatalog
    {
        private static void AddPhase18Companions()
        {
            Add(new DialogueDefinition
            {
                Id = "dlg_going_home", QuestId = QuestCatalog.CompanionGoingHome,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"{kin_home} isn't far now. I keep thinking what I'll say.\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "Of course I'll come.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.RevealLocation, role: "kin") } },
                            new DialogueResponse { Id = "decline", Text = "Not now — the road won't wait.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "later", Text = "Soon.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "kin",
                        Lines = new[] { L("{npc} stares, then laughs and cries at once. \"Look at you. Look at you! And who's this — the one you went off with?\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "vouch", Text = "{giver} has done good work on the road. You'd be proud.", Kind = ResponseKind.Continue, Conditions = new[] { Active }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "meet"), Do(DialogueConsequenceKind.SetOutcome, "vouched") } },
                            new DialogueResponse { Id = "leave", Text = "I'll leave you two to talk.", Kind = ResponseKind.Continue, Conditions = new[] { Active }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "meet"), Do(DialogueConsequenceKind.SetOutcome, "left_alone") } },
                        },
                    },
                },
            });

            Add(new DialogueDefinition
            {
                Id = "dlg_companion_grave", QuestId = QuestCatalog.CompanionGrave,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[]
                        {
                            L("\"{introduction}\"", Offerable),
                            L("{npc} stands a long time at the grave without speaking.", Active, QuestCondition.Of(ConditionKind.ObjectiveDone, "go")),
                            L("\"When we're in {grave_town}, I'll need a while.\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "We'll go together.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest) } },
                            new DialogueResponse { Id = "decline", Text = "I'm sorry. Not now.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "speak", Text = "Tell me about them.", Kind = ResponseKind.Continue, Conditions = new[] { Active, QuestCondition.Of(ConditionKind.ObjectiveDone, "go") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "words"), Do(DialogueConsequenceKind.SetOutcome, "spoke") } },
                            new DialogueResponse { Id = "silent", Text = "[Stand with them in silence.]", Kind = ResponseKind.Continue, Conditions = new[] { Active, QuestCondition.Of(ConditionKind.ObjectiveDone, "go") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "words"), Do(DialogueConsequenceKind.SetOutcome, "silent") } },
                            new DialogueResponse { Id = "later", Text = "When we get there.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                },
            });
        }
    }
}
