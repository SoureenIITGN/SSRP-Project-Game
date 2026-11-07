# FSR calibration

## What controls paddle velocity?

The Arduino sends four **raw 10-bit ADC readings** (`0` to `1023`) and does not
apply the paddle formula or calibration. The channel order is:

| Channel | Sensor |
|---|---|
| 0 / A0 | Player 1 Up |
| 1 / A1 | Player 1 Down |
| 2 / A2 | Player 2 Up |
| 3 / A3 | Player 2 Down |

For a hardware input, Unity processes each channel through this path:

```text
raw ADC
  -> calibration normalisation
  -> configured filter chain
  -> deadzone and rescaling
  -> Up/Down values in [0, 1]
  -> Velocity = Up - Down
  -> target paddle speed = PaddleConfig.maxSpeed * Velocity
```

The calibration normalisation is:

```text
if raw <= max:
    normalised = normalizationUpperLimit * (raw - baseline) / (max - baseline)
else:
    normalised = lerp(normalizationUpperLimit, 1,
                      (raw - max) / (normalizationForceMax - max))
```

This piecewise mapping makes the calibrated firm-grip value (`max`) equal to
`normalizationUpperLimit` (default `0.75`). A force above firm grip continues
to increase the value until `normalizationForceMax` reaches `1.0`. Values
below the baseline are clamped to zero. `normalizationForceMax` is a raw ADC
threshold between `0` and `1023`; for example, setting it to `910` makes a
raw reading of `910` map to `1.0`. The implementation ensures this threshold
cannot be below the calibrated firm-grip value.

This setting changes Unity's raw-to-normalised mapping; it does not change the
Arduino's serial text. The serial stream continues to report raw ADC values
from `0` to `1023`.

For player 1, for example:

```text
Velocity = filtered(P1 Up) - filtered(P1 Down)
target speed = maxSpeed * Velocity
```

Therefore the original model

```text
v = K * (Fup - Fdown)
```

is conceptually correct, but in the current Unity implementation `Fup` and
`Fdown` are **calibrated, filtered values from 0 to 1**, not raw ADC counts.
`K` is not learned separately for each user: it is the shared
`PaddleConfig.maxSpeed` value, in Unity units per second. A full-strength
calibrated Up press gives approximately `+maxSpeed *
normalizationUpperLimit`; a force above firm grip can approach `+maxSpeed`.
The equivalent Down values are negative. If both sensors are pressed equally,
their difference is near zero.

If `PaddleConfig.acceleration` is non-zero, the current speed approaches the
target speed gradually. The paddle is moved by `MovePosition` in
`FixedUpdate`, and arena bounds can clamp its actual movement.

## What calibration does

Calibration measures the useful range of **each physical sensor independently**:

- **Baseline:** the median raw ADC value while the sensor is released.
- **Max:** the 95th-percentile raw ADC value while the sensor is held firmly.

These values compensate for sensor-to-sensor differences, mounting pressure,
component variation, and a non-zero resting reading. They make the pressure
used during the guided “press FIRMLY and hold” step correspond to approximately
`1.0` for that channel during play. Calibration does not change
`maxSpeed`, the filter settings, the deadzone, or the paddle’s physical
maximum speed.

The default calibration is `baseline = 0`, `max = 1023` for all channels. It
is used when no saved calibration exists, and is also used after choosing
**Use defaults**. Calibration is applied only to Serial and UDP hardware input;
keyboard and scripted sources already use the default 0–1023 range.

## Full calibration

Use **Match Setup > Calibrate sensors... > Full calibration**:

1. Connect the Arduino (or a connected UDP hardware source).
2. Keep all four sensors released for 2 seconds.
3. Press and hold **P1 Up**, **P1 Down**, **P2 Up**, and **P2 Down** firmly,
   one channel at a time, for 3 seconds each.
4. The first 0.5 seconds of every step is ignored so the user can settle.
5. The routine calculates the baseline and max values, then validates every
   channel.
6. If validation succeeds, Unity saves the result and uses it immediately.

The median and 95th percentile reduce the effect of individual noisy spikes.
The calibration is rejected if any channel moves less than 150 ADC counts. A
movement below 30 counts is reported as a likely non-responding sensor or
wiring problem.

The saved JSON file is named `ssrp_calibration.json` under Unity’s
`Application.persistentDataPath`. It is loaded on a later run. A hardware
match can be configured to require a saved calibration before starting.

## Quick re-zero

**Quick re-zero** performs only the released-sensor step. It replaces each
channel’s baseline with a new median, while preserving the existing max values.
Use it when the sensors have drifted or their unloaded readings changed, but
do not use it as a substitute for the first full calibration.

## Practical interpretation of “firm grip”

“Firm grip” is a reference action for measuring each channel’s upper operating
point, not a request to calculate a new velocity constant. During a match, a
force close to the force used during that step produces the configured
`normalizationUpperLimit` (normally `0.75`). A force above firm grip can
continue toward `1.0`; the configured `maxSpeed` determines the corresponding
`Vmax` or `Vmin`.

The mapping is linear from baseline to firm grip, then linear again from firm
grip to the ADC ceiling. Consequently, pressing harder than the recorded max
can increase the paddle speed beyond the firm-grip speed, but never beyond
`maxSpeed`. A resting reading below the recorded baseline becomes zero after
clamping. The per-channel deadzone is applied afterward.

## Code locations

- Raw ADC acquisition: `arduino/ssrp_fsr/ssrp_fsr.ino`
- Serial parsing and raw channel access:
  `Assets/Scripts/Input/SerialInputSource.cs`
- Calibration capture and validation:
  `Assets/Scripts/Input/Calibration/CalibrationRoutine.cs`
- Calibration data persistence and statistics:
  `Assets/Scripts/Input/Calibration/CalibrationData.cs`
- Raw-to-normalised input pipeline:
  `Assets/Scripts/Input/PlayerInputPipeline.cs`
- Differential input state:
  `Assets/Scripts/Core/PlayerInputState.cs`
- Paddle speed and movement:
  `Assets/Scripts/Gameplay/Paddle/PaddleController.cs`
- Shared speed/deadzone settings:
  `Assets/Scripts/Core/PaddleConfig.cs`

## Match CSV recording

During `Playing`, Unity records one row per physics step and writes
`ssrp_match_data.csv` under `Application.persistentDataPath` when the match
enters `MatchOver`. Starting a new match clears the previous samples, so the
file is replaced with the most recent completed game.

The columns are:

```text
Up1,Down1,Speed1,Up2,Down2,Speed2
```

`Up1` through `Down2` are raw input values clamped to `0-1023`. Keyboard input
is feasible because its source already generates synthetic `0-1023` readings;
Arduino (USB) is feasible because its serial source supplies raw ADC readings
in the same range. Calibration and normalization do not change these CSV
input columns. `Speed1` and `Speed2` are the actual magnitudes of the two
paddles' movement velocities in Unity units per second.

Calibration samples are also exported to `ssrp_calibration_data.csv` in the
same `Application.persistentDataPath` directory. The CSV columns are
`Up1,Down1,Up2,Down2`, containing every raw four-channel sample accepted after
the settle period during the calibration routine. The file is written when
calibration completes, including when validation rejects the calibration.
