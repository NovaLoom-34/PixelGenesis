"""Original PixelGenesis sound design. Requires numpy; no external samples.

Run from any directory. Outputs PCM WAVs to Audio/, plus an audition reel.
All random streams are offline presentation data; simulation RNG is untouched.
"""
from pathlib import Path
import hashlib
import json
import wave

import numpy as np

ROOT = Path(__file__).resolve().parents[1]
SR = 22050
OUT = ROOT / "Audio"
TAU = 2 * np.pi


def clock(seconds):
    return np.arange(round(seconds * SR)) / SR


def rng_for(name):
    return np.random.default_rng(int.from_bytes(hashlib.sha256(name.encode()).digest()[:8], "little"))


def noise(n, rng, low=60, high=6000):
    # A periodic filtered noise bed: FFT avoids a filter startup transient at the seam.
    f = np.fft.rfftfreq(n, 1 / SR)
    spectrum = np.fft.rfft(rng.standard_normal(n))
    mask = (1 - np.exp(-(f / low) ** 4)) * np.exp(-(f / high) ** 2)
    result = np.fft.irfft(spectrum * mask, n=n)
    return result / max(np.std(result), 1e-9)


def note(freq, seconds, kind="harp"):
    t = clock(seconds)
    attack = 1 - np.exp(-t / 0.008)
    if kind == "bell":
        y = sum(a * np.sin(TAU * freq * h * t) * np.exp(-t / decay)
                for h, a, decay in [(1, 1, .65), (2.01, .32, .28), (3.98, .1, .12)])
    elif kind == "wood":
        y = np.sin(TAU * freq * t) * np.exp(-t / .06)
        y += .28 * np.sin(TAU * freq * 2.71 * t) * np.exp(-t / .028)
    elif kind == "horn":
        y = sum(np.sin(TAU * freq * h * t) / h**1.65 for h in range(1, 7))
        y *= np.sin(np.pi * np.minimum(t / seconds, 1)) ** .7
    elif kind == "pad":
        y = sum(np.sin(TAU * freq * h * t + .08 * np.sin(TAU * .2 * t)) / h**2
                for h in range(1, 5))
        y *= np.sin(np.pi * t / seconds) ** 1.6
    else:
        y = sum(np.sin(TAU * freq * h * t) * np.exp(-t * (1.5 + h * .55)) / h**1.7
                for h in range(1, 6))
    y *= attack
    # Every finite event has a clean tail before any reverb is applied.
    fade = min(len(y), int(.035 * SR))
    y[-fade:] *= np.linspace(1, 0, fade)
    return y


def add(dst, event, when, gain=1, wrap=False):
    start = round(when * SR)
    if wrap:
        indices = (np.arange(len(event)) + start) % len(dst)
        np.add.at(dst, indices, event * gain)
    elif start < len(dst):
        count = min(len(event), len(dst) - start)
        dst[start:start + count] += event[:count] * gain


def space(y, loop=False):
    dry = y.copy()
    for delay, gain in [(.117, .16), (.241, .10), (.389, .06), (.617, .035)]:
        shift = round(delay * SR)
        if loop:
            y += np.roll(dry, shift) * gain
        elif shift < len(y):
            y[shift:] += dry[:-shift] * gain
    return y


def finish(y, rms=.13, loop=False):
    y = y - y.mean()
    y *= min(rms / max(np.sqrt(np.mean(y * y)), 1e-9), .72 / max(np.max(np.abs(y)), 1e-9))
    if not loop:
        count = min(len(y) // 4, round(.008 * SR))
        y[:count] *= np.linspace(0, 1, count)
        y[-count:] *= np.linspace(1, 0, count)
    return y


def effect(name):
    rng = rng_for(name)
    # Recognizable short musical gestures with restrained high frequencies.
    gestures = {
        "ui_click": ([700], .19, "wood"),
        "ui_open": ([440, 660], .52, "harp"),
        "ui_close": ([660, 440], .46, "harp"),
        "ui_notify": ([660, 880], .85, "bell"),
        "achievement": ([392, 494, 587, 784], 2.3, "bell"),
        "building_done": ([392, 494, 587], 1.35, "harp"),
        "birth": ([784, 988], .42, "harp"),
        "power_civ": ([330, 440, 554], .9, "wood"),
        "power_creatures": ([587, 784], .7, "harp"),
        "power_other": ([523, 784, 1046], 1.05, "bell"),
        "war_bell": ([147, 220, 147], 3.0, "bell"),
        "victory_horn": ([262, 330, 392, 523], 2.7, "horn"),
        "hit_magic": ([440, 660, 880], .75, "bell"),
    }
    if name in gestures:
        tones, seconds, kind = gestures[name]
        y = np.zeros(round(seconds * SR))
        for i, freq in enumerate(tones):
            add(y, note(freq, min(1.5, seconds * .7), kind), i * seconds * .12, 1 / (1 + i * .08))
        return finish(space(y), .12 if name.startswith("ui") else .16)

    seconds = {"brush": .22, "power_world": .65, "power_nature": .85,
               "power_destruction": 1.1, "lightning": 2.8, "earthquake": 3.8,
               "tornado": 3.6, "explosion_small": .65, "explosion_medium": 1.15,
               "explosion_large": 2.1, "explosion_huge": 3.6, "splash": .8,
               "hit_melee": .22, "hit_ranged": .3, "building_destroyed": 1.55,
               "battle_crowd": 2.4}.get(name, .7)
    t = clock(seconds)
    n = len(t)
    y = np.zeros(n)
    if name.startswith("death_"):
        base = {"small": 540, "large": 135, "civ": 250, "monster": 83, "undead": 110, "flying": 1150}[name[6:]]
        phase = TAU * base * (t - .32 * t * t / seconds)
        y = (.7 * np.sin(phase) + .15 * np.sin(2.15 * phase) + .2 * noise(n, rng, 100, 1600))
        y *= np.sin(np.pi * t / seconds) ** 2 * np.exp(-3 * t)
    elif name in ("splash", "power_nature"):
        y = noise(n, rng, 180, 4200) * np.exp(-4 * t) * (1 - np.exp(-60 * t)) * .35
        for when in rng.uniform(.02, seconds * .7, 12):
            f = rng.uniform(360, 1400)
            drop_t = clock(.10)
            drop = np.sin(TAU * f * (drop_t + 2 * drop_t**2)) * np.exp(-45 * drop_t)
            add(y, drop, when, .13)
    elif name == "brush":
        y = noise(n, rng, 220, 2800) * np.sin(np.pi * t / seconds) ** 2
    elif name == "hit_melee":
        y = noise(n, rng, 500, 3800) * np.exp(-55 * t) * .5
        y += np.sin(TAU * 175 * t) * np.exp(-35 * t)
    elif name == "hit_ranged":
        y = noise(n, rng, 900, 4000) * np.sin(np.pi * t / seconds) ** 2
    elif name == "tornado":
        y = noise(n, rng, 75, 1100) * (.65 + .35 * np.sin(TAU * 3 * t)) * np.sin(np.pi * t / seconds)
    else:
        # Low rounded impact, air transient, and discrete debris; never a clipped noise burst.
        weight = {"explosion_small": 120, "explosion_medium": 86, "explosion_large": 62,
                  "explosion_huge": 43, "earthquake": 38, "lightning": 70}.get(name, 130)
        y = noise(n, rng, 30, 650) * np.exp(-4 * t / seconds) * .8
        y += .6 * np.sin(TAU * weight * (.7 * t + .3 * (.12 * (1 - np.exp(-t / .12))))) * np.exp(-7 * t / seconds)
        y += .16 * noise(n, rng, 900, 4700) * np.exp(-t / .035)
        for when in rng.uniform(.05, seconds * .85, 14 if name == "battle_crowd" else 7):
            add(y, note(rng.uniform(180, 450), .13, "wood"), when, .15)
        if name in ("earthquake", "battle_crowd", "power_world"):
            y *= np.sin(np.pi * t / seconds)
    return finish(space(y), .15)


def ambience(name):
    rng = rng_for(name)
    t = clock(16)
    n = len(t)
    if name == "ocean":
        swell = .32 + .68 * (.5 + .5 * np.sin(TAU * t / 8)) ** 2
        y = noise(n, rng, 65, 1500) * swell
        y += noise(n, rng, 1800, 4500) * (.5 + .5 * np.sin(TAU * t / 8 - .8)) ** 6 * .09
    elif name == "forest":
        y = .075 * noise(n, rng, 250, 2800)
        for when in [1.1, 1.38, 4.8, 5.15, 8.3, 8.51, 12.1, 12.43, 14.5]:
            ct = clock(.24)
            f = rng.uniform(1750, 2750)
            chirp = np.sin(TAU * (f * ct + 600 * ct**2 + 20 * np.sin(TAU * 11 * ct)))
            add(y, chirp * np.sin(np.pi * ct / .24) ** 3, when, .24, True)
    elif name == "city":
        y = .08 * noise(n, rng, 180, 900)
        for when in rng.uniform(0, 16, 28):
            add(y, note(rng.uniform(180, 550), .22, "wood"), when, .17, True)
        for when in [3.2, 10.8]:
            add(y, note(660, 1.5, "bell"), when, .08, True)
    elif name == "fire":
        y = .17 * noise(n, rng, 60, 900)
        for when in rng.uniform(0, 16, 100):
            ct = clock(rng.uniform(.012, .065))
            crack = rng.standard_normal(len(ct)) * np.exp(-ct * 100)
            crack *= np.minimum(ct / .001, 1)
            add(y, crack, when, rng.uniform(.03, .16), True)
    elif name == "rain":
        y = .4 * noise(n, rng, 450, 6200) + .1 * noise(n, rng, 100, 1000)
        for when in rng.uniform(0, 16, 75):
            add(y, note(rng.uniform(600, 1400), .055, "wood"), when, .1, True)
    else:
        y = noise(n, rng, 45, 650) * (.6 + .25 * np.sin(TAU * t / 16) + .15 * np.sin(TAU * t / 4))
    return finish(space(y, True), .10 if name in ("forest", "city") else .14, True)


def music(era, root, tense):
    beat = 60 / 72
    seconds = 32 * beat
    y = np.zeros(round(seconds * SR))
    dark = era in ("shadow", "blood", "ash", "pale")
    third = 3 if dark else 4
    progression = [0, 8, 3, 10] if dark else [0, 5, 9, 7]
    melody = [0, 7, 12, 7, third, 7, 14, 12]
    era_index = ["dawn", "scorch", "rain", "ice", "shadow", "blood", "glimmer", "ash", "pale", "harvest"].index(era)
    for bar in range(8):
        offset = progression[bar % 4]
        chord_third = 3 if (dark and offset == 0) or (not dark and offset == 9) else 4
        base = root * 2 ** (offset / 12)
        when = bar * 4 * beat
        if not tense:
            # Slow bowed harmonics under an original, spacious eight-bar mallet motif.
            for interval in [0, chord_third, 7]:
                add(y, note(base * 2 ** (interval / 12) / 2, 5 * beat, "pad"), when - .5 * beat, .15, True)
            for k in range(3):
                interval = melody[(bar + k + era_index) % len(melody)]
                if interval == third:
                    interval = chord_third
                add(y, note(base * 2 ** (interval / 12), 2.6 * beat, "harp"), when + (k * 1.25 + .15) * beat, .27, True)
            if bar % 2 == 0:
                add(y, note(base * 2, 2.2, "bell"), when + 3 * beat, .055, True)
        else:
            # Same harmony and length as the calm stem: blends without drifting out of key.
            for pulse in range(8):
                f = base / 4 if pulse % 2 == 0 else base * 1.5 / 4
                add(y, note(f, .65, "wood"), when + pulse * .5 * beat, .32 if pulse % 2 == 0 else .16, True)
            for k in range(4):
                add(y, note(base * 2 ** ([0, 7, chord_third, 7][k] / 12), 1.1, "harp"), when + k * beat, .12, True)
    return finish(space(y, True), .13, True)


def write(path, y):
    path.parent.mkdir(parents=True, exist_ok=True)
    pcm = np.round(np.clip(y, -.999, .999) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(SR)
        out.writeframes(pcm.tobytes())


def main():
    sounds = json.loads((ROOT / "Data/sounds.json").read_text(encoding="utf-8"))["items"]
    roots = dict(zip(["dawn", "scorch", "rain", "ice", "shadow", "blood", "glimmer", "ash", "pale", "harvest"],
                     [262, 294, 220, 247, 196, 208, 330, 185, 233, 277]))
    report = []
    clips = {}
    for sound in sounds:
        name = sound["id"][4:]
        if name.startswith("amb_"):
            y = ambience(name[4:])
        elif name.startswith("music_"):
            _, era, layer = name.split("_")
            y = music(era, roots[era], layer == "tense")
        else:
            y = effect(name)
        relative = Path(sound["category"]) / (name + ".wav")
        write(OUT / relative, y)
        clips[name] = y
        report.append(dict(id=sound["id"], file=relative.as_posix(), seconds=round(len(y) / SR, 3),
                           peak=round(float(np.max(np.abs(y))), 4), rms=round(float(np.sqrt(np.mean(y*y))), 4),
                           seam=round(float(abs(y[-1] - y[0])), 4)))
    # Deliberately short montage; it is a preview, not an additional runtime asset.
    parts = []
    for name, duration in [("music_dawn_calm", 8), ("amb_forest", 5), ("amb_ocean", 5),
                           ("ui_click", 1), ("ui_open", 1), ("building_done", 2),
                           ("lightning", 3), ("victory_horn", 3), ("music_glimmer_calm", 7)]:
        segment = clips[name][:round(duration * SR)].copy()
        fade = min(len(segment) // 4, round(.2 * SR))
        segment[:fade] *= np.linspace(0, 1, fade)
        segment[-fade:] *= np.linspace(1, 0, fade)
        parts.extend([segment, np.zeros(round(.35 * SR))])
    write(ROOT / "Logs/audio-preview.wav", np.concatenate(parts))
    (OUT / "manifest.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Generated {len(report)} original clips; {sum(p.stat().st_size for p in OUT.rglob('*.wav')) / 1048576:.1f} MiB")


if __name__ == "__main__":
    main()
