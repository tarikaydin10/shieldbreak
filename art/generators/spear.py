"""Spear: hexagonal shaft, butt ferrule, leather grip binding, iron socket, faceted leaf head.
Built along +Z, grip point at the origin."""

REQUIRED_COLORS = ["shaft", "head", "socket", "ferrule", "binding"]
SEGMENTS = 6


def build(b, p, c, rng):
    length = p["length"]
    head_len = p["head_length"]
    head_w = p["head_width"]
    head_t = p.get("head_thickness", head_w * 0.35)
    r = p["shaft_radius"]
    grip = p["grip_from_butt"]
    socket_len = p.get("socket_length", 0.12)

    shaft_top = length - head_len - socket_len
    b.cylinder(c["shaft"], r, r, 0.0, shaft_top + 0.01, SEGMENTS)
    b.cylinder(c["ferrule"], r * 1.25, r * 1.15, -0.01, 0.07, SEGMENTS)
    b.cylinder(c["binding"], r * 1.2, r * 1.2, grip - 0.1, grip + 0.1, SEGMENTS)
    b.cylinder(c["socket"], r * 1.25, r * 0.9, shaft_top, shaft_top + socket_len, SEGMENTS)

    # Leaf head: widest point at 35 % of its length, diamond cross-section
    base = shaft_top + socket_len
    ring = [(head_w, 0.0), (0.0, head_t), (-head_w, 0.0), (0.0, -head_t)]
    b.bipyramid(c["head"], ring, base + head_len * 0.35, base - 0.02, base + head_len)

    b.translate((0.0, 0.0, -grip))
