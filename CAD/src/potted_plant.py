"""Floor plant for the dining-room corners, a peace lily (Spathiphyllum), as separate parts that only touch:
saucer, terracotta pot, soil, leaves, flowers.
Units: mm, origin at the floor under the saucer centre, Z up.

Saucer: Ø340, 8 mm floor, 30 high. Pot: turned terracotta standing in it, Ø280 foot widening to Ø400 under a
20 mm rolled rim, 380 high, 12 mm walls, 15 mm floor (drain hole not modeled); soil fills it to 40 below the rim.
Leaves: 30, set round the crown at the golden angle (137.5 deg) with a little irregularity, like a real clump:
the old outer ones low and spread, the young inner ones tall. Each is a thin round petiole leaving the soil at a
lean and bowing up and out, carrying a broad elliptic blade (length x 0.36-0.42) with a drawn-out tip, 1 mm thick,
cupped in a smooth trough with the midrib proud underneath, turned a little about its own length, that leaves the
petiole climbing and arches over. Two young leaves are still rolled into spears in the centre.
Flowers: 3 white spathes, broad cupped hoods leaning forward and facing out, on tall stalks above the leaves,
each with a cream spadix standing in the cup. Leaves, spathes and spadices are separate parts that only touch.
About 1150 tall, 1100 across.
"""

import math

from cadgen import build123d as bd
from cadgen import glb, srgb, step

POT_R0, POT_R1, POT_H, WALL, FLOOR = 140.0, 200.0, 380.0, 12.0, 15.0
RIM_H, RIM_OUT = 20.0, 10.0
SOIL_BELOW_RIM = 40.0
SAUCER_R, SAUCER_H, SAUCER_T = 170.0, 30.0, 8.0
PETIOLE_R, BLADE_T = 3.5, 1.5
GOLDEN = 137.5

TERRACOTTA, SOIL, LEAF, SPATHE, SPADIX = "#B5653F", "#3B2A1E", "#2F5A2E", "#F2F0E6", "#E3D6A4"


def revolved(points):
    """A solid of revolution about Z from a closed (r, z) outline"""
    return bd.revolve(bd.Plane.XZ * bd.make_face(bd.Polyline(*points, close=True)), bd.Axis.Z)


def pot(r0, r1, h, wall, floor, rim_h, rim_out, fill_below_rim):
    """A tapered pot open at the top and the fill inside it, touching its inside: (pot, fill, fill top)"""
    outer = revolved([(0, 0), (r0, 0), (r1, h - rim_h), (r1 + rim_out, h - rim_h), (r1 + rim_out, h), (0, h)])
    slope = (r1 - r0) / (h - rim_h)
    # the inside follows the outside taper, `wall` in from it
    cavity = revolved([(0, floor), (r0 - wall + slope * floor, floor), (r1 - wall + slope * rim_h, h), (0, h)])
    top = h - fill_below_rim
    fill = cavity - bd.Pos(0, 0, top + h) * bd.Box(4 * r1, 4 * r1, 2 * h)
    return outer - cavity, fill, top


def direction(heading, elev):
    h, e = math.radians(heading), math.radians(elev)
    return bd.Vector(math.cos(e) * math.cos(h), math.cos(e) * math.sin(h), math.sin(e))


def lance(t):
    """Generic lance outline, half width as a share of the full: narrow base, widest a third up, long point"""
    return math.sin(math.pi * t ** 0.8) ** 0.75


def lily(t):
    """Peace-lily blade: a short wedge base, broad elliptic middle (widest just below half), drawn-out tip"""
    return math.sin(math.pi * t ** 0.9) ** 0.55 * (1 - 0.25 * t ** 4)


def spathe_shape(t):
    """Spathe: narrow claw at the stalk, broad ovate hood, short point"""
    return math.sin(math.pi * t ** 1.2) ** 0.6


def folded(w, fold, thick):
    """Blade cross-section: a shallow V up from the midrib, `thick` deep"""
    return [(-w, fold * w), (0, 0), (w, fold * w), (w, fold * w + thick), (0, thick), (-w, fold * w + thick)]


ACROSS = (-1.0, -0.7, -0.35, 0.0, 0.35, 0.7, 1.0)


def arched(w, fold, thick):
    """Blade cross-section: a smooth trough (sides rising as the square of the distance out) `thick` deep, with
    the midrib standing proud underneath"""
    rib = min(1.2, 0.05 * w)
    bottom = [(x * w, fold * w * x * x - (rib if x == 0 else 0)) for x in ACROSS]
    top = [(x * w, fold * w * x * x + thick) for x in reversed(ACROSS)]
    return bottom + top


def blade(length, width, elev, droop, fold, thick=BLADE_T, outline=lance, section=folded, n=12, twist=0.0):
    """A leaf lofted through cross-sections along an arc in the XZ plane, leaving the origin along +X `elev` deg
    above horizontal and turning down by `droop` deg over its length (negative curls it up), its sections turning
    `twist` deg about the leaf's length from base to tip"""
    faces, pos = [], bd.Vector(0, 0, 0)
    for i in range(n + 1):
        t = i / n
        a = math.radians(elev - droop * t)
        tangent = bd.Vector(math.cos(a), 0, math.sin(a))
        w = max(width / 2 * outline(t), 0.6)
        phi = math.radians(twist * t)
        across = bd.Vector(0, math.cos(phi), 0) + bd.Vector(-math.sin(a), 0, math.cos(a)) * math.sin(phi)
        plane = bd.Plane(origin=pos, x_dir=across, z_dir=tangent)
        faces.append(plane * bd.make_face(bd.Polyline(*section(w, fold, thick), close=True)))
        pos = pos + tangent * (length / n)
    return bd.loft(faces)


def stalk(start, end, end_dir, r=PETIOLE_R, lean=0.0):
    """A round stalk swept from `start` to `end`, arriving along `end_dir`. It leaves the soil `lean` (0..1) of the
    way from upright toward `end`, bowing up through a midpoint like a real petiole"""
    out = (end - start)
    flat = bd.Vector(out.X, out.Y, 0)
    up = (bd.Vector(0, 0, 1) + flat.normalized() * lean).normalized() if flat.length else bd.Vector(0, 0, 1)
    mid = start + bd.Vector(out.X * 0.35, out.Y * 0.35, out.Z * 0.62)
    path = bd.Spline(start, mid, end, tangents=(up, end_dir))
    return bd.sweep(bd.Plane(origin=start, z_dir=up) * bd.Circle(r), path=path)


def jitter(i, k):
    """Repeatable 0..1 variation for leaf i, quantity k (no random module: the model must build the same)"""
    return (math.sin(i * 12.9898 + k * 78.233) * 43758.5453) % 1.0


def leaves_and_flowers(soil_top):
    leaves, flowers, spadices = [], [], []
    n = 30
    for i in range(n):
        heading = i * GOLDEN + 25 * (jitter(i, 1) - 0.5)
        age = 1 - i / n  # the first are the old outer leaves: lower, wider, more spread
        hx, hy = math.cos(math.radians(heading)), math.sin(math.radians(heading))
        r0 = 8 + 40 * age * (0.6 + 0.4 * jitter(i, 2))
        s = bd.Vector(hx * r0, hy * r0, soil_top)
        reach = 60 + 190 * age + 40 * jitter(i, 3)
        rise = 260 + 260 * (1 - age) + 80 * jitter(i, 4)
        elev = 22 + 48 * (1 - age) + 10 * jitter(i, 5)
        droop = 45 + 55 * age + 20 * jitter(i, 6)
        end = s + bd.Vector(hx * reach, hy * reach, rise)
        leaves.append(stalk(s, end, direction(heading, elev), r=2.6, lean=0.35 + 0.4 * age))
        length = 230 + 90 * jitter(i, 7) + 40 * age
        width = length * (0.36 + 0.06 * jitter(i, 8))
        b = blade(length, width, elev, droop, 0.22, thick=1.0, outline=lily, section=arched, n=14)
        roll = 25 * (jitter(i, 9) - 0.5)  # each blade turned a little about its own length
        leaves.append(bd.Pos(end.X, end.Y, end.Z) * bd.Rot(0, 0, heading) * bd.Rot(roll, 0, 0) * b)
    # two young leaves still rolled up, spearing out of the crown
    for k, heading in enumerate((60, 230)):
        hx, hy = math.cos(math.radians(heading)), math.sin(math.radians(heading))
        s = bd.Vector(hx * 6, hy * 6, soil_top)
        end = s + bd.Vector(hx * 25, hy * 25, 330 + 60 * k)
        leaves.append(stalk(s, end, direction(heading, 82), r=2.6))
        leaves.append(bd.Pos(end.X, end.Y, end.Z) * bd.Rot(0, 0, heading) *
                      blade(170 + 30 * k, 22, 82, 8, 1.6, thick=1.2, outline=lance, section=arched))
    # flowers: tall stalks above the leaves; the spathe is turned to face out, its hood leaning forward
    for k, heading in enumerate((15, 140, 255)):
        face = heading + 180
        hx, hy = math.cos(math.radians(heading)), math.sin(math.radians(heading))
        s = bd.Vector(hx * 22, hy * 22, soil_top)
        end = s + bd.Vector(hx * 70, hy * 70, 600 + 50 * k)
        e = 78
        leaves.append(stalk(s, end, direction(face, e), r=2.4, lean=0.2))
        flowers.append(bd.Pos(end.X, end.Y, end.Z) * bd.Rot(0, 0, face) *
                       blade(125, 78, e, 40, 0.7, thick=1.0, outline=spathe_shape, section=arched, n=14))
        # the spadix stands in the cup: from the stalk's end face, 1 mm clear of the spathe's inner face
        up = bd.Vector(math.sin(math.radians(e)) * hx, math.sin(math.radians(e)) * hy, math.cos(math.radians(e)))
        base = end + direction(face, e) * 14 + up * (1.0 + 3.0 + 1.5)
        spadices.append(bd.Pos(base.X, base.Y, base.Z) * bd.Rot(0, 0, face) * bd.Rot(0, 90 - e + 8, 0) *
                        (bd.Cylinder(3.0, 42, align=(bd.Align.CENTER, bd.Align.CENTER, bd.Align.MIN)) +
                         bd.Pos(0, 0, 42) * bd.Sphere(3.0)))
    soil = bd.Pos(0, 0, soil_top - 500) * bd.Box(2000, 2000, 1000)
    leaves = [leaf - soil for leaf in leaves]  # a stalk leaving the soil at a lean dips its end into it
    return bd.Compound(children=leaves), bd.Compound(children=flowers), bd.Compound(children=spadices)


def plant_parts():
    saucer = revolved([(0, 0), (SAUCER_R - 10, 0), (SAUCER_R, SAUCER_H), (SAUCER_R - 6, SAUCER_H),
                       (SAUCER_R - 15, SAUCER_T), (0, SAUCER_T)])
    body, soil, top = pot(POT_R0, POT_R1, POT_H, WALL, FLOOR, RIM_H, RIM_OUT, SOIL_BELOW_RIM)
    lift = bd.Pos(0, 0, SAUCER_T)
    leaves, flowers, spadices = leaves_and_flowers(top)
    return [("saucer", saucer, TERRACOTTA), ("pot", lift * body, TERRACOTTA), ("soil", lift * soil, SOIL),
            ("leaves", lift * leaves, LEAF), ("flowers", lift * flowers, SPATHE), ("spadices", lift * spadices, SPADIX)]


@glb(out="../GLB/potted_plant.glb")
@step(out="../STEP/potted_plant.step")
def potted_plant():
    parts = []
    for name, part, color in plant_parts():
        part.label = name
        part.color = srgb(color)
        parts.append(part)
    body = bd.Compound(children=parts)
    body.label = "potted_plant"
    return body


if __name__ == "__main__":
    potted_plant()
