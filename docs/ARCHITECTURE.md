# Game Architecture

This document explains how the Unity game is organised and how teammates can
add features without rewriting each other's work. It starts with the Unity
ideas used by this project, then describes the architecture that currently
exists in the repository.

## 1. Unity concepts in simple terms

### GameObjects

A **GameObject** is an object that exists in a Unity scene. It can represent a
ball, paddle, wall, tile, camera, or an empty organiser object. A GameObject
usually becomes useful by attaching one or more **components** to it.

For example, a paddle GameObject might have:

- A visual component that draws the paddle.
- A collider component that lets the ball hit it.
- A script component that moves it.

**Real-life analogy:** A GameObject is like a physical object on a stage. The
components are the object's parts and abilities: its appearance, collision
shape, and behaviour.

### Components and scripts

A **component** is a reusable piece of data or behaviour attached to a
GameObject. A C# script is commonly used to create a custom component.

For example, a future `PaddleController` component will read player input and
move a paddle. A `GameplayTile` component could react when the ball hits a
tile.

**Real-life analogy:** If a GameObject is a car, components are its engine,
steering wheel, brakes, and paint. Each part has one responsibility, and
together they make the car work.

### Interfaces

An **interface** is a contract. It says which operations or pieces of
information a class must provide, without deciding how that class performs
them.

For example, `IPlayerInputSource` says that an input source can report whether
it is connected and can provide the latest sensor sample. A keyboard input
source and an Arduino input source can implement that same contract in
different ways.

**Real-life analogy:** An interface is a standard electrical socket. Different
devices can plug into it as long as they follow the same socket shape; the
socket does not care how each device works internally.

### GameEvents

`GameEvents` is a static collection of notifications. A system raises an event
when something important happens, such as a goal being scored or a tile being
destroyed. Other systems can listen without directly depending on the system
that caused the event.

For example, a goal system can raise `GoalScored`. The score display, sound
system, and particle system can all react independently.

Listeners must subscribe in `OnEnable` and unsubscribe in `OnDisable`:

```csharp
private void OnEnable()
{
    GameEvents.GoalScored += HandleGoalScored;
}

private void OnDisable()
{
    GameEvents.GoalScored -= HandleGoalScored;
}
```

**Real-life analogy:** GameEvents is a public announcement system. The person
running the match announces “goal”, and the scoreboard, crowd, and sound
operator can each respond without the announcer knowing how they work.

### ScriptableObjects

A **ScriptableObject** is a Unity data asset stored as a project file. It
holds tunable values separately from code and scene objects.

This project currently defines:

- `MatchConfig` for match settings such as the target score.
- `BallConfig` for ball speed and bounce settings.
- `PaddleConfig` for paddle speed, height, and input deadzone.

The classes are definitions for the asset type. In Unity, actual assets can be
created through `Assets > Create > SSRP > Configs`.

**Real-life analogy:** A ScriptableObject is a settings sheet or recipe card.
The same ball code can use different recipe cards for a slow training ball or
a fast tournament ball without changing the code.

### Scenes

A **scene** is a saved Unity level or screen. It contains GameObjects and
their relationships. A project may use separate scenes for a boot screen,
menus, the match, and development tests.

**Real-life analogy:** A scene is a theatre stage prepared for one act. The
main game stage has the arena and players; a menu stage has buttons; a
development stage contains temporary test objects.

### Prefabs

A **prefab** is a reusable template for a GameObject and its components. A
paddle prefab can be configured once and then used for both players.

**Real-life analogy:** A prefab is a mould. Each object made from the mould is
a separate instance, but all instances start with the same structure.

### Namespaces

A **namespace** groups related C# types and prevents name collisions. Core
contracts in this repository use:

```csharp
namespace SSRP.Core
```

Other areas should use the matching pattern, such as `SSRP.Input` or
`SSRP.Gameplay`.

**Real-life analogy:** A namespace is a labelled department in a company.
Two departments might each have an employee named Alex, but the department
label tells you which Alex is meant.

## 2. Plug-in architecture

The project is designed so that new features plug into contracts, events, and
data assets instead of changing central gameplay code.

| You want to add... | Plug in via | Edits to core? |
|---|---|---|
| New arena | `ArenaDefinition` asset + arena prefab | No |
| New tile type | `TileBehaviour` ScriptableObject + `GameplayTile` asset | No |
| Power-up / ball effect | `BallModifierStack.Add(key, IBallModifier)` | No |
| Parry / dash / shield | `IPaddleAbility` component reading `PlayerInputState` | No |
| New input device or signal filter | `IPlayerInputSource` / `ISignalFilter` | No |
| Sounds, particles, score rules | Subscribe to `GameEvents` | No |
| Tuning numbers | ScriptableObject configs (`BallConfig`, `PaddleConfig`, ...) | No |

The planned input flow is:

```text
Input source (keyboard / serial / UDP)
    -> input pipeline (normalise and filter)
    -> PlayerInputState
    -> PaddleController and abilities
```

The ball, tiles, and goals communicate with the rest of the game through
interfaces and `GameEvents` rather than reaching into each other's
implementation details.

## 3. Repository structure

```text
Assets/
├── Scripts/                 SSRP.Runtime assembly (SSRP.Runtime.asmdef)
│   ├── Core/                SSRP.Core      contracts, GameEvents, configs, enums
│   ├── Input/               SSRP.Input     sources, pipeline, filters, serial/UDP, calibration
│   ├── Gameplay/            SSRP.Gameplay  paddle, ball, goals, tiles, abilities, extensions
│   ├── Arena/               SSRP.Arena     ArenaDefinition/Registry/Anchors/Loader, camera fit
│   ├── Flow/                SSRP.Flow      GameManager, scene loading, settings, services
│   ├── UI/                  SSRP.UI        menus, HUD, flow UI, panels (built from code)
│   └── Audio/               SSRP.Audio     AudioManager, FeedbackManager
├── Editor/Build/            SSRP > Build Project (generates scenes, prefabs, assets)
├── Tests/EditMode|PlayMode  NUnit tests (own assemblies)
├── Art/ Audio/              generated sprites and sounds (tools/*.py)
├── Prefabs/ ScriptableObjects/ Scenes/ Resources/SSRP/GameCatalog.asset   generated by the builder
arduino/ python/ tools/ data/ docs/
```

`Flow/` belongs to Member D. It holds the match flow so that `Arena/` stays data-only.

## 4. Runtime picture

```text
Boot scene ──> GameServices (DontDestroyOnLoad, from GameCatalog.servicesPrefab)
                 ├─ SceneLoader (fade)
                 ├─ Input: Keyboard / Scripted / Serial(+ConnectionManager) / UDP / NoiseInjector
                 │         └─ InputSourceRegistry  (InputSourceType -> shared source)
                 └─ Audio: AudioManager (listens to GameEvents)
MainMenu ──> MainMenuController ─ MatchSetupPanel ─ writes MatchSession.Config (runtime copy) + PlayerPrefs
Game ─────> GameManager (state machine, composition root)
              ├─ ArenaLoader.Load(ArenaDefinition) -> ArenaAnchors -> ArenaBounds, spawns, goals
              ├─ PlayerSpawner.Spawn(MatchConfig.players) -> Paddle prefab per PlayerDefinition
              │     PlayerSlot -> PlayerInputPipeline -> PaddleController (+ IPaddleAbility: ParryAbility)
              ├─ Ball prefab: BallController + BallModifierStack + PaddleHitResolver
              └─ raises MatchStateChanged / ScoreChanged / CountdownTick
            PlayerHud, FlowUi, FeedbackManager: listeners only
```

**Input path.** `IPlayerInputSource` returns raw 0-1023 counts, with `Time` taken from `InputClock`
(thread-safe). `PlayerInputPipeline` then does four things: normalisation (`CalibrationStore` for
Serial/UDP, otherwise 0-1023), the filter chain (`FilterStage` list -> `FilterFactory`), the deadzone,
and the conversion to `PlayerInputState`. It has a stale-data watchdog (250 ms -> zeros +
`InputConnectionChanged(false)`).

**Ball.** Speed `= base * min(ramp^hits, cap) * product(modifiers)` is re-imposed every `FixedUpdate`.
Anti-stall keeps `|dir.x| >= 0.25`. `PaddleHitResolver` sets the angle from the hit offset. Each
`TileHitResolver` maps contact -> cell -> `GameplayTile.behaviour.OnBallHit(context)`.

**Static state and domain reload.** Enter Play Mode Options are on, with domain reload disabled.
So every static (events, registries, singletons) is reset in a
`[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]` method. Follow this pattern for any new static.

## 5. Events and configuration

`GameEvents` (Core): `GoalScored(id)`, `ScoreChanged(id, score)`, `BallHitPaddle`, `BallHitWall`,
`BallHitTile`, `TileDestroyed(position)`, `MatchStateChanged(MatchPhase)`, `CountdownTick(secondsLeft)`,
`ParryTriggered(id)`, `InputConnectionChanged(id, connected)`.

**Core change log (team-reviewed):** `MatchStateChanged` and `TileDestroyed` gained payloads, and
`ScoreChanged` and `CountdownTick` were added. `MatchPhase`, `PaddleSide` and `PlayerDefinition` are
new. `InputSourceType` moved from Input to Core (values unchanged). `MatchConfig`, `BallConfig` and
`PaddleConfig` gained tuning fields. Reasons: the UI needs to know *which* state was entered, the
particles need a *position*, and `MatchConfig` must store a per-player device without Core depending
on Input.

Config assets live in `Assets/ScriptableObjects/`: `DefaultMatchConfig`, `Ball_Slow/Normal/Fast`,
`PaddleConfig`, `SerialConfig`, `ParryConfig`, `AudioConfig`, the tiles and their behaviours, the
arena definitions and `ArenaRegistry`. `Resources/SSRP/GameCatalog.asset` ties them together. Match
Setup edits only `MatchSession.Config`, a `CreateRuntimeCopy()`, so playing never modifies assets.

## 6. Generated vs hand-written

Unity scenes and prefabs are YAML with cross-file GUID references, so they are not written by hand.
`Assets/Editor/Build/*` creates them with the editor API. The builder is safe to re-run: it updates
assets in place and keeps their GUIDs. If you change a layout in the editor and want to keep the
change, update the builder too, or stop using the builder for that asset and say so in the PR.
UI screens build their layout in code (`UiFactory`) when they wake up, so their prefabs are just a
root object plus the component.

## 7. Team rules

1. Each teammate owns their assigned `Scripts/<Area>/` and
   `Prefabs/<Area>/` folders.
2. Changes to Core contracts or `GameEvents` require review by the team.
3. Only the designated scene owner edits shared `Boot`, `MainMenu`, and
   `Game` scenes.
4. Other teammates should use their own development scene, such as
   `Dev_<Name>.unity`, for experiments.
5. New features should use the existing interfaces, events, and configuration
   assets rather than introducing direct dependencies between unrelated
   systems.
