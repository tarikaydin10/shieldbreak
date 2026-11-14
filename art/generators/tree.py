"""Tree: a pine of stacked cones or a broadleaf of round crowns, on a short trunk. Origin at the foot of the trunk.
The seed varies proportions within the spec's ranges; the game scales and turns each tree it plants."""
REQUIRED_COLORS = ["trunk", "crown"]
OPTIONAL_COLORS = ["crown_dark"]


def _between(rng, pair):
    return pair[0] + (pair[1] - pair[0]) * rng.random()


def build(b, p, c, rng):
    height = p.get("height", 7.0)
    trunk_h = height * _between(rng, p.get("trunk_share", [0.18, 0.26]))
    trunk_r = p.get("trunk_radius", 0.16)
    dark = c.get("crown_dark", c["crown"])
    b.cylinder(c["trunk"], trunk_r * 1.15, trunk_r * 0.85, 0.0, trunk_h + 0.3, 6)
    if p.get("kind", "pine") == "pine":
        tiers = p.get("tiers", 3)
        width = height * _between(rng, p.get("width_share", [0.28, 0.34]))
        bottom = trunk_h
        span = height - trunk_h
        for t in range(tiers):
            share = 1.0 - t / tiers
            z0 = bottom + span * (t / tiers) * 0.8
            z1 = min(height, z0 + span * (0.55 + 0.1 * rng.random()))
            b.cylinder(c["crown"] if t % 2 == 0 else dark, width * share, 0.0, z0, z1, 7)
    else:
        crowns = p.get("crowns", 2)
        width = height * _between(rng, p.get("width_share", [0.3, 0.36]))
        for k in range(crowns):
            r = width * (1.0 - 0.22 * k)
            z = trunk_h + r * 0.8 + (height - trunk_h - 2.0 * r * 0.8) * (k / max(1, crowns - 1))
            b.sphere(c["crown"] if k % 2 == 0 else dark, r, (0.0, 0.0, z), 7, 4)

    # Odd segment counts leave the crown off centre by a few centimetres: centre the bounding box over the origin
    xs = [v.co.x for v in b.bm.verts]
    ys = [v.co.y for v in b.bm.verts]
    b.translate((-(min(xs) + max(xs)) * 0.5, -(min(ys) + max(ys)) * 0.5, 0.0))
