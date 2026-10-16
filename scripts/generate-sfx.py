"""Create MoneyTown's original short UI sounds using only Python's standard library."""
import argparse
import hashlib
import io
import math
from pathlib import Path
import random
import struct
import uuid
import wave

ROOT = Path(__file__).resolve().parents[1]
AUDIO = ROOT / "unity/Assets/Resources/Audio"
RATE = 44100
GAP = 0.35
# File order is also the listening order in assets/audio/preview.wav.
SPECS = {
    "tap": (0.12, 0.42),
    "buy": (0.32, 0.50),
    "sell": (0.43, 0.48),
    "paper": (0.42, 0.40),
    "settlement": (0.64, 0.50),
    "work": (0.22, 0.46),
    "success": (0.52, 0.52),
    "error": (0.38, 0.42),
}


def envelope(t, duration, decay=3.0, attack=0.006, release=0.018):
    fade_in = math.sin(math.pi / 2 * min(1.0, t / attack)) ** 2
    fade_out = math.sin(math.pi / 2 * min(1.0, max(0.0, duration - t) / release)) ** 2
    return fade_in * fade_out * math.exp(-decay * t / duration)


def tone(signal, start, duration, frequency, gain=1.0, harmonics=((1.0, 1.0),), decay=3.0):
    first, count = round(start * RATE), round(duration * RATE)
    for i in range(min(count, len(signal) - first)):
        t = i / RATE
        value = sum(level * math.sin(2 * math.pi * frequency * ratio * t) for ratio, level in harmonics)
        signal[first + i] += gain * value * envelope(t, duration, decay)


def noise(signal, start, duration, seed, gain, bright=False):
    rng = random.Random(seed)
    first, count = round(start * RATE), round(duration * RATE)
    low = filtered = 0.0
    for i in range(min(count, len(signal) - first)):
        white = rng.uniform(-1.0, 1.0)
        low += 0.08 * (white - low)
        filtered += 0.55 * ((white - low if bright else low) - filtered)
        signal[first + i] += gain * filtered * envelope(i / RATE, duration, 2.6, 0.004, 0.014)


def synthesize(name):
    duration, peak = SPECS[name]
    signal = [0.0] * round(duration * RATE)
    warm = ((1.0, 1.0), (2.0, 0.18), (3.0, 0.05))
    bell = ((1.0, 1.0), (2.0, 0.14), (3.01, 0.05))
    if name == "tap":
        tone(signal, 0, 0.10, 820, harmonics=((1.0, 1.0), (1.5, 0.20)), decay=6)
        noise(signal, 0, 0.045, 101, 0.35)
    elif name == "buy":
        tone(signal, 0, 0.17, 523.25, harmonics=warm)
        tone(signal, 0.105, 0.205, 783.99, harmonics=warm)
    elif name == "sell":
        for start, pitch in ((0, 659.25), (0.11, 554.37), (0.22, 440.0)):
            tone(signal, start, 0.20, pitch, harmonics=warm, decay=2.6)
    elif name == "paper":
        for start, length, seed, gain in ((0, 0.13, 201, 0.60), (0.075, 0.16, 202, 0.85), (0.19, 0.14, 203, 0.65)):
            noise(signal, start, length, seed, gain, bright=True)
        tone(signal, 0.285, 0.115, 480, 0.28, ((1.0, 1.0), (1.7, 0.20)), decay=5)
    elif name == "settlement":
        tone(signal, 0, 0.60, 392.0, harmonics=bell, decay=2.8)
        tone(signal, 0.09, 0.53, 587.33, 0.65, bell, decay=2.8)
    elif name == "work":
        tone(signal, 0, 0.20, 170, harmonics=((1.0, 1.0), (2.1, 0.35), (4.2, 0.12)), decay=7)
        noise(signal, 0, 0.075, 301, 1.7)
    elif name == "success":
        for start, pitch in ((0, 523.25), (0.115, 659.25), (0.23, 783.99)):
            tone(signal, start, 0.28, pitch, harmonics=bell, decay=3.2)
    elif name == "error":
        tone(signal, 0, 0.17, 220, harmonics=((1.0, 1.0), (2.0, 0.12)), decay=2.0)
        tone(signal, 0.19, 0.18, 196, harmonics=((1.0, 1.0), (2.0, 0.12)), decay=2.0)
    gain = peak / max(abs(value) for value in signal)
    fade = round(0.004 * RATE)
    samples = []
    for i, value in enumerate(signal):
        edge = min(1.0, i / fade, (len(signal) - 1 - i) / fade)
        samples.append(round(value * gain * math.sin(math.pi / 2 * edge) ** 2 * 32767))
    samples[0] = samples[-1] = 0
    return samples


def wav_bytes(samples):
    buffer = io.BytesIO()
    with wave.open(buffer, "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(struct.pack(f"<{len(samples)}h", *samples))
    return buffer.getvalue()


def meta(name, folder=False):
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "moneytown:original-sfx:v1:" + name).hex
    header = f"fileFormatVersion: 2\nguid: {guid}\n"
    if folder:
        return (header + "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode()
    return (header + """AudioImporter:
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
  forceToMono: 1
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData:
  assetBundleName:
  assetBundleVariant:
""").encode()


def verify_wav(path, data, frames, preview=False):
    with wave.open(io.BytesIO(data), "rb") as source:
        assert (source.getnchannels(), source.getsampwidth(), source.getframerate(), source.getcomptype()) == (1, 2, RATE, "NONE"), path
        assert source.getnframes() == frames, f"Wrong sample count: {path}"
        samples = [sample[0] for sample in struct.iter_unpack("<h", source.readframes(frames))]
    duration = frames / RATE
    peak = max(abs(value) for value in samples) / 32768
    rms = math.sqrt(sum(value * value for value in samples) / frames) / 32768
    assert (4 <= duration <= 8) if preview else (0.1 <= duration <= 0.8), f"Wrong duration: {path}"
    assert samples[0] == samples[-1] == 0, f"Nonzero endpoint: {path}"
    assert 0 < peak <= 0.65 and 0.02 <= rms <= 0.22, f"Unexpected level: {path}: peak={peak}, RMS={rms}"
    print(f"{path.relative_to(ROOT).as_posix()},{frames},{duration:.3f},{peak:.4f},{rms:.4f},{hashlib.sha256(data).hexdigest()}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--check", action="store_true", help="Verify committed bytes and audio metrics without writing files.")
    args = parser.parse_args()
    outputs = {AUDIO.with_suffix(".meta"): meta("Audio", folder=True)}
    frames = {}
    preview = []
    for name in SPECS:
        samples = synthesize(name)
        path = AUDIO / f"{name}.wav"
        outputs[path] = wav_bytes(samples)
        outputs[path.with_suffix(".wav.meta")] = meta(name)
        frames[path] = len(samples)
        if preview:
            preview.extend([0] * round(GAP * RATE))
        preview.extend(samples)
    preview_path = ROOT / "assets/audio/preview.wav"
    outputs[preview_path] = wav_bytes(preview)
    frames[preview_path] = len(preview)
    print("file,samples,seconds,peak,rms,sha256")
    for path, expected in outputs.items():
        if not args.check:
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(expected)
        actual = path.read_bytes()
        if hashlib.sha256(actual).digest() != hashlib.sha256(expected).digest():
            raise SystemExit(f"Generated content differs: {path.relative_to(ROOT)}")
        if path in frames:
            verify_wav(path, actual, frames[path], path == preview_path)
    print(f"PASS: {len(SPECS)} clips, preview and Unity metadata reproduce exactly; PCM format, levels, duration and endpoints verified.")


if __name__ == "__main__":
    main()
