using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Quests
{
    /// <summary>
    /// The dialogue graphs (ADR-0058). "npc_general" is anyone's talk: a greeting that depends on how they feel about the
    /// player and their state, the Phase 4 topics answered from their own knowledge, and, added by the engine, a way into
    /// each quest they play a part in and their shop. Each quest's dialogue has one node per role. Lines and responses
    /// carry conditions; responses carry skill checks and consequences. All writing is Realmkeeper's own.
    /// </summary>
    public static partial class DialogueCatalog
    {
        public const string General = "npc_general";

        private static readonly Dictionary<string, DialogueDefinition> ById = new Dictionary<string, DialogueDefinition>(StringComparer.Ordinal);

        public static IEnumerable<DialogueDefinition> All => ById.Values;

        public static DialogueDefinition Find(string id) => id != null && ById.TryGetValue(id, out var d) ? d : null;

        private static void Add(DialogueDefinition d) => ById[d.Id] = d;

        private static QuestCondition C(ConditionKind k, string key = null, string value = null, int min = int.MinValue, int max = int.MaxValue, bool negate = false) => QuestCondition.Of(k, key, value, min, max, negate);
        private static DialogueConsequence Do(DialogueConsequenceKind k, string key = null, int amount = 0, string value = null, string role = null, bool? onSuccess = null) => DialogueConsequence.Of(k, key, amount, value, role, onSuccess);
        private static DialogueLine L(string text, params QuestCondition[] when) => new DialogueLine { Text = text, Conditions = when };
        private static DialogueCheck Check(string skill, int dc, string resist = null) => new DialogueCheck { Skill = skill, Dc = dc, ResistTrait = resist };

        private static readonly QuestCondition Offerable = QuestCondition.Of(ConditionKind.QuestState, "this", "Available|Discovered|Offered");
        private static readonly QuestCondition Active = QuestCondition.Of(ConditionKind.QuestState, "this", "Active");

        static DialogueCatalog()
        {
            // ---- Anyone's talk ----------------------------------------------------------------------------------------
            var topics = new List<DialogueResponse>();
            foreach (var (topic, label) in new[]
            {
                ("Themselves", "Tell me about yourself."), ("Settlement", "How are things here?"), ("Governor", "Who governs here?"), ("FactionLeader", "Who leads your people?"),
                ("News", "Any news?"), ("Rumours", "Heard any rumours?"), ("Work", "What's your work?"), ("Shops", "Where can I buy things?"), ("Goods", "What's for sale here?"),
                ("Prices", "How are prices?"), ("Factions", "Tell me about the factions."),
            })
                topics.Add(new DialogueResponse { Id = "ask_" + topic.ToLowerInvariant(), Text = label, Kind = ResponseKind.Ask, Next = "topics", Consequences = new[] { Do(DialogueConsequenceKind.AskTopic, topic) } });
            topics.Add(new DialogueResponse { Id = "back", Text = "Something else.", Kind = ResponseKind.Continue, Next = "start" });

            Add(new DialogueDefinition
            {
                Id = General,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "start",
                        Lines = new[]
                        {
                            L("{npc} looks at you hard. \"I know what you did. Don't think it's forgotten.\"", C(ConditionKind.KnowsCrime)), // Phase 14
                            L("{npc} keeps their distance. \"I've nothing to say to you.\"", C(ConditionKind.Regard, "Hostility", min: 50)),
                            L("{npc} watches you carefully. \"Yes? What do you want?\"", C(ConditionKind.Regard, "Fear", min: 40)),
                            L("\"There you are, {player}. Good to see a friendly face.\"", C(ConditionKind.Regard, "Gratitude", min: 20)),
                            L("{npc} is asleep, or near enough. \"Mm? Come back in daylight.\"", C(ConditionKind.NpcActivity, value: "Sleeping")),
                            L("\"Can't stop long — I'm working. What is it?\"", C(ConditionKind.NpcActivity, value: "Working")),
                            L("\"Hard days, these.\" {npc} looks tired.", C(ConditionKind.NpcMorale, max: 30)),
                            L("\"Hello again, {player}.\"", C(ConditionKind.Regard, "Familiarity", min: 10)),
                            L("\"Well met. I'm {npc}.\""),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "settle", Text = "Let me put it right. [Give back what I took and pay what is owed]", Kind = ResponseKind.Continue, Next = "start",
                                Conditions = new[] { C(ConditionKind.KnowsCrime) }, Consequences = new[] { Do(DialogueConsequenceKind.SettleCrime) } }, // Phase 14 (ADR-0069)
                            new DialogueResponse { Id = "ask", Text = "I'd like to ask you something.", Kind = ResponseKind.Ask, Next = "topics" },
                            // Phase 15 (ADR-0073): asking someone to travel with you, and parting ways. A refusal is said with its reason.
                            new DialogueResponse { Id = "recruit", Text = "Travel with me. [Ask them to join your party]", Kind = ResponseKind.Continue, Next = "start",
                                Conditions = new[] { C(ConditionKind.InParty, negate: true) }, Consequences = new[] { Do(DialogueConsequenceKind.Recruit) } },
                            new DialogueResponse { Id = "recruit_short", Text = "Come with me for a few days. [Temporary]", Kind = ResponseKind.Continue, Next = "start",
                                Conditions = new[] { C(ConditionKind.InParty, negate: true) }, Consequences = new[] { Do(DialogueConsequenceKind.Recruit, value: "temporary") } },
                            new DialogueResponse { Id = "dismiss", Text = "It's time we parted ways. [Dismiss from your party]", Kind = ResponseKind.End, Next = "end",
                                Conditions = new[] { C(ConditionKind.InParty) }, Consequences = new[] { Do(DialogueConsequenceKind.Dismiss) } },
                            new DialogueResponse { Id = "bye", Text = "Farewell.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                    new DialogueNode { Id = "topics", Lines = new[] { L("{answer}", C(ConditionKind.Variable, "answered", "yes")), L("\"Ask away.\"") }, Responses = topics },
                },
            });

            // ---- Short Measures: the granary shortfall ---------------------------------------------------------------
            Add(new DialogueDefinition
            {
                Id = "dlg_granary", QuestId = QuestCatalog.GranaryShortfall,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[]
                        {
                            L("\"{introduction}\"", Offerable),
                            L("\"You said it was {suspect}? And the grain's back in the stores? Then I owe you more than coin.\"", Active, C(ConditionKind.ObjectiveDone, "returned")),
                            L("\"Four sacks, out of your own purse? {settlement} won't forget that.\"", Active, C(ConditionKind.ObjectiveDone, "replaced")),
                            L("\"Well? Where did the grain go?\"", Active, C(ConditionKind.ObjectiveDone, "covered")),
                            L("\"Any word on the grain? The ledger won't balance itself.\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll find out where it went.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "giver_accepted",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.Regard, "Trust", 3) } },
                            new DialogueResponse { Id = "worth", Text = "And what's it worth to you?", Kind = ResponseKind.Negotiate, Conditions = new[] { Offerable }, Check = Check("Persuasion", 12, "Greed"), Once = true, Next = "giver",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.SetVariable, "bonus", value: "15", onSuccess: true), Do(DialogueConsequenceKind.Regard, "Respect", 5, onSuccess: true),
                                    Do(DialogueConsequenceKind.Regard, "Affinity", -3, onSuccess: false),
                                } },
                            new DialogueResponse { Id = "decline", Text = "Not my business.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest), Do(DialogueConsequenceKind.Regard, "Affinity", -3) } },
                            new DialogueResponse { Id = "report_true", Text = "It was {suspect}. The grain is back where it belongs.", Kind = ResponseKind.Continue, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "returned") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "report") } },
                            new DialogueResponse { Id = "report_replaced", Text = "I couldn't get it back, so I made it good.", Kind = ResponseKind.Continue, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "replaced") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "report") } },
                            new DialogueResponse { Id = "report_lie", Text = "Rats and damp, I'd say. Nobody took it.", Kind = ResponseKind.Deceive, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "covered") },
                                Check = Check("Deception", 12, "-Trustfulness"), Next = "end", Tags = ContentTags.None,
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.CompleteObjective, "report"), Do(DialogueConsequenceKind.Decision, "lied_to_giver", value: "Told {giver} nobody took the grain"),
                                    Do(DialogueConsequenceKind.Regard, "Trust", -25, onSuccess: false), Do(DialogueConsequenceKind.Regard, "Hostility", 15, onSuccess: false),
                                    Do(DialogueConsequenceKind.NpcMemory, "{player} lied to me about the grain", 70, onSuccess: false), Do(DialogueConsequenceKind.PlayerDeed, "lied_about_grain", onSuccess: false),
                                } },
                            new DialogueResponse { Id = "progress", Text = "Still looking.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "giver_accepted",
                        Lines = new[] { L("\"Start with the ledger by the granary, and {keeper} — they keep the stores. If someone's lying, it'll be in the numbers first.\"") },
                        Responses = new[] { new DialogueResponse { Id = "ok", Text = "I'll start there.", Kind = ResponseKind.End, Next = "end", Consequences = new[] { Do(DialogueConsequenceKind.RevealLocation, role: "keeper") } } },
                    },
                    new DialogueNode
                    {
                        Id = "keeper",
                        Lines = new[]
                        {
                            L("\"Grain doesn't walk off by itself. I locked up as always. {suspect} was hauling sacks late that night, and their cart was out past dark — make of that what you will.\"", Active, C(ConditionKind.ObjectiveDone, "keeper", negate: true)),
                            L("\"Four sacks would see us right, if you can find them anywhere.\"", Active, C(ConditionKind.ObjectiveDone, "keeper")),
                            L("\"Busy, sorry. Stores don't count themselves.\""),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "thanks", Text = "Thank you. That helps.", Kind = ResponseKind.Continue, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "keeper", negate: true) }, Next = "end",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.CompleteObjective, "keeper"), Do(DialogueConsequenceKind.Reveal, "suspect_cart", value: "{keeper} saw {suspect}'s cart out past dark the night the grain went."),
                                    Do(DialogueConsequenceKind.RevealObjective, "replaced"), Do(DialogueConsequenceKind.RevealLocation, role: "suspect"),
                                } },
                            new DialogueResponse { Id = "insight", Text = "You're holding something back.", Kind = ResponseKind.RequestInformation, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "keeper", negate: true) },
                                Check = Check("Insight", 10), Once = true, Next = "keeper",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.Reveal, "suspect_debt", value: "{suspect} owes money to {suspect_faction}, and has been short of it since the spring.", onSuccess: true),
                                    Do(DialogueConsequenceKind.Regard, "Affinity", -2, onSuccess: false),
                                } },
                            new DialogueResponse { Id = "sacks", Text = "Here — four sacks of grain, for the stores.", Kind = ResponseKind.Give, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "suspect"), C(ConditionKind.HasItem, "grain_sack", min: 4) }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.GiveItem, "grain_sack", 4), Do(DialogueConsequenceKind.SetOutcome, "replaced") } },
                            new DialogueResponse { Id = "bye", Text = "Goodbye.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "suspect",
                        Lines = new[]
                        {
                            L("{npc} goes still. \"Grain? I haul what I'm told to haul. Who's been talking?\"", Active, C(ConditionKind.ObjectiveDone, "suspect", negate: true)),
                            L("\"I've said all I'll say.\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "press", Text = "Your cart was out past dark, the night the grain went.", Kind = ResponseKind.Challenge, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "suspect", negate: true) }, Next = "suspect_cornered",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "suspect"), Do(DialogueConsequenceKind.RevealObjective, "returned"), Do(DialogueConsequenceKind.RevealObjective, "covered") } },
                            new DialogueResponse { Id = "bye", Text = "Never mind.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "suspect_cornered",
                        Lines = new[]
                        {
                            L("{npc} looks past you, then back. \"All right. I owed {suspect_faction}, and they don't wait. It's in the shed behind my place — most of it. What now?\"", C(ConditionKind.ObjectiveDone, "returned", negate: true)),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "persuade", Text = "Put it back tonight, quietly, and I'll tell {giver} it was found.", Kind = ResponseKind.Persuade, Check = Check("Persuasion", 13, "Greed"), Once = true,
                                Next = "suspect_done", NextOnFailure = "suspect_cornered",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.CompleteObjective, "returned", onSuccess: true), Do(DialogueConsequenceKind.SetOutcome, "returned", onSuccess: true),
                                    Do(DialogueConsequenceKind.Regard, "Affinity", -5, onSuccess: false),
                                } },
                            new DialogueResponse { Id = "intimidate", Text = "Put it back, or {giver} hears it from me — and {suspect_faction} hears you can't keep a secret.", Kind = ResponseKind.Intimidate,
                                Check = Check("Intimidation", 12, "Courage"), Once = true, Next = "suspect_done", NextOnFailure = "end", Tags = ContentTags.Threats,
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.CompleteObjective, "returned", onSuccess: true), Do(DialogueConsequenceKind.SetOutcome, "returned", onSuccess: true), Do(DialogueConsequenceKind.Notoriety, amount: 1),
                                    Do(DialogueConsequenceKind.Regard, "Hostility", 15, onSuccess: false), Do(DialogueConsequenceKind.PlayerDeed, "threatened_resident", onSuccess: false),
                                } },
                            new DialogueResponse { Id = "silver", Text = "How much is my silence worth?", Kind = ResponseKind.Bribe, Next = "suspect_bribe" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "suspect_bribe",
                        Lines = new[] { L("\"Fifty. It's all I have left after them. Take it and walk away.\"") },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "take", Text = "Done. I never saw your cart.", Kind = ResponseKind.Bribe, Next = "end",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.ReceiveCoin, amount: 50), Do(DialogueConsequenceKind.Decision, "took_silver", value: "Took {suspect}'s silver to keep quiet about the grain"),
                                    Do(DialogueConsequenceKind.SetOutcome, "covered"),
                                } },
                            new DialogueResponse { Id = "refuse", Text = "Keep it. Put the grain back.", Kind = ResponseKind.Continue, Next = "suspect_cornered" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "suspect_done",
                        Lines = new[] { L("\"Tonight, then. You'll have your full measure.\" {npc} won't meet your eye.") },
                        Responses = new[] { new DialogueResponse { Id = "ok", Text = "See that I do.", Kind = ResponseKind.End, Next = "end" } },
                    },
                },
            });

            // ---- Teeth at the Folds ----------------------------------------------------------------------------------
            Add(new DialogueDefinition
            {
                Id = "dlg_predators", QuestId = QuestCatalog.WolvesAtTheFolds,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[]
                        {
                            L("\"{introduction}\"", Offerable),
                            L("\"{count} of them? Then the folds will sleep easier. Here — you've earned it.\"", Active, C(ConditionKind.ObjectiveDone, "hunt")),
                            L("\"They come down off the high pasture at dusk. Go out to the farms and you'll find them, if they don't find you.\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll hunt them.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.Reveal, "predators_farms", value: "{creature_plural} come down to the farms around {settlement} at dusk.") } },
                            new DialogueResponse { Id = "decline", Text = "Not today.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end", Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "go", Text = "Show me where. I'll go now.", Kind = ResponseKind.Continue, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "hunt", negate: true) }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.StartEncounter, "Farms") }, Tags = ContentTags.Violence },
                            new DialogueResponse { Id = "report", Text = "It's done.", Kind = ResponseKind.Continue, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "hunt") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "report") } },
                            new DialogueResponse { Id = "later", Text = "Later.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                },
            });

            // ---- Under Seal ------------------------------------------------------------------------------------------
            Add(new DialogueDefinition
            {
                Id = "dlg_letter", QuestId = QuestCatalog.SealedLetter,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"Has it reached {recipient} yet?\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll carry it.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.ReceiveItem, "sealed_letter", 1, value: "letter_stack"), Do(DialogueConsequenceKind.RevealLocation, role: "recipient") } },
                            new DialogueResponse { Id = "ask", Text = "What's in it?", Kind = ResponseKind.RequestInformation, Conditions = new[] { Offerable }, Next = "giver", Once = true,
                                Consequences = new[] { Do(DialogueConsequenceKind.Regard, "Trust", -2) } },
                            new DialogueResponse { Id = "decline", Text = "Find another carrier.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end", Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "notyet", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "recipient",
                        Lines = new[]
                        {
                            L("\"A letter? From {giver}?\" {npc} turns it over. \"The seal's whole. Good.\"", C(ConditionKind.ObjectiveDone, "deliver"), C(ConditionKind.Variable, "opened", "yes", negate: true)),
                            L("{npc} looks at the broken seal for a long moment. \"Read it, did you? Then you know more of my business than I'd like.\"", C(ConditionKind.ObjectiveDone, "deliver"), C(ConditionKind.Variable, "opened", "yes")),
                            L("\"Yes?\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "give_sealed", Text = "A letter from {giver}, sealed as it was given to me.", Kind = ResponseKind.Give, Next = "recipient",
                                Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "deliver", negate: true), C(ConditionKind.Variable, "opened", "yes", negate: true), C(ConditionKind.HasItem, "var:letter_stack", min: 1) },
                                Consequences = new[] { Do(DialogueConsequenceKind.SetOutcome, "sealed"), Do(DialogueConsequenceKind.GiveItem, "var:letter_stack", 1) } },
                            new DialogueResponse { Id = "give_opened", Text = "A letter from {giver}. The seal — it came open.", Kind = ResponseKind.Give, Next = "recipient",
                                Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "deliver", negate: true), C(ConditionKind.Variable, "opened", "yes"), C(ConditionKind.HasItem, "var:letter_stack", min: 1) },
                                Consequences = new[] { Do(DialogueConsequenceKind.SetOutcome, "opened"), Do(DialogueConsequenceKind.GiveItem, "var:letter_stack", 1) } },
                            new DialogueResponse { Id = "bye", Text = "Goodbye.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                },
            });

            // ---- Bread Before Winter ---------------------------------------------------------------------------------
            Add(new DialogueDefinition
            {
                Id = "dlg_relief", QuestId = QuestCatalog.FoodRelief,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"Bless you. That's more than most have brought.\"", Active, C(ConditionKind.ObjectiveDone, "deliver")), L("\"Anything you can spare. Bread, rations, grain.\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll bring what I can.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end", Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest) } },
                            new DialogueResponse { Id = "decline", Text = "I've barely enough myself.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end", Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "give", Text = "Here's food for the stores.", Kind = ResponseKind.Give, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "deliver", negate: true), C(ConditionKind.HasItem, "@food", min: 1) }, Next = "giver",
                                Consequences = new[] { Do(DialogueConsequenceKind.GiveItem, "@food", 8) } },
                            new DialogueResponse { Id = "report", Text = "The stores should hold now.", Kind = ResponseKind.Continue, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "deliver") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "report") } },
                            new DialogueResponse { Id = "later", Text = "Later.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                },
            });

            // ---- Empty Shelves ---------------------------------------------------------------------------------------
            Add(new DialogueDefinition
            {
                Id = "dlg_supplies", QuestId = QuestCatalog.ShopSupplies,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"Got my {item_name}?\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll get you some.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end", Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest) } },
                            new DialogueResponse { Id = "decline", Text = "Can't help, sorry.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end", Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "give", Text = "Here: {quantity} {item_name}.", Kind = ResponseKind.Give, Conditions = new[] { Active, C(ConditionKind.HasItem, "role:item", min: 1) }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.GiveItem, "role:item", 4) } },
                            new DialogueResponse { Id = "later", Text = "Still looking.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                },
            });
            AddPhase13(); // Phase 13: dialogue for places
            AddPhase14(); // Phase 14: givers of the new places' quests
            AddPhase15(); // Phase 15: a companion's own quest
        }

        /// <summary>Checks every graph: a start node, unique node and response IDs, every Next pointing at a node, "end" or the hub.</summary>
        public static List<string> Validate()
        {
            var errors = new List<string>();
            foreach (var d in All)
            {
                var nodes = new HashSet<string>(StringComparer.Ordinal);
                foreach (var n in d.Nodes) if (!nodes.Add(n.Id)) errors.Add($"Dialogue '{d.Id}' repeats node '{n.Id}'.");
                if (d.QuestId == null && !nodes.Contains(d.StartNode)) errors.Add($"Dialogue '{d.Id}' has no start node.");
                if (d.QuestId != null && QuestCatalog.Find(d.QuestId) == null) errors.Add($"Dialogue '{d.Id}' belongs to unknown quest '{d.QuestId}'.");
                foreach (var n in d.Nodes)
                {
                    if (n.Lines.Count == 0) errors.Add($"Dialogue '{d.Id}' node '{n.Id}' says nothing.");
                    var ids = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var r in n.Responses)
                    {
                        if (!ids.Add(r.Id)) errors.Add($"Dialogue '{d.Id}' node '{n.Id}' repeats response '{r.Id}'.");
                        foreach (var next in new[] { r.Next, r.NextOnFailure })
                            if (next != null && next != "end" && !nodes.Contains(next)) errors.Add($"Dialogue '{d.Id}' response '{n.Id}.{r.Id}' leads to unknown node '{next}'.");
                        if (r.Check != null && (r.Check.Dc < 1 || string.IsNullOrEmpty(r.Check.Skill))) errors.Add($"Dialogue '{d.Id}' response '{r.Id}' has a bad check.");
                    }
                }

                if (d.QuestId != null)
                {
                    var q = QuestCatalog.Find(d.QuestId);
                    if (q != null && !nodes.Contains("giver")) errors.Add($"Dialogue '{d.Id}' has no node for the giver.");
                }
            }

            return errors;
        }
    }
}
