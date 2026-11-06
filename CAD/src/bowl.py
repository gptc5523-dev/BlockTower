"""Turned wooden bowl for the sideboard top, one part.
Units: mm, origin at the bottom centre, Z up.

Ø300 x 90, walnut, 8 mm walls following a soft curve from a Ø120 foot to a slightly flared rim; the foot is
turned hollow 3 mm so the bowl stands on its 10 mm foot ring, as turned bowls do.
"""

from cadgen import build123d as bd
from cadgen import glb, srgb, step

D, H, WALL, FOOT_D, FOOT_RING, FOOT_RECESS = 300.0, 90.0, 8.0, 120.0, 10.0, 3.0

WALNUT = "#6E4A30"


def bowl_part():
    r, f = D / 2, FOOT_D / 2
    side = bd.Spline((f, 0), (r * 0.78, H * 0.35), (r * 0.97, H * 0.8), (r, H))
    outline = bd.Polyline((0, H), (0, 0), (f, 0)) + side + bd.Line((r, H), (0, H))
    solid = bd.revolve(bd.Plane.XZ * bd.make_face(outline), bd.Axis.Z)
    body = bd.offset(solid, -WALL, openings=solid.faces().sort_by(bd.Axis.Z)[-1])  # open at the rim
    recess = bd.Pos(0, 0, FOOT_RECESS / 2) * bd.Cylinder(f - FOOT_RING, FOOT_RECESS)
    return body - recess


@glb(out="../GLB/bowl.glb")
@step(out="../STEP/bowl.step")
def bowl():
    body = bowl_part()
    body.label = "bowl"
    body.color = srgb(WALNUT)
    return body


if __name__ == "__main__":
    bowl()
