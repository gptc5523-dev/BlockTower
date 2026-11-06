"""Dining room shell for the game: floor, four walls and a ceiling, baseboards,
and one interior door, as separate parts. Units: mm, origin at the floor
centre, Z up.

Size: 4000 x 5000 interior, 2400 ceiling (a typical apartment dining room).
The 1600 x 900 table at the centre leaves ~1.5 m on the long sides for pulled-out
chairs plus a walkway, and ~1.7 m at the ends.

Door: a standard 900 x 2100 x 40 interior leaf in the north wall (+Y), which
ends up behind the player's start in Unity. It is hung on three butt hinges
in the 3 mm gap to the jamb (hinge knuckles and screws not modeled); 10 mm
under-cut above the floor. The jamb lines the opening, casing trims the room
face. No windows.

Colours mark the finish for the game: wall paint, floor, white trim, metal.
"""

from cadgen import build123d as bd
from cadgen import glb, srgb, step

WIDTH = 4000.0  # X
LENGTH = 5000.0  # Y
CEILING = 2400.0
WALL = 120.0
SLAB = 50.0

BASE_H, BASE_T = 80.0, 12.0  # baseboard
LEAF_W, LEAF_H, LEAF_T = 900.0, 2100.0, 40.0
LEAF_GAP, UNDERCUT = 3.0, 10.0
JAMB_T = 30.0
CASING_W, CASING_T = 60.0, 15.0
DOOR_X = -1200.0  # opening centre; Unity mirrors X, so this lands at x = +1.2 m
HINGE_H, HINGE_D = 100.0, 25.0
HINGE_Z = (250.0, 1050.0, 1850.0)  # hinge centres above the leaf bottom

OPEN_W = LEAF_W + 2 * LEAF_GAP + 2 * JAMB_T
JAMB_H = UNDERCUT + LEAF_H + LEAF_GAP  # side jambs stand on the floor
OPEN_H = JAMB_H + JAMB_T

PAINT, FLOOR, TRIM, METAL = "#E9E4DA", "#9A7650", "#F4F2EE", "#9C9C98"


def box(x0, x1, y0, y1, z0, z1):
    return bd.Pos((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2) * bd.Box(x1 - x0, y1 - y0, z1 - z0)


def shell():
    w, l = WIDTH / 2, LENGTH / 2
    floor = box(-w - WALL, w + WALL, -l - WALL, l + WALL, -SLAB, 0)
    ceiling = box(-w - WALL, w + WALL, -l - WALL, l + WALL, CEILING, CEILING + SLAB)
    west = box(-w - WALL, -w, -l - WALL, l + WALL, 0, CEILING)
    east = box(w, w + WALL, -l - WALL, l + WALL, 0, CEILING)
    south = box(-w, w, -l - WALL, -l, 0, CEILING)
    north = box(-w, w, l, l + WALL, 0, CEILING)
    north -= box(DOOR_X - OPEN_W / 2, DOOR_X + OPEN_W / 2, l, l + WALL, 0, OPEN_H)
    return [("floor", floor, FLOOR), ("ceiling", ceiling, PAINT), ("wall_west", west, PAINT),
            ("wall_east", east, PAINT), ("wall_south", south, PAINT), ("wall_north", north, PAINT)]


def baseboards():
    w, l, t = WIDTH / 2, LENGTH / 2, BASE_T
    door_lo, door_hi = DOOR_X - OPEN_W / 2 - CASING_W, DOOR_X + OPEN_W / 2 + CASING_W
    runs = [
        ("base_west", box(-w, -w + t, -l, l, 0, BASE_H)),
        ("base_east", box(w - t, w, -l, l, 0, BASE_H)),
        ("base_south", box(-w + t, w - t, -l, -l + t, 0, BASE_H)),
        ("base_north_1", box(-w + t, door_lo, l - t, l, 0, BASE_H)),
        ("base_north_2", box(door_hi, w - t, l - t, l, 0, BASE_H)),
    ]
    return [(n, s, TRIM) for n, s in runs]


def door():
    l = LENGTH / 2
    x0, x1 = DOOR_X - OPEN_W / 2, DOOR_X + OPEN_W / 2
    jambs = [
        ("jamb_left", box(x0, x0 + JAMB_T, l, l + WALL, 0, JAMB_H)),
        ("jamb_right", box(x1 - JAMB_T, x1, l, l + WALL, 0, JAMB_H)),
        ("jamb_head", box(x0, x1, l, l + WALL, JAMB_H, OPEN_H)),
    ]
    casing = [
        ("casing_left", box(x0 - CASING_W, x0, l - CASING_T, l, 0, OPEN_H)),
        ("casing_right", box(x1, x1 + CASING_W, l - CASING_T, l, 0, OPEN_H)),
        ("casing_head", box(x0 - CASING_W, x1 + CASING_W, l - CASING_T, l, OPEN_H, OPEN_H + CASING_W)),
    ]
    # leaf centred in the wall thickness, hinged on the +X jamb
    lx0, lx1 = x0 + JAMB_T + LEAF_GAP, x1 - JAMB_T - LEAF_GAP
    ly0, ly1 = l + (WALL - LEAF_T) / 2, l + (WALL + LEAF_T) / 2
    lz0 = UNDERCUT
    leaf = ("door_leaf", box(lx0, lx1, ly0, ly1, lz0, lz0 + LEAF_H))
    hinges = [
        (f"hinge_{i + 1}", box(lx1, lx1 + LEAF_GAP, ly0, ly0 + HINGE_D, lz0 + z - HINGE_H / 2, lz0 + z + HINGE_H / 2))
        for i, z in enumerate(HINGE_Z)
    ]
    # lever handle each side, 1000 above the floor, 60 in from the latch edge
    hx, hz = lx0 + 60, 1000.0
    handles = []
    for side, face, s in (("room", ly0, -1), ("hall", ly1, 1)):
        rose = bd.Pos(hx, face + s * 4, hz) * bd.Rot(90, 0, 0) * bd.Cylinder(25, 8)
        ya, yb = sorted((face + s * 8, face + s * 26))
        lever = box(hx - 9, hx + 120, ya, yb, hz - 9, hz + 9)
        handles += [(f"rose_{side}", rose, METAL), (f"lever_{side}", lever, METAL)]
    trim = [(n, s, TRIM) for n, s in jambs + casing + [leaf]]
    return trim + [(n, s, METAL) for n, s in hinges] + handles


def room_parts():
    parts = []
    for name, part, color in shell() + baseboards() + door():
        part.label = name
        part.color = srgb(color)
        parts.append(part)
    return parts


@glb(out="../GLB/room.glb")
@step(out="../STEP/room.step")
def room():
    body = bd.Compound(children=room_parts())
    body.label = "room"
    return body


if __name__ == "__main__":
    room()
