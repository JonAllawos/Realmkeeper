using System.Collections.Generic;
using Realmkeeper.Domain.Quests;
using static Realmkeeper.Domain.Campaigns.CampaignEffectKind;

namespace Realmkeeper.Domain.Campaigns
{
    public static partial class CampaignCatalog
    {
        public const string Waystone = "waystone_count";

        private static QuestCondition C(ConditionKind k, string key = null, string value = null, int min = int.MinValue, int max = int.MaxValue, bool negate = false) => QuestCondition.Of(k, key, value, min, max, negate);
        private static CampaignEffect E(CampaignEffectKind kind, string role = null, string key = null, int amount = 0, string value = null, string text = null) => CampaignEffect.Of(kind, role, key, amount, value, text);
        private static CampaignRoleSpec Person(string name, string of, string part, params string[] trades) =>
            new CampaignRoleSpec { Name = name, Kind = CampaignRoleKind.Npc, Rule = CampaignRoleRule.PersonIn, Of = of, Part = part, NpcRoles = trades };
        private static Dictionary<string, string> Map(params string[] pairs)
        {
            var d = new Dictionary<string, string>();
            for (var i = 0; i + 1 < pairs.Length; i += 2) d[pairs[i]] = pairs[i + 1];
            return d;
        }

        /// <summary>
        /// "The Waystone Count" — a PROVISIONAL, NON-CANONICAL demonstration of the campaign engine (ADR-0082). Two real
        /// towns on either side of a border crossing in the generated world, a toll-house between them, a short count and
        /// a quarrel. It establishes no kingdom, no central antagonist, no world history and no destiny for the player, who
        /// is simply a traveller able to walk to the far town and back. Everything it names is bound from the world.
        /// </summary>
        private static void AddWaystone()
        {
            Register(new CampaignDefinition
            {
                Id = Waystone, Version = 1, Provisional = true, Title = "The Waystone Count",
                Summary = "Two towns, one road, and a toll that no longer adds up.",
                Premise = "Where the road out of {home_town} crosses into the next land stands a waystone, and beside it a toll-house. For a generation {home_town} and {far_town} have split the tolls and the upkeep of the road on a count kept by {tollkeeper}. This season the count came up short, and each town says the other is cheating. Nobody thinks you are anyone in particular. You are simply someone who can walk to {far_town} and back.",
                Requires = "It needs a road from the home region into a neighbouring land, with people living at both ends (discover the lands beyond first, in an older world).",
                Roles = new[]
                {
                    new CampaignRoleSpec { Name = "home_town", Kind = CampaignRoleKind.Settlement, Rule = CampaignRoleRule.HomeTown, Part = "the home town" },
                    new CampaignRoleSpec { Name = "crossing", Kind = CampaignRoleKind.Route, Rule = CampaignRoleRule.HomeCrossing, Of = "home_town", Part = "the road over the border" },
                    new CampaignRoleSpec { Name = "far_town", Kind = CampaignRoleKind.Settlement, Rule = CampaignRoleRule.FarEnd, Of = "crossing", Part = "the town across the border" },
                    new CampaignRoleSpec { Name = "home_faction", Kind = CampaignRoleKind.Faction, Rule = CampaignRoleRule.FactionOf, Of = "home_town", Optional = true, Part = "those {home_town} answers to" },
                    new CampaignRoleSpec { Name = "far_faction", Kind = CampaignRoleKind.Faction, Rule = CampaignRoleRule.FactionOf, Of = "far_town", Optional = true, Part = "those {far_town} answers to" },
                    Person("reeve", "home_town", "speaks for {home_town}", "Elder", "Guard", "Merchant", "Innkeeper"),
                    new CampaignRoleSpec { Name = "carter", Kind = CampaignRoleKind.Npc, Rule = CampaignRoleRule.PersonIn, Of = "home_town", Optional = true, Part = "pays the toll", NpcRoles = new[] { "Merchant", "Labourer", "Farmer", "Smith" } },
                    Person("warden", "far_town", "keeps the crossing for {far_town}", "Guard", "Elder", "Merchant"),
                    Person("tollkeeper", "far_town", "keeps the count at the toll-house", "Merchant", "Labourer", "Innkeeper", "Farmer", "Smith"),
                    new CampaignRoleSpec { Name = "tollkeeper_kin", Kind = CampaignRoleKind.Npc, Rule = CampaignRoleRule.HouseholdOf, Of = "tollkeeper", Optional = true, Part = "shares the toll-keeper's house" },
                    new CampaignRoleSpec { Name = "far_leader", Kind = CampaignRoleKind.FactionLeader, Of = "far_faction", Part = "leads those {far_town} answers to" },
                    new CampaignRoleSpec { Name = "counterpart", Kind = CampaignRoleKind.FirstAvailable, Candidates = new[] { "warden", "far_leader" }, Part = "speaks for {far_town} at the stone" },
                    new CampaignRoleSpec { Name = "home_voice", Kind = CampaignRoleKind.FirstAvailable, Candidates = new[] { "reeve", "carter" }, Part = "will hear how it ended, in {home_town}" },
                },
                Quests = new[]
                {
                    new CampaignQuestRef { Key = "letter", QuestId = QuestCatalog.WaystoneLetter, Roles = Map("giver", "reeve", "recipient", "counterpart", "far", "far_town", "home", "home_town") },
                    new CampaignQuestRef { Key = "tally", QuestId = QuestCatalog.WaystoneTally, Roles = Map("giver", "carter", "tollkeeper", "tollkeeper", "far", "far_town") },
                    new CampaignQuestRef { Key = "hold", QuestId = QuestCatalog.WaystoneHold, Roles = Map("giver", "counterpart", "home", "home_town", "far", "far_town", "reeve", "reeve") },
                    new CampaignQuestRef { Key = "keeper", QuestId = QuestCatalog.WaystoneKeeper, Roles = Map("giver", "counterpart", "tollkeeper", "tollkeeper", "far", "far_town") },
                    new CampaignQuestRef { Key = "kin", QuestId = QuestCatalog.WaystoneKin, Roles = Map("giver", "tollkeeper_kin", "tollkeeper", "tollkeeper") },
                    new CampaignQuestRef { Key = "accord", QuestId = QuestCatalog.WaystoneAccord, Roles = Map("giver", "home_voice", "home", "home_town", "far", "far_town") },
                },
                Chapters = new[]
                {
                    new ChapterDefinition { Id = "short_count", Title = "The Short Count", Summary = "A letter goes over the border, and someone looks at the tallies." },
                    new ChapterDefinition { Id = "two_accounts", Title = "Two Accounts", Summary = "Both towns want an answer, and the toll-keeper has to answer for the count.", After = new[] { "short_count" } },
                    new ChapterDefinition { Id = "settling_up", Title = "Settling Up", Summary = "Whatever was decided at the stone, {home_town} will want to hear it.", After = new[] { "two_accounts" } },
                },
                Arcs = new[]
                {
                    new ArcDefinition
                    {
                        Id = "letter", ChapterId = "short_count", Title = "A Letter Across the Line", MayRefuse = true,
                        Summary = "{reeve} wants word carried to {far_town} that {home_town} knows the count is short.",
                        StartQuests = new[] { "letter" },
                        Objectives = new[]
                        {
                            new ArcObjectiveDefinition { Id = "delivered", Group = "word", Kind = ArcObjectiveKind.Quest, QuestKey = "letter", Outcome = "delivered", Text = "Carry {reeve}'s letter to {far_town}." },
                            new ArcObjectiveDefinition { Id = "unanswered", Group = "word", Kind = ArcObjectiveKind.State, Hidden = true, Outcome = "unanswered",
                                Conditions = new[] { C(ConditionKind.CampaignQuest, "letter", "Failed|Abandoned|None") , C(ConditionKind.CharacterStatus, "warden", "Dead|Missing|Captured|Hostile|Departed") },
                                Text = "With {warden} gone, {far_town} hears of the quarrel some other way." },
                        },
                    },
                    new ArcDefinition
                    {
                        Id = "tally", ChapterId = "short_count", Title = "Counting Carts", Optional = true,
                        Summary = "{carter} thinks the toll-house has been counting carts that were never there.",
                        Conditions = new[] { C(ConditionKind.CharacterStatus, "carter", "Available|Injured|Unavailable|Allied") },
                        StartQuests = new[] { "tally" },
                        Objectives = new[] { new ArcObjectiveDefinition { Id = "looked", Kind = ArcObjectiveKind.Quest, QuestKey = "tally", Text = "Look at {tollkeeper}'s tally sticks for {carter}." } },
                    },
                    new ArcDefinition
                    {
                        Id = "claim", ChapterId = "two_accounts", Title = "Who Holds the Road",
                        Summary = "{counterpart} will hear your answer: whose count should it be?",
                        StartQuests = new[] { "hold" }, TimeLimitDays = 20,
                        ExpiryText = "{home_town} and {far_town} settled the count between themselves while you were elsewhere, and neither is happy with it",
                        Objectives = new[] { new ArcObjectiveDefinition { Id = "answered", Kind = ArcObjectiveKind.Decision, DecisionId = "road_claim", Text = "Give {counterpart} your answer about the count." } },
                        OnFail = new[] { E(Flag, key: "claim", value: "unsettled"), E(Spread, "home_town", text: "{home_town} and {far_town} patched up the waystone count between them, grudgingly") },
                    },
                    new ArcDefinition
                    {
                        Id = "keeper", ChapterId = "two_accounts", Title = "The Toll-Keeper", MayRefuse = true,
                        Summary = "Someone kept a crooked count. {tollkeeper} has to answer for it — or someone has to answer in their place.",
                        StartQuests = new[] { "keeper" },
                        Fallbacks = new[]
                        {
                            new ArcFallback
                            {
                                Quest = "kin", Note = "{tollkeeper} can't answer for the count, but {tollkeeper_kin} might.",
                                When = new[] { C(ConditionKind.CharacterStatus, "tollkeeper", "Dead|Missing|Captured|Hostile|Departed"), C(ConditionKind.CharacterStatus, "tollkeeper_kin", "Available|Injured|Unavailable|Allied") },
                            },
                        },
                        Objectives = new[]
                        {
                            new ArcObjectiveDefinition { Id = "faced", Group = "fate", Kind = ArcObjectiveKind.Decision, DecisionId = "tollkeeper_fate", Text = "Settle what's to be done about {tollkeeper}." },
                            new ArcObjectiveDefinition { Id = "sticks", Group = "fate", Kind = ArcObjectiveKind.Quest, QuestKey = "kin", Hidden = true, Outcome = "kin", Text = "Hear from {tollkeeper_kin} what the tally sticks show." },
                            new ArcObjectiveDefinition { Id = "lost", Group = "fate", Kind = ArcObjectiveKind.State, Hidden = true, Outcome = "lost",
                                Conditions = new[] { C(ConditionKind.CharacterStatus, "tollkeeper", "Dead|Missing|Captured|Hostile|Departed"), C(ConditionKind.CharacterStatus, "tollkeeper_kin", "Dead|Missing|Captured|Hostile|Departed"),
                                    C(ConditionKind.CampaignDecision, "tollkeeper_fate", negate: true) },
                                Text = "With {tollkeeper} beyond asking, the truth of the count is lost." },
                        },
                    },
                    new ArcDefinition
                    {
                        Id = "accord", ChapterId = "settling_up", Title = "Settling Up",
                        Summary = "Tell {home_voice} how the waystone business ended.",
                        StartQuests = new[] { "accord" },
                        Objectives = new[]
                        {
                            new ArcObjectiveDefinition { Id = "told", Group = "told", Kind = ArcObjectiveKind.Quest, QuestKey = "accord", Text = "Tell {home_voice} how it ended." },
                            new ArcObjectiveDefinition { Id = "unheard", Group = "told", Kind = ArcObjectiveKind.State, Hidden = true, Outcome = "unheard",
                                Conditions = new[] { C(ConditionKind.CharacterStatus, "reeve", "Dead|Missing|Captured|Hostile|Departed"), C(ConditionKind.CharacterStatus, "carter", "Dead|Missing|Captured|Hostile|Departed") },
                                Text = "There is nobody left in {home_town} who asked." },
                        },
                        OnComplete = new[] { E(Experience, amount: 150), E(Chronicle, "home_town", text: "the waystone business was settled, and the road between {home_town} and {far_town} went on") },
                    },
                },
                Decisions = new[]
                {
                    new DecisionDefinition
                    {
                        Id = "road_claim", ArcId = "claim", SpeakerRole = "counterpart", Title = "Whose count?",
                        Prompt = "{counterpart} asks you plainly: who should keep the count at the waystone?",
                        Choices = new[]
                        {
                            new ChoiceDefinition
                            {
                                Id = "home", Text = "The toll-house stands on {home_town}'s side of the stone. The count is theirs to keep.",
                                Summary = "Told {counterpart} the count belongs to {home_town}", Outcome = "home",
                                Effects = new[]
                                {
                                    E(Flag, key: "claim", value: "home"), E(FactionStanding, "home_faction", amount: 8), E(FactionStanding, "far_faction", amount: -10),
                                    E(FactionOpinion, "home_faction", "far_faction", -10), E(Regard, "reeve", "Gratitude", 15), E(Regard, "counterpart", "Hostility", 15),
                                    E(SettlementStability, "home_town", amount: 3), E(NpcMemory, "counterpart", text: "{player} took {home_town}'s side over the waystone", amount: 70),
                                },
                                Delayed = new[]
                                {
                                    new DelayedConsequence
                                    {
                                        Id = "refused", AfterDays = 6, Hint = "{far_town} will remember whose side you took.",
                                        Text = "{far_town}'s carters stopped paying at the waystone, and trade on the crossing slowed to a trickle.",
                                        Effects = new[]
                                        {
                                            E(RaiseWorldEvent, "crossing", "TradeDisruption", 2, text: "carters from {far_town} refuse to pay {home_town}'s toll"),
                                            E(FactionOpinion, "home_faction", "far_faction", -5), E(Spread, "far_town", text: "{home_town} has the waystone count now, and {far_town} won't pay it"),
                                        },
                                    },
                                },
                                Leanings = new[]
                                {
                                    new ChoiceLeaning { Trait = "Ambition", Sign = 1, Approve = "\"{home_town} needed someone to say it plainly. You did.\"", Disapprove = "\"You backed the stronger town. Anyone could have done that.\"" },
                                    new ChoiceLeaning { Trait = "Compassion", Sign = -1, Approve = "\"Somebody had to win it. Better us.\"", Disapprove = "\"{far_town} has hungry mouths too. You picked a side because it was nearer.\"" },
                                },
                            },
                            new ChoiceDefinition
                            {
                                Id = "far", Text = "{far_town} has kept that road open through two bad winters. Let them keep the count.",
                                Summary = "Told {counterpart} the count belongs to {far_town}", Outcome = "far",
                                Effects = new[]
                                {
                                    E(Flag, key: "claim", value: "far"), E(FactionStanding, "far_faction", amount: 8), E(FactionStanding, "home_faction", amount: -10),
                                    E(FactionOpinion, "home_faction", "far_faction", -10), E(Regard, "counterpart", "Gratitude", 15), E(Regard, "reeve", "Trust", -15),
                                    E(SettlementStability, "home_town", amount: -3), E(NpcMemory, "reeve", text: "{player} gave the waystone count to {far_town}", amount: 70),
                                },
                                Delayed = new[]
                                {
                                    new DelayedConsequence
                                    {
                                        Id = "sold_out", AfterDays = 6, Hint = "{home_town} will hear of this.",
                                        Text = "In {home_town} they say you sold the waystone to {far_town}. {reeve} has stopped saying otherwise.",
                                        Effects = new[]
                                        {
                                            E(RaiseWorldEvent, "home_town", "LocalUnrest", 2, text: "{home_town} feels sold out at the waystone"),
                                            E(SettlementStability, "home_town", amount: -4), E(Regard, "reeve", "Hostility", 10),
                                        },
                                    },
                                },
                                Leanings = new[]
                                {
                                    new ChoiceLeaning { Trait = "Courage", Sign = 1, Approve = "\"Going against the town that fed us took nerve. I think you were right.\"", Disapprove = "\"You'll not be welcome in {home_town} for a while. I hope it was worth it.\"" },
                                    new ChoiceLeaning { Trait = "Sociability", Sign = -1, Approve = "\"Fair's fair, whoever's asking.\"", Disapprove = "\"{home_town} took us in. You might have remembered that.\"" },
                                },
                            },
                            new ChoiceDefinition
                            {
                                Id = "shared", Text = "Count it twice: one tally for each town, kept side by side and read together at each month's turn.",
                                Summary = "Persuaded both towns to keep the count twice, side by side", Outcome = "shared",
                                Conditions = new[] { C(ConditionKind.CampaignFlag, "case_made") },
                                UnavailableText = "You'd need proof the count is being cheated, or a better argument, before both towns would share it.",
                                Effects = new[]
                                {
                                    E(Flag, key: "claim", value: "shared"), E(FactionStanding, "home_faction", amount: 4), E(FactionStanding, "far_faction", amount: 4),
                                    E(FactionOpinion, "home_faction", "far_faction", 8), E(SettlementStability, "home_town", amount: 2), E(SettlementStability, "far_town", amount: 2),
                                    E(Regard, "counterpart", "Respect", 10), E(Regard, "reeve", "Respect", 10), E(Experience, amount: 150),
                                },
                                Delayed = new[]
                                {
                                    new DelayedConsequence
                                    {
                                        Id = "holds", AfterDays = 8, Hint = "If both towns keep their word, the road will show it.",
                                        When = new[] { C(ConditionKind.RouteOpen, "crossing") },
                                        Text = "The shared count has held. With the tolls honest again, both towns put money into the road, and the crossing has been mended.",
                                        Effects = new[]
                                        {
                                            E(RouteCondition, "crossing", value: "Good"), E(SettlementProsperity, "home_town", amount: 3), E(SettlementProsperity, "far_town", amount: 3),
                                            E(Chronicle, "far_town", text: "the waystone count is kept twice now, and the crossing has been mended"),
                                        },
                                    },
                                },
                                Leanings = new[]
                                {
                                    new ChoiceLeaning { Trait = "Trustfulness", Sign = 1, Approve = "\"Two tallies. Simple, once someone says it. That was well done.\"", Disapprove = "\"You think a second stick of wood stops a thief? They'll learn to cheat both.\"" },
                                    new ChoiceLeaning { Trait = "Compassion", Sign = 1, Approve = "\"Nobody lost today. That's rarer than it should be.\"", Disapprove = null },
                                },
                            },
                            new ChoiceDefinition
                            {
                                Id = "apart", Text = "This is between your two towns. I carried the letter; I won't carry the quarrel.",
                                Summary = "Refused to take a side over the waystone", Outcome = "apart",
                                Effects = new[] { E(Flag, key: "claim", value: "apart"), E(Regard, "counterpart", "Respect", 5), E(Regard, "reeve", "Affinity", -5) },
                                Delayed = new[]
                                {
                                    new DelayedConsequence
                                    {
                                        Id = "festers", AfterDays = 10,
                                        Text = "Left to themselves, {home_town} and {far_town} have only grown further apart over the waystone.",
                                        Effects = new[] { E(RaiseWorldEvent, "far_town", "LocalUnrest", 1, text: "{far_town} is restless over the waystone"), E(SettlementStability, "far_town", amount: -3), E(FactionOpinion, "home_faction", "far_faction", -6) },
                                    },
                                },
                                Leanings = new[]
                                {
                                    new ChoiceLeaning { Trait = "Courage", Sign = -1, Approve = "\"Not our fight. I'm glad you saw it.\"", Disapprove = "\"You walked away from it. Someone will pay for that, and it won't be us.\"" },
                                },
                            },
                        },
                    },
                    new DecisionDefinition
                    {
                        Id = "tollkeeper_fate", ArcId = "keeper", SpeakerRole = "tollkeeper", Title = "The toll-keeper's count",
                        Prompt = "The count was {tollkeeper}'s to keep. What's to be done?",
                        Choices = new[]
                        {
                            new ChoiceDefinition
                            {
                                Id = "expose", Text = "You'll tell {counterpart} yourself, or I will.", Summary = "Exposed {tollkeeper}'s skimming", Outcome = "exposed",
                                Effects = new[]
                                {
                                    E(Flag, key: "fate", value: "exposed"), E(Regard, "tollkeeper", "Hostility", 75), E(NpcMemory, "tollkeeper", text: "{player} exposed me at the toll-house", amount: 85),
                                    E(Regard, "counterpart", "Trust", 10), E(SettlementStability, "far_town", amount: 2), E(Spread, "far_town", text: "{tollkeeper} has been padding the waystone count"),
                                },
                                Leanings = new[]
                                {
                                    new ChoiceLeaning { Trait = "Discipline", Sign = 1, Approve = "\"A thief's a thief. You did the honest thing.\"", Disapprove = "\"You've ruined them over a few coin a cart.\"" },
                                    new ChoiceLeaning { Trait = "Compassion", Sign = -1, Approve = "\"No sense pretending it was a mistake.\"", Disapprove = "\"You might have given them a way back. You didn't even look for one.\"" },
                                },
                            },
                            new ChoiceDefinition
                            {
                                Id = "repay", Text = "Pay back what you took, a little each month, and this stays between us and the tally.", Summary = "Let {tollkeeper} repay the tolls quietly", Outcome = "repaid",
                                Conditions = new[] { C(ConditionKind.CampaignFlag, "evidence") }, UnavailableText = "You can't bargain over a theft you can't show.",
                                Effects = new[]
                                {
                                    E(Flag, key: "fate", value: "repaid"), E(SettlementResource, "far_town", "coin", 40), E(Regard, "tollkeeper", "Gratitude", 25), E(Regard, "tollkeeper", "Trust", 10),
                                    E(NpcMorale, "tollkeeper", amount: -10),
                                },
                                Delayed = new[]
                                {
                                    new DelayedConsequence
                                    {
                                        Id = "word", AfterDays = 5,
                                        When = new[] { C(ConditionKind.CharacterStatus, "tollkeeper", "Available|Injured|Unavailable|Allied") },
                                        Text = "{tollkeeper} kept the bargain, and sent word of something you might need: where the outlaws on the crossing wait for carts.",
                                        Effects = new[] { E(Discovery, key: "tollkeeper_word", text: "{tollkeeper} sent word: outlaws on {crossing} wait for carts at the bend below the waystone, an hour after dawn."), E(Regard, "tollkeeper", "Affinity", 10) },
                                    },
                                },
                                Leanings = new[]
                                {
                                    new ChoiceLeaning { Trait = "Compassion", Sign = 1, Approve = "\"That was kind, and it might even work.\"", Disapprove = "\"So a thief keeps their post because they said sorry.\"" },
                                    new ChoiceLeaning { Trait = "Discipline", Sign = -1, Approve = "\"Better mended than broken.\"", Disapprove = "\"Rules are rules. You just taught them otherwise.\"" },
                                },
                            },
                            new ChoiceDefinition
                            {
                                Id = "cut", Text = "I'll keep quiet. For a share.", Summary = "Took a share of {tollkeeper}'s skimming", Outcome = "cut",
                                Effects = new[] { E(Flag, key: "fate", value: "cut"), E(Coin, amount: 30), E(PlayerDeed, "tollkeeper", text: "took_toll_share") },
                                Delayed = new[]
                                {
                                    new DelayedConsequence
                                    {
                                        Id = "found_out", AfterDays = 7,
                                        Text = "Word got out that you'd taken a share of the toll money. {far_town} counts its coins twice when you pass.",
                                        Effects = new[] { E(Regard, "counterpart", "Trust", -20), E(Spread, "far_town", text: "{player} took a share of the waystone toll money"), E(SettlementStability, "far_town", amount: -2) },
                                    },
                                },
                                Leanings = new[]
                                {
                                    new ChoiceLeaning { Trait = "Greed", Sign = 1, Approve = "\"Can't say I'd have turned it down.\"", Disapprove = "\"You took their coin. That makes two of you.\"" },
                                    new ChoiceLeaning { Trait = "Discipline", Sign = -1, Approve = null, Disapprove = "\"I didn't take you for someone who'd be bought for thirty coin.\"" },
                                },
                            },
                        },
                    },
                },
                FinalArc = "accord",
                Endings = new[]
                {
                    new EndingDefinition { Id = "shared", Title = "A Count Kept Twice", Conditions = new[] { C(ConditionKind.CampaignDecision, "road_claim", "shared") },
                        Text = "Two tallies hang in the toll-house now, one for each town, and are read aloud together at every month's turn. Nobody loves the arrangement. It holds." },
                    new EndingDefinition { Id = "home", Title = "{home_town}'s Road", Conditions = new[] { C(ConditionKind.CampaignDecision, "road_claim", "home") },
                        Text = "{home_town} keeps the count, and {far_town} pays it when it must. The road is quieter for it, and colder." },
                    new EndingDefinition { Id = "far", Title = "{far_town}'s Road", Conditions = new[] { C(ConditionKind.CampaignDecision, "road_claim", "far") },
                        Text = "{far_town} keeps the count. In {home_town} your name is said with a certain care." },
                    new EndingDefinition { Id = "apart", Title = "Their Own Quarrel", Conditions = new[] { C(ConditionKind.CampaignDecision, "road_claim", "apart") },
                        Text = "You left the towns to settle it themselves. They are still settling it." },
                    new EndingDefinition { Id = "unsettled", Title = "Settled Without You", Text = "The towns patched the count together without you. It will do, until it doesn't." },
                },
            });
        }
    }
}
