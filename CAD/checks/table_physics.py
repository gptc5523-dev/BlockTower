"""Physical sanity checks for STEP/table.step (run from the CAD project root).

Material: European beech, density 720 kg/m^3, bending strength ~100 MPa, E ~14 GPa.
Loads follow published EN 12521 / EN 1730 domestic-table figures where found:
1000 N vertical on the top, 400 N horizontal with 50 kg on the top. The 400 N
edge stability load is from an older template and unverified. Static hand
calculations, not a certification test.
"""
import itertools

from cadgen import read_step

G = 9.81
RHO = 720e-9  # kg/mm^3
MOR, E = 100.0, 14000.0  # MPa
V_LOAD, H_LOAD, H_BALLAST, EDGE_LOAD = 1000.0, 400.0, 50 * G, 400.0
SITTER = 75 * G  # someone sitting on the table end, informative
# From src/chair.py and src/table.py
CHAIR_SEAT, CHAIR_WIDTH, THIGH = 440.0, 450.0, 175.0  # THIGH: assumed large-adult thigh thickness
TOP_T, LEG, APRON_H, APRON_T = 30.0, 60.0, 60.0, 22.0
MOVE_COEF, DMC = 0.0038, 4.0  # beech flat-sawn: width change per 1% moisture, seasonal swing in %

table = read_step("STEP/table.step")
parts = table.solids()
fails = []


def check(ok, msg):
    print(("PASS " if ok else "FAIL ") + msg)
    if not ok:
        fails.append(msg)


worst = max(
    (sum(s.volume for s in (a & b).solids()) if (a & b) else 0.0)
    for a, b in itertools.combinations(parts, 2)
)
check(worst <= 1.0, f"no interpenetration (worst overlap {worst:.2f} mm^3)")
touch = {i: {j for j, b in enumerate(parts) if j != i and a.distance_to(b) < 0.05} for i, a in enumerate(parts)}
seen, todo = set(), [0]
while todo:
    k = todo.pop()
    if k not in seen:
        seen.add(k)
        todo += touch[k]
check(len(seen) == len(parts), f"all {len(parts)} parts connected by contact")

bb = table.bounding_box()
L, W, H = bb.size.X, bb.size.Y, bb.size.Z
mass = sum(p.volume for p in parts) * RHO
Wt = mass * G
print(f"     size {L:.0f} x {W:.0f} x {H:.0f} mm, mass {mass:.1f} kg")
feet = [f for f in table.faces() if abs(f.center().Z) < 1e-6 and f.normal_at().Z < -0.99]
check(len(feet) == 4, f"4 feet on the floor ({len(feet)})")
foot_x = max(f.bounding_box().max.X for f in feet)  # outer foot edge, tipping pivot
foot_y = max(f.bounding_box().max.Y for f in feet)

# Ergonomics against the chair.
apron_bottom = H - TOP_T - APRON_H
check(270 <= H - CHAIR_SEAT <= 300, f"seat-to-top difference {H - CHAIR_SEAT:.0f} mm (270-300)")
check(apron_bottom - (CHAIR_SEAT + THIGH) >= 10, f"thigh clearance {apron_bottom - CHAIR_SEAT - THIGH:.0f} mm under apron")
between = 2 * (foot_x - LEG)
check(between >= 2 * CHAIR_WIDTH + 100, f"{between:.0f} mm between legs fits two chairs per side")
check(L / 2 >= 600, f"{L / 2:.0f} mm of edge per diner (>= 600)")

# Stability.
m_end = Wt * foot_x - EDGE_LOAD * (L / 2 - 50 - foot_x)
check(m_end > 0, f"{EDGE_LOAD:.0f} N at 50 mm from the end edge: restoring moment {m_end / 1000:.0f} N*m")
m_sit = Wt * foot_x - SITTER * (L / 2 - 150 - foot_x)
print(f"     info: 75 kg sitting on the end (150 mm in): restoring moment {m_sit / 1000:.0f} N*m "
      f"({'stays' if m_sit > 0 else 'tips'})")
f_tip = (Wt + H_BALLAST) * foot_y / H
check(f_tip >= H_LOAD, f"horizontal push across the width tips at {f_tip:.0f} N with 50 kg on top (test {H_LOAD:.0f} N)")

# Strength.
span = 2 * (foot_x - LEG)  # long apron, leg face to leg face
S_apron = APRON_T * APRON_H**2 / 6
sig_apron = V_LOAD * span / 4 / S_apron
d_apron = V_LOAD * span**3 / (48 * E * APRON_T * APRON_H**3 / 12)
check(sig_apron * 3 < MOR, f"long apron alone, {V_LOAD:.0f} N mid-span: {sig_apron:.1f} MPa (SF {MOR / sig_apron:.1f}), "
      f"{d_apron:.1f} mm (top adds stiffness, so real value is lower)")
top_span, strip = 2 * (foot_y - LEG / 2), 300  # across the width between long aprons, 300 mm strip
sig_top = V_LOAD * top_span / 4 / (strip * TOP_T**2 / 6)
d_top = V_LOAD * top_span**3 / (48 * E * strip * TOP_T**3 / 12)
check(d_top <= top_span / 300, f"top, {V_LOAD:.0f} N at centre: {sig_top:.1f} MPa, {d_top:.2f} mm (limit {top_span / 300:.1f})")
leg_m = H_LOAD / 4 * apron_bottom
sig_leg = leg_m / (LEG**3 / 6)
check(sig_leg * 3 < MOR, f"leg racking, {H_LOAD:.0f} N push: {sig_leg:.1f} MPa at the apron joint, "
      f"joint moment {leg_m / 1000:.0f} N*m per corner")

# Seasonal wood movement across the top's width.
move = W * MOVE_COEF * DMC
print(f"     top moves ~{move:.0f} mm across the width per season -> fix with slotted buttons, slot >= {move / 2 + 2:.0f} mm each way")

print("ALL PASS" if not fails else f"{len(fails)} FAIL")
