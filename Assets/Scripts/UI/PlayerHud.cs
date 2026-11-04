using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using SSRP.Core;
using SSRP.Input;

namespace SSRP.UI
{
    /// <summary>
    /// [C4] Two-player HUD + live input debug overlay (toggle with F1).
    ///
    /// Event-driven: scores come from GameEvents.ScoreChanged, the connection dot from
    /// GameEvents.InputConnectionChanged. Live values are read from the public state of the
    /// pipelines (PlayerInputPipeline.Active) - no Find* calls. Drop the prefab into any scene.
    /// Anchored to the top corners, so the layout works at 16:9 and 16:10.
    /// </summary>
    public class PlayerHud : MonoBehaviour
    {
        private class SidePanel
        {
            public Text Name;
            public Text Score;
            public Image Dot;
            public Text Source;
            public GameObject Debug;
            public RectTransform UpBar;
            public RectTransform DownBar;
            public RectTransform RawUpBar;
            public RectTransform RawDownBar;
            public Text DebugText;
        }

        [SerializeField] private bool debugVisibleOnStart;
        [SerializeField] private int sortingOrder = 10;

        private readonly Dictionary<int, int> scores = new Dictionary<int, int>();
        private readonly Dictionary<int, bool> connections = new Dictionary<int, bool>();
        private SidePanel left;
        private SidePanel right;
        private Text hint;
        private bool debugVisible;
        private bool built;

        public bool DebugVisible => debugVisible;

        private void Awake()
        {
            EnsureView();
            SetDebugVisible(debugVisibleOnStart);
        }

        private void OnEnable()
        {
            GameEvents.ScoreChanged += OnScoreChanged;
            GameEvents.InputConnectionChanged += OnConnectionChanged;
        }

        private void OnDisable()
        {
            GameEvents.ScoreChanged -= OnScoreChanged;
            GameEvents.InputConnectionChanged -= OnConnectionChanged;
        }

        private void OnScoreChanged(int playerId, int score) => scores[playerId] = score;

        private void OnConnectionChanged(int playerId, bool connected) => connections[playerId] = connected;

        public void SetDebugVisible(bool visible)
        {
            debugVisible = visible;
            left.Debug.SetActive(visible);
            right.Debug.SetActive(visible);
            hint.text = visible ? "F1: hide input debug" : "F1: input debug";
        }

        private void EnsureView()
        {
            if (built)
                return;

            built = true;
            UiFactory.SetupCanvas(gameObject, sortingOrder);

            left = BuildSide("Left", true);
            right = BuildSide("Right", false);

            hint = UiFactory.CreateText(transform, "Hint", "", 20, TextAnchor.LowerCenter, UiTheme.TextDim);
            UiFactory.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 12f), new Vector2(600f, 30f));
        }

        private SidePanel BuildSide(string name, bool isLeft)
        {
            var panel = new SidePanel();
            float anchorX = isLeft ? 0f : 1f;
            float sign = isLeft ? 1f : -1f;
            TextAnchor align = isLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;

            RectTransform root = UiFactory.CreateRect(name + "Hud", transform);
            UiFactory.Place(root, new Vector2(anchorX, 1f), new Vector2(anchorX, 1f),
                new Vector2(sign * 40f, -24f), new Vector2(520f, 150f));

            panel.Score = UiFactory.CreateText(root, "Score", "0", 96, align, UiTheme.Text, FontStyle.Bold);
            UiFactory.Place(panel.Score.rectTransform, new Vector2(anchorX, 1f), new Vector2(anchorX, 1f),
                Vector2.zero, new Vector2(200f, 100f));

            panel.Name = UiFactory.CreateText(root, "Name", "Player", 30, align);
            UiFactory.Place(panel.Name.rectTransform, new Vector2(anchorX, 1f), new Vector2(anchorX, 1f),
                new Vector2(sign * 150f, -10f), new Vector2(360f, 40f));

            panel.Dot = UiFactory.CreateImage(root, "ConnectionDot", UiTheme.Off);
            UiFactory.Place(panel.Dot.rectTransform, new Vector2(anchorX, 1f), new Vector2(anchorX, 1f),
                new Vector2(sign * 152f, -62f), new Vector2(20f, 20f));

            panel.Source = UiFactory.CreateText(root, "Source", "", 22, align, UiTheme.TextDim);
            UiFactory.Place(panel.Source.rectTransform, new Vector2(anchorX, 1f), new Vector2(anchorX, 1f),
                new Vector2(sign * 184f, -52f), new Vector2(330f, 40f));

            // Debug overlay block under the score.
            Image debugBg = UiFactory.CreateImage(root, "Debug", UiTheme.Panel);
            UiFactory.Place(debugBg.rectTransform, new Vector2(anchorX, 1f), new Vector2(anchorX, 1f),
                new Vector2(0f, -150f), new Vector2(520f, 300f));
            panel.Debug = debugBg.gameObject;

            float barsX = isLeft ? 24f : -24f;
            panel.RawUpBar = AddBar(debugBg.transform, "RawUp", anchorX, barsX, UiTheme.TextDim);
            panel.UpBar = AddBar(debugBg.transform, "Up", anchorX, barsX + sign * 34f, UiTheme.Accent);
            panel.RawDownBar = AddBar(debugBg.transform, "RawDown", anchorX, barsX + sign * 90f, UiTheme.TextDim);
            panel.DownBar = AddBar(debugBg.transform, "Down", anchorX, barsX + sign * 124f, UiTheme.Warn);

            Text labels = UiFactory.CreateText(debugBg.transform, "BarLabels", "  UP        DOWN", 18, align, UiTheme.TextDim);
            UiFactory.Place(labels.rectTransform, new Vector2(anchorX, 0f), new Vector2(anchorX, 0f),
                new Vector2(barsX, 6f), new Vector2(200f, 24f));

            panel.DebugText = UiFactory.CreateText(debugBg.transform, "Values", "", 20,
                isLeft ? TextAnchor.UpperLeft : TextAnchor.UpperRight, UiTheme.Text);
            UiFactory.Place(panel.DebugText.rectTransform, new Vector2(anchorX, 1f), new Vector2(anchorX, 1f),
                new Vector2(sign * 190f, -16f), new Vector2(310f, 270f));

            return panel;
        }

        private static RectTransform AddBar(Transform parent, string name, float anchorX, float x, Color colour)
        {
            RectTransform fill = UiFactory.CreateBar(parent, name, colour, true, 26f, 230f);
            RectTransform back = (RectTransform)fill.parent;
            UiFactory.Place(back, new Vector2(anchorX, 0f), new Vector2(anchorX, 0f), new Vector2(x, 36f),
                new Vector2(26f, 230f));
            return fill;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
                SetDebugVisible(!debugVisible);

            bool leftSeen = false;
            bool rightSeen = false;
            IReadOnlyList<PlayerInputPipeline> pipelines = PlayerInputPipeline.Active;

            for (int i = 0; i < pipelines.Count; i++)
            {
                PlayerInputPipeline pipeline = pipelines[i];
                PlayerSlot slot = pipeline != null ? pipeline.Slot : null;

                if (slot == null)
                    continue;

                bool isLeft = slot.Side == PaddleSide.Left;

                if (isLeft && leftSeen || !isLeft && rightSeen)
                    continue;

                if (isLeft) leftSeen = true;
                else rightSeen = true;

                Refresh(isLeft ? left : right, pipeline, slot);
            }

            SetPanelVisible(left, leftSeen);
            SetPanelVisible(right, rightSeen);
        }

        private static void SetPanelVisible(SidePanel panel, bool visible)
        {
            panel.Score.transform.parent.gameObject.SetActive(visible);
        }

        private void Refresh(SidePanel panel, PlayerInputPipeline pipeline, PlayerSlot slot)
        {
            int id = slot.PlayerId;
            panel.Name.text = slot.DisplayName;
            panel.Name.color = slot.Colour;
            panel.Score.text = (scores.TryGetValue(id, out int s) ? s : slot.Score).ToString();
            panel.Score.color = slot.Colour;

            bool connected = connections.TryGetValue(id, out bool c) ? c : pipeline.IsConnected;
            panel.Dot.color = connected ? UiTheme.Good : UiTheme.Bad;
            panel.Source.text = slot.InputSourceType.DisplayName() + (connected ? "" : " - no data");

            if (!debugVisible)
                return;

            PlayerInputState state = pipeline.State;
            RawFsrSample raw = pipeline.LastRawSample;

            UiFactory.SetBar(panel.RawUpBar, pipeline.NormalisedUp, true);
            UiFactory.SetBar(panel.UpBar, state.Up, true);
            UiFactory.SetBar(panel.RawDownBar, pipeline.NormalisedDown, true);
            UiFactory.SetBar(panel.DownBar, state.Down, true);

            string age = double.IsInfinity(pipeline.SampleAge) ? "-" : $"{pipeline.SampleAge * 1000.0:0} ms";
            string filters = "";
            foreach (FilterStage stage in pipeline.FilterChain)
            {
                filters += stage.type == FilterType.Exponential
                    ? $"EMA(tau={stage.timeConstant * 1000f:0}ms) "
                    : "pass ";
            }

            panel.DebugText.text =
                $"raw up/down  {raw.Up:0} / {raw.Down:0}\n" +
                $"norm         {pipeline.NormalisedUp:0.00} / {pipeline.NormalisedDown:0.00}\n" +
                $"filtered     {state.Up:0.00} / {state.Down:0.00}\n" +
                $"velocity     {state.Velocity:+0.00;-0.00;0.00}\n" +
                $"sum          {state.Sum:0.00}\n" +
                $"sample age   {age}\n" +
                $"filters      {filters}";
        }
    }
}
