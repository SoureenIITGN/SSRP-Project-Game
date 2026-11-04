using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SSRP.UI
{
    /// <summary>
    /// [D2 stretch] Anything that can drive menu navigation: +1 = up, -1 = down, submit = press.
    /// Implemented by FsrMenuNavigationSource (sensors). Keyboard/mouse use the EventSystem directly.
    /// </summary>
    public interface IMenuNavigationSource
    {
        bool IsActive { get; }
        bool TryConsume(out int verticalStep, out bool submit);
    }

    /// <summary>[D2 stretch] Moves the EventSystem selection from IMenuNavigationSource components.</summary>
    public class MenuNavigator : MonoBehaviour
    {
        private IMenuNavigationSource[] sources = new IMenuNavigationSource[0];

        private void Awake()
        {
            sources = GetComponentsInChildren<IMenuNavigationSource>(true);
        }

        private void Update()
        {
            EventSystem eventSystem = EventSystem.current;

            if (eventSystem == null)
                return;

            for (int i = 0; i < sources.Length; i++)
            {
                if (!sources[i].IsActive || !sources[i].TryConsume(out int step, out bool submit))
                    continue;

                GameObject current = eventSystem.currentSelectedGameObject;

                if (current == null || !current.activeInHierarchy)
                    continue;

                if (step != 0 && current.TryGetComponent(out Selectable selectable))
                {
                    Selectable target = step > 0 ? selectable.FindSelectableOnUp() : selectable.FindSelectableOnDown();

                    if (target != null)
                        target.Select();
                }

                if (submit)
                    ExecuteEvents.Execute(current, new BaseEventData(eventSystem), ExecuteEvents.submitHandler);
            }
        }
    }
}
