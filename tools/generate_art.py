"""Generate the game's sprites and arena thumbnails (our own art, no licensing questions).

    pip install pillow
    python tools/generate_art.py         -> writes Assets/Art/Sprites/*.png

Import settings (sprite mode, pixels-per-unit) are applied by SSRP > Build Project in Unity.
Arena thumbnails mirror the layouts built in Assets/Editor/Build/ArenaFactory.cs.
"""
import os
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Art", "Sprites")

BG = (8, 11, 24, 255)
FIELD = (14, 19, 38, 255)
WALL = (90, 110, 160, 255)
P1 = (64, 204, 255, 255)
P2 = (255, 115, 77, 255)
SOLID = (110, 122, 150, 255)
BRICK = (240, 150, 60, 255)
BOUNCE = (80, 230, 120, 255)
SPEED = (90, 220, 255, 255)
SLOW = (190, 120, 255, 255)


def save(img, name):
    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, name + ".png")
    img.save(path)
    print("wrote", os.path.normpath(path))


def square():
    save(Image.new("RGBA", (32, 32), (255, 255, 255, 255)), "square")


def circle(size=128, name="circle", soft=False):
    scale = 4
    big = Image.new("RGBA", (size * scale, size * scale), (0, 0, 0, 0))
    d = ImageDraw.Draw(big)
    d.ellipse((2, 2, size * scale - 3, size * scale - 3), fill=(255, 255, 255, 255))
    img = big.resize((size, size), Image.LANCZOS)
    if soft:
        img = img.filter(ImageFilter.GaussianBlur(size / 10))
    save(img, name)


def tile_base(colour, inset=2):
    img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    r, g, b, a = colour
    d.rectangle((0, 0, 31, 31), fill=(r // 2, g // 2, b // 2, 255))
    d.rectangle((inset, inset, 31 - inset, 31 - inset), fill=colour)
    d.line((inset, inset, 31 - inset, inset), fill=(min(255, r + 60), min(255, g + 60), min(255, b + 60), 255), width=2)
    return img, d


def tiles():
    img, d = tile_base(SOLID)
    d.rectangle((10, 10, 21, 21), outline=(70, 80, 100, 255), width=2)
    save(img, "tile_solid")

    img, d = tile_base(BRICK)
    d.line((2, 16, 29, 16), fill=(150, 80, 30, 255), width=2)
    d.line((16, 2, 16, 15), fill=(150, 80, 30, 255), width=2)
    d.line((9, 17, 9, 29), fill=(150, 80, 30, 255), width=2)
    d.line((23, 17, 23, 29), fill=(150, 80, 30, 255), width=2)
    save(img, "tile_breakable")

    d.line((4, 5, 12, 13, ), fill=(40, 20, 10, 255), width=2)
    d.line((12, 13, 9, 22), fill=(40, 20, 10, 255), width=2)
    d.line((12, 13, 22, 18), fill=(40, 20, 10, 255), width=2)
    d.line((22, 18, 28, 27), fill=(40, 20, 10, 255), width=2)
    save(img, "tile_breakable_cracked")

    img, d = tile_base(BOUNCE)
    for y in (8, 16):
        d.polygon([(8, y + 8), (16, y), (24, y + 8)], outline=(20, 90, 40, 255), width=3)
    save(img, "tile_bounce")

    img = Image.new("RGBA", (32, 32), (SPEED[0], SPEED[1], SPEED[2], 70))
    d = ImageDraw.Draw(img)
    for x in (6, 16):
        d.line((x, 8, x + 8, 16), fill=SPEED, width=3)
        d.line((x + 8, 16, x, 24), fill=SPEED, width=3)
    save(img, "tile_speed")

    img = Image.new("RGBA", (32, 32), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.ellipse((3, 3, 28, 28), fill=SLOW, outline=(255, 255, 255, 255), width=2)
    d.line((16, 16, 16, 8), fill=(255, 255, 255, 255), width=3)
    d.line((16, 16, 22, 19), fill=(255, 255, 255, 255), width=3)
    save(img, "tile_slowmo")


# ---- arena thumbnails (world units: x -9..9, y -5..5; 1 unit = 24 px) ----
W, H, U = 480, 270, 24


def to_px(x, y):
    return (W / 2 + x * U, H / 2 - y * U)


def rect(d, x0, y0, x1, y1, colour):
    a, b = to_px(x0, y1), to_px(x1, y0)
    d.rectangle((a[0], a[1], b[0] - 1, b[1] - 1), fill=colour)


def thumb_base():
    img = Image.new("RGBA", (W, H), BG)
    d = ImageDraw.Draw(img)
    rect(d, -9.5, -5, 9.5, 5, FIELD)
    rect(d, -9.75, 5, 9.75, 5.5, WALL)
    rect(d, -9.75, -5.5, 9.75, -5, WALL)
    for i in range(-9, 10, 2):
        rect(d, -0.06, i * 0.5, 0.06, i * 0.5 + 0.5, (60, 70, 100, 255))
    rect(d, -8.45, -1, -8.15, 1, P1)
    rect(d, 8.15, -1, 8.45, 1, P2)
    cx, cy = to_px(0, 0)
    d.ellipse((cx - 4, cy - 4, cx + 4, cy + 4), fill=(255, 255, 255, 255))
    return img, d


def thumbnails():
    img, d = thumb_base()
    save(img, "arena_classic")

    img, d = thumb_base()
    rect(d, -0.5, 1, 0.5, 3, BRICK)
    rect(d, -0.5, -3, 0.5, -1, BRICK)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x0 = 4 if sx > 0 else -5
            y0 = 2.5 if sy > 0 else -3.5
            rect(d, x0, y0, x0 + 1, y0 + 1, SOLID)
        rect(d, sx * 4.5 - 0.5, -0.5, sx * 4.5 + 0.5, 0.5, BOUNCE)
    save(img, "arena_bunker")

    img, d = thumb_base()
    for sx in (-1, 1):
        for sy in (-1, 1):
            x0 = 2.5 if sx > 0 else -3.5
            y0 = 1 if sy > 0 else -2.5
            rect(d, x0, y0, x0 + 1, y0 + 1.5, SOLID)
            rect(d, sx * 6 - 0.5, sy * 4 - 0.5, sx * 6 + 0.5, sy * 4 + 0.5, BOUNCE)
    rect(d, -0.5, -1.5, 0.5, 1.5, (SPEED[0], SPEED[1], SPEED[2], 255))
    rect(d, -0.5, 3, 0.5, 3.5, SLOW)
    rect(d, -0.5, -3.5, 0.5, -3, SLOW)
    save(img, "arena_pillars")


def main():
    square()
    circle()
    circle(64, "soft_dot", soft=True)
    tiles()
    thumbnails()


if __name__ == "__main__":
    main()
