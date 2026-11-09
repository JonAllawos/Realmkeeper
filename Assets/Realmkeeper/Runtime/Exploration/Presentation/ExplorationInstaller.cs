using Realmkeeper.Application;
using Realmkeeper.Exploration.Application;
using Realmkeeper.Hosting;
using UnityEngine;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Scene composition for exploration: registers the exploration command handlers on the one
    /// world session (now and on every later session) and creates the
    /// <see cref="ExplorationController"/>. Runs before the strategic bootstrap so the handlers
    /// exist before the world is created or loaded.
    /// </summary>
    [DefaultExecutionOrder(-150)]
    public sealed class ExplorationInstaller : MonoBehaviour, IRuntimeModule
    {
        public RealmkeeperRuntime Runtime { get; private set; }
        public ExplorationController Controller { get; private set; }

        private void Awake()
        {
            Runtime = RealmkeeperRuntime.FindOrCreate();
            Runtime.RegisterModule(this);

            var go = new GameObject("Exploration");
            go.transform.SetParent(transform, false);
            Controller = go.AddComponent<ExplorationController>();
            Controller.ManageCursor = !UnityEngine.Application.isBatchMode;
            Controller.Initialize(Runtime);
        }

        /// <summary>Registers the exploration handlers once per session.</summary>
        public void ConfigureSession(WorldSession session)
        {
            if (!session.Commands.IsRegistered<EnterSettlementCommand>()) ExplorationModule.Register(session);
        }

        private void OnDestroy()
        {
            if (Runtime != null) Runtime.UnregisterModule(this);
        }
    }
}
