using System;
using System.Collections.Generic;

namespace Realmkeeper.Domain.Quests
{
    /// <summary>
    /// The quest templates (ADR-0056). A small number of complete, working quests rather than many thin ones: three authored
    /// for the vertical slice (a settlement problem with an investigation and alternative endings, a hunt through the
    /// encounter system, a delivery of a particular item with a choice) and two that arise from the world (a food shortage,
    /// a merchant out of stock). All text is Realmkeeper's own; no setting canon (kingdoms, the protagonist's identity, a
    /// central antagonist) is fixed here. The main-campaign category exists and has no authored quests yet.
    /// </summary>
    public static partial class QuestCatalog
    {
        public const string GranaryShortfall = "granary_shortfall";
        public const string WolvesAtTheFolds = "predators_at_the_folds";
        public const string SealedLetter = "sealed_letter";
        public const string FoodRelief = "food_relief";
        public const string ShopSupplies = "shop_supplies";

        private static readonly Dictionary<string, QuestDefinition> ById = new Dictionary<string, QuestDefinition>(StringComparer.Ordinal);

        public static IEnumerable<QuestDefinition> All => ById.Values;

        public static QuestDefinition Find(string id) => id != null && ById.TryGetValue(id, out var d) ? d : null;

        private static void Add(QuestDefinition d) => ById[d.Id] = d;

        private static RoleSpec Npc(string name, string where, params string[] roles) => new RoleSpec { Name = name, Kind = RoleKind.Npc, Where = where, NpcRoles = roles };
        private static RoleSpec SettlementOf(string name, string of) => new RoleSpec { Name = name, Kind = RoleKind.Settlement, Of = of };
        private static QuestCondition C(ConditionKind k, string key = null, string value = null, int min = int.MinValue, int max = int.MaxValue, bool negate = false) => QuestCondition.Of(k, key, value, min, max, negate);

        static QuestCatalog()
        {
            // ---- 1. A settlement problem: grain gone from the stores, an investigation, three ways to end it ----------------
            Add(new QuestDefinition
            {
                Id = GranaryShortfall, Name = "Short Measures", Category = QuestCategory.Investigation, DialogueId = "dlg_granary",
                Description = "Grain has gone missing from the {settlement} granary: {missing} measures that the ledger says should be there. {giver} wants to know where it went.",
                Introduction = "The granary's down {missing} measures and nobody will say how. I can't spare a guard to sit with the ledger. You've a sharp eye — find out where it went.",
                Roles = new[]
                {
                    Npc("giver", "same", "Elder", "Guard", "Merchant"),
                    SettlementOf("settlement", "giver"),
                    Npc("keeper", "same", "Farmer", "Labourer", "Merchant", "Resident"),
                    new RoleSpec { Name = "suspect", Kind = RoleKind.Npc, Where = "same", NpcRoles = new[] { "Labourer", "Resident", "Merchant", "Farmer", "Smith" }, NeedsFaction = true },
                    new RoleSpec { Name = "suspect_faction", Kind = RoleKind.Faction, Of = "suspect" },
                    new RoleSpec { Name = "ledger", Kind = RoleKind.WorldObject, Value = "ledger:{settlement}" },
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "ledger", Stage = 0, Kind = ObjectiveKind.Investigate, Target = "role:ledger", Text = "Examine the granary ledger in {settlement}." },
                    new ObjectiveDefinition { Id = "keeper", Stage = 0, Kind = ObjectiveKind.TalkTo, Target = "role:keeper", Text = "Ask {keeper}, who keeps the stores, what they saw." },
                    new ObjectiveDefinition { Id = "suspect", Stage = 1, Kind = ObjectiveKind.TalkTo, Target = "role:suspect", Hidden = true, Text = "Find {suspect} and put the matter to them." },
                    new ObjectiveDefinition { Id = "returned", Stage = 2, Group = "resolve", Kind = ObjectiveKind.Persuade, Target = "role:suspect", Hidden = true, Outcome = "returned",
                        Text = "Get {suspect} to put the grain back." },
                    new ObjectiveDefinition { Id = "replaced", Stage = 2, Group = "resolve", Kind = ObjectiveKind.Deliver, Target = "item:grain_sack", To = "keeper", Quantity = 4, Hidden = true, Outcome = "replaced",
                        Text = "Make the loss good yourself: bring {keeper} 4 sacks of grain." },
                    new ObjectiveDefinition { Id = "covered", Stage = 2, Group = "resolve", Kind = ObjectiveKind.Decision, Target = "decision:took_silver", Hidden = true, Outcome = "covered",
                        Text = "Take {suspect}'s silver and let the matter lie." },
                    new ObjectiveDefinition { Id = "report", Stage = 3, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Tell {giver} how it ended." },
                },
                Failures = new[]
                {
                    new FailureDefinition { Kind = FailureKind.NpcDied, Key = "giver", Text = "{giver} is dead; nobody else is asking." },
                    new FailureDefinition { Kind = FailureKind.TimeLimit, Amount = 21, Text = "Three weeks passed and the trail went cold." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 150, Outcome = "returned", Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 120, Outcome = "replaced", Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 60, Outcome = "covered" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 40, Outcome = "returned", Shown = "{giver} will pay 40 coin" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 70, Outcome = "replaced", Shown = "70 coin if you make the loss good" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Gratitude", Amount = 20, Outcome = "returned" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Trust", Amount = 15, Outcome = "returned" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Gratitude", Amount = 25, Outcome = "replaced" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 15, Outcome = "returned", Shown = "Standing in {settlement}" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 20, Outcome = "replaced" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "giver", Amount = 8, Outcome = "returned" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "suspect", Key = "Gratitude", Amount = 20, Outcome = "covered" },
                },
                Consequences = new[]
                {
                    // The problem is real: the grain leaves the settlement's stores when the quest comes into being.
                    new ConsequenceDefinition { When = ConsequenceTiming.OnAvailable, Kind = ConsequenceKind.SettlementResource, Role = "settlement", Key = "food", Amount = -1, Value = "var:missing" },
                    new ConsequenceDefinition { When = ConsequenceTiming.OnAvailable, Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "grain missing from the stores" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementResource, Role = "settlement", Key = "food", Amount = 1, Value = "var:missing", Outcome = "returned" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementResource, Role = "settlement", Key = "food", Amount = 1, Value = "var:missing", Outcome = "replaced" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = 3, Outcome = "returned" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = 2, Outcome = "replaced" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = -2, Outcome = "covered" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.FactionInfluence, Role = "suspect", Amount = -3, Outcome = "returned" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcRegard, Role = "suspect", Key = "Hostility", Amount = 30, Outcome = "returned" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcRegard, Role = "suspect", Key = "Fear", Amount = 15, Outcome = "returned" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "suspect", Key = "Made to return the grain by {player}", Amount = 70, Outcome = "returned" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "giver", Key = "{player} found the missing grain", Amount = 60, Outcome = "returned" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "giver", Key = "{player} made good the missing grain", Amount = 60, Outcome = "replaced" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "suspect", Key = "{player} took silver to keep quiet", Amount = 60, Outcome = "covered" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "recovered_grain", Outcome = "returned" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "fed_settlement", Outcome = "replaced" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.Notoriety, Amount = 5, Outcome = "covered" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "the missing grain was returned to the stores", Outcome = "returned" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "a stranger made good the missing grain", Outcome = "replaced" },
                },
            });

            // ---- 2. A hunt: predators taking stock near a settlement, through the encounter system --------------------------
            Add(new QuestDefinition
            {
                Id = WolvesAtTheFolds, Name = "Teeth at the Folds", Category = QuestCategory.Hunting, DialogueId = "dlg_predators",
                Description = "{creature_plural} have been coming down on the herds around {settlement}. {giver} is offering coin for {count} of them.",
                Introduction = "We've lost stock three nights running. {creature_plural}, by the tracks. I can't leave the gate. Bring down {count} and there's coin in it.",
                Roles = new[]
                {
                    Npc("giver", "same", "Guard", "Farmer", "Elder"),
                    SettlementOf("settlement", "giver"),
                    new RoleSpec { Name = "creature", Kind = RoleKind.Creature, Value = "wolf" },
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "hunt", Stage = 0, Kind = ObjectiveKind.DefeatCreature, Target = "role:creature", Quantity = 2, Text = "Bring down {count} {creature_plural} near {settlement} (go out to the farms)." },
                    new ObjectiveDefinition { Id = "survive", Stage = 0, Optional = true, Kind = ObjectiveKind.SurviveEncounter, Target = "role:creature", Text = "Optional: come back from every hunt on your feet." },
                    new ObjectiveDefinition { Id = "report", Stage = 1, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Tell {giver} the folds are safer." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.NpcDied, Key = "giver", Text = "{giver} is dead." } },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 100, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 35, Shown = "35 coin bounty" },
                    new RewardDefinition { Kind = RewardKind.Item, Key = "potion_healing", Amount = 1, Shown = "A potion of healing" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Respect", Amount = 20 },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 10, Shown = "Standing in {settlement}" },
                    new RewardDefinition { Kind = RewardKind.FactionStanding, Role = "giver", Amount = 5 },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = 2 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "giver", Key = "{player} hunted the {creature_plural} off the folds", Amount = 55 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "hunted_predators" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "the herds were made safer from {creature_plural}" },
                },
            });

            // ---- 3. A delivery of one particular letter, and whether to break its seal -----------------------------------
            Add(new QuestDefinition
            {
                Id = SealedLetter, Name = "Under Seal", Category = QuestCategory.DeliveryRetrieval, DialogueId = "dlg_letter",
                Description = "{giver} has a sealed letter for {recipient} in {recipient_settlement} and will not trust it to a carter.",
                Introduction = "This is for {recipient}, in {recipient_settlement}, and for nobody else's eyes. Carters read. I'm told you don't.",
                Roles = new[]
                {
                    Npc("giver", "same", "Merchant", "Innkeeper", "Smith", "Elder"),
                    SettlementOf("settlement", "giver"),
                    Npc("recipient", "other", "Merchant", "Elder", "Innkeeper", "Resident", "Farmer"),
                    SettlementOf("recipient_settlement", "recipient"),
                    new RoleSpec { Name = "letter", Kind = RoleKind.Item, Value = "sealed_letter" },
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "deliver", Stage = 0, Kind = ObjectiveKind.Deliver, Target = "var:letter_stack", To = "recipient", Text = "Put the sealed letter into {recipient}'s hands, in {recipient_settlement}." },
                    new ObjectiveDefinition { Id = "read", Stage = 0, Optional = true, Hidden = true, Kind = ObjectiveKind.DiscoverInfo, Target = "info:letter_contents", Text = "You broke the seal and read it." },
                    new ObjectiveDefinition { Id = "reach", Stage = 0, Optional = true, Kind = ObjectiveKind.ReachSettlement, Target = "role:recipient_settlement", Text = "Travel to {recipient_settlement}." },
                },
                Failures = new[]
                {
                    new FailureDefinition { Kind = FailureKind.NpcDied, Key = "recipient", Text = "{recipient} is dead; the letter will never be read by the one it was for." },
                    new FailureDefinition { Kind = FailureKind.TimeLimit, Amount = 30, Text = "A month went by; the news in it is stale." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 80, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 25, Outcome = "sealed", Shown = "{recipient} will pay the carriage" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 10, Outcome = "opened" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "recipient", Key = "Gratitude", Amount = 15, Outcome = "sealed" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Trust", Amount = 15, Outcome = "sealed" },
                    new RewardDefinition { Kind = RewardKind.Information, Key = "letter_contents", Outcome = "opened" },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcRegard, Role = "recipient", Key = "Trust", Amount = -20, Outcome = "opened" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "recipient", Key = "{player} brought a letter with its seal broken", Amount = 50, Outcome = "opened" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "recipient", Key = "{player} brought {giver}'s letter safely", Amount = 40, Outcome = "sealed" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "recipient", Key = "opened_letter", Outcome = "opened" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "recipient", Key = "kept_seal", Outcome = "sealed" },
                },
            });

            // ---- 4. Emergent: a settlement short of food asks for food ---------------------------------------------------
            Add(new QuestDefinition
            {
                Id = FoodRelief, Name = "Bread Before Winter", Category = QuestCategory.Emergent, DialogueId = "dlg_relief", ExpiresAfterDays = 14,
                Description = "{settlement} is short of food. {giver} is asking anyone with means to bring provisions to the stores.",
                Introduction = "The stores won't last. Bread, rations, grain — whatever you can carry. The people here won't forget it.",
                Roles = new[] { Npc("giver", "same", "Elder", "Farmer", "Merchant"), SettlementOf("settlement", "giver") },
                Availability = new[] { C(ConditionKind.SettlementFoodShort, "settlement") },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "deliver", Stage = 0, Kind = ObjectiveKind.Deliver, Target = "item:@food", To = "giver", Quantity = 8, Text = "Bring {giver} 8 portions of food (bread, rations or grain)." },
                    new ObjectiveDefinition { Id = "report", Stage = 1, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Let {giver} know the stores are filled." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.CauseEnded, Text = "The shortage in {settlement} ended before the food came." } },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 60, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.SettlementReputation, Role = "settlement", Amount = 20, Shown = "Standing in {settlement}" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Gratitude", Amount = 20 },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementResource, Role = "settlement", Key = "food", Amount = 1, Value = "var:delivered_food" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.SettlementStability, Role = "settlement", Amount = 2 },
                    new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "fed_settlement" },
                    new ConsequenceDefinition { Kind = ConsequenceKind.ChronicleNote, Role = "settlement", Key = "a traveller brought food to the stores in the shortage" },
                },
            });

            // ---- 5. Emergent: a merchant out of a material they deal in -------------------------------------------------
            Add(new QuestDefinition
            {
                Id = ShopSupplies, Name = "Empty Shelves", Category = QuestCategory.CraftingGathering, DialogueId = "dlg_supplies", ExpiresAfterDays = 10,
                Description = "{giver} has no {item_name} left to sell and pays above the price for {quantity}.",
                Introduction = "Not a scrap of {item_name} in the place, and people keep asking. Bring me {quantity} and I'll pay better than I sell for.",
                Roles = new[] { Npc("giver", "same", "Merchant", "Smith"), SettlementOf("settlement", "giver"), new RoleSpec { Name = "item", Kind = RoleKind.Item, Value = "timber_plank" } },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "deliver", Stage = 0, Kind = ObjectiveKind.Deliver, Target = "role:item", To = "giver", Quantity = 4, Text = "Bring {giver} {quantity} {item_name} (gather or make them)." },
                },
                Failures = new[] { new FailureDefinition { Kind = FailureKind.CauseEnded, Text = "{giver} restocked from elsewhere." } },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 40, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Coin, Amount = 0, Key = "var:payment", Shown = "Paid above the shop's price" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Trust", Amount = 10 },
                },
                Consequences = new[] { new ConsequenceDefinition { Kind = ConsequenceKind.PlayerDeed, Role = "giver", Key = "supplied_shop" } },
            });
            AddPhase13(); // Phase 13: quests in places
        }

        /// <summary>Checks the templates: unique IDs, a giver first, objectives with IDs and stages, every role named in text and targets defined.</summary>
        public static List<string> Validate()
        {
            var errors = new List<string>();
            foreach (var d in All)
            {
                if (d.Roles.Count == 0 || d.Roles[0].Name != "giver") errors.Add($"Quest '{d.Id}' has no giver role first.");
                var roles = new HashSet<string>(StringComparer.Ordinal);
                foreach (var r in d.Roles) if (!roles.Add(r.Name)) errors.Add($"Quest '{d.Id}' names role '{r.Name}' twice.");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                var required = 0;
                foreach (var o in d.Objectives)
                {
                    if (string.IsNullOrEmpty(o.Id) || !ids.Add(o.Id)) errors.Add($"Quest '{d.Id}' has a missing or repeated objective ID.");
                    if (o.Quantity < 1) errors.Add($"Quest '{d.Id}' objective '{o.Id}' needs {o.Quantity}.");
                    if (!o.Optional) required++;
                    if (o.Target != null && o.Target.StartsWith("role:", StringComparison.Ordinal) && !roles.Contains(o.Target.Substring(5).Split('#')[0])) errors.Add($"Quest '{d.Id}' objective '{o.Id}' targets unknown role '{o.Target}'.");
                    if (o.To != null && !roles.Contains(o.To)) errors.Add($"Quest '{d.Id}' objective '{o.Id}' delivers to unknown role '{o.To}'.");
                }

                if (required == 0) errors.Add($"Quest '{d.Id}' has no required objective.");
                foreach (var r in d.Rewards)
                    if (r.Role != null && !roles.Contains(r.Role)) errors.Add($"Quest '{d.Id}' rewards unknown role '{r.Role}'.");
                foreach (var c in d.Consequences)
                    if (c.Role != null && !roles.Contains(c.Role)) errors.Add($"Quest '{d.Id}' consequence names unknown role '{c.Role}'.");
                if (d.DialogueId != null && DialogueCatalog.Find(d.DialogueId) == null) errors.Add($"Quest '{d.Id}' uses unknown dialogue '{d.DialogueId}'.");
                if (string.IsNullOrWhiteSpace(d.Name) || string.IsNullOrWhiteSpace(d.Description)) errors.Add($"Quest '{d.Id}' has no name or description.");
            }

            return errors;
        }
    }
}
