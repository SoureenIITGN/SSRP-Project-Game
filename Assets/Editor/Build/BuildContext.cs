using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SSRP.Arena;
using SSRP.Audio;
using SSRP.Core;
using SSRP.Gameplay;
using SSRP.Input;

namespace SSRP.EditorTools
{
    /// <summary>Everything the builder creates, passed between the build steps.</summary>
    internal class BuildContext
    {
        // Art
        public Sprite Square, Circle, SoftDot;
        public Sprite TileSolid, TileBreakable, TileCracked, TileBounce, TileSpeed, TileSlowMo;
        public Sprite ThumbClassic, ThumbBunker, ThumbPillars;
        public Material SpriteMaterial, ParticleMaterial;
        public PhysicsMaterial2D Bouncy;

        // Configs
        public PaddleConfig PaddleConfig;
        public BallConfig BallSlow, BallNormal, BallFast;
        public MatchConfig MatchConfig;
        public SerialConfig SerialConfig;
        public ParryConfig ParryConfig;
        public AudioConfig AudioConfig;

        // Tiles
        public GameplayTile Solid, Breakable, BreakableCracked, BouncePad, SpeedZone, SlowMo;

        // Prefabs
        public PaddleController PaddlePrefab;
        public BallController BallPrefab;
        public GameObject ServicesPrefab;
        public GameObject HudPrefab, FlowUiPrefab, SerialPanelPrefab, CalibrationPanelPrefab;
        public ParticleSystem TileBurstPrefab;
        public readonly List<ArenaDefinition> Arenas = new List<ArenaDefinition>();
        public ArenaRegistry ArenaRegistry;
        public GameObject ArenaClassicPrefab, ArenaBunkerPrefab;

        public readonly List<string> Report = new List<string>();

        public void Log(string line)
        {
            Report.Add(line);
            Debug.Log("[SSRP Builder] " + line);
        }
    }

    /// <summary>Art import, materials and ScriptableObject configs.</summary>
    internal static class ConfigFactory
    {
        public static void ImportArt(BuildContext c)
        {
            c.Square = BuildUtil.ImportSprite("square", 32f);
            c.Circle = BuildUtil.ImportSprite("circle", 128f);
            c.SoftDot = BuildUtil.ImportSprite("soft_dot", 64f);
            c.TileSolid = BuildUtil.ImportSprite("tile_solid", 64f, FilterMode.Point);
            c.TileBreakable = BuildUtil.ImportSprite("tile_breakable", 64f, FilterMode.Point);
            c.TileCracked = BuildUtil.ImportSprite("tile_breakable_cracked", 64f, FilterMode.Point);
            c.TileBounce = BuildUtil.ImportSprite("tile_bounce", 64f, FilterMode.Point);
            c.TileSpeed = BuildUtil.ImportSprite("tile_speed", 64f, FilterMode.Point);
            c.TileSlowMo = BuildUtil.ImportSprite("tile_slowmo", 64f, FilterMode.Point);
            c.ThumbClassic = BuildUtil.ImportSprite("arena_classic", 100f);
            c.ThumbBunker = BuildUtil.ImportSprite("arena_bunker", 100f);
            c.ThumbPillars = BuildUtil.ImportSprite("arena_pillars", 100f);

            c.SpriteMaterial = BuildUtil.CreateSpriteMaterial("SpriteUnlit");
            c.ParticleMaterial = BuildUtil.CreateSpriteMaterial("ParticleSoft",
                c.SoftDot != null ? c.SoftDot.texture : null);

            string bouncyPath = $"{BuildUtil.PhysicsFolder}/Bouncy.physicsMaterial2D";
            BuildUtil.EnsureFolder(BuildUtil.PhysicsFolder);
            c.Bouncy = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(bouncyPath);

            if (c.Bouncy == null)
            {
                c.Bouncy = new PhysicsMaterial2D("Bouncy");
                AssetDatabase.CreateAsset(c.Bouncy, bouncyPath);
            }

            c.Bouncy.bounciness = 1f;
            c.Bouncy.friction = 0f;
            EditorUtility.SetDirty(c.Bouncy);
            c.Log("Art imported (sprites, unlit materials, Bouncy physics material).");
        }

        public static void CreateConfigs(BuildContext c)
        {
            c.PaddleConfig = BuildUtil.LoadOrCreate<PaddleConfig>($"{BuildUtil.SoCore}/PaddleConfig.asset");
            c.PaddleConfig.maxSpeed = 9f;
            c.PaddleConfig.height = 2f;
            c.PaddleConfig.width = 0.3f;
            c.PaddleConfig.deadzone = 0.05f;
            c.PaddleConfig.acceleration = 0f;
            BuildUtil.Save(c.PaddleConfig);

            c.BallSlow = Ball("Ball_Slow", "Slow", 6f, 1.03f, 1.6f);
            c.BallNormal = Ball("Ball_Normal", "Normal", 8f, 1.04f, 1.8f);
            c.BallFast = Ball("Ball_Fast", "Fast", 10.5f, 1.05f, 1.9f);

            c.MatchConfig = BuildUtil.LoadOrCreate<MatchConfig>($"{BuildUtil.SoCore}/DefaultMatchConfig.asset");
            c.MatchConfig.targetScore = 5;
            c.MatchConfig.ballConfig = c.BallNormal;
            c.MatchConfig.paddleConfig = c.PaddleConfig;
            c.MatchConfig.arenaId = "classic";
            c.MatchConfig.countdownSeconds = 3f;
            c.MatchConfig.pointScoredPause = 1f;
            c.MatchConfig.players = new List<PlayerDefinition>
            {
                new PlayerDefinition
                {
                    playerId = 1, displayName = "Player 1", colour = new Color(0.25f, 0.8f, 1f),
                    side = PaddleSide.Left, inputSource = InputSourceType.Keyboard
                },
                new PlayerDefinition
                {
                    playerId = 2, displayName = "Player 2", colour = new Color(1f, 0.45f, 0.3f),
                    side = PaddleSide.Right, inputSource = InputSourceType.Keyboard
                }
            };
            BuildUtil.Save(c.MatchConfig);

            c.SerialConfig = BuildUtil.LoadOrCreate<SerialConfig>($"{BuildUtil.SoInput}/SerialConfig.asset");
            BuildUtil.Save(c.SerialConfig);

            c.ParryConfig = BuildUtil.LoadOrCreate<ParryConfig>($"{BuildUtil.SoGameplay}/ParryConfig.asset");
            BuildUtil.Save(c.ParryConfig);

            c.AudioConfig = BuildUtil.LoadOrCreate<AudioConfig>($"{BuildUtil.SoAudio}/AudioConfig.asset");
            c.AudioConfig.paddleHit = Clip("paddle_hit");
            c.AudioConfig.wallHit = Clip("wall_hit");
            c.AudioConfig.tileHit = Clip("tile_hit");
            c.AudioConfig.tileBreak = Clip("tile_break");
            c.AudioConfig.goal = Clip("goal");
            c.AudioConfig.parry = Clip("parry");
            c.AudioConfig.countdownTick = Clip("countdown_tick");
            c.AudioConfig.countdownGo = Clip("countdown_go");
            c.AudioConfig.matchOver = Clip("match_over");
            BuildUtil.Save(c.AudioConfig);

            c.Log("Configs: PaddleConfig, Ball_Slow/Normal/Fast, DefaultMatchConfig, SerialConfig, ParryConfig, AudioConfig.");
        }

        private static BallConfig Ball(string file, string presetName, float speed, float ramp, float cap)
        {
            BallConfig b = BuildUtil.LoadOrCreate<BallConfig>($"{BuildUtil.SoCore}/{file}.asset");
            b.presetName = presetName;
            b.speed = speed;
            b.hitSpeedRamp = ramp;
            b.maxSpeedMultiplier = cap;
            b.maxBounceAngle = 60f;
            b.minimumHorizontalDirection = 0.25f;
            b.paddleVelocityInfluence = 0.15f;
            b.serveAngleRange = 25f;
            BuildUtil.Save(b);
            return b;
        }

        private static AudioClip Clip(string name)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{BuildUtil.Sfx}/{name}.wav");

            if (clip == null)
                Debug.LogWarning($"[SSRP Builder] Missing sound {name}.wav. Run: python tools/generate_sfx.py");

            return clip;
        }
    }
}
