using System;
using System.Collections.Generic;
using System.Globalization;
using Realmkeeper.Domain.World;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>A number and how it is made up ("Dexterity +2, proficiency +2").</summary>
    public sealed class Breakdown
    {
        private readonly List<(string label, int value)> _parts = new List<(string, int)>();

        public int Total { get; private set; }
        public IReadOnlyList<(string label, int value)> Parts => _parts;

        public Breakdown Add(string label, int value)
        {
            _parts.Add((label, value));
            Total += value;
            return this;
        }

        private readonly List<string> _notes = new List<string>();

        /// <summary>Phase 8: restrictions and remarks that explain the number without changing it.</summary>
        public IReadOnlyList<string> Notes => _notes;

        public Breakdown Note(string note)
        {
            if (!string.IsNullOrEmpty(note) && !_notes.Contains(note)) _notes.Add(note);
            return this;
        }

        public string Text
        {
            get
            {
                var parts = new List<string>();
                foreach (var (label, value) in _parts) parts.Add(label + " " + (value >= 0 ? "+" : string.Empty) + value.ToString(CultureInfo.InvariantCulture));
                return string.Join(", ", parts) + (_notes.Count > 0 ? "; " + string.Join("; ", _notes) : string.Empty);
            }
        }

        public static string Signed(int v) => (v >= 0 ? "+" : string.Empty) + v.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>One effect, where it came from, and the level it arrived at (for explanations and ordering).</summary>
    public readonly struct SourcedEffect
    {
        public SourcedEffect(RuleEffect effect, string source, int level = 1)
        {
            Effect = effect;
            Source = source;
            Level = level;
        }

        public RuleEffect Effect { get; }
        public string Source { get; }
        public int Level { get; }
    }

    /// <summary>Advantage and disadvantage on a roll, each with its sources (Phase 7). Both together cancel out.</summary>
    public sealed class RollMode
    {
        public List<string> Advantage { get; } = new List<string>();
        public List<string> Disadvantage { get; } = new List<string>();

        /// <summary>Situational sources ("against being poisoned") that apply only sometimes.</summary>
        public List<string> Situational { get; } = new List<string>();

        public bool HasAdvantage => Advantage.Count > 0 && Disadvantage.Count == 0;
        public bool HasDisadvantage => Disadvantage.Count > 0 && Advantage.Count == 0;

        public string Text
        {
            get
            {
                var parts = new List<string>();
                if (Advantage.Count > 0) parts.Add("advantage (" + string.Join(", ", Advantage) + ")");
                if (Disadvantage.Count > 0) parts.Add("disadvantage (" + string.Join(", ", Disadvantage) + ")");
                if (Advantage.Count > 0 && Disadvantage.Count > 0) parts.Add("they cancel out");
                foreach (var s in Situational) parts.Add(s);
                return string.Join("; ", parts);
            }
        }
    }

    /// <summary>A feature the character has: what, from where, and from which level.</summary>
    public readonly struct FeatureEntry
    {
        public FeatureEntry(FeatureDefinition feature, string source, int level)
        {
            Feature = feature;
            Source = source;
            Level = level;
        }

        public FeatureDefinition Feature { get; }
        public string Source { get; }
        public int Level { get; }
    }

    /// <summary>
    /// Everything derived from a character's choices (Phase 6, level-aware since Phase 7). One evaluator for all
    /// typed effects: it collects the effects of the species, background, class features up to the character's
    /// level (minus any a later feature replaces), the subclass, every choice made up to that level (options,
    /// skills, languages, feats and their ability increases), then answers questions — scores, modifiers,
    /// skills and saves with breakdowns and roll modes, initiative, passive scores, hit points, armour class,
    /// speed, senses, resources, spellcasting — for any <see cref="CharacterSheetData"/>, player or not.
    ///
    /// Stacking (SRD 5.2.1 "combining effects", encoded here): bonuses from different features add up; the
    /// same feature never counts twice; only one unarmoured-defence formula applies (the best); carrying
    /// capacity and senses take the best value; advantage and disadvantage on the same roll cancel. Nothing
    /// here is stored; nothing here mutates.
    /// </summary>
    public sealed class CharacterRules
    {
        private readonly CharacterSheetData _sheet;
        private readonly Loadout _loadout;
        private readonly List<SourcedEffect> _effects = new List<SourcedEffect>();
        private readonly List<FeatureEntry> _features = new List<FeatureEntry>();
        private readonly List<FeatureEntry> _choiceFeatures = new List<FeatureEntry>();
        private IReadOnlyList<ChoiceSlot> _choices;
        private int[] _scores;

        public CharacterRules(CharacterSheetData sheet) : this(sheet, Loadout.Unarmored)
        {
        }

        public CharacterRules(CharacterSheetData sheet, Loadout loadout)
        {
            _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
            _loadout = loadout;
            Species = RpgCatalog.FindSpecies(sheet.SpeciesId);
            Background = RpgCatalog.FindBackground(sheet.BackgroundId);
            Class = RpgCatalog.FindClass(sheet.ClassId);
            var sub = RpgCatalog.FindSubclass(sheet.SubclassId);
            Subclass = sub != null && Class != null && sub.ClassId == Class.Id ? sub : null;
            Collect();
        }

        public CharacterSheetData Sheet => _sheet;
        public Loadout Loadout => _loadout;
        public SpeciesDefinition Species { get; }
        public BackgroundDefinition Background { get; }
        public ClassDefinition Class { get; }
        public SubclassDefinition Subclass { get; }
        public bool IsComplete => _sheet.State == CreationState.Complete && Species != null && Background != null && Class != null;
        public IReadOnlyList<SourcedEffect> Effects => _effects;
        public int Level => Math.Max(1, Math.Min(AbilityRules.MaxLevel, _sheet.Level));
        public int ProficiencyBonus => AbilityRules.ProficiencyBonus(Level);

        /// <summary>The choices that exist up to the character's level.</summary>
        public IReadOnlyList<ChoiceSlot> ChoiceSlots => _choices;

        // ----- Collection --------------------------------------------------------------------------------

        private void Collect()
        {
            if (Species != null)
            {
                foreach (var t in Species.Traits) _features.Add(new FeatureEntry(t, Species.Name, 1));
                foreach (var l in Species.Languages) Add(new RuleEffect(EffectKind.Language, l), Species.Name, 1);
            }

            if (Background != null)
            {
                foreach (var s in Background.Skills) Add(new RuleEffect(EffectKind.SkillProficiency, s.ToString()), Background.Name, 1);
                foreach (var t in Background.Tools) Add(new RuleEffect(EffectKind.ToolProficiency, t), Background.Name, 1);
                foreach (var l in Background.Languages) Add(new RuleEffect(EffectKind.Language, l), Background.Name, 1);
                if (Background.Feature != null) _features.Add(new FeatureEntry(Background.Feature, Background.Name, 1));
            }

            if (Class != null)
            {
                foreach (var a in Class.SavingThrows) Add(new RuleEffect(EffectKind.SaveProficiency, a.ToString()), Class.Name, 1);
                foreach (var a in Class.ArmorProficiencies) Add(new RuleEffect(EffectKind.ArmorProficiency, a), Class.Name, 1);
                foreach (var w in Class.WeaponProficiencies) Add(new RuleEffect(EffectKind.WeaponProficiency, w), Class.Name, 1);
                foreach (var t in Class.Tools) Add(new RuleEffect(EffectKind.ToolProficiency, t), Class.Name, 1);
                foreach (var s in _sheet.Chosen(ClassDefinition.SkillChoiceId)) Add(new RuleEffect(EffectKind.SkillProficiency, s), Class.Name + " (chosen)", 1);
                foreach (var f in Class.Features) _features.Add(new FeatureEntry(f, Class.Name + " 1", 1));
                for (var level = 2; level <= Level; level++)
                {
                    if (Class.LaterFeatures.TryGetValue(level, out var later))
                        foreach (var f in later) _features.Add(new FeatureEntry(f, Class.Name + " " + level, level));
                    if (Subclass != null && Subclass.Features.TryGetValue(level, out var sf))
                        foreach (var f in sf) _features.Add(new FeatureEntry(f, Subclass.Name + " " + level, level));
                }
            }

            // A feature replaced by a later one stops applying (Extra Attack → Two Extra Attacks).
            var replaced = new HashSet<string>(StringComparer.Ordinal);
            foreach (var f in _features) if (f.Feature.Replaces != null) replaced.Add(f.Feature.Replaces);
            if (replaced.Count > 0) _features.RemoveAll(f => replaced.Contains(f.Feature.Id));
            foreach (var f in _features)
                foreach (var e in f.Feature.Effects) Add(e, f.Source + ": " + f.Feature.Name, f.Level);

            _choices = Class != null || Species != null ? CharacterProgression.Choices(_sheet, Level) : Array.Empty<ChoiceSlot>();
            foreach (var slot in _choices) CollectChoice(slot);

            // Phase 11 (ADR-0052): worn magic items, through the same effects as features (filtered by the loadout's owner).
            if (_loadout.ItemEffects != null)
                foreach (var (source, effect) in _loadout.ItemEffects) Add(effect, source, 1);

            // Order by level so ability increases apply in the order they were gained (caps depend on it).
            var ordered = new List<(int index, SourcedEffect effect)>();
            for (var i = 0; i < _effects.Count; i++) ordered.Add((i, _effects[i]));
            ordered.Sort((a, b) => a.effect.Level != b.effect.Level ? a.effect.Level.CompareTo(b.effect.Level) : a.index.CompareTo(b.index));
            _effects.Clear();
            foreach (var (_, e) in ordered) _effects.Add(e);
        }

        private void Add(RuleEffect e, string source, int level) => _effects.Add(new SourcedEffect(e, source, level));

        private void CollectChoice(ChoiceSlot slot)
        {
            var c = slot.Choice;
            var picked = _sheet.Chosen(c.Id);
            switch (c.Kind)
            {
                case ChoiceKind.Option:
                    foreach (var id in picked)
                        foreach (var o in c.Options)
                            if (o.Id == id)
                            {
                                foreach (var e in o.Effects) Add(e, slot.Source + ": " + c.Name + " (" + o.Name + ")", c.Level);
                                _choiceFeatures.Add(new FeatureEntry(new FeatureDefinition(c.Id + ":" + o.Id, c.Name + ": " + o.Name, o.Description) { Kind = FeatureKind.Choice, Needs = o.Needs | NeedsOf(o.Effects) }, slot.Source, c.Level));
                            }

                    break;
                case ChoiceKind.Skill:
                    foreach (var s in picked) Add(new RuleEffect(EffectKind.SkillProficiency, s), slot.Source + ": " + c.Name, c.Level);
                    break;
                case ChoiceKind.Language:
                    foreach (var l in picked) Add(new RuleEffect(EffectKind.Language, l), slot.Source + ": " + c.Name, c.Level);
                    break;
                case ChoiceKind.Feat:
                    var feat = picked.Count == 1 ? RpgCatalog.FindFeat(picked[0]) : null;
                    if (feat == null) break;
                    foreach (var e in feat.Effects) Add(e, "Feat: " + feat.Name, c.Level);
                    _choiceFeatures.Add(new FeatureEntry(new FeatureDefinition("feat:" + feat.Id + ":" + c.Level, feat.Name, feat.Description) { Kind = FeatureKind.Choice, Needs = feat.Needs | NeedsOf(feat.Effects) }, slot.Source, c.Level));
                    break;
                case ChoiceKind.AbilityIncrease:
                    foreach (var a in picked) Add(new RuleEffect(EffectKind.AbilityIncrease, a, 1, null, EffectCondition.Always, c.Limit), slot.Source, c.Level);
                    break;
                // Expertise is a rank on existing proficiencies (SkillRank); the subclass lives in SubclassId.
            }
        }

        /// <summary>Systems a feature waits for, including those implied by its effects (combat rules, innate magic).</summary>
        public static IntegrationNeed NeedsOf(IReadOnlyList<RuleEffect> effects)
        {
            var n = IntegrationNeed.None;
            foreach (var e in effects)
            {
                if (e.Kind == EffectKind.CombatRule) n |= IntegrationNeed.Combat;
                if (e.Kind == EffectKind.InnateMagic) n |= IntegrationNeed.Magic;
            }

            return n;
        }

        private bool Applies(RuleEffect e) => _loadout.Allows(e.Condition);

        private IEnumerable<SourcedEffect> Of(EffectKind kind)
        {
            foreach (var se in _effects)
                if (se.Effect.Kind == kind && Applies(se.Effect)) yield return se;
        }

        // ----- Abilities -------------------------------------------------------------------------------

        public int SpeciesBonus(Ability a) => Species != null && Species.AbilityBonuses.TryGetValue(a, out var v) ? v : 0;

        public int BaseScore(Ability a) => _sheet.BaseScores != null && _sheet.BaseScores.Length == 6 ? _sheet.BaseScores[(int)a] : 10;

        /// <summary>
        /// The final score: base plus species increase (at most 20), then every later increase in the order it was
        /// gained, each up to its own limit (20 for Ability Score Improvement, 25 for a capstone, 30 for an Epic Boon).
        /// </summary>
        public int Score(Ability a)
        {
            if (_scores == null)
            {
                var scores = new int[6];
                foreach (var ab in AbilityRules.All) scores[(int)ab] = ScoreBreakdown(ab).Total;
                _scores = scores;
            }

            return _scores[(int)a];
        }

        public Breakdown ScoreBreakdown(Ability a)
        {
            var b = new Breakdown().Add("base", BaseScore(a));
            if (SpeciesBonus(a) != 0) b.Add(Species.Name, SpeciesBonus(a));
            if (b.Total > AbilityRules.MaxScore) b.Add("maximum", AbilityRules.MaxScore - b.Total);
            foreach (var se in Of(EffectKind.AbilityIncrease))
            {
                if (se.Effect.Key != a.ToString()) continue;
                var limit = se.Effect.Limit > 0 ? se.Effect.Limit : AbilityRules.MaxScore;
                var gain = Math.Max(0, Math.Min(limit, b.Total + se.Effect.Value) - b.Total);
                b.Add(se.Source + (gain < se.Effect.Value ? $" (capped at {limit})" : string.Empty), gain);
            }

            // Phase 11: an ability set to a minimum while an item is worn (Gauntlets of Ogre Power: Strength 19).
            foreach (var se in Of(EffectKind.AbilityMinimum))
                if (se.Effect.Key == a.ToString() && b.Total < se.Effect.Value) b.Add(se.Source + " (" + a + " " + se.Effect.Value + ")", se.Effect.Value - b.Total);

            return b;
        }

        public int Modifier(Ability a) => AbilityRules.Modifier(Score(a));

        // ----- Skills and saves ------------------------------------------------------------------------

        public ProficiencyRank SkillRank(Skill skill)
        {
            var proficient = false;
            foreach (var se in Of(EffectKind.SkillProficiency)) if (se.Effect.Key == skill.ToString()) proficient = true;
            if (!proficient) return ProficiencyRank.None;
            foreach (var slot in _choices)
                if (slot.Choice.Kind == ChoiceKind.Expertise)
                    foreach (var s in _sheet.Chosen(slot.Choice.Id))
                        if (s == skill.ToString()) return ProficiencyRank.Expertise;
            return ProficiencyRank.Proficient;
        }

        /// <summary>Who grants the skill (for the sheet).</summary>
        public IReadOnlyList<string> SkillSources(Skill skill)
        {
            var list = new List<string>();
            foreach (var se in Of(EffectKind.SkillProficiency)) if (se.Effect.Key == skill.ToString()) list.Add(se.Source);
            return list;
        }

        public Breakdown SkillBonus(Skill skill)
        {
            var ability = SkillRules.AbilityOf(skill);
            var b = new Breakdown().Add(ability.ToString(), Modifier(ability));
            var rank = SkillRank(skill);
            if (rank == ProficiencyRank.Proficient) b.Add("proficiency", ProficiencyBonus);
            if (rank == ProficiencyRank.Expertise) b.Add("expertise", ProficiencyBonus * 2);
            if (rank == ProficiencyRank.None)
                foreach (var se in Once(EffectKind.HalfProficiencyUntrained))
                    b.Add(se.Source, ProficiencyBonus / 2);
            foreach (var se in Once(EffectKind.SkillAbilityBonus))
            {
                if (se.Effect.Key != skill.ToString() || !Enum.TryParse<Ability>(se.Effect.Text, out var extra)) continue;
                b.Add(se.Source + " (" + extra + ")", Math.Max(se.Effect.Limit, Modifier(extra)));
            }

            foreach (var se in Of(EffectKind.SaveBonus)) b.Add(se.Source, se.Effect.Value); // Phase 11: Ring and Cloak of Protection

            return b;
        }

        /// <summary>Advantage and disadvantage on a skill check (Remarkable Athlete …).</summary>
        public RollMode SkillRoll(Skill skill) => Roll("skill:" + skill, "check:" + SkillRules.AbilityOf(skill));

        /// <summary>Skills granted outright (species traits and background), which creation choices may not repeat.</summary>
        public IReadOnlyList<string> GrantedSkills()
        {
            var list = new List<string>();
            if (Species != null)
                foreach (var t in Species.Traits)
                    foreach (var e in t.Effects)
                        if (e.Kind == EffectKind.SkillProficiency && !list.Contains(e.Key)) list.Add(e.Key);
            if (Background != null)
                foreach (var s in Background.Skills)
                    if (!list.Contains(s.ToString())) list.Add(s.ToString());
            return list;
        }

        public bool SaveProficient(Ability a)
        {
            foreach (var se in Of(EffectKind.SaveProficiency)) if (se.Effect.Key == a.ToString()) return true;
            return false;
        }

        public Breakdown SaveBonus(Ability a)
        {
            var b = new Breakdown().Add(a.ToString(), Modifier(a));
            if (SaveProficient(a)) b.Add("proficiency", ProficiencyBonus);
            foreach (var se in Once(EffectKind.SaveAbilityBonus))
            {
                if ((se.Effect.Key != "all" && se.Effect.Key != a.ToString()) || !Enum.TryParse<Ability>(se.Effect.Text, out var extra)) continue;
                b.Add(se.Source + " (" + extra + ")", Math.Max(se.Effect.Limit, Modifier(extra)));
            }

            return b;
        }

        /// <summary>Advantage and disadvantage on a saving throw; conditional save advantages are listed as situational.</summary>
        public RollMode SaveRoll(Ability a)
        {
            var r = Roll("save:" + a);
            return r;
        }

        /// <summary>Initiative: Dexterity plus bonuses (Alert adds the proficiency bonus).</summary>
        public Breakdown Initiative()
        {
            var b = new Breakdown().Add("Dexterity", Modifier(Ability.Dexterity));
            foreach (var se in Once(EffectKind.InitiativeBonus))
                b.Add(se.Source, se.Effect.Key == "proficiency" ? ProficiencyBonus : se.Effect.Value);
            return b;
        }

        public RollMode InitiativeRoll => Roll("initiative");

        /// <summary>A passive score: 10 + the skill bonus, +5 with unconditional advantage, −5 with disadvantage.</summary>
        public Breakdown Passive(Skill skill)
        {
            var b = new Breakdown().Add("base", 10).Add(SkillRules.Name(skill), SkillBonus(skill).Total);
            var roll = SkillRoll(skill);
            if (roll.HasAdvantage) b.Add("advantage", 5);
            if (roll.HasDisadvantage) b.Add("disadvantage", -5);
            return b;
        }

        public int PassivePerception => Passive(Skill.Perception).Total;

        /// <summary>The lowest a d20 counts as on proficient ability checks (Reliable Talent), or 0.</summary>
        public int ProficientCheckFloor
        {
            get
            {
                var v = 0;
                foreach (var se in Of(EffectKind.MinimumRoll)) if (se.Effect.Key == "proficient_checks") v = Math.Max(v, se.Effect.Value);
                return v;
            }
        }

        private RollMode Roll(params string[] targets)
        {
            var r = new RollMode();
            foreach (var se in _effects)
            {
                if (!Applies(se.Effect) || (se.Effect.Kind != EffectKind.Advantage && se.Effect.Kind != EffectKind.Disadvantage)) continue;
                if (Array.IndexOf(targets, se.Effect.Key) < 0) continue;
                if (!string.IsNullOrEmpty(se.Effect.Text))
                {
                    r.Situational.Add((se.Effect.Kind == EffectKind.Advantage ? "advantage " : "disadvantage ") + se.Effect.Text + " (" + se.Source + ")");
                    continue;
                }

                var list = se.Effect.Kind == EffectKind.Advantage ? r.Advantage : r.Disadvantage;
                if (!list.Contains(se.Source)) list.Add(se.Source);
            }

            // Phase 8 (SRD 5.2.1 Armor Training): untrained armour gives disadvantage on every D20 test that uses
            // Strength or Dexterity; armour with a stealth penalty gives disadvantage on Stealth.
            if (UntrainedArmor)
                foreach (var t in targets)
                    if ((t.EndsWith(":Strength", StringComparison.Ordinal) || t.EndsWith(":Dexterity", StringComparison.Ordinal)) && !r.Disadvantage.Contains("untrained armour"))
                        r.Disadvantage.Add("untrained armour");
            if (_loadout.BodyArmor != null && _loadout.BodyArmor.StealthDisadvantage && Array.IndexOf(targets, "skill:" + Skill.Stealth) >= 0)
                r.Disadvantage.Add((ItemCatalog.Find(_loadout.BodyArmorId)?.Name ?? "armour") + " (stealth)");
            return r;
        }

        /// <summary>
        /// Phase 8: advantage and disadvantage on any roll target the engine knows ("attack:Strength",
        /// "save:Dexterity", "initiative" …), from features, conditions in effects, and armour training. Combat uses
        /// it for attack rolls; conditions on a combatant add their own sources on top.
        /// </summary>
        public RollMode RollFor(params string[] targets) => Roll(targets);

        /// <summary>Effects of a kind with each source counted once (the same feature never stacks with itself).</summary>
        private IEnumerable<SourcedEffect> Once(EffectKind kind)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var se in Of(kind))
                if (seen.Add(se.Source + "|" + se.Effect.Key)) yield return se;
        }

        // ----- Health, defence, movement --------------------------------------------------------------

        /// <summary>
        /// Maximum hit points: at level 1 the class hit die's maximum; each later level the die's average rounded up
        /// (die / 2 + 1, the fixed-value rule: no rolling); plus the Constitution modifier per level and per-level
        /// bonuses (dwarven toughness, draconic resilience); at least 1 per level.
        /// </summary>
        public Breakdown MaxHitPoints()
        {
            var b = new Breakdown();
            if (Class == null) return b.Add("no class", 1);
            var con = Modifier(Ability.Constitution);
            b.Add("d" + Class.HitDie + " maximum", Class.HitDie);
            if (Level > 1) b.Add("levels 2–" + Level + " (average " + (Class.HitDie / 2 + 1) + ")", (Class.HitDie / 2 + 1) * (Level - 1));
            b.Add("Constitution × " + Level, con * Level);
            foreach (var se in Once(EffectKind.MaxHpPerLevel)) b.Add(se.Source + " × " + Level, se.Effect.Value * Level);
            if (b.Total < Level) b.Add("minimum", Level - b.Total);
            return b;
        }

        public int MaxHp => MaxHitPoints().Total;

        public int HitDie => Class?.HitDie ?? 0;

        /// <summary>Walking speed: the species' speed plus every applicable bonus (conditions checked against the loadout).</summary>
        public Breakdown SpeedBreakdown()
        {
            var b = new Breakdown().Add(Species?.Name ?? "base", Species?.Speed ?? 30);
            foreach (var se in Once(EffectKind.SpeedBonus))
            {
                var v = se.Effect.Value;
                if (se.Effect.Key != null && se.Effect.Key.StartsWith("scale:", StringComparison.Ordinal)) v = Scaling(se.Effect.Key.Substring(6));
                if (v != 0) b.Add(se.Source, v);
            }

            if (ArmorTooHeavy) b.Add($"{ItemCatalog.Find(_loadout.BodyArmorId)?.Name ?? "armour"} (Strength below {_loadout.BodyArmor.StrengthRequirement})", -10);
            // Phase 11 (SRD 5.2.1 carrying): more than the carrying capacity leaves a Speed of 5 ft.
            if (Encumbered) b.Add($"encumbered ({Carrying.Pounds(_loadout.CarriedGrams)} of {Carrying.Pounds(CarryingCapacityGrams)} lb)", 5 - b.Total);
            return b;
        }

        public int Speed => SpeedBreakdown().Total;

        /// <summary>Other movement ("climb 30 ft", "swim 30 ft").</summary>
        public IReadOnlyList<string> MovementModes
        {
            get
            {
                var list = new List<string>();
                foreach (var se in Of(EffectKind.MovementMode))
                {
                    var text = se.Effect.Key + " " + (se.Effect.Value > 0 ? se.Effect.Value : Speed) + " ft";
                    if (!list.Contains(text)) list.Add(text);
                }

                return list;
            }
        }

        /// <summary>Phase 11: carrying capacity in grams (SRD 5.2.1: Strength × 15 lb, scaled by Powerful Build and the like).</summary>
        public int CarryingCapacityGrams => Carrying.CapacityGrams(Score(Ability.Strength), CarryPercent);

        /// <summary>Phase 11: carrying more than the capacity (Speed 5 ft).</summary>
        public bool Encumbered => _loadout.CarriedGrams > CarryingCapacityGrams;

        public int CarryPercent
        {
            get
            {
                var p = 100;
                foreach (var se in Of(EffectKind.CarryCapacityPercent)) p = Math.Max(p, se.Effect.Value);
                return p;
            }
        }

        public int Darkvision
        {
            get
            {
                var d = 0;
                foreach (var se in Of(EffectKind.Darkvision)) d = Math.Max(d, se.Effect.Value);
                return d;
            }
        }

        /// <summary>Special senses with the best range of each ("blindsight 30 ft", "truesight 60 ft").</summary>
        public IReadOnlyList<(string sense, int range)> Senses
        {
            get
            {
                var best = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var se in Of(EffectKind.Sense))
                    if (!best.TryGetValue(se.Effect.Key, out var v) || se.Effect.Value > v) best[se.Effect.Key] = se.Effect.Value;
                var list = new List<(string, int)>();
                foreach (var pair in best) list.Add((pair.Key, pair.Value));
                list.Sort((a, b) => string.CompareOrdinal(a.Item1, b.Item1));
                return list;
            }
        }

        public int ShopPricePercent
        {
            get
            {
                var p = 0;
                foreach (var se in Once(EffectKind.ShopPricePercent)) p += se.Effect.Value;
                return p;
            }
        }

        /// <summary>The loadout a player's equipment gives: the suit of armour in the chest slot and a shield in the off hand.</summary>
        public static Loadout LoadoutOf(PlayerCharacterData player) =>
            player == null ? Loadout.Unarmored : LoadoutOf(player.Equipment, ItemWeights.CarriedGrams(player));

        /// <summary>Phase 15: a companion's loadout from their own item instances, by the same rule as the player's.</summary>
        public static Loadout LoadoutOf(NpcCharacterData companion) =>
            companion == null ? Loadout.Unarmored : companion.OwnsItems ? LoadoutOf(companion.Equipment, ItemWeights.CarriedGrams(companion)) : companion.Gear?.Loadout ?? Loadout.Unarmored;

        /// <summary>The loadout a set of equipped stacks gives (the player's or a companion's): armour, shield and worn magic.</summary>
        public static Loadout LoadoutOf(IDictionary<EquipmentSlot, ItemStackData> equipment, int carriedGrams)
        {
            equipment.TryGetValue(EquipmentSlot.Chest, out var body);
            equipment.TryGetValue(EquipmentSlot.OffHand, out var shield);

            // Phase 11 (ADR-0052): worn magic items' effects, only those that apply now (attuned where it is required, not broken).
            var effects = new List<(string Source, RuleEffect Effect)>();
            foreach (var pair in equipment)
            {
                var stack = pair.Value;
                var def = ItemCatalog.Find(stack?.ItemId);
                var m = def?.Magic;
                if (m == null || (m.RequiresAttunement && !stack.Attuned) || ItemConditions.Unusable(stack.Condition)) continue;
                if (m.AcBonus != 0) effects.Add((def.Name, new RuleEffect(EffectKind.ArmorClassBonus, value: m.AcBonus)));
                if (m.SaveBonus != 0) effects.Add((def.Name, new RuleEffect(EffectKind.SaveBonus, value: m.SaveBonus)));
                foreach (var e in m.Effects) effects.Add((def.Name, e));
            }

            return new Loadout(body?.ItemId, shield?.ItemId, body?.Condition ?? ItemCondition.Good, shield?.Condition ?? ItemCondition.Good, carriedGrams, effects);
        }

        /// <summary>The armour training the character has ("light", "medium", "heavy", "shields").</summary>
        public bool TrainedIn(string training)
        {
            foreach (var a in ArmorProficiencies) if (a == training) return true;
            return false;
        }

        /// <summary>True when the worn suit of armour is one the character is not trained in (SRD 5.2.1 Armor Training).</summary>
        public bool UntrainedArmor => _loadout.BodyArmor != null && !TrainedIn(_loadout.BodyArmor.Training);

        /// <summary>True when the worn armour's Strength requirement is not met (speed −10 ft).</summary>
        /// <summary>Phase 9: wearing heavy armour (no rage in it).</summary>
        public bool HeavyArmorWorn => _loadout.BodyArmor != null && _loadout.BodyArmor.Category == Realmkeeper.Domain.World.ArmorCategory.Heavy;

        public bool ArmorTooHeavy => _loadout.BodyArmor != null && _loadout.BodyArmor.StrengthRequirement > 0 && Score(Ability.Strength) < _loadout.BodyArmor.StrengthRequirement;

        /// <summary>
        /// Armour class (Phase 8; SRD 5.2.1). Exactly one base formula applies:
        /// <list type="bullet">
        /// <item>a suit of armour: its base AC plus Dexterity up to its cap (none for heavy armour);</item>
        /// <item>no armour: 10 + Dexterity, or the best unarmoured defence (10 + Dexterity + an ability), whichever is
        /// higher (a monk's needs no shield);</item>
        /// </list>
        /// plus a shield's +2 when trained with shields (none otherwise), plus armour-class bonuses whose
        /// conditions hold (Defense). Restrictions — untrained armour, an unmet Strength requirement, stealth
        /// disadvantage — are listed as notes. Accessory pieces (bracers, leggings, pauldrons, caps) add nothing.
        /// </summary>
        public Breakdown ArmorClass(PlayerCharacterData player)
        {
            var loadout = player != null ? LoadoutOf(player) : _loadout;
            var dex = Modifier(Ability.Dexterity);
            var b = new Breakdown();
            var body = ItemConditions.Unusable(loadout.BodyCondition) ? null : loadout.BodyArmor; // Phase 11: broken armour gives no AC
            if (loadout.BodyArmor != null && body == null) b.Note((ItemCatalog.Find(loadout.BodyArmorId)?.Name ?? "armour") + " is broken: it gives no AC until repaired");
            if (body != null)
            {
                var name = ItemCatalog.Find(loadout.BodyArmorId)?.Name ?? "armour";
                b.Add(name + " (" + body.Category.ToString().ToLowerInvariant() + ")", body.BaseAc);
                if (ItemConditions.Penalty(loadout.BodyCondition) > 0) b.Add(name + " (damaged)", -ItemConditions.Penalty(loadout.BodyCondition));
                var dexPart = body.DexCap < 0 ? dex : Math.Min(dex, body.DexCap);
                if (body.DexCap == 0) b.Note("Dexterity does not apply to heavy armour");
                else b.Add("Dexterity" + (body.DexCap > 0 && dex > body.DexCap ? $" (max {body.DexCap})" : string.Empty), dexPart);
                if (!TrainedIn(body.Training)) b.Note($"untrained in {body.Training} armour: disadvantage on Strength and Dexterity rolls, and no spellcasting");
                if (body.StrengthRequirement > 0 && Score(Ability.Strength) < body.StrengthRequirement) b.Note($"Strength below {body.StrengthRequirement}: speed −10 ft");
                if (body.StealthDisadvantage) b.Note("disadvantage on Stealth");
            }
            else
            {
                var best = 0;
                string from = null;
                foreach (var se in _effects)
                {
                    if (se.Effect.Kind != EffectKind.UnarmoredDefense) continue;
                    if (!Enum.TryParse<Ability>(se.Effect.Key, out var a)) continue;
                    if (a == Ability.Wisdom && loadout.Shield) continue; // a monk's defence needs both hands free of shields
                    var m = Modifier(a);
                    if (m > best) { best = m; from = a + " (" + se.Source + ")"; }
                }

                b.Add(from != null ? "unarmoured defence: 10" : "no armour: 10", 10).Add("Dexterity", dex);
                if (from != null) b.Add(from, best);
            }

            var shield = loadout.ShieldArmor;
            if (shield != null && ItemConditions.Unusable(loadout.ShieldCondition)) { b.Note((ItemCatalog.Find(loadout.ShieldId)?.Name ?? "shield") + " is broken: it gives no AC until repaired"); shield = null; }
            if (shield != null)
            {
                if (TrainedIn("shields") && ItemConditions.Penalty(loadout.ShieldCondition) > 0) b.Add((ItemCatalog.Find(loadout.ShieldId)?.Name ?? "shield") + " (damaged)", -ItemConditions.Penalty(loadout.ShieldCondition));
                if (TrainedIn("shields")) b.Add(ItemCatalog.Find(loadout.ShieldId)?.Name ?? "shield", shield.BaseAc);
                else b.Note("untrained with shields: the shield gives no AC");
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var se in _effects)
                if (se.Effect.Kind == EffectKind.ArmorClassBonus && loadout.Allows(se.Effect.Condition) && seen.Add(se.Source))
                    b.Add(se.Source, se.Effect.Value);
            return b;
        }

        // ----- Proficiencies, senses, languages, features ---------------------------------------------

        public IReadOnlyList<string> Keys(EffectKind kind)
        {
            var list = new List<string>();
            foreach (var se in Of(kind)) if (se.Effect.Key != null && !list.Contains(se.Effect.Key)) list.Add(se.Effect.Key);
            return list;
        }

        public IReadOnlyList<string> Languages => Keys(EffectKind.Language);
        public IReadOnlyList<string> ToolProficiencies => Keys(EffectKind.ToolProficiency);
        public IReadOnlyList<string> WeaponProficiencies => Keys(EffectKind.WeaponProficiency);
        public IReadOnlyList<string> ArmorProficiencies => Keys(EffectKind.ArmorProficiency);
        public IReadOnlyList<string> Resistances => Keys(EffectKind.Resistance);
        public IReadOnlyList<string> SaveAdvantages => Keys(EffectKind.SaveAdvantage);
        public IReadOnlyList<string> FightingStyles => Keys(EffectKind.FightingStyle);

        /// <summary>Active class, subclass, species and background features (replaced ones removed), in level order.</summary>
        public IReadOnlyList<FeatureEntry> ActiveFeatures => _features;

        /// <summary>Every feature the character has, with where it comes from: active features, chosen options and feats.</summary>
        public IReadOnlyList<(string source, FeatureDefinition feature)> Features
        {
            get
            {
                var list = new List<(string, FeatureDefinition)>();
                foreach (var f in _features) if (!IsChoiceSlot(f.Feature)) list.Add((f.Source, f.Feature));
                foreach (var f in _choiceFeatures) list.Add((f.Source, f.Feature));
                return list;
            }
        }

        /// <summary>
        /// A placeholder feature that only announces a choice (a feat level, the Epic Boon, the subclass level). The
        /// sheet lists what was chosen instead, so these stay out of <see cref="Features"/>.
        /// </summary>
        public static bool IsChoiceSlot(FeatureDefinition f) =>
            f.Effects.Count == 0 && (f.Id.StartsWith("feat_", StringComparison.Ordinal) || f.Id == "epic_boon" || f.Id == "subclass");

        public bool HasFeature(string id)
        {
            foreach (var f in _features) if (f.Feature.Id == id) return true;
            return false;
        }

        /// <summary>The systems a feature waits for (its own declaration plus what its effects imply).</summary>
        public static IntegrationNeed Needs(FeatureDefinition f) => f.Needs | NeedsOf(f.Effects);

        /// <summary>A class table column at the character's level (e.g. sneak attack dice), or 0.</summary>
        public int Scaling(string id) => Class?.FindScaling(id)?.At(Level) ?? 0;

        public IReadOnlyList<(ScalingDefinition scaling, string text)> Scalings
        {
            get
            {
                var list = new List<(ScalingDefinition, string)>();
                if (Class != null) foreach (var s in Class.Scalings) list.Add((s, s.Text(Level)));
                return list;
            }
        }

        // ----- Resources ----------------------------------------------------------------------------

        /// <summary>Resources available at the character's level: species, class, subclass, and hit dice.</summary>
        public IReadOnlyList<ResourceDefinition> ResourceDefinitions
        {
            get
            {
                var list = new List<ResourceDefinition>();
                if (Species != null) foreach (var r in Species.Resources) if (r.FirstLevel <= Level) list.Add(r);
                if (Class != null)
                {
                    foreach (var r in Class.Resources) if (r.FirstLevel <= Level) list.Add(r);
                    if (Subclass != null) foreach (var r in Subclass.Resources) if (r.FirstLevel <= Level) list.Add(r);
                    if (IsComplete) list.Add(RpgCatalog.HitDice);
                }

                return list;
            }
        }

        public ResourceDefinition FindResource(string id)
        {
            foreach (var r in ResourceDefinitions) if (r.Id == id) return r;
            return null;
        }

        public int ResourceMax(ResourceDefinition r)
        {
            switch (r.Formula)
            {
                case ResourceFormula.Fixed: return r.Value;
                case ResourceFormula.AbilityModifierMin1: return Math.Max(1, Modifier(r.Ability));
                case ResourceFormula.OnePlusAbilityModifier: return Math.Max(1, 1 + Modifier(r.Ability));
                case ResourceFormula.PerLevel: return r.Value * Level;
                case ResourceFormula.ProficiencyBonus: return ProficiencyBonus;
                case ResourceFormula.Table: return r.Table != null && r.Table.Count >= Level ? r.Table[Level - 1] : 0;
                default: return 0;
            }
        }

        /// <summary>How the resource returns at the character's level (Font of Inspiration adds short rests).</summary>
        public ResourceRecovery RecoveryOf(ResourceDefinition r) => r.RecoveryAt(Level);

        /// <summary>The character's maximums by resource ID.</summary>
        public IReadOnlyDictionary<string, int> ResourceMaximums()
        {
            var map = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var r in ResourceDefinitions) map[r.Id] = ResourceMax(r);
            return map;
        }

        // ----- Spellcasting -------------------------------------------------------------------------

        public Breakdown SpellSaveDc()
        {
            if (Class?.Spellcasting == null) return null;
            var a = Class.Spellcasting.Ability;
            return new Breakdown().Add("base", 8).Add("proficiency", ProficiencyBonus).Add(a.ToString(), Modifier(a));
        }

        public Breakdown SpellAttack()
        {
            if (Class?.Spellcasting == null) return null;
            var a = Class.Spellcasting.Ability;
            return new Breakdown().Add("proficiency", ProficiencyBonus).Add(a.ToString(), Modifier(a));
        }

        public int CantripsKnown
        {
            get
            {
                var n = Class?.Spellcasting?.CantripsAt(Level) ?? 0;
                foreach (var se in Of(EffectKind.InnateMagic)) if (se.Effect.Key == "extra-cantrip") n += Math.Max(1, se.Effect.Value);
                return n;
            }
        }

        public int PreparedSpells => Class?.Spellcasting?.PreparedAt(Level) ?? 0;

        /// <summary>Spell slots at the character's level ("4 × 1st, 3 × 2nd" or "2 pact slots of level 3").</summary>
        public string SpellSlotText
        {
            get
            {
                var sc = Class?.Spellcasting;
                if (sc == null) return string.Empty;
                if (sc.Progression == CasterProgression.Pact)
                    return $"{ResourceMax(FindResource("pact_slots") ?? RpgCatalog.HitDice)} pact slots of level {Scaling("pact_slot_level")}";
                var parts = new List<string>();
                for (var slot = 1; slot <= 9; slot++)
                {
                    var n = RpgCatalog.SlotsAt(sc.Progression, Level, slot);
                    if (n > 0) parts.Add(n + " × " + (slot == 1 ? "1st" : slot == 2 ? "2nd" : slot == 3 ? "3rd" : slot + "th"));
                }

                return string.Join(", ", parts);
            }
        }

        // ----- Prerequisites ------------------------------------------------------------------------

        /// <summary>Whether a prerequisite holds for a choice made at <paramref name="level"/>, given the picks so far in its pool.</summary>
        public bool Meets(Prerequisite p, int level, ICollection<string> poolPicks)
        {
            switch (p.Kind)
            {
                case PrerequisiteKind.MinLevel: return level >= p.Value;
                case PrerequisiteKind.AbilityAtLeast:
                    foreach (var a in p.Abilities) if (Score(a) >= p.Value) return true;
                    return false;
                case PrerequisiteKind.HasFeature: return HasFeature(p.Key);
                case PrerequisiteKind.PoolPick: return poolPicks != null && poolPicks.Contains(p.Key);
                case PrerequisiteKind.Spellcasting: return Class?.Spellcasting != null;
                case PrerequisiteKind.FightingStyleFeature: return Class != null && Class.FightingStyleLevel > 0 && level >= Class.FightingStyleLevel;
                case PrerequisiteKind.NotFightingStyle:
                    foreach (var s in FightingStyles) if (s == p.Key) return false;
                    return true;
                default: return false;
            }
        }

        /// <summary>The unmet prerequisites, described ("level 5+", "Strength or Dexterity 13+"); empty when all hold.</summary>
        public IReadOnlyList<string> Unmet(IReadOnlyList<Prerequisite> prerequisites, int level, ICollection<string> poolPicks)
        {
            var list = new List<string>();
            foreach (var p in prerequisites) if (!Meets(p, level, poolPicks)) list.Add(CharacterProgression.Describe(p));
            return list;
        }

        public string ToolName(string id) => RpgCatalog.FindTool(id)?.Name ?? id;
        public string LanguageName(string id) => RpgCatalog.FindLanguage(id)?.Name ?? id;
    }
}
