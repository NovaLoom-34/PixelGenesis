from lib import *

# ---------------- SOUNDS (Bölüm 9.1) ----------------
# trigger: key the SoundEventMap / ambience / music look up. synth: procedural placeholder used while files are missing:
#   tone:freq:seconds  noise:seconds:lowpass(0-1)  sweep:from:to:seconds  chord:root:seconds  pulse:freq:seconds:rate
def snd(id, trigger, synth, volume=0.8, pitch=(0.9, 1.1), max_inst=4, cooldown=60, category="sfx", world=True, min_lod=0, max_lod=3, loop=False, files=None, **extra):
    d = dict(id=id, name=id[4:].replace("_", " "), files=files or [], volume=volume, pitch=list(pitch), maxInstances=max_inst, cooldownMs=cooldown,
             category=category, worldSpace=world, minLod=min_lod, maxLod=max_lod, loop=loop, trigger=trigger, synth=synth)
    d.update(extra)
    return d

S = [
    snd("snd.brush", "brush", "noise:0.12:0.3", 0.35, cooldown=90, world=False, max_inst=1),
    snd("snd.power_world", "power.tab.world", "sweep:300:180:0.2", 0.5, world=False),
    snd("snd.power_civ", "power.tab.civ", "tone:660:0.18", 0.5, world=False),
    snd("snd.power_creatures", "power.tab.creatures", "sweep:500:900:0.15", 0.5, world=False),
    snd("snd.power_nature", "power.tab.nature", "noise:0.3:0.15", 0.5, world=False),
    snd("snd.power_destruction", "power.tab.destruction", "sweep:220:60:0.3", 0.6, world=False),
    snd("snd.power_other", "power.tab.other", "tone:880:0.12", 0.45, world=False),
    snd("snd.lightning", "power:pw.lightning", "noise:0.6:0.9", 0.9, max_inst=3),
    snd("snd.earthquake", "power:pw.earthquake", "noise:2.5:0.02", 0.9, max_inst=1, cooldown=1500),
    snd("snd.tornado", "power:pw.tornado", "noise:2.0:0.08", 0.7, max_inst=2, cooldown=1000),
    snd("snd.explosion_small", "explosion.small", "noise:0.35:0.25", 0.7),
    snd("snd.explosion_medium", "explosion.medium", "noise:0.7:0.12", 0.85),
    snd("snd.explosion_large", "explosion.large", "noise:1.4:0.06", 0.95, max_inst=3),
    snd("snd.explosion_huge", "explosion.huge", "noise:3.0:0.03", 1.0, max_inst=2, min_lod=0, max_lod=3),
    snd("snd.splash", "death.drowned", "noise:0.25:0.5", 0.5, max_lod=1),
    snd("snd.hit_melee", "hit.melee", "noise:0.06:0.7", 0.45, max_inst=6, cooldown=40, max_lod=1),
    snd("snd.hit_ranged", "hit.ranged", "sweep:1200:700:0.08", 0.4, max_inst=6, cooldown=40, max_lod=1),
    snd("snd.hit_magic", "hit.magic", "sweep:400:1600:0.2", 0.5, max_inst=4, max_lod=1),
    snd("snd.death_small", "death.small", "sweep:900:500:0.12", 0.4, max_lod=1),
    snd("snd.death_large", "death.large", "sweep:260:120:0.35", 0.55, max_lod=1),
    snd("snd.death_civ", "death.civ", "sweep:420:210:0.3", 0.5, max_lod=1),
    snd("snd.death_monster", "death.monster", "sweep:160:60:0.6", 0.7, max_lod=2),
    snd("snd.death_undead", "death.undead", "sweep:200:90:0.5", 0.55, max_lod=1),
    snd("snd.death_flying", "death.flying", "sweep:1500:800:0.15", 0.4, max_lod=1),
    snd("snd.birth", "birth", "tone:1046:0.1", 0.3, max_inst=2, cooldown=400, max_lod=0),
    snd("snd.building_done", "building.completed", "chord:523:0.4", 0.45, max_inst=2, cooldown=300, max_lod=2),
    snd("snd.building_destroyed", "building.destroyed", "noise:0.6:0.1", 0.6, max_lod=2),
    snd("snd.war_bell", "war.declared", "chord:196:1.5", 0.8, world=False, max_inst=1, cooldown=2000),
    snd("snd.victory_horn", "war.ended", "chord:262:1.2", 0.8, world=False, max_inst=1, cooldown=2000),
    snd("snd.battle_crowd", "crowd", "noise:1.2:0.2", 0.6, max_inst=2, cooldown=800),
    snd("snd.ui_click", "ui.click", "tone:1200:0.03", 0.3, pitch=(1, 1), category="ui", world=False, max_inst=2, cooldown=30),
    snd("snd.ui_open", "ui.open", "sweep:600:900:0.08", 0.35, pitch=(1, 1), category="ui", world=False),
    snd("snd.ui_close", "ui.close", "sweep:900:600:0.08", 0.35, pitch=(1, 1), category="ui", world=False),
    snd("snd.ui_notify", "ui.notify", "tone:988:0.15", 0.4, pitch=(1, 1), category="ui", world=False, max_inst=1, cooldown=250),
    snd("snd.achievement", "achievement", "chord:784:1.0", 0.6, pitch=(1, 1), category="ui", world=False, max_inst=1),
    # ambience layers (looped, mixed by what the camera sees)
    snd("snd.amb_ocean", "ambient.ocean", "noise:4:0.04", 0.45, pitch=(1, 1), category="ambient", world=False, loop=True),
    snd("snd.amb_forest", "ambient.forest", "noise:4:0.35", 0.25, pitch=(1, 1), category="ambient", world=False, loop=True),
    snd("snd.amb_city", "ambient.city", "noise:4:0.12", 0.35, pitch=(1, 1), category="ambient", world=False, loop=True),
    snd("snd.amb_fire", "ambient.fire", "noise:4:0.6", 0.4, pitch=(1, 1), category="ambient", world=False, loop=True),
    snd("snd.amb_rain", "ambient.rain", "noise:4:0.8", 0.35, pitch=(1, 1), category="ambient", world=False, loop=True, clouds=["cloud.rain", "cloud.storm", "cloud.acid"]),
    snd("snd.amb_wind", "ambient.wind", "noise:4:0.02", 0.3, pitch=(1, 1), category="ambient", world=False, loop=True),
]

# music: one piece per era, calm + tense layers (root note per era)
ERAS = [("dawn", 262), ("scorch", 294), ("rain", 220), ("ice", 247), ("shadow", 196), ("blood", 208), ("glimmer", 330), ("ash", 185), ("pale", 233), ("harvest", 277)]
for era, root in ERAS:
    S.append(snd(f"snd.music_{era}_calm", f"music.era.{era}.calm", f"chord:{root}:8", 0.35, pitch=(1, 1), category="music", world=False, loop=True, max_inst=1))
    S.append(snd(f"snd.music_{era}_tense", f"music.era.{era}.tense", f"pulse:{root // 2}:8:2", 0.35, pitch=(1, 1), category="music", world=False, loop=True, max_inst=1))

# Original authored WAV pack; the synth remains an offline/missing-file fallback.
for sound in S:
    sound["files"] = [f"{sound['category']}/{sound['id'][4:]}.wav"]

save("sounds", S, "Sesler", ["id", "trigger", "category", "synth"])
