"""Fit check for STEP/dining_set.step: chairs tucked under the table must not
collide with it or each other, and must clear the aprons (run from CAD root)."""
import itertools

from cadgen import build123d as bd
from cadgen import read_step

TABLE_PARTS, CHAIR_PARTS = 9, 11
solids = read_step("STEP/dining_set.step").solids()
assert len(solids) == TABLE_PARTS + 4 * CHAIR_PARTS, len(solids)
groups = [solids[:TABLE_PARTS]] + [solids[TABLE_PARTS + i * CHAIR_PARTS:TABLE_PARTS + (i + 1) * CHAIR_PARTS] for i in range(4)]
tbb = bd.Compound(children=groups[0]).bounding_box()
assert round(tbb.size.Z) == 730 and tbb.size.X > 1000, "first group is not the table"
names = ["table"] + [f"chair_{i}" for i in range(1, 5)]
fails = []


def check(ok, msg):
    print(("PASS " if ok else "FAIL ") + msg)
    if not ok:
        fails.append(msg)


for (i, ga), (j, gb) in itertools.combinations(enumerate(groups), 2):
    overlap = sum(sum(s.volume for s in c.solids()) for a in ga for b in gb if a.distance_to(b) < 1e-6 and (c := a & b))
    gap = min(a.distance_to(b) for a in ga for b in gb)
    if i == 0 or gap < 200:
        check(overlap < 1.0 and gap > 0.5, f"{names[i]} vs {names[j]}: overlap {overlap:.2f} mm^3, closest gap {gap:.1f} mm")

print("ALL PASS" if not fails else f"{len(fails)} FAIL")
