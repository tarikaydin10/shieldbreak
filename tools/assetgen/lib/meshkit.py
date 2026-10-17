"""Low-poly mesh construction kit. Every face carries a palette index; UVs are derived from it."""
import math

import bmesh
import bpy
from mathutils import Matrix, Vector


class MeshBuilder:
    def __init__(self, palette):
        self.palette = palette
        self.bm = bmesh.new()
        self.pal = self.bm.faces.layers.int.new("pal")

    # --- tagging -------------------------------------------------------------

    def _tag_faces(self, faces, color):
        index = self.palette.index(color)
        for f in faces:
            f[self.pal] = index
            f.smooth = False

    def _tag_verts(self, verts, color):
        self._tag_faces({f for v in verts for f in v.link_faces}, color)

    def face(self, verts, color):
        f = self.bm.faces.new(verts)
        self._tag_faces([f], color)
        return f

    # --- primitives ----------------------------------------------------------

    def cylinder(self, color, r_bottom, r_top, z0, z1, segments, matrix=None):
        """Capped cylinder/cone along +Z from z0 to z1."""
        m = (matrix or Matrix()) @ Matrix.Translation((0, 0, (z0 + z1) * 0.5))
        res = bmesh.ops.create_cone(self.bm, cap_ends=True, cap_tris=False, segments=segments,
                                    radius1=r_bottom, radius2=r_top, depth=z1 - z0, matrix=m)
        self._tag_verts(res["verts"], color)
        return res["verts"]

    def box(self, color, size, center, matrix=None):
        m = (matrix or Matrix()) @ Matrix.Translation(center) @ Matrix.Diagonal((*size, 1.0))
        res = bmesh.ops.create_cube(self.bm, size=1.0, matrix=m)
        self._tag_verts(res["verts"], color)
        return res["verts"]

    def bipyramid(self, color, ring, z_ring, z_bottom, z_top):
        """Closed double pyramid: ring of (x, y) points at z_ring, apexes on the Z axis."""
        bottom = self.bm.verts.new((0, 0, z_bottom))
        top = self.bm.verts.new((0, 0, z_top))
        rv = [self.bm.verts.new((x, y, z_ring)) for x, y in ring]
        n = len(rv)
        for i in range(n):
            a, b = rv[i], rv[(i + 1) % n]
            self.face((a, b, top), color)
            self.face((b, a, bottom), color)

    def lathe(self, profile, segments, color_fn, angle_offset=0.0, cap_bottom=None):
        """Revolve (radius, z) profile around +Z. A radius of 0 at the end closes with an apex.
        color_fn(band, segment) -> palette color name."""
        rings = []
        for r, z in profile:
            if r <= 0.0:
                rings.append([self.bm.verts.new((0, 0, z))])
                continue
            ring = []
            for s in range(segments):
                a = angle_offset + 2.0 * math.pi * s / segments
                ring.append(self.bm.verts.new((r * math.cos(a), r * math.sin(a), z)))
            rings.append(ring)
        for band in range(len(rings) - 1):
            lo, hi = rings[band], rings[band + 1]
            for s in range(segments):
                s1 = (s + 1) % segments
                color = color_fn(band, s)
                if len(hi) == 1:
                    self.face((lo[s], lo[s1], hi[0]), color)
                elif len(lo) == 1:
                    self.face((lo[0], hi[s1], hi[s]), color)
                else:
                    self.face((lo[s], lo[s1], hi[s1], hi[s]), color)
        if cap_bottom and len(rings[0]) > 1:
            self.face(list(reversed(rings[0])), cap_bottom)
        return rings

    # --- whole-mesh ops ------------------------------------------------------

    def transform(self, matrix):
        bmesh.ops.transform(self.bm, matrix=matrix, verts=self.bm.verts)

    def translate(self, offset):
        self.transform(Matrix.Translation(Vector(offset)))

    def to_object(self, name, material):
        bm = self.bm
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        uv_layer = bm.loops.layers.uv.new("UVMap")
        for f in bm.faces:
            uv = self.palette.uv(f[self.pal])
            for loop in f.loops:
                loop[uv_layer].uv = uv
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        mesh.materials.append(material)
        obj = bpy.data.objects.new(name, mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj
