"""Physical sanity checks for the room (door, baseboards), the sideboard and the
wall decor, as placed in the game (run from the CAD project root).

Placement (Unity SceneBuilder, CAD coordinates): sideboard back against the
south wall face (Y = -2500), centred; decor origin on the same wall face.

Materials and ratings (static estimates, not certification tests):
- Hollow-core interior door leaf, ~280 kg/m^3 effective (about 20 kg for 900 x 2100 x 40).
- 3.5" steel butt hinge: ~20 kg per hinge (typical retailer rating, unverified); we ask 2x on the leaf.
- Walnut 640 kg/m^3, hardboard back 900 kg/m^3. Anti-tip angle: 2 screws into a wall plug,
  ~300 N pull-out per angle (typical 5 mm screw in a plug, unverified); we take half.
- Picture hook: 10 kg rating (typical single-nail hook, unverified); frame pine ~500 kg/m^3,
  hardboard backing, mat board ~700 kg/m^3.
- Stoneware 2300 kg/m^3.
"""
import itertools
import math

from cadgen import build123d as bd
from cadgen import read_step

G = 9.81
SOUTH = -2500.0
fails = []


def check(ok, msg):
    print(("PASS " if ok else "FAIL ") + msg)
    if not ok:
        fails.append(msg)


def by_label(shape):
    return {c.label: c for c in shape.children}


def contact_checks(name, solids):
    worst = max(
        (sum(s.volume for s in (a & b).solids()) if (a & b) else 0.0)
        for a, b in itertools.combinations(solids, 2)
    )
    check(worst <= 1.0, f"{name}: no interpenetration (worst overlap {worst:.2f} mm^3)")
    touch = {i: {j for j, b in enumerate(solids) if j != i and a.distance_to(b) < 0.05} for i, a in enumerate(solids)}
    seen, todo = set(), [0]
    while todo:
        k = todo.pop()
        if k not in seen:
            seen.add(k)
            todo += touch[k]
    check(len(seen) == len(solids), f"{name}: all {len(solids)} parts connected by contact")


room = by_label(read_step("STEP/room.step"))
board = by_label(read_step("STEP/sideboard.step"))
decor = by_label(read_step("STEP/wall_decor.step"))
# sideboard: back at the wall, front toward the room (+Y), i.e. its model rotated 180 deg
to_wall = bd.Location((0, SOUTH, 0), (0, 0, 180))
board_placed = {k: to_wall * v for k, v in board.items()}
decor_placed = {k: to_wall * v for k, v in decor.items()}

contact_checks("room", list(room.values()))
contact_checks("sideboard", list(board.values()))
# decor only stands up through the wall and the sideboard top
contact_checks("decor on wall + sideboard",
               list(decor_placed.values()) + [room["wall_south"], board_placed["top"]]
               + [v for k, v in board_placed.items() if k != "top"] + [room["floor"]])
worst = max((sum(s.volume for s in (a & b).solids()) if (a & b) else 0.0)
            for a, b in itertools.product(list(board_placed.values()) + list(decor_placed.values()), room.values()))
check(worst <= 1.0, f"furniture vs room: no interpenetration (worst {worst:.2f} mm^3)")

# door
leaf = room["door_leaf"]
leaf_kg = leaf.volume * 280e-9
hinges = [v for k, v in room.items() if k.startswith("hinge")]
check(len(hinges) * 20 >= 2 * leaf_kg, f"door leaf {leaf_kg:.1f} kg on {len(hinges)} hinges (20 kg each, 2x)")
lb = leaf.bounding_box()
check(8 <= lb.min.Z <= 15, f"door undercut {lb.min.Z:.0f} mm (8-15, clears flooring and lets air pass)")
lever = room["lever_room"].bounding_box()
check(900 <= lever.center().Z <= 1100, f"lever at {lever.center().Z:.0f} mm (900-1100)")
# leaf swings into the room about the hinge edge: keep the arc off the pulled-out chairs
chairs = read_step("STEP/dining_set.step")
cb = chairs.bounding_box()
PULL = 350.0  # chair pulled out to sit (MEMO: ~350 mm)
hinge_x, hinge_y, swing = lb.max.X, lb.min.Y, lb.size.X
# chairs on the door side, pulled out toward the door wall
seats = [c.bounding_box() for c in chairs.children if c.label.startswith("chair")]
near = [b for b in seats if b.center().Y > 0]
chair_x = max(b.max.X for b in near)
chair_xmin = min(b.min.X for b in near)
chair_y = cb.max.Y + PULL
check(chair_xmin > hinge_x or (hinge_x - swing) > chair_x or hinge_y - swing > chair_y,
      f"door swing ({hinge_x - swing:.0f}..{hinge_x:.0f}) clears pulled-out chairs (x {chair_xmin:.0f}..{chair_x:.0f})")

# sideboard
WALNUT, HARDBOARD = 640e-9, 900e-9
sb_kg = sum(v.volume * (HARDBOARD if k == "back" else WALNUT) for k, v in board.items() if not k.startswith(("knob", "anti")))
bb = bd.Compound(children=[v for k, v in board.items() if not k.startswith(("knob", "anti"))]).bounding_box()
legs = [v.bounding_box() for k, v in board.items() if k.startswith("leg")]
front_y = min(l.min.Y for l in legs)  # model front is -Y
arm = bb.center().Y - front_y
free_tip = sb_kg * G * arm / bb.size.Z
angles = [k for k in board if k.startswith("anti_tip")]
# tipping about the front feet pulls the angles' wall screws straight out, at the top height
tip_force = sb_kg * G * arm / bb.size.Z + len(angles) * 150
check(tip_force >= 300, f"sideboard {sb_kg:.0f} kg: tips with {free_tip:.0f} N at the top front unstrapped, "
      f"{tip_force:.0f} N with {len(angles)} anti-tip angles (>=300 N, assumed: an adult leaning on it)")
span = 1600 / 3  # between dividers
E_WAL = 11600.0
I = 420 * 18**3 / 12  # full-depth panel, mm^4
defl = 25 * G * span**3 / (48 * E_WAL * I)
check(defl < span / 300, f"top panel under 25 kg mid-span: {defl:.2f} mm (< L/300 = {span / 300:.1f})")
sb_front = (to_wall * bd.Compound(children=list(board.values()))).bounding_box().max.Y
south_chairs = [b for b in seats if b.center().Y < 0]
chair_back = min(b.min.Y for b in south_chairs) - PULL
check(chair_back - sb_front >= 400, f"pulled-out chair to sideboard {chair_back - sb_front:.0f} mm (>=400 to squeeze past)")

# decor
pic_kg = decor["frame"].volume * 500e-9 + decor["backing"].volume * HARDBOARD + decor["mat"].volume * 700e-9
check(pic_kg * 3 <= 10, f"picture {pic_kg:.2f} kg on a 10 kg hook (3x)")
v = decor["vase"]
vb = v.bounding_box()
base_r = 150 / 2 * 0.55
com_h = v.center(bd.CenterOf.MASS).Z - vb.min.Z
tilt = math.degrees(math.atan(base_r / com_h))
check(tilt >= 15, f"vase {v.volume * 2300e-9:.2f} kg tips at {tilt:.0f} deg (>=15)")

print("ALL PASS" if not fails else f"{len(fails)} FAIL")
