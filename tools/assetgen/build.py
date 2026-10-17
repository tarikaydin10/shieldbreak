"""SHIELDBREAK asset pipeline. Runs inside headless Blender:

    blender -b --factory-startup -P tools/assetgen/build.py -- [--check] [--no-render]

Builds every spec in art/specs/, validates it against art/style/, exports FBX to
game/Assets/_Project/Art/Generated/, writes manifest.json and art/contact-sheet.png.
--check rebuilds and compares geometry hashes against the manifest without writing anything.
"""
import hashlib
import importlib
import json
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import bpy  # noqa: E402

from lib.meshkit import MeshBuilder  # noqa: E402
from lib.palette import Palette  # noqa: E402
from lib import contact_sheet, validate  # noqa: E402

GENERATOR_VERSION = 1
REPO = os.path.dirname(os.path.dirname(HERE))
STYLE_DIR = os.path.join(REPO, "art", "style")
SPEC_DIR = os.path.join(REPO, "art", "specs")
UNITY_ART = os.path.join(REPO, "game", "Assets", "_Project", "Art")
OUT_DIR = os.path.join(UNITY_ART, "Generated")
PALETTE_PNG = os.path.join(UNITY_ART, "Palette", "palette.png")
MANIFEST = os.path.join(OUT_DIR, "manifest.json")
CONTACT_SHEET = os.path.join(REPO, "art", "contact-sheet.png")


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 16), b""):
            h.update(chunk)
    return h.hexdigest()


def rel(path):
    return os.path.relpath(path, REPO).replace("\\", "/")


def make_palette_material(image, name):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    bsdf = next(n for n in nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Roughness"].default_value = 0.9
    tex = nodes.new("ShaderNodeTexImage")
    tex.image = image
    tex.interpolation = "Closest"
    mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def export_fbx(obj, path):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={"MESH"},
        apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=True, mesh_smooth_type="FACE", use_mesh_modifiers=True,
        add_leaf_bones=False, bake_anim=False, path_mode="STRIP")


def main(argv):
    check = "--check" in argv
    render = "--no-render" not in argv and not check

    with open(os.path.join(STYLE_DIR, "style.json"), "r", encoding="utf-8") as f:
        style = json.load(f)
    palette_json = os.path.join(STYLE_DIR, "palette.json")
    palette = Palette(palette_json)

    bpy.ops.wm.read_factory_settings(use_empty=True)
    os.makedirs(os.path.dirname(PALETTE_PNG), exist_ok=True)
    os.makedirs(OUT_DIR, exist_ok=True)
    if check:
        image = palette.write_png(bpy, os.path.join(bpy.app.tempdir, "palette_check.png"))
    else:
        image = palette.write_png(bpy, PALETTE_PNG)
    material = make_palette_material(image, style["global"]["material_name"])

    old = {}
    if os.path.exists(MANIFEST):
        with open(MANIFEST, "r", encoding="utf-8") as f:
            old = {a["name"]: a for a in json.load(f)["assets"]}

    style_hash = hashlib.sha256()
    for p in (palette_json, os.path.join(STYLE_DIR, "style.json")):
        style_hash.update(open(p, "rb").read())
    style_hash = style_hash.hexdigest()

    entries, failures, seen = [], 0, set()
    for fname in sorted(os.listdir(SPEC_DIR)):
        if not fname.endswith(".json"):
            continue
        spec_path = os.path.join(SPEC_DIR, fname)
        with open(spec_path, "r", encoding="utf-8") as f:
            spec = json.load(f)
        name = spec.get("name", fname)
        seen.add(name)
        try:
            gen = importlib.import_module(f"generators.{spec['generator']}")
        except (KeyError, ModuleNotFoundError) as e:
            print(f"[FAIL] {name}: unknown generator ({e})")
            failures += 1
            continue

        errors = validate.check_spec(spec, style, palette, gen)
        if os.path.splitext(fname)[0] != name:
            errors.append(f"spec file name must equal asset name '{name}'")
        if errors:
            print(f"[FAIL] {name}:\n  - " + "\n  - ".join(errors))
            failures += 1
            continue

        builder = MeshBuilder(palette)
        gen.build(builder, spec["params"], spec["colors"], random.Random(spec.get("seed", 0)))
        obj = builder.to_object(name, material)
        errors, tris = validate.check_object(obj, spec, style, palette)
        geo = validate.geometry_hash(obj)
        if errors:
            print(f"[FAIL] {name} ({tris} tris):\n  - " + "\n  - ".join(errors))
            failures += 1
            continue

        fbx = os.path.join(OUT_DIR, name + ".fbx")
        entry = {
            "name": name, "category": spec["category"], "generator": spec["generator"],
            "spec": rel(spec_path), "tris": tris, "geometry_sha256": geo,
            "fbx": rel(fbx),
        }
        prev = old.get(name)
        if check:
            if prev is None:
                print(f"[DRIFT] {name}: not in manifest")
                failures += 1
            elif prev["geometry_sha256"] != geo:
                print(f"[DRIFT] {name}: geometry hash differs from manifest")
                failures += 1
            else:
                print(f"[OK] {name}: reproduced ({tris} tris)")
            continue

        unchanged = prev and prev["geometry_sha256"] == geo and os.path.exists(fbx) \
            and prev.get("fbx_sha256") == sha256_file(fbx)
        if not unchanged:
            export_fbx(obj, fbx)
        entry["fbx_sha256"] = sha256_file(fbx)
        print(f"[OK] {name}: {tris} tris, {'unchanged' if unchanged else 'exported'}")
        entries.append(dict(entry, object=obj))

    if check:
        for stale in sorted(set(old) - seen):
            print(f"[DRIFT] {stale}: in manifest but has no spec")
            failures += 1
        print("CHECK", "FAILED" if failures else "PASSED")
        return 1 if failures else 0

    if failures:
        print(f"{failures} asset(s) failed; manifest and contact sheet not written")
        return 1

    names = {e["name"] for e in entries}
    for stale in sorted(set(old) - names):
        print(f"[WARN] {stale}: in old manifest but has no spec anymore (FBX left in place)")

    manifest = {
        "generator_version": GENERATOR_VERSION,
        "style_sha256": style_hash,
        "palette_png_sha256": sha256_file(PALETTE_PNG),
        "assets": [{k: v for k, v in e.items() if k != "object"} for e in entries],
    }
    with open(MANIFEST, "w", encoding="utf-8", newline="\n") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")

    if render:
        engine = contact_sheet.render(entries, material, palette, CONTACT_SHEET)
        print(f"contact sheet: {rel(CONTACT_SHEET)} ({engine})")
    print("BUILD PASSED")
    return 0


if __name__ == "__main__":
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    code = 1
    try:
        code = main(argv)
    finally:
        sys.stdout.flush()
        sys.exit(code)
