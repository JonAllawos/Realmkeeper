using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using Realmkeeper.Application;
using Realmkeeper.Application.Party;
using Realmkeeper.Application.Places;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Places;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.Simulation.Party;
using Realmkeeper.Simulation.Places;
using Realmkeeper.Tests.EditMode.Living;
using Realmkeeper.Tests.EditMode.Phase2;
using Realmkeeper.Tests.EditMode.Places;

namespace Realmkeeper.Tests.EditMode.Party
{
    /// <summary>
    /// Phase 15 (ADR-0073–0075): companions and the party, played through the commands. Recruiting and refusals,
    /// temporary and permanent terms, dismissal and return, capacity and reserve, follow and wait, formation and leader,
    /// fights beside the player with explained decisions, what fights and deeds do to loyalty, departure, the personal
    /// quest, resting together, items with one owner, conversation, saving, migrating and validating.
    /// </summary>
    public sealed class Phase15PartyTests : Phase14Fixture
    {
        private SettlementData Town => Realmkeeper.Simulation.Quests.QuestGenesis.FirstTown(W);

        /// <summary>
        /// The hero made and in the first town, and <paramref name="n"/> grown residents of it who know and like the player
        /// well enough to come (no post, no open request of their own, steady nerves). Returns their IDs, lowest first.
        /// </summary>
        private List<NpcId> Willing(int n, bool withPost = false)
        {
            H.Hero();
            Ok(S.Dispatch(new EnterSettlementCommand(Town.SettlementId)));
            var givers = new HashSet<string>(W.Quests.Where(q => q.IsOpen && q.State == QuestState.Active).Select(q => q.Bound("giver")).Where(x => x != null));
            var people = W.Npcs.Where(x => !x.IsDeceased && x.HomeSettlementId == Town.SettlementId &&
                                           (!x.BirthDate.IsValidDate || x.AgeOn(W.CurrentSimulationDate) >= 18) && !givers.Contains(x.NpcId.ToString()) &&
                                           W.FindEscort(x.NpcId) == null && PlacePresence.PostedAt(W, x.NpcId) == null).ToList();
            // People without a post first; then (a test's licence) others asked to stand in as ordinary residents.
            var picks = people.Where(x => RecruitRules.HoldsPost(x.Role) == withPost).OrderBy(x => x.NpcId)
                .Concat(withPost ? Enumerable.Empty<NpcData>() : people.Where(x => RecruitRules.HoldsPost(x.Role)).OrderBy(x => x.NpcId))
                .Take(n).Select(x => x.NpcId).ToList();
            if (picks.Count < n) Assert.Inconclusive($"Only {picks.Count} suitable people in this town.");
            H.Edit(w =>
            {
                foreach (var id in picks)
                {
                    var x = w.FindNpc(id);
                    if (!withPost && RecruitRules.HoldsPost(x.Role)) x.Role = NpcRole.Resident;
                    x.PlayerRegard.Familiarity = 40;
                    x.PlayerRegard.Trust = 40;
                    x.PlayerRegard.Affinity = 20;
                    x.Personality[NpcTrait.Courage] = 60;
                }
            });
            return picks;
        }

        private CompanionRecordData Record(NpcId id) => W.FindCompanion(id);
        private NpcData Npc(NpcId id) => W.FindNpc(id);

        private void Recruit(NpcId id, bool temporary = false) => Ok(S.Dispatch(new RecruitCompanionCommand(id, temporary)));

        private static List<ItemStackId> AllStackIds(WorldData w)
        {
            var ids = new List<ItemStackId>();
            ids.AddRange(w.Player.Inventory.Select(s => s.StackId));
            ids.AddRange(w.Player.Equipment.Values.Select(s => s.StackId));
            foreach (var n in w.Npcs.Where(n => n.Character != null && n.Character.OwnsItems))
            {
                ids.AddRange(n.Character.Inventory.Select(s => s.StackId));
                ids.AddRange(n.Character.Equipment.Values.Select(s => s.StackId));
            }

            return ids;
        }

        // 1 ------------------------------------------------------------------------------------------------------

        [Test]
        public void Recruiting_AWillingPerson_MakesThemACompanion_Once_WithASheet_AndTheirGearAsTheirOwnItems()
        {
            var id = Willing(1)[0];
            var gear = Npc(id).Character?.Gear?.All().ToList() ?? new List<string>();
            Recruit(id);

            var npc = Npc(id);
            Assert.That(W.Party.Contains(id), Is.True);
            Assert.That(npc.Character.Sheet, Is.Not.Null, "A full sheet made by the rules engine.");
            Assert.That(npc.Character.OwnsItems, Is.True);
            Assert.That(npc.Character.Gear.All().Any(), Is.False, "Gear by name is cleared once it becomes items: nothing exists twice.");
            var owned = npc.Character.Inventory.Select(s => s.ItemId).Concat(npc.Character.Equipment.Values.Select(s => s.ItemId)).ToList();
            foreach (var g in gear.Where(g => ItemCatalog.Find(g) != null)) Assert.That(owned.Contains(g), Is.True, g + " became their own item.");
            Assert.That(Record(id).Standing, Is.EqualTo(CompanionStanding.InParty));
            Assert.That(Record(id).TimesRecruited, Is.EqualTo(1));
            Assert.That(PlacePresence.IsAway(W, npc), Is.True, "Their daily schedule no longer moves them while they travel with the player.");
            Assert.That(W.HistoricalEvents.Any(e => e.EventType == HistoricalEventType.CompanionJoined), Is.True);

            Refused(new RecruitCompanionCommand(id), "already with you");
            var ids = AllStackIds(W);
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count), "Every item has one owner.");
            Valid();
        }

        // 2 ------------------------------------------------------------------------------------------------------

        [Test]
        public void Refusals_NameTheirCause_AndChangeNothing()
        {
            var ids = Willing(4);
            H.Edit(w => w.FindNpc(ids[0]).PlayerRegard.Familiarity = 5);
            Refused(new RecruitCompanionCommand(ids[0]), "barely knows you");

            H.Edit(w => w.FindNpc(ids[1]).PlayerRegard.Trust = 5);
            Refused(new RecruitCompanionCommand(ids[1]), "does not trust you");

            H.Edit(w => w.FindNpc(ids[2]).Personality[NpcTrait.Courage] = 10);
            Refused(new RecruitCompanionCommand(ids[2]), "no stomach for the road");
            Recruit(ids[2], temporary: true); // but for a few days, yes

            H.Edit(w => w.FindNpc(ids[3]).PlayerRegard.Hostility = 60);
            Refused(new RecruitCompanionCommand(ids[3]), "does not like you");
        }

        [Test]
        public void SomeoneWithAPost_ComesForAFewDays_NotForGood()
        {
            var id = Willing(1, withPost: true)[0];
            Refused(new RecruitCompanionCommand(id), "has a post to keep");
            Recruit(id, temporary: true);
            Assert.That(W.Party.Find(id).Temporary, Is.True);
        }

        // 3 ------------------------------------------------------------------------------------------------------

        [Test]
        public void ATemporaryArrangement_EndsWhenItsDaysAreOver_AndTheyGoBackToTheirLife()
        {
            var id = Willing(1)[0];
            Recruit(id, temporary: true);
            Hours(24 * PartyRules.TemporaryDays - 2);
            Assert.That(W.Party.Contains(id), Is.True, "Not yet.");
            Hours(26);
            Assert.That(W.Party.Contains(id), Is.False);
            Assert.That(Record(id).Standing, Is.EqualTo(CompanionStanding.TermEnded));
            Assert.That(PlacePresence.IsAway(W, Npc(id)), Is.False, "Back on their own schedule.");
            Valid();
        }

        // 4 ------------------------------------------------------------------------------------------------------

        [Test]
        public void Dismissed_TheyKeepWhatTheyCarry_AndRejoining_GrantsNothingTwice()
        {
            var id = Willing(1)[0];
            Recruit(id);
            var items = AllStackIds(W).Count;
            Ok(S.Dispatch(new DismissCompanionCommand(id)));
            Assert.That(W.Party.Contains(id), Is.False);
            Assert.That(Record(id).Standing, Is.EqualTo(CompanionStanding.Dismissed));
            Assert.That(AllStackIds(W).Count, Is.EqualTo(items), "They take their things home.");
            Refused(new DismissCompanionCommand(id), "not in your party");

            Recruit(id);
            Assert.That(Record(id).TimesRecruited, Is.EqualTo(2));
            Assert.That(AllStackIds(W).Count, Is.EqualTo(items), "Their gear became items once, never again.");
            Assert.That(W.Companions.Count(c => c.NpcId == id), Is.EqualTo(1));
            Valid();
        }

        // 5 ------------------------------------------------------------------------------------------------------

        [Test]
        public void Capacity_ThreeTravel_ThreeWaitInReserve_ThenThePartyIsFull()
        {
            var ids = Willing(7);
            for (var i = 0; i < 6; i++) Recruit(ids[i]);
            Assert.That(W.Party.Count(PartyRole.Active), Is.EqualTo(PartyRules.ActiveCapacity));
            Assert.That(W.Party.Count(PartyRole.Reserve), Is.EqualTo(PartyRules.ReserveCapacity));
            Assert.That(PlacePresence.IsAway(W, Npc(ids[0])), Is.True, "Active members travel with the player.");
            Assert.That(PlacePresence.IsAway(W, Npc(ids[4])), Is.False, "Reserve members live their ordinary lives.");
            Refused(new RecruitCompanionCommand(ids[6]), "party is full");
            Refused(new SetPartyRoleCommand(ids[4], PartyRole.Active), "line-up is full");
            Refused(new SetPartyRoleCommand(ids[1], PartyRole.Reserve), "reserve is full");
            Ok(S.Dispatch(new DismissCompanionCommand(ids[0])));
            Ok(S.Dispatch(new SetPartyRoleCommand(ids[4], PartyRole.Active)));
            Assert.That(W.Party.Count(PartyRole.Active), Is.EqualTo(3));
            Assert.That(PlacePresence.IsAway(W, Npc(ids[4])), Is.True, "Called up: now travelling.");
            Valid();
        }

        // 6 ------------------------------------------------------------------------------------------------------

        [Test]
        public void Waiting_HoldsTheirPlace_RegroupOnlyGathersThoseWhereYouAre()
        {
            var ids = Willing(2);
            Recruit(ids[0]);
            Recruit(ids[1]);
            Ok(S.Dispatch(new SetFollowModeCommand(ids[0], FollowMode.Wait)));
            var m = W.Party.Find(ids[0]);
            Assert.That(m.WaitSettlement, Is.EqualTo(Town.SettlementId));
            Assert.That(CompanionLoyalty.Present(W).Select(x => x.NpcId).Contains(ids[0]), Is.True, "Waiting where the player stands: still present.");

            Ok(S.Dispatch(new ExitSettlementCommand()));
            Assert.That(CompanionLoyalty.Present(W).Select(x => x.NpcId).Contains(ids[0]), Is.False, "Left behind in town.");
            Assert.That(CompanionLoyalty.Present(W).Select(x => x.NpcId).Contains(ids[1]), Is.True, "The follower came along.");
            Refused(new SetFollowModeCommand(ids[0], FollowMode.Follow), "Go back for them");
            var said = S.Dispatch(new RegroupPartyCommand());
            Ok(said);
            Assert.That(said.Message, Does.Contain("waiting in " + Town.Name));
            Assert.That(W.Party.Find(ids[0]).Mode, Is.EqualTo(FollowMode.Wait));

            Ok(S.Dispatch(new EnterSettlementCommand(Town.SettlementId)));
            Ok(S.Dispatch(new RegroupPartyCommand()));
            Assert.That(W.Party.Find(ids[0]).Mode, Is.EqualTo(FollowMode.Follow));
            Valid();
        }

        // 7 ------------------------------------------------------------------------------------------------------

        [Test]
        public void Formation_OrderAndLeader_StayConsistent()
        {
            var ids = Willing(3);
            foreach (var id in ids) Recruit(id);
            Ok(S.Dispatch(new MovePartyMemberCommand(ids[2], 0)));
            Assert.That(W.Party.Members.OrderBy(m => m.Order).Select(m => m.NpcId).ToArray(), Is.EqualTo(new[] { ids[2], ids[0], ids[1] }));
            Ok(S.Dispatch(new SetPartyLeaderCommand(ids[0])));
            Assert.That(W.Party.Leader, Is.EqualTo(ids[0]));
            Ok(S.Dispatch(new DismissCompanionCommand(ids[0])));
            Assert.That(W.Party.Leader.IsEmpty, Is.True, "A leader who leaves hands the lead back to the player.");
            Assert.That(W.Party.Members.Select(m => m.Order).OrderBy(o => o).ToArray(), Is.EqualTo(new[] { 0, 1 }), "No gaps in the order.");
            Refused(new SetPartyLeaderCommand(ids[0]), "Only a party member");
            Valid();
        }

        // 8 ------------------------------------------------------------------------------------------------------

        [Test]
        public void Companions_FightBesideThePlayer_AsPeopleWithTheirOwnSheets()
        {
            var ids = Willing(2);
            Recruit(ids[0]);
            Recruit(ids[1]);
            Ok(S.Dispatch(new StartPracticeEncounterCommand(lethal: false)));
            var e = W.ActiveEncounter;
            var player = e.Combatants.Single(c => c.Kind == CombatantKind.Player);
            foreach (var id in ids)
            {
                var c = e.Combatants.Single(x => x.Kind == CombatantKind.Npc && x.SourceId == id.ToString());
                Assert.That(c.Side, Is.EqualTo(player.Side));
                Assert.That(c.Control, Is.EqualTo(CombatControl.Ai));
            }

            var squares = e.Combatants.Select(c => c.Position).ToList();
            Assert.That(squares.Distinct().Count(), Is.EqualTo(squares.Count), "Nobody shares a square.");
            Valid();
        }

        // 9 ------------------------------------------------------------------------------------------------------

        [Test]
        public void CompanionDecisions_AreExplained_AndTheSameStateGivesTheSameChoice()
        {
            var id = Willing(1)[0];
            Recruit(id);
            Ok(S.Dispatch(new StartPracticeEncounterCommand(lethal: false)));
            EncounterGenerationTests.FightToTheEnd(S);
            var e = W.Encounters.Last();
            var name = Npc(id).Name;
            var reasons = e.Log.Where(l => l.Kind == CombatLogKind.Reason).ToList();
            Assert.That(reasons.Any(l => l.Text.StartsWith(name + ": ")), Is.True, "The companion's choices carry reasons: " + string.Join(" | ", reasons.Select(r => r.Text)));

        }

        [Test]
        public void ADyingFriend_IsTendedFirst()
        {
            var id = Willing(1)[0];
            Recruit(id);
            Ok(S.Dispatch(new StartPracticeEncounterCommand(lethal: false)));
            var w = S.CurrentWorldSnapshotForTests();
            var e = w.ActiveEncounter;
            var me = e.Combatants.Single(c => c.SourceId == id.ToString());
            var player = e.Combatants.Single(c => c.Kind == CombatantKind.Player);
            player.State = VitalState.Dying;
            w.Player.Rpg.CurrentHp = 0;
            var engine = new CombatEngine(w, e);
            var first = CombatAi.ForCompanion(engine, me, out var why1);
            var second = CombatAi.ForCompanion(engine, me, out var why2);
            Assert.That(why1, Does.Contain("dying"), why1);
            Assert.That(first.GetType(), Is.EqualTo(second.GetType()));
            Assert.That(why2, Is.EqualTo(why1), "Deterministic.");
            Assert.That(first is StabilizeAction || first is MoveAction || first is CastSpellAction, Is.True, first.Describe);
        }

        [Test]
        public void AHealer_HealsTheHurt_AndTheLogSaysSo()
        {
            var id = Willing(1)[0];
            H.Edit(w => w.FindNpc(id).Character = new NpcCharacterData
            {
                ArchetypeId = "priest", Occupation = "shrine priest", Purse = 25,
                Gear = new NpcGearData { Body = "chain_shirt", Shield = "shield_round", MainHand = "mace_flanged", Carried = new List<string> { "holy_symbol" } },
            });
            Recruit(id);
            Assert.That(Npc(id).Character.Sheet.ClassId, Is.EqualTo("cleric"));
            Ok(S.Dispatch(new StartPracticeEncounterCommand(lethal: false)));
            H.Edit(w => w.Player.Rpg.CurrentHp = 2);
            for (var i = 0; i < 40 && W.ActiveEncounter != null; i++)
            {
                var snap = S.GetEncounter();
                if (snap.IsPlayersTurn) { Ok(S.Dispatch(new CombatActionCommand(snap.Id, snap.Player.Id, new EndTurnAction()))); continue; }
                S.Dispatch(new RunAiTurnCommand(snap.Id));
                if (W.Encounters.Last().Log.Any(l => l.Kind == CombatLogKind.Spell && l.Text.StartsWith(Npc(id).Name))) break;
            }

            var log = W.Encounters.Last().Log;
            var cast = log.FindIndex(l => l.Kind == CombatLogKind.Spell && l.Text.StartsWith(Npc(id).Name));
            Assert.That(cast, Is.GreaterThanOrEqualTo(0), "The priest cast: " + string.Join(" | ", log.Select(l => l.Text)));
            var reason = log.Take(cast).LastOrDefault(l => l.Kind == CombatLogKind.Reason && l.Text.StartsWith(Npc(id).Name));
            Assert.That(reason?.Text, Does.Contain("heals"), "The reason given before the spell: " + string.Join(" | ", log.Skip(System.Math.Max(0, cast - 6)).Take(8).Select(l => l.Kind + ":" + l.Text)));
        }

        // 10 -----------------------------------------------------------------------------------------------------

        [Test]
        public void WhatHappensInAFight_StaysWithThem()
        {
            var id = Willing(1)[0];
            Recruit(id);
            Ok(S.Dispatch(new StartPracticeEncounterCommand(lethal: false)));
            EncounterGenerationTests.FightToTheEnd(S);
            var e = W.Encounters.Last();
            var c = e.Combatants.Single(x => x.SourceId == id.ToString());
            var sheet = Npc(id).Character.Sheet;
            Assert.That(sheet.CurrentHp, Is.LessThanOrEqualTo(new Realmkeeper.Domain.Rpg.CharacterRules(sheet, Realmkeeper.Domain.Rpg.CharacterRules.LoadoutOf(Npc(id).Character)).MaxHp));
            var damage = e.Log.Where(l => l.Kind == CombatLogKind.Damage && l.ActorId == c.CombatantId && l.TargetId.IsEmpty).Sum(l => l.Value);
            if (damage > 0) Assert.That(sheet.CurrentHp, Is.LessThan(new Realmkeeper.Domain.Rpg.CharacterRules(sheet, Realmkeeper.Domain.Rpg.CharacterRules.LoadoutOf(Npc(id).Character)).MaxHp), "Wounds persist on their sheet after the fight.");
            Valid();
        }

        // 11 -----------------------------------------------------------------------------------------------------

        [Test]
        public void WithdrawingAndLeavingThemDying_CostsLoyalty_Once()
        {
            var id = Willing(1)[0];
            Recruit(id);
            Ok(S.Dispatch(new StartPracticeEncounterCommand(lethal: false)));
            H.Edit(w =>
            {
                var c = w.ActiveEncounter.Combatants.Single(x => x.SourceId == id.ToString());
                c.State = VitalState.Dying;
                w.FindNpc(id).Character.Sheet.CurrentHp = 0;
            });
            var before = Record(id).Loyalty;
            Ok(S.Dispatch(new EndEncounterCommand(W.ActiveEncounter.EncounterId, withdraw: true)));
            Assert.That(Record(id).Loyalty, Is.EqualTo(before - 20));
            var approval = Record(id).Approvals.Single(a => a.Key.StartsWith("abandoned:"));
            Assert.That(approval.Reason, Does.Contain("left them dying"));
            Assert.That(CompanionLoyalty.Apply(W, id, approval.Key, -20, "again", null), Is.False, "The same cause never counts twice.");
        }

        // 12 -----------------------------------------------------------------------------------------------------

        [Test]
        public void ACrimeTheyWitness_MovesLoyalty_ByWhoTheyAre_Once()
        {
            var lawful = new NpcData { Name = "L" };
            lawful.Personality[NpcTrait.Discipline] = 80;
            var greedy = new NpcData { Name = "G" };
            greedy.Personality[NpcTrait.Greed] = 90;
            greedy.Personality[NpcTrait.Discipline] = 20;
            var kind = new NpcData { Name = "K" };
            kind.Personality[NpcTrait.Compassion] = 80;

            Assert.That(CompanionLoyalty.CrimeReaction(lawful, CrimeKind.Theft, out var why), Is.EqualTo(-9));
            Assert.That(why, Does.Contain("respects the law"));
            Assert.That(CompanionLoyalty.CrimeReaction(greedy, CrimeKind.Theft, out why), Is.EqualTo(3));
            Assert.That(why, Does.Contain("profit"));
            Assert.That(CompanionLoyalty.CrimeReaction(kind, CrimeKind.Murder, out why), Is.EqualTo(-30));
            Assert.That(CompanionLoyalty.CrimeReaction(new NpcData(), CrimeKind.Trespass, out _), Is.EqualTo(-2));

            var id = Willing(1)[0];
            Recruit(id);
            var w = S.CurrentWorldSnapshotForTests();
            var crime = new CrimeRecordData { CrimeId = "crime:99", Kind = CrimeKind.Theft };
            var before = w.FindCompanion(id).Loyalty;
            CompanionLoyalty.OnCrime(w, crime, "steal a purse", null);
            CompanionLoyalty.OnCrime(w, crime, "steal a purse", null);
            var record = w.FindCompanion(id);
            Assert.That(record.Approvals.Count(a => a.Key == "crime:99"), Is.EqualTo(1));
            Assert.That(record.Loyalty, Is.EqualTo(before + record.Approvals.Single().Delta));
            Assert.That(record.Approvals.Single().Reason, Does.Contain("saw you steal a purse"));
        }

        // 13 -----------------------------------------------------------------------------------------------------

        [Test]
        public void BelowTheirLimit_TheyLeave_AndComeBackOnlyWhenItIsMended()
        {
            var id = Willing(1)[0];
            Recruit(id);
            var w = S.CurrentWorldSnapshotForTests();
            CompanionLoyalty.Apply(w, id, "test:betrayal", -(w.FindCompanion(id).Loyalty - PartyRules.LeaveBelow + 1), "you broke your word", null);
            Assert.That(w.Party.Contains(id), Is.False);
            Assert.That(w.FindCompanion(id).Standing, Is.EqualTo(CompanionStanding.Departed));
            Assert.That(RecruitRules.Problem(w, w.FindNpc(id), false), Does.Contain("will not come back yet"));
            CompanionLoyalty.Apply(w, id, "test:amends", PartyRules.ReturnAt, "you made amends", null);
            Assert.That(RecruitRules.Problem(w, w.FindNpc(id), false), Is.Null);
        }

        // 14 -----------------------------------------------------------------------------------------------------

        [Test]
        public void ATrustingCompanion_AsksTheirOwnQuest_AndSeeingItThrough_EarnsLoyalty()
        {
            var id = Willing(1)[0];
            if (!W.Places.Any(p => p.SettlementId == Npc(id).HomeSettlementId)) Assert.Inconclusive("No place near their home.");
            Recruit(id);
            H.Edit(w => w.FindCompanion(id).Loyalty = 65);
            Hours(40); // two midnights: a full day with the player has passed at the second
            var record = Record(id);
            Assert.That(record.PersonalQuestId, Is.Not.Null, "Offered after a day with the player.");
            var q = W.FindQuest(record.PersonalQuestId);
            Assert.That(q.DefinitionId, Is.EqualTo(QuestCatalog.CompanionRoad));
            Assert.That(q.Bound("giver"), Is.EqualTo(id.ToString()));

            var w2 = S.CurrentWorldSnapshotForTests();
            var before = w2.FindCompanion(id).Loyalty;
            foreach (var to in new[] { QuestState.Discovered, QuestState.Offered, QuestState.Accepted, QuestState.Active, QuestState.Completed })
                Realmkeeper.Simulation.Quests.QuestLifecycle.Move(w2, w2.FindQuest(q.InstanceId), to, "test", null);
            Assert.That(w2.FindQuest(q.InstanceId).State, Is.EqualTo(QuestState.Completed));
            Assert.That(w2.FindCompanion(id).Loyalty, Is.EqualTo(System.Math.Min(100, before + 25)));
            Assert.That(w2.FindCompanion(id).Approvals.Any(a => a.Key == q.InstanceId + ":done"), Is.True);
        }

        // 15 -----------------------------------------------------------------------------------------------------

        [Test]
        public void RestingTogether_RestoresEachMember_OnceForEachRest()
        {
            var id = Willing(1)[0];
            Recruit(id);
            H.Edit(w => w.FindNpc(id).Character.Sheet.CurrentHp = 1);
            var r = S.LongRest();
            Assert.That(r.IsSuccess, Is.True, r.Message);
            var sheet = Npc(id).Character.Sheet;
            var max = new Realmkeeper.Domain.Rpg.CharacterRules(sheet, Realmkeeper.Domain.Rpg.CharacterRules.LoadoutOf(Npc(id).Character)).MaxHp;
            Assert.That(sheet.CurrentHp, Is.EqualTo(max));
            Assert.That(r.Lines.Any(l => l.StartsWith(Npc(id).Name + ":")), Is.True, string.Join(" | ", r.Lines));
            Assert.That(sheet.Spellcasting.RestLog.Count(x => x.Kind == Realmkeeper.Domain.Rpg.RestKind.Long), Is.EqualTo(1));

            var again = S.LongRest();
            Assert.That(again.IsSuccess, Is.False, "One long rest per day, for the player and each companion.");
            Assert.That(PartyRest.HitDiceNeeded(sheet), Is.EqualTo(0), "Nobody hurt: no dice spent on a short rest.");
        }

        // 16 -----------------------------------------------------------------------------------------------------

        [Test]
        public void Items_MoveBetweenOwners_WholeAndOnce_AndAreWornByTheSameRules()
        {
            var id = Willing(1)[0];
            Recruit(id);
            H.Edit(w => w.Player.Inventory.Add(new ItemStackData { StackId = new ItemStackId(System.Guid.NewGuid()), ItemId = "greataxe", Quantity = 1 }));
            var axe = W.Player.Inventory.Single(s => s.ItemId == "greataxe");
            Ok(S.Dispatch(new TransferItemCommand(default, id, axe.StackId)));
            Assert.That(W.Player.Inventory.Any(s => s.StackId == axe.StackId), Is.False);
            Assert.That(Npc(id).Character.Inventory.Any(s => s.StackId == axe.StackId), Is.True, "The same instance, now theirs.");

            var hadOffHand = Npc(id).Character.Equipped(EquipmentSlot.OffHand) != null;
            var equipped = S.Dispatch(new CompanionEquipCommand(id, axe.StackId));
            Ok(equipped);
            Assert.That(Npc(id).Character.Equipped(EquipmentSlot.MainHand).StackId, Is.EqualTo(axe.StackId));
            Assert.That(Npc(id).Character.Equipped(EquipmentSlot.OffHand), Is.Null, "A two-handed weapon frees the off hand.");
            if (hadOffHand) Assert.That(equipped.Message, Does.Contain("Put away"));

            Refused(new TransferItemCommand(id, default, axe.StackId), "equipped items must be taken off first");
            Ok(S.Dispatch(new CompanionUnequipCommand(id, EquipmentSlot.MainHand)));
            Ok(S.Dispatch(new TransferItemCommand(id, default, axe.StackId)));
            Assert.That(W.Player.Inventory.Any(s => s.StackId == axe.StackId), Is.True);

            var ids = AllStackIds(W);
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Count));
            Valid();
        }

        // 17 -----------------------------------------------------------------------------------------------------

        [Test]
        public void SaveAndLoad_KeepTheWholeParty_Exactly()
        {
            var ids = Willing(2);
            Recruit(ids[0]);
            Recruit(ids[1], temporary: true);
            Ok(S.Dispatch(new SetFollowModeCommand(ids[1], FollowMode.Wait)));
            H.Edit(w => CompanionLoyalty.Apply(w, ids[0], "test:kept", 7, "kept a promise", null));
            var before = WorldJson();
            H.Edit(_ => { });
            Assert.That(WorldJson(), Is.EqualTo(before), "Save and load are exact for the party, records and companion items.");
            Assert.That(W.SchemaVersion, Is.EqualTo(WorldData.CurrentSchemaVersion));
        }

        // 18 -----------------------------------------------------------------------------------------------------

        [Test]
        public void AVersion16Save_Loads_WithAnEmptyParty_AndThePartySystemOn()
        {
            Willing(0);
            var serializer = new JsonWorldSerializer();
            var doc = JObject.Parse(serializer.Serialize(W));
            Phase2Worlds.StripToV16(doc);
            Assert.That(doc["world"]["party"], Is.Null);
            var result = serializer.Deserialize(doc.ToString());
            Assert.That(result.IsSuccess, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.World.Party.Members, Is.Empty);
            Assert.That(result.World.Companions, Is.Empty);
            Assert.That(result.World.WorldSettings.EnabledSimulationSystems.Contains(PartySystem.Id), Is.True);
            Assert.That(WorldValidator.Validate(result.World).IsValid, Is.True);
        }

        // 19 -----------------------------------------------------------------------------------------------------

        [Test]
        public void TheValidator_CatchesBrokenParties()
        {
            var ids = Willing(1);
            Recruit(ids[0]);
            var w = S.CurrentWorldSnapshotForTests();
            w.Party.Members.Add(w.Party.Members[0].Clone());
            Assert.That(WorldValidator.Validate(w).Errors.Any(e => e.Contains("twice")), Is.True);

            w = S.CurrentWorldSnapshotForTests();
            var npc = w.FindNpc(ids[0]);
            w.Player.Inventory.Add(npc.Character.Equipment.Values.Concat(npc.Character.Inventory).First().Clone());
            Assert.That(WorldValidator.Validate(w).Errors.Any(e => e.Contains("more than one")), Is.True, "One owner per item.");

            w = S.CurrentWorldSnapshotForTests();
            w.FindCompanion(ids[0]).Standing = CompanionStanding.Dismissed;
            Assert.That(WorldValidator.Validate(w).Errors.Any(e => e.Contains("companion record")), Is.True);
        }

        // 20 -----------------------------------------------------------------------------------------------------

        [Test]
        public void InConversation_AskingThemToJoin_AndPartingWays()
        {
            var ids = Willing(2);
            H.Edit(w => w.FindNpc(ids[1]).PlayerRegard.Trust = 0);
            Ok(S.Dispatch(new StartConversationCommand(ids[1])));
            Ok(S.Dispatch(new ChooseResponseCommand("recruit")));
            Assert.That(W.Party.Contains(ids[1]), Is.False);
            Assert.That(W.Conversation.Transcript.Any(l => l.Text.Contains("does not trust you")), Is.True, "A refusal is said, with its reason.");

            if (W.Conversation != null) S.Dispatch(new ChooseResponseCommand("bye"));
            Ok(S.Dispatch(new StartConversationCommand(ids[0])));
            Ok(S.Dispatch(new ChooseResponseCommand("recruit")));
            Assert.That(W.Party.Contains(ids[0]), Is.True);
            Assert.That(W.Conversation.Transcript.Any(l => l.Text.Contains("joins you")), Is.True);
            Ok(S.Dispatch(new ChooseResponseCommand("dismiss")));
            Assert.That(W.Party.Contains(ids[0]), Is.False);
            Assert.That(Record(ids[0]).Standing, Is.EqualTo(CompanionStanding.Dismissed));
            Valid();
        }
    }
}
