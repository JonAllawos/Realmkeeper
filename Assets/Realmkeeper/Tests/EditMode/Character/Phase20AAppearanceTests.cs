using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Character
{
    /// <summary>
    /// Phase 20A (schema v22, ADR-0104): eye colour, the iris mask it depends on, and the player's look reaching the body in
    /// the world (the hub used to drop height and build).
    /// </summary>
    public sealed class Phase20AAppearanceTests
    {
        private string _dir;
        private WorldSession _s;

        [SetUp]
        public void SetUp()
        {
            _dir = Phase2Worlds.TempDirectory();
            _s = Phase2Worlds.Session(_dir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private WorldData W => _s.CurrentWorldSnapshotForTests();

        [Test]
        public void EyeColour_IsChosen_Checked_Saved_AndReachesTheHub_WithHeightAndBuild()
        {
            Assert.That(PlayerAppearanceOptions.EyePalette[0].name, Is.EqualTo("Brown"), "0 is the eye as drawn, so every earlier character keeps its eyes.");
            Assert.That(PlayerAppearanceOptions.EyePalette.Select(e => e.name).Distinct().Count(), Is.EqualTo(PlayerAppearanceOptions.EyePalette.Count));
            Assert.That(new PlayerAppearanceData().EyeColour, Is.EqualTo(0));

            var d = new CharacterDraft();
            d.Choices.Name = "Edda Thorne";
            d.SetSpecies("human");
            d.SetBackground("hermit");
            d.SetClass("fighter");
            var c = RpgCatalog.FindClass("fighter");
            foreach (var s in c.SkillOptions) if (d.Picked(ClassDefinition.SkillChoiceId).Count < c.SkillChoices) d.Toggle(ClassDefinition.SkillChoiceId, s.ToString());
            foreach (var ch in c.Choices.Where(x => x.Kind == ChoiceKind.Option)) d.Toggle(ch.Id, ch.Options[0].Id);
            foreach (var ch in RpgCatalog.FindSpecies("human").Choices.Where(x => x.Kind == ChoiceKind.Skill)) d.Toggle(ch.Id, nameof(Skill.Insight));
            foreach (var ch in RpgCatalog.FindSpecies("human").Choices.Where(x => x.Kind == ChoiceKind.Option)) d.Toggle(ch.Id, ch.Options[0].Id);
            d.Choices.Appearance.EyeColour = 5; // blue
            d.Choices.Appearance.HeightPercent = 94;
            d.Choices.Appearance.Build = 0;
            Assert.That(d.Problems(CreationStep.Review), Is.Empty, string.Join("; ", d.Problems(CreationStep.Review)));
            var r = _s.Dispatch(d.ToCommand());
            Assert.That(r.IsSuccess, Is.True, r.ToString());

            var hub = _s.GetPlayerHub();
            Assert.That(hub.Appearance.EyeColour, Is.EqualTo(5));
            Assert.That(hub.Appearance.HeightPercent, Is.EqualTo(94), "The hub (which the body in the world is built from) keeps the height.");
            Assert.That(hub.Appearance.Build, Is.EqualTo(0), "And the build.");

            var bad = W.Player.Appearance.Clone();
            bad.EyeColour = PlayerAppearanceOptions.EyePalette.Count;
            Assert.That(PlayerAppearanceOptions.Check(bad), Does.Contain("Eye colour"));
            Assert.That(_s.Dispatch(new SetPlayerAppearanceCommand(bad)).IsSuccess, Is.False);
            var green = W.Player.Appearance.Clone();
            green.EyeColour = 4;
            Assert.That(_s.Dispatch(new SetPlayerAppearanceCommand(green)).IsSuccess, Is.True);
            Assert.That(W.Player.Appearance.EyeColour, Is.EqualTo(4), "Changing appearance later keeps the new eye colour.");
            Assert.That(W.Player.Appearance.HeightPercent, Is.EqualTo(94));

            var serializer = new JsonWorldSerializer();
            var json = serializer.Serialize(W);
            Assert.That((int)JObject.Parse(json)["schemaVersion"], Is.EqualTo(24)); // Phase 22 (v24)
            var back = serializer.Deserialize(json);
            Assert.That(back.IsSuccess, Is.True, string.Join("; ", back.Errors));
            Assert.That(back.World.Player.Appearance.EyeColour, Is.EqualTo(4));
            Assert.That(WorldFingerprint.Of(back.World), Is.EqualTo(WorldFingerprint.Of(W)));
        }

        [Test]
        public void AVersion21Save_LoadsWithTheEyesItHad()
        {
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(W));
            doc["schemaVersion"] = 21;
            ((JObject)doc["world"]["player"]["appearance"]).Remove("eyeColour");
            var loaded = serializer.Deserialize(doc.ToString());
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            Assert.That(loaded.World.Player.Appearance.EyeColour, Is.EqualTo(0), "Absent: the drawn brown.");
            Assert.That(loaded.World.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
        }

        [Test]
        public void TheIrisMask_IsInTheProject_Readable_AndDerivedFromTheCc0Eye()
        {
            string root = null;
            foreach (var start in new[] { TestContext.CurrentContext.TestDirectory, Directory.GetCurrentDirectory() })
                for (var dir = new DirectoryInfo(start); dir != null && root == null; dir = dir.Parent)
                    if (Directory.Exists(Path.Combine(dir.FullName, "Assets", "Realmkeeper"))) root = Path.Combine(dir.FullName, "Assets", "Realmkeeper");
            Assert.That(root, Is.Not.Null, "The project's Assets folder is above the tests.");
            var mask = Path.Combine(root, "Art", "ThirdParty", "Quaternius", "UniversalBaseCharacters", "Resources", "RealmkeeperEyes", "T_Eye_IrisMask.png");
            Assert.That(File.Exists(mask), Is.True, mask);
            var meta = File.ReadAllText(mask + ".meta");
            Assert.That(meta, Does.Contain("isReadable: 1"), "Read on the CPU to make each colour.");
            var bytes = File.ReadAllBytes(mask);
            Assert.That(bytes[25], Is.EqualTo(6), "RGBA: the iris weight is in alpha.");
        }
    }
}
