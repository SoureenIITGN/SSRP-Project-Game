using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using SSRP.Arena;
using SSRP.Core;
using SSRP.Gameplay;
using Object = UnityEngine.Object;

namespace SSRP.EditorTools
{
    /// <summary>
    /// Tile behaviours + tiles, the tile palette, the arena prefabs (01 Classic, 02 Bunker, 03 Pillars),
    /// their ArenaDefinitions and the ArenaRegistry. Arena 03 is the [D7] proof: it uses only the
    /// generic pieces (prefab layout, tiles, definition, registry entry) and no new scripts.
    /// </summary>
    internal static class ArenaFactory
    {
        private const float Cell = 0.5f;
        private const float HalfWidth = 9f;
        private const float HalfHeight = 5f;

        private class Layout
        {
            public readonly List<(Vector2 min, Vector2 max)> Solid = new List<(Vector2, Vector2)>();
            public readonly List<(Vector2 min, Vector2 max, GameplayTile tile)> Interactive =
                new List<(Vector2, Vector2, GameplayTile)>();
            public readonly List<(Vector2 min, Vector2 max, GameplayTile tile)> Zones =
                new List<(Vector2, Vector2, GameplayTile)>();
        }

        public static void CreateTiles(BuildContext c)
        {
            string b = BuildUtil.SoTileBehaviours;
            SolidBehaviour solid = BuildUtil.LoadOrCreate<SolidBehaviour>($"{b}/SolidBehaviour.asset");
            BreakableBehaviour breakable = BuildUtil.LoadOrCreate<BreakableBehaviour>($"{b}/BreakableBehaviour.asset");
            BouncePadBehaviour bounce = BuildUtil.LoadOrCreate<BouncePadBehaviour>($"{b}/BouncePadBehaviour.asset");
            SpeedZoneBehaviour speed = BuildUtil.LoadOrCreate<SpeedZoneBehaviour>($"{b}/SpeedZoneBehaviour.asset");
            SlowMoPickupBehaviour slow = BuildUtil.LoadOrCreate<SlowMoPickupBehaviour>($"{b}/SlowMoPickupBehaviour.asset");

            c.Solid = Tile("Tile_Solid", c.TileSolid, solid, 1);
            c.BreakableCracked = Tile("Tile_BreakableCracked", c.TileCracked, breakable, 1);
            c.Breakable = Tile("Tile_Breakable", c.TileBreakable, breakable, 2);
            c.BouncePad = Tile("Tile_BouncePad", c.TileBounce, bounce, 1);
            c.SpeedZone = Tile("Tile_SpeedZone", c.TileSpeed, speed, 1);
            c.SlowMo = Tile("Tile_SlowMoPickup", c.TileSlowMo, slow, 1);

            breakable.damagedTile = c.BreakableCracked;
            BuildUtil.Save(breakable);
            BuildUtil.Save(solid);
            BuildUtil.Save(bounce);
            BuildUtil.Save(speed);
            BuildUtil.Save(slow);
            c.Log("Tiles: Solid, Breakable (+cracked), BouncePad, SpeedZone, SlowMoPickup.");
        }

        private static GameplayTile Tile(string name, Sprite sprite, TileBehaviour behaviour, int hp)
        {
            GameplayTile t = BuildUtil.LoadOrCreate<GameplayTile>($"{BuildUtil.SoTiles}/{name}.asset");
            t.sprite = sprite;
            t.color = Color.white;
            t.colliderType = UnityEngine.Tilemaps.Tile.ColliderType.Grid;
            t.behaviour = behaviour;
            t.hitPoints = hp;
            BuildUtil.Save(t);
            return t;
        }

        public static void CreateArenas(BuildContext c)
        {
            // Arena 01 - Classic: walls and goals only.
            var classic = new Layout();
            c.ArenaClassicPrefab = BuildArena(c, "Arena01_Classic", classic);

            // Arena 02 - Bunker: breakable centre wall, solid corner blocks, bounce pads.
            var bunker = new Layout();
            bunker.Interactive.Add((new Vector2(-0.5f, -3f), new Vector2(0.5f, 3f), c.Breakable));
            foreach (float sx in new[] { -1f, 1f })
            {
                float x0 = sx > 0 ? 4f : -5f;
                bunker.Solid.Add((new Vector2(x0, 2.5f), new Vector2(x0 + 1f, 3.5f)));
                bunker.Solid.Add((new Vector2(x0, -3.5f), new Vector2(x0 + 1f, -2.5f)));
                bunker.Interactive.Add((new Vector2(sx * 4.5f - 0.5f, -0.5f), new Vector2(sx * 4.5f + 0.5f, 0.5f), c.BouncePad));
            }
            c.ArenaBunkerPrefab = BuildArena(c, "Arena02_Bunker", bunker);

            // Arena 03 - Pillars ([D7] assets-only arena + [B6] extension tiles).
            var pillars = new Layout();
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sy in new[] { -1f, 1f })
                {
                    float x0 = sx > 0 ? 2.5f : -3.5f;
                    float y0 = sy > 0 ? 1f : -2.5f;
                    pillars.Solid.Add((new Vector2(x0, y0), new Vector2(x0 + 1f, y0 + 1.5f)));
                    pillars.Interactive.Add((new Vector2(sx * 6f - 0.5f, sy * 4f - 0.5f),
                        new Vector2(sx * 6f + 0.5f, sy * 4f + 0.5f), c.BouncePad));
                }
            }
            pillars.Zones.Add((new Vector2(-0.5f, -1.5f), new Vector2(0.5f, 1.5f), c.SpeedZone));
            pillars.Zones.Add((new Vector2(-0.5f, 3f), new Vector2(0.5f, 3.5f), c.SlowMo));
            pillars.Zones.Add((new Vector2(-0.5f, -3.5f), new Vector2(0.5f, -3f), c.SlowMo));
            GameObject pillarsPrefab = BuildArena(c, "Arena03_Pillars", pillars);

            c.Arenas.Clear();
            c.Arenas.Add(Definition("Arena01_Classic", "classic", "Classic",
                "Plain Pong: two walls, two goals. Best for tuning filters.", c.ThumbClassic, c.ArenaClassicPrefab));
            c.Arenas.Add(Definition("Arena02_Bunker", "bunker", "Bunker",
                "A breakable brick wall splits the court. Corner blocks and bounce pads.", c.ThumbBunker, c.ArenaBunkerPrefab));
            c.Arenas.Add(Definition("Arena03_Pillars", "pillars", "Pillars",
                "Four pillars, a speed zone through the middle and slow-mo pickups.", c.ThumbPillars, pillarsPrefab));

            c.ArenaRegistry = BuildUtil.LoadOrCreate<ArenaRegistry>($"{BuildUtil.SoArena}/ArenaRegistry.asset");
            c.ArenaRegistry.arenas = new List<ArenaDefinition>(c.Arenas);
            BuildUtil.Save(c.ArenaRegistry);
            c.Log("Arenas: Classic, Bunker, Pillars (+ definitions and registry).");
        }

        private static ArenaDefinition Definition(string file, string id, string displayName, string description,
            Sprite thumbnail, GameObject prefab)
        {
            ArenaDefinition d = BuildUtil.LoadOrCreate<ArenaDefinition>($"{BuildUtil.SoArena}/{file}.asset");
            d.id = id;
            d.displayName = displayName;
            d.description = description;
            d.thumbnail = thumbnail;
            d.arenaPrefab = prefab;
            BuildUtil.Save(d);
            return d;
        }

        private static GameObject BuildArena(BuildContext c, string name, Layout layout)
        {
            var root = new GameObject(name);
            ArenaAnchors anchors = root.AddComponent<ArenaAnchors>();

            // Field background + centre line (decoration only).
            GameObject field = BuildUtil.Child(root, "Field", new Vector3(0f, 0f, 1f));
            BuildUtil.AddSprite(field, c.Square, c.SpriteMaterial, BuildUtil.Field, -10);
            field.transform.localScale = new Vector3(HalfWidth * 2f + 1f, HalfHeight * 2f, 1f);

            GameObject line = BuildUtil.Child(root, "CentreLine", Vector3.zero);
            for (float y = -HalfHeight + 0.25f; y < HalfHeight; y += 1f)
            {
                GameObject dash = BuildUtil.Child(line, "Dash", new Vector3(0f, y + 0.25f, 0f));
                BuildUtil.AddSprite(dash, c.Square, c.SpriteMaterial, BuildUtil.LineColour, -9);
                dash.transform.localScale = new Vector3(0.1f, 0.5f, 1f);
            }

            // Walls (top/bottom).
            GameObject walls = BuildUtil.Child(root, "Walls", Vector3.zero);
            Wall(c, walls, "TopWall", new Vector2(0f, HalfHeight + 0.25f), new Vector2(HalfWidth * 2f + 1.5f, 0.5f));
            Wall(c, walls, "BottomWall", new Vector2(0f, -HalfHeight - 0.25f), new Vector2(HalfWidth * 2f + 1.5f, 0.5f));

            // Goals just outside the playable area.
            GameObject goals = BuildUtil.Child(root, "Goals", Vector3.zero);
            GoalZone left = Goal(goals, "LeftGoal", new Vector2(-HalfWidth - 1.25f, 0f), PaddleSide.Left, 2);
            GoalZone right = Goal(goals, "RightGoal", new Vector2(HalfWidth + 1.25f, 0f), PaddleSide.Right, 1);

            // Spawn points.
            GameObject spawns = BuildUtil.Child(root, "Spawns", Vector3.zero);
            GameObject leftSpawn = BuildUtil.Child(spawns, "LeftPaddleSpawn", new Vector3(-HalfWidth + 0.7f, 0f, 0f));
            GameObject rightSpawn = BuildUtil.Child(spawns, "RightPaddleSpawn", new Vector3(HalfWidth - 0.7f, 0f, 0f));
            GameObject ballSpawn = BuildUtil.Child(spawns, "BallSpawn", Vector3.zero);

            BuildUtil.SetVector2(anchors, "boundsMin", new Vector2(-HalfWidth, -HalfHeight));
            BuildUtil.SetVector2(anchors, "boundsMax", new Vector2(HalfWidth, HalfHeight));
            BuildUtil.SetFloat(anchors, "cameraMargin", 1.3f);
            BuildUtil.Set(anchors, "leftPaddleSpawn", leftSpawn.transform);
            BuildUtil.Set(anchors, "rightPaddleSpawn", rightSpawn.transform);
            BuildUtil.Set(anchors, "ballSpawn", ballSpawn.transform);
            BuildUtil.Set(anchors, "leftGoal", left);
            BuildUtil.Set(anchors, "rightGoal", right);

            // Tilemaps.
            GameObject gridGo = BuildUtil.Child(root, "Grid", Vector3.zero);
            Grid grid = gridGo.AddComponent<Grid>();
            grid.cellSize = new Vector3(Cell, Cell, 0f);

            Tilemap solid = TilemapLayer(c, gridGo, "Solid", composite: true, trigger: false, order: 1);
            Tilemap interactive = TilemapLayer(c, gridGo, "Interactive", composite: false, trigger: false, order: 2);
            Tilemap zones = TilemapLayer(c, gridGo, "Zones", composite: false, trigger: true, order: 0);

            foreach (var r in layout.Solid)
                Fill(solid, c.Solid, r.min, r.max);

            foreach (var r in layout.Interactive)
                Fill(interactive, r.tile, r.min, r.max);

            foreach (var r in layout.Zones)
                Fill(zones, r.tile, r.min, r.max);

            return BuildUtil.SavePrefab(root, $"{BuildUtil.PrefabsArena}/{name}.prefab");
        }

        private static void Wall(BuildContext c, GameObject parent, string name, Vector2 position, Vector2 size)
        {
            GameObject wall = BuildUtil.Child(parent, name, position);
            BuildUtil.AssignLayer(wall, PhysicsLayers.WallName);
            BuildUtil.AddSprite(wall, c.Square, c.SpriteMaterial, BuildUtil.WallColour, 0);
            wall.transform.localScale = new Vector3(size.x, size.y, 1f);
            BoxCollider2D box = wall.AddComponent<BoxCollider2D>();
            box.size = Vector2.one;
            box.sharedMaterial = c.Bouncy;
        }

        private static GoalZone Goal(GameObject parent, string name, Vector2 position, PaddleSide defended, int scorer)
        {
            GameObject go = BuildUtil.Child(parent, name, position);
            BuildUtil.AssignLayer(go, PhysicsLayers.GoalName);
            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.5f, HalfHeight * 2f + 4f);
            GoalZone goal = go.AddComponent<GoalZone>();
            BuildUtil.SetEnum(goal, "defendedSide", (int)defended);
            BuildUtil.SetInt(goal, "scoringPlayerId", scorer);
            return goal;
        }

        private static Tilemap TilemapLayer(BuildContext c, GameObject grid, string name, bool composite, bool trigger, int order)
        {
            GameObject go = BuildUtil.Child(grid, name, Vector3.zero);
            BuildUtil.AssignLayer(go, PhysicsLayers.TileName);

            Tilemap map = go.AddComponent<Tilemap>();
            TilemapRenderer renderer = go.AddComponent<TilemapRenderer>();
            renderer.sharedMaterial = c.SpriteMaterial;
            renderer.sortingOrder = order;

            if (composite)
            {
                Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Static;
            }

            TilemapCollider2D collider = go.AddComponent<TilemapCollider2D>();
            collider.isTrigger = trigger;
            collider.sharedMaterial = c.Bouncy;

            if (composite)
            {
                collider.compositeOperation = Collider2D.CompositeOperation.Merge;
                CompositeCollider2D comp = go.AddComponent<CompositeCollider2D>();
                comp.geometryType = CompositeCollider2D.GeometryType.Polygons;
                comp.sharedMaterial = c.Bouncy;
            }

            go.AddComponent<SSRP.Gameplay.TileHitResolver>();
            return map;
        }

        private static void Fill(Tilemap map, TileBase tile, Vector2 worldMin, Vector2 worldMax)
        {
            // Cell (x, y) covers world [x*Cell, (x+1)*Cell). Rectangles are given in world units.
            int x0 = Mathf.FloorToInt(worldMin.x / Cell + 0.001f);
            int x1 = Mathf.CeilToInt(worldMax.x / Cell - 0.001f);
            int y0 = Mathf.FloorToInt(worldMin.y / Cell + 0.001f);
            int y1 = Mathf.CeilToInt(worldMax.y / Cell - 0.001f);

            for (int x = x0; x < x1; x++)
                for (int y = y0; y < y1; y++)
                    map.SetTile(new Vector3Int(x, y, 0), tile);
        }

        /// <summary>Tile palette 'SSRP Tiles' for the Tile Palette window (reflection: the class is editor-internal in some versions).</summary>
        public static void CreatePalette(BuildContext c)
        {
            string path = $"{BuildUtil.PrefabsArena}/Palette/SSRP Tiles.prefab";
            BuildUtil.EnsureFolder($"{BuildUtil.PrefabsArena}/Palette");

            var root = new GameObject("SSRP Tiles");
            Grid grid = root.AddComponent<Grid>();
            grid.cellSize = new Vector3(Cell, Cell, 0f);
            GameObject layer = BuildUtil.Child(root, "Layer1", Vector3.zero);
            Tilemap map = layer.AddComponent<Tilemap>();
            layer.AddComponent<TilemapRenderer>();

            GameplayTile[] tiles = { c.Solid, c.Breakable, c.BreakableCracked, c.BouncePad, c.SpeedZone, c.SlowMo };
            for (int i = 0; i < tiles.Length; i++)
                map.SetTile(new Vector3Int(i, 0, 0), tiles[i]);

            GameObject prefab = BuildUtil.SavePrefab(root, path);

            Type paletteType = null;
            foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                paletteType = asm.GetType("UnityEditor.GridPalette") ?? asm.GetType("UnityEditor.Tilemaps.GridPalette");
                if (paletteType != null)
                    break;
            }

            if (paletteType == null)
            {
                c.Log("Tile palette prefab created; open it via Window > 2D > Tile Palette > Create New Palette if it is not listed.");
                return;
            }

            bool hasPaletteAsset = false;
            foreach (Object sub in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (sub != null && paletteType.IsInstanceOfType(sub))
                    hasPaletteAsset = true;
            }

            if (!hasPaletteAsset)
            {
                ScriptableObject palette = ScriptableObject.CreateInstance(paletteType);
                palette.name = "Palette Settings";
                AssetDatabase.AddObjectToAsset(palette, prefab);
                AssetDatabase.ImportAsset(path);
            }

            c.Log("Tile palette 'SSRP Tiles' created (Window > 2D > Tile Palette).");
        }
    }
}
