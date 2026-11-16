"""Build launcher and splash assets from the official Growfit logo."""

from __future__ import annotations

import math
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "assets"
SOURCE = ASSETS / "images" / "growfit-logo.png"
FOREST = (6, 19, 13, 255)
CROP = (190, 130, 640, 516)


def symbol_from_logo() -> Image.Image:
    source = Image.open(SOURCE).convert("RGBA")
    crop = source.crop(CROP)
    pixels = crop.load()
    width, height = crop.size
    distance = [[0.0 for _ in range(width)] for _ in range(height)]
    strong = [[False for _ in range(width)] for _ in range(height)]
    seed = (0, 0)
    seed_green = -1
    for y in range(height):
        for x in range(width):
            red, green, blue, _alpha = pixels[x, y]
            value = math.sqrt((red - 6) ** 2 + (green - 19) ** 2 + (blue - 13) ** 2)
            distance[y][x] = value
            if value >= 90:
                strong[y][x] = True
                if green > seed_green:
                    seed_green = green
                    seed = (x, y)

    seen = [[False for _ in range(width)] for _ in range(height)]
    stack = [seed]
    seen[seed[1]][seed[0]] = True
    while stack:
        x, y = stack.pop()
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if nx < 0 or ny < 0 or nx >= width or ny >= height or seen[ny][nx] or not strong[ny][nx]:
                continue
            seen[ny][nx] = True
            stack.append((nx, ny))

    extracted = Image.new("RGBA", (width, height))
    output = extracted.load()
    for y in range(height):
        for x in range(width):
            red, green, blue, _alpha = pixels[x, y]
            if seen[y][x]:
                output[x, y] = (red, green, blue, 255)
                continue
            adjacent = any(
                0 <= nx < width and 0 <= ny < height and seen[ny][nx]
                for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1), (x - 1, y - 1), (x + 1, y - 1), (x - 1, y + 1), (x + 1, y + 1))
            )
            if adjacent and distance[y][x] >= 55:
                alpha = int(min(255, (distance[y][x] - 55) / 35 * 255))
                output[x, y] = (red, green, blue, alpha)
    bounds = extracted.getbbox()
    if bounds is None:
        raise RuntimeError("The G symbol could not be separated from the logo background.")
    return extracted.crop(bounds)


def fit(symbol: Image.Image, canvas: int, fraction: float) -> Image.Image:
    target = Image.new("RGBA", (canvas, canvas), (0, 0, 0, 0))
    limit = int(canvas * fraction)
    scale = min(limit / symbol.width, limit / symbol.height)
    size = (max(1, int(symbol.width * scale)), max(1, int(symbol.height * scale)))
    resized = symbol.resize(size, Image.Resampling.LANCZOS)
    origin = ((canvas - size[0]) // 2, (canvas - size[1]) // 2)
    target.alpha_composite(resized, origin)
    return target


def monochrome(symbol: Image.Image) -> Image.Image:
    white = Image.new("RGBA", symbol.size, (255, 255, 255, 0))
    white.putalpha(symbol.getchannel("A"))
    return white


def main() -> None:
    symbol = symbol_from_logo()
    icon = Image.new("RGBA", (1024, 1024), FOREST)
    icon.alpha_composite(fit(symbol, 1024, 0.64))
    icon.convert("RGB").save(ASSETS / "icon.png", optimize=True)

    fit(symbol, 1024, 0.58).save(ASSETS / "android-icon-foreground.png", optimize=True)
    fit(monochrome(symbol), 1024, 0.58).save(ASSETS / "android-icon-monochrome.png", optimize=True)
    Image.new("RGB", (1024, 1024), FOREST[:3]).save(ASSETS / "android-icon-background.png", optimize=True)

    splash_width = 768
    splash_height = max(1, int(symbol.height * (splash_width / symbol.width)))
    symbol.resize((splash_width, splash_height), Image.Resampling.LANCZOS).save(ASSETS / "splash-icon.png", optimize=True)
    print(f"symbol {symbol.size[0]}x{symbol.size[1]}")


if __name__ == "__main__":
    main()
