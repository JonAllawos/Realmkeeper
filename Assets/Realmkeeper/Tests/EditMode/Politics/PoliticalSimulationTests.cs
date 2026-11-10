using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Domain.Economy;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Infrastructure.Randomness;
using Realmkeeper.Simulation;
using Realmkeeper.Simulation.Systems;

namespace Realmkeeper.Tests.EditMode.Politics
{
    /// <summary>
    /// A small hand-built political world: Ashford (1,000 people) and Brookmere (400), both
    /// governed by the Crown (a kingdom); House Vale (a noble house) present in Brookmere; the
    /// Carters (a guild) present in Ashford. Only the political system runs, so prosperity (50)
    /// and stability (60) stay fixed and every change is political.
    /// </summary>
    public sealed class PoliticalFixture
    {
        public WorldData World;
        public SettlementData Ashford;
        public SettlementData Brookmere;
        public FactionData Crown;
        public FactionData House;
        public FactionData Guild;

        public static readonly PoliticalRules Rules = new PoliticalRules();

        public static FactionId Fid(int n) => new FactionId(new Guid($"8d4f8f8e-6a43-4c1b-9d55-0d1f6f4af{n:000}"));

        public static PoliticalFixture Create(params string[] extraSystems)
        {
            var systems = new List<string> { PoliticalSystem.Id };
            systems.AddRange(extraSystems);
            var fx = new PoliticalFixture();
            fx.World = TestWorlds.TwoSettlements(out fx.Ashford, out fx.Brookmere, out _, systems.ToArray());
            fx.Ashford.Population = 1000;
            fx.Brookmere.Population = 400;

            fx.Crown = fx.Add(1, "The Crown", FactionType.Kingdom, 40);
            fx.House = fx.Add(2, "House Vale", FactionType.NobleHouse, 30);
            fx.Guild = fx.Add(3, "The Carters", FactionType.Guild, 30);
            fx.Ashford.FactionId = fx.Crown.FactionId;
            fx.Brookmere.FactionId = fx.Crown.FactionId;
            Present(fx.Crown, fx.Ashford, 70, 400);
            Present(fx.Crown, fx.Brookmere, 70, 150);
            Present(fx.House, fx.Brookmere, 40, 20);
            Present(fx.Guild, fx.Ashford, 50, 80);

            foreach (var a in fx.World.Factions)
            {
                foreach (var b in fx.World.Factions)
                {
                    if (a.FactionId.CompareTo(b.FactionId) < 0) fx.World.Relations.Add(new FactionRelationData { FactionA = a.FactionId, FactionB = b.FactionId, Trust = 50 });
                }
            }

            Assert.That(WorldValidator.Validate(fx.World).Errors, Is.Empty, "Fixture world is valid.");
            return fx;
        }

        public FactionData Add(int n, string name, FactionType type, int influence)
        {
            var f = new FactionData
            {
                FactionId = Fid(n), Name = name, Type = type, FoundedOn = TestWorlds.Start, Status = FactionStatus.Active,
                Influence = influence, InfluenceTarget = influence, LastReportedInfluence = influence,
            };
            World.Factions.Add(f);
            return f;
        }

        public static FactionPresenceData Present(FactionData f, SettlementData s, int strength, int members)
        {
            var p = new FactionPresenceData
            {
                SettlementId = s.SettlementId, Strength = strength, Members = members, Status = PresenceStatus.Established, EstablishedOn = TestWorlds.Start,
                LastReportedSharePerMille = PoliticalMath.SharePerMille(members, s.Population),
            };
            f.Presence.Add(p);
            return p;
        }

        public static FactionGoalData Goal(FactionData f, int n, GoalType type, int value = 0, SettlementId settlement = default, FactionId faction = default)
        {
            var g = new FactionGoalData
            {
                GoalId = new GoalId(new Guid($"8d4f8f8e-6a43-4c1b-9d55-0d1f6f4a9{n:000}")), Type = type, TargetValue = value, TargetSettlementId = settlement,
                TargetFactionId = faction, Priority = 3, Status = GoalStatus.Active, CreatedOn = TestWorlds.Start,
            };
            f.Goals.Add(g);
            return g;
        }

        public TerritorialClaimData Claim(int n, FactionData claimant, SettlementData s, ClaimStatus status)
        {
            var c = new TerritorialClaimData
            {
                ClaimId = new ClaimId(new Guid($"8d4f8f8e-6a43-4c1b-9d55-0d1f6f4ac{n:000}")), ClaimantId = claimant.FactionId, SettlementId = s.SettlementId,
                Status = status, FiledOn = TestWorlds.Start, UpdatedOn = TestWorlds.Start,
            };
            World.Claims.Add(c);
            return c;
        }

        public static SimulationScheduler Scheduler(PoliticalRules rules = null) =>
            StandardSimulationSystems.CreateScheduler(new Xoshiro256RandomFactory(), TestWorlds.ExactRules(), rules ?? new PoliticalRules());

        /// <summary>Advances whole weeks, so exactly <paramref name="ticks"/> political ticks run.</summary>
        public static WorldData Ticks(WorldData world, int ticks, PoliticalRules rules = null)
        {
            var result = Scheduler(rules).AdvanceDays(world, ticks * 7);
            Assert.That(result.Completed, Is.True, string.Join("; ", result.Errors));
            return result.World;
        }

        public static FactionData F(WorldData w, FactionData f) => w.FindFaction(f.FactionId);
    }

    public sealed class PoliticalSimulationTests
    {
        // ----- Influence ----------------------------------------------------------------------

        [Test]
        public void EveryFactionType_HasWeightsThatAddUpTo100_AndItsOwnProfile()
        {
            var rules = new PoliticalRules();
            rules.Validate();
            foreach (var type in FactionRules.AllTypes) Assert.That(rules.Profile(type).TotalWeight, Is.EqualTo(100), type.ToString());
            Assert.That(FactionRules.AllTypes.Count, Is.EqualTo(7));
            Assert.That(rules.Profile(FactionType.Kingdom).HoldingsWeight, Is.GreaterThan(rules.Profile(FactionType.Guild).HoldingsWeight), "Kingdoms rest on territory.");
            Assert.That(rules.Profile(FactionType.Guild).EconomyWeight, Is.GreaterThan(rules.Profile(FactionType.Kingdom).EconomyWeight), "Guilds rest on the economy.");
            Assert.That(rules.Profile(FactionType.ReligiousOrder).MembershipWeight, Is.GreaterThan(rules.Profile(FactionType.NobleHouse).MembershipWeight), "Religions rest on their following.");
            Assert.That(rules.Profile(FactionType.MercenaryCompany).WealthWeight, Is.GreaterThan(rules.Profile(FactionType.MilitaryOrder).WealthWeight), "Mercenaries rest on coin.");
        }

        [Test]
        public void InfluenceTarget_IsTheSumOfItsContributions_WithEachScoreFromItsOwnQuantity()
        {
            var fx = PoliticalFixture.Create();
            fx.Crown.Treasury = 750; // half of WealthForFullScore
            var parts = PoliticalMath.InfluenceContributions(fx.World, fx.Crown, PoliticalFixture.Rules);
            int Score(InfluenceFactor f) => parts.Single(p => p.Factor == f).Score;

            Assert.That(Score(InfluenceFactor.Membership), Is.EqualTo(550 * 1000 * 100 / (1400 * 500)), "550 members of 1,400 people against a 500 per mille full score.");
            Assert.That(Score(InfluenceFactor.Wealth), Is.EqualTo(50));
            Assert.That(Score(InfluenceFactor.Holdings), Is.EqualTo(100), "It governs everyone.");
            Assert.That(Score(InfluenceFactor.Presence), Is.EqualTo(70), "Strength 70 in both of two settlements.");
            Assert.That(Score(InfluenceFactor.Economy), Is.EqualTo(50), "Prosperity of the settlements it is present in.");
            Assert.That(Score(InfluenceFactor.Authority), Is.EqualTo(60), "Stability of the settlements it governs.");
            Assert.That(Score(InfluenceFactor.Reputation), Is.EqualTo(50), "Neutral opinions.");

            var expected = parts.Sum(p => p.Weight * p.Score) / 100;
            Assert.That(PoliticalMath.InfluenceTarget(parts), Is.EqualTo(expected));
            Assert.That(expected, Is.EqualTo((15 * 78 + 10 * 50 + 30 * 100 + 10 * 70 + 5 * 50 + 20 * 60 + 10 * 50) / 100));
        }

        [Test]
        public void SameWorld_DifferentFactionType_DifferentInfluence()
        {
            var fx = PoliticalFixture.Create();
            var asKingdom = PoliticalMath.InfluenceTarget(PoliticalMath.InfluenceContributions(fx.World, fx.Crown, PoliticalFixture.Rules));
            fx.Crown.Type = FactionType.NobleHouse; // same holdings, members and presence
            var asHouse = PoliticalMath.InfluenceTarget(PoliticalMath.InfluenceContributions(fx.World, fx.Crown, PoliticalFixture.Rules));
            Assert.That(asHouse, Is.Not.EqualTo(asKingdom));
        }

        [Test]
        public void Influence_StaysInBounds_AtBothExtremes()
        {
            var fx = PoliticalFixture.Create();
            var nobody = fx.Add(9, "Nobody", FactionType.CriminalOrganization, 0); // no presence, no coin, reputation weight 0
            Assert.That(PoliticalMath.InfluenceTarget(PoliticalMath.InfluenceContributions(fx.World, nobody, PoliticalFixture.Rules)), Is.EqualTo(0));

            fx.Crown.Treasury = int.MaxValue;
            fx.Crown.Presence.ForEach(p => { p.Strength = 100; p.Members = fx.World.FindSettlement(p.SettlementId).Population; });
            fx.House.Presence.Clear();
            fx.Guild.Presence.Clear();
            fx.Ashford.Stability = fx.Brookmere.Stability = fx.Ashford.Prosperity = fx.Brookmere.Prosperity = 100;
            fx.World.Relations.ForEach(r => r.Opinion = 100);
            Assert.That(PoliticalMath.InfluenceTarget(PoliticalMath.InfluenceContributions(fx.World, fx.Crown, PoliticalFixture.Rules)), Is.EqualTo(100));
        }

        [Test]
        public void EconomicConditions_MoveInfluence_ByExactlyTheDocumentedWeights()
        {
            var fx = PoliticalFixture.Create();
            int Target(FactionData f) => PoliticalMath.InfluenceTarget(PoliticalMath.InfluenceContributions(fx.World, f, PoliticalFixture.Rules));

            // A guild's economy score follows trade activity where it is present (weight 25).
            fx.Ashford.Economy.TradeActivity = 0;
            var quiet = PoliticalMath.InfluenceContributions(fx.World, fx.Guild, PoliticalFixture.Rules).Single(p => p.Factor == InfluenceFactor.Economy);
            fx.Ashford.Economy.TradeActivity = 600;
            var busy = PoliticalMath.InfluenceContributions(fx.World, fx.Guild, PoliticalFixture.Rules).Single(p => p.Factor == InfluenceFactor.Economy);
            Assert.That(quiet.Score, Is.EqualTo(0));
            Assert.That(busy.Score, Is.EqualTo(100));
            Assert.That(busy.Hundredths - quiet.Hundredths, Is.EqualTo(25 * 100));

            // A kingdom's authority score is the stability of what it governs (weight 20).
            var steady = Target(fx.Crown);
            fx.Ashford.Stability = fx.Brookmere.Stability = 20; // from 60
            Assert.That(steady - Target(fx.Crown), Is.EqualTo(20 * 40 / 100), "40 points of stability at weight 20.");

            // Trade activity does not touch a kingdom's score; stability does not touch a guild's.
            var guildNow = Target(fx.Guild);
            fx.Ashford.Stability = 90;
            Assert.That(Target(fx.Guild), Is.EqualTo(guildNow));
        }

        [Test]
        public void Influence_MovesTowardItsTarget_AtMostTwoPointsATick_AndNeverPastIt()
        {
            var fx = PoliticalFixture.Create();
            fx.Crown.Influence = 10;
            var world = fx.World;
            var previous = 10;
            for (var tick = 1; tick <= 40; tick++)
            {
                world = PoliticalFixture.Ticks(world, 1);
                var f = PoliticalFixture.F(world, fx.Crown);
                Assert.That(f.Influence - previous, Is.InRange(0, 2), $"Tick {tick}");
                Assert.That(f.Influence, Is.LessThanOrEqualTo(Math.Max(previous, f.InfluenceTarget)), "Never overshoots.");
                previous = f.Influence;
            }

            Assert.That(previous, Is.EqualTo(PoliticalFixture.F(world, fx.Crown).InfluenceTarget), "It arrives.");
        }

        [Test]
        public void InfluenceMilestones_AreReportedEveryTenPoints_WithTheLargestFactor()
        {
            var fx = PoliticalFixture.Create();
            fx.Crown.Influence = 10;
            fx.Crown.LastReportedInfluence = 10;
            var world = PoliticalFixture.Ticks(fx.World, 12); // +24 points
            var events = world.HistoricalEvents.Where(e => e.EventType == HistoricalEventType.FactionInfluenceChanged && e.FactionIds[0] == fx.Crown.FactionId).ToList();

            Assert.That(events.Count, Is.EqualTo(2), "At 20 and 30, not every tick.");
            Assert.That(events[0].GetInt("from"), Is.EqualTo(10));
            Assert.That(events[0].GetInt("to"), Is.EqualTo(20));
            Assert.That(events[0].Get("factor"), Is.EqualTo(InfluenceFactor.Holdings.ToString()));
            Assert.That(events[0].Get("source"), Is.EqualTo(PoliticalSystem.SimulationSource));
        }

        // ----- Dues -----------------------------------------------------------------------------

        [Test]
        public void Dues_MoveRealCoinFromSettlements_AndPoorSettlementsPayNothing()
        {
            var fx = PoliticalFixture.Create();
            fx.Brookmere.ResourceInventory.Set(ResourceTypes.Coin, 100); // reserve is 30 days × 4 = 120
            long Coin(WorldData w) => w.Settlements.Sum(s => (long)s.ResourceInventory.Get(ResourceTypes.Coin));
            var before = Coin(fx.World);

            var world = PoliticalFixture.Ticks(fx.World, 1);

            // Ashford: crown 400 × 20/1000 = 8, guild 80 × 80/1000 = 6. Brookmere pays nothing.
            Assert.That(before - Coin(world), Is.EqualTo(8 + 6));
            Assert.That(world.Settlements[1].ResourceInventory.Get(ResourceTypes.Coin), Is.EqualTo(100));
            Assert.That(PoliticalFixture.F(world, fx.Crown).Treasury, Is.EqualTo(8), "8 collected; 3% upkeep of 8 rounds to nothing.");
            Assert.That(PoliticalFixture.F(world, fx.Guild).Treasury, Is.EqualTo(6));
            Assert.That(PoliticalFixture.F(world, fx.House).Treasury, Is.EqualTo(0));
            Assert.That(world.Settlements[0].Economy.FindFlow(ResourceTypes.Coin).Exported, Is.EqualTo(14), "Recorded in the settlement's ledger.");
        }

        [Test]
        public void Upkeep_SpendsThreePercentOfTheTreasuryEachTick()
        {
            var fx = PoliticalFixture.Create();
            fx.House.Treasury = 1000;
            fx.House.Presence.Clear(); // no dues
            var world = PoliticalFixture.Ticks(fx.World, 1);
            Assert.That(PoliticalFixture.F(world, fx.House).Treasury, Is.EqualTo(970));
        }

        // ----- Presence and membership -----------------------------------------------------------

        [Test]
        public void Membership_FollowsStabilityWhereAFactionGoverns_Gradually()
        {
            var fx = PoliticalFixture.Create();
            fx.Ashford.Stability = 20; // hardship under the crown
            var crownInAshford = fx.Crown.FindPresence(fx.Ashford.SettlementId);
            var target = PoliticalMath.MembershipTarget(fx.Crown, crownInAshford, fx.Ashford, PoliticalFixture.Rules);
            Assert.That(target, Is.EqualTo(1000 * (600 * 70 * 20 / 10000) / 1000), "Population × max share × strength × stability.");
            Assert.That(target, Is.LessThan(400));

            var world = fx.World;
            var previous = 400;
            for (var tick = 1; tick <= 8; tick++)
            {
                world = PoliticalFixture.Ticks(world, 1);
                var members = PoliticalFixture.F(world, fx.Crown).FindPresence(fx.Ashford.SettlementId).Members;
                Assert.That(previous - members, Is.InRange(1, 10), $"Tick {tick}: at most population / 100 people change sides.");
                previous = members;
            }
        }

        [Test]
        public void AffiliationShift_IsChronicledOnceItReachesFivePointsOfThePopulation()
        {
            var fx = PoliticalFixture.Create();
            fx.Ashford.Stability = 20;
            var world = PoliticalFixture.Ticks(fx.World, 12); // up to 120 people = 12 points
            var shifts = world.HistoricalEvents.Where(e => e.EventType == HistoricalEventType.AffiliationShifted && e.FactionIds[0] == fx.Crown.FactionId
                                                           && e.SettlementId == fx.Ashford.SettlementId).ToList();
            Assert.That(shifts.Count, Is.EqualTo(2), "At −5 and −10 points, not every tick.");
            Assert.That(shifts[0].Get("direction"), Is.EqualTo("fell"));
            Assert.That(shifts[0].GetInt("fromPerMille"), Is.EqualTo(400));
            Assert.That(shifts[0].GetInt("toPerMille"), Is.EqualTo(350));
        }

        [Test]
        public void Members_NeverExceedThePopulation_EvenWhenItCollapsesBetweenTicks()
        {
            var fx = PoliticalFixture.Create();
            // Something earlier in the same day (here a test "plague" at order 420, in the game starvation)
            // shrinks Ashford to 300 people: crown 400 + guild 80 members no longer fit.
            fx.World.WorldSettings.EnabledSimulationSystems.Add("plague");
            var scheduler = PoliticalFixture.Scheduler();
            scheduler.Register(new PlagueSystem(fx.Ashford.SettlementId, 300));
            Assert.That(PoliticalMath.IsTickDay(fx.World.CurrentSimulationDate.AddDays(1), PoliticalFixture.Rules), Is.False, "Fixture: not a tick day.");

            var step = scheduler.AdvanceDay(fx.World);
            Assert.That(step.Committed, Is.True, string.Join("; ", step.Errors));
            var total = step.World.Factions.Sum(f => f.FindPresence(fx.Ashford.SettlementId)?.Members ?? 0);
            Assert.That(total, Is.LessThanOrEqualTo(300));
            Assert.That(PoliticalFixture.F(step.World, fx.Crown).FindPresence(fx.Ashford.SettlementId).Members, Is.EqualTo(400 * 300 / 480).Or.EqualTo(300 * 300 / 380),
                "Scaled down in proportion.");
        }

        [Test]
        public void Presence_GrowsWhereAFactionGoverns_AndIsPressedByStrongerRivals()
        {
            var fx = PoliticalFixture.Create();
            var houseInBrookmere = fx.House.FindPresence(fx.Brookmere.SettlementId);
            var calm = PoliticalMath.PresenceTarget(fx.World, fx.House, houseInBrookmere, fx.Brookmere, PoliticalFixture.Rules);
            Assert.That(calm, Is.EqualTo(30 + 30 / 2));

            fx.World.FindRelation(fx.House.FactionId, fx.Crown.FactionId).Opinion = -60; // hostile, and the crown is stronger there
            Assert.That(PoliticalMath.PresenceTarget(fx.World, fx.House, houseInBrookmere, fx.Brookmere, PoliticalFixture.Rules), Is.EqualTo(calm - 5));

            var crownInBrookmere = fx.Crown.FindPresence(fx.Brookmere.SettlementId);
            Assert.That(PoliticalMath.PresenceTarget(fx.World, fx.Crown, crownInBrookmere, fx.Brookmere, PoliticalFixture.Rules), Is.EqualTo(30 + 40 / 2 + 25),
                "Governing adds 25; the weaker house does not press it.");

            houseInBrookmere.Strength = 26;
            var world = PoliticalFixture.Ticks(fx.World, 3);
            var after = PoliticalFixture.F(world, fx.House).FindPresence(fx.Brookmere.SettlementId);
            Assert.That(after.Strength, Is.EqualTo(29), "One point a tick.");
            Assert.That(after.Status, Is.EqualTo(PresenceStatus.Established));
        }

        // ----- Relationships ----------------------------------------------------------------------

        [TestCase(-50, 0, AgreementType.None, RelationState.Hostile)]
        [TestCase(-50, 0, AgreementType.MutualSupport, RelationState.Hostile)]
        [TestCase(40, 100, AgreementType.MutualSupport, RelationState.Allied)]
        [TestCase(39, 0, AgreementType.MutualSupport, RelationState.Friendly)]
        [TestCase(80, 0, AgreementType.TradeAgreement, RelationState.Friendly)]
        [TestCase(-25, 0, AgreementType.None, RelationState.Rival)]
        [TestCase(24, 60, AgreementType.None, RelationState.Rival)]
        [TestCase(25, 60, AgreementType.None, RelationState.Friendly)]
        [TestCase(-10, 0, AgreementType.None, RelationState.Wary)]
        [TestCase(0, 30, AgreementType.None, RelationState.Wary)]
        [TestCase(-9, 29, AgreementType.None, RelationState.Neutral)]
        [TestCase(24, 0, AgreementType.None, RelationState.Neutral)]
        public void RelationState_FollowsTheDocumentedThresholds(int opinion, int rivalry, AgreementType agreement, RelationState expected)
        {
            Assert.That(PoliticalMath.StateOf(opinion, rivalry, agreement), Is.EqualTo(expected));
        }

        [Test]
        public void EveryOpinionAndRivalry_MapsToExactlyOneState()
        {
            foreach (AgreementType agreement in Enum.GetValues(typeof(AgreementType)))
            {
                for (var opinion = -100; opinion <= 100; opinion++)
                {
                    for (var rivalry = 0; rivalry <= 100; rivalry += 5)
                    {
                        var state = PoliticalMath.StateOf(opinion, rivalry, agreement);
                        Assert.That(Enum.IsDefined(typeof(RelationState), state), Is.True);
                        if (state == RelationState.Allied || state == RelationState.Friendly) Assert.That(opinion, Is.GreaterThanOrEqualTo(25));
                        if (state == RelationState.Hostile) Assert.That(opinion, Is.LessThanOrEqualTo(-50));
                    }
                }
            }
        }

        [Test]
        public void RelationEvaluation_NamesEveryTermAndItsCause()
        {
            var fx = PoliticalFixture.Create();
            var crownHouse = fx.World.FindRelation(fx.Crown.FactionId, fx.House.FactionId);
            var crownGuild = fx.World.FindRelation(fx.Crown.FactionId, fx.Guild.FactionId);

            // Crown and house can both govern and share Brookmere: competitors.
            var e = PoliticalMath.EvaluateRelation(fx.World, fx.Crown, fx.House, crownHouse, PoliticalFixture.Rules);
            Assert.That(e.RivalryTarget, Is.EqualTo(20));
            Assert.That(e.OpinionTarget, Is.EqualTo(-10));
            Assert.That(e.MainCause, Is.EqualTo(RelationCause.Competition));

            // The house administering Brookmere for the crown is political support (+15).
            fx.Brookmere.AdministratorFactionId = fx.House.FactionId;
            e = PoliticalMath.EvaluateRelation(fx.World, fx.Crown, fx.House, crownHouse, PoliticalFixture.Rules);
            Assert.That(e.RivalryTarget, Is.EqualTo(0), "A governor and its administrator do not compete.");
            Assert.That(e.OpinionTarget, Is.EqualTo(15));
            Assert.That(e.MainCause, Is.EqualTo(RelationCause.PoliticalSupport));

            // A contested claim: +40 rivalry and −30 opinion.
            fx.Claim(1, fx.House, fx.Ashford, ClaimStatus.Contested);
            e = PoliticalMath.EvaluateRelation(fx.World, fx.Crown, fx.House, crownHouse, PoliticalFixture.Rules);
            Assert.That(e.RivalryTarget, Is.EqualTo(40));
            Assert.That(e.OpinionTarget, Is.EqualTo(15 - 20 - 30));
            Assert.That(e.Terms.Select(t => t.Cause), Is.EquivalentTo(new[] { RelationCause.PoliticalSupport, RelationCause.Competition, RelationCause.TerritorialDispute }));

            // Crown and guild are not competitors: sharing a prosperous Ashford warms them; an agreement more.
            fx.Ashford.Prosperity = 70;
            crownGuild.Agreement = AgreementType.TradeAgreement;
            crownGuild.AgreementSince = TestWorlds.Start;
            e = PoliticalMath.EvaluateRelation(fx.World, fx.Crown, fx.Guild, crownGuild, PoliticalFixture.Rules);
            Assert.That(e.RivalryTarget, Is.EqualTo(0));
            Assert.That(e.OpinionTarget, Is.EqualTo(20 + 10));
            Assert.That(e.MainCause, Is.EqualTo(RelationCause.Agreement));
        }

        [Test]
        public void ContestedClaim_SoursARelationship_Gradually_AndTheChronicleSaysWhy()
        {
            var fx = PoliticalFixture.Create();
            fx.Claim(1, fx.House, fx.Ashford, ClaimStatus.Contested);
            var world = PoliticalFixture.Ticks(fx.World, 1);
            var r = world.FindRelation(fx.Crown.FactionId, fx.House.FactionId);
            Assert.That(r.Opinion, Is.EqualTo(-2), "Two points a tick.");
            Assert.That(r.Rivalry, Is.EqualTo(3), "Three points a tick.");

            world = PoliticalFixture.Ticks(world, 14);
            r = world.FindRelation(fx.Crown.FactionId, fx.House.FactionId);
            Assert.That(r.Opinion, Is.EqualTo(-30));
            Assert.That(r.Rivalry, Is.EqualTo(45));
            Assert.That(PoliticalMath.StateOf(r), Is.EqualTo(RelationState.Rival));

            var changes = world.HistoricalEvents.Where(e => e.EventType == HistoricalEventType.RelationshipChanged).ToList();
            Assert.That(changes.Select(e => e.Get("to")), Is.EqualTo(new[] { "Wary", "Rival" }), "One entry per change of state, not one per tick.");
            Assert.That(changes.All(e => e.FactionIds.Contains(fx.Crown.FactionId) && e.FactionIds.Contains(fx.House.FactionId)), Is.True);
            Assert.That(changes[1].Get("cause"), Is.EqualTo(RelationCause.Competition.ToString()).Or.EqualTo(RelationCause.TerritorialDispute.ToString()));

            world = PoliticalFixture.Ticks(world, 40);
            r = world.FindRelation(fx.Crown.FactionId, fx.House.FactionId);
            Assert.That(r.Opinion, Is.EqualTo(-60), "Settles at the target (−30 competition, −30 dispute).");
            Assert.That(r.Rivalry, Is.EqualTo(60));
        }

        [Test]
        public void Trust_GrowsUnderAnAgreement_AndDriftsBackWithoutOne()
        {
            var fx = PoliticalFixture.Create();
            var r = fx.World.FindRelation(fx.Crown.FactionId, fx.Guild.FactionId);
            r.Agreement = AgreementType.TradeAgreement;
            r.AgreementSince = TestWorlds.Start;
            var other = fx.World.FindRelation(fx.House.FactionId, fx.Guild.FactionId);
            other.Trust = 80;

            var world = PoliticalFixture.Ticks(fx.World, 5);

            Assert.That(world.FindRelation(fx.Crown.FactionId, fx.Guild.FactionId).Trust, Is.EqualTo(55));
            Assert.That(world.FindRelation(fx.House.FactionId, fx.Guild.FactionId).Trust, Is.EqualTo(75));
        }

        // ----- Goals -------------------------------------------------------------------------------

        [Test]
        public void Goal_CompletesWhenTheWorldReachesIt_NotBefore_AndStaysInTheRecord()
        {
            var fx = PoliticalFixture.Create();
            fx.Crown.Influence = 40; // heading up toward its target, 2 a tick
            var goal = PoliticalFixture.Goal(fx.Crown, 1, GoalType.GainInfluence, 44);

            var world = PoliticalFixture.Ticks(fx.World, 1);
            var g = PoliticalFixture.F(world, fx.Crown).FindGoal(goal.GoalId);
            Assert.That(g.Status, Is.EqualTo(GoalStatus.Active), "Influence 42 is not 44.");

            world = PoliticalFixture.Ticks(world, 1); // goals are evaluated before influence moves: still 42 when checked
            Assert.That(PoliticalFixture.F(world, fx.Crown).FindGoal(goal.GoalId).Status, Is.EqualTo(GoalStatus.Active));
            Assert.That(PoliticalFixture.F(world, fx.Crown).Influence, Is.EqualTo(44));

            world = PoliticalFixture.Ticks(world, 1);
            g = PoliticalFixture.F(world, fx.Crown).FindGoal(goal.GoalId);
            Assert.That(g.Status, Is.EqualTo(GoalStatus.Completed));
            Assert.That(g.CloseReason, Is.EqualTo(GoalCloseReason.Achieved));
            Assert.That(g.ClosedOn.ToDayNumber() % 7, Is.EqualTo(0), "Closed on a political tick day.");
            Assert.That(world.CurrentSimulationDate.ToDayNumber() - g.ClosedOn.ToDayNumber(), Is.InRange(0, 6));
            var e = world.HistoricalEvents.Single(x => x.EventType == HistoricalEventType.FactionGoalCompleted);
            Assert.That(e.Get("goal"), Is.EqualTo(goal.GoalId.ToString()));

            world = PoliticalFixture.Ticks(world, 10);
            Assert.That(PoliticalFixture.F(world, fx.Crown).FindGoal(goal.GoalId).Status, Is.EqualTo(GoalStatus.Completed), "A completed goal stays, as history.");
            Assert.That(world.HistoricalEvents.Count(x => x.EventType == HistoricalEventType.FactionGoalCompleted), Is.EqualTo(1), "Reported once.");
        }

        [Test]
        public void UnreachableGoal_NeverCompletes_AndContinuousGoalsOnlyTrackProgress()
        {
            var fx = PoliticalFixture.Create();
            var impossible = PoliticalFixture.Goal(fx.Guild, 1, GoalType.GainInfluence, 100);
            var control = PoliticalFixture.Goal(fx.Crown, 2, GoalType.MaintainControl);
            fx.Brookmere.Stability = 30; // below the control threshold of 40

            var world = PoliticalFixture.Ticks(fx.World, 30);

            var g = PoliticalFixture.F(world, fx.Guild).FindGoal(impossible.GoalId);
            Assert.That(g.Status, Is.EqualTo(GoalStatus.Active));
            Assert.That(g.Progress, Is.EqualTo(PoliticalFixture.F(world, fx.Guild).Influence).Within(2), "Progress is influence / 100.");
            var c = PoliticalFixture.F(world, fx.Crown).FindGoal(control.GoalId);
            Assert.That(c.Status, Is.EqualTo(GoalStatus.Active));
            Assert.That(c.Progress, Is.EqualTo(50), "One of its two settlements is steady.");
        }

        [TestCase(GoalType.IncreaseMembership, 160, 50)]
        [TestCase(GoalType.EstablishPresence, 80, 62)]
        [TestCase(GoalType.ImproveRelationship, 60, 62)]
        public void GoalProgress_IsComputedFromWorldValues(GoalType type, int target, int expected)
        {
            var fx = PoliticalFixture.Create();
            var goal = new FactionGoalData
            {
                Type = type, TargetValue = target,
                TargetSettlementId = type == GoalType.EstablishPresence ? fx.Ashford.SettlementId : default,
                TargetFactionId = type == GoalType.ImproveRelationship ? fx.Crown.FactionId : default,
            };
            // Guild: 80 members; strength 50 in Ashford; opinion 0 with the crown.
            Assert.That(PoliticalMath.GoalProgress(fx.World, fx.Guild, goal, PoliticalFixture.Rules), Is.EqualTo(expected));
        }

        [Test]
        public void SecureClaimProgress_FollowsTheClaimsStatus()
        {
            var fx = PoliticalFixture.Create();
            var goal = new FactionGoalData { Type = GoalType.SecureClaim, TargetSettlementId = fx.Brookmere.SettlementId };
            int Progress() => PoliticalMath.GoalProgress(fx.World, fx.House, goal, PoliticalFixture.Rules);
            Assert.That(Progress(), Is.EqualTo(0));
            var claim = fx.Claim(1, fx.House, fx.Brookmere, ClaimStatus.Unresolved);
            Assert.That(Progress(), Is.EqualTo(25));
            claim.Status = ClaimStatus.Contested;
            Assert.That(Progress(), Is.EqualTo(50));
            claim.Status = ClaimStatus.Recognized;
            Assert.That(Progress(), Is.EqualTo(100));
            claim.Status = ClaimStatus.Relinquished;
            Assert.That(Progress(), Is.EqualTo(0));
            fx.Brookmere.FactionId = fx.House.FactionId;
            Assert.That(Progress(), Is.EqualTo(100), "Governing it is better than any claim.");
        }

        [Test]
        public void Goals_MakeFactionsAct_OpenAPresence_ThenFileOneClaim()
        {
            var fx = PoliticalFixture.Create();
            PoliticalFixture.Goal(fx.Guild, 1, GoalType.EstablishPresence, 30, fx.Brookmere.SettlementId);
            PoliticalFixture.Goal(fx.House, 2, GoalType.SecureClaim, 0, fx.Brookmere.SettlementId);

            var world = PoliticalFixture.Ticks(fx.World, 1);

            var opened = PoliticalFixture.F(world, fx.Guild).FindPresence(fx.Brookmere.SettlementId);
            Assert.That(opened, Is.Not.Null, "Influence 30 ≥ 25: the guild opens a branch.");
            Assert.That(opened.Strength, Is.EqualTo(5));
            Assert.That(opened.Status, Is.EqualTo(PresenceStatus.Waning));
            Assert.That(world.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.FactionPresenceEstablished), Is.EqualTo(1));
            Assert.That(world.Claims.Count, Is.EqualTo(1), "House Vale (strength 40 ≥ 30) files its claim.");
            Assert.That(world.Claims[0].IsManual, Is.False);
            Assert.That(world.Settlements[1].FactionId, Is.EqualTo(fx.Crown.FactionId), "Filing a claim changes nothing about who governs.");

            world = PoliticalFixture.Ticks(world, 10);
            Assert.That(world.Claims.Count, Is.EqualTo(1), "Never a second claim on the same settlement.");
            Assert.That(PoliticalFixture.F(world, fx.Guild).Presence.Count(p => p.SettlementId == fx.Brookmere.SettlementId), Is.EqualTo(1));
            Assert.That(PoliticalFixture.F(world, fx.Guild).FindPresence(fx.Brookmere.SettlementId).Strength, Is.GreaterThan(5 + 10), "The goal adds a point a tick on top of drift.");
            Assert.That(WorldValidator.Validate(world).Errors, Is.Empty);
        }

        [Test]
        public void WeakFaction_DoesNotExpand()
        {
            var fx = PoliticalFixture.Create();
            fx.Guild.Influence = 10;
            fx.Guild.Presence.Clear(); // target stays low
            PoliticalFixture.Goal(fx.Guild, 1, GoalType.EstablishPresence, 30, fx.Brookmere.SettlementId);
            var world = PoliticalFixture.Ticks(fx.World, 3);
            Assert.That(PoliticalFixture.F(world, fx.Guild).Presence, Is.Empty, "Below 25 influence it cannot open a branch.");
        }

        // ----- Claims --------------------------------------------------------------------------------

        [Test]
        public void UnansweredClaims_AreAnsweredByRelations_AndNeverMoveGovernance()
        {
            var fx = PoliticalFixture.Create();
            var claim = fx.Claim(1, fx.House, fx.Brookmere, ClaimStatus.Unresolved);

            var world = PoliticalFixture.Ticks(fx.World, 3);
            Assert.That(world.FindClaim(claim.ClaimId).Status, Is.EqualTo(ClaimStatus.Unresolved), "Wary relations: the crown does not answer.");

            world.FindRelation(fx.Crown.FactionId, fx.House.FactionId).Opinion = -40; // rival
            world = PoliticalFixture.Ticks(world, 1);
            Assert.That(world.FindClaim(claim.ClaimId).Status, Is.EqualTo(ClaimStatus.Contested));
            var e = world.HistoricalEvents.Single(x => x.EventType == HistoricalEventType.TerritorialClaimContested);
            Assert.That(e.Get("cause"), Is.EqualTo("poorRelations"));
            Assert.That(e.FactionIds, Is.EqualTo(new[] { fx.House.FactionId, fx.Crown.FactionId }));

            world = PoliticalFixture.Ticks(world, 30);
            Assert.That(world.Settlements[1].FactionId, Is.EqualTo(fx.Crown.FactionId), "A contested claim is still only a claim.");
            Assert.That(world.FindClaim(claim.ClaimId).Status, Is.EqualTo(ClaimStatus.Contested), "A decision stands until someone changes it.");
        }

        [Test]
        public void Claims_AreRecognizedForFriendsAndAdministrators_ContestedWhenTheyCompete_FulfilledByGovernance()
        {
            var fx = PoliticalFixture.Create();
            var order = fx.Add(4, "Order of the Wall", FactionType.MilitaryOrder, 30);
            PoliticalFixture.Present(order, fx.Ashford, 30, 10);
            foreach (var f in new[] { fx.Crown, fx.House, fx.Guild })
            {
                var (a, b) = PoliticalMath.OrderPair(f.FactionId, order.FactionId);
                fx.World.Relations.Add(new FactionRelationData { FactionA = a, FactionB = b, Trust = 50 });
            }

            // Friendly claimant → recognized.
            fx.World.FindRelation(fx.Crown.FactionId, order.FactionId).Opinion = 40;
            var friendly = fx.Claim(1, order, fx.Ashford, ClaimStatus.Unresolved);
            var world = PoliticalFixture.Ticks(fx.World, 1);
            Assert.That(world.FindClaim(friendly.ClaimId).Status, Is.EqualTo(ClaimStatus.Recognized));
            Assert.That(world.Settlements[0].FactionId, Is.EqualTo(fx.Crown.FactionId), "Recognition is not a transfer.");

            // Two unanswered claims on one settlement contest each other.
            var fx2 = PoliticalFixture.Create();
            var order2 = fx2.Add(4, "Order of the Wall", FactionType.MilitaryOrder, 30);
            PoliticalFixture.Present(order2, fx2.Brookmere, 30, 10);
            var first = fx2.Claim(1, fx2.House, fx2.Brookmere, ClaimStatus.Unresolved);
            var second = fx2.Claim(2, order2, fx2.Brookmere, ClaimStatus.Unresolved);
            var world2 = PoliticalFixture.Ticks(fx2.World, 1);
            Assert.That(world2.FindClaim(first.ClaimId).Status, Is.EqualTo(ClaimStatus.Contested));
            Assert.That(world2.FindClaim(second.ClaimId).Status, Is.EqualTo(ClaimStatus.Contested));
            Assert.That(world2.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.TerritorialClaimContested && e.Get("cause") == "competingClaim"), Is.EqualTo(2));

            // The administrator's claim is recognized; a claimant that comes to govern has it fulfilled.
            var fx3 = PoliticalFixture.Create();
            fx3.Brookmere.AdministratorFactionId = fx3.House.FactionId;
            var admin = fx3.Claim(1, fx3.House, fx3.Brookmere, ClaimStatus.Unresolved);
            var world3 = PoliticalFixture.Ticks(fx3.World, 1);
            Assert.That(world3.FindClaim(admin.ClaimId).Status, Is.EqualTo(ClaimStatus.Recognized));
            world3.Settlements[1].AdministratorFactionId = default;
            world3.Settlements[1].FactionId = fx3.House.FactionId;
            world3 = PoliticalFixture.Ticks(world3, 1);
            Assert.That(world3.FindClaim(admin.ClaimId).Status, Is.EqualTo(ClaimStatus.Fulfilled));
            Assert.That(world3.FindClaim(admin.ClaimId).IsOpen, Is.False);
        }

        [Test]
        public void DecidedClaims_AreLeftAlone_WhateverRelationsDo()
        {
            var fx = PoliticalFixture.Create();
            var recognized = fx.Claim(1, fx.House, fx.Brookmere, ClaimStatus.Recognized);
            fx.World.FindRelation(fx.Crown.FactionId, fx.House.FactionId).Opinion = -80; // hostile: an unanswered claim would be contested

            var world = PoliticalFixture.Ticks(fx.World, 4);

            Assert.That(world.FindClaim(recognized.ClaimId).Status, Is.EqualTo(ClaimStatus.Recognized), "A decision already made stands.");
            Assert.That(world.FindClaim(recognized.ClaimId).UpdatedOn, Is.EqualTo(TestWorlds.Start));
            Assert.That(world.HistoricalEvents.Any(e => e.EventType == HistoricalEventType.TerritorialClaimContested || e.EventType == HistoricalEventType.TerritorialClaimResolved), Is.False,
                "And nothing about it is reported again.");
        }

        [Test]
        public void ClaimOnAnUngovernedSettlement_NeedsRealPresence()
        {
            var fx = PoliticalFixture.Create();
            fx.Brookmere.FactionId = default; // independent
            fx.Crown.Presence.RemoveAll(p => p.SettlementId == fx.Brookmere.SettlementId);
            var claim = fx.Claim(1, fx.House, fx.Brookmere, ClaimStatus.Unresolved);

            var world = PoliticalFixture.Ticks(fx.World, 2);
            Assert.That(world.FindClaim(claim.ClaimId).Status, Is.EqualTo(ClaimStatus.Unresolved), "Strength 40-odd is below 50.");

            PoliticalFixture.F(world, fx.House).FindPresence(fx.Brookmere.SettlementId).Strength = 60; // drifts a point a tick toward its target, so start well above 50
            world = PoliticalFixture.Ticks(world, 1);
            Assert.That(world.FindClaim(claim.ClaimId).Status, Is.EqualTo(ClaimStatus.Recognized));
            Assert.That(world.Settlements[1].FactionId.IsEmpty, Is.True, "Still independent.");
        }

        // ----- Status and dissolution ---------------------------------------------------------------

        [Test]
        public void Status_DeclinesBelow20_RecoversAt25_NotBefore()
        {
            var fx = PoliticalFixture.Create();
            fx.Guild.Influence = 21;
            fx.Guild.Presence.Clear(); // target falls to about 5
            var world = PoliticalFixture.Ticks(fx.World, 1);
            Assert.That(PoliticalFixture.F(world, fx.Guild).Influence, Is.EqualTo(19));
            Assert.That(PoliticalFixture.F(world, fx.Guild).Status, Is.EqualTo(FactionStatus.Declining));
            var e = world.HistoricalEvents.Single(x => x.EventType == HistoricalEventType.FactionStatusChanged);
            Assert.That(e.Get("to"), Is.EqualTo("Declining"));

            // Back in business: influence climbs, but 20..24 is still Declining (hysteresis).
            var guild = PoliticalFixture.F(world, fx.Guild);
            PoliticalFixture.Present(guild, world.Settlements[0], 80, 120);
            guild.Treasury = 1500;
            world = PoliticalFixture.Ticks(world, 2);
            Assert.That(PoliticalFixture.F(world, fx.Guild).Influence, Is.EqualTo(23));
            Assert.That(PoliticalFixture.F(world, fx.Guild).Status, Is.EqualTo(FactionStatus.Declining));
            world = PoliticalFixture.Ticks(world, 1);
            Assert.That(PoliticalFixture.F(world, fx.Guild).Influence, Is.EqualTo(25));
            Assert.That(PoliticalFixture.F(world, fx.Guild).Status, Is.EqualTo(FactionStatus.Active));
        }

        [Test]
        public void DormantFaction_DissolvesAfterASustainedPeriod_ButItsHistoryRemains()
        {
            var rules = new PoliticalRules { DissolveAfterDormantTicks = 3 };
            var fx = PoliticalFixture.Create();
            fx.Guild.Influence = 4;
            fx.Guild.Presence.Clear();
            fx.Guild.LastReportedInfluence = 4;
            var goal = PoliticalFixture.Goal(fx.Guild, 1, GoalType.GainInfluence, 50);
            var towardGuild = PoliticalFixture.Goal(fx.House, 2, GoalType.ImproveRelationship, 50, default, fx.Guild.FactionId);
            var pact = fx.World.FindRelation(fx.Crown.FactionId, fx.Guild.FactionId);
            pact.Agreement = AgreementType.TradeAgreement;
            pact.AgreementSince = TestWorlds.Start;
            pact.Opinion = -100; // keeps the guild's reputation, and so its influence target, at the floor
            fx.World.FindRelation(fx.House.FactionId, fx.Guild.FactionId).Opinion = -100;

            var world = PoliticalFixture.Ticks(fx.World, 2, rules);
            Assert.That(PoliticalFixture.F(world, fx.Guild).Status, Is.EqualTo(FactionStatus.Dormant));
            Assert.That(PoliticalFixture.F(world, fx.Guild).DormantTicks, Is.EqualTo(2));

            world = PoliticalFixture.Ticks(world, 1, rules);
            var guild = PoliticalFixture.F(world, fx.Guild);
            Assert.That(guild, Is.Not.Null, "A dissolved faction is never deleted.");
            Assert.That(guild.Status, Is.EqualTo(FactionStatus.Dissolved));
            Assert.That(guild.DissolvedOn.ToDayNumber() % 7, Is.EqualTo(0), "Dissolved on a political tick day.");
            Assert.That(guild.FindGoal(goal.GoalId).CloseReason, Is.EqualTo(GoalCloseReason.FactionDissolved));
            Assert.That(world.FindRelation(fx.Crown.FactionId, fx.Guild.FactionId).Agreement, Is.EqualTo(AgreementType.None));
            var dissolved = world.HistoricalEvents.Single(e => e.EventType == HistoricalEventType.FactionDissolved);
            Assert.That(dissolved.FactionIds, Is.EqualTo(new[] { fx.Guild.FactionId }));
            Assert.That(dissolved.Importance, Is.EqualTo(EventImportance.Major));
            Assert.That(WorldValidator.Validate(world).Errors, Is.Empty, "History still points at a faction that exists.");

            // Others' goals about it are closed as lost at the next tick; it never acts again.
            world = PoliticalFixture.Ticks(world, 5, rules);
            var lost = PoliticalFixture.F(world, fx.House).FindGoal(towardGuild.GoalId);
            Assert.That(lost.Status, Is.EqualTo(GoalStatus.Abandoned));
            Assert.That(lost.CloseReason, Is.EqualTo(GoalCloseReason.TargetLost));
            Assert.That(PoliticalFixture.F(world, fx.Guild).Status, Is.EqualTo(FactionStatus.Dissolved));
            Assert.That(world.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.FactionDissolved), Is.EqualTo(1));
            Assert.That(WorldValidator.Validate(world).Errors, Is.Empty);
        }

        [Test]
        public void DissolvingAGoverningFaction_FreesWhatItHeld()
        {
            var fx = PoliticalFixture.Create();
            fx.Brookmere.AdministratorFactionId = fx.House.FactionId;
            var claim = fx.Claim(1, fx.Crown, fx.Brookmere, ClaimStatus.Unresolved); // nonsensical but open
            fx.Brookmere.FactionId = fx.House.FactionId;
            fx.Brookmere.AdministratorFactionId = default;
            PoliticalFixture.Goal(fx.Crown, 1, GoalType.MaintainControl);

            PoliticalMath.Dissolve(fx.World, fx.Crown, fx.World.CurrentSimulationDate);

            Assert.That(fx.Ashford.FactionId.IsEmpty, Is.True, "Its settlement becomes independent.");
            Assert.That(fx.Brookmere.FactionId, Is.EqualTo(fx.House.FactionId), "Others' holdings are untouched.");
            Assert.That(fx.Crown.Presence, Is.Empty);
            Assert.That(fx.Crown.Goals.All(g => g.Status == GoalStatus.Abandoned && g.CloseReason == GoalCloseReason.FactionDissolved), Is.True);
            Assert.That(claim.Status, Is.EqualTo(ClaimStatus.Relinquished));
            Assert.That(fx.World.Factions, Does.Contain(fx.Crown));
            Assert.That(WorldValidator.Validate(fx.World).Errors, Is.Empty);
        }

        // ----- Cadence, determinism, atomicity --------------------------------------------------------

        [Test]
        public void PoliticalTick_RunsOnlyOnDaysDivisibleBySeven()
        {
            var fx = PoliticalFixture.Create();
            fx.Crown.Influence = 10;
            var scheduler = PoliticalFixture.Scheduler();
            var world = fx.World;
            var tickDays = new List<long>();
            for (var day = 0; day < 28; day++)
            {
                var before = PoliticalFixture.F(world, fx.Crown).Influence;
                world = scheduler.AdvanceDay(world).World;
                var number = world.CurrentSimulationDate.ToDayNumber();
                var changed = PoliticalFixture.F(world, fx.Crown).Influence != before;
                Assert.That(changed, Is.EqualTo(number % 7 == 0), $"Day number {number}");
                if (changed) tickDays.Add(number);
            }

            Assert.That(tickDays.Count, Is.EqualTo(4), "Four ticks in four weeks: none skipped, none doubled.");
            Assert.That(PoliticalFixture.F(world, fx.Crown).Influence, Is.EqualTo(10 + 4 * 2));
        }

        [Test]
        public void Politics_IsDeterministic_AndIndependentOfHowDaysAreBatched()
        {
            WorldData Build()
            {
                var fx = PoliticalFixture.Create();
                fx.Crown.Influence = 10;
                fx.Claim(1, fx.House, fx.Ashford, ClaimStatus.Contested);
                PoliticalFixture.Goal(fx.Guild, 1, GoalType.EstablishPresence, 30, fx.Brookmere.SettlementId);
                PoliticalFixture.Goal(fx.House, 2, GoalType.SecureClaim, 0, fx.Brookmere.SettlementId);
                return fx.World;
            }

            var oneRun = PoliticalFixture.Scheduler().AdvanceDays(Build(), 70).World;
            var daily = Build();
            var scheduler = PoliticalFixture.Scheduler();
            for (var i = 0; i < 70; i++) daily = scheduler.AdvanceDay(daily).World;

            Assert.That(WorldFingerprint.Of(daily), Is.EqualTo(WorldFingerprint.Of(oneRun)));
            Assert.That(WorldFingerprint.Of(PoliticalFixture.Scheduler().AdvanceDays(Build(), 70).World), Is.EqualTo(WorldFingerprint.Of(oneRun)));
            Assert.That(oneRun.Claims.Select(c => c.ClaimId).Distinct().Count(), Is.EqualTo(oneRun.Claims.Count));
        }

        [Test]
        public void FailedDay_LeavesNoPoliticalChangeBehind()
        {
            var fx = PoliticalFixture.Create("bomb");
            fx.Crown.Influence = 10;
            PoliticalFixture.Goal(fx.House, 2, GoalType.SecureClaim, 0, fx.Brookmere.SettlementId);
            var scheduler = PoliticalFixture.Scheduler();
            var world = fx.World;
            while ((world.CurrentSimulationDate.ToDayNumber() + 1) % 7 != 0) world = scheduler.AdvanceDay(world).World; // next day is a tick day
            var before = WorldFingerprint.Of(world);

            var failing = PoliticalFixture.Scheduler();
            failing.Register(new BombSystem());
            var result = failing.AdvanceDay(world);

            Assert.That(result.Committed, Is.False);
            Assert.That(WorldFingerprint.Of(result.World), Is.EqualTo(before), "The political tick that ran before the failure was discarded whole.");
            Assert.That(result.World.Claims, Is.Empty);
        }

        [Test]
        public void EconomyStillRunsEveryDay_WithPoliticsEnabled()
        {
            var world = TestWorlds.SingleSettlement(100, 1000, 0, TestWorlds.FoodSystems.Concat(new[] { PoliticalSystem.Id }).ToArray());
            var s = world.Settlements[0];
            var crown = new FactionData { FactionId = PoliticalFixture.Fid(1), Name = "The Crown", Type = FactionType.Kingdom, FoundedOn = TestWorlds.Start, Influence = 40 };
            world.Factions.Add(crown);
            s.FactionId = crown.FactionId;
            PoliticalFixture.Present(crown, s, 70, 40);

            var after = PoliticalFixture.Scheduler().AdvanceDays(world, 10).World;

            Assert.That(after.Settlements[0].FoodReserves, Is.EqualTo(1000 - 10 * 10), "Ten people-days of food a day, every day.");
        }

        private sealed class PlagueSystem : ISimulationSystem
        {
            private readonly SettlementId _id;
            private readonly int _population;

            public PlagueSystem(SettlementId id, int population)
            {
                _id = id;
                _population = population;
            }

            public string SystemId => "plague";
            public int ExecutionOrder => 420; // after population, before politics
            public void Execute(SimulationDayContext context) => context.World.FindSettlement(_id).Population = _population;
        }

        private sealed class BombSystem : ISimulationSystem
        {
            public string SystemId => "bomb";
            public int ExecutionOrder => 475; // after politics, before the chronicle
            public void Execute(SimulationDayContext context) => throw new InvalidOperationException("later system failed");
        }
    }
}
