"""Limb segment for procedural arms: a 6-sided prism of unit size along +Z (radius 0.5, length 1), scaled at runtime
to the bone it covers. One colour, so arms stay on the palette like every other asset."""

REQUIRED_COLORS = ["surface"]


def build(b, p, c, rng):
    b.cylinder(c["surface"], 0.5, p.get("end_radius", 0.42), 0.0, 1.0, p.get("segments", 6))
