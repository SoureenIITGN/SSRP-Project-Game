"""Generate the game's sound effects procedurally (no third-party audio, so no licensing issues).

    python tools/generate_sfx.py            -> writes Assets/Audio/SFX/*.wav

Only the Python standard library is used. Re-run after tweaking a recipe below.
"""
import math
import os
import random
import struct
import wave

RATE = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Audio", "SFX")


def envelope(i, n, attack=0.005, release=None):
    t = i / RATE
    a = min(1.0, t / attack) if attack > 0 else 1.0
    if release is None:
        r = 1.0 - i / n
    else:
        r = min(1.0, (n - i) / (release * RATE))
    return a * max(0.0, r)


def tone(freq_start, freq_end, seconds, wave_shape="sine", volume=0.6, decay=1.0):
    n = int(RATE * seconds)
    phase = 0.0
    out = []
    for i in range(n):
        f = freq_start + (freq_end - freq_start) * (i / n)
        phase += 2 * math.pi * f / RATE
        if wave_shape == "square":
            s = 1.0 if math.sin(phase) >= 0 else -1.0
            s *= 0.5
        elif wave_shape == "triangle":
            s = 2 / math.pi * math.asin(math.sin(phase))
        else:
            s = math.sin(phase)
        out.append(s * volume * envelope(i, n) ** decay)
    return out


def noise(seconds, volume=0.5, lowpass=0.2, seed=1):
    rnd = random.Random(seed)
    n = int(RATE * seconds)
    y, out = 0.0, []
    for i in range(n):
        y += lowpass * (rnd.uniform(-1, 1) - y)
        out.append(y * volume * envelope(i, n) ** 2)
    return out


def mix(*tracks):
    n = max(len(t) for t in tracks)
    return [sum(t[i] for t in tracks if i < len(t)) for i in range(n)]


def concat(*tracks):
    out = []
    for t in tracks:
        out.extend(t)
    return out


def metallic_ring(f0, seconds, partials, volume=0.6, detune=2.5):
    """Inharmonic struck-metal ring: each partial (ratio, amplitude, decay/s) decays on its own.
    A slightly detuned copy of every partial adds the shimmering 'beating' of a real blade."""
    n = int(RATE * seconds)
    out = [0.0] * n
    for ratio, amp, decay in partials:
        for f in (f0 * ratio, f0 * ratio + detune):
            w = 2 * math.pi * f / RATE
            for i in range(n):
                out[i] += 0.5 * amp * math.sin(w * i) * math.exp(-decay * i / RATE)
    attack = int(RATE * 0.002)
    return [x * volume * min(1.0, i / max(1, attack)) for i, x in enumerate(out)]


def thump(f_start, f_end, seconds, volume=0.9):
    """Bass impact with an exponential pitch drop."""
    n = int(RATE * seconds)
    phase, out = 0.0, []
    for i in range(n):
        t = i / RATE
        f = f_end + (f_start - f_end) * math.exp(-t * 25)
        phase += 2 * math.pi * f / RATE
        out.append(math.sin(phase) * volume * math.exp(-t * 9))
    return out


def reverb(samples, taps=((0.031, 0.45), (0.047, 0.35), (0.071, 0.27), (0.109, 0.2), (0.163, 0.13)), tail=0.6):
    """Cheap room: a few early reflections, then a feedback echo for the tail."""
    extra = int(RATE * (taps[-1][0] + tail))
    out = list(samples) + [0.0] * extra
    for delay, gain in taps:
        d = int(RATE * delay)
        for i, x in enumerate(samples):
            out[i + d] += x * gain
    d = int(RATE * 0.083)
    for i in range(d, len(out)):
        out[i] += out[i - d] * 0.33
    return out


def write(name, samples):
    os.makedirs(OUT, exist_ok=True)
    peak = max(1e-9, max(abs(s) for s in samples))
    scale = min(1.0, 0.95 / peak)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s * scale)) * 32767)) for s in samples))
    print("wrote", os.path.normpath(path))


def main():
    write("paddle_hit", mix(tone(520, 440, 0.09, "square", 0.5, 1.5), noise(0.03, 0.3, 0.5, 2)))
    write("wall_hit", tone(300, 260, 0.07, "triangle", 0.6, 1.5))
    write("tile_hit", mix(tone(880, 700, 0.06, "square", 0.35, 2.0), noise(0.04, 0.25, 0.6, 3)))
    write("tile_break", mix(noise(0.32, 0.7, 0.35, 4), tone(400, 90, 0.3, "square", 0.3, 1.2)))
    write("goal", concat(tone(523, 523, 0.10, "square", 0.4), tone(659, 659, 0.10, "square", 0.4),
                         tone(784, 784, 0.10, "square", 0.4), tone(1047, 1047, 0.28, "square", 0.4, 0.8)))
    write("parry", mix(tone(1200, 1800, 0.22, "sine", 0.5, 0.7), tone(1800, 2400, 0.22, "sine", 0.25, 0.7)))
    # Successful parry: bass hit + bright crack + ringing steel 'TING' in a small room (an 'ultra-kill' clang).
    ring = metallic_ring(1320, 1.1, [(1.0, 1.0, 3.2), (2.32, 0.7, 4.5), (3.87, 0.5, 6.0), (5.41, 0.35, 8.0), (8.93, 0.2, 11.0)], 0.55)
    shing = tone(2600, 5200, 0.12, "sine", 0.18, 1.5)
    crack = noise(0.03, 0.9, 0.85, 7)
    write("parry_success", reverb(mix(thump(180, 42, 0.35, 1.0), crack, ring, shing)))
    write("countdown_tick", tone(660, 660, 0.12, "sine", 0.6, 1.0))
    write("countdown_go", tone(990, 990, 0.35, "sine", 0.7, 0.8))
    write("match_over", concat(tone(784, 784, 0.14, "triangle", 0.5), tone(659, 659, 0.14, "triangle", 0.5),
                               tone(523, 523, 0.14, "triangle", 0.5), tone(1047, 1047, 0.5, "triangle", 0.5, 0.6)))


if __name__ == "__main__":
    main()
