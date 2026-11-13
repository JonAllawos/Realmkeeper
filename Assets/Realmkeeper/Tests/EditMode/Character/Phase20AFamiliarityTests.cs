using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Character
{
    /// <summary>
    /// Phase 20A: the audit of how well the player knows a person. Familiarity grows once per day per person from talk
    /// (+2) and once per day per keeper from trade (+3); repetition the same day adds nothing; 50 ("familiar") is reached
    /// by ordinary play. Hostility, death and a companion leaving never reveal more than the acquaintance allows, and the
    /// regard survives a save.
    /// </summary>
    public sealed class Phase20AFamiliarityTests
    {
        private static (WorldData w, NpcData n) Stranger(int seed = 31337)
        {
            var w = Phase2Worlds.Generated(seed);
            NpcCharacterGenesis.Ensure(w);
            var n = w.Npcs.First(x => x.IsAlive && x.Character?.Sheet != null && !x.FactionId.IsEmpty);
            n.ConversationCount = 0;
            n.MetPlayerOn = default;
            n.PlayerRegard.Familiarity = 0;
            n.PlayerRegard.Conversations = 0;
            n.PlayerRegard.Trades = 0;
            n.PlayerRegard.LastTalkDay = 0;
            n.PlayerRegard.LastTradeDay = 0;
            return (w, n);
        }

        private static string[] Labels(WorldData w, NpcData n) => KnownPersonQueries.View(w, n).Lines.Select(l => l.Label).ToArray();

        private static readonly string[] SheetLabels = { "Trained as", "Good at", "Speaks", "Level", "Hit points", "Abilities", "Saving throws", "Features" };

        [Test]
        public void FirstAndRepeatedConversations_CountOncePerDay_AndCannotBeFarmed()
        {
            var (w, n) = Stranger();
            var day = w.CurrentSimulationDate;
            Assert.That(KnownPersonQueries.AcquaintanceOf(w, n), Is.EqualTo(Acquaintance.Seen));

            PlayerCommandHandlers.Acquaint(n, day, false);
            n.ConversationCount++;
            Assert.That(n.PlayerRegard.Familiarity, Is.EqualTo(PlayerCommandHandlers.TalkFamiliarity));
            Assert.That(KnownPersonQueries.AcquaintanceOf(w, n), Is.EqualTo(Acquaintance.Acquainted), "The first conversation makes them an acquaintance.");

            for (var i = 0; i < 50; i++) PlayerCommandHandlers.Acquaint(n, day, false);
            Assert.That(n.PlayerRegard.Familiarity, Is.EqualTo(2), "Fifty more conversations the same day add nothing.");
            Assert.That(n.PlayerRegard.Conversations, Is.EqualTo(51), "They are still counted.");

            PlayerCommandHandlers.Acquaint(n, day, true);
            for (var i = 0; i < 20; i++) PlayerCommandHandlers.Acquaint(n, day, true);
            Assert.That(n.PlayerRegard.Familiarity, Is.EqualTo(5), "Trade counts once that day too (+3).");

            PlayerCommandHandlers.Acquaint(n, day.AddDays(1), false);
            Assert.That(n.PlayerRegard.Familiarity, Is.EqualTo(7), "A new day, a new conversation.");
        }

        [Test]
        public void Familiar_IsReachedByOrdinaryPlay_InWeeksNotMinutes()
        {
            var (w, n) = Stranger();
            var start = w.CurrentSimulationDate;
            n.ConversationCount = 1;
            var days = 0;
            while (n.PlayerRegard.Familiarity < KnownPersonQueries.FamiliarAt) PlayerCommandHandlers.Acquaint(n, start.AddDays(days++), false);
            Assert.That(days, Is.EqualTo(25), "Talking every day: 25 days.");
            Assert.That(KnownPersonQueries.AcquaintanceOf(w, n), Is.EqualTo(Acquaintance.Familiar));

            var (w2, k) = Stranger(4242);
            k.ConversationCount = 1;
            days = 0;
            while (k.PlayerRegard.Familiarity < KnownPersonQueries.FamiliarAt)
            {
                PlayerCommandHandlers.Acquaint(k, start.AddDays(days), false);
                PlayerCommandHandlers.Acquaint(k, start.AddDays(days), true);
                days++;
            }

            Assert.That(days, Is.EqualTo(10), "Talking and trading with a keeper every day: 10 days.");
            Assert.That(KnownPersonQueries.AcquaintanceOf(w2, k), Is.EqualTo(Acquaintance.Familiar));
        }

        [Test]
        public void Hostility_NeitherHidesNorRevealsWhatTheAcquaintanceAllows()
        {
            var (w, n) = Stranger();
            n.PlayerRegard.Hostility = 90;
            Assert.That(KnownPersonQueries.AcquaintanceOf(w, n), Is.EqualTo(Acquaintance.Seen), "An enemy you never spoke to is still a stranger.");
            Assert.That(Labels(w, n).Intersect(SheetLabels.Concat(new[] { "Background", "Serves", "From" })), Is.Empty, "No sheet, no history.");
            n.ConversationCount = 1;
            n.PlayerRegard.Familiarity = KnownPersonQueries.FamiliarAt;
            Assert.That(KnownPersonQueries.AcquaintanceOf(w, n), Is.EqualTo(Acquaintance.Familiar), "An old acquaintance turned hostile is still known.");
            Assert.That(Labels(w, n), Has.No.Member("Abilities"), "But never a companion's numbers.");
        }

        [Test]
        public void TheDead_ShowAsDead_AndLearnNothingNew()
        {
            var (w, n) = Stranger();
            n.IsDeceased = true;
            var view = KnownPersonQueries.View(w, n);
            Assert.That(view.Lines.Single(l => l.Label == "Condition").Value, Is.EqualTo("dead"));
            Assert.That(view.Acquaintance, Is.EqualTo(Acquaintance.Seen), "Death does not make a stranger known.");
            Assert.That(Labels(w, n).Intersect(SheetLabels), Is.Empty);
        }

        [Test]
        public void ACompanionWhoLeaves_IsKnownWell_ButNoLongerMeasured()
        {
            var (w, n) = Stranger();
            n.ConversationCount = 3;
            n.PlayerRegard.Familiarity = 40;
            var record = new CompanionRecordData { NpcId = n.NpcId, Standing = CompanionStanding.InParty };
            w.Companions.Add(record);
            Assert.That(KnownPersonQueries.AcquaintanceOf(w, n), Is.EqualTo(Acquaintance.Companion));
            Assert.That(Labels(w, n), Has.Member("Abilities"));

            record.Standing = CompanionStanding.Dismissed;
            Assert.That(KnownPersonQueries.AcquaintanceOf(w, n), Is.EqualTo(Acquaintance.Acquainted), "Sent home below 50: an acquaintance again.");
            Assert.That(Labels(w, n), Has.No.Member("Abilities").And.No.Member("Hit points"));
            record.Standing = CompanionStanding.Departed;
            n.PlayerRegard.Familiarity = 60;
            Assert.That(KnownPersonQueries.AcquaintanceOf(w, n), Is.EqualTo(Acquaintance.Familiar), "Departed over a grievance but long known: familiar.");
        }

        [Test]
        public void TheRegard_AndWhatItReveals_SurviveASave()
        {
            var (w, n) = Stranger();
            var day = w.CurrentSimulationDate;
            n.ConversationCount = 2;
            for (var d = 0; d < 26; d++) PlayerCommandHandlers.Acquaint(n, day.AddDays(d), false);
            PlayerCommandHandlers.Acquaint(n, day.AddDays(25), true);
            var before = KnownPersonQueries.View(w, n);

            var serializer = new JsonWorldSerializer();
            var loaded = serializer.Deserialize(serializer.Serialize(w));
            Assert.That(loaded.IsSuccess, Is.True, string.Join("; ", loaded.Errors));
            var m = loaded.World.FindNpc(n.NpcId);
            Assert.That(m.PlayerRegard.Familiarity, Is.EqualTo(n.PlayerRegard.Familiarity));
            Assert.That(m.PlayerRegard.Conversations, Is.EqualTo(n.PlayerRegard.Conversations));
            Assert.That(m.PlayerRegard.LastTalkDay, Is.EqualTo(n.PlayerRegard.LastTalkDay));
            Assert.That(m.PlayerRegard.LastTradeDay, Is.EqualTo(n.PlayerRegard.LastTradeDay));
            var after = KnownPersonQueries.View(loaded.World, m);
            Assert.That(after.Acquaintance, Is.EqualTo(before.Acquaintance));
            Assert.That(after.Lines.Select(l => l.Label + "=" + l.Value), Is.EqualTo(before.Lines.Select(l => l.Label + "=" + l.Value)));

            PlayerCommandHandlers.Acquaint(m, day.AddDays(25), false);
            Assert.That(m.PlayerRegard.Familiarity, Is.EqualTo(n.PlayerRegard.Familiarity), "The day's talk already counted before the save; loading does not reset it.");
        }
    }
}
