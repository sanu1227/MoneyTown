"""Compose four original MoneyTown tracks with Python's standard library.

No samples, third-party music, external synthesizer or audio dependency is used.
Run normally to write WAV + Unity importer metadata, or --check to reproduce and
verify the existing files without modifying them.
"""
import argparse
from array import array
from functools import lru_cache
import hashlib
import io
import math
from pathlib import Path
import random
import sys
import wave

ROOT = Path(__file__).resolve().parents[1]
PATH = ROOT / "unity/Assets/Resources/Audio/town-lounge.wav"
RATE, BPM, BARS = 44100, 88, 32
BEAT = 60 / BPM
FRAMES = round(BARS * 4 * BEAT * RATE)
TAU = 2 * math.pi

# Written for this game: root (MIDI), close Rhodes voicing, named harmony.
A = [
    (38, (54, 61, 64, 69), "Dmaj9"),
    (35, (57, 61, 62, 66), "Bm9"),
    (40, (55, 62, 66, 71), "Em9"),
    (33, (55, 61, 66, 71), "A13"),
    (42, (57, 61, 64, 68), "F#m9"),
    (35, (57, 63, 66, 68), "B13"),
    (40, (55, 62, 66, 71), "Em9"),
    (33, (55, 61, 66, 71), "A13"),
]
B = [
    (43, (54, 57, 59, 62), "Gmaj9"),
    (42, (57, 61, 64, 69), "Dmaj9/F#"),
    (40, (55, 62, 66, 71), "Em9"),
    (33, (55, 61, 66, 71), "A13"),
    (38, (54, 61, 64, 69), "Dmaj9"),
    (35, (57, 61, 62, 66), "Bm9"),
    (40, (55, 62, 66, 71), "Em9"),
    (33, (55, 61, 66, 71), "A13"),
]

# Sparse, original answer-and-response phrases: beat, MIDI pitch, held beats.
MELODY_A = [
    ((.5, 73, .65), (1.55, 76, .85), (3, 78, .65)),
    ((.55, 78, .6), (1.55, 76, .65), (2.55, 74, 1.05)),
    ((1, 78, .85), (2.55, 74, .65), (3.55, 71, .45)),
    ((.55, 73, 1.25), (2.55, 71, .65)),
    ((.5, 73, .65), (1.55, 76, .85), (3, 80, .6)),
    ((.55, 78, .65), (1.55, 75, .6), (2.55, 73, .85)),
    ((1, 74, .85), (2.55, 78, .6), (3.55, 76, .4)),
    ((.5, 73, 1.15), (2.55, 71, .6), (3.55, 69, .4)),
]
MELODY_B = [
    ((.5, 74, .7), (1.55, 78, .7), (3, 81, .7)),
    ((.5, 81, .65), (1.55, 78, .65), (2.55, 76, 1.05)),
    ((.5, 78, .65), (1.55, 79, .7), (3, 78, .7)),
    ((.5, 76, .85), (2, 73, .85)),
    ((1, 78, .85), (2.55, 76, .65), (3.55, 73, .4)),
    ((.5, 74, .7), (1.55, 73, .7), (3, 69, .7)),
    ((.5, 71, .85), (2, 74, .85), (3.55, 78, .4)),
    ((.5, 76, .65), (1.55, 73, .7), (3, 69, .7)),
]


@lru_cache(maxsize=None)
def note(instrument, midi, held):
    """Small additive/FM instruments; release tails are mixed over the loop end."""
    frequency = 440 * 2 ** ((midi - 69) / 12)
    hold = held * BEAT
    tail = .72 if instrument == "keys" else .32 if instrument == "lead" else .15
    duration = hold + tail
    result = array("f")
    for i in range(round(duration * RATE)):
        t = i / RATE
        attack = min(1, t / (.012 if instrument == "bass" else .018))
        release = min(1, max(0, (duration - t) / tail)) ** 2
        phase = TAU * frequency * t
        if instrument == "keys":
            # Soft tine attack, slowly beating warm body, no bright bell partials.
            mod = .65 * math.exp(-5 * t) * math.sin(2 * phase)
            value = (.83 * math.sin(phase + mod)
                     + .12 * math.sin(2 * phase) * math.exp(-2.8 * t)
                     + .05 * math.sin(phase * 1.0014))
            env = (.28 + .72 * math.exp(-1.9 * t)) * attack * release
        elif instrument == "lead":
            value = math.sin(phase + .15 * math.sin(2 * phase)) + .1 * math.sin(2 * phase)
            env = (.32 + .68 * math.exp(-2.7 * t)) * attack * release
        else:
            value = math.sin(phase) + .13 * math.sin(2 * phase) + .035 * math.sin(3 * phase)
            env = (.64 + .36 * math.exp(-3 * t)) * attack * release
        result.append(value * env)
    return result


@lru_cache(maxsize=None)
def drum(kind, variant):
    rng = random.Random(8800 + variant)
    duration = {"kick": .25, "rim": .11, "hat": .065, "brush": .20}[kind]
    result = array("f")
    low = phase = 0.
    for i in range(round(duration * RATE)):
        t = i / RATE
        white = rng.uniform(-1, 1)
        low += .14 * (white - low)
        if kind == "kick":
            phase += TAU * (46 + 40 * math.exp(-32 * t)) / RATE
            value = math.sin(phase) * math.exp(-17 * t) + .05 * low * math.exp(-60 * t)
        elif kind == "rim":
            value = (.65 * math.sin(TAU * 980 * t) + .3 * math.sin(TAU * 1460 * t) + .35 * low) * math.exp(-60 * t)
        elif kind == "hat":
            value = (white - low) * math.exp(-65 * t)
        else:
            value = low * math.exp(-21 * t)
        edge = min(1, t / .002, (duration - t) / .01)
        result.append(value * edge)
    return result


def synthesize():
    left, right = array("f", [0]) * FRAMES, array("f", [0]) * FRAMES

    def mix(signal, start, level, pan=0):
        first = round(start * BEAT * RATE) % FRAMES
        lg, rg = level * math.sqrt((1 - pan) / 2), level * math.sqrt((1 + pan) / 2)
        # Normal case avoids per-sample modulus. Only final notes cross the seam.
        count = min(len(signal), FRAMES - first)
        for i in range(count):
            left[first + i] += signal[i] * lg
            right[first + i] += signal[i] * rg
        for i in range(count, len(signal)):
            left[i - count] += signal[i] * lg
            right[i - count] += signal[i] * rg

    for bar in range(BARS):
        section, index = divmod(bar, 8)
        root, voicing, _ = (B if section == 2 else A)[index]
        start = bar * 4
        # Four arrangements with small timing/velocity differences, not an 8-bar copy.
        rhythm = (((0, 1.8, .10), (2.55, 1.1, .072)),
                  ((.06, 1.5, .095), (1.55, .6, .055), (2.55, 1.0, .072)),
                  ((0, 1.3, .092), (1.55, .8, .067), (3.05, .8, .067)),
                  ((0, 1.8, .10), (2.55, 1.1, .075)))[section]
        for beat, length, gain in rhythm:
            for n, pitch in enumerate(voicing):
                mix(note("keys", pitch, length), start + beat + n * .018,
                    gain * (1 + .025 * ((bar + n) % 3 - 1)), -.34 + n * .20)
        # Root/fifth/octave bass; restrained eighth-note pickups in alternate bars.
        mix(note("bass", root, 1.65), start, .22, -.04)
        mix(note("bass", root + (7 if index % 2 == 0 else 12), 1.15), start + 2, .155, -.04)
        if section in (1, 2) and index % 2 == 1:
            next_root = (B if section == 2 else A)[(index + 1) % 8][0]
            mix(note("bass", next_root - 1, .30), start + 3.55, .105, -.04)
        # Light swing: offbeats fall at .56 of each beat. Last bar leaves room.
        for beat in (0, 2):
            mix(drum("kick", 0), start + beat, .20 if beat == 0 else .145)
        for beat in (1, 3):
            mix(drum("rim", bar % 3), start + beat + .018, .048, -.19)
            mix(drum("brush", bar % 4), start + beat + .03, .072, .17)
        for half in range(8):
            if section == 0 and half in (3, 7):
                continue
            if bar == 31 and half >= 6:
                continue
            beat = half // 2 + (.56 if half % 2 else 0)
            mix(drum("hat", (bar + half) % 5), start + beat,
                (.022 if half % 2 else .031) * (1 if section != 2 else 1.13), .30)
        phrase = (MELODY_B if section == 2 else MELODY_A)[index]
        # A is spacious; A' answers one octave lower. B opens the register.
        for j, (beat, pitch, length) in enumerate(phrase):
            if section == 0 and index in (1, 5) and j == 0:
                continue
            if section == 3 and index == 7 and j > 0:
                continue
            octave = -12 if section == 1 and index in (0, 2, 4, 6) else 0
            mix(note("lead", pitch + octave, length), start + beat,
                .068 if section == 2 else .061, .12)
        if section == 3 and index in (1, 3, 5):
            mix(note("keys", voicing[-1] + 12, .65), start + 3.55, .035, -.30)

    # Circular low-pass, cross-channel room reflections: the release from bar 32
    # is already present at bar 1. No padded silence or end fade is introduced.
    dry_l, dry_r = array("f", left), array("f", right)
    for seconds, gain in ((.137, .10), (.251, .07), (.389, .045)):
        delay = round(seconds * RATE)
        low_l, low_r = dry_r[-delay - 1], dry_l[-delay - 1]
        for i in range(FRAMES):
            source = i - delay
            low_l += .18 * (dry_r[source] - low_l)
            low_r += .18 * (dry_l[source] - low_r)
            left[i] += low_l * gain
            right[i] += low_r * gain
    peak = max(max(abs(v) for v in left), max(abs(v) for v in right))
    gain = .50 / peak
    pcm = array("h")
    for l, r in zip(left, right):
        pcm.append(round(l * gain * 32767))
        pcm.append(round(r * gain * 32767))
    if sys.byteorder != "little":
        pcm.byteswap()
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(pcm.tobytes())
    return buffer.getvalue()


META = b"""fileFormatVersion: 2
guid: 9254f7f931704ea586393aebd729a05b
AudioImporter:
  externalObjects: {}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 2
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""


TRACKS = (
    ("market-morning", 96, "4b423992067e46baa03c035d3e78669a", "marimba",
     # A: Cmaj9 / Am9 / Dm9 / G13 / Em9 / A13 / Dm9 / G13.
     ((36, (52, 55, 59, 62)), (33, (48, 55, 59, 60)),
      (38, (53, 57, 60, 64)), (31, (53, 57, 59, 64)),
      (40, (55, 59, 62, 66)), (33, (55, 61, 66, 71)),
      (38, (53, 57, 60, 64)), (31, (53, 57, 59, 64))),
     # B: Fmaj9 / Em9 / Dm9 / G13 / Cmaj9 / Am9 / Fmaj9 / G13.
     ((41, (57, 60, 64, 67)), (40, (55, 59, 62, 66)),
      (38, (53, 57, 60, 64)), (31, (53, 57, 59, 64)),
      (36, (52, 55, 59, 62)), (33, (48, 55, 59, 60)),
      (41, (57, 60, 64, 67)), (31, (53, 57, 59, 64))),
     (((.5, 76, .4), (1, 79, .6), (2.5, 74, .5)),
      ((.5, 72, .6), (1.5, 71, .4), (3, 67, .5)),
      ((0, 69, .5), (1.5, 72, .6), (3, 76, .5)),
      ((.5, 74, .8), (2.5, 71, .8)),
      ((.5, 71, .4), (1, 74, .6), (2.5, 78, .5)),
      ((.5, 76, .6), (1.5, 73, .4), (3, 71, .5)),
      ((0, 72, .5), (1.5, 76, .6), (3, 69, .5)),
      ((.5, 71, .8), (2.5, 74, .8))),
     (((0, 76, .5), (1.5, 79, .5), (2.5, 81, .6)),
      ((.5, 78, .8), (2, 74, .8)),
      ((0, 76, .5), (1.5, 72, .5), (3, 69, .6)),
      ((.5, 71, .8), (2, 74, .8)),
      ((0, 79, .5), (1.5, 76, .5), (3, 74, .6)),
      ((.5, 72, .8), (2, 71, .8)),
      ((0, 69, .5), (1.5, 72, .5), (3, 76, .6)),
      ((.5, 74, .8), (2.5, 71, .8)))),
    ("newsroom-bossa", 80, "245d3cdbe87044cdb40529677980e171", "nylon",
     # A: G6/9 / F# half-diminished / B7 / Em9 / Am9 / D13 / Gmaj9 / D13.
     ((31, (55, 59, 64, 69)), (30, (57, 60, 64, 66)),
      (35, (57, 59, 63, 66)), (40, (55, 59, 62, 66)),
      (33, (55, 59, 60, 64)), (38, (54, 60, 64, 71)),
      (31, (54, 57, 59, 62)), (38, (54, 60, 64, 71))),
     # B: Cmaj9 / C#m7b5 / D6/9 / B7 / Em9 / A13 / Am9 / D13.
     ((36, (55, 59, 62, 64)), (37, (55, 59, 64, 67)),
      (38, (54, 59, 64, 69)), (35, (57, 59, 63, 66)),
      (40, (55, 59, 62, 66)), (33, (55, 61, 66, 71)),
      (33, (55, 59, 60, 64)), (38, (54, 60, 64, 71))),
     (((.75, 71, .9), (2.5, 69, .5), (3.25, 67, .5)),
      ((.25, 66, 1.0), (2, 64, .75)),
      ((.75, 66, .5), (1.5, 63, .75), (3.25, 71, .5)),
      ((.25, 67, 1), (2.5, 66, .75)),
      ((.75, 64, .9), (2.5, 67, .5), (3.25, 71, .5)),
      ((.25, 69, 1), (2, 66, .75)),
      ((.75, 67, .5), (1.5, 69, .75), (3.25, 71, .5)),
      ((.25, 72, 1), (2.5, 69, .75))),
     (((.25, 76, .9), (1.75, 74, .6), (3, 71, .75)),
      ((.75, 71, .7), (2.5, 67, .8)),
      ((.25, 69, .9), (1.75, 71, .6), (3, 76, .75)),
      ((.75, 75, .7), (2.5, 71, .8)),
      ((.25, 74, .9), (1.75, 71, .6), (3, 67, .75)),
      ((.75, 66, .7), (2.5, 73, .8)),
      ((.25, 72, .9), (1.75, 71, .6), (3, 67, .75)),
      ((.75, 66, .7), (2.5, 69, .8)))),
    ("city-evening", 72, "760d41215904407799f285f361c96f4c", "bell",
     # A: Ebmaj9 / Cm9 / Abmaj9 / Bb13 / Gm9 / Cm9 / Fm9 / Bb13.
     ((39, (55, 58, 62, 65)), (36, (55, 58, 62, 63)),
      (32, (55, 58, 60, 63)), (34, (56, 60, 62, 67)),
      (31, (53, 57, 58, 62)), (36, (55, 58, 62, 63)),
      (41, (56, 60, 63, 67)), (34, (56, 60, 62, 67))),
     # B: Abmaj9 / Ebmaj9/G / Fm9 / Bb13 / Cm9 / Gm9 / Abmaj9 / Bb13.
     ((32, (55, 58, 60, 63)), (31, (55, 58, 62, 65)),
      (41, (56, 60, 63, 67)), (34, (56, 60, 62, 67)),
      (36, (55, 58, 62, 63)), (31, (53, 57, 58, 62)),
      (32, (55, 58, 60, 63)), (34, (56, 60, 62, 67))),
     (((.5, 74, 1.3), (2.75, 70, .8)),
      ((1, 67, 1.7),), ((.5, 72, 1.3), (2.75, 75, .8)),
      ((1, 74, 1.7),), ((.5, 69, 1.3), (2.75, 70, .8)),
      ((1, 74, 1.7),), ((.5, 75, 1.3), (2.75, 72, .8)),
      ((1, 70, 1.7),)),
     (((0, 75, 1.5), (2.5, 79, .8)),
      ((1, 77, 1.7),), ((0, 75, 1.5), (2.5, 72, .8)),
      ((1, 74, 1.7),), ((0, 70, 1.5), (2.5, 67, .8)),
      ((1, 69, 1.7),), ((0, 72, 1.5), (2.5, 75, .8)),
      ((1, 74, 1.7),))),
)


@lru_cache(maxsize=256)
def playlist_note(instrument, midi, seconds):
    """Original additive instruments with attacks and releases free of samples."""
    frequency = 440 * 2 ** ((midi - 69) / 12)
    tail = {"felt": .45, "marimba": .2, "nylon": .35,
            "pad": .9, "bell": .8, "bass": .2}[instrument]
    duration = seconds + tail
    result = array("f")
    for i in range(round(duration * RATE)):
        t = i / RATE
        phase = TAU * frequency * t
        attack = min(1, t / (.28 if instrument == "pad" else .008))
        release = min(1, max(0, (duration - t) / tail)) ** 2
        if instrument == "marimba":
            value = (math.sin(phase) * math.exp(-3.3 * t)
                     + .19 * math.sin(phase * 4) * math.exp(-12 * t)
                     + .06 * math.sin(phase * 7) * math.exp(-19 * t))
        elif instrument == "nylon":
            value = (math.sin(phase) + .28 * math.sin(2 * phase) * math.exp(-3 * t)
                     + .15 * math.sin(3 * phase) * math.exp(-5 * t)
                     + .07 * math.sin(4 * phase) * math.exp(-8 * t)) * math.exp(-1.8 * t)
        elif instrument == "pad":
            value = (.64 * math.sin(phase) + .23 * math.sin(phase * 1.0012)
                     + .10 * math.sin(2 * phase) + .03 * math.sin(3 * phase))
        elif instrument == "bell":
            value = (math.sin(phase) * math.exp(-1.9 * t)
                     + .16 * math.sin(phase * 2.003) * math.exp(-4.5 * t)
                     + .045 * math.sin(phase * 4.01) * math.exp(-7 * t))
        elif instrument == "felt":
            value = (math.sin(phase) + .18 * math.sin(2 * phase) * math.exp(-4 * t)
                     + .035 * math.sin(3 * phase) * math.exp(-7 * t)) * (.2 + .8 * math.exp(-2.1 * t))
        else:
            value = (math.sin(phase) + .09 * math.sin(2 * phase)) * (.65 + .35 * math.exp(-3 * t))
        result.append(value * attack * release)
    return result


def synthesize_playlist(track):
    slug, bpm, _, lead, harmony_a, harmony_b, melody_a, melody_b = track
    beat = 60 / bpm
    frames = round(24 * 4 * beat * RATE)
    left, right = array("f", [0]) * frames, array("f", [0]) * frames

    def mix(signal, position, level, pan=0):
        first = round(position * beat * RATE) % frames
        lg, rg = level * math.sqrt((1 - pan) / 2), level * math.sqrt((1 + pan) / 2)
        count = min(len(signal), frames - first)
        for i in range(count):
            left[first + i] += signal[i] * lg
            right[first + i] += signal[i] * rg
        for i in range(count, len(signal)):
            left[i - count] += signal[i] * lg
            right[i - count] += signal[i] * rg

    def pitched(instrument, pitch, held, position, level, pan=0):
        mix(playlist_note(instrument, pitch, held * beat), position, level, pan)

    for bar in range(24):
        section, index = divmod(bar, 8)
        root, voicing = (harmony_b if section == 1 else harmony_a)[index]
        start = bar * 4
        if lead == "marimba":
            # Straight pulse, alternating felt chords and wooden arpeggio answers.
            for position, held, level in ((0, 1.3, .083), (2.5, .8, .064)):
                for n, pitch in enumerate(voicing):
                    pitched("felt", pitch, held, start + position + n * .012, level, -.36 + n * .22)
            for n, position in enumerate((.5, 1.5, 2, 3.5)):
                if section == 0 and index % 2 == 1 and n == 3:
                    continue
                pitched("marimba", voicing[(n + index) % 4], .42, start + position, .075, -.26)
            for position in (0, 2):
                pitched("bass", root + (7 if position == 2 else 0), 1.6, start + position, .195)
                mix(drum("kick", 0), start + position, .14)
            for n in range(8):
                mix(drum("hat", (bar + n) % 5), start + n * .5, .021 if n % 2 else .027, .28)
            for position in (1, 3):
                mix(drum("rim", bar % 3), start + position, .031, -.16)
        elif lead == "nylon":
            # Distinct bossa syncopation and gently staggered nylon-string chords.
            rhythm = ((0, .72, .088), (1.5, .60, .074), (2.5, .78, .083)) if index % 2 == 0 else ((.5, .65, .078), (2, .70, .088), (3.5, .43, .063))
            for position, held, level in rhythm:
                for n, pitch in enumerate(voicing):
                    pitched("nylon", pitch, held, start + position + n * .026, level, -.30 + n * .18)
            for position, interval, held in ((0, 0, 1.3), (1.75, 7, .36), (2.5, 0, .9)):
                pitched("bass", root + interval, held, start + position, .19 if interval == 0 else .135, -.04)
            for position in (0, 2):
                mix(drum("kick", 1), start + position, .105)
            for position in (1, 2.5, 3.5):
                mix(drum("rim", bar % 3), start + position, .029, -.24)
            for n in range(8):
                mix(drum("brush", (bar + n) % 4), start + n * .5, .041 if n % 2 else .058, .26)
        else:
            # Airy sustained harmony; one soft beat per half bar and sparse bells.
            for n, pitch in enumerate(voicing):
                pitched("pad", pitch, 3.8, start + n * .018, .072, -.45 + n * .30)
            pitched("bass", root, 3.55, start, .20, -.03)
            mix(drum("kick", 2), start, .12)
            mix(drum("brush", bar % 4), start + 2, .066, .20)
            if section == 1 or index % 2 == 1:
                mix(drum("hat", bar % 5), start + 3, .019, -.25)
            if section == 2 and index in (1, 3, 5):
                pitched("bell", voicing[-1] + 12, .8, start + 3, .034, -.32)
        phrase = (melody_b if section == 1 else melody_a)[index]
        for n, (position, pitch, held) in enumerate(phrase):
            # Final A varies register and leaves space before the cadence.
            if section == 2 and index == 7 and n > 0:
                continue
            if section == 2 and index in (1, 5):
                pitch -= 12
            pitched(lead, pitch, held, start + position, .083 if lead == "nylon" else .069, .13)

    # Circular short room reflections retain note tails without silent padding.
    dry_l, dry_r = array("f", left), array("f", right)
    reflections = ((.173, .13), (.317, .08)) if lead == "bell" else ((.097, .07), (.193, .045))
    for seconds, gain in reflections:
        delay = round(seconds * RATE)
        low_l, low_r = dry_r[-delay - 1], dry_l[-delay - 1]
        for i in range(frames):
            low_l += .16 * (dry_r[i - delay] - low_l)
            low_r += .16 * (dry_l[i - delay] - low_r)
            left[i] += low_l * gain
            right[i] += low_r * gain
    peak = max(max(abs(v) for v in left), max(abs(v) for v in right))
    # Sustained pads have a lower crest factor; match their average loudness.
    gain = (.40 if lead == "bell" else .50) / peak
    pcm = array("h")
    for l, r in zip(left, right):
        pcm.append(round(l * gain * 32767))
        pcm.append(round(r * gain * 32767))
    if sys.byteorder != "little":
        pcm.byteswap()
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as output:
        output.setnchannels(2)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(pcm.tobytes())
    return buffer.getvalue()


def verify(data, frames=FRAMES):
    with wave.open(io.BytesIO(data), "rb") as source:
        assert (source.getnchannels(), source.getsampwidth(), source.getframerate(), source.getcomptype()) == (2, 2, RATE, "NONE")
        assert source.getnframes() == frames
        pcm = array("h", source.readframes(frames))
    if sys.byteorder != "little":
        pcm.byteswap()
    peak = max(abs(v) for v in pcm) / 32768
    rms = math.sqrt(sum(v * v for v in pcm) / len(pcm)) / 32768
    seam = max(abs(pcm[0] - pcm[-2]), abs(pcm[1] - pcm[-1])) / 32768
    max_step = max(abs(pcm[i] - pcm[i - 2]) for i in range(2, len(pcm))) / 32768
    # Every quarter second contains audible musical content, including the seam.
    windows = []
    width = round(RATE * .25) * 2
    for first in range(0, len(pcm), width):
        chunk = pcm[first:first + width]
        windows.append(math.sqrt(sum(v * v for v in chunk) / len(chunk)) / 32768)
    assert .06 <= rms <= .13 and peak <= .55, (rms, peak)
    assert seam < .025 and seam <= max_step, (seam, max_step)
    assert min(windows) > .006, min(windows)
    assert len(data) < 15 * 1024 * 1024
    print(f"frames={frames}, seconds={frames / RATE:.6f}, bytes={len(data)}")
    print(f"peak={peak:.6f}, RMS={rms:.6f}, seam_step={seam:.6f}, max_sample_step={max_step:.6f}, min_250ms_RMS={min(windows):.6f}")
    print(f"sha256={hashlib.sha256(data).hexdigest()}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Reproduce and check bytes/metrics without writing files.")
    args = parser.parse_args()
    digests = set()
    for track in (None, *TRACKS):
        slug = track[0] if track else "town-lounge"
        print(slug, flush=True)
        generated = synthesize_playlist(track) if track else synthesize()
        frames = round(24 * 4 * 60 / track[1] * RATE) if track else FRAMES
        verify(generated, frames)
        digest = hashlib.sha256(generated).hexdigest()
        assert digest not in digests, "Tracks must have distinct audio."
        digests.add(digest)
        if track is None:
            assert digest == "b734ffa4cbee239d3e39eff6888c80ac743c7918c21e1876c210e22a26028bc4", "Original lounge must remain byte-identical."
        path = PATH.with_name(slug + ".wav")
        meta = META.replace(b"9254f7f931704ea586393aebd729a05b", track[2].encode()) if track else META
        for target, expected in ((path, generated), (path.with_suffix(".wav.meta"), meta)):
            if not args.check and (not target.exists() or target.read_bytes() != expected):
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(expected)
            assert target.read_bytes() == expected, f"Generated bytes differ: {target}"
        print("PASS: WAV and importer bytes match.", flush=True)
    assert len(digests) == 4
    print("PASS: four original tracks; exact reproduction, distinct audio, PCM, levels, lengths, seams and silence checks passed.")


if __name__ == "__main__":
    main()
