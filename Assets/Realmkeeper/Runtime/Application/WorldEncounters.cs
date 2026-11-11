using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Simulation.Encounters;
using Realmkeeper.Simulation.Npcs;

namespace Realmkeeper.Application
{
    /// <summary>
    /// Phase 10: go looking for trouble around the current settlement — its outskirts, the road out, or the farms
    /// (ADR-0046). The world decides who turns up; the individuals are drawn from their populations and keep their
    /// identity; the consequences are applied once when the fight ends.
    /// </summary>
    public sealed class StartWorldEncounterCommand : ICommand
    {
        public StartWorldEncounterCommand(EncounterPurpose purpose, string seek = null)
        {
            Purpose = purpose;
            Seek = seek;
        }

        public EncounterPurpose Purpose { get; }

        /// <summary>Phase 12: a creature sought (a hunting quest's quarry), or null.</summary>
        public string Seek { get; }
    }

    public static class WorldEncounters
    {
        /// <summary>The plan an outing would produce now (the same world state gives the same plan), or the reason there is none.</summary>
        public static EncounterPlan Preview(WorldData world, Domain.Geography.RegionMap map, EncounterPurpose purpose, out string problem, string seek = null)
        {
            problem = null;
            var p = world?.Player;
            if (p?.Rpg == null || p.Rpg.State != CreationState.Complete) { problem = "Create your character first."; return null; }
            if (p.Rpg.IsDead) { problem = $"{p.Name} is dead."; return null; }
            if (!p.IsInSettlement) { problem = "Outings start from a settlement."; return null; }
            if (world.ActiveEncounter != null) { problem = "A fight is already going on."; return null; }
            if (map == null) { problem = "The region's geography is not loaded."; return null; }
            var attempt = 0;
            foreach (var e in world.Encounters) if (e.Origin != null) attempt++;
            return EncounterGenerator.Plan(world, map, new EncounterRequest { Near = p.SettlementId, Purpose = purpose, Attempt = attempt, Seek = seek });
        }

        internal static CommandResult Start(StartWorldEncounterCommand command, CommandContext context)
        {
            if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
            var plan = Preview(context.Committed, context.Map, command.Purpose, out var problem, command.Seek);
            if (plan == null) return CommandResult.InvalidState(problem);
            if (!plan.IsValid) return CommandResult.Invalid("Nothing comes of the outing: " + string.Join(" ", plan.Problems), plan.Problems);

            // Draw the individuals: each is a new member of its population, with its own lasting record.
            var world = context.Edit();
            foreach (var pair in plan.Draws)
            {
                CreaturePopulationData pop = null;
                foreach (var p in world.CreaturePopulations) if (p.PopulationId == pair.Value) pop = p;
                var participant = plan.Setup.Participants[pair.Key];
                var def = CreatureCatalog.Find(participant.CreatureId);
                pop.Drawn++;
                var instance = new CreatureInstanceData
                {
                    InstanceId = pop.PopulationId + "#" + pop.Drawn.ToString(CultureInfo.InvariantCulture), CreatureId = def.Id, OriginId = pop.PopulationId,
                    Name = def.Name + " " + pop.Drawn.ToString(CultureInfo.InvariantCulture), State = CreatureInstanceState.Alive, CurrentHp = def.MaxHp,
                    FirstSeenOn = world.CurrentSimulationDate,
                };
                world.Creatures.Add(instance);
                participant.CreatureInstanceId = instance.InstanceId;
                participant.Name = instance.Name;
            }

            TrimCreatures(world);
            return CombatCommandHandlers.StartGenerated(context, plan.Setup, plan.Description);
        }

        /// <summary>Keeps the record of creatures bounded: the oldest dead are forgotten first (the living never are).</summary>
        private static void TrimCreatures(WorldData world)
        {
            for (var i = 0; world.Creatures.Count > FaunaBounds.MaxInstances && i < world.Creatures.Count;)
            {
                if (world.Creatures[i].State != CreatureInstanceState.Alive) world.Creatures.RemoveAt(i);
                else i++;
            }
        }

        /// <summary>
        /// The aftermath of a generated encounter, applied once (the origin's Resolved flag and the experience ledger's
        /// key both guard it): creatures' fates (the dead leave their population, the fled return to it), the objective,
        /// experience for the player and for named people with sheets on their side, loot from the fallen (seeded by the
        /// encounter), a memory for a protected farmer, and one chronicle line.
        /// </summary>
        public static void Resolve(CommandContext context, WorldData world, EncounterData e)
        {
            var o = e.Origin;
            if (o == null || o.Resolved || e.IsActive) return;
            o.Resolved = true;
            var playerSide = "player";
            var won = e.WinningSide == playerSide;
            var dead = new List<CombatantData>();
            var defeated = new List<CombatantData>();
            var lines = new List<string>();

            foreach (var c in e.Combatants)
            {
                if (c.CreatureInstanceId == null) continue;
                CreatureInstanceData instance = null;
                foreach (var x in world.Creatures) if (x.InstanceId == c.CreatureInstanceId) instance = x;
                if (instance == null || instance.State == CreatureInstanceState.Dead) continue;
                instance.LastEncounterId = e.EncounterId;
                if (c.State == VitalState.Dead)
                {
                    instance.State = CreatureInstanceState.Dead;
                    instance.CurrentHp = 0;
                    instance.DiedOn = world.CurrentSimulationDate;
                    foreach (var p in world.CreaturePopulations)
                        if (p.PopulationId == instance.OriginId)
                        {
                            p.Count = Math.Max(0, p.Count - 1);
                            p.Deaths++;
                        }

                    dead.Add(c);
                    defeated.Add(c);
                }
                else
                {
                    instance.State = c.State == VitalState.Fled ? CreatureInstanceState.Fled : CreatureInstanceState.Alive;
                    instance.CurrentHp = Math.Max(0, c.CurrentHp);
                    if (c.State == VitalState.Fled && won) defeated.Add(c);
                }
            }

            // The objective.
            var protectedOk = true;
            if (o.Objective == EncounterObjective.Protect)
                foreach (var c in e.Combatants)
                    if (c.Kind == CombatantKind.Npc && c.SourceId == o.ProtectedNpcId.ToString() && c.State == VitalState.Dead) protectedOk = false;
            o.ObjectiveMet = o.Objective == EncounterObjective.Escape ? e.Outcome == EncounterOutcome.Withdrawn || won
                : o.Objective == EncounterObjective.Hunt ? dead.Count > 0
                : o.Objective == EncounterObjective.Protect ? won && protectedOk
                : won;
            lines.Add(o.ObjectiveMet ? "objective met" : "objective failed");
            if (dead.Count > 0) lines.Add(dead.Count + " slain");

            // Phase 12 (ADR-0057): what the fight did, for quests — each fallen foe, and whether the player came back on their feet.
            var playerC = e.Combatants.Find(c => c.Kind == CombatantKind.Player);
            var kinds = new HashSet<string>();
            foreach (var c in dead)
            {
                if (c.CreatureId != null) context.Quest(new Realmkeeper.Simulation.Quests.QuestEvent { Kind = Realmkeeper.Simulation.Quests.QuestEventKind.Defeated, Subject = c.CreatureId, Settlement = o.NearSettlementId });
                if (c.CreatureId != null) kinds.Add(c.CreatureId);
            }

            foreach (var c in e.Combatants)
                if (c.Kind == CombatantKind.Npc && c.Side != playerSide && c.State == VitalState.Dead)
                    context.Quest(new Realmkeeper.Simulation.Quests.QuestEvent { Kind = Realmkeeper.Simulation.Quests.QuestEventKind.DefeatedNamed, Subject = c.SourceId, Settlement = o.NearSettlementId });
            foreach (var c in e.Combatants) if (c.CreatureId != null) kinds.Add(c.CreatureId);
            var onFeet = won && playerC != null && playerC.State == VitalState.Conscious;
            foreach (var k in kinds)
                context.Quest(new Realmkeeper.Simulation.Quests.QuestEvent { Kind = onFeet ? Realmkeeper.Simulation.Quests.QuestEventKind.SurvivedEncounter : Realmkeeper.Simulation.Quests.QuestEventKind.LostEncounter, Subject = k, Settlement = o.NearSettlementId });

            // Experience: for those defeated (slain, or routed by the winners), to the player and to sheet-holders who fought with them.
            var xp = 0;
            foreach (var c in defeated) xp += CreatureCatalog.Find(c.CreatureId)?.Xp ?? 0;
            var key = "encounter:" + e.EncounterId;
            if (xp > 0)
            {
                var player = world.Player?.Rpg;
                if (player != null && !player.IsDead && !HasKey(player, key))
                {
                    player.ExperienceLog.Add(new ExperienceAward { Key = key, Source = ExperienceSource.Encounter, SourceId = e.EncounterId.ToString(), Amount = xp, Date = world.CurrentSimulationDate, Reason = e.Name });
                    player.Experience += xp;
                    lines.Add($"{xp} experience");
                }

                foreach (var c in e.Combatants)
                {
                    if (c.Kind != CombatantKind.Npc || c.Side != playerSide || c.State == VitalState.Dead || !Guid.TryParse(c.SourceId, out var g)) continue;
                    var sheet = world.FindNpc(new NpcId(g))?.Character?.Sheet;
                    if (sheet == null || HasKey(sheet, key)) continue;
                    sheet.ExperienceLog.Add(new ExperienceAward { Key = key, Source = ExperienceSource.Encounter, SourceId = e.EncounterId.ToString(), Amount = xp, Date = world.CurrentSimulationDate, Reason = e.Name });
                    sheet.Experience += xp;
                }
            }

            // Loot from the slain (Phase 11, ADR-0053): not dropped into the pack but left in one container keyed by the
            // encounter, filled once, from what each creature carried (its weapons, as used) and its loot table, seeded by
            // the encounter and the combatant (the same fight leaves the same things). The player takes what they want.
            if (won && world.Player != null && !world.Player.Rpg.IsDead)
            {
                var pile = FillLoot(context, world, e, dead);
                if (pile != null)
                {
                    var count = 0;
                    foreach (var s in pile.Items) count += s.Quantity;
                    lines.Add($"left behind: {count} item{(count == 1 ? string.Empty : "s")}{(pile.Coin > 0 ? $" and {pile.Coin} coin" : string.Empty)}");
                }

                var remains = new List<string>();
                foreach (var c in dead)
                {
                    var r = CreatureCatalog.Find(c.CreatureId)?.Loot?.Remains;
                    if (!string.IsNullOrEmpty(r)) remains.Add(r);
                }

                if (remains.Count > 0) lines.Add("remains: " + string.Join("; ", Distinct(remains)));
            }

            // The farmer remembers who stood by them.
            if (o.Objective == EncounterObjective.Protect && o.ObjectiveMet)
            {
                var farmer = world.FindNpc(o.ProtectedNpcId);
                if (farmer != null && !farmer.IsDeceased)
                {
                    NpcMind.Remember(farmer, new NpcMemoryData
                    {
                        MemoryId = new MemoryId(context.NextGuid("memory")), Date = world.CurrentSimulationDate, Category = MemoryCategory.Personal,
                        Subject = $"{world.Player.Name} stood with me when {e.Name.Split('—')[0].Trim().ToLowerInvariant()} came", SettlementId = farmer.HomeSettlementId,
                        Salience = 70, Confidence = 100, Source = MemorySource.Experienced,
                    });
                    farmer.PlayerRegard.Affinity += 10;
                }
            }

            o.Result = string.Join("; ", lines) + ".";
            context.ReportEvent(HistoricalEventType.EncounterResolved, o.ObjectiveMet ? EventSeverity.Positive : EventSeverity.Warning, EventImportance.Minor,
                o.NearSettlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { o.NearSettlementId },
                new Dictionary<string, string> { ["source"] = "combat", ["name"] = e.Name.Split('—')[0].Trim(), ["summary"] = o.Result, ["xp"] = xp.ToString(CultureInfo.InvariantCulture), ["deaths"] = dead.Count.ToString(CultureInfo.InvariantCulture) });
        }

        public static string LootKey(EncounterId id) => "loot:encounter:" + id;

        /// <summary>
        /// Fills the encounter's loot container once (ADR-0053): returns it, or null when nothing was left. Called again
        /// for the same encounter it changes nothing (the key is stable and an existing container is kept as it is).
        /// </summary>
        public static ContainerData FillLoot(CommandContext context, WorldData world, EncounterData e, List<CombatantData> dead)
        {
            var key = LootKey(e.EncounterId);
            var existing = world.FindContainer(key);
            if (existing != null) return existing;
            var pile = new ContainerData
            {
                Key = key, Kind = ContainerKind.Loot, Name = "What the fight left — " + e.Name.Split('—')[0].Trim(), PlayerAccess = true, AcceptsDeposits = false,
                SourceKey = "encounter:" + e.EncounterId, CreatedAt = SpellCommandHandlers.Now(world),
            };
            var ctx = new LootContext { MaxRarity = ItemRarity.Uncommon };
            foreach (var c in dead)
            {
                var def = CreatureCatalog.Find(c.CreatureId);
                if (def == null) continue;
                var seed = SeedMixer.Combine(e.Seed, "loot:" + c.CombatantId);
                var results = new List<LootResult>();

                // What it carried: its weapons, as used (falling back to the bestiary's own line for creatures without one).
                if (LootTables.Carried.TryGetValue(def.Id, out var carried)) results.Add(LootTables.RollLines(carried, ctx, SeedMixer.Combine(seed, "carried")));
                else if (def.Loot != null)
                {
                    var lines = new List<LootTableEntry>();
                    foreach (var l in def.Loot.Items) lines.Add(new LootTableEntry { ItemId = l.ItemId, Min = l.Min, Max = l.Max, ChancePercent = l.ChancePercent, Condition = ItemCondition.Worn });
                    results.Add(LootTables.RollLines(lines, ctx, SeedMixer.Combine(seed, "carried")));
                }

                // Its purse or what a hunter takes from it; the bestiary's coin when it has no table.
                if (LootTables.ForCreature.TryGetValue(def.Id, out var table)) results.Add(LootTables.Roll(table, ctx, SeedMixer.Combine(seed, "table")));
                else if (def.Loot != null && def.Loot.CoinMax > 0)
                    pile.Coin += def.Loot.CoinMin + (int)(SeedMixer.Mix(SeedMixer.Combine(seed, "coin")) % (ulong)(def.Loot.CoinMax - def.Loot.CoinMin + 1));

                foreach (var r in results)
                {
                    pile.Coin += r.Coin;
                    foreach (var d in r.Items)
                    {
                        var item = ItemCatalog.Find(d.ItemId);
                        if (item == null) continue;
                        var s = ItemStacks.Create(ItemCommandHandlers.NewStackId(context), item, d.Quantity, "Taken from " + Article(def.Name), -1);
                        s.Condition = d.Condition;
                        ItemCommandHandlers.Merge(pile.Items, s);
                    }
                }
            }

            if (pile.IsEmpty) return null;
            world.Containers.Add(pile);
            while (world.Containers.Count > MaxLootPiles + 64)
            {
                // The oldest emptied or long-left loot piles are cleared away (never chests or caches).
                var oldest = world.Containers.FindIndex(x => x.Kind == ContainerKind.Loot);
                if (oldest < 0 || world.Containers[oldest] == pile) break;
                world.Containers.RemoveAt(oldest);
            }

            return pile;
        }

        /// <summary>Loot piles kept (beyond the settlements' own containers); the oldest are cleared first.</summary>
        public const int MaxLootPiles = 30;

        private static string Article(string name) => (name.Length > 0 && "AEIOUaeiou".IndexOf(name[0]) >= 0 ? "an " : "a ") + name.ToLowerInvariant();


        private static IEnumerable<string> Distinct(List<string> list)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var x in list) if (seen.Add(x)) yield return x;
        }

        private static bool HasKey(CharacterSheetData s, string key)
        {
            foreach (var a in s.ExperienceLog) if (a.Key == key) return true;
            return false;
        }

    }

    public sealed partial class WorldSession
    {
        /// <summary>Phase 10: what an outing would find now (no change), or null with the reason.</summary>
        public EncounterPlan PreviewWorldEncounter(EncounterPurpose purpose, out string problem) => WorldEncounters.Preview(_world, Map, purpose, out problem);
    }
}
