#!/usr/bin/env python3
"""Synthesises AVAPacMan's sound effects and tunes into ../src/AVAPacMan/Sounds as 16-bit mono WAV.

Nothing is sampled from any arcade machine: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVAPacMan", "Sounds")
RNG = random.Random(1980)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def loop(seconds, freq, kind="tri", gain=1.0):
    """A seamless loop: the frequency follows freq(t) and the phase is nudged to finish on a whole cycle."""
    n = int(RATE * seconds)
    phases, ph = [], 0.0
    for i in range(n):
        ph += freq(i / RATE) / RATE
        phases.append(ph)
    k = round(ph) / ph
    return [voice(kind, p * k) * gain for p in phases]


def main():
    os.makedirs(OUT, exist_ok=True)
    # The chomp: two halves that alternate dot by dot.
    write("WakaA.wav", tone(0.085, 560, 260, "tri", attack=0.003, release=0.5), 0.45)
    write("WakaB.wav", tone(0.085, 270, 540, "tri", attack=0.003, release=0.5), 0.45)
    write("EatFruit.wav", cat(tone(0.06, 400, 900, "square", release=0.4), tone(0.12, 900, 1500, "tri", release=0.6)), 0.5)
    write("EatGhost.wav", cat(tone(0.25, 180, 2400, "tri", attack=0.002, release=0.3), tone(0.12, 2400, 2600, "tri", release=1.2)), 0.5)
    # Caught: a falling, wobbling whine, then two pops.
    write("Death.wav", cat(
        tone(1.1, 900, 160, "tri", attack=0.01, release=0.15, vibrato=0.18, vib_rate=9),
        silence(0.05), tone(0.08, 300, 900, "square", release=0.6), silence(0.05), tone(0.08, 300, 900, "square", release=0.6)), 0.55)
    write("ExtraLife.wav", cat(*[tone(0.07, hz(n), kind="square", release=0.4) for n in ["C6", "E6", "G6", "C7"] * 3]), 0.45)

    # The siren: rising and falling, higher as the dots run out. Loops.
    for i, (lo, hi, period) in enumerate([(360, 720, 0.42), (420, 860, 0.38), (480, 1000, 0.34), (560, 1160, 0.30)], start=1):
        write(f"Siren{i}.wav", loop(period * 4, lambda t, lo=lo, hi=hi, p=period: lo * (hi / lo) ** (1 - abs(2 * ((t / p) % 1.0) - 1)), "tri"), 0.32)
    # Frightened: a low, quick warble. The eyes: a fast rising whoosh.
    write("Fright.wav", loop(1.2, lambda t: 180 + 120 * (1 - abs(2 * ((t / 0.15) % 1.0) - 1)), "square", 0.6), 0.25)
    write("Eyes.wav", loop(1.0, lambda t: 500 * (3.2 ** ((t / 0.125) % 1.0)), "tri"), 0.28)

    # Getting ready: a bright, bouncing fanfare in C (new), about four seconds.
    write("StartTune.wav", tune(150,
        [("C5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 0.5), ("B5", 0.5), ("G5", 0.5), ("A5", 1),
         ("F5", 0.5), ("A5", 0.5), ("C6", 0.5), ("F6", 0.5), ("E6", 0.5), ("C6", 0.5), ("D6", 1),
         ("G5", 0.5), ("B5", 0.5), ("D6", 0.5), ("F6", 0.5), ("E6", 1), ("C6", 1)],
        [("C3", 1), ("G3", 1), ("C3", 1), ("G3", 1), ("F3", 1), ("C4", 1), ("F3", 1), ("A3", 1), ("G3", 1), ("D3", 1), ("C3", 2)]), 0.6)
    # Between levels: a jaunty little march (new).
    write("Intermission.wav", tune(132,
        [("E5", 0.5), ("G5", 0.5), ("E5", 0.5), ("C5", 0.5), ("D5", 1), ("G4", 1),
         ("E5", 0.5), ("G5", 0.5), ("A5", 0.5), ("G5", 0.5), ("E5", 2),
         ("F5", 0.5), ("A5", 0.5), ("F5", 0.5), ("D5", 0.5), ("E5", 1), ("C5", 1),
         ("D5", 0.5), ("E5", 0.5), ("F5", 0.5), ("D5", 0.5), ("C5", 2),
         ("G5", 0.5), ("E5", 0.5), ("G5", 0.5), ("C6", 0.5), ("B5", 1), ("G5", 1), ("C6", 2), ("-", 2)],
        [(n, 1) for n in ["C3", "G2", "C3", "G2", "C3", "G2", "A2", "E2", "F2", "C3", "F2", "C3", "G2", "D3", "C3", "G2",
                          "E3", "C3", "G2", "B2", "C3", "-", "-", "-"]]), 0.6)

    # The title tune: a playful, skipping theme in F (new), 16 bars, loops.
    a = [("F5", 0.5), ("A5", 0.5), ("C6", 0.5), ("A5", 0.5), ("Bb5", 1), ("G5", 1)]
    b = [("E5", 0.5), ("G5", 0.5), ("Bb5", 0.5), ("G5", 0.5), ("A5", 1), ("F5", 1)]
    c = [("D6", 0.5), ("C6", 0.5), ("Bb5", 0.5), ("A5", 0.5), ("G5", 1), ("C6", 1)]
    d = [("A5", 0.5), ("Bb5", 0.5), ("C6", 0.5), ("E5", 0.5), ("F5", 2)]
    lead = a + b + a + d + c + b + c + [("G5", 0.5), ("A5", 0.5), ("Bb5", 0.5), ("B5", 0.5), ("C6", 2)] \
        + a + b + a + d + c + [("E5", 1), ("G5", 1), ("C6", 1), ("Bb5", 1)] + [("A5", 1), ("G5", 1), ("E5", 1), ("C5", 1)] + d
    bass = [(n, 0.5) for bar in ["F2", "C3", "F2", "C3", "Bb2", "C3", "Bb2", "C3", "F2", "C3", "F2", "C3", "Bb2", "C3", "C3", "F2"]
            for n in [bar, bar[0:-1] + str(int(bar[-1]) + 1)] * 4]
    write("TitleTune.wav", tune(138, lead, bass), 0.6)
    write("GameOver.wav", tune(92,
        [("C5", 1), ("A4", 1), ("F4", 1), ("E4", 1), ("F4", 2), ("-", 1)],
        [("F2", 2), ("D2", 2), ("C2", 2), ("F2", 1)], kind="tri"), 0.65)
    print("sounds written to", os.path.normpath(OUT))


if __name__ == "__main__":
    main()
