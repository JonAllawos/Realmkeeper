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
        public StartWorldEncounterCommand(EncounterPurpose purpose) { Purpose = purpose; }
        public EncounterPurpose Purpose { get; }
    }

    public static class WorldEncounters
    {
        /// <summary>The plan an outing would produce now (the same world state gives the same plan), or the reason there is none.</summary>
        public static EncounterPlan Preview(WorldData world, Domain.Geography.RegionMap map, EncounterPurpose purpose, out string problem)
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
            return EncounterGenerator.Plan(world, map, new EncounterRequest { Near = p.SettlementId, Purpose = purpose, Attempt = attempt });
        }

        internal static CommandResult Start(StartWorldEncounterCommand command, CommandContext context)
        {
            if (!context.HasWorld) return CommandResult.InvalidState("There is no active world.");
            var plan = Preview(context.Committed, context.Map, command.Purpose, out var problem);
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

            // Loot from the slain, for the winners to take (seeded by the encounter: the same fight leaves the same things).
            if (won && world.Player != null && !world.Player.Rpg.IsDead)
            {
                var coin = 0;
                var found = new List<string>();
                var remains = new List<string>();
                var n = 0;
                foreach (var c in dead)
                {
                    var def = CreatureCatalog.Find(c.CreatureId);
                    var loot = def?.Loot ?? LootProfile.None;
                    ulong Roll() => SeedMixer.Mix(SeedMixer.Combine(e.Seed, "loot:" + c.CombatantId + ":" + (n++).ToString(CultureInfo.InvariantCulture)));
                    if (loot.CoinMax > 0) coin += loot.CoinMin + (int)(Roll() % (ulong)(loot.CoinMax - loot.CoinMin + 1));
                    foreach (var l in loot.Items)
                    {
                        if ((int)(Roll() % 100) >= l.ChancePercent) continue;
                        var qty = l.Min + (int)(Roll() % (ulong)(l.Max - l.Min + 1));
                        AddItem(context, world.Player, l.ItemId, qty);
                        found.Add(qty > 1 ? $"{qty} × {ItemCatalog.Find(l.ItemId)?.Name}" : ItemCatalog.Find(l.ItemId)?.Name);
                    }

                    if (!string.IsNullOrEmpty(loot.Remains)) remains.Add(loot.Remains);
                }

                if (coin > 0)
                {
                    world.Player.Purse += coin;
                    lines.Add($"{coin} coin");
                }

                if (found.Count > 0) lines.Add("found " + string.Join(", ", found));
                if (remains.Count > 0) lines.Add("left: " + string.Join("; ", Distinct(remains)));
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

        private static void AddItem(CommandContext context, PlayerCharacterData p, string itemId, int quantity)
        {
            var def = ItemCatalog.Find(itemId);
            if (def == null || quantity < 1) return;
            var max = def.Stackable ? Math.Max(1, def.MaxStack) : 1;
            var left = quantity;
            foreach (var s in p.Inventory)
            {
                if (left == 0) break;
                if (s.ItemId != def.Id || s.Quantity >= max) continue;
                var add = Math.Min(left, max - s.Quantity);
                s.Quantity += add;
                left -= add;
            }

            while (left > 0)
            {
                var add = Math.Min(left, max);
                p.Inventory.Add(new ItemStackData { StackId = new ItemStackId(context.NextGuid("item-stack")), ItemId = def.Id, Quantity = add });
                left -= add;
            }
        }
    }

    public sealed partial class WorldSession
    {
        /// <summary>Phase 10: what an outing would find now (no change), or null with the reason.</summary>
        public EncounterPlan PreviewWorldEncounter(EncounterPurpose purpose, out string problem) => WorldEncounters.Preview(_world, Map, purpose, out problem);
    }
}
