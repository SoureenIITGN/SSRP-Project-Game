# GitHub Issues: Game Development Plan

Scope: **game development only** (Unity + Arduino interface). Analysis, filter design and the other rubric items come later.

## How to use this file

- **Fast way:** run `scripts/create_issues.sh` (needs the [GitHub CLI](https://cli.github.com/); run `gh auth login` once; on Windows use *Git Bash*). It creates labels, milestones and all 26 issues.
- **Manual way:** copy each issue below into *Issues -> New issue*.
- Issue codes (`[A1]`, `[B3]` ...) are in the titles; "Depends on" uses the same codes.

## Team split

| Member | Area | Issues |
|---|---|---|
| **A** | Arduino interface (firmware + Unity serial side) | A1-A6 |
| **B** | Ball & Tile physics | B1-B6 |
| **C** | Multiplayer interface, input pipeline, TinkerCAD | C0-C6 |
| **D** | Arena & Main menu, game flow | D1-D7 |

Interpretations I made (tell me if wrong): *Tile* = arena tiles with behaviour (solid / breakable / bounce pad). *Multiplayer* = two local players on one PC. The Arduino task includes both the sketch and the laptop-side Unity code you asked to be assigned.

## Architecture: plug-in points (so new features never rewrite core code)

| You want to add... | Plug in via | Edits to core? |
|---|---|---|
| New arena | `ArenaDefinition` asset + arena prefab | No |
| New tile type | `TileBehaviour` ScriptableObject + `GameplayTile` asset | No |
| Power-up / ball effect | `BallModifierStack.Add(key, IBallModifier)` | No |
| Parry / dash / shield | `IPaddleAbility` component reading `PlayerInputState` | No |
| New input device or signal filter | `IPlayerInputSource` / `ISignalFilter` | No |
| Sounds, particles, score rules | Subscribe to `GameEvents` | No |
| Tuning numbers | ScriptableObject configs (`BallConfig`, `PaddleConfig`, ...) | No |

Data flow: `Source (Keyboard/Serial/UDP) -> Pipeline (normalise, filter) -> PlayerInputState -> PaddleController (+ abilities)`; the ball, tiles and goals talk to the rest of the game only through interfaces and `GameEvents`.

## Rules that prevent merge conflicts

1. **Folder ownership:** each member owns `Scripts/<Area>/` and `Prefabs/<Area>/`. Changes to `Core` (interfaces, `GameEvents`) need a small PR approved by all four.
2. **Scenes:** only **Member D** edits `Boot`, `MainMenu` and `Game`. Everyone else builds **prefabs** and tests in their own scene `Dev_<Name>.unity`.
3. **Cross-review pairs:** A <-> C, B <-> D. A second person from another pair may also review.
4. Extension-proof issues must add **no changes to core scripts**. If one is needed, open an issue first: it means the architecture has a gap.

## Order of work

- **M1 (days 1-3):** [C0] first, merged fast. In parallel: [A1], [B1], [C1], [D1].
- **M2:** [A2]-[A5], [B2]-[B4], [C2]-[C5], [D2]-[D5]. Goal: a full match on keyboard, then on FSRs.
- **M3:** extension proofs ([B6], [C6], [D7]), tests ([B5]), audio/feel ([D6]), stretch ([A6]).
- Leave the final days before the deadline for the grading work that is not game development.

---

## Member C: Multiplayer interface & TinkerCAD

### [C0] Foundation: core contracts, folder structure & conventions
**Labels:** `area:multiplayer`, `foundation`, `blocker` | **Milestone:** M1 - Foundation

### Why
Four people can only code in parallel if everyone codes against agreed *interfaces* rather than each other's classes (the dependency-inversion principle). Doing this first, in a small PR everyone reviews, is what lets later features (parry, power-ups, new arenas) plug in instead of forcing rewrites.

### Tasks
- [ ] Create `Assets/Scripts/{Core,Input,Gameplay,Arena,UI,Audio}`, plus `Assets/Prefabs`, `Assets/ScriptableObjects`, `Assets/Scenes` with area subfolders. Namespaces: `SSRP.<Area>`.
- [ ] Write the contracts in `Core` (sketch below): `RawFsrSample`, `IPlayerInputSource`, `ISignalFilter`, `PlayerInputState`, `IPaddleAbility`, `IPaddleInfo`, `IBallHittable`, `IBallModifier`, `BallHitInfo`, `ArenaBounds`.
- [ ] Add static `GameEvents` (events: `GoalScored(int playerId)`, `BallHitPaddle`, `BallHitWall`, `BallHitTile`, `TileDestroyed`, `MatchStateChanged`, `ParryTriggered(int)`, `InputConnectionChanged(int, bool)`). Rule: subscribe in `OnEnable`, unsubscribe in `OnDisable`.
- [ ] Add empty-but-sensible ScriptableObject configs: `MatchConfig`, `BallConfig`, `PaddleConfig`.
- [ ] Write `docs/ARCHITECTURE.md` (copy the plug-in points table from `docs/GITHUB_ISSUES.md`).
- [ ] Verify Edit > Project Settings > Editor: *Version Control = Visible Meta Files*, *Asset Serialization = Force Text*; verify `.gitignore` covers `Library/ Temp/ Obj/ Logs/ UserSettings/ Builds/`.
- [ ] All three teammates review and approve before merging.

### Done when
- [ ] Project compiles with zero errors/warnings from the new scripts.
- [ ] Every teammate can reference the contracts from their own area without editing `Core`.
- [ ] Merged within the first 1-2 days.

### Depends on
None (everyone else depends on this).

### Notes
Sketch only; finalise in the PR.
```csharp
namespace SSRP.Core {
  public struct RawFsrSample { public float Up, Down; public double Time; }   // raw ADC counts 0-1023
  public interface IPlayerInputSource {
    bool IsConnected { get; }
    bool TryGetLatest(int playerId, out RawFsrSample sample);
  }
  public interface ISignalFilter { float Process(float x, float dt); void Reset(); }
  public struct PlayerInputState {                       // normalised 0-1, after filtering
    public float Up, Down;
    public float Velocity => Mathf.Clamp(Up - Down, -1f, 1f);   // drives the paddle
    public float Sum => Up + Down;                              // used by abilities (parry)
  }
  public interface IPaddleAbility { void Tick(PlayerInputState input, float dt); }
  public interface IPaddleInfo { int PlayerId { get; } float Height { get; } Vector2 Velocity { get; } }
  public interface IBallHittable { void OnBallHit(BallHitInfo info); }
  public interface IBallModifier { float SpeedMultiplier { get; } }
  public struct BallHitInfo { public Vector2 Point, Normal; public GameObject Other; }
}
```
Why `Velocity = Up - Down`: differential sensing cancels disturbances common to both sensors (supply noise, slow drift), and it gives the parry feature a natural signal (both pressed = near-zero velocity but high `Sum`).

---
Branch: `feature/core-contracts` | Reviewer: Member A | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [C1] Keyboard & simulated input sources (so everyone can play without hardware)
**Labels:** `area:multiplayer` | **Milestone:** M1 - Foundation

### Why
Only one Arduino kit exists, so three of four teammates need a hardware-free way to test. The keyboard must imitate a *finger pressing an FSR* (analogue ramp), not a digital on/off key, or the game will feel different on real sensors.

### Tasks
- [ ] `KeyboardInputSource : IPlayerInputSource`. P1: `W`/`S`, P2: `Up`/`Down`. Holding a key ramps its channel 0 -> max over `rampSeconds` (default ~0.1 s); release ramps back down.
- [ ] Pressing both keys of a player gives roughly equal values (needed to test parry later).
- [ ] Optional `NoiseInjector` wrapper: adds Gaussian noise and rare spikes to any source (for testing filters visually).
- [ ] `ScriptedInputSource`: sine-sweep and step patterns (used by automated tests).
- [ ] Check *Project Settings > Player > Active Input Handling* matches the API you use (Unity 6 templates default to the Input System package).

### Done when
- [ ] Output is in the same units as the Arduino source (raw 0-1023) so the rest of the pipeline cannot tell the difference.
- [ ] A `PlayerSlot` can select Keyboard / Scripted / Serial from the inspector.

### Depends on
[C0]

---
Branch: `feature/keyboard-input` | Reviewer: Member A | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [C2] Input pipeline: Raw -> Normalise -> Filter chain -> PlayerInputState
**Labels:** `area:multiplayer` | **Milestone:** M2 - Playable Core

### Why
This is the single place where raw sensor data becomes game input. Filters attach here later (grading work) without touching paddle code. Normalisation maps each sensor to 0-1 with `(x - baseline) / (max - baseline)` so differently-behaving FSRs are comparable.

### Tasks
- [ ] `PlayerInputPipeline` component (one per player slot): reads an `IPlayerInputSource`, applies calibration (from [A4]; default to 0-1023 -> 0-1 when no calibration exists), then a per-channel list of `ISignalFilter`.
- [ ] Implement `PassThroughFilter` (default) and `ExponentialFilter` (`y += alpha * (x - y)`) only to prove the hook works. Real filter design is out of scope for now.
- [ ] Filter selection through an inspector enum + factory, so swapping filters needs no code.
- [ ] Compute `PlayerInputState` (Up, Down, Velocity, Sum). Apply deadzone and clamp from `PaddleConfig`.
- [ ] Stale-data safety: if no new sample for > timeout, output zeros (never hold the last high value).

### Done when
- [ ] Changing the filter type in the inspector changes behaviour with no script edits.
- [ ] EditMode unit test: a unit step through `ExponentialFilter` reaches ~63% after one time constant.

### Depends on
[C0], [C1]; consumes calibration from [A4] once available.

---
Branch: `feature/input-pipeline` | Reviewer: Member A | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [C3] PaddleController & PlayerSlot system (data-driven, supports add-on abilities)
**Labels:** `area:multiplayer` | **Milestone:** M2 - Playable Core

### Why
Paddle *velocity* control means the player's pressure differential sets the speed, not the position. That is a first-order integrator in the control loop, so filter delay will be directly visible as overshoot. Abilities such as parry must be add-on components, not edits to this class.

### Tasks
- [ ] `PaddleController` (Kinematic `Rigidbody2D`, `MovePosition` in `FixedUpdate`): `velocity = maxSpeed * state.Velocity`; optional acceleration smoothing; clamp inside `ArenaBounds`.
- [ ] Implement `IPaddleInfo` (PlayerId, Height, Velocity) so the ball code never references this class.
- [ ] Collect `IPaddleAbility` components with `GetComponents` once, and call `Tick(state, dt)` each frame.
- [ ] `PlayerSlot` (id, colour, side, input source, pipeline, score) and `PlayerSpawner` that builds slots from `MatchConfig` (data-driven: no hard-coded 'Player1/Player2' strings).
- [ ] Paddle prefab + `PaddleConfig` ScriptableObject (maxSpeed, height, deadzone).

### Done when
- [ ] Both paddles move independently with the keyboard source.
- [ ] No references to Ball or Arena classes, other than the `ArenaBounds` struct.

### Depends on
[C0], [C1], [C2]; `ArenaBounds` provided by [D4].

---
Branch: `feature/paddle-and-slots` | Reviewer: Member A | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [C4] Two-player HUD, scoreboard & live input debug overlay
**Labels:** `area:multiplayer` | **Milestone:** M2 - Playable Core

### Why
A live view of each FSR (Up/Down bars) makes tuning and bug-hunting hours faster, and it is also a good demo visual. Everything must be event-driven so new features only add listeners.

### Tasks
- [ ] `PlayerHud` prefab: score (listens to `GoalScored`), connection status dot (`InputConnectionChanged`), TextMeshPro.
- [ ] Debug overlay toggled with `F1`: per-player Up/Down bars (UI Image fill), current `Velocity`, sample age.
- [ ] No `Find*` calls in `Update`; subscribe via `GameEvents` and the pipeline's public state only.

### Done when
- [ ] HUD updates only through events/state. Toggle works. Layout good at 16:9 and 16:10.
- [ ] Prefab can be dropped into the Game scene by [D1] without edits.

### Depends on
[C0], [C2], [C3]

---
Branch: `feature/hud-debug` | Reviewer: Member A | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [C5] TinkerCAD circuit simulation + wiring document
**Labels:** `area:multiplayer` | **Milestone:** M2 - Playable Core

### Why
TinkerCAD Circuits can validate the *wiring and firmware logic* before touching real hardware. Each FSR forms a voltage divider: `Vout = Vcc * R_fixed / (R_fsr + R_fixed)`. With the fixed resistor to ground, pressing harder lowers `R_fsr`, so `Vout` (and the ADC reading) goes up.

### Tasks
- [ ] Build the circuit: Arduino Uno, 4 sensors on `A0`-`A3`, each in a divider with a ~10 kOhm resistor to GND (confirm values against your FSR's datasheet).
- [ ] I could not confirm that TinkerCAD has an FSR part. If it does not, use potentiometers (or a flex sensor) as stand-ins; electrically they are also variable resistors.
- [ ] Paste firmware from [A1], run the simulation, and capture Serial Monitor output proving the line format matches the protocol.
- [ ] Save screenshots and the share link in `docs/hardware/`, plus a pin-map table (`A0=P1 Up, A1=P1 Down, A2=P2 Up, A3=P2 Down`).
- [ ] State the limitation: simulated serial cannot feed Unity, so this proves wiring and code logic only. The real-board test is [A5].

### Done when
- [ ] Pin map in the document matches the Unity channel mapping for P1/P2.
- [ ] A teammate can rebuild the real circuit from the document alone.

### Depends on
[A1] (firmware + protocol)

---
Branch: `docs/tinkercad-circuit` | Reviewer: Member A | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [C6] Extension proof: ParryAbility stub (no core edits allowed)
**Labels:** `area:multiplayer`, `extension-proof` | **Milestone:** M3 - Extension-Ready & Polish

### Why
Proves the architecture. Parry is an add-on that reads `PlayerInputState` through `IPaddleAbility`. Design insight: moving from 'up' to 'down' briefly passes through a balanced state, so detection needs a minimum press level *and* a hold time to avoid false triggers. Also, balanced pressure gives `Velocity ~ 0`, so the paddle naturally braces in place.

### Tasks
- [ ] `ParryAbility : MonoBehaviour, IPaddleAbility`: trigger when `min(Up, Down) >= pressThreshold` AND `|Up - Down| <= balanceTolerance` for `holdTime`, then cooldown.
- [ ] Raise `GameEvents.ParryTriggered(playerId)` and flash the paddle colour. The actual ball-reflect effect is future work.
- [ ] All numbers in a `ParryConfig` ScriptableObject.
- [ ] **Rule:** if you feel you must edit `PaddleController` or the pipeline, stop and open an issue. That means the architecture has a gap.

### Done when
- [ ] Adding the component to the paddle prefab triggers parry with the keyboard (both keys) and with real FSRs.
- [ ] PR contains zero diffs in `Core`, `PaddleController` or the pipeline.

### Depends on
[C2], [C3]

---
Branch: `feature/parry-stub` | Reviewer: Member A | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


## Member A: Arduino interface

### [A1] Arduino firmware + serial protocol specification
**Labels:** `area:arduino`, `blocker` | **Milestone:** M1 - Foundation

### Why
The serial protocol is the contract between firmware, Unity, TinkerCAD and later Python analysis. Firmware sends *raw* ADC counts and does no filtering, because filtering in firmware would hide the noise you need to study later.

### Tasks
- [ ] Write `docs/serial-protocol.md`: 115200 baud 8N1; one ASCII line per sample `t_us,p1_up,p1_down,p2_up,p2_down\n`; lines starting with `#` are comments; boot banner `# SSRP-FSR v1 rate=200Hz`; pin map `A0..A3`.
- [ ] Sketch in `arduino/ssrp_fsr/ssrp_fsr.ino`: fixed-rate loop using `micros()` and unsigned subtraction (no `delay()`); build each line with `snprintf` and send it with a single `Serial.write`.
- [ ] Compile-time switches: `SAMPLE_RATE_HZ` (default 200), `DISCARD_FIRST_READ` (ADC multiplexer residue from the previous channel can corrupt the first read after switching, i.e. crosstalk), `OVERSAMPLE`.
- [ ] Header comment documenting wiring and each switch.
- [ ] Budget check: 4 channels x 2 reads is under 1 ms; each line is ~28 bytes = ~2.4 ms at 115200 baud, so 200 Hz fits. Fall back to 100 Hz if lines are dropped.

### Done when
- [ ] Serial Monitor shows 4 columns; pressing one FSR changes mainly its own column.
- [ ] Logged mean interval ~5000 us; jitter noted in the doc.
- [ ] No truncated lines in a 5-minute run.

### Depends on
None. Merge the spec first so [C5] and [A2] can start.

---
Branch: `feature/arduino-firmware` | Reviewer: Member C | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [A2] Unity SerialInputSource (threaded reader + robust parser)
**Labels:** `area:arduino` | **Milestone:** M2 - Playable Core

### Why
Reading a serial port on Unity's main thread would stall frames. A background thread reads lines and publishes the latest sample; Unity APIs must only be touched on the main thread.

### Tasks
- [ ] Set *Project Settings > Player > Other Settings > Api Compatibility Level = .NET Framework* (`System.IO.Ports` is not available under .NET Standard). Commit `ProjectSettings`.
- [ ] Background thread with `ReadLine` + `ReadTimeout`. Parse with `TryParse`; ignore `#` lines, partial lines and garbage; count parse errors.
- [ ] Thread-safe latest-sample storage per channel (lock or volatile snapshot).
- [ ] Implement `IPlayerInputSource`: player 0 = channels 0-1, player 1 = channels 2-3.
- [ ] Diagnostics: packets/s, parse errors, age of last packet. `SerialConfig` ScriptableObject (port, baud).
- [ ] **Clean shutdown** in `OnDisable`/`OnApplicationQuit`: close the port and join the thread. Otherwise the COM port stays locked and the next Play press fails until Unity restarts.

### Done when
- [ ] With the real Arduino, values appear in the debug overlay ([C4]).
- [ ] Pressing Play/Stop 10 times causes no 'port in use / access denied' errors.

### Depends on
[C0], [A1]

---
Branch: `feature/serial-input-source` | Reviewer: Member C | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [A3] Connection manager, watchdog & reusable Serial connection panel
**Labels:** `area:arduino` | **Milestone:** M2 - Playable Core

### Why
The worst real-hardware bug is a cable unplug that leaves the *last high pressure value* active, so the paddle runs into the wall forever. A watchdog zeroes inputs when data stops.

### Tasks
- [ ] `SerialConnectionManager`: list ports (`SerialPort.GetPortNames()`), connect/disconnect, auto-reconnect every ~2 s after unplug.
- [ ] Watchdog: no valid line within 250 ms -> raise `InputConnectionChanged(false)` and output zeros.
- [ ] Remember the last port in `PlayerPrefs`.
- [ ] Reusable `SerialConnectionPanel` prefab (port dropdown, refresh, connect button, status LED, Hz readout) for [D3] to embed.

### Done when
- [ ] Unplugging mid-game freezes paddles within 0.3 s; replugging resumes without restarting.
- [ ] Panel works standalone in a test scene.

### Depends on
[A2]

---
Branch: `feature/serial-connection` | Reviewer: Member C | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [A4] Calibration routine (baseline & max per FSR)
**Labels:** `area:arduino` | **Milestone:** M2 - Playable Core

### Why
FSRs vary from part to part and drift with mounting. Normalising each channel with `clamp((x - baseline) / (max - baseline))` makes players comparable and the deadzone meaningful. Use the median for baseline and a high percentile (~95th) for max, so spikes do not corrupt them.

### Tasks
- [ ] `CalibrationData`: per channel baseline and max, saved as JSON in `Application.persistentDataPath`.
- [ ] Guided `CalibrationRoutine`: 'Release everything' (2 s), then 'Press P1 UP firmly', and so on, with a progress bar.
- [ ] Validation: reject if `max - baseline` is below a minimum span; warn on a non-responding channel.
- [ ] 'Quick re-zero' (baseline only), since sensors drift.
- [ ] `CalibrationPanel` prefab that [D3] can open. Skipping falls back to defaults.

### Done when
- [ ] After calibration all four channels reach ~1.0 on a firm press and ~0 at rest.
- [ ] Data survive an app restart; a bad calibration is rejected with a message.

### Depends on
[A2], [C2]

---
Branch: `feature/calibration` | Reviewer: Member C | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [A5] Real-hardware bring-up, mounting & integration test
**Labels:** `area:arduino` | **Milestone:** M2 - Playable Core

### Why
Mechanical mounting causes many 'software' problems: bending an FSR's tail or loading it off-axis gives false readings. This issue also validates the whole chain end-to-end.

### Tasks
- [ ] Wire the real circuit from [C5]. Verify all four channels in the Arduino Serial Plotter.
- [ ] Record rest value and max for each channel. Press one FSR at a time and note visible crosstalk on the others (keep notes for later analysis).
- [ ] Mount FSRs on firm pads with finger targets; add strain relief; label P1/P2 Up/Down.
- [ ] Save a 1-minute raw CSV in `data/samples/` (small) and photos in `docs/hardware/`.
- [ ] Final integration test: two people play a match with FSRs.

### Done when
- [ ] Channel mapping verified against the document.
- [ ] A full two-player match is completed on real FSRs.

### Depends on
[A2], [A3], [A4], [C5] and most of M2 from other members.

---
Branch: `docs/hardware-bringup` | Reviewer: Member C | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [A6] Stretch: UdpInputSource for the future Python bridge
**Labels:** `area:arduino`, `stretch` | **Milestone:** M3 - Extension-Ready & Polish

### Why
Later signal-processing work may run in Python and send filtered values to Unity. Implementing the same `IPlayerInputSource` now means no game code changes then.

### Tasks
- [ ] `UdpInputSource` listening on a configurable port, same packet format as the serial protocol, background thread, same watchdog behaviour.
- [ ] Add 'UDP' as a selectable source in `PlayerSlot`.
- [ ] Test with a tiny Python sender script in `python/`.

### Done when
- [ ] Switching a slot from Serial to UDP changes nothing else in the game.

### Depends on
[A2], [A3]

---
Branch: `feature/udp-input-source` | Reviewer: Member C | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


## Member B: Ball & Tile physics

### [B1] Physics setup, layers & sandbox scene
**Labels:** `area:physics` | **Milestone:** M1 - Foundation

### Why
Getting layers, materials and body types right once prevents most 'ball glitch' bugs. Tunnelling happens when a fast ball moves farther than a collider's thickness in one physics step (for example 15 u/s x 0.02 s = 0.3 u). Continuous detection sweeps the path instead of only checking end positions.

### Tasks
- [ ] Layers: `Ball, Paddle, Wall, Tile, Goal`; set the Physics 2D collision matrix.
- [ ] `PhysicsMaterial2D` bounciness 1, friction 0. Gravity 0.
- [ ] Body types: ball Dynamic + *Continuous* + *Interpolate*; paddles Kinematic; walls Static. Choose and document the fixed timestep (0.01-0.02 s).
- [ ] `Dev_PhysicsSandbox.unity` with a closed box arena for testing.
- [ ] Write `docs/physics.md` explaining each choice.

### Done when
- [ ] Documented. A test ball bounces around the sandbox for 5 minutes without escaping or visibly changing speed.

### Depends on
[C0]

---
Branch: `feature/physics-setup` | Reviewer: Member D | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [B2] BallController: constant speed, serve API & modifier stack
**Labels:** `area:physics` | **Milestone:** M2 - Playable Core

### Why
Elastic collisions in a physics engine slowly gain or lose energy through numerical error, so speed is re-imposed every step. A *modifier stack* lets power-ups, tiles and abilities change ball speed without editing this class.

### Tasks
- [ ] Every `FixedUpdate`: `velocity = velocity.normalized * currentSpeed`. On Unity 6, use `Rigidbody2D.linearVelocity` (renamed from `velocity`).
- [ ] `currentSpeed = baseSpeed * hitRamp^hits (capped) * product(modifiers)`.
- [ ] API: `Serve(Vector2 dir)`, `ResetToCenter()`, `Freeze()`, `Unfreeze()`.
- [ ] Anti-stall guard: keep `|dir.x| >= 0.25` so the ball cannot bounce vertically forever.
- [ ] `BallModifierStack`: `Add(key, IBallModifier)`, `Remove(key)`, optional auto-expiry time.
- [ ] All numbers in `BallConfig`. Raise `BallHitWall` events.

### Done when
- [ ] In the sandbox, speed stays within +/-1% of the expected value each physics step.
- [ ] A modifier added from a test script changes speed with no edit to `BallController`.

### Depends on
[C0], [B1]

---
Branch: `feature/ball-controller` | Reviewer: Member D | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [B3] Paddle deflection, goal zones & serve logic
**Labels:** `area:physics` | **Milestone:** M2 - Playable Core

### Why
Classic Pong control comes from *where* the ball hits the paddle: offset from the centre sets the outgoing angle. The ball finds paddles through `IPaddleInfo`, so it never depends on `PaddleController`.

### Tasks
- [ ] `PaddleHitResolver`: `offset = clamp((ball.y - paddle.y) / (Height/2), -1, 1)`; angle = `offset * maxBounceAngle` (default 60 deg); direction mirrored by side.
- [ ] Optional `paddleVelocityInfluence` ('english') from `IPaddleInfo.Velocity` in `BallConfig`.
- [ ] Raise `BallHitPaddle`. Everything with `IBallHittable` is notified on contact.
- [ ] `GoalZone` triggers at left/right edges -> `GoalScored(scoringPlayerId)`. Guard against double-firing.
- [ ] Serve toward the player who conceded, random angle within +/-25 deg.

### Done when
- [ ] Edge hits give steeper angles than centre hits.
- [ ] Exactly one `GoalScored` event per goal.

### Depends on
[B2], [C3] (`IPaddleInfo` implemented by paddle)

---
Branch: `feature/deflection-goals` | Reviewer: Member D | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [B4] Tile system: GameplayTile + pluggable behaviours (Solid, Breakable, Bounce Pad)
**Labels:** `area:physics` | **Milestone:** M2 - Playable Core

### Why
Arenas need tiles with behaviour. Design: each `GameplayTile` asset points to a `TileBehaviour` ScriptableObject with `OnBallHit(context)`. A new tile type is a new behaviour class plus an asset; the core resolver never changes.

### Tasks
- [ ] Use Tilemap + `TilemapCollider2D`. `TileHitResolver` handles ball-vs-Tile-layer collisions: take the contact point, nudge it slightly along the ball's incoming direction (sign-safe) into the tile, call `tilemap.WorldToCell`, get the `GameplayTile`, run its behaviour.
- [ ] Behaviours: `SolidBehaviour`, `BreakableBehaviour` (HP; swap to a crack sprite then remove; raise `BallHitTile` / `TileDestroyed`), `BouncePadBehaviour` (adds a temporary speed modifier via `BallModifierStack`).
- [ ] Do **not** put breakable tiles in a CompositeCollider2D: composite merges colliders, so individual tile identity is lost. Use one 'Solid' tilemap (composite OK) and a separate 'Interactive' tilemap.
- [ ] Handle corner hits (two tiles): process only one tile per collision.
- [ ] Create a Tile Palette and a test arena with all three tile kinds.

### Done when
- [ ] Breaking and bouncing tiles work repeatedly.
- [ ] No `GetComponent` or allocations in the hit path.

### Depends on
[B2], [B1]

---
Branch: `feature/tile-system` | Reviewer: Member D | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [B5] PlayMode tests for ball & tiles
**Labels:** `area:physics`, `testing` | **Milestone:** M3 - Extension-Ready & Polish

### Why
Automated tests catch physics regressions the moment someone changes a config or script, and they are a good source of evidence for the final report.

### Tasks
- [ ] Unity Test Framework (PlayMode): speed constancy; ball never stuck on one axis for N seconds; deflection angle increases with offset; goal event fires once; breakable tile HP reaches zero after N hits.
- [ ] Use `ScriptedInputSource` from [C1] where input is needed.
- [ ] Put tests in `Assets/Tests/PlayMode` with the generated assembly definition.

### Done when
- [ ] All tests green in the Test Runner; a short how-to-run note in `docs/physics.md`.

### Depends on
[B2], [B3], [B4]

---
Branch: `feature/physics-tests` | Reviewer: Member D | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [B6] Extension proof: Speed Zone tile + Slow-Mo power-up with no core edits
**Labels:** `area:physics`, `extension-proof` | **Milestone:** M3 - Extension-Ready & Polish

### Why
Demonstrates that new tiles and ball effects need only new assets and behaviour classes. The same process will be used for the team's creative features.

### Tasks
- [ ] Create `SpeedZoneBehaviour` (tile) and `SlowMoModifier` (ball modifier). Add assets and place them in the test arena.
- [ ] Write `docs/EXTENDING.md`: 'Add a tile type' and 'Add a ball modifier' step-by-step.

### Done when
- [ ] The PR adds only new files plus assets; no diffs in `BallController`, `TileHitResolver`, or `Core`.

### Depends on
[B2], [B4]

---
Branch: `feature/extension-proof-physics` | Reviewer: Member D | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


## Member D: Arena & Main menu

### [D1] Scene flow & GameManager state machine
**Labels:** `area:arena-ui`, `blocker` | **Milestone:** M1 - Foundation

### Why
A state machine makes game flow explicit and extensible; new modes just add states or listeners. Scene ownership: only Member D edits `Boot`, `MainMenu` and `Game` scenes; everyone else delivers prefabs.

### Tasks
- [ ] Scenes: `Boot`, `MainMenu`, `Game`; add to Build Settings. Scene names in a `SceneNames` constants class (no string literals elsewhere).
- [ ] `GameManager` states: `Menu, Calibration, Countdown, Serving, Playing, PointScored, Paused, MatchOver`; enter/exit methods; raise `MatchStateChanged`.
- [ ] Consume `GoalScored` -> update a plain `MatchState` class (scores) -> check `MatchConfig.targetScore` -> `MatchOver`.
- [ ] Use stubs/interfaces for the ball serve and paddle spawn so the loop works before B/C finish.
- [ ] `SceneLoader` with fade; carry the runtime `MatchConfig` between scenes.

### Done when
- [ ] Menu -> Game -> Match over -> Menu loop works with stub objects.
- [ ] The state machine has no direct references to UI classes.

### Depends on
[C0]

---
Branch: `feature/game-manager` | Reviewer: Member B | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [D2] Main menu
**Labels:** `area:arena-ui` | **Milestone:** M2 - Playable Core

### Why
The first screen the demo audience sees. Build it in uGUI (Canvas + TextMeshPro), which has the most tutorials for beginners.

### Tasks
- [ ] Buttons: Play, Match Setup, How to Play, Quit. `CanvasScaler` set to Scale With Screen Size (1920x1080 reference).
- [ ] Mouse and keyboard navigation (`EventSystem`); check 16:9 and 16:10.
- [ ] Stretch: navigate the menu with the FSRs (up/down moves the highlight, both pressed selects) through an `IMenuNavigationSource` reading `PlayerInputState`.

### Done when
- [ ] All buttons work. No hard-coded scene-name strings.

### Depends on
[D1]

---
Branch: `art/main-menu` | Reviewer: Member B | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [D3] Match setup & settings screen
**Labels:** `area:arena-ui` | **Milestone:** M2 - Playable Core

### Why
Everything configurable by the players lives here and is written to a runtime copy of `MatchConfig`. Gotcha: editing a ScriptableObject *asset* at runtime in the Editor persists the change to disk, so always work on `Instantiate(config)`.

### Tasks
- [ ] Options: target score (5/7/11), ball speed preset (`BallConfig` variants), arena choice (from `ArenaRegistry` with thumbnail), input source per player (Keyboard / Arduino), volume.
- [ ] Embed the `SerialConnectionPanel` ([A3]) and a Calibrate button that opens `CalibrationPanel` ([A4]).
- [ ] Persist choices with `PlayerPrefs`.

### Done when
- [ ] Changing options changes the next match; settings survive a restart; no asset files are modified by playing.

### Depends on
[D1], [D4]; panels from [A3], [A4].

---
Branch: `feature/match-setup` | Reviewer: Member B | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [D4] Arena system + Arena 01/02 + auto camera fit
**Labels:** `area:arena-ui` | **Milestone:** M2 - Playable Core

### Why
Arenas must be *data*, not code. An `ArenaDefinition` asset describes an arena; adding the 10th arena should need no script changes.

### Tasks
- [ ] `ArenaDefinition` ScriptableObject (id, display name, thumbnail, arena prefab, optional `BallConfig` override) and an `ArenaRegistry`.
- [ ] `ArenaAnchors` component inside each arena prefab: bounds, paddle spawn points, ball spawn, goal zones.
- [ ] `ArenaLoader`: instantiates the arena under `ArenaRoot`, reads the anchors, publishes `ArenaBounds` for paddles, unloads cleanly.
- [ ] Auto-fit orthographic camera size to arena bounds + margin for any aspect ratio.
- [ ] Arena 01 'Classic' (top/bottom walls, goals). Arena 02 'Bunker' (breakable centre tiles from [B4]).

### Done when
- [ ] Choosing an arena in setup loads it with no code change; paddles respect its bounds.

### Depends on
[D1], [C0]; Arena 02 needs [B4].

---
Branch: `feature/arena-system` | Reviewer: Member B | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [D5] Game-flow UI: countdown, pause & results
**Labels:** `area:arena-ui` | **Milestone:** M2 - Playable Core

### Why
Players need clear feedback between points. Pause uses `Time.timeScale = 0`, so UI animation must use unscaled time.

### Tasks
- [ ] 3-2-1-GO countdown before every serve.
- [ ] Pause (`Esc`/`P`): resume, back to menu. Also shown when [A3] reports a disconnect.
- [ ] Results screen: winner, final score, Rematch, Main Menu.
- [ ] Event-driven: driven by `MatchStateChanged`.

### Done when
- [ ] A full match flows from countdown to results and back. Pause freezes ball and paddles.

### Depends on
[D1], [B3]

---
Branch: `feature/flow-ui` | Reviewer: Member B | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [D6] Audio & game-feel hooks (event-driven)
**Labels:** `area:arena-ui` | **Milestone:** M3 - Extension-Ready & Polish

### Why
If all feedback listens to `GameEvents`, any new feature gets sound and effects just by raising an existing or new event.

### Tasks
- [ ] `AudioManager` + `AudioConfig` ScriptableObject mapping events to clips: paddle hit, wall, tile break, goal, parry, countdown.
- [ ] `FeedbackManager`: screen shake (coroutine on camera), short hit-stop (~40 ms), particle burst on tile destroyed.
- [ ] Only use sounds you may legally use. Record every source in `docs/CREDITS.md`.

### Done when
- [ ] Every listed event has a sound; effects can be toggled in settings.

### Depends on
[C0], [B3]

---
Branch: `art/audio-feel` | Reviewer: Member B | Follow the workflow in `README.md` (branch -> PR -> review -> merge).


### [D7] Extension proof: Arena 03 from assets only + 'Add an arena' guide
**Labels:** `area:arena-ui`, `extension-proof` | **Milestone:** M3 - Extension-Ready & Polish

### Why
Verifies that arenas really are data-driven. Any place that needs a code edit to add an arena must be refactored.

### Tasks
- [ ] Build Arena 03 using only a new prefab, tiles, an `ArenaDefinition` and a registry entry.
- [ ] Add the section 'Add an arena in 5 minutes' to `docs/EXTENDING.md`.
- [ ] Fix and note every hard-coded assumption you discover.

### Done when
- [ ] PR contains no script changes except genuine refactors; the guide has screenshots.

### Depends on
[D4], [B4]

---
Branch: `feature/extension-proof-arena` | Reviewer: Member B | Follow the workflow in `README.md` (branch -> PR -> review -> merge).

