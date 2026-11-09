using System;

namespace Realmkeeper.Application
{
    public enum SessionHostState
    {
        /// <summary>No world is loaded.</summary>
        Idle = 0,

        /// <summary>Exactly one world session is active.</summary>
        Active = 1,

        /// <summary>The host has been disposed and cannot start sessions any more.</summary>
        Disposed = 2,
    }

    /// <summary>
    /// The one place that owns the active <see cref="WorldSession"/> (ADR-0009 point 1). The strategic
    /// map and exploration both use the session this host exposes; neither creates its own.
    /// Engine-free and instance-based (no static singleton), so it is tested without Unity. A Unity
    /// bridge keeps one host alive across scene changes.
    ///
    /// Lifecycle: Idle --StartNew/StartFromSave--> Active --Shutdown--> Idle. Starting while Active
    /// is refused, never silently replacing the world. Shutdown closes the session, so stale
    /// references fail safely with <see cref="CommandStatus.InvalidSessionState"/>.
    /// </summary>
    public sealed class WorldSessionHost : IDisposable
    {
        private readonly Func<WorldSession> _createSession;
        private readonly Action<WorldSession> _configure;
        private WorldSession _session;

        /// <param name="createSession">Builds a new, empty session (the composition root).</param>
        /// <param name="configure">Registers module command handlers on each new session (for example exploration).</param>
        public WorldSessionHost(Func<WorldSession> createSession, Action<WorldSession> configure = null)
        {
            _createSession = createSession ?? throw new ArgumentNullException(nameof(createSession));
            _configure = configure;
        }

        public SessionHostState State { get; private set; }

        public bool IsActive => State == SessionHostState.Active;

        /// <summary>The active session. Throws when no session is active: callers check <see cref="IsActive"/> first.</summary>
        public WorldSession Session =>
            IsActive ? _session : throw new InvalidOperationException("No world session is active.");

        /// <summary>Raised after a session becomes active (new or loaded).</summary>
        public event Action<WorldSession> SessionStarted;

        /// <summary>Raised after the active session has been closed.</summary>
        public event Action SessionEnded;

        public CommandResult StartNew(CreateWorldCommand command)
        {
            if (command == null) return CommandResult.Invalid("A create-world command is required.");
            return Start(session => session.Execute(command));
        }

        public CommandResult StartFromSave(string slotName)
        {
            if (string.IsNullOrWhiteSpace(slotName)) return CommandResult.Invalid("A save slot name is required.");
            return Start(session => session.Execute(new LoadWorldCommand(slotName)));
        }

        public CommandResult Shutdown()
        {
            if (State == SessionHostState.Disposed) return CommandResult.InvalidState("The session host has been disposed.");
            if (!IsActive) return CommandResult.InvalidState("No world session is active.");
            var session = _session;
            _session = null;
            State = SessionHostState.Idle;
            session.Close();
            SessionEnded?.Invoke();
            return CommandResult.Ok("The world session was closed.");
        }

        public void Dispose()
        {
            if (State == SessionHostState.Disposed) return;
            if (IsActive) Shutdown();
            State = SessionHostState.Disposed;
            SessionStarted = null;
            SessionEnded = null;
        }

        private CommandResult Start(Func<WorldSession, CommandResult> open)
        {
            if (State == SessionHostState.Disposed) return CommandResult.InvalidState("The session host has been disposed.");
            if (IsActive) return CommandResult.InvalidState("A world session is already active; shut it down before starting another.");

            var session = _createSession() ?? throw new InvalidOperationException("The session factory returned null.");
            _configure?.Invoke(session);
            var result = open(session);
            if (!result.IsSuccess)
            {
                session.Close();
                return result;
            }

            _session = session;
            State = SessionHostState.Active;
            SessionStarted?.Invoke(session);
            return result;
        }
    }
}
