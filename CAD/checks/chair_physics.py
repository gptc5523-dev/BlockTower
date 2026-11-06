"""Physical sanity checks for STEP/chair.step (run from the CAD project root).

Material: European beech, density 720 kg/m^3, bending strength ~100 MPa, E ~14 GPa.
Loads are simple static cases, not a certification test.
"""
import itertools
import math

from cadgen import read_step

G = 9.81
RHO = 720e-9  # kg/mm^3
MOR = 100.0  # MPa
E = 14000.0  # MPa
SITTER = 100 * G  # N, 100 kg occupant
EN_SEAT, EN_BACK = 600.0, 80.0  # N, EN 1022:2018 rearward overbalancing (backrest >= 720 mm)
LEAN = 100 * G * 0.25  # N, informative only: 100 kg sitter pushing back with a quarter of body weight
EDGE = 600.0  # N, someone sitting on the very edge

chair = read_step("STEP/chair.step")
parts = chair.solids()
names = [p.label or f"part{i}" for i, p in enumerate(parts)]
fails = []


def check(ok, msg):
    print(("PASS " if ok else "FAIL ") + msg)
    if not ok:
        fails.append(msg)


# 1. No two parts occupy the same space; everything is connected.
worst = 0.0
for (i, a), (j, b) in itertools.combinations(enumerate(parts), 2):
    common = a & b
    vol = sum(s.volume for s in common.solids()) if common else 0.0
    worst = max(worst, vol)
    if vol > 1.0:
        print(f"     overlap {names[i]} / {names[j]}: {vol:.1f} mm^3")
check(worst <= 1.0, f"no interpenetration (worst overlap {worst:.2f} mm^3)")
touch = {i: {j for j, b in enumerate(parts) if j != i and a.distance_to(b) < 0.05} for i, a in enumerate(parts)}
seen, todo = set(), [0]
while todo:
    k = todo.pop()
    if k not in seen:
        seen.add(k)
        todo += touch[k]
check(len(seen) == len(parts), f"all {len(parts)} parts connected by contact")

# 2. Chair mass and centre of mass.
mass = sum(p.volume for p in parts) * RHO
com = sum((p.center() * p.volume for p in parts), start=parts[0].center() * 0) / sum(p.volume for p in parts)
print(f"     mass {mass:.1f} kg, centre of mass y={com.Y:.0f} z={com.Z:.0f} mm")

# 3. Stability, tipping about the foot edges.
bb = chair.bounding_box()
feet = [f for f in chair.faces() if abs(f.center().Z) < 1e-6 and f.normal_at().Z < -0.99]
check(len(feet) == 4, f"4 feet on the floor ({len(feet)})")
front_pivot = min(f.bounding_box().min.Y for f in feet)
rear_pivot = max(f.bounding_box().max.Y for f in feet)
side_pivot = max(f.bounding_box().max.X for f in feet)
seat_front, seat_side, top = bb.min.Y, bb.max.X, bb.max.Z
Wc = mass * G

sitter_y = 0.6 * 410  # assumed seat loading point / seated centre of mass
f_tip = (EN_SEAT * (rear_pivot - sitter_y) + Wc * (rear_pivot - com.Y)) / top
check(f_tip > EN_BACK, f"rearward (EN 1022 loads): tips at {f_tip:.0f} N on backrest top, test applies {EN_BACK:.0f} N")
f_lean = (SITTER * (rear_pivot - sitter_y) + Wc * (rear_pivot - com.Y)) / top
print(f"     info: 100 kg sitter tips it back with {f_lean:.0f} N at the backrest top ({f_lean / G:.0f} kgf)")
load_y = seat_front + 60
m_fwd = Wc * (com.Y - front_pivot) - EDGE * (front_pivot - load_y)
check(m_fwd > 0, f"forward: {EDGE:.0f} N at 60 mm from front edge ({load_y - front_pivot:.0f} mm behind "
      f"the front feet), restoring moment {m_fwd / 1000:.1f} N*m")
load_x = seat_side - 60
m_side = Wc * side_pivot - EDGE * (load_x - side_pivot)
check(m_side > 0, f"sideways: {EDGE:.0f} N at 60 mm from side edge, restoring moment {m_side / 1000:.1f} N*m")

# 4. Strength. Rear leg bend (40x40) under the backrest push, shared by two legs.
bend_z, push_z = 412.0, top - 50
M = 450.0 * (push_z - bend_z) / 2  # 450 N backrest static strength load
S = 40 * 40**2 / 6
sigma = M / S
check(sigma * 3 < MOR, f"rear leg bend stress {sigma:.1f} MPa, safety factor {MOR / sigma:.1f} (needs > 3)")
# Grain runout at the bend: grain follows the bisector of the two leg directions.
leg_tilt = math.degrees(math.atan(70 / 412))
post_tilt = 102 + 4 - 90
runout = (leg_tilt + post_tilt) / 2
check(runout < 15, f"grain runout at bend {runout:.1f} deg (board-cut leg, keep < 15)")
# Seat board spanning between side aprons, sitter as point load.
span, width, t = 2 * 190 - 40, 410, 25
I = width * t**3 / 12
sig_seat = SITTER * 1.3 * span / 4 / (width * t**2 / 6)
defl = SITTER * 1.3 * span**3 / (48 * E * I)
check(sig_seat * 3 < MOR, f"seat board {sig_seat:.1f} MPa, deflection {defl:.2f} mm at 1.3x sitter load")
# Front leg buckling (pinned-pinned Euler), half the sitter each.
L, I_leg = 400, 40**4 / 12
p_cr = math.pi**2 * E * I_leg / L**2
check(p_cr > 10 * SITTER / 2, f"front leg buckling load {p_cr / 1000:.0f} kN vs {SITTER / 2 / 1000:.2f} kN")

print("ALL PASS" if not fails else f"{len(fails)} FAIL")
