namespace Realmkeeper.Domain.Quests
{
    public static partial class QuestCatalog
    {
        // Chain 1 — investigation (Hollin Tower).
        public const string RecordHouse = "ashes_record_house";
        public const string ReevesAccounts = "reeves_accounts";

        // Chain 2 — faction and politics (Cairn Watch).
        public const string CairnQuestion = "cairn_question";
        public const string CairnKeeping = "cairn_keeping";

        // Chain 3 — wilderness and dungeon (Greywater Drift).
        public const string DrownedDrift = "drowned_drift";
        public const string DriftFuture = "drift_future";

        // Chain 4 — settlement and economy (any market town).
        public const string MillersMeasure = "millers_measure";
        public const string MillForTwo = "mill_for_two";

        /// <summary>
        /// Phase 18 (ADR-0091): four chains of two quests, each with a decision that changes the world and two or more ways to
        /// end. Every objective advances from real events (a place entered, a feature worked, a discovery made, an item handed
        /// over, a choice made in conversation); outcomes change settlements, factions and people through the existing
        /// consequence paths. All text is Realmkeeper's own; the histories are local and fix no campaign canon.
        /// </summary>
        private static void AddPhase18()
        {
            AddPhase18Stories(); // ADR-0093: the world's new stories
            AddPhase18Companions(); // ADR-0095: two more companion stories

            // ----- Chain 1: Ashes in the Record-House → The Reeve's Accounts -----
            Add(new QuestDefinition
            {
                Id = RecordHouse, Name = "Ashes in the Record-House", Category = QuestCategory.Investigation, DialogueId = "dlg_record_house",
                Description = "Someone burned {settlement}'s record-house. {giver} believes the tithe ledgers were taken first, and hidden at {place}.",
                Introduction = "Somebody fired the record-house three nights ago. The tithe ledgers burned — or so we're meant to think. A cart went up the old tower road that night. Find out what was hidden out there, and by whom.",
                Roles = new[]
                {
                    Npc("giver", "same", "Elder", "Guard", "Resident"),
                    Npc("suspect", "same", "Merchant", "Elder", "Smith"),
                    new RoleSpec { Name = "place", Kind = RoleKind.WorldObject },
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "find", Stage = 0, Kind = ObjectiveKind.DiscoverLocation, Target = "role:place", Text = "Learn where {place} lies." },
                    new ObjectiveDefinition { Id = "enter", Stage = 1, Kind = ObjectiveKind.VisitLocation, Target = "role:place", Text = "Search {place}." },
                    new ObjectiveDefinition { Id = "ash", Stage = 1, Optional = true, Kind = ObjectiveKind.DiscoverInfo, Target = "hollin_burned_copy", Text = "Find out what was burned at the tower." },
                    new ObjectiveDefinition { Id = "ledger", Stage = 2, Kind = ObjectiveKind.Acquire, Target = "item:true_ledger", Text = "Find what was hidden at {place}." },
                    new ObjectiveDefinition { Id = "report", Stage = 3, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Bring {giver} what you found." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.NpcDied, Key = "giver", Text = "{giver} is dead; nobody else is asking." } },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 150, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 5, Shown = "Standing in {settlement}" },
                    new RewardDefinition { Kind = RewardKind.Item, Key = "shuttered_lantern", Amount = 1, Shown = "A mason's shuttered lantern (reads old cuts in stone)" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { When = ConsequenceTiming.OnAvailable, Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "the record-house burned" },
                    new ConsequenceDefinition { When = ConsequenceTiming.OnAvailable, Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = -2 },
                },
                FollowUps = new[] { ReevesAccounts },
            });

            Add(new QuestDefinition
            {
                Id = ReevesAccounts, Name = "The Reeve's Accounts", Category = QuestCategory.Investigation, DialogueId = "dlg_reeve",
                Description = "The true ledger shows {suspect} shaved {settlement}'s measures for years. How it comes out is for you to decide.",
                Introduction = "This is {suspect}'s hand, no doubt of it. Years of shaved measures, and the difference sold down the road. You found it — you put it to them. However it ends, I want it ended.",
                Roles = new[]
                {
                    Npc("giver", "same", "Elder", "Guard", "Resident"),
                    Npc("suspect", "same", "Merchant", "Elder", "Smith"),
                    new RoleSpec { Name = "place", Kind = RoleKind.WorldObject },
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "confront", Stage = 0, Kind = ObjectiveKind.TalkTo, Target = "role:suspect", Text = "Put the ledger to {suspect}." },
                    new ObjectiveDefinition { Id = "exposed", Stage = 1, Group = "resolve", Kind = ObjectiveKind.Deliver, Target = "item:true_ledger", To = "giver", Outcome = "exposed",
                        Text = "Give the ledger to {giver}, and let {settlement} judge {suspect}." },
                    new ObjectiveDefinition { Id = "restored", Stage = 1, Group = "resolve", Kind = ObjectiveKind.Persuade, Target = "role:suspect", Hidden = true, Outcome = "restored",
                        Text = "Persuade {suspect} to pay back what was taken, quietly." },
                    new ObjectiveDefinition { Id = "bought", Stage = 1, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:reeve_paid", Hidden = true, Outcome = "bought",
                        Text = "Take {suspect}'s money, and let the ledger burn after all." },
                    new ObjectiveDefinition { Id = "report", Stage = 2, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Tell {giver} how it ended." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.NpcDied, Key = "giver", Text = "{giver} is dead; the matter dies with them." } },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 200, Outcome = "exposed", Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 220, Outcome = "restored", Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 80, Outcome = "bought" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 20, Outcome = "exposed", Shown = "Standing in {settlement}" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 10, Outcome = "restored" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Trust", Amount = 20, Outcome = "exposed" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "suspect", Key = "Gratitude", Amount = 25, Outcome = "restored" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 40, Outcome = "exposed", Shown = "40 coin from the town chest" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = -2, Outcome = "exposed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementProsperity, Role = "settlement", Amount = 3, Outcome = "exposed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcRegard, Role = "suspect", Key = "Hostility", Amount = 40, Outcome = "exposed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMorale, Role = "suspect", Amount = -30, Outcome = "exposed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "suspect", Key = "Shamed before the town by {player} over the ledgers", Amount = 80, Outcome = "exposed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "{suspect} shown to have shaved the measures", Outcome = "exposed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "exposed_the_reeve", Outcome = "exposed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = 3, Outcome = "restored" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementResource, Role = "settlement", Key = "food", Amount = 40, Outcome = "restored" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "suspect", Key = "{player} let me make it right", Amount = 70, Outcome = "restored" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "the shortfall in the measures quietly made good", Outcome = "restored" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = -1, Outcome = "bought" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.Notoriety, Amount = 6, Outcome = "bought" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "suspect", Key = "{player} took my silver and kept quiet", Amount = 70, Outcome = "bought" },
                },
            });

            // ----- Chain 2: The Cairn Question → Who Keeps the Cairn -----
            Add(new QuestDefinition
            {
                Id = CairnQuestion, Name = "The Cairn Question", Category = QuestCategory.Faction, DialogueId = "dlg_cairn_question",
                Description = "{giver} of {faction} says Cairn Watch was always {faction}'s by right, and that {holder} took it in a hard winter. Find out the truth at {place}.",
                Introduction = "That tower on the cairn was ours as much as theirs — built by both, kept by turns. Then one winter {holder} shut the gate and never opened it again. Go and look. The truth's in that tower somewhere, and I'll stand by it, whichever way it falls.",
                Roles = new[]
                {
                    Npc("giver", "same", "Elder", "Guard"),
                    new RoleSpec { Name = "faction", Kind = RoleKind.Faction, Of = "giver" },
                    new RoleSpec { Name = "holder", Kind = RoleKind.Faction },
                    new RoleSpec { Name = "place", Kind = RoleKind.WorldObject },
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "find", Stage = 0, Kind = ObjectiveKind.DiscoverLocation, Target = "role:place", Text = "Learn where {place} lies." },
                    new ObjectiveDefinition { Id = "enter", Stage = 1, Kind = ObjectiveKind.VisitLocation, Target = "role:place", Text = "Go to {place}." },
                    new ObjectiveDefinition { Id = "log", Stage = 2, Kind = ObjectiveKind.DiscoverInfo, Target = "cairn_watch_log", Text = "Read the watch's own log." },
                    new ObjectiveDefinition { Id = "stone", Stage = 2, Optional = true, Kind = ObjectiveKind.DiscoverInfo, Target = "cairn_boundary", Text = "Read the boundary stone." },
                    new ObjectiveDefinition { Id = "charter", Stage = 2, Optional = true, Kind = ObjectiveKind.Acquire, Target = "item:old_charter", Text = "Find the old watch charter, said to have been laid with the ringers of the crossing." },
                    new ObjectiveDefinition { Id = "report", Stage = 3, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Tell {giver} what the tower says." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 150, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "giver", Amount = 5, Shown = "Standing with {faction}" },
                },
                FollowUps = new[] { CairnKeeping },
            });

            Add(new QuestDefinition
            {
                Id = CairnKeeping, Name = "Who Keeps the Cairn", Category = QuestCategory.Faction, DialogueId = "dlg_cairn_keeping",
                Description = "{faction} and {holder} both claim Cairn Watch. What you say of it will be taken to the other side — and acted on.",
                Introduction = "You've seen the tower. You've read what's there. I'll put your word to {holder}, and they'll know it was yours. So — whose is it?",
                Roles = new[]
                {
                    Npc("giver", "same", "Elder", "Guard"),
                    new RoleSpec { Name = "faction", Kind = RoleKind.Faction, Of = "giver" },
                    new RoleSpec { Name = "holder", Kind = RoleKind.Faction },
                    new RoleSpec { Name = "place", Kind = RoleKind.WorldObject },
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "claimed", Stage = 0, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:cairn_claimed", Outcome = "claimed",
                        Text = "Back {faction}'s claim to the tower." },
                    new ObjectiveDefinition { Id = "upheld", Stage = 0, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:cairn_upheld", Outcome = "upheld",
                        Text = "Tell {giver} that {holder} holds it fairly." },
                    new ObjectiveDefinition { Id = "shared", Stage = 0, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:cairn_shared", Outcome = "shared",
                        Text = "Hold both to the old charter: the watch kept by both, by turns." },
                    new ObjectiveDefinition { Id = "aside", Stage = 0, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:cairn_aside", Outcome = "aside",
                        Text = "Stay out of it." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 200, Outcome = "claimed", Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 200, Outcome = "upheld" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 300, Outcome = "shared" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 50, Outcome = "aside" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "giver", Amount = 20, Outcome = "claimed", Shown = "Standing with {faction}" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "holder", Amount = -20, Outcome = "claimed" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "giver", Amount = -10, Outcome = "upheld" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "holder", Amount = 20, Outcome = "upheld" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "giver", Amount = 10, Outcome = "shared" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "holder", Amount = 10, Outcome = "shared" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.FactionRelation, Role = "faction", Value = "holder", Amount = -20, Outcome = "claimed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.FactionInfluence, Role = "faction", Amount = 5, Outcome = "claimed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "{faction} pressed its claim to Cairn Watch", Outcome = "claimed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.FactionRelation, Role = "faction", Value = "holder", Amount = -5, Outcome = "upheld" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.FactionInfluence, Role = "holder", Amount = 4, Outcome = "upheld" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcRegard, Role = "giver", Key = "Affinity", Amount = -10, Outcome = "upheld" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.FactionRelation, Role = "faction", Value = "holder", Amount = 25, Outcome = "shared" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = 3, Outcome = "shared" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "Cairn Watch to be kept by turns again, under the old charter", Outcome = "shared" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "mediated_the_cairn", Outcome = "shared" },
                },
            });

            // ----- Chain 3: The Drowned Drift → Greywater's Future -----
            Add(new QuestDefinition
            {
                Id = DrownedDrift, Name = "The Drowned Drift", Category = QuestCategory.Exploration, DialogueId = "dlg_drowned_drift",
                Description = "Nine miners went down into {place} and none came up. {giver} wants to know what happened to them.",
                Introduction = "Nine went down into Greywater and none came up. The owner swore the pump was sound and the water was nothing. My brother was the ninth. Go down — find out what happened, and what they died for.",
                Roles = new[]
                {
                    Npc("giver", "same", "Labourer", "Smith", "Farmer", "Resident"),
                    new RoleSpec { Name = "place", Kind = RoleKind.WorldObject },
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "find", Stage = 0, Kind = ObjectiveKind.DiscoverLocation, Target = "role:place", Text = "Learn where {place} lies." },
                    new ObjectiveDefinition { Id = "enter", Stage = 1, Kind = ObjectiveKind.VisitLocation, Target = "role:place", Text = "Go down into {place}." },
                    new ObjectiveDefinition { Id = "tally", Stage = 2, Kind = ObjectiveKind.DiscoverInfo, Target = "drift_last_shift", Text = "Find the last shift's tally." },
                    new ObjectiveDefinition { Id = "drain", Stage = 2, Optional = true, Kind = ObjectiveKind.DiscoverInfo, Target = "drift_drained", Text = "Get the pump working and drain the flooded gallery." },
                    new ObjectiveDefinition { Id = "seam", Stage = 2, Kind = ObjectiveKind.DiscoverInfo, Target = "drift_silver", Text = "Reach the flooded gallery and see what they were digging for." },
                    new ObjectiveDefinition { Id = "report", Stage = 3, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Tell {giver} what you found." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 250, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Gratitude", Amount = 20 },
                },
                FollowUps = new[] { DriftFuture },
            });

            Add(new QuestDefinition
            {
                Id = DriftFuture, Name = "Greywater's Future", Category = QuestCategory.Settlement, DialogueId = "dlg_drift_future",
                Description = "{settlement} must decide what becomes of {place}: a rich seam, or nine miners' grave. {giver} will abide by what you advise.",
                Introduction = "There's silver down there — you've seen it. Half the town wants it worked again. The other half says it's a grave now and should stay shut. They'll listen to you; you're the only one who's been down.",
                Roles = new[]
                {
                    Npc("giver", "same", "Labourer", "Smith", "Farmer", "Resident"),
                    new RoleSpec { Name = "place", Kind = RoleKind.WorldObject },
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "reopen", Stage = 0, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:drift_reopened", Outcome = "reopened",
                        Text = "Advise that the drift be worked again — with the pump kept as the drill board says." },
                    new ObjectiveDefinition { Id = "seal", Stage = 0, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:drift_sealed", Outcome = "sealed",
                        Text = "Advise that the drift be sealed as the nine miners' grave." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 150, Outcome = "reopened", Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 150, Outcome = "sealed" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 60, Outcome = "reopened", Shown = "A share of the first silver (60 coin)" },
                    new RewardDefinition { Kind = RewardKind.Item, Key = "pump_wrench", Amount = 1, Outcome = "reopened", Shown = "The pump-wright's wrench" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 15, Outcome = "sealed", Shown = "Standing in {settlement}" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Trust", Amount = 20, Outcome = "sealed" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementProsperity, Role = "settlement", Amount = 6, Outcome = "reopened" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = -2, Outcome = "reopened" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "Greywater Drift opened again for silver", Outcome = "reopened" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = 4, Outcome = "sealed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMorale, Role = "giver", Amount = 25, Outcome = "sealed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "Greywater Drift sealed as the miners' grave", Outcome = "sealed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "laid_the_nine_to_rest", Outcome = "sealed" },
                },
            });

            // ----- Chain 4: The Miller's Measure → A Mill for Two -----
            Add(new QuestDefinition
            {
                Id = MillersMeasure, Name = "The Miller's Measure", Category = QuestCategory.Settlement, DialogueId = "dlg_millers_measure",
                Description = "{giver} says {miller} takes too great a share of every sack ground at the mill. {settlement}'s bread depends on how it is settled.",
                Introduction = "Every sack I take to {miller}, I bring back lighter than the custom allows. They say the stones are worn and the price of everything's gone up. Maybe so. Look at the market book and talk to them — and if they're cheating us, say so.",
                Roles = new[]
                {
                    Npc("giver", "same", "Farmer", "Labourer"),
                    Npc("miller", "same", "Merchant", "Smith", "Innkeeper"),
                    SettlementOf("settlement", "giver"),
                    new RoleSpec { Name = "ledger", Kind = RoleKind.WorldObject, Value = "ledger:{settlement}" },
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "book", Stage = 0, Kind = ObjectiveKind.Investigate, Target = "role:ledger", Text = "Examine the market ledger in {settlement}." },
                    new ObjectiveDefinition { Id = "miller", Stage = 0, Kind = ObjectiveKind.TalkTo, Target = "role:miller", Text = "Hear {miller}'s side." },
                    new ObjectiveDefinition { Id = "fair", Stage = 1, Group = "resolve", Kind = ObjectiveKind.Persuade, Target = "role:miller", Outcome = "fair",
                        Text = "Persuade {miller} to grind at the old measure." },
                    new ObjectiveDefinition { Id = "miller_right", Stage = 1, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:miller_right", Outcome = "miller_right",
                        Text = "Tell {giver} the miller's price is fair, worn stones and all." },
                    new ObjectiveDefinition { Id = "supplied", Stage = 1, Group = "resolve", Kind = ObjectiveKind.Deliver, Target = "item:grain_sack", To = "giver", Quantity = 3, Outcome = "supplied",
                        Text = "Make up the shortfall yourself: bring {giver} 3 sacks of grain." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 150, Outcome = "fair", Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 100, Outcome = "miller_right" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 120, Outcome = "supplied" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Gratitude", Amount = 25, Outcome = "fair" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "miller", Key = "Affinity", Amount = 15, Outcome = "miller_right" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 30, Outcome = "miller_right", Shown = "The miller's thanks (30 coin)" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Gratitude", Amount = 30, Outcome = "supplied" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementResource, Role = "settlement", Key = "food", Amount = 30, Outcome = "fair" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementProsperity, Role = "settlement", Amount = -1, Outcome = "fair" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMorale, Role = "miller", Amount = -10, Outcome = "fair" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementProsperity, Role = "settlement", Amount = 2, Outcome = "miller_right" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = -2, Outcome = "miller_right" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcRegard, Role = "giver", Key = "Hostility", Amount = 15, Outcome = "miller_right" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementResource, Role = "settlement", Key = "food", Amount = 20, Outcome = "supplied" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "the dispute over the mill's measure settled", Outcome = "fair" },
                },
                FollowUps = new[] { MillForTwo },
            });

            Add(new QuestDefinition
            {
                Id = MillForTwo, Name = "A Mill for Two", Category = QuestCategory.Settlement, DialogueId = "dlg_mill_for_two",
                Description = "{miller}'s stones really are worn. New ones need dressed timber for the frame; with it, {settlement} could grind enough for two harvests.",
                Introduction = "Turns out the stones are worn, whatever else is true. {miller} says with a new frame the mill could grind twice what it does. Four planks of good timber — that's all it would take.",
                Roles = new[]
                {
                    Npc("giver", "same", "Farmer", "Labourer"),
                    Npc("miller", "same", "Merchant", "Smith", "Innkeeper"),
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "timber", Stage = 0, Kind = ObjectiveKind.Deliver, Target = "item:timber_plank", To = "miller", Quantity = 4, Text = "Bring {miller} 4 planks of timber." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 120, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 15, Shown = "Standing in {settlement}" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 30, Shown = "30 coin" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementProsperity, Role = "settlement", Amount = 4 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementResource, Role = "settlement", Key = "food", Amount = 40 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "new millstones set in {miller}'s mill" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "miller", Key = "rebuilt_the_mill" },
                },
            });
        }
    }

    public static partial class DialogueCatalog
    {
        /// <summary>Phase 18: the four chains' conversations.</summary>
        private static void AddPhase18()
        {
            AddPhase18Stories();
            AddPhase18Companions();
            Add(Giver("dlg_record_house", QuestCatalog.RecordHouse,
                "\"So it wasn't burned. It was hidden. That's worse, somehow.\"", "\"Have you been up to the tower?\"",
                "I'll find out what was hidden.", "Which tower?", "This is what was hidden at the tower. It's the true ledger.", "ledger"));

            Add(new DialogueDefinition
            {
                Id = "dlg_reeve", QuestId = QuestCatalog.ReevesAccounts,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[]
                        {
                            L("\"{introduction}\"", Offerable),
                            L("\"It's done, then. The town's heard it, and the town will remember.\"", Active, C(ConditionKind.ObjectiveDone, "exposed")),
                            L("\"Well? How did it end with {suspect}?\"", Active, C(ConditionKind.ObjectiveDone, "confront")),
                            L("\"Have you put it to {suspect} yet?\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll put it to them.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.RevealLocation, role: "suspect") } },
                            new DialogueResponse { Id = "expose", Text = "Here's the ledger. Let the town judge {suspect}.", Kind = ResponseKind.Give,
                                Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "confront"), C(ConditionKind.HasItem, "true_ledger"), C(ConditionKind.ObjectiveDone, "restored", negate: true), C(ConditionKind.ObjectiveDone, "bought", negate: true) },
                                Next = "giver", Consequences = new[] { Do(DialogueConsequenceKind.GiveItem, "true_ledger", 1), Do(DialogueConsequenceKind.SetOutcome, "exposed") } },
                            new DialogueResponse { Id = "report_exposed", Text = "It's over.", Kind = ResponseKind.Continue, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "exposed") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "report") } },
                            new DialogueResponse { Id = "report_restored", Text = "{suspect} will pay it all back, quietly. The stores will be made good.", Kind = ResponseKind.Continue,
                                Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "restored") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "report"), Do(DialogueConsequenceKind.Regard, "Trust", 5) } },
                            new DialogueResponse { Id = "report_bought", Text = "The ledger's gone. There's nothing more to find.", Kind = ResponseKind.Deceive,
                                Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "bought") }, Check = Check("Deception", 13, "-Trustfulness"), Next = "end",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.CompleteObjective, "report"), Do(DialogueConsequenceKind.Regard, "Trust", -30, onSuccess: false),
                                    Do(DialogueConsequenceKind.NpcMemory, "{player} lied to me about the ledger", 70, onSuccess: false),
                                } },
                            new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "suspect",
                        Lines = new[]
                        {
                            L("{npc} turns the ledger's pages without a word, then closes it. \"Where did you — no. It doesn't matter. What do you want?\"", Active, C(ConditionKind.HasItem, "true_ledger")),
                            L("\"I've nothing to say to you.\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "confront", Text = "Years of shaved measures, in your own hand.", Kind = ResponseKind.Challenge,
                                Conditions = new[] { Active, C(ConditionKind.HasItem, "true_ledger"), C(ConditionKind.ObjectiveDone, "confront", negate: true) }, Next = "suspect_cornered",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "confront"), Do(DialogueConsequenceKind.RevealObjective, "restored"), Do(DialogueConsequenceKind.RevealObjective, "bought") } },
                            new DialogueResponse { Id = "back", Text = "About the ledger…", Kind = ResponseKind.Continue,
                                Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "confront"), C(ConditionKind.ObjectiveDone, "restored", negate: true), C(ConditionKind.ObjectiveDone, "bought", negate: true), C(ConditionKind.ObjectiveDone, "exposed", negate: true) },
                                Next = "suspect_cornered" },
                            new DialogueResponse { Id = "bye", Text = "Never mind.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "suspect_cornered",
                        Lines = new[] { L("\"The stones were worn, the price of salt doubled, and nobody asked how the books balanced. I'm not the only one who did it — I'm the one who kept the record. So.\"") },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "restore", Text = "Pay it back. Every measure, into the stores, and I keep this quiet.", Kind = ResponseKind.Persuade, Check = Check("Persuasion", 13, "Greed"), Once = true,
                                Next = "end", NextOnFailure = "suspect_cornered",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.CompleteObjective, "restored", onSuccess: true), Do(DialogueConsequenceKind.SetOutcome, "restored", onSuccess: true),
                                    Do(DialogueConsequenceKind.GiveItem, "true_ledger", 1, onSuccess: true), Do(DialogueConsequenceKind.Regard, "Affinity", -5, onSuccess: false),
                                } },
                            new DialogueResponse { Id = "sell", Text = "What's my silence worth?", Kind = ResponseKind.Negotiate, Next = "end", Tags = ContentTags.None,
                                Conditions = new[] { C(ConditionKind.ObjectiveDone, "restored", negate: true) },
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.ReceiveCoin, amount: 60), Do(DialogueConsequenceKind.GiveItem, "true_ledger", 1),
                                    Do(DialogueConsequenceKind.Decision, "reeve_paid", value: "Took {suspect}'s silver and handed back the true ledger"), Do(DialogueConsequenceKind.SetOutcome, "bought"),
                                } },
                            new DialogueResponse { Id = "leave", Text = "That's for the town to hear.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                },
            });

            Add(Giver("dlg_cairn_question", QuestCatalog.CairnQuestion,
                "\"Two hands in the log. I knew it. Now we'll see what's to be done about it.\"", "\"Have you been to the cairn?\"",
                "I'll go and look.", "Where is the cairn?", "I read the watch's log. Here is what it says.", "log"));

            Add(new DialogueDefinition
            {
                Id = "dlg_cairn_keeping", QuestId = QuestCatalog.CairnKeeping,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[]
                        {
                            L("\"{introduction}\"", Offerable),
                            L("\"Whose is it? Say it plain, and I'll carry it to {holder}.\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll tell you what I think.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "giver",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest) } },
                            new DialogueResponse { Id = "claim", Text = "It was built by both. {holder} shut you out. Your claim is just — press it.", Kind = ResponseKind.Continue, Conditions = new[] { Active }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.Decision, "cairn_claimed", value: "Backed {faction}'s claim to Cairn Watch"), Do(DialogueConsequenceKind.SetOutcome, "claimed") } },
                            new DialogueResponse { Id = "uphold", Text = "Your side stopped sending people. {holder} kept the watch alone. It's theirs.", Kind = ResponseKind.Continue,
                                Conditions = new[] { Active, C(ConditionKind.KnowsDiscovery, "cairn_watch_log") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.Decision, "cairn_upheld", value: "Told {faction} that {holder} holds Cairn Watch fairly"), Do(DialogueConsequenceKind.SetOutcome, "upheld") } },
                            new DialogueResponse { Id = "share", Text = "Here is the old charter. Both built it; both keep it, by turns — as it says.", Kind = ResponseKind.Persuade,
                                Conditions = new[] { Active, C(ConditionKind.HasItem, "old_charter") }, Next = "end",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.GiveItem, "old_charter", 1), Do(DialogueConsequenceKind.Decision, "cairn_shared", value: "Held {faction} and {holder} to the old charter of Cairn Watch"),
                                    Do(DialogueConsequenceKind.SetOutcome, "shared"),
                                } },
                            new DialogueResponse { Id = "share_word", Text = "The log shows it was kept by turns. Go back to that.", Kind = ResponseKind.Persuade,
                                Conditions = new[] { Active, C(ConditionKind.HasItem, "old_charter", negate: true), C(ConditionKind.KnowsDiscovery, "cairn_watch_log"), C(ConditionKind.KnowsDiscovery, "cairn_boundary", "confirmed") },
                                Check = Check("Persuasion", 15), Once = true, Next = "end", NextOnFailure = "giver",
                                Consequences = new[]
                                {
                                    Do(DialogueConsequenceKind.Decision, "cairn_shared", value: "Talked {faction} round to keeping Cairn Watch by turns", onSuccess: true),
                                    Do(DialogueConsequenceKind.SetOutcome, "shared", onSuccess: true), Do(DialogueConsequenceKind.Regard, "Affinity", -3, onSuccess: false),
                                } },
                            new DialogueResponse { Id = "aside", Text = "This is between your people and theirs. I'll not choose for you.", Kind = ResponseKind.Continue, Conditions = new[] { Active }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.Decision, "cairn_aside", value: "Stayed out of the dispute over Cairn Watch"), Do(DialogueConsequenceKind.SetOutcome, "aside") } },
                            new DialogueResponse { Id = "later", Text = "Let me think on it.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                },
            });

            Add(Giver("dlg_drowned_drift", QuestCatalog.DrownedDrift,
                "\"Nine names on a slate, and the water to the third rung. And silver below it all. That's what they died for.\"", "\"Have you been down into Greywater?\"",
                "I'll go down.", "Where is the drift?", "I found the last shift's tally, and the seam they were digging.", "seam"));

            Add(new DialogueDefinition
            {
                Id = "dlg_drift_future", QuestId = QuestCatalog.DriftFuture,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"What do you say — work it, or seal it?\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll tell you what I think.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "giver",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest) } },
                            new DialogueResponse { Id = "reopen", Text = "Work it — but keep the pump as the drill board says, and the grate clear. It's dry now.", Kind = ResponseKind.Continue,
                                Conditions = new[] { Active, C(ConditionKind.KnowsDiscovery, "drift_drained") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.Decision, "drift_reopened", value: "Advised that Greywater Drift be worked again"), Do(DialogueConsequenceKind.SetOutcome, "reopened") } },
                            new DialogueResponse { Id = "reopen_wet", Text = "Work it.", Kind = ResponseKind.Continue, ShowWhenUnavailable = true,
                                Conditions = new[] { Active }, Requirements = new[] { C(ConditionKind.KnowsDiscovery, "drift_drained") },
                                UnavailableText = "Nobody will go down while the gallery is flooded. Get the pump working first.", Next = "end" },
                            new DialogueResponse { Id = "seal", Text = "Seal it. Let the nine lie where they are.", Kind = ResponseKind.Continue, Conditions = new[] { Active }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.Decision, "drift_sealed", value: "Advised that Greywater Drift be sealed as a grave"), Do(DialogueConsequenceKind.SetOutcome, "sealed") } },
                            new DialogueResponse { Id = "later", Text = "Let me think on it.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                },
            });

            Add(new DialogueDefinition
            {
                Id = "dlg_millers_measure", QuestId = QuestCatalog.MillersMeasure,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[]
                        {
                            L("\"{introduction}\"", Offerable),
                            L("\"Have you talked to {miller}?\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll look into it.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest), Do(DialogueConsequenceKind.RevealLocation, role: "miller") } },
                            new DialogueResponse { Id = "decline", Text = "Not my quarrel.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "right", Text = "The stones are worn and salt's dear. Their price is fair.", Kind = ResponseKind.Continue,
                                Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "book"), C(ConditionKind.ObjectiveDone, "miller") }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.Decision, "miller_right", value: "Told {giver} the miller's price is fair"), Do(DialogueConsequenceKind.SetOutcome, "miller_right") } },
                            new DialogueResponse { Id = "sacks", Text = "Here — three sacks, to make up what you've lost.", Kind = ResponseKind.Give,
                                Conditions = new[] { Active, C(ConditionKind.HasItem, "grain_sack", min: 3) }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.GiveItem, "grain_sack", 3), Do(DialogueConsequenceKind.SetOutcome, "supplied") } },
                            new DialogueResponse { Id = "later", Text = "Still looking into it.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "miller",
                        Lines = new[]
                        {
                            L("\"The custom? The custom was set when the stones were new and salt was cheap. Look at them — worn smooth as a river pebble. I take what keeps the wheel turning, and not a measure more.\"", Active),
                        },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "heard", Text = "I hear you.", Kind = ResponseKind.Continue, Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "miller", negate: true) }, Next = "miller",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "miller") } },
                            new DialogueResponse { Id = "fair", Text = "Grind at the old measure for this harvest, and I'll see the town helps with new stones.", Kind = ResponseKind.Persuade,
                                Conditions = new[] { Active, C(ConditionKind.ObjectiveDone, "miller"), C(ConditionKind.ObjectiveDone, "book") }, Check = Check("Persuasion", 13, "Greed"), Once = true,
                                Next = "end", NextOnFailure = "miller",
                                Consequences = new[] { Do(DialogueConsequenceKind.CompleteObjective, "fair", onSuccess: true), Do(DialogueConsequenceKind.SetOutcome, "fair", onSuccess: true) } },
                            new DialogueResponse { Id = "bye", Text = "Goodbye.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                },
            });

            Add(new DialogueDefinition
            {
                Id = "dlg_mill_for_two", QuestId = QuestCatalog.MillForTwo,
                Nodes = new[]
                {
                    new DialogueNode
                    {
                        Id = "giver",
                        Lines = new[] { L("\"{introduction}\"", Offerable), L("\"Any luck with the timber?\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "accept", Text = "I'll find the timber.", Kind = ResponseKind.AcceptQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.AcceptQuest) } },
                            new DialogueResponse { Id = "decline", Text = "Not now.", Kind = ResponseKind.DeclineQuest, Conditions = new[] { Offerable }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.DeclineQuest) } },
                            new DialogueResponse { Id = "later", Text = "Not yet.", Kind = ResponseKind.End, Conditions = new[] { Active }, Next = "end" },
                        },
                    },
                    new DialogueNode
                    {
                        Id = "miller",
                        Lines = new[] { L("\"Four planks, dressed and dry, and I'll have the new frame up in a week.\"", Active) },
                        Responses = new[]
                        {
                            new DialogueResponse { Id = "give", Text = "Here — four planks.", Kind = ResponseKind.Give, Conditions = new[] { Active, C(ConditionKind.HasItem, "timber_plank", min: 4) }, Next = "end",
                                Consequences = new[] { Do(DialogueConsequenceKind.GiveItem, "timber_plank", 4) } },
                            new DialogueResponse { Id = "bye", Text = "Goodbye.", Kind = ResponseKind.End, Next = "end" },
                        },
                    },
                },
            });
        }
    }
}
