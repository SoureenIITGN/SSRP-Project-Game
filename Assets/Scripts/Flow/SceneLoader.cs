using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SSRP.Flow
{
    /// <summary>
    /// [D1] Loads scenes with a fade to black. Lives on the persistent services object.
    /// Uses unscaled time and resets Time.timeScale, so it works from the pause menu.
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float fadeDuration = 0.3f;
        [SerializeField] private Color fadeColour = Color.black;

        private CanvasGroup fade;
        private bool loading;

        public static SceneLoader Instance { get; private set; }

        public static bool IsLoading => Instance != null && Instance.loading;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            BuildFadeOverlay();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Load a scene by name (use SceneNames constants).</summary>
        public static void Load(string sceneName)
        {
            Time.timeScale = 1f;

            if (Instance != null)
            {
                if (!Instance.loading)
                    Instance.StartCoroutine(Instance.LoadRoutine(sceneName));
            }
            else
            {
                SceneManager.LoadScene(sceneName);
            }
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            loading = true;
            fade.blocksRaycasts = true;
            yield return FadeTo(1f);

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);

            if (op != null)
            {
                while (!op.isDone)
                    yield return null;
            }
            else
            {
                Debug.LogError($"SceneLoader: scene '{sceneName}' is not in Build Settings. Run SSRP > Build Project.");
            }

            yield return FadeTo(0f);
            fade.blocksRaycasts = false;
            loading = false;
        }

        private IEnumerator FadeTo(float target)
        {
            float start = fade.alpha;
            float t = 0f;

            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fade.alpha = Mathf.Lerp(start, target, fadeDuration > 0f ? t / fadeDuration : 1f);
                yield return null;
            }

            fade.alpha = target;
        }

        private void BuildFadeOverlay()
        {
            var go = new GameObject("Fade Overlay", typeof(RectTransform));
            go.transform.SetParent(transform, false);

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;

            fade = go.AddComponent<CanvasGroup>();
            fade.alpha = 0f;
            fade.blocksRaycasts = false;
            fade.interactable = false;

            var image = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
            image.transform.SetParent(go.transform, false);
            image.color = fadeColour;
            RectTransform rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
