using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.Rpg;
using Realmkeeper.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Phase 25: the exploration side of the interface overhaul.
    /// <list type="bullet">
    /// <item><b>The pause menu (Esc).</b> Every entry runs an existing action: Resume, Save, Settings, Return to the world
    /// map, Main menu, Exit to desktop. Leaving asks first, and says that unsaved progress would be lost.</item>
    /// <item><b>Notifications.</b> Derived twice a second from the world as the player knows it (<see cref="PlayerNotices"/>).</item>
    /// <item><b>The vitals panel.</b> Hit points, armour, hunger and lasting conditions, refreshed once a second.</item>
    /// <item><b>The HUD stepping aside</b> while a window is open.</item>
    /// </list>
    /// Nothing here changes the world except through the commands the entries call.
    /// </summary>
    public sealed partial class ExplorationController
    {
        public const float NoticeSeconds = 0.5f;
        public const float VitalsSeconds = 1f;

        public RkMenuOverlay Pause { get; private set; }
        public bool PauseOpen => Pause != null && Pause.IsOpen;
        public RkSettingsWindow PauseSettings { get; private set; }

        /// <summary>Notices shown since the controller started (tests and the audit).</summary>
        public int NoticesShown { get; private set; }

        /// <summary>The last notices raised (newest last), for tests and the capture log.</summary>
        public List<PlayerNotice> LastNotices { get; } = new List<PlayerNotice>();

        /// <summary>Milliseconds the last notice check took (taking the snapshot and comparing).</summary>
        public double LastNoticeMs { get; private set; }

        private NoticeSnapshot _noticeBefore;
        private float _noticeTimer;
        private float _vitalsTimer;
        private bool _pauseWasPlaying;
        private int _pauseOpenedFrame = -1;

        /// <summary>Phase 25: opens the pause menu (Esc). The world's clock stops while it is open.</summary>
        public void OpenPauseMenu()
        {
            if (PauseOpen || Hud?.Root == null) return;
            var playback = _runtime?.Playback;
            _pauseWasPlaying = playback != null && playback.IsPlaying;
            playback?.Pause();
            SetCursorFree(true);
            if (Player != null) { _movementBeforePause = Player.MovementEnabled; Player.MovementEnabled = false; }
            var host = Hud.Root.panel?.visualTree ?? Hud.Root;
            var where = InOverland ? "In the country" : InPlace ? "In a place" : RoadStage != null ? "On the road" : "In a settlement";
            var canLeave = !InFight;
            var entries = new List<RkMenuOverlay.Entry>
            {
                new RkMenuOverlay.Entry { Name = "resume", Text = RkText.Get("pause.resume"), Icon = "play", Act = ClosePauseMenu, Primary = true },
                new RkMenuOverlay.Entry { Name = "save", Text = RkText.Get("pause.save"), Icon = "save", Act = PauseSave },
                new RkMenuOverlay.Entry { Name = "settings", Text = RkText.Get("pause.settings"), Icon = "settings", Act = PauseOpenSettings },
                new RkMenuOverlay.Entry
                {
                    Name = "map", Text = RkText.Get("pause.map"), Icon = "map", Act = () => { ClosePauseMenu(); ReturnToWorldMap(); },
                    Enabled = canLeave, Hint = canLeave ? null : RkText.Get("pause.notinfight"),
                },
                new RkMenuOverlay.Entry
                {
                    Name = "main", Text = RkText.Get("pause.main"), Icon = "book", Act = PauseMainMenu,
                    Enabled = canLeave && RkHooks.OpenMainMenu != null, Hint = !canLeave ? "Not during a fight." : RkHooks.OpenMainMenu == null ? RkText.Get("pause.nomainmenu") : null,
                },
                new RkMenuOverlay.Entry { Name = "exit", Text = RkText.Get("pause.exit"), Icon = "exit", Act = PauseExit, Danger = true },
            };
            Pause = new RkMenuOverlay(host, RkText.Get("pause.title"), RkText.Format("pause.clock", where), entries, Hud.Toasts?.History);
            _pauseOpenedFrame = Time.frameCount;
            Hud.SetWindowOpen(true);
        }

        public void ClosePauseMenu()
        {
            if (Pause == null) return;
            PauseSettings?.Close();
            Pause.Close();
            Pause = null;
            _pauseClosedFrame = Time.frameCount;
            if (Player != null) Player.MovementEnabled = _movementBeforePause;
            if (_pauseWasPlaying) _runtime?.Playback?.Play();
            _pauseWasPlaying = false;
            Hud?.SetWindowOpen(WindowOpen);
        }

        private void PauseSave()
        {
            var result = SaveNow();
            Hud?.Toasts?.Push("saved:" + Time.realtimeSinceStartup, result.IsSuccess ? "saved" : "error", result.IsSuccess ? "Saved" : "Not saved", result.Message);
            ClosePauseMenu();
        }

        private void PauseOpenSettings()
        {
            var host = Pause?.Root;
            if (host == null) return;
            PauseSettings = new RkSettingsWindow(host, () => Pause?.Buttons[2]?.Focus());
        }

        private void PauseMainMenu()
        {
            var host = Pause?.Root;
            if (host == null) return;
            RkModal.Confirm(host, RkText.Get("pause.mainconfirm.title"), RkText.Get("pause.mainconfirm.body"),
                RkText.Get("pause.main"), () =>
                {
                    ClosePauseMenu();
                    ReturnToWorldMap();
                    RkHooks.OpenMainMenu?.Invoke();
                });
        }

        private void PauseExit()
        {
            var host = Pause?.Root;
            if (host == null) return;
            RkModal.Confirm(host, RkText.Get("pause.exitconfirm.title"), RkText.Get("pause.exitconfirm.body"),
                RkText.Get("pause.exit"), () =>
                {
                    if (UnityEngine.Application.isEditor) { Hud?.Toasts?.Push("exit-editor", "notify", "Exit closes the built game", "In the editor, stop Play mode instead."); return; }
                    UnityEngine.Application.Quit();
                }, true);
        }

        private int _pauseClosedFrame = -10;
        private bool _movementBeforePause = true;

        /// <summary>
        /// Esc opens the pause menu when nothing else wants it: no window, conversation or fight is open (those use Esc
        /// to close or cancel). Returns true when it opened the menu this frame.
        /// </summary>
        private bool HandlePauseKey()
        {
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (keys == null || !keys.escapeKey.wasPressedThisFrame) return false;
            if (Time.frameCount - _pauseClosedFrame <= 1) return false; // the Esc that closed it is not a new request
            if (WindowOpen || InFight || Typing()) return false;
            OpenPauseMenu();
            return PauseOpen;
        }

        /// <summary>While the menu is open: Esc resumes, unless a dialog inside it (settings, a confirmation) has it.</summary>
        private void UpdatePauseKeys()
        {
            var keys = UnityEngine.InputSystem.Keyboard.current;
            if (keys == null || !keys.escapeKey.wasPressedThisFrame || Time.frameCount == _pauseOpenedFrame) return;
            if (PauseSettings != null && PauseSettings.IsOpen) return;
            if (Pause?.Root.Q<VisualElement>("rk-modal") != null) return;
            ClosePauseMenu();
        }

        // ----- Notices and vitals ---------------------------------------------------------------------------------

        /// <summary>Called every frame while exploring: steps the notice and vitals timers and keeps the HUD out of windows.</summary>
        private void UpdatePhase25()
        {
            if (Hud == null || _session == null || !_session.HasWorld) return;
            Hud.SetWindowOpen(WindowOpen || PauseOpen);
            _noticeTimer -= Time.unscaledDeltaTime;
            if (_noticeTimer <= 0f)
            {
                _noticeTimer = NoticeSeconds;
                CheckNotices();
            }

            _vitalsTimer -= Time.unscaledDeltaTime;
            if (_vitalsTimer <= 0f)
            {
                _vitalsTimer = VitalsSeconds;
                RefreshVitals();
            }
        }

        /// <summary>Compares the world as the player knows it with the last look, and shows what changed.</summary>
        public void CheckNotices()
        {
            if (_session == null || !_session.HasWorld || Hud?.Toasts == null) return;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var now = _session.GetNoticeSnapshot();
            if (_noticeBefore != null)
            {
                _session.FillNoticeEvents(_noticeBefore, now);
                foreach (var n in PlayerNotices.Compare(_noticeBefore, now))
                {
                    Hud.Toasts.Push(n.Key, n.Kind, n.Title, n.Body, n.Important);
                    NoticesShown++;
                    LastNotices.Add(n);
                    if (LastNotices.Count > 40) LastNotices.RemoveAt(0);
                }
            }

            _noticeBefore = now;
            LastNoticeMs = watch.Elapsed.TotalMilliseconds;
        }

        /// <summary>Forgets the last look (a new world or a load): what is already known is not announced again.</summary>
        public void ResetNotices()
        {
            _noticeBefore = null;
            _noticeTimer = 0f;
            Hud?.Toasts?.Clear();
        }

        public void RefreshVitals()
        {
            if (Hud == null || _session == null || !_session.HasWorld) return;
            var ch = _session.GetCharacter();
            if (ch == null || ch.NeedsCreation)
            {
                Hud.SetVitals(ch?.Name, 0, 0, ch == null ? null : "Create your character (C) to see your health here.", null);
                return;
            }

            var p = ch.Player;
            var max = ch.Rules.MaxHp;
            var hp = p.Rpg.CurrentHp;
            var conditions = new List<(string, string)>();
            if (ch.IsDead) conditions.Add(("Dead", "bad"));
            else if (ch.IsDying) conditions.Add(("Dying", "bad"));
            else if (ch.IsStable && hp == 0) conditions.Add(("Stable, unconscious", "warn"));
            var fed = PlayerMovement.FedText(p.Fed);
            if (p.Fed < PlayerMovement.HungryBelow) conditions.Add((fed, "warn"));
            var time = _session.GetWorldTime();
            var nowMinute = time == null ? 0L : time.Date.ToDayNumber() * 1440L + time.MinuteOfDay;
            foreach (var c in LastingConditions.Active(p.Rpg, nowMinute))
                conditions.Add((c.Type == Realmkeeper.Domain.Combat.ConditionType.Exhaustion ? "Exhausted " + c.Level : c.Type.ToString(), "bad"));
            if (ch.CanAdvance) conditions.Add(("Level up ready (L)", "info"));
            var line = $"{hp} / {max} HP · AC {ch.ArmorClass.Total} · {fed} · level {ch.Level}";
            Hud.SetVitals(ch.FullName, hp, max, line, conditions);
        }
    }
}
