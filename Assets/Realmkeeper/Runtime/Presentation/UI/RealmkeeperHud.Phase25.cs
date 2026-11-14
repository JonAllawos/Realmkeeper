using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.UI;
using UnityEngine.UIElements;

namespace Realmkeeper.Presentation
{
    /// <summary>
    /// Phase 25: the strategic map's share of the interface overhaul. It gains:
    /// <list type="bullet">
    /// <item>accessibility and width classes (<see cref="RkRoot"/>);</item>
    /// <item>interface sounds;</item>
    /// <item>notification toasts from the same rule as exploration (<see cref="PlayerNotices"/>), checked once a second
    /// while the map is shown, so days passing on the map announce what the player learns.</item>
    /// </list>
    /// A new session starts from a fresh look, so a loaded world is never announced as news.
    /// </summary>
    public sealed partial class RealmkeeperHud
    {
        public RkToasts Toasts { get; private set; }

        /// <summary>Notices shown on the map (tests).</summary>
        public int NoticesShown { get; private set; }

        public List<PlayerNotice> LastNotices { get; } = new List<PlayerNotice>();

        private NoticeSnapshot _noticeBefore;
        private WorldSession _noticeSession;

        private void DressPhase25()
        {
            var root = _document.rootVisualElement;
            RkRoot.Attach(root);
            RkUiSound.Attach(root);
            Toasts = new RkToasts(Root);
            Toasts.Stack.style.top = 70;
            Root.schedule.Execute(CheckNotices).Every(1000);
        }

        /// <summary>Compares the world as the player knows it with the last look, and shows what changed (map only).</summary>
        public void CheckNotices()
        {
            var s = _session;
            if (s == null || !s.HasWorld || Toasts == null) { _noticeBefore = null; _noticeSession = null; return; }
            if (!ReferenceEquals(s, _noticeSession)) { _noticeSession = s; _noticeBefore = null; Toasts.Clear(); }
            var now = s.GetNoticeSnapshot();
            if (_noticeBefore != null && IsVisible)
            {
                s.FillNoticeEvents(_noticeBefore, now);
                foreach (var n in PlayerNotices.Compare(_noticeBefore, now))
                {
                    Toasts.Push(n.Key, n.Kind, n.Title, n.Body, n.Important);
                    NoticesShown++;
                    LastNotices.Add(n);
                    if (LastNotices.Count > 40) LastNotices.RemoveAt(0);
                }
            }

            _noticeBefore = now; // while exploring, the exploration HUD announces; the map takes a fresh look meanwhile
        }
    }
}
