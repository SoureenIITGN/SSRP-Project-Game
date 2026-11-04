using System;
using UnityEngine;
using UnityEngine.UI;
using SSRP.Core;
using SSRP.Input;

namespace SSRP.UI
{
    /// <summary>
    /// [A4] Guided calibration screen: prompts, progress bar, live raw bars for the 4 sensors,
    /// Full calibration / Quick re-zero / Reset to defaults / Close.
    /// Reads from the Arduino source (or UDP when that is the one receiving data).
    /// Closing without calibrating keeps the defaults (0-1023).
    /// Owner: Member A.
    /// </summary>
    public class CalibrationPanel : MonoBehaviour
    {
        public event Action Closed;

        private Text promptText;
        private Text messageText;
        private Text sourceText;
        private RectTransform progressFill;
        private readonly RectTransform[] channelFills = new RectTransform[CalibrationData.ChannelCount];
        private readonly Text[] channelValues = new Text[CalibrationData.ChannelCount];
        private Button fullButton;
        private Button quickButton;
        private Button resetButton;
        private Button closeButton;

        private CalibrationRoutine routine;
        private readonly float[] channels = new float[CalibrationData.ChannelCount];
        private bool built;

        public bool IsRunning => routine != null && !routine.IsDone;

        /// <summary>Creates a full-screen calibration overlay under <paramref name="parent"/>.</summary>
        public static CalibrationPanel Create(Transform parent)
        {
            RectTransform rt = UiFactory.CreateRect("CalibrationPanel", parent);
            UiFactory.Stretch(rt);
            return rt.gameObject.AddComponent<CalibrationPanel>();
        }

        private void Awake()
        {
            EnsureView();
        }

        private void OnEnable()
        {
            EnsureView();
            routine = null;
            messageText.color = UiTheme.Warn;
            messageText.text = CalibrationStore.HasSavedCalibration
                ? "A saved calibration is active."
                : "No calibration saved: using defaults (0-1023).";
            promptText.text = "Choose Full calibration (first time) or Quick re-zero (sensors drifted).";
            UiFactory.SetBar(progressFill, 0f, false);
            SetButtonsInteractable(true);
        }

        private void EnsureView()
        {
            if (built)
                return;

            built = true;

            RectTransform root = (RectTransform)transform;
            UiFactory.Stretch(root);
            Image backdrop = gameObject.GetComponent<Image>();
            if (backdrop == null)
                backdrop = gameObject.AddComponent<Image>();
            backdrop.color = UiTheme.Backdrop;

            Image window = UiFactory.CreateImage(transform, "Window", UiTheme.Panel);
            UiFactory.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1100f, 820f));
            UiFactory.AddVertical(window, 18f, 36, TextAnchor.UpperCenter);

            Text title = UiFactory.CreateText(window.transform, "Title", "Sensor calibration", 48,
                TextAnchor.MiddleCenter, UiTheme.Accent, FontStyle.Bold);
            UiFactory.SetSize(title, -1f, 64f, 1f);

            sourceText = UiFactory.CreateText(window.transform, "Source", "", 24, TextAnchor.MiddleCenter, UiTheme.TextDim);
            UiFactory.SetSize(sourceText, -1f, 30f, 1f);

            promptText = UiFactory.CreateText(window.transform, "Prompt", "", 34);
            UiFactory.SetSize(promptText, -1f, 90f, 1f);

            RectTransform progressRow = UiFactory.CreateRow(window.transform, "ProgressRow", 28f);
            progressFill = UiFactory.CreateBar(progressRow, "Progress", UiTheme.Accent, false, 1000f, 24f);

            RectTransform bars = UiFactory.CreateRow(window.transform, "ChannelBars", 250f, 40f);
            bars.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;

            for (int i = 0; i < CalibrationData.ChannelCount; i++)
            {
                RectTransform column = UiFactory.CreateRect("Channel" + i, bars);
                UiFactory.AddVertical(column, 6f, 0, TextAnchor.LowerCenter, false);
                UiFactory.SetSize(column, 200f, 250f, 0f);

                channelValues[i] = UiFactory.CreateText(column, "Value", "-", 22);
                UiFactory.SetSize(channelValues[i], 200f, 28f, 0f);

                channelFills[i] = UiFactory.CreateBar(column, "Bar", i < 2 ? UiTheme.Accent : UiTheme.Warn, true, 60f, 160f);

                Text label = UiFactory.CreateText(column, "Label", CalibrationData.ChannelNames[i], 22, TextAnchor.MiddleCenter, UiTheme.TextDim);
                UiFactory.SetSize(label, 200f, 28f, 0f);
            }

            messageText = UiFactory.CreateText(window.transform, "Message", "", 24, TextAnchor.MiddleCenter, UiTheme.Warn);
            UiFactory.SetSize(messageText, -1f, 90f, 1f);

            RectTransform buttons = UiFactory.CreateRow(window.transform, "Buttons", 76f, 20f);
            buttons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            fullButton = UiFactory.CreateButton(buttons, "FullButton", "Full calibration", () => Begin(false), 280f, 70f, 28);
            quickButton = UiFactory.CreateButton(buttons, "QuickButton", "Quick re-zero", () => Begin(true), 240f, 70f, 28);
            resetButton = UiFactory.CreateButton(buttons, "ResetButton", "Use defaults", ResetDefaults, 220f, 70f, 28);
            closeButton = UiFactory.CreateButton(buttons, "CloseButton", "Close", Close, 180f, 70f, 28);
        }

        private void Begin(bool quick)
        {
            routine = new CalibrationRoutine(quick, CalibrationStore.Current);
            messageText.text = "";
            SetButtonsInteractable(false);
        }

        private void ResetDefaults()
        {
            CalibrationStore.ResetToDefaults();
            messageText.text = "Defaults restored (0-1023 per channel).";
        }

        public void Close()
        {
            routine = null;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        private void SetButtonsInteractable(bool value)
        {
            fullButton.interactable = value;
            quickButton.interactable = value;
            resetButton.interactable = value;
            closeButton.interactable = value;
        }

        private bool ReadChannels(out string sourceName)
        {
            InputSourceRegistry registry = InputSourceRegistry.Instance;
            sourceName = "no source";

            if (registry == null)
                return false;

            // Prefer the Arduino; fall back to any other hardware source (UDP) that is connected.
            InputSourceType[] candidates = { InputSourceType.Serial, InputSourceType.Udp };

            foreach (InputSourceType type in candidates)
            {
                IPlayerInputSource source = registry.Get(type);

                if (source == null || !source.IsConnected)
                    continue;

                if (source.TryGetLatest(1, out RawFsrSample p1) && source.TryGetLatest(2, out RawFsrSample p2))
                {
                    channels[0] = p1.Up;
                    channels[1] = p1.Down;
                    channels[2] = p2.Up;
                    channels[3] = p2.Down;
                    sourceName = type.DisplayName();
                    return true;
                }
            }

            return false;
        }

        private void Update()
        {
            bool hasSample = ReadChannels(out string sourceName);
            sourceText.text = hasSample ? $"Reading from: {sourceName}" : "No sensor data - connect the Arduino first.";

            for (int i = 0; i < CalibrationData.ChannelCount; i++)
            {
                float v = hasSample ? channels[i] : 0f;
                UiFactory.SetBar(channelFills[i], v / 1023f, true);
                channelValues[i].text = hasSample ? v.ToString("0") : "-";
            }

            if (routine == null)
                return;

            routine.Tick(Time.unscaledDeltaTime, channels, hasSample);
            promptText.text = routine.Prompt;
            UiFactory.SetBar(progressFill, routine.OverallProgress, false);

            if (!routine.IsDone)
                return;

            if (routine.Succeeded)
            {
                CalibrationStore.Save(routine.Result);
                messageText.color = UiTheme.Good;
                messageText.text = (routine.IsQuick ? "Baselines updated. " : "Calibration saved. ") + routine.Message;
            }
            else
            {
                messageText.color = UiTheme.Bad;
                messageText.text = routine.Message;
            }

            routine = null;
            SetButtonsInteractable(true);
        }
    }
}
