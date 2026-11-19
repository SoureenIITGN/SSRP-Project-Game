# Serial protocol: SSRP-FSR v1

The contract between the Arduino firmware (`arduino/ssrp_fsr/ssrp_fsr.ino`), Unity
(`SerialInputSource`, `UdpInputSource`), the TinkerCAD simulation and later Python analysis.
If you change anything here, change all of them in the same PR.

## Link settings

| Setting | Value |
|---|---|
| Baud rate | 115200 |
| Framing | 8 data bits, no parity, 1 stop bit (8N1) |
| Encoding | ASCII |
| Line ending | `\n` (a preceding `\r` is tolerated) |
| Sample rate | 200 Hz by default (`SAMPLE_RATE_HZ`) |
| Anti-alias filter | Disabled by default (`ANTI_ALIAS_ENABLED`); when enabled, a cascaded one-pole filter uses `ANTI_ALIAS_ORDER` stages and `ANTI_ALIAS_CUTOFF_HZ` |

## Data lines

One line per sample, five comma-separated unsigned integers, no spaces:

```
t_us,p1_up,p1_down,p2_up,p2_down
```

| Field | Meaning | Range |
|---|---|---|
| `t_us` | `micros()` when the sample was taken. Wraps after ~71.6 min, so compute intervals with unsigned subtraction. | 0 to 4294967295 |
| `p1_up` | Player 1 UP sensor, pin A0 | 0 to 1023 (raw ADC counts) |
| `p1_down` | Player 1 DOWN sensor, pin A1 | 0 to 1023 |
| `p2_up` | Player 2 UP sensor, pin A2 | 0 to 1023 |
| `p2_down` | Player 2 DOWN sensor, pin A3 | 0 to 1023 |

Example: `1234567,512,3,0,1020`

Values are raw ADC counts unless the optional firmware anti-alias filter is
enabled. With the filter enabled, values are still in the `0..1023` ADC-count
range, but are low-pass filtered before transmission. A higher number means
more force.

## Comment lines

Lines starting with `#` are comments. Parsers must ignore them, apart from optionally showing them.

- Boot banner (always the first line): `# SSRP-FSR v1 rate=200Hz discard=1 oversample=1 aa=0 order=2 cutoff=20Hz`
- Column header: `# columns: t_us,p1_up,p1_down,p2_up,p2_down`
- Loop overruns, at most once per second: `# overruns=3`

Opening the port resets an Uno (DTR), so the banner shows up again on every connect.

## Channel mapping in Unity

| Channel | Pin | Unity player | `RawFsrSample` field |
|---|---|---|---|
| 0 | A0 | Player 1 (playerId 1) | `Up` |
| 1 | A1 | Player 1 | `Down` |
| 2 | A2 | Player 2 (playerId 2) | `Up` |
| 3 | A3 | Player 2 | `Down` |

Player ids are 1-based everywhere in the game (`PlayerDefinition.playerId`, `GameEvents`).

## Parser rules (implemented in `SerialLineParser`)

1. Trim whitespace and `\r`. An empty line is ignored.
2. A line starting with `#` is a comment.
3. Otherwise, the line must split into exactly 5 fields. Each field must parse as an integer,
   with channel values in 0 to 1023. Anything else counts as a **parse error** and is dropped.
   This covers partial lines at connect time, garbage, and truncated lines.
4. The receive time on the PC (`InputClock`) is used as the sample time. `t_us` is kept for
   jitter analysis.

## Timing budget

- ADC time: 4 channels x 2 conversions (`DISCARD_FIRST_READ`) x ~112 us = ~0.9 ms per sample.
- Line length: up to 10 + 4x4 + 4 commas + newline = 31 bytes. At 115200 baud
  (11520 bytes/s) that takes ~2.7 ms, which fits in the 5 ms period at 200 Hz.
- If the PC shows gaps or many parse errors, set `SAMPLE_RATE_HZ 100`.
- If `ANTI_ALIAS_ENABLED` is `1`, keep `ANTI_ALIAS_CUTOFF_HZ` below
  `SAMPLE_RATE_HZ / 2`. The sketch cascades identical one-pole stages, so
  `ANTI_ALIAS_ORDER` controls the filter order; higher order gives steeper
  roll-off and more delay.
- This is a digital low-pass stage for filtering before later software
  downsampling. It cannot undo aliasing that already occurred at the Arduino
  ADC; use an analog RC low-pass before the ADC when that protection is needed.

## Measured results (fill in during [A1]/[A5] on the real board)

| Quantity | Expected | Measured |
|---|---|---|
| Mean interval between `t_us` values | 5000 us | _TBD_ |
| Interval std-dev (jitter) | < 50 us | _TBD_ |
| Truncated or malformed lines in a 5-minute run | 0 | _TBD_ |
| Packets/s shown in the Unity connection panel | ~200 | _TBD_ |

How to measure: record 5 minutes with `python/serial_logger.py --seconds 300 --out data/samples/a1_timing.csv`.
The script prints the mean interval, jitter and malformed-line count when it finishes.
