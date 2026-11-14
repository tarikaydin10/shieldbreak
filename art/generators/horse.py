"""Horse for riders, in two parts so the legs can swing at runtime (like the soldier's procedural arms).

part "body": barrel, neck, head with ears, mane, tail, saddle and team saddle cloth. Faces -Y (Blender) = forward.
Origin at the seat of the saddle (pivot "attach"): the game puts it seat_height above the ground.
part "leg": one leg from the hoof (origin, ground) up to the hip; the game hangs four of them under the body."""
import math

from mathutils import Matrix, Vector

REQUIRED_COLORS = ["coat"]
OPTIONAL_COLORS = ["dark", "saddle", "cloth", "hoof"]
SEG = 8


def _along(start, end):
    """Matrix that puts a +Z cylinder of length |end - start| from start to end."""
    a, b = Vector(start), Vector(end)
    d = b - a
    rot = Vector((0.0, 0.0, 1.0)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    return Matrix.Translation(a) @ rot, d.length


def build(b, p, c, rng):
    if p.get("part", "body") == "leg":
        _leg(b, p, c)
        return
    length = p.get("barrel_length", 1.6)
    radius = p.get("barrel_radius", 0.33)
    below = p.get("barrel_below_seat", 0.3)

    # Barrel: lathe along +Z, then laid down facing -Y, a little narrower than tall
    profile = [(0.0, -0.04), (radius * 0.66, 0.0), (radius * 0.9, length * 0.16), (radius, length * 0.5),
               (radius * 0.94, length * 0.84), (radius * 0.72, length), (0.0, length + 0.05)]
    rings = b.lathe(profile, SEG, lambda band, s: c["coat"], angle_offset=math.pi / SEG)
    barrel = [v for ring in rings for v in ring]
    for v in barrel:
        v.co.x *= 0.82
    import bmesh
    bmesh.ops.transform(b.bm, matrix=Matrix.Translation((0.0, length * 0.5, -below)) @ Matrix.Rotation(math.radians(90.0), 4, "X"),
                        verts=barrel)

    front = -length * 0.5
    dark = c.get("dark", c["coat"])
    # Neck up and forward from the chest, head hanging forward and down
    neck_base = (0.0, front + 0.18, -below + 0.18)
    neck_top = (0.0, front - 0.32, -below + 0.95)
    m, l = _along(neck_base, neck_top)
    b.cylinder(c["coat"], 0.19, 0.13, 0.0, l, 6, m)
    m, l = _along((0.0, front + 0.05, -below + 0.62), (0.0, front - 0.36, -below + 1.02))
    b.cylinder(dark, 0.045, 0.04, 0.0, l, 4, m @ Matrix.Translation((0.0, 0.12, 0.0)))
    head = Matrix.Translation((0.0, front - 0.52, -below + 0.88)) @ Matrix.Rotation(math.radians(-35.0), 4, "X")
    b.box(c["coat"], (0.18, 0.5, 0.2), (0.0, 0.0, 0.0), head)
    b.box(dark, (0.12, 0.08, 0.1), (0.0, -0.27, -0.03), head)
    for side in (-1, 1):
        b.box(c["coat"], (0.04, 0.04, 0.11), (side * 0.06, 0.2, 0.15), head)

    # Tail
    m, l = _along((0.0, -front - 0.02, -below + 0.15), (0.0, -front + 0.28, -below - 0.45))
    b.cylinder(dark, 0.07, 0.035, 0.0, l, 5, m)

    # Saddle and the saddle cloth that tells the side from afar
    if "cloth" in c:
        b.box(c["cloth"], (radius * 2.0, 0.66, 0.03), (0.0, 0.04, -0.07))
    saddle = c.get("saddle", dark)
    b.box(saddle, (0.36, 0.5, 0.07), (0.0, 0.05, -0.02))
    b.box(saddle, (0.24, 0.07, 0.12), (0.0, -0.18, 0.04))
    b.box(saddle, (0.28, 0.06, 0.1), (0.0, 0.28, 0.03))


def _leg(b, p, c):
    height = p.get("height", 0.95)
    hoof = c.get("hoof", c["coat"])
    b.cylinder(hoof, 0.075, 0.07, 0.0, 0.12, 6)
    b.cylinder(c["coat"], 0.055, 0.06, 0.1, height * 0.55, 6)
    b.cylinder(c["coat"], 0.065, 0.1, height * 0.53, height, 6)
