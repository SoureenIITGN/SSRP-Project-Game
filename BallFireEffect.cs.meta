using System;
using UnityEngine;
using UnityEngine.Tilemaps;
using SSRP.Core;

namespace SSRP.Arena
{
    /// <summary>
    /// [D4] Instantiates an arena prefab under ArenaRoot, reads its ArenaAnchors and publishes the
    /// ArenaBounds (the GameManager hands them to the paddles). Unloads cleanly.
    /// </summary>
    public class ArenaLoader : MonoBehaviour
    {
        [SerializeField, Tooltip("Parent for the arena instance (defaults to this transform).")]
        private Transform arenaRoot;

        private GameObject instance;

        public ArenaDefinition Current { get; private set; }
        public ArenaAnchors Anchors { get; private set; }
        public ArenaBounds Bounds { get; private set; }
        public bool IsLoaded => instance != null && Anchors != null;

        public event Action<ArenaBounds> BoundsPublished;

        public ArenaAnchors Load(ArenaDefinition definition)
        {
            Unload();

            if (definition == null || definition.arenaPrefab == null)
            {
                Debug.LogError("ArenaLoader: arena definition or prefab missing.", this);
                return null;
            }

            Transform parent = arenaRoot != null ? arenaRoot : transform;
            instance = Instantiate(definition.arenaPrefab, parent);
            instance.name = "Arena - " + definition.displayName;

            Anchors = instance.GetComponentInChildren<ArenaAnchors>();

            if (Anchors == null)
            {
                Debug.LogError($"ArenaLoader: prefab '{definition.arenaPrefab.name}' has no ArenaAnchors.", this);
                Unload();
                return null;
            }

            Current = definition;
            Bounds = Anchors.Bounds;
            BoundsPublished?.Invoke(Bounds);
            return Anchors;
        }

        /// <summary>
        /// Removes every tile whose cell lies within <paramref name="radius"/> of <paramref name="centre"/>
        /// (on the solid tilemaps of the loaded arena) and returns how many were removed. Used to keep the ball
        /// spawn clear: a ball that starts inside a tile collider gets stuck.
        /// </summary>
        public int ClearTilesAround(Vector2 centre, float radius)
        {
            if (instance == null)
                return 0;

            int removed = 0;

            foreach (Tilemap map in instance.GetComponentsInChildren<Tilemap>())
            {
                // Only solid tilemaps can trap the ball; trigger zones (speed zones, pickups) are fine.
                if (!map.TryGetComponent(out TilemapCollider2D collider) || collider.isTrigger)
                    continue;

                Vector3Int min = map.WorldToCell(centre - Vector2.one * radius);
                Vector3Int max = map.WorldToCell(centre + Vector2.one * radius);
                float reach = radius + map.cellSize.x * 0.71f;   // half the cell diagonal

                for (int x = min.x; x <= max.x; x++)
                {
                    for (int y = min.y; y <= max.y; y++)
                    {
                        var cell = new Vector3Int(x, y, 0);

                        if (!map.HasTile(cell))
                            continue;

                        Vector2 cellCentre = map.GetCellCenterWorld(cell);

                        if ((cellCentre - centre).magnitude > reach)
                            continue;

                        map.SetTile(cell, null);
                        removed++;
                    }
                }
            }

            return removed;
        }

        public void Unload()
        {
            if (instance != null)
                Destroy(instance);

            instance = null;
            Anchors = null;
            Current = null;
        }
    }
}
