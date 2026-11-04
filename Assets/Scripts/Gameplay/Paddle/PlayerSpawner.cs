using System;
using System.Collections.Generic;
using UnityEngine;
using SSRP.Core;
using SSRP.Input;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [C3] Builds one paddle (PlayerSlot + PlayerInputPipeline + PaddleController + abilities)
    /// per PlayerDefinition in the MatchConfig. Data-driven: the number of players, names,
    /// colours, sides and input devices all come from the config.
    /// </summary>
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField, Tooltip("Paddle prefab (needs PlayerSlot, PlayerInputPipeline, PaddleController).")]
        private PaddleController paddlePrefab;

        [SerializeField, Tooltip("Parent for spawned paddles (defaults to this transform).")]
        private Transform paddleRoot;

        private readonly List<PaddleController> paddles = new List<PaddleController>();

        public IReadOnlyList<PaddleController> Paddles => paddles;

        public void SetPrefab(PaddleController prefab) => paddlePrefab = prefab;

        /// <summary>
        /// Spawn all players. <paramref name="spawnPointForSide"/> returns the spawn position for a side
        /// (supplied by the arena), so this class never references Arena code.
        /// </summary>
        public void Spawn(MatchConfig config, ArenaBounds bounds, Func<PaddleSide, Vector2> spawnPointForSide)
        {
            Despawn();

            if (paddlePrefab == null)
            {
                Debug.LogError("PlayerSpawner: no paddle prefab assigned.", this);
                return;
            }

            Transform parent = paddleRoot != null ? paddleRoot : transform;

            foreach (PlayerDefinition definition in config.players)
            {
                Vector2 position = spawnPointForSide(definition.side);
                PaddleController paddle = Instantiate(paddlePrefab, position, Quaternion.identity, parent);
                paddle.name = "Paddle - " + definition.displayName;

                PlayerSlot slot = paddle.Slot != null ? paddle.Slot : paddle.GetComponent<PlayerSlot>();

                if (slot != null)
                    slot.Configure(definition);

                paddle.Configure(config.paddleConfig);
                paddle.SetColour(definition.colour);
                paddle.SetBounds(bounds);
                paddle.SetPosition(position);
                paddles.Add(paddle);
            }
        }

        public void Despawn()
        {
            foreach (PaddleController paddle in paddles)
            {
                if (paddle != null)
                    Destroy(paddle.gameObject);
            }

            paddles.Clear();
        }

        public PaddleController GetPaddle(int playerId)
        {
            foreach (PaddleController paddle in paddles)
            {
                if (paddle != null && paddle.PlayerId == playerId)
                    return paddle;
            }

            return null;
        }

        public PlayerSlot GetSlot(int playerId)
        {
            PaddleController paddle = GetPaddle(playerId);
            return paddle != null ? paddle.Slot : null;
        }

        /// <summary>Re-centre every paddle vertically (e.g. for a rematch).</summary>
        public void ResetPositions(Func<PaddleSide, Vector2> spawnPointForSide)
        {
            foreach (PaddleController paddle in paddles)
            {
                if (paddle != null && paddle.Slot != null)
                    paddle.SetPosition(spawnPointForSide(paddle.Slot.Side));
            }
        }
    }
}
