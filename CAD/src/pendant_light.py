"""Pendant light hung over the dining table, as separate parts that only touch.
Units: mm, origin at the ceiling underside on the fixture axis, Z up (hangs into -Z).

Dimension rationale:
- Shade rim 850 below a 2400 ceiling = 1550 above the floor, 820 above the
  730 table: inside the usual 750-900 band over a dining table, and the
  rim still hides the bulb from a seated eye (~1200) at the table edge.
- Shade 380 across: well inside the 900 table width, so no one standing
  beside the table walks into it.
- Spun aluminium shade 0.7 mm, E27 A60 bulb, fabric cord Ø6.

Construction: the canopy screws to the ceiling box; the cord hangs from it
into the socket. The shade's top hole (Ø41) slips over the socket (Ø40) and
rests on the socket's lip ring; the bulb's neck butts the socket's underside
(thread engagement not modeled).
"""

import math

from cadgen import build123d as bd
from cadgen import glb, srgb, step

CANOPY_D, CANOPY_H = 120.0, 25.0
CORD_D = 6.0
SOCKET_D, SOCKET_H = 40.0, 60.0
SOCKET_TOP = -600.0
RING_D, RING_H = 56.0, 4.0
SHADE_RIM_Z = -850.0
SHADE_H = 216.0  # rim to top-cap underside
SHADE_RIM_D, SHADE_TOP_D = 380.0, 62.0
SHADE_T = 0.7
SHADE_HOLE_D = 41.0
NECK_D, NECK_H = 27.0, 25.0
GLOBE_D = 60.0

SOCKET_BOTTOM = SOCKET_TOP - SOCKET_H
SHADE_TOP_Z = SOCKET_TOP - 30.0 - RING_H  # cap underside, resting on the ring top


def at(z, solid):
    """Places a solid built upward from z=0 so its base sits at z"""
    return bd.Pos(0, 0, z) * solid


def column(d, h):
    return bd.Cylinder(d / 2, h, align=(bd.Align.CENTER, bd.Align.CENTER, bd.Align.MIN))


def shade():
    """Spun cone: wall of normal thickness SHADE_T, flat top cap with the socket hole"""
    r0, r1, h, t = SHADE_RIM_D / 2, SHADE_TOP_D / 2, SHADE_H, SHADE_T
    d = t * math.hypot(r0 - r1, h) / h  # horizontal width of the sloped wall
    profile = bd.Polygon(
        (r0, 0), (r1, h + t), (SHADE_HOLE_D / 2, h + t), (SHADE_HOLE_D / 2, h),
        (r1 - d + (r0 - r1) * t / h, h), (r0 - d, 0), align=None,
    )
    return at(SHADE_RIM_Z, bd.revolve(bd.Plane.XZ * profile, bd.Axis.Z))


def bulb():
    neck = at(SOCKET_BOTTOM - NECK_H, column(NECK_D, NECK_H))
    globe = bd.Pos(0, 0, SOCKET_BOTTOM - NECK_H - GLOBE_D / 2 + 5) * bd.Sphere(GLOBE_D / 2)
    return neck + globe


def pendant_parts():
    canopy = bd.fillet(at(-CANOPY_H, column(CANOPY_D, CANOPY_H)).edges().group_by(bd.Axis.Z)[0], 6)
    cord = at(SOCKET_TOP, column(CORD_D, -SOCKET_TOP - CANOPY_H))
    socket = at(SOCKET_BOTTOM, column(SOCKET_D, SOCKET_H))
    ring = at(SHADE_TOP_Z - RING_H, column(RING_D, RING_H)) - at(SHADE_TOP_Z - RING_H, column(SOCKET_D, RING_H))
    parts = [canopy, cord, socket, ring, shade(), bulb()]
    colors = ["#F2F0EB", "#2B2B2B", "#3A3A3A", "#B08D57", "#1F3B33", "#FFF4DC"]
    for name, part, c in zip(["canopy", "cord", "socket", "socket_ring", "shade", "bulb"], parts, colors):
        part.label = name
        part.color = srgb(c)
    return parts


@glb(out="../GLB/pendant_light.glb")
@step(out="../STEP/pendant_light.step")
def pendant_light():
    body = bd.Compound(children=pendant_parts())
    body.label = "pendant_light"
    return body


if __name__ == "__main__":
    pendant_light()
