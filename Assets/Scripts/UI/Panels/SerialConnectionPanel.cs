using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SSRP.Input;

namespace SSRP.UI
{
    /// <summary>
    /// [A3] Reusable Arduino connection panel: port dropdown, refresh, connect/disconnect,
    /// status LED and packets/s readout. Drop the prefab (or call <see cref="Create"/>) into
    /// any UI; it talks to the persistent <see cref="SerialConnectionManager"/>.
    /// Owner: Member A (lives in Scripts/UI because it is a UI widget).
    /// </summary>
    public class SerialConnectionPanel : MonoBehaviour
    {
        private Dropdown portDropdown;
        private Button connectButton;
        private Image led;
        private Text statusText;
        private Text detailText;
        private readonly List<string> ports = new List<string>();
        private bool built;

        public static SerialConnectionPanel Create(Transform parent)
        {
            RectTransform rt = UiFactory.CreateRect("SerialConnectionPanel", parent);
            return rt.gameObject.AddComponent<SerialConnectionPanel>();
        }

        private void Awake()
        {
            EnsureView();
        }

        private void OnEnable()
        {
            EnsureView();
            RefreshPorts();
        }

        private void EnsureView()
        {
            if (built)
                return;

            built = true;

            Image bg = gameObject.GetComponent<Image>();
            if (bg == null)
                bg = gameObject.AddComponent<Image>();
            bg.color = UiTheme.PanelLight;

            UiFactory.AddVertical(this, 10f, 18, TextAnchor.UpperLeft);
            UiFactory.SetSize(this, 820f, 250f, 0f);

            Text title = UiFactory.CreateText(transform, "Title", "Arduino connection", 28,
                TextAnchor.MiddleLeft, UiTheme.Accent, FontStyle.Bold);
            UiFactory.SetSize(title, -1f, 36f, 1f);

            RectTransform row1 = UiFactory.CreateRow(transform, "PortRow", 60f);
            portDropdown = UiFactory.CreateDropdown(row1, "PortDropdown", 460f);
            UiFactory.CreateButton(row1, "RefreshButton", "Refresh", RefreshPorts, 160f, 60f, 26);
            connectButton = UiFactory.CreateButton(row1, "ConnectButton", "Connect", OnConnectClicked, 160f, 60f, 26);

            RectTransform row2 = UiFactory.CreateRow(transform, "StatusRow", 40f);
            led = UiFactory.CreateImage(row2, "StatusLed", UiTheme.Off);
            UiFactory.SetSize(led, 28f, 28f, 0f);
            statusText = UiFactory.CreateText(row2, "StatusText", "Disconnected", 26, TextAnchor.MiddleLeft);
            UiFactory.SetSize(statusText, -1f, 40f, 1f);

            detailText = UiFactory.CreateText(transform, "DetailText", "", 22, TextAnchor.MiddleLeft, UiTheme.TextDim);
            UiFactory.SetSize(detailText, -1f, 30f, 1f);
        }

        public void RefreshPorts()
        {
            EnsureView();

            ports.Clear();
            ports.AddRange(SerialConnectionManager.GetPortNames());

            portDropdown.ClearOptions();

            if (ports.Count == 0)
            {
                portDropdown.AddOptions(new List<string> { "(no serial ports found)" });
                portDropdown.interactable = false;
                return;
            }

            portDropdown.interactable = true;
            portDropdown.AddOptions(ports);

            SerialConnectionManager manager = SerialConnectionManager.Instance;
            string remembered = manager != null ? manager.LastPort : null;
            int index = remembered != null ? ports.IndexOf(remembered) : -1;
            portDropdown.SetValueWithoutNotify(Mathf.Max(0, index));
            portDropdown.RefreshShownValue();
        }

        private void OnConnectClicked()
        {
            SerialConnectionManager manager = SerialConnectionManager.Instance;

            if (manager == null)
                return;

            if (manager.WantsConnection)
            {
                manager.Disconnect();
                return;
            }

            if (ports.Count == 0)
                return;

            manager.Connect(ports[Mathf.Clamp(portDropdown.value, 0, ports.Count - 1)]);
        }

        private void Update()
        {
            SerialConnectionManager manager = SerialConnectionManager.Instance;

            if (manager == null || manager.Source == null)
            {
                led.color = UiTheme.Off;
                statusText.text = "Serial service not running";
                detailText.text = "";
                return;
            }

            SerialInputSource src = manager.Source;
            UiFactory.SetButtonLabel(connectButton, manager.WantsConnection ? "Disconnect" : "Connect");

            switch (manager.Status)
            {
                case SerialConnectionManager.ConnectionStatus.Connected:
                    led.color = UiTheme.Good;
                    statusText.text = $"Connected to {manager.DesiredPort}  -  {src.PacketsPerSecond:0} Hz";
                    break;

                case SerialConnectionManager.ConnectionStatus.NoData:
                    led.color = UiTheme.Warn;
                    statusText.text = $"Port open, no valid data ({manager.DesiredPort})";
                    break;

                case SerialConnectionManager.ConnectionStatus.Reconnecting:
                    led.color = UiTheme.Bad;
                    statusText.text = $"Reconnecting to {manager.DesiredPort}...";
                    break;

                default:
                    led.color = UiTheme.Off;
                    statusText.text = "Disconnected";
                    break;
            }

            string age = double.IsInfinity(src.LastPacketAge) ? "-" : $"{src.LastPacketAge * 1000.0:0} ms";
            string error = string.IsNullOrEmpty(src.LastError) ? "" : $"   last error: {src.LastError}";
            detailText.text = $"packets {src.PacketCount}   parse errors {src.ParseErrors}   age {age}{error}";
        }
    }
}
