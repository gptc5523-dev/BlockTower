"""Dining set: the table with four chairs tucked in, two per long side.

Each chair is pushed under the table until its backrest is CLEARANCE mm from
the table edge, or until its seat front meets the chair opposite (a narrow
top is too narrow for both to tuck fully). Chairs sit between the legs. Units: mm.
"""

from cadgen import build123d as bd
from cadgen import glb, step

from chair import chair
from table import END_OVERHANG, HEIGHT, LEG, LENGTH, TOP_THICK, WIDTH, table

CLEARANCE = 10.0  # backrest to table edge, and between opposite chairs


@glb(out="../GLB/dining_set.glb")
@step(out="../STEP/dining_set.step")
def dining_set():
    t = table()
    c = chair()
    # Chair material at table-top height: the backrest, which stops the chair.
    band = bd.Pos(0, 0, HEIGHT - TOP_THICK / 2) * bd.Box(2000, 2000, TOP_THICK)
    back_y = (c & band).bounding_box().min.Y
    dy = max(WIDTH / 2 + CLEARANCE - back_y, CLEARANCE / 2 - c.bounding_box().min.Y)
    between = LENGTH - 2 * (END_OVERHANG + LEG)  # leg face to leg face
    cx = between / 4  # two chairs evenly spaced between the legs

    children = [t]
    for i, (x, side) in enumerate([(-cx, 1), (cx, 1), (-cx, -1), (cx, -1)], 1):
        loc = bd.Location((x, side * dy, 0), (0, 0, 0 if side > 0 else 180))
        placed = loc * c
        placed.label = f"chair_{i}"
        children.append(placed)
    body = bd.Compound(children=children)
    body.label = "dining_set"
    return body


if __name__ == "__main__":
    dining_set()
