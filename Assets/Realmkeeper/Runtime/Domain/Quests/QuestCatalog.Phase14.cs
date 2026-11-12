namespace Realmkeeper.Domain.Quests
{
    public static partial class QuestCatalog
    {
        public const string FoundersRegister = "founders_register";
        public const string FaneLantern = "fane_lantern";
        public const string RoadTolls = "road_tolls";
        public const string Culverts = "old_culverts";

        /// <summary>
        /// Phase 14 (ADR-0071): one quest for each new place that has a story to ask about. Every objective advances from
        /// real events (a place entered, a document read, a feature used, an item handed over, a choice made at the
        /// bandits' den); nothing completes on a flag of its own. Bound when the place is put into the world. All text is
        /// Realmkeeper's own; the histories are local and fix no campaign canon.
        /// </summary>
        private static void AddPhase14()
        {
            Add(new QuestDefinition
            {
                Id = FoundersRegister, Name = "The Founders' Register", Category = QuestCategory.Exploration, DialogueId = "dlg_register",
                Description = "{giver} wants to know what became of {settlement}'s first families, buried in {place}.",
                Introduction = "Our first families are buried in the old catacombs. There was a register of every burial — the sexton kept it down there. We've a quarrel over land that it would settle. Will you fetch me word of what it says?",
                Roles = new[]
                {
                    Npc("giver", "same", "Elder", "Resident", "Merchant"),
                    new RoleSpec { Name = "place", Kind = RoleKind.WorldObject },
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "find", Stage = 0, Kind = ObjectiveKind.DiscoverLocation, Target = "role:place", Text = "Learn where {place} lies." },
                    new ObjectiveDefinition { Id = "enter", Stage = 1, Kind = ObjectiveKind.VisitLocation, Target = "role:place", Text = "Go down into {place}." },
                    new ObjectiveDefinition { Id = "read", Stage = 2, Kind = ObjectiveKind.UseFeature, Target = "role:place#story:burial_register", Text = "Find the burial register and read it." },
                    new ObjectiveDefinition { Id = "fever", Stage = 2, Optional = true, Kind = ObjectiveKind.DiscoverInfo, Target = "ashgrave_fever", Text = "Learn why the lowest vault was sealed." },
                    new ObjectiveDefinition { Id = "report", Stage = 3, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Tell {giver} what the register says." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 150, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 10, Shown = "Standing in {settlement}" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Trust", Amount = 15 },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "the founders' register read again, from {place}" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "read_the_register" },
                },
            });

            Add(new QuestDefinition
            {
                Id = FaneLantern, Name = "The Lantern of the Fane", Category = QuestCategory.Exploration, DialogueId = "dlg_fane",
                Description = "{giver} of {faction} wants the old lantern brought back from {place}, which their founders built.",
                Introduction = "Before we held any town, we kept a fane on the hill, and a lantern burned in it. There are lights up there again — not ours. Bring the lantern home, and {faction} will remember it.",
                Roles = new[]
                {
                    Npc("giver", "same", "Elder", "Guard", "Resident"),
                    new RoleSpec { Name = "faction", Kind = RoleKind.Faction, Of = "giver" },
                    new RoleSpec { Name = "place", Kind = RoleKind.WorldObject },
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "find", Stage = 0, Kind = ObjectiveKind.DiscoverLocation, Target = "role:place", Text = "Learn where {place} lies." },
                    new ObjectiveDefinition { Id = "enter", Stage = 1, Kind = ObjectiveKind.VisitLocation, Target = "role:place", Text = "Go up into {place}." },
                    new ObjectiveDefinition { Id = "mural", Stage = 1, Optional = true, Kind = ObjectiveKind.DiscoverInfo, Target = "fane_founders", Text = "See whose fane it was." },
                    new ObjectiveDefinition { Id = "bring", Stage = 2, Kind = ObjectiveKind.Deliver, Target = "item:fane_lantern", To = "giver", Text = "Bring the fane's lantern to {giver}." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 250, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "faction", Amount = 15, Shown = "Standing with {faction}" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 40, Shown = "40 coin from {faction}'s purse" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "the fane's lantern brought home to {faction}" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "returned_the_lantern" },
                },
            });

            Add(new QuestDefinition
            {
                Id = RoadTolls, Name = "Tolls on the Road", Category = QuestCategory.Exploration, DialogueId = "dlg_tolls",
                Description = "Outlaws at {place} take a cut of every wagon on {settlement}'s road. {giver} wants it stopped — however it is done.",
                Introduction = "Every wagon out of here pays twice: once at market, once to the crows in the hills. They hole up at a cave above the road. Drive them off or talk them off — I don't care which, so long as the wagons come through whole.",
                Roles = new[]
                {
                    Npc("giver", "same", "Merchant", "Elder", "Innkeeper", "Smith", "Resident"),
                    new RoleSpec { Name = "place", Kind = RoleKind.WorldObject },
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "find", Stage = 0, Kind = ObjectiveKind.DiscoverLocation, Target = "role:place", Text = "Learn where {place} lies." },
                    new ObjectiveDefinition { Id = "enter", Stage = 1, Kind = ObjectiveKind.VisitLocation, Target = "role:place", Text = "Go to {place}." },
                    new ObjectiveDefinition { Id = "ledger", Stage = 1, Optional = true, Kind = ObjectiveKind.DiscoverInfo, Target = "crowfoot_tolls", Text = "Find out how organised they are." },
                    new ObjectiveDefinition { Id = "deal", Stage = 2, Kind = ObjectiveKind.Decision, Target = "crowfoot_dealt", Text = "Drive the outlaws off the road, or talk them off it." },
                    new ObjectiveDefinition { Id = "report", Stage = 3, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Tell {giver} the road is clear." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 250, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 50, Shown = "50 coin from the traders" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 15, Shown = "Standing in {settlement}" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "the road below {place} is safe again" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "cleared_the_road" },
                },
            });

            Add(new QuestDefinition
            {
                Id = Culverts, Name = "The Old Culverts", Category = QuestCategory.Exploration, DialogueId = "dlg_culverts",
                Description = "The floods come up through {settlement}'s cellars from {place}. The old sluice there could stop it.",
                Introduction = "Every spring the water comes up through our cellars. My grandmother said there's a sluice under the old culverts that kept it down, before the works were left to rot. Someone has to go down and turn it.",
                Roles = new[]
                {
                    Npc("giver", "same", "Elder", "Guard", "Merchant", "Resident", "Smith"),
                    new RoleSpec { Name = "place", Kind = RoleKind.WorldObject },
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "find", Stage = 0, Kind = ObjectiveKind.DiscoverLocation, Target = "role:place", Text = "Learn where {place} lies." },
                    new ObjectiveDefinition { Id = "enter", Stage = 1, Kind = ObjectiveKind.VisitLocation, Target = "role:place", Text = "Go down into {place}." },
                    new ObjectiveDefinition { Id = "plan", Stage = 1, Optional = true, Kind = ObjectiveKind.DiscoverInfo, Target = "culverts_plan", Text = "Find a plan of the works." },
                    new ObjectiveDefinition { Id = "sluice", Stage = 2, Kind = ObjectiveKind.UseFeature, Target = "role:place#story:sluice_wheel", Text = "Reach the sluice and turn it." },
                    new ObjectiveDefinition { Id = "report", Stage = 3, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Tell {giver} the sluice is turned." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 200, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 25, Shown = "25 coin" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 10, Shown = "Standing in {settlement}" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "the old sluice under {place} turned again" },
                },
            });
        }
    }

    public static partial class DialogueCatalog
    {
        /// <summary>Phase 14: the givers of the new places' quests.</summary>
        private static void AddPhase14()
        {
            Add(Giver("dlg_register", QuestCatalog.FoundersRegister,
                "\"You read it? Then it's settled — and I'm sorry about the fever year. I'd half forgotten we lost so many.\"", "\"Any word from the register?\"",
                "I'll go down and read it.", "Where are these catacombs?", "The register says what it says. Here it is, as best I can tell it.", "read"));
            Add(Giver("dlg_culverts", QuestCatalog.Culverts,
                "\"Turned? Then let the spring come. You've done this town a better turn than most will know.\"", "\"Have you been down to the sluice?\"",
                "I'll find the sluice.", "Where do the culverts start?", "The sluice is turned. The channels are dropping.", "sluice"));
            Add(Giver("dlg_tolls", QuestCatalog.RoadTolls,
                "\"Clear? Then the wagons roll tomorrow, and you'll drink free here for a month.\"", "\"Are the crows still on the road?\"",
                "I'll deal with them.", "Where is this cave?", "The road is clear. They won't trouble the wagons now.", "deal"));

            Add(new DialogueDefinition
            {
                Id = "dlg_fane", QuestId = QuestCatalog.FaneLantern,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[]
                        {
                            L("\"{introduction}\"", Offerable),
                            L("\"Is that — it is. The old lantern. We'll light it at the next gathering.\"", Active, C(ConditionKind.HasItem, "fane_lantern")),
                            L("\"Have you been up to the fane?\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll bring it back.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.RevealPlace, "place", value: "Discovered") } },
                            new DialogueResponse { Id = "where", Text = "Where is the fane?", Kind = ResponseKind.RequestInformation, Conditions = new[] { Offerable }, Next = "giver", Once = true,
                                Consequences = new[] { Do(DialogueConsequenceKind.RevealPlace, "place", value: "Discovered") } },
                            new DialogueResponse { Id = "decline", Text = "Not now.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "give", Text = "Here is the lantern.", Kind = ResponseKind.Continue, Conditions = new[] { Active, C(ConditionKind.HasItem, "fane_lantern") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.GiveItem, "fane_lantern", 1) } },
                            new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active, C(ConditionKind.HasItem, "fane_lantern", negate: true) }, Next = "end" },
                        },
                    },
                },
            });
        }

        /// <summary>A giver's dialogue: the introduction, acceptance, where the place is, and the report once a key objective is done.</summary>
        private static DialogueDefinition Giver(string id, string quest, string thanks, string waiting, string accept, string where, string report, string doneObjective) => new DialogueDefinition
        {
            Id = id, QuestId = quest,
            Nodes = new[]
            {
                new DialogueNode
                {
                    Id = "giver",
                    Lines = new[]
                    {
                        L("\"{introduction}\"", Offerable),
                        L(thanks, Active, C(ConditionKind.ObjectiveDone, doneObjective)),
                        L(waiting, Active),
                    },
                    Responses = new[]
                    {
                        new DialogueResponse { Id = "accept", Text = accept, Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                            Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.RevealPlace, "place", value: "Discovered") } },
                        new DialogueResponse { Id = "where", Text = where, Kind = ResponseKind.RequestInformation, Conditions = new[] { Offerable }, Next = "giver", Once = true,
                            Consequences = new[] { Do(DialogueConsequenceKind.RevealPlace, "place", value: "Discovered") } },
                        new DialogueResponse { Id = "decline", Text = "Not now.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                            Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                        new DialogueResponse { Id = "report", Text = report, Kind = ResponseKind.Continue, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, doneObjective) }, Next = "end",
                            Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "report") } },
                        new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, doneObjective, negate: true) }, Next = "end" },
                    },
                },
            },
        };
    }
}
