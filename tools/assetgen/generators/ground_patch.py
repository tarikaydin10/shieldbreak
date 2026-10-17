"""Tileable ground patch: triangulated grid, seeded height jitter and per-triangle color variation.
Border vertices stay at z = 0 so patches tile in any rotation. Origin at the bottom center."""

REQUIRED_COLORS = ["base", "dark", "light"]


def build(b, p, c, rng):
    size = p["size"]
    n = p["cells"]
    jitter = p["height_jitter"]
    w_dark = p.get("dark_weight", 0.25)
    w_light = p.get("light_weight", 0.2)
    step = size / n
    half = size * 0.5

    verts = []
    for j in range(n + 1):
        row = []
        for i in range(n + 1):
            border = i in (0, n) or j in (0, n)
            z = 0.0 if border else rng.random() * jitter
            row.append(b.bm.verts.new((-half + i * step, -half + j * step, z)))
        verts.append(row)

    def pick():
        r = rng.random()
        if r < w_dark:
            return c["dark"]
        if r < w_dark + w_light:
            return c["light"]
        return c["base"]

    for j in range(n):
        for i in range(n):
            a, bb = verts[j][i], verts[j][i + 1]
            cc, d = verts[j + 1][i + 1], verts[j + 1][i]
            if (i + j) % 2 == 0:
                b.face((a, bb, cc), pick())
                b.face((a, cc, d), pick())
            else:
                b.face((a, bb, d), pick())
                b.face((bb, cc, d), pick())
