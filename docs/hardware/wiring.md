# FSR wiring + TinkerCAD simulation ([C5])

Goal: a teammate can rebuild the real circuit from this page alone. TinkerCAD validates the
**wiring and firmware logic** before we touch real hardware.

## Circuit: one voltage divider per sensor

```
 5V ────┬──────────┬──────────┬──────────┐
        │          │          │          │
     [FSR P1U]  [FSR P1D]  [FSR P2U]  [FSR P2D]
        │          │          │          │
 A0 ────┤   A1 ────┤   A2 ────┤   A3 ────┤
        │          │          │          │
     [10 kΩ]    [10 kΩ]    [10 kΩ]    [10 kΩ]
        │          │          │          │
 GND ───┴──────────┴──────────┴──────────┘
```

`Vout = Vcc · R_fixed / (R_fsr + R_fixed)`. The fixed resistor goes to **GND**. Pressing harder
lowers `R_fsr`, so `Vout` and the ADC reading go **up**. At rest an FSR is above 1 MΩ, so the
reading is near 0.

**Choosing R_fixed.** Check the datasheet. For an Interlink FSR-402, a firm finger press gives
roughly 1 to 10 kΩ, so 10 kΩ puts the press in the middle of the ADC range. If the readings
saturate at 1023 too early, use a smaller resistor (4.7 kΩ). If they never get above about 300,
use a larger one (22 kΩ to 47 kΩ).

## Pin map (must match Unity, see docs/serial-protocol.md)

| Arduino pin | Sensor | Unity player | `RawFsrSample` | Protocol column |
|---|---|---|---|---|
| A0 | Player 1 UP | playerId 1 | `Up` | `p1_up` |
| A1 | Player 1 DOWN | playerId 1 | `Down` | `p1_down` |
| A2 | Player 2 UP | playerId 2 | `Up` | `p2_up` |
| A3 | Player 2 DOWN | playerId 2 | `Down` | `p2_down` |
| 5V | divider supply | | | |
| GND | divider ground | | | |

Wire colours we use: red = 5V, black = GND, yellow = A0, orange = A1, green = A2, blue = A3.

## Bill of materials

| Qty | Part |
|---|---|
| 1 | Arduino Uno (or compatible) + USB cable |
| 4 | FSR (for example Interlink FSR-402, 0.5" round) |
| 4 | 10 kΩ resistor, 1/4 W |
| 1 | Breadboard + jumper wires |
| 4 | Firm pads (MDF/acrylic squares) + foam finger targets, tape for strain relief |

## TinkerCAD simulation steps

1. Go to tinkercad.com, then **Circuits > Create new Circuit**.
2. Place an **Arduino Uno R3** and a **breadboard**.
3. **Sensor part:** TinkerCAD has a *Force Sensor* component. If your account does not show it,
   use a **potentiometer** per sensor. Wire its outer pins to 5V and GND and its wiper to the
   analog pin: the wiper replaces the FSR + resistor divider, which is electrically also a
   variable divider. A *flex sensor* in series with 10 kΩ also works.
4. Wire the four dividers to A0 to A3 exactly as in the pin map above.
5. **Code > Text**: paste the whole `arduino/ssrp_fsr/ssrp_fsr.ino`.
6. **Start Simulation**, open the **Serial Monitor**, and drag each slider or potentiometer in turn.
7. Capture the evidence (save it in this folder):
   - `tinkercad-circuit.png`: a screenshot of the breadboard
   - `tinkercad-serial.png`: the Serial Monitor showing the banner `# SSRP-FSR v1 rate=200Hz ...`
     and 5-column lines, while one sensor is pressed and only its own column changes
   - the share link (**Share > Invite people / public link**), pasted below

TinkerCAD share link: _paste here_

**What to check:**

- [ ] The first line is the banner, the second is the column header.
- [ ] Every data line has exactly 5 integers: `t_us,p1_up,p1_down,p2_up,p2_down`.
- [ ] Moving sensor A0 changes only the 2nd column, A1 the 3rd, A2 the 4th, A3 the 5th.
- [ ] `t_us` grows by about 5000 per line. TinkerCAD runs slower than real time, so the
      *interval* matters, not the wall clock.

## Limitation

TinkerCAD's simulated serial port cannot be opened by Unity on our PC. The simulation proves the
**wiring and firmware logic only**. The end-to-end test with the real board is [A5]
(`docs/hardware/bringup.md`).
