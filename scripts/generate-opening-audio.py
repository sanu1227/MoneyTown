"""Mix MoneyTown's saved Korean opening narration with original sound effects.

Python stdlib only. Read the committed source WAV and its phrase timing manifest.
--check verifies the exact mix without speech synthesis or file changes.
"""
import argparse
from array import array
import hashlib
import io
import json
import math
from pathlib import Path
import random
import sys
import wave

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets/audio/opening-narration-source.wav"
MANIFEST = SOURCE.with_suffix(".json")
OUTPUT = ROOT / "unity/Assets/Resources/Audio/story-opening.wav"
RATE = 44100
TEXT = "이건 내가 세계 최고의 부자가 되는 이야기이다"
PHRASES = ("이건 내가", "세계 최고의 부자가", "되는 이야기이다")
LEAD = .2
TAIL = .6
SEED = 20261016
META = """fileFormatVersion: 2
guid: 52aa2d6136bf4db7b9f807c9da055b48
AudioImporter:
  externalObjects: {}
  serializedVersion: 7
  defaultSettings:
    serializedVersion: 2
    loadType: 0
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


def sha(data):
    return hashlib.sha256(data).hexdigest()


def read_source(manifest):
    raw = SOURCE.read_bytes()
    if sha(raw) != manifest["source"]["sha256"]:
        raise ValueError("Source WAV differs from the saved manifest.")
    if manifest["text"] != TEXT:
        raise ValueError("Source text does not match this generator.")
    with wave.open(io.BytesIO(raw), "rb") as reader:
        if (reader.getnchannels(), reader.getsampwidth(), reader.getframerate(), reader.getcomptype()) != (1, 2, RATE, "NONE"):
            raise ValueError("Source must be mono 44.1 kHz 16-bit PCM.")
        samples = array("h", reader.readframes(reader.getnframes()))
    if sys.byteorder != "little":
        samples.byteswap()
    expected_source = {"file": SOURCE.relative_to(ROOT).as_posix(), "sha256": sha(raw),
                       "sample_rate": RATE, "channels": 1, "sample_width_bits": 16,
                       "frames": len(samples), "duration_seconds": len(samples) / RATE}
    if manifest["source"] != expected_source:
        raise ValueError("Source format or duration metadata differs from its WAV.")
    if not samples or max(abs(x) for x in samples) < 100:
        raise ValueError("Source voice is empty or effectively silent.")
    cues = manifest["cues"]
    if [cue["text"] for cue in cues] != list(PHRASES):
        raise ValueError("Three ordered opening phrases are required.")
    times = [float(cue["start_seconds"]) for cue in cues]
    if not 0 <= times[0] < times[1] < times[2] < len(samples) / RATE:
        raise ValueError("Phrase cue times are outside the source or out of order.")
    return samples, times


def synthesize(source, times):
    duration = LEAD + len(source) / RATE + TAIL
    mix = [0.0] * round(duration * RATE)
    offset = round(LEAD * RATE)
    voice_peak = max(abs(value) for value in source) / 32768
    gain = .69 / voice_peak
    for i, value in enumerate(source):
        # Keep the spoken timing unchanged. Only a 5 ms edge fade and gain apply.
        edge = min(1, i / (RATE * .005), (len(source) - 1 - i) / (RATE * .005))
        mix[offset + i] = value / 32768 * gain * max(0, edge)
    tau = 2 * math.pi
    events = []

    def add(name, start, length, sound):
        events.append({"name": name, "start_seconds": round(start, 6), "duration_seconds": length})
        first = round(start * RATE)
        count = min(round(length * RATE), len(mix) - first)
        for i in range(count):
            t = i / RATE
            fade = min(1, t / .025, (length - t) / .08)
            mix[first + i] += sound(t) * max(0, fade)

    emphasis = LEAD + times[1]
    add("soft-low-resonance", emphasis, 1.35,
        lambda t: .045 * (1 - math.exp(-14 * t)) * math.exp(-2.8 * t)
        * (math.sin(tau * 98 * t) + .35 * math.sin(tau * 147 * t)))
    for step, frequency in enumerate((784.0, 987.77, 1174.66)):
        add("gold-chime-" + str(step + 1), emphasis + .06 + step * .11, 1.15,
            lambda t, f=frequency: .014 * math.exp(-4.8 * t)
            * (math.sin(tau * f * t) + .18 * math.sin(tau * f * 2.01 * t)))

    rng = random.Random(SEED)
    previous = 0.0
    swell_start = LEAD + times[2] + .12
    swell_length = min(1.65, duration - swell_start - .12)

    def swell(t):
        nonlocal previous
        previous = .91 * previous + .09 * rng.uniform(-1, 1)
        envelope = math.sin(math.pi * t / swell_length) ** 2
        # Quiet filtered air plus a rising harmonic; well below the narration.
        phase = tau * (330 * t + 80 * t * t / swell_length)
        return envelope * (.025 * previous + .009 * math.sin(phase))

    add("city-reveal-swell", swell_start, swell_length, swell)
    peak = max(abs(value) for value in mix)
    if peak > .85:
        raise ValueError("Opening mix exceeds the 0.85 peak limit.")
    pcm = array("h", (round(value * 32767) for value in mix))
    if sys.byteorder != "little":
        pcm.byteswap()
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(RATE)
        writer.writeframes(pcm.tobytes())
    result = {"file": OUTPUT.relative_to(ROOT).as_posix(), "sha256": sha(buffer.getvalue()),
              "frames": len(mix), "sample_rate": RATE, "channels": 1, "sample_width_bits": 16,
              "duration_seconds": round(len(mix) / RATE, 6), "peak": round(peak, 6),
              "rms": round(math.sqrt(sum(value * value for value in mix) / len(mix)), 6),
              "voice_gain": round(gain, 9), "lead_seconds": LEAD, "tail_seconds": TAIL,
              "noise_seed": SEED,
              "text_cues": [{"text": text, "start_seconds": round(LEAD + start, 6)}
                            for text, start in zip(PHRASES, times)], "effects": events}
    return buffer.getvalue(), result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify committed source and exact mix without TTS or writes.")
    args = parser.parse_args()
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    samples, times = read_source(manifest)
    data, result = synthesize(samples, times)
    meta_path = OUTPUT.with_suffix(".wav.meta")
    if args.check:
        if OUTPUT.read_bytes() != data or manifest.get("mix") != result:
            raise ValueError("Final WAV or mix manifest does not reproduce exactly.")
        if meta_path.read_text(encoding="utf-8").replace("\r\n", "\n") != META:
            raise ValueError("Unity audio importer metadata differs from generator.")
        with wave.open(io.BytesIO(data), "rb") as reader:
            pcm = array("h", reader.readframes(reader.getnframes()))
        if sys.byteorder != "little":
            pcm.byteswap()
        if any(pcm[:round(.19 * RATE)]) or pcm[-1] != 0 or max(abs(x) for x in pcm) > round(.85 * 32768):
            raise ValueError("Unexpected leading sound, tail edge, or clipping.")
        print("Opening audio check passed: source SHA, exact mix, phrase cues, PCM format, level, edges, Unity meta.")
    else:
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        OUTPUT.write_bytes(data)
        meta_path.write_text(META, encoding="utf-8", newline="\n")
        manifest["mix"] = result
        MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
        print("Opening audio written using the saved narration and original synthesized effects.")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, RuntimeError, KeyError, wave.Error) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
