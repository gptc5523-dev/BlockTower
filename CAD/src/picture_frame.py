"""Picture for the side walls (one each side of the table), separate parts that only touch.
Units: mm, origin at the floor at the wall face, wall at Y = 0, room toward -Y, Z up.

Landscape 900 x 640 in a stepped oak moulding: a 50 wide outer rail 38 deep with a rounded front edge,
stepping down to a 14 wide inner lip 22 deep, so the frame reads as a profile and not a flat board.
Behind the lip: a 2 mm mat with a 70 mm border, the print on 3 mm hardboard (glass not modeled).
It hangs from a sawtooth hanger on a wall hook, held 5 mm off the wall by two bumpers (wire and fixings not
modeled). Centre 1450 above the floor, the same eye height as the picture over the sideboard (wall_decor.py).
The print is a plain face here: Blender paints it (Unity/BlockTower/Blender/Props.blend).
"""

from cadgen import build123d as bd
from cadgen import glb, srgb, step

PIC_W, PIC_H, PIC_Z = 900.0, 640.0, 1450.0
RAIL, RAIL_D, FRONT_R = 50.0, 38.0, 8.0
LIP, LIP_D = 14.0, 22.0
MAT_T, BACKING_T, MAT_BORDER = 2.0, 3.0, 70.0
STANDOFF = 5.0

OAK, MAT, PRINT, METAL = "#B0844F", "#F1ECE2", "#C8BBA2", "#8E8E8A"


def box(x0, x1, y0, y1, z0, z1):
    return bd.Pos((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2) * bd.Box(x1 - x0, y1 - y0, z1 - z0)


def ring(w, h, inset, y0, y1, z):
    """A rectangular band: outer w x h, `inset` wide, from y0 to y1, centred at height z"""
    return box(-w / 2, w / 2, y0, y1, z - h / 2, z + h / 2) - \
        box(-w / 2 + inset, w / 2 - inset, y0, y1, z - h / 2 + inset, z + h / 2 - inset)


def picture():
    w, h, z = PIC_W, PIC_H, PIC_Z
    yb = -STANDOFF  # frame back
    outer = ring(w, h, RAIL, yb - RAIL_D, yb, z)
    # round the outer front edge: the edges on the front face that lie on the outside boundary
    front = outer.faces().sort_by(bd.Axis.Y)[0]
    outside = [e for e in front.edges() if abs(abs(e.center().X) - w / 2) < 1e-3 or abs(abs(e.center().Z - z) - h / 2) < 1e-3]
    outer = bd.fillet(outside, FRONT_R)
    lip = ring(w - 2 * RAIL, h - 2 * RAIL, LIP, yb - LIP_D, yb, z)
    frame = outer + lip
    # the mat, print and backing sit in a 6 mm rebate behind the lip
    open_w, open_h = w - 2 * (RAIL + LIP), h - 2 * (RAIL + LIP)
    rw, rh = open_w + 12, open_h + 12
    frame -= box(-rw / 2, rw / 2, yb - MAT_T - BACKING_T, yb, z - rh / 2, z + rh / 2)
    mat = ring(rw, rh, MAT_BORDER + 6, yb - BACKING_T - MAT_T, yb - BACKING_T, z)
    aw, ah = rw - 2 * (MAT_BORDER + 6), rh - 2 * (MAT_BORDER + 6)
    art = box(-aw / 2, aw / 2, yb - BACKING_T - 0.5, yb - BACKING_T, z - ah / 2, z + ah / 2)
    backing = box(-rw / 2, rw / 2, yb - BACKING_T, yb, z - rh / 2, z + rh / 2)
    top = z + h / 2
    hanger = box(-25, 25, yb, yb + 1.0, top - 30, top - 20)  # sawtooth plate on the back of the top rail
    hook = box(-8, 8, yb + 1.0, 0, top - 40, top - 30)  # wall hook, the hanger rests on its top
    bumpers = [box(x - 6, x + 6, yb, 0, z - h / 2 + 12, z - h / 2 + 24) for x in (-w / 2 + 30, w / 2 - 30)]
    return [("frame", frame, OAK), ("mat", mat, MAT), ("print", art, PRINT), ("backing", backing, "#5A4A3A"),
            ("hanger", hanger, METAL), ("hook", hook, METAL)] + \
        [(f"bumper_{i + 1}", b, "#2A2A2A") for i, b in enumerate(bumpers)]


@glb(out="../GLB/picture_frame.glb")
@step(out="../STEP/picture_frame.step")
def picture_frame():
    parts = []
    for name, part, color in picture():
        part.label = name
        part.color = srgb(color)
        parts.append(part)
    body = bd.Compound(children=parts)
    body.label = "picture_frame"
    return body


if __name__ == "__main__":
    picture_frame()
