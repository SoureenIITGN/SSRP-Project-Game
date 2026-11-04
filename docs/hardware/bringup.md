# Real-hardware bring-up, mounting & integration test ([A5])

This is a checklist plus a results template. The measured values and photos have to come from
the real kit, so fill in every _TBD_ during the session and commit this page with the photos and
the CSV.

## 1. Wire and verify (Arduino IDE only)

- [ ] Build the circuit from `docs/hardware/wiring.md` and flash `arduino/ssrp_fsr/ssrp_fsr.ino`.
- [ ] Open **Tools > Serial Plotter** at 115200 baud. Four traces near 0 at rest.
- [ ] Press each FSR alone. Its own trace rises; note how much the others move (crosstalk).

| Channel | Pin | Rest value (counts) | Firm press (counts) | Crosstalk on other channels when pressed |
|---|---|---|---|---|
| P1 Up | A0 | _TBD_ | _TBD_ | _TBD_ |
| P1 Down | A1 | _TBD_ | _TBD_ | _TBD_ |
| P2 Up | A2 | _TBD_ | _TBD_ | _TBD_ |
| P2 Down | A3 | _TBD_ | _TBD_ | _TBD_ |

Then run the test once with `DISCARD_FIRST_READ 0` and once with `1`, and note the difference
(this is evidence for the noise analysis): _TBD_

## 2. Mounting

Many "software" problems are mechanical: a bent tail or an off-axis load gives false readings.

- [ ] Each FSR is flat on a **firm pad** (MDF or acrylic), with the active area facing up.
- [ ] A small foam or rubber **finger target** sits centred on the active area, so the press stays on-axis.
- [ ] **Strain relief:** tape the tail and wires to the pad 1 to 2 cm from the sensor. Never
      bend the tail near the active area.
- [ ] Labels: **P1 UP, P1 DOWN, P2 UP, P2 DOWN** (same colours as the wiring table).
- [ ] Photos in `docs/hardware/` (`mount-p1.jpg`, `mount-p2.jpg`, `breadboard.jpg`).

## 3. Raw capture

```
pip install pyserial
python python/serial_logger.py --port COM3 --seconds 60 --out data/samples/raw_1min.csv
```

Do the following during the minute: 10 s at rest, then press each sensor in turn, then both
sensors of each player. Keep the file small (about 12,000 rows), it is used later for the
filter analysis.

- [ ] `data/samples/raw_1min.csv` committed
- [ ] Logger summary pasted here: _TBD_ (mean interval, jitter, malformed lines)

## 4. Unity integration

1. Close the Serial Plotter, since only one program can open the port.
2. Play the **Boot** scene, then go to **Match Setup**.
3. In the Arduino connection panel, pick the port and press **Connect**. The LED should turn green, at about 200 Hz.
4. Press **Calibrate sensors... > Full calibration** and follow the prompts.
5. Set both players' input to **Arduino (USB)**, press **Back**, then **Play**.
6. Press **F1** during the match: all four bars reach about 1.0 on a firm press and about 0 at rest.

Checklist:

- [ ] Channel mapping verified: P1 UP moves the left paddle up, and so on. Matches `wiring.md`.
- [ ] Unplug the USB mid-match: the paddles freeze within 0.3 s and the pause screen says
      "Sensor disconnected". Plug it back in: it reconnects within about 2 s, then press Resume.
- [ ] Press Play/Stop 10 times in the editor: there are no "port in use / access denied" errors.
- [ ] **A full two-player match completed on real FSRs.** Players, date and final score: _TBD_

## 5. Notes for the analysis

Write down anything that looked like noise: mains hum, drift while holding a press, steps from
quantisation, crosstalk. These are the starting points for the filter-design work. _TBD_
