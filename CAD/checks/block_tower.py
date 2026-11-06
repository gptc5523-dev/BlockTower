"""Physics check for the block tower in STEP/game_set.step (run from CAD root).

Checks the tower stands on the table without overlap, then estimates the push
force needed to slide a block out, which sets the game's force scale.
Levels alternate maple 705 / walnut 610 kg/m^3; wood-on-wood static friction mu = 0.4 (sanded hardwood).
"""
import sys

sys.path.insert(0, "src")
from cadgen import read_step  # noqa: E402

from game_set import LEVELS  # noqa: E402
from tower_block import PER_LEVEL, RHO_MAPLE, RHO_WALNUT, THICK  # noqa: E402
from table import HEIGHT  # noqa: E402

G, MU = 9.81, 0.4  # m/s^2, -
TABLE_PARTS, CHAIR_PARTS = 9, 11
fails = []


def check(ok, msg):
    print(("PASS " if ok else "FAIL ") + msg)
    if not ok:
        fails.append(msg)


solids = read_step("STEP/game_set.step").solids()
n_set = TABLE_PARTS + 4 * CHAIR_PARTS
blocks = solids[n_set:]
top = solids[0]
check(len(blocks) == PER_LEVEL * LEVELS, f"{len(blocks)} blocks ({LEVELS} levels x {PER_LEVEL})")

# overlap between touching blocks (faces coplanar, so test volume of the intersection)
worst = 0.0
for i, a in enumerate(blocks):
    for b in blocks[i + 1:]:
        if a.distance_to(b) < 1e-6 and (c := a & b):
            worst = max(worst, sum(s.volume for s in c.solids()))
check(worst < 1.0, f"no block overlap (worst {worst:.2f} mm^3)")

bb = [s.bounding_box() for s in blocks]
check(abs(min(b.min.Z for b in bb) - HEIGHT) < 0.01 and top.bounding_box().max.Z == HEIGHT,
      f"bottom level sits on the table top (z = {HEIGHT:.0f})")
check(all(abs(b.min.Z - (HEIGHT + (i // PER_LEVEL) * THICK)) < 0.01 for i, b in enumerate(bb)),
      "every level rests on the one below (no gaps)")
check(top.distance_to(blocks[0]) < 1e-6, "tower touches the table")

rho = [RHO_MAPLE if (i // PER_LEVEL) % 2 == 0 else RHO_WALNUT for i in range(len(blocks))]
m = sum(s.volume * r for s, r in zip(blocks, rho))
mb = m / len(blocks)  # mean
com = sum((s.center() * (s.volume * r) for s, r in zip(blocks[1:], rho[1:])), blocks[0].center() * (blocks[0].volume * rho[0])) / m
check(abs(com.X) < 1 and abs(com.Y) < 1, f"tower centre of mass over the base centre ({com.X:.2f}, {com.Y:.2f})")
print(f"     block {mb * 1000:.1f} g (mean), tower {m * 1000:.0f} g, {LEVELS * THICK:.0f} mm tall, top at {HEIGHT + LEVELS * THICK:.0f} mm")

# Push force to slide the middle block of a level: friction on its top and bottom faces.
# Load on it = 1/PER_LEVEL of everything above (nominal blocks share evenly).
print("     push force to slide a middle block (mu 0.4, even load sharing):")
for level in (1, LEVELS // 2, LEVELS - 2):  # level 1 = bottom
    w_above = (LEVELS - level) * PER_LEVEL * mb * G
    f = MU * (2 * w_above / PER_LEVEL + mb * G)
    print(f"       level {level:2d}: {f:.2f} N")
print("     with +/-0.3 mm thickness variation a 0.3 mm thinner block carries ~0 load -> ~0.08 N (loose)")
print("ALL PASS" if not fails else f"{len(fails)} FAIL")
