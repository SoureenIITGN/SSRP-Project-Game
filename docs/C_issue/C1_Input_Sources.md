# C1 — Input Sources: Implementation and Setup Documentation

## 1. Objective

The objective of this task was to implement a flexible input-source system for the two-player Pong game.

Each player requires two input channels:

- Up
- Down

Initially, keyboard inputs are used to simulate the FSR readings. Later, the same interface will allow the game to receive artificially generated signals or actual Arduino readings.

Three input sources were implemented:

1. `KeyboardInputSource`
2. `ScriptedInputSource`
3. `SerialInputSource`

A `PlayerSlot` component was also implemented to allow each player to select an input source through the Unity Inspector.

The optional `NoiseInjector` component has not yet been implemented.

---

## 2. Architecture

All three input sources implement the common interface:

`IPlayerInputSource`

The interface provides:

- `IsConnected`: indicates whether the source is available.
- `TryGetLatest(int playerId, out RawFsrSample sample)`: retrieves the latest readings for a particular player.

The readings are represented using `RawFsrSample`, containing:

- `Up`: upper sensor reading.
- `Down`: lower sensor reading.
- `Time`: timestamp associated with the sample.

The values are maintained in the raw Arduino ADC range of 0–1023.

### Architecture overview

```text
                   PlayerSlot
                       |
              Selects input source
                       |
          +------------+------------+
          |            |            |
       Keyboard     Scripted      Serial
       Source        Source       Source
          |            |            |
          +------------+------------+
                       |
                IPlayerInputSource
                       |
                  RawFsrSample
                       |
              Future signal pipeline
```

The purpose of this design is to ensure that the rest of the game does not need to know how the readings were generated or acquired.

For example, a paddle controller should be able to request readings through the same interface regardless of whether the source is a keyboard, generated signal, or Arduino.

---

## 3. KeyboardInputSource

### Purpose

This component simulates the four FSR channels using keyboard inputs.

The key mapping is:

| Player | Up | Down |
|---|---|---|
| Player 1 | W | S |
| Player 2 | Up Arrow | Down Arrow |

### Ramp behaviour

Holding a key gradually increases its corresponding channel from 0 towards 1023.

Releasing the key gradually decreases the reading towards 0.

The ramp duration is controlled using `rampSeconds`, which defaults to approximately 0.1 seconds.

This simulates a changing pressure reading rather than an instantaneous binary key press.

Pressing both keys assigned to a player causes both channels to increase. This is useful for testing the differential input and the future parry feature.

### Input System issue encountered

The implementation was changed to use:

`Keyboard.current.wKey.isPressed`

The corresponding keys for the other channels were also updated to use the new Input System.

The project is configured with Active Input Handling set to Input System Package (New).

### Testing

The following were tested:

- Holding W increases Player 1 Up.
- Releasing W decreases Player 1 Up.
- Holding S changes Player 1 Down.
- Holding both W and S changes both channels.
- The arrow keys control Player 2's channels.

A temporary `OnGUI()` method was used to display the readings during testing. It was only a diagnostic display and is not part of the final input architecture.

---

## 4. ScriptedInputSource

### Purpose

This component generates artificial FSR readings without requiring keyboard presses or physical sensors.

It is useful for automated testing and for studying how the signal-processing pipeline responds to known input patterns.

Two patterns were implemented.

### A. Sine Sweep

The sine-sweep pattern generates a sinusoidal signal whose frequency gradually increases over time.

The frequency changes from a configurable minimum to a configurable maximum over the sweep duration.

The two channels are generated in opposite directions:

\[
Up = 511.5 + 511.5\sin(\phi(t))
\]

\[
Down = 511.5 - 511.5\sin(\phi(t))
\]

Therefore:

\[
Up + Down = 1023
\]

This provides a predictable differential signal for later testing.

The sweep duration and minimum and maximum frequencies can be configured through the Inspector.

### B. Step Pattern

The step pattern switches between four states at regular intervals.

| State | Up | Down |
|---|---:|---:|
| 1 | 0 | 0 |
| 2 | 1023 | 0 |
| 3 | 1023 | 1023 |
| 4 | 0 | 1023 |

The pattern repeats continuously.

Player 2 uses a one-step offset so that its pattern can be distinguished from Player 1's.

### Testing

The component was tested independently in Unity.

- The Sine Sweep pattern produced continuously varying readings.
- The Step pattern produced the expected changes between the four states.
- Both players returned readings within the 0–1023 range.

The pattern can be selected through the ScriptedInputSource Inspector settings.

---

## 5. SerialInputSource

### Purpose

This component is intended to receive FSR readings from an Arduino through a USB serial connection.

The Arduino reads the sensors using its ADC and sends the readings to Unity.

### Serial data format

The current implementation expects one comma-separated line per sample:

```text
PlayerID,Up,Down
```

Example:

```text
1,350,120
2,700,250
```

The Arduino must terminate each sample with a newline, for example by using `Serial.println()`.

The configured baud rate in Unity must match the baud rate used by the Arduino.

### Processing received data

The component:

1. Opens the configured COM port on the first `TryGetLatest()` call.
2. Reads incoming serial data.
3. Buffers incoming characters until a complete newline-terminated line is available.
4. Splits the line into three values.
5. Converts the values into a player ID, Up reading, and Down reading.
6. Checks that the player ID is valid.
7. Clamps the readings to the 0–1023 range.
8. Stores the latest sample for the corresponding player.

The same `TryGetLatest()` interface is then used to retrieve the readings.

### Compilation issue encountered

Initially, Unity reported:

- CS0234: The namespace `System.IO.Ports` could not be found.
- CS0246: The type `SerialPort` could not be found.

The code was importing:

`using System.IO.Ports;`

The project configuration did not initially expose the required serial-port library to the compiler.

### Solution

The API Compatibility Level was changed under:

Edit → Project Settings → Player → Other Settings → Configuration.

The project was changed to use .NET Framework compatibility, after which Unity successfully compiled the SerialInputSource code.

### Current testing status

The SerialInputSource code was compiled successfully after the compatibility setting was changed.

Physical Arduino-to-Unity communication has not yet been validated.

The current implementation expects the serial format described above, so the Arduino code must follow the same format.

---

## NoiseInjector

### Purpose

NoiseInjector is a wrapper implementing `IPlayerInputSource`.

It receives readings from another input source, adds artificial noise, and returns the modified readings in the same `RawFsrSample` format.

This allows the team to test the effect of signal filtering using controlled synthetic disturbances without requiring physical sensors.

### Noise models

Two types of noise are supported.

#### A. Gaussian noise

Gaussian noise is generated using the Box-Muller transform.

\[
n_g=\sigma\sqrt{-2\ln(U_1)}\cos(2\pi U_2)
\]

The generated noise has a zero mean and configurable standard deviation.

It is added independently to the Up and Down channels.

#### B. Rare spikes

Each channel has a configurable probability of receiving a sudden disturbance.

The spike magnitude and probability are configurable through the Unity Inspector. The spike direction is randomly positive or negative.

### Output

The modified values are clamped to the raw ADC range:

\[
0\leq Up,Down\leq1023
\]

The wrapper preserves the underlying sample timestamp.

### Configuration

The Inspector exposes:

- Source Component
- Enable Gaussian Noise
- Gaussian Standard Deviation
- Enable Spikes
- Spike Probability
- Spike Magnitude

The selected Source Component must implement `IPlayerInputSource`.

### Testing

The following tests should be performed independently:

1. Gaussian noise enabled and spikes disabled.
2. Spikes enabled and Gaussian noise disabled.
3. Both noise models enabled.

The output should show small random fluctuations for Gaussian noise and occasional larger disturbances for the spike model.

### Integration

PlayerSlot includes a `NoiseInjected` input-source option.

When selected, it obtains readings from NoiseInjector instead of directly using Keyboard, Scripted or Serial.

The underlying source can be changed through the NoiseInjector Inspector reference.

### Limitations

The current implementation generates fresh noise each time `TryGetLatest()` is called. Therefore, the effective noise behaviour depends on the rate at which the caller requests samples.

The model is intended for software testing and does not reproduce every physical noise mechanism in an FSR or ADC circuit.

## 6. PlayerSlot

### Purpose

PlayerSlot connects an individual player to the selected input source.

It does not read the keyboard or communicate with the Arduino itself.

Instead, it selects the required source and requests readings through `IPlayerInputSource`.

### Inspector configuration

The component provides:

- Player ID
- Input Source Type
- Keyboard Source
- Scripted Source
- Serial Source

The input-source dropdown contains:

- Keyboard
- Scripted
- Serial

The three references use their respective concrete C# component types rather than the generic `MonoBehaviour` type. This ensures that Unity assigns the intended component to each field.

### Player configuration

Both PlayerSlots can reference the same InputManager GameObject.

| Setting | Player 1 | Player 2 |
|---|---|---|
| Player ID | 1 | 2 |
| Keyboard Source | KeyboardInputSource | KeyboardInputSource |
| Scripted Source | ScriptedInputSource | ScriptedInputSource |
| Serial Source | SerialInputSource | SerialInputSource |

Only the source selected in the dropdown is used.

An unassigned source can remain empty if it is not selected.

---

## 7. Unity Hierarchy Setup

The intended hierarchy after all input components have been integrated is:

```text
Hierarchy
├── Main Camera
├── InputManager
│   ├── KeyboardInputSource
│   ├── ScriptedInputSource
│   └── SerialInputSource
├── Player1Input
│   └── PlayerSlot
└── Player2Input
    └── PlayerSlot
```

### Important note about SerialInputSource

The SerialInputSource opens its configured COM port lazily on the first
`TryGetLatest()` call. This prevents an unused serial source from attempting
to connect merely because its GameObject is enabled. A failed connection is
reported in the Unity Console and is retried when the component is disabled
and enabled again.

---

## 8. Errors and Troubleshooting Summary

| Issue | Cause | Resolution |
|---|---|---|
| Keyboard values remained unchanged | Old Input Manager API was not detecting the keys | Switched keyboard polling to the new Input System |
| Old Input W remained False while New Input W was True | Different behaviour between the two input APIs in the current project setup | Used `Keyboard.current` |
| SerialInputSource CS0234 error | `System.IO.Ports` namespace unavailable | Changed API Compatibility Level |
| SerialInputSource CS0246 error | `SerialPort` type unavailable because its namespace/library was not accessible | Changed API Compatibility Level |
| All PlayerSlot references displayed KeyboardInputSource | All three fields were declared as generic MonoBehaviour references | Changed fields to their concrete component types |
| Unity GameView NullReferenceException appeared during Play Mode transition | Editor-side exception referencing `UnityEditor.GameView` | Treated separately from the input-code issue; it was not established as the cause of the keyboard problem |

---

## 9. Instructions for Teammates

### A. Obtain the branch

After the C1 Pull Request is created, teammates can retrieve the branch using:

```bash
git fetch origin
git switch --track origin/C1
```

If the local branch already exists:

```bash
git switch C1
git pull
```

Replace `C1` if the actual branch name differs.

Close Unity before switching branches.

### B. Open the project

1. Open Unity Hub.
2. Select Add → Add project from disk.
3. Select the cloned repository folder containing `Assets`, `Packages`, and `ProjectSettings`.
4. Open it using the Unity Editor version specified in `ProjectSettings/ProjectVersion.txt`.
5. Wait for Unity to finish importing assets and compiling scripts.

### C. Check project settings

Verify:

- Active Input Handling supports the New Input System.
- API Compatibility Level matches the committed project configuration.
- The Input System package is available.

Avoid independently changing shared project settings unless necessary.

### D. Configure the scene

1. Create or select the InputManager GameObject.
2. Attach KeyboardInputSource.
3. Attach ScriptedInputSource.
4. Attach SerialInputSource only when ready to test serial communication.
5. Create Player1Input and Player2Input GameObjects.
6. Attach PlayerSlot to each.
7. Set Player IDs to 1 and 2 respectively.
8. Assign the appropriate input-source component references.

### E. Test Keyboard

Select Keyboard for both PlayerSlots.

Run the game and test:

- W and S for Player 1.
- Up Arrow and Down Arrow for Player 2.

### F. Test Scripted

Select Scripted for a PlayerSlot.

Test both Sine Sweep and Step patterns using the ScriptedInputSource Inspector settings.

### G. Test Serial

Only after the Arduino and serial connection are ready:

1. Identify the Arduino COM port.
2. Ensure the Arduino uses the agreed baud rate.
3. Ensure it sends newline-terminated `PlayerID,Up,Down` lines.
4. Close the Arduino Serial Monitor.
5. Configure the COM port in SerialInputSource.
6. Attach and enable the component.
7. Select Serial in the appropriate PlayerSlot.
8. Verify that readings are received.

---

## 10. Remaining Work

The following tasks are not included in this implementation:

- Optional NoiseInjector component.
- Physical Arduino serial communication validation.
- Final connection of PlayerSlot to the paddle movement controller.
- Integration with the signal-filtering pipeline.
- Full gameplay testing.

These can be implemented in subsequent feature branches.

---

## 11. Summary

The C1 implementation establishes a common input-source architecture for the two-player Pong project.

Keyboard, scripted, and serial inputs are designed to provide readings through the same `IPlayerInputSource` interface and `RawFsrSample` format.

This allows the project to develop and test its signal-processing and gameplay systems without being tightly coupled to a particular input device.

The keyboard and scripted sources have been tested in Unity. The serial source compiles, but physical hardware validation remains outstanding.