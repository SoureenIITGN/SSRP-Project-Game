using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using SSRP.Arena;
using SSRP.Audio;
using SSRP.Core;
using SSRP.Flow;
using SSRP.Gameplay;
using SSRP.Input;
using SSRP.UI;

namespace SSRP.EditorTools
{
    /// <summary>Boot, MainMenu, Game and the development scenes (only the builder writes these files).</summary>
    internal static class SceneFactory
    {
        public static string BootPath => $"{BuildUtil.Scenes}/{SceneNames.Boot}.unity";
        public static string MainMenuPath => $"{BuildUtil.Scenes}/{SceneNames.MainMenu}.unity";
        public static string GamePath => $"{BuildUtil.Scenes}/{SceneNames.Game}.unity";
        public static string SandboxPath => $"{BuildUtil.Scenes}/Gameplay/{SceneNames.DevPhysicsSandbox}.unity";
        public static string TileArenaPath => $"{BuildUtil.Scenes}/Gameplay/{SceneNames.DevTileArena}.unity";
        public static string InputPath => $"{BuildUtil.Scenes}/Input/{SceneNames.DevInputPipeline}.unity";
        public static string SerialPath => $"{BuildUtil.Scenes}/Input/{SceneNames.DevSerialPanel}.unity";

        public static void CreateAll(BuildContext c)
        {
            Boot();
            MainMenu();
            Game(c);
            PhysicsSandbox(c);
            TileArena(c);
            InputPipeline(c);
            SerialPanel(c);
            SetBuildSettings();
            c.Log("Scenes: Boot, MainMenu, Game, Dev_PhysicsSandbox, Dev_TileArena, Dev_InputPipeline, Dev_SerialPanel; Build Settings updated.");
        }

        // ------------------------------------------------------------------ helpers

        private static Scene NewScene()
        {
            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void SaveScene(Scene scene, string path)
        {
            BuildUtil.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            EditorSceneManager.SaveScene(scene, path);
        }

        private static Camera MakeCamera(float size, bool listener = true)
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            go.transform.position = new Vector3(0f, 0f, -10f);
            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BuildUtil.Background;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 50f;

            if (listener)
                go.AddComponent<AudioListener>();

            return cam;
        }

        private static void MakeEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject Spawn(GameObject prefab, string name = null, Transform parent = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);

            if (name != null)
                go.name = name;

            if (parent != null)
                go.transform.SetParent(parent, false);

            return go;
        }

        private static void Wall(BuildContext c, string name, Vector2 pos, Vector2 size)
        {
            var wall = new GameObject(name);
            wall.transform.position = pos;
            wall.transform.localScale = new Vector3(size.x, size.y, 1f);
            BuildUtil.AssignLayer(wall, PhysicsLayers.WallName);
            BuildUtil.AddSprite(wall, c.Square, c.SpriteMaterial, BuildUtil.WallColour, 0);
            BoxCollider2D box = wall.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
            box.sharedMaterial = c.Bouncy;
        }

        private static PaddleController DevPaddle(BuildContext c, int id, string displayName, Color colour,
            PaddleSide side, InputSourceType source, float x)
        {
            GameObject go = Spawn(c.PaddlePrefab.gameObject, "Paddle - " + displayName);
            go.transform.position = new Vector3(x, 0f, 0f);
            PlayerSlot slot = go.GetComponent<PlayerSlot>();
            BuildUtil.SetInt(slot, "playerId", id);
            BuildUtil.SetEnum(slot, "inputSourceType", (int)source);
            BuildUtil.SetString(slot, "displayName", displayName);
            BuildUtil.SetColor(slot, "colour", colour);
            BuildUtil.SetEnum(slot, "side", (int)side);
            go.GetComponent<SpriteRenderer>().color = colour;
            return go.GetComponent<PaddleController>();
        }

        // ------------------------------------------------------------------ scenes

        private static void Boot()
        {
            Scene scene = NewScene();
            MakeCamera(5f);
            new GameObject("Bootstrapper").AddComponent<Bootstrapper>();
            SaveScene(scene, BootPath);
        }

        private static void MainMenu()
        {
            Scene scene = NewScene();
            MakeCamera(5f);
            MakeEventSystem();

            var menu = new GameObject("MainMenu", typeof(RectTransform));
            MainMenuController controller = menu.AddComponent<MainMenuController>();

            var input = new GameObject("MenuInput");
            input.AddComponent<MenuNavigator>();
            var fsr = new GameObject("FsrNavigation");
            fsr.transform.SetParent(input.transform, false);
            PlayerSlot slot = fsr.AddComponent<PlayerSlot>();
            PlayerInputPipeline pipeline = fsr.AddComponent<PlayerInputPipeline>();
            BuildUtil.Set(pipeline, "slot", slot);
            FsrMenuNavigationSource nav = fsr.AddComponent<FsrMenuNavigationSource>();
            BuildUtil.Set(controller, "fsrNavigation", nav);

            SaveScene(scene, MainMenuPath);
        }

        private static void Game(BuildContext c)
        {
            Scene scene = NewScene();
            Camera cam = MakeCamera(7f);
            cam.gameObject.AddComponent<ArenaCameraFitter>();
            FeedbackManager feedback = cam.gameObject.AddComponent<FeedbackManager>();
            BuildUtil.Set(feedback, "shakeTarget", cam.transform);
            BuildUtil.Set(feedback, "tileBurstPrefab", c.TileBurstPrefab);
            MakeEventSystem();

            var arenaRoot = new GameObject("ArenaRoot");
            ArenaLoader loader = arenaRoot.AddComponent<ArenaLoader>();
            BuildUtil.Set(loader, "arenaRoot", arenaRoot.transform);

            var players = new GameObject("Players");
            PlayerSpawner spawner = players.AddComponent<PlayerSpawner>();
            BuildUtil.Set(spawner, "paddlePrefab", c.PaddlePrefab);
            BuildUtil.Set(spawner, "paddleRoot", players.transform);

            var ballRoot = new GameObject("BallRoot");

            var managerGo = new GameObject("GameManager");
            GameManager manager = managerGo.AddComponent<GameManager>();
            BuildUtil.Set(manager, "arenaLoader", loader);
            BuildUtil.Set(manager, "playerSpawner", spawner);
            BuildUtil.Set(manager, "ballPrefab", c.BallPrefab);
            BuildUtil.Set(manager, "ballRoot", ballRoot.transform);
            BuildUtil.Set(manager, "cameraFitter", cam.GetComponent<ArenaCameraFitter>());

            Spawn(c.HudPrefab, "PlayerHud");
            Spawn(c.FlowUiPrefab, "FlowUi");

            SaveScene(scene, GamePath);
        }

        private static void PhysicsSandbox(BuildContext c)
        {
            Scene scene = NewScene();
            MakeCamera(6f);

            Wall(c, "TopWall", new Vector2(0f, 4.75f), new Vector2(17f, 0.5f));
            Wall(c, "BottomWall", new Vector2(0f, -4.75f), new Vector2(17f, 0.5f));
            Wall(c, "LeftWall", new Vector2(-8.25f, 0f), new Vector2(0.5f, 10f));
            Wall(c, "RightWall", new Vector2(8.25f, 0f), new Vector2(0.5f, 10f));

            GameObject ball = Spawn(c.BallPrefab.gameObject, "Ball");
            var sandbox = new GameObject("Sandbox");
            SandboxServer server = sandbox.AddComponent<SandboxServer>();
            BuildUtil.Set(server, "ball", ball.GetComponent<BallController>());
            SpeedProbe probe = sandbox.AddComponent<SpeedProbe>();
            BuildUtil.Set(probe, "ball", ball.GetComponent<BallController>());

            SaveScene(scene, SandboxPath);
        }

        private static void TileArena(BuildContext c)
        {
            Scene scene = NewScene();
            MakeCamera(7f);

            Spawn(c.ArenaBunkerPrefab, "Arena (Bunker)");

            // Close the goal mouths so the ball keeps bouncing without a GameManager.
            Wall(c, "LeftBackstop", new Vector2(-10f, 0f), new Vector2(0.5f, 11f));
            Wall(c, "RightBackstop", new Vector2(10f, 0f), new Vector2(0.5f, 11f));

            GameObject ball = Spawn(c.BallPrefab.gameObject, "Ball");
            var sandbox = new GameObject("Sandbox");
            SandboxServer server = sandbox.AddComponent<SandboxServer>();
            BuildUtil.Set(server, "ball", ball.GetComponent<BallController>());
            SpeedProbe probe = sandbox.AddComponent<SpeedProbe>();
            BuildUtil.Set(probe, "ball", ball.GetComponent<BallController>());

            SaveScene(scene, TileArenaPath);
        }

        private static void InputPipeline(BuildContext c)
        {
            Scene scene = NewScene();
            MakeCamera(7f);
            MakeEventSystem();
            new GameObject("EnsureServices").AddComponent<EnsureServices>();

            PaddleController p1 = DevPaddle(c, 1, "Player 1 (keyboard)", new Color(0.25f, 0.8f, 1f),
                PaddleSide.Left, InputSourceType.Keyboard, -8.3f);
            PaddleController p2 = DevPaddle(c, 2, "Player 2 (noisy keyboard)", new Color(1f, 0.45f, 0.3f),
                PaddleSide.Right, InputSourceType.NoiseInjected, 8.3f);

            var bounds = new GameObject("DevArenaBounds");
            DevArenaBounds devBounds = bounds.AddComponent<DevArenaBounds>();
            BuildUtil.SetArray(devBounds, "paddles", new Object[] { p1, p2 });

            GameObject hud = Spawn(c.HudPrefab, "PlayerHud");
            BuildUtil.SetBool(hud.GetComponent<PlayerHud>(), "debugVisibleOnStart", true);

            SaveScene(scene, InputPath);
        }

        private static void SerialPanel(BuildContext c)
        {
            Scene scene = NewScene();
            MakeCamera(7f);
            MakeEventSystem();
            new GameObject("EnsureServices").AddComponent<EnsureServices>();

            var canvasGo = new GameObject("Canvas", typeof(RectTransform));
            UiFactory.SetupCanvas(canvasGo, 30);
            GameObject panel = Spawn(c.SerialPanelPrefab, "SerialConnectionPanel", canvasGo.transform);
            var rt = (RectTransform)panel.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 30f);
            rt.sizeDelta = new Vector2(820f, 250f);

            PaddleController p1 = DevPaddle(c, 1, "Player 1 (Arduino)", new Color(0.25f, 0.8f, 1f),
                PaddleSide.Left, InputSourceType.Serial, -8.3f);
            PaddleController p2 = DevPaddle(c, 2, "Player 2 (Arduino)", new Color(1f, 0.45f, 0.3f),
                PaddleSide.Right, InputSourceType.Serial, 8.3f);

            var bounds = new GameObject("DevArenaBounds");
            DevArenaBounds devBounds = bounds.AddComponent<DevArenaBounds>();
            BuildUtil.SetArray(devBounds, "paddles", new Object[] { p1, p2 });

            GameObject hud = Spawn(c.HudPrefab, "PlayerHud");
            BuildUtil.SetBool(hud.GetComponent<PlayerHud>(), "debugVisibleOnStart", true);

            SaveScene(scene, SerialPath);
        }

        private static void SetBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(BootPath, true),
                new EditorBuildSettingsScene(MainMenuPath, true),
                new EditorBuildSettingsScene(GamePath, true),
                new EditorBuildSettingsScene(SandboxPath, false),
                new EditorBuildSettingsScene(TileArenaPath, false),
                new EditorBuildSettingsScene(InputPath, false),
                new EditorBuildSettingsScene(SerialPath, false)
            };

            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
