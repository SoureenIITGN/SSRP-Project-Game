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
        [SerializeField] private UdpInputSource udp;

        public KeyboardInputSource Keyboard => keyboard;
        public ScriptedInputSource Scripted => scripted;
        public SerialInputSource Serial => serial;
        public NoiseInjector NoiseInjected => noiseInjected;
        public UdpInputSource Udp => udp;

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
                case InputSourceType.Udp: udp = source as UdpInputSource; break;
            }
        }

        public IPlayerInputSource Get(InputSourceType type)
        {
            switch (type)
            {
                case InputSourceType.Keyboard: return Valid(keyboard);
                case InputSourceType.Scripted: return Valid(scripted);
                case InputSourceType.Serial: return Valid(serial);
                case InputSourceType.NoiseInjected: return Valid(noiseInjected);
                case InputSourceType.Udp: return Valid(udp);
                default: return null;
            }
        }

        // Unity "fake null" objects (unassigned/destroyed) must not leak out as non-null interfaces.
        private static IPlayerInputSource Valid(MonoBehaviour source) =>
            source != null ? source as IPlayerInputSource : null;
    }
}
