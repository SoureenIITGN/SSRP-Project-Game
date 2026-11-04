using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SSRP.Gameplay
{
    /// <summary>Everything a TileBehaviour needs to react to a hit (a struct: no allocations).</summary>
    public readonly struct TileHitContext
    {
        public readonly TileHitResolver Resolver;
        public readonly Vector3Int Cell;
        public readonly GameplayTile Tile;
        public readonly BallController Ball;
        public readonly Vector2 Point;
        public readonly Vector2 Normal;

        public TileHitContext(TileHitResolver resolver, Vector3Int cell, GameplayTile tile,
            BallController ball, Vector2 point, Vector2 normal)
        {
            Resolver = resolver;
            Cell = cell;
            Tile = tile;
            Ball = ball;
            Point = point;
            Normal = normal;
        }

        public BallModifierStack Modifiers => Ball.Modifiers;

        public Vector2 CellCentre => Resolver.Tilemap.GetCellCenterWorld(Cell);
    }

    /// <summary>
    /// [B4] Put on each tilemap the ball can touch (Tile layer). On a collision it takes the contact
    /// point, nudges it a little INTO the tile (along the ball-centre -> contact direction, which is
    /// sign-safe whatever the contact-normal convention), converts it with WorldToCell, looks up the
    /// GameplayTile and runs its behaviour. Exactly one tile is processed per collision, so corner hits
    /// touching two tiles only affect one.
    ///
    /// Also works on TRIGGER tilemaps (ball passes through, e.g. speed zones): the probe point is the
    /// leading edge of the ball. Per-cell hit points are stored here.
    /// No GetComponent and no allocations in the hit path.
    ///
    /// Layout rule: breakable tiles must NOT be in a CompositeCollider2D tilemap; use a separate
    /// 'Interactive' tilemap. A 'Solid' tilemap with a composite is fine.
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public class TileHitResolver : MonoBehaviour
    {
        [SerializeField, Min(0.001f), Tooltip("How far (world units) the contact point is pushed into the tile.")]
        private float probeDistance = 0.08f;

        private readonly Dictionary<Vector3Int, int> hitPoints = new Dictionary<Vector3Int, int>();
        private Tilemap tilemap;

        public Tilemap Tilemap
        {
            get
            {
                if (tilemap == null)
                    tilemap = GetComponent<Tilemap>();

                return tilemap;
            }
        }

        private void Awake()
        {
            tilemap = GetComponent<Tilemap>();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.contactCount == 0 || !BallController.TryGet(collision.collider, out BallController ball))
                return;

            ContactPoint2D contact = collision.GetContact(0);
            Vector2 into = contact.point - ball.Position;

            if (into.sqrMagnitude < 1e-8f)
                into = ball.Direction;

            ProcessHit(ball, contact.point, into.normalized, contact.normal);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!BallController.TryGet(other, out BallController ball))
                return;

            Vector2 forward = ball.Direction;
            Vector2 leadingEdge = ball.Position + forward * ball.Radius;
            ProcessHit(ball, leadingEdge, forward, -forward);
        }

        /// <summary>Find the tile at a world point and run its behaviour. Public for tests and tools.</summary>
        public bool ProcessHit(BallController ball, Vector2 point, Vector2 into, Vector2 normal)
        {
            if (!TryFindTile(point, into, out Vector3Int cell, out GameplayTile tile))
                return false;

            if (tile.behaviour != null)
                tile.behaviour.OnBallHit(new TileHitContext(this, cell, tile, ball, point, normal));

            return true;
        }

        public bool TryFindTile(Vector2 point, Vector2 into, out Vector3Int cell, out GameplayTile tile)
        {
            Tilemap map = Tilemap;

            // Nudge into the tile; try a slightly deeper probe and the raw point as fallbacks.
            for (int attempt = 0; attempt < 3; attempt++)
            {
                float depth = attempt == 0 ? probeDistance : (attempt == 1 ? probeDistance * 3f : 0f);
                Vector3 probe = point + into * depth;
                cell = map.WorldToCell(probe);
                tile = map.GetTile<GameplayTile>(cell);

                if (tile != null)
                    return true;
            }

            cell = default;
            tile = null;
            return false;
        }

        public int GetHitPoints(Vector3Int cell, GameplayTile tile)
        {
            if (hitPoints.TryGetValue(cell, out int hp))
                return hp;

            return tile != null ? Mathf.Max(1, tile.hitPoints) : 1;
        }

        public void SetHitPoints(Vector3Int cell, int value)
        {
            hitPoints[cell] = value;
        }

        public void RemoveTile(Vector3Int cell)
        {
            Tilemap.SetTile(cell, null);
            hitPoints.Remove(cell);
        }

        public void ReplaceTile(Vector3Int cell, GameplayTile tile)
        {
            Tilemap.SetTile(cell, tile);
        }
    }
}
