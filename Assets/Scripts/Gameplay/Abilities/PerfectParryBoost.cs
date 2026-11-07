// Assets/Scripts/Gameplay/Abilities/PerfectParryBoost.cs
using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    public class PerfectParryBoost : MonoBehaviour, IBallHittable
    {
        [SerializeField] private float window = 0.25f;      // parry must land this close before contact
        [SerializeField] private float multiplier = 2.5f;
        [SerializeField] private float duration = 4f;

        private IPaddleInfo paddle;
        private float lastParryTime = -99f;

        private void Awake() => paddle = GetComponent<IPaddleInfo>();
        private void OnEnable() => GameEvents.ParryTriggered += OnParry;
        private void OnDisable() => GameEvents.ParryTriggered -= OnParry;

        private void OnParry(int playerId)
        {
            if (paddle != null && playerId == paddle.PlayerId)
                lastParryTime = Time.time;
        }

        public void OnBallHit(BallHitInfo info)   // the ball calls this on contact
        {
            if (Time.time - lastParryTime > window) return;
            if (info.Other.TryGetComponent(out BallController ball))
                ball.Modifiers.Add("parry.perfect", new SpeedMultiplierModifier(multiplier), duration);
        }
    }
}