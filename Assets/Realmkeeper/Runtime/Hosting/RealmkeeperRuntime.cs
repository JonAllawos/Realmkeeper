using System;
using System.Collections.Generic;
using System.IO;
using Realmkeeper.Application;
using Realmkeeper.Infrastructure.Composition;
using UnityEngine;

namespace Realmkeeper.Hosting
{
    /// <summary>Which presentation is showing the world.</summary>
    public enum ViewMode
    {
        Strategic = 0,
        Exploration = 1,
    }

    /// <summary>A Unity-side module (for example exploration) that registers command handlers on every session.</summary>
    public interface IRuntimeModule
    {
        void ConfigureSession(WorldSession session);
    }

    /// <summary>
    /// Lifecycle bridge between Unity and the one authoritative world session (ADR-0009 point 1;
    /// prerequisite 2). It survives scene changes (<c>DontDestroyOnLoad</c>), owns the
    /// engine-free <see cref="WorldSessionHost"/> and the <see cref="SimulationPlayback"/>
    /// clock, and derives the view mode from the player character's location in world data.
    /// It holds no world state of its own. Presentations find it in the scene; there is no static
    /// instance field.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class RealmkeeperRuntime : MonoBehaviour
    {
        public const string SaveFolder = "Saves";

        private readonly List<IRuntimeModule> _modules = new List<IRuntimeModule>();
        private WorldSession _boundSession;

        public WorldSessionHost Host { get; private set; }

        /// <summary>The active session (throws if none: check <see cref="WorldSessionHost.IsActive"/>).</summary>
        public WorldSession Session => Host.Session;

        public SimulationPlayback Playback { get; private set; }

        public ViewMode Mode { get; private set; } = ViewMode.Strategic;

        /// <summary>Raised when the player character's location switches the view (old, new).</summary>
        public event Action<ViewMode, ViewMode> ModeChanged;

        /// <summary>Raised before a save so presentations can record transient state (such as the character's position) through commands.</summary>
        public event Action PreparingSave;

        /// <summary>Finds the runtime in the loaded scenes, or creates it. Never creates a second one.</summary>
        public static RealmkeeperRuntime FindOrCreate()
        {
            var existing = FindAnyObjectByType<RealmkeeperRuntime>();
            if (existing != null) return existing;
            var go = new GameObject("Realmkeeper Runtime");
            return go.AddComponent<RealmkeeperRuntime>();
        }

        private void Awake()
        {
            if (FindObjectsByType<RealmkeeperRuntime>(FindObjectsSortMode.None).Length > 1)
            {
                Debug.LogError("Realmkeeper: a second runtime was created; destroying it so only one world session exists.");
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
            var saves = Path.Combine(UnityEngine.Application.persistentDataPath, SaveFolder);
            Host = RealmkeeperComposition.CreateHost(saves, ConfigureSession);
            Host.SessionStarted += Bind;
            Host.SessionEnded += Unbind;
        }

        /// <summary>Starts a new world unless one is already active (returns the existing session's state in that case).</summary>
        public CommandResult EnsureWorld(string worldName, long seed)
        {
            if (Host.IsActive) return CommandResult.Ok("A world session is already active.");
            return Host.StartNew(new CreateWorldCommand(worldName, seed));
        }

        /// <summary>Registers a module; it configures the active session now and every later session.</summary>
        public void RegisterModule(IRuntimeModule module)
        {
            if (module == null || _modules.Contains(module)) return;
            _modules.Add(module);
            if (Host.IsActive) module.ConfigureSession(Host.Session);
        }

        public void UnregisterModule(IRuntimeModule module) => _modules.Remove(module);

        /// <summary>
        /// The save slot both views save to and load from. Starts as <see cref="SaveSlot.Default"/>
        /// ("autosave"), so behaviour from before slots existed is unchanged.
        /// </summary>
        public SaveSlot ActiveSlot { get; private set; } = SaveSlot.Default;

        /// <summary>Raised when <see cref="ActiveSlot"/> changes.</summary>
        public event Action<SaveSlot> ActiveSlotChanged;

        /// <summary>Chooses the slot later saves and loads use. An invalid name is refused and the active slot is kept.</summary>
        public CommandResult SelectSlot(string slotName)
        {
            if (!SaveSlot.TryCreate(slotName, out var slot, out var error)) return CommandResult.Invalid(error);
            if (slot == ActiveSlot) return CommandResult.Ok($"Save slot '{slot}' is already selected.");
            ActiveSlot = slot;
            ActiveSlotChanged?.Invoke(slot);
            return CommandResult.Ok($"Save slot '{slot}' selected.");
        }

        /// <summary>Saves to <see cref="ActiveSlot"/>.</summary>
        public CommandResult Save() => Save(ActiveSlot.Name);

        /// <summary>Saves through the command registry, after letting presentations record transient state.</summary>
        public CommandResult Save(string slot)
        {
            if (!Host.IsActive) return CommandResult.InvalidState("No world session is active.");
            var slotError = SaveSlot.Validate(slot);
            if (slotError != null) return CommandResult.Invalid(slotError);
            PreparingSave?.Invoke();
            return Host.Session.Dispatch(new SaveWorldCommand(slot));
        }

        /// <summary>Loads <see cref="ActiveSlot"/> into the active session (the session object stays the same).</summary>
        public CommandResult Load() => Load(ActiveSlot.Name);

        /// <summary>
        /// Loads a slot into the active session; on success that slot becomes active. On failure
        /// the current world, and the active slot, stay unchanged.
        /// </summary>
        public CommandResult Load(string slot)
        {
            if (!Host.IsActive) return CommandResult.InvalidState("No world session is active.");
            if (!SaveSlot.TryCreate(slot, out var parsed, out var error)) return CommandResult.Invalid(error);
            Playback?.Pause();
            var result = Host.Session.Execute(new LoadWorldCommand(parsed));
            if (result.IsSuccess && parsed != ActiveSlot)
            {
                ActiveSlot = parsed;
                ActiveSlotChanged?.Invoke(parsed);
            }

            return result;
        }

        private void ConfigureSession(WorldSession session)
        {
            foreach (var module in _modules) module.ConfigureSession(session);
        }

        private void Bind(WorldSession session)
        {
            _boundSession = session;
            Playback = new SimulationPlayback(session);
            session.WorldChanged += OnWorldChanged;
            UpdateMode();
        }

        private void Unbind()
        {
            if (_boundSession != null) _boundSession.WorldChanged -= OnWorldChanged;
            _boundSession = null;
            Playback?.Pause();
            Playback = null;
            SetMode(ViewMode.Strategic);
        }

        private void OnWorldChanged(WorldChange change)
        {
            if (change.Reason == WorldChangeReason.Closed) return;
            UpdateMode();
        }

        private void UpdateMode()
        {
            var player = Host.IsActive ? Host.Session.GetPlayer() : null;
            SetMode(player != null && (player.IsInSettlement || player.LocationKind == Realmkeeper.Domain.World.CharacterLocationKind.Place) ? ViewMode.Exploration : ViewMode.Strategic); // Phase 13: a place is explored too
        }

        private void SetMode(ViewMode mode)
        {
            if (mode == Mode) return;
            var old = Mode;
            Mode = mode;
            ModeChanged?.Invoke(old, mode);
        }

        /// <summary>The clock runs in every view mode (ADR-0009 point 4); it never depends on the camera.</summary>
        private void Update()
        {
            Playback?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            Host?.Dispose();
        }
    }
}
