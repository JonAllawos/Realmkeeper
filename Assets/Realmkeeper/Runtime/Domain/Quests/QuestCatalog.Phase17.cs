namespace Realmkeeper.Domain.Quests
{
    public static partial class QuestCatalog
    {
        // The Waystone Count (provisional demonstration campaign, ADR-0082): quests bound to campaign roles.
        public const string WaystoneLetter = "waystone_letter";
        public const string WaystoneTally = "waystone_tally";
        public const string WaystoneHold = "waystone_hold";
        public const string WaystoneKeeper = "waystone_keeper";
        public const string WaystoneKin = "waystone_kin";
        public const string WaystoneAccord = "waystone_accord";

        // Stories the world offers (ADR-0085) and a companion's second story (ADR-0087).
        public const string StoryLostCaravan = "story_lost_caravan";
        public const string StoryGrainRoad = "story_grain_road";
        public const string CompanionUnfinished = "companion_unfinished_business";

        private static RoleSpec Given(string name, RoleKind kind) => new RoleSpec { Name = name, Kind = kind };

        /// <summary>
        /// Phase 17. All text is Realmkeeper's own. The Waystone quests are labelled provisional in the campaign that uses
        /// them: two towns, a road and a toll-house, with no kingdom, antagonist or world history decided.
        /// </summary>
        private static void AddPhase17()
        {
            Add(new QuestDefinition
            {
                Id = WaystoneLetter, Name = "A Letter Across the Line", Category = QuestCategory.MainCampaign, DialogueId = "dlg_waystone_letter",
                Description = "{giver} wants a letter carried over the border to {recipient} in {far}, about the short count at the waystone.",
                Introduction = "You're bound for the crossing anyway, or you could be. Take this to {recipient} in {far}. It says we know the count is short and we want it put right before the month turns. I'd rather it went by someone who isn't from here.",
                Roles = new[] { Given("giver", RoleKind.Npc), Given("recipient", RoleKind.Npc), Given("far", RoleKind.Settlement), Given("home", RoleKind.Settlement) },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "cross", Stage = 0, Kind = ObjectiveKind.ReachSettlement, Target = "role:far", Text = "Cross the border to {far}." },
                    new ObjectiveDefinition { Id = "deliver", Stage = 1, Kind = ObjectiveKind.TalkTo, Target = "role:recipient", Text = "Put {giver}'s letter into {recipient}'s hands." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.NpcDied, Key = "recipient", Text = "{recipient} is dead; the letter has nobody to go to." } },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 80, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Trust", Amount = 6 },
                },
            });

            Add(new QuestDefinition
            {
                Id = WaystoneTally, Name = "Counting Carts", Category = QuestCategory.Investigation, DialogueId = "dlg_waystone_tally",
                Description = "{giver} pays the toll at the waystone every week and thinks the count kept by {tollkeeper} is wrong. They want someone to look.",
                Introduction = "I pay at the stone twice a week. Two carts, every time. Last month the toll-house had me down for three. Either I've a cart I've never seen, or someone is padding the count. Look at {tollkeeper}'s tallies, would you? I can't — they know my face.",
                Roles = new[] { Given("giver", RoleKind.Npc), Given("tollkeeper", RoleKind.Npc), Given("far", RoleKind.Settlement) },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "count", Stage = 0, Kind = ObjectiveKind.TalkTo, Target = "role:tollkeeper", Text = "Get a look at {tollkeeper}'s tally sticks in {far}." },
                    new ObjectiveDefinition { Id = "report", Stage = 1, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Tell {giver} what the tallies show." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.NpcDied, Key = "giver", Text = "{giver} is dead." } },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 70, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 20, Outcome = "proof", Shown = "{giver} will pay 20 coin for proof" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Gratitude", Amount = 10, Outcome = "proof" },
                },
            });

            Add(new QuestDefinition
            {
                Id = WaystoneHold, Name = "Who Holds the Road", Category = QuestCategory.MainCampaign, DialogueId = "dlg_waystone_hold",
                Description = "{giver} speaks for {far} at the waystone. Before the month turns, both towns want an answer: whose count is it?",
                Introduction = "You've seen both ends of this road now, which is more than most who argue about it. So I'll ask you plainly, and I'll listen: whose count should it be?",
                Roles = new[] { Given("giver", RoleKind.Npc), Given("home", RoleKind.Settlement), Given("far", RoleKind.Settlement), Given("reeve", RoleKind.Npc) },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "answer", Stage = 0, Kind = ObjectiveKind.Decision, Target = "decision:road_claim", Text = "Give {giver} your answer about the waystone count." },
                },
                Rewards = new[] { new RewardDefinition { Kind = RewardKind.Experience, Amount = 120, Shown = "Experience" } },
            });

            Add(new QuestDefinition
            {
                Id = WaystoneKeeper, Name = "The Toll-Keeper", Category = QuestCategory.MainCampaign, DialogueId = "dlg_waystone_keeper",
                Description = "Whatever the towns decide, someone has been keeping a crooked count. {giver} wants {tollkeeper} dealt with, one way or another.",
                Introduction = "The count was {tollkeeper}'s to keep, and it's wrong. I know {tollkeeper}. I'd rather not be the one who asks. Will you?",
                Roles = new[] { Given("giver", RoleKind.Npc), Given("tollkeeper", RoleKind.Npc), Given("far", RoleKind.Settlement) },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "confront", Stage = 0, Kind = ObjectiveKind.Decision, Target = "decision:tollkeeper_fate", To = "tollkeeper", Text = "Put the count to {tollkeeper}, and decide what's to be done about it." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.NpcDied, Key = "tollkeeper", Text = "{tollkeeper} is dead; whatever was in the count went with them." } },
                Rewards = new[] { new RewardDefinition { Kind = RewardKind.Experience, Amount = 90, Shown = "Experience" } },
            });

            Add(new QuestDefinition
            {
                Id = WaystoneKin, Name = "The Tally Sticks", Category = QuestCategory.Investigation, DialogueId = "dlg_waystone_kin",
                Description = "{tollkeeper} can't or won't answer for the count, but {giver}, who shares their house, knows where the tally sticks are kept.",
                Introduction = "You're here about the sticks. I know. They're in the chest by the hearth, and I'm tired of pretending I don't know what's on them.",
                Roles = new[] { Given("giver", RoleKind.Npc), Given("tollkeeper", RoleKind.Npc) },
                Objectives = new[] { new ObjectiveDefinition { Id = "sticks", Stage = 0, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Hear what {giver} knows about {tollkeeper}'s tallies." } },
                Rewards = new[] { new RewardDefinition { Kind = RewardKind.Experience, Amount = 60, Shown = "Experience" } },
            });

            Add(new QuestDefinition
            {
                Id = WaystoneAccord, Name = "Settling Up", Category = QuestCategory.MainCampaign, DialogueId = "dlg_waystone_accord",
                Description = "The waystone business is as settled as it is going to be. {giver} in {home} will want to hear how it ended.",
                Introduction = "So. Tell me how it stands at the stone.",
                Roles = new[] { Given("giver", RoleKind.Npc), Given("home", RoleKind.Settlement), Given("far", RoleKind.Settlement) },
                Objectives = new[] { new ObjectiveDefinition { Id = "tell", Stage = 0, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Tell {giver} in {home} how the waystone business ended." } },
                Rewards = new[] { new RewardDefinition { Kind = RewardKind.Experience, Amount = 100, Shown = "Experience" } },
            });

            // ---- Stories the world offers (ADR-0085) --------------------------------------------------------------
            Add(new QuestDefinition
            {
                Id = StoryLostCaravan, Name = "What Became of the Caravan", Category = QuestCategory.Emergent, DialogueId = "dlg_story_caravan", ExpiresAfterDays = 14,
                Description = "{giver} in {settlement} has had no proper word of the caravan {leader_name} took out on the road. All anyone says is that {account}.",
                Introduction = "The caravan {leader_name} took out hasn't come back, and I'm getting the story in pieces. {witness} heard it from someone who was there. Find out what they know — I'd rather hear it from you than from the market.",
                Roles = new[] { Given("giver", RoleKind.Npc), Given("witness", RoleKind.Npc), Given("settlement", RoleKind.Settlement) },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "ask", Stage = 0, Kind = ObjectiveKind.TalkTo, Target = "role:witness", Text = "Ask {witness} what they heard about the caravan." },
                    new ObjectiveDefinition { Id = "report", Stage = 1, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Tell {giver} what became of it." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.NpcDied, Key = "witness", Text = "{witness} is dead; nobody else heard it first-hand." } },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 60, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Gratitude", Amount = 10 },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = 2 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "giver", Key = "{player} found out what became of the caravan" },
                },
            });

            Add(new QuestDefinition
            {
                Id = StoryGrainRoad, Name = "Grain Down the Road", Category = QuestCategory.Emergent, DialogueId = "dlg_story_grain", ExpiresAfterDays = 10,
                Description = "{settlement} is going hungry while {supplier} has grain to spare. {giver} hopes {merchant} can be talked into sending some.",
                Introduction = "We're eating seed corn here. {supplier} isn't — I hear their stores are full. {merchant} there could send a wagon if they had a reason. Would you go and give them one?",
                Roles = new[] { Given("giver", RoleKind.Npc), Given("settlement", RoleKind.Settlement), Given("merchant", RoleKind.Npc), Given("supplier", RoleKind.Settlement) },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "ask", Stage = 0, Kind = ObjectiveKind.TalkTo, Target = "role:merchant", Text = "Persuade {merchant} in {supplier} to send grain to {settlement}." },
                    new ObjectiveDefinition { Id = "report", Stage = 1, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Tell {giver} the grain is coming." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.CauseEnded, Text = "{settlement} found food before the grain could be sent." } },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 90, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 12, Shown = "Standing in {settlement}" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementResource, Role = "settlement", Key = "food", Value = "var:share", Amount = 1 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementResource, Role = "supplier", Key = "food", Value = "var:share", Amount = -1 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "grain came down the road from {supplier}" },
                },
            });

            // ---- A companion's second story (ADR-0087) ---------------------------------------------------------------
            Add(new QuestDefinition
            {
                Id = CompanionUnfinished, Name = "Unfinished Business", Category = QuestCategory.Personal, DialogueId = "dlg_unfinished",
                Description = "{giver} parted badly with {rival}, who lives in {rival_home}. They want to see {rival} again, with you there.",
                Introduction = "There's someone in {rival_home} — {rival}. We said things, years back, that neither of us took back. I keep thinking about it on the road. Will you come with me? I don't trust myself to go alone.",
                Roles = new[] { Given("giver", RoleKind.Npc), Given("rival", RoleKind.Npc), Given("rival_home", RoleKind.Settlement) },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "go", Stage = 0, Kind = ObjectiveKind.ReachSettlement, Target = "role:rival_home", Text = "Go with {giver} to {rival_home}." },
                    new ObjectiveDefinition { Id = "meet", Stage = 1, Kind = ObjectiveKind.TalkTo, Target = "role:rival", Text = "Be there when {giver} faces {rival}." },
                    new ObjectiveDefinition { Id = "after", Stage = 2, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Talk it over with {giver} afterwards." },
                },
                Failures = new[]
                {
                    new FailureDefinition { Kind = FailureKind.NpcDied, Key = "rival", Text = "{rival} is dead. Whatever was left unsaid stays that way." },
                    new FailureDefinition { Kind = FailureKind.NpcDied, Key = "giver", Text = "{giver} is dead." },
                },
                Rewards = new[] { new RewardDefinition { Kind = RewardKind.Experience, Amount = 110, Shown = "Experience" } },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "giver", Key = "faced {rival} again, with {player} beside me", Amount = 70 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "rival", Key = "{giver} came back to {rival_home} to settle things", Amount = 60 },
                },
            });
        }
    }

    public static partial class DialogueCatalog
    {
        private static QuestCondition Flag(string key, string value = null, bool negate = false) => QuestCondition.Of(ConditionKind.CampaignFlag, key, value, negate: negate);
        private static QuestCondition Decided(string key, string choice = null, bool negate = false) => QuestCondition.Of(ConditionKind.CampaignDecision, key, choice, negate: negate);
        private static QuestCondition Done(string objective, bool negate = false) => QuestCondition.Of(ConditionKind.ObjectiveDone, objective, negate: negate);

        private static void AddPhase17()
        {
            // ---- A Letter Across the Line -------------------------------------------------------------------------
            Add(new DialogueDefinition
            {
                Id = "dlg_waystone_letter", QuestId = QuestCatalog.WaystoneLetter,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[]
                        {
                            L("\"{introduction}\"", Offerable),
                            L("\"Has it reached {recipient}? I'd like to know before I hear it from the market.\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll carry it.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.RevealLocation, role: "recipient") } },
                            new DialogueResponse { Id = "why", Text = "Why not send one of your own?", Kind = ResponseKind.RequestInformation, Conditions = new[] { Offerable }, Once = true, Next = "giver_why" },
                            new DialogueResponse { Id = "decline", Text = "Your quarrel, your letter.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "giver_why",
                        Lines = new[] { L("\"Because anyone from {home} who turns up at the stone with a letter is a provocation. You're just a traveller with a letter. They'll read it before they decide to be angry.\"") },
                        Responses = new[] { new DialogueResponse { Id = "back", Text = "Fair enough.", Kind = ResponseKind.Continue, Next = "giver" } },
                    },
                    new DialogueNode
                    {
                        Id = "recipient",
                        Lines = new[]
                        {
                            L("\"From {giver}?\" {npc} breaks the seal and reads it twice. \"'Put right before the month turns.' As if we were the ones short. Tell {giver} it's read. I'll want a word with you myself before long.\"", Active, Done("deliver", true)),
                            L("\"I've read it. I'll be in touch.\"", Active),
                            L("\"Yes?\""),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "give", Text = "A letter from {giver} in {home}.", Kind = ResponseKind.Give, Conditions = new[] { Active, Done("cross"), Done("deliver", true) }, Next = "recipient",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "deliver"), Do(DialogueConsequenceKind.SetOutcome, "delivered") } },
                            new DialogueResponse { Id = "bye", Text = "Goodbye.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                },
            });

            // ---- Counting Carts ---------------------------------------------------------------------------------------
            Add(new DialogueDefinition
            {
                Id = "dlg_waystone_tally", QuestId = QuestCatalog.WaystoneTally,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[]
                        {
                            L("\"{introduction}\"", Offerable),
                            L("\"So it's true. Three carts where I drove two.\" {npc} sits down heavily. \"I'd half hoped I was going soft in the head.\"", Active, Done("count"), Flag("evidence")),
                            L("\"Nothing you could swear to? Well. At least I asked.\"", Active, Done("count")),
                            L("\"Have you seen the sticks yet?\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll take a look.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.RevealLocation, role: "tollkeeper") } },
                            new DialogueResponse { Id = "decline", Text = "I'm not going to spy on a toll-house.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "report", Text = "Here's what the tallies show.", Kind = ResponseKind.Continue, Conditions = new[] { Active, Done("count") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "report") } },
                            new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "tollkeeper",
                        Lines = new[]
                        {
                            L("{npc} has a knife and a bundle of hazel sticks on the table, and doesn't put either down. \"Toll's two coin a wheel. You don't look like you've got wheels.\"", Active, Done("count", true)),
                            L("\"You've seen my sticks. Anything else?\"", Active),
                            L("\"Toll's two coin a wheel.\""),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "look", Text = "I'd like to see this month's tally. [Investigation]", Kind = ResponseKind.RequestInformation, Conditions = new[] { Active, Done("count", true) },
                                Check = Check("Investigation", 12), Next = "tollkeeper_found", NextOnFailure = "tollkeeper_nothing",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.CampaignFlag, "evidence", value: "tally", onSuccess: true), Do(DialogueConsequenceKind.CampaignFlag, "case_made", value: "tally", onSuccess: true),
                                    Do(DialogueConsequenceKind.SetOutcome, "proof", onSuccess: true), Do(DialogueConsequenceKind.SetOutcome, "no_proof", onSuccess: false),
                                    Do(DialogueConsequenceKind.Reveal, "waystone_tally", value: "{tollkeeper}'s tally for {far}-bound carts runs a notch long on every third stick.", onSuccess: true),
                                    Do(DialogueConsequenceKind.CompleteObjective, "count"),
                                } },
                            new DialogueResponse { Id = "read", Text = "Your hands are steadier than your answers. Something's wrong with the count. [Insight]", Kind = ResponseKind.Challenge, Conditions = new[] { Active, Done("count", true) },
                                Check = Check("Insight", 13, "-Trustfulness"), Next = "tollkeeper_found", NextOnFailure = "tollkeeper_nothing",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.CampaignFlag, "evidence", value: "word", onSuccess: true), Do(DialogueConsequenceKind.CampaignFlag, "case_made", value: "word", onSuccess: true),
                                    Do(DialogueConsequenceKind.SetOutcome, "proof", onSuccess: true), Do(DialogueConsequenceKind.SetOutcome, "no_proof", onSuccess: false),
                                    Do(DialogueConsequenceKind.Regard, "Fear", 5, onSuccess: true), Do(DialogueConsequenceKind.CompleteObjective, "count"),
                                } },
                            new DialogueResponse { Id = "bye", Text = "Just passing.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "tollkeeper_found",
                        Lines = new[] { L("{npc} watches you turn the sticks over. When you find the long notches, they don't argue. \"There's a difference,\" they say at last, \"between a mistake and a habit. I'll not tell you which this is.\"") },
                        Responses = new[] { new DialogueResponse { Id = "ok", Text = "You won't need to.", Kind = ResponseKind.End, Next = "end" } },
                    },
                    new DialogueNode
                    {
                        Id = "tollkeeper_nothing",
                        Lines = new[] { L("{npc} sweeps the sticks into a sack before you've made sense of them. \"Seen enough? Good. Two coin a wheel, and good day.\"") },
                        Responses = new[] { new DialogueResponse { Id = "ok", Text = "Good day.", Kind = ResponseKind.End, Next = "end" } },
                    },
                },
            });

            // ---- Who Holds the Road -----------------------------------------------------------------------------------
            var claim = QuestCondition.Of(ConditionKind.CampaignDecision, "road_claim", negate: true);
            Add(new DialogueDefinition
            {
                Id = "dlg_waystone_hold", QuestId = QuestCatalog.WaystoneHold,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[]
                        {
                            L("\"{introduction}\"", Offerable),
                            L("\"And now there's a caravan gone on the crossing, and each town says the other's tolls drove the carters to the back roads.\" {npc} rubs their eyes. \"So — whose count is it?\"",
                                Active, claim, QuestCondition.Of(ConditionKind.WorldEventOpen, "CaravanLost", "crossing")),
                            L("\"The crossing's barred and the carts are piling up on both sides. Everyone wants this settled yesterday. Whose count is it?\"",
                                Active, claim, QuestCondition.Of(ConditionKind.WorldEventOpen, "RoadBlocked", "crossing")),
                            L("\"You've been to the toll-house. I can see it in your face. Whose count is it?\"", Active, claim, Flag("evidence")),
                            L("\"Well? I've a town asking me every morning what the traveller thinks.\"", Active, claim),
                            L("\"It's decided. Whether it holds is up to the towns now, and to whoever keeps the next count.\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "Ask me, then.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "giver",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest) } },
                            new DialogueResponse { Id = "decline", Text = "Not today.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "home", Text = "The toll-house stands on {home}'s side of the stone. The count is theirs to keep.", Kind = ResponseKind.Continue, Conditions = new[] { Active, claim }, Next = "decided",
                                Consequences = new[] { Do(DialogueConsequenceKind.CampaignDecision, "road_claim", value: "home") } },
                            new DialogueResponse { Id = "far", Text = "{far} has kept that road open through two bad winters. Let them keep the count.", Kind = ResponseKind.Continue, Conditions = new[] { Active, claim }, Next = "decided",
                                Consequences = new[] { Do(DialogueConsequenceKind.CampaignDecision, "road_claim", value: "far") } },
                            new DialogueResponse { Id = "shared", Text = "Count it twice: one tally for each town, kept side by side and read together at each month's turn.", Kind = ResponseKind.Negotiate,
                                Conditions = new[] { Active, claim }, Next = "decided", ShowWhenUnavailable = true,
                                Requirements = new[] { Flag("case_made") }, UnavailableText = "You'd need proof the count is being cheated, or a better argument, before both towns would share it.",
                                Consequences = new[] { Do(DialogueConsequenceKind.CampaignDecision, "road_claim", value: "shared") } },
                            new DialogueResponse { Id = "argue", Text = "Neither town trusts the other's tally — so give them two. [Persuasion]", Kind = ResponseKind.Persuade,
                                Conditions = new[] { Active, claim, Flag("case_made", negate: true) }, Check = Check("Persuasion", 14, "-Trustfulness"), Once = true, Next = "giver", NextOnFailure = "giver",
                                Consequences = new[] { Do(DialogueConsequenceKind.CampaignFlag, "case_made", value: "argued", onSuccess: true), Do(DialogueConsequenceKind.Regard, "Respect", 4, onSuccess: true), Do(DialogueConsequenceKind.Regard, "Affinity", -2, onSuccess: false) } },
                            new DialogueResponse { Id = "friend", Text = "I've stood with {far}'s people before. Hear me as a friend: a shared count keeps both towns honest.", Kind = ResponseKind.Persuade,
                                Conditions = new[] { Active, claim, Flag("case_made", negate: true), QuestCondition.Of(ConditionKind.CampaignStanding, "far_faction", min: 20) }, Next = "giver",
                                Consequences = new[] { Do(DialogueConsequenceKind.CampaignFlag, "case_made", value: "friend") } },
                            // Phase 18 (ADR-0091): what the player has found elsewhere makes the case — precedent, not a roll.
                            new DialogueResponse { Id = "precedent_cairn", Text = "The towns by Cairn Watch kept their tower by turns for a generation — it's in the watch's own log. A shared count is old custom here, not a new trick.",
                                Kind = ResponseKind.Persuade, Conditions = new[] { Active, claim, Flag("case_made", negate: true), QuestCondition.Of(ConditionKind.KnowsDiscovery, "cairn_watch_log") }, Next = "giver",
                                Consequences = new[] { Do(DialogueConsequenceKind.CampaignFlag, "case_made", value: "precedent"), Do(DialogueConsequenceKind.Regard, "Respect", 3) } },
                            new DialogueResponse { Id = "precedent_ringers", Text = "At the old crossing they hung three bells for three trades and rang them together, none above the others. Your towns could keep one count the same way.",
                                Kind = ResponseKind.Persuade, Conditions = new[] { Active, claim, Flag("case_made", negate: true), QuestCondition.Of(ConditionKind.KnowsDiscovery, "ringers_order") }, Next = "giver",
                                Consequences = new[] { Do(DialogueConsequenceKind.CampaignFlag, "case_made", value: "precedent"), Do(DialogueConsequenceKind.Regard, "Respect", 3) } },
                            new DialogueResponse { Id = "apart", Text = "This is between your two towns. I carried the letter; I won't carry the quarrel.", Kind = ResponseKind.Continue, Conditions = new[] { Active, claim }, Next = "decided",
                                Consequences = new[] { Do(DialogueConsequenceKind.CampaignDecision, "road_claim", value: "apart") } },
                            new DialogueResponse { Id = "think", Text = "Let me think on it.", Kind = ResponseKind.End, Conditions = new[] { Active, claim }, Next = "end" },
                            new DialogueResponse { Id = "bye", Text = "Goodbye.", Kind = ResponseKind.End, Conditions = new[] { Active, Decided("road_claim") }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "decided",
                        Lines = new[]
                        {
                            L("{npc} is quiet a long moment. \"{home} will drink to you tonight. Here, they'll remember it longer.\"", Decided("road_claim", "home")),
                            L("\"Ours, then.\" {npc} doesn't smile, quite. \"{reeve} won't thank you. I will, for what it's worth.\"", Decided("road_claim", "far")),
                            L("\"Two sticks.\" {npc} laughs once, despite themself. \"It's so simple that both towns will hate it, and then they'll keep it.\"", Decided("road_claim", "shared")),
                            L("\"Then we'll settle it ourselves, and badly.\" {npc} shrugs. \"Perhaps that's honest of you. It isn't helpful.\"", Decided("road_claim", "apart")),
                        },
                        Responses = new[] { new DialogueResponse { Id = "ok", Text = "So be it.", Kind = ResponseKind.End, Next = "end" } },
                    },
                },
            });

            // ---- The Toll-Keeper --------------------------------------------------------------------------------------
            var fate = QuestCondition.Of(ConditionKind.CampaignDecision, "tollkeeper_fate", negate: true);
            var talks = QuestCondition.Of(ConditionKind.CharacterStatus, "npc", "Hostile", negate: true);
            Add(new DialogueDefinition
            {
                Id = "dlg_waystone_keeper", QuestId = QuestCatalog.WaystoneKeeper,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"Have you spoken to {tollkeeper}?\"", Active, fate), L("\"It's done, then. I won't ask how it felt.\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll speak to them.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.RevealLocation, role: "tollkeeper") } },
                            new DialogueResponse { Id = "decline", Text = "That's for {far} to sort out.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "tollkeeper",
                        Lines = new[]
                        {
                            L("{npc} won't look at you, and won't answer.", Active, fate, QuestCondition.Of(ConditionKind.CharacterStatus, "npc", "Hostile")),
                            L("{npc} sets down the knife before you've said a word. \"You've seen the sticks. Go on, then. Say it.\"", Active, fate, Flag("evidence")),
                            L("{npc} folds their arms. \"The count's the count. If you've something to say about it, say it.\"", Active, fate),
                            L("\"We're done, you and I.\"", Active),
                            L("\"Two coin a wheel.\""),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "expose", Text = "You'll tell {giver} yourself, or I will.", Kind = ResponseKind.Challenge, Conditions = new[] { Active, fate, talks }, Next = "tollkeeper_after",
                                Consequences = new[] { Do(DialogueConsequenceKind.CampaignDecision, "tollkeeper_fate", value: "expose") } },
                            new DialogueResponse { Id = "repay", Text = "Pay back what you took, a little each month, and this stays between us and the tally.", Kind = ResponseKind.Negotiate, Conditions = new[] { Active, fate, talks }, Next = "tollkeeper_after",
                                ShowWhenUnavailable = true, Requirements = new[] { Flag("evidence") }, UnavailableText = "You can't bargain over a theft you can't show.",
                                Consequences = new[] { Do(DialogueConsequenceKind.CampaignDecision, "tollkeeper_fate", value: "repay") } },
                            new DialogueResponse { Id = "cut", Text = "I'll keep quiet. For a share.", Kind = ResponseKind.Bribe, Conditions = new[] { Active, fate, talks }, Next = "tollkeeper_after",
                                Consequences = new[] { Do(DialogueConsequenceKind.CampaignDecision, "tollkeeper_fate", value: "cut") } },
                            // Phase 18 (ADR-0091): a harder, fairer way — owning it in public, with the evidence and a witness.
                            new DialogueResponse { Id = "witness", Text = "Stand up with me before both towns — you, {giver} and the sticks — and own it. Then pay it back where everyone can see. [Persuasion]",
                                Kind = ResponseKind.Persuade, Conditions = new[] { Active, fate, talks }, Next = "tollkeeper_after", NextOnFailure = "tollkeeper",
                                ShowWhenUnavailable = true, Requirements = new[] { Flag("evidence") }, UnavailableText = "Nobody owns up to a theft you can't show.",
                                Check = Check("Persuasion", 13, "-Courage"), Once = true,
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.CampaignDecision, "tollkeeper_fate", value: "witness", onSuccess: true), Do(DialogueConsequenceKind.Regard, "Affinity", -5, onSuccess: false),
                                } },
                            new DialogueResponse { Id = "bye", Text = "Not now.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "tollkeeper_after",
                        Lines = new[]
                        {
                            L("{npc} goes white, then red. \"Get out of my toll-house.\"", Decided("tollkeeper_fate", "expose")),
                            L("\"A little each month.\" {npc} lets out a breath they seem to have held since spring. \"I can do that. I will do that.\"", Decided("tollkeeper_fate", "repay")),
                            L("{npc} counts coins into your palm without a word, and doesn't look up until you've gone.", Decided("tollkeeper_fate", "cut")),
                            L("{npc} stares at the sticks a long time. \"Both towns, then. In the square, with everyone looking.\" A short, bitter laugh. \"Better that than the knock on the door at night.\"",
                                Decided("tollkeeper_fate", "witness")),
                        },
                        Responses = new[] { new DialogueResponse { Id = "ok", Text = "Leave.", Kind = ResponseKind.End, Next = "end" } },
                    },
                },
            });

            // ---- The Tally Sticks (the household, when the toll-keeper cannot answer) ---------------------------------
            Add(new DialogueDefinition
            {
                Id = "dlg_waystone_kin", QuestId = QuestCatalog.WaystoneKin,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"Read them if you like. Every third stick runs long. It always has, since the spring.\"", Active), L("\"I've told you all I know.\"") },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "Show me.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "giver",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest) } },
                            new DialogueResponse { Id = "sticks", Text = "Thank you. That can't have been easy.", Kind = ResponseKind.Continue, Conditions = new[] { Active }, Next = "end",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.CampaignFlag, "evidence", value: "kin"), Do(DialogueConsequenceKind.CampaignFlag, "case_made", value: "kin"),
                                    Do(DialogueConsequenceKind.Reveal, "waystone_sticks", value: "{giver} showed you {tollkeeper}'s tally sticks: every third one runs a notch long."),
                                    Do(DialogueConsequenceKind.CompleteObjective, "sticks"),
                                } },
                            new DialogueResponse { Id = "decline", Text = "Keep them. It's not my business.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                        },
                    },
                },
            });

            // ---- Settling Up --------------------------------------------------------------------------------------------
            Add(new DialogueDefinition
            {
                Id = "dlg_waystone_accord", QuestId = QuestCatalog.WaystoneAccord,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[]
                        {
                            L("\"{introduction}\"", Offerable),
                            L("\"Two tallies at the stone, read together.\" {npc} turns the idea over like a coin. \"I'll grumble about it in public. In private I'll sleep better.\"", Active, Decided("road_claim", "shared")),
                            L("\"Ours, and they know it. Good.\" {npc} hesitates. \"Though I'd not walk the crossing alone for a while, if I were you.\"", Active, Decided("road_claim", "home")),
                            L("\"You gave it to {far}.\" {npc}'s voice is flat. \"I suppose you had your reasons. I'd like to hear them, one day, when I'm less angry.\"", Active, Decided("road_claim", "far")),
                            L("\"You walked away from it.\" {npc} shrugs. \"Well, it's still our quarrel. It always was.\"", Active, Decided("road_claim", "apart")),
                            L("\"We settled it ourselves in the end, while you were elsewhere. Nobody's happy. That's how you know it's settled.\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll tell you.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "giver",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest) } },
                            new DialogueResponse { Id = "tell", Text = "That's how it stands.", Kind = ResponseKind.Continue, Conditions = new[] { Active }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "tell") } },
                        },
                    },
                },
            });

            // ---- What Became of the Caravan -----------------------------------------------------------------------------
            Add(new DialogueDefinition
            {
                Id = "dlg_story_caravan", QuestId = QuestCatalog.StoryLostCaravan,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"Well? What did {witness} say?\"", Active, Done("ask")), L("\"Any word yet?\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll ask {witness}.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.RevealLocation, role: "witness") } },
                            new DialogueResponse { Id = "decline", Text = "I'm sorry for your loss. I can't help.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "report", Text = "{account}. {leader_fate}", Kind = ResponseKind.Continue, Conditions = new[] { Active, Done("ask") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "report") } },
                            new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "witness",
                        Lines = new[] { L("{npc} lowers their voice. \"I had it from someone who was on that road: {account}. {leader_fate}\"", Active, Done("ask", true)), L("\"I've told you what I heard.\"") },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "thanks", Text = "Thank you. Their people will want to know.", Kind = ResponseKind.Continue, Conditions = new[] { Active, Done("ask", true) }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "ask") } },
                            new DialogueResponse { Id = "bye", Text = "Goodbye.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                },
            });

            // ---- Grain Down the Road ------------------------------------------------------------------------------------
            Add(new DialogueDefinition
            {
                Id = "dlg_story_grain", QuestId = QuestCatalog.StoryGrainRoad,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"A wagon? From {supplier}? You'll have every child in {settlement} singing your name.\"", Active, Done("ask")), L("\"Any word from {supplier}?\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll go to {supplier}.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.RevealLocation, role: "merchant") } },
                            new DialogueResponse { Id = "decline", Text = "I can't make another town feed yours.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "report", Text = "{merchant} is sending {share} measures of grain.", Kind = ResponseKind.Continue, Conditions = new[] { Active, Done("ask") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "report") } },
                            new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "merchant",
                        Lines = new[] { L("\"Grain for {settlement}?\" {npc} doesn't say no at once, which is something. \"Grain's money. What's in it for {supplier}?\"", Active, Done("ask", true)), L("\"The wagon's going. Don't make me regret it.\"", Active), L("\"Grain's money.\"") },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "persuade", Text = "A town that remembers who fed it in a bad year is a customer for life. [Persuasion]", Kind = ResponseKind.Persuade,
                                Conditions = new[] { Active, Done("ask", true) }, Check = Check("Persuasion", 13, "Greed"), Once = true, Next = "merchant", NextOnFailure = "merchant",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "ask", onSuccess: true), Do(DialogueConsequenceKind.Regard, "Respect", 4, onSuccess: true) } },
                            new DialogueResponse { Id = "pay", Text = "Then let me pay for the wagon. [40 coin]", Kind = ResponseKind.Bribe, Conditions = new[] { Active, Done("ask", true) }, Next = "merchant",
                                ShowWhenUnavailable = true, Requirements = new[] { QuestCondition.Of(ConditionKind.PlayerCoin, min: 40) }, UnavailableText = "You don't have 40 coin.",
                                Consequences = new[] { Do(DialogueConsequenceKind.PayCoin, amount: 40), Do(DialogueConsequenceKind.CompleteObjective, "ask") } },
                            new DialogueResponse { Id = "bye", Text = "I'll think of something.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                },
            });

            // ---- Unfinished Business --------------------------------------------------------------------------------------
            Add(new DialogueDefinition
            {
                Id = "dlg_unfinished", QuestId = QuestCatalog.CompanionUnfinished,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[]
                        {
                            L("\"{introduction}\"", Offerable),
                            L("\"Well.\" {npc} lets out a long breath. \"I said it. Whatever {rival} does with it now is theirs to carry. Thank you for standing there.\"", Active, Done("meet"), QuestCondition.Of(ConditionKind.Variable, "ending", "mended")),
                            L("\"You paid it for me.\" {npc} shakes their head, unsteadily. \"I'll not forget that. Funny how much lighter it feels, gone.\"", Active, Done("meet"), QuestCondition.Of(ConditionKind.Variable, "ending", "paid")),
                            L("\"I'm not sorry.\" {npc} is still angry, and doesn't hide it. \"But I'm glad it was you beside me and not someone who'd have talked me down.\"", Active, Done("meet"), QuestCondition.Of(ConditionKind.Variable, "ending", "defied")),
                            L("\"When we're in {rival_home}, I'll know it's time.\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll come with you.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.RevealLocation, role: "rival") } },
                            new DialogueResponse { Id = "decline", Text = "Some things are better left.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest), Do(DialogueConsequenceKind.Regard, "Affinity", -4) } },
                            new DialogueResponse { Id = "after", Text = "How are you?", Kind = ResponseKind.Continue, Conditions = new[] { Active, Done("meet") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "after") } },
                            new DialogueResponse { Id = "later", Text = "When we get there.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "rival",
                        Lines = new[] { L("{npc} sees who is with you, and their face closes like a door. \"I wondered if you'd have the nerve.\"", Active, Done("meet", true)), L("\"Is there something else?\"") },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "mend", Text = "[Let {giver} speak first, and apologise.]", Kind = ResponseKind.Continue, Conditions = new[] { Active, Done("go"), Done("meet", true) }, Next = "rival_after",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.SetVariable, "ending", value: "mended"), Do(DialogueConsequenceKind.Relation, "Rivalry", -30, value: "rival", role: "giver"),
                                    Do(DialogueConsequenceKind.Relation, "Affinity", 20, value: "rival", role: "giver"), Do(DialogueConsequenceKind.CompleteObjective, "meet"),
                                } },
                            new DialogueResponse { Id = "pay", Text = "{giver} owes you. Here's what's owed, and more. [25 coin]", Kind = ResponseKind.Give, Conditions = new[] { Active, Done("go"), Done("meet", true) }, Next = "rival_after",
                                ShowWhenUnavailable = true, Requirements = new[] { QuestCondition.Of(ConditionKind.PlayerCoin, min: 25) }, UnavailableText = "You don't have 25 coin.",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.PayCoin, amount: 25), Do(DialogueConsequenceKind.SetVariable, "ending", value: "paid"),
                                    Do(DialogueConsequenceKind.Relation, "Rivalry", -15, value: "rival", role: "giver"), Do(DialogueConsequenceKind.CompleteObjective, "meet"),
                                } },
                            new DialogueResponse { Id = "defy", Text = "{giver} came to say their piece, not to beg. Say yours, or step aside. [Intimidation]", Kind = ResponseKind.Intimidate,
                                Conditions = new[] { Active, Done("go"), Done("meet", true) }, Check = Check("Intimidation", 12, "Courage"), Next = "rival_after", NextOnFailure = "rival_after", Tags = ContentTags.Threats,
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.SetVariable, "ending", value: "defied"), Do(DialogueConsequenceKind.Relation, "Rivalry", 15, value: "rival", role: "giver"),
                                    Do(DialogueConsequenceKind.Regard, "Fear", 10, onSuccess: true), Do(DialogueConsequenceKind.Regard, "Hostility", 15, onSuccess: false), Do(DialogueConsequenceKind.CompleteObjective, "meet"),
                                } },
                            new DialogueResponse { Id = "bye", Text = "[Leave it for now.]", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "rival_after",
                        Lines = new[]
                        {
                            L("{npc} looks at {giver} for a long time. \"I'd stopped expecting that,\" they say. \"Come by before you leave. Or don't. But — thank you.\"", QuestCondition.Of(ConditionKind.Variable, "ending", "mended")),
                            L("{npc} weighs the purse, and doesn't count it. \"It was never the money. But I'll take it.\"", QuestCondition.Of(ConditionKind.Variable, "ending", "paid")),
                            L("{npc} steps back. \"Then we've nothing to say to each other. We never did.\"", QuestCondition.Of(ConditionKind.Variable, "ending", "defied")),
                        },
                        Responses = new[] { new DialogueResponse { Id = "ok", Text = "[Go.]", Kind = ResponseKind.End, Next = "end" } },
                    },
                },
            });
        }
    }
}
