"""Game scene: the dining set with a 20-level block tower at the table centre.

Layers alternate direction; each layer is three blocks side by side, resting
on the layer below. Units: mm. Block origin is its bottom-face centre.
"""

from cadgen import build123d as bd
from cadgen import glb, step

from dining_set import dining_set
from tower_block import LENGTH, PER_LEVEL, THICK, WIDTH, block_shape
from table import HEIGHT

LEVELS = 20


def tower_locations():
    """(x, y, z, yaw) of each block, bottom level first, table top at z = HEIGHT."""
    locs = []
    for level in range(LEVELS):
        z = HEIGHT + level * THICK
        for i in range(PER_LEVEL):
            k = i - (PER_LEVEL - 1) / 2
            if level % 2 == 0:
                locs.append((0.0, k * WIDTH, z, 0.0))  # long side along X
            else:
                locs.append((k * WIDTH, 0.0, z, 90.0))
    return locs


@glb(out="../GLB/game_set.glb")
@step(out="../STEP/game_set.step")
def game_set():
    b = block_shape()
    blocks = []
    for i, (x, y, z, yaw) in enumerate(tower_locations(), 1):
        placed = bd.Location((x, y, z), (0, 0, yaw)) * b
        placed.label = f"block_{i:02d}"
        blocks.append(placed)
    tower = bd.Compound(children=blocks)
    tower.label = "tower"
    body = bd.Compound(children=[dining_set(), tower])
    body.label = "game_set"
    return body


if __name__ == "__main__":
    assert LENGTH == PER_LEVEL * WIDTH
    game_set()
