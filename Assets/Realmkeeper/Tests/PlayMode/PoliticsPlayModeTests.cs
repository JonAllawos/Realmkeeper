using System.Collections;
using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using Realmkeeper.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Realmkeeper.Tests.PlayMode
{
    /// <summary>Phase 3 on screen: faction list and inspector, the settlement's politics, the optional map overlay, and HUD interventions.</summary>
    public sealed class PoliticsPlayModeTests : RealmkeeperPlayModeFixture
    {
        private WorldSession Session => Bootstrap.Session;

        private SettlementSummary Of(SettlementType type) => Session.GetSettlementsInView().Single(s => s.SettlementType == type);

        // Phase 16: every region has its own kingdom; this is the one governing the vale on the map.
        private FactionSummary Kingdom => Session.GetFactions().Single(f => f.Type == FactionType.Kingdom && Session.GetSettlementsInView().Any(s => s.FactionId == f.FactionId));

        [UnityTest]
        public IEnumerator FactionList_ListsEveryFaction_AndOpensItsInspector()
        {
            yield return null;
            var factions = Session.GetFactions();
            Assert.That(factions.Count, Is.EqualTo(5 * Session.GetAtlas().Regions.Count), "Five factions to each region (Phase 16).");
            Assert.That(Bootstrap.Hud.FactionList.Query<Button>(className: "rk-faction-item").ToList().Count, Is.EqualTo(factions.Count));
            Assert.That(Bootstrap.Hud.FactionInspectorVisible, Is.False);

            var kingdom = Kingdom;
            Bootstrap.Selection.Select(Of(SettlementType.Town).SettlementId);
            yield return null;
            Assert.That(Bootstrap.Hud.InspectorVisible, Is.True);

            Click(Bootstrap.Hud.FactionList.Q<Button>("faction-" + kingdom.FactionId));
            yield return null;

            Assert.That(Bootstrap.Hud.FactionInspectorVisible, Is.True);
            Assert.That(Bootstrap.Hud.InspectorVisible, Is.False, "One inspector at a time.");
            Assert.That(Bootstrap.Hud.SelectedFactionId, Is.EqualTo(kingdom.FactionId));
            Assert.That(Bootstrap.Hud.FactionInspectorName.text, Is.EqualTo(kingdom.Name));
            var body = Bootstrap.Hud.FactionInspectorBody;
            var text = string.Join("\n", body.Query<Label>().ToList().Select(l => l.text));
            StringAssert.Contains(kingdom.LeaderName, text);
            StringAssert.Contains(kingdom.PrimaryGoal.Description, text);
            foreach (var p in kingdom.Presence) StringAssert.Contains(p.SettlementName, text);
            foreach (var r in kingdom.Relations) StringAssert.Contains(r.OtherName(kingdom.FactionId), text);
            Assert.That(body.Q<Label>("finsp-influence-why").text, Is.EqualTo(RealmkeeperHud.InfluenceWhy(kingdom.InfluenceContributions)));
            StringAssert.Contains("Holdings", body.Q<Label>("finsp-influence-why").text, "A kingdom rests on what it holds (among more factions since Phase 16, authority can weigh more).");

            // Selecting a settlement switches back.
            Bootstrap.Selection.Select(Of(SettlementType.Village).SettlementId);
            yield return null;
            Assert.That(Bootstrap.Hud.FactionInspectorVisible, Is.False);
            Assert.That(Bootstrap.Hud.InspectorVisible, Is.True);
        }

        [UnityTest]
        public IEnumerator SettlementInspector_ShowsGovernance_Presence_AndKeepsTheEconomy()
        {
            var village = Of(SettlementType.Village);
            Bootstrap.Selection.Select(village.SettlementId);
            yield return null;

            var politics = Session.GetSettlementPolitics(village.SettlementId);
            var lines = Bootstrap.Hud.SettlementPolitics.Query<Label>().ToList().Select(l => l.text).ToList();
            Assert.That(lines.Take(4), Is.EqualTo(new[] { "Governed by", politics.GoverningFactionName, "Administered by", politics.AdministratorFactionName }));
            foreach (var p in politics.Presence)
            {
                var row = Bootstrap.Hud.SettlementPolitics.Q<VisualElement>("faction-link-" + p.FactionId);
                Assert.That(row, Is.Not.Null, p.FactionName);
                Assert.That(row.Q<Label>(className: "rk-faction-row-name").text, Is.EqualTo(p.FactionName));
                StringAssert.EndsWith($"{p.SharePerMille / 10}%", row.Q<Label>(className: "rk-value").text);
            }

            Assert.That(lines, Has.Member("Affiliated"));
            Assert.That(Bootstrap.Hud.SettlementPolitics.Query<Button>().ToList(), Is.Empty, "No action buttons until Manage is pressed.");
            Bootstrap.Hud.SetManagingPolitics(true);
            yield return null;
            Assert.That(Bootstrap.Hud.SettlementPolitics.Query<Button>().ToList().Count, Is.GreaterThan(0));
            Bootstrap.Hud.SetManagingPolitics(false);
            Assert.That(Bootstrap.Hud.EconomyTable.childCount, Is.EqualTo(5), "The Phase 2 economy table is still there.");
            Assert.That(Bootstrap.Hud.InspectorFood.text, Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator PoliticalOverlay_IsOptional_ShowsGovernors_AndFollowsChanges()
        {
            yield return null;
            Assert.That(Bootstrap.PoliticalOverlay.IsOn, Is.False, "Off by default.");
            Assert.That(Bootstrap.PoliticalOverlay.MarkerCount, Is.EqualTo(0));
            var economicMarkers = Bootstrap.Indicators.MarkerCount;

            Click(Bootstrap.Hud.PoliticsToggle);
            yield return null;

            Assert.That(Bootstrap.PoliticalOverlay.IsOn, Is.True);
            foreach (var s in Session.GetSettlementsInView())
            {
                Assert.That(Bootstrap.PoliticalOverlay.TryGetShown(s.SettlementId, out var shown), Is.True, s.Name);
                Assert.That(shown.Governor, Is.EqualTo(Session.GetSettlementPolitics(s.SettlementId).GoverningFactionId), s.Name);
            }

            Assert.That(Bootstrap.PoliticalOverlay.MarkerCount, Is.EqualTo(3 + Session.GetClaims(default, true).Count));
            Assert.That(Bootstrap.Indicators.MarkerCount, Is.EqualTo(economicMarkers), "Economic markers are untouched.");
            Assert.That(Bootstrap.PoliticalOverlay.GetComponentsInChildren<Collider>(), Is.Empty, "The overlay never blocks selection.");

            // A governance change and a contested claim show up at once.
            var hamlet = Of(SettlementType.Hamlet);
            var oldGovernor = Session.GetSettlementPolitics(hamlet.SettlementId).GoverningFactionId;
            Assert.That(Session.Dispatch(new FileTerritorialClaimCommand(Kingdom.FactionId, hamlet.SettlementId)).IsSuccess, Is.True);
            var claim = Session.GetClaims(hamlet.SettlementId, true).Single();
            Assert.That(Session.Dispatch(new ResolveTerritorialClaimCommand(claim.ClaimId, ClaimStatus.Contested)).IsSuccess, Is.True);
            yield return null;
            Bootstrap.PoliticalOverlay.TryGetShown(hamlet.SettlementId, out var contested);
            Assert.That(contested.Governor, Is.EqualTo(oldGovernor), "A claim, even contested, does not change the banner.");
            Assert.That(contested.Claims, Is.EqualTo(1));
            Assert.That(contested.Contested, Is.EqualTo(1));

            Assert.That(Session.Dispatch(new SetSettlementGovernanceCommand(hamlet.SettlementId, Kingdom.FactionId)).IsSuccess, Is.True);
            yield return null;
            Bootstrap.PoliticalOverlay.TryGetShown(hamlet.SettlementId, out var changed);
            Assert.That(changed.Governor, Is.EqualTo(Kingdom.FactionId));
            Assert.That(changed.Claims, Is.EqualTo(0), "The claim was fulfilled.");

            // Selection and the camera still work with the overlay on.
            Bootstrap.Selection.Select(hamlet.SettlementId);
            yield return null;
            Assert.That(Bootstrap.Selection.SelectedId, Is.EqualTo(hamlet.SettlementId));
            Assert.That(Bootstrap.Hud.InspectorName.text, Is.EqualTo(hamlet.Name));

            Click(Bootstrap.Hud.PoliticsToggle);
            yield return null;
            Assert.That(Bootstrap.PoliticalOverlay.MarkerCount, Is.EqualTo(0), "Switched off again.");
        }

        [UnityTest]
        public IEnumerator HudButtons_SendPoliticalCommands_ThroughTheRegistry()
        {
            var guild = Session.GetFactions().First(f => f.Type == FactionType.Guild && Session.GetSettlementsInView().Any(s => f.Presence.Any(p => p.SettlementName == s.Name))); // Phase 16: the vale's own guild
            var kingdom = Kingdom;
            Bootstrap.Hud.SelectFaction(guild.FactionId);
            yield return null;
            var before = Session.GetRelations(guild.FactionId).Single(r => r.Other(guild.FactionId) == kingdom.FactionId).Opinion;

            Click(Bootstrap.Hud.FactionInspectorBody.Q<Button>("relation-improve-" + kingdom.FactionId));
            yield return null;

            var after = Session.GetRelations(guild.FactionId).Single(r => r.Other(guild.FactionId) == kingdom.FactionId);
            Assert.That(after.Opinion, Is.EqualTo(Mathf.Min(100, before + 10)));
            Assert.That(after.LastCause, Is.EqualTo(RelationCause.ManualIntervention));
            Assert.That(Session.GetRecentEvents(1)[0].IsManual, Is.True);
            StringAssert.Contains(kingdom.Name, Bootstrap.Hud.MessageLabel.text);
            Assert.That(Bootstrap.Hud.FactionInspectorVisible, Is.True, "The inspector refreshed in place.");

            // Abandon one of the guild's goals from the inspector.
            var goal = guild.Goals.First(g => g.Status == GoalStatus.Active);
            Click(Bootstrap.Hud.FactionInspectorBody.Q<Button>("goal-abandon-" + goal.GoalId));
            yield return null;
            Assert.That(Session.FindFaction(guild.FactionId).Goals.Single(g => g.GoalId == goal.GoalId).CloseReason, Is.EqualTo(GoalCloseReason.AbandonedByPlayer));

            // From the settlement inspector: invite the guild into the hamlet, then remove it again.
            var hamlet = Of(SettlementType.Hamlet);
            Bootstrap.Selection.Select(hamlet.SettlementId);
            Bootstrap.Hud.SetManagingPolitics(true);
            yield return null;
            Click(Bootstrap.Hud.SettlementPolitics.Q<Button>("presence-add-" + guild.FactionId));
            yield return null;
            Assert.That(Session.GetSettlementPolitics(hamlet.SettlementId).Presence.Any(p => p.FactionId == guild.FactionId), Is.True);
            Click(Bootstrap.Hud.SettlementPolitics.Q<Button>("presence-remove-" + guild.FactionId));
            yield return null;
            Assert.That(Session.GetSettlementPolitics(hamlet.SettlementId).Presence.Any(p => p.FactionId == guild.FactionId), Is.False);

            // A refused command changes nothing and says why.
            var events = Session.GetOverview().EventCount;
            Bootstrap.Hud.SelectFaction(guild.FactionId);
            Assert.That(Session.Dispatch(new FileTerritorialClaimCommand(guild.FactionId, hamlet.SettlementId)).Status, Is.EqualTo(CommandStatus.ValidationFailed));
            Assert.That(Session.GetOverview().EventCount, Is.EqualTo(events));
        }

        [UnityTest]
        public IEnumerator PoliticsAdvanceWithTheSimulation_AndTheHudFollows()
        {
            Bootstrap.Hud.SelectFaction(Kingdom.FactionId);
            Click(Bootstrap.Hud.PoliticsToggle);
            yield return null;
            var factions = Session.GetFactions().Count;

            Session.Execute(new AdvanceDaysCommand(70));
            yield return null;

            var kingdom = Kingdom;
            Assert.That(Bootstrap.Hud.FactionInspectorBody.Q<Label>("finsp-influence-why").text, Is.EqualTo(RealmkeeperHud.InfluenceWhy(kingdom.InfluenceContributions)));
            Assert.That(Session.GetClaims(default, true).Count, Is.GreaterThan(0), "The crown has filed its claim on the hamlet by now.");
            var hamlet = Of(SettlementType.Hamlet);
            Bootstrap.PoliticalOverlay.TryGetShown(hamlet.SettlementId, out var shown);
            Assert.That(shown.Claims, Is.EqualTo(Session.GetClaims(hamlet.SettlementId, true).Count));
            Assert.That(shown.Governor, Is.EqualTo(Session.GetSettlementPolitics(hamlet.SettlementId).GoverningFactionId));
            Assert.That(Session.GetFactions().Count, Is.EqualTo(factions), "No faction appeared or vanished.");
        }
    }
}
