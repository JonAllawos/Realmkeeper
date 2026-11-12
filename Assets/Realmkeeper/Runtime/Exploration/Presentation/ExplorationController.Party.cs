using System.Collections.Generic;
using System.Linq;
using Realmkeeper.Application;
using Realmkeeper.Application.Party;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 15 (ADR-0073): the party in a settlement. Each active member with the player has one body
    /// (<see cref="CompanionFollower"/>), keyed by their ID; a resident body standing for the same person is removed
    /// first, so nobody is drawn twice. Following members keep their formation slot; a member told to wait stays where
    /// they stood. The HUD strip shows who travels with the player (HP, state, loyalty) and opens the Party section.
    /// In places the place scene draws the party (as followers); in fights the fight draws them.
    /// </summary>
    public sealed partial class ExplorationController
    {
        private readonly Dictionary<NpcId, CompanionFollower> _companions = new Dictionary<NpcId, CompanionFollower>();
        private readonly List<CompanionFollower> _companionList = new List<CompanionFollower>();
        private Transform _companionsRoot;
        private string _partySignature;
        private float _partyCheck;
        private bool _partyFresh = true;
        private Vector3 _lastPlayerPos;
        private VisualElement _partyStrip;
        private string _partyStripSignature;

        /// <summary>The bodies of the party members drawn in this settlement, by person.</summary>
        public IReadOnlyDictionary<NpcId, CompanionFollower> Companions => _companions;

        /// <summary>The text the party strip shows (for tests and captures).</summary>
        public string PartyStripText { get; private set; } = string.Empty;

        /// <summary>Recoveries made by followers that could not keep up (explicit and counted).</summary>
        public int CompanionRecoveries => _companionList.Sum(c => c != null ? c.Recoveries : 0);

        /// <summary>Dispatches a party command from a window, refreshes the hub and the bodies, and returns the result.</summary>
        public CommandResult PartyCommand(ICommand command)
        {
            if (_session == null) return CommandResult.InvalidState("No world session is active.");
            // Phase 16: told to wait, a companion keeps the spot their body stands on (found there on return).
            if (command is SetFollowModeCommand wait && wait.Mode == FollowMode.Wait && wait.At == null && _companions.TryGetValue(wait.NpcId, out var standing) && standing != null)
                command = new SetFollowModeCommand(wait.NpcId, FollowMode.Wait, new LocalPose(standing.transform.position.x, standing.transform.position.y, standing.transform.position.z, standing.transform.eulerAngles.y));
            var r = DispatchParty(command);
            _partySignature = null;
            SyncParty();
            if (HubWindow != null && HubWindow.IsOpen) ShowHub(ResidentView.Describe(r), !r.IsSuccess);
            if (HubWindow != null && HubWindow.IsOpen && HubWindow.Section != "Party") HubWindow.Select("Party");
            return r;
        }

        /// <summary>The session resolves handlers by the command's static type: each party command is dispatched as itself.</summary>
        private CommandResult DispatchParty(ICommand command)
        {
            switch (command)
            {
                case RecruitCompanionCommand x: return _session.Dispatch(x);
                case DismissCompanionCommand x: return _session.Dispatch(x);
                case SetFollowModeCommand x: return _session.Dispatch(x);
                case RegroupPartyCommand x: return _session.Dispatch(x);
                case SetPartyRoleCommand x: return _session.Dispatch(x);
                case MovePartyMemberCommand x: return _session.Dispatch(x);
                case SetPartyLeaderCommand x: return _session.Dispatch(x);
                case TransferItemCommand x: return _session.Dispatch(x);
                case CompanionEquipCommand x: return _session.Dispatch(x);
                case CompanionUnequipCommand x: return _session.Dispatch(x);
                default: return CommandResult.Unsupported("Not a party command: " + command?.GetType().Name);
            }
        }

        /// <summary>Opens the hub at the Party section (K).</summary>
        public void OpenParty()
        {
            if (HubWindow == null) return;
            ShowHub(null, false);
            HubWindow.ShowParty();
        }

        /// <summary>Makes the bodies match the party: who is drawn, their order, follow or wait. Cheap when nothing changed.</summary>
        private void SyncParty()
        {
            if (_session == null || !_session.HasWorld || !IsExploring || InPlace || Player == null)
            {
                ClearCompanions();
                return;
            }

            var party = _session.GetParty();
            var sig = string.Join("|", party.Members.Select(m => m.NpcId + ":" + m.Role + m.Mode + m.Order + m.Present + m.State));
            RefreshPartyStrip(party);
            if (sig == _partySignature && !_partyFresh) return;
            _partySignature = sig;
            if (_companionsRoot == null) _companionsRoot = new GameObject("Companions").transform;

            var wanted = new HashSet<NpcId>();
            foreach (var m in party.Members)
            {
                if (m.Role != PartyRole.Active || m.State == "dead" || !m.Present) continue;
                wanted.Add(m.NpcId);
                if (!_companions.TryGetValue(m.NpcId, out var f))
                {
                    var npc = _session.FindNpc(m.NpcId);
                    if (npc == null) continue;
                    var slot = CompanionFollower.SlotFor(Player.transform, m.Order, 2.5f + 1.2f * m.Order);
                    var at = slot;
                    if (m.Mode == FollowMode.Wait && m.WaitPose is LocalPose kept) at = new Vector3(kept.X, kept.Y, kept.Z); // Phase 16: where they were left
                    if (_residents.TryGetValue(m.NpcId, out var resident))
                    {
                        // They step out of their resident body: where it stood if they just joined here; beside the player on arrival.
                        if (!_partyFresh && resident != null && !resident.IsHidden && Vector3.Distance(resident.transform.position, Player.transform.position) < 25f) at = resident.transform.position;
                        _detector.Unregister(resident);
                        _residents.Remove(m.NpcId);
                        if (resident != null) Destroy(resident.gameObject);
                    }

                    f = CompanionFollower.Create(_companionsRoot, npc, Ground(at, Player.transform.position), Player.transform.eulerAngles.y, Catalog, _materials);
                    _companions.Add(m.NpcId, f);
                    _companionList.Add(f);
                    _detector.Register(f);
                }

                f.Mode = m.Mode;
                f.Order = m.Order;
                f.FollowDistance = 2.2f + 1.1f * m.Order;
            }

            foreach (var id in _companions.Keys.Where(k => !wanted.Contains(k)).ToList()) RemoveCompanion(id, restoreResident: true);
            _partyFresh = false;
        }

        private void RemoveCompanion(NpcId id, bool restoreResident)
        {
            if (!_companions.TryGetValue(id, out var f)) return;
            _detector.Unregister(f);
            _companions.Remove(id);
            _companionList.Remove(f);
            var at = f != null ? f.transform.position : Vector3.zero;
            if (f != null) Destroy(f.gameObject);
            if (!restoreResident || _residentsRoot == null || _streamer.Current == null || _residents.ContainsKey(id)) return;

            // Back to their own life in this settlement: their resident body returns where they stood, and the
            // whereabouts of the hour then walk them on.
            var layout = _streamer.Current;
            var spot = layout.Residents.FirstOrDefault(s => s.NpcId == id);
            var npc = _session?.FindNpc(id);
            if (spot == null || npc == null || !npc.IsAlive) return;
            var view = ResidentView.Create(_residentsRoot, spot, npc, _session, Player != null ? Player.transform : null, _materials, Catalog, layout);
            view.transform.position = at;
            _residents.Add(id, view);
            _detector.Register(view);
        }

        private void ClearCompanions()
        {
            foreach (var f in _companionList) if (f != null) Destroy(f.gameObject);
            foreach (var f in _companionList) _detector.Unregister(f);
            _companions.Clear();
            _companionList.Clear();
            if (_companionsRoot != null) Destroy(_companionsRoot.gameObject);
            _companionsRoot = null;
            _partySignature = null;
            _partyFresh = true;
            _partyStrip?.RemoveFromHierarchy();
            _partyStrip = null;
            _partyStripSignature = null;
        }

        /// <summary>Each frame: followers walk; in a fight they are hidden (the fight draws its own figures).</summary>
        private void UpdateParty()
        {
            if (Player == null || InPlace) return;
            _partyCheck -= Time.deltaTime;
            if (_partyCheck <= 0f)
            {
                _partyCheck = 1f;
                SyncParty();
            }

            if (_partyStrip != null) _partyStrip.style.display = WindowOpen ? DisplayStyle.None : DisplayStyle.Flex; // windows sit on top
            var dt = Time.deltaTime;
            var pos = Player.transform.position;
            var playerSpeed = dt > 0f ? Vector3.ProjectOnPlane(pos - _lastPlayerPos, Vector3.up).magnitude / dt : 0f;
            if (playerSpeed > 25f) playerSpeed = 0f; // a teleport, not movement
            _lastPlayerPos = pos;
            foreach (var f in _companionList)
            {
                if (f == null) continue;
                f.SetShown(!InFight);
                if (!InFight) f.Step(Player.transform, playerSpeed, _companionList, dt);
            }
        }

        private static Vector3 Ground(Vector3 want, Vector3 fallback)
        {
            if (Physics.Raycast(want + Vector3.up * 3f, Vector3.down, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) return hit.point;
            return new Vector3(want.x, fallback.y, want.z);
        }

        /// <summary>The HUD's party strip: one line per member (HP, state, follow or wait, loyalty); click to open the Party section.</summary>
        private void RefreshPartyStrip(PartyView party)
        {
            if (Hud?.Root == null) return;
            var sig = string.Join("|", party.Members.Select(m => m.Name + m.Hp + "/" + m.MaxHp + m.State + m.Mode + m.Role + m.Loyalty + m.IsLeader));
            if (sig == _partyStripSignature && _partyStrip != null && _partyStrip.panel != null) return;
            _partyStripSignature = sig;
            _partyStrip?.RemoveFromHierarchy();
            _partyStrip = null;
            if (party.Members.Count == 0) { PartyStripText = string.Empty; return; }
            var strip = new VisualElement { name = "xk-party" };
            strip.AddToClassList("xk-party");
            var text = new System.Text.StringBuilder();
            foreach (var m in party.Members)
            {
                var row = new Button(() => { if (HubWindow != null) { ShowHub(null, false); HubWindow.ShowParty(m.NpcId); } }) { name = "xk-party-" + m.NpcId };
                row.AddToClassList("xk-party-row");
                row.EnableInClassList("xk-party-row--reserve", m.Role == PartyRole.Reserve);
                var name = new Label((m.IsLeader ? "★ " : string.Empty) + m.Name);
                name.AddToClassList("xk-party-name");
                row.Add(name);
                var bar = new VisualElement();
                bar.AddToClassList("xk-party-hp");
                var fill = new VisualElement();
                fill.AddToClassList("xk-party-hp-fill");
                fill.EnableInClassList("xk-party-hp-fill--low", m.MaxHp > 0 && m.Hp * 4 <= m.MaxHp);
                fill.style.width = new Length(m.MaxHp > 0 ? Mathf.Clamp01((float)m.Hp / m.MaxHp) * 100f : 0f, LengthUnit.Percent);
                bar.Add(fill);
                row.Add(bar);
                var state = m.Role == PartyRole.Reserve ? "reserve" : m.Mode == FollowMode.Wait ? "waiting" : m.State;
                var info = new Label($"{m.Hp}/{m.MaxHp} · {state} · {m.LoyaltyWord}");
                info.AddToClassList("xk-party-info");
                row.Add(info);
                strip.Add(row);
                text.AppendLine($"{m.Name}: {m.Hp}/{m.MaxHp} HP, {state}, loyalty {m.Loyalty} ({m.LoyaltyWord})");
            }

            PartyStripText = text.ToString();
            Hud.Root.Add(strip);
            _partyStrip = strip;
        }
    }
}
