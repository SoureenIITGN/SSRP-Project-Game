using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SSRP.Flow;
using SSRP.Gameplay;

namespace SSRP.EditorTools
{
    /// <summary>
    /// SSRP > Build Project: generates every Unity asset the issues need (configs, tiles, palette,
    /// prefabs, arenas, scenes, Build Settings) from code, so the repository can be rebuilt
    /// identically on any machine. Safe to run again: existing assets are updated in place and keep
    /// their GUIDs. Only one person needs to run it and commit the result.
    /// </summary>
    public static class SSRPProjectBuilder
    {
        private const string FirstRunKey = "SSRP.Builder.AskedThisSession";

        [MenuItem("SSRP/Build Project", priority = 0)]
        public static void BuildProject()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var c = new BuildContext();

            try
            {
                EditorUtility.DisplayProgressBar("SSRP Build", "Project settings", 0.05f);
                ApplyProjectSettings(c);

                EditorUtility.DisplayProgressBar("SSRP Build", "Art + configs", 0.15f);
                ConfigFactory.ImportArt(c);
                ConfigFactory.CreateConfigs(c);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayProgressBar("SSRP Build", "Tiles", 0.3f);
                ArenaFactory.CreateTiles(c);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayProgressBar("SSRP Build", "Prefabs", 0.45f);
                PrefabFactory.CreateAll(c);

                EditorUtility.DisplayProgressBar("SSRP Build", "Arenas", 0.6f);
                ArenaFactory.CreateArenas(c);
                ArenaFactory.CreatePalette(c);

                EditorUtility.DisplayProgressBar("SSRP Build", "Game catalog", 0.7f);
                CreateCatalog(c);
                AssetDatabase.SaveAssets();

                EditorUtility.DisplayProgressBar("SSRP Build", "Scenes", 0.8f);
                SceneFactory.CreateAll(c);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorSceneManager.OpenScene(SceneFactory.BootPath);
            Debug.Log("[SSRP Builder] Done.\n - " + string.Join("\n - ", c.Report));
            EditorUtility.DisplayDialog("SSRP Build",
                "Project built.\n\nThe Boot scene is open: press Play.\n\nCommit the new files under Assets/ " +
                "(scenes, prefabs, ScriptableObjects, .meta files) so teammates get the same assets.", "OK");
        }

        [MenuItem("SSRP/Open Boot Scene", priority = 20)]
        public static void OpenBoot()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(SceneFactory.BootPath);
        }

        [MenuItem("SSRP/Open Game Scene", priority = 21)]
        public static void OpenGame()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(SceneFactory.GamePath);
        }

        [MenuItem("SSRP/Reset Saved Settings (PlayerPrefs)", priority = 40)]
        public static void ResetPrefs()
        {
            if (EditorUtility.DisplayDialog("Reset", "Delete all saved PlayerPrefs (match setup, serial port, volume)?", "Delete", "Cancel"))
                PlayerPrefs.DeleteAll();
        }

        /// <summary>Offer to build once per editor session when the catalog is missing (fresh clone).</summary>
        [InitializeOnLoadMethod]
        private static void OfferFirstBuild()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(FirstRunKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                SessionState.SetBool(FirstRunKey, true);

                if (AssetDatabase.LoadAssetAtPath<GameCatalog>($"{BuildUtil.ResourcesSsrp}/GameCatalog.asset") != null)
                    return;

                if (EditorUtility.DisplayDialog("SSRP Pong",
                        "The game's scenes, prefabs and assets have not been generated yet.\n\nRun SSRP > Build Project now?",
                        "Build now", "Later"))
                {
                    BuildProject();
                }
            };
        }

        // ------------------------------------------------------------------ steps

        private static readonly (int index, string name)[] Layers =
        {
            (6, PhysicsLayers.BallName),
            (7, PhysicsLayers.PaddleName),
            (8, PhysicsLayers.WallName),
            (9, PhysicsLayers.TileName),
            (10, PhysicsLayers.GoalName)
        };

        private static void ApplyProjectSettings(BuildContext c)
        {
            // Layers (also committed in TagManager.asset; this repairs them if someone removed one).
            Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");

            if (tagManagerAssets.Length > 0)
            {
                var tagManager = new SerializedObject(tagManagerAssets[0]);
                SerializedProperty layers = tagManager.FindProperty("layers");

                foreach ((int index, string name) in Layers)
                {
                    SerializedProperty layer = layers.GetArrayElementAtIndex(index);

                    if (string.IsNullOrEmpty(layer.stringValue))
                        layer.stringValue = name;
                    else if (layer.stringValue != name)
                        Debug.LogWarning($"[SSRP Builder] Layer {index} is '{layer.stringValue}', expected '{name}'.");
                }

                tagManager.ApplyModifiedPropertiesWithoutUndo();
            }

            // Physics 2D (docs/physics.md).
            Physics2D.gravity = Vector2.zero;
            Physics2D.bounceThreshold = 0.01f;

            int ball = LayerMask.NameToLayer(PhysicsLayers.BallName);
            var others = new List<int>();
            foreach ((int index, string name) in Layers)
                others.Add(LayerMask.NameToLayer(name));

            foreach (int a in others)
            {
                foreach (int b in others)
                {
                    if (a < 0 || b < 0)
                        continue;

                    bool collide = (a == ball) != (b == ball);   // only ball-vs-other pairs collide
                    Physics2D.IgnoreLayerCollision(a, b, !collide);
                }
            }

            Time.fixedDeltaTime = 0.01f;

            // Player: input handling + serial API level are committed in ProjectSettings.asset.
            c.Log("Project settings: layers, Physics2D matrix, gravity 0, bounce threshold 0.01, fixed timestep 0.01.");
        }

        private static void CreateCatalog(BuildContext c)
        {
            GameCatalog catalog = BuildUtil.LoadOrCreate<GameCatalog>($"{BuildUtil.ResourcesSsrp}/GameCatalog.asset");
            catalog.defaultMatchConfig = c.MatchConfig;
            catalog.ballPresets = new List<SSRP.Core.BallConfig> { c.BallSlow, c.BallNormal, c.BallFast };
            catalog.arenaRegistry = c.ArenaRegistry;
            catalog.servicesPrefab = c.ServicesPrefab;
            catalog.targetScoreOptions = new List<int> { 5, 7, 11 };
            BuildUtil.Save(catalog);
            c.Log("GameCatalog (Resources/SSRP) links config, ball presets, arenas and services.");
        }
    }
}
