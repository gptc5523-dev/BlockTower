"""Physical sanity checks for STEP/pendant_light.step (run from the CAD project root).

Materials: canopy steel 1.5 mm pressed (modeled solid, mass taken as a shell
estimate), cord PVC/fabric with copper, socket phenolic, ring brass, shade
aluminium 2700 kg/m^3, bulb ~30 g. Load rule of thumb: a fabric pendant cord
with a strain-relief grip carries ~3 kg (unverified, typical retailer figure);
we ask for a 5x margin on the hanging weight. Static estimates, not a
certification test.
"""
import itertools
import math

from cadgen import read_step

G = 9.81
CEILING, TABLE_H, TABLE_W = 2400.0, 730.0, 900.0  # src/room.py, src/table.py
SEATED_EYE, EYE_REACH = 1200.0, 750.0  # assumed: seated eye height, horizontal distance to the lamp axis
CORD_LIMIT = 3.0  # kg

lamp = read_step("STEP/pendant_light.step")
solids = lamp.solids()
fails = []


def check(ok, msg):
    print(("PASS " if ok else "FAIL ") + msg)
    if not ok:
        fails.append(msg)


worst = max(
    (sum(s.volume for s in (a & b).solids()) if (a & b) else 0.0)
    for a, b in itertools.combinations(solids, 2)
)
check(worst <= 1.0, f"no interpenetration (worst overlap {worst:.2f} mm^3)")
touch = {i: {j for j, b in enumerate(solids) if j != i and a.distance_to(b) < 0.05} for i, a in enumerate(solids)}
seen, todo = set(), [0]
while todo:
    k = todo.pop()
    if k not in seen:
        seen.add(k)
        todo += touch[k]
check(len(seen) == len(solids), f"all {len(solids)} parts connected by contact")

bb = lamp.bounding_box()
rim_floor = CEILING + bb.min.Z
check(bb.max.Z <= 0.01, "nothing above the ceiling plane")
check(750 <= rim_floor - TABLE_H <= 900, f"shade rim {rim_floor - TABLE_H:.0f} mm above the table (750-900)")
check(bb.size.X < TABLE_W - 300, f"shade {bb.size.X:.0f} mm across stays inside the table footprint")

shade = max(solids, key=lambda s: s.bounding_box().size.X)
bulb = min(solids, key=lambda s: s.bounding_box().min.Z if s is not shade else 1e9)
rim_r = shade.bounding_box().size.X / 2
bulb_bottom = CEILING + bulb.bounding_box().min.Z
# sight line from a seated eye to the bulb's lowest point, height where it crosses the rim radius
cross = SEATED_EYE + (bulb_bottom - SEATED_EYE) * (EYE_REACH - rim_r) / EYE_REACH
check(cross < rim_floor, f"rim hides the bulb from a seated eye (sight line {cross:.0f} < rim {rim_floor:.0f} mm)")

shade_kg = shade.volume * 2700e-9
hanging = shade_kg + 0.08 + 0.03 + 0.02  # + socket, bulb, ring
check(hanging * 5 <= CORD_LIMIT, f"hanging mass {hanging:.2f} kg, cord margin {CORD_LIMIT / hanging:.1f}x (>=5x)")

# shade on the ring: brass ring bearing area vs aluminium bearing strength (~100 MPa conservatively)
ring_area = math.pi / 4 * (56.0**2 - 41.0**2)
stress = shade_kg * G / ring_area
check(stress < 1.0, f"shade-on-ring bearing stress {stress * 1000:.1f} kPa")

print("ALL PASS" if not fails else f"{len(fails)} FAIL")
