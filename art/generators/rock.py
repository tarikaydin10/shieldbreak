"""Boulder: rings of jittered points closed by a flat bottom and a top point, so no two rocks look alike.
Origin at the ground, centred under the rock."""
import math

REQUIRED_COLORS = ["stone"]
OPTIONAL_COLORS = ["stone_dark"]
SEG = 7


def build(b, p, c, rng):
    radius = p.get("radius", 0.7)
    height = p.get("height", 0.8)
    jitter = p.get("jitter", 0.25)
    dark = c.get("stone_dark", c["stone"])
    levels = [(0.0, 0.92), (0.35, 1.0), (0.75, 0.7)]
    rings = []
    for z_share, r_share in levels:
        ring = []
        for s in range(SEG):
            a = 2.0 * math.pi * (s + 0.3 * rng.random()) / SEG
            r = radius * r_share * (1.0 - jitter + 2.0 * jitter * rng.random())
            z = height * z_share * (1.0 if z_share == 0.0 else 0.85 + 0.3 * rng.random())
            ring.append(b.bm.verts.new((r * math.cos(a), r * math.sin(a), z)))
        rings.append(ring)
    top = b.bm.verts.new((radius * 0.15 * (rng.random() - 0.5), radius * 0.15 * (rng.random() - 0.5), height))
    for band in range(len(rings) - 1):
        lo, hi = rings[band], rings[band + 1]
        for s in range(SEG):
            s1 = (s + 1) % SEG
            b.face((lo[s], lo[s1], hi[s1], hi[s]), dark if (s + band) % 3 == 0 else c["stone"])
    for s in range(SEG):
        b.face((rings[-1][s], rings[-1][(s + 1) % SEG], top), c["stone"])
    b.face(list(reversed(rings[0])), dark)

    # Centre the bounding box over the origin (the jitter moved it)
    xs = [v.co.x for v in b.bm.verts]
    ys = [v.co.y for v in b.bm.verts]
    b.translate((-(min(xs) + max(xs)) * 0.5, -(min(ys) + max(ys)) * 0.5, 0.0))
