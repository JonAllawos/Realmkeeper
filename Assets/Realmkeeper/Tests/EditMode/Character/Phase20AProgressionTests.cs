using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Rpg;

namespace Realmkeeper.Tests.EditMode.Character
{
    /// <summary>
    /// Phase 20A: what the existing 1–20 walk (<c>ProgressionTests.EveryClass_AdvancesFromOneToTwenty_ValidAtEveryLevel</c>)
    /// did not check. Every class advances 1 → 20 as a tiefling, a goliath and a gnome, and at every level: nothing is left
    /// outstanding; the subclass is absent before its level and kept after; the lineage choice is never dropped or changed;
    /// a goliath's ancestry uses equal the proficiency bonus; a tiefling's legacy cantrip stays known and its attack grows
    /// with the proficiency bonus.
    /// </summary>
    public sealed class Phase20AProgressionTests
    {
        private static readonly (string species, string choice, string option)[] Lineages =
        {
            ("tiefling", "species:fiendish_legacy", "chthonic"),
            ("goliath", "species:giant_ancestry", "storm"),
            ("gnome", "species:gnome_lineage", "rock"),
        };

        [Test]
        public void EveryClass_AsEachLineageSpecies_AdvancesToTwenty_KeepingItsLineageAndItsSubclassOrder()
        {
            foreach (var cls in RpgCatalog.Classes)
                foreach (var (species, choice, option) in Lineages)
                {
                    var c = ProgressionTests.Choices(cls.Id, species);
                    c.Choices[choice] = new List<string> { option };
                    var sheet = CharacterCommandHandlers.BuildSheet(c, out var errors);
                    Assert.That(errors, Is.Empty, $"{species} {cls.Id}");
                    sheet.ExperienceLog.Add(new ExperienceAward { Key = "all", Amount = ProgressionRules.ExperienceFor(20) });
                    sheet.Experience = ProgressionRules.ExperienceFor(20);
                    var where = $"{species} ({option}) {cls.Name}";

                    for (var level = 2; level <= 20; level++)
                    {
                        var next = CharacterAdvancement.Apply(sheet, new[] { CharacterAdvancement.SuggestChoices(sheet, level) }, default, out errors);
                        Assert.That(next, Is.Not.Null, $"{where} {level}: {string.Join("; ", errors)}");
                        sheet = next;
                        var rules = new CharacterRules(sheet);
                        Assert.That(CharacterValidation.CheckSheet(sheet), Is.Empty, $"{where} {level}");
                        Assert.That(CharacterProgression.Outstanding(sheet), Is.Empty, $"{where} {level}: nothing left to choose");
                        Assert.That(sheet.SubclassId == null, Is.EqualTo(level < cls.SubclassLevel), $"{where} {level}: the subclass arrives at level {cls.SubclassLevel}, not before");
                        Assert.That(sheet.Chosen(choice), Is.EqualTo(new[] { option }), $"{where} {level}: the lineage is kept");
                        Assert.That(rules.HasFeature(choice + ":" + option), Is.True, $"{where} {level}");

                        if (species == "goliath")
                            Assert.That(rules.ResourceMax(rules.FindResource("stones_endurance")), Is.EqualTo(rules.ProficiencyBonus), $"{where} {level}: uses = proficiency bonus");
                        if (species == "tiefling")
                        {
                            Assert.That(rules.InnateSpells.Select(x => x.spellId), Is.EqualTo(new[] { "chill_touch" }), $"{where} {level}");
                            Assert.That(rules.InnateSpellAttack(Ability.Charisma).Total, Is.EqualTo(rules.ProficiencyBonus + rules.Modifier(Ability.Charisma)));
                        }
                    }

                    Assert.That(sheet.Level, Is.EqualTo(20), where);
                    Assert.That(new CharacterRules(sheet).ProficiencyBonus, Is.EqualTo(6));
                }
        }

        [Test]
        public void EveryClass_HasExactlyOneSubclass_FromTheSrd_ChosenAtItsDeclaredLevel()
        {
            foreach (var cls in RpgCatalog.Classes)
            {
                var subs = RpgCatalog.SubclassesOf(cls.Id);
                Assert.That(subs.Count, Is.EqualTo(1), cls.Name + ": the SRD's one subclass; none invented to fill a menu");
                Assert.That(subs[0].Source, Is.Not.EqualTo(ContentSource.RealmkeeperOriginal), cls.Name);
                Assert.That(cls.SubclassLevel, Is.EqualTo(3), cls.Name + ": SRD 5.2.1 gives every class its subclass at level 3");
            }
        }
    }
}
