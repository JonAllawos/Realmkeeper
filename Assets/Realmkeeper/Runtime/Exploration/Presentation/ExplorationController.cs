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
    public sealed partial class ExplorationController : MonoBehaviour
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
        private Transform _residentsRoot;
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

        /// <summary>Phase 17 (ADR-0086): the settlement whose open ground stages a fight on the road, while one is loaded (else null).</summary>
        public SettlementId? RoadStage { get; private set; }

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
            if (!change.GeographyChanged) RefreshLiving(snap: false);
        }

        /// <summary>Makes the presentation match world data: environment loaded if and only if the character is in a settlement.</summary>
        private void Sync(bool forceReload)
        {
            var player = _session != null && _session.HasWorld ? _session.GetPlayer() : null;
            if (player != null && player.LocationKind == CharacterLocationKind.Place)
            {
                SyncPlace(player, forceReload); // Phase 13: a place, not a settlement
                return;
            }

            if (InPlace) UnloadEnvironment(); // left a place: the settlement (or the map) is rebuilt

            // Phase 17 (ADR-0086): a fight on the road is staged on the open ground at the road's nearer end. The player
            // stays on the road in the world; the scene is only where the existing combat is shown and played.
            var road = player != null && !player.IsInSettlement ? _session.GetRoadFight() : null;
            if (road != null)
            {
                if (forceReload || !_streamer.IsLoaded || RoadStage != road.Ground)
                {
                    UnloadEnvironment();
                    LoadEnvironment(player, road);
                }

                RefreshCombat();
                RefreshHud();
                return;
            }

            if (player == null || !player.IsInSettlement)
            {
                UnloadEnvironment();
                return;
            }

            if (RoadStage != null) UnloadEnvironment(); // back from the road's fight into a settlement

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

        private void LoadEnvironment(PlayerSummary player, RoadFightView road = null)
        {
            var settlement = _session.FindSettlement(road?.Ground ?? player.SettlementId);
            if (settlement == null)
            {
                Debug.LogError($"Realmkeeper: the character is in settlement {player.SettlementId}, which does not exist.");
                return;
            }

            var layout = ExplorationModule.LayoutFor(_session, settlement);
            if (road != null) layout = RoadsideGround(layout); // Phase 17: open country, not the town
            RoadStage = road?.Ground;
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

            Player = PlayerCharacter.Create(null, _materials.Get(new Color(0.24f, 0.36f, 0.52f)), _materials.Get(new Color(0.55f, 0.42f, 0.30f)), Catalog,
                Catalog != null ? CurrentLook() : null, Catalog != null ? Assets() : null);
            Player.Input = _input;
            Player.Teleport(road != null ? layout.ArrivalPose : player.Pose);
            CameraRig = ExplorationCameraRig.Create(null, Player);
            CameraRig.Input = _input;
            if (Catalog != null) ExplorationAtmosphere.ConfigureCamera(CameraRig.Camera);
            CameraRig.SetPerspective(player.Perspective);
            CameraRig.SetView(player.Pose.Yaw, 12f);
            CameraRig.Snap();

            _detector.Clear();
            var residents = new GameObject("Residents").transform;
            _residentsRoot = residents;
            foreach (var spot in layout.Residents)
            {
                if (_residents.ContainsKey(spot.NpcId)) continue; // one body per persistent identity
                var npc = _session.FindNpc(spot.NpcId);
                if (npc == null) continue;
                var view = ResidentView.Create(residents, spot, npc, _session, Player.transform, _materials, Catalog, layout);
                _residents.Add(spot.NpcId, view);
                _detector.Register(view);
            }

            var points = new GameObject("Interaction points").transform;
            foreach (var spec in layout.Interactables)
            {
                var view = EnvironmentInteractable.Create(points, spec, settlement.SettlementId, _materials, LocalPaths.StandingHeight(layout, spec.Position.X, spec.Position.Z));
                _interactables.Add(view);
                _detector.Register(view);
            }

            SetupBuildingDoors(layout); // Phase 13: doors into buildings with interiors

            Hud = ExplorationHud.Create(null);
            Hud.ReturnRequested += OnReturnRequested;
            Hud.SaveRequested += OnSaveRequested;
            Hud.SetPerspective(player.Perspective);
            SetupLiving(layout);

            _input?.Enable();
            SetCursorFree(false);
            if (road != null) Hud.ShowMessage(CommandResult.Ok($"A fight on the road near {settlement.Name}."));
            Debug.Log(road != null ? $"Realmkeeper: a fight on the road, staged on the open ground by {settlement.Name}."
                : $"Realmkeeper: entered {settlement.Name} ({layout.EnvironmentKey}): {layout.Buildings.Count} buildings, " +
                  $"{_residents.Count} residents, {layout.Props.Count} props; character at {player.Pose}.");
        }

        /// <summary>
        /// Phase 17 (ADR-0086): the ground a fight on the road is staged on — the settlement's land and terrain at the
        /// road's end, without its buildings, people, herds, doors or interactions (it is open country, not the town).
        /// The fight itself is the engine's: positions, cover and terrain come from the encounter, not from this scene.
        /// </summary>
        public static LocalEnvironmentLayout RoadsideGround(LocalEnvironmentLayout town)
        {
            town.Buildings.Clear();
            town.Props.Clear();
            town.Interactables.Clear();
            town.Residents.Clear();
            town.Herds.Clear();
            town.Streets.Clear();
            town.HasWalls = false;
            if (town.Plan != null) // the town's plan is shared: a copy without its walls (open country)
            {
                var plan = town.Plan.Clone();
                plan.HasWalls = false;
                plan.Wall.Clear();
                town.Plan = plan;
            }

            return town;
        }

        private void UnloadEnvironment()
        {
            if (!_streamer.IsLoaded) return;
            TeardownPlace(); // Phase 13
            TeardownLiving();
            ClearCompanions(); // Phase 15
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
            RoadStage = null;
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
            if (RoadStage != null) return CommandResult.Ok(); // Phase 17: on the road the party has no place in a settlement to record
            if (InPlace) return _placeScene.PlayerRoom == null ? CommandResult.Ok() : _session.Dispatch(new Realmkeeper.Application.Places.MoveInPlaceCommand(_placeScene.PlayerRoom, Player.CurrentPose)); // Phase 13
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
            if (Player != null) Player.GetComponentInChildren<CharacterAnimationDriver>()?.Interact(); // Phase 15: reaching out to what is used
            if (CurrentTarget is PlaceInteractable placed)
            {
                // Phase 13: places and building doors answer for themselves (choices stay on screen).
                LastResponse = placed.Interact(_session, Player.transform);
                if (LastResponse != null && !string.IsNullOrEmpty(LastResponse.Text)) Hud?.ShowResponse(LastResponse);
                RefreshHud();
                return LastResponse;
            }

            if (CurrentTarget is CompanionFollower companion) // Phase 15: a companion is talked to in full conversation
            {
                var talk = StartDialogue(companion.NpcId);
                companion.Animation?.Talk(3f);
                LastResponse = new InteractionResponse(companion.DisplayName, talk.IsSuccess ? string.Empty : talk.Message, companion.Kind, talk);
                return LastResponse;
            }

            LastResponse = CurrentTarget.Interact(_session, Player.transform);
            if (CurrentTarget is ContainerInteractable box && LastResponse != null && LastResponse.Succeeded)
            {
                OpenStorage(box.Key); // Phase 11: the chest opens in the hub's Storage section
                return LastResponse;
            }

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
            (ConversationTopic.Work, "Your work"),
            (ConversationTopic.Shops, "Where to buy"),
            (ConversationTopic.Goods, "What's for sale"),
            (ConversationTopic.Prices, "Prices"),
            (ConversationTopic.Factions, "The factions"),
        };

        private IReadOnlyList<(string, Action)> TopicButtons()
        {
            var list = new List<(string, Action)>();
            if (TalkingTo != null) { var who = TalkingTo.NpcId; list.Add(("Talk…", () => StartDialogue(who))); } // Phase 12: the full conversation
            foreach (var (topic, label) in Topics)
            {
                var t = topic;
                list.Add((label, () => AskResident(t)));
            }

            // Phase 5: a shopkeeper can be asked to trade (the window opens on their shop here).
            var kept = TalkingTo != null ? _session?.GetShopKeptBy(TalkingTo.NpcId) : null;
            if (kept != null && _streamer.Current != null && kept.SettlementId == _streamer.Current.SettlementId)
            {
                var id = kept.ShopId;
                list.Add(("Trade", () => OpenTrade(id)));
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
            if (InPlace) return LeavePlace(); // Phase 13: only by the way out
            if (RoadStage != null) return LeaveRoadFight(); // Phase 17: back to the road (the journey halted where it was)
            RecordPose();
            var result = _session.Dispatch(new ExitSettlementCommand());
            if (!result.IsSuccess) Hud?.ShowMessage(result);
            return result;
        }

        /// <summary>Phase 17 (ADR-0086): leaves the ground of a finished fight on the road; refused (with the reason) while it goes on.</summary>
        public CommandResult LeaveRoadFight()
        {
            if (_session == null) return CommandResult.InvalidState("No world session is active.");
            var result = _session.Dispatch(new Realmkeeper.Application.Atlas.LeaveRoadFightCommand());
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

            UpdateLivingInput();
            UpdateCombat(); // Phase 8
            UpdateParty(); // Phase 15: companions walk with the player
            UpdateExchanges(); // Phase 18: people talking to each other
            if (InPlace) UpdatePlace(Keyboard.current); // Phase 13
            if (WindowOpen)
            {
                Hud?.SetPrompt(null);
                return;
            }

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
            else if (_cursorFree && !InFight && mouse != null && mouse.leftButton.wasPressedThisFrame && Hud != null && !Hud.IsPointerOverUi(mouse.position.ReadValue())) // Combat Overhaul: in a fight a click on the ground is a move
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
            if (InPlace) { RefreshPlaceHud(); RefreshConditionsHud(); return; } // Phase 13; Phase 14: conditions, followers, rest
            if (Hud == null || _session == null || !_session.HasWorld) return;
            var player = _session.GetPlayer();
            if (player != null && RoadStage != null) // Phase 17: where the party is, on the road
            {
                var ground = _session.FindSettlement(RoadStage.Value);
                Hud.SetLocation("On the road", "a fight near " + (ground?.Name ?? "a town") + " · the party stays on the road", _session.GetOverview().Date.ToString());
                SyncParty();
                return;
            }

            if (player == null || !player.IsInSettlement) return;
            var settlement = _session.FindSettlement(player.SettlementId);
            var region = _session.FindRegion(player.RegionId);
            // The governing faction comes from the same world data the map shows; exploration keeps no political state.
            var politics = _session.GetSettlementPolitics(player.SettlementId);
            var authority = politics == null ? string.Empty : politics.IsIndependent ? " · independent" : " · governed by " + politics.GoverningFactionName;
            Hud.SetLocation(settlement?.Name ?? "-", (region != null ? region.Name : "-") + authority, _session.GetOverview().Date.ToString());
            RefreshConditionsHud(); // Phase 14
            SyncParty(); // Phase 15
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
