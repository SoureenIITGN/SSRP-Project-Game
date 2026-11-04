using UnityEngine;
using SSRP.Core;
using SSRP.Input;

namespace SSRP.Flow
{
    /// <summary>
    /// [D1] Root of the persistent services object (DontDestroyOnLoad): scene loader, input sources
    /// + registry, serial connection manager, audio. Created once by the Boot scene, or on demand by
    /// <see cref="Ensure"/> when a scene is played directly in the editor.
    /// </summary>
    public class GameServices : MonoBehaviour
    {
        public static GameServices Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        /// <summary>Returns the services, creating them from the catalog prefab if needed.</summary>
        public static GameServices Ensure()
        {
            if (Instance != null)
                return Instance;

            GameCatalog catalog = GameCatalog.Load();

            if (catalog != null && catalog.servicesPrefab != null)
            {
                GameObject go = Instantiate(catalog.servicesPrefab);
                go.name = "SSRP Services";
                GameServices services = go.GetComponent<GameServices>();

                if (services != null)
                    return services;

                Debug.LogError("GameServices: the services prefab has no GameServices component.");
                Destroy(go);
            }

            return CreateCodeFallback();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            AudioListener.volume = GameSettings.Volume;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Minimal services built in code, so the game still runs (keyboard only, no sound) before the
        /// project builder has generated the services prefab.
        /// </summary>
        private static GameServices CreateCodeFallback()
        {
            Debug.LogWarning("GameServices: using the code fallback (no services prefab). Run SSRP > Build Project.");

            var root = new GameObject("SSRP Services (code fallback)");
            root.SetActive(false);

            GameServices services = root.AddComponent<GameServices>();
            root.AddComponent<SceneLoader>();

            var input = new GameObject("Input");
            input.transform.SetParent(root.transform, false);

            KeyboardInputSource keyboard = input.AddComponent<KeyboardInputSource>();
            ScriptedInputSource scripted = input.AddComponent<ScriptedInputSource>();
            SerialInputSource serial = input.AddComponent<SerialInputSource>();
            input.AddComponent<SerialConnectionManager>();
            NoiseInjector noise = input.AddComponent<NoiseInjector>();
            noise.SetSource(keyboard);

            InputSourceRegistry registry = input.AddComponent<InputSourceRegistry>();
            registry.Register(InputSourceType.Keyboard, keyboard);
            registry.Register(InputSourceType.Scripted, scripted);
            registry.Register(InputSourceType.Serial, serial);
            registry.Register(InputSourceType.NoiseInjected, noise);

            AddFallbackExtras(root, input, registry);

            root.SetActive(true);
            return services;
        }

        // Extended by later features (UDP source, audio) without changing the method above.
        private static void AddFallbackExtras(GameObject root, GameObject input, InputSourceRegistry registry)
        {
            FallbackExtras.Add(root, input, registry);
        }
    }

    /// <summary>Extra components added to the code fallback services (UDP, audio).</summary>
    internal static class FallbackExtras
    {
        public static void Add(GameObject root, GameObject input, InputSourceRegistry registry)
        {
        }
    }
}
