using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Combat;
using Realmkeeper.Domain.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 8 of the settlement visit: fights. The practice fight starts from the HUD (or F); the encounter panel shows
    /// the engine's snapshot; the player's choices and moves are commands; computer-controlled combatants act one turn
    /// at a time on a short timer (or at once with Continue), each turn a command. While a fight is on the character
    /// stands still in the street, the clock is paused, and placeholder ground markers show where everyone stands on
    /// the 5-ft grid (1.5 m squares around where the player stood when it began). Nothing here decides a rule.
    /// </summary>
    public sealed partial class ExplorationController
    {
        public const float AiTurnSeconds = 0.6f;
        public const float SquareMetres = 1.5f;

        private Button _practiceButton;
        private Button _casterPracticeButton;
        private Button _outingButton;
        private int _outing;
        private GameObject _markers;
        private readonly Dictionary<string, GameObject> _markerViews = new Dictionary<string, GameObject>();
        private Vector3 _gridOrigin;
        private bool _gridOriginSet;
        private bool _fightOpen;
        private bool _resumePlaybackAfterFight;
        private float _aiTimer;

        /// <summary>The encounter panel.</summary>
        public CombatWindow Combat { get; private set; }

        /// <summary>When true, computer-controlled turns run by themselves every <see cref="AiTurnSeconds"/>; tests may turn it off and step them.</summary>
        public bool AutoAi { get; set; } = true;

        /// <summary>True while an encounter is going on (the panel is open on an active fight).</summary>
        public bool InFight => Combat != null && Combat.IsOpen && Combat.Snapshot != null && Combat.Snapshot.Status == EncounterStatus.Active;

        /// <summary>True when the player character has died (no resurrection exists yet: they can only look on).</summary>
        public bool PlayerIsDead => _session?.GetCharacter()?.IsDead == true;

        public GameObject CombatMarkers => _markers;

        private void SetupCombat()
        {
            if (Hud?.Root == null) return;
            Combat = new CombatWindow(Hud.Root);
            Combat.ActionRequested += a => CombatAct(a);
            Combat.MoveRequested += p => CombatMove(p);
            Combat.WithdrawRequested += () => EndFight(withdraw: true);
            Combat.CallOffRequested += () => EndFight(withdraw: false);
            Combat.ContinueRequested += () => RunAiTurn();
            Combat.CloseRequested += CloseFight;
            Combat.LootRequested += key => { CloseFight(); OpenStorage(key); }; // Phase 11
            Combat.CastRequested += a => CombatAct(a); // Phase 9
            Combat.FeatureRequested += f => CombatAct(f);
            Combat.ReactionAnswered += (id, accept) => AnswerReaction(id, accept); // Phase 10
            Combat.MoveProblem = p => _session?.PlayerMoveProblem(p, out _);
            var row = Hud.Root.Q<VisualElement>("xk-clock-row");
            if (row != null)
            {
                _practiceButton = TimeButton(row, "Practice fight (F)", "xk-open-practice", () => StartPracticeFight());
                _practiceButton.tooltip = "A labelled practice encounter: two bandits at the edge of town, with the local guard beside you if there is one. Lethal: the fallen can die.";
                _casterPracticeButton = TimeButton(row, "Practice vs caster (G)", "xk-open-practice-caster", () => StartPracticeFight(true, true));
                _casterPracticeButton.tooltip = "The practice fight with a Cult Fanatic (an SRD spellcaster) among the bandits.";
                _outingButton = TimeButton(row, "Go out: outskirts", "xk-open-outing", () => StartOuting());
                _outingButton.tooltip = "Phase 10: walk out of the settlement — to its outskirts, along the road, or to the farms — and meet whatever lives there now (from the world's own creature populations).";
            }
        }

        private void TeardownCombat()
        {
            if (_fightOpen) ResumeTimeAfterFight();
            ClearMarkers();
            Combat = null;
            _practiceButton = null;
            _casterPracticeButton = null;
            _outingButton = null;
            _fightOpen = false;
            _gridOriginSet = false;
        }

        // ----- Commands --------------------------------------------------------------------------------

        /// <summary>Starts the practice fight here (refused with the reason: no character yet, dead, another fight …).</summary>
        public CommandResult StartPracticeFight(bool lethal = true, bool withCaster = false)
        {
            if (_session == null) return CommandResult.InvalidState("No world session is active.");
            CloseWindows();
            var result = _session.Dispatch(new StartPracticeEncounterCommand(lethal, withCaster));
            if (!result.IsSuccess)
            {
                Hud?.ShowMessage(result);
                return result;
            }

            _gridOriginSet = false;
            RefreshCombat(result.Message, false);
            return result;
        }

        private static readonly Realmkeeper.Simulation.Encounters.EncounterPurpose[] Outings =
        {
            Realmkeeper.Simulation.Encounters.EncounterPurpose.Outskirts, Realmkeeper.Simulation.Encounters.EncounterPurpose.Road, Realmkeeper.Simulation.Encounters.EncounterPurpose.Farms,
        };

        /// <summary>
        /// Phase 10: an outing — the encounter the world gives where the player goes (refused with the reason). The button
        /// cycles where the next outing goes after each one.
        /// </summary>
        public CommandResult StartOuting(Realmkeeper.Simulation.Encounters.EncounterPurpose? purpose = null)
        {
            if (_session == null) return CommandResult.InvalidState("No world session is active.");
            CloseWindows();
            var where = purpose ?? Outings[_outing % Outings.Length];
            var result = _session.Dispatch(new StartWorldEncounterCommand(where));
            if (!result.IsSuccess)
            {
                Hud?.ShowMessage(result);
                return result;
            }

            _outing++;
            if (_outingButton != null) _outingButton.text = "Go out: " + Outings[_outing % Outings.Length].ToString().ToLowerInvariant();
            _gridOriginSet = false;
            RefreshCombat(result.Message, false);
            return result;
        }

        /// <summary>The player's action this turn (the snapshot's sequence guards against a double click).</summary>
        public CommandResult CombatAct(CombatAction action)
        {
            var s = Combat?.Snapshot;
            if (_session == null || s == null || s.Player == null) return CommandResult.InvalidState("There is no fight going on.");
            var result = _session.Dispatch(new CombatActionCommand(s.Id, s.Player.Id, action, s.Sequence));
            RefreshCombat(Explain(result), !result.IsSuccess);
            _aiTimer = AiTurnSeconds;
            return result;
        }

        /// <summary>Phase 10: the player's answer to the reaction or decision they are asked (refused with the reason when stale).</summary>
        public CommandResult AnswerReaction(int reactionId, bool accept)
        {
            var s = Combat?.Snapshot;
            if (_session == null || s == null) return CommandResult.InvalidState("There is no fight going on.");
            var result = _session.Dispatch(new RespondToReactionCommand(s.Id, reactionId, accept));
            RefreshCombat(Explain(result), !result.IsSuccess);
            _aiTimer = AiTurnSeconds;
            return result;
        }

        /// <summary>Moves the player to a square along the straight path the session plans (refused with the reason).</summary>
        public CommandResult CombatMove(GridPos to)
        {
            if (_session == null) return CommandResult.InvalidState("No world session is active.");
            var problem = _session.PlayerMoveProblem(to, out var path);
            if (problem != null)
            {
                var refused = CommandResult.Invalid(problem, new[] { problem });
                RefreshCombat(problem, true);
                return refused;
            }

            return CombatAct(new MoveAction(path));
        }

        /// <summary>Runs the active computer-controlled combatant's whole turn.</summary>
        public CommandResult RunAiTurn()
        {
            var s = Combat?.Snapshot;
            if (_session == null || s == null) return CommandResult.InvalidState("There is no fight going on.");
            var result = _session.Dispatch(new RunAiTurnCommand(s.Id, s.Sequence));
            RefreshCombat(result.IsSuccess ? (string.IsNullOrEmpty(result.Message) ? $"{s.Active?.Name} waits." : result.Message) : Explain(result), !result.IsSuccess);
            _aiTimer = AiTurnSeconds;
            return result;
        }

        public CommandResult EndFight(bool withdraw)
        {
            var s = Combat?.Snapshot;
            if (_session == null || s == null) return CommandResult.InvalidState("There is no fight going on.");
            var result = _session.Dispatch(new EndEncounterCommand(s.Id, withdraw));
            RefreshCombat(Explain(result), !result.IsSuccess);
            return result;
        }

        /// <summary>Closes the panel of a finished fight (an active one cannot be closed: withdraw or call it off).</summary>
        public void CloseFight()
        {
            if (Combat == null || InFight) return;
            Combat.Hide();
            ClearMarkers();
            _fightOpen = false;
            _gridOriginSet = false;
            SetWindowMode(WindowOpen);
            if (PlayerIsDead) Hud?.ShowMessage(CommandResult.InvalidState($"{_session.GetCharacter().Name} has died. There is no way back yet; load an earlier save to play on."));
            RefreshLiving(snap: false);
        }

        private static string Explain(CommandResult r) => r.IsSuccess ? r.Message : (r.Message + (r.Errors.Count > 0 && r.Errors[0] != r.Message ? " " + string.Join(" ", r.Errors) : string.Empty));

        // ----- Showing the fight -----------------------------------------------------------------------

        /// <summary>Opens or updates the panel from the session (an active encounter, or the one just finished while the panel is open).</summary>
        private void RefreshCombat(string result = null, bool failed = false)
        {
            if (Combat == null || _session == null || !_session.HasWorld) return;
            var s = _session.GetEncounter(includeEnded: _fightOpen);
            if (s == null || (!_fightOpen && s.Status != EncounterStatus.Active))
            {
                if (Combat.IsOpen && !_fightOpen) Combat.Hide();
                RefreshPracticeButton();
                return;
            }

            if (!_fightOpen)
            {
                _fightOpen = true;
                PauseTimeForFight();
                Hud?.HideResponse();
                TalkingTo = null;
            }

            Combat.Show(s, result, failed);
            UpdateMarkers(s);
            SetWindowMode(true);
            RefreshPracticeButton();
        }

        private void RefreshPracticeButton()
        {
            if (_practiceButton == null) return;
            var character = _session?.GetCharacter();
            _practiceButton.EnableInClassList("xk-hidden", character == null || character.NeedsCreation || character.IsDead || _fightOpen);
            _casterPracticeButton?.EnableInClassList("xk-hidden", character == null || character.NeedsCreation || character.IsDead || _fightOpen);
            _outingButton?.EnableInClassList("xk-hidden", character == null || character.NeedsCreation || character.IsDead || _fightOpen);
        }

        private void PauseTimeForFight()
        {
            var playback = _runtime?.Playback;
            if (playback == null) return;
            _resumePlaybackAfterFight = playback.IsPlaying;
            playback.Pause();
        }

        private void ResumeTimeAfterFight()
        {
            var playback = _runtime?.Playback;
            if (playback != null && _resumePlaybackAfterFight && !PlayerIsDead) playback.Play();
            _resumePlaybackAfterFight = false;
        }

        /// <summary>Per frame: computer-controlled turns on a timer; F starts a practice fight.</summary>
        private void UpdateCombat()
        {
            if (Combat == null) return;
            if (_fightOpen && !InFight && _resumePlaybackAfterFight) ResumeTimeAfterFight();
            if (InFight && !Combat.Snapshot.IsPlayersTurn && AutoAi && Combat.Snapshot.Prompt == null) // a waiting question stops the clock
            {
                _aiTimer -= Time.unscaledDeltaTime;
                if (_aiTimer <= 0f) RunAiTurn();
            }

            var keys = Keyboard.current;
            if (keys != null && !WindowOpen && !Typing() && !InPlace && keys.fKey.wasPressedThisFrame) StartPracticeFight(); // Phase 13: not inside places
            if (keys != null && !WindowOpen && !Typing() && keys.gKey.wasPressedThisFrame) StartPracticeFight(true, true);
        }

        // ----- Placeholder ground markers --------------------------------------------------------------

        private Vector3 SquareToWorld(GridPos p) => _gridOrigin + new Vector3(p.X * SquareMetres, 0f, p.Y * SquareMetres);

        private void UpdateMarkers(EncounterSnapshot s)
        {
            if (Player == null || _materials == null) return;
            if (!_gridOriginSet)
            {
                var at = s.Player != null ? s.Player.Position : default;
                _gridOrigin = Player.transform.position - new Vector3(at.X * SquareMetres, 0f, at.Y * SquareMetres);
                _gridOriginSet = true;
            }

            if (_markers == null) _markers = new GameObject("Combat markers");
            foreach (var c in s.Order)
            {
                var key = c.Id.ToString();
                if (!_markerViews.TryGetValue(key, out var view) || view == null)
                {
                    view = MakeMarker(c);
                    _markerViews[key] = view;
                }

                var pos = SquareToWorld(c.Position);
                view.transform.position = pos;
                view.SetActive(c.State != VitalState.Fled);
                var ring = view.transform.Find("Ring");
                if (ring != null) ring.localScale = c.IsActive ? new Vector3(1.25f, 0.02f, 1.25f) : new Vector3(1.0f, 0.02f, 1.0f);
                var figure = view.transform.Find("Figure");
                if (figure != null)
                {
                    var down = c.State != VitalState.Conscious;
                    figure.localRotation = down ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
                    figure.localPosition = down ? new Vector3(0f, 0.3f, 0f) : new Vector3(0f, 0.9f, 0f);
                }

                if (c.Kind == CombatantKind.Player && c.State == VitalState.Conscious && (Player.transform.position - pos).sqrMagnitude > 0.04f)
                    Player.Teleport(new LocalPose(pos.x, pos.y, pos.z, Player.transform.eulerAngles.y));
            }
        }

        private GameObject MakeMarker(CombatantView c)
        {
            var root = new GameObject("Marker " + c.Name);
            root.transform.SetParent(_markers.transform, false);
            var colour = c.Kind == CombatantKind.Player ? new Color(0.25f, 0.55f, 0.95f) : c.HostileToPlayer ? new Color(0.85f, 0.22f, 0.18f) : new Color(0.30f, 0.72f, 0.35f);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Ring";
            Object.Destroy(ring.GetComponent<Collider>());
            ring.transform.SetParent(root.transform, false);
            ring.transform.localScale = new Vector3(1.0f, 0.02f, 1.0f);
            ring.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            ring.GetComponent<Renderer>().sharedMaterial = _materials.Get(colour, 0.4f);
            if (c.Kind != CombatantKind.Player)
            {
                // A stand-in figure (no bandit or wolf models exist yet): a capsule in the side's colour.
                var figure = GameObject.CreatePrimitive(c.CreatureIsBeast ? PrimitiveType.Cube : PrimitiveType.Capsule);
                figure.name = "Figure";
                Object.Destroy(figure.GetComponent<Collider>());
                figure.transform.SetParent(root.transform, false);
                figure.transform.localScale = c.CreatureIsBeast ? new Vector3(0.45f, 0.6f, 1.0f) : new Vector3(0.55f, 0.9f, 0.55f);
                figure.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                figure.GetComponent<Renderer>().sharedMaterial = _materials.Get(Color.Lerp(colour, new Color(0.35f, 0.3f, 0.25f), 0.45f));
            }

            if (c.Kind == CombatantKind.Player) return root; // the body is the player's marker; its label would cover the others
            var label = new GameObject("Label");
            label.transform.SetParent(root.transform, false);
            label.transform.localPosition = new Vector3(0f, 2.15f, 0f);
            WorldText(label, c.Name, colour, 0.022f);
            label.AddComponent<FaceCamera>();
            return root;
        }

        private void ClearMarkers()
        {
            if (_markers != null) Object.Destroy(_markers);
            _markers = null;
            _markerViews.Clear();
        }
    }

    /// <summary>Turns a label to face the main camera (marker names).</summary>
    public sealed class FaceCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = UnityEngine.Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
