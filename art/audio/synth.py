#!/usr/bin/env python3
"""
Procedural sound effects and music for EmberDeck, from the Python standard library alone.

    python3 -u art/audio/synth.py             # everything
    python3 -u art/audio/synth.py --sfx       # sound effects only (seconds)
    python3 -u art/audio/synth.py --music     # music only (a few minutes)
    python3 -u art/audio/synth.py --only hit  # one sound or track, by name

Why synthesis: this machine has no audio tools beyond ffmpeg, and the one audio model on it
(facebook/musicgen-small, in the Hugging Face cache) ships weights under CC-BY-NC — not usable in
a commercial Steam release. Everything below is generated from this file, so the game owns every
sound outright, and changing a sound is an edit and a re-run rather than a hunt for a new sample.

Output goes to Assets/EmberDeck/Resources/Audio: sfx_<name>.wav, music_<name>.ogg (or .wav when
ffmpeg has no Vorbis encoder). Names must match AudioDirector's Sfx and MusicTrack enums.
"""
import argparse
import array
import math
import pathlib
import random
import shutil
import subprocess
import sys
import tempfile
import time
import wave
import zlib

RATE = 44100
INV = 1.0 / RATE
TAU = 2.0 * math.pi
ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "EmberDeck" / "Resources" / "Audio"

NOTE = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6,
        "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}


def hz(name):
    """'A4' -> 440.0. Octave is the last character, so only octaves 0-9."""
    midi = 12 * (int(name[-1]) + 1) + NOTE[name[:-1]]
    return 440.0 * 2.0 ** ((midi - 69) / 12.0)


def silence(seconds):
    return array.array("f", bytes(4 * int(seconds * RATE)))


def span(out, start, dur):
    s0 = int(start * RATE)
    return s0, max(0, min(int(dur * RATE), len(out) - s0))


# ── Voices ───────────────────────────────────────────────────────────────────────────────
# Each voice adds itself into an existing buffer. Every voice ends with a few milliseconds of
# linear release: an envelope cut off at a non-zero value is a click, and a click on every card
# played is the fastest way to make a game sound cheap.

def sweep(out, start, dur, f0, f1, gain, decay, attack=0.003, release=0.005):
    """Sine with an exponential pitch glide and exponential decay — thumps, zaps, tones."""
    s0, count = span(out, start, dur)
    sin, exp = math.sin, math.exp
    k = math.log(f1 / f0) / dur
    phase = 0.0
    for i in range(count):
        t = i * INV
        phase += TAU * f0 * exp(k * t) * INV
        amp = gain * exp(-decay * t)
        if t < attack:
            amp *= t / attack
        rem = dur - t
        if rem < release:
            amp *= rem / release
        out[s0 + i] += amp * sin(phase)


def noise(out, start, dur, gain, decay, rng, lp0=18000.0, lp1=None, hp=0.0, attack=0.002):
    """White noise through a swept one-pole lowpass and optional highpass.

    A one-pole lowpass at a low cutoff also removes most of the energy, which would make a
    whoosh at 500 Hz inaudible next to a hiss at 8 kHz. The gain is compensated by the filter's
    variance so a cutoff sweep changes colour, not loudness."""
    s0, count = span(out, start, dur)
    exp, sqrt, uni = math.exp, math.sqrt, rng.random
    lp1 = lp1 or lp0
    k = math.log(lp1 / lp0) / dur
    a_hp = 1.0 / (1.0 + TAU * hp * INV) if hp > 0 else 0.0
    lo = hi = prev = 0.0
    for i in range(count):
        t = i * INV
        alpha = 1.0 - exp(-TAU * lp0 * exp(k * t) * INV)
        lo += alpha * (uni() * 2.0 - 1.0 - lo)
        v = lo * min(6.0, 1.0 / sqrt(alpha / (2.0 - alpha)))
        if a_hp:
            hi = a_hp * (hi + v - prev)
            prev = v
            v = hi
        amp = gain * exp(-decay * t)
        if t < attack:
            amp *= t / attack
        rem = dur - t
        if rem < 0.005:
            amp *= rem / 0.005
        out[s0 + i] += amp * v * 0.35


def bell(out, start, dur, freq, gain, decay=3.0, index=2.5, ratio=1.4):
    """Two-operator FM bell. The modulation fades faster than the tone, so the strike is bright
    and the ring settles to something close to a sine."""
    s0, count = span(out, start, dur)
    sin, exp = math.sin, math.exp
    w, wm = TAU * freq, TAU * freq * ratio
    for i in range(count):
        t = i * INV
        amp = gain * exp(-decay * t)
        if t < 0.002:
            amp *= t / 0.002
        rem = dur - t
        if rem < 0.02:
            amp *= rem / 0.02
        out[s0 + i] += amp * sin(w * t + index * exp(-decay * 1.8 * t) * sin(wm * t))


def metal(out, start, dur, base, gain, parts):
    """Inharmonic partials, each with its own decay — the difference between 'metal' and 'tone'."""
    for ratio, amp, decay in parts:
        sweep(out, start, dur, base * ratio, base * ratio, gain * amp, decay, attack=0.0008, release=0.01)


def growl(out, start, dur, f0, f1, gain, cutoff=1200.0, attack=0.01, decay=0.0, vibrato=0.0, release=0.04):
    """Lowpassed sawtooth with a pitch glide: bass notes, grunts, buff and debuff stabs."""
    s0, count = span(out, start, dur)
    sin, exp = math.sin, math.exp
    k = math.log(f1 / f0) / dur
    alpha = 1.0 - exp(-TAU * cutoff * INV)
    phase = lo = 0.0
    for i in range(count):
        t = i * INV
        f = f0 * exp(k * t)
        if vibrato:
            f *= 1.0 + vibrato * sin(TAU * 5.5 * t)
        phase += f * INV
        if phase >= 1.0:
            phase -= 1.0
        lo += alpha * (2.0 * phase - 1.0 - lo)
        amp = gain * exp(-decay * t) if decay else gain
        if t < attack:
            amp *= t / attack
        rem = dur - t
        if rem < release:
            amp *= rem / release
        out[s0 + i] += amp * lo


def pad(out, start, dur, freqs, gain, attack=0.8, release=1.0, cutoff=900.0, detune=0.005):
    """Detuned sawtooth pairs through one lowpass — warm, wide, and cheap enough for a minute
    of music in pure Python."""
    s0, count = span(out, start, dur)
    incs = [f * (1.0 + d) * INV for f in freqs for d in (-detune, detune)]
    phases = [(i * 0.371) % 1.0 for i in range(len(incs))]
    voices = range(len(incs))
    nv = float(len(incs))
    g = gain / math.sqrt(nv)
    alpha = 1.0 - math.exp(-TAU * cutoff * INV)
    lo = 0.0
    for i in range(count):
        s = 0.0
        for v in voices:
            p = phases[v] + incs[v]
            if p >= 1.0:
                p -= 1.0
            phases[v] = p
            s += p
        lo += alpha * (2.0 * s - nv - lo)
        t = i * INV
        amp = g
        if t < attack:
            amp *= t / attack
        rem = dur - t
        if rem < release:
            amp *= rem / release
        out[s0 + i] += amp * lo


def pluck(out, start, dur, freq, gain, rng, damping=0.995, smooth=2):
    """Karplus-Strong string: a burst of noise in a delay line that averages itself into a tone."""
    s0, count = span(out, start, dur)
    period = max(2, int(round(RATE / freq)))
    line = [rng.random() * 2.0 - 1.0 for _ in range(period)]
    for _ in range(smooth):
        line = [0.5 * (line[j] + line[j - 1]) for j in range(period)]
    idx = 0
    for i in range(count):
        j = idx + 1
        if j == period:
            j = 0
        cur = line[idx]
        line[idx] = damping * 0.5 * (cur + line[j])
        idx = j
        t = i * INV
        amp = gain
        if t < 0.001:
            amp *= t / 0.001
        rem = dur - t
        if rem < 0.03:
            amp *= rem / 0.03
        out[s0 + i] += amp * cur


# ── Drums ────────────────────────────────────────────────────────────────────────────────

def kick(x, t, gain, rng):
    sweep(x, t, 0.42, 108, 42, gain, 8.5, attack=0.001)
    noise(x, t, 0.025, gain * 0.6, 120, rng, lp0=4200)


def tom(x, t, f, gain, rng):
    sweep(x, t, 0.32, f, f * 0.62, gain, 10, attack=0.001)
    noise(x, t, 0.05, gain * 0.4, 55, rng, lp0=2200)


def shaker(x, t, gain, rng):
    noise(x, t, 0.07, gain, 50, rng, lp0=12000, hp=5000)


def clap(x, t, gain, rng):
    for offset in (0.0, 0.011, 0.023):
        noise(x, t + offset, 0.16, gain, 24, rng, lp0=5000, hp=900)
    sweep(x, t, 0.08, 220, 180, gain * 0.3, 30)


# ── Effects on whole buffers ────────────────────────────────────────────────────────────

COMBS = (1116, 1188, 1277, 1356)
ALLPASSES = (556, 441)


def _comb_into(x, wet, delay, feedback, damp):
    line = [0.0] * delay
    idx = 0
    store = 0.0
    keep = 1.0 - damp
    for i in range(len(x)):
        out = line[idx]
        store = out * keep + store * damp
        line[idx] = x[i] + store * feedback
        idx += 1
        if idx == delay:
            idx = 0
        wet[i] += out


def _allpass(x, delay, feedback=0.5):
    y = array.array("f", bytes(4 * len(x)))
    line = [0.0] * delay
    idx = 0
    for i in range(len(x)):
        b = line[idx]
        v = x[i]
        y[i] = b - v
        line[idx] = v + b * feedback
        idx += 1
        if idx == delay:
            idx = 0
    return y


def reverb(x, room=0.82, damp=0.3, spread=0):
    """Schroeder reverb: parallel damped combs into series allpasses. `spread` offsets every delay
    so the left and right channels decorrelate into width."""
    wet = array.array("f", bytes(4 * len(x)))
    for d in COMBS:
        _comb_into(x, wet, d + spread, room, damp)
    for i in range(len(wet)):
        wet[i] *= 0.25
    for d in ALLPASSES:
        wet = _allpass(wet, d + spread)
    return wet


def add_reverb(x, mix, room=0.8, damp=0.3):
    wet = reverb(x, room, damp)
    for i in range(len(x)):
        x[i] += mix * wet[i]


def finalize(x, peak=0.9, drive=1.0):
    """Normalise, optionally saturate, trim trailing silence, and fade the edges."""
    m = max(abs(v) for v in x) or 1.0
    if drive > 1.0:
        tanh = math.tanh
        norm = drive / m
        td = tanh(drive)
        for i in range(len(x)):
            x[i] = tanh(x[i] * norm) / td
        m = max(abs(v) for v in x) or 1.0
    scale = peak / m
    for i in range(len(x)):
        x[i] *= scale

    floor = 0.002 * peak
    last = len(x) - 1
    while last > 0 and abs(x[last]) < floor:
        last -= 1
    x = x[:min(len(x), last + 441)]

    fade_in, fade_out = 32, min(441, len(x))
    for i in range(min(fade_in, len(x))):
        x[i] *= i / fade_in
    for i in range(fade_out):
        x[len(x) - 1 - i] *= i / fade_out
    return x


def master_loop(x, length, mix, room, damp, peak=0.8):
    """Stereo reverb, then wrap the tail onto the start so the loop has no seam: the reverb and
    the pads ringing past the loop point become the tail that the next pass through begins in."""
    loop = int(length * RATE)
    wet_l = reverb(x, room, damp, 0)
    wet_r = reverb(x, room, damp, 23)
    left = array.array("f", x)
    right = array.array("f", x)
    for i in range(len(x)):
        left[i] += mix * wet_l[i]
        right[i] += mix * wet_r[i]
    for i in range(len(x) - loop):
        left[i] += left[loop + i]
        right[i] += right[loop + i]
    left, right = left[:loop], right[:loop]
    m = max(max(abs(v) for v in left), max(abs(v) for v in right)) or 1.0
    s = peak / m
    for i in range(loop):
        left[i] *= s
        right[i] *= s
    return left, right


def write_wav(path, *channels):
    n = len(channels[0])
    frames = array.array("h", bytes(2 * n * len(channels)))
    idx = 0
    for i in range(n):
        for ch in channels:
            v = ch[i]
            v = 1.0 if v > 1.0 else (-1.0 if v < -1.0 else v)
            frames[idx] = int(v * 32767)
            idx += 1
    if sys.byteorder == "big":
        frames.byteswap()
    with wave.open(str(path), "wb") as w:
        w.setnchannels(len(channels))
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(frames.tobytes())


# ── Sound effects ───────────────────────────────────────────────────────────────────────

def sfx_card_draw(rng):
    x = silence(0.2)
    noise(x, 0, 0.16, 1.0, 26, rng, lp0=2200, lp1=7500, hp=900, attack=0.01)
    return finalize(x, peak=0.45)


def sfx_card_play(rng):
    x = silence(0.35)
    noise(x, 0, 0.3, 1.0, 11, rng, lp0=500, lp1=6000, hp=250, attack=0.05)
    sweep(x, 0.06, 0.22, 170, 85, 0.6, 16)
    return finalize(x, peak=0.62)


def sfx_hit(rng):
    x = silence(0.4)
    sweep(x, 0, 0.32, 150, 48, 1.0, 13, attack=0.001)
    noise(x, 0, 0.2, 1.2, 28, rng, lp0=3800, lp1=700)
    return finalize(x, peak=0.85, drive=2.2)


def sfx_heavy_hit(rng):
    x = silence(0.9)
    sweep(x, 0, 0.65, 118, 36, 1.0, 6.5, attack=0.001)
    sweep(x, 0, 0.5, 58, 34, 0.7, 5)
    noise(x, 0, 0.45, 1.3, 11, rng, lp0=2600, lp1=300)
    noise(x, 0.02, 0.3, 0.6, 14, rng, lp0=900, lp1=200)
    add_reverb(x, 0.18, room=0.7)
    return finalize(x, peak=0.95, drive=2.6)


def sfx_blocked_hit(rng):
    x = silence(0.6)
    metal(x, 0, 0.55, 430, 0.8, ((1.0, 1.0, 9), (2.76, 0.55, 12), (5.40, 0.35, 16), (8.93, 0.22, 22)))
    noise(x, 0, 0.04, 1.2, 90, rng, lp0=7000, hp=1500)
    sweep(x, 0, 0.16, 210, 120, 0.5, 24)
    return finalize(x, peak=0.72)


def sfx_block_gain(rng):
    x = silence(0.8)
    for f, g in ((520, 0.5), (780, 0.35), (1040, 0.25)):
        sweep(x, 0, 0.42, f, f * 1.33, g, 8, attack=0.02)
    noise(x, 0, 0.32, 0.4, 10, rng, lp0=3000, lp1=9000, hp=2000, attack=0.03)
    add_reverb(x, 0.25, room=0.75)
    return finalize(x, peak=0.52)


def sfx_burn(rng):
    x = silence(0.6)
    noise(x, 0, 0.5, 0.5, 5, rng, lp0=5500, hp=2500, attack=0.05)
    for _ in range(40):
        noise(x, rng.random() * 0.45, 0.012, rng.uniform(0.4, 1.3), 320, rng, lp0=7000, hp=1400)
    return finalize(x, peak=0.52)


def sfx_heat(rng):
    x = silence(0.45)
    noise(x, 0, 0.4, 1.0, 5, rng, lp0=350, lp1=4200, hp=150, attack=0.14)
    sweep(x, 0, 0.36, 90, 230, 0.35, 5, attack=0.09)
    return finalize(x, peak=0.45)


def sfx_overheat(rng):
    x = silence(1.0)
    noise(x, 0, 0.9, 1.0, 3.2, rng, lp0=8000, hp=2600, attack=0.03)
    for k in range(3):
        t = k * 0.22
        sweep(x, t, 0.18, 440, 432, 0.45, 12)
        sweep(x, t, 0.18, 466, 458, 0.45, 12)
    return finalize(x, peak=0.66)


def sfx_enemy_death(rng):
    x = silence(1.5)
    sweep(x, 0, 1.0, 320, 52, 0.8, 3.2)
    noise(x, 0, 0.9, 1.1, 3.8, rng, lp0=3200, lp1=140)
    for i in range(24):
        t = 0.05 + (i / 24) ** 1.5 * 0.7
        noise(x, t, 0.02, 0.8 * (1 - i / 30), 160, rng, lp0=2500, hp=300)
    add_reverb(x, 0.3, room=0.8)
    return finalize(x, peak=0.8)


def sfx_player_hurt(rng):
    x = silence(0.45)
    growl(x, 0, 0.3, 190, 118, 0.8, cutoff=1100, vibrato=0.03)
    sweep(x, 0, 0.2, 120, 58, 0.7, 15)
    return finalize(x, peak=0.72, drive=1.8)


def sfx_turn_start(rng):
    x = silence(1.4)
    bell(x, 0, 1.0, hz("E5"), 0.6, decay=4.0, index=1.8)
    bell(x, 0.09, 0.95, hz("B5"), 0.45, decay=4.5, index=1.6)
    add_reverb(x, 0.35, room=0.82)
    return finalize(x, peak=0.42)


def sfx_victory(rng):
    x = silence(3.2)
    for i, name in enumerate(("A4", "C#5", "E5", "A5")):
        bell(x, i * 0.14, 1.8, hz(name), 0.55, decay=2.2, index=2.0)
    pad(x, 0.35, 1.9, [hz(n) for n in ("A3", "E4", "A4", "C#5")], 0.4, attack=0.3, release=1.0, cutoff=1800)
    add_reverb(x, 0.4, room=0.85)
    return finalize(x, peak=0.78)


def sfx_defeat(rng):
    x = silence(3.6)
    for i, name in enumerate(("A3", "F3", "D3")):
        last = i == 2
        f = hz(name)
        pad(x, i * 0.45, 1.8 if last else 0.9, [f, f * 1.5], 0.5,
            attack=0.05, release=1.0 if last else 0.4, cutoff=700)
    sweep(x, 0, 2.2, 110, 55, 0.4, 1.4)
    add_reverb(x, 0.45, room=0.86)
    return finalize(x, peak=0.72)


def sfx_click(rng):
    x = silence(0.08)
    sweep(x, 0, 0.05, 1900, 1500, 0.8, 90, attack=0.0005)
    noise(x, 0, 0.012, 0.5, 380, rng, lp0=8000, hp=3000)
    return finalize(x, peak=0.32)


def sfx_map_select(rng):
    x = silence(0.35)
    sweep(x, 0, 0.26, 330, 250, 0.9, 20, attack=0.001)
    noise(x, 0, 0.06, 0.6, 60, rng, lp0=1600, hp=300)
    return finalize(x, peak=0.5)


def sfx_reward(rng):
    x = silence(1.4)
    for i, name in enumerate(("E5", "G#5", "B5", "E6")):
        bell(x, i * 0.07, 1.0, hz(name), 0.5, decay=5.0, index=1.5)
    add_reverb(x, 0.3, room=0.8)
    return finalize(x, peak=0.55)


def sfx_upgrade(rng):
    x = silence(2.2)
    ring = ((1.0, 1.0, 2.2), (2.76, 0.6, 3.5), (5.40, 0.4, 5.0), (8.93, 0.25, 7.0))
    for t, g in ((0.0, 1.0), (0.32, 0.6)):
        metal(x, t, 1.5, 392, g, ring)
        noise(x, t, 0.05, 1.2 * g, 70, rng, lp0=7000, hp=1200)
        sweep(x, t, 0.2, 160, 90, 0.6 * g, 18)
    add_reverb(x, 0.3, room=0.8)
    return finalize(x, peak=0.8)


def sfx_relic(rng):
    x = silence(2.4)
    for i, name in enumerate(("D5", "F#5", "A5", "D6")):
        bell(x, i * 0.05, 1.8, hz(name), 0.45, decay=2.2, index=1.4, ratio=2.01)
    noise(x, 0, 1.2, 0.2, 2.5, rng, lp0=12000, hp=5000, attack=0.2)
    add_reverb(x, 0.45, room=0.86)
    return finalize(x, peak=0.58)


def sfx_buff(rng):
    x = silence(0.7)
    growl(x, 0, 0.18, hz("D4"), hz("D4"), 0.5, cutoff=1800)
    growl(x, 0.12, 0.3, hz("A4"), hz("A4") * 1.01, 0.5, cutoff=2200)
    add_reverb(x, 0.2, room=0.7)
    return finalize(x, peak=0.46)


def sfx_debuff(rng):
    x = silence(0.8)
    growl(x, 0, 0.2, hz("G4"), hz("G4") * 0.97, 0.5, cutoff=1400)
    growl(x, 0.14, 0.35, hz("C#4"), hz("C#4") * 0.95, 0.5, cutoff=1000)
    add_reverb(x, 0.2, room=0.7)
    return finalize(x, peak=0.46)


SFX = {name[4:]: fn for name, fn in globals().items() if name.startswith("sfx_") and callable(fn)}


# ── Music ───────────────────────────────────────────────────────────────────────────────
# All three tracks sit in D, so the crossfade between map and combat never lands on a clash.

def loop_track(bpm, chords, seed, boss):
    rng = random.Random(seed)
    drums = random.Random(seed + 1)
    beat = 60.0 / bpm
    bar = 4 * beat
    length = 4 * len(chords) * bar
    x = silence(length + 5.0)
    bass_pattern = (1, 0, 1, 2, 0, 1, 2, 1) if boss else (1, 0, 0, 1, 0, 1, 2, 0)

    pad(x, 0, length + 2.0, [hz("D1")], 0.4, attack=2.0, release=2.0, cutoff=200)

    for ci, (root, tones) in enumerate(chords):
        t0 = ci * 4 * bar
        freqs = [hz(t) for t in tones]
        pad(x, t0, 4 * bar + 1.0, freqs, 0.30 if boss else 0.26, attack=1.0, release=1.2,
            cutoff=800 if boss else 650)
        rf = hz(root)
        for b in range(4):
            i = ci * 4 + b
            bt = t0 + b * bar
            for step, hit in enumerate(bass_pattern):
                if not hit:
                    continue
                st = bt + step * beat / 2
                f = rf * (2 if hit == 2 else 1)
                growl(x, st, beat * 0.46, f, f, 0.42, cutoff=520, attack=0.004, decay=3.5)
                sweep(x, st, beat * 0.46, f, f, 0.32, 3.5)

            for k in ((0, 1, 2, 3) if boss else (0, 2)):
                kick(x, bt + k * beat, 0.95, drums)
            if boss:
                for k in (1, 3):
                    clap(x, bt + k * beat, 0.4, drums)
            if i >= 2:
                for s in range(8):
                    shaker(x, bt + s * beat / 2, 0.16 if s % 2 == 0 else 0.09, drums)
            if i % 4 == 3:
                for s, f in enumerate((210, 175, 145, 120)):
                    tom(x, bt + 3 * beat + s * beat / 4, f, 0.5, drums)
            elif i % 2 == 1:
                tom(x, bt + 3.5 * beat, 150, 0.4, drums)

            if boss or i >= 4:
                order = (0, 1, 2, 1, 0, 2, 1, 2)
                for s in range(8):
                    pluck(x, bt + s * beat / 2, beat * 1.1, freqs[order[s]] * 2,
                          0.2 if boss else 0.17, rng, damping=0.993)

            if not boss and i >= 8 and i % 2 == 0:
                name = rng.choice(("D5", "F5", "G5", "A5", "C6"))
                bell(x, bt + rng.choice((0, 1, 2)) * beat, 1.6, hz(name), 0.12, decay=1.8, index=1.6)

    return master_loop(x, length, mix=0.32, room=0.8, damp=0.35)


def music_combat():
    chords = [("D2", ("D3", "F3", "A3")), ("Bb1", ("Bb2", "D3", "F3")),
              ("C2", ("C3", "E3", "G3")), ("A1", ("A2", "C#3", "E3"))]
    return loop_track(bpm=96, chords=chords, seed=11, boss=False)


def music_boss():
    chords = [("D2", ("D3", "F3", "A3")), ("Eb2", ("Eb3", "G3", "Bb3")),
              ("D2", ("D3", "F3", "A3")), ("C#2", ("C#3", "E3", "G3"))]
    return loop_track(bpm=112, chords=chords, seed=23, boss=True)


def music_map():
    rng = random.Random(7)
    beat = 60.0 / 64
    bar = 4 * beat
    chords = [("D2", ("D3", "F3", "A3", "C4", "E4")), ("Bb1", ("Bb2", "D3", "F3", "A3")),
              ("G1", ("G2", "Bb2", "D3", "F3")), ("C2", ("C3", "E3", "G3", "D4"))]
    length = 4 * len(chords) * bar
    x = silence(length + 6.0)

    pad(x, 0, length + 3.0, [hz("D2")], 0.3, attack=3.0, release=3.0, cutoff=240)
    for ci, (_root, tones) in enumerate(chords):
        t0 = ci * 4 * bar
        freqs = [hz(t) for t in tones]
        pad(x, t0, 4 * bar + 2.5, freqs, 0.34, attack=2.5, release=2.5, cutoff=950, detune=0.007)
        for b in range(16):
            if rng.random() < 0.42:
                f = rng.choice(freqs) * rng.choice((2, 4))
                pluck(x, t0 + b * beat + rng.uniform(0, 0.05), 2.4, f, 0.16, rng, damping=0.9975, smooth=3)
        bell_t = t0 + rng.choice((1, 2, 3)) * bar + rng.choice((0, 1, 2)) * beat
        bell(x, bell_t, 2.2, hz(rng.choice(("D5", "E5", "F5", "A5", "C6"))), 0.08, decay=1.4, index=1.2)

    # A bed of embers: sparse crackles, the one sound that says "fire" without a melody.
    for _ in range(int(length * 3)):
        noise(x, rng.random() * length, 0.012, rng.uniform(0.03, 0.1), 330, rng, lp0=7000, hp=1600)

    return master_loop(x, length, mix=0.55, room=0.88, damp=0.3, peak=0.7)


MUSIC = {"map": music_map, "combat": music_combat, "boss": music_boss}


# ── Main ────────────────────────────────────────────────────────────────────────────────

def has_vorbis():
    if not shutil.which("ffmpeg"):
        return False
    listing = subprocess.run(["ffmpeg", "-hide_banner", "-encoders"], capture_output=True, text=True).stdout
    return "libvorbis" in listing


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--sfx", action="store_true")
    parser.add_argument("--music", action="store_true")
    parser.add_argument("--only")
    args = parser.parse_args()
    everything = not (args.sfx or args.music or args.only)

    OUT.mkdir(parents=True, exist_ok=True)

    for name, fn in SFX.items():
        if args.only not in (None, name) or not (everything or args.sfx or args.only):
            continue
        start = time.time()
        data = fn(random.Random(zlib.crc32(name.encode())))
        write_wav(OUT / f"sfx_{name}.wav", data)
        print(f"[audio] sfx_{name}: {len(data) / RATE:.2f}s ({time.time() - start:.1f}s)")

    vorbis = has_vorbis()
    for name, fn in MUSIC.items():
        if args.only not in (None, name) or not (everything or args.music or args.only):
            continue
        start = time.time()
        left, right = fn()
        if vorbis:
            with tempfile.TemporaryDirectory() as tmp:
                wav = pathlib.Path(tmp) / f"{name}.wav"
                write_wav(wav, left, right)
                subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav), "-c:a", "libvorbis",
                                "-q:a", "5", str(OUT / f"music_{name}.ogg")], check=True)
            (OUT / f"music_{name}.wav").unlink(missing_ok=True)
            target = f"music_{name}.ogg"
        else:
            write_wav(OUT / f"music_{name}.wav", left, right)
            target = f"music_{name}.wav"
        print(f"[audio] {target}: {len(left) / RATE:.1f}s loop ({time.time() - start:.0f}s)")

    print(f"[audio] done -> {OUT}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
