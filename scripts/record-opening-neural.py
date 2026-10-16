"""Explicitly regenerate the opening voice with the downloaded Supertonic 3 model.

Requires the isolated dependencies and pinned helper/model described in
docs/opening-audio.md. No downloads, paid APIs or voice cloning occur here.
"""
import argparse
import hashlib
import importlib.metadata
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
TEXT = "이건 내가 세계 최고의 부자가 되는 이야기이다"
INPUT_TEXT = "이건 내가, 세계 최고의 부자가 되는 이야기이다."
MODEL_REVISION = "aafc6e32416a594460b32413efc49d7fe4ce6d46"
HELPER_REVISION = "1e9799e964ea4c0dad7cde993b65c3c813a7b373"
PRESET = "M2"
SEED = 20261016


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--tool-dir", type=Path, default=ROOT / "build/voice-tools")
    args = parser.parse_args()
    tool = args.tool_dir.resolve()
    sys.path.insert(0, str(tool))
    import numpy as np
    import soundfile as sf
    from helper import load_text_to_speech, load_voice_style

    model = tool / "model"
    files = [tool / "helper.py", model / "voice_styles" / f"{PRESET}.json"]
    files += sorted((model / "onnx").glob("*"))
    provenance = {str(p.relative_to(tool)).replace("\\", "/"):
                  hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
    tts = load_text_to_speech(str(model / "onnx"), False)
    style = load_voice_style([str(model / "voice_styles" / f"{PRESET}.json")])
    np.random.seed(SEED)
    wav, duration = tts(INPUT_TEXT, "ko", style, 12, .9)
    samples = wav[0, :int(tts.sample_rate * duration[0].item())]
    if tts.sample_rate != 44100 or not 3 < len(samples) / 44100 < 8:
        raise ValueError("Unexpected voice format or duration; recheck timing before replacing it.")
    source = ROOT / "assets/audio/opening-narration-source.wav"
    sf.write(str(source), samples, 44100, subtype="PCM_16")
    manifest = {
        "text": TEXT,
        "voice": {"engine": "Supertonic 3", "preset": PRESET, "language": "ko",
                  "input_text": INPUT_TEXT, "speed": .9, "total_steps": 12, "seed": SEED},
        "source": {"file": source.relative_to(ROOT).as_posix(),
                   "sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                   "sample_rate": 44100, "channels": 1, "sample_width_bits": 16,
                   "frames": len(samples), "duration_seconds": len(samples) / 44100},
        "cues": [{"text": text, "start_seconds": start} for text, start in
                 zip(("이건 내가", "세계 최고의 부자가", "되는 이야기이다"), (0, 1.1, 2.7))],
        "generation": {
            "model": "supertone-oss-archive/supertonic-3", "model_revision": MODEL_REVISION,
            "helper_revision": HELPER_REVISION, "files_sha256": provenance,
            "packages": {name: importlib.metadata.version(name) for name in
                         ("numpy", "onnxruntime", "soundfile")},
            "cue_method": "Approximate word starts from local faster-whisper small; not phoneme alignment.",
            "reference_verification": {
                "source_sha256": "9a16422d3f1a6da170d3ff19fa2c464440a9c567da6794ba5d8196a2929daaf2",
                "asr_model": "Systran/faster-whisper-small", "transcript": TEXT + ".",
                "note": "Verified selection only. Regeneration does not rerun ASR."
            },
            "disclosure": "AI generated narration; original preset, no voice cloning."
        }
    }
    source.with_suffix(".json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n",
                                         encoding="utf-8")
    print(json.dumps(manifest["source"], ensure_ascii=False))


if __name__ == "__main__":
    main()
