"""Send SSRP-FSR v1 lines to Unity over UDP (the future Python signal-processing bridge, [A6]).

Modes
-----
  sine    synthetic signals, no hardware:  python python/udp_sender.py --mode sine
  serial  forward the Arduino's raw lines: python python/udp_sender.py --mode serial --serial-port COM3
          (pip install pyserial; close Unity's serial connection first, only one program can own the port)
  filter  like 'serial' but applies a first-order low-pass in Python before sending
          (example of where the signal-processing work will plug in)

In Unity: Match Setup > <player> input > UDP (Python). Default port 5005.
"""
import argparse
import math
import socket
import sys
import time


def make_line(t_us, values):
    v = [max(0, min(1023, int(round(x)))) for x in values]
    return f"{t_us & 0xFFFFFFFF},{v[0]},{v[1]},{v[2]},{v[3]}\n"


def run_sine(sock, addr, rate):
    period = 1.0 / rate
    start = time.perf_counter()
    n = 0
    while True:
        t = time.perf_counter() - start
        p1 = math.sin(2 * math.pi * 0.4 * t)
        p2 = math.sin(2 * math.pi * 0.25 * t + 1.0)
        values = [
            511 + 511 * max(0.0, p1), 511 + 511 * max(0.0, -p1),
            511 + 511 * max(0.0, p2), 511 + 511 * max(0.0, -p2),
        ]
        values = [v - 511 for v in values]  # 0 at rest, up to 511 when pressed
        sock.sendto(make_line(int(t * 1e6), [v * 2 for v in values]).encode("ascii"), addr)
        n += 1
        time.sleep(max(0.0, start + n * period - time.perf_counter()))


def run_serial(sock, addr, port, baud, tau):
    try:
        import serial  # pyserial
    except ImportError:
        sys.exit("pyserial is missing: pip install pyserial")

    ser = serial.Serial(port, baud, timeout=0.1)
    state = None
    last_t = None
    while True:
        raw = ser.readline().decode("ascii", errors="replace").strip()
        if not raw or raw.startswith("#"):
            continue
        parts = raw.split(",")
        if len(parts) != 5:
            continue
        try:
            t_us = int(parts[0])
            values = [float(p) for p in parts[1:]]
        except ValueError:
            continue

        if tau > 0:
            if state is None:
                state, last_t = values, t_us
            dt = ((t_us - last_t) & 0xFFFFFFFF) / 1e6
            last_t = t_us
            alpha = 1 - math.exp(-dt / tau) if dt > 0 else 0
            state = [s + alpha * (x - s) for s, x in zip(state, values)]
            values = state

        sock.sendto(make_line(t_us, values).encode("ascii"), addr)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=5005)
    ap.add_argument("--mode", choices=["sine", "serial", "filter"], default="sine")
    ap.add_argument("--rate", type=float, default=200.0, help="sine mode sample rate (Hz)")
    ap.add_argument("--serial-port", default="COM3")
    ap.add_argument("--baud", type=int, default=115200)
    ap.add_argument("--tau", type=float, default=0.03, help="filter mode time constant (s)")
    args = ap.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    addr = (args.host, args.port)
    print(f"sending to udp://{args.host}:{args.port} in '{args.mode}' mode, Ctrl+C to stop")
    try:
        if args.mode == "sine":
            run_sine(sock, addr, args.rate)
        else:
            run_serial(sock, addr, args.serial_port, args.baud, args.tau if args.mode == "filter" else 0)
    except KeyboardInterrupt:
        pass


if __name__ == "__main__":
    main()
