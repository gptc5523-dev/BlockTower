"""Low walnut sideboard for the dining-room wall, as separate parts that only
touch. Units: mm, origin at the floor under the back-centre, front toward -Y,
Z up.

1600 x 420 x 760: a common sideboard size, waist-high so the top is usable as a
serving surface, low enough to sit under a picture. Carcass in 18 mm panels on
four 150 mm legs (clears the 80 mm baseboard, so the back sits flush to the
wall). Two 2 mm steel anti-tip angles inside, under the top's back edge, screw it to the wall
through the back panel, hidden (the 41 kg cabinet otherwise tips with ~81 N at the top front). Three
full-overlay doors with 2 mm reveals, brass knobs. The back is a
6 mm panel between the sides. Joinery, hinges and shelves not modeled; doors
are shown resting against the carcass front edges they close onto.
"""

from cadgen import build123d as bd
from cadgen import glb, srgb, step

W, D, H = 1600.0, 420.0, 760.0
T, BACK_T = 18.0, 6.0
LEG, LEG_H, LEG_INSET = 40.0, 150.0, 40.0
DOOR_T, REVEAL, EDGE_GAP = 18.0, 2.0, 1.0
KNOB_D, KNOB_L = 25.0, 25.0
ANGLE_T, ANGLE_LEG, ANGLE_W, ANGLE_X = 2.0, 30.0, 40.0, 600.0
DOORS = 3

WALNUT, BRASS, STEEL = "#5A3B26", "#B08D57", "#8E8E8A"


def box(x0, x1, y0, y1, z0, z1):
    return bd.Pos((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2) * bd.Box(x1 - x0, y1 - y0, z1 - z0)


def sideboard_parts():
    w, front = W / 2, -D + DOOR_T  # carcass front plane; doors sit in front of it
    zb, zt = LEG_H, H - T  # bottom panel underside, top panel underside
    parts = [
        ("top", box(-w, w, front, 0, zt, H), WALNUT),
        ("bottom", box(-w, w, front, 0, zb, zb + T), WALNUT),
        ("side_left", box(-w, -w + T, front, 0, zb + T, zt), WALNUT),
        ("side_right", box(w - T, w, front, 0, zb + T, zt), WALNUT),
        ("back", box(-w + T, w - T, -BACK_T, 0, zb + T, zt), WALNUT),
    ]
    door_w = (W - 2 * EDGE_GAP - (DOORS - 1) * REVEAL) / DOORS
    xs = [-w + EDGE_GAP + i * (door_w + REVEAL) for i in range(DOORS)]
    for i in range(1, DOORS):  # dividers behind each reveal, so both doors close onto one
        cx = xs[i] - REVEAL / 2
        parts.append((f"divider_{i}", box(cx - T / 2, cx + T / 2, front, -BACK_T, zb + T, zt), WALNUT))
    for i, x in enumerate(xs):
        parts.append((f"door_{i + 1}", box(x, x + door_w, -D, front, zb + T + REVEAL, zt - REVEAL), WALNUT))
        kx = x + door_w - 40 if i < DOORS - 1 else x + 40  # knobs near the meeting edges
        knob = bd.Pos(kx, -D - KNOB_L / 2, zt - 60) * bd.Rot(90, 0, 0) * bd.Cylinder(KNOB_D / 2, KNOB_L)
        parts.append((f"knob_{i + 1}", knob, BRASS))
    for i, (x, y) in enumerate((x, y) for x in (-w + LEG_INSET, w - LEG_INSET - LEG)
                               for y in (front + LEG_INSET, -LEG_INSET - LEG)):
        parts.append((f"leg_{i + 1}", box(x, x + LEG, y, y + LEG, 0, LEG_H), WALNUT))
    for i, x in enumerate((-ANGLE_X, ANGLE_X)):
        # inside the cabinet: one leg under the top, the other on the back panel, screwed through it into the wall
        yb = -BACK_T
        angle = box(x - ANGLE_W / 2, x + ANGLE_W / 2, yb - ANGLE_LEG, yb, zt - ANGLE_T, zt) + \
            box(x - ANGLE_W / 2, x + ANGLE_W / 2, yb - ANGLE_T, yb, zt - ANGLE_LEG, zt - ANGLE_T)
        parts.append((f"anti_tip_{i + 1}", angle, STEEL))
    out = []
    for name, part, color in parts:
        part.label = name
        part.color = srgb(color)
        out.append(part)
    return out


@glb(out="../GLB/sideboard.glb")
@step(out="../STEP/sideboard.step")
def sideboard():
    body = bd.Compound(children=sideboard_parts())
    body.label = "sideboard"
    return body


if __name__ == "__main__":
    sideboard()
