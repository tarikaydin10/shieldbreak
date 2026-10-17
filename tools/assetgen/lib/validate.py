"""Hard style checks. Every rule comes from art/style/style.json; returns a list of error strings."""
import hashlib
import re
import struct


def check_spec(spec, style, palette, generator):
    errors = []
    for key in ("name", "category", "generator", "params", "colors"):
        if key not in spec:
            errors.append(f"spec: missing '{key}'")
    if errors:
        return errors
    if spec["category"] not in style["categories"]:
        errors.append(f"spec: unknown category '{spec['category']}'")
    for slot in getattr(generator, "REQUIRED_COLORS", []):
        if slot not in spec["colors"]:
            errors.append(f"spec: missing color slot '{slot}'")
    allowed = set(getattr(generator, "REQUIRED_COLORS", [])) | set(getattr(generator, "OPTIONAL_COLORS", []))
    for slot, name in spec["colors"].items():
        if slot not in allowed:
            errors.append(f"spec: unknown color slot '{slot}'")
        if name not in palette.names:
            errors.append(f"spec: color '{name}' is not in palette.json")
        if name in style["global"]["forbidden_colors"]:
            errors.append(f"spec: color '{name}' is forbidden")
    return errors


def check_object(obj, spec, style, palette):
    g = style["global"]
    cat = style["categories"][spec["category"]]
    mesh = obj.data
    errors = []

    if not re.match(g["name_pattern"], obj.name):
        errors.append(f"name '{obj.name}' does not match {g['name_pattern']}")
    if not obj.name.startswith(cat["prefix"] + "_"):
        errors.append(f"name must start with '{cat['prefix']}_' for category {spec['category']}")

    tris = sum(len(p.vertices) - 2 for p in mesh.polygons)
    if tris > cat["max_tris"]:
        errors.append(f"tris {tris} > budget {cat['max_tris']}")

    xs = [v.co.x for v in mesh.vertices]
    ys = [v.co.y for v in mesh.vertices]
    zs = [v.co.z for v in mesh.vertices]
    mn = (min(xs), min(ys), min(zs))
    mx = (max(xs), max(ys), max(zs))
    longest = max(mx[i] - mn[i] for i in range(3))
    lo, hi = cat["size_m"]["longest"]
    if not lo <= longest <= hi:
        errors.append(f"longest dimension {longest:.3f} m outside [{lo}, {hi}]")

    if cat["pivot"] in ("grip", "attach"):
        if not all(mn[i] <= 0.0 <= mx[i] for i in range(3)):
            errors.append(f"pivot {cat['pivot']!r}: origin must lie inside the bounding box")
    elif cat["pivot"] == "ground":
        if abs(mn[2]) > 0.001:
            errors.append(f"pivot 'ground': min z is {mn[2]:.4f}, expected 0")
        cx, cy = (mn[0] + mx[0]) * 0.5, (mn[1] + mx[1]) * 0.5
        if abs(cx) > 0.01 or abs(cy) > 0.01:
            errors.append(f"pivot 'ground': bbox center xy ({cx:.3f}, {cy:.3f}) not at origin")

    if len(mesh.materials) != g["max_materials"] or mesh.materials[0].name != g["material_name"]:
        errors.append(f"exactly one material '{g['material_name']}' required")

    if g["flat_shading"]:
        sharp = mesh.attributes.get("sharp_face")
        if sharp is None or not all(d.value for d in sharp.data):
            errors.append("flat shading required (all faces sharp)")

    uv = mesh.uv_layers.active
    if uv is None:
        errors.append("no UV layer")
    else:
        forbidden = {palette.index(n) for n in g["forbidden_colors"]}
        eps = g["uv_epsilon"]
        bad = 0
        for p in mesh.polygons:
            uvs = [uv.data[li].uv for li in p.loop_indices]
            idx = palette.index_from_uv(uvs[0].x, uvs[0].y, eps)
            same = all(abs(u.x - uvs[0].x) <= eps and abs(u.y - uvs[0].y) <= eps for u in uvs)
            if idx is None or not same or idx >= len(palette.names) or idx in forbidden:
                bad += 1
        if bad:
            errors.append(f"{bad} faces not on a single allowed palette cell")

    small = sum(1 for p in mesh.polygons if p.area < g["min_face_area_m2"])
    if small:
        errors.append(f"{small} degenerate faces")
    used = set()
    for p in mesh.polygons:
        used.update(p.vertices)
    loose = len(mesh.vertices) - len(used)
    if loose:
        errors.append(f"{loose} loose vertices")

    return errors, tris


def geometry_hash(obj):
    """Hash of quantized positions, topology and UVs: identical specs must give identical hashes."""
    mesh = obj.data
    h = hashlib.sha256()
    for v in mesh.vertices:
        h.update(struct.pack("<3i", *(round(c * 1e5) for c in v.co)))
    uv = mesh.uv_layers.active
    for p in mesh.polygons:
        h.update(struct.pack(f"<{len(p.vertices)}i", *p.vertices))
        u = uv.data[p.loop_indices[0]].uv
        h.update(struct.pack("<2i", round(u.x * 1e5), round(u.y * 1e5)))
    return h.hexdigest()
