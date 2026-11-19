# FSR input filters

This document describes the filters currently implemented for force-sensitive
resistor (FSR) input in Unity. The filter implementation is in
`Assets/Scripts/Input/Filters/Filters.cs`, and the filter chain is applied by
`Assets/Scripts/Input/PlayerInputPipeline.cs`.

## Available filters

The inspector currently exposes five filtering values in the `FilterType` enum:

| Inspector value | Runtime class | Behavior |
|---|---|---|
| `PassThrough` | `PassThroughFilter` | Returns the input unchanged. |
| `Exponential` | `ExponentialFilter` | A first-order IIR low-pass filter, also known as an exponential moving average (EMA). |
| `MovingAverage` | `MovingAverageFilter` | A finite window average of the most recent samples. |
| `Median` | `MedianFilter` | The median of the most recent samples; effective against isolated spikes. |
| `Butterworth` | `ButterworthFilter` | A cascaded digital low-pass with configurable cutoff frequency and order. |

All five choices are implemented by `FilterFactory`. The Arduino anti-alias
filter is a separate hardware-side filter and is not one of these Unity
filter-chain choices.

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

## Moving Average

`MovingAverageFilter` stores the latest `N` samples and returns their
arithmetic mean. `windowSize` is measured in samples, not seconds:

```text
y[n] = (x[n] + x[n-1] + ... + x[n-N+1]) / N
```

During startup, it averages only the samples received so far, so it does not
start with artificial zeroes. A larger window removes more rapid noise but
adds more lag and makes a sudden input change ramp across the window. The
approximate time span of the window is `(N - 1) * dt`; unlike the exponential
filter, its behavior depends on the actual sample/update rate.

Use an odd or even window depending on the desired response; unlike the median
filter, both are valid. A window of `1` is equivalent to pass-through.

## Median

`MedianFilter` sorts the latest `N` samples and returns the middle value (or
the average of the two middle values for an even-sized window). It is
particularly good at rejecting isolated spikes without smearing a step as
strongly as an average:

```text
y[n] = median(x[n], x[n-1], ..., x[n-N+1])
```

`windowSize` is a sample count and is capped at 101 samples. Larger windows
reject longer disturbances but delay the point at which a sustained change
reaches the output. A window of `1` is pass-through. Median filtering is
nonlinear, so a single fixed cutoff frequency or phase-delay formula does not
fully describe its behavior.

## Butterworth

`ButterworthFilter` is a digital low-pass assembled from cascaded first- and
second-order sections. Its two controls are:

- `cutoffFrequency` (`fc`): the approximate -3 dB cutoff in Hz;
- `order`: 1 to 4, controlling the steepness of the roll-off.

Increasing `cutoffFrequency` makes the signal respond faster and preserves
more high-frequency movement. Decreasing it smooths more aggressively and
increases phase delay. Increasing `order` makes attenuation beyond the cutoff
steeper, but adds phase shift and transient settling time. It does not replace
the cutoff setting: cutoff chooses the frequency/time scale, while order
chooses the roll-off shape.

The cutoff must be below half the effective sample rate. Because the pipeline
updates once per Unity frame, use a cutoff comfortably below the frame-rate
Nyquist frequency. The implementation recalculates its coefficients from
`dt`, and clamps the normalized cutoff internally for safety. This filter is
causal and therefore has phase delay; it is not a zero-phase/offline filter.

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
| `timeConstant` | `Exponential`: seconds to reach 63.2% of a step. |
| `windowSize` | `MovingAverage`/`Median`: number of samples in the rolling window, from 1 to 101. |
| `cutoffFrequency` | `Butterworth`: approximate -3 dB cutoff in Hz. |
| `order` | `Butterworth`: order from 1 to 4. Higher values have steeper roll-off and more delay. |

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
   - choose **MovingAverage** and set **Window Size** in samples;
   - choose **Median** and set **Window Size** in samples;
   - choose **Butterworth** and set **Cutoff Frequency** and **Order**.
6. Configure only the fields relevant to the selected type:
   - **Exponential**: set **Time Constant** in seconds;
   - **MovingAverage** or **Median**: set **Window Size**;
   - **Butterworth**: set **Cutoff Frequency** in Hz and **Order**.
7. Put stages in the desired order. The first list element processes the
   normalised value first.
8. Enter Play mode and use the input debug HUD, if enabled, to see the
   configured chain. It reports `pass` for `PassThrough` and
   `EMA(tau=...ms)`, `MA(N=...)`, `median(N=...)`, or
   `Butterworth(order=..., fc=...Hz)` for the other types.

The filter list belongs to each `PlayerInputPipeline`, so changing the
`Paddle` prefab changes newly spawned paddles that use that prefab. A scene
object with its own pipeline can have a different chain. Changes made to an
instance may be overridden by later prefab changes according to Unity’s normal
prefab override rules.

The generated `Paddle` prefab currently serializes one `PassThrough` stage.
Unused settings such as `timeConstant = 0.05` remain serialized on the stage
but have no effect unless its type is changed to `Exponential`. The pipeline’s
`staleTimeout` is separate from the filter and controls when missing data
causes zero output and a filter reset.

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

## Noise experiments

The project has three different ways to study or increase apparent FSR noise:

1. remove or weaken the existing noise-reduction and signal-conditioning
   steps;
2. reduce the Arduino sample rate; or
3. add controlled synthetic noise with `NoiseInjector`.

These approaches do not affect the same representation of the signal. The
Arduino options affect raw ADC readings before they are sent to Unity.
Calibration and deadzone affect the signal inside the Unity input pipeline.
`NoiseInjector` modifies a sample in Unity before the pipeline normalises it.

### Existing algorithms and safeguards that reduce noise

The following are the relevant mechanisms in the current repository. They
should be distinguished from connection, scheduling, and validation code that
does not intentionally smooth the signal.

#### Arduino ADC acquisition

**Discard-first-read** is enabled by default:

```cpp
#define DISCARD_FIRST_READ 1
```

When the ADC multiplexer changes from one analog pin to the next, the
sample-and-hold capacitor can retain charge from the previous channel. The
first conversion after switching can therefore be pulled toward the preceding
channel. `readChannel()` performs and ignores one conversion, then keeps the
next conversion. This reduces channel-to-channel crosstalk artifacts, rather
than random sensor noise.

To deliberately expose more crosstalk, change it to:

```cpp
#define DISCARD_FIRST_READ 0
```

Then recompile and upload the sketch. This is a firmware change, not a Unity
Inspector setting.

**Oversampling/averaging** is available through:

```cpp
#define OVERSAMPLE 1
```

For values greater than one, the sketch takes multiple conversions of the
same channel and returns their arithmetic mean. Averaging reduces
approximately uncorrelated white ADC noise by `sqrt(N)` in standard deviation,
at the cost of more ADC work and a small measurement delay. With the current
value of `1`, oversampling is disabled and it contributes no noise reduction.
Changing it to `4`, for example, enables four-conversion averaging; upload the
modified sketch afterward.

The Arduino does not currently implement a digital low-pass, median filter,
or moving average beyond this optional ADC average. It intentionally transmits
raw values so the Unity and analysis stages can be evaluated.

#### Unity calibration and normalisation

Calibration measures each physical channel’s released baseline using a median
and its firm-grip maximum using a 95th percentile. This makes the input range
more repeatable and reduces the influence of outliers while measuring the
calibration points. It is not a continuous live-signal filter: once
calibration is complete, each incoming sample is mapped using the saved
baseline and maximum.

Values at or below baseline become zero. Values above the calibrated maximum
are mapped toward one using `normalizationForceMax`. This removes baseline
offset from the usable signal, but it does not remove rapid noise around the
mapped value.

#### Unity live filters

The Unity pipeline now provides exponential, moving-average, median, and
Butterworth stages. The generated configuration uses `PassThrough`, so these
reductions are inactive unless the Inspector chain is changed. Exponential,
moving-average, and Butterworth are low-pass filters; median is a nonlinear
outlier-rejection filter.

#### Deadzone and rescaling

The `PaddleConfig.deadzone` removes small post-filter values and rescales the
remaining range. It suppresses idle creep in the paddle, but does not reduce
noise in the raw serial stream or in the filter state. Removing it does not
make the sensor electrically noisier; it makes more of the existing small
noise visible as paddle movement.

#### Clamping

The pipeline clamps filtered values to `0..1` before applying the deadzone.
`NoiseInjector` separately clamps modified raw samples to `0..1023`. These
limits prevent out-of-range values from propagating, but they are not noise
filters and can clip large disturbances.

#### Stale-data watchdog and parser validation

The serial parser rejects malformed lines and out-of-range ADC fields. The
pipeline’s stale-data watchdog outputs zero and resets filter state when no
fresh sample arrives. These mechanisms protect gameplay from invalid or old
data; they do not reduce valid sample-to-sample noise.

#### Timing and scheduling

The Arduino uses a fixed-rate `micros()` schedule and avoids sending catch-up
bursts after an overrun. This improves timing regularity, but is not a
signal-smoothing algorithm. The serial protocol, background reader, and
latest-frame buffer likewise transport the newest valid sample; they do not
average samples.

### Approach 1: remove noise reduction

This approach can be useful when the goal is to identify where the noise
enters, but it combines effects with very different meanings:

- Set `DISCARD_FIRST_READ` to `0` to expose ADC multiplexer crosstalk.
- Keep `OVERSAMPLE` at `1`; it is already disabled. If a future sketch uses a
  value greater than one, returning it to `1` removes that averaging.
- Set the `PaddleConfig` **Deadzone** to `0` to expose low-level idle movement.
- Keep the filter chain as `PassThrough` to avoid live low-pass smoothing. If
  the chain currently contains `Exponential`, replace it with
  `PassThrough` or reduce its time constant.
- Do not remove calibration, parser validation, or the stale-data watchdog
  merely to create noise. They establish the input range and protect against
  invalid/disconnected data rather than hiding valid noise.

This approach can have a large visible gameplay effect, especially when
discard-first-read is disabled and the adjacent FSR channels have different
pressures. However, it is not a controlled noise model: it changes the
measurement process, may produce channel-dependent artifacts, and can make
comparisons difficult. Removing the deadzone mainly increases visible
jitter; it does not increase the raw ADC noise amplitude.

### Approach 2: reduce the Arduino sample rate

The current firmware uses:

```cpp
#define SAMPLE_RATE_HZ 200
```

The period is calculated as `1,000,000 / SAMPLE_RATE_HZ`, so changing this to
`100` makes the Arduino transmit one frame every 10 ms instead of every 5 ms.
Upload the sketch after changing it. The serial baud rate remains `115200`.

A lower sample rate does **not** inherently add random noise to each ADC
reading. It reduces temporal resolution and can make the signal appear
stepped or stale between updates. It also makes short spikes easier to miss
and can increase control latency. The Unity pipeline repeatedly consumes the
latest available sample, so with `PassThrough` the paddle may hold each
reading for longer.

The lower rate can expose timing and responsiveness problems, but it is a
weak and indirect way to create amplitude noise. At 115200 baud, 200 Hz is
already within the documented line-time budget. If the rate is reduced,
watch the Unity connection panel’s packets-per-second value and parse-error
count. Do not confuse serial gaps or missed samples with a controlled sensor
noise experiment.

### Approach 3: `NoiseInjector`

`NoiseInjector` is a Unity component that wraps any `IPlayerInputSource`. For
each `TryGetLatest()` call it:

1. obtains the underlying `RawFsrSample`;
2. independently adds Gaussian noise to Up and Down when enabled;
3. independently attempts a random positive or negative spike on each channel
   when enabled;
4. clamps the resulting raw values to `0..1023`; and
5. returns the modified sample with its original timestamp.

Gaussian noise is generated with the Box-Muller transform. Its
`gaussianStdDev` is in raw ADC counts, so `10` means a typical random
disturbance with standard deviation of approximately 10 counts before
normalisation. Spikes occur independently per channel per request with
`spikeProbability`; when one occurs, its magnitude is
`spikeMagnitude` ADC counts and its sign is random. The default values are
Gaussian enabled with standard deviation `10`, spikes enabled with
probability `0.005` and magnitude `200`.

Because new random values are generated on each request, the effective noise
rate depends on how frequently the source is queried. This is a software test
instrument, not a physical model of every FSR, ADC, wiring, or EMI failure.

#### Activating the noise injector in Unity

The easiest way to use this feature is to start with the existing services
prefab. The relevant files and objects are:

| Item | Location |
|---|---|
| NoiseInjector script | `Assets/Scripts/Input/NoiseInjector.cs` |
| Input source type enum | `Assets/Scripts/Core/InputSourceType.cs` |
| Player source selection | `Assets/Scripts/Input/PlayerSlot.cs` |
| Shared source registry | `Assets/Scripts/Input/InputSourceRegistry.cs` |
| Services prefab | `Assets/Prefabs/Core/SSRP_Services.prefab` |
| NoiseInjector object | `SSRP_Services` prefab > `Input` child |
| NoiseInjector component | On the `Input` GameObject |
| Noise settings | NoiseInjector component Inspector |
| Player selection field | Player Slot component > **Input Selection** |

The prefab is normally included in the game through the persistent
`SSRP_Services` object. If it is not present, run the project-generation/build
step used by the repository, or use the code fallback created by
`Assets/Scripts/Flow/GameServices.cs`; that fallback also creates an `Input`
object and a `NoiseInjector`.

##### Beginner setup: keyboard plus artificial noise

This is the safest first test because it does not require an Arduino.

1. Open a gameplay scene and press Play, or open the
   `SSRP_Services.prefab` asset directly from
   `Assets/Prefabs/Core/SSRP_Services.prefab`.
2. In the **Hierarchy**, find `SSRP_Services`, expand it, then select its
   `Input` child.
3. In the Inspector, find the **Noise Injector** component. Its
   **Source Component** should point to the `KeyboardInputSource` component on
   the same `Input` object. This is the default prefab wiring.
4. For the first test, use these fields:
   - **Enable Gaussian Noise**: checked
   - **Gaussian Std Dev**: `10`
   - **Enable Spikes**: unchecked
   - **Spike Probability**: leave at `0.005`
   - **Spike Magnitude**: leave at `200`
5. Select the player’s GameObject with the **Player Slot** component. For
   spawned paddles this is normally the `Paddle` prefab at
   `Assets/Prefabs/Gameplay/Paddle.prefab`; for a scene-authored player it is
   the GameObject containing that player’s `PlayerSlot`.
6. In **Player Slot > Input Selection > Input Source Type**, select
   **NoiseInjected**. The enum is defined in
   `Assets/Scripts/Core/InputSourceType.cs`; the Inspector display is
   **Keyboard + noise**.
7. Leave **Noise Injector Source** empty unless you need a special
   per-player injector. An empty field makes `PlayerSlot` use the shared
   injector registered by `InputSourceRegistry` on the `Input` object.
8. Press Play and move the keyboard-controlled paddle. The movement should
   contain small random fluctuations. Set **Enable Gaussian Noise** off and
   **Enable Spikes** on to test rare large disturbances separately.

##### Using real Arduino readings plus noise

To add artificial noise on top of the Arduino:

1. Upload the firmware from
   `arduino/ssrp_fsr/ssrp_fsr.ino` to the Arduino and connect it.
2. In the `SSRP_Services > Input` object, use the
   **SerialInputSource** component to connect to the correct COM port.
   The serial settings asset is
   `Assets/ScriptableObjects/Input/SerialConfig.asset`; its default baud rate
   is `115200`.
3. Select the **Noise Injector** component on that same `Input` object.
4. Drag the `SerialInputSource` component from the `Input` object into the
   NoiseInjector’s **Source Component** field. Do not drag the Arduino file
   itself; this field expects a Unity component implementing
   `IPlayerInputSource`.
5. Set the player’s **Player Slot > Input Selection > Input Source Type** to
   **NoiseInjected**. If **Noise Injector Source** is empty, the slot uses the
   shared injector from `InputSourceRegistry`.
6. Connect the serial port and verify that valid readings arrive before
   judging the noise. Then tune **Gaussian Std Dev**, **Spike Probability**,
   and **Spike Magnitude**.

For a first hardware test, use a small Gaussian value such as `5` or `10`,
disable spikes, and compare the result with the same Arduino source selected
directly as **Serial (Arduino USB)**. This makes it clear what the injector
added.

##### What each Inspector field means

All of these fields are on the **Noise Injector** component attached to
`SSRP_Services > Input`:

| Inspector field | What it does | Suggested beginner test |
|---|---|---|
| **Source Component** | The input component to copy before adding noise. It must implement `IPlayerInputSource`. | Leave the default `KeyboardInputSource`. |
| **Enable Gaussian Noise** | Turns continuous random fluctuations on or off. | On. |
| **Gaussian Std Dev** | Typical noise size in raw ADC counts. Larger values create larger fluctuations. | `10`. |
| **Enable Spikes** | Turns rare sudden disturbances on or off. | Off initially. |
| **Spike Probability** | Chance of a spike on each channel each time a sample is requested. | `0.005` is occasional. |
| **Spike Magnitude** | Size of a spike in raw ADC counts; its sign is random. | `200` for obvious outliers. |
| **Show Debug Gui** | Displays noisy Player 1 raw Up/Down values during Play mode. | On while diagnosing. |

The player-selection fields are on the **Player Slot** component:

| Inspector field | Location | Meaning |
|---|---|---|
| **Input Source Type** | Player Slot > Input Selection | Select **NoiseInjected** / **Keyboard + noise**. |
| **Noise Injector Source** | Player Slot > Available Input Sources | Optional direct reference to a specific NoiseInjector. Leave empty to use the shared registry. |

##### How to tell whether it is active

If the player still uses **Keyboard** or **Serial (Arduino USB)** under
**Input Source Type**, the injector is not being used. The source must say
**Keyboard + noise** (the display name for `NoiseInjected`). If the injector
has no valid **Source Component**, Unity logs a validation warning and the
player receives no noisy samples.

With the debug HUD enabled, compare:

- `raw up/down`: the source values after `NoiseInjector`;
- `norm`: the values after normalisation;
- `filtered`: the final values after filtering, clamping, and deadzone.

If using the optional **Show Debug Gui**, the on-screen `Noisy P1 Up/Down`
values provide an additional direct check of the injector output.

Important: `NoiseInjected` is classified as a non-hardware input source by
`InputSourceType.IsHardware()`. Therefore a NoiseInjector wrapping the serial
source causes `PlayerInputPipeline` to use the default `0..1023` range rather
than the saved Serial/UDP calibration. This is appropriate for raw,
controlled experiments, but it is not identical to “calibrated hardware plus
noise.” If that distinction matters, the input-source classification or
pipeline would need a targeted code change so the wrapper preserves the
underlying hardware calibration semantics.

For repeatable tests, start with one model at a time:

| Test | Gaussian | Spikes | What it isolates |
|---|---:|---:|---|
| Baseline | Off | Off | Underlying source and pipeline |
| Continuous noise | On | Off | Small random fluctuations and filter smoothing |
| Outliers | Off | On | Response to rare large disturbances |
| Combined | On | On | More realistic mixed synthetic disturbance |

### Which approach has the greatest impact?

For a controlled experiment whose purpose is to study how filters respond,
**`NoiseInjector` has the highest overall impact and is the recommended first
choice**. It can add a known, adjustable continuous noise level and large
outliers without changing the Arduino firmware, wiring, sampling schedule, or
baseline recording. Gaussian standard deviation, spike probability, and spike
magnitude can be varied independently, and the same experiment can be
repeated with `PassThrough` and `Exponential` chains.

For actual physical-signal impact, the ranking is different:

1. **Removing discard-first-read and any active averaging** can create the
   largest raw acquisition artifacts, especially channel crosstalk. In the
   current firmware only discard-first-read is active; oversampling is already
   `1`.
2. **Removing the deadzone** can create a large visible gameplay effect from
   small idle noise, but does not change the raw FSR signal.
3. **Reducing sample rate** mainly reduces temporal resolution and increases
   apparent latency; it is least effective as a way to increase amplitude
   noise.

The strongest practical test matrix is therefore:

1. record a clean hardware baseline at 200 Hz with discard-first-read enabled,
   `OVERSAMPLE = 1`, the normal calibration, and the normal deadzone;
2. use `NoiseInjector` to test controlled Gaussian noise and spikes;
3. compare `PassThrough` against `Exponential` at the same injected settings;
4. separately disable discard-first-read to measure real ADC crosstalk; and
5. only then reduce the sample rate to measure latency, missed transients, and
   temporal degradation.

This keeps “more noise,” “more visible noise,” and “fewer samples” as separate
experimental variables instead of treating them as interchangeable.
