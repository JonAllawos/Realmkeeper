using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Calendar;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Character
{
    /// <summary>
    /// Phase 20: measured costs of the character work the interface asks for on each change (a creator redraw evaluates
    /// the draft's rules; the sheet and the build panel read breakdowns; inspection builds a person's known view; a level-up
    /// is validated and applied). Generous bounds catch pathological regressions; the figures go to the test output.
    /// </summary>
    public sealed class Phase20PerformanceTests
    {
        [Test]
        public void TheCharacterWork_IsMeasured()
        {
            var d = new CharacterDraft();
            d.Choices.Name = "Measure";
            d.SetSpecies("tiefling");
            d.Toggle("species:fiendish_legacy", "infernal");
            d.SetBackground("criminal");
            d.SetClass("wizard");

            // One creator redraw: the draft's rules, the build panel's breakdowns and every step's problems.
            var sw = Stopwatch.StartNew();
            const int redraws = 200;
            for (var i = 0; i < redraws; i++)
            {
                var r = d.Rules;
                foreach (var a in AbilityRules.All) { _ = r.ScoreBreakdown(a).Text; _ = r.SaveBonus(a).Text; }
                foreach (var s in SkillRules.All) _ = r.SkillBonus(s).Text;
                _ = r.MaxHitPoints().Text;
                _ = r.Features.Count;
                foreach (CreationStep step in CharacterDraft.Steps) _ = d.Problems(step).Count;
            }

            sw.Stop();
            var redrawMs = sw.Elapsed.TotalMilliseconds / redraws;

            // A known person, for every named person in a generated world.
            var w = Phase2Worlds.Generated(7);
            NpcCharacterGenesis.Ensure(w);
            var people = w.Npcs.Where(n => n.IsAlive).ToList();
            sw.Restart();
            foreach (var n in people) KnownPersonQueries.View(w, n);
            sw.Stop();
            var knownMs = sw.Elapsed.TotalMilliseconds / people.Count;

            // A sheet's rules from a saved record (what opening the sheet or a companion's detail costs).
            var sheets = people.Where(n => n.Character?.Sheet != null).Select(n => n.Character.Sheet).ToList();
            sw.Restart();
            foreach (var s in sheets) { var r = new CharacterRules(s); _ = r.MaxHp; _ = r.Features.Count; }
            sw.Stop();
            var sheetMs = sw.Elapsed.TotalMilliseconds / sheets.Count;

            // A level-up: suggestion, validation and the transactional apply, from level 1 to 5 (a fighter, choices suggested by the rules).
            var f = new CharacterDraft();
            f.Choices.Name = "Measure";
            f.SetSpecies("human");
            f.SetBackground("soldier");
            f.SetClass("fighter");
            f.Toggle("species:skillful", nameof(Skill.Insight));
            f.Toggle(ClassDefinition.SkillChoiceId, nameof(Skill.Perception));
            f.Toggle(ClassDefinition.SkillChoiceId, nameof(Skill.Survival));
            f.Toggle("class:fighting_style", "archery");
            var sheet = CharacterCommandHandlers.BuildSheet(f.Clean(), out var built);
            Assert.That(sheet, Is.Not.Null, string.Join("; ", built));
            sheet.Experience = ProgressionRules.ExperienceFor(5);
            sheet.ExperienceLog.Add(new ExperienceAward { Key = "measure", Source = ExperienceSource.Milestone, Amount = sheet.Experience, Date = new WorldDate(412, 3, 1), Reason = "measurement" });
            sw.Restart();
            var levels = 0;
            while (CharacterAdvancement.CanAdvance(sheet))
            {
                var draft = CharacterAdvancement.SuggestChoices(sheet, sheet.Level + 1, c => null);
                var next = CharacterAdvancement.Apply(sheet, new[] { draft }, new WorldDate(412, 3, 1), out var errors);
                Assert.That(next, Is.Not.Null, string.Join("; ", errors));
                sheet = next;
                levels++;
            }

            sw.Stop();
            var levelMs = levels == 0 ? 0 : sw.Elapsed.TotalMilliseconds / levels;

            TestContext.WriteLine($"Creator redraw (rules, breakdowns, problems): {redrawMs:0.000} ms. Known person: {knownMs:0.000} ms each over {people.Count}. " +
                                  $"Sheet rules from a record: {sheetMs:0.000} ms each over {sheets.Count}. Level-up (suggest, validate, apply): {levelMs:0.00} ms each over {levels}.");
            Assert.That(levels, Is.EqualTo(4));
            Assert.That(redrawMs, Is.LessThan(20));
            Assert.That(knownMs, Is.LessThan(5));
            Assert.That(sheetMs, Is.LessThan(5));
            Assert.That(levelMs, Is.LessThan(50));
        }
    }
}
