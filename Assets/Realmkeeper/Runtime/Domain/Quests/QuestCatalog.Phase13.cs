namespace Realmkeeper.Domain.Quests
{
    public static partial class QuestCatalog
    {
        public const string LostBelow = "lost_below";

        /// <summary>
        /// Phase 13 (ADR-0065, ADR-0066): a rescue and escort in a persistent place. The captive is a real resident, held in
        /// the abandoned mine from the day the world began; the giver is someone from the same settlement. Bound when the
        /// mine is placed in the world (the binder takes the given roles as they are). All text is Realmkeeper's own.
        /// </summary>
        private static void AddPhase13()
        {
            Add(new QuestDefinition
            {
                Id = LostBelow, Name = "Lost Below", Category = QuestCategory.Exploration, DialogueId = "dlg_lost",
                Description = "{captive} went up to {place} after scrap silver and has not come back. {giver} fears the worst.",
                Introduction = "{captive} went up to the old seam three days back — said there was silver left in the spoil. Nobody's seen them since. I can't go in there. Can you?",
                Roles = new[]
                {
                    Npc("giver", "same", "Resident", "Farmer", "Labourer", "Merchant", "Innkeeper", "Elder"),
                    new RoleSpec { Name = "captive", Kind = RoleKind.Npc },
                    new RoleSpec { Name = "place", Kind = RoleKind.WorldObject },
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "find", Stage = 0, Kind = ObjectiveKind.DiscoverLocation, Target = "role:place", Text = "Learn where {place} lies." },
                    new ObjectiveDefinition { Id = "enter", Stage = 1, Kind = ObjectiveKind.VisitLocation, Target = "role:place", Text = "Go into {place}." },
                    new ObjectiveDefinition { Id = "lower", Stage = 1, Optional = true, Kind = ObjectiveKind.ReachFloor, Target = "role:place#1", Text = "Get down to the lower level." },
                    new ObjectiveDefinition { Id = "rescue", Stage = 2, Kind = ObjectiveKind.Rescue, Target = "role:captive", Text = "Find {captive} and free them." },
                    new ObjectiveDefinition { Id = "protect", Stage = 3, Optional = true, Hidden = true, Kind = ObjectiveKind.Protect, Target = "role:captive", Text = "Keep {captive} alive through the ambush." },
                    new ObjectiveDefinition { Id = "home", Stage = 3, Kind = ObjectiveKind.Escort, Target = "role:captive", Text = "Bring {captive} home to {settlement}." },
                    new ObjectiveDefinition { Id = "report", Stage = 4, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Tell {giver} that {captive} is home." },
                },
                Failures = new[]
                {
                    new FailureDefinition { Kind = FailureKind.NpcDied, Key = "captive", Text = "{captive} did not come out of {place} alive." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 200, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 30, Shown = "{giver} will give what they can spare: 30 coin" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Gratitude", Amount = 25 },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "captive", Key = "Gratitude", Amount = 35 },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "captive", Key = "Trust", Amount = 15 },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "captive", Key = "{player} carried me out of {place}", Amount = 80 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "rescued_neighbour" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "{captive} brought home from {place}" },
                },
            });
        }
    }

    public static partial class DialogueCatalog
    {
        /// <summary>Phase 13: the worried neighbour (giver) and the person held in the mine (captive).</summary>
        private static void AddPhase13()
        {
            Add(new DialogueDefinition
            {
                Id = "dlg_lost", QuestId = QuestCatalog.LostBelow,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[]
                        {
                            L("\"{introduction}\"", Offerable),
                            L("\"You found them? And they're home? Thank you — I can't say it better than that.\"", Active, C(ConditionKind.ObjectiveDone, "home")),
                            L("\"Any word of {captive}?\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll go up and look.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.RevealPlace, "place", value: "Discovered") } },
                            new DialogueResponse { Id = "where", Text = "Where is this seam?", Kind = ResponseKind.RequestInformation, Conditions = new[] { Offerable }, Next = "giver", Once = true,
                                Consequences = new[] { Do(DialogueConsequenceKind.RevealPlace, "place", value: "Discovered") } },
                            new DialogueResponse { Id = "decline", Text = "I can't, not now.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "report", Text = "{captive} is home.", Kind = ResponseKind.Continue, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "home") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "report") } },
                            new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "home", negate: true) }, Next = "end" },
                        },
                    },
                },
            });

            // The watch at a fort's gate (Phase 13, ADR-0066): leave to pass, by standing, words or coin.
            Add(new DialogueDefinition
            {
                Id = "dlg_gate",
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "start",
                        Lines = new[]
                        {
                            L("{npc} steps into the gateway. \"That's far enough. State your business.\""),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "standing", Text = "You know me. Let me through.", Kind = ResponseKind.Continue, Next = "end",
                                Conditions = new[] { C(ConditionKind.FactionStanding, "npc", min: 30) },
                                Consequences = new[] { Do(DialogueConsequenceKind.GrantPassage, value: "watch") } },
                            new DialogueResponse { Id = "persuade", Text = "I've business with the captain — ask him yourself if you like.", Kind = ResponseKind.Persuade, Next = "end", NextOnFailure = "start", Once = true,
                                Check = new DialogueCheck { Skill = "Persuasion", Dc = 14, ResistTrait = "Discipline" },
                                Consequences = new[] { Do(DialogueConsequenceKind.GrantPassage, value: "watch", onSuccess: true), Do(DialogueConsequenceKind.Regard, "Trust", -3, onSuccess: false) } },
                            new DialogueResponse { Id = "bribe", Text = "[10 coin] For your trouble.", Kind = ResponseKind.Continue, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.PayCoin, amount: 10), Do(DialogueConsequenceKind.GrantPassage, value: "watch"), Do(DialogueConsequenceKind.Notoriety, amount: 2) } },
                            new DialogueResponse { Id = "leave", Text = "No business. I'll go.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                },
            });
        }
    }
}
