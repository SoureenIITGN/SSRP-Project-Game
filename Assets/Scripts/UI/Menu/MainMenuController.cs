using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SSRP.Core;
using SSRP.Flow;

namespace SSRP.UI
{
    /// <summary>
    /// [D2] Main menu: Play, Match Setup, How to Play, Quit. uGUI canvas scaled for 1920x1080
    /// (Scale With Screen Size), mouse + keyboard/gamepad navigation through the EventSystem,
    /// and optional FSR navigation (FsrMenuNavigationSource child, see [D2] stretch).
    /// Scene names come from SceneNames only.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional FSR navigation source (child object). Follows Player 1's input choice.")]
        private FsrMenuNavigationSource fsrNavigation;

        private GameObject mainPanel;
        private GameObject howToPanel;
        private MatchSetupPanel setupPanel;
        private Button playButton;
        private Button howToBack;
        private GameObject current;

        private void Awake()
        {
            GameServices.Ensure();
            Build();
        }

        private void Start()
        {
            GameEvents.RaiseMatchStateChanged(MatchPhase.Menu);
            SyncFsrNavigation();
            ShowMain();
        }

        private void Build()
        {
            UiFactory.SetupCanvas(gameObject, 0);

            Image background = UiFactory.CreatePanel(transform, "Background", new Color(0.03f, 0.04f, 0.09f, 1f));
            background.raycastTarget = false;

            // Main panel ----------------------------------------------------------
            RectTransform main = UiFactory.CreateRect("MainPanel", transform);
            UiFactory.Stretch(main);
            mainPanel = main.gameObject;

            Text title = UiFactory.CreateText(main, "Title", "FSR PONG", 140, TextAnchor.MiddleCenter,
                UiTheme.Accent, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -120f), new Vector2(1400f, 180f));

            Text subtitle = UiFactory.CreateText(main, "Subtitle",
                "PVP Pong using FSR controllers", 34, TextAnchor.MiddleCenter, UiTheme.TextDim);
            UiFactory.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -290f), new Vector2(1400f, 60f));

            RectTransform buttons = UiFactory.CreateRect("Buttons", main);
            UiFactory.Place(buttons, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, 40f),
                new Vector2(460f, 420f));
            UiFactory.AddVertical(buttons, 22f, 0, TextAnchor.UpperCenter);

            playButton = UiFactory.CreateButton(buttons, "PlayButton", "Play", Play, 460f, 84f, 38);
            UiFactory.CreateButton(buttons, "SetupButton", "Match Setup", ShowSetup, 460f, 84f, 38);
            UiFactory.CreateButton(buttons, "HowToButton", "How to Play", ShowHowTo, 460f, 84f, 38);
            UiFactory.CreateButton(buttons, "QuitButton", "Quit", Quit, 460f, 84f, 38);

            Text footer = UiFactory.CreateText(main, "Footer",
                "SSRP project - real-time FSR signal processing", 22, TextAnchor.MiddleCenter, UiTheme.TextDim);
            UiFactory.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), new Vector2(1200f, 40f));

            // Match setup ---------------------------------------------------------
            setupPanel = MatchSetupPanel.Create(transform);
            setupPanel.BackRequested += ShowMain;
            setupPanel.SettingsChanged += SyncFsrNavigation;

            // How to play ---------------------------------------------------------
            Image howTo = UiFactory.CreatePanel(transform, "HowToPanel", UiTheme.Backdrop);
            howToPanel = howTo.gameObject;

            Text howToText = UiFactory.CreateText(howTo.transform, "Text", HowToText, 32, TextAnchor.UpperLeft);
            howToText.supportRichText = true;
            UiFactory.Place(howToText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 60f), new Vector2(1400f, 760f));

            howToBack = UiFactory.CreateButton(howTo.transform, "BackButton", "Back", ShowMain, 300f, 72f);
            UiFactory.Place(howToBack.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 60f), new Vector2(300f, 72f));
        }

        private const string HowToText =
            "<b><size=48>How to play</size></b>\n\n" +
            "Each player has two force sensors: <b>UP</b> and <b>DOWN</b>.\n" +
            "The paddle's <b>speed</b> follows the difference: press UP harder to move up, DOWN harder to move down.\n" +
            "Press both equally to hold still. Press both <b>firmly</b> for a moment to <b>parry</b>.\n\n" +
            "Hit the ball with the edge of the paddle for a steeper angle. First to the target score wins.\n\n" +
            "<b>Keyboard:</b>  Player 1 = W / S     Player 2 = Up / Down arrows (both keys = parry)\n" +
            "<b>During a match:</b>  Esc or P = pause     F1 = live sensor debug overlay\n\n" +
            "<b>Using the Arduino:</b> Match Setup > connect the port > Calibrate sensors > set a player's input to Arduino.";

        private void Show(GameObject panel, Selectable firstSelected)
        {
            mainPanel.SetActive(panel == mainPanel);
            setupPanel.gameObject.SetActive(panel == setupPanel.gameObject);
            howToPanel.SetActive(panel == howToPanel);
            current = panel;

            if (EventSystem.current != null && firstSelected != null)
                EventSystem.current.SetSelectedGameObject(firstSelected.gameObject);
        }

        public void ShowMain() => Show(mainPanel, playButton);

        public void ShowSetup() => Show(setupPanel.gameObject, setupPanel.FirstSelectable);

        public void ShowHowTo() => Show(howToPanel, howToBack);

        public void Play()
        {
            GameSettings.SaveFrom(MatchSession.Config, GameCatalog.Load());
            SceneLoader.Load(SceneNames.Game);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void SyncFsrNavigation()
        {
            if (fsrNavigation == null)
                return;

            MatchConfig config = MatchSession.Config;
            PlayerDefinition first = config.players.Count > 0 ? config.players[0] : null;

            if (first != null)
                fsrNavigation.SetInputSource(first.inputSource);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && current != mainPanel)
                ShowMain();

            // Keep something selected so keyboard / FSR navigation always works.
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.currentSelectedGameObject == null && current == mainPanel)
                eventSystem.SetSelectedGameObject(playButton.gameObject);
        }
    }
}
