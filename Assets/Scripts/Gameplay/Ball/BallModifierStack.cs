using System.Collections.Generic;
using UnityEngine;
using SSRP.Core;

namespace SSRP.Gameplay
{
    /// <summary>
    /// [B2] Keyed collection of IBallModifier. Power-ups, tiles and abilities change ball speed by
    /// adding a modifier here, never by editing BallController. The speed multiplier is the
    /// product of all active modifiers. Adding with an existing key replaces (refreshes) it.
    /// Expiry uses scaled time, so modifiers freeze while the game is paused.
    /// </summary>
    public class BallModifierStack : MonoBehaviour
    {
        private struct Entry
        {
            public IBallModifier Modifier;
            public float ExpiresAt;   // < 0 = never
        }

        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private readonly List<string> expired = new List<string>();

        public int Count => entries.Count;

        public float Product
        {
            get
            {
                float product = 1f;

                foreach (KeyValuePair<string, Entry> pair in entries)
                    product *= pair.Value.Modifier.SpeedMultiplier;

                return product;
            }
        }

        public void Add(string key, IBallModifier modifier) => Add(key, modifier, -1f);

        /// <param name="duration">Seconds until it is removed automatically; &lt;= 0 means until Remove().</param>
        public void Add(string key, IBallModifier modifier, float duration)
        {
            if (string.IsNullOrEmpty(key) || modifier == null)
                return;

            entries[key] = new Entry
            {
                Modifier = modifier,
                ExpiresAt = duration > 0f ? Time.time + duration : -1f
            };
        }

        public bool Remove(string key) => key != null && entries.Remove(key);

        public bool Contains(string key) => key != null && entries.ContainsKey(key);

        public void Clear() => entries.Clear();

        private void Update()
        {
            if (entries.Count == 0)
                return;

            float now = Time.time;
            expired.Clear();

            foreach (KeyValuePair<string, Entry> pair in entries)
            {
                if (pair.Value.ExpiresAt >= 0f && now >= pair.Value.ExpiresAt)
                    expired.Add(pair.Key);
            }

            for (int i = 0; i < expired.Count; i++)
                entries.Remove(expired[i]);
        }
    }

    /// <summary>A modifier that multiplies speed by a fixed factor.</summary>
    public sealed class SpeedMultiplierModifier : IBallModifier
    {
        public SpeedMultiplierModifier(float multiplier)
        {
            SpeedMultiplier = Mathf.Max(0f, multiplier);
        }

        public float SpeedMultiplier { get; }
    }
}
