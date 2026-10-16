"""Create MoneyTown's free local Korean opening voice and original sound bed.

Python stdlib only. Windows System.Speech / Microsoft Heami Desktop records the
source once. --check never invokes speech synthesis and never changes files.
"""
import argparse
from array import array
import base64
import hashlib
import io
import json
import math
import os
from pathlib import Path
import random
import subprocess
import sys
import wave

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets/audio/opening-narration-source.wav"
MANIFEST = SOURCE.with_suffix(".json")
OUTPUT = ROOT / "unity/Assets/Resources/Audio/story-opening.wav"
RATE = 44100
SOURCE_RATE = 16000
VOICE = "Microsoft Heami Desktop"
TEXT = "이건 내가 세계 최고의 부자가 되는 이야기이다"
PHRASES = ("이건 내가", "세계 최고의 부자가", "되는 이야기이다")
SSML = ('<speak version="1.0" xmlns="http://www.w3.org/2001/10/synthesis" '
        'xml:lang="ko-KR"><mark name="line-1"/>이건 내가'
        '<break time="180ms"/><mark name="line-2"/>세계 최고의 부자가'
        '<break time="140ms"/><mark name="line-3"/>되는 이야기이다</speak>')
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


def record_source():
    if os.name != "nt":
        raise RuntimeError("Source recording requires Windows and Microsoft Heami Desktop.")
    # EncodedCommand carries script code; paths and SSML travel as environment
    # data, never interpolated into shell commands. No network or paid service.
    command = r'''
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
Add-Type -AssemblyName System.Speech
$code = @'
using System;
using System.Collections.Generic;
using System.Speech.Synthesis;
using System.Speech.AudioFormat;
public class OpeningBookmark {
    public string Name;
    public double Seconds;
}
public class OpeningRecording {
    public string VoiceName;
    public string Culture;
    public string Gender;
    public string Age;
    public int Rate;
    public List<OpeningBookmark> Bookmarks = new List<OpeningBookmark>();
    public static OpeningRecording Capture(string path, string ssml, string voice) {
        var result = new OpeningRecording();
        using (var synth = new SpeechSynthesizer()) {
            synth.SelectVoice(voice);
            if (synth.Voice.Culture.Name != "ko-KR")
                throw new InvalidOperationException("Korean voice required.");
            synth.Rate = -1;
            synth.Volume = 100;
            result.VoiceName = synth.Voice.Name;
            result.Culture = synth.Voice.Culture.Name;
            result.Gender = synth.Voice.Gender.ToString();
            result.Age = synth.Voice.Age.ToString();
            result.Rate = synth.Rate;
            synth.BookmarkReached += (sender, item) => result.Bookmarks.Add(
                new OpeningBookmark { Name = item.Bookmark, Seconds = item.AudioPosition.TotalSeconds });
            synth.SetOutputToWaveFile(path, new SpeechAudioFormatInfo(
                16000, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
            synth.SpeakSsml(ssml);
            synth.SetOutputToNull();
        }
        return result;
    }
}
'@
Add-Type -TypeDefinition $code -ReferencedAssemblies ([System.Speech.Synthesis.SpeechSynthesizer].Assembly.Location)
[OpeningRecording]::Capture($env:MONEYTOWN_OPENING_SOURCE, $env:MONEYTOWN_OPENING_SSML, $env:MONEYTOWN_OPENING_VOICE) | ConvertTo-Json -Depth 5
'''
    SOURCE.parent.mkdir(parents=True, exist_ok=True)
    environment = os.environ.copy()
    environment.update(MONEYTOWN_OPENING_SOURCE=str(SOURCE),
                       MONEYTOWN_OPENING_SSML=SSML, MONEYTOWN_OPENING_VOICE=VOICE)
    powershell = Path(os.environ["SystemRoot"]) / "System32/WindowsPowerShell/v1.0/powershell.exe"
    process = subprocess.run([str(powershell), "-NoProfile", "-NonInteractive", "-EncodedCommand",
                              base64.b64encode(command.encode("utf-16le")).decode("ascii")],
                             env=environment, capture_output=True, timeout=60)
    if process.returncode:
        raise RuntimeError("Local SAPI recording failed: " + process.stderr.decode("utf-8", errors="replace"))
    info = json.loads(process.stdout.decode("utf-8-sig"))
    with wave.open(str(SOURCE), "rb") as reader:
        frames = reader.getnframes()
    return {"text": TEXT, "ssml": SSML, "voice": info,
            "source": {"file": SOURCE.relative_to(ROOT).as_posix(), "sha256": sha(SOURCE.read_bytes()),
                       "sample_rate": SOURCE_RATE, "channels": 1, "sample_width_bits": 16,
                       "frames": frames, "duration_seconds": frames / SOURCE_RATE}}


def read_source(manifest):
    raw = SOURCE.read_bytes()
    if sha(raw) != manifest["source"]["sha256"]:
        raise ValueError("Source WAV differs from the recorded manifest.")
    if manifest["text"] != TEXT or manifest["ssml"] != SSML:
        raise ValueError("Source text or SSML does not match this generator.")
    if manifest["voice"]["VoiceName"] != VOICE or manifest["voice"]["Culture"] != "ko-KR":
        raise ValueError("Unexpected source voice.")
    with wave.open(io.BytesIO(raw), "rb") as reader:
        if (reader.getnchannels(), reader.getsampwidth(), reader.getframerate(), reader.getcomptype()) != (1, 2, SOURCE_RATE, "NONE"):
            raise ValueError("Source must be mono 16 kHz 16-bit PCM.")
        samples = array("h", reader.readframes(reader.getnframes()))
    if sys.byteorder != "little":
        samples.byteswap()
    expected_source = {"file": SOURCE.relative_to(ROOT).as_posix(), "sha256": sha(raw),
                       "sample_rate": SOURCE_RATE, "channels": 1, "sample_width_bits": 16,
                       "frames": len(samples), "duration_seconds": len(samples) / SOURCE_RATE}
    if manifest["source"] != expected_source:
        raise ValueError("Source format or duration metadata differs from its WAV.")
    if not samples or max(abs(x) for x in samples) < 100:
        raise ValueError("Source voice is empty or effectively silent.")
    marks = manifest["voice"]["Bookmarks"]
    if [mark["Name"] for mark in marks] != ["line-1", "line-2", "line-3"]:
        raise ValueError("Three ordered speech bookmarks are required.")
    times = [float(mark["Seconds"]) for mark in marks]
    if not 0 <= times[0] < times[1] < times[2] < len(samples) / SOURCE_RATE:
        raise ValueError("Speech bookmark times are outside the source.")
    return samples, times


def synthesize(source, times):
    # SAPI bookmarks stay correct at Heami's native 16 kHz. Resample only after
    # recording; asking SAPI for 44.1 kHz gives rate-scaled bookmark timestamps.
    resampled = []
    for i in range(round(len(source) * RATE / SOURCE_RATE)):
        position = i * SOURCE_RATE / RATE
        left = min(int(position), len(source) - 1)
        right = min(left + 1, len(source) - 1)
        resampled.append(source[left] + (source[right] - source[left]) * (position - left))
    source = resampled
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
            lambda t, f=frequency: .018 * math.exp(-4.8 * t)
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
    parser.add_argument("--record-source", action="store_true", help="Explicitly record a new source using installed Windows Heami.")
    args = parser.parse_args()
    if args.check and args.record_source:
        parser.error("--check cannot record a source")
    if args.record_source or (not args.check and not SOURCE.exists() and not MANIFEST.exists()):
        manifest = record_source()
    else:
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
        print("Opening audio check passed: source SHA, exact mix, bookmarks, PCM format, level, edges, Unity meta.")
    else:
        OUTPUT.parent.mkdir(parents=True, exist_ok=True)
        OUTPUT.write_bytes(data)
        meta_path.write_text(META, encoding="utf-8", newline="\n")
        manifest["mix"] = result
        MANIFEST.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
        print("Opening audio written using free local SAPI and original synthesized effects.")
    print(json.dumps(result, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, RuntimeError, KeyError, subprocess.TimeoutExpired) as error:
        print(str(error), file=sys.stderr)
        sys.exit(1)
