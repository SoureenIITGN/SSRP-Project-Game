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

## 3. Current repository structure

The Unity project is located in `Assets/`. The current area structure is:

```text
Assets/
├── Scripts/
│   ├── Core/
│   ├── Input/
│   ├── Gameplay/
│   ├── Arena/
│   ├── UI/
│   └── Audio/
├── Prefabs/
│   ├── Core/
│   ├── Input/
│   ├── Gameplay/
│   ├── Arena/
│   ├── UI/
│   └── Audio/
├── ScriptableObjects/
│   ├── Core/
│   ├── Input/
│   ├── Gameplay/
│   ├── Arena/
│   ├── UI/
│   └── Audio/
└── Scenes/
    ├── Core/
    ├── Input/
    ├── Gameplay/
    ├── Arena/
    ├── UI/
    └── Audio/
```

The area folders make ownership clear and reduce merge conflicts. Empty
folders are preserved in Git with `.gitkeep` files until real assets are
added.

## 4. Current Core contracts

The current Core code is under `Assets/Scripts/Core/` and uses the
`SSRP.Core` namespace.

### Input contracts

- `RawFsrSample` stores the raw up and down force-sensor values and their
  timestamp.
- `IPlayerInputSource` is implemented by a device or simulator that provides
  samples and reports connection status.
- `ISignalFilter` processes one signal and can be reset.
- `PlayerInputState` stores normalised up/down values. Its `Velocity` is the
  clamped difference between them, and `Sum` is their total.

The difference is useful for paddle movement: pressing the upper sensor moves
up, pressing the lower sensor moves down, and similar pressure on both sensors
can be used by an ability such as parry.

### Paddle contracts

- `IPaddleAbility` defines `Tick(PlayerInputState input, float dt)`, allowing
  abilities such as parry, dash, or shield to read the current input.
- `IPaddleInfo` exposes a paddle's player ID, height, and velocity without
  requiring ball code to know the concrete paddle class.

### Ball and arena contracts

- `IBallHittable` defines `OnBallHit(BallHitInfo info)` for objects that can
  react to a ball hit.
- `IBallModifier` exposes a speed multiplier for effects that alter ball
  behaviour.
- `BallHitInfo` carries the collision point, collision normal, and the other
  GameObject involved.
- `ArenaBounds` stores the minimum and maximum coordinates of the playable
  area.

These contracts are deliberately small. A future ball implementation can
interact with paddles and tiles through the contracts instead of depending on
their concrete scripts.

## 5. Current events and configuration

`GameEvents` currently declares notifications for:

- Goals.
- Ball hits on paddles, walls, and tiles.
- Tile destruction.
- Match-state changes.
- Parry activation.
- Input connection changes.

The event source should call the matching `Raise...` method. A listener should
subscribe and unsubscribe in its Unity lifecycle methods. This prevents
stale listeners from remaining registered after a GameObject is disabled or
destroyed.

The current configuration assets are defined by:

- `MatchConfig`.
- `BallConfig`.
- `PaddleConfig`.

They are data definitions; gameplay systems will consume instances of these
assets when those systems are implemented.

## 6. What is not implemented yet

The repository currently contains the Core contracts and configuration
definitions, but not the complete gameplay systems. The following are future
implementations:

- Keyboard, serial, and UDP input sources.
- The input normalisation and filter pipeline.
- Paddle, ball, goal, tile, and match-management components.
- UI, audio, particle, and effects listeners.
- Concrete arena definitions and gameplay prefabs.
- Saved `.asset` instances of the configuration types.

This is expected for the C0 foundation. Later issues should build on these
contracts instead of changing them unnecessarily. If a later feature truly
needs a Core change, it should be discussed and reviewed before modifying
`Assets/Scripts/Core/`.

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
