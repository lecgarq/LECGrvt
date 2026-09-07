"""Generate the LECG Revit ribbon icon family.

The source geometry uses a 64 x 64 technical-drawing grid. Each icon is
exported as an SVG master plus transparent 32 px and 16 px PNGs.
"""

from __future__ import annotations

import math
from pathlib import Path
from xml.sax.saxutils import escape

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
SVG_DIR = ROOT / "src" / "Resources" / "IconSources"
PNG_DIR = ROOT / "src" / "Resources" / "Images"
REVIEW_DIR = ROOT / "docs" / "review"

BLUE = "#1E4257"
INK = "#1E1E1C"
WATER = "#CFE0EB"
PAPER = "#F4F3EF"
STROKE = 4.0


class Icon:
    def __init__(self) -> None:
        self.ops: list[tuple] = []

    def line(self, *points: tuple[float, float], color: str = BLUE, width: float = STROKE) -> "Icon":
        self.ops.append(("line", points, color, width))
        return self

    def dash(self, a: tuple[float, float], b: tuple[float, float], color: str = BLUE, width: float = STROKE) -> "Icon":
        x1, y1 = a
        x2, y2 = b
        length = math.hypot(x2 - x1, y2 - y1)
        if length == 0:
            return self
        ux, uy = (x2 - x1) / length, (y2 - y1) / length
        step, dash_len = 8.0, 4.0
        offset = 0.0
        while offset < length:
            end = min(offset + dash_len, length)
            self.line((x1 + ux * offset, y1 + uy * offset), (x1 + ux * end, y1 + uy * end), color=color, width=width)
            offset += step
        return self

    def rect(self, box: tuple[float, float, float, float], *, fill: str | None = None,
             stroke: str | None = BLUE, width: float = STROKE, radius: float = 2.0) -> "Icon":
        self.ops.append(("rect", box, fill, stroke, width, radius))
        return self

    def ellipse(self, box: tuple[float, float, float, float], *, fill: str | None = None,
                stroke: str | None = BLUE, width: float = STROKE) -> "Icon":
        self.ops.append(("ellipse", box, fill, stroke, width))
        return self

    def polygon(self, points: tuple[tuple[float, float], ...], *, fill: str | None = None,
                stroke: str | None = BLUE, width: float = STROKE) -> "Icon":
        self.ops.append(("polygon", points, fill, stroke, width))
        return self

    def arrow(self, a: tuple[float, float], b: tuple[float, float], *, color: str = BLUE,
              width: float = STROKE, head: float = 6.0) -> "Icon":
        self.line(a, b, color=color, width=width)
        angle = math.atan2(b[1] - a[1], b[0] - a[0])
        left = (b[0] - head * math.cos(angle - math.pi / 4), b[1] - head * math.sin(angle - math.pi / 4))
        right = (b[0] - head * math.cos(angle + math.pi / 4), b[1] - head * math.sin(angle + math.pi / 4))
        self.line(left, b, right, color=color, width=width)
        return self

    def render(self, size: int) -> Image.Image:
        scale = max(8, 256 // size)
        canvas = size * scale
        image = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
        draw = ImageDraw.Draw(image)

        def pts(values):
            return [(round(x / 64 * canvas), round(y / 64 * canvas)) for x, y in values]

        for op in self.ops:
            kind = op[0]
            if kind == "line":
                _, values, color, width = op
                xy = pts(values)
                w = max(1, round(width / 64 * canvas))
                draw.line(xy, fill=color, width=w, joint="curve")
                radius = w / 2
                for x, y in (xy[0], xy[-1]):
                    draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=color)
            elif kind == "rect":
                _, box, fill, stroke, width, radius = op
                x1, y1, x2, y2 = [round(v / 64 * canvas) for v in box]
                w = max(1, round(width / 64 * canvas))
                draw.rounded_rectangle((x1, y1, x2, y2), radius=round(radius / 64 * canvas), fill=fill,
                                       outline=stroke, width=w if stroke else 1)
            elif kind == "ellipse":
                _, box, fill, stroke, width = op
                values = [round(v / 64 * canvas) for v in box]
                w = max(1, round(width / 64 * canvas))
                draw.ellipse(values, fill=fill, outline=stroke, width=w if stroke else 1)
            elif kind == "polygon":
                _, values, fill, stroke, width = op
                xy = pts(values)
                draw.polygon(xy, fill=fill)
                if stroke:
                    w = max(1, round(width / 64 * canvas))
                    draw.line(xy + [xy[0]], fill=stroke, width=w, joint="curve")

        return image.resize((size, size), Image.Resampling.LANCZOS)

    def svg(self, title: str) -> str:
        elements: list[str] = []
        for op in self.ops:
            kind = op[0]
            if kind == "line":
                _, points, color, width = op
                values = " ".join(f"{x:g},{y:g}" for x, y in points)
                elements.append(f'<polyline points="{values}" fill="none" stroke="{color}" stroke-width="{width:g}" stroke-linecap="round" stroke-linejoin="round"/>')
            elif kind == "rect":
                _, (x1, y1, x2, y2), fill, stroke, width, radius = op
                elements.append(f'<rect x="{x1:g}" y="{y1:g}" width="{x2-x1:g}" height="{y2-y1:g}" rx="{radius:g}" fill="{fill or "none"}" stroke="{stroke or "none"}" stroke-width="{width:g}"/>')
            elif kind == "ellipse":
                _, (x1, y1, x2, y2), fill, stroke, width = op
                elements.append(f'<ellipse cx="{(x1+x2)/2:g}" cy="{(y1+y2)/2:g}" rx="{(x2-x1)/2:g}" ry="{(y2-y1)/2:g}" fill="{fill or "none"}" stroke="{stroke or "none"}" stroke-width="{width:g}"/>')
            elif kind == "polygon":
                _, points, fill, stroke, width = op
                values = " ".join(f"{x:g},{y:g}" for x, y in points)
                elements.append(f'<polygon points="{values}" fill="{fill or "none"}" stroke="{stroke or "none"}" stroke-width="{width:g}" stroke-linejoin="round"/>')
        content = "\n  ".join(elements)
        return f'''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" role="img" aria-labelledby="title">
  <title id="title">{escape(title)}</title>
  {content}
</svg>
'''


def slab(icon: Icon, y: float, *, contoured: bool = False, layers: int = 1) -> None:
    if contoured:
        icon.polygon(((10, y), (22, y - 5), (38, y - 2), (54, y - 6), (54, y + 5), (38, y + 9), (22, y + 6), (10, y + 10)), fill=WATER)
        icon.line((16, y + 1), (25, y - 1), (35, y + 2), (46, y - 2), width=2.5)
    else:
        icon.polygon(((10, y), (22, y - 5), (54, y - 5), (44, y), (44, y + 8), (10, y + 8)), fill=WATER)
    for offset in range(1, layers):
        yy = y + 7 * offset
        icon.line((10, yy), (44, yy), (54, yy - 5), width=2.5)


def home() -> Icon:
    i = Icon().polygon(((8, 31), (24, 15), (40, 31)), fill=None).line((12, 27), (12, 51), (36, 51), (36, 27))
    for x, y in ((44, 18), (52, 18), (44, 28), (52, 28)):
        i.rect((x, y, x + 5, y + 5), fill=BLUE, stroke=None, radius=1)
    return i


def clean_schemas() -> Icon:
    i = Icon()
    for y in (12, 24, 36):
        i.rect((8, y, 34, y + 9), fill=WATER if y == 24 else None, radius=4)
    i.line((42, 18), (55, 31), (42, 44)).line((55, 18), (42, 31), (55, 44))
    return i


def compact_styles() -> Icon:
    i = Icon()
    i.line((8, 14), (25, 14), width=2.5).line((8, 24), (29, 24), width=4).line((8, 35), (33, 35), width=6)
    i.polygon(((34, 11), (55, 11), (47, 25), (47, 43), (42, 48), (42, 25)), fill=WATER)
    return i


def purge_unused() -> Icon:
    i = Icon().line((18, 18), (48, 18)).line((25, 12), (41, 12)).rect((21, 20, 45, 52), fill=WATER, radius=3)
    i.line((29, 28), (29, 44), width=2.5).line((37, 28), (37, 44), width=2.5)
    for x, y in ((11, 16), (13, 28), (10, 39)):
        i.ellipse((x - 2, y - 2, x + 2, y + 2), fill=INK, stroke=None)
    return i


def formula_grouping() -> Icon:
    i = Icon().rect((34, 11, 56, 53), fill=WATER, radius=3)
    for y in (18, 32, 46):
        i.ellipse((8, y - 3, 14, y + 3), fill=BLUE, stroke=None).line((14, y), (25, y))
    i.line((25, 18), (25, 46)).arrow((25, 32), (34, 32), head=5)
    return i


def warnings() -> Icon:
    i = Icon().polygon(((24, 8), (43, 40), (5, 40)), fill=WATER)
    i.line((24, 19), (24, 29), color=INK).ellipse((21.5, 33, 26.5, 38), fill=INK, stroke=None)
    i.line((47, 17), (58, 17), width=3).line((47, 27), (58, 27), width=3).line((47, 37), (58, 37), width=3)
    return i


def cad_blocks() -> Icon:
    i = Icon().polygon(((7, 8), (35, 8), (43, 16), (43, 31), (35, 31), (35, 16), (27, 8)), fill=None)
    i.rect((13, 16, 23, 26), stroke=BLUE, width=2.5).arrow((27, 42), (39, 42), head=5)
    i.rect((43, 34, 56, 47), fill=WATER).ellipse((45, 50, 56, 61), fill=BLUE, stroke=None)
    return i


def batch_rename() -> Icon:
    i = Icon().polygon(((7, 14), (31, 14), (37, 20), (31, 26), (7, 26)), fill=WATER)
    i.polygon(((27, 38), (51, 38), (57, 44), (51, 50), (27, 50)), fill=None)
    i.arrow((18, 33), (46, 33), head=5)
    return i


def convert_family() -> Icon:
    i = Icon().rect((7, 10, 14, 47), fill=WATER).rect((14, 24, 27, 35), fill=None)
    i.arrow((30, 29), (43, 29), head=5).dash((38, 45), (59, 45)).rect((45, 34, 56, 45), fill=WATER)
    return i


def convert_shared() -> Icon:
    i = Icon().polygon(((9, 21), (22, 13), (35, 21), (35, 38), (22, 46), (9, 38)), fill=WATER)
    i.ellipse((33, 18, 47, 32)).ellipse((43, 28, 57, 42)).line((38, 43), (55, 17), color=INK)
    return i


def category_changer() -> Icon:
    i = Icon().polygon(((8, 21), (22, 13), (36, 21), (36, 39), (22, 47), (8, 39)), fill=WATER)
    i.rect((42, 12, 56, 24), fill=None).rect((42, 40, 56, 52), fill=WATER).arrow((49, 29), (49, 38), head=5)
    return i


def shared_to_family_param() -> Icon:
    i = Icon()
    for x, y in ((8, 18), (8, 32), (8, 46)):
        i.ellipse((x, y - 3, x + 6, y + 3), fill=BLUE, stroke=None).line((14, y), (22, 32), width=2.5)
    i.arrow((23, 32), (35, 32), head=5).rect((38, 13, 57, 51), fill=WATER)
    i.ellipse((44, 26, 51, 33), fill=INK, stroke=None).line((47.5, 33), (47.5, 42), color=INK, width=3)
    return i


def filter_copy() -> Icon:
    i = Icon().rect((6, 11, 27, 49), fill=None).rect((37, 15, 58, 53), fill=WATER)
    i.polygon(((10, 18), (23, 18), (19, 26), (19, 34), (15, 37), (15, 26)), fill=None, width=2.5)
    i.arrow((27, 31), (37, 31), head=4)
    return i


def assign_material() -> Icon:
    i = Icon()
    slab(i, 37)
    i.polygon(((37, 8), (46, 22), (37, 31), (28, 22)), fill=None)
    i.line((32, 22), (42, 22), width=2.5)
    return i


def offset_elevations() -> Icon:
    i = Icon().dash((8, 18), (55, 18)).dash((8, 46), (55, 46)).rect((13, 29, 40, 36), fill=WATER)
    i.arrow((49, 22), (49, 42), head=5).arrow((49, 42), (49, 22), head=5)
    return i


def reset_slabs() -> Icon:
    i = Icon().line((8, 23), (20, 18), (34, 26), (54, 16), width=5)
    i.arrow((44, 30), (35, 42), head=5).line((10, 48), (54, 48), width=5)
    return i


def simplify_points() -> Icon:
    i = Icon().line((7, 18), (14, 12), (21, 20), (28, 13), (35, 21), width=2.5).arrow((39, 18), (49, 18), head=4)
    for x, y in ((7, 18), (14, 12), (21, 20), (28, 13), (35, 21)):
        i.ellipse((x - 2, y - 2, x + 2, y + 2), fill=INK, stroke=None)
    i.line((7, 46), (21, 39), (35, 46), width=2.5)
    for x, y in ((7, 46), (21, 39), (35, 46)):
        i.ellipse((x - 2, y - 2, x + 2, y + 2), fill=BLUE, stroke=None)
    return i


def align_edges() -> Icon:
    i = Icon().dash((32, 8), (32, 56), color=INK, width=2.5)
    i.line((9, 18), (24, 18), (24, 30), (32, 30), width=5).line((55, 46), (40, 46), (40, 36), (32, 36), width=5)
    return i


def update_contours() -> Icon:
    i = Icon().line((8, 20), (16, 14), (26, 18), (36, 12), (48, 17), width=2.5)
    i.line((8, 31), (18, 25), (29, 29), (41, 23), (55, 28), width=2.5)
    i.line((8, 43), (19, 37), (31, 41), (44, 35), (55, 40), width=2.5)
    i.arrow((47, 49), (55, 43), head=4)
    return i


def change_level() -> Icon:
    i = Icon().dash((7, 15), (44, 15), width=2.5).dash((7, 49), (44, 49), width=2.5).rect((10, 29, 38, 36), fill=WATER)
    i.arrow((52, 20), (52, 44), head=5).arrow((52, 44), (52, 20), head=5)
    return i


def floor_to_toposolid() -> Icon:
    i = Icon()
    slab(i, 15)
    i.arrow((32, 29), (32, 38), head=4)
    slab(i, 46, contoured=True)
    return i


def toposolid_to_floor() -> Icon:
    i = Icon()
    slab(i, 14, contoured=True)
    i.arrow((32, 31), (32, 40), head=4)
    slab(i, 49)
    return i


def fix_points() -> Icon:
    i = Icon().line((8, 41), (20, 29), (31, 35), (44, 17), (56, 24), width=2.5)
    for x, y in ((8, 41), (20, 29), (31, 35), (44, 17), (56, 24)):
        i.ellipse((x - 2.5, y - 2.5, x + 2.5, y + 2.5), fill=BLUE, stroke=None)
    i.ellipse((28, 32, 34, 38), fill=INK, stroke=None).line((25, 46), (39, 46), color=INK).line((32, 39), (32, 53), color=INK)
    return i


def split_boundaries() -> Icon:
    i = Icon().rect((7, 14, 57, 50), fill=WATER, radius=8).dash((32, 10), (32, 54), color=INK, width=2.5)
    i.ellipse((14, 24, 25, 35), fill=None).ellipse((40, 29, 50, 40), fill=None)
    i.arrow((27, 10), (27, 22), head=4).arrow((37, 54), (37, 42), head=4)
    return i


def divide_toposolid() -> Icon:
    i = Icon()
    slab(i, 13, layers=3)
    i.arrow((32, 34), (32, 42), head=4)
    i.line((8, 50), (23, 50), width=5).line((25, 50), (40, 50), width=5).line((42, 50), (57, 50), width=5)
    return i


def align_master() -> Icon:
    i = Icon().dash((32, 8), (32, 56), color=INK, width=2.5).dash((8, 32), (56, 32), color=INK, width=2.5)
    for box in ((10, 17, 26, 27), (38, 12, 54, 27), (12, 38, 26, 49), (38, 38, 52, 53)):
        i.rect(box, fill=WATER)
    return i


def align_horizontal(kind: str) -> Icon:
    i = Icon()
    x = {"left": 12, "center": 32, "right": 52}[kind]
    i.line((x, 8), (x, 56), color=INK, width=2.5)
    widths = (18, 28, 13)
    ys = (14, 29, 44)
    for w, y in zip(widths, ys):
        if kind == "left":
            x1, x2 = x, x + w
        elif kind == "right":
            x1, x2 = x - w, x
        else:
            x1, x2 = x - w / 2, x + w / 2
        i.rect((x1, y, x2, y + 8), fill=WATER, radius=1.5)
    return i


def align_vertical(kind: str) -> Icon:
    i = Icon()
    y = {"top": 12, "middle": 32, "bottom": 52}[kind]
    i.line((8, y), (56, y), color=INK, width=2.5)
    heights = (16, 26, 12)
    xs = (13, 29, 45)
    for h, x in zip(heights, xs):
        if kind == "top":
            y1, y2 = y, y + h
        elif kind == "bottom":
            y1, y2 = y - h, y
        else:
            y1, y2 = y - h / 2, y + h / 2
        i.rect((x, y1, x + 8, y2), fill=WATER, radius=1.5)
    return i


def distribute(horizontal: bool) -> Icon:
    i = Icon()
    if horizontal:
        for x in (8, 28, 48):
            i.rect((x, 20, x + 8, 44), fill=WATER, radius=1.5)
        i.arrow((18, 13), (26, 13), head=3).arrow((26, 13), (18, 13), head=3)
        i.arrow((38, 13), (46, 13), head=3).arrow((46, 13), (38, 13), head=3)
    else:
        for y in (8, 28, 48):
            i.rect((20, y, 44, y + 8), fill=WATER, radius=1.5)
        i.arrow((13, 18), (13, 26), head=3).arrow((13, 26), (13, 18), head=3)
        i.arrow((13, 38), (13, 46), head=3).arrow((13, 46), (13, 38), head=3)
    return i


def type_to_linked() -> Icon:
    i = Icon().polygon(((6, 23), (17, 16), (28, 23), (28, 37), (17, 44), (6, 37)), fill=WATER)
    for x, y in ((42, 10), (48, 27), (42, 44)):
        i.rect((x, y, x + 14, y + 10), fill=None)
        i.arrow((28, 30), (x, y + 5), head=4, width=2.5)
    return i


def render_match() -> Icon:
    i = Icon().rect((7, 13, 26, 51), fill=WATER).ellipse((38, 13, 57, 32), fill=None)
    i.line((47.5, 13), (47.5, 32), width=2.5).line((38, 22.5), (57, 22.5), width=2.5)
    i.arrow((28, 37), (41, 37), head=5).arrow((41, 44), (28, 44), head=5)
    return i


def pbr_material() -> Icon:
    i = Icon().ellipse((10, 8, 54, 52), fill=WATER)
    i.line((32, 9), (32, 51), width=2.5).line((11, 30), (53, 30), width=2.5)
    i.line((34, 48), (50, 32), width=2.5).line((38, 50), (52, 36), width=2.5)
    i.ellipse((20, 18, 24, 22), fill=INK, stroke=None).ellipse((25, 23, 29, 27), fill=INK, stroke=None)
    i.line((20, 55), (44, 55), color=INK, width=5)
    return i


def sexy_revit() -> Icon:
    i = Icon().rect((7, 15, 48, 52), fill=WATER)
    i.polygon(((14, 43), (14, 30), (24, 22), (37, 28), (37, 43)), fill=None)
    i.line((14, 43), (41, 43), width=2.5)
    i.line((53, 8), (53, 18), color=INK, width=3).line((48, 13), (58, 13), color=INK, width=3)
    i.line((54, 27), (54, 34), color=INK, width=2.5).line((50.5, 30.5), (57.5, 30.5), color=INK, width=2.5)
    return i


ICONS: dict[str, tuple[str, Icon]] = {
    "Home": ("Home", home()),
    "CleanSchemas": ("Clean Schemas", clean_schemas()),
    "CompactStyles": ("Compact Styles", compact_styles()),
    "PurgeUnused": ("Purge Unused", purge_unused()),
    "FormulaGrouping": ("Formula Grouping", formula_grouping()),
    "Warnings": ("Warnings", warnings()),
    "CadBlocks": ("CAD Blocks", cad_blocks()),
    "BatchRename": ("Batch Rename", batch_rename()),
    "ConvertFamily": ("Convert Family", convert_family()),
    "ConvertShared": ("Convert Shared", convert_shared()),
    "CategoryChanger": ("Category Changer", category_changer()),
    "SharedToFamilyParam": ("Shared to Family Parameter", shared_to_family_param()),
    "FilterCopy": ("Filter Copy", filter_copy()),
    "AssignMaterial": ("Assign Material", assign_material()),
    "OffsetElevations": ("Offset Elevations", offset_elevations()),
    "ResetSlabs": ("Reset Slabs", reset_slabs()),
    "SimplifyPoints": ("Simplify Points", simplify_points()),
    "AlignEdges": ("Align Edges", align_edges()),
    "UpdateContours": ("Update Contours", update_contours()),
    "ChangeLevel": ("Change Level", change_level()),
    "FloorToToposolid": ("Floor to Toposolid", floor_to_toposolid()),
    "ToposolidToFloor": ("Toposolid to Floor", toposolid_to_floor()),
    "FixPoints": ("Fix Points", fix_points()),
    "SplitBoundaries": ("Split Boundaries", split_boundaries()),
    "DivideToposolid": ("Divide Toposolid", divide_toposolid()),
    "AlignMaster": ("Align Elements", align_master()),
    "AlignLeft": ("Align Left", align_horizontal("left")),
    "AlignCenter": ("Align Center", align_horizontal("center")),
    "AlignRight": ("Align Right", align_horizontal("right")),
    "AlignTop": ("Align Top", align_vertical("top")),
    "AlignMiddle": ("Align Middle", align_vertical("middle")),
    "AlignBottom": ("Align Bottom", align_vertical("bottom")),
    "DistributeH": ("Distribute Horizontally", distribute(True)),
    "DistributeV": ("Distribute Vertically", distribute(False)),
    "TypeToLinked": ("Type to Linked", type_to_linked()),
    "RenderMatch": ("Render Match", render_match()),
    "PbrMaterial": ("PBR Material", pbr_material()),
    "SexyRevit": ("Sexy Revit", sexy_revit()),
}


def make_contact_sheet() -> None:
    columns = 6
    cell_w, cell_h = 184, 166
    rows = math.ceil(len(ICONS) / columns)
    sheet = Image.new("RGB", (columns * cell_w, rows * cell_h), PAPER)
    draw = ImageDraw.Draw(sheet)
    for index, (name, (title, icon)) in enumerate(ICONS.items()):
        column, row = index % columns, index // columns
        x, y = column * cell_w, row * cell_h
        preview32 = icon.render(32).resize((64, 64), Image.Resampling.NEAREST)
        preview16 = icon.render(16).resize((64, 64), Image.Resampling.NEAREST)
        sheet.paste(preview32, (x + 22, y + 14), preview32)
        sheet.paste(preview16, (x + 98, y + 14), preview16)
        draw.text((x + 43, y + 80), "32", fill=BLUE)
        draw.text((x + 119, y + 80), "16", fill=BLUE)
        bbox = draw.textbbox((0, 0), title)
        text_x = x + (cell_w - (bbox[2] - bbox[0])) / 2
        draw.text((text_x, y + 105), title, fill=INK)
        draw.text((x + 8, y + 137), f"Lecg{name}", fill=BLUE)
    sheet.save(REVIEW_DIR / "LECG-icon-family-contact-sheet.png", optimize=True)


def validate_outputs() -> None:
    for name in ICONS:
        svg_path = SVG_DIR / f"Lecg{name}.svg"
        svg = svg_path.read_text(encoding="utf-8")
        if 'viewBox="0 0 64 64"' not in svg:
            raise ValueError(f"Invalid SVG viewBox: {svg_path}")
        for size in (16, 32):
            png_path = PNG_DIR / f"Lecg{name}_{size}.png"
            with Image.open(png_path) as image:
                if image.mode != "RGBA" or image.size != (size, size):
                    raise ValueError(f"Invalid PNG contract: {png_path} ({image.mode}, {image.size})")
                alpha_min, alpha_max = image.getchannel("A").getextrema()
                if alpha_min != 0 or alpha_max < 200:
                    raise ValueError(f"PNG must contain transparent and strongly visible pixels: {png_path}")


def main() -> None:
    SVG_DIR.mkdir(parents=True, exist_ok=True)
    PNG_DIR.mkdir(parents=True, exist_ok=True)
    REVIEW_DIR.mkdir(parents=True, exist_ok=True)
    for name, (title, icon) in ICONS.items():
        (SVG_DIR / f"Lecg{name}.svg").write_text(icon.svg(title), encoding="utf-8")
        for size in (16, 32):
            icon.render(size).save(PNG_DIR / f"Lecg{name}_{size}.png", optimize=True)
    make_contact_sheet()
    validate_outputs()
    print(f"Generated and validated {len(ICONS)} SVG masters and {len(ICONS) * 2} PNG ribbon assets.")


if __name__ == "__main__":
    main()
