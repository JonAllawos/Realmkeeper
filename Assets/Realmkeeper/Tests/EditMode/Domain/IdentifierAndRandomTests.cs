using System.Collections.Generic;
using NUnit.Framework;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Randomness;
using Realmkeeper.Infrastructure.Randomness;

namespace Realmkeeper.Tests.EditMode.Domain
{
    public sealed class IdentifierAndRandomTests
    {
        [Test]
        public void DeterministicGuids_AreUniqueAcrossLargeSample()
        {
            var random = new Xoshiro256Random(12345);
            var seen = new HashSet<SettlementId>();
            for (var i = 0; i < 10000; i++)
            {
                var id = new SettlementId(DeterministicGuid.Next(random));
                Assert.That(id.IsEmpty, Is.False);
                Assert.That(seen.Add(id), Is.True, $"Duplicate identifier at index {i}");
            }
        }

        [Test]
        public void DeterministicGuids_AreReproducibleFromSameSeed()
        {
            var a = new Xoshiro256Random(99);
            var b = new Xoshiro256Random(99);
            for (var i = 0; i < 100; i++)
            {
                Assert.That(DeterministicGuid.Next(a), Is.EqualTo(DeterministicGuid.Next(b)));
            }
        }

        [Test]
        public void DeterministicGuids_AreVersion4Shaped()
        {
            var guid = DeterministicGuid.Next(new Xoshiro256Random(7)).ToString("D");
            Assert.That(guid[14], Is.EqualTo('4'), guid);
            Assert.That("89ab".IndexOf(guid[19]), Is.GreaterThanOrEqualTo(0), guid);
        }

        [Test]
        public void Identifier_RoundTripsThroughString_AndDefaultIsEmpty()
        {
            var id = WorldId.NewRandom();
            Assert.That(WorldId.TryParse(id.ToString(), out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(id));
            Assert.That(default(WorldId).IsEmpty, Is.True);
            Assert.That(WorldId.TryParse("not-a-guid", out _), Is.False);
        }

        [Test]
        public void Random_SameSeedProducesSameSequence_DifferentSeedDiffers()
        {
            var a = new Xoshiro256Random(42);
            var b = new Xoshiro256Random(42);
            var c = new Xoshiro256Random(43);
            var anyDifference = false;
            for (var i = 0; i < 64; i++)
            {
                var va = a.NextULong();
                Assert.That(b.NextULong(), Is.EqualTo(va));
                if (c.NextULong() != va) anyDifference = true;
            }

            Assert.That(anyDifference, Is.True);
        }

        [Test]
        public void Random_KnownSequenceIsStable()
        {
            // Golden values guard against accidental algorithm changes, which would silently
            // change every generated world. Update only together with a GeneratorVersion bump.
            var r = new Xoshiro256Random(1);
            var first = r.NextULong();
            var second = r.NextULong();
            Assert.That(first, Is.EqualTo(GoldenFirst));
            Assert.That(second, Is.EqualTo(GoldenSecond));
        }

        [Test]
        public void Random_NextIntStaysInRange()
        {
            var r = new Xoshiro256Random(5);
            for (var i = 0; i < 5000; i++)
            {
                Assert.That(r.NextInt(-3, 4), Is.InRange(-3, 3));
                Assert.That(r.NextDouble(), Is.GreaterThanOrEqualTo(0d).And.LessThan(1d));
            }
        }

        [Test]
        public void SeedMixer_StringHashIsStable()
        {
            Assert.That(SeedMixer.HashString("realmkeeper.food"), Is.EqualTo(SeedMixer.HashString("realmkeeper.food")));
            Assert.That(SeedMixer.HashString("a"), Is.Not.EqualTo(SeedMixer.HashString("b")));
            Assert.That(SeedMixer.Combine(1, "x"), Is.Not.EqualTo(SeedMixer.Combine(2, "x")));
        }

        internal const ulong GoldenFirst = 12966619160104079557UL;
        internal const ulong GoldenSecond = 9600361134598540522UL;
    }
}
