"""Numeric import validation only; does not create or edit image files."""
from pathlib import Path
import json
import base64
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]

names = [name + suffix for name in ('human', 'orc', 'dwarf', 'elf') for suffix in ('', '_female')]
names += ['sheep', 'wolf', 'bear', 'deer', 'dragon', 'slime', 'skeleton', 'fairy', 'zombie', 'fire_elemental',
          'ember_imp', 'sandworm', 'walking_tree', 'ghost', 'water_elemental', 'earth_golem',
          'kraken', 'rot_crawler', 'candy_golem', 'gummy_bear',
          'shroomling', 'crystal_beetle', 'ash_crawler', 'bone_crawler',
          'clock_crab', 'void_moth', 'flesh_mound', 'devourer',
          'zombie_runner', 'zombie_brute', 'zombie_bloater', 'zombie_crawler',
          'zombie_beast', 'zombie_dragon', 'zombie_lord', 'necromancer',
          'bloodsucker', 'sky_whale_calf', 'dark_mage', 'white_mage',
          'druid', 'snowman', 'storm_spirit', 'cat', 'dog', 'rabbit', 'cow',
          'chicken', 'rat', 'monkey', 'fox', 'boar', 'goat', 'horse_wild', 'reindeer',
          'penguin', 'crab', 'scorpion', 'lizard', 'crocodile', 'snake', 'turtle', 'seal',
          'hyena', 'lion', 'rhino', 'buffalo', 'camel', 'polar_bear', 'salamander',
          'frog', 'bee', 'ant', 'beetle', 'owl', 'crow', 'eagle', 'parrot',
          'snail', 'fish', 'butterfly', 'caterpillar',
          'piranha', 'mosquito_swarm', 'sky_visitor', 'colossus', 'bandit', 'cultist']
for name in names:
    base = ROOT / 'Art/Final/units' / (name + '_detail')
    raw = np.array(Image.open(base.with_suffix('.png')).convert('RGBA'))
    data = json.loads(base.with_suffix('.json').read_text())
    assert len(data['frames']) == 24
    hashes = []
    opaque = []
    for frame in data['frames']:
        # Match prepare_unit_sheets.py and DetailedUnitSheet.Build midpoint handling.
        w = max(1, int(np.floor(frame['width'] * data['scale'] + 0.5)))
        h = max(1, int(np.floor(frame['height'] * data['scale'] + 0.5)))
        assert w <= 38 and h <= 35
        xs = frame['x'] + np.minimum(frame['width'] - 1, ((np.arange(w) + .5) * frame['width'] / w).astype(int))
        ys = frame['y'] + np.minimum(frame['height'] - 1, ((np.arange(h) + .5) * frame['height'] / h).astype(int))
        sample = raw[np.ix_(ys, xs)]
        colors = (sample[:, :, :3].copy() if data.get('fullColor') else
                  np.clip(np.rint(sample[:, :, :3].astype(float).sum(axis=2) / 84), 1, 8).astype(np.uint8))
        colors[sample[:, :, 3] < 128] = 0
        mask = np.frombuffer(base64.b64decode(frame['mask']), dtype=np.uint8).reshape(h, w)
        assert set(np.unique(mask)).issubset({0, 1})
        colors[mask == 0] = 0
        assert np.count_nonzero(colors) > 30
        assert ((40-w)//2) >= 1 and data['baseline'] - h >= 1
        hashes.append((w, h, colors.tobytes()))
        opaque.append(int(np.count_nonzero(colors)))
    assert len(set(hashes[4:8])) >= 3
    print(f"{name}: 24 nonempty poses; 4 distinct walk frames; {'full color' if data.get('fullColor') else 'palette 1..8'}; gutters safe; opaque values {min(opaque)}..{max(opaque)}")
print(f'PASS: {len(names) * 24} source poses and import geometry. This is not a Unity runtime test.')
