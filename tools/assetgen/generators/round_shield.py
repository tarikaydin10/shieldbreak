"""Round shield: domed plank face with paint pattern, raised rim, iron boss, back brace and grip.
Front faces -Y (Blender) = Unity forward, grip at the origin."""
import math

from mathutils import Matrix

REQUIRED_COLORS = ["face_a", "face_b", "rim", "boss", "back", "brace", "grip"]
PATTERNS = ("solid", "halves", "quarters", "rays")


def _pattern_color(pattern, seg, segments, c):
    if pattern == "solid":
        return c["face_a"]
    if pattern == "halves":
        return c["face_a"] if seg < segments // 2 else c["face_b"]
    if pattern == "quarters":
        return c["face_a"] if (seg * 4 // segments) % 2 == 0 else c["face_b"]
    if pattern == "rays":
        return c["face_a"] if seg % 2 == 0 else c["face_b"]
    raise ValueError(f"unknown shield pattern '{pattern}', expected one of {PATTERNS}")


def build(b, p, c, rng):
    R = p["radius"]
    seg = p["segments"]
    t = p["thickness"]
    dome = p["dome"]
    rim_w = p["rim_width"]
    rim_h = p.get("rim_height", 0.012)
    boss_r = p["boss_radius"]
    boss_h = p["boss_height"]
    pattern = p.get("pattern", "solid")

    # Built facing +Z, rotated to face -Y at the end
    def z_front(r):
        return t * 0.5 + dome * (1.0 - (r / R) ** 2)

    def ring(r, z):
        return [b.bm.verts.new((r * math.cos(2 * math.pi * s / seg),
                                r * math.sin(2 * math.pi * s / seg), z)) for s in range(seg)]

    r_inner = R - rim_w
    boss_ring = ring(boss_r, z_front(boss_r))
    plank_outer = ring(r_inner, z_front(r_inner))
    rim_in_top = ring(r_inner, z_front(r_inner) + rim_h)
    rim_out_top = ring(R, z_front(R) + rim_h)
    back_ring = ring(R, -t * 0.5)

    for s in range(seg):
        s1 = (s + 1) % seg
        b.face((boss_ring[s], plank_outer[s], plank_outer[s1], boss_ring[s1]),
               _pattern_color(pattern, s, seg, c))
        b.face((plank_outer[s], rim_in_top[s], rim_in_top[s1], plank_outer[s1]), c["rim"])
        b.face((rim_in_top[s], rim_out_top[s], rim_out_top[s1], rim_in_top[s1]), c["rim"])
        b.face((rim_out_top[s], back_ring[s], back_ring[s1], rim_out_top[s1]), c["rim"])

    back_center = b.bm.verts.new((0, 0, -t * 0.5))
    for s in range(seg):
        b.face((back_ring[(s + 1) % seg], back_ring[s], back_center), c["back"])

    # Boss: faceted dome closing the plank hole
    z0 = z_front(boss_r)
    mid = ring(boss_r * 0.75, z0 + boss_h * 0.7)
    top = b.bm.verts.new((0, 0, z0 + boss_h))
    for s in range(seg):
        s1 = (s + 1) % seg
        b.face((boss_ring[s1], mid[s1], mid[s], boss_ring[s]), c["boss"])
        b.face((mid[s1], top, mid[s]), c["boss"])

    # Back: horizontal brace board and vertical grip bar
    grip_z = -t * 0.5 - 0.035
    b.box(c["brace"], (R * 1.7, 0.08, 0.02), (0, 0, -t * 0.5 - 0.01))
    b.box(c["grip"], (0.03, 0.14, 0.03), (0, 0, grip_z))

    b.translate((0, 0, -grip_z))
    b.transform(Matrix.Rotation(math.radians(90.0), 4, "X"))   # +Z -> -Y
