"""One-hand sword: pommel, leather grip, crossguard, flat diamond-section blade with a point.
Built along +Z, grip centre at the origin; the wide side of the blade lies along X."""

REQUIRED_COLORS = ["blade", "guard", "grip", "pommel"]


def _section(b, z, half_width, half_thickness):
    return [b.bm.verts.new((half_width, 0.0, z)), b.bm.verts.new((0.0, half_thickness, z)),
            b.bm.verts.new((-half_width, 0.0, z)), b.bm.verts.new((0.0, -half_thickness, z))]


def build(b, p, c, rng):
    blade_len = p["blade_length"]
    w = p["blade_width"] * 0.5
    t = p["blade_thickness"] * 0.5
    grip_len = p.get("grip_length", 0.18)
    guard_w = p.get("guard_width", 0.20)
    point = p.get("point_length", 0.16)

    grip_r = 0.017
    b.cylinder(c["grip"], grip_r, grip_r, -grip_len * 0.55, grip_len * 0.45, 6)
    b.sphere(c["pommel"], 0.028, (0.0, 0.0, -grip_len * 0.55 - 0.02), 6, 4)
    guard_z = grip_len * 0.45 + 0.012
    b.box(c["guard"], (guard_w, 0.03, 0.024), (0.0, 0.0, guard_z))

    # Blade: slight taper from the guard to the start of the point, then a 4-sided point
    z0 = guard_z + 0.012
    z1 = z0 + blade_len - point
    base = _section(b, z0, w, t)
    top = _section(b, z1, w * 0.85, t * 0.9)
    tip = b.bm.verts.new((0.0, 0.0, z0 + blade_len))
    for i in range(4):
        j = (i + 1) % 4
        b.face((base[i], base[j], top[j], top[i]), c["blade"])
        b.face((top[i], top[j], tip), c["blade"])
    b.face(list(reversed(base)), c["blade"])
