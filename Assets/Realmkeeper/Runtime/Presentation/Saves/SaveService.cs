using System.Diagnostics;
using Realmkeeper.Application;
using Realmkeeper.Infrastructure.Persistence;
using Realmkeeper.UI;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// M1.1 (ADR-0123): autosaves and thumbnails. Autosaves go to the rotation (auto-1 … auto-3, the oldest replaced):
    /// <list type="bullet">
    /// <item>right after a loading transition completes, still behind the loading screen (<see cref="RkHooks.TransitionArrived"/>);</item>
    /// <item>after a campaign decision or chapter change is recorded (the campaign fingerprint changes);</item>
    /// <item>on Exit Game and Return to Title, after their confirmation (<see cref="RkHooks.BeforeLeavingGame"/>).</item>
    /// </list>
    /// Never during a fight, an open conversation, an unfinished transition or a command, nor within 60 s of any save
    /// (<see cref="AutosavePolicy"/>). A story trigger refused for a passing reason waits and is written when allowed.
    /// Every successful save, manual or auto, gets a thumbnail. The time each autosave takes is logged.
    /// </summary>
    public sealed class SaveService : MonoBehaviour
    {
        private RealmkeeperBootstrap _bootstrap;
        private JsonWorldStore _store;
        private WorldSession _session;
        private string _campaignSignature = string.Empty;

        /// <summary>A trigger waiting for an allowed moment ("story"), or null.</summary>
        public string Pending { get; private set; }

        public int Autosaves { get; private set; }
        public string LastAutosaveSlot { get; private set; }
        public string LastAutosaveWhy { get; private set; }
        public double LastAutosaveMs { get; private set; }

        /// <summary>Why the last autosave trigger was not written, or null.</summary>
        public string LastSkipReason { get; private set; }

        public int ThumbnailsWritten { get; private set; }

        /// <summary>Tests switch thumbnails off to keep captures out of timing.</summary>
        public bool Thumbnails { get; set; } = true;

        public JsonWorldStore Store => _store;

        public static SaveService Create(RealmkeeperBootstrap bootstrap)
        {
            var s = bootstrap.gameObject.AddComponent<SaveService>();
            s._bootstrap = bootstrap;
            s._store = new JsonWorldStore(bootstrap.Runtime.SaveRoot);
            s.Subscribe();
            return s;
        }

        private bool _subscribed;

        private void Subscribe()
        {
            if (_subscribed || _bootstrap == null || _bootstrap.Runtime == null) return;
            _subscribed = true;
            RkHooks.TransitionArrived += OnArrived;
            RkHooks.BeforeLeavingGame += OnLeaving;
            var runtime = _bootstrap.Runtime;
            runtime.Saved += OnSaved;
            runtime.Host.SessionStarted += Bind;
            runtime.Host.SessionEnded += Unbind;
            if (runtime.Host.IsActive) Bind(runtime.Session);
        }

        private void OnDestroy()
        {
            RkHooks.TransitionArrived -= OnArrived;
            RkHooks.BeforeLeavingGame -= OnLeaving;
            var runtime = _bootstrap != null ? _bootstrap.Runtime : null;
            if (runtime != null)
            {
                runtime.Saved -= OnSaved;
                if (runtime.Host != null)
                {
                    runtime.Host.SessionStarted -= Bind;
                    runtime.Host.SessionEnded -= Unbind;
                }
            }

            Unbind();
        }

        private void Bind(WorldSession session)
        {
            if (_session == session) return;
            Unbind();
            _session = session;
            if (_session == null) return;
            _session.WorldChanged += OnWorldChanged;
            _campaignSignature = _session.HasWorld ? _session.GetCampaignProgressSignature() : string.Empty;
            Pending = null;
        }

        private void Unbind()
        {
            if (_session != null) _session.WorldChanged -= OnWorldChanged;
            _session = null;
            Pending = null;
        }

        private void OnWorldChanged(WorldChange change)
        {
            if (change.Reason == WorldChangeReason.Closed || _session == null || !_session.HasWorld) return;
            var signature = _session.GetCampaignProgressSignature();
            var story = signature != _campaignSignature && change.Reason != WorldChangeReason.Loaded && change.Reason != WorldChangeReason.Created;
            _campaignSignature = signature;
            if (story) Pending = "story";
        }

        private void Update()
        {
            if (Pending == null) return;
            var c = Conditions();
            if (AutosavePolicy.Check(c) == null) Autosave(Pending);
            else if (!AutosavePolicy.IsTransient(c)) Pending = null;
        }

        private void OnArrived(string destination) => TryAutosave("arrival: " + destination);

        private void OnLeaving(string why) => TryAutosave(why);

        /// <summary>Writes an autosave now if <see cref="AutosavePolicy"/> allows it. Returns true when one was written.</summary>
        public bool TryAutosave(string why)
        {
            var reason = AutosavePolicy.Check(Conditions());
            if (reason != null)
            {
                LastSkipReason = reason;
                Debug.Log($"Realmkeeper: autosave ({why}) skipped: {reason}");
                return false;
            }

            return Autosave(why);
        }

        public AutosaveConditions Conditions()
        {
            var runtime = _bootstrap != null ? _bootstrap.Runtime : null;
            var has = _session != null && _session.HasWorld && runtime != null && runtime.Host.IsActive;
            return new AutosaveConditions
            {
                Enabled = RkSettings.Autosave,
                HasWorld = has,
                InFight = has && _session.IsInFight(),
                InConversation = has && _session.IsInConversation(),
                InTransition = RkHooks.TransitionInProgress,
                CommandApplying = has && _session.IsApplyingCommand,
                SecondsSinceLastSave = runtime != null ? runtime.SecondsSinceLastSave : null,
            };
        }

        private bool Autosave(string why)
        {
            var slot = SaveSlots.NextAutosave(s => _store.Exists(s), s => _store.Inspect(s, false)?.LastPlayedUtc);
            var watch = Stopwatch.StartNew();
            var result = _bootstrap.Runtime.Save(slot);
            watch.Stop();
            LastAutosaveMs = watch.Elapsed.TotalMilliseconds;
            if (!result.IsSuccess)
            {
                LastSkipReason = result.Message;
                Debug.LogWarning($"Realmkeeper: autosave ({why}) to '{slot}' failed after {LastAutosaveMs:0} ms: {result.Message} {string.Join(" ", result.Errors)}");
                Pending = null;
                return false;
            }

            Pending = null;
            Autosaves++;
            LastAutosaveSlot = slot;
            LastAutosaveWhy = why;
            LastSkipReason = null;
            Debug.Log($"Realmkeeper: autosave ({why}) to '{slot}' in {LastAutosaveMs:0} ms (editor timing; save and thumbnail).");
            return true;
        }

        private void OnSaved(string slot, CommandResult result)
        {
            if (!result.IsSuccess || !Thumbnails) return;
            var jpeg = SaveThumbnail.Capture();
            if (jpeg != null && _store.WriteThumbnail(slot, jpeg)) ThumbnailsWritten++;
        }
    }
}
