using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Identifiers;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Exploration.Domain;
using Realmkeeper.Exploration.Interaction;
using Realmkeeper.Exploration.WorldStreaming;
using Realmkeeper.Hosting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Presents the player character's settlement visit. It owns no world state: it watches the
    /// session and shows the local environment whenever world data says the character is in a
    /// settlement, and removes it when the character is back on the world map. Everything that
    /// must persist (entering, leaving, position, perspective, conversations, notices) is a
    /// command dispatched through the session's registry.
    /// </summary>
    public sealed class ExplorationController : MonoBehaviour
    {

        private readonly LocalEnvironmentStreamer _streamer = new LocalEnvironmentStreamer();
        private readonly InteractionDetector _detector = new InteractionDetector();
        private readonly Dictionary<NpcId, ResidentView> _residents = new Dictionary<NpcId, ResidentView>();
        private readonly List<EnvironmentInteractable> _interactables = new List<EnvironmentInteractable>();

        private RealmkeeperRuntime _runtime;
        private WorldSessionBinding _binding;
        private WorldSession _session => _binding?.Session;
        private ExplorationMaterials _materials;
        private IExplorationInput _input;
        private bool _ownsInput;
        private SettlementEnvironmentBuilder.RenderSettingsSnapshot _renderSnapshot;
        private ExplorationAtmosphere _atmosphere;
        private bool _cursorFree;

        public LocalEnvironmentStreamer Streamer => _streamer;
        public PlayerCharacter Player { get; private set; }
        public ExplorationCameraRig CameraRig { get; private set; }
        public ExplorationHud Hud { get; private set; }
        public GameObject EnvironmentRoot { get; private set; }

        /// <summary>The art catalog the current environment was built with, or null for the primitive fallback.</summary>
        public ArtCatalog Catalog { get; private set; }
        public IReadOnlyDictionary<NpcId, ResidentView> Residents => _residents;
        public IReadOnlyList<EnvironmentInteractable> Interactables => _interactables;
        public InteractionDetector Detector => _detector;
        public IInteractable CurrentTarget { get; private set; }
        public InteractionResponse LastResponse { get; private set; }

        /// <summary>When false, the cursor is never locked (tests and batch mode).</summary>
        public bool ManageCursor { get; set; } = true;

        public bool IsExploring => _streamer.IsLoaded;

        /// <param name="input">Input to read; null creates the keyboard-and-mouse Input System bindings.</param>
        public void Initialize(RealmkeeperRuntime runtime, IExplorationInput input = null)
        {
            _runtime = runtime;
            _input = input;
            _binding = new WorldSessionBinding(OnWorldChanged);
            if (_input == null)
            {
                _input = new InputSystemExplorationInput();
                _ownsInput = true;
            }

            _runtime.PreparingSave += OnPreparingSave;
            _runtime.Host.SessionStarted += BindSession;
            _runtime.Host.SessionEnded += UnbindSession;
            if (_runtime.Host.IsActive) BindSession(_runtime.Host.Session);
        }

        /// <summary>Replaces the input source (tests drive the character with scripted input).</summary>
        public void SetInput(IExplorationInput input)
        {
            if (_ownsInput && _input is InputSystemExplorationInput owned) owned.Dispose();
            _ownsInput = false;
            _input = input;
            if (Player != null) Player.Input = input;
            if (CameraRig != null) CameraRig.Input = input;
            if (IsExploring) _input?.Enable();
        }

        // ----- Session binding ---------------------------------------------------------------

        private void BindSession(WorldSession session)
        {
            if (!_binding.Bind(session)) return; // already bound: never subscribe twice
            UnloadEnvironment();
            Sync(forceReload: true);
        }

        private void UnbindSession()
        {
            _binding?.Unbind();
            UnloadEnvironment();
        }

        private void OnWorldChanged(WorldChange change)
        {
            if (change.Reason == WorldChangeReason.Closed)
            {
                UnloadEnvironment();
                return;
            }

            // A loaded save may put the character somewhere else (or in the same place, with
            // different residents' state): rebuild from world data instead of trusting the scene.
            Sync(forceReload: change.GeographyChanged);
        }

        /// <summary>Makes the presentation match world data: environment loaded if and only if the character is in a settlement.</summary>
        private void Sync(bool forceReload)
        {
            var player = _session != null && _session.HasWorld ? _session.GetPlayer() : null;
            if (player == null || !player.IsInSettlement)
            {
                UnloadEnvironment();
                return;
            }

            var loaded = _streamer.Current;
            if (forceReload || loaded == null || loaded.SettlementId != player.SettlementId)
            {
                UnloadEnvironment();
                LoadEnvironment(player);
            }

            if (CameraRig != null && CameraRig.Perspective != player.Perspective)
            {
                CameraRig.SetPerspective(player.Perspective);
                Hud?.SetPerspective(player.Perspective);
            }

            RefreshHud();
        }

        // ----- Environment lifetime ----------------------------------------------------------

        private void LoadEnvironment(PlayerSummary player)
        {
            var settlement = _session.FindSettlement(player.SettlementId);
            if (settlement == null)
            {
                Debug.LogError($"Realmkeeper: the character is in settlement {player.SettlementId}, which does not exist.");
                return;
            }

            var layout = ExplorationModule.LayoutFor(_session, settlement);
            _materials = new ExplorationMaterials();
            _renderSnapshot = SettlementEnvironmentBuilder.RenderSettingsSnapshot.Capture();
            _atmosphere = new ExplorationAtmosphere();
            _atmosphere.Capture(); // before the exploration scene becomes the active scene
            _streamer.Load(layout, settlement.Name); // creates and activates the exploration scene

            // The modular art kit when the art catalog is set up; the primitive vertical-slice builder otherwise.
            var catalog = ArtCatalog.Load();
            Catalog = catalog != null && catalog.Modules.Count > 0 ? catalog : null;
            if (Catalog != null)
            {
                EnvironmentRoot = ModularEnvironmentBuilder.Build(layout, Catalog);
                _atmosphere.Apply(EnvironmentRoot.transform, Catalog);
            }
            else
            {
                SettlementEnvironmentBuilder.ApplyAtmosphere();
                EnvironmentRoot = SettlementEnvironmentBuilder.Build(layout, _materials);
            }

            Player = PlayerCharacter.Create(null, _materials.Get(new Color(0.24f, 0.36f, 0.52f)), _materials.Get(new Color(0.55f, 0.42f, 0.30f)), Catalog);
            Player.Input = _input;
            Player.Teleport(player.Pose);
            CameraRig = ExplorationCameraRig.Create(null, Player);
            CameraRig.Input = _input;
            if (Catalog != null) ExplorationAtmosphere.ConfigureCamera(CameraRig.Camera);
            CameraRig.SetPerspective(player.Perspective);
            CameraRig.SetView(player.Pose.Yaw, 12f);
            CameraRig.Snap();

            _detector.Clear();
            var residents = new GameObject("Residents").transform;
            foreach (var spot in layout.Residents)
            {
                if (_residents.ContainsKey(spot.NpcId)) continue; // one body per persistent identity
                var npc = _session.FindNpc(spot.NpcId);
                if (npc == null) continue;
                var view = ResidentView.Create(residents, spot, npc, _session, Player.transform, _materials, Catalog);
                _residents.Add(spot.NpcId, view);
                _detector.Register(view);
            }

            var points = new GameObject("Interaction points").transform;
            foreach (var spec in layout.Interactables)
            {
                var view = EnvironmentInteractable.Create(points, spec, settlement.SettlementId, _materials);
                _interactables.Add(view);
                _detector.Register(view);
            }

            Hud = ExplorationHud.Create(null);
            Hud.ReturnRequested += OnReturnRequested;
            Hud.SaveRequested += OnSaveRequested;
            Hud.SetPerspective(player.Perspective);

            _input?.Enable();
            SetCursorFree(false);
            Debug.Log($"Realmkeeper: entered {settlement.Name} ({layout.EnvironmentKey}): {layout.Buildings.Count} buildings, " +
                      $"{_residents.Count} residents, {layout.Props.Count} props; character at {player.Pose}.");
        }

        private void UnloadEnvironment()
        {
            if (!_streamer.IsLoaded) return;
            _input?.Disable();
            if (Hud != null)
            {
                Hud.ReturnRequested -= OnReturnRequested;
                Hud.SaveRequested -= OnSaveRequested;
            }

            _detector.Clear();
            _residents.Clear();
            _interactables.Clear();
            CurrentTarget = null;
            _streamer.Unload(); // destroys every object in the exploration scene
            Player = null;
            CameraRig = null;
            Hud = null;
            EnvironmentRoot = null;
            _atmosphere?.Restore();
            _atmosphere = null;
            Catalog = null;
            _renderSnapshot?.Restore();
            _renderSnapshot = null;
            _materials?.Dispose();
            _materials = null;
            SetCursorFree(true);
        }

        // ----- Player actions ----------------------------------------------------------------

        private void OnReturnRequested() => ReturnToWorldMap();

        private void OnSaveRequested() => SaveNow();

        private void OnPreparingSave()
        {
            if (IsExploring) RecordPose();
        }

        /// <summary>Records where the character stands (bounded to the settlement) through the registry.</summary>
        public CommandResult RecordPose()
        {
            if (!IsExploring || Player == null || _session == null) return CommandResult.InvalidState("The character is not exploring.");
            var layout = _streamer.Current;
            var pose = Player.CurrentPose;
            var limit = layout.HalfExtent - 0.01f;
            pose = new LocalPose(Mathf.Clamp(pose.X, -limit, limit), pose.Y, Mathf.Clamp(pose.Z, -limit, limit), pose.Yaw);
            return _session.Dispatch(new SetPlayerPoseCommand(layout.SettlementId, pose));
        }

        public CommandResult TogglePerspective()
        {
            if (!IsExploring) return CommandResult.InvalidState("The character is not exploring.");
            RecordPose();
            var next = CameraRig.Perspective == CameraPerspective.ThirdPerson ? CameraPerspective.FirstPerson : CameraPerspective.ThirdPerson;
            var result = _session.Dispatch(new SetCameraPerspectiveCommand(next));
            if (!result.IsSuccess) Hud?.ShowMessage(result);
            return result;
        }

        public InteractionResponse Interact()
        {
            if (!IsExploring || CurrentTarget == null) return null;
            LastResponse = CurrentTarget.Interact(_session, Player.transform);
            Hud?.ShowResponse(LastResponse);
            TalkingTo = LastResponse != null && LastResponse.Succeeded ? CurrentTarget as ResidentView : null;
            Hud?.ShowTopics(TalkingTo == null ? null : TopicButtons());
            RefreshHud();
            return LastResponse;
        }

        /// <summary>The resident in conversation (Phase 4), or null.</summary>
        public ResidentView TalkingTo { get; private set; }

        /// <summary>Topics a player can raise; answers come from the resident's own knowledge.</summary>
        public static readonly IReadOnlyList<(ConversationTopic topic, string label)> Topics = new[]
        {
            (ConversationTopic.Themselves, "About you"),
            (ConversationTopic.Settlement, "This place"),
            (ConversationTopic.Governor, "Who governs"),
            (ConversationTopic.FactionLeader, "Your leader"),
            (ConversationTopic.News, "Any news?"),
            (ConversationTopic.Rumours, "Rumours"),
        };

        private IReadOnlyList<(string, Action)> TopicButtons()
        {
            var list = new List<(string, Action)>();
            foreach (var (topic, label) in Topics)
            {
                var t = topic;
                list.Add((label, () => AskResident(t)));
            }

            return list;
        }

        /// <summary>Asks the resident in conversation about a topic (typed command; the answer is theirs).</summary>
        public CommandResult AskResident(ConversationTopic topic, NpcId aboutPerson = default)
        {
            if (TalkingTo == null || _session == null) return CommandResult.InvalidState("You are not talking to anyone.");
            var result = _session.Dispatch(new AskNpcCommand(TalkingTo.NpcId, topic, aboutPerson));
            LastResponse = new InteractionResponse(TalkingTo.DisplayName, result.IsSuccess ? result.Message : ResidentView.Describe(result), InteractionKind.StateChanging, result);
            Hud?.ShowResponse(LastResponse);
            if (result.IsSuccess) TalkingTo.Animation?.Talk(4f);
            return result;
        }

        public CommandResult ReturnToWorldMap()
        {
            if (_session == null) return CommandResult.InvalidState("No world session is active.");
            RecordPose();
            var result = _session.Dispatch(new ExitSettlementCommand());
            if (!result.IsSuccess) Hud?.ShowMessage(result);
            return result;
        }

        public CommandResult SaveNow()
        {
            var result = _runtime.Save(); // the runtime's active slot, shared with the map view
            Hud?.ShowMessage(result);
            return result;
        }

        // ----- Frame loop --------------------------------------------------------------------

        private void Update()
        {
            if (!IsExploring || Player == null) return;
            HandleCursor();
            if (CameraRig != null) CameraRig.LookEnabled = !_cursorFree || !ManageCursor;

            var forward = CameraRig != null ? CameraRig.transform.forward : Player.transform.forward;
            CurrentTarget = _detector.FindTarget(Player.transform.position, forward);
            Hud?.SetPrompt(CurrentTarget);
            if (TalkingTo != null && !ReferenceEquals(CurrentTarget, TalkingTo))
            {
                TalkingTo = null; // walked away: the conversation ends
                Hud?.ShowTopics(null);
            }

            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (TalkingTo != null && keys != null)
            {
                for (var i = 0; i < Topics.Count && i < 9; i++)
                    if (keys[UnityEngine.InputSystem.Key.Digit1 + i].wasPressedThisFrame) AskResident(Topics[i].topic);
            }

            if (_input == null) return;
            if (_input.InteractPressed) Interact();
            if (_input.TogglePerspectivePressed) TogglePerspective();
            if (_input.SavePressed) SaveNow();
            if (_input.ReturnToMapPressed) ReturnToWorldMap();
        }

        private void HandleCursor()
        {
            if (!ManageCursor) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (!_cursorFree && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                SetCursorFree(true);
            }
            else if (_cursorFree && mouse != null && mouse.leftButton.wasPressedThisFrame && Hud != null && !Hud.IsPointerOverUi(mouse.position.ReadValue()))
            {
                SetCursorFree(false);
            }
        }

        private void SetCursorFree(bool free)
        {
            _cursorFree = free;
            if (!ManageCursor) return;
            Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = free;
        }

        private void RefreshHud()
        {
            if (Hud == null || _session == null || !_session.HasWorld) return;
            var player = _session.GetPlayer();
            if (player == null || !player.IsInSettlement) return;
            var settlement = _session.FindSettlement(player.SettlementId);
            var region = _session.FindRegion(player.RegionId);
            // The governing faction comes from the same world data the map shows; exploration keeps no political state.
            var politics = _session.GetSettlementPolitics(player.SettlementId);
            var authority = politics == null ? string.Empty : politics.IsIndependent ? " · independent" : " · governed by " + politics.GoverningFactionName;
            Hud.SetLocation(settlement?.Name ?? "-", (region != null ? region.Name : "-") + authority, _session.GetOverview().Date.ToString());
        }

        private void OnDestroy()
        {
            if (_runtime != null)
            {
                _runtime.PreparingSave -= OnPreparingSave;
                if (_runtime.Host != null)
                {
                    _runtime.Host.SessionStarted -= BindSession;
                    _runtime.Host.SessionEnded -= UnbindSession;
                }
            }

            UnbindSession();
            if (_ownsInput && _input is InputSystemExplorationInput owned) owned.Dispose();
            _atmosphere?.Restore(); // never leave street-level render settings (or the URP shadow distance) behind
            _atmosphere = null;
            if (ManageCursor)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
