using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Magic;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Tests.EditMode.Combat;
using Realmkeeper.Tests.EditMode.Phase2;

namespace Realmkeeper.Tests.EditMode.Magic
{
    /// <summary>
    /// Phase 9 costs: catalog load, lookup and validation; single-target, area and multi-target casts; concentration
    /// checks; a feature activation; caster AI decisions in an encounter with several casters; short and long rests
    /// through the session (each with the world's time advance); save and load with spellcasting data; the spellbook
    /// query. Ceilings are generous; the numbers are printed for the report.
    /// </summary>
    public sealed class MagicPerformanceTests
    {
        [Test]
        public void Magic_IsCheap()
        {
            var catalog = Stopwatch.StartNew();
            var count = SpellCatalog.All.Count;
            catalog.Stop();
            var lookup = Stopwatch.StartNew();
            for (var i = 0; i < 20000; i++) Assert.That(SpellCatalog.Find("fireball"), Is.Not.Null);
            lookup.Stop();
            var validate = Stopwatch.StartNew();
            for (var i = 0; i < 20; i++) Assert.That(SpellCatalog.Validate(), Is.Empty);
            validate.Stop();

            var dir = Phase2Worlds.TempDirectory();
            try
            {
                var session = Phase2Worlds.Session(dir);
                var w = SpellTests.Caster(session, "wizard", 9, new[] { "fire_bolt" }, new[] { "fireball", "magic_missile" }, new[] { "fireball", "magic_missile" });
                var n = 0;
                Guid G(string p) => new Guid(++n, 0, 0, new byte[8]);
                var setup = new EncounterSetup { Name = "Perf", Kind = EncounterKind.Other };
                setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(0, 0), Control = CombatControl.Player });
                for (var i = 0; i < 20; i++) setup.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "bandit", Side = "bandits", Position = new GridPos(10 + i % 5, -2 + i / 5) });
                var enc = CombatEngine.Create(w, setup, G, out var errors);
                Assert.That(enc, Is.Not.Null, string.Join("; ", errors));
                w.Encounters.Add(enc);
                var e = new CombatEngine(w, enc);
                e.Begin();
                var p = enc.Combatants.Single(c => c.Kind == CombatantKind.Player);
                var foe = enc.Combatants.First(c => c.Kind != CombatantKind.Player);

                void Fresh()
                {
                    enc.TurnIndex = enc.Order.IndexOf(p.CombatantId);
                    p.Turn = new TurnResources();
                    foreach (var c in enc.Combatants) if (c.Kind != CombatantKind.Player) { c.MaxHp = c.CurrentHp = 10000; c.State = VitalState.Conscious; c.Conditions.Clear(); }
                    foreach (var pair in new CharacterRules(w.Player.Rpg).ResourceMaximums()) w.Player.Rpg.Resources[pair.Key] = pair.Value;
                }

                const int N = 200;
                var single = Stopwatch.StartNew();
                for (var i = 0; i < N; i++) { Fresh(); Assert.That(e.Act(p.CombatantId, new CastSpellAction("fire_bolt", 0, new[] { foe.CombatantId })).Ok, Is.True); }
                single.Stop();
                var area = Stopwatch.StartNew();
                for (var i = 0; i < N; i++) { Fresh(); Assert.That(e.Act(p.CombatantId, new CastSpellAction("fireball", 3, null, new GridPos(12, 0))).Ok, Is.True); }
                area.Stop();
                var affected = e.AreaTargets(p, SpellCatalog.Find("fireball"), new GridPos(12, 0)).Count;
                var multi = Stopwatch.StartNew();
                for (var i = 0; i < N; i++) { Fresh(); Assert.That(e.Act(p.CombatantId, new CastSpellAction("magic_missile", 5, enc.Combatants.Where(c => c.Kind != CombatantKind.Player).Take(7).Select(c => c.CombatantId))).Ok, Is.True); }
                multi.Stop();

                // Concentration checks: damage on a concentrating caster.
                p.Concentration = new ConcentrationData { InstanceId = enc.NextInstanceId++, SpellId = "bless", Name = "Bless" };
                var conc = Stopwatch.StartNew();
                for (var i = 0; i < N; i++)
                {
                    if (p.Concentration == null) p.Concentration = new ConcentrationData { InstanceId = enc.NextInstanceId++, SpellId = "bless", Name = "Bless" };
                    w.Player.Rpg.CurrentHp = e.MaxHp(p);
                    e.ApplyDamage(p, 1, Realmkeeper.Domain.World.DamageType.Slashing, false, "perf");
                }

                conc.Stop();

                // A feature: Rage, with its effect.
                var barbarian = SpellTests.Caster(Phase2Worlds.Session(dir), "barbarian", 5);
                n = 1000;
                var e2 = new CombatEngine(barbarian, null == null ? CombatEngine.Create(barbarian, new EncounterSetup
                {
                    Participants =
                    {
                        new ParticipantSetup { Kind = CombatantKind.Player, Side = "player", Position = new GridPos(0, 0), Control = CombatControl.Player },
                        new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "bandit", Side = "bandits", Position = new GridPos(1, 0) },
                    },
                }, G, out _) : null);
                barbarian.Encounters.Add(e2.Encounter);
                e2.Begin();
                var bp = e2.Encounter.Combatants.Single(c => c.Kind == CombatantKind.Player);
                var feature = Stopwatch.StartNew();
                for (var i = 0; i < N; i++)
                {
                    e2.Encounter.TurnIndex = e2.Encounter.Order.IndexOf(bp.CombatantId);
                    bp.Turn = new TurnResources();
                    barbarian.Player.Rpg.Resources["rage"] = 3;
                    e2.Encounter.Effects.Clear();
                    Assert.That(e2.Act(bp.CombatantId, new UseFeatureAction("rage")).Ok, Is.True);
                }

                feature.Stop();

                // The caster AI: decisions in a fight with three casters on each side.
                var ai = Phase2Worlds.Generated(4242);
                var big = new EncounterSetup { Name = "Casters", Kind = EncounterKind.Other };
                for (var i = 0; i < 3; i++)
                {
                    big.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "acolyte", Side = "temple", Position = new GridPos(i, 0) });
                    big.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "guard", Side = "temple", Position = new GridPos(i, 1) });
                    big.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "cult_fanatic", Side = "cult", Position = new GridPos(i, 10) });
                    big.Participants.Add(new ParticipantSetup { Kind = CombatantKind.Creature, CreatureId = "bandit", Side = "cult", Position = new GridPos(i, 11) });
                }

                n = 2000;
                var aiEnc = CombatEngine.Create(ai, big, G, out var aiErrors);
                Assert.That(aiEnc, Is.Not.Null, string.Join("; ", aiErrors));
                ai.Encounters.Add(aiEnc);
                var e3 = new CombatEngine(ai, aiEnc);
                e3.Begin();
                var decide = Stopwatch.StartNew();
                var decisions = 0;
                for (; decisions < 5000 && aiEnc.IsActive; decisions++)
                {
                    var actor = aiEnc.Active;
                    Assert.That(e3.Act(actor.CombatantId, CombatAi.Next(e3, actor)).Ok, Is.True);
                }

                decide.Stop();
                Assert.That(aiEnc.IsActive, Is.False);

                // Rests through the session (each passes world time), save and load, the spellbook.
                var rs = Phase2Worlds.Session(dir);
                CombatTests.Create(rs, "cleric", CombatTests.Scores);
                var shortRest = Stopwatch.StartNew();
                for (var i = 0; i < 5; i++) Assert.That(rs.ShortRest().IsSuccess, Is.True);
                shortRest.Stop();
                var longRest = Stopwatch.StartNew();
                Assert.That(rs.LongRest().IsSuccess, Is.True);
                longRest.Stop();
                var book = Stopwatch.StartNew();
                for (var i = 0; i < 100; i++) Assert.That(rs.GetSpellbook(), Is.Not.Null);
                book.Stop();
                var serializer = new JsonWorldSerializer();
                var json = serializer.Serialize(w);
                var save = Stopwatch.StartNew();
                for (var i = 0; i < 5; i++) json = serializer.Serialize(w);
                save.Stop();
                var load = Stopwatch.StartNew();
                for (var i = 0; i < 5; i++) Assert.That(serializer.Deserialize(json).IsSuccess, Is.True);
                load.Stop();

                TestContext.Out.WriteLine(
                    $"PERF Phase9: catalog first load {catalog.Elapsed.TotalMilliseconds:F2} ms ({count} spells); lookup {lookup.Elapsed.TotalMilliseconds * 1000 / 20000:F2} µs; catalog validation {validate.Elapsed.TotalMilliseconds / 20:F2} ms; " +
                    $"single-target cast {single.Elapsed.TotalMilliseconds * 1000 / N:F0} µs; area cast ({affected} creatures) {area.Elapsed.TotalMilliseconds * 1000 / N:F0} µs; 7-dart Magic Missile {multi.Elapsed.TotalMilliseconds * 1000 / N:F0} µs; " +
                    $"concentration check {conc.Elapsed.TotalMilliseconds * 1000 / N:F0} µs; feature activation {feature.Elapsed.TotalMilliseconds * 1000 / N:F0} µs; " +
                    $"caster AI {decide.Elapsed.TotalMilliseconds * 1000 / Math.Max(1, decisions):F0} µs per decision over {decisions}; " +
                    $"short rest {shortRest.Elapsed.TotalMilliseconds / 5:F1} ms; long rest {longRest.Elapsed.TotalMilliseconds:F1} ms; spellbook query {book.Elapsed.TotalMilliseconds / 100:F2} ms; " +
                    $"save {save.Elapsed.TotalMilliseconds / 5:F1} ms; load {load.Elapsed.TotalMilliseconds / 5:F1} ms");

                Assert.That(single.Elapsed.TotalMilliseconds / N, Is.LessThan(20));
                Assert.That(area.Elapsed.TotalMilliseconds / N, Is.LessThan(50));
                Assert.That(decide.Elapsed.TotalMilliseconds / Math.Max(1, decisions), Is.LessThan(20));
                Assert.That(book.Elapsed.TotalMilliseconds / 100, Is.LessThan(50));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }
    }
}
