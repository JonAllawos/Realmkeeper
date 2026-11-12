using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>
    /// Phase 10: gives a lightweight named person a full character sheet (ADR-0043), keeping their identity, home,
    /// schedule, relationships, memories and gear. The class is the archetype's, its promotion class, or the one given.
    /// </summary>
    public sealed class PromoteNpcCommand : ICommand
    {
        public PromoteNpcCommand(NpcId npcId, string classId = null)
        {
            NpcId = npcId;
            ClassId = classId;
        }

        public NpcId NpcId { get; }
        public string ClassId { get; }
    }

    /// <summary>Phase 10: experience for a named person with a sheet, once per key (the same ledger as the player's).</summary>
    public sealed class AwardNpcExperienceCommand : ICommand
    {
        public AwardNpcExperienceCommand(NpcId npcId, int amount, string key, ExperienceSource source, string reason)
        {
            NpcId = npcId;
            Amount = amount;
            Key = key;
            Source = source;
            Reason = reason;
        }

        public NpcId NpcId { get; }
        public int Amount { get; }
        public string Key { get; }
        public ExperienceSource Source { get; }
        public string Reason { get; }
    }

    /// <summary>Phase 10: a named person takes every level their experience allows, through the shared advancement rules.</summary>
    public sealed class AdvanceNpcCommand : ICommand
    {
        public AdvanceNpcCommand(NpcId npcId) { NpcId = npcId; }
        public NpcId NpcId { get; }
    }

    /// <summary>The character view of a named person (Phase 10): what the inspector shows.</summary>
    public sealed class NpcCharacterView
    {
        public NpcId NpcId { get; set; }
        public string Name { get; set; }
        public string Occupation { get; set; }
        public string Archetype { get; set; }
        public NpcTier Tier { get; set; }
        public bool HasSheet { get; set; }
        public string Species { get; set; }
        public string Background { get; set; }
        public string ClassName { get; set; }
        public string Subclass { get; set; }
        public int Level { get; set; }
        public int Experience { get; set; }
        public int Hp { get; set; }
        public int MaxHp { get; set; }
        public int ArmorClass { get; set; }
        public string ArmorClassText { get; set; }
        public int Speed { get; set; }
        public int ProficiencyBonus { get; set; }
        public int[] Scores { get; set; } = new int[6];
        public List<string> Saves { get; } = new List<string>();
        public List<string> Skills { get; } = new List<string>();
        public List<string> Equipment { get; } = new List<string>();
        public List<string> Features { get; } = new List<string>();
        public List<string> Spells { get; } = new List<string>();
        public List<string> Resources { get; } = new List<string>();
        public List<string> Languages { get; } = new List<string>();
        public string CombatProfile { get; set; }
        public string State { get; set; }
        public int Purse { get; set; }
        public bool CanAdvance { get; set; }
    }

    public static class NpcCharacterCommandHandlers
    {
        public static void Register(CommandRegistry commands)
        {
            commands.Register<PromoteNpcCommand>(new PromoteHandler());
            commands.Register<AwardNpcExperienceCommand>(new AwardHandler());
            commands.Register<AdvanceNpcCommand>(new AdvanceHandler());
        }

        private static NpcData Find(CommandContext context, NpcId id, out CommandResult failure)
        {
            failure = null;
            if (context.Committed == null) { failure = CommandResult.InvalidState("There is no active world."); return null; }
            return NpcCommandHandlers.Living(context.Committed, id, out failure);
        }

        private static Dictionary<string, string> Facts(NpcData n, CharacterSheetData s) => new Dictionary<string, string>
        {
            ["npc"] = n.NpcId.ToString(),
            ["name"] = n.Name,
            ["class"] = RpgCatalog.FindClass(s.ClassId)?.Name ?? s.ClassId,
            ["level"] = s.Level.ToString(CultureInfo.InvariantCulture),
            ["occupation"] = n.Character?.Occupation ?? string.Empty,
        };

        private sealed class PromoteHandler : ICommandHandler<PromoteNpcCommand>
        {
            public CommandResult Handle(PromoteNpcCommand c, CommandContext context)
            {
                var n = Find(context, c.NpcId, out var failure);
                if (n == null) return failure;
                if (n.Character != null && n.Character.HasSheet) return CommandResult.Invalid($"{n.Name} already has a character sheet.");
                if (context.Committed.ActiveEncounter != null && context.Committed.ActiveEncounter.Combatants.Exists(x => x.SourceId == n.NpcId.ToString()))
                    return CommandResult.InvalidState($"{n.Name} is in a fight.");
                var world = context.Committed;
                var archetype = NpcArchetypeCatalog.Find(n.Character?.ArchetypeId) ?? NpcArchetypeCatalog.For(world, n);
                var classId = c.ClassId ?? archetype.ClassId ?? archetype.PromotionClassId;
                if (RpgCatalog.FindClass(classId) == null) return CommandResult.Invalid($"Unknown class '{classId}'.");

                var edit = context.Edit();
                var e = edit.FindNpc(n.NpcId);
                var made = NpcCharacterGenesis.Make(edit, e, archetype, classId, out var errors);
                if (made.Sheet == null) return CommandResult.Invalid($"{n.Name} could not be given a sheet.", errors);
                if (e.Character != null)
                {
                    // Keep what they already carry; only the sheet and its record are new.
                    made.Gear = e.Character.Gear?.Clone() ?? made.Gear;
                    made.Purse = e.Character.Purse;
                }

                e.Character = made;
                e.Tier = NpcTier.Notable;
                context.ReportEvent(HistoricalEventType.NpcPromoted, EventSeverity.Info, EventImportance.Minor,
                    e.HomeSettlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { e.HomeSettlementId }, Facts(e, made.Sheet), null, new[] { e.NpcId });
                return CommandResult.Ok($"{e.Name} now has a character sheet: {NpcCharacterGenesis.Describe(e)}.");
            }
        }

        private sealed class AwardHandler : ICommandHandler<AwardNpcExperienceCommand>
        {
            public CommandResult Handle(AwardNpcExperienceCommand c, CommandContext context)
            {
                var n = Find(context, c.NpcId, out var failure);
                if (n == null) return failure;
                if (n.Character == null || !n.Character.HasSheet) return CommandResult.InvalidState($"{n.Name} has no character sheet to record experience on.");
                if (c.Amount < 1) return CommandResult.Invalid("Award at least 1 experience point.");
                if (string.IsNullOrWhiteSpace(c.Key)) return CommandResult.Invalid("An award needs a key (so it is given once).");
                foreach (var a in n.Character.Sheet.ExperienceLog) if (a.Key == c.Key) return CommandResult.InvalidState($"{n.Name} already received the award '{c.Key}'.");
                var edit = context.Edit();
                var sheet = edit.FindNpc(n.NpcId).Character.Sheet;
                sheet.ExperienceLog.Add(new ExperienceAward { Key = c.Key, Source = c.Source, Amount = c.Amount, Date = edit.CurrentSimulationDate, Reason = (c.Reason ?? string.Empty).Trim() });
                sheet.Experience += c.Amount;
                return CommandResult.Ok($"{n.Name} gains {c.Amount} experience ({sheet.Experience} in all)" +
                    (CharacterAdvancement.CanAdvance(sheet) ? "; a new level awaits." : "."));
            }
        }

        private sealed class AdvanceHandler : ICommandHandler<AdvanceNpcCommand>
        {
            public CommandResult Handle(AdvanceNpcCommand c, CommandContext context)
            {
                var n = Find(context, c.NpcId, out var failure);
                if (n == null) return failure;
                if (n.Character == null || !n.Character.HasSheet) return CommandResult.InvalidState($"{n.Name} has no character sheet.");
                if (!CharacterAdvancement.CanAdvance(n.Character.Sheet)) return CommandResult.InvalidState($"{n.Name} has not earned another level.");
                if (context.Committed.ActiveEncounter != null && context.Committed.ActiveEncounter.Combatants.Exists(x => x.SourceId == n.NpcId.ToString()))
                    return CommandResult.InvalidState($"{n.Name} is in a fight.");
                var archetype = NpcArchetypeCatalog.Find(n.Character.ArchetypeId);
                var edit = context.Edit();
                var e = edit.FindNpc(n.NpcId);
                var from = e.Character.Sheet.Level;
                var errors = new List<string>();
                var advanced = NpcCharacterGenesis.AdvanceAvailable(e.Character.Sheet, archetype, edit.CurrentSimulationDate, errors);
                if (advanced == null) return CommandResult.Invalid($"{n.Name} could not advance.", errors);
                if (archetype != null) NpcCharacterGenesis.Prepare(advanced, archetype);
                e.Character.Sheet = advanced;
                var facts = Facts(e, advanced);
                facts["from"] = from.ToString(CultureInfo.InvariantCulture);
                facts["to"] = advanced.Level.ToString(CultureInfo.InvariantCulture);
                context.ReportEvent(HistoricalEventType.NpcLevelAdvanced, EventSeverity.Positive, EventImportance.Minor,
                    e.HomeSettlementId.IsEmpty ? Array.Empty<SettlementId>() : new[] { e.HomeSettlementId }, facts, null, new[] { e.NpcId });
                return CommandResult.Ok($"{e.Name} rises from level {from} to {advanced.Level}.");
            }
        }

        /// <summary>The character view of a person, from their record and the shared rules (null when unknown).</summary>
        public static NpcCharacterView View(WorldData world, NpcData n)
        {
            if (n == null) return null;
            var v = new NpcCharacterView { NpcId = n.NpcId, Name = n.Name, Tier = n.Tier, State = n.IsDeceased ? "dead" : "alive" };
            var c = n.Character;
            if (c == null) { v.Occupation = n.Role.ToString().ToLowerInvariant(); v.CombatProfile = Domain.Combat.CreatureCatalog.ProfileFor(n.Role); return v; }
            v.Occupation = c.Occupation;
            v.Archetype = c.ArchetypeId;
            v.Purse = c.Purse;
            if (c.OwnsItems) foreach (var pair in c.Equipment) v.Equipment.Add(ItemCatalog.Find(pair.Value.ItemId)?.Name ?? pair.Value.ItemId); // Phase 15: a companion's own items
            else foreach (var id in c.Gear.All()) v.Equipment.Add(ItemCatalog.Find(id)?.Name ?? id);
            if (!c.HasSheet)
            {
                v.CombatProfile = Domain.Combat.CreatureCatalog.Find(Domain.Combat.CreatureCatalog.ProfileFor(n.Role))?.Name ?? "Commoner";
                return v;
            }

            var s = c.Sheet;
            var r = new CharacterRules(s, CharacterRules.LoadoutOf(c));
            v.HasSheet = true;
            v.Species = r.Species?.Name;
            v.Background = r.Background?.Name;
            v.ClassName = r.Class?.Name;
            v.Subclass = RpgCatalog.FindSubclass(s.SubclassId)?.Name;
            v.Level = s.Level;
            v.Experience = s.Experience;
            v.Hp = s.CurrentHp;
            v.MaxHp = r.MaxHp;
            var ac = r.ArmorClass(null);
            v.ArmorClass = ac.Total;
            v.ArmorClassText = ac.Text;
            v.Speed = r.Speed;
            v.ProficiencyBonus = r.ProficiencyBonus;
            foreach (var a in AbilityRules.All) v.Scores[(int)a] = r.Score(a);
            foreach (var a in AbilityRules.All)
            {
                var save = r.SaveBonus(a);
                v.Saves.Add($"{a.ToString().Substring(0, 3)} {(save.Total >= 0 ? "+" : string.Empty)}{save.Total}");
            }

            foreach (var sk in SkillRules.All)
                if (r.SkillRank(sk) != ProficiencyRank.None) v.Skills.Add($"{SkillRules.Name(sk)} {(r.SkillBonus(sk).Total >= 0 ? "+" : string.Empty)}{r.SkillBonus(sk).Total}");
            foreach (var f in r.Features) v.Features.Add(f.feature.Name);
            foreach (var l in r.Languages) v.Languages.Add(RpgCatalog.FindLanguage(l)?.Name ?? l);
            if (r.Class?.Spellcasting != null)
            {
                var access = new SpellAccess(r);
                foreach (var sp in access.ReadyCantrips()) v.Spells.Add(sp.Name + " (cantrip)");
                foreach (var sp in access.ReadySpells()) v.Spells.Add($"{sp.Name} (level {sp.Level})");
            }

            foreach (var res in r.ResourceDefinitions)
                v.Resources.Add($"{res.Name} {(s.Resources.TryGetValue(res.Id, out var left) ? left : 0)}/{r.ResourceMax(res)}");
            v.CanAdvance = CharacterAdvancement.CanAdvance(s);
            v.State = n.IsDeceased || s.IsDead ? "dead" : s.CurrentHp <= 0 ? (s.IsStable ? "unconscious, stable" : "dying") : s.CurrentHp < r.MaxHp ? "injured" : "unhurt";
            return v;
        }
    }

    public sealed partial class WorldSession
    {
        /// <summary>Phase 10: a named person's character view (archetype, sheet, gear), or null.</summary>
        public NpcCharacterView GetNpcCharacter(NpcId id) => HasWorld ? NpcCharacterCommandHandlers.View(_world, _world.FindNpc(id)) : null;

        /// <summary>Phase 10: a copy of a settlement's persisted plan (ADR-0047), or null when it has none.</summary>
        public Realmkeeper.Domain.Settlements.SettlementLayoutData GetSettlementLayout(SettlementId id) => HasWorld ? _world.FindLayout(id)?.Clone() : null;

        /// <summary>Phase 10: a settlement's herds (creature, head count, pasture key).</summary>
        public IReadOnlyList<(string CreatureId, int Count, string PastureKey)> GetHerds(SettlementId id)
        {
            var list = new List<(string, int, string)>();
            if (!HasWorld) return list;
            foreach (var h in _world.Livestock) if (h.SettlementId == id) list.Add((h.CreatureId, h.Count, h.PastureKey));
            return list;
        }
    }
}
