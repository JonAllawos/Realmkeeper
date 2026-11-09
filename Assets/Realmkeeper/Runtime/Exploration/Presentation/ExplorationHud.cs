using System;
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
        private PanelSettings _ownedPanelSettings;
        private float _responseTimer;

        public VisualElement Root { get; private set; }
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
            var panelSettings = Resources.Load<PanelSettings>(PanelSettingsResource);
            if (panelSettings == null)
            {
                panelSettings = hud._ownedPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
                panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("RealmkeeperTheme");
                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(1920, 1080);
                panelSettings.match = 0.5f;
            }

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

            ReturnButton.clicked += () => ReturnRequested?.Invoke();
            SaveButton.clicked += () => SaveRequested?.Invoke();
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
            if (_ownedPanelSettings != null) Destroy(_ownedPanelSettings);
        }
    }
}
