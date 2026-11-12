using System.Linq;
using NUnit.Framework;
using Realmkeeper.Application;
using Realmkeeper.Application.Campaigns;
using Realmkeeper.Domain.Campaigns;
using Realmkeeper.Domain.Quests;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Simulation.Campaigns;
using Realmkeeper.Simulation.Quests;
using Realmkeeper.Tests.EditMode.Atlas;
using Realmkeeper.Tests.EditMode.Combat;

namespace Realmkeeper.Tests.EditMode.Campaigns
{
    /// <summary>
    /// Phase 17: the connected world of Phase 16 with a player character, for the campaign, decision, resilience,
    /// companion-story and emergent-story tests. Moving between towns is done the tests' way (a save edit puts the
    /// player at the town's gate on the map, then the ordinary command enters it, so the quest events still happen).
    /// </summary>
    public abstract class Phase17Fixture : Phase16Fixture
    {
        protected const string Waystone = CampaignCatalog.Waystone;

        protected CampaignInstanceData C => CampaignEngine.Find(W, CampaignEngine.InstanceIdFor(Waystone));
        protected CampaignDefinition Def => CampaignCatalog.Find(Waystone);
        protected NpcData Npc(string role) => CampaignEngine.Npc(W, C, role);
        protected SettlementData Town(string role) => CampaignEngine.Settlement(W, C, role);
        protected QuestInstanceData Quest(string key) => C.Quests.TryGetValue(key, out var id) ? W.FindQuest(id) : null;
        protected ArcProgressData Arc(string id) => C.Arc(id);

        protected void Hero(int purse = 500)
        {
            CombatTests.Create(S, "fighter", new[] { 15, 14, 13, 8, 10, 12 });
            Edit(w => { w.MinuteOfDay = 10 * 60; w.Player.Purse = purse; });
            Ok(S.Dispatch(new EnterSettlementCommand(QuestGenesis.FirstTown(W).SettlementId)));
        }

        protected void Begin()
        {
            Ok(S.Dispatch(new StartCampaignCommand(Waystone)));
            Assert.That(C, Is.Not.Null);
        }

        /// <summary>Puts the player outside a settlement (on the map, in its region) and enters it by the ordinary command.</summary>
        protected void GoTo(SettlementData s)
        {
            Edit(w =>
            {
                var p = w.Player;
                if (w.Conversation != null) w.Conversation = null;
                p.LocationKind = CharacterLocationKind.WorldMap;
                p.SettlementId = default;
                p.RegionId = s.RegionId;
                p.LastSettlementId = s.SettlementId;
                w.Journey = null;
                w.MinuteOfDay = 10 * 60;
            });
            Ok(S.Dispatch(new EnterSettlementCommand(s.SettlementId)));
        }

        /// <summary>Makes sure a person can be spoken to now: at home, awake, not leading a caravan.</summary>
        protected void AtHome(NpcData npc)
        {
            Edit(w =>
            {
                w.MinuteOfDay = 10 * 60;
                foreach (var g in w.Groups) if (g.LeaderNpcId == npc.NpcId) g.LeaderNpcId = default;
            });
        }

        protected void Talk(NpcData npc)
        {
            if (W.Conversation != null) Ok(S.Dispatch(new EndConversationCommand()));
            if (!W.Player.IsInSettlement || W.Player.SettlementId != npc.HomeSettlementId) GoTo(W.FindSettlement(npc.HomeSettlementId));
            AtHome(npc);
            Ok(S.Dispatch(new StartConversationCommand(npc.NpcId)));
        }

        /// <summary>Talks to a person about one of the campaign's quests (the hook their general talk offers).</summary>
        protected void About(NpcData npc, string questKey, string role)
        {
            Talk(npc);
            Ok(Say("hook:" + Quest(questKey).InstanceId + ":" + role));
        }

        protected CommandResult Say(string id) => S.Dispatch(new ChooseResponseCommand(id));

        /// <summary>Someone dies, through the simulation's own record of a death (leadership passes, their sheet falls to 0).</summary>
        protected void Kill(params NpcData[] people)
        {
            Edit(w =>
            {
                foreach (var p in people)
                {
                    if (p == null) continue;
                    var n = w.FindNpc(p.NpcId);
                    Realmkeeper.Simulation.Npcs.NpcSystem.RecordDeath(w, n, w.FindSettlement(n.HomeSettlementId), w.CurrentSimulationDate);
                    if (n.Character?.Sheet != null) n.Character.Sheet.CurrentHp = 0;
                }
            });
        }

        protected string[] Options() => Realmkeeper.Application.DialogueEngine.View(W).Responses.Where(r => r.Enabled).Select(r => r.Id).ToArray();

        protected void Days(int n) => Ok(S.Execute(new AdvanceDaysCommand(n)));

        protected void Save(string slot) => Ok(S.Execute(new SaveWorldCommand(slot)));

        protected void Load(string slot) => Ok(S.Execute(new LoadWorldCommand(slot)));

        /// <summary>Plays chapter one: the letter delivered, the tally looked at (proof found: the case is made).</summary>
        protected void ChapterOne(bool lookAtTally = true)
        {
            About(Npc("reeve"), "letter", "giver");
            Ok(Say("accept"));
            if (lookAtTally && Quest("tally") != null)
            {
                About(Npc("carter"), "tally", "giver");
                Ok(Say("accept"));
            }

            About(Npc("counterpart"), "letter", "recipient");
            Ok(Say("give"));
            if (lookAtTally && Quest("tally") != null)
            {
                MakeTheCase();
                About(Npc("carter"), "tally", "giver");
                Ok(Say("report"));
            }
        }

        /// <summary>Sets the evidence flags as the tally's dialogue does on a successful check (the dice are the world's; the test does not depend on them).</summary>
        protected void MakeTheCase()
        {
            About(Npc("tollkeeper"), "tally", "tollkeeper");
            var r = Say("look");
            Ok(r);
            if (C.Flags.ContainsKey("evidence")) return;
            Edit(w =>
            {
                var c = CampaignEngine.Find(w, CampaignEngine.InstanceIdFor(Waystone));
                c.Flags["evidence"] = "tally";
                c.Flags["case_made"] = "tally";
            });
        }
    }
}
