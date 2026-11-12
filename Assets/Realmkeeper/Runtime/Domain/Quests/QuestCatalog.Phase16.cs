namespace Realmkeeper.Domain.Quests
{
    public static partial class QuestCatalog
    {
        public const string RoadOutlaws = "road_outlaws";
        public const string BridgeTimber = "bridge_timber";

        /// <summary>
        /// Phase 16 (ADR-0080): quests a world event calls for (source key "event:{id}", so the offer lapses and an accepted
        /// quest fails when the event closes another way). Outlaws on a road: a guard of the road's town asks for them to be
        /// broken (they count down only from real fights with them). A stalled bridge: the mending town asks for timber
        /// (delivered planks become the timber its people mend the bridge with). Original text; no campaign canon.
        /// </summary>
        private static void AddPhase16()
        {
            Add(new QuestDefinition
            {
                Id = RoadOutlaws, Name = "The Road to {other}", Category = QuestCategory.BountyContract, DialogueId = "dlg_road_outlaws", ExpiresAfterDays = 14,
                Description = "Outlaws work the road between {home} and {other}. {giver} wants them broken.",
                Introduction = "There are outlaws on the road to {other}. They took a cart and they will take more. Break them, and the town will pay.",
                Roles = new[]
                {
                    Npc("giver", "same", "Guard", "Elder", "Resident"),
                    SettlementOf("home", "giver"),
                    new RoleSpec { Name = "other", Kind = RoleKind.Settlement },
                    new RoleSpec { Name = "creature", Kind = RoleKind.Creature, Value = "bandit" },
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "break", Stage = 0, Kind = ObjectiveKind.DefeatCreature, Target = "role:creature", Quantity = 3, Text = "Bring down {count} of the outlaws on the road between {home} and {other}." },
                    new ObjectiveDefinition { Id = "report", Stage = 1, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Tell {giver} the road is safer." },
                },
                Failures = new[]
                {
                    new FailureDefinition { Kind = FailureKind.NpcDied, Key = "giver", Text = "{giver} is dead." },
                    new FailureDefinition { Kind = FailureKind.CauseEnded, Text = "the outlaws are gone: others broke them first." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 150, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 50, Shown = "50 coin from {home}" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "home", Amount = 10, Shown = "Standing in {home}" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "giver", Amount = 5 },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "cleared_the_road" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "home", Key = "the road between {home} and {other} made safer" },
                },
            });

            Add(new QuestDefinition
            {
                Id = BridgeTimber, Name = "Timber for the Bridge", Category = QuestCategory.DeliveryRetrieval, DialogueId = "dlg_bridge_timber", ExpiresAfterDays = 14,
                Description = "{giver} of {home} needs timber to mend the bridge on the road to {other}.",
                Introduction = "The bridge to {other} is broken and our stores are bare of timber. Bring me {quantity} planks and we can mend it.",
                Roles = new[]
                {
                    Npc("giver", "same", "Smith", "Elder", "Labourer", "Resident"),
                    SettlementOf("home", "giver"),
                    new RoleSpec { Name = "other", Kind = RoleKind.Settlement },
                    new RoleSpec { Name = "item", Kind = RoleKind.Item, Value = "timber_plank" },
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "deliver", Stage = 0, Kind = ObjectiveKind.Deliver, Target = "role:item", To = "giver", Quantity = 3, Text = "Bring {giver} {quantity} timber planks." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.CauseEnded, Text = "the bridge was mended without you." } },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 60, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "home", Amount = 8, Shown = "Standing in {home}" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementResource, Role = "home", Key = "timber", Amount = 30 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "brought_bridge_timber" },
                },
            });
        }
    }

    public static partial class DialogueCatalog
    {
        private static void AddPhase16()
        {
            Add(Simple("dlg_road_outlaws", QuestCatalog.RoadOutlaws, "I'll break them.", "\"Any luck on the road to {other}?\"", "The road is safer now.", "break"));
            Add(new DialogueDefinition
            {
                Id = "dlg_bridge_timber", QuestId = QuestCatalog.BridgeTimber,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"Any timber yet?\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll find you some.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end", Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest) } },
                            new DialogueResponse { Id = "decline", Text = "Not now.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end", Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "give", Text = "Here: {quantity} planks.", Kind = ResponseKind.Give, Conditions = new[] { Active, C(ConditionKind.HasItem, "role:item", min: 1) }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.GiveItem, "role:item", 3) } },
                            new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                },
            });
        }

        /// <summary>A giver's dialogue without a place: the introduction, acceptance, and the report once the key objective is done.</summary>
        private static DialogueDefinition Simple(string id, string quest, string accept, string waiting, string report, string doneObjective) => new DialogueDefinition
        {
            Id = id, QuestId = quest,
            Nodes = new[]
            {
                new DialogueNode
                {
                    Id = "giver",
                    Lines = new[] { L("\"{introduction}\"", Offerable), L(waiting, Active) },
                    Responses = new[]
                    {
                        new DialogueResponse { Id = "accept", Text = accept, Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end", Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest) } },
                        new DialogueResponse { Id = "decline", Text = "Not now.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end", Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                        new DialogueResponse { Id = "report", Text = report, Kind = ResponseKind.Continue, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, doneObjective) }, Next = "end",
                            Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "report") } },
                        new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, doneObjective, negate: true) }, Next = "end" },
                    },
                },
            },
        };
    }
}
