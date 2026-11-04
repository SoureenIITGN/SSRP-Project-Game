using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SSRP.Core;
using SSRP.Flow;

namespace SSRP.UI
{
    /// <summary>
    /// [D5] Game-flow UI driven only by events: 3-2-1-GO countdown (CountdownTick), pause screen
    /// (MatchStateChanged Paused, also shown on sensor disconnect), results screen (MatchOver) with
    /// Rematch / Main Menu, and the calibration overlay (Calibration phase).
    /// Animations use unscaled time because pause sets Time.timeScale = 0.
    /// </summary>
    public class FlowUi : MonoBehaviour
    {
        [SerializeField] private int sortingOrder = 20;

        private Text countdownText;
        private float countdownAnim;
        private GameObject pausePanel;
        private Text pauseTitle;
        private Text pauseDetail;
        private Button resumeButton;
        private GameObject resultsPanel;
        private Text winnerText;
        private Text scoreText;
        private Button rematchButton;
        private Text pointText;
        private float pointAnim;
        private CalibrationPanel calibrationPanel;
        private bool built;

        private void Awake()
        {
            EnsureView();
        }

        private void OnEnable()
        {
            GameEvents.MatchStateChanged += OnPhaseChanged;
            GameEvents.CountdownTick += OnCountdownTick;
            GameEvents.GoalScored += OnGoalScored;
        }

        private void OnDisable()
        {
            GameEvents.MatchStateChanged -= OnPhaseChanged;
            GameEvents.CountdownTick -= OnCountdownTick;
            GameEvents.GoalScored -= OnGoalScored;
        }

        private void EnsureView()
        {
            if (built)
                return;

            built = true;
            UiFactory.SetupCanvas(gameObject, sortingOrder);

            countdownText = UiFactory.CreateText(transform, "Countdown", "", 220, TextAnchor.MiddleCenter,
                UiTheme.Text, FontStyle.Bold);
            UiFactory.Place(countdownText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(800f, 300f));

            pointText = UiFactory.CreateText(transform, "PointBanner", "", 64, TextAnchor.MiddleCenter,
                UiTheme.Text, FontStyle.Bold);
            UiFactory.Place(pointText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 180f), new Vector2(1200f, 100f));

            // Pause --------------------------------------------------------------
            Image pause = UiFactory.CreatePanel(transform, "PausePanel", UiTheme.Backdrop);
            pausePanel = pause.gameObject;
            RectTransform pauseBox = UiFactory.CreateRect("Box", pause.transform);
            UiFactory.Place(pauseBox, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(900f, 560f));
            UiFactory.AddVertical(pauseBox, 20f, 0, TextAnchor.MiddleCenter, false);

            pauseTitle = UiFactory.CreateText(pauseBox, "Title", "Paused", 90, TextAnchor.MiddleCenter, UiTheme.Accent, FontStyle.Bold);
            UiFactory.SetSize(pauseTitle, 900f, 120f, 0f);
            pauseDetail = UiFactory.CreateText(pauseBox, "Detail", "", 30, TextAnchor.MiddleCenter, UiTheme.Warn);
            UiFactory.SetSize(pauseDetail, 900f, 90f, 0f);
            resumeButton = UiFactory.CreateButton(pauseBox, "ResumeButton", "Resume", OnResume, 420f, 80f);
            UiFactory.CreateButton(pauseBox, "MenuButton", "Main Menu", OnMainMenu, 420f, 80f);

            // Results ------------------------------------------------------------
            Image results = UiFactory.CreatePanel(transform, "ResultsPanel", UiTheme.Backdrop);
            resultsPanel = results.gameObject;
            RectTransform resultsBox = UiFactory.CreateRect("Box", results.transform);
            UiFactory.Place(resultsBox, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(1000f, 620f));
            UiFactory.AddVertical(resultsBox, 20f, 0, TextAnchor.MiddleCenter, false);

            winnerText = UiFactory.CreateText(resultsBox, "Winner", "", 96, TextAnchor.MiddleCenter, UiTheme.Text, FontStyle.Bold);
            UiFactory.SetSize(winnerText, 1000f, 140f, 0f);
            scoreText = UiFactory.CreateText(resultsBox, "FinalScore", "", 60, TextAnchor.MiddleCenter, UiTheme.TextDim);
            UiFactory.SetSize(scoreText, 1000f, 100f, 0f);
            rematchButton = UiFactory.CreateButton(resultsBox, "RematchButton", "Rematch", OnRematch, 420f, 80f);
            UiFactory.CreateButton(resultsBox, "MenuButton", "Main Menu", OnMainMenu, 420f, 80f);

            pausePanel.SetActive(false);
            resultsPanel.SetActive(false);
        }

        private void OnPhaseChanged(MatchPhase phase)
        {
            pausePanel.SetActive(phase == MatchPhase.Paused);
            resultsPanel.SetActive(phase == MatchPhase.MatchOver);

            if (phase != MatchPhase.Countdown && phase != MatchPhase.Serving && phase != MatchPhase.Playing)
                countdownText.text = "";

            GameManager gm = GameManager.Instance;

            switch (phase)
            {
                case MatchPhase.Paused:
                    bool disconnected = gm != null && gm.PauseReason == PauseReason.InputDisconnected;
                    pauseTitle.text = disconnected ? "Sensor disconnected" : "Paused";
                    pauseDetail.text = disconnected
                        ? gm.PauseDetail + "\nReconnect the Arduino, then Resume."
                        : "Esc / P to resume";
                    Select(resumeButton);
                    break;

                case MatchPhase.MatchOver:
                    ShowResults(gm);
                    Select(rematchButton);
                    break;

                case MatchPhase.Calibration:
                    OpenCalibration();
                    break;
            }
        }

        private void ShowResults(GameManager gm)
        {
            if (gm == null)
                return;

            PlayerDefinition winner = gm.GetPlayer(gm.WinnerId);
            winnerText.text = winner != null ? winner.displayName + " wins!" : "Match over";
            winnerText.color = winner != null ? winner.colour : UiTheme.Text;

            string score = "";
            foreach (PlayerDefinition player in gm.Config.players)
            {
                if (score.Length > 0)
                    score += "   -   ";
                score += $"{player.displayName} {gm.Score.GetScore(player.playerId)}";
            }

            scoreText.text = score;
        }

        private void OnCountdownTick(int secondsLeft)
        {
            countdownText.text = secondsLeft > 0 ? secondsLeft.ToString() : "GO!";
            countdownText.color = secondsLeft > 0 ? UiTheme.Text : UiTheme.Good;
            countdownAnim = 1f;
        }

        private void OnGoalScored(int playerId)
        {
            GameManager gm = GameManager.Instance;
            PlayerDefinition player = gm != null ? gm.GetPlayer(playerId) : null;

            if (player == null)
                return;

            pointText.text = "Point " + player.displayName;
            pointText.color = player.colour;
            pointAnim = 1.2f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (countdownAnim > 0f)
            {
                countdownAnim = Mathf.Max(0f, countdownAnim - dt);
                float pulse = 1f + 0.35f * countdownAnim * countdownAnim;
                countdownText.rectTransform.localScale = new Vector3(pulse, pulse, 1f);

                if (countdownAnim <= 0f && countdownText.text == "GO!")
                    countdownText.text = "";
            }

            if (pointAnim > 0f)
            {
                pointAnim = Mathf.Max(0f, pointAnim - dt);
                Color c = pointText.color;
                c.a = Mathf.Clamp01(pointAnim * 2f);
                pointText.color = c;
            }
        }

        private void OpenCalibration()
        {
            if (calibrationPanel == null)
            {
                calibrationPanel = CalibrationPanel.Create(transform);
                calibrationPanel.Closed += OnCalibrationClosed;
            }

            calibrationPanel.transform.SetAsLastSibling();
            calibrationPanel.gameObject.SetActive(true);
        }

        private void OnCalibrationClosed()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.CompleteCalibration();
        }

        private static void Select(Selectable selectable)
        {
            if (EventSystem.current != null && selectable != null)
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }

        private void OnResume()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.Resume();
        }

        private void OnRematch()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.Rematch();
        }

        private void OnMainMenu()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.ReturnToMenu();
            else
                SceneLoader.Load(SceneNames.MainMenu);
        }
    }
}
