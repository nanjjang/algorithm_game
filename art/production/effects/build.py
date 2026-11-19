"""Export the spell, enemy, loot and ritual pixel sprites and their frame rectangles."""
from pathlib import Path
import json
import math
import random

from PIL import Image, ImageColor, ImageDraw, ImageFont

ROOT = Path(__file__).parent
NEAREST = Image.Resampling.NEAREST
ELEMENTS = {
    "fire": ("#7D361B", "#FF8A1F", "#FFE08A"),
    "lightning": ("#5B4D2D", "#FFF36B", "#FFFFFF"),
    "arcane": ("#184850", "#4FE3C1", "#D6FFF3"),
    "ice": ("#314E83", "#9FDCFF", "#F4FBFF"),
    "dark": ("#271646", "#8A5CFF", "#D9C7FF"),
}
DANGER = ("#7E193A", "#FF2E88", "#FFF8EE")
DANGER_RED = ("#7C271F", "#FF3B30", "#FFF8EE")
GOLD = ("#734B23", "#FFD24A", "#FFF1AF")
SMOKE = ("#343345", "#6C687D", "#B1ADBC")
NEUTRAL = ("#131421", "#80899D", "#EEF1F7")


def point(x, y):
    return round(x), round(y)


def polar(x, y, r, a):
    return point(x + r * math.cos(a), y + r * math.sin(a))


def diamond(x, y, rx, ry):
    return [point(x, y - ry), point(x + rx, y), point(x, y + ry), point(x - rx, y)]


def alpha(color, value):
    return (*ImageColor.getrgb(color), value)


def star(d, x, y, radius, color, arms=4):
    nodes = [polar(x, y, radius if i % 2 == 0 else radius * .32, i * math.pi / arms - math.pi / 2)
             for i in range(arms * 2)]
    d.polygon(nodes, fill=color)


def bolts(d, origin, end, colors, f, weight=3, branch=True):
    x0, y0 = origin
    x1, y1 = end
    dx, dy = x1 - x0, y1 - y0
    length = max(1, math.hypot(dx, dy))
    points = [origin]
    for i in range(1, 7):
        t = i / 7
        jitter = ((i * 3 + f * 2) % 7 - 3) * min(2.5, length / 48)
        points.append(point(x0 + dx * t - dy / length * jitter, y0 + dy * t + dx / length * jitter))
    points.append(end)
    if branch:
        for i in (2, 4, 5):
            x, y = points[i]
            distance = min(15, length / 4)
            side = (-1) ** (i + f)
            end_branch = point(x + dx * .14 - dy / length * distance * side,
                               y + dy * .14 + dx / length * distance * side)
            d.line([(x, y), point(x * .55 + end_branch[0] * .45, y * .45 + end_branch[1] * .55),
                    end_branch], colors[0], width=2)
    d.line(points, colors[0], width=weight + 2)
    d.line(points, colors[1], width=weight)
    d.line(points, colors[2], width=max(1, weight // 3))


def glyph(d, kind, x, y, r, colors, f=0):
    dark, main, light = colors
    if kind == "fire":
        d.polygon([point(x - r, y + r * .33), point(x - r * .5, y - r * .3),
                   point(x - r * .12, y - r), point(x + r * .1, y - r * .3),
                   point(x + r * .57, y - r * .64), point(x + r, y + r * .2),
                   point(x + r * .58, y + r * .8), point(x - r * .5, y + r)], fill=dark)
        d.polygon([point(x - r * .72, y + r * .27), point(x - r * .15, y - r * .78),
                   point(x + r * .13, y - r * .08), point(x + r * .46, y - r * .4),
                   point(x + r * .75, y + r * .28), point(x + r * .41, y + r * .72),
                   point(x - r * .4, y + r * .72)], fill=main)
        d.polygon([point(x - r * .35, y + r * .55), point(x + r * .06, y - r * .26),
                   point(x + r * .43, y + r * .5), point(x + r * .14, y + r * .7)], fill=light)
    elif kind == "lightning":
        points = [point(x + .2 * r, y - r), point(x - .7 * r, y + .12 * r), point(x - .08 * r, y + .12 * r),
                  point(x - .44 * r, y + r), point(x + .8 * r, y - .23 * r), point(x + .21 * r, y - .23 * r)]
        d.polygon(points, fill=main, outline=dark)
        d.line([point(x + .16 * r, y - .62 * r), point(x - .36 * r, y), point(x + .2 * r, y),
                point(x - .13 * r, y + .44 * r)], fill=light, width=1)
    elif kind == "arcane":
        d.polygon(diamond(x, y, r, r * .82), fill=dark)
        d.polygon(diamond(x, y, r * .78, r * .62), fill=main)
        d.polygon(diamond(x, y, r * .42, r * .34), fill=light)
        d.line([point(x - r * .6, y), point(x, y - r * .5)], fill=light)
    elif kind == "ice":
        pts = [polar(x, y, r, a * math.pi / 3) for a in range(6)]
        d.polygon(pts, fill=dark)
        d.polygon([polar(x, y, r * .83, a * math.pi / 3) for a in range(6)], fill=main)
        d.polygon([pts[3], pts[4], pts[5], point(x + r * .3, y)], fill=light)
        d.line([pts[0], point(x + r * .22, y), pts[2]], fill=dark)
    else:
        d.ellipse((x - r, y - r, x + r, y + r), fill=dark, outline=main, width=max(1, round(r / 5)))
        d.arc((x - r + 2, y - r + 2, x + r - 2, y + r - 2), 205 + f * 20, 302 + f * 20,
              fill=light, width=max(1, round(r / 7)))
        d.ellipse((x - r // 4, y - r // 4, x + r // 4, y + r // 4), fill=light)


def ring(im, f, count, colors, shape="circle", segments=1):
    d = ImageDraw.Draw(im)
    w, h = im.size
    x, y = w // 2, h // 2
    progress = (f + 1) / count
    radius = (min(w, h) / 2 - 3) * (.32 + .68 * progress)
    width = max(2, round((1 - progress) * min(w, h) / 30 + 2))
    ink, middle, light = colors
    if shape == "fan":
        box = (-radius * .7, y - radius, radius * 1.65, y + radius)
        d.arc(box, 305, 55, fill=ink, width=width + 4)
        d.arc(box, 310, 50, fill=middle, width=width + 1)
        d.arc(box, 324, 35, fill=light, width=1)
    elif shape == "square":
        a = round(radius * .75)
        d.polygon(diamond(x, y, a, a), outline=ink, width=width + 2)
        d.polygon(diamond(x, y, a - 1, a - 1), outline=middle, width=width)
        d.line([point(x - a + 2, y), point(x, y - a + 2), point(x + a - 2, y)], light)
    else:
        bounds = (x - radius, y - radius, x + radius, y + radius)
        for i in range(segments):
            a = 360 * i / segments + f * 7
            b = a + 360 / segments - (0 if segments == 1 else 16)
            d.arc(bounds, a, b, fill=ink, width=width + 3)
            d.arc(bounds, a + 1, b - 1, fill=middle, width=width + 1)
            d.arc(bounds, a + 3, b - 3, fill=light, width=1)
    for i in range(4 + segments):
        a = i * 2 * math.pi / (4 + segments) + .25 * f
        px, py = polar(x, y, min(min(w, h) / 2 - 2, radius * 1.22), a)
        d.rectangle((px, py, px + (f % 2), py + (f % 2)), fill=middle)


def blast(im, f, count, colors, kind="arcane", power=1):
    d = ImageDraw.Draw(im)
    w, h = im.size
    x, y = w // 2, h // 2
    a = (f + 1) / count
    dark, middle, light = colors
    radius = (min(w, h) / 2 - 3) * (.44 + a * .56) * power
    rng = random.Random(73 + sum(ord(c) for c in kind))
    for i in range(8 if power < 1 else 11):
        angle = i * math.tau / (8 if power < 1 else 11) + rng.uniform(-.2, .2)
        tip = radius * rng.uniform(.68, .98)
        r = radius * (.11 + a * .39)
        left = polar(x, y, r, angle - .14)
        right = polar(x, y, r, angle + .12)
        hit = polar(x, y, tip, angle)
        d.polygon([left, hit, right], fill=dark)
        center = polar(x, y, r + 1, angle)
        d.line([center, polar(x, y, tip * .84, angle)], middle, width=max(1, min(w, h) // (20 + f * 4)))
        if f < count - 1 and i % 2 == 0:
            d.line([center, polar(x, y, tip * .62, angle)], light)
        if i % 3 == 0:
            px, py = polar(x, y, radius + 1, angle)
            d.polygon(diamond(px, py, 1, 2), fill=middle)
    if f < count // 2:
        glyph(d, kind, x, y, max(2, round(radius * .33)), colors, f)


def shards(im, f, count, colors, seed=1, amount=9):
    d = ImageDraw.Draw(im)
    w, h = im.size
    rng = random.Random(seed)
    for i in range(amount):
        angle = i * math.tau / amount + rng.uniform(-.22, .22)
        dist = (min(w, h) * .33 * (f + 1) / count) * rng.uniform(.35, 1)
        x, y = polar(w / 2, h / 2, dist, angle)
        size = max(2, int(min(w, h) * .11 * (1 - .57 * f / count) * rng.uniform(.6, 1.6)))
        d.polygon([polar(x, y, size, angle - .32), polar(x, y, size * .82, angle + 2.1),
                   polar(x, y, size, angle + 3.7)], fill=colors[i % 3], outline=colors[0])
        if size >= 5:
            d.line([(x, y), polar(x, y, size, angle - .32)], fill=colors[2])


def vapor(im, f, count, colors, seed=1, ground=False):
    d = ImageDraw.Draw(im)
    w, h = im.size
    rng = random.Random(seed)
    for i in range(8):
        a = i * math.tau / 8
        r = (.12 + .21 * f / count) * w * rng.uniform(.65, 1.1)
        x = w / 2 + math.cos(a) * r
        y = h * (.64 - (.07 if ground else .27) * f / count) + math.sin(a) * h * .2
        k = max(2, round(min(w, h) * (.17 - f * .06 / count) * rng.uniform(.5, 1)))
        opacity = int(232 * (1 - f / (count + 1)))
        d.polygon([point(x - k, y + k / 2), point(x - k, y - k / 3), point(x - k * .44, y - k),
                   point(x + k * .4, y - k), point(x + k, y - k / 3), point(x + k, y + k / 3),
                   point(x + k * .4, y + k / 2)], fill=alpha(colors[i % 2], opacity))
        d.line([point(x - k * .45, y - k + 1), point(x + k * .22, y - k + 1)],
               alpha(colors[2], opacity // 2))


def shot(im, f, count, colors, kind):
    d = ImageDraw.Draw(im)
    w, h = im.size
    x, y = w // 2, h // 2
    if kind == "needle":
        d.polygon([(1, y - 1), (w - 7, 1), (w - 1, y), (w - 7, h - 2), (1, y + 1)],
                  fill=colors[1], outline=colors[0])
        d.line([(w // 3, y), (w - 4, y - 1)], colors[2], width=2)
    elif kind == "feather":
        d.polygon([(1, y), (w // 2, 1), (w - 2, y), (w // 2 + 1, h - 2), (3, y + 1)],
                  fill=colors[1], outline=colors[0])
        d.line([(3, y), (w - 4, y)], colors[2])
        d.line([(w // 2, 3 + f), (w // 2 + 4, y)], colors[2])
    elif kind == "ice_lance":
        d.polygon([(x, 1), (w - 2, y + 5), (x + 1, h - 2), (1, y + 4)], fill=colors[1], outline=colors[0])
        d.polygon([(x, 3), (x, h - 3), (2, y + 4)], fill=colors[2])
        d.point((x + 1 + f, y - 2), fill=colors[2])
    elif kind == "enemy_round":
        r = min(w, h) // 2 - 2
        d.ellipse((x - r, y - r, x + r, y + r), fill=colors[1], outline=colors[0], width=2)
        d.ellipse((x - r + 3, y - r + 2, x + r - 3, y + r - 3), fill=colors[2])
        if f:
            d.line([(x - r + 2, y + 1), (x + r - 2, y + 1)], colors[1])
    elif kind == "round":
        radius = min(w, h) // 2 - 3
        glyph(d, "fire", x, y, radius, colors, f)
        d.line([(x - radius + 1, y + 1), (3, y + 1 + f), (2, y + 4)], colors[1], width=2)
    else:
        glyph(d, kind, x, y, min(w, h) // 2 - 3, colors, f)


def seal(im, f, count, colors, variant=0):
    d = ImageDraw.Draw(im)
    w, h = im.size
    x, y = w // 2, h // 2
    r = min(w, h) // 2 - 4
    ink, middle, light = colors
    for offset, width in ((0, 2), (3, 1), (int(r * .23), 2), (int(r * .42), 1)):
        radius = r - offset
        d.ellipse((x - radius, y - radius, x + radius, y + radius), outline=middle if offset else ink, width=width)
    arms = 3 + variant % 6
    vertices = [polar(x, y, r * .56, math.tau * i / arms - math.pi / 2 + f * .09) for i in range(arms)]
    d.polygon(vertices, outline=light, width=1)
    if variant % 2 == 0 and arms > 4:
        for i in range(arms):
            d.line([vertices[i], vertices[(i + 2) % arms]], fill=middle)
    for i in range(arms * 2):
        a = i * math.pi / arms + f * .04
        p = polar(x, y, r - 4, a)
        q = polar(x, y, r - (11 if i % 2 == 0 else 8), a)
        d.line([p, q], fill=light if i % 2 == 0 else middle, width=2 if i % 2 == 0 else 1)
    star(d, x, y, max(3, r // 9), light, 4 if variant % 2 else 5)


def world_patch(im, f, count, colors, kind):
    d = ImageDraw.Draw(im)
    w, h = im.size
    x, y = w // 2, h // 2
    if kind in ("rock", "pillar"):
        if f >= count - 2:
            shards(im, f - count + 3, 3, colors, 81)
        else:
            half = w * (.28 if kind == "rock" else .21)
            top = h * (.26 if kind == "rock" else .09) + max(0, 1 - f) * 8
            d.polygon([point(x - half, h * .81), point(x - half, top + 7), point(x - half / 3, top),
                       point(x + half * .46, top + 3), point(x + half, top + 12),
                       point(x + half, h * .85), point(x, h * .93)], fill=colors[1], outline=colors[0], width=2)
            d.polygon([point(x - half + 2, h * .77), point(x - half + 2, top + 8),
                       point(x - half / 3, top + 2), point(x + 2, top + 9), point(x + 1, h * .9)], fill=colors[2])
            d.line([point(x + 2, top + 9), point(x + half - 3, top + 13)], colors[2], width=2)
    elif kind == "crack":
        rng = random.Random(105)
        for i in range(8):
            a = i * math.tau / 8
            coords = [(x, y)]
            for step in range(1, 4):
                rx, ry = math.cos(a) * w / 7 * step, math.sin(a) * h / 7 * step
                coords.append(point(x + rx + rng.randint(-3, 3), y + ry + rng.randint(-2, 2)))
            d.line(coords, colors[0], width=f + 1)
            d.line([(u, v + 1) for u, v in coords], colors[1])
    elif kind in ("thorns", "embers", "frost"):
        rng = random.Random(129)
        for i in range(25):
            px, py = rng.randrange(5, w - 5), rng.randrange(5, h - 5)
            if kind == "thorns":
                d.line([(px - 4, py + 2), (px, py), (px - 2, py - 4), (px + 3, py - 1),
                        (px + 5, py - 3)], alpha(colors[1], 210), width=2)
                d.point((px - 1, py - 4), fill=colors[2])
            elif kind == "embers":
                if i % 3 == f % 3:
                    d.polygon(diamond(px, py, 2, 3), fill=colors[1])
                    d.point((px, py - 1), fill=colors[2])
            else:
                for angle in (0, math.pi / 3, math.pi * 2 / 3):
                    d.line([polar(px, py, 4, angle), polar(px, py, 4, angle + math.pi)], alpha(colors[1], 140))
                if (i + f) % 3 == 0:
                    d.point((px, py), fill=colors[2])
    else:
        vapor(im, f, count, colors, 322, True)


def loot(im, f, count, colors, kind):
    d = ImageDraw.Draw(im)
    w, h = im.size
    x, y = w // 2, h // 2 + (1 if f % 2 else 0)
    r = min(w, h) // 2 - 3
    ink, middle, light = colors
    if kind == "coin":
        a = max(1, r - min(f, count - f) * 2)
        d.ellipse((x - a, y - r, x + a, y + r), fill=middle, outline=ink)
        d.arc((x - a + 1, y - r + 1, x + a - 1, y + r - 1), 170, 300, fill=light)
        if a > 2:
            d.polygon(diamond(x, y, a - 2, r - 2), outline=ink)
    elif kind == "bundle":
        d.polygon([point(x - r, y + r * .6), point(x - r * .8, y - r * .1), point(x - r * .32, y - r * .65),
                   point(x - r * .55, y - r), point(x + r * .45, y - r), point(x + r * .22, y - r * .58),
                   point(x + r * .85, y - r * .12), point(x + r, y + r * .65),
                   point(x + r * .56, y + r), point(x - r * .54, y + r)], fill=middle, outline=ink)
        d.polygon([point(x - r * .32, y - r * .65), point(x - r * .49, y + r * .29),
                   point(x - r * .13, y + r * .74), point(x - r * .68, y + r * .62)], fill=light)
        d.line([point(x - r * .38, y - r * .62), point(x + r * .36, y - r * .62)], ink, width=2)
        d.line([point(x + r * .32, y - r * .6), point(x + r * .38, y - r * .19)], light)
        if w >= 40:
            d.line([point(x - r * .5, y + r * .43), point(x - r * .2, y + r * .51),
                    point(x + r * .11, y + r * .44)], ink, width=2)
            d.line([point(x + r * .43, y - r * .22), point(x + r * .6, y + r * .22),
                    point(x + r * .51, y + r * .66), point(x + r * .16, y + r * .84)], ink, width=2)
            for dx in (-5, -2, 1, 4):
                d.line([(x + dx, y + r // 2), (x + dx + 1, y + r // 2 + 2)], light)
    elif kind == "heart":
        d.polygon([(x, y + r), (x - r, y), (x - r, y - 3), (x - 3, y - r),
                   (x, y - 2), (x + 3, y - r), (x + r, y - 3), (x + r, y)],
                  fill=middle, outline=ink)
        d.point((x - 2, y - 2), fill=light)
    elif kind == "exit":
        d.polygon(diamond(x, y, r, r * .67), fill=alpha(ink, 104), outline=middle, width=2)
        star(d, x, y, r * .44, light)
    else:
        d.polygon(diamond(x, y, r, r), fill=ink, outline=middle, width=2)
        d.polygon([point(x - r + 3, y), point(x, y - r + 3), point(x + 1, y),
                   point(x, y + r - 2)], fill=light)
        d.polygon([point(x + 1, y), point(x + r - 3, y), point(x, y + r - 2)], fill=middle)


def shaft(im, f, count, colors, ritual=True):
    d = ImageDraw.Draw(im)
    w, h = im.size
    x = w // 2
    r = max(2, round(w * (.14 + .21 * (f + 1) / count)))
    dark, main, light = colors
    d.polygon([(x - r, h - 7), (x - r * .66, h * .31), (x - r * .36, 5),
               (x + r * .4, 5), (x + r * .68, h * .31), (x + r, h - 7),
               (x + r * .6, h - 2), (x - r * .6, h - 2)], fill=alpha(main, 100 if ritual else 125))
    d.polygon([(x - r * .32, h - 8), (x - r * .18, h * .2), (x, 0),
               (x + r * .24, h * .28), (x + r * .37, h - 7)], fill=alpha(light, 202))
    for i in range(4):
        a = ((i * 13 + f * 7) % 39) / 39
        dx = x + (-1) ** i * r * (.4 + a * .72)
        y = h - 5 - a * h
        d.line([point(dx, y), point(dx, y - max(2, h / 36))], alpha(main, 155), width=max(1, w // 40))


def ui_frame(im, border, fill=None, inset=5):
    d = ImageDraw.Draw(im)
    w, h = im.size
    d.polygon([(inset, 1), (w-inset-1, 1), (w-2, inset), (w-2, h-inset-1),
               (w-inset-1, h-2), (inset, h-2), (1, h-inset-1), (1, inset)],
              fill=fill, outline=border, width=2)
    d.line([(inset+3, 4), (w-inset-4, 4)], fill=border)


def tag(im, i, count, colors):
    d = ImageDraw.Draw(im)
    w, h = im.size
    reach = max(12, w * (.52 + .48 * (i + 1) / count) / 2 - 2)
    left, right = w / 2 - reach, w / 2 + reach
    d.polygon([(left+7,3),(right-7,3),(right,h//2),(right-7,h-4),(left+7,h-4),(left,h//2)],
              fill=colors[0], outline=colors[1], width=2)
    d.line([(left+13,7), (right-13,7)], fill=colors[2])
    d.line([(left+13,h-8), (right-13,h-8)], fill=colors[1])
    for x in (left + 10, right - 10):
        d.polygon(diamond(x,h//2,3,4),fill=colors[2])


DIGITS = {
    "0": "01110100011001110101110011000101110",
    "1": "00100011000010000100001000010001110",
    "2": "01110100010000100110010001000011111",
    "3": "11110000010000101110000010000111110",
    "4": "00010001100101010010111110001000010",
    "5": "11111100001000011110000010000111110",
    "6": "00110010001000011110100011000101110",
    "7": "11111000010001000100010000100001000",
    "8": "01110100011000101110100011000101110",
    "9": "01110100011000101111000010001001100",
    "+": "00000001000010011111001000010000000",
    "!": "00100001000010000100001000000000100",
    "▲": "00000001000111011111000000000000000",
    "▼": "00000111110111000100000000000000000",
}


def digits(im, index, symbols, color):
    d = ImageDraw.Draw(im)
    w, h = im.size
    scale = min((w-2)//5, (h-2)//7)
    left, top = (w-5*scale)//2, (h-7*scale)//2
    for i, pixel in enumerate(DIGITS[symbols[index]]):
        if pixel == "1":
            x, y = left + i%5*scale, top + i//5*scale
            d.rectangle((x,y,x+scale-1,y+scale-1), fill=color)


def export(manifest, group, name, size, count, painter, fps=12, loop=False):
    frames = []
    folder = ROOT / group
    folder.mkdir(exist_ok=True)
    w, h = size
    cols = min(count, max(1, 2048 // w))
    rows = math.ceil(count / cols)
    sheet = Image.new("RGBA", (w * cols, h * rows))
    for i in range(count):
        img = Image.new("RGBA", size)
        painter(img, i, count)
        if not img.getbbox():
            raise ValueError(f"empty sprite {name} frame {i}")
        x, y = i % cols * w, i // cols * h
        sheet.alpha_composite(img, (x, y))
        frames.append([x, y, w, h])
    name_on_disk = f"{name}_{w}x{h}_f{count}.png"
    sheet.save(folder / name_on_disk)
    manifest[group][name] = {"path": f"{group}/{name_on_disk}", "size": [w, h], "frames": frames, "fps": fps, "loop": loop}


def main():
    m = {k: {} for k in ("player", "special", "ritual", "enemy", "loot", "wand", "status")}
    m["notes"] = {"colorSources": ["docs/DECISIONS.md §2.2", "docs/effects.md §0.1"],
                  "origin": "top-left", "filter": "point", "mipmaps": False}
    def out(group, name, size, count, painter, *args):
        export(m, group, name, size, count, painter, *args)

    for kind, pal in ELEMENTS.items():
        out("player", f"hit_{kind}", (32, 32), 3, lambda im, i, n: blast(im, i, n, pal, kind))
        out("player", f"hit_large_{kind}", (64, 64), 4, lambda im, i, n: blast(im, i, n, pal, kind))
        out("player", f"weakness_{kind}", (64, 64), 4, lambda im, i, n: blast(im, i, n, pal, kind, .84))
        out("player", f"explosion_{kind}", (256, 256), 5, lambda im, i, n: ring(im, i, n, pal, segments=2 if kind == "lightning" else 1))
        out("player", f"resist_{kind}", (32, 16), 3, lambda im, i, n: ring(im, i, n, pal, "fan"))
        out("player", f"crystal_{kind}", (24, 24), 2, lambda im, i, n: loot(im, i, n, pal, "gem"), 5, True)
        out("player", f"trace_{kind}", (64, 32), 2, lambda im, i, n: vapor(im, i, n, pal, 29, True))
        for key, size, count, shape, segments in (
            ("release", (64, 64), 4, "circle", 1), ("muzzle_storm", (96, 64), 4, "fan", 1),
            ("fan", (192, 128), 4, "fan", 1), ("wing", (96, 64), 4, "fan", 1),
            ("octagram", (128, 128), 4, "square", 1), ("bloom", (192, 192), 6, "circle", 6)
        ):
            if key in ("octagram", "bloom"):
                out("special", f"{key}_{kind}", size, count, lambda im, i, n: seal(im, i, n, pal, i + (5 if key == "octagram" else 11)))
            else:
                out("special", f"{key}_{kind}", size, count, lambda im, i, n: ring(im, i, n, pal, shape, segments))
        for key, size, count in (("spear_shaft", (64, 32), 3), ("spear_tip", (64, 32), 3),
                                  ("thunderlance_shaft", (64, 48), 4), ("thunderlance_tip", (64, 48), 4)):
            if key.endswith("tip"):
                out("special", f"{key}_{kind}", size, count, lambda im, i, n: shot(im, i, n, pal, "needle"))
            else:
                out("special", f"{key}_{kind}", size, count, lambda im, i, n: bolts(ImageDraw.Draw(im), (0, im.height // 2),
                    (im.width - 1, im.height // 2), pal, i, 5, True))
        out("special", f"petal_{kind}", (32, 48), 3, lambda im, i, n: shards(im, i, n, pal, 7, 3))
        out("ritual", f"seal_{kind}", (240, 240), 1, lambda im, i, n: seal(im, i, n, pal, list(ELEMENTS).index(kind)))
        out("ritual", f"outer_glyph_{kind}", (480, 480), 4, lambda im, i, n: ring(im, i, n, pal, segments=8))
        out("ritual", f"charge_{kind}", (96, 96), 6, lambda im, i, n: seal(im, i, n, pal, i + list(ELEMENTS).index(kind) * 3))
        out("ritual", f"success_column_{kind}", (192, 540), 6, lambda im, i, n: shaft(im, i, n, pal))
        out("ritual", f"max_column_{kind}", (192, 540), 8, lambda im, i, n: shaft(im, i, n, pal), 12, True)
        out("ritual", f"success_mote_{kind}", (16, 16), 3, lambda im, i, n: shot(im, i, n, pal, kind))
        out("ritual", f"break_pieces_{kind}", (64, 64), 18, lambda im, i, n: shards(im, i % 9, 9, pal, i + 72, 2))
        out("ritual", f"extinguish_{kind}", (96, 96), 5, lambda im, i, n: vapor(im, i, n, SMOKE, list(ELEMENTS).index(kind) * 70))
        out("ritual", f"ward_ribbon_{kind}", (128, 128), 6, lambda im, i, n: ring(im, i, n, pal, "fan", 3))
        out("ritual", f"stage_hold_{kind}", (128, 96), 4, lambda im, i, n: ring(im, i, n, pal, "square", 4))
        out("wand", f"trail_{kind}", (32, 16), 4, lambda im, i, n: vapor(im, i, n, pal, 7, True))
        out("wand", f"muzzle_{kind}", (32, 32), 3, lambda im, i, n: blast(im, i, n, pal, kind, .76))
        out("wand", f"footprint_{kind}", (32, 16), 4, lambda im, i, n: world_patch(im, i, n, pal, "frost" if kind == "ice" else "embers"))

    out("player", "mana_bolt", (16, 16), 2, lambda im, i, n: shot(im, i, n, ELEMENTS["arcane"], "arcane"), 10, True)
    out("player", "fireball", (32, 24), 4, lambda im, i, n: shot(im, i, n, ELEMENTS["fire"], "round"), 14, True)
    out("player", "chain_lightning", (64, 16), 2, lambda im, i, n: bolts(ImageDraw.Draw(im), (0, 8), (63, 8), ELEMENTS["lightning"], i, 3, True))
    out("player", "piercing", (24, 12), 2, lambda im, i, n: shot(im, i, n, ELEMENTS["arcane"], "needle"))
    out("player", "ice_lance", (12, 28), 2, lambda im, i, n: shot(im, i, n, ELEMENTS["ice"], "ice_lance"))
    out("player", "ice_muzzle", (32, 32), 3, lambda im, i, n: blast(im, i, n, ELEMENTS["ice"], "ice", .8))
    out("player", "ice_tail", (8, 24), 2, lambda im, i, n: shot(im, i, n, ELEMENTS["ice"], "ice_lance"))
    out("player", "shadow_orb", (40, 40), 4, lambda im, i, n: shot(im, i, n, ELEMENTS["dark"], "dark"), 8, True)
    out("player", "shadow_orb_cast", (48, 48), 4, lambda im, i, n: ring(im, i, n, ELEMENTS["dark"], segments=3))
    out("player", "shadow_orb_ripple", (32, 32), 3, lambda im, i, n: ring(im, i, n, ELEMENTS["dark"]))
    out("player", "shadow_orb_fade", (40, 40), 4, lambda im, i, n: vapor(im, i, n, ELEMENTS["dark"]))
    out("player", "shadow_small", (16, 16), 2, lambda im, i, n: shot(im, i, n, ELEMENTS["dark"], "dark"))
    out("player", "critical_muzzle", (32, 32), 3, lambda im, i, n: ring(im, i, n, NEUTRAL, segments=3))
    out("player", "critical_hit", (32, 32), 3, lambda im, i, n: blast(im, i, n, NEUTRAL, "arcane", .9))
    out("player", "critical_chevron", (6, 8), 1, lambda im, i, n: ImageDraw.Draw(im).polygon(
        [(0,0),(2,0),(5,4),(2,7),(0,7),(2,4)], fill="white", outline="#000000"))
    out("player", "graze_strike", (32, 128), 2, lambda im, i, n: bolts(ImageDraw.Draw(im), (18, 0), (17, 125), ELEMENTS["lightning"], i, 4, True))
    out("player", "heat_lock_steam", (32, 32), 4, lambda im, i, n: vapor(im, i, n, SMOKE))
    out("player", "heat_lock_x", (24, 24), 2, lambda im, i, n: ImageDraw.Draw(im).line(
        [(5,5),(18,18),(12,12),(18,5),(5,18)], fill=SMOKE[1+i], width=3))
    out("player", "overflow_warning", (16, 16), 1, lambda im, i, n: ImageDraw.Draw(im).polygon(
        [(8,1),(15,14),(1,14)], outline=ELEMENTS["fire"][2], width=2))
    out("player", "overflow_steam_arc", (320, 320), 5, lambda im, i, n: ring(im, i, n, ELEMENTS["fire"], segments=6))

    for i in range(15):
        out("special", f"cast_seal_{i+1:02d}", (96, 96), 4, lambda im, f, n: seal(im, f, n, NEUTRAL, i))
    out("special", "dodge_dust", (32, 32), 4, lambda im, i, n: vapor(im, i, n, SMOKE, 17, True))
    out("special", "purge_steam", (32, 32), 4, lambda im, i, n: vapor(im, i, n, SMOKE, 27))
    out("special", "break_armor", (48, 48), 4, lambda im, i, n: shards(im, i, n, NEUTRAL, 54))
    out("special", "just_star", (48, 48), 2, lambda im, i, n: star(ImageDraw.Draw(im), 24, 24, 12 + 7*i, NEUTRAL[2], 4))
    out("special", "kiln_border", (320, 320), 4, lambda im, i, n: ring(im, 2+i, 6, ELEMENTS["fire"], segments=8))
    out("special", "kiln_ember", (8, 8), 2, lambda im, i, n: shot(im, i, n, ELEMENTS["fire"], "fire"))
    out("special", "lightning_dash", (256, 64), 3, lambda im, i, n: bolts(ImageDraw.Draw(im),
        (4, 41-i*8), (250, 25+i*6), ELEMENTS["lightning"], i, 6, True))
    out("special", "arcane_copy", (96, 96), 4, lambda im, i, n: seal(im, i, n, ELEMENTS["arcane"], 12))
    out("special", "scattered_pages", (16, 16), 3, lambda im, i, n: shards(im, i, n, ("#54413C", "#D6BE90", "#F2E7C9"), 35, 2))
    for axis in ("horizontal", "vertical"):
        def screen(im, f, count):
            d = ImageDraw.Draw(im)
            if f >= 6:
                shards(im, f-6, 4, ELEMENTS["ice"], 6, 15)
                return
            for j in range(6 if axis == "horizontal" else 4):
                x = 25 + 28 * j if axis == "horizontal" else 75 + j * 12
                y = 71 - (j % 3) * 6 if axis == "horizontal" else 82 - j * 16
                h = (24 + 4*(j % 3)) * min(1, (f+1)/3)
                d.polygon([point(x-11,y),point(x-10,y-h*.7),point(x+2,y-h),point(x+11,y-h*.7),
                           point(x+12,y-4),point(x+3,y+3)], fill=ELEMENTS["ice"][1], outline=ELEMENTS["ice"][0], width=2)
                d.polygon([point(x-9,y-3),point(x-8,y-h*.68),point(x+2,y-h+3),point(x+1,y-1)], fill=ELEMENTS["ice"][2])
                if f >= 4:
                    d.line([point(x+2,y-h+3),point(x-3,y-h*.4),point(x+5,y-h*.3),point(x+3,y-1)], ELEMENTS["ice"][0])
        out("special", f"ice_screen_{axis}", (192, 96), 10, screen)
    out("special", "ice_screen_ripple", (32, 32), 3, lambda im, i, n: ring(im, i, n, ELEMENTS["ice"], "circle", 2))
    out("special", "ice_screen_shard", (16, 16), 3, lambda im, i, n: shards(im, i, n, ELEMENTS["ice"], 24, 3))
    out("special", "shadow_pool", (128, 128), 4, lambda im, i, n: ring(im, i, n, ELEMENTS["dark"], segments=4))
    def shadow_hand(im, i, n):
        d = ImageDraw.Draw(im)
        x, y = im.width // 2, im.height // 2 + 4 - i
        r = im.width // 4
        d.polygon([(x-r,y+6),(x-r-3,y),(x-r-3,y-6),(x-r,y-7),(x-4,y-2),(x-5,y-r),
                   (x-2,y-r-3),(x+1,y-r),(x+3,y-3),(x+6,y-r+1),(x+9,y-r),(x+10,y-4),
                   (x+13,y-7),(x+15,y-4),(x+r,y+7),(x+3,y+10),(x-r+1,y+10)], fill="#4A2F68", outline="#241932")
        d.line([(x-r+2,y+4),(x-1,y+6),(x+r-1,y+2)], "#8A5CFF", width=2)
    out("special", "shadow_hand", (32, 32), 4, shadow_hand)
    out("special", "shadow_fist", (64, 64), 3, shadow_hand)
    out("special", "shadow_arm", (32, 16), 2, lambda im, i, n: bolts(ImageDraw.Draw(im), (0, 8), (31, 8), ELEMENTS["dark"], i, 4, False))

    out("ritual", "seal_base", (240, 240), 1, lambda im, i, n: seal(im, i, n, NEUTRAL, 8))
    out("ritual", "gold_burst", (256, 256), 8, lambda im, i, n: blast(im, i, n, GOLD))
    out("ritual", "gold_aureole", (256, 256), 8, lambda im, i, n: ring(im, i, n, GOLD, segments=3))
    out("ritual", "failure_dust", (48, 48), 4, lambda im, i, n: vapor(im, i, n, SMOKE, 23, True))
    out("ritual", "failure_smoke", (64, 64), 5, lambda im, i, n: vapor(im, i, n, SMOKE))
    out("ritual", "artisan_ember", (32, 32), 6, lambda im, i, n: shot(im, i, n, ELEMENTS["fire"], "fire"))
    out("ritual", "ward_burn", (64, 96), 6, lambda im, i, n: blast(im, i, n, ELEMENTS["fire"], "fire", .9))
    out("ritual", "slot_open", (64, 64), 6, lambda im, i, n: ring(im, i, n, GOLD, "square"))
    out("ritual", "mutation_vine", (192, 256), 24, lambda im, i, n: world_patch(im, i % 6, 6, ("#385122", "#B8F04A", "#DEFFA0"), "thorns"))
    out("ritual", "trace_fragment", (16, 16), 3, lambda im, i, n: shards(im, i, n, SMOKE, 42, 3))
    out("ritual", "screen_crack", (1920, 1080), 3, lambda im, i, n: world_patch(im, i, n, SMOKE, "crack"))
    out("ritual", "max_sky_band", (1920, 240), 4, lambda im, i, n: bolts(ImageDraw.Draw(im),
        (1, 180+i*9), (1919, 150-i*9), NEUTRAL, i, 6, True))
    for key, size in (("offering", (56,56)), ("ward", (48,80)), ("artisan", (24,24))):
        out("ritual", f"{key}_slot", size, 2, lambda im, i, n: ui_frame(im, GOLD[1] if i else SMOKE[1],
            "#373240" if i else "#232432"))
    out("ritual", "ward_attached", (48,80), 1, lambda im, i, n: tag(im, i, n, ("#755537","#E7CE8C","#FFF0B9")))
    out("ritual", "mutation_card", (480,272), 1, lambda im, i, n: ui_frame(im, "#9AB650", "#252C2B", 11))
    out("ritual", "result_card", (560,320), 2, lambda im, i, n: ui_frame(im, GOLD[1] if i else SMOKE[2], "#262432", 12))
    for key, size, count in (("title_ribbon",(720,96),4),("plaza_notice",(960,80),3),("news_line",(960,40),1)):
        for tier, palette in (("normal",("#302943","#8B7EAA","#D8CBF1")), ("gold",GOLD)):
            out("ritual",f"{key}_{tier}",size,count,lambda im,i,n: tag(im,i,n,palette))
    for variant, color in (("tint", "#EEF1F7"),("gold","#FFD24A"),("muted","#797786")):
        out("ritual", f"stage_digits_{variant}", (48,64), 11, lambda im, i, n: digits(im, i, "+0123456789", color))
    out("status", "combo_digits", (24,32), 10, lambda im,i,n: digits(im,i,"0123456789","#EEF1F7"))
    out("status", "damage_glyphs", (16,24), 13, lambda im,i,n: digits(im,i,"0123456789!▲▼","#EEF1F7"))
    out("status", "critical_ring_chevrons", (8,8), 2, lambda im,i,n: ImageDraw.Draw(im).polygon(
        [(1,1+i),(3,1+i),(7,4),(3,7-i),(1,7-i),(4,4)], fill="white", outline="#000000"))
    out("status", "health_full", (32,32), 1, lambda im,i,n: loot(im,i,n,("#442F3B","#D58C81","#FFE4BF"),"heart"))
    out("status", "health_empty", (32,32), 1, lambda im,i,n: loot(im,i,n,("#282333","#4E485B","#6F6475"),"heart"))
    out("status", "health_break", (32,32), 4, lambda im,i,n: shards(im,i,n,("#442F3B","#D58C81","#FFE4BF"),67,6))
    out("status", "slow_ankle_ring_double", (32,8), 2, lambda im,i,n: ImageDraw.Draw(im).arc(
        (1,1,30,6+i), 0, 360, fill=ELEMENTS["ice"][1+i], width=2))
    out("special", "technique_name_ribbon", (192,32), 2, lambda im,i,n: tag(im,i,n,NEUTRAL))
    out("special", "technique_name_ribbon_large", (320,48), 2, lambda im,i,n: tag(im,i,n,NEUTRAL))

    for key, size, count, kind, pal in [
        ("round_small", (16,16), 2, "enemy_round", DANGER_RED), ("round_large", (24,24), 2, "enemy_round", DANGER),
        ("pine_needle", (24,8), 1, "needle", DANGER_RED), ("feather", (24,12), 2, "feather", DANGER_RED),
        ("ice_shard", (16,16), 1, "needle", DANGER), ("shadow_needle", (24,12), 2, "needle", DANGER),
        ("vine_seed", (16,16), 2, "enemy_round", DANGER_RED)
    ]:
        out("enemy", key, size, count, lambda im, i, n: shot(im, i, n, pal, kind))
    out("enemy", "seed_shadow", (16,8), 1, lambda im, i, n: ImageDraw.Draw(im).ellipse((2,2,14,6), fill=alpha("#201D2A",160)))
    for key, size, count, kind, pal in [
        ("thorn_ground",(96,96),2,"thorns",DANGER), ("ember_lane",(64,64),3,"embers",DANGER_RED),
        ("spore_cloud",(128,128),4,"vapor",DANGER), ("frost_field",(160,160),2,"frost",DANGER),
        ("ice_pillar",(64,96),6,"pillar",ELEMENTS["ice"]), ("rock",(64,64),5,"rock",("#302E3B","#777281","#ABA2A4")),
        ("soil_crack",(64,32),2,"crack",("#251F2B","#775A45","#AB8162")), ("stain",(64,64),1,"crack",SMOKE)
    ]:
        out("enemy", key, size, count, lambda im, i, n: world_patch(im, i, n, pal, kind))
    out("enemy", "lightning_strike", (64,128), 3, lambda im, i, n: bolts(ImageDraw.Draw(im), (35,0),(32,127), DANGER_RED, i, 5, True))
    out("enemy", "soil_particle", (8,8), 1, lambda im, i, n: loot(im, i, n, ("#3E2930","#845F47","#C4A880"),"gem"))
    out("enemy", "explosion", (128,128), 5, lambda im, i, n: blast(im, i, n, DANGER_RED, "fire"))
    out("enemy", "death_smoke", (48,48), 4, lambda im, i, n: vapor(im, i, n, SMOKE, 3))
    out("enemy", "elite_column", (32,128), 3, lambda im, i, n: shaft(im, i, n, GOLD, False))
    out("enemy", "toad_buff", (64,32), 3, lambda im, i, n: ring(im, i, n, ("#504759","#A6989B","#E8D8B2"), segments=4))
    out("enemy", "stun_star", (32,16), 3, lambda im, i, n: star(ImageDraw.Draw(im), 8+i*7, 6+i%2*4, 5, NEUTRAL[2]))
    out("enemy", "rage_smoke", (32,48), 4, lambda im, i, n: vapor(im, i, n, ("#17171F","#3F3F4E","#92909E"), 56))
    out("enemy", "mirror_shards", (16,16), 2, lambda im, i, n: shards(im, i, n, NEUTRAL, 16, 3))
    out("enemy", "impact_circle", (128,128), 3, lambda im, i, n: ring(im, i, n, DANGER_RED))
    out("enemy", "impact_fan", (128,128), 3, lambda im, i, n: ring(im, i, n, DANGER, "fan"))
    out("enemy", "impact_lane", (128,64), 3, lambda im, i, n: blast(im, i, n, DANGER_RED, "lightning", 1))
    out("enemy", "rage_slot", (16,16), 2, lambda im,i,n: ui_frame(im, SMOKE[1] if not i else DANGER_RED[1],
        "#292638" if not i else "#862E4E", 4))
    out("enemy", "final_rage_ribbon", (480,64), 3, lambda im,i,n: tag(im,i,n,DANGER_RED))
    out("enemy", "mirror_silver_particle", (8,8), 2, lambda im,i,n: star(ImageDraw.Draw(im), 4,4,2+i, "#C8D0E0"))

    for key, size, count, kind, pal in [
        ("coin",(16,16),4,"coin",GOLD), ("block_common",(24,24),2,"gem",("#25313B","#899EAA","#E2E6DC")),
        ("block_rare",(24,24),2,"gem",("#21334C","#719BDE","#D9F2F9")),
        ("block_heroic",(24,24),2,"gem",("#442A59","#A582CA","#EFE1FF")),
        ("material_pack",(24,24),2,"bundle",("#422D38","#956B4C","#CEA76F")),
        ("crow_pack",(64,64),4,"bundle",("#3C2A35","#A87B50","#DEC18D")),
        ("season_drop",(32,32),4,"gem",GOLD),
        ("health_shard",(16,16),2,"heart",("#4C313C","#CA986F","#FFE0B5")),
        ("exit_marker",(64,64),2,"exit",NEUTRAL)
    ]:
        out("loot", key, size, count, lambda im, i, n: loot(im, i, n, pal, kind), 7, True)
    out("loot", "drop_column", (16,64), 3, lambda im, i, n: shaft(im, i, n, GOLD, False))
    out("loot", "campfire", (64,64), 4, lambda im, i, n: shot(im, i, n, ELEMENTS["fire"], "fire"), 8, True)
    for key, count in (("crystal_empty",1),("crystal_debt",1),("crystal_cracked",1),("crystal_break",4),("crystal_particle",3)):
        size = (16,16) if key.endswith("particle") else (32,32) if key.endswith("break") else (24,24)
        if count >= 3:
            out("status", key, size, count, lambda im, i, n: shards(im, i, n, ELEMENTS["ice"], 78, 5))
        else:
            def crystal(im, i, n):
                d = ImageDraw.Draw(im)
                c = ELEMENTS["dark"][1] if key == "crystal_debt" else NEUTRAL[1]
                if key == "crystal_cracked":
                    d.line([(12,3),(10,9),(15,13),(11,20)], ELEMENTS["ice"][2], width=2)
                else:
                    d.polygon(diamond(12,12,8,10), outline=c, width=2)
            out("status", key, size, count, crystal)
    out("status", "armor_bounce", (32,32), 3, lambda im, i, n: ring(im, i, n, NEUTRAL, "fan"))
    out("status", "frost_mark", (16,8), 1, lambda im, i, n: star(ImageDraw.Draw(im), 8,4,3,ELEMENTS["ice"][2],6))
    for size in (64,96):
        out("status", f"frozen_shell_{size}", (size,size), 1, lambda im, i, n: world_patch(im,i,3,ELEMENTS["ice"],"pillar"))
    out("status", "frozen_shatter", (96,96), 4, lambda im,i,n: shards(im,i,n,ELEMENTS["ice"],61,12))
    out("status", "static_outline", (64,64), 2, lambda im,i,n: ring(im,i,n,ELEMENTS["lightning"],segments=5))
    out("status", "slow_ankle_ring", (32,8), 2, lambda im,i,n: ImageDraw.Draw(im).ellipse(
        (1,1,30,6+i),outline=ELEMENTS["ice"][1+i],width=1+i))
    out("status", "corrosion", (32,32), 2, lambda im,i,n: world_patch(im,i,n,ELEMENTS["dark"],"crack"))
    out("status", "frost_shield", (192,32), 3, lambda im,i,n: ImageDraw.Draw(im).rounded_rectangle(
        (2+i,7,189-i,24),radius=3,outline=ELEMENTS["ice"][1],width=2+i))
    out("status", "meteor_fall", (32,64), 3, lambda im,i,n: shaft(im,i,n,GOLD,False))
    out("status", "meteor_ground", (32,32), 4, lambda im,i,n: loot(im,i,n,GOLD,"gem"))
    out("status", "frost_particle", (4,4), 2, lambda im,i,n: ImageDraw.Draw(im).point((1+i,1),fill=ELEMENTS["ice"][2]))
    out("status", "corrosion_tail", (8,16), 2, lambda im,i,n: vapor(im,i,n,ELEMENTS["dark"],44))
    (ROOT / "manifest.json").write_text(json.dumps(m, ensure_ascii=False, indent=2) + "\n")
    make_preview(m)


def make_preview(m):
    selected = [("player", "mana_bolt"), ("player", "fireball"), ("player", "chain_lightning"),
                ("player", "ice_lance"), ("player", "shadow_orb"), ("player", "critical_hit"),
                ("enemy", "round_small"), ("enemy", "pine_needle"), ("enemy", "feather"),
                ("enemy", "ice_pillar"), ("enemy", "spore_cloud"), ("enemy", "explosion"),
                ("special", "ice_screen_horizontal"), ("special", "lightning_dash"),
                ("special", "shadow_hand"), ("special", "arcane_copy"), ("special", "bloom_fire"),
                ("ritual", "seal_dark"), ("ritual", "gold_burst"), ("loot", "coin"),
                ("loot", "block_heroic"), ("loot", "crow_pack"), ("loot", "exit_marker"), ("status", "frost_shield")]
    preview = Image.new("RGBA", (900, 540), "#171827")
    draw = ImageDraw.Draw(preview)
    draw.text((16, 12), "SPELLS, TELEGRAPHS AND DROPS", "#D9C7A9", font=ImageFont.load_default())
    for i, (category, name) in enumerate(selected):
        item = m[category][name]
        x, y, w, h = item["frames"][0 if len(item["frames"]) <= 2 else 1]
        sprite = Image.open(ROOT / item["path"]).crop((x,y,x+w,y+h))
        size = min(2, 95/max(w,h))
        sprite = sprite.resize((max(1,round(w*size)),max(1,round(h*size))), NEAREST)
        px, py = 150 * (i % 6), 121 * (i // 6) + 36
        draw.rectangle((px+4,py,px+145,py+111), fill="#202238")
        preview.alpha_composite(sprite, (px+75-sprite.width//2,py+49-sprite.height//2))
        draw.text((px+10,py+95),name.replace("_"," "),fill="#D9C7A9",font=ImageFont.load_default())
    preview.save(ROOT / "preview.png")


if __name__ == "__main__":
    main()
