# Physics setup (Ball & Tiles)

This explains every physics choice so nobody "fixes" one by accident.

## Layers

Set in `ProjectSettings/TagManager.asset`, with names in `SSRP.Gameplay.PhysicsLayers`.

| # | Layer | Used by |
|---|---|---|
| 6 | Ball | the ball |
| 7 | Paddle | both paddles |
| 8 | Wall | top and bottom walls, arena frame |
| 9 | Tile | all tilemaps (solid, interactive, zones) |
| 10 | Goal | the goal trigger zones |

## Collision matrix (Physics 2D)

Only the ball needs to touch anything. Everything else ignores everything else.

| | Ball | Paddle | Wall | Tile | Goal |
|---|---|---|---|---|---|
| **Ball** | - | yes | yes | yes | yes (trigger) |
| **Paddle** | yes | - | - | - | - |
| **Wall** | yes | - | - | - | - |
| **Tile** | yes | - | - | - | - |
| **Goal** | yes | - | - | - | - |

Paddles never collide with walls. `PaddleController` clamps them inside `ArenaBounds` instead,
which is cheaper and cannot jitter. The project builder (**SSRP > Build Project**) applies the
matrix again with `Physics2D.IgnoreLayerCollision`, so it is always correct.

## Global settings

| Setting | Value | Why |
|---|---|---|
| Gravity | (0, 0) | Pong is top-down. |
| Fixed timestep | **0.01 s (100 Hz)** | See tunnelling below. Also gives the paddle (a velocity integrator) a finer step. |
| Velocity (bounce) threshold | 0.01 | Unity treats slower impacts as inelastic. With the default of 1, a ball travelling almost parallel to a wall (normal speed < 1 u/s) would *stick* to it and slide. |

## Material

`Assets/Art/Physics/Bouncy.physicsMaterial2D`: **bounciness 1, friction 0**. It is assigned to the
ball, paddles, walls and tilemaps, so the combine mode can never reduce it. Even so, collisions in a
physics engine slowly gain or lose energy through numerical error, so `BallController` re-imposes
the speed every step (see [B2]).

## Body types

| Object | Body type | Detection | Interpolation | Why |
|---|---|---|---|---|
| Ball | Dynamic, gravity scale 0, rotation frozen | **Continuous** | **Interpolate** | Fast and small. |
| Paddles | Kinematic (moved with `MovePosition`) | Discrete | Interpolate | Moved by our code, not by forces. |
| Walls | Static (collider only) | n/a | n/a | Never move. |
| Solid tilemap | Static Rigidbody2D + CompositeCollider2D | n/a | n/a | Merged shapes are cheaper. |
| Interactive tilemap | TilemapCollider2D, no composite | n/a | n/a | Keeps one collider shape per tile for breakables. |

### Tunnelling

Tunnelling happens when the ball moves farther than a collider's thickness in one physics step.
At the speed cap (8 u/s x 1.8 = 14.4 u/s) with a 0.02 s step, the ball travels 0.29 u per step,
which is more than a 0.25 u tile. With 0.01 s it travels 0.14 u. Continuous detection also sweeps
the path between steps instead of only checking the end positions.

## Sandbox

`Assets/Scenes/Gameplay/Dev_PhysicsSandbox.unity` is a closed box with one ball and a
`SpeedProbe` that logs the worst speed error. Done-when check: let it run for 5 minutes. The ball
must never escape, and the probe must report an error below 1%.

## Tests (B5)

How to run them: **Window > General > Test Runner**.

- **EditMode** tab: fast pure-logic tests (filters, serial parser, calibration statistics,
  deflection maths, serve angle, scoring rules).
- **PlayMode** tab: physics tests that build their own objects in an empty scene. They cover speed
  constancy, the anti-stall guard, the deflection angle increasing with offset, the goal event
  firing exactly once, and a breakable tile being removed after N hits.

Press **Run All** in each tab. Everything should be green. CI or command line:

```
Unity -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results-edit.xml
Unity -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults results-play.xml
```
