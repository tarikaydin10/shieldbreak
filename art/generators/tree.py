"""Tree or bush. Origin at the foot.
- pine: a long bare trunk, the crown of stacked cones high up.
- broadleaf: a long trunk that forks into two or three branches, round crowns clustered at the top.
- bush: a low clump of round crowns without a trunk, the undergrowth of the forest.
The seed varies proportions within the spec's ranges; the game scales and turns each tree it plants."""
import math

from mathutils import Matrix

REQUIRED_COLORS = ["trunk", "crown"]
OPTIONAL_COLORS = ["crown_dark"]


def _between(rng, pair):
    return pair[0] + (pair[1] - pair[0]) * rng.random()


def _pine(b, p, c, rng, height, trunk_h, trunk_r, dark):
    b.cylinder(c["trunk"], trunk_r * 1.2, trunk_r * 0.6, 0.0, height * 0.92, 6)
    tiers = p.get("tiers", 4)
    width = height * _between(rng, p.get("width_share", [0.14, 0.19]))
    span = height - trunk_h
    for t in range(tiers):
        share = 1.0 - 0.7 * t / tiers
        z0 = trunk_h + span * (t / tiers) * 0.78
        z1 = min(height, z0 + span * (0.42 + 0.1 * rng.random()))
        b.cylinder(c["crown"] if t % 2 == 0 else dark, width * share, 0.0, z0, z1, 7)


def _broadleaf(b, p, c, rng, height, trunk_h, trunk_r, dark):
    width = height * _between(rng, p.get("width_share", [0.17, 0.22]))
    top = height - width
    b.cylinder(c["trunk"], trunk_r * 1.2, trunk_r * 0.75, 0.0, top, 6)
    # Branches leave the trunk at its top third and carry the side crowns
    branches = p.get("branches", 2)
    turn = rng.random() * 2.0 * math.pi
    for k in range(branches):
        angle = turn + 2.0 * math.pi * k / branches
        lean = math.radians(_between(rng, [28.0, 40.0]))
        length = (top - trunk_h) * _between(rng, [0.8, 1.1]) + width * 0.4
        m = Matrix.Translation((0.0, 0.0, trunk_h)) @ Matrix.Rotation(angle, 4, "Z") @ Matrix.Rotation(lean, 4, "Y")
        b.cylinder(c["trunk"], trunk_r * 0.6, trunk_r * 0.3, 0.0, length, 5, m)
        tip = m @ Matrix.Translation((0.0, 0.0, length))
        x, y, z = tip.translation
        b.sphere(dark if k % 2 == 0 else c["crown"], width * _between(rng, [0.62, 0.78]), (x, y, z + width * 0.1), 7, 4)
    b.sphere(c["crown"], width, (0.0, 0.0, top), 8, 5)


def _bush(b, p, c, rng, height, dark):
    clumps = p.get("clumps", 3)
    for k in range(clumps):
        r = height * _between(rng, [0.38, 0.5])
        angle = 2.0 * math.pi * (k / clumps + 0.2 * rng.random())
        reach = 0.0 if k == 0 else height * 0.45
        b.sphere(c["crown"] if k % 2 == 0 else dark, r, (reach * math.cos(angle), reach * math.sin(angle), r * 0.8), 6, 4)


def build(b, p, c, rng):
    height = p.get("height", 18.0)
    kind = p.get("kind", "pine")
    dark = c.get("crown_dark", c["crown"])
    if kind == "bush":
        _bush(b, p, c, rng, height, dark)
    else:
        trunk_h = height * _between(rng, p.get("trunk_share", [0.42, 0.55]))
        trunk_r = p.get("trunk_radius", height * 0.02)
        if kind == "pine":
            _pine(b, p, c, rng, height, trunk_h, trunk_r, dark)
        else:
            _broadleaf(b, p, c, rng, height, trunk_h, trunk_r, dark)

    # Odd segment counts and leaning branches leave the tree off centre: centre the bounding box over the origin
    # (style rule for pivot "ground"), foot on the ground
    xs = [v.co.x for v in b.bm.verts]
    ys = [v.co.y for v in b.bm.verts]
    zs = [v.co.z for v in b.bm.verts]
    b.translate((-(min(xs) + max(xs)) * 0.5, -(min(ys) + max(ys)) * 0.5, -min(zs)))
