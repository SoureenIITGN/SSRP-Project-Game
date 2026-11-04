using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SSRP.UI
{
    /// <summary>A "Label   &lt;  Value  &gt;" row that cycles through options (mouse, keyboard or FSR navigation).</summary>
    public class OptionSelector : MonoBehaviour
    {
        private readonly List<string> options = new List<string>();
        private Text valueText;
        private Button previous;
        private Button next;
        private int index;

        public event Action<int> Changed;

        public int Index => index;
        public Button PreviousButton => previous;
        public Button NextButton => next;

        public static OptionSelector Create(Transform parent, string label, IList<string> choices, int startIndex,
            Action<int> onChanged)
        {
            RectTransform row = UiFactory.CreateRow(parent, "Option - " + label, 62f, 12f);
            OptionSelector selector = row.gameObject.AddComponent<OptionSelector>();

            Text labelText = UiFactory.CreateText(row, "Label", label, 30, TextAnchor.MiddleLeft);
            UiFactory.SetSize(labelText, 330f, 62f, 0f);

            selector.previous = UiFactory.CreateButton(row, "Previous", "<", () => selector.Step(-1), 64f, 56f, 32);

            selector.valueText = UiFactory.CreateText(row, "Value", "", 30, TextAnchor.MiddleCenter, UiTheme.Accent);
            UiFactory.SetSize(selector.valueText, 330f, 62f, 0f);

            selector.next = UiFactory.CreateButton(row, "Next", ">", () => selector.Step(1), 64f, 56f, 32);

            selector.SetOptions(choices, startIndex, false);

            if (onChanged != null)
                selector.Changed += onChanged;

            return selector;
        }

        public void SetOptions(IList<string> choices, int startIndex, bool notify)
        {
            options.Clear();

            if (choices != null)
                options.AddRange(choices);

            SetIndex(startIndex, notify);
        }

        public void SetIndex(int value, bool notify)
        {
            index = options.Count == 0 ? 0 : Mathf.Clamp(value, 0, options.Count - 1);
            valueText.text = options.Count > 0 ? options[index] : "-";

            if (notify)
                Changed?.Invoke(index);
        }

        public void Step(int delta)
        {
            if (options.Count == 0)
                return;

            int n = options.Count;
            SetIndex(((index + delta) % n + n) % n, true);
        }
    }
}
