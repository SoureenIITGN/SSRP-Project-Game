# Extending the game

These guides show how to add content without editing core scripts. If a step below seems to need
a change to `Core`, `BallController`, `TileHitResolver`, `PaddleController` or the input pipeline,
stop and open an issue: it means the architecture has a gap.

Contents:

1. [Add a tile type](#1-add-a-tile-type)
2. [Add a ball modifier (power-up)](#2-add-a-ball-modifier-power-up)
3. [Add a paddle ability](#3-add-a-paddle-ability)
4. [Add an arena in 5 minutes](#4-add-an-arena-in-5-minutes)
5. [Add sounds and effects](#5-add-sounds-and-effects)
6. [Add an input device or a signal filter](#6-add-an-input-device-or-a-signal-filter)

---

## 1. Add a tile type

A tile type is **one behaviour class plus assets**. Worked example in the repo: `SpeedZoneBehaviour` ([B6]).

1. Create `Assets/Scripts/Gameplay/Extensions/MyBehaviour.cs`:

   ```csharp
   using UnityEngine;
   using SSRP.Core;

   namespace SSRP.Gameplay
   {
       [CreateAssetMenu(fileName = "MyBehaviour", menuName = "SSRP/Tiles/Behaviours/My Behaviour")]
       public class MyBehaviour : TileBehaviour
       {
           public float strength = 1f;            // tuning numbers live in the asset

           public override void OnBallHit(in TileHitContext context)
           {
               // context.Ball, context.Modifiers, context.Cell, context.Resolver (HP, remove/replace tile)
               GameEvents.RaiseBallHitTile();      // so audio/feel react automatically
           }
       }
   }
   ```

   The class name must match the file name. Behaviours are shared assets, so they must not
   store per-tile state. Use `context.Resolver.GetHitPoints/SetHitPoints` for per-cell data.
2. **Assets > Create > SSRP > Tiles > Behaviours > My Behaviour** creates the behaviour asset.
   Put it in `Assets/ScriptableObjects/Gameplay/Tiles/Behaviours/`.
3. **Assets > Create > SSRP > Tiles > Gameplay Tile** creates the tile. Set its *Sprite*, set
   *Collider Type = Grid*, and drag your behaviour into *Behaviour*.
4. Drag the tile into the tile palette (**Window > 2D > Tile Palette**, palette `SSRP Tiles`).
5. Paint it onto a tilemap that has a `TileHitResolver`. Which tilemap depends on the tile:
   - **Interactive** (solid, one collider per tile): for tiles the ball bounces off and that can change.
   - **Zones** (`TilemapCollider2D` set to *Is Trigger*): for tiles the ball passes through.
   - **Solid** (composite collider): only for tiles that never change.

## 2. Add a ball modifier (power-up)

Worked example: `SlowMoModifier` + `SlowMoPickupBehaviour` ([B6]).

1. Implement `IBallModifier` (one property, `SpeedMultiplier`):

   ```csharp
   public sealed class TurboModifier : IBallModifier
   {
       public const string Key = "powerup.turbo";
       public float SpeedMultiplier => 1.8f;
   }
   ```
2. Add it from anything that can reach the ball's `BallModifierStack`. That can be a tile
   (`context.Modifiers`), an ability, or a test:

   ```csharp
   ball.Modifiers.Add(TurboModifier.Key, new TurboModifier(), duration: 3f);   // auto-expires
   ball.Modifiers.Remove(TurboModifier.Key);                                   // or remove it yourself
   ```
   Adding with the same key refreshes the modifier instead of stacking it. Use different keys to
   stack. The ball's speed is `base * ramp^hits * product(modifiers)`.

## 3. Add a paddle ability

Worked example: `ParryAbility` ([C6]).

1. Write `MyAbility : MonoBehaviour, IPaddleAbility`. `Tick(PlayerInputState input, float dt)` is
   called every frame. `input.Up`, `input.Down`, `input.Velocity` and `input.Sum` are normalised
   and filtered.
2. Read the player id with `GetComponent<IPaddleInfo>()` in `Awake`.
3. Raise a `GameEvents` event so sound and effects can react.
4. Add the component to `Assets/Prefabs/Gameplay/Paddle.prefab`. Put its numbers in a
   ScriptableObject config.

### Parry: how it works and where to tune it

All numbers live in `Assets/ScriptableObjects/Gameplay/ParryConfig.asset` and can be edited in the
Inspector, even during Play.

- **Triggering a parry:** `pressThreshold`, `balanceTolerance`, `holdTime` (input lag), `cooldown`
  and `requireRelease`. When it fires: a white paddle flash, a quiet swish, and `ParryTriggered`.
- **Success:** the ball touches the paddle within `activeWindow` after the parry, or the parry comes
  within `lateGrace` after the touch. Then the ball catches fire (`BallFireEffect`: orange flicker,
  fiery trail, embers) and speeds up by `speedMultiplier`. You also get the parry clang, a 0.12 s
  freeze-frame, screen shake, an orange paddle flash, a "PARRY!" banner, and `ParrySucceeded`.
- **When the fire ends:** the other player returns the ball, a goal is scored, or `fireMaxDuration`
  runs out.

## 4. Add an arena in 5 minutes

Arenas are **data**: a prefab + an `ArenaDefinition` asset + a registry entry. No scripts.
Arena 03 "Pillars" ([D7]) was made exactly this way.

1. **Duplicate a prefab.** In `Assets/Prefabs/Arena/`, select `Arena01_Classic.prefab` and press
   *Ctrl+D*. Rename the copy, for example `Arena04_MyArena`.
2. **Edit it** (double-click to open Prefab Mode):
   - Root: `ArenaAnchors`. Set *Bounds Min/Max* to the playable area (paddles are clamped to it),
     the paddle spawn points, the ball spawn, and the two goal zones.
   - `Walls`: move or resize the top and bottom walls (layer **Wall**, `BoxCollider2D`, material `Bouncy`).
   - `Grid/Solid`, `Grid/Interactive`, `Grid/Zones`: paint tiles with the `SSRP Tiles` palette.
   - The goal zones stay just outside the playable area on the left and right (layer **Goal**, trigger).
3. **Create the definition.** **Assets > Create > SSRP > Arena > Arena Definition** in
   `Assets/ScriptableObjects/Arena/`. Fill in the *id* (unique, lowercase), *Display Name*,
   *Description*, *Thumbnail* (any sprite; 16:9 looks best) and *Arena Prefab*. Optionally set a
   *Ball Config Override*.
4. **Register it.** Open `Assets/ScriptableObjects/Arena/ArenaRegistry.asset`, add an element to
   *Arenas* and drag in your definition.
5. **Play.** Main Menu > Match Setup > Arena shows it with its thumbnail. The camera fits the
   bounds automatically for any aspect ratio.

> Screenshots: `docs/images/arena-*.png` (capture them from your own editor: prefab mode, the
> definition inspector and the setup screen).

**Hard-coded assumptions found while building Arena 03 (and fixed):**

| Assumption | Where | Fix |
|---|---|---|
| Paddle x position was a constant | spawn logic | Now read from `ArenaAnchors` spawn points |
| Camera size was set per scene | `Game` scene camera | `ArenaCameraFitter` computes it from the bounds |
| Goals knew which player scores | goal prefab | `GameManager` assigns `ScoringPlayerId` from the player sides at load |
| Ball speed fixed per match | `MatchConfig` | `ArenaDefinition.ballConfigOverride` |

Known remaining assumption: exactly one player per side. `PlayerSpawner` would stack two
same-side paddles at the same spawn point.

## 5. Add sounds and effects

Subscribe to `GameEvents` in `OnEnable` and unsubscribe in `OnDisable`. For a new sound:
add an `AudioClip` field to `AudioConfig`, assign it in `Assets/ScriptableObjects/Audio/AudioConfig.asset`,
and play it from `AudioManager` on an existing event. If none fits, add a new event to `GameEvents`
(a Core change that needs team review). Record the clip's source in `docs/CREDITS.md`.

## 6. Add an input device or a signal filter

- **Device:** implement `IPlayerInputSource`, returning raw 0-1023 values with `Time = InputClock.Now`.
  Add a value to `InputSourceType`, then a field and `case` in `InputSourceRegistry` and `PlayerSlot`.
  Put the component on `SSRP_Services.prefab/Input`. `UdpInputSource` ([A6]) is the worked example.
- **Filter:** implement `ISignalFilter` (`Process(x, dt)`, `Reset()`), add a `FilterType` value and
  a case in `FilterFactory`. It then appears in the `PlayerInputPipeline` inspector. Changing it
  during Play rebuilds the chain live.
