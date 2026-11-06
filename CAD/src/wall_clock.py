"""Wall clock for the door wall, as separate parts that only touch.
Units: mm, origin at the floor at the wall face, wall at Y = 0, room toward -Y, Z up.

Ø300 brushed aluminium case 40 deep with a 4 mm back that hangs flat on a nail (keyhole not modeled), a white
2 mm dial resting on the back's inside, 12 raised hour bars and 48 minute ticks on it, and three hands on a
centre pin, each on its own hub ring round the pin, stacked 1 mm apart, at 10:10:30. Glass not modeled.
Centre 1900 above the floor, over head height beside the door.
"""

import math

from cadgen import build123d as bd
from cadgen import glb, srgb, step

R, DEPTH, CASE_T, BACK_T, CLOCK_Z = 150.0, 40.0, 8.0, 4.0, 1900.0
DIAL_T, MARK_T, PIN_R = 2.0, 1.0, 3.0

ALU, DIAL, INK, RED = "#A7AAAE", "#F4F2EC", "#2B2B2B", "#C0392B"


def disc(r, y0, y1):
    """A disc about the clock axis (Y), from y0 to y1"""
    return bd.Pos(0, (y0 + y1) / 2, CLOCK_Z) * bd.Rot(90, 0, 0) * bd.Cylinder(r, abs(y1 - y0))


def on_dial(shape, angle, radius):
    """Places a shape drawn upright at the top of the dial, turned `angle` deg clockwise round the centre"""
    return bd.Pos(0, 0, CLOCK_Z) * bd.Rot(0, angle, 0) * bd.Pos(0, 0, radius) * shape


def flat(w, h, y0, y1):
    return bd.Pos(0, (y0 + y1) / 2, 0) * bd.Box(w, abs(y1 - y0), h)


def hand(length, width, tail, y0, y1, angle):
    blade = bd.Pos(0, 0, (length - tail) / 2) * flat(width, length + tail, y0, y1)
    hub = disc(PIN_R + 4, y0, y1) - disc(PIN_R, y0, y1)
    blade = bd.Pos(0, 0, CLOCK_Z) * blade - disc(PIN_R, y0, y1)
    return bd.Pos(0, 0, CLOCK_Z) * bd.Rot(0, angle, 0) * bd.Pos(0, 0, -CLOCK_Z) * (blade + hub)


def clock_parts():
    case = disc(R, -DEPTH, 0) - disc(R - CASE_T, -DEPTH, -BACK_T)
    yd = -BACK_T - DIAL_T  # dial front
    dial = disc(R - CASE_T, yd, -BACK_T)
    marks = None
    for i in range(60):
        big = i % 5 == 0
        m = on_dial(flat(6 if big else 2, 22 if big else 8, yd - MARK_T, yd), i * 6, (R - CASE_T - 18) - (11 if big else 4))
        marks = m if marks is None else marks + m
    pin = disc(PIN_R, yd - 6, yd)
    # 10:10:30
    hour = hand(80, 8, 15, yd - 2, yd - 1, -55)     # 10 h 10 min: 300 + 5 deg
    minute = hand(115, 5, 20, yd - 4, yd - 3, 60)
    second = hand(125, 1.6, 30, yd - 6, yd - 5, 180)
    return [("case", case, ALU), ("dial", dial, DIAL), ("marks", marks, INK), ("pin", pin, INK),
            ("hour_hand", hour, INK), ("minute_hand", minute, INK), ("second_hand", second, RED)]


@glb(out="../GLB/wall_clock.glb")
@step(out="../STEP/wall_clock.step")
def wall_clock():
    parts = []
    for name, part, color in clock_parts():
        part.label = name
        part.color = srgb(color)
        parts.append(part)
    body = bd.Compound(children=parts)
    body.label = "wall_clock"
    return body


if __name__ == "__main__":
    wall_clock()
