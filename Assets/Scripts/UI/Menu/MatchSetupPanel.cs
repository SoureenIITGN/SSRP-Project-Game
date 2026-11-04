using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using SSRP.Arena;
using SSRP.Core;
using SSRP.Flow;

namespace SSRP.UI
{
    /// <summary>
    /// [D3] Match setup & settings. Everything is written to the RUNTIME MatchConfig
    /// (MatchSession.Config, an Instantiate copy) and persisted with PlayerPrefs, so assets are never
    /// modified by playing and the choices survive a restart.
    ///
    /// Options: target score, ball speed preset, arena (with thumbnail), input source per player,
    /// volume, effect toggles. Embeds the Arduino connection panel and opens the calibration panel.
    /// </summary>
    public class MatchSetupPanel : MonoBehaviour
    {
        private static readonly InputSourceType[] InputChoices =
        {
            InputSourceType.Keyboard,
            InputSourceType.Serial,
            InputSourceType.Udp,
            InputSourceType.NoiseInjected,
            InputSourceType.Scripted
        };

        public event Action BackRequested;
        public event Action SettingsChanged;

        private GameCatalog catalog;
        private MatchConfig config;
        private Image arenaThumbnail;
        private Text arenaDescription;
        private CalibrationPanel calibrationPanel;
        private Button firstSelectable;
        private bool built;

        public Button FirstSelectable => firstSelectable;

        public static MatchSetupPanel Create(Transform parent)
        {
            RectTransform rt = UiFactory.CreateRect("MatchSetupPanel", parent);
            UiFactory.Stretch(rt);
            return rt.gameObject.AddComponent<MatchSetupPanel>();
        }

        private void Awake()
        {
            EnsureView();
        }

        private void EnsureView()
        {
            if (built)
                return;

            built = true;
            catalog = GameCatalog.Load();
            config = MatchSession.Config;

            Text title = UiFactory.CreateText(transform, "Title", "Match setup", 64, TextAnchor.MiddleCenter,
                UiTheme.Accent, FontStyle.Bold);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(900f, 90f));

            // Left column: options.
            Image left = UiFactory.CreateImage(transform, "Options", UiTheme.Panel);
            UiFactory.Place(left.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(-20f, -30f), new Vector2(880f, 800f));
            UiFactory.AddVertical(left, 8f, 28, TextAnchor.UpperLeft);

            BuildOptions(left.transform);

            // Right column: arena preview, Arduino panel, calibration.
            RectTransform right = UiFactory.CreateRect("Hardware", transform);
            UiFactory.Place(right, new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, -30f),
                new Vector2(840f, 800f));
            UiFactory.AddVertical(right, 16f, 0, TextAnchor.UpperLeft);

            Image preview = UiFactory.CreateImage(right, "ArenaPreview", UiTheme.Panel);
            UiFactory.SetSize(preview, 840f, 300f, 0f);
            UiFactory.AddHorizontal(preview, 20f, 18, TextAnchor.MiddleLeft);
            arenaThumbnail = UiFactory.CreateImage(preview.transform, "Thumbnail", Color.white);
            arenaThumbnail.preserveAspect = true;
            UiFactory.SetSize(arenaThumbnail, 420f, 236f, 0f);
            arenaDescription = UiFactory.CreateText(preview.transform, "Description", "", 24, TextAnchor.MiddleLeft, UiTheme.TextDim);
            UiFactory.SetSize(arenaDescription, 360f, 236f, 0f);

            SerialConnectionPanel.Create(right);

            Button calibrate = UiFactory.CreateButton(right, "CalibrateButton", "Calibrate sensors...", OpenCalibration, 840f, 70f);
            UiFactory.SetSize(calibrate, 840f, 70f, 0f);

            Button back = UiFactory.CreateButton(transform, "BackButton", "Back", () => BackRequested?.Invoke(), 300f, 72f);
            UiFactory.Place(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 40f), new Vector2(300f, 72f));

            RefreshArenaPreview();
        }

        private void BuildOptions(Transform parent)
        {
            // Target score.
            List<int> targets = catalog != null && catalog.targetScoreOptions.Count > 0
                ? catalog.targetScoreOptions
                : new List<int> { 5, 7, 11 };
            var targetLabels = new List<string>();
            foreach (int t in targets) targetLabels.Add("First to " + t);
            int targetIndex = Mathf.Max(0, targets.IndexOf(config.targetScore));
            OptionSelector target = OptionSelector.Create(parent, "Target score", targetLabels, targetIndex, i =>
            {
                config.targetScore = targets[i];
                Save();
            });
            firstSelectable = target.NextButton;

            // Ball speed preset.
            if (catalog != null && catalog.ballPresets.Count > 0)
            {
                var names = new List<string>();
                foreach (BallConfig preset in catalog.ballPresets)
                    names.Add(preset != null ? preset.presetName : "?");

                int index = Mathf.Max(0, catalog.IndexOfBallPreset(config.ballConfig));
                OptionSelector.Create(parent, "Ball speed", names, index, i =>
                {
                    config.ballConfig = catalog.ballPresets[i];
                    Save();
                });
            }

            // Arena.
            ArenaRegistry registry = catalog != null ? catalog.arenaRegistry : null;
            if (registry != null && registry.Count > 0)
            {
                var names = new List<string>();
                foreach (ArenaDefinition arena in registry.arenas)
                    names.Add(arena != null ? arena.displayName : "?");

                int index = Mathf.Max(0, registry.IndexOf(config.arenaId));
                OptionSelector.Create(parent, "Arena", names, index, i =>
                {
                    config.arenaId = registry.arenas[i].id;
                    RefreshArenaPreview();
                    Save();
                });
            }

            // Input source per player (data-driven: one row per PlayerDefinition).
            var inputNames = new List<string>();
            foreach (InputSourceType type in InputChoices)
                inputNames.Add(type.DisplayName());

            foreach (PlayerDefinition player in config.players)
            {
                PlayerDefinition p = player;
                int index = Mathf.Max(0, Array.IndexOf(InputChoices, p.inputSource));
                OptionSelector.Create(parent, p.displayName + " input", inputNames, index, i =>
                {
                    p.inputSource = InputChoices[i];
                    Save();
                });
            }

            // Volume.
            RectTransform volumeRow = UiFactory.CreateRow(parent, "Option - Volume", 62f, 12f);
            Text volumeLabel = UiFactory.CreateText(volumeRow, "Label", "Volume", 30, TextAnchor.MiddleLeft);
            UiFactory.SetSize(volumeLabel, 330f, 62f, 0f);
            UiFactory.CreateSlider(volumeRow, "VolumeSlider", 0f, 1f, GameSettings.Volume, v =>
            {
                GameSettings.Volume = v;
                GameSettings.Save();
            }, 480f);

            // Effects toggles.
            var onOff = new List<string> { "On", "Off" };
            OptionSelector.Create(parent, "Screen shake", onOff, GameSettings.ScreenShake ? 0 : 1, i =>
            {
                GameSettings.ScreenShake = i == 0;
                GameSettings.Save();
            });
            OptionSelector.Create(parent, "Hit-stop", onOff, GameSettings.HitStop ? 0 : 1, i =>
            {
                GameSettings.HitStop = i == 0;
                GameSettings.Save();
            });
            OptionSelector.Create(parent, "Particles", onOff, GameSettings.Particles ? 0 : 1, i =>
            {
                GameSettings.Particles = i == 0;
                GameSettings.Save();
            });
        }

        private void Save()
        {
            GameSettings.SaveFrom(config, catalog);
            SettingsChanged?.Invoke();
        }

        private void RefreshArenaPreview()
        {
            ArenaRegistry registry = catalog != null ? catalog.arenaRegistry : null;
            ArenaDefinition arena = registry != null ? registry.Get(config.arenaId) : null;

            arenaThumbnail.sprite = arena != null ? arena.thumbnail : null;
            arenaThumbnail.color = arenaThumbnail.sprite != null ? Color.white : UiTheme.PanelLight;
            arenaDescription.text = arena != null
                ? $"<b>{arena.displayName}</b>\n\n{arena.description}"
                : "No arenas found. Run SSRP > Build Project.";
            arenaDescription.supportRichText = true;
        }

        private void OpenCalibration()
        {
            if (calibrationPanel == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                Transform parent = canvas != null ? canvas.transform : transform;
                calibrationPanel = CalibrationPanel.Create(parent);
                calibrationPanel.Closed += OnCalibrationClosed;
            }

            calibrationPanel.transform.SetAsLastSibling();
            calibrationPanel.gameObject.SetActive(true);
        }

        private void OnCalibrationClosed()
        {
            if (EventSystem.current != null && firstSelectable != null)
                EventSystem.current.SetSelectedGameObject(firstSelectable.gameObject);
        }
    }
}
