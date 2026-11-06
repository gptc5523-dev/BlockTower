"""Stacking-tower block, 21 x 28 x 84 mm (length = 3 widths, so three blocks
side by side make a square layer). Units: mm, length along X.

Levels alternate hard maple (705 kg/m^3 -> 34.8 g) and black walnut
(610 kg/m^3 -> 30.1 g), USDA Wood Handbook, 12 % moisture. Real blocks vary about +/-0.3 mm in
thickness; that variation is what makes some blocks loose. It is a game-side
parameter (per-block random scale in Unreal), so this model stays nominal.
"""

from cadgen import build123d as bd
from cadgen import glb, step

PER_LEVEL = 3
WIDTH = 28.0
LENGTH = PER_LEVEL * WIDTH
THICK = 21.0
RHO_MAPLE, RHO_WALNUT = 705e-9, 610e-9  # kg/mm^3, even and odd levels
EASE = 1.0  # sanded edges


def block_shape():
    body = bd.Pos(0, 0, THICK / 2) * bd.Box(LENGTH, WIDTH, THICK)
    return bd.fillet(body.edges(), EASE)


@glb(out="../GLB/tower_block.glb")
@step(out="../STEP/tower_block.step")
def tower_block():
    body = block_shape()
    body.label = "tower_block"
    return body


if __name__ == "__main__":
    tower_block()
