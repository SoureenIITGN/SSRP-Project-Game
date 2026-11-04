
using UnityEngine;
using SSRP.Core;

namespace SSRP.Input
{
    //An enum is a way of defining a fixed set of choices.
    public enum InputSourceType
    {
        Keyboard,
        Scripted,
        Serial,
        NoiseInjected
    }

    //By storing a MonoBehaviour reference, we can drag a component into the Inspector and then check whether it implements IPlayerInputSource.
    //The same applies to the Scripted and Serial references.
    public class PlayerSlot : MonoBehaviour
    {
        [Header("Player Settings")]
        [SerializeField]
        private int playerId = 1;

        [Header("Input Selection")]
        [SerializeField]
        private InputSourceType inputSourceType =
            InputSourceType.Keyboard;

        [Header("Available Input Sources")]
        [SerializeField]
        private KeyboardInputSource keyboardSource;

        [SerializeField]
        private ScriptedInputSource scriptedSource;

        [SerializeField]
        private SerialInputSource serialSource;

        [SerializeField]
        private NoiseInjector noiseInjectorSource;

        private IPlayerInputSource GetSelectedSource()
        {
            switch (inputSourceType)
            {
                case InputSourceType.Keyboard:
                    return keyboardSource;

                case InputSourceType.Scripted:
                    return scriptedSource;

                case InputSourceType.Serial:
                    return serialSource;

                case InputSourceType.NoiseInjected:
                    return noiseInjectorSource;

                default:
                    return null;
            }
        }

        public bool IsConnected =>
            GetSelectedSource()?.IsConnected ?? false;

        public bool TryGetLatest(out RawFsrSample sample)
        {
            IPlayerInputSource source = GetSelectedSource();

            if (source == null)
            {
                sample = default;
                return false;
            }

            //This asks the selected source for the readings belonging to this particular player
            return source.TryGetLatest(playerId, out sample);
        }
    }
}
