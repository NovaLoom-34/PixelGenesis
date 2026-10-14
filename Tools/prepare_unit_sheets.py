"""Build runtime-ready atlases from generated character sheets.

Requires Pillow, NumPy, SciPy. Alpha components avoid assuming a perfectly uniform
generated grid. High-resolution source PNGs remain the source of truth; runtime
atlases are only 160x240 pixels and can be regenerated at any time.
"""
from pathlib import Path
import json
import base64
import numpy as np
from PIL import Image
from scipy.ndimage import label, find_objects

ROOT = Path(__file__).resolve().parents[1]
TARGET_HEIGHT = dict(human=28, orc=30, dwarf=23, elf=30, sheep=18, wolf=19, bear=23, deer=23,
                     dragon=28, slime=16, skeleton=26, fairy=26, zombie=26, fire_elemental=28,
                     ember_imp=22, sandworm=24, walking_tree=28, ghost=23, water_elemental=27, earth_golem=28,
                     kraken=24, rot_crawler=19, candy_golem=27, gummy_bear=20,
                     shroomling=19, crystal_beetle=21, ash_crawler=21, bone_crawler=20,
                     clock_crab=21, void_moth=21, flesh_mound=27, devourer=28,
                     zombie_runner=26, zombie_brute=28, zombie_bloater=27, zombie_crawler=19,
                     zombie_beast=25, zombie_dragon=28, zombie_lord=28, necromancer=27,
                     bloodsucker=25, sky_whale_calf=22, dark_mage=27, white_mage=26,
                     druid=26, snowman=23, storm_spirit=24,
                     cat=18, dog=19, rabbit=17, cow=24,
                     chicken=17, rat=15, monkey=19, fox=20,
                     boar=21, goat=20, horse_wild=25, reindeer=23,
                     penguin=19, crab=18, scorpion=20, lizard=18,
                     crocodile=24, snake=19, turtle=19, seal=20,
                     hyena=21, lion=23, rhino=26, buffalo=25,
                     camel=24, polar_bear=24, salamander=18,
                     frog=18, bee=17, ant=16, beetle=17,
                     owl=20, crow=19, eagle=23, parrot=21,
                     snail=17, fish=19, butterfly=18, caterpillar=17,
                     piranha=19, mosquito_swarm=21, sky_visitor=24, colossus=30,
                     bandit=25, cultist=26)


def prepare(name, target_height):
    path = ROOT / 'Art/Final/units' / (name + '_detail.png')
    rgba = np.array(Image.open(path).convert('RGBA'))
    labels, _ = label(rgba[:, :, 3] >= 128, structure=np.ones((3, 3)))
    pieces = []
    if name == 'mosquito_swarm':
        # A swarm is intentionally many disconnected insects. Crop each authored
        # grid cell as one pose so the smaller insects are not discarded.
        assert rgba.shape[:2] == (1536, 1024)
        for row in range(6):
            for col in range(4):
                cell = rgba[row*256:(row+1)*256, col*256:(col+1)*256, 3] >= 128
                ys, xs = np.where(cell)
                assert len(xs) >= 500, f'{name}: empty pose at {row}, {col}'
                pieces.append(dict(x=col*256+int(xs.min()), y=row*256+int(ys.min()),
                                   width=int(xs.max()-xs.min()+1), height=int(ys.max()-ys.min()+1),
                                   area=len(xs), label=0))
        ordered = pieces
    else:
        for index, bounds in enumerate(find_objects(labels)):
            area = int(np.count_nonzero(labels[bounds] == index + 1))
            if area < 500:
                continue
            y, x = bounds
            pieces.append(dict(x=x.start, y=y.start, width=x.stop-x.start, height=y.stop-y.start, area=area, label=index+1))
        if name in {'slime', 'water_elemental', 'shroomling', 'crystal_beetle', 'ash_crawler', 'bone_crawler', 'clock_crab', 'void_moth', 'zombie_runner', 'zombie_bloater', 'necromancer', 'sky_whale_calf', 'dark_mage', 'white_mage', 'druid', 'snowman', 'storm_spirit', 'chicken', 'fox', 'boar', 'goat', 'reindeer', 'crab'} and len(pieces) > 24:
            # Detached bubbles, sparks and fragments are smaller than every main body.
            pieces = sorted(pieces, key=lambda f: f['area'], reverse=True)[:24]
        assert len(pieces) == 24, f'{name}: expected 24 connected poses, found {len(pieces)}'
        pieces.sort(key=lambda f: f['y'] + f['height'] / 2)
        ordered = []
        for row in range(6):
            ordered.extend(sorted(pieces[row*4:row*4+4], key=lambda f: f['x']))
    reference_height = float(np.median([f['height'] for f in ordered[:4]]))
    scale = min(target_height/reference_height, 36/max(f['width'] for f in ordered), 34/max(f['height'] for f in ordered))
    frames = []
    for f in ordered:
        frame = {k: f[k] for k in ('x', 'y', 'width', 'height')}
        w, h = max(1, int(np.floor(f['width']*scale + 0.5))), max(1, int(np.floor(f['height']*scale + 0.5)))
        xs = f['x'] + np.minimum(f['width']-1, ((np.arange(w)+.5)*f['width']/w).astype(int))
        ys = f['y'] + np.minimum(f['height']-1, ((np.arange(h)+.5)*f['height']/h).astype(int))
        mask = ((rgba[np.ix_(ys, xs)][:, :, 3] >= 128) if name == 'mosquito_swarm'
                else (labels[np.ix_(ys, xs)] == f['label'])).astype(np.uint8)
        frame['mask'] = base64.b64encode(mask.tobytes()).decode('ascii')
        frames.append(frame)
    full_color = name.replace('_female', '') not in {'human', 'orc', 'dwarf', 'elf'}
    data = dict(version=1, frameSize=40, baseline=36, worldSize=2.0, scale=scale, fullColor=full_color, frames=frames)
    path.with_suffix('.json').write_text(json.dumps(data, indent=2)+'\n', encoding='utf-8')
    # Bake the exact nearest-neighbour/mask conversion used by DetailedUnitSheet.Build.
    # Unity uploads this tiny atlas directly, avoiding a 2K source texture per unit
    # plus the CPU-side resampling pass during startup.
    atlas = np.zeros((240, 160, 4), dtype=np.uint8)
    for index, f in enumerate(frames):
        w = max(1, int(np.floor(f['width'] * scale + 0.5)))
        h = max(1, int(np.floor(f['height'] * scale + 0.5)))
        xs = f['x'] + np.minimum(f['width'] - 1, ((np.arange(w) + .5) * f['width'] / w).astype(int))
        ys = f['y'] + np.minimum(f['height'] - 1, ((np.arange(h) + .5) * f['height'] / h).astype(int))
        sampled = rgba[np.ix_(ys, xs)]
        mask = np.frombuffer(base64.b64decode(f['mask']), dtype=np.uint8).reshape(h, w) != 0
        visible = mask & (sampled[:, :, 3] >= 128)
        if full_color:
            pixels = sampled.copy()
            pixels[:, :, 3] = 255
        else:
            shade = np.clip(np.floor(sampled[:, :, :3].astype(np.float32).sum(axis=2) / 84.0 + 0.5), 1, 8) * 28
            pixels = np.stack((shade, shade, shade, np.full((h, w), 255)), axis=2).astype(np.uint8)
        left = index % 4 * 40 + (40 - w) // 2
        top = index // 4 * 40 + 36 - h
        target = atlas[top:top+h, left:left+w]
        target[visible] = pixels[visible]
    runtime_path = path.with_name(name + '_detail_runtime.png')
    Image.fromarray(atlas).save(runtime_path, optimize=True)
    runtime_meta = dict(version=1, worldSize=data['worldSize'], footOffset=data['worldSize'] * (data['frameSize'] - data['baseline']) / data['frameSize'], fullColor=full_color)
    path.with_name(name + '_detail_runtime.json').write_text(json.dumps(runtime_meta, indent=2)+'\n', encoding='utf-8')
    print(f'{name}: 24 poses, standing height {reference_height*scale:.1f}px; runtime sheet 160x240')


if __name__ == '__main__':
    import sys
    selected = sys.argv[1:] or TARGET_HEIGHT.keys()
    for species in selected:
        height = TARGET_HEIGHT[species]
        prepare(species, height)
        if (ROOT / 'Art/Final/units' / (species + '_female_detail.png')).exists():
            prepare(species + '_female', height)
