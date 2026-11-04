using System;
using UnityEngine;
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
