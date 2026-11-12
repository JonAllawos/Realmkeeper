using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Tests.EditMode.Living;
using FeatureKind = Realmkeeper.Domain.Places.FeatureKind;

namespace Realmkeeper.Tests.EditMode.Places
{
    /// <summary>
    /// Phase 14 (ADR-0069): crime is a record of what happened; only those who perceived it know, and only what they
    /// could tell. Played through the commands: stealing in front of someone and alone, being recognised or not, the news
    /// travelling only between people who meet, a merchant's refusal, settling in conversation, a household's warning,
    /// breaking in at night, and an assault that turns to murder.
    /// </summary>
    public sealed class Phase14CrimeTests : Phase14Fixture
    {
        /// <summary>
        /// A home and a minute when someone is at home, awake, in the same room as a household container with something in
        /// it — found by asking the indoor schedule itself (so the test does not guess). The player stands inside.
        /// </summary>
        private (PlaceData Home, PlaceFeature Box, NpcData Owner) InsideWithSomeone(bool alone)
        {
            H.Hero();
            var town = Realmkeeper.Simulation.Quests.QuestGenesis.FirstTown(W);
            Ok(S.Dispatch(new EnterSettlementCommand(town.SettlementId)));
            var layout = ExplorationModule.LayoutFor(W, W.FindSettlement(town.SettlementId));
            foreach (var b in layout.Buildings.Where(b => b.Function != null && b.Kind != Realmkeeper.Exploration.Domain.BuildingKind.Hall && b.Fit == Realmkeeper.Exploration.Domain.ShopFit.None &&
                                                          InteriorGenerator.HasInterior(b.Function.Value)))
            {
                if (W.Player.IsInPlace) Ok(S.Dispatch(new ExitPlaceCommand()));
                H.Edit(w => w.MinuteOfDay = 9 * 60);
                if (!S.Dispatch(new EnterBuildingCommand(b.Key)).IsSuccess) continue;
                var home = W.FindPlace(W.Player.PlaceId);
                foreach (var f in home.Layout.Features.Where(f => f.Kind == FeatureKind.Container && f.Get("owned") == "yes"))
                {
                    var box = W.FindContainer(PlaceContents.Key(home.PlaceId, f.Id));
                    if (box == null || (box.Items.Count == 0 && box.Coin == 0)) continue;
                    for (var minute = 7 * 60; minute < 21 * 60; minute += 20)
                    {
                        var abs = W.CurrentSimulationDate.ToDayNumber() * 1440L + minute;
                        var people = IndoorSchedule.Everyone(W, home, abs).Where(p => p.Activity != Realmkeeper.Simulation.Npcs.ActivityKind.Sleeping).ToList();
                        var here = people.FirstOrDefault(p => p.Room == f.Room);
                        if (alone ? people.Count == 0 : here == null) continue;
                        H.Edit(w => w.MinuteOfDay = minute);
                        if (!alone && IndoorSchedule.Everyone(W, home, Now).All(p => p.Room != f.Room)) continue;
                        if (home.Layout.Room(f.Room).Restricted) continue; // a private room has its own story (tested below)
                        H.WalkTo(f.Room, fight: false);
                        if (W.Player.PlaceRoom != f.Room) continue;
                        return (home, f, alone ? null : W.FindNpc(here.Npc.NpcId));
                    }
                }
            }

            Assert.Inconclusive("No suitable household in this world.");
            return default;
        }

        [Test]
        public void ATheft_SeenBySomeoneWhoKnowsYou_HasABounty_CostsReputationOnce_AndTheyRememberIt()
        {
            var (home, box, owner) = InsideWithSomeone(alone: false);
            var witness = IndoorSchedule.Everyone(W, home, Now).First(p => p.Room == box.Room).Npc;
            H.Edit(w => w.FindNpc(witness.NpcId).MetPlayerOn = w.CurrentSimulationDate); // they have met
            var opts = S.GetPlaceActions(box.Id);
            Assert.That(opts.Single(o => o.Action == PlaceAction.Open).Enabled, Is.False, "Not the player's.");
            Assert.That(opts.Any(o => o.Action == PlaceAction.Steal && o.Enabled), Is.True);
            var standing = W.Player.Social.SettlementStandings.FirstOrDefault(s => s.SettlementId == home.SettlementId)?.Reputation ?? 0;
            var took = S.Dispatch(new PlaceActionCommand(box.Id, PlaceAction.Steal));
            Ok(took);
            Assert.That(took.Message, Does.Contain("sees you"));
            var crime = W.Crimes.Single();
            Assert.That(crime.Kind, Is.EqualTo(CrimeKind.Theft).Or.EqualTo(CrimeKind.ShopTheft));
            Assert.That(crime.Status, Is.EqualTo(CrimeStatus.Witnessed));
            Assert.That(crime.KnownToBeThePlayer, Is.True);
            Assert.That(crime.Bounty, Is.GreaterThanOrEqualTo(10));
            Assert.That(crime.Taken, Is.Not.Empty.Or.Matches<Dictionary<string, int>>(_ => crime.Coin > 0));
            Assert.That(W.FindContainer(PlaceContents.Key(home.PlaceId, box.Id)).Items, Is.Empty, "Everything moved to the pack: one holder, never copied.");
            var w = W;
            var after = w.Player.Social.SettlementStandings.FirstOrDefault(s => s.SettlementId == home.SettlementId)?.Reputation ?? 0;
            Assert.That(after, Is.LessThan(standing));
            Assert.That(w.FindNpc(witness.NpcId).Knowledge.Any(k => k.SubjectId == crime.CrimeId && k.Topic == KnowledgeTopic.PlayerDeed), Is.True);
            Assert.That(w.FindNpc(witness.NpcId).Memories.Any(m => m.Source == MemorySource.Witnessed && m.Subject.Contains("took")), Is.True);
            Assert.That(w.HistoricalEvents.Count(e => e.EventType == HistoricalEventType.CrimeWitnessed), Is.GreaterThanOrEqualTo(1));
            Assert.That(w.Player.Social.Ledger.Count(l => l.Key == crime.CrimeId + ":settlement"), Is.EqualTo(1), "Keyed: once.");
            Assert.That(S.GetCrimes().Single().Known, Is.True);
            Valid();

            // They will not have the player in their trade, and talking to them settles it.
            Assert.That(CrimeRules.RefusesTrade(W, W.FindNpc(witness.NpcId)), Does.Contain("I know what you did"));
            H.Edit(x => x.Player.Purse = 500);
            Ok(S.Dispatch(new StartConversationCommand(witness.NpcId)));
            var settle = S.GetConversation().Responses.Single(r => r.Id == "settle");
            Ok(S.Dispatch(new ChooseResponseCommand(settle.Id)));
            Assert.That(W.Crimes.Single().Status, Is.EqualTo(CrimeStatus.Settled));
            Assert.That(W.Crimes.Single().Bounty, Is.EqualTo(0));
            Assert.That(CrimeRules.RefusesTrade(W, W.FindNpc(witness.NpcId)), Is.Null, "Settled: they will trade again.");
            Assert.That(W.FindNpc(witness.NpcId).Memories.Any(m => m.Subject.Contains("took")), Is.True, "People do not forget.");
            Valid();
        }

        [Test]
        public void ATheft_NobodySees_IsUndiscovered_ThenFoundOut_WithNoCulprit_AndCostsNothing()
        {
            var (home, box, _) = InsideWithSomeone(alone: true);
            var standing = W.Player.Social.Notoriety;
            Ok(S.Dispatch(new PlaceActionCommand(box.Id, PlaceAction.Steal)));
            var crime = W.Crimes.Single();
            Assert.That(crime.Status, Is.EqualTo(CrimeStatus.Undiscovered));
            Assert.That(crime.Witnesses, Is.Empty);
            Assert.That(crime.Bounty, Is.EqualTo(0));
            Assert.That(W.Player.Social.Notoriety, Is.EqualTo(standing));
            Assert.That(W.Npcs.Any(n => n.Knowledge.Any(k => k.SubjectId == crime.CrimeId)), Is.False, "Nobody knows anything.");
            Ok(S.Dispatch(new ExitPlaceCommand()));
            Hours(24);
            crime = W.Crimes.Single();
            Assert.That(crime.Status, Is.EqualTo(CrimeStatus.Discovered), "Found out the next day.");
            Assert.That(crime.KnownToBeThePlayer, Is.False);
            var owner = W.FindNpc(crime.VictimNpcId);
            if (owner != null) Assert.That(owner.Knowledge.Any(k => k.SubjectId == crime.CrimeId && k.Topic == KnowledgeTopic.RecentEvent && k.Value.Contains("unknown")), Is.True);
            Assert.That(W.Npcs.Any(n => n.Knowledge.Any(k => k.SubjectId == crime.CrimeId && k.Topic == KnowledgeTopic.PlayerDeed)), Is.False, "Nobody can name the player.");
            Assert.That(W.Player.Social.Notoriety, Is.EqualTo(standing));
            Valid();
        }

        [Test]
        public void AStranger_WhoDoesNotKnowYou_GivesOnlyADescription_AndNoBounty()
        {
            var (home, box, _) = InsideWithSomeone(alone: false);
            var witness = IndoorSchedule.Everyone(W, home, Now).First(p => p.Room == box.Room).Npc;
            H.Edit(w => { w.FindNpc(witness.NpcId).MetPlayerOn = default; w.Player.Social.Notoriety = 0; });
            Ok(S.Dispatch(new PlaceActionCommand(box.Id, PlaceAction.Steal)));
            var crime = W.Crimes.Single();
            Assert.That(crime.Status, Is.EqualTo(CrimeStatus.Witnessed));
            Assert.That(crime.KnownToBeThePlayer, Is.False);
            Assert.That(crime.Bounty, Is.EqualTo(0));
            Assert.That(crime.Witnesses.Single().Description, Does.Contain("a stranger"));
            var k = W.FindNpc(witness.NpcId).Knowledge.Single(x => x.SubjectId == crime.CrimeId);
            Assert.That(k.Topic, Is.EqualTo(KnowledgeTopic.RecentEvent));
            Assert.That(k.Value, Does.Contain("a stranger"));
        }

        [Test]
        public void TheNews_TravelsOnlyBetweenPeopleWhoMeet_SoOtherTownsDoNotKnow()
        {
            var (home, box, _) = InsideWithSomeone(alone: false);
            var witness = IndoorSchedule.Everyone(W, home, Now).First(p => p.Room == box.Room).Npc;
            H.Edit(w => w.FindNpc(witness.NpcId).MetPlayerOn = w.CurrentSimulationDate);
            Ok(S.Dispatch(new PlaceActionCommand(box.Id, PlaceAction.Steal)));
            var crime = W.Crimes.Single();
            Ok(S.Dispatch(new ExitPlaceCommand()));
            for (var d = 0; d < 4; d++) Hours(24);
            var w = W;
            var knowers = w.Npcs.Where(n => n.Knowledge.Any(k => k.SubjectId == crime.CrimeId)).ToList();
            Assert.That(knowers.Select(n => n.NpcId), Does.Contain(witness.NpcId));
            Assert.That(knowers.All(n => n.HomeSettlementId == home.SettlementId || n.CurrentLocationId.IsEmpty || Realmkeeper.Simulation.Npcs.NpcMind.SettlementAt(w, n.CurrentLocationId)?.SettlementId == home.SettlementId),
                Is.True, "Only people who met the witness, here, have heard.");
            foreach (var n in knowers.Where(n => n.NpcId != witness.NpcId))
                Assert.That(n.Knowledge.Single(k => k.SubjectId == crime.CrimeId).Status, Is.Not.EqualTo(KnowledgeStatus.Known), "Heard, not seen.");
            TestContext.WriteLine($"After four days {knowers.Count} people know of {crime.CrimeId}.");
        }

        [Test]
        [Timeout(600000)] // a scan with a save and reload per half hour per house: ~8 s in .NET, 140–190 s under the Unity editor's Mono
        public void AHousehold_WarnsOnce_ThenShowsYouOut_AndThatIsTrespass()
        {
            H.Hero();
            var town = Realmkeeper.Simulation.Quests.QuestGenesis.FirstTown(W);
            Ok(S.Dispatch(new EnterSettlementCommand(town.SettlementId)));
            var layout = ExplorationModule.LayoutFor(W, W.FindSettlement(town.SettlementId));
            foreach (var b in layout.Buildings.Where(b => b.Function != null && b.Kind != Realmkeeper.Exploration.Domain.BuildingKind.Hall && b.Fit == Realmkeeper.Exploration.Domain.ShopFit.None &&
                                                          InteriorGenerator.HasInterior(b.Function.Value)))
                for (var minute = 7 * 60; minute < 21 * 60; minute += 30)
                {
                    if (W.Player.IsInPlace) Ok(S.Dispatch(new ExitPlaceCommand()));
                    H.Edit(w => w.MinuteOfDay = minute);
                    if (!S.Dispatch(new EnterBuildingCommand(b.Key)).IsSuccess) break;
                    var home = W.FindPlace(W.Player.PlaceId);
                    var priv = home.Layout.Rooms.FirstOrDefault(r => r.Restricted && home.Layout.Connections.Any(c => c.Joins(r.Id) && c.Joins(W.Player.PlaceRoom)));
                    if (priv == null) break;
                    var people = IndoorSchedule.Everyone(W, home, Now).Where(p => p.Activity != Realmkeeper.Simulation.Npcs.ActivityKind.Sleeping).ToList();
                    if (!people.Any(p => p.Room == priv.Id || PlaceRules.InSight(home, p.Room, priv.Id))) continue;
                    var entrance = W.Player.PlaceRoom;
                    H.WalkTo(priv.Id, fight: false);
                    var r1 = W.Player.PlaceRoom;
                    if (r1 != priv.Id) continue;
                    Assert.That(W.Crimes, Is.Empty, "A warning first, no crime.");
                    H.WalkTo(entrance, fight: false);
                    var back = S.Dispatch(new MoveInPlaceCommand(priv.Id, PlaceRules.PoseAt(home, priv.Floor, priv.Rect.CenterX, priv.Rect.CenterZ, 0f)));
                    Ok(back);
                    if (W.Crimes.Count == 0) continue; // they had stepped away
                    Assert.That(back.Message, Does.Contain("shown the door"));
                    Assert.That(W.Player.IsInPlace, Is.False, "Out in the street.");
                    Assert.That(W.Crimes.Single().Kind, Is.EqualTo(CrimeKind.Trespass));
                    Valid();
                    return;
                }

            Assert.Inconclusive("No household with a private room watched in this world.");
        }

        [Test]
        public void AtNight_AHomeIsLocked_AndBreakingIn_IsACrime()
        {
            H.Hero();
            var town = Realmkeeper.Simulation.Quests.QuestGenesis.FirstTown(W);
            Ok(S.Dispatch(new EnterSettlementCommand(town.SettlementId)));
            var layout = ExplorationModule.LayoutFor(W, W.FindSettlement(town.SettlementId));
            var home = layout.Buildings.First(b => b.Function != null && b.Kind != Realmkeeper.Exploration.Domain.BuildingKind.Hall && b.Fit == Realmkeeper.Exploration.Domain.ShopFit.None &&
                                                   InteriorGenerator.HasInterior(b.Function.Value) && InteriorGenerator.Plan(b.Function.Value) == "home");
            H.Edit(w => w.MinuteOfDay = 23 * 60);
            Refused(new EnterBuildingCommand(home.Key), "locked for the night");
            for (var i = 0; i < 3 && !W.Player.IsInPlace; i++) Ok(S.Dispatch(new EnterBuildingCommand(home.Key, PlaceAction.Force)));
            if (!W.Player.IsInPlace) { Assert.Inconclusive("The door held three times."); return; }
            var crime = W.Crimes.Single();
            Assert.That(crime.Kind, Is.EqualTo(CrimeKind.PropertyDamage));
            Assert.That(crime.Detail, Does.Contain("forced the door"));
            Valid();
        }

        [Test]
        public void AnAssault_IsRecordedBeforeTheFirstBlow_AndAKillingIsMurder()
        {
            var (home, box, _) = InsideWithSomeone(alone: false);
            var victim = IndoorSchedule.Everyone(W, home, Now).First(p => p.Room == box.Room).Npc;
            H.Edit(w => w.FindNpc(victim.NpcId).MetPlayerOn = w.CurrentSimulationDate);
            Ok(S.Dispatch(new AttackPersonCommand(victim.NpcId)));
            var assault = W.Crimes.Single();
            Assert.That(assault.Kind, Is.EqualTo(CrimeKind.Assault));
            Assert.That(assault.Witnesses.Select(x => x.NpcId), Does.Contain(victim.NpcId), "The victim always knows.");
            Assert.That(S.GetEncounter().Status, Is.EqualTo(EncounterStatus.Active));
            EncounterGenerationTests.FightToTheEnd(S);
            if (W.FindNpc(victim.NpcId).IsDeceased)
            {
                var murder = W.Crimes.Single(c => c.Kind == CrimeKind.Murder);
                Assert.That(murder.VictimNpcId, Is.EqualTo(victim.NpcId));
                Assert.That(murder.Witnesses.Any(x => x.NpcId == victim.NpcId), Is.False, "The dead tell nobody.");
            }

            Valid();
        }

        [Test]
        public void AWitness_InARoomInSight_ButBehindItsWall_SeesNothing_AndOneInTheLine_Does()
        {
            // The fort's guards are persistent people. One guard group stands in a room the player's room opens onto;
            // from the player's square, part of that room lies behind the wall beside the doorway.
            H.Hero();
            var fort = Place(PlaceCatalog.Fort);
            foreach (var g in fort.Layout.Groups.Where(g => g.Members.Count > 0 && g.Members.All(m => !m.NpcId.IsEmpty)))
            {
                var gr = fort.Layout.Room(g.Room);
                foreach (var c in fort.Layout.Connections.Where(c => !c.IsVertical && c.Kind != ConnectionKind.Entrance && c.Kind != ConnectionKind.Secret && (c.A == g.Room || c.B == g.Room)))
                {
                    var pr = fort.Layout.Room(c.A == g.Room ? c.B : c.A);
                    if (pr == null || pr.Floor != gr.Floor) continue;
                    H.Edit(w => { if (c.Kind != ConnectionKind.Opening) w.FindPlace(fort.PlaceId).Doors[c.Id] = DoorState.Open; });
                    var place = Place(PlaceCatalog.Fort);
                    Assume.That(PlaceRules.InSight(place, pr.Id, g.Room), Is.True);
                    var grid = new PlaceSightGrid(place, pr.Floor);
                    for (var px = pr.Rect.X; px < pr.Rect.X + pr.Rect.W; px++)
                        for (var pz = pr.Rect.Z; pz < pr.Rect.Z + pr.Rect.D; pz++)
                        {
                            (int, int)? hidden = null, seen = null;
                            for (var qx = gr.Rect.X; qx < gr.Rect.X + gr.Rect.W; qx++)
                                for (var qz = gr.Rect.Z; qz < gr.Rect.Z + gr.Rect.D; qz++)
                                {
                                    if (System.Math.Max(System.Math.Abs(qx - px), System.Math.Abs(qz - pz)) > CrimeRules.SightCells) continue;
                                    if (GridSight.CanSee(grid, new GridPos(qx, qz, pr.Floor), new GridPos(px, pz, pr.Floor))) seen = seen ?? (qx, qz);
                                    else hidden = hidden ?? (qx, qz);
                                }

                            if (hidden == null || seen == null) continue;
                            var guard = g.Members[0].NpcId;
                            bool Sees((int X, int Z) spot)
                            {
                                // One guard moves to the spot (trading places with whoever stood there: two people never share a cell).
                                H.Edit(w =>
                                {
                                    var ms = w.FindPlace(fort.PlaceId).Layout.Group(g.Id).Members;
                                    var other = ms.Skip(1).FirstOrDefault(m => m.X == spot.X && m.Z == spot.Z);
                                    if (other != null) { other.X = ms[0].X; other.Z = ms[0].Z; }
                                    ms[0].X = spot.X;
                                    ms[0].Z = spot.Z;
                                });
                                return CrimeRules.Witnesses(W, Place(PlaceCatalog.Fort), pr, px, pz, true).Any(x => x.Npc.NpcId == guard);
                            }

                            Assert.That(Sees(hidden.Value), Is.False, "Behind the wall beside the doorway: the guard cannot see the act.");
                            Assert.That(Sees(seen.Value), Is.True, "In the line through the doorway: the guard sees it.");
                            return;
                        }
                }
            }

            Assert.Inconclusive("No guard room in this fort has a corner hidden from a neighbouring room.");
        }
    }
}
