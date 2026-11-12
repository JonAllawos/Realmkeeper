namespace Realmkeeper.Domain.Quests
{
    public static partial class QuestCatalog
    {
        public const string StoryRefugees = "story_refugees";
        public const string StoryUnrest = "story_unrest";
        public const string StoryLongWay = "story_long_way";
        public const string StoryWordHome = "story_word_home";
        public const string StoryConflict = "story_conflict";

        /// <summary>
        /// Phase 18 (ADR-0093): more stories the world tells itself, each from a real cause — refugees arriving (settlement
        /// life), unrest in a town (regional security), trade cut off on a road (economy), a death far from a friend
        /// (personal). Bound by <c>StorySynthesis</c> to the people really involved; each can expire if left too long; each
        /// changes the world through the existing consequence paths, and the unrest story settles the event that caused it.
        /// </summary>
        private static void AddPhase18Stories()
        {
            // Phase 18 (ADR-0090): two factions at odds at a crossing — the player can back a side, the other side, try to
            // make peace, stay out of it, or ignore it (the offer lapses). The world goes on either way.
            Add(new QuestDefinition
            {
                Id = StoryConflict, Name = "Bad Blood at the Crossing", Category = QuestCategory.Faction, DialogueId = "dlg_story_conflict", ExpiresAfterDays = 10,
                Description = "{faction} and {other} are at odds over the crossing by {settlement}. {giver} wants to know whose side you are on.",
                Introduction = "You'll have seen the soldiers at the crossing. {account} It's getting worse, not better. {faction} could use a friend in this — or someone who could talk sense into both sides, if there's such a person.",
                Roles = new[]
                {
                    Npc("giver", "same", "Elder", "Guard"),
                    new RoleSpec { Name = "faction", Kind = RoleKind.Faction, Of = "giver" },
                    new RoleSpec { Name = "other", Kind = RoleKind.Faction },
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "supported", Stage = 0, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:conflict_supported", Outcome = "supported", Text = "Stand with {faction}." },
                    new ObjectiveDefinition { Id = "opposed", Stage = 0, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:conflict_opposed", Outcome = "opposed", Text = "Tell {giver} that {other} is in the right." },
                    new ObjectiveDefinition { Id = "mediated", Stage = 0, Group = "resolve", Kind = ObjectiveKind.Persuade, Target = "role:giver", Outcome = "mediated", Text = "Talk {faction} into meeting {other} halfway." },
                    new ObjectiveDefinition { Id = "neutral", Stage = 0, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:conflict_neutral", Outcome = "neutral", Text = "Stay out of it." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 100, Outcome = "supported", Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 100, Outcome = "opposed" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 200, Outcome = "mediated" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 30, Outcome = "neutral" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "faction", Amount = 15, Outcome = "supported", Shown = "Standing with {faction}" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "other", Amount = -10, Outcome = "supported" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "other", Amount = 10, Outcome = "opposed" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "faction", Amount = -15, Outcome = "opposed" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "faction", Amount = 8, Outcome = "mediated" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "other", Amount = 8, Outcome = "mediated" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.FactionRelation, Role = "faction", Value = "other", Amount = -10, Outcome = "supported" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.FactionInfluence, Role = "faction", Amount = 3, Outcome = "supported" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.FactionRelation, Role = "faction", Value = "other", Amount = -5, Outcome = "opposed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcRegard, Role = "giver", Key = "Hostility", Amount = 20, Outcome = "opposed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.FactionRelation, Role = "faction", Value = "other", Amount = 30, Outcome = "mediated" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = 3, Outcome = "mediated" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "{faction} and {other} agreed to talk at the crossing", Outcome = "mediated" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "made_peace_at_the_crossing", Outcome = "mediated" },
                },
            });

            Add(new QuestDefinition
            {
                Id = StoryRefugees, Name = "Room at the Hearth", Category = QuestCategory.Emergent, DialogueId = "dlg_story_refugees", ExpiresAfterDays = 10,
                Description = "People driven from their homes have reached {settlement}. {giver} must decide whether the town can take them in.",
                Introduction = "They came in last night — families, mostly, with what they could carry. {account} We've barely enough for ourselves. Do we take them in, or send them on? I'd hear what you think. You've seen more of the roads than any of us.",
                Roles = new[] { Npc("giver", "same", "Elder", "Guard", "Resident"), SettlementOf("settlement", "giver") },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "welcomed", Stage = 0, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:refugees_welcomed", Outcome = "welcomed",
                        Text = "Advise {giver} to take them in." },
                    new ObjectiveDefinition { Id = "sent_on", Stage = 0, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:refugees_sent_on", Outcome = "sent_on",
                        Text = "Advise {giver} to send them on." },
                    new ObjectiveDefinition { Id = "fed", Stage = 0, Group = "resolve", Kind = ObjectiveKind.Deliver, Target = "item:grain_sack", To = "giver", Quantity = 3, Outcome = "fed",
                        Text = "Bring {giver} 3 sacks of grain, so the newcomers can be fed without the town going short." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.TimeLimit, Amount = 14, Text = "The newcomers moved on before anything was decided." } },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 80, Outcome = "welcomed", Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 50, Outcome = "sent_on" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 120, Outcome = "fed" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 15, Outcome = "fed", Shown = "Standing in {settlement}" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementProsperity, Role = "settlement", Amount = 3, Outcome = "welcomed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementResource, Role = "settlement", Key = "food", Amount = -30, Outcome = "welcomed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = -1, Outcome = "welcomed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "families from the road taken in at {settlement}", Outcome = "welcomed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = 1, Outcome = "sent_on" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "giver", Key = "{player} told us to send the families on", Amount = 50, Outcome = "sent_on" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementProsperity, Role = "settlement", Amount = 3, Outcome = "fed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = 2, Outcome = "fed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ResolveWorldEvent, Key = "event", Value = "the newcomers settled at {settlement}", Outcome = "welcomed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ResolveWorldEvent, Key = "event", Value = "the newcomers moved on from {settlement}", Outcome = "sent_on" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ResolveWorldEvent, Key = "event", Value = "the newcomers fed and settled at {settlement}", Outcome = "fed" },
                },
            });

            Add(new QuestDefinition
            {
                Id = StoryUnrest, Name = "Hard Words in {settlement}", Category = QuestCategory.Emergent, DialogueId = "dlg_story_unrest", ExpiresAfterDays = 10,
                Description = "{settlement} is restless, and {agitator} is saying out loud what others mutter. {giver} wants it settled before it turns ugly.",
                Introduction = "You'll have heard the shouting. {agitator} has half the lane behind them now — bread, wages, who gets heard and who doesn't. I can break it up, or someone can talk to them. I'd rather the second.",
                Roles = new[]
                {
                    Npc("giver", "same", "Guard", "Elder"),
                    Npc("agitator", "same", "Labourer", "Farmer", "Resident", "Smith"),
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "hear", Stage = 0, Kind = ObjectiveKind.TalkTo, Target = "role:agitator", Text = "Hear what {agitator} has to say." },
                    new ObjectiveDefinition { Id = "heard", Stage = 1, Group = "resolve", Kind = ObjectiveKind.Persuade, Target = "role:agitator", Outcome = "heard",
                        Text = "Persuade {agitator} to bring the lane's grievances to {giver}, not into the street." },
                    new ObjectiveDefinition { Id = "fed", Stage = 1, Group = "resolve", Kind = ObjectiveKind.Deliver, Target = "item:grain_sack", To = "agitator", Quantity = 2, Outcome = "fed",
                        Text = "Bring {agitator} 2 sacks of grain for the lane." },
                    new ObjectiveDefinition { Id = "crushed", Stage = 1, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:unrest_crushed", Outcome = "crushed",
                        Text = "Tell {giver} to break it up." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.TimeLimit, Amount = 14, Text = "The trouble in {settlement} ran its course without you." } },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 120, Outcome = "heard", Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 100, Outcome = "fed" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 60, Outcome = "crushed" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "agitator", Key = "Trust", Amount = 20, Outcome = "heard" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Respect", Amount = 15, Outcome = "heard" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 10, Outcome = "fed" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Gratitude", Amount = 10, Outcome = "crushed" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = 6, Outcome = "heard" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = 4, Outcome = "fed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = 3, Outcome = "crushed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcRegard, Role = "agitator", Key = "Hostility", Amount = 35, Outcome = "crushed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMorale, Role = "agitator", Amount = -20, Outcome = "crushed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.Notoriety, Amount = 3, Outcome = "crushed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ResolveWorldEvent, Key = "event", Value = "{agitator} was heard, and the lane went home", Outcome = "heard" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ResolveWorldEvent, Key = "event", Value = "grain for the lane quieted {settlement}", Outcome = "fed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ResolveWorldEvent, Key = "event", Value = "the guard broke up the crowd in {settlement}", Outcome = "crushed" },
                },
            });

            Add(new QuestDefinition
            {
                Id = StoryLongWay, Name = "The Long Way Round", Category = QuestCategory.Emergent, DialogueId = "dlg_story_long_way", ExpiresAfterDays = 10,
                Description = "Trade on the road between {settlement} and {market} has stopped. {giver} will pay to have a load carried through to {buyer} by whatever way is open.",
                Introduction = "{account} My cloth's sitting in the store and {buyer} in {market} is waiting on it. No carter will take the road. You travel — carry two bolts through for me, any way you can, and I'll pay you for the trouble.",
                Roles = new[]
                {
                    Npc("giver", "same", "Merchant", "Smith", "Innkeeper"),
                    SettlementOf("settlement", "giver"),
                    new RoleSpec { Name = "buyer", Kind = RoleKind.Npc },
                    new RoleSpec { Name = "market", Kind = RoleKind.Settlement },
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "carry", Stage = 0, Kind = ObjectiveKind.Deliver, Target = "item:merchants_cloth", To = "buyer", Quantity = 2, Text = "Carry two bolts of cloth to {buyer} in {market}." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.TimeLimit, Amount = 20, Text = "{buyer} found the cloth elsewhere." } },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 80, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 40, Shown = "40 coin from {giver}" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Trust", Amount = 15 },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementProsperity, Role = "settlement", Amount = 2 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementProsperity, Role = "market", Amount = 2 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "buyer", Key = "{player} brought the cloth through when the road was shut", Amount = 50 },
                },
            });

            Add(new QuestDefinition
            {
                Id = StoryWordHome, Name = "Word for a Friend", Category = QuestCategory.Personal, DialogueId = "dlg_story_word_home", ExpiresAfterDays = 10,
                Description = "{deceased_name} has died in {settlement}. Their friend {friend}, in {far}, does not know yet.",
                Introduction = "You'll not have known {deceased_name}. Few did, outside this lane. But they had a friend in {far} — {friend} — who'll not have heard. Somebody ought to tell them properly, not let it reach them as gossip.",
                Roles = new[]
                {
                    Npc("giver", "same", "Resident", "Elder", "Innkeeper", "Labourer", "Farmer", "Merchant", "Smith", "Guard"),
                    SettlementOf("settlement", "giver"),
                    new RoleSpec { Name = "friend", Kind = RoleKind.Npc },
                    new RoleSpec { Name = "far", Kind = RoleKind.Settlement },
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "tell", Stage = 0, Kind = ObjectiveKind.TalkTo, Target = "role:friend", Text = "Tell {friend} in {far} that {deceased_name} has died." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.TimeLimit, Amount = 20, Text = "The news reached {friend} as gossip, as it was always going to." } },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 60, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "friend", Key = "Gratitude", Amount = 15 },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Trust", Amount = 10 },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "friend", Key = "{player} came to tell me that {deceased_name} had died", Amount = 70 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMorale, Role = "friend", Amount = -8 },
                },
            });
        }
    }

    public static partial class DialogueCatalog
    {
        private static void AddPhase18Stories()
        {
            Add(new DialogueDefinition
            {
                Id = "dlg_story_conflict", QuestId = QuestCatalog.StoryConflict,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"Well? Are you with us, or not?\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "Tell me what's happened.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "giver",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest) } },
                            new DialogueResponse { Id = "support", Text = "I'm with {faction}.", Kind = ResponseKind.Continue, Conditions = new[] { Active }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.Decision, "conflict_supported", value: "Stood with {faction} against {other}"), Do(DialogueConsequenceKind.SetOutcome, "supported") } },
                            new DialogueResponse { Id = "oppose", Text = "{other} has the right of it, and you know it.", Kind = ResponseKind.Challenge, Conditions = new[] { Active }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.Decision, "conflict_opposed", value: "Told {faction} that {other} was in the right"), Do(DialogueConsequenceKind.SetOutcome, "opposed") } },
                            new DialogueResponse { Id = "mediate", Text = "Neither side can afford a war over a road. Meet them halfway — I'll carry the word. [Persuasion]", Kind = ResponseKind.Persuade,
                                Conditions = new[] { Active }, Check = Check("Persuasion", 14, "Ambition"), Once = true, Next = "end", NextOnFailure = "giver",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.CompleteObjective, "mediated", onSuccess: true), Do(DialogueConsequenceKind.SetOutcome, "mediated", onSuccess: true),
                                    Do(DialogueConsequenceKind.Decision, "conflict_mediated", value: "Brought {faction} to meet {other} halfway", onSuccess: true),
                                    Do(DialogueConsequenceKind.Regard, "Affinity", -5, onSuccess: false),
                                } },
                            new DialogueResponse { Id = "mediate_friend", Text = "You know I've no reason to lie to you, and {other} knows it too. Let me carry an offer between you.", Kind = ResponseKind.Persuade,
                                Conditions = new[] { Active, C(ConditionKind.FactionStanding, "other", min: 20), C(ConditionKind.FactionStanding, "faction", min: 20) }, Next = "end",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.CompleteObjective, "mediated"), Do(DialogueConsequenceKind.SetOutcome, "mediated"),
                                    Do(DialogueConsequenceKind.Decision, "conflict_mediated", value: "Carried an offer between {faction} and {other}, trusted by both"),
                                } },
                            new DialogueResponse { Id = "neutral", Text = "This isn't my quarrel. I'll not take a side.", Kind = ResponseKind.Continue, Conditions = new[] { Active }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.Decision, "conflict_neutral", value: "Stayed out of the quarrel between {faction} and {other}"), Do(DialogueConsequenceKind.SetOutcome, "neutral") } },
                            new DialogueResponse { Id = "later", Text = "Let me think on it.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                },
            });

            Add(new DialogueDefinition
            {
                Id = "dlg_story_refugees", QuestId = QuestCatalog.StoryRefugees,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"Well? They're still in the barn, waiting to hear.\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "Let me think about what's best.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "giver",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest) } },
                            new DialogueResponse { Id = "welcome", Text = "Take them in. Hands are hands, and they'll work.", Kind = ResponseKind.Continue, Conditions = new[] { Active }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.Decision, "refugees_welcomed", value: "Advised {settlement} to take in the families from the road"), Do(DialogueConsequenceKind.SetOutcome, "welcomed") } },
                            new DialogueResponse { Id = "send_on", Text = "Send them on. The town can't carry them through the winter.", Kind = ResponseKind.Continue, Conditions = new[] { Active }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.Decision, "refugees_sent_on", value: "Advised {settlement} to send the families on"), Do(DialogueConsequenceKind.SetOutcome, "sent_on") } },
                            new DialogueResponse { Id = "feed", Text = "Here — three sacks of grain. Feed them, and let them stay.", Kind = ResponseKind.Give, Conditions = new[] { Active, C(ConditionKind.HasItem, "grain_sack", min: 3) }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.GiveItem, "grain_sack", 3), Do(DialogueConsequenceKind.SetOutcome, "fed") } },
                            new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                },
            });

            Add(new DialogueDefinition
            {
                Id = "dlg_story_unrest", QuestId = QuestCatalog.StoryUnrest,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"Have you spoken to {agitator}?\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll talk to them.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.RevealLocation, role: "agitator") } },
                            new DialogueResponse { Id = "crush", Text = "Break it up, before it spreads.", Kind = ResponseKind.Continue, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "hear") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.Decision, "unrest_crushed", value: "Told the guard of {settlement} to break up the crowd"), Do(DialogueConsequenceKind.SetOutcome, "crushed") } },
                            new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "agitator",
                        Lines = new[]
                        {
                            L("\"Bread's dear, work's thin, and nobody up the hill listens. You want me to go home quietly? Give me a reason.\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "hear", Text = "Tell me, then. All of it.", Kind = ResponseKind.Continue, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "hear", negate: true) }, Next = "agitator",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "hear"), Do(DialogueConsequenceKind.Regard, "Affinity", 5) } },
                            new DialogueResponse { Id = "persuade", Text = "Take it to {giver}, with the lane behind you, in daylight. They'll have to listen — and nobody gets hurt. [Persuasion]",
                                Kind = ResponseKind.Persuade, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "hear") }, Check = Check("Persuasion", 12, "-Trustfulness"), Once = true,
                                Next = "end", NextOnFailure = "agitator",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "heard", onSuccess: true), Do(DialogueConsequenceKind.SetOutcome, "heard", onSuccess: true) } },
                            new DialogueResponse { Id = "grain", Text = "Here — two sacks of grain, for the lane.", Kind = ResponseKind.Give, Conditions = new[] { Active, C(ConditionKind.HasItem, "grain_sack", min: 2) }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.GiveItem, "grain_sack", 2), Do(DialogueConsequenceKind.SetOutcome, "fed") } },
                            new DialogueResponse { Id = "bye", Text = "Goodbye.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                },
            });

            Add(new DialogueDefinition
            {
                Id = "dlg_story_long_way", QuestId = QuestCatalog.StoryLongWay,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"Has the cloth reached {buyer}?\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll carry it.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.ReceiveItem, "merchants_cloth", 2), Do(DialogueConsequenceKind.RevealLocation, role: "buyer") } },
                            new DialogueResponse { Id = "decline", Text = "Not this time.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "later", Text = "On my way.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "buyer",
                        Lines = new[] { L("\"From {giver}? By the long way? I'd given it up.\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "give", Text = "Two bolts, as promised.", Kind = ResponseKind.Give, Conditions = new[] { Active, C(ConditionKind.HasItem, "merchants_cloth", min: 2) }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.GiveItem, "merchants_cloth", 2) } },
                            new DialogueResponse { Id = "bye", Text = "Goodbye.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                },
            });

            Add(new DialogueDefinition
            {
                Id = "dlg_story_word_home", QuestId = QuestCatalog.StoryWordHome,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"Have you been to {far}?\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll tell them.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.RevealLocation, role: "friend") } },
                            new DialogueResponse { Id = "decline", Text = "It's not my place.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "friend",
                        Lines = new[] { L("{npc} reads your face before you speak. \"It's {deceased_name}, isn't it.\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "tell", Text = "I'm sorry. They died in {settlement}. Their neighbours thought you should hear it from someone, not from gossip.",
                                Kind = ResponseKind.Continue, Conditions = new[] { Active }, Next = "end", Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "tell") } },
                        },
                    },
                },
            });
        }
    }
}
