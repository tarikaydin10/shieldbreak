"""Nasal helmet: brow band, plated conical dome, nasal guard, optional team-colored crest.
Origin at the center of the brow band's lower edge (head attach point), front faces -Y."""
import math

REQUIRED_COLORS = ["band", "plate_a", "plate_b", "nasal", "inside"]
OPTIONAL_COLORS = ["crest"]


def build(b, p, c, rng):
    r = p["radius"]
    h = p["height"]
    seg = p["segments"]
    band_h = p.get("band_height", 0.03)
    point = p.get("point", 0.12)

    # Radius factor, height factor of the dome profile (conical with a slight bulge)
    profile = [(1.00, 0.0), (1.00, band_h), (0.98, band_h + 0.01),
               (0.86, h * 0.55), (0.55, h * 0.85), (0.0, h + point * 0.25)]
    prof = [(r * rf if rf > 0 else 0.0, z) for rf, z in profile]

    def color(band, s):
        if band <= 1:
            return c["band"]
        return c["plate_a"] if s % 2 == 0 else c["plate_b"]

    # A vertex on -Y so the nasal sits on a ridge
    b.lathe(prof, seg, color, angle_offset=-math.pi / 2, cap_bottom=c["inside"])

    nasal_len = p.get("nasal_length", 0.07)
    if nasal_len > 0:
        b.box(c["nasal"], (0.022, 0.014, nasal_len + band_h),
              (0, -r - 0.004, (band_h - nasal_len) * 0.5))

    crest = p.get("crest")
    if crest:
        if "crest" not in c:
            raise KeyError("helmet: crest requested but no 'crest' color in spec")
        ch = crest.get("height", 0.06)
        cl = crest.get("length", r * 1.5)
        b.box(c["crest"], (0.025, cl, ch), (0, 0, h * 0.8 + ch * 0.5))
