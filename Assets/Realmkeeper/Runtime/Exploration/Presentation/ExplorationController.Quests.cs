using System.Collections.Generic;
using System.Text;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.Quests;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 12 (ADR-0055, ADR-0058): conversations and quests in exploration — the dialogue window opened from a
    /// resident ("Talk…"), number keys for responses, the journal's actions, a line on the HUD for tracked quests, and a
    /// notice when a quest changes. Every change goes through a command; this only shows and asks.
    /// </summary>
    public sealed partial class ExplorationController
    {
        public DialogueWindow Dialogue { get; private set; }

        private Label _tracked;
        private string _questSignature = string.Empty;

        /// <summary>The last notice about quests (tests and captures read it).</summary>
        public string LastQuestNotice { get; private set; }

        private void SetupQuests()
        {
            if (Hud?.Root == null) return;
            Dialogue = new DialogueWindow(Hud.Root);
            Dialogue.ChooseRequested += id => ChooseResponse(id);
            Dialogue.EndRequested += () => EndDialogue();
            Dialogue.TradeRequested += shop =>
            {
                _session?.Dispatch(new EndConversationCommand());
                Dialogue.Hide();
                OpenTrade(shop);
            };
            _tracked = new Label { name = "xk-tracked" };
            _tracked.AddToClassList("xk-tracked");
            _tracked.pickingMode = PickingMode.Ignore;
            Hud.Root.Add(_tracked);
            if (HubWindow != null)
            {
                HubWindow.JournalSource = () => _session?.GetJournal();
                HubWindow.QuestSource = id => _session?.GetQuest(id);
                HubWindow.ReputationSource = () => _session?.GetReputation();
                HubWindow.PlacesSource = () => _session?.GetKnownPlaces(); // Phase 13
            }

            _questSignature = QuestSignature();
            RefreshTracked();
        }

        /// <summary>Opens a conversation with someone here (as "Talk…" does).</summary>
        public CommandResult StartDialogue(NpcId npc)
        {
            if (_session == null || Dialogue == null) return CommandResult.InvalidState("No world session is active.");
            var result = _session.Dispatch(new StartConversationCommand(npc));
            if (!result.IsSuccess)
            {
                Hud?.ShowMessage(result);
                return result;
            }

            Hud?.HideResponse();
            Trade?.Hide();
            HubWindow?.Hide();
            Dialogue.Show(_session.GetConversation());
            SetWindowMode(true);
            AfterQuestCommand();
            return result;
        }

        public CommandResult ChooseResponse(string id)
        {
            if (_session == null || Dialogue == null) return CommandResult.InvalidState("No world session is active.");
            var result = _session.Dispatch(new ChooseResponseCommand(id));
            var view = _session.GetConversation();
            if (InFight)
            {
                Dialogue.Hide();
                _gridOriginSet = false;
                RefreshCombat(result.Message, !result.IsSuccess);
            }
            else if (view == null)
            {
                Dialogue.Hide();
                SetWindowMode(false);
                Hud?.ShowMessage(result);
            }
            else Dialogue.Show(view, result.IsSuccess ? null : ResidentView.Describe(result), !result.IsSuccess);

            RefreshLiving(snap: false);
            AfterQuestCommand();
            return result;
        }

        public void EndDialogue()
        {
            if (_session != null && _session.GetConversation() != null) _session.Dispatch(new EndConversationCommand());
            Dialogue?.Hide();
            if (!WindowOpen) SetWindowMode(false);
        }

        /// <summary>Examines a world object (the ledger, the notice board) through the one inspect command.</summary>
        public CommandResult Inspect(string objectKey)
        {
            if (_session == null) return CommandResult.InvalidState("No world session is active.");
            var result = _session.Dispatch(new InspectObjectCommand(objectKey));
            AfterQuestCommand();
            return result;
        }

        /// <summary>After any command that may move a quest: refresh the tracked line and say what changed.</summary>
        public void AfterQuestCommand()
        {
            var signature = QuestSignature();
            if (signature != _questSignature)
            {
                var notice = Notice(_questSignature, signature);
                _questSignature = signature;
                if (notice != null)
                {
                    LastQuestNotice = notice;
                    Hud?.ShowMessage(CommandResult.Ok(notice));
                }
            }

            RefreshTracked();
        }

        private string QuestSignature()
        {
            var journal = _session?.GetJournal();
            if (journal == null) return string.Empty;
            var sb = new StringBuilder();
            foreach (var list in new[] { journal.Active, journal.Available, journal.Completed, journal.Failed, journal.Abandoned })
                foreach (var q in list) sb.Append(q.InstanceId).Append('=').Append(q.State).Append(':').Append(q.Next).Append('|');
            return sb.ToString();
        }

        /// <summary>The first change between two signatures, in words ("Quest completed: Short Measures").</summary>
        private string Notice(string before, string after)
        {
            var journal = _session?.GetJournal();
            if (journal == null) return null;
            foreach (var list in new[] { journal.Completed, journal.Failed, journal.Abandoned, journal.Active, journal.Available })
                foreach (var q in list)
                {
                    var key = q.InstanceId + "=" + q.State + ":" + q.Next + "|";
                    if (before.Contains(key)) continue;
                    switch (q.State)
                    {
                        case QuestState.Completed: return "Quest completed: " + q.Name + ".";
                        case QuestState.Failed: return "Quest failed: " + q.Name + " — " + q.Next;
                        case QuestState.Abandoned: return "Quest abandoned: " + q.Name + ".";
                        case QuestState.Active: return before.Contains(q.InstanceId + "=") && !before.Contains(q.InstanceId + "=Offered") && !before.Contains(q.InstanceId + "=Discovered") ? "Quest updated: " + q.Name + " — " + q.Next : "New quest: " + q.Name + ".";
                        case QuestState.Discovered:
                        case QuestState.Offered: return "You heard of something: " + q.Name + ".";
                    }
                }

            return null;
        }

        private void RefreshTracked()
        {
            if (_tracked == null) return;
            var tracked = _session?.GetTrackedQuests();
            if (tracked == null || tracked.Count == 0 || WindowOpen) // hidden while a window is open (it would sit over it)
            {
                _tracked.text = string.Empty;
                _tracked.AddToClassList("xk-hidden");
                return;
            }

            var lines = new List<string>();
            foreach (var q in tracked) lines.Add(q.Name + (string.IsNullOrEmpty(q.Next) ? string.Empty : "\n  " + q.Next));
            _tracked.text = string.Join("\n", lines);
            _tracked.RemoveFromClassList("xk-hidden");
        }

        /// <summary>Number keys choose responses while a conversation is open (Esc leaves it).</summary>
        private bool UpdateDialogueInput(Keyboard keys)
        {
            if (Dialogue == null || !Dialogue.IsOpen || keys == null) return false;
            if (keys.escapeKey.wasPressedThisFrame)
            {
                EndDialogue();
                return true;
            }

            var digits = new[] { keys.digit1Key, keys.digit2Key, keys.digit3Key, keys.digit4Key, keys.digit5Key, keys.digit6Key, keys.digit7Key, keys.digit8Key, keys.digit9Key };
            for (var i = 0; i < digits.Length; i++)
                if (digits[i].wasPressedThisFrame)
                {
                    Dialogue.PickNumber(i + 1);
                    return true;
                }

            return true;
        }
    }
}
