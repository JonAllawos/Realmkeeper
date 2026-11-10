using System;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Graphics
{
    public sealed class NpcAppearanceTests
    {
        private static readonly NpcId Aldric = new NpcId(Guid.Parse("3d6f0b52-1a2c-4b7e-9f40-8c2a7e5d1b90"));

        [Test]
        public void SameIdentity_AlwaysResolvesTheSameAppearance()
        {
            var a = NpcAppearanceResolver.Resolve(Aldric, NpcRole.Smith, "Aldric Greaves");
            var b = NpcAppearanceResolver.Resolve(new NpcId(Aldric.Value), NpcRole.Smith, "Aldric Greaves");
            Assert.That(b.Signature, Is.EqualTo(a.Signature));
            Assert.That(a.NpcId, Is.EqualTo(Aldric), "The appearance refers to the identity; it does not own one.");
        }

        [Test]
        public void Appearance_IsStableAcrossKnownValues()
        {
            // Pinned: a change to the mapping changes every resident's look in existing saves, so it must be deliberate.
            var a = NpcAppearanceResolver.Resolve(Aldric, NpcRole.Smith, "Aldric Greaves");
            Assert.That(a.BodyId, Is.EqualTo("Male_Peasant"));
            Assert.That(a.OutfitMaterialId, Is.EqualTo("Peasant2"));
            Assert.That(a.HeadId, Is.EqualTo("Head_Male"));
        }

        [Test]
        public void Role_DecidesTheOutfit()
        {
            var id = NpcId.Parse("a0b1c2d3-e4f5-4607-8899-aabbccddeeff");
            Assert.That(NpcAppearanceResolver.Resolve(id, NpcRole.Guard, "Brand Wade").BodyId, Is.EqualTo("Male_Ranger"));
            Assert.That(NpcAppearanceResolver.Resolve(id, NpcRole.Guard, "Brand Wade").OutfitMaterialId, Is.EqualTo("Ranger"));
            Assert.That(NpcAppearanceResolver.Resolve(id, NpcRole.Elder, "Brand Wade").OutfitMaterialId, Is.EqualTo("Ranger3"));
            Assert.That(NpcAppearanceResolver.Resolve(id, NpcRole.Elder, "Brand Wade").HairColour, Is.EqualTo(NpcAppearanceResolver.GreyHair));
            Assert.That(NpcAppearanceResolver.Resolve(id, NpcRole.Labourer, "Brand Wade").BodyId, Is.EqualTo("Male_Peasant"));
        }

        [Test]
        public void GivenName_SelectsTheBodyFrame()
        {
            var id = NpcId.Parse("a0b1c2d3-e4f5-4607-8899-aabbccddeeff");
            Assert.That(NpcAppearanceResolver.Resolve(id, NpcRole.Resident, "Maud Penrose").Frame, Is.EqualTo(BodyFrame.Female));
            Assert.That(NpcAppearanceResolver.Resolve(id, NpcRole.Resident, "Maud Penrose").BodyId, Does.StartWith("Female_"));
            Assert.That(NpcAppearanceResolver.Resolve(id, NpcRole.Resident, "Osric Penrose").Frame, Is.EqualTo(BodyFrame.Male));
            Assert.That(NpcAppearanceResolver.Resolve(id, NpcRole.Resident, "Maud Penrose").BeardId, Is.Null);
        }

        [Test]
        public void IndicesAreInsideThePalettes()
        {
            for (var i = 0; i < 200; i++)
            {
                var id = new NpcId(new Guid(i, 7, 9, 1, 2, 3, 4, 5, 6, 7, 8));
                foreach (NpcRole role in Enum.GetValues(typeof(NpcRole)))
                {
                    var a = NpcAppearanceResolver.Resolve(id, role, "Edda Oakes");
                    Assert.That(a.SkinTone, Is.InRange(0, NpcAppearanceResolver.SkinTones.Count - 1));
                    Assert.That(a.HairColour, Is.InRange(0, NpcAppearanceResolver.HairColours.Count - 1));
                    Assert.That(a.OutfitTint, Is.InRange(0, NpcAppearanceResolver.OutfitTints.Count - 1));
                    Assert.That(a.HeightScale, Is.InRange(0.95f, 1.05f));
                }
            }
        }

        [Test]
        public void TheDefaultWorldsResidents_GetVariedAppearances()
        {
            var dir = Phase2Worlds.TempDirectory();
            var session = Phase2Worlds.Session(dir);
            var npcs = session.GetSettlements().SelectMany(s => session.GetResidents(s.SettlementId)).ToList();
            Assert.That(npcs.Count, Is.GreaterThanOrEqualTo(10));
            var looks = npcs.Select(n => NpcAppearanceResolver.Resolve(n.NpcId, n.Role, n.Name).Signature).ToList();
            Assert.That(looks.Distinct().Count(), Is.EqualTo(looks.Count), "No two named residents look identical.");
        }

        [Test]
        public void EmptyId_IsRejected()
        {
            Assert.Throws<ArgumentException>(() => NpcAppearanceResolver.Resolve(default(NpcId), NpcRole.Resident, "Edda"));
        }
    }
}
