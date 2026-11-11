using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Character
{
    /// <summary>
    /// Phase 7 costs, measured on a level-20 character (the most choices, features and validation work): rules
    /// evaluation, full sheet derivation, sheet validation (every level's choices), planning an advancement, an
    /// experience award and a level-up through the session (each including whole-world validation), and saving and
    /// loading. Ceilings are generous; the numbers are printed for the report.
    /// </summary>
    public sealed class ProgressionPerformanceTests
    {
        [Test]
        public void Progression_IsCheap()
        {
            var sheet = CharacterCommandHandlers.BuildSheet(ProgressionTests.Choices("warlock"), out _);
            sheet.ExperienceLog.Add(new ExperienceAward { Key = "all", Amount = ProgressionRules.ExperienceFor(20) });
            sheet.Experience = ProgressionRules.ExperienceFor(20);
            for (var level = 2; level <= 20; level++)
                sheet = CharacterAdvancement.Apply(sheet, new[] { CharacterAdvancement.SuggestChoices(sheet, level) }, default, out _);
            Assert.That(sheet.Level, Is.EqualTo(20));

            const int N = 500;
            var evaluate = Stopwatch.StartNew();
            for (var i = 0; i < N; i++) Assert.That(new CharacterRules(sheet).Level, Is.EqualTo(20));
            evaluate.Stop();

            var player = new PlayerCharacterData { Rpg = sheet };
            var derive = Stopwatch.StartNew();
            var sum = 0;
            for (var i = 0; i < N; i++)
            {
                var rules = new CharacterRules(sheet);
                foreach (var s in SkillRules.All) sum += rules.SkillBonus(s).Total + rules.SkillRoll(s).Advantage.Count;
                foreach (var a in AbilityRules.All) sum += rules.SaveBonus(a).Total;
                sum += rules.MaxHp + rules.ArmorClass(player).Total + rules.Speed + rules.Initiative().Total + rules.PassivePerception + rules.Features.Count;
                foreach (var pair in rules.ResourceMaximums()) sum += pair.Value;
            }

            derive.Stop();
            Assert.That(sum, Is.Not.Zero);

            var validate = Stopwatch.StartNew();
            for (var i = 0; i < N / 5; i++) Assert.That(CharacterValidation.CheckSheet(sheet), Is.Empty);
            validate.Stop();

            // Planning a choice-heavy level: warlock 4 → 5 (two invocations, with level and pact prerequisites).
            var four = CharacterCommandHandlers.BuildSheet(ProgressionTests.Choices("warlock"), out _);
            four.ExperienceLog.Add(new ExperienceAward { Key = "all", Amount = ProgressionRules.ExperienceFor(5) });
            four.Experience = ProgressionRules.ExperienceFor(5);
            for (var level = 2; level <= 4; level++) four = CharacterAdvancement.Apply(four, new[] { CharacterAdvancement.SuggestChoices(four, level) }, default, out _);
            var plan = Stopwatch.StartNew();
            for (var i = 0; i < N / 5; i++) Assert.That(CharacterAdvancement.Preview(four, CharacterAdvancement.SuggestChoices(four, 5)).Choices.Count, Is.GreaterThan(0));
            plan.Stop();

            var dir = Phase2Worlds.TempDirectory();
            try
            {
                var session = Phase2Worlds.Session(dir);
                Assert.That(session.Dispatch(new CreateCharacterCommand(ProgressionTests.Choices("fighter"))).IsSuccess, Is.True);
                var award = Stopwatch.StartNew();
                for (var i = 0; i < 20; i++) Assert.That(session.Dispatch(new AwardExperienceCommand(1000, ExperienceSource.Encounter)).IsSuccess, Is.True);
                award.Stop();
                var levelUp = Stopwatch.StartNew();
                var levels = 0;
                while (session.GetCharacter().CanAdvance)
                {
                    var current = session.CurrentWorldSnapshotForTests().Player.Rpg;
                    Assert.That(session.Dispatch(new AdvanceCharacterCommand(CharacterAdvancement.SuggestChoices(current, current.Level + 1))).IsSuccess, Is.True);
                    levels++;
                }

                levelUp.Stop();

                var world = Phase2Worlds.Generated(777);
                world.Player.Rpg = sheet;
                world.Player.Species = "Human";
                world.Player.Background = "Hermit";
                var serializer = new JsonWorldSerializer();
                serializer.Serialize(world);
                var save = Stopwatch.StartNew();
                var json = serializer.Serialize(world);
                save.Stop();
                var load = Stopwatch.StartNew();
                var back = serializer.Deserialize(json);
                load.Stop();
                Assert.That(back.IsSuccess, Is.True, string.Join("; ", back.Errors));

                var perEvaluate = evaluate.Elapsed.TotalMilliseconds / N;
                var perDerive = derive.Elapsed.TotalMilliseconds / N;
                var perValidate = validate.Elapsed.TotalMilliseconds / (N / 5);
                var perPlan = plan.Elapsed.TotalMilliseconds / (N / 5);
                var perAward = award.Elapsed.TotalMilliseconds / 20;
                var perLevel = levelUp.Elapsed.TotalMilliseconds / levels;
                TestContext.Out.WriteLine(
                    $"PERF7 level-20 rules evaluation {perEvaluate * 1000:F0} µs | full sheet {perDerive * 1000:F0} µs | sheet validation (all 20 levels) {perValidate:F2} ms | " +
                    $"advancement suggestion + preview (warlock 4→5) {perPlan:F2} ms | XP award command {perAward:F2} ms | level-up command {perLevel:F2} ms (×{levels}) | " +
                    $"world save {save.Elapsed.TotalMilliseconds:F1} ms, load {load.Elapsed.TotalMilliseconds:F1} ms");
                Assert.That(perDerive, Is.LessThan(5.0), "Deriving a level-20 sheet takes well under a frame.");
                Assert.That(perValidate, Is.LessThan(20.0), "Validating a level-20 sheet stays well under a frame budget for a command.");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
