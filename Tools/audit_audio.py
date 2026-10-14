"""Validate actual PCM files, content references, loop seams and paired music timing."""
import json
import re
import wave
from pathlib import Path
import numpy as np

ROOT = Path(__file__).resolve().parents[1]
TRIGGER_LITERAL = re.compile(
    r'"((?:explosion|death|hit|birth|building|war|crowd|brush|ui\.|power(?:\.tab\.|:)|ambient\.|music\.era\.)[^"]*)"'
)
DYNAMIC_PREFIXES = {"death", "hit", "power:", "power.tab.", "ambient.", "music.era."}


def audit():
    catalog = json.loads((ROOT / "Data/sounds.json").read_text(encoding="utf-8"))
    sounds = catalog["items"]
    assert catalog.get("count") == len(sounds), "Sound catalog count does not match its items"
    ids = [sound["id"] for sound in sounds]
    assert len(ids) == len(set(ids)), "Duplicate sound id"
    triggers = {sound.get("trigger") for sound in sounds if sound.get("trigger")}
    assert len(triggers) > 0, "Sound catalog has no trigger keys"

    missing = set()
    audio_manager = ROOT / "Assets/_Project/Render/AudioManager.cs"
    for path in (ROOT / "Assets/_Project").rglob("*.cs"):
        if "/Tests/" in path.as_posix():
            continue
        lines = path.read_text(encoding="utf-8-sig").splitlines()
        if path == audio_manager:
            relevant_lines = lines
        else:
            relevant_lines = [line for line in lines if re.search(r"Audio\??\.Play(?:Power)?\s*\(", line)]
        for trigger in (key for line in relevant_lines for key in TRIGGER_LITERAL.findall(line)):
            if trigger not in DYNAMIC_PREFIXES and trigger not in triggers:
                missing.add((path.relative_to(ROOT).as_posix(), trigger))

    audio_source = audio_manager.read_text(encoding="utf-8-sig")
    ambient_match = re.search(r"AmbientKeys\s*=\s*\{([^}]+)\}", audio_source)
    assert ambient_match, "AudioManager ambient keys could not be read"
    ambient_keys = re.findall(r'"([^"]+)"', ambient_match.group(1))
    for key in ambient_keys:
        if f"ambient.{key}" not in triggers:
            missing.add(("AudioManager.AmbientKeys", f"ambient.{key}"))

    eras = json.loads((ROOT / "Data/eras.json").read_text(encoding="utf-8"))["items"]
    for era in eras:
        key = era["id"].split(".", 1)[-1]
        for mood in ("calm", "tense"):
            trigger = f"music.era.{key}.{mood}"
            if trigger not in triggers:
                missing.add(("Data/eras.json", trigger))

    powers = json.loads((ROOT / "Data/powers.json").read_text(encoding="utf-8"))["items"]
    for power in powers:
        if f"power:{power['id']}" in triggers or power.get("type") == "brush":
            continue
        if f"power.tab.{power.get('tab', 'other')}" not in triggers:
            missing.add(("Data/powers.json", f"power:{power['id']} (and no tab fallback)"))

    assert not missing, "Unbound runtime sound triggers: " + "; ".join(f"{path}: {key}" for path, key in sorted(missing))

    measurements = {}
    for sound in sounds:
        assert sound["files"], f"Missing asset binding: {sound['id']}"
        for file in sound["files"]:
            path = ROOT / "Audio" / file
            assert path.is_file(), path
            with wave.open(str(path), "rb") as wav:
                assert (wav.getframerate(), wav.getnchannels(), wav.getsampwidth()) == (22050, 1, 2), path
                frames = wav.getnframes()
                data = np.frombuffer(wav.readframes(frames), dtype="<i2").astype(float) / 32768
            assert len(data) == frames and frames > 1000, path
            peak = np.max(np.abs(data))
            rms = np.sqrt(np.mean(data * data))
            assert .01 < rms < .2, (path, "silent/too loud", rms)
            assert peak < .73, (path, "insufficient headroom", peak)
            assert abs(data.mean()) < .003, (path, "DC offset")
            if sound["loop"]:
                assert frames >= 16 * 22050, (path, "short loop")
                # A seam must be comparable to normal adjacent sample changes, not a discontinuity.
                limit = max(.025, float(np.quantile(np.abs(np.diff(data)), .999)) * 1.5)
                assert abs(data[-1] - data[0]) <= limit, (path, "loop seam", abs(data[-1] - data[0]), limit)
            else:
                assert abs(data[0]) < .001 and abs(data[-1]) < .001, (path, "click at endpoint")
            measurements[sound["id"]] = frames
    for key, frames in measurements.items():
        if key.endswith("_calm"):
            assert measurements[key.replace("_calm", "_tense")] == frames, key
    print(f"PASS: {len(measurements)} WAVs; {len(triggers)} sound triggers cover source calls, {len(ambient_keys)} ambience layers, {len(eras)} era pairs and all power fallbacks; PCM, headroom, loop seams and music pairs are valid.")


if __name__ == "__main__":
    audit()
