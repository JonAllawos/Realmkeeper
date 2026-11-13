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
    /// Phase 20 (rules 9, schema v21): species lineages (SRD 5.2.1 tiefling legacies, goliath ancestries, gnome lineages),
    /// identity (pronouns, concept), height and build, species skin tones, and the migration that keeps every earlier
    /// character exactly as it was.
    /// </summary>
    public sealed class Phase20CharacterTests
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

        private static CharacterDraft Draft(string species, string cls = "fighter")
        {
            var d = new CharacterDraft();
            d.Choices.Name = "Wren Halloway";
            d.SetSpecies(species);
            d.SetBackground("hermit");
            d.SetClass(cls);
            var c = RpgCatalog.FindClass(cls);
            foreach (var s in c.SkillOptions) if (d.Picked(ClassDefinition.SkillChoiceId).Count < c.SkillChoices) d.Toggle(ClassDefinition.SkillChoiceId, s.ToString());
            foreach (var ch in c.Choices.Where(x => x.Kind == ChoiceKind.Option)) d.Toggle(ch.Id, ch.Options[0].Id);
            foreach (var ch in RpgCatalog.FindSpecies(species).Choices.Where(x => x.Kind == ChoiceKind.Skill)) d.Toggle(ch.Id, nameof(Skill.Insight));
            return d;
        }

        private static CharacterRules Rules(CharacterDraft d) => new CharacterRules(CharacterCommandHandlers.BuildSheet(d.Clean(), out _));

        [Test]
        public void EachLineage_IsAChoice_TheCreatorRequires_AndItsEffectsApply()
        {
            foreach (var (species, choice, options) in new[]
                     {
                         ("tiefling", "species:fiendish_legacy", new[] { "abyssal", "chthonic", "infernal" }),
                         ("goliath", "species:giant_ancestry", new[] { "cloud", "fire", "frost", "hill", "stone", "storm" }),
                         ("gnome", "species:gnome_lineage", new[] { "forest", "rock" }),
                     })
            {
                var def = RpgCatalog.FindSpecies(species).Choices.Single(c => c.Id == choice);
                Assert.That(def.Options.Select(o => o.Id), Is.EqualTo(options), species);
                Assert.That(def.IntroducedInRules, Is.EqualTo(9));
                var d = Draft(species);
                Assert.That(d.Problems(CreationStep.Species), Is.Not.Empty, species + ": the lineage must be chosen.");
                d.Toggle(choice, options[0]);
                Assert.That(d.Problems(CreationStep.Species), Is.Empty, species);
            }

            var resist = new[] { ("abyssal", "poison"), ("chthonic", "necrotic"), ("infernal", "fire") };
            foreach (var (legacy, damage) in resist)
            {
                var d = Draft("tiefling");
                d.Toggle("species:fiendish_legacy", legacy);
                var r = Rules(d);
                Assert.That(r.Resistances, Has.Member(damage), legacy);
                Assert.That(r.Resistances.Count(x => resist.Any(p => p.Item2 == x)), Is.EqualTo(1), "Only the chosen legacy's resistance.");
            }

            var forest = Draft("gnome");
            forest.Toggle("species:gnome_lineage", "forest");
            var rock = Draft("gnome");
            rock.Toggle("species:gnome_lineage", "rock");
            Assert.That(Rules(rock).ToolProficiencies, Has.Member("tinkers_tools"));
            Assert.That(Rules(forest).ToolProficiencies, Has.No.Member("tinkers_tools"), "Tinker's tools belong to the rock lineage now.");

            var goliath = Draft("goliath");
            goliath.Toggle("species:giant_ancestry", "fire");
            var g = Rules(goliath);
            Assert.That(g.ResourceDefinitions.Select(x => x.Id), Has.Member("stones_endurance"), "The ancestry's uses keep their saved resource ID.");
            Assert.That(RpgCatalog.FindSpecies("goliath").Choices.Single().Options.All(o => o.Needs == IntegrationNeed.Combat), Is.True,
                "No ancestry boon is played by combat yet, and each says so.");
        }

        [Test]
        public void ASavedTieflingGoliathAndGnome_KeepTheTraitsTheyHad_AfterMigration()
        {
            var serializer = new JsonWorldSerializer();
            var world = Phase2Worlds.Generated(424242);
            // A v20 save: a created tiefling player, and three people with sheets of the three species, written before lineages existed.
            var doc = JObject.Parse(serializer.Serialize(world));
            doc["schemaVersion"] = 20;
            var rpg = (JObject)doc["world"]["player"]["rpg"];
            rpg["state"] = "Complete";
            rpg["speciesId"] = "tiefling";
            rpg["choices"] = new JObject { ["species:ancestry"] = new JArray() };
            var pending = JObject.Parse(serializer.Serialize(world));
            pending["schemaVersion"] = 20;

            var migrated = JObject.Parse(doc.ToString());
            new MigrationV20ToV21().Migrate(migrated);
            Assert.That((string)migrated["world"]["player"]["rpg"]["choices"]["species:fiendish_legacy"][0], Is.EqualTo("infernal"), "The fire-resistant tiefling it was.");

            foreach (var (species, choice, option) in new[] { ("goliath", "species:giant_ancestry", "stone"), ("gnome", "species:gnome_lineage", "rock") })
            {
                var o = new JObject { ["world"] = new JObject { ["npcs"] = new JArray(new JObject { ["character"] = new JObject { ["sheet"] = new JObject { ["speciesId"] = species, ["choices"] = new JObject() } } }) } };
                new MigrationV20ToV21().Migrate(o);
                Assert.That((string)o["world"]["npcs"][0]["character"]["sheet"]["choices"][choice][0], Is.EqualTo(option), species);
            }

            var already = new JObject { ["world"] = new JObject { ["player"] = new JObject { ["rpg"] = new JObject { ["state"] = "Complete", ["speciesId"] = "tiefling", ["choices"] = new JObject { ["species:fiendish_legacy"] = new JArray("abyssal") } } } } };
            new MigrationV20ToV21().Migrate(already);
            Assert.That((string)already["world"]["player"]["rpg"]["choices"]["species:fiendish_legacy"][0], Is.EqualTo("abyssal"), "A choice already made is never overwritten.");

            var loaded = serializer.Deserialize(pending.ToString());
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            Assert.That(loaded.World.Player.Rpg.RulesVersion, Is.EqualTo(ProgressionRules.CurrentRulesVersion), "A character not yet created is made under the current rules.");
            Assert.That(loaded.World.Player.Appearance.HeightPercent, Is.EqualTo(100), "Absent height: the species' typical height.");
            Assert.That(loaded.World.Player.Appearance.Build, Is.EqualTo(1), "Absent build: average.");
        }

        [Test]
        public void Identity_Height_Build_AndSpeciesSkin_AreCreated_Saved_AndLoaded()
        {
            var d = Draft("tiefling");
            d.Toggle("species:fiendish_legacy", "chthonic");
            Assert.That(PlayerAppearanceOptions.SkinTonesFor("tiefling"), Has.Member(d.Choices.Appearance.SkinTone), "The draft keeps a tone a tiefling can have.");
            d.Choices.Pronouns = "they/them";
            d.Choices.Concept = "a ferry-keeper's child who never learned to stay put";
            d.Choices.Appearance.SkinTone = 7; // violet: a tiefling tone
            d.Choices.Appearance.HeightPercent = 106;
            d.Choices.Appearance.Build = 2;
            Assert.That(d.Problems(CreationStep.Review), Is.Empty, string.Join("; ", d.Problems(CreationStep.Review)));
            var r = _s.Dispatch(d.ToCommand());
            Assert.That(r.IsSuccess, Is.True, r.ToString());

            Directory.CreateDirectory(_dir);
            var path = Path.Combine(_dir, "p20.rkworld.json");
            var json = new JsonWorldSerializer().Serialize(W);
            File.WriteAllText(path, json);
            var back = new JsonWorldSerializer().Deserialize(File.ReadAllText(path));
            Assert.That(back.IsSuccess, Is.True, string.Join("; ", back.Errors));
            var p = back.World.Player;
            Assert.That(p.Rpg.Pronouns, Is.EqualTo("they/them"));
            Assert.That(p.Rpg.Concept, Does.StartWith("a ferry-keeper's child"));
            Assert.That(p.Rpg.Chosen("species:fiendish_legacy"), Is.EqualTo(new[] { "chthonic" }));
            Assert.That(p.Appearance.SkinTone, Is.EqualTo(7));
            Assert.That(p.Appearance.HeightPercent, Is.EqualTo(106));
            Assert.That(p.Appearance.Build, Is.EqualTo(2));
            Assert.That(WorldFingerprint.Of(back.World), Is.EqualTo(WorldFingerprint.Of(W)), "Nothing else changed on the way.");
        }

        [Test]
        public void Appearance_RefusesWhatTheSpeciesCannotHave_AndSaysWhy()
        {
            var a = new PlayerAppearanceData { Frame = NpcSex.Male, HairId = "Hair_Long", SkinTone = 7 };
            Assert.That(PlayerAppearanceOptions.Check(a, "tiefling"), Is.Null);
            Assert.That(PlayerAppearanceOptions.Check(a, "human"), Does.Contain("Violet").And.Contain("human"));
            Assert.That(PlayerAppearanceOptions.Check(a, null), Does.Contain("choose the species first"));
            a.SkinTone = 1;
            a.HeightPercent = 120;
            Assert.That(PlayerAppearanceOptions.Check(a, "human"), Does.Contain("Height"));
            a.HeightPercent = 100;
            a.Build = 3;
            Assert.That(PlayerAppearanceOptions.Check(a, "human"), Does.Contain("Build"));
            Assert.That(PlayerAppearanceOptions.SkinTonesFor("dragonborn").All(i => i >= PlayerAppearanceOptions.SkinTones), Is.True, "Dragonborn have scales, not human skin.");

            var d = new CharacterDraft();
            d.Choices.Appearance.SkinTone = 1;
            d.SetSpecies("dragonborn");
            Assert.That(PlayerAppearanceOptions.SkinTonesFor("dragonborn"), Has.Member(d.Choices.Appearance.SkinTone));
            Assert.That(d.Adjustment, Does.Contain("skin set to"), "The change is said, never silent.");
            Assert.That(PlayerAppearanceOptions.BuildWidth(0), Is.LessThan(1f));
            Assert.That(PlayerAppearanceOptions.BuildWidth(2), Is.GreaterThan(1f));

            var c = Draft("human");
            c.Choices.Concept = new string('x', CharacterSheetData.MaxConceptLength + 1);
            Assert.That(c.Problems(CreationStep.Identity), Has.Some.Contains("concept"));
        }

        [Test]
        public void GeneratedPeople_OfTheNewLineages_GetValidSheets_Deterministically()
        {
            var a = Phase2Worlds.Generated(31337);
            var b = Phase2Worlds.Generated(31337);
            NpcCharacterGenesis.Ensure(a);
            NpcCharacterGenesis.Ensure(b);
            var sheets = a.Npcs.Where(n => n.Character?.Sheet != null).ToList();
            Assert.That(sheets, Is.Not.Empty);
            foreach (var n in sheets)
                Assert.That(CharacterValidation.CheckSheet(n.Character.Sheet), Is.Empty, n.Name);
            Assert.That(WorldFingerprint.Of(a), Is.EqualTo(WorldFingerprint.Of(b)));
        }

        [Test]
        public void APerson_IsKnownOnlyAsFarAsThePlayerKnowsThem()
        {
            var w = Phase2Worlds.Generated(27182);
            NpcCharacterGenesis.Ensure(w);
            var n = w.Npcs.First(x => x.IsAlive && x.Character?.Sheet != null && !x.FactionId.IsEmpty);
            n.ConversationCount = 0;
            n.MetPlayerOn = default;
            n.PlayerRegard.Familiarity = 0;
            n.PlayerRegard.Conversations = 0;
            string[] Labels() => KnownPersonQueries.View(w, n).Lines.Select(l => l.Label).ToArray();

            var stranger = KnownPersonQueries.View(w, n);
            Assert.That(stranger.Acquaintance, Is.EqualTo(Acquaintance.Seen));
            Assert.That(Labels(), Is.SupersetOf(new[] { "Looks like", "Occupation" }));
            Assert.That(Labels().Intersect(new[] { "Background", "Serves", "Trained as", "Abilities" }), Is.Empty, "A stranger's history and sheet are not on show.");
            Assert.That(stranger.Withheld, Does.Contain("Speak with them"));

            n.ConversationCount = 1;
            Assert.That(Labels(), Has.Member("Serves").And.Member("Background"));
            Assert.That(Labels().Intersect(new[] { "Trained as", "Abilities" }), Is.Empty);

            n.PlayerRegard.Familiarity = KnownPersonQueries.FamiliarAt;
            Assert.That(Labels(), Has.Member("Trained as").And.Member("Good at"));
            Assert.That(Labels(), Has.No.Member("Abilities"), "Numbers are a companion's, not an acquaintance's.");
            Assert.That(KnownPersonQueries.View(w, n).Lines.Single(l => l.Label == "Trained as").Value, Does.Not.Match("[0-9]"), "A level is said in words.");

            w.Companions.Add(new CompanionRecordData { NpcId = n.NpcId, Standing = CompanionStanding.InParty });
            var companion = KnownPersonQueries.View(w, n);
            Assert.That(companion.Acquaintance, Is.EqualTo(Acquaintance.Companion));
            Assert.That(Labels(), Has.Member("Abilities").And.Member("Hit points"));
            Assert.That(companion.Withheld, Is.Null);
            var rules = new CharacterRules(n.Character.Sheet);
            Assert.That(companion.Lines.Single(l => l.Label == "Abilities").Value, Does.Contain("Str " + rules.Score(Ability.Strength)), "The same record the rules use, not a second set of numbers.");
        }
    }
}
