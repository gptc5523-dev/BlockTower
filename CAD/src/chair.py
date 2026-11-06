"""Ergonomic wooden side chair, modeled as separate parts. Units: mm,
X = width, -Y = front, +Y = back, Z = up.

Dimension rationale (anthropometric design rules):
- Seat height: fits the 5th-percentile female popliteal height + shoe, so feet stay flat
  and the underside of the thigh is not compressed.
- Seat depth: shorter than the 5th-percentile female buttock-popliteal length, so the
  front edge clears the back of the knee and the lower back reaches the backrest.
- Seat width: wider than the 95th-percentile female hip breadth plus clothing allowance.
- Seat tilt 4 deg back: keeps the pelvis from sliding forward.
- Seat-to-back angle 102 deg: opens the hip angle and reduces lumbar disc pressure.
- Lumbar rail center ~200 mm above the seat: supports the L3-L5 region.
- Back rails curved in plan to follow the back's horizontal curvature.

Construction (physically buildable): parts never share volume, they only touch.
Each rear leg is one board-cut piece whose bend sits at seat-top level, above the
apron joint, so the joint lands on straight grain. The seat is notched around the
rear legs. Front legs are straight so aprons meet flat faces. Aprons and back
rails butt against the legs; their mortise-and-tenon joints are not modeled.
"""

import math

from cadgen import build123d as bd
from cadgen import glb, step

SEAT_HEIGHT = 440.0  # front edge, top surface
SEAT_DEPTH = 410.0
SEAT_WIDTH = 450.0
SEAT_THICK = 25.0
SEAT_TILT = 4.0  # deg, rear lower
SEAT_OVERHANG = 15.0  # seat edge beyond the leg faces, sides and front
BACK_ANGLE = 102.0  # deg, between seat and backrest
LUMBAR_CENTER = 200.0  # vertical, above seat surface
LUMBAR_HEIGHT = 90.0
BACK_TOP = 450.0  # vertical, backrest top above seat surface
TOP_RAIL_HEIGHT = 80.0
RAIL_THICK = 18.0
RAIL_RADIUS = 600.0  # plan curvature of the back rails
LEG = 40.0  # leg section
REAR_FOOT = 30.0  # rear leg depth at the floor (side-profile taper)
REAR_SPLAY = 40.0  # rear foot offset behind the seat
BEND_RADIUS = 80.0  # rear leg bend, inside of the curve
APRON_HEIGHT = 80.0
APRON_THICK = 20.0
APRON_INSET = 5.0  # apron face set back from the leg face
EASE = 2.0  # edge rounding


def eased(part, r=EASE):
    return bd.fillet(part.edges(), r)


def rear_leg(x, foot, joint, top):
    """One-piece rear leg: side profile cut from a board, bent at seat-top level."""
    hf, hj, ht = REAR_FOOT / 2, LEG / 2, LEG / 2
    pts = [
        (foot.Y - hf, 0), (joint.Y - hj, joint.Z), (top.Y - ht, top.Z),
        (top.Y + ht, top.Z), (joint.Y + hj, joint.Z), (foot.Y + hf, 0),
    ]
    plane = bd.Plane(origin=(x, 0, 0), x_dir=(0, 1, 0), z_dir=(1, 0, 0))  # local (x, y) = (Y, Z)
    profile = plane * bd.Polygon(*pts, align=None)
    bend = [vtx for vtx in profile.vertices() if abs(bd.Vector(vtx).Z - joint.Z) < 1]
    profile = bd.fillet(bend, BEND_RADIUS)
    return eased(bd.extrude(profile, amount=LEG / 2, both=True))


def seat_panel():
    plan = bd.Rectangle(SEAT_WIDTH, SEAT_DEPTH)
    plan = bd.fillet(plan.vertices().sort_by(bd.Axis.Y)[:2], 40)
    panel = bd.extrude(plan, amount=SEAT_THICK)
    panel = bd.fillet(panel.edges().group_by(bd.Axis.Z)[-1], 8)  # rounded top edge
    return bd.fillet(panel.edges().group_by(bd.Axis.Z)[0], 2)


def curved_rail(span, height):
    """Back rail curved in plan, concave toward the sitter, centered on the post line."""
    ring = bd.Circle(RAIL_RADIUS + RAIL_THICK) - bd.Circle(RAIL_RADIUS)
    window = bd.Pos(0, RAIL_RADIUS) * bd.Rectangle(span, 3 * RAIL_THICK + span**2 / RAIL_RADIUS)
    sag = RAIL_RADIUS - math.sqrt(RAIL_RADIUS**2 - (span / 2) ** 2)
    arc = bd.Pos(0, -(RAIL_RADIUS + RAIL_THICK / 2) + sag / 2) * (ring & window)
    return bd.Pos(0, 0, -height / 2) * bd.extrude(arc, amount=height)


def chair_parts():
    a = math.radians(SEAT_TILT)
    theta = math.radians(BACK_ANGLE + SEAT_TILT - 90)  # backrest tilt from vertical
    u = bd.Vector(0, math.cos(a), -math.sin(a))  # along seat, front to back
    v = bd.Vector(0, math.sin(theta), math.cos(theta))  # up the backrest
    front_top = bd.Vector(0, 0, SEAT_HEIGHT)
    rear_top = front_top + u * SEAT_DEPTH

    def seat_bottom(y):
        return SEAT_HEIGHT - y * math.tan(a) - SEAT_THICK / math.cos(a)

    xl = SEAT_WIDTH / 2 - SEAT_OVERHANG - LEG / 2
    front_y = SEAT_OVERHANG + LEG / 2
    joint = bd.Vector(0, rear_top.Y - LEG / 2, rear_top.Z)  # bend at seat-top level
    foot_y = rear_top.Y + REAR_SPLAY
    post_len = BACK_TOP / math.cos(theta)

    def rear_leg_y(z):  # rear leg centerline below the bend
        return joint.Y + (foot_y - joint.Y) * (joint.Z - z) / joint.Z

    legs = []
    for x in (-xl, xl):
        h = seat_bottom(front_y) + 5  # trimmed to the seat underside below
        legs.append(eased(bd.Pos(x, front_y, h / 2) * bd.Box(LEG, LEG, h)))
        j = bd.Vector(x, joint.Y, joint.Z)
        legs.append(rear_leg(x, bd.Vector(x, foot_y, 0), j, j + v * post_len))
    legs = [leg - bd.Pos(0, 0, -500) * bd.Box(2000, 2000, 1000) for leg in legs]  # flat feet

    seat = bd.Pos(*front_top) * bd.Rot(-SEAT_TILT, 0, 0) * bd.Pos(0, SEAT_DEPTH / 2, -SEAT_THICK) * seat_panel()
    for i in (0, 2):  # front legs stop at the seat underside
        legs[i] = legs[i] - seat
    seat = seat - legs  # notches around the rear legs

    apron_z = seat_bottom(front_y) - APRON_HEIGHT / 2
    apron_x = xl + LEG / 2 - APRON_INSET - APRON_THICK / 2
    apron_y = SEAT_OVERHANG + APRON_INSET + APRON_THICK / 2
    rear_y = rear_leg_y(apron_z)
    aprons = []
    for x in (-apron_x, apron_x):
        start = bd.Vector(x, front_y, apron_z)
        length = (rear_y - front_y) / math.cos(a)
        plane = bd.Plane(origin=start, x_dir=(1, 0, 0), z_dir=u)
        aprons.append(bd.extrude(plane * bd.Rectangle(APRON_THICK, APRON_HEIGHT), amount=length))
    aprons.append(bd.Pos(0, apron_y, apron_z) * bd.Box(2 * xl, APRON_THICK, APRON_HEIGHT))
    back_z = seat_bottom(rear_y) - APRON_HEIGHT / 2
    aprons.append(bd.Pos(0, rear_leg_y(back_z), back_z) * bd.Box(2 * xl, APRON_THICK, APRON_HEIGHT))
    aprons = [eased(ap, 1) - seat - legs for ap in aprons]

    def rail(z_above_seat, height):
        c = joint + v * (z_above_seat / math.cos(theta))
        shape = curved_rail(2 * xl - LEG, height)  # butts against the post inner faces
        return bd.Pos(*c) * bd.Rot(-math.degrees(theta), 0, 0) * shape

    rails = [
        rail(LUMBAR_CENTER, LUMBAR_HEIGHT),
        rail(BACK_TOP - TOP_RAIL_HEIGHT / 2 - 10, TOP_RAIL_HEIGHT),  # sits below the post tops
    ]

    names = ["front_leg_L", "rear_leg_L", "front_leg_R", "rear_leg_R"]
    names += ["seat", "side_apron_L", "side_apron_R", "front_apron", "back_apron"]
    names += ["lumbar_rail", "top_rail"]
    parts = []
    for name, part in zip(names, legs + [seat] + aprons + rails):
        part.label = name
        parts.append(part)
    return parts


@glb(out="../GLB/chair.glb")
@step(out="../STEP/chair.step")
def chair():
    body = bd.Compound(children=chair_parts())
    body.label = "chair"
    return body


if __name__ == "__main__":
    chair()
