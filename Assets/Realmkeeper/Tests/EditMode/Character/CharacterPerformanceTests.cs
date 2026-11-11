using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Character
{
    /// <summary>
    /// Phase 6 costs: catalog validation, building and validating a character, deriving a full sheet (every skill,
    /// save, hit points, armour class), and saving and loading a world with a created character. Ceilings are
    /// generous; the numbers are printed for the report.
    /// </summary>
    public sealed class CharacterPerformanceTests
    {
        [Test]
        public void CharacterRules_AreCheap()
        {
            var catalog = Stopwatch.StartNew();
            var catalogErrors = RpgCatalog.Validate();
            catalog.Stop();
            Assert.That(catalogErrors, Is.Empty);

            var d = new CharacterDraft();
            d.SetBackground("soldier");
            d.SetClass("fighter");
            d.Toggle(ClassDefinition.SkillChoiceId, nameof(Skill.Perception));
            d.Toggle(ClassDefinition.SkillChoiceId, nameof(Skill.Survival));
            d.Toggle("species:skillful", nameof(Skill.Insight));
            d.Toggle("class:fighting_style", "defense");
            var choices = d.Clean();

            const int N = 2000;
            var validate = Stopwatch.StartNew();
            for (var i = 0; i < N; i++) Assert.That(CharacterCommandHandlers.Check(null, choices), Is.Empty);
            validate.Stop();

            var sheet = CharacterCommandHandlers.BuildSheet(choices, out _);
            var player = new PlayerCharacterData { Rpg = sheet };
            var derive = Stopwatch.StartNew();
            var sum = 0;
            for (var i = 0; i < N; i++)
            {
                var rules = new CharacterRules(sheet);
                foreach (var s in SkillRules.All) sum += rules.SkillBonus(s).Total;
                foreach (var a in AbilityRules.All) sum += rules.SaveBonus(a).Total;
                sum += rules.MaxHp + rules.ArmorClass(player).Total + rules.Speed;
            }

            derive.Stop();
            Assert.That(sum, Is.Not.Zero);

            var world = Phase2Worlds.Generated(777);
            world.Player.Rpg = sheet;
            world.Player.Species = "Human";
            world.Player.Background = "Soldier";
            var serializer = new JsonWorldSerializer();
            serializer.Serialize(world);
            var save = Stopwatch.StartNew();
            var json = serializer.Serialize(world);
            save.Stop();
            var load = Stopwatch.StartNew();
            var back = serializer.Deserialize(json);
            load.Stop();
            Assert.That(back.IsSuccess, Is.True, string.Join("; ", back.Errors));

            var perValidate = validate.Elapsed.TotalMilliseconds / N;
            var perDerive = derive.Elapsed.TotalMilliseconds / N;
            TestContext.Out.WriteLine(
                $"PERF6 catalog validation {catalog.Elapsed.TotalMilliseconds:F2} ms (first use, includes building) | creation check {perValidate * 1000:F1} µs | " +
                $"full sheet (18 skills, 6 saves, HP, AC, speed) {perDerive * 1000:F1} µs | world save {save.Elapsed.TotalMilliseconds:F1} ms, load {load.Elapsed.TotalMilliseconds:F1} ms");
            Assert.That(perValidate, Is.LessThan(2.0), "Checking a character's choices takes well under a frame.");
            Assert.That(perDerive, Is.LessThan(1.0), "Deriving a full sheet takes well under a frame.");
        }
    }
}
