"""Four-seat wooden dining table, sized for src/chair.py. Units: mm,
X = length, Y = width, Z = up.

Dimension rationale:
- Height 730: seat-to-table difference of 290 mm (chair seat 440) sits in the
  270-300 mm range where forearms rest level on the top.
- 1600 x 900: 800 mm of edge per diner (600 mm minimum elbow room), 450 mm
  of depth each side, so chairs on both sides tuck in fully.
- Apron underside at 640: clears the chair seat plus a large thigh, so knees
  and thighs slide under without touching.
- Legs 100 mm in from the ends: two 450 mm chairs fit between them per side.
- Side overhang 30 mm: widens the leg stance so a 400 N sideways push does not tip it.

Construction: separate parts that only touch. Aprons butt against the legs
(mortise-and-tenon joints not modeled) and sit 10 mm back from the leg faces.
The top rests on the aprons; in a real build it is held with slotted buttons so
the solid top can move across the grain with the seasons.
"""

from cadgen import build123d as bd
from cadgen import glb, step

LENGTH = 1600.0
WIDTH = 900.0
HEIGHT = 730.0
TOP_THICK = 30.0
END_OVERHANG = 100.0  # top beyond the leg faces, ends
SIDE_OVERHANG = 30.0  # top beyond the leg faces, long sides
LEG = 60.0
APRON_HEIGHT = 60.0
APRON_THICK = 22.0
APRON_INSET = 10.0  # apron face set back from the leg face
EASE = 3.0


def eased(part, r=EASE):
    return bd.fillet(part.edges(), r)


def table_top():
    plan = bd.fillet(bd.Rectangle(LENGTH, WIDTH).vertices(), 10)
    top = bd.extrude(plan, amount=TOP_THICK)
    top = bd.fillet(top.edges().group_by(bd.Axis.Z)[-1], 5)
    return bd.fillet(top.edges().group_by(bd.Axis.Z)[0], 2)


def table_parts():
    under = HEIGHT - TOP_THICK
    lx = LENGTH / 2 - END_OVERHANG - LEG / 2
    ly = WIDTH / 2 - SIDE_OVERHANG - LEG / 2
    top = bd.Pos(0, 0, under) * table_top()
    legs = [eased(bd.Pos(x, y, under / 2) * bd.Box(LEG, LEG, under)) for x in (-lx, lx) for y in (-ly, ly)]

    az = under - APRON_HEIGHT / 2
    off = LEG / 2 - APRON_INSET - APRON_THICK / 2  # apron centre offset from leg centre
    long_len, short_len = 2 * lx - LEG, 2 * ly - LEG
    aprons = [eased(bd.Pos(0, s * (ly + off), az) * bd.Box(long_len, APRON_THICK, APRON_HEIGHT), 1) for s in (-1, 1)]
    aprons += [eased(bd.Pos(s * (lx + off), 0, az) * bd.Box(APRON_THICK, short_len, APRON_HEIGHT), 1) for s in (-1, 1)]

    names = ["top", "leg_1", "leg_2", "leg_3", "leg_4", "apron_long_1", "apron_long_2", "apron_end_1", "apron_end_2"]
    parts = [top] + legs + aprons
    for name, part in zip(names, parts):
        part.label = name
    return parts


@glb(out="../GLB/table.glb")
@step(out="../STEP/table.step")
def table():
    body = bd.Compound(children=table_parts())
    body.label = "table"
    return body


if __name__ == "__main__":
    table()
