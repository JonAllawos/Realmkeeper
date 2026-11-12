using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;

namespace Realmkeeper.Tests.EditMode.Application
{
    /// <summary>
    /// Phase 19: the in-game credits carry the attribution the interface assets require. The CC BY 3.0 icons must name
    /// their authors and the source; the OFL fonts are named with their licence; the CC0 kits are credited anyway.
    /// </summary>
    public sealed class Phase19CreditsTests
    {
        [Test]
        public void TheIconAuthors_TheFonts_AndTheKits_AreCreditedInTheGame()
        {
            var icons = Credits.Assets.Single(a => a.Work.Contains("game-icons.net"));
            foreach (var author in new[] { "Lorc", "Delapouite", "sbed", "Skoll", "Guard13007", "Carl Olsen", "Willdabeast", "DarkZaitzev", "Faithtoken", "Lucas", "Zeromancer" })
                Assert.That(icons.Author, Does.Contain(author), author);
            Assert.That(icons.Licence, Does.StartWith("CC BY 3.0"));

            Assert.That(Credits.Assets.Count(a => a.Licence.StartsWith("SIL Open Font License 1.1")), Is.EqualTo(2), "EB Garamond and Alegreya Sans, as OFL (not CC).");
            Assert.That(Credits.Assets.Any(a => a.Work.StartsWith("Fantasy UI Borders") && a.Author.StartsWith("Kenney")), Is.True);
            Assert.That(Credits.Assets.Any(a => a.Work.StartsWith("Cartography Pack") && a.Author.StartsWith("Kenney")), Is.True);
        }
    }
}
