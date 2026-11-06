"""Floor plant for the left far corner, a snake plant (Sansevieria trifasciata), as separate parts that only
touch: pot, soil, leaves. Units: mm, origin at the floor under the pot centre, Z up.

Pot: a matte charcoal ceramic cylinder, Ø300 widening slightly to Ø320 under a 6 mm lip, 400 high, 10 mm walls,
14 mm floor (pot() from potted_plant.py); soil to 30 below the rim.
Leaves: 17 stiff sword leaves in three clumps from the creeping rhizome, as the plant grows: each rises nearly
upright from the soil, leaning a little out from its clump, 420-760 long, 48-78 wide, narrow and channelled at
the base, broadest above the middle, ending in a point; 2.2 mm thick, slightly cupped, and each twists 15-60 deg
about its length as real ones do. Trimmed at the soil surface. About 1150 tall, 520 across.
"""

import math

from cadgen import build123d as bd
from cadgen import glb, srgb, step

from potted_plant import arched, blade, jitter, pot

POT_R0, POT_R1, POT_H, WALL, FLOOR = 150.0, 160.0, 400.0, 10.0, 14.0
RIM_H, RIM_OUT, SOIL_BELOW_RIM = 12.0, 6.0, 30.0
CLUMPS = [((-45, 30), 6), ((50, 25), 6), ((0, -55), 5)]  # (centre on the soil, leaves)

CHARCOAL, SOIL, LEAF = "#3A3A3C", "#3B2A1E", "#2E4A2A"


def sword(t):
    """Sansevieria leaf outline: a narrow channelled base, broadest above the middle, tapering to a point"""
    return (0.32 + 0.68 * math.sin(math.pi / 2 * min(t / 0.6, 1))) * (1 - max(0, (t - 0.6) / 0.4) ** 1.6)


def leaves(soil_top):
    out, i = [], 0
    for (cx, cy), n in CLUMPS:
        out_dir = math.degrees(math.atan2(cy, cx))  # each clump leans away from the pot centre
        for k in range(n):
            heading = out_dir + (k - (n - 1) / 2) * 34 + 14 * (jitter(i, 1) - 0.5)
            length = 420 + 340 * jitter(i, 2)
            width = 48 + 30 * jitter(i, 3)
            elev = 80 + 7 * jitter(i, 4)
            hx, hy = math.cos(math.radians(heading)), math.sin(math.radians(heading))
            at = bd.Pos(cx + hx * 10, cy + hy * 10, soil_top - 8) * bd.Rot(0, 0, heading)
            b = blade(length, width, elev, 6 + 10 * jitter(i, 5), 0.12, thick=2.2, outline=sword, section=arched,
                      n=16, twist=(15 + 45 * jitter(i, 6)) * (1 if jitter(i, 7) > 0.5 else -1))
            out.append(at * b)
            i += 1
    soil = bd.Pos(0, 0, soil_top - 500) * bd.Box(2000, 2000, 1000)
    return bd.Compound(children=[leaf - soil for leaf in out])


def plant_parts():
    body, soil, top = pot(POT_R0, POT_R1, POT_H, WALL, FLOOR, RIM_H, RIM_OUT, SOIL_BELOW_RIM)
    return [("pot", body, CHARCOAL), ("soil", soil, SOIL), ("leaves", leaves(top), LEAF)]


@glb(out="../GLB/snake_plant.glb")
@step(out="../STEP/snake_plant.step")
def snake_plant():
    parts = []
    for name, part, color in plant_parts():
        part.label = name
        part.color = srgb(color)
        parts.append(part)
    body = bd.Compound(children=parts)
    body.label = "snake_plant"
    return body


if __name__ == "__main__":
    snake_plant()
