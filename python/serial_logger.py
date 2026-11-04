"""Record raw SSRP-FSR v1 lines from the Arduino into a CSV and report timing statistics.

Usage:
    pip install pyserial
    python python/serial_logger.py --port COM3 --seconds 300 --out data/samples/a1_timing.csv

Close Unity (or press Disconnect in its connection panel) first: only one program can
open a serial port at a time.
"""
import argparse
import csv
import statistics
import sys
import time

try:
    import serial  # pyserial
except ImportError:  # pragma: no cover
    sys.exit("pyserial is missing: pip install pyserial")


def parse(line):
    """Return (t_us, [4 ints]) or None for comments/garbage (same rules as SerialLineParser)."""
    line = line.strip()
    if not line or line.startswith("#"):
        return None
    parts = line.split(",")
    if len(parts) != 5:
        raise ValueError(line)
    t_us = int(parts[0])
    values = [int(p) for p in parts[1:]]
    if any(v < 0 or v > 1023 for v in values):
        raise ValueError(line)
    return t_us, values


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--port", default="COM3")
    ap.add_argument("--baud", type=int, default=115200)
    ap.add_argument("--seconds", type=float, default=60)
    ap.add_argument("--out", default="data/samples/raw_capture.csv")
    args = ap.parse_args()

    port = serial.Serial(args.port, args.baud, timeout=0.1)
    time.sleep(2.0)            # the Uno resets when the port opens
    port.reset_input_buffer()  # drop a possibly partial first line

    rows, bad, comments = [], 0, []
    end = time.time() + args.seconds
    while time.time() < end:
        raw = port.readline().decode("ascii", errors="replace")
        if not raw:
            continue
        try:
            parsed = parse(raw)
        except ValueError:
            bad += 1
            continue
        if parsed is None:
            if raw.strip():
                comments.append(raw.strip())
            continue
        t_us, v = parsed
        rows.append([time.time(), t_us] + v)
    port.close()

    with open(args.out, "w", newline="") as f:
        w = csv.writer(f)
        w.writerow(["pc_time_s", "t_us", "p1_up", "p1_down", "p2_up", "p2_down"])
        w.writerows(rows)

    intervals = [(b[1] - a[1]) & 0xFFFFFFFF for a, b in zip(rows, rows[1:])]
    print(f"samples: {len(rows)}  malformed lines: {bad}  comments: {len(comments)}")
    if intervals:
        print(f"mean interval: {statistics.mean(intervals):.1f} us   "
              f"jitter (std-dev): {statistics.pstdev(intervals):.1f} us   "
              f"min/max: {min(intervals)}/{max(intervals)} us")
    for c in comments[:5]:
        print("  ", c)
    print("saved", args.out)


if __name__ == "__main__":
    main()
