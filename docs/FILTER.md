# FSR input filters

This document describes the filters currently implemented for force-sensitive
resistor (FSR) input in Unity. The filter implementation is in
`Assets/Scripts/Input/Filters/Filters.cs`, and the filter chain is applied by
`Assets/Scripts/Input/PlayerInputPipeline.cs`.

## Available filters

The inspector currently exposes two values in the `FilterType` enum:

| Inspector value | Runtime class | Behavior |
|---|---|---|
| `PassThrough` | `PassThroughFilter` | Returns the input unchanged. |
| `Exponential` | `ExponentialFilter` | A first-order IIR low-pass filter, also known as an exponential moving average (EMA). |

These are the only filter choices currently implemented by
`FilterFactory`. The mentions of median, moving-average, and other filters in
the project overview are future analysis/design work; they are not currently
available in the Unity inspector.

## Where filtering occurs

For each player, the pipeline processes the two corresponding sensor channels
independently:

```text
raw FSR ADC value (0..1023)
    -> calibration normalisation (hardware input)
    -> filter chain (Up and Down independently)
    -> clamp to 0..1
    -> deadzone and rescaling
    -> PlayerInputState.Up / Down
    -> paddle velocity = Up - Down
```

The chain is used for Serial and UDP hardware sources, as well as for the
other input sources that pass through `PlayerInputPipeline`. Calibration
normalises hardware readings before filtering; therefore filter parameters
operate on normalised values, not raw ADC counts. The calibrated signal is
normally in the `0..1` range, and the final values are clamped before the
deadzone is applied.

Player 1 uses channels 0 and 1 (`Up` and `Down`); player 2 uses channels 2 and
3. Each channel has its own filter instances and state. A change in the Up
signal does not alter the Down signal.

If no valid sample arrives, or the newest sample is older than the pipeline’s
`staleTimeout`, the pipeline outputs zero and resets every filter. This avoids
resuming from an old, possibly high FSR value when the device reconnects.

## PassThrough

`PassThroughFilter` implements:

```text
y = x
```

The `dt` argument is ignored and the filter has no history. `Reset()` has no
effect. This is the default stage in the generated paddle prefab and is useful
as a baseline: it introduces no smoothing and no intentional filter delay
(apart from the rest of the input and game update pipeline).

Use `PassThrough` when:

- the sensor signal is already sufficiently stable;
- immediate response is more important than noise suppression; or
- you want to compare gameplay against an unfiltered baseline.

## Exponential

`ExponentialFilter` is a first-order low-pass filter with one state value
`y`. For every valid pipeline tick it calculates:

```text
alpha = 1 - exp(-dt / tau)
y     = y + alpha * (x - y)
```

where:

- `x` is the current normalised input;
- `y` is the previous filtered output;
- `dt` is the elapsed time supplied by the pipeline, in seconds; and
- `tau` is the inspector’s `timeConstant`, in seconds.

The constructor clamps `tau` to zero or greater. A positive `tau` smooths
rapid changes: larger values produce more smoothing and more response delay;
smaller values follow the input more closely. The coefficient uses the
exponential form rather than a fixed per-frame blend, so the response is
substantially independent of frame rate.

For a unit step from zero to one, the output reaches:

```text
1 - e^-1 = 0.632 (63.2%)
```

after one time constant. This is the meaning of the tooltip's "63% of a step
after tau" description. It is a time-domain setting, not a cutoff frequency
entered directly in hertz. As an approximate reference for a first-order
low-pass, the -3 dB cutoff is:

```text
fc = 1 / (2 * pi * tau)
```

For example, `tau = 0.05` seconds corresponds to approximately 3.18 Hz.

### Edge cases

- `timeConstant = 0`: the filter becomes a pass-through for that stage
  (`y = x`).
- `dt <= 0` with a positive time constant: the existing state is returned and
  is not advanced.
- `Reset()`: the state is set to zero.
- A stale/disconnected input: the pipeline resets both the Up and Down
  instances, so the next valid sample starts from zero rather than an old
  output.

The implementation starts its state at zero. Consequently, enabling an
exponential filter can briefly ramp up from zero when a non-zero sensor value
first becomes valid.

## Filter chains

`PlayerInputPipeline.filterChain` is a serialized `List<FilterStage>`. The
pipeline creates one runtime filter instance for each list element for the Up
channel and a separate matching instance for the Down channel. Stages are
executed in list order:

```text
normalised input
    -> stage 0
    -> stage 1
    -> ...
    -> filtered input
```

For example, two exponential stages with time constants `0.03` and `0.05`
seconds apply the exponential operation twice, in that order. This generally
produces a stronger, higher-order smoothing effect than either stage alone,
with additional response delay. A `PassThrough` stage does not change the
value, but it can be retained as an explicit baseline stage.

Every `FilterStage` has:

| Field | Meaning |
|---|---|
| `type` | Selects the implementation through `FilterFactory`. |
| `timeConstant` | Used by `Exponential`; displayed in seconds and constrained to `>= 0`. It is ignored by `PassThrough`. |

The runtime chain is rebuilt when the component is enabled and whenever Unity
calls `OnValidate`, including an inspector change during Play mode. The same
stage configuration is used for both Up and Down, while their filter state
remains independent.

## Selecting a filter in the Unity inspector

1. Open the scene or prefab containing the input pipeline. In the normal game
   setup this is the `Paddle` prefab; the component is also present on the
   FSR menu-navigation object in the main menu scene.
2. Select the GameObject that has the **Player Input Pipeline** component.
3. In **Filter Chain**, expand the list.
4. Set **Size** to the number of stages you want. The default generated
   configuration contains one stage.
5. Expand each list element and choose **Type**:
   - choose **PassThrough** for no filtering;
   - choose **Exponential** for smoothing.
6. When **Exponential** is selected, set **Time Constant** in seconds. The
   value is clamped to zero or greater; use `0` for pass-through behavior.
7. Put stages in the desired order. The first list element processes the
   normalised value first.
8. Enter Play mode and use the input debug HUD, if enabled, to see the
   configured chain. It reports `pass` for `PassThrough` and
   `EMA(tau=...ms)` for `Exponential`.

The filter list belongs to each `PlayerInputPipeline`, so changing the
`Paddle` prefab changes newly spawned paddles that use that prefab. A scene
object with its own pipeline can have a different chain. Changes made to an
instance may be overridden by later prefab changes according to Unity’s normal
prefab override rules.

The generated `Paddle` prefab currently serializes one `PassThrough` stage with
`timeConstant = 0.05`; that value has no effect until the stage type is changed
to `Exponential`. The pipeline’s `staleTimeout` is separate from the filter and
controls when missing data causes zero output and a filter reset.

## Deadzone

A deadzone suppresses very small input values. FSRs can report a small
non-zero reading when released because of sensor tolerance, electrical noise,
mechanical pressure, or calibration drift. Without a deadzone, those readings
can make a paddle creep even when the sensor is not intentionally pressed.

The game's deadzone is a per-channel threshold applied after filtering and
after the filtered value is clamped to the `0..1` range. It is applied
independently to `Up` and `Down`:

```text
if value <= deadzone:
    output = 0
else:
    output = (value - deadzone) / (1 - deadzone)
```

The second branch rescales the surviving range so that a full-scale input
still produces `1.0`. For example, with `deadzone = 0.05`:

| Filtered value | Final channel value |
|---:|---:|
| `0.03` | `0` |
| `0.05` | `0` |
| `0.50` | `(0.50 - 0.05) / 0.95 = 0.474` |
| `1.00` | `1.00` |

This is a **scaled deadzone**, not just a threshold. Values above the
threshold do not keep their original magnitude; they are shifted and expanded
back to the full `0..1` range. The comparison is inclusive, so a value exactly
equal to the deadzone becomes zero.

The deadzone does not alter the raw serial data, calibration values, or
filter-history state. It only changes the final `PlayerInputState.Up` and
`Down` values used to calculate paddle velocity:

```text
velocity = final Up - final Down
```

If `paddleConfig` is not assigned to a pipeline, the code uses a deadzone of
`0`. The generated `PaddleConfig` asset normally uses `0.05` (5% of the
normalised channel range).

### Tuning the deadzone in Unity

1. In the **Project** window, locate the `PaddleConfig` asset. The generated
   asset is under `Assets/ScriptableObjects/Core/PaddleConfig.asset`.
2. Select the asset and, in the **Inspector**, find **Deadzone** under the
   paddle configuration fields.
3. Set **Deadzone** between `0` and `1`. Unity displays this field as a
   slider because it is marked with `[Range(0f, 1f)]`.
4. Ensure the relevant `PlayerInputPipeline` references this same
   `PaddleConfig` asset. The normal `Paddle` prefab has this reference in its
   **Paddle Config** field.
5. Enter Play mode and observe the input debug HUD. Compare `norm` (the value
   before filtering/deadzone) with `filtered` (the final Up/Down state) while
   the sensor is released and while applying a light press.
6. Increase the value until released-sensor movement stops, then reduce it if
   light intentional presses no longer respond. Recalibrate first if the
   released readings or sensor ranges have changed substantially.

Typical effects:

- `0`: preserves every non-negative normalised signal, including small noise.
- Small values such as `0.02` to `0.10`: commonly remove idle creep while
  retaining light presses; the correct value depends on the hardware.
- Large values: require more pressure before movement starts and can make
  delicate control feel less responsive.
- `1`: makes every value zero because no value can be greater than `1`; do not
  use it for playable input.

Changing the shared `PaddleConfig` changes every pipeline that references it,
including spawned paddles. If only one player or one object should differ,
assign a separate `PaddleConfig` asset or use an intentional prefab/scene
override rather than changing an unrelated shared asset.

## Choosing values

There is a direct trade-off:

- lower `timeConstant`: faster response, less smoothing, more sensor noise;
- higher `timeConstant`: smoother response, more lag, slower reaction to a
  change in pressure.

Tune the value with the actual sensors and game feel. Compare the HUD's
normalised and filtered values, and remember that paddle velocity uses the
difference between the filtered Up and Down channels. Filtering both channels
can reduce jitter, but excessive smoothing can make direction changes feel
late.

The edit-mode tests in `Assets/Tests/EditMode/FilterTests.cs` verify the
63%-after-one-time-constant behavior, frame-rate independence, reset behavior,
pass-through behavior, and factory selection.
