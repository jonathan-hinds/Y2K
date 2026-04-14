using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Race.Multiplayer
{
    [DisallowMultipleComponent]
    public sealed class MultiplayerMenuPresenter : MonoBehaviour
    {
        [Header("Display")]
        [SerializeField] private string title = "Multiplayer";
        [SerializeField] private Color panelColor = new(0.07f, 0.09f, 0.12f, 0.92f);
        [SerializeField] private Color accentColor = new(0.21f, 0.72f, 0.98f, 1f);

        private MultiplayerSessionController controller;
        private Canvas canvas;
        private GameObject panel;
        private Text statusLabel;
        private Text joinCodeLabel;
        private Button hostButton;
        private Button joinButton;
        private Button leaveButton;
        private Button closeButton;
        private GameObject joinPrompt;
        private InputField joinCodeInputField;
        private bool isBusy;

        public bool IsVisible => panel != null && panel.activeSelf;

        private void Awake()
        {
            EnsureUi();
            SetVisible(false);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ToggleVisibility();
            }
            else if (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame)
            {
                ToggleVisibility();
            }
        }

        public void Bind(MultiplayerSessionController sessionController)
        {
            controller = sessionController;
            controller.StatusChanged += HandleStatusChanged;
            controller.SessionStateChanged += HandleSessionStateChanged;
            RefreshUiState();
        }

        private void OnDestroy()
        {
            if (controller != null)
            {
                controller.StatusChanged -= HandleStatusChanged;
                controller.SessionStateChanged -= HandleSessionStateChanged;
            }
        }

        private void ToggleVisibility()
        {
            SetVisible(!IsVisible);
        }

        private void SetVisible(bool visible)
        {
            EnsureUi();
            panel.SetActive(visible);
            controller?.SetGameplayMenuVisible(visible);

            if (visible)
            {
                joinPrompt.SetActive(false);
                RefreshUiState();
                EventSystem.current?.SetSelectedGameObject(hostButton != null && hostButton.gameObject.activeInHierarchy ? hostButton.gameObject : closeButton.gameObject);
            }
        }

        private void RefreshUiState()
        {
            bool sessionActive = controller != null && controller.IsSessionActive;
            if (hostButton != null)
            {
                hostButton.gameObject.SetActive(!sessionActive);
                hostButton.interactable = !isBusy;
            }

            if (joinButton != null)
            {
                joinButton.gameObject.SetActive(!sessionActive);
                joinButton.interactable = !isBusy;
            }

            if (leaveButton != null)
            {
                leaveButton.gameObject.SetActive(sessionActive);
                leaveButton.interactable = !isBusy;
            }

            if (joinCodeLabel != null)
            {
                bool showJoinCode = sessionActive && controller != null && controller.IsHost && !string.IsNullOrWhiteSpace(controller.ActiveJoinCode);
                joinCodeLabel.gameObject.SetActive(showJoinCode);
                joinCodeLabel.text = showJoinCode ? $"Join Code\n{controller.ActiveJoinCode}\nCopied to clipboard" : string.Empty;
            }
        }

        private void EnsureUi()
        {
            if (canvas != null)
            {
                return;
            }

            CreateEventSystemIfNeeded();

            GameObject canvasObject = new GameObject("MultiplayerMenuCanvas");
            canvasObject.transform.SetParent(transform, false);

            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            canvasObject.AddComponent<GraphicRaycaster>();

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            panel = CreatePanel(canvas.transform, "Panel", new Vector2(520f, 520f));
            Text titleLabel = CreateText(panel.transform, "Title", title, 34, FontStyle.Bold, TextAnchor.UpperCenter);
            titleLabel.rectTransform.anchoredPosition = new Vector2(0f, -42f);

            statusLabel = CreateText(panel.transform, "Status", "Press ESC to open multiplayer controls.", 20, FontStyle.Normal, TextAnchor.UpperLeft);
            statusLabel.rectTransform.sizeDelta = new Vector2(420f, 100f);
            statusLabel.rectTransform.anchoredPosition = new Vector2(0f, -110f);

            joinCodeLabel = CreateText(panel.transform, "JoinCode", string.Empty, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            joinCodeLabel.rectTransform.sizeDelta = new Vector2(420f, 96f);
            joinCodeLabel.rectTransform.anchoredPosition = new Vector2(0f, -208f);

            hostButton = CreateButton(panel.transform, "HostButton", "Host Session", new Vector2(0f, -310f), HandleHostClicked);
            joinButton = CreateButton(panel.transform, "JoinButton", "Join Session", new Vector2(0f, -376f), HandleJoinClicked);
            leaveButton = CreateButton(panel.transform, "LeaveButton", "Leave Session", new Vector2(0f, -310f), HandleLeaveClicked);
            closeButton = CreateButton(panel.transform, "CloseButton", "Close", new Vector2(0f, -442f), HandleCloseClicked);

            joinPrompt = CreatePanel(panel.transform, "JoinPrompt", new Vector2(440f, 180f));
            joinPrompt.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -300f);

            Text joinPromptLabel = CreateText(joinPrompt.transform, "JoinPromptLabel", "Enter Join Code", 24, FontStyle.Bold, TextAnchor.UpperCenter);
            joinPromptLabel.rectTransform.anchoredPosition = new Vector2(0f, -26f);

            joinCodeInputField = CreateInputField(joinPrompt.transform, "JoinCodeInput");
            joinCodeInputField.textComponent.alignment = TextAnchor.MiddleCenter;
            joinCodeInputField.characterLimit = 12;
            joinCodeInputField.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -84f);

            CreateButton(joinPrompt.transform, "ConfirmJoinButton", "Join", new Vector2(-82f, -136f), HandleConfirmJoinClicked);
            CreateButton(joinPrompt.transform, "CancelJoinButton", "Cancel", new Vector2(82f, -136f), HandleCancelJoinClicked);
            joinPrompt.SetActive(false);
        }

        private void HandleStatusChanged(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }

            RefreshUiState();
        }

        private void HandleSessionStateChanged(bool _)
        {
            RefreshUiState();
        }

        private async void HandleHostClicked()
        {
            await RunBusyAction(async () =>
            {
                joinPrompt.SetActive(false);
                await controller.HostSessionAsync();
            });
        }

        private void HandleJoinClicked()
        {
            joinPrompt.SetActive(true);
            joinCodeInputField.text = string.Empty;
            EventSystem.current?.SetSelectedGameObject(joinCodeInputField.gameObject);
        }

        private async void HandleConfirmJoinClicked()
        {
            string joinCode = joinCodeInputField.text;
            await RunBusyAction(async () =>
            {
                await controller.JoinSessionAsync(joinCode);
                joinPrompt.SetActive(false);
            });
        }

        private void HandleCancelJoinClicked()
        {
            joinPrompt.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(joinButton.gameObject);
        }

        private void HandleLeaveClicked()
        {
            controller.LeaveSession();
            joinPrompt.SetActive(false);
        }

        private void HandleCloseClicked()
        {
            SetVisible(false);
        }

        private async Task RunBusyAction(Func<Task> action)
        {
            if (controller == null || isBusy)
            {
                return;
            }

            isBusy = true;
            RefreshUiState();

            try
            {
                await action();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                isBusy = false;
                RefreshUiState();
            }
        }

        private void CreateEventSystemIfNeeded()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);

            RectTransform rectTransform = panelObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = size;

            Image image = panelObject.GetComponent<Image>();
            image.color = panelColor;
            return panelObject;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPosition, Action onClick)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.sizeDelta = new Vector2(320f, 50f);
            rectTransform.anchoredPosition = anchoredPosition;

            buttonObject.GetComponent<Image>().color = accentColor;
            Button button = buttonObject.GetComponent<Button>();
            button.onClick.AddListener(() => onClick?.Invoke());

            Text text = CreateText(buttonObject.transform, "Label", label, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;
            return button;
        }

        private InputField CreateInputField(Transform parent, string name)
        {
            GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
            inputObject.transform.SetParent(parent, false);

            RectTransform rectTransform = inputObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.sizeDelta = new Vector2(260f, 42f);

            inputObject.GetComponent<Image>().color = new Color(0.13f, 0.15f, 0.18f, 1f);

            InputField inputField = inputObject.GetComponent<InputField>();
            inputField.lineType = InputField.LineType.SingleLine;
            inputField.contentType = InputField.ContentType.Alphanumeric;

            Text placeholder = CreateText(inputObject.transform, "Placeholder", "JOIN CODE", 20, FontStyle.Italic, TextAnchor.MiddleCenter);
            placeholder.color = new Color(0.72f, 0.77f, 0.84f, 0.65f);
            placeholder.rectTransform.anchorMin = Vector2.zero;
            placeholder.rectTransform.anchorMax = Vector2.one;
            placeholder.rectTransform.offsetMin = new Vector2(12f, 6f);
            placeholder.rectTransform.offsetMax = new Vector2(-12f, -6f);

            Text text = CreateText(inputObject.transform, "Text", string.Empty, 22, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(12f, 6f);
            text.rectTransform.offsetMax = new Vector2(-12f, -6f);

            inputField.placeholder = placeholder;
            inputField.textComponent = text;
            return inputField;
        }

        private Text CreateText(Transform parent, string name, string content, int fontSize, FontStyle fontStyle, TextAnchor anchor)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = anchor;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            RectTransform rectTransform = text.rectTransform;
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.sizeDelta = new Vector2(420f, 48f);
            return text;
        }
    }
}
