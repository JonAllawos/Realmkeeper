using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Infrastructure.Persistence
{
    /// <summary>
    /// v10 → v11 (Phase 9: spellcasting, class features, rests). Adds the character's spellcasting record (no prepared
    /// spells, the preparation window open, no rests yet) and moves a character not yet created to rules version 8 (it has
    /// made no choices, so nothing changes for it). A created character keeps its rules version: its spell picks are
    /// new "up to" choices it can make in the spellbook; nothing is chosen or reset for it. Encounters gain their active
    /// effects, combatant resources and concentration as empty when absent (the mapper reads absent lists as empty).
    /// </summary>
    /// <summary>
    /// v13 → v14 (Phase 12: quests, dialogue, reputation). Structural: an empty quest list, no conversation, an empty
    /// social record on the player (no standing, no notoriety, nothing discovered), regard dimensions absent (read as 0),
    /// content presentation absent (everything shown), and the quest system enabled after gathering. The authored quests
    /// and today's emergent ones are not written here: the session adds them once with the rest of the living world, so an
    /// old save and a fresh world of its seed agree (as Phases 10 and 11 do).
    /// </summary>
    /// <summary>
    /// v14 → v15 (Phase 13: places). Structural: empty lists of places and escorts. The places themselves are not written
    /// here: the session adds them once with the rest of the living world, so an old save and a fresh world of its seed
    /// get the same places. A player in a settlement or on the map stays where they were.
    /// </summary>
    /// <summary>
    /// v15 → v16 (Phase 14: conditions, crime, bridges). Structural: an empty crime list (the next number 1). Lasting
    /// conditions, fight doors, bridge states and woken groups are absent and read as none. The five new places are not
    /// written here: the session ensures each place definition once with the living world, so a Phase 13 world gains them
    /// and keeps every place, room, door, chest, creature and quest it had.
    /// </summary>
    public sealed class MigrationV15ToV16 : ISaveMigration
    {
        public int SourceVersion => 15;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world)) return;
            if (world["crimes"] == null) world["crimes"] = new JArray();
            if (world["nextCrimeNumber"] == null) world["nextCrimeNumber"] = 1;
        }
    }

    public sealed class MigrationV14ToV15 : ISaveMigration
    {
        public int SourceVersion => 14;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world)) return;
            foreach (var list in new[] { "places", "escorts" })
                if (world[list] == null) world[list] = new JArray();
        }
    }

    public sealed class MigrationV13ToV14 : ISaveMigration
    {
        public int SourceVersion => 13;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world)) return;
            if (world["quests"] == null) world["quests"] = new JArray();
            if (world["player"] is JObject player && player["social"] == null) player["social"] = new JObject();
            if (!(world["settings"] is JObject settings) || !(settings["enabledSimulationSystems"] is JArray systems)) return;
            JToken after = null;
            var has = false;
            foreach (var id in systems)
            {
                if (id.Type != JTokenType.String) continue;
                if ((string)id == Simulation.Items.GatheringSystem.Id) after = id;
                if ((string)id == Simulation.Quests.QuestSystem.Id) has = true;
            }

            if (after != null && !has) after.AddAfterSelf(Simulation.Quests.QuestSystem.Id);
        }
    }

    /// <summary>
    /// v12 → v13 (Phase 11: items). Structural: empty containers, work and gathering sites; catalog version 1 (the
    /// items the save was made with); every shop's particular goods empty; and the gathering system enabled after the
    /// fauna system. Existing stacks need nothing: absent instance state reads as good condition, common make, no
    /// charges, not attuned. The player's chests, caches and gathering sites are not written here: the session adds them
    /// once with the rest of the living world (as for Phase 10), so an old save and a fresh world of its seed agree.
    /// </summary>
    public sealed class MigrationV12ToV13 : ISaveMigration
    {
        public int SourceVersion => 12;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world)) return;
            foreach (var list in new[] { "containers", "workJobs", "gatheringSites" })
                if (world[list] == null) world[list] = new JArray();
            if (world["itemCatalogVersion"] == null) world["itemCatalogVersion"] = 1;
            if (world["shops"] is JArray shops)
                foreach (var shop in shops)
                    if (shop is JObject s && s["goods"] == null) s["goods"] = new JArray();
            if (!(world["settings"] is JObject settings) || !(settings["enabledSimulationSystems"] is JArray systems)) return;
            JToken after = null;
            var has = false;
            foreach (var id in systems)
            {
                if (id.Type != JTokenType.String) continue;
                if ((string)id == Simulation.Fauna.FaunaSystem.Id) after = id;
                if ((string)id == Simulation.Items.GatheringSystem.Id) has = true;
            }

            if (after != null && !has) after.AddAfterSelf(Simulation.Items.GatheringSystem.Id);
        }
    }

    /// <summary>
    /// v11 → v12 (Phase 10: the living world). Structural only: empty creature populations, creatures, herds and settlement
    /// layouts, and the fauna system enabled after the chronicle's predecessors (where the standard systems run). NPC
    /// character records, populations, herds and layouts are not written here: the session adds them once, after the
    /// region map is regenerated (ADR-0048), so an old save and a fresh world of its seed get the same additions.
    /// </summary>
    public sealed class MigrationV11ToV12 : ISaveMigration
    {
        public int SourceVersion => 11;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world)) return;
            foreach (var list in new[] { "creaturePopulations", "creatures", "livestock", "settlementLayouts" })
                if (world[list] == null) world[list] = new JArray();
            if (!(world["settings"] is JObject settings) || !(settings["enabledSimulationSystems"] is JArray systems)) return;
            JToken chronicle = null;
            var hasFauna = false;
            foreach (var id in systems)
            {
                if (id.Type != JTokenType.String) continue;
                if ((string)id == ChronicleSystem.Id) chronicle = id;
                if ((string)id == Simulation.Fauna.FaunaSystem.Id) hasFauna = true;
            }

            if (chronicle != null && !hasFauna) chronicle.AddAfterSelf(Simulation.Fauna.FaunaSystem.Id);
        }
    }

    public sealed class MigrationV10ToV11 : ISaveMigration
    {
        public int SourceVersion => 10;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world) || !(world["player"] is JObject player) || !(player["rpg"] is JObject rpg)) return;
            if (rpg["spellcasting"] == null)
                rpg["spellcasting"] = new JObject { ["prepared"] = new JArray(), ["preparationChanges"] = -1, ["lastLongRestEnded"] = -1, ["restLog"] = new JArray() };
            if ((string)rpg["state"] != "Complete") rpg["rulesVersion"] = 8;
        }
    }

    /// <summary>
    /// v9 → v10 (Phase 8: combat foundation, settlement structures). Adds an empty encounter list and the
    /// character's dying state (no death saves, not stable, not dead). Structures are not written here: the loader
    /// builds them from the save's own locations and shops after mapping (deterministic, once). Everything else,
    /// including hit points, inventory and equipment, is untouched; armour class is derived, so the SRD armour
    /// rules apply to the loaded character without changing any saved value.
    /// </summary>
    public sealed class MigrationV9ToV10 : ISaveMigration
    {
        public int SourceVersion => 9;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world)) return;
            if (world["encounters"] == null) world["encounters"] = new JArray();
            if (world["player"] is JObject player && player["rpg"] is JObject rpg)
            {
                if (rpg["deathSaveSuccesses"] == null) rpg["deathSaveSuccesses"] = 0;
                if (rpg["deathSaveFailures"] == null) rpg["deathSaveFailures"] = 0;
                if (rpg["isStable"] == null) rpg["isStable"] = false;
                if (rpg["isDead"] == null) rpg["isDead"] = false;
            }
        }
    }

    /// <summary>
    /// v8 → v9 (Phase 7: progression). Adds the progression record to the character and changes nothing else:
    /// <list type="bullet">
    /// <item>the rules version: 6 for a created character (its choices were made under Phase 6 rules, so the
    /// level-1 choices Phase 7 introduced stay open until its next advancement), 7 for one not yet created;</item>
    /// <item>an empty experience ledger and advancement history (a v8 character is level 1; experience it already
    /// had, which Phase 6 never awarded, is kept as one ledger entry so nothing is lost);</item>
    /// <item>hit dice, full (one per level), for a created character;</item>
    /// <item>the warlock's level-1 slot moves from <c>spell_slots_1</c> to <c>pact_slots</c>, keeping its uses.</item>
    /// </list>
    /// Identity, species, background, class, scores, choices, hit points, inventory, equipment, purse, place and
    /// chronicle are untouched.
    /// </summary>
    public sealed class MigrationV8ToV9 : ISaveMigration
    {
        public int SourceVersion => 8;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world) || !(world["player"] is JObject player) || !(player["rpg"] is JObject rpg)) return;
            var complete = (string)rpg["state"] == "Complete";
            if (rpg["rulesVersion"] == null) rpg["rulesVersion"] = complete ? 6 : 7;
            if (rpg["advancements"] == null) rpg["advancements"] = new JArray();
            if (rpg["experienceLog"] == null)
            {
                var log = new JArray();
                var xp = (int?)rpg["experience"] ?? 0;
                if (xp > 0)
                    log.Add(new JObject
                    {
                        ["key"] = "migrated:v8", ["source"] = "Other", ["amount"] = xp,
                        ["reason"] = "Experience recorded before the experience ledger existed",
                    });
                rpg["experienceLog"] = log;
            }

            if (!complete) return;
            if (!(rpg["resources"] is JObject resources)) rpg["resources"] = resources = new JObject();
            if (resources["hit_dice"] == null) resources["hit_dice"] = (int?)rpg["level"] ?? 1;
            if ((string)rpg["classId"] == "warlock" && resources["spell_slots_1"] != null && resources["pact_slots"] == null)
            {
                resources["pact_slots"] = resources["spell_slots_1"];
                resources.Remove("spell_slots_1");
            }
        }
    }

    /// <summary>
    /// v7 → v8 (Phase 6: character creation). Gives the player an explicit legacy character record: no
    /// species, background, class, scores or kit are assigned or invented, and nothing else changes (identity,
    /// appearance, belongings, purse, location). The game then offers character setup, which grants the class
    /// and background kit once.
    /// </summary>
    public sealed class MigrationV7ToV8 : ISaveMigration
    {
        public int SourceVersion => 7;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world) || !(world["player"] is JObject player)) return;
            if (player["rpg"] != null) return;
            player["rpg"] = new JObject { ["state"] = "Legacy", ["level"] = 1, ["experience"] = 0 };
        }
    }

    /// <summary>
    /// v6 → v7 (Phase 5: living world, player hub, commerce). Structural only: adds the minute of the
    /// day (08:00), empty <c>shops</c> and <c>transactions</c>, and enables the shop system after the NPC
    /// system. Shops and the player's belongings cannot come from JSON, so after mapping the serializer
    /// runs <see cref="Simulation.Commerce.CommerceGenesis"/> once: shops kept by existing residents and
    /// stocked from the settlement's own stores (paid for by the keeper), and the player's starting
    /// clothes, knife, bread and purse, the same as a new world's (the player had no belongings before).
    /// </summary>
    public sealed class MigrationV6ToV7 : ISaveMigration
    {
        public int SourceVersion => 6;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world)) return;
            if (world["minuteOfDay"] == null) world["minuteOfDay"] = Domain.Calendar.WorldTime.DefaultStartMinute;
            if (world["shops"] == null) world["shops"] = new JArray();
            if (world["transactions"] == null) world["transactions"] = new JArray();
            if (!(world["settings"] is JObject settings) || !(settings["enabledSimulationSystems"] is JArray systems)) return;
            JToken npcs = null;
            var hasShops = false;
            foreach (var id in systems)
            {
                if (id.Type != JTokenType.String) continue;
                if ((string)id == Simulation.Npcs.NpcSystem.Id) npcs = id;
                if ((string)id == Simulation.Commerce.ShopSystem.Id) hasShops = true;
            }

            if (npcs != null && !hasShops) npcs.AddAfterSelf(Simulation.Commerce.ShopSystem.Id);
        }
    }

    /// <summary>
    /// v5 → v6 (Phase 4: NPC Intelligence Engine). Structural only: adds empty <c>locations</c> and
    /// <c>npcRelations</c> arrays and enables the NPC system for worlds that run the political system
    /// (listed before it, where a fresh world lists it). People keep their IDs, names, homes, factions,
    /// roles and meetings with the player. Their new state cannot come from JSON, so after mapping the
    /// serializer runs <see cref="Simulation.Npcs.NpcGenesis.EnsurePeople"/>, which derives it from the seed
    /// and IDs, once, and invents no memories or past events.
    /// </summary>
    public sealed class MigrationV5ToV6 : ISaveMigration
    {
        public int SourceVersion => 5;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world)) return;
            if (world["locations"] == null) world["locations"] = new JArray();
            if (world["npcRelations"] == null) world["npcRelations"] = new JArray();

            if (!(world["settings"] is JObject settings) || !(settings["enabledSimulationSystems"] is JArray systems)) return;
            JToken politics = null;
            var hasNpcs = false;
            foreach (var id in systems)
            {
                if (id.Type != JTokenType.String) continue;
                if ((string)id == PoliticalSystem.Id) politics = id;
                if ((string)id == Simulation.Npcs.NpcSystem.Id) hasNpcs = true;
            }

            if (politics != null && !hasNpcs) politics.AddBeforeSelf(Simulation.Npcs.NpcSystem.Id);
        }
    }

    /// <summary>
    /// v4 → v5 (Phase 3: factions and politics). Structural only: adds empty <c>relations</c> and
    /// <c>claims</c> arrays and enables the political system for worlds that run the standard
    /// Phase 2 systems. Faction types, presence, leaders, relationships and goals cannot be
    /// derived from JSON alone, so after mapping the serializer runs
    /// <see cref="Generation.PoliticalInitializer"/>: the two existing factions keep their IDs
    /// and names, and the new ones get the IDs a fresh world of that seed gets. No settlement,
    /// resource, route or event is changed.
    /// </summary>
    public sealed class MigrationV4ToV5 : ISaveMigration
    {
        public int SourceVersion => 4;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world)) return;
            if (world["relations"] == null) world["relations"] = new JArray();
            if (world["claims"] == null) world["claims"] = new JArray();

            if (!(world["settings"] is JObject settings) || !(settings["enabledSimulationSystems"] is JArray systems)) return;
            JToken chronicle = null;
            var hasPolitics = false;
            foreach (var id in systems)
            {
                if (id.Type != JTokenType.String) continue;
                if ((string)id == ChronicleSystem.Id) chronicle = id;
                if ((string)id == PoliticalSystem.Id) hasPolitics = true;
            }

            // Listed where a fresh world lists it (the scheduler orders systems itself; this only keeps saves alike).
            if (chronicle != null && !hasPolitics) chronicle.AddBeforeSelf(PoliticalSystem.Id);
        }
    }

    /// <summary>
    /// v3 → v4 (Phase 2 completion): trade routes gain "isSuspended". Every route in an older
    /// save was trading, so the documented default is false (not suspended). Nothing else
    /// changes; resources, routes, shipments and history are kept exactly as saved.
    /// </summary>
    public sealed class MigrationV3ToV4 : ISaveMigration
    {
        public int SourceVersion => 3;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world) || !(world["tradeRoutes"] is JArray routes)) return;
            foreach (var token in routes)
            {
                if (token is JObject route && route["isSuspended"] == null) route["isSuspended"] = false;
            }
        }
    }

    /// <summary>
    /// v2 → v3 (ADR-0009 prerequisites). The JSON layout gains regions, settlement.regionId, the
    /// player and resident fields, but none of them can be derived from JSON alone: they come from
    /// seeded streams. So this step is structural only. After mapping, the serializer runs
    /// <see cref="Generation.WorldPopulationInitializer"/>, which adds the region (assigning each
    /// settlement to the region containing it), the player character on the world map, and named
    /// residents if there are none, with the same IDs a fresh world of that seed gets. Existing
    /// IDs and data are never changed or regenerated.
    /// </summary>
    public sealed class MigrationV2ToV3 : ISaveMigration
    {
        public int SourceVersion => 2;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world)) return;
            if (world["regions"] == null) world["regions"] = new JArray();
        }
    }

    /// <summary>
    /// Phase 1 (v1) → Phase 2 (v2):
    /// - the "ore" resource becomes "iron" (quantities merged if both exist);
    /// - settlement.foodProductionPerDay is dropped (food now comes from farms, fishing, hunting);
    /// - the Phase 1 system list (food, wellbeing) becomes the Phase 2 list;
    /// - economy, trade routes and shipments are left absent: on load the session derives them
    ///   from the regenerated terrain (see IWorldGenerator.InitializeEconomy).
    /// Events keep their text descriptions and gain no structured data.
    /// </summary>
    public sealed class MigrationV1ToV2 : ISaveMigration
    {
        public int SourceVersion => 1;

        public void Migrate(JObject document)
        {
            if (!(document["world"] is JObject world)) return;

            if (world["settlements"] is JArray settlements)
            {
                foreach (var token in settlements)
                {
                    if (!(token is JObject settlement)) continue;
                    settlement.Remove("foodProductionPerDay");
                    if (settlement["resources"] is JArray resources) RenameOre(resources);
                }
            }

            if (world["settings"] is JObject settings && settings["enabledSimulationSystems"] is JArray systems)
            {
                var legacy = new HashSet<string>(StandardSimulationSystems.LegacyIds);
                var usesLegacy = false;
                foreach (var id in systems)
                {
                    if (id.Type == JTokenType.String && legacy.Contains((string)id)) usesLegacy = true;
                }

                if (usesLegacy) settings["enabledSimulationSystems"] = new JArray(StandardSimulationSystems.AllIds);
            }
        }

        private static void RenameOre(JArray resources)
        {
            JObject ore = null;
            JObject iron = null;
            foreach (var token in resources)
            {
                if (!(token is JObject stack)) continue;
                var key = (string)stack["resourceTypeId"];
                if (key == "ore") ore = stack;
                else if (key == "iron") iron = stack;
            }

            if (ore == null) return;
            if (iron == null)
            {
                ore["resourceTypeId"] = "iron";
                return;
            }

            iron["quantity"] = (int)iron["quantity"] + (int)ore["quantity"];
            ore.Remove();
        }
    }
}
