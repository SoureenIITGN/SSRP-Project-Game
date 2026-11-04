
using UnityEngine;
using SSRP.Core;

namespace SSRP.Input
{
    // InputSourceType (the enum of choices) now lives in SSRP.Core so MatchConfig can use it.

    //By storing a MonoBehaviour reference, we can drag a component into the Inspector and then check whether it implements IPlayerInputSource.
    //The same applies to the Scripted and Serial references.
    //If a field is left empty, the slot falls back to the shared source in InputSourceRegistry
    //(the persistent services object), which is what paddles spawned at runtime use.
    public class PlayerSlot : MonoBehaviour
    {
        [Header("Player Settings")]
        [SerializeField]
        private int playerId = 1;

        [Header("Input Selection")]
        [SerializeField]
        private InputSourceType inputSourceType =
            InputSourceType.Keyboard;

        [Header("Available Input Sources (optional, registry used if empty)")]
        [SerializeField]
        private KeyboardInputSource keyboardSource;

        [SerializeField]
        private ScriptedInputSource scriptedSource;

        [SerializeField]
        private SerialInputSource serialSource;

        [SerializeField]
        private NoiseInjector noiseInjectorSource;

        [SerializeField]
        private UdpInputSource udpSource;

        [Header("Player Identity ([C3], set from MatchConfig by PlayerSpawner)")]
        [SerializeField]
        private string displayName = "Player";

        [SerializeField]
        private Color colour = Color.white;

        [SerializeField]
        private PaddleSide side = PaddleSide.Left;

        private PlayerInputPipeline pipeline;

        public int PlayerId => playerId;

        public string DisplayName => displayName;

        public Color Colour => colour;

        public PaddleSide Side => side;

        /// <summary>Current score (the GameManager's MatchState is the source of truth and updates this).</summary>
        public int Score { get; set; }

        /// <summary>The pipeline on the same GameObject (may be null for input-only test objects).</summary>
        public PlayerInputPipeline Pipeline
        {
            get
            {
                if (pipeline == null)
                    pipeline = GetComponent<PlayerInputPipeline>();

                return pipeline;
            }
        }

        /// <summary>Apply a PlayerDefinition from MatchConfig (data-driven, no 'Player1/Player2' strings).</summary>
        public void Configure(PlayerDefinition definition)
        {
            if (definition == null)
                return;

            playerId = definition.playerId;
            displayName = definition.displayName;
            colour = definition.colour;
            side = definition.side;
            inputSourceType = definition.inputSource;
            Score = 0;
        }

        public InputSourceType InputSourceType => inputSourceType;

        /// <summary>Change the device used by this player at runtime.</summary>
        public void SetInputSource(InputSourceType type)
        {
            inputSourceType = type;
        }

        public void SetPlayerId(int id)
        {
            playerId = id;
        }

        /// <summary>The source currently selected (null if none is available).</summary>
        public IPlayerInputSource CurrentSource => GetSelectedSource();

        private IPlayerInputSource GetSelectedSource()
        {
            IPlayerInputSource local = GetInspectorSource();

            if (local != null)
                return local;

            InputSourceRegistry registry = InputSourceRegistry.Instance;
            return registry != null ? registry.Get(inputSourceType) : null;
        }

        private IPlayerInputSource GetInspectorSource()
        {
            // Explicit Unity null checks: a destroyed component is "== null" but not C#-null.
            switch (inputSourceType)
            {
                case InputSourceType.Keyboard:
                    return keyboardSource != null ? keyboardSource : null;

                case InputSourceType.Scripted:
                    return scriptedSource != null ? scriptedSource : null;

                case InputSourceType.Serial:
                    return serialSource != null ? serialSource : null;

                case InputSourceType.NoiseInjected:
                    return noiseInjectorSource != null ? noiseInjectorSource : null;

                case InputSourceType.Udp:
                    return udpSource != null ? udpSource : null;

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
