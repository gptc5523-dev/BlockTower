"""Picture and vase for the sideboard wall, as separate parts that only touch.
Units: mm, origin at the floor at the wall face, wall at Y = 0, room toward -Y,
Z up. Placed with the sideboard (src/sideboard.py, 760 high, 1600 wide).

Picture 700 x 500 in a 30 x 25 black frame, white mat and print behind it,
hardboard back. It hangs from a sawtooth hanger on the top rail resting on a
wall hook, held 5 mm off the wall by two bumpers at the bottom (wire, glass and
fixings not modeled). Centre 1450 above the floor: eye height and ~430 above
the sideboard top.

Vase: thrown stoneware, Ø150 x 280, 6 mm walls, standing on the sideboard top
right of centre.
"""

from cadgen import build123d as bd
from cadgen import glb, srgb, step

PIC_W, PIC_H, PIC_Z = 700.0, 500.0, 1450.0
RAIL, RAIL_D = 30.0, 25.0
MAT_T, BACKING_T = 2.0, 3.0
STANDOFF = 5.0
SIDEBOARD_H, SIDEBOARD_D = 760.0, 420.0
VASE_X, VASE_D, VASE_H, VASE_T = 420.0, 150.0, 280.0, 6.0

FRAME, MAT, PRINT, METAL, STONEWARE = "#1E1C1A", "#F3F0E8", "#7A8C6E", "#8E8E8A", "#C9B8A0"


def box(x0, x1, y0, y1, z0, z1):
    return bd.Pos((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2) * bd.Box(x1 - x0, y1 - y0, z1 - z0)


def picture():
    w, h = PIC_W / 2, PIC_H / 2
    yb = -STANDOFF  # frame back
    yf = yb - RAIL_D
    z0, z1 = PIC_Z - h, PIC_Z + h
    frame = box(-w, w, yf, yb, z0, z1) - box(-w + RAIL, w - RAIL, yf, yb, z0 + RAIL, z1 - RAIL)
    # the mat and print sit in a 5 mm rebate at the back of the frame opening
    rebate = box(-w + RAIL - 5, w - RAIL + 5, yb - MAT_T - BACKING_T, yb, z0 + RAIL - 5, z1 - RAIL + 5)
    frame -= rebate
    inner = (-w + RAIL - 5, w - RAIL + 5, z0 + RAIL - 5, z1 - RAIL + 5)
    mat = box(inner[0], inner[1], yb - MAT_T - BACKING_T, yb - BACKING_T, inner[2], inner[3]) - \
        box(-w + 90, w - 90, yb - MAT_T - BACKING_T, yb - BACKING_T, z0 + 90, z1 - 90)
    art = box(-w + 90, w - 90, yb - BACKING_T - 0.5, yb - BACKING_T, z0 + 90, z1 - 90)
    backing = box(inner[0], inner[1], yb - BACKING_T, yb, inner[2], inner[3])
    hanger = box(-25, 25, yb + 0, yb + 1.0, z1 - 20, z1 - 10)  # sawtooth plate on the top rail back
    hook = box(-8, 8, -STANDOFF + 0.5, 0, z1 - 30, z1 - 20)  # wall hook, hanger rests on its top
    bumpers = [box(x - 6, x + 6, -STANDOFF, 0, z0 + 10, z0 + 22) for x in (-w + 30, w - 30)]
    return [("frame", frame, FRAME), ("mat", mat, MAT), ("print", art, PRINT), ("backing", backing, FRAME),
            ("hanger", hanger, METAL), ("hook", hook, METAL)] + \
        [(f"bumper_{i + 1}", b, METAL) for i, b in enumerate(bumpers)]


def vase():
    r, h, t = VASE_D / 2, VASE_H, VASE_T
    side = bd.Spline((r * 0.55, 0), (r, h * 0.35), (r * 0.8, h * 0.75), (r * 0.42, h * 0.92), (r * 0.5, h))
    outline = bd.Polyline((0, h), (0, 0), (r * 0.55, 0)) + side + bd.Line((r * 0.5, h), (0, h))
    solid = bd.revolve(bd.Plane.XZ * bd.make_face(outline), bd.Axis.Z)
    body = bd.offset(solid, -t, openings=solid.faces().sort_by(bd.Axis.Z)[-1])  # open at the mouth
    return [("vase", bd.Pos(VASE_X, -SIDEBOARD_D / 2, SIDEBOARD_H) * body, STONEWARE)]


def decor_parts():
    out = []
    for name, part, color in picture() + vase():
        part.label = name
        part.color = srgb(color)
        out.append(part)
    return out


@glb(out="../GLB/wall_decor.glb")
@step(out="../STEP/wall_decor.step")
def wall_decor():
    body = bd.Compound(children=decor_parts())
    body.label = "wall_decor"
    return body


if __name__ == "__main__":
    wall_decor()
