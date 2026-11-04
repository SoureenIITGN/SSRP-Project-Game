using UnityEngine;
using SSRP.Core;

namespace SSRP.Input
{
    /// <summary>
    /// Lives on the persistent services object. Maps an InputSourceType to the one shared
    /// instance of that source, so paddles spawned at runtime (and PlayerSlots with empty
    /// inspector fields) can find their device without Find* calls.
    /// </summary>
    public class InputSourceRegistry : MonoBehaviour
    {
        public static InputSourceRegistry Instance { get; private set; }

        [SerializeField] private KeyboardInputSource keyboard;
        [SerializeField] private ScriptedInputSource scripted;
        [SerializeField] private SerialInputSource serial;
        [SerializeField] private NoiseInjector noiseInjected;

        public KeyboardInputSource Keyboard => keyboard;
        public ScriptedInputSource Scripted => scripted;
        public SerialInputSource Serial => serial;
        public NoiseInjector NoiseInjected => noiseInjected;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("InputSourceRegistry: a second registry was created; ignoring it.", this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Wire a source from code (used by the code-only services fallback and tests).</summary>
        public void Register(InputSourceType type, MonoBehaviour source)
        {
            switch (type)
            {
                case InputSourceType.Keyboard: keyboard = source as KeyboardInputSource; break;
                case InputSourceType.Scripted: scripted = source as ScriptedInputSource; break;
                case InputSourceType.Serial: serial = source as SerialInputSource; break;
                case InputSourceType.NoiseInjected: noiseInjected = source as NoiseInjector; break;
            }
        }

        public IPlayerInputSource Get(InputSourceType type)
        {
            switch (type)
            {
                case InputSourceType.Keyboard: return keyboard;
                case InputSourceType.Scripted: return scripted;
                case InputSourceType.Serial: return serial;
                case InputSourceType.NoiseInjected: return noiseInjected;
                default: return null;
            }
        }
    }
}
