"""Small succulent for the sideboard top, an echeveria rosette, as separate parts that only touch: glazed pot,
gravel, plant.
Units: mm, origin at the bottom of the pot centre, Z up.

Pot: a low bowl pot, Ø96 foot widening to Ø120 under a 10 mm rim, 80 high, 6 mm walls, 8 mm floor (pot() from
potted_plant.py), so the rosette sits up at the rim as echeverias are potted. Gravel fills it to 5 mm below the rim.
Plant: 44 fleshy spoon-shaped leaves on a short stem, set at the golden angle (137.5 deg) so they overlap like
rose petals. The outer, oldest leaves are the largest (46 mm) and lie nearly flat; each newer one is smaller,
sits higher on the stem and stands more upright, closing to a bud in the centre. Each leaf is lofted through
smooth sections: a narrow base, broadest two thirds out, a short point; cupped on top, round and thick
underneath, thinning to the edges; the tips curl up. One part with the stem, trimmed at the gravel surface.
About 110 across.
"""

import math

from cadgen import build123d as bd
from cadgen import glb, srgb, step

from potted_plant import ACROSS, GOLDEN, blade, jitter, pot

POT_R0, POT_R1, POT_H, WALL, FLOOR = 48.0, 58.0, 80.0, 6.0, 8.0
RIM_H, RIM_OUT, FILL_BELOW_RIM = 10.0, 2.0, 5.0
LEAVES, STEM_R, STEM_H = 44, 8.0, 18.0

GLAZE, GRAVEL, LEAF = "#E9E4DA", "#B8AFA2", "#8FA89A"


def spoon(t):
    """Echeveria leaf outline, half width as a share of the full: a narrow base, broadest two thirds out,
    a short point"""
    if t < 0.68:
        return 0.38 + 0.62 * math.sin(math.pi / 2 * t / 0.68)
    return math.cos(math.pi / 2 * (t - 0.68) / 0.32) ** 0.7


def fleshy(w, fold, thick):
    """Fleshy leaf section: a cupped top (edges rising as the square of the distance out) over a rounded belly,
    `thick` deep at the midrib and 0.4 mm at the edges"""
    belly = min(thick, 0.45 * w)
    top = [(x * w, fold * w * x * x) for x in reversed(ACROSS)]
    bottom = [(x * w, fold * w * x * x - 0.4 - belly * (1 - x * x)) for x in ACROSS]
    return bottom + top


def rosette(top):
    below = bd.Pos(0, 0, top - 500) * bd.Box(1000, 1000, 1000)
    parts = [bd.Pos(0, 0, top) * bd.Cylinder(STEM_R, STEM_H, align=(bd.Align.CENTER, bd.Align.CENTER, bd.Align.MIN))]
    for i in range(LEAVES):
        age = 1 - i / LEAVES  # the first are the outer, oldest leaves
        heading = i * GOLDEN + 6 * (jitter(i, 1) - 0.5)
        length = (16 + 30 * age ** 0.8) * (0.94 + 0.12 * jitter(i, 2))
        elev = 10 + 72 * (1 - age) ** 1.4 + 6 * jitter(i, 3)
        droop = -(12 + 22 * (1 - age))  # the tip curls up
        r = STEM_R * 0.6 * (0.4 + 0.6 * age)
        z = top + 1.5 + (STEM_H - 4) * (1 - age)
        at = bd.Pos(math.cos(math.radians(heading)) * r, math.sin(math.radians(heading)) * r, z) * bd.Rot(0, 0, heading)
        b = blade(length, length * 0.62, elev, droop, 0.16, thick=4.5 * (0.5 + 0.5 * age), outline=spoon,
                  section=fleshy, n=12)
        parts.append(at * bd.Rot(8 * (jitter(i, 4) - 0.5), 0, 0) * b)
    # the outer leaves' bellies would dip into the gravel: trimmed at its surface
    return bd.Compound(children=[p - below for p in parts])


def succulent_parts():
    body, gravel, top = pot(POT_R0, POT_R1, POT_H, WALL, FLOOR, RIM_H, RIM_OUT, FILL_BELOW_RIM)
    return [("pot", body, GLAZE), ("gravel", gravel, GRAVEL), ("plant", rosette(top), LEAF)]


@glb(out="../GLB/succulent.glb")
@step(out="../STEP/succulent.step")
def succulent():
    parts = []
    for name, part, color in succulent_parts():
        part.label = name
        part.color = srgb(color)
        parts.append(part)
    body = bd.Compound(children=parts)
    body.label = "succulent"
    return body


if __name__ == "__main__":
    succulent()
