using System;
using System.IO;
using System.Linq;
using Realmkeeper.Application;
using Realmkeeper.Application.Ports;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Composition;
using Realmkeeper.Infrastructure.Generation;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Tests.EditMode.Phase2
{
    /// <summary>Generated worlds and sessions with the production rules (as the game runs them).</summary>
    internal static class Phase2Worlds
    {
        public static readonly WorldId FixedId = new WorldId(new Guid("0b5cf6a2-1d8e-4f3e-9a5b-6c7d8e9f0a2b"));

        public static WorldData Generated(long seed) => Generate(seed).World;

        /// <summary>The world of that seed as generated before Phase 5 (no shops or starting kit): the source of faithful old saves.</summary>
        public static WorldData GeneratedBeforeCommerce(long seed) => Generate(seed, false).World;

        /// <summary>
        /// Phase 10: the world of that seed as generated before Phase 10 (no extra posts filled, no NPC sheets, layouts or
        /// wildlife): what a v11 save holds, and what the deserializer's migrations alone produce. The living-world
        /// additions arrive when a session loads a save (tested by the Phase 10 tests).
        /// </summary>
        public static WorldData GeneratedBeforeLivingWorld(long seed) => Generate(seed, true, false).World;

        public static GeneratedWorld Generate(long seed, bool includeCommerce = true, bool includeLivingWorld = true)
        {
            var settings = new WorldSettings();
            settings.EnabledSimulationSystems.AddRange(StandardSimulationSystems.AllIds);
            return new RegionGenerator(new Xoshiro256RandomFactory()).Generate(new WorldGenerationRequest("Phaseland", seed, settings, FixedId) { IncludeCommerce = includeCommerce, IncludeLivingWorld = includeLivingWorld });
        }

        /// <summary>Rewrites a current save document to the v5 layout: no NPC-engine data, no NPC system.</summary>
        /// <summary>Rebuilds the v10 layout: no spellcasting record; an uncreated character on rules version 7 (Phase 9).</summary>
        /// <summary>
        /// Rebuilds the v16 layout (Phase 14): no party, no companion records, no party system, no items owned by people
        /// (their gear stays by name) and no AI reasons in fight logs.
        /// </summary>
        /// <summary>Rebuilds the v18 layout (Phase 16): no campaigns, no companions' remarks or second stories, no campaign system.</summary>
        public static void StripToV18(Newtonsoft.Json.Linq.JObject doc)
        {
            doc["schemaVersion"] = 18;
            var w = (Newtonsoft.Json.Linq.JObject)doc["world"];
            w.Remove("campaigns");
            foreach (Newtonsoft.Json.Linq.JObject c in (w["companions"] as Newtonsoft.Json.Linq.JArray) ?? new Newtonsoft.Json.Linq.JArray()) { c.Remove("remarks"); c.Remove("storyQuestId"); }
            var systems = w["settings"]?["enabledSimulationSystems"] as Newtonsoft.Json.Linq.JArray;
            systems?.FirstOrDefault(t => (string)t == Realmkeeper.Simulation.Campaigns.CampaignSystem.Id)?.Remove();
        }

        /// <summary>Rebuilds the v17 layout (Phase 15): no route network, journey, groups, world events, atlas fields or Phase 16 systems.</summary>
        public static void StripToV17(Newtonsoft.Json.Linq.JObject doc)
        {
            StripToV18(doc); // Phase 17
            doc["schemaVersion"] = 17;
            var w = (Newtonsoft.Json.Linq.JObject)doc["world"];
            foreach (var field in new[] { "routes", "journey", "groups", "worldEvents", "nextWorldEventNumber" }) w.Remove(field);
            var systems = w["settings"]?["enabledSimulationSystems"] as Newtonsoft.Json.Linq.JArray;
            foreach (var id in new[] { Realmkeeper.Simulation.Atlas.GroupSystem.Id, Realmkeeper.Simulation.Atlas.FactionActivitySystem.Id, Realmkeeper.Simulation.Atlas.WorldEventSystem.Id })
                systems?.FirstOrDefault(t => (string)t == id)?.Remove();
            foreach (Newtonsoft.Json.Linq.JObject r in (w["regions"] as Newtonsoft.Json.Linq.JArray) ?? new Newtonsoft.Json.Linq.JArray())
                foreach (var f in new[] { "atlasX", "atlasZ", "seed", "mountainDirection", "generatorVersion", "kind", "discovery", "neighbours", "landmarks" }) r.Remove(f);
            foreach (Newtonsoft.Json.Linq.JObject t in (w["tradeRoutes"] as Newtonsoft.Json.Linq.JArray) ?? new Newtonsoft.Json.Linq.JArray()) t.Remove("blockedBy");
        }

        public static void StripToV16(Newtonsoft.Json.Linq.JObject doc)
        {
            StripToV17(doc); // Phase 16
            doc["schemaVersion"] = 16;
            var w = (Newtonsoft.Json.Linq.JObject)doc["world"];
            foreach (var field in new[] { "party", "companions" }) w.Remove(field);
            var systems = w["settings"]?["enabledSimulationSystems"] as Newtonsoft.Json.Linq.JArray;
            systems?.FirstOrDefault(t => (string)t == Realmkeeper.Simulation.Party.PartySystem.Id)?.Remove();
            foreach (var o in w.DescendantsAndSelf().OfType<Newtonsoft.Json.Linq.JObject>().ToList())
            {
                if (o["archetypeId"] != null) { o.Remove("ownsItems"); o.Remove("inventory"); o.Remove("equipment"); }
                if (o["log"] is Newtonsoft.Json.Linq.JArray log && o["combatants"] != null)
                    foreach (var l in log.Where(l => (string)l["kind"] == "Reason").ToList()) l.Remove();
            }
        }

        /// <summary>
        /// Rebuilds the v15 layout (Phase 13): no crimes, lasting conditions, fight doors or low cover, bridge states, woken
        /// groups or goods lost on the road — and none of the Phase 14 places, their quests, chests or creatures.
        /// </summary>
        public static void StripToV15(Newtonsoft.Json.Linq.JObject doc)
        {
            StripToV16(doc); // Phase 15
            doc["schemaVersion"] = 15;
            var w = (Newtonsoft.Json.Linq.JObject)doc["world"];
            foreach (var field in new[] { "crimes", "nextCrimeNumber" }) w.Remove(field);
            var newer = new System.Collections.Generic.HashSet<string>(Realmkeeper.Domain.Places.Phase14Places.All.Select(d => "place:" + d));
            if (w["places"] is Newtonsoft.Json.Linq.JArray places)
            {
                foreach (var p in places.Where(p => newer.Contains((string)p["placeId"])).ToList()) p.Remove();
                foreach (Newtonsoft.Json.Linq.JObject p in places)
                {
                    p.Remove("bridges");
                    foreach (Newtonsoft.Json.Linq.JObject c in (p["layout"]?["connections"] as Newtonsoft.Json.Linq.JArray) ?? new Newtonsoft.Json.Linq.JArray()) c.Remove("bridge");
                    foreach (var g in ((p["groups"] as Newtonsoft.Json.Linq.JObject)?.Properties() ?? Enumerable.Empty<Newtonsoft.Json.Linq.JProperty>()).ToList())
                        (g.Value as Newtonsoft.Json.Linq.JObject)?.Remove("woken");
                }
            }

            var quests = new[] { "founders_register", "fane_lantern", "road_tolls", "old_culverts" };
            if (w["quests"] is Newtonsoft.Json.Linq.JArray qs)
                foreach (var q in qs.Where(q => quests.Contains((string)q["definitionId"])).ToList()) q.Remove();
            if (w["containers"] is Newtonsoft.Json.Linq.JArray boxes)
                foreach (var b in boxes.Where(b => newer.Any(id => ((string)b["key"]).StartsWith("box:" + id + ":"))).ToList()) b.Remove();
            if (w["creatures"] is Newtonsoft.Json.Linq.JArray creatures)
                foreach (var c in creatures.Where(c => newer.Any(id => ((string)c["originId"] ?? string.Empty).StartsWith("place:" + id + ":"))).ToList()) c.Remove();

            // Lasting conditions live on character sheets (a sheet has a classId); fight conditions stay, without their lasting key.
            foreach (var o in w.DescendantsAndSelf().OfType<Newtonsoft.Json.Linq.JObject>().ToList())
            {
                if (o["classId"] != null && o["conditions"] is Newtonsoft.Json.Linq.JArray) o.Remove("conditions");
                o.Remove("lastingKey");
                o.Remove("lostOnRoad");
                if (o["blocked"] != null && o["combatants"] != null) { o.Remove("doors"); o.Remove("lowCover"); }
            }
        }

        /// <summary>
        /// Rebuilds the v14 layout (Phase 12): no places, escorts, place containers, garrison people, the rescue quest or
        /// place references on the player and containers.
        /// </summary>
        public static void StripToV14(Newtonsoft.Json.Linq.JObject doc)
        {
            StripToV15(doc); // Phase 14
            doc["schemaVersion"] = 14;
            var w = (Newtonsoft.Json.Linq.JObject)doc["world"];
            var garrison = new System.Collections.Generic.HashSet<string>();
            foreach (Newtonsoft.Json.Linq.JObject p in (w["places"] as Newtonsoft.Json.Linq.JArray) ?? new Newtonsoft.Json.Linq.JArray())
                foreach (Newtonsoft.Json.Linq.JObject g in (p["layout"]?["groups"] as Newtonsoft.Json.Linq.JArray) ?? new Newtonsoft.Json.Linq.JArray())
                    foreach (Newtonsoft.Json.Linq.JObject m in (g["members"] as Newtonsoft.Json.Linq.JArray) ?? new Newtonsoft.Json.Linq.JArray())
                        if (m["npcId"] != null && m["npcId"].Type == Newtonsoft.Json.Linq.JTokenType.String) garrison.Add((string)m["npcId"]);
            foreach (var field in new[] { "places", "escorts" }) w.Remove(field);
            if (w["npcs"] is Newtonsoft.Json.Linq.JArray npcs)
                foreach (var n in npcs.Where(n => garrison.Contains((string)n["npcId"])).ToList()) n.Remove();
            if (w["quests"] is Newtonsoft.Json.Linq.JArray quests)
                foreach (var q in quests.Where(q => (string)q["definitionId"] == "lost_below").ToList()) q.Remove();
            bool Mentions(Newtonsoft.Json.Linq.JToken t) => (t is Newtonsoft.Json.Linq.JContainer jc ? jc.DescendantsAndSelf() : new[] { t }).OfType<Newtonsoft.Json.Linq.JValue>().Any(v => v.Type == Newtonsoft.Json.Linq.JTokenType.String && garrison.Contains((string)v));
            if (w["npcRelations"] is Newtonsoft.Json.Linq.JArray rels)
                foreach (var r in rels.Where(Mentions).ToList()) r.Remove();
            if (w["historicalEvents"] is Newtonsoft.Json.Linq.JArray events)
                foreach (var e in events.Where(Mentions).ToList()) e.Remove();
            if (w["containers"] is Newtonsoft.Json.Linq.JArray boxes)
            {
                foreach (var b in boxes.Where(b => ((string)b["key"]).StartsWith("box:")).ToList()) b.Remove();
                foreach (Newtonsoft.Json.Linq.JObject b in boxes) { b.Remove("placeId"); b.Remove("featureId"); }
            }

            if (w["player"] is Newtonsoft.Json.Linq.JObject player) foreach (var field in new[] { "placeId", "placeFloor", "placeRoom" }) player.Remove(field);
        }

        /// <summary>
        /// Rebuilds the v13 layout (Phase 11): no quest system, quests, conversation, player social record, content
        /// settings, or further regard dimensions.
        /// </summary>
        public static void StripToV13(Newtonsoft.Json.Linq.JObject doc)
        {
            StripToV14(doc); // Phase 13
            doc["schemaVersion"] = 13;
            var w = (Newtonsoft.Json.Linq.JObject)doc["world"];
            var systems = w["settings"]?["enabledSimulationSystems"] as Newtonsoft.Json.Linq.JArray;
            systems?.FirstOrDefault(t => (string)t == Realmkeeper.Simulation.Quests.QuestSystem.Id)?.Remove();
            (w["settings"] as Newtonsoft.Json.Linq.JObject)?.Remove("content");
            foreach (var field in new[] { "quests", "conversation" }) w.Remove(field);
            (w["player"] as Newtonsoft.Json.Linq.JObject)?.Remove("social");
            foreach (Newtonsoft.Json.Linq.JObject n in (w["npcs"] as Newtonsoft.Json.Linq.JArray) ?? new Newtonsoft.Json.Linq.JArray())
                if (n["playerRegard"] is Newtonsoft.Json.Linq.JObject r)
                    foreach (var field in new[] { "trust", "respect", "fear", "gratitude", "hostility" }) r.Remove(field);
        }

        /// <summary>
        /// Rebuilds the v12 layout (Phase 10): no gathering system, containers, work, gathering sites, catalog version or
        /// shop goods, and item stacks with only their ID, item and quantity.
        /// </summary>
        public static void StripToV12(Newtonsoft.Json.Linq.JObject doc)
        {
            StripToV13(doc); // Phase 12
            doc["schemaVersion"] = 12;
            var w = (Newtonsoft.Json.Linq.JObject)doc["world"];
            var systems = w["settings"]?["enabledSimulationSystems"] as Newtonsoft.Json.Linq.JArray;
            systems?.FirstOrDefault(t => (string)t == Realmkeeper.Simulation.Items.GatheringSystem.Id)?.Remove();
            foreach (var field in new[] { "containers", "workJobs", "gatheringSites", "itemCatalogVersion" }) w.Remove(field);
            foreach (Newtonsoft.Json.Linq.JObject s in (w["shops"] as Newtonsoft.Json.Linq.JArray) ?? new Newtonsoft.Json.Linq.JArray()) s.Remove("goods");
            void Plain(Newtonsoft.Json.Linq.JObject stack)
            {
                foreach (var field in new[] { "condition", "quality", "charges", "attuned", "bound", "provenance", "acquiredAt" }) stack.Remove(field);
            }

            if (w["player"] is Newtonsoft.Json.Linq.JObject p)
            {
                foreach (Newtonsoft.Json.Linq.JObject s in (p["inventory"] as Newtonsoft.Json.Linq.JArray) ?? new Newtonsoft.Json.Linq.JArray()) Plain(s);
                if (p["equipment"] is Newtonsoft.Json.Linq.JObject eq) foreach (var pair in eq) Plain((Newtonsoft.Json.Linq.JObject)pair.Value);
            }
        }

        /// <summary>
        /// Rebuilds the v11 layout (Phase 9): no fauna system, no creature populations, creatures or herds, no settlement
        /// layouts or identities, no NPC character records, no Phase 10 combatant fields. Expects a world generated
        /// before the living world (<see cref="GeneratedBeforeLivingWorld"/>), so no Phase 10 person exists to remove.
        /// </summary>
        public static void StripToV11(Newtonsoft.Json.Linq.JObject doc)
        {
            StripToV12(doc); // Phase 11
            doc["schemaVersion"] = 11;
            var w = (Newtonsoft.Json.Linq.JObject)doc["world"];
            var systems = w["settings"]?["enabledSimulationSystems"] as Newtonsoft.Json.Linq.JArray;
            systems?.FirstOrDefault(t => (string)t == Realmkeeper.Simulation.Fauna.FaunaSystem.Id)?.Remove();
            foreach (var field in new[] { "creaturePopulations", "creatures", "livestock", "settlementLayouts" }) w.Remove(field);
            foreach (Newtonsoft.Json.Linq.JObject n in (w["npcs"] as Newtonsoft.Json.Linq.JArray) ?? new Newtonsoft.Json.Linq.JArray()) n.Remove("character");
            foreach (var e in (w["encounters"] as Newtonsoft.Json.Linq.JArray) ?? new Newtonsoft.Json.Linq.JArray())
            {
                foreach (var field in new[] { "pendingReaction", "objective", "origin", "resolved" }) ((Newtonsoft.Json.Linq.JObject)e).Remove(field);
                foreach (Newtonsoft.Json.Linq.JObject c in (Newtonsoft.Json.Linq.JArray)e["combatants"])
                    {
                        foreach (var field in new[] { "anchor", "creatureInstanceId" }) c.Remove(field);
                        if (c["turn"] is Newtonsoft.Json.Linq.JObject turn) foreach (var field in new[] { "hasStart", "startX", "startY" }) turn.Remove(field);
                    }
            }
        }

        public static void StripToV10(Newtonsoft.Json.Linq.JObject doc)
        {
            StripToV11(doc); // Phase 10
            doc["schemaVersion"] = 10;
            if (doc["world"]["player"]?["rpg"] is Newtonsoft.Json.Linq.JObject rpg)
            {
                rpg.Remove("spellcasting");
                if ((int?)rpg["rulesVersion"] == 8) rpg["rulesVersion"] = 7;
            }

            foreach (var e in (doc["world"]["encounters"] as Newtonsoft.Json.Linq.JArray) ?? new Newtonsoft.Json.Linq.JArray())
            {
                ((Newtonsoft.Json.Linq.JObject)e).Remove("effects");
                foreach (Newtonsoft.Json.Linq.JObject c in (Newtonsoft.Json.Linq.JArray)e["combatants"])
                    foreach (var field in new[] { "resources", "concentration", "formCreatureId" }) c.Remove(field);
            }
        }

        /// <summary>Rebuilds the v9 layout: no encounters, structures or dying state (Phase 8).</summary>
        public static void StripToV9(Newtonsoft.Json.Linq.JObject doc)
        {
            StripToV10(doc);
            doc["schemaVersion"] = 9;
            var w = (Newtonsoft.Json.Linq.JObject)doc["world"];
            w.Remove("encounters");
            w.Remove("structures");
            if (w["player"]?["rpg"] is Newtonsoft.Json.Linq.JObject rpg)
                foreach (var field in new[] { "deathSaveSuccesses", "deathSaveFailures", "isStable", "isDead" })
                    rpg.Remove(field);
        }

        /// <summary>Rebuilds the v7 layout: no character record (Phase 6).</summary>
        public static void StripToV7(Newtonsoft.Json.Linq.JObject doc)
        {
            StripToV9(doc);
            doc["schemaVersion"] = 7;
            if (doc["world"]["player"] is Newtonsoft.Json.Linq.JObject p) p.Remove("rpg");
        }

        /// <summary>
        /// An old save's player loads as Legacy (not yet created), where a fresh world's is Pending; otherwise they
        /// must be identical. Checks that, then marks the loaded player Pending so whole worlds can be compared.
        /// </summary>
        public static void ExpectLegacyThenTreatAsFresh(WorldData loaded)
        {
            NUnit.Framework.Assert.That(loaded.Player.Rpg.State, NUnit.Framework.Is.EqualTo(Realmkeeper.Domain.Rpg.CreationState.Legacy), "An old save's player is Legacy.");
            loaded.Player.Rpg.State = Realmkeeper.Domain.Rpg.CreationState.Pending;
        }

        /// <summary>Rebuilds the v6 layout: no clock minute, shops, trades, belongings or regard (Phase 5).</summary>
        public static void StripToV6(Newtonsoft.Json.Linq.JObject doc)
        {
            StripToV7(doc);
            doc["schemaVersion"] = 6;
            var w = (Newtonsoft.Json.Linq.JObject)doc["world"];
            w.Remove("minuteOfDay");
            w.Remove("shops");
            w.Remove("transactions");
            if (w["player"] is Newtonsoft.Json.Linq.JObject p)
                foreach (var field in new[] { "species", "age", "background", "description", "appearance", "purse", "fed", "isOutfitted", "inventory", "equipment" })
                    p.Remove(field);
            foreach (Newtonsoft.Json.Linq.JObject n in (Newtonsoft.Json.Linq.JArray)w["npcs"]) n.Remove("playerRegard");
            var systems = (Newtonsoft.Json.Linq.JArray)w["settings"]["enabledSimulationSystems"];
            foreach (var t in new System.Collections.Generic.List<Newtonsoft.Json.Linq.JToken>(systems))
                if ((string)t == Realmkeeper.Simulation.Commerce.ShopSystem.Id) t.Remove();
        }

        public static void StripToV5(Newtonsoft.Json.Linq.JObject doc)
        {
            StripToV6(doc);
            doc["schemaVersion"] = 5;
            var w = (Newtonsoft.Json.Linq.JObject)doc["world"];
            w.Remove("locations");
            w.Remove("npcRelations");
            foreach (Newtonsoft.Json.Linq.JObject n in (Newtonsoft.Json.Linq.JArray)w["npcs"])
            {
                foreach (var field in new[] { "sex", "birthDate", "tier", "personality", "skills", "condition", "residenceId", "workplaceId",
                             "currentLocationId", "schedule", "goals", "memories", "knowledge", "isDeceased", "diedOn", "lastUpdatedOn" })
                    n.Remove(field);
            }

            foreach (Newtonsoft.Json.Linq.JObject e in (Newtonsoft.Json.Linq.JArray)w["historicalEvents"]) e.Remove("npcIds");
            var systems = (Newtonsoft.Json.Linq.JArray)w["settings"]["enabledSimulationSystems"];
            foreach (var t in new System.Collections.Generic.List<Newtonsoft.Json.Linq.JToken>(systems))
                if ((string)t == Realmkeeper.Simulation.Npcs.NpcSystem.Id) t.Remove();
        }

        public static string TempDirectory() => Path.Combine(Path.GetTempPath(), "realmkeeper-phase2-" + Guid.NewGuid().ToString("N"));

        public static WorldSession Session(string directory, long seed = 123456789)
        {
            var session = RealmkeeperComposition.CreateSession(directory);
            var result = session.Execute(new CreateWorldCommand("Phaseland", seed));
            if (!result.IsSuccess) throw new InvalidOperationException(result.Message);
            return session;
        }
    }
}
