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

        public string Text
        {
            get
            {
                var parts = new List<string>();
                foreach (var (label, value) in _parts) parts.Add(label + " " + (value >= 0 ? "+" : string.Empty) + value.ToString(CultureInfo.InvariantCulture));
                return string.Join(", ", parts);
            }
        }

        public static string Signed(int v) => (v >= 0 ? "+" : string.Empty) + v.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>One effect and where it came from (for the sheet's explanations).</summary>
    public readonly struct SourcedEffect
    {
        public SourcedEffect(RuleEffect effect, string source)
        {
            Effect = effect;
            Source = source;
        }

        public RuleEffect Effect { get; }
        public string Source { get; }
    }

    /// <summary>
    /// Everything derived from a character's choices (Phase 6). One evaluator for all typed effects: it
    /// collects the effects of the species (and its chosen options), the background, the class (proficiencies,
    /// features and chosen options) and the creation choices, then answers questions: final scores,
    /// modifiers, proficiency, skills and saves with their breakdowns, maximum hit points, armour class,
    /// speed, senses, resistances, languages, resources. Nothing here is stored; nothing here mutates.
    /// </summary>
    public sealed class CharacterRules
    {
        private readonly CharacterSheetData _sheet;
        private readonly List<SourcedEffect> _effects = new List<SourcedEffect>();

        public CharacterRules(CharacterSheetData sheet)
        {
            _sheet = sheet ?? throw new ArgumentNullException(nameof(sheet));
            Species = RpgCatalog.FindSpecies(sheet.SpeciesId);
            Background = RpgCatalog.FindBackground(sheet.BackgroundId);
            Class = RpgCatalog.FindClass(sheet.ClassId);
            Collect();
        }

        public CharacterSheetData Sheet => _sheet;
        public SpeciesDefinition Species { get; }
        public BackgroundDefinition Background { get; }
        public ClassDefinition Class { get; }
        public bool IsComplete => _sheet.State == CreationState.Complete && Species != null && Background != null && Class != null;
        public IReadOnlyList<SourcedEffect> Effects => _effects;
        public int Level => Math.Max(1, _sheet.Level);
        public int ProficiencyBonus => AbilityRules.ProficiencyBonus(Level);

        private void Collect()
        {
            if (Species != null)
            {
                foreach (var t in Species.Traits) foreach (var e in t.Effects) _effects.Add(new SourcedEffect(e, Species.Name + ": " + t.Name));
                foreach (var l in Species.Languages) _effects.Add(new SourcedEffect(new RuleEffect(EffectKind.Language, l), Species.Name));
                AddChoices(Species.Choices, Species.Name);
            }

            if (Background != null)
            {
                foreach (var s in Background.Skills) _effects.Add(new SourcedEffect(new RuleEffect(EffectKind.SkillProficiency, s.ToString()), Background.Name));
                foreach (var t in Background.Tools) _effects.Add(new SourcedEffect(new RuleEffect(EffectKind.ToolProficiency, t), Background.Name));
                foreach (var l in Background.Languages) _effects.Add(new SourcedEffect(new RuleEffect(EffectKind.Language, l), Background.Name));
                if (Background.Feature != null)
                    foreach (var e in Background.Feature.Effects) _effects.Add(new SourcedEffect(e, Background.Name + ": " + Background.Feature.Name));
            }

            if (Class != null)
            {
                foreach (var a in Class.SavingThrows) _effects.Add(new SourcedEffect(new RuleEffect(EffectKind.SaveProficiency, a.ToString()), Class.Name));
                foreach (var a in Class.ArmorProficiencies) _effects.Add(new SourcedEffect(new RuleEffect(EffectKind.ArmorProficiency, a), Class.Name));
                foreach (var w in Class.WeaponProficiencies) _effects.Add(new SourcedEffect(new RuleEffect(EffectKind.WeaponProficiency, w), Class.Name));
                foreach (var t in Class.Tools) _effects.Add(new SourcedEffect(new RuleEffect(EffectKind.ToolProficiency, t), Class.Name));
                foreach (var f in Class.Features) foreach (var e in f.Effects) _effects.Add(new SourcedEffect(e, Class.Name + ": " + f.Name));
                foreach (var s in _sheet.Chosen(ClassDefinition.SkillChoiceId))
                    _effects.Add(new SourcedEffect(new RuleEffect(EffectKind.SkillProficiency, s), Class.Name + " (chosen)"));
                AddChoices(Class.Choices, Class.Name);
            }
        }

        private void AddChoices(IReadOnlyList<ChoiceDefinition> choices, string source)
        {
            foreach (var c in choices)
            {
                var picked = _sheet.Chosen(c.Id);
                switch (c.Kind)
                {
                    case ChoiceKind.Option:
                        foreach (var id in picked)
                            foreach (var o in c.Options)
                                if (o.Id == id)
                                    foreach (var e in o.Effects) _effects.Add(new SourcedEffect(e, source + ": " + c.Name + " (" + o.Name + ")"));
                        break;
                    case ChoiceKind.Skill:
                        foreach (var s in picked) _effects.Add(new SourcedEffect(new RuleEffect(EffectKind.SkillProficiency, s), source + ": " + c.Name));
                        break;
                    // Expertise is a rank on existing proficiencies, read by SkillRank.
                }
            }
        }

        private IEnumerable<RuleEffect> Of(EffectKind kind)
        {
            foreach (var se in _effects) if (se.Effect.Kind == kind) yield return se.Effect;
        }

        // ----- Abilities -------------------------------------------------------------------------------

        public int SpeciesBonus(Ability a) => Species != null && Species.AbilityBonuses.TryGetValue(a, out var v) ? v : 0;

        public int BaseScore(Ability a) => _sheet.BaseScores != null && _sheet.BaseScores.Length == 6 ? _sheet.BaseScores[(int)a] : 10;

        public int Score(Ability a) => Math.Min(AbilityRules.MaxScore, BaseScore(a) + SpeciesBonus(a));

        public int Modifier(Ability a) => AbilityRules.Modifier(Score(a));

        // ----- Skills and saves ------------------------------------------------------------------------

        public ProficiencyRank SkillRank(Skill skill)
        {
            var proficient = false;
            foreach (var e in Of(EffectKind.SkillProficiency)) if (e.Key == skill.ToString()) proficient = true;
            if (!proficient) return ProficiencyRank.None;
            if (Class != null)
                foreach (var c in Class.Choices)
                    if (c.Kind == ChoiceKind.Expertise)
                        foreach (var s in _sheet.Chosen(c.Id))
                            if (s == skill.ToString()) return ProficiencyRank.Expertise;
            return ProficiencyRank.Proficient;
        }

        /// <summary>Who grants the skill (for the sheet).</summary>
        public IReadOnlyList<string> SkillSources(Skill skill)
        {
            var list = new List<string>();
            foreach (var se in _effects) if (se.Effect.Kind == EffectKind.SkillProficiency && se.Effect.Key == skill.ToString()) list.Add(se.Source);
            return list;
        }

        public Breakdown SkillBonus(Skill skill)
        {
            var ability = SkillRules.AbilityOf(skill);
            var b = new Breakdown().Add(ability.ToString(), Modifier(ability));
            var rank = SkillRank(skill);
            if (rank == ProficiencyRank.Proficient) b.Add("proficiency", ProficiencyBonus);
            if (rank == ProficiencyRank.Expertise) b.Add("expertise", ProficiencyBonus * 2);
            return b;
        }

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
            foreach (var e in Of(EffectKind.SaveProficiency)) if (e.Key == a.ToString()) return true;
            return false;
        }

        public Breakdown SaveBonus(Ability a)
        {
            var b = new Breakdown().Add(a.ToString(), Modifier(a));
            if (SaveProficient(a)) b.Add("proficiency", ProficiencyBonus);
            return b;
        }

        public int PassivePerception => 10 + SkillBonus(Skill.Perception).Total;

        // ----- Health, defence, movement --------------------------------------------------------------

        /// <summary>
        /// Maximum hit points: at level 1 the class hit die's maximum; each later level the die's average
        /// rounded up (die / 2 + 1); plus the Constitution modifier per level and per-level bonuses; at least 1 per level.
        /// </summary>
        public Breakdown MaxHitPoints()
        {
            var b = new Breakdown();
            if (Class == null) return b.Add("no class", 1);
            var con = Modifier(Ability.Constitution);
            b.Add("d" + Class.HitDie + " maximum", Class.HitDie);
            if (Level > 1) b.Add("levels 2–" + Level + " (average)", (Class.HitDie / 2 + 1) * (Level - 1));
            b.Add("Constitution × " + Level, con * Level);
            var perLevel = 0;
            foreach (var e in Of(EffectKind.MaxHpPerLevel)) perLevel += e.Value;
            if (perLevel != 0) b.Add("species × " + Level, perLevel * Level);
            if (b.Total < Level) b.Add("minimum", Level - b.Total);
            return b;
        }

        public int MaxHp => MaxHitPoints().Total;

        public int Speed
        {
            get
            {
                var s = Species?.Speed ?? 30;
                foreach (var e in Of(EffectKind.SpeedBonus)) s += e.Value;
                return s;
            }
        }

        public int CarryPercent
        {
            get
            {
                var p = 100;
                foreach (var e in Of(EffectKind.CarryCapacityPercent)) p = Math.Max(p, e.Value);
                return p;
            }
        }

        public int Darkvision
        {
            get
            {
                var d = 0;
                foreach (var e in Of(EffectKind.Darkvision)) d = Math.Max(d, e.Value);
                return d;
            }
        }

        public int ShopPricePercent
        {
            get
            {
                var p = 0;
                foreach (var e in Of(EffectKind.ShopPricePercent)) p += e.Value;
                return p;
            }
        }

        /// <summary>
        /// Armour class (provisional until combat defines armour types): 10 + Dexterity, or the best unarmoured
        /// defence when no armour is worn, plus the protection of everything worn.
        /// </summary>
        public Breakdown ArmorClass(PlayerCharacterData player)
        {
            var b = new Breakdown().Add("base", 10).Add("Dexterity", Modifier(Ability.Dexterity));
            var armoured = false;
            var shield = false;
            var protection = 0;
            if (player != null)
            {
                foreach (var pair in player.Equipment)
                {
                    var d = ItemCatalog.Find(pair.Value.ItemId);
                    if (d == null) continue;
                    if (d.Category == ItemCategory.Armour && pair.Key == EquipmentSlot.Chest) armoured = true;
                    if (d.Category == ItemCategory.Shield) shield = true;
                    protection += d.Protection;
                }
            }

            if (!armoured)
            {
                var best = 0;
                string from = null;
                foreach (var se in _effects)
                {
                    if (se.Effect.Kind != EffectKind.UnarmoredDefense) continue;
                    if (!Enum.TryParse<Ability>(se.Effect.Key, out var a)) continue;
                    if (a == Ability.Wisdom && shield) continue; // a monk's defence needs both hands free of shields
                    var m = Modifier(a);
                    if (m > best) { best = m; from = a.ToString(); }
                }

                if (from != null) b.Add(from + " (unarmoured defence)", best);
            }

            if (protection != 0) b.Add("worn protection", protection);
            return b;
        }

        // ----- Proficiencies, senses, languages, features ---------------------------------------------

        public IReadOnlyList<string> Keys(EffectKind kind)
        {
            var list = new List<string>();
            foreach (var e in Of(kind)) if (e.Key != null && !list.Contains(e.Key)) list.Add(e.Key);
            return list;
        }

        public IReadOnlyList<string> Languages => Keys(EffectKind.Language);
        public IReadOnlyList<string> ToolProficiencies => Keys(EffectKind.ToolProficiency);
        public IReadOnlyList<string> WeaponProficiencies => Keys(EffectKind.WeaponProficiency);
        public IReadOnlyList<string> ArmorProficiencies => Keys(EffectKind.ArmorProficiency);
        public IReadOnlyList<string> Resistances => Keys(EffectKind.Resistance);
        public IReadOnlyList<string> SaveAdvantages => Keys(EffectKind.SaveAdvantage);

        /// <summary>Every feature the character has, with where it comes from.</summary>
        public IReadOnlyList<(string source, FeatureDefinition feature)> Features
        {
            get
            {
                var list = new List<(string, FeatureDefinition)>();
                if (Species != null)
                {
                    foreach (var t in Species.Traits) list.Add((Species.Name, t));
                    AddChosenOptions(list, Species.Choices, Species.Name);
                }

                if (Background?.Feature != null) list.Add((Background.Name, Background.Feature));
                if (Class != null)
                {
                    foreach (var f in Class.Features) list.Add((Class.Name + " 1", f));
                    AddChosenOptions(list, Class.Choices, Class.Name + " 1");
                }

                return list;
            }
        }

        /// <summary>A chosen option (Divine Order, Fighting Style, Draconic Ancestry) is shown as a feature of its own.</summary>
        private void AddChosenOptions(List<(string, FeatureDefinition)> list, IReadOnlyList<ChoiceDefinition> choices, string source)
        {
            foreach (var c in choices)
            {
                if (c.Kind != ChoiceKind.Option) continue;
                foreach (var id in _sheet.Chosen(c.Id))
                    foreach (var o in c.Options)
                        if (o.Id == id)
                            list.Add((source, new FeatureDefinition(c.Id + ":" + o.Id, c.Name + ": " + o.Name, o.Description, new List<RuleEffect>(o.Effects).ToArray())));
            }
        }

        public IReadOnlyList<ResourceDefinition> ResourceDefinitions
        {
            get
            {
                var list = new List<ResourceDefinition>();
                if (Species != null) list.AddRange(Species.Resources);
                if (Class != null) list.AddRange(Class.Resources);
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
                default: return 0;
            }
        }

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

        public string ToolName(string id) => RpgCatalog.FindTool(id)?.Name ?? id;
        public string LanguageName(string id) => RpgCatalog.FindLanguage(id)?.Name ?? id;
    }
}
