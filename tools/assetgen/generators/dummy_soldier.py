"""Stand-in soldier until M4: legs, team tunic, belt, arms hanging down, head. Static, no rig.
Origin at the feet, faces -Y. Hands at (+-hand_x, 0, hand_z) for attaching weapon and shield."""
import math

from mathutils import Matrix

REQUIRED_COLORS = ["tunic", "tunic_trim", "legs", "boots", "belt", "skin"]
SEG = 6


def build(b, p, c, rng):
    height = p.get("height", 1.8)
    s = height / 1.8
    hip = 0.92 * s
    shoulder = 1.42 * s
    head_r = 0.11 * s
    head_z = 1.63 * s

    for side in (-1, 1):
        x = side * 0.1 * s
        b.cylinder(c["boots"], 0.07 * s, 0.065 * s, 0.0, 0.22 * s, SEG, Matrix.Translation((x, -0.02 * s, 0)))
        b.cylinder(c["legs"], 0.065 * s, 0.08 * s, 0.2 * s, hip, SEG, Matrix.Translation((x, 0, 0)))

    # Tunic: skirt flaring to the knees, torso narrowing to the shoulders
    b.cylinder(c["tunic_trim"], 0.23 * s, 0.215 * s, 0.6 * s, 0.66 * s, 8)
    b.cylinder(c["tunic"], 0.215 * s, 0.17 * s, 0.66 * s, hip + 0.04 * s, 8)
    b.cylinder(c["belt"], 0.175 * s, 0.175 * s, hip, hip + 0.07 * s, 8)
    b.cylinder(c["tunic"], 0.17 * s, 0.21 * s, hip + 0.07 * s, shoulder, 8)
    b.cylinder(c["tunic"], 0.21 * s, 0.09 * s, shoulder, shoulder + 0.07 * s, 8)
    b.cylinder(c["skin"], 0.05 * s, 0.05 * s, shoulder + 0.05 * s, head_z - head_r * 0.6, SEG)

    # Arms hang slightly outwards; hands end at hand_z
    hand_z = p.get("hand_z", 0.9) * s
    hand_x = p.get("hand_x", 0.29) * s
    # "left" leaves out the right arm (Blender -X = soldier's right) so a procedural arm can be attached at runtime
    sides = (-1, 1) if p.get("arms", "both") == "both" else (1,)
    for side in sides:
        top = (side * 0.24 * s, 0, shoulder - 0.04 * s)
        bottom = (side * hand_x, 0, hand_z + 0.06 * s)
        dx, dz = bottom[0] - top[0], bottom[2] - top[2]
        length = math.hypot(dx, dz)
        tilt = Matrix.Rotation(math.atan2(-dx, -dz), 4, "Y")
        frame = Matrix.Translation(top) @ tilt @ Matrix.Rotation(math.pi, 4, "X")
        b.cylinder(c["tunic"], 0.065 * s, 0.055 * s, 0.0, length * 0.45, SEG, frame)
        b.cylinder(c["skin"], 0.045 * s, 0.04 * s, length * 0.45, length, SEG, frame)
        b.box(c["skin"], (0.07 * s, 0.08 * s, 0.1 * s), (side * hand_x, 0, hand_z + 0.02 * s))

    b.sphere(c["skin"], head_r, (0, 0, head_z), 8, 5)
