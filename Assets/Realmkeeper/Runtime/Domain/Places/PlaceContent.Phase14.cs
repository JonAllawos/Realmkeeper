using System.Collections.Generic;
using Realmkeeper.Domain.Rpg;

namespace Realmkeeper.Domain.Places
{
    /// <summary>
    /// Phase 14 (ADR-0071): the five archetypes Phase 13 left in the catalogue become places with their own stories.
    /// Every name, clue and line here is Realmkeeper's own; the archetypes are generic kinds. No campaign canon is fixed:
    /// the histories are local (a village's founders, a faction's early days), told through what can be found.
    /// </summary>
    public static class Phase14Places
    {
        public const string Catacomb = "ashgrave_catacombs";
        public const string Temple = "lantern_fane";
        public const string Hideout = "crowfoot_den";
        public const string Works = "old_culverts";
        public const string Hamlet = "thornwick";

        public static readonly string[] All = { Catacomb, Temple, Hideout, Works, Hamlet };

        /// <summary>The features the new places' stories use (added to <see cref="FeatureCatalog"/>).</summary>
        public static IEnumerable<FeatureDefinition> Features()
        {
            FeatureDefinition F(string id, string name, FeatureKind kind, string look, Skill? skill = null, int dc = 0, string insight = null, string discovery = null, string missed = null) =>
                new FeatureDefinition
                {
                    Id = id, Name = name, Kind = kind, Look = look, Study = skill.HasValue ? new FeatureCheck { Skill = skill.Value, Dc = dc } : null, Insight = insight,
                    Missed = missed ?? "Nothing more comes of it — not yet, anyway.", Discovery = discovery,
                };

            // The Ashgrave Catacombs: a village's founders, and the fever year.
            yield return F("burial_register", "Burial register", FeatureKind.Document, "A bound register on a lectern, its pages stiff with damp.", Skill.History, 10,
                "The first names are the founders of the village above, laid here by their children. Then a run of entries in one summer, forty in a month, each marked with the same small cross — and after that, nothing. The lowest vault was sealed that autumn.",
                "ashgrave_founders");
            yield return F("fever_epitaph", "Plague epitaph", FeatureKind.Clue, "A slab cut in haste, the letters shallow.", Skill.Medicine, 12,
                "\"Taken by the sweating fever, the summer the well-water turned.\" Whoever cut this knew the sickness came from the water, not from the dead.",
                "ashgrave_fever");
            yield return F("sealed_vault", "Sealed vault", FeatureKind.Container, "A stone door set flush in the wall, sealed with lead and stamped with a warding sign.");
            yield return F("ossuary_wall", "Ossuary wall", FeatureKind.Clue, "Skulls and long bones stacked in neat courses, floor to ceiling.", Skill.Religion, 11,
                "Arranged with care, family by family — this was a place of respect, not a charnel pit.");

            // The Lantern Fane: a faction's first shrine, now held by a cult.
            yield return F("founders_mural", "Founders' mural", FeatureKind.Clue, "A mural of robed figures carrying lanterns up a hill, the paint flaking.", Skill.History, 11,
                "The banner the figures carry is the old form of a faction's colours. This fane was theirs once — before they had towns to hold.",
                "fane_founders");
            yield return F("reliquary", "Reliquary", FeatureKind.Container, "A brass-bound reliquary on the high altar, its lid shaped like a lantern's hood.");
            yield return F("cult_tract", "Cult tract", FeatureKind.Document, "Loose pages, copied out by hand many times.", Skill.Religion, 12,
                "A cult's tract: they mean to keep the fane's lantern unlit, so that \"the old light does not call the old masters home\". They have been here a season at most.",
                "fane_cult");
            yield return F("spilled_braziers", "Spilled braziers", FeatureKind.Clue, "Braziers knocked over; burning oil runs between the flagstones.", Skill.Survival, 10,
                "The oil will burn itself out in a few hours. Until then the floor is fire.");

            // Crowfoot Den: outlaws on the road.
            yield return F("toll_ledger", "Toll ledger", FeatureKind.Document, "A greasy book of names, wagons and amounts.", Skill.Investigation, 9,
                "Every wagon on the road between two towns, and what was taken from it. Whoever keeps this book is organised, and patient.",
                "crowfoot_tolls");
            yield return F("strongbox", "Strongbox", FeatureKind.Container, "An iron strongbox chained to a post.");
            yield return F("lookout_post", "Lookout post", FeatureKind.Clue, "A gap in the rock with a view down to the road, a horn hanging beside it.", Skill.Perception, 10,
                "From here they see every wagon long before it sees them.");

            // The Old Culverts: failing drains, a damaged bridge, marsh gas.
            yield return F("cracked_arch", "Cracked arch", FeatureKind.Clue, "A brick arch over the channel, split along its crown.", Skill.Investigation, 12,
                "The culvert arches are failing, one after another. When the last spring flood came up through the town's cellars, this is where it came from.",
                "culverts_failing");
            yield return F("sluice_wheel", "Sluice wheel", FeatureKind.Mechanism, "A great iron wheel that works the sluice gate, rusted but whole.", Skill.Athletics, 12,
                "Turned, it would drop the water in the channels — and the flooded galleries with it.");
            yield return F("engineers_plan", "Engineer's plan", FeatureKind.Document, "A plan of the culverts on oiled cloth, pinned to the wall.", Skill.Investigation, 9,
                "The works were dug in three stages; the newest gallery bypasses the old bridge — a longer way round, but dry.",
                "culverts_plan");
            yield return F("gas_seep", "Gas seep", FeatureKind.Clue, "Bubbles rise through the standing water; the air smells of rotten eggs.", Skill.Nature, 11,
                "Marsh gas. A naked flame here would be a mistake — and someone has already made it: the far end is burning.");

            // Thornwick: why the hamlet emptied.
            yield return F("boarded_well", "Boarded well", FeatureKind.Clue, "A well, boarded over and marked with a daubed cross.", Skill.Nature, 11,
                "The boards are old and the cross is a warning. The water below is foul — green, and smelling of the marsh.",
                "thornwick_well");
            yield return F("last_letter", "Last letter", FeatureKind.Document, "A letter left weighted under a cup on a table.", null, 0,
                "\"We are going to my sister's in the valley. The well went bad after the floods and the little ones sickened. Take what you need. Leave the door shut against the wolves.\"",
                "thornwick_why");
            yield return F("grain_bin", "Grain bin", FeatureKind.Container, "A wooden grain bin, its lid gnawed at the corners.");
            yield return F("smouldering_beams", "Smouldering beams", FeatureKind.Clue, "Roof beams that fell into the room, still glowing at the ends.", Skill.Survival, 9,
                "Someone camped here and their fire took the roof. It will burn out in a few hours.");
        }

        /// <summary>The five definitions (added to <see cref="PlaceCatalog"/>).</summary>
        public static IEnumerable<PlaceDefinition> Definitions()
        {
            yield return new PlaceDefinition
            {
                Id = Catacomb, Name = "The Ashgrave Catacombs", ArchetypeId = "catacomb", KeyItem = "ashgrave_vault_key",
                Description = "Catacombs under the old burying ground, where the first families of the village were laid. The lowest vault has been sealed for generations.",
                Story =
                {
                    ("burial_register", RoomKind.Chamber, true),
                    ("fever_epitaph", RoomKind.Tomb, false),
                    ("ossuary_wall", RoomKind.Corridor, false),
                    ("sealed_vault", RoomKind.Tomb, false),
                },
                Waking = { ("sealed_vault", "the vault's sleepers", new[] { "skeleton" }, 2) },
                StoryLoot = { ["sealed_vault"] = "vault_treasure" },
            };
            yield return new PlaceDefinition
            {
                Id = Temple, Name = "The Lantern Fane", ArchetypeId = "temple",
                Description = "A hilltop fane built by a faction in its earliest days and long forsaken. Lights have been seen there again.",
                Story =
                {
                    ("founders_mural", RoomKind.Hall, true),
                    ("cult_tract", RoomKind.Quarters, false),
                    ("reliquary", RoomKind.Shrine, true),
                    ("spilled_braziers", RoomKind.Chamber, false),
                },
                Waking = { ("reliquary", "the fane's old wardens", new[] { "skeleton" }, 2) },
                StoryLoot = { ["reliquary"] = "tomb_offerings" }, StoryItem = { ["reliquary"] = "fane_lantern" },
                FireRoom = RoomKind.Chamber,
            };
            yield return new PlaceDefinition
            {
                Id = Hideout, Name = "Crowfoot Den", ArchetypeId = "bandit_hideout", KeyItem = "crowfoot_strongbox_key",
                Description = "A cave above the road where outlaws watch the wagons go by — and stop the ones worth stopping.",
                Story =
                {
                    ("lookout_post", RoomKind.Entrance, false),
                    ("toll_ledger", RoomKind.Quarters, true),
                    ("strongbox", RoomKind.Storage, false),
                },
                Parley = true,
                StoryLoot = { ["strongbox"] = "stolen_goods" },
            };
            yield return new PlaceDefinition
            {
                Id = Works, Name = "The Old Culverts", ArchetypeId = "underground",
                Description = "The drains and culverts under a town, dug in three stages and half forgotten. Since the floods nobody goes down willingly.",
                Story =
                {
                    ("engineers_plan", RoomKind.Hall, true),
                    ("cracked_arch", RoomKind.Chamber, false),
                    ("gas_seep", RoomKind.Storage, false),
                    ("sluice_wheel", RoomKind.Vault, true),
                },
                Bridges = 1, DamagedBridge = true, FireRoom = RoomKind.Storage,
            };
            yield return new PlaceDefinition
            {
                Id = Hamlet, Name = "Thornwick", ArchetypeId = "abandoned_settlement",
                Description = "A hamlet of a few houses, empty since the floods. The doors are shut; the wolves come down at dusk.",
                Story =
                {
                    ("boarded_well", RoomKind.Yard, true),
                    ("last_letter", RoomKind.Quarters, true),
                    ("grain_bin", RoomKind.Storage, false),
                    ("smouldering_beams", RoomKind.Workshop, false),
                },
                Waking = { ("grain_bin", "rats in the grain", new[] { "giant_rat" }, 3) },
                StoryLoot = { ["grain_bin"] = "household_goods" },
                FireRoom = RoomKind.Workshop,
            };
        }
    }
}
