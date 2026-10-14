"""Generate the calm, seamless pixel-grass material used by the map shader."""
from math import floor
from functools import lru_cache
from pathlib import Path
import random

from PIL import Image, ImageDraw


SIZE = 1024
CELL = 4
GRID = SIZE // CELL
OUTPUT = Path(__file__).resolve().parents[1] / "Assets/_Project/Render/Resources/terrain_grass_detail_v7.png"


@lru_cache(maxsize=None)
def noise_grid(period, seed):
    rng = random.Random(seed)
    return tuple(tuple(rng.random() for _ in range(period)) for _ in range(period))


def periodic_noise(x, y, period, seed):
    grid = noise_grid(period, seed)
    gx, gy = x * period / GRID, y * period / GRID
    ix, iy = floor(gx), floor(gy)
    fx, fy = gx - ix, gy - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a, b = grid[iy % period][ix % period], grid[iy % period][(ix + 1) % period]
    c, d = grid[(iy + 1) % period][ix % period], grid[(iy + 1) % period][(ix + 1) % period]
    return (a + (b - a) * fx) * (1 - fy) + (c + (d - c) * fx) * fy


def main():
    image = Image.new("RGB", (SIZE, SIZE))
    draw = ImageDraw.Draw(image)
    palette = ["#3e6035", "#466b38", "#4d743b", "#547b3f", "#5b8243"]
    soil = ["#756344", "#816b48", "#8b754e"]

    for y in range(GRID):
        for x in range(GRID):
            broad = periodic_noise(x, y, 9, 41)
            medium = periodic_noise(x, y, 27, 83)
            fine = periodic_noise(x, y, 81, 127)
            field = broad * 0.58 + medium * 0.31 + fine * 0.11
            if field < 0.22:
                color = palette[0]
            elif field < 0.43:
                color = palette[1]
            elif field < 0.65:
                color = palette[2]
            elif field < 0.84:
                color = palette[3]
            else:
                color = palette[4]

            earth = periodic_noise(x, y, 20, 239) * 0.68 + periodic_noise(x, y, 50, 311) * 0.32
            if earth > 0.78 and broad > 0.44:
                color = soil[1 if fine > 0.52 else 0]
            draw.rectangle((x * CELL, y * CELL, x * CELL + CELL - 1, y * CELL + CELL - 1), fill=color)

    # Few short, low-contrast grass clusters break up the field without competing
    # with the actual vegetation sprites placed by the game.
    rng = random.Random(20261007)
    colors = ["#668744", "#708c4a", "#405f36"]
    for _ in range(58):
        x, y = rng.randrange(GRID), rng.randrange(GRID)
        shade = colors[rng.randrange(len(colors))]
        for dx, dy in ((0, 0), (-1, 1), (1, 1)):
            left, top = ((x + dx) % GRID) * CELL, ((y + dy) % GRID) * CELL
            draw.rectangle((left, top, left + CELL - 1, top + CELL - 1), fill=shade)

    image.save(OUTPUT, optimize=True)
    print(f"Wrote {OUTPUT} ({SIZE}x{SIZE}, seamless 4px clusters)")


if __name__ == "__main__":
    main()
