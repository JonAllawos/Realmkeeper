using System;
using System.Collections.Generic;
using Realmkeeper.Application;
using Realmkeeper.Domain.World;
using Realmkeeper.Exploration.Interaction;
using UnityEngine;
using UnityEngine.UIElements;

namespace Realmkeeper.Exploration.Presentation
{
    /// <summary>
    /// Minimal exploration HUD (UI Toolkit, same style as the strategic HUD): where the
    /// character is, the camera perspective, the interaction prompt, the last interaction's
    /// response (marked informational or state-changing), Save and Return to World Map.
    /// </summary>
    public sealed class ExplorationHud : MonoBehaviour
    {
        public const string LayoutResource = "ExplorationHud";
        public const string PanelSettingsResource = "RealmkeeperPanelSettings";
        public const float ResponseSeconds = 9f;

        private UIDocument _document;
        private float _responseTimer;

        public VisualElement Root { get; private set; }

        /// <summary>Phase 25: the document's root, which carries the accessibility and width classes.</summary>
        public VisualElement DocumentRoot => _document != null ? _document.rootVisualElement : null;
        public Label SettlementLabel { get; private set; }
        public Label RegionLabel { get; private set; }
        public Label DateLabel { get; private set; }
        public Label PerspectiveLabel { get; private set; }
        public Button ReturnButton { get; private set; }
        public Button SaveButton { get; private set; }
        public VisualElement Prompt { get; private set; }
        public Label PromptText { get; private set; }
        public VisualElement Response { get; private set; }
        public Label ResponseTitle { get; private set; }
        public Label ResponseText { get; private set; }
        public Label MessageLabel { get; private set; }

        public bool PromptVisible => Prompt != null && !Prompt.ClassListContains("xk-hidden");
        public bool ResponseVisible => Response != null && !Response.ClassListContains("xk-hidden");

        public event Action ReturnRequested;
        public event Action SaveRequested;

        public static ExplorationHud Create(Transform parent)
        {
            var go = new GameObject("Exploration HUD");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<ExplorationHud>();
            hud._document = go.AddComponent<UIDocument>();
            // Phase 19: one shared runtime panel (theme, tokens, the player's interface scale) for every document.
            var panelSettings = Realmkeeper.UI.RkSettings.Panel;

            hud._document.panelSettings = panelSettings;
            hud._document.sortingOrder = 10;
            hud._document.visualTreeAsset = Resources.Load<VisualTreeAsset>(LayoutResource);
            go.SetActive(true);
            hud.Bind();
            return hud;
        }

        private void Bind()
        {
            var documentRoot = _document.rootVisualElement;
            documentRoot.pickingMode = PickingMode.Ignore;
            Root = documentRoot.Q<VisualElement>("xhud-root");
            if (Root == null)
            {
                Debug.LogError($"Realmkeeper: exploration HUD layout '{LayoutResource}' could not be loaded from Resources.");
                return;
            }

            foreach (var child in documentRoot.Children())
            {
                child.pickingMode = PickingMode.Ignore;
                child.style.flexGrow = 1;
            }

            SettlementLabel = Root.Q<Label>("xk-settlement");
            RegionLabel = Root.Q<Label>("xk-region");
            DateLabel = Root.Q<Label>("xk-date");
            PerspectiveLabel = Root.Q<Label>("xk-perspective");
            ReturnButton = Root.Q<Button>("xk-return");
            SaveButton = Root.Q<Button>("xk-save");
            Prompt = Root.Q<VisualElement>("xk-prompt");
            PromptText = Root.Q<Label>("xk-prompt-text");
            Response = Root.Q<VisualElement>("xk-response");
            ResponseTitle = Root.Q<Label>("xk-response-title");
            ResponseText = Root.Q<Label>("xk-response-text");
            MessageLabel = Root.Q<Label>("xk-message");

            // Phase 19: tooltips at run time, icons on the two standing actions, the serif place name.
            Realmkeeper.UI.RkTooltips.Attach(documentRoot);
            Realmkeeper.UI.RkIcons.Lead(SaveButton, "save", "s");
            Realmkeeper.UI.RkIcons.Lead(ReturnButton, "map", "s");
            SaveButton.tooltip = "Save the world (F5).";
            ReturnButton.tooltip = "Leave for the world map (M). Time keeps running.";
            SettlementLabel.AddToClassList("rk-serif");

            ReturnButton.clicked += () => ReturnRequested?.Invoke();
            SaveButton.clicked += () => SaveRequested?.Invoke();
            BuildPhase25(documentRoot);
        }

        // ----- Phase 25: accessibility and width classes, interface sounds, toasts, vitals, the action bar -----------

        /// <summary>Phase 25: the notification toasts (top right, under the standing actions).</summary>
        public Realmkeeper.UI.RkToasts Toasts { get; private set; }

        /// <summary>Phase 25: the bottom action bar: time, the hub, the party, practice and the way out.</summary>
        public VisualElement ActionBar { get; private set; }

        /// <summary>Phase 25: the player's vitals (bottom left): name, hit points, armour, hunger and lasting conditions.</summary>
        public VisualElement Vitals { get; private set; }

        public Label VitalsName { get; private set; }
        public Label VitalsLine { get; private set; }
        public VisualElement VitalsHp { get; private set; }
        public VisualElement VitalsHpFill { get; private set; }
        public VisualElement VitalsConditions { get; private set; }

        /// <summary>True while a window covers the screen: the HUD's panels step aside (Phase 25: no HUD showing through a window).</summary>
        public bool WindowOpen => Root != null && Root.ClassListContains("xk-root--window");

        private void BuildPhase25(VisualElement documentRoot)
        {
            Realmkeeper.UI.RkRoot.Attach(documentRoot);
            Realmkeeper.UI.RkUiSound.Attach(documentRoot);

            // The action bar sits in the bottom stack, above the controls line: the prompt and responses rise above it,
            // and it wraps on narrow screens instead of running off the edge.
            ActionBar = new VisualElement { name = "xk-actionbar", pickingMode = PickingMode.Ignore };
            ActionBar.AddToClassList("rk-actionbar");
            ActionBar.AddToClassList("xk-actionbar");
            var bottom = Root.Q<VisualElement>(className: "xk-bottom");
            var help = Root.Q<Label>("xk-help");
            if (bottom != null && help != null) bottom.Insert(bottom.IndexOf(help), ActionBar);
            else Root.Add(ActionBar);

            Vitals = new VisualElement { name = "xk-vitals" };
            Vitals.AddToClassList("rk-vitals");
            Vitals.AddToClassList("xk-hidden");
            VitalsName = new Label("-") { name = "xk-vitals-name" };
            VitalsName.AddToClassList("rk-vitals-name");
            Vitals.Add(VitalsName);
            VitalsHp = new VisualElement { name = "xk-vitals-hp" };
            VitalsHp.AddToClassList("rk-meter");
            VitalsHp.AddToClassList("rk-vitals-hp");
            VitalsHpFill = new VisualElement();
            VitalsHpFill.AddToClassList("rk-meter-fill");
            VitalsHp.Add(VitalsHpFill);
            Vitals.Add(VitalsHp);
            VitalsLine = new Label("-") { name = "xk-vitals-line" };
            VitalsLine.AddToClassList("rk-vitals-line");
            Vitals.Add(VitalsLine);
            VitalsConditions = new VisualElement { name = "xk-vitals-conditions" };
            VitalsConditions.AddToClassList("rk-vitals-conditions");
            Vitals.Add(VitalsConditions);
            // The vitals stand under the place's name, in one left column, so nothing at the bottom can cover them.
            var location = Root.Q<VisualElement>(className: "xk-location");
            if (location != null && location.parent != null)
            {
                var column = new VisualElement { name = "xk-left", pickingMode = PickingMode.Ignore };
                column.AddToClassList("xk-left-col");
                var parent = location.parent;
                parent.Insert(parent.IndexOf(location), column);
                column.Add(location);
                column.Add(Vitals);
            }
            else Root.Add(Vitals);

            Toasts = new Realmkeeper.UI.RkToasts(Root);
        }

        /// <summary>
        /// Phase 25: shows the player's state. <paramref name="conditions"/> are words with a tone ("good", "warn", "bad");
        /// the hit-point bar is backed by its numbers in the line, so colour never stands alone.
        /// </summary>
        public void SetVitals(string name, int hp, int maxHp, string line, IReadOnlyList<(string text, string tone)> conditions)
        {
            if (Vitals == null) return;
            Vitals.EnableInClassList("xk-hidden", name == null);
            if (name == null) return;
            VitalsName.text = name;
            var share = maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 0f;
            VitalsHpFill.style.width = new StyleLength(new Length(share * 100f, LengthUnit.Percent));
            VitalsHpFill.EnableInClassList("rk-meter-fill--good", share > 0.5f);
            VitalsHpFill.EnableInClassList("rk-meter-fill--warn", share > 0.25f && share <= 0.5f);
            VitalsHpFill.EnableInClassList("rk-meter-fill--bad", share <= 0.25f);
            VitalsHp.tooltip = $"Hit points {hp} of {maxHp}.";
            VitalsLine.text = line;
            VitalsConditions.Clear();
            if (conditions == null) return;
            foreach (var (text, tone) in conditions)
            {
                var b = new Label(text);
                b.AddToClassList("rk-badge");
                if (!string.IsNullOrEmpty(tone)) b.AddToClassList("rk-badge--" + tone);
                VitalsConditions.Add(b);
            }
        }

        /// <summary>Phase 25: hides the HUD's panels behind an open window (the toasts stay).</summary>
        public void SetWindowOpen(bool open)
        {
            if (Root == null || Root.ClassListContains("xk-root--window") == open) return;
            Root.EnableInClassList("xk-root--window", open);
            Toasts?.Stack.BringToFront(); // notices stay readable over a window (moved to the bottom right by the style sheet)
        }

        public void SetLocation(string settlement, string region, string date)
        {
            if (Root == null) return;
            SettlementLabel.text = settlement;
            RegionLabel.text = region;
            DateLabel.text = date;
        }

        public void SetPerspective(CameraPerspective perspective)
        {
            if (Root == null) return;
            PerspectiveLabel.text = perspective == CameraPerspective.FirstPerson ? "First person (V)" : "Third person (V)";
            Root.Q<VisualElement>("xk-crosshair").EnableInClassList("xk-hidden", perspective != CameraPerspective.FirstPerson);
        }

        public void SetPrompt(IInteractable target)
        {
            if (Root == null) return;
            Prompt.EnableInClassList("xk-hidden", target == null);
            if (target == null) return;
            PromptText.text = target.Prompt;
            SetKind(Root.Q<Label>("xk-prompt-kind"), target.Kind);
        }

        public void ShowResponse(InteractionResponse response)
        {
            if (Root == null || response == null) return;
            ResponseTitle.text = response.Title;
            ResponseText.text = response.Text;
            SetKind(Root.Q<Label>("xk-response-kind"), response.Kind);
            Response.EnableInClassList("xk-response--failed", !response.Succeeded);
            Response.RemoveFromClassList("xk-hidden");
            _responseTimer = ResponseSeconds;
        }

        /// <summary>The conversation topics on offer (Phase 4), shown under the response; empty hides them.</summary>
        public VisualElement Topics => Root?.Q<VisualElement>("xk-topics");

        public bool TopicsVisible => Topics != null && !Topics.ClassListContains("xk-hidden");

        public void ShowTopics(IReadOnlyList<(string label, Action ask)> topics)
        {
            var box = Topics;
            if (box == null) return;
            box.Clear();
            var shown = topics != null && topics.Count > 0;
            box.EnableInClassList("xk-hidden", !shown);
            if (!shown) return;
            for (var i = 0; i < topics.Count; i++)
            {
                var (label, ask) = topics[i];
                var button = new Button(() => { ask(); _responseTimer = ResponseSeconds; }) { text = (i + 1) + "  " + label, name = "xk-topic-" + i };
                button.AddToClassList("xk-button");
                button.AddToClassList("xk-topic");
                box.Add(button);
            }

            _responseTimer = ResponseSeconds * 2f;
        }

        /// <summary>Dismisses the last interaction response and its topics (a window opened, or time moved on).</summary>
        public void HideResponse()
        {
            Response?.AddToClassList("xk-hidden");
            Topics?.AddToClassList("xk-hidden");
            _responseTimer = 0f;
        }

        public void ShowMessage(CommandResult result)
        {
            if (MessageLabel == null || result == null) return;
            var text = result.Message;
            if (result.Errors.Count > 0) text += " " + string.Join(" ", result.Errors);
            MessageLabel.text = text;
            MessageLabel.EnableInClassList("xk-message--error", !result.IsSuccess);
        }

        /// <summary>True when a screen position (origin bottom-left) is over a clickable HUD element.</summary>
        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            var panel = Root?.panel;
            if (panel == null) return false;
            var panelPosition = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
            var picked = panel.Pick(panelPosition);
            return picked != null && picked != Root && picked != _document.rootVisualElement;
        }

        private static void SetKind(Label label, InteractionKind kind)
        {
            if (label == null) return;
            label.text = kind == InteractionKind.Informational ? "Informational" : "Changes the world";
            label.EnableInClassList("xk-kind--changes", kind == InteractionKind.StateChanging);
        }

        private void Update()
        {
            if (_responseTimer <= 0f) return;
            _responseTimer -= Time.unscaledDeltaTime;
            if (_responseTimer <= 0f) Response?.AddToClassList("xk-hidden");
        }

        private void OnDestroy()
        {
        }
    }
}
