using System;
using System.Collections.Generic;
using Realmkeeper.Domain.Magic;

namespace Realmkeeper.Domain.Rpg
{
    /// <summary>
    /// Phase 9 (rules version 8): spell picks as ordinary choices, so the creator, the level-up window, atomic
    /// advancement and validation handle them unchanged (ADR-0038). Cantrips for every class with cantrips; known spells
    /// for the bard, sorcerer and warlock; the wizard's spellbook. Prepared lists (cleric, druid, paladin, ranger, and
    /// the wizard from the book) are not choices: they live in <see cref="SpellcastingData"/>. Picks are "up to" the
    /// class table: the catalog may hold fewer spells than the table allows, and open picks can be made later.
    /// </summary>
    public static partial class RpgCatalog
    {
        public const int SpellChoicesIntroducedInRules = 8;

        /// <summary>How a class keeps its spells.</summary>
        public static SpellKeeping KeepingOf(ClassDefinition c)
        {
            if (c?.Spellcasting == null) return SpellKeeping.None;
            switch (c.Id)
            {
                case "bard":
                case "sorcerer":
                case "warlock": return SpellKeeping.Known;
                case "wizard": return SpellKeeping.Spellbook;
                case "paladin":
                case "ranger": return SpellKeeping.PreparedOneSwap;
                default: return SpellKeeping.Prepared;
            }
        }

        /// <summary>The class level at which a class can first cast spells of a level (0 when it never can by slots).</summary>
        public static int ClassLevelForSpellLevel(SpellcastingDefinition sc, int spellLevel)
        {
            if (sc == null) return 0;
            if (spellLevel <= 0) return 1;
            switch (sc.Progression)
            {
                case CasterProgression.Full: return spellLevel <= 9 ? 2 * spellLevel - 1 : 0;
                case CasterProgression.Half: return spellLevel <= 5 ? 4 * spellLevel - 3 : 0;
                case CasterProgression.Pact: return spellLevel <= 5 ? 2 * spellLevel - 1 : 0;
                default: return 0;
            }
        }

        /// <summary>The highest spell level a class casts by slots at a class level (0 = none).</summary>
        public static int MaxSpellLevel(SpellcastingDefinition sc, int classLevel)
        {
            var max = 0;
            for (var l = 1; l <= 9; l++)
            {
                var need = ClassLevelForSpellLevel(sc, l);
                if (need > 0 && classLevel >= need) max = l;
            }

            return max;
        }

        private static ChoiceOption SpellOption(SpellDefinition s, SpellcastingDefinition sc)
        {
            var tag = s.Status == ImplementationStatus.Functional ? string.Empty :
                s.Status == ImplementationStatus.Partial ? " (partly simulated)" :
                s.Status == ImplementationStatus.DataOnly ? " (no effect yet)" : " (not castable yet)";
            var o = new ChoiceOption(s.Id, s.Name + tag, s.LevelText + ". " + s.Description + (s.StatusNote != null ? " — " + s.StatusNote : string.Empty))
            {
                Needs = IntegrationNeed.Magic,
            };
            var level = ClassLevelForSpellLevel(sc, s.Level);
            if (level > 1) o.Prerequisites = new[] { Prerequisite.Level(level) };
            return o;
        }

        private static List<ChoiceOption> SpellOptions(ClassDefinition c, bool cantrips)
        {
            var list = new List<ChoiceOption>();
            foreach (var s in SpellCatalog.All)
            {
                if (!SpellCatalog.OnList(s, c.Id) || s.IsCantrip != cantrips) continue;
                if (!cantrips && ClassLevelForSpellLevel(c.Spellcasting, s.Level) == 0) continue;
                list.Add(SpellOption(s, c.Spellcasting));
            }

            return list;
        }

        /// <summary>Adds the spell choices to a caster's level-1 and level choices (after its progression is applied).</summary>
        private static void AddSpellChoices(ClassDefinition c)
        {
            var sc = c.Spellcasting;
            if (sc == null) return;
            var first = new List<ChoiceDefinition>(c.Choices);
            var levels = new List<ChoiceDefinition>(c.LevelChoices);
            var keeping = KeepingOf(c);

            ChoiceDefinition Pick(string id, int level, string name, string prompt, int count, string pool, IReadOnlyList<ChoiceOption> options) => new ChoiceDefinition
            {
                Id = id, Level = level, Name = name, Prompt = prompt, Kind = ChoiceKind.Option, Count = count, Pool = pool, Options = options, UpTo = true,
                IntroducedInRules = SpellChoicesIntroducedInRules,
            };

            if (sc.Cantrips != null && sc.CantripsAt(1) > 0)
            {
                var options = SpellOptions(c, true);
                first.Add(Pick("class:cantrips", 1, "Cantrips", $"Choose up to {sc.CantripsAt(1)} cantrips.", sc.CantripsAt(1), "cantrips", options));
                for (var level = 2; level <= 20; level++)
                {
                    var more = sc.CantripsAt(level) - sc.CantripsAt(level - 1);
                    if (more > 0) levels.Add(Pick($"lv{level}:cantrips", level, "Cantrips", $"Choose up to {more} more cantrip{(more == 1 ? string.Empty : "s")}.", more, "cantrips", options));
                }
            }

            if (keeping == SpellKeeping.Known)
            {
                var options = SpellOptions(c, false);
                first.Add(Pick("class:spells", 1, "Spells", $"Choose up to {sc.PreparedAt(1)} spells you know.", sc.PreparedAt(1), "spells", options));
                for (var level = 2; level <= 20; level++)
                {
                    var more = sc.PreparedAt(level) - sc.PreparedAt(level - 1);
                    if (more > 0) levels.Add(Pick($"lv{level}:spells", level, "Spells", $"Choose up to {more} more spell{(more == 1 ? string.Empty : "s")} you know.", more, "spells", options));
                }
            }
            else if (keeping == SpellKeeping.Spellbook)
            {
                var options = SpellOptions(c, false);
                first.Add(Pick("class:spellbook", 1, "Spellbook", "Write up to six level-1 wizard spells in your spellbook.", 6, "spellbook", options));
                for (var level = 2; level <= 20; level++)
                    levels.Add(Pick($"lv{level}:spellbook", level, "Spellbook", "Add up to two wizard spells you can cast to your spellbook.", 2, "spellbook", options));
            }

            c.Choices = first;
            c.LevelChoices = levels;
        }

        /// <summary>Spells always prepared by a feature (never counted against the prepared limit), by class level.</summary>
        public static IEnumerable<string> AlwaysPrepared(CharacterRules rules)
        {
            if (rules?.Class == null) yield break;
            if (rules.HasFeature("paladins_smite")) yield return "divine_smite";
            if (rules.HasFeature("favored_enemy")) yield return "hunters_mark";
            if (rules.Subclass == null) yield break;
            var level = rules.Level;
            switch (rules.Subclass.Id)
            {
                case "life":
                    foreach (var s in new[] { "aid", "bless", "cure_wounds", "lesser_restoration" }) if (level >= 3) yield return s;
                    if (level >= 5) yield return "revivify";
                    if (level >= 9) yield return "mass_cure_wounds";
                    break;
                case "devotion":
                    if (level >= 3) yield return "shield_of_faith";
                    if (level >= 5) yield return "aid";
                    if (level >= 9) yield return "dispel_magic";
                    break;
                case "fiend":
                    if (level >= 3) yield return "burning_hands";
                    if (level >= 5) yield return "scorching_ray";
                    if (level >= 7) yield return "fireball";
                    break;
            }
        }
    }

    /// <summary>How a class keeps its spells (SRD 5.2.1).</summary>
    public enum SpellKeeping
    {
        None = 0,

        /// <summary>Prepared from the whole class list; the list changes after a long rest (cleric, druid).</summary>
        Prepared = 1,

        /// <summary>Prepared from the class list; one spell can be swapped after a long rest (paladin, ranger).</summary>
        PreparedOneSwap = 2,

        /// <summary>Chosen at creation and level-up (bard, sorcerer, warlock).</summary>
        Known = 3,

        /// <summary>Written in a spellbook at creation and level-up; prepared from the book after a long rest (wizard).</summary>
        Spellbook = 4,
    }
}
