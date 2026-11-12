namespace Realmkeeper.Domain.Quests
{
    public static partial class QuestCatalog
    {
        public const string CompanionRoad = "companion_old_ground";

        /// <summary>
        /// Phase 15 (ADR-0074): one personal quest a companion can ask of the player once they trust them (loyalty 60+).
        /// It is bound to that companion and to a place near their home, and advances only from real events: entering the
        /// place, then talking it over with them. Done, it earns their loyalty through the companion-loyalty rules
        /// (abandoned or failed, it costs it). All text is Realmkeeper's own.
        /// </summary>
        private static void AddPhase15()
        {
            Add(new QuestDefinition
            {
                Id = CompanionRoad, Name = "Old Ground", Category = QuestCategory.Personal, DialogueId = "dlg_old_ground",
                Description = "{giver} wants to walk {place}, near {settlement}, with you beside them.",
                Introduction = "There's a place near {settlement} I've never had the nerve to go into alone: {place}. The stories my family tells start there. Walk it with me, and I'll tell you why it matters.",
                Roles = new[]
                {
                    Npc("giver", "same"),
                    new RoleSpec { Name = "place", Kind = RoleKind.WorldObject },
                    SettlementOf("settlement", "giver"),
                },
                Objectives = new[]
                {
                    new ObjectiveDefinition { Id = "enter", Stage = 0, Kind = ObjectiveKind.VisitLocation, Target = "role:place", Text = "Go into {place} with {giver}." },
                    new ObjectiveDefinition { Id = "report", Stage = 1, Kind = ObjectiveKind.TalkTo, Target = "role:giver", Text = "Talk it over with {giver}." },
                },
                Rewards = new[]
                {
                    new RewardDefinition { Kind = RewardKind.Experience, Amount = 100, Shown = "Experience" },
                    new RewardDefinition { Kind = RewardKind.Regard, Role = "giver", Key = "Trust", Amount = 10 },
                },
                Consequences = new[]
                {
                    new ConsequenceDefinition { Kind = ConsequenceKind.NpcMemory, Role = "giver", Key = "walked {place} with {player}" },
                },
            });
        }
    }

    public static partial class DialogueCatalog
    {
        /// <summary>Phase 15: the companion's own quest, asked and reported in the giver pattern.</summary>
        private static void AddPhase15()
        {
            Add(Giver("dlg_old_ground", QuestCatalog.CompanionRoad,
                "\"So that's what it looks like. Smaller than the stories. Thank you for coming with me.\"", "\"Will we go to {place} soon?\"",
                "Let's go together.", "Where is it?", "We walked it. Tell me the story now.", "enter"));
        }
    }
}
