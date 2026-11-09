using System;

namespace Realmkeeper.Application
{
    /// <summary>
    /// One listener's subscription to <see cref="WorldSession.WorldChanged"/>, rebindable to a
    /// newer session. Binding to a new session first removes the old subscription, binding to
    /// the same session again does nothing, so a listener is subscribed at most once to at most
    /// one session. When its session closes, the binding drops the reference, so presentation
    /// never keeps a closed session alive. Engine-free, used by every presentation component.
    /// </summary>
    public sealed class WorldSessionBinding : IDisposable
    {
        private readonly Action<WorldChange> _handler;
        private readonly Action<WorldChange> _relay;

        /// <param name="handler">Called for every change of the bound session (including its <see cref="WorldChangeReason.Closed"/> change).</param>
        public WorldSessionBinding(Action<WorldChange> handler)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _relay = Relay;
        }

        /// <summary>The bound session, or null when unbound or after it closed.</summary>
        public WorldSession Session { get; private set; }

        public bool IsBound => Session != null;

        /// <summary>Binds to <paramref name="session"/> (null or closed: unbinds). Returns true when the bound session changed.</summary>
        public bool Bind(WorldSession session)
        {
            if (session != null && session.IsClosed) session = null;
            if (ReferenceEquals(session, Session)) return false;
            Unbind();
            Session = session;
            if (session != null) session.WorldChanged += _relay;
            return true;
        }

        public void Unbind()
        {
            if (Session != null) Session.WorldChanged -= _relay;
            Session = null;
        }

        public void Dispose() => Unbind();

        private void Relay(WorldChange change)
        {
            if (change.Reason == WorldChangeReason.Closed)
            {
                Session = null; // the closed session cleared its own subscribers
            }

            _handler(change);
        }
    }
}
