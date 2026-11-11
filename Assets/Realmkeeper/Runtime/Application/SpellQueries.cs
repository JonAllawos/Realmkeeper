using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Application
{
    /// <summary>One spell as the spellbook shows it (Phase 9): the definition, explained, and where the character stands with it.</summary>
    public sealed class SpellView
    {
        public SpellDefinition Definition { get; set; }
        public string Id => Definition.Id;
        public string Name => Definition.Name;
        public int Level => Definition.Level;
        public bool Known { get; set; }
        public bool Prepared { get; set; }
        public bool InBook { get; set; }
        public bool Always { get; set; }

        /// <summary>Known, prepared or always prepared (cantrips: chosen).</summary>
        public bool Ready => Known || Prepared || Always;

        public string CastingTimeText => Definition.CastingTime == CastingTime.BonusAction ? "Bonus action" : Definition.CastingTime == CastingTime.Reaction ? "Reaction" : Definition.CastingTime == CastingTime.Longer ? "1 minute or more" : "Action";
        public string StatusText => Definition.Status == ImplementationStatus.Functional ? "Functional" : Definition.Status == ImplementationStatus.Partial ? "Partly simulated" : Definition.Status == ImplementationStatus.DataOnly ? "No effect yet" : "Not castable yet";

        /// <summary>What it does, in words built from its effects (the numbers the engine uses).</summary>
        public string EffectsText { get; set; }
    }

    /// <summary>An open spell pick the character can still make.</summary>
    public sealed class OpenSpellPick
    {
        public string ChoiceId { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int Remaining { get; set; }
        public IReadOnlyList<SpellView> Options { get; set; } = Array.Empty<SpellView>();
    }

    public sealed class SpellbookView
    {
        public bool IsCaster { get; set; }
        public string ClassName { get; set; }
        public Ability Ability { get; set; }
        public int SaveDc { get; set; }
        public int AttackBonus { get; set; }
        public string SaveDcText { get; set; }
        public string AttackText { get; set; }
        public SpellKeeping Keeping { get; set; }
        public string KeepingText { get; set; }
        public int MaxSpellLevel { get; set; }
        public int PreparedLimit { get; set; }
        public string PreparationText { get; set; }
        public bool UsesPactMagic { get; set; }
        public int PactSlotLevel { get; set; }

        /// <summary>(level, left, max) for each slot level the class has (one line for pact slots).</summary>
        public IReadOnlyList<(int level, int left, int max)> Slots { get; set; } = Array.Empty<(int, int, int)>();

        public IReadOnlyList<SpellView> Cantrips { get; set; } = Array.Empty<SpellView>();

        /// <summary>Every leveled spell the character knows, has in its book, prepared or always has.</summary>
        public IReadOnlyList<SpellView> Spells { get; set; } = Array.Empty<SpellView>();

        /// <summary>Spells the character could prepare (the class list or the book, up to its spell level).</summary>
        public IReadOnlyList<SpellView> Preparable { get; set; } = Array.Empty<SpellView>();

        public IReadOnlyList<string> Prepared { get; set; } = Array.Empty<string>();
        public IReadOnlyList<OpenSpellPick> OpenPicks { get; set; } = Array.Empty<OpenSpellPick>();
        public IReadOnlyList<RestRecord> Rests { get; set; } = Array.Empty<RestRecord>();
        public string ShortRestProblem { get; set; }
        public string LongRestProblem { get; set; }
        public int HitDiceLeft { get; set; }
        public int HitDie { get; set; }
    }

    public sealed partial class WorldSession
    {
        /// <summary>The player's spellbook and rest state, derived (null when there is no created character).</summary>
        public SpellbookView GetSpellbook()
        {
            var p = _world?.Player;
            if (p?.Rpg == null || p.Rpg.State != CreationState.Complete) return null;
            var rules = new CharacterRules(p.Rpg, CharacterRules.LoadoutOf(p));
            var a = new SpellAccess(rules);
            var view = new SpellbookView
            {
                IsCaster = a.IsCaster, ClassName = rules.Class?.Name, Keeping = a.Keeping, MaxSpellLevel = a.MaxSpellLevel, PreparedLimit = a.PreparedLimit,
                UsesPactMagic = a.UsesPactMagic, PactSlotLevel = a.PactSlotLevel, Prepared = new List<string>(a.Prepared), Rests = new List<RestRecord>(p.Rpg.Spellcasting.RestLog),
                ShortRestProblem = RestProblem(RestKind.Short), LongRestProblem = RestProblem(RestKind.Long),
                HitDiceLeft = p.Rpg.Resources.TryGetValue("hit_dice", out var hd) ? hd : 0, HitDie = rules.HitDie,
            };
            if (!a.IsCaster) return view;
            var sc = rules.Class.Spellcasting;
            view.Ability = sc.Ability;
            var dc = rules.SpellSaveDc();
            var atk = rules.SpellAttack();
            view.SaveDc = dc.Total;
            view.AttackBonus = atk.Total;
            view.SaveDcText = dc.Text;
            view.AttackText = atk.Text;
            view.KeepingText = a.Keeping == SpellKeeping.Known ? "Known spells: chosen at creation and level-up." :
                a.Keeping == SpellKeeping.Spellbook ? "Spellbook: prepare spells from your book; two new spells are written in at each level." :
                a.Keeping == SpellKeeping.PreparedOneSwap ? "Prepared from your class list; after a long rest you can replace one." :
                "Prepared from your whole class list; after a long rest you can change the list.";
            var changes = p.Rpg.Spellcasting.PreparationChanges;
            view.PreparationText = a.Keeping == SpellKeeping.Known ? string.Empty :
                $"{a.Prepared.Count} of {a.PreparedLimit} prepared · " + (changes < 0 ? "you can change the list now" : changes > 0 ? $"you can replace {changes} now" : "you can add spells while the list has room; changes wait for a long rest");

            var slots = new List<(int, int, int)>();
            if (a.UsesPactMagic)
            {
                var res = rules.FindResource("pact_slots");
                slots.Add((a.PactSlotLevel, a.SlotsLeft(a.PactSlotLevel), res == null ? 0 : rules.ResourceMax(res)));
            }
            else
                for (var l = 1; l <= 9; l++)
                {
                    var res = rules.FindResource("spell_slots_" + l);
                    if (res == null || rules.ResourceMax(res) <= 0) continue;
                    slots.Add((l, a.SlotsLeft(l), rules.ResourceMax(res)));
                }

            view.Slots = slots;
            var known = new HashSet<string>(a.KnownSpells);
            var prepared = new HashSet<string>(a.Prepared);
            var book = new HashSet<string>(a.Spellbook);
            var always = new HashSet<string>(a.AlwaysPrepared);
            SpellView V(SpellDefinition s) => new SpellView
            {
                Definition = s, Known = known.Contains(s.Id) || s.IsCantrip, Prepared = prepared.Contains(s.Id), InBook = book.Contains(s.Id), Always = always.Contains(s.Id), EffectsText = Explain(s),
            };

            var cantrips = new List<SpellView>();
            foreach (var s in a.ReadyCantrips()) cantrips.Add(V(s));
            view.Cantrips = cantrips;
            var spells = new List<SpellView>();
            var ids = new List<string>();
            ids.AddRange(a.KnownSpells);
            ids.AddRange(a.Spellbook);
            ids.AddRange(a.Prepared);
            ids.AddRange(a.AlwaysPrepared);
            foreach (var id in ids)
                if (SpellCatalog.Find(id) is SpellDefinition s && !spells.Exists(x => x.Id == id)) spells.Add(V(s));
            spells.Sort((x, y) => x.Level != y.Level ? x.Level.CompareTo(y.Level) : string.CompareOrdinal(x.Name, y.Name));
            view.Spells = spells;

            var preparable = new List<SpellView>();
            if (a.Keeping == SpellKeeping.Spellbook) foreach (var id in a.Spellbook) { if (SpellCatalog.Find(id) is SpellDefinition s && s.Level <= a.MaxSpellLevel) preparable.Add(V(s)); }
            else if (a.Keeping == SpellKeeping.Prepared || a.Keeping == SpellKeeping.PreparedOneSwap)
                foreach (var s in SpellCatalog.All) if (!s.IsCantrip && s.Level <= a.MaxSpellLevel && SpellCatalog.OnList(s, rules.Class.Id) && !always.Contains(s.Id)) preparable.Add(V(s));
            preparable.Sort((x, y) => x.Level != y.Level ? x.Level.CompareTo(y.Level) : string.CompareOrdinal(x.Name, y.Name));
            view.Preparable = preparable;

            var picks = new List<OpenSpellPick>();
            foreach (var slot in CharacterProgression.Choices(p.Rpg, p.Rpg.Level))
            {
                var c = slot.Choice;
                if (!c.UpTo) continue;
                var remaining = c.Count - p.Rpg.Chosen(c.Id).Count;
                if (remaining <= 0) continue;
                var options = new List<SpellView>();
                foreach (var o in c.Options)
                {
                    var s = SpellCatalog.Find(o.Id);
                    if (s == null || ids.Contains(s.Id) || new List<string>(a.Cantrips).Contains(s.Id)) continue;
                    if (s.Level > 0 && s.Level > RpgCatalog.MaxSpellLevel(sc, slot.Level)) continue;
                    options.Add(V(s));
                }

                if (options.Count > 0) picks.Add(new OpenSpellPick { ChoiceId = c.Id, Name = c.Name + (slot.Level > 1 ? $" (level {slot.Level})" : string.Empty), Level = slot.Level, Remaining = remaining, Options = options });
            }

            view.OpenPicks = picks;
            return view;
        }

        /// <summary>A spell's effects in words, from its definition (what the engine resolves).</summary>
        public static string Explain(SpellDefinition s)
        {
            var parts = new List<string>();
            if (s.Resolution == SpellResolution.Attack) parts.Add(s.Instances > 1 || s.ScalesByInstances ? $"{(s.ScalesByInstances ? "beams" : s.Instances.ToString(CultureInfo.InvariantCulture))}, each a spell attack" : "spell attack");
            if (s.Resolution == SpellResolution.Save) parts.Add($"{s.SaveAbility} save");
            foreach (var e in s.Effects)
                switch (e.Kind)
                {
                    case CombatEffectKind.Damage: parts.Add($"{e.Dice} {e.DamageType.ToString().ToLowerInvariant()}{(e.HalfOnSave ? " (half on a save)" : string.Empty)}{(s.Instances > 1 ? " each" : string.Empty)}"); break;
                    case CombatEffectKind.Healing: parts.Add($"heals {e.Dice} + ability modifier"); break;
                    case CombatEffectKind.Condition: parts.Add($"{CombatEngine.Describe(e.Condition.Value)}{(e.RepeatSaveEachTurn ? " (save again each turn)" : string.Empty)}{(e.EscapeWithStrengthCheck ? " (break free with Strength)" : string.Empty)}"); break;
                    case CombatEffectKind.RemoveCondition: parts.Add("ends " + string.Join(" or ", new List<ConditionType>(e.Conditions).ConvertAll(c => CombatEngine.Describe(c)))); break;
                    case CombatEffectKind.ArmorClassBonus: parts.Add($"+{e.Value} AC"); break;
                    case CombatEffectKind.ArmorClassBase: parts.Add($"AC {e.Value} + Dex"); break;
                    case CombatEffectKind.SpeedChange: parts.Add($"speed {(e.Value > 0 ? "+" : string.Empty)}{e.Value} ft"); break;
                    case CombatEffectKind.SpeedMultiplier: parts.Add($"speed ×{e.Value}"); break;
                    case CombatEffectKind.AttackAndSaveDie: parts.Add($"{(e.Value < 0 ? "−" : "+")}{e.Dice} to attacks and saves"); break;
                    case CombatEffectKind.AttackersHaveAdvantage: parts.Add("attacks against have advantage"); break;
                    case CombatEffectKind.NextAttackAgainstHasAdvantage: parts.Add("next attack against it has advantage"); break;
                    case CombatEffectKind.NextAttackHasDisadvantage: parts.Add("its next attack has disadvantage"); break;
                    case CombatEffectKind.ExtraDamageWhenCasterHits: parts.Add($"+{e.Dice} {e.DamageType.ToString().ToLowerInvariant()} on your hits"); break;
                    case CombatEffectKind.NoReactions: parts.Add("no reactions"); break;
                    case CombatEffectKind.Teleport: parts.Add($"teleport {e.Value} ft"); break;
                    case CombatEffectKind.Push: parts.Add($"pushed {e.Value} ft on a failed save"); break;
                    case CombatEffectKind.AdvantageOnDexteritySaves: parts.Add("advantage on Dexterity saves"); break;
                    case CombatEffectKind.HitPointMaximumBonus: parts.Add($"+{e.Value} hit point maximum"); break;
                    case CombatEffectKind.BonusDash: parts.Add("Dash as a bonus action"); break;
                    case CombatEffectKind.EndSpells: parts.Add($"ends spells of level {e.Value} or lower"); break;
                    case CombatEffectKind.EndsOnHostileAct: parts.Add("ends if the target attacks or casts"); break;
                }

            if (s.HasArea) parts.Add($"{s.AreaSize}-ft {s.Area.ToString().ToLowerInvariant()}");
            if (s.Concentration) parts.Add("concentration");
            return string.Join(" · ", parts);
        }
    }
}
