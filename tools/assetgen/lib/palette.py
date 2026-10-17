"""Palette loading, UV lookup and palette texture generation."""
import json


class Palette:
    def __init__(self, path):
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
        self.grid = data["grid"]
        self.cell_px = data["cell_px"]
        self.colors = data["colors"]
        self.names = [c["name"] for c in self.colors]
        if len(set(self.names)) != len(self.names):
            raise ValueError("palette.json: duplicate color names")
        if len(self.names) > self.grid * self.grid:
            raise ValueError("palette.json: more colors than grid cells")

    def index(self, name):
        try:
            return self.names.index(name)
        except ValueError:
            raise KeyError(f"unknown palette color '{name}'") from None

    def uv(self, index):
        """UV at the center of the palette cell. Row 0 is the top row of the texture."""
        col = index % self.grid
        row = index // self.grid
        return ((col + 0.5) / self.grid, 1.0 - (row + 0.5) / self.grid)

    def index_from_uv(self, u, v, eps):
        col = int(u * self.grid)
        row = int((1.0 - v) * self.grid)
        if not (0 <= col < self.grid and 0 <= row < self.grid):
            return None
        index = row * self.grid + col
        cu, cv = self.uv(index)
        if abs(cu - u) > eps or abs(cv - v) > eps:
            return None
        return index

    @staticmethod
    def _rgb(hex_color):
        h = hex_color.lstrip("#")
        return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))

    def write_png(self, bpy, path):
        size = self.grid * self.cell_px
        unused = self._rgb("#FF00FF")
        pixels = [0.0] * (size * size * 4)
        for py in range(size):           # Blender pixel rows start at the bottom
            row = self.grid - 1 - py // self.cell_px
            for px in range(size):
                index = row * self.grid + px // self.cell_px
                rgb = self._rgb(self.colors[index]["hex"]) if index < len(self.colors) else unused
                o = (py * size + px) * 4
                pixels[o:o + 4] = (rgb[0], rgb[1], rgb[2], 1.0)
        img = bpy.data.images.new("palette", size, size, alpha=False)
        img.pixels = pixels
        img.filepath_raw = path
        img.file_format = "PNG"
        img.save()
        return img
