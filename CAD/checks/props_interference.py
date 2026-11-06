"""Parts of each room prop must only touch: no pair overlaps by more than a sliver (mm^3)."""
from itertools import combinations

from cadgen import read_step

for name in ("potted_plant", "snake_plant", "succulent", "wall_clock", "bowl", "sideboard", "picture_frame"):
    # parts are the labelled children; one part may hold several solids (a plant's stems and leaves)
    parts = list(read_step(f"STEP/{name}.step").children)
    worst = 0.0
    for a, b in combinations(parts, 2):
        common = a.intersect(b)
        v = sum(s.volume for s in common) if common else 0.0
        worst = max(worst, v)
    bb = read_step(f"STEP/{name}.step").bounding_box()
    print(f"{name}: {len(parts)} parts, worst overlap {worst:.3f} mm^3, size {bb.size.X:.0f} x {bb.size.Y:.0f} x {bb.size.Z:.0f}")
    assert worst < 1.0, name
