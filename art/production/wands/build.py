"""Draw the five wand families from their stage tables and export aligned PNGs."""
from pathlib import Path
import json
import math

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).parent
NEAREST = Image.Resampling.NEAREST
FAMILIES = ("dalgu", "chorong", "galpi", "seonge", "geumeum")
OUTLINES = dict(zip(FAMILIES, ("#2A1A20", "#1C1830", "#1A2030", "#1E2238", "#1B0D1B")))


class PixelWand:
    def __init__(self, family, size):
        self.image = Image.new("RGBA", (32 * size, 64 * size))
        self.draw = ImageDraw.Draw(self.image)
        self.scale = size
        self.ink = OUTLINES[family]

    def point(self, point):
        return tuple(round(n * self.scale) for n in point)

    def poly(self, points, color, edge=True):
        self.draw.polygon(
            [self.point(p) for p in points], fill=color,
            outline=self.ink if edge else None, width=max(1, self.scale // 2)
        )

    def rect(self, x, y, w, h, color, edge=False):
        a, b = self.point((x, y)), self.point((x + w, y + h))
        self.draw.rectangle((a, (b[0] - 1, b[1] - 1)), fill=color,
                            outline=self.ink if edge else None)

    def line(self, points, color, width=1):
        self.draw.line([self.point(p) for p in points], fill=color, width=max(1, round(width * self.scale)))

    def gem(self, x, y, w, h, color, light):
        self.poly([(x, y - h), (x + w, y - 1), (x + w - 1, y + 2),
                   (x, y + h), (x - w, y + 1), (x - w + 1, y - 2)], color)
        self.poly([(x, y - h + 1), (x, y + 1), (x - w + 1, y), (x - w + 2, y - 2)], light, False)

    def oval(self, x, y, w, h, color, edge=True):
        self.draw.ellipse((self.point((x - w, y - h)), self.point((x + w, y + h))), fill=color,
                          outline=self.ink if edge else None, width=max(1, self.scale // 2))

    def arc(self, x, y, w, h, a, b, color, width=1):
        self.draw.arc((self.point((x - w, y - h)), self.point((x + w, y + h))),
                      a, b, fill=color, width=max(1, round(width * self.scale)))


def forge(p, n):
    iron, copper, old, amber, hot, white = "#6B6672", "#E39A4E", "#B5692F", "#C46210", "#FF8A1F", "#FFF6D8"
    top = 24 - (0 if n < 3 else 2 if n < 6 else 4 if n < 9 else 6 if n < 13 else 8)
    shaft = copper if n >= 8 else iron
    p.rect(13 if n >= 6 else 14, top + 2, 7 if n >= 6 else 4, 54 - top, shaft, True)
    if n >= 6:
        p.line([(15, top + 10), (15, 44)], "#B5692F" if n >= 8 else "#3E3A44")
    if p.scale > 1 and n < 8:
        p.line([(15.3, top + 5), (15.3, 43)], "#A9A4AE", .4)
    p.rect(14, 46, 4, 10, "#5A3A2A", True)
    for y in (47, 50, 53):
        p.line([(14, y + 1), (17, y)], old)
    if n <= 3:
        reach = (2, 6, 4, 6)[n]
        p.poly([(14, top), (22, top), (23, top + 2), (23, top + reach + 1),
                (20, top + reach + 1), (20, top + 3), (14, top + 4)], hot if n == 3 else iron)
        p.rect(15, top + 1, 5, 1, white if n == 3 else "#A9A4AE")
        if n >= 1:
            p.rect(21, top + reach - 1, 2, 2, amber if n == 1 else hot)
        if n >= 2:
            p.poly([(16, top + 10), (20, top + 10), (21, top + 12),
                    (20, top + 15 + 3 * (n == 3)), (16, top + 15)], amber)
    elif n < 7:
        p.poly([(14, top), (20, top), (25, top + 4), (25, top + 9), (22, top + 13),
                (13, top + 13), (9, top + 8), (10, top + 4)], hot)
        p.poly([(14, top + 2), (19, top + 2), (21, top + 5), (15, top + 6), (12, top + 5)], "#FFE08A", False)
        p.rect(7 if n >= 5 else 11, top + 11, 22 if n >= 5 else 15, 3, copper, True)
        if n >= 5:
            p.rect(8, top + 14, 2, 4, amber)
        if n == 6:
            p.line([(18, top + 13), (21, top + 18), (21, top + 25)], hot, 3)
            p.rect(24, top + 14, 2, 6, amber)
    elif n < 10:
        p.poly([(6, top + 2), (10, top + 2), (12, top + 5), (21, top + 5),
                (24, top + 2), (27, top + 2), (25, top + 11), (21, top + 15),
                (12, top + 15), (8, top + 11)], copper)
        p.rect(11, top + 5, 13, 3, hot)
        p.rect(13, top + 5, 7, 1, white)
        p.line([(17, top + 10), (17, top + 22)], hot, 2)
        if n >= 8:
            for y in (36, 40, 44):
                p.rect(11, y, 11, 2, old, True)
        if n == 9:
            p.poly([(10, top + 4), (14, top), (22, top + 1), (26, top + 5),
                    (29, top + 6), (29, top + 15), (27, top + 15), (25, top + 8)], hot)
            p.line([(13, top + 4), (17, top + 2), (22, top + 3)], white)
    else:
        width = (21, 24, 29, 30)[n - 10]
        left = 16 - width // 2
        bottom = (38, 43, 46, 45)[n - 10]
        p.poly([(9, top + 4), (23, top + 4), (26, top + 9), (31 if n >= 12 else 28, top + 11),
                (27, top + 14), (left + width - 1, bottom - 3), (left + width - 4, bottom),
                (left + 2, bottom), (left, bottom - 4), (left + 2, top + 10)], amber if n == 13 else old)
        if n < 13:
            tilt = 4 if n == 11 else 0
            p.poly([(8, top + 6), (10, top + 3), (23, top + 3 - tilt),
                    (25, top + 6 - tilt), (23, top + 7), (9, top + 8)], copper)
            p.rect(14, top, 5, 4, copper, True)
            p.rect(12, top + 12, 9, 8, p.ink)
            p.rect(14, top + 14, 5, 4, hot)
            p.rect(15, top + 14, 2, 2, white)
            p.line([(left + 2, top + 12), (left + 2, bottom - 5),
                    (left + 5, bottom - 3)], copper, 1)
            if p.scale > 1:
                p.line([(left + 4, top + 9), (left + 4, bottom - 6)], "#FFD79A", .45)
            if n == 12:
                for x in (6, 15, 26):
                    p.line([(x, bottom - 4), (x, bottom + 3)], hot, 2)
        else:
            p.poly([(10, top + 5), (16, top + 2), (22, top + 4), (27, bottom - 4),
                    (5, bottom - 4)], hot, False)
            for x in (9, 15, 22):
                p.line([(x, top + 12), (x + 2, top + 16), (x + 1, bottom - 5)], white)


def lightning(p, n):
    brass, gold, pale, lemon, white = "#A8842A", "#C89A22", "#FFE9A0", "#FFF36B", "#FFFFFF"
    height = [38, 38, 38, 44, 44, 44, 44, 44, 48, 52, 58, 58, 58, 62][n]
    bottom = 57 + (5 if n >= 9 else 0)
    top = bottom - height
    hub = top + (3 if n < 3 else 10 if n < 8 else 12 if n < 10 else 16)
    shaft = brass if n < 4 else gold if n < 12 else pale
    p.rect(14, hub, 4, bottom - hub, shaft, True)
    if p.scale > 1:
        p.line([(15.25, hub + 4), (15.25, bottom - 2)], pale, .38)
    if n >= 9:
        p.poly([(13, bottom - 5), (18, bottom - 5), (16, bottom)], pale)
    for y in (hub + 10, 40, 49):
        p.rect(13, y, 6, 2, gold if n < 7 else pale, True)
    lamp_positions = []
    if n < 12:
        for x in ([22] if n < 6 else [7, 24] if n < 11 else [5, 26]):
            y = hub + (5 if n <= 1 else 7 if n < 11 else 12)
            p.line([(16, hub + 3), (17 if x > 16 else 13, hub), (x, hub), (x, y)], p.ink, 4)
            p.line([(16, hub + 2), (17 if x > 16 else 13, hub), (x, hub), (x, y)], shaft, 2)
            if x >= 16 or n >= 7:
                lamp_positions.append((x, y))
    else:
        for x in (4, 27):
            p.line([(16, hub + 8), (x + (3 if x < 16 else -3), hub + 4), (x, hub), (x, hub - 4)], pale, 2)
            lamp_positions.append((x, hub - 10))
        p.poly([(6, hub + 8), (9, hub + 5), (22, hub + 5), (26, hub + 9),
                (22, hub + 11), (10, hub + 10)], "#51496B")
    if n >= 9:
        p.rect(10, hub - 1, 12, 4, pale, True)
    if n >= 3:
        rods = [(16, top)]
        if n >= 5:
            rods = [(14, top), (19, top + 3)]
        if n >= 8:
            rods = [(11, top + 3), (16, top), (21, top + 3)]
        if n >= 10:
            rods = [(6, top + 5), (11, top + 4), (16, top), (21, top + 4), (26, top + 5)]
        if n == 13:
            rods = [(2 + 4 * i, top + abs(3 - i) * 2) for i in range(7)]
        for x, y in rods:
            p.line([(x, y), (x, hub + 1)], p.ink, 3)
            p.line([(x, y + 1), (x, hub + 1)], pale)
    if n >= 10:
        lamp_positions.append((16, hub - (12 if n == 13 else 8)))
    for x, y in lamp_positions:
        half = 2 if n == 0 else 3 if n < 4 else 4
        h = 4 if n == 0 else 6 if n < 4 else 9
        if n >= 11:
            p.oval(x, y + 3, 3 if n == 12 else 4, 4, lemon)
            p.rect(x - 1, y + 1, 2, 2, white)
        else:
            p.poly([(x - half - (n >= 2), y), (x - 2, y - (2 if n >= 2 else 0)),
                    (x + 2, y - (2 if n >= 2 else 0)), (x + half + (n >= 2), y),
                    (x + half, y + h), (x - half, y + h)], "#625437" if n == 0 else lemon)
            if n >= 2:
                p.rect(x - 1, y + 2, 2, max(2, h - 3), "#37354D" if 4 <= n < 7 else white)
                if p.scale > 1 and n >= 4:
                    p.line([(x - half + .6, y + 1), (x - half + .6, y + h - 1)], gold, .4)
                    p.line([(x + half - .6, y + 1), (x + half - .6, y + h - 1)], gold, .4)
            if 5 <= n <= 6:
                p.line([(x, y + h), (x + 1, y + h + 4)], shaft, 2)


def paper(p, n):
    oak, oak_light, page, darkpage, mint = "#8A5A3E", "#B88A5A", "#EDE3C8", "#BBA583", "#4FE3C1"
    shaft = oak if n < 9 else darkpage if n < 12 else oak_light
    p.poly([(13, 12), (18, 12), (18, 54), (17, 56), (15, 55), (13, 56)], shaft)
    p.line([(14, 14), (14, 44)], oak_light)
    if p.scale > 1:
        p.line([(16.5, 18), (16, 28), (16.5, 44), (16.5, 52)], "#5A3A2E", .42)
    if n < 7:
        p.gem(15, 11, 2, 4, darkpage, page)
    else:
        p.poly([(15, 4), (19, 5), (20, 8), (17, 15), (14, 19),
                (12, 17), (12, 11)], page)
        p.line([(14, 16), (17, 7)], mint if n >= 10 else oak_light)
    if 1 <= n <= 3:
        p.poly([(17, 14), (22, 14), (22, 18), (20, 19), (22, 21), (22, 25),
                (20, 26), (22, 28), (22, 30), (17, 30)], oak_light)
        if n >= 2:
            p.rect(19, 28, 9 + 2 * (n == 3), 2, mint)
        if n == 3:
            for y in (14, 19, 24):
                p.line([(23, y), (26, y), (26, y + 3)], "#D6FFF3", 2)
    if 4 <= n <= 7:
        p.poly([(18, 14), (27, 12), (30, 16), (30, 26), (28, 30), (18, 31)], page)
        if n >= 5:
            p.rect(11, 12, 12, 2, oak_light, True)
            p.rect(11, 30, 12, 2, oak_light, True)
            p.rect(11, 53, 10, 3, oak_light, True)
            p.line([(21, 19), (26, 18)], mint)
            p.line([(21, 23), (28, 22)], darkpage)
        if n >= 6:
            p.poly([(11, 13), (9, 17), (9, 25), (7, 29), (10, 28), (12, 29), (13, 16)], mint)
            p.poly([(18, 32), (25, 31), (28, 34), (28, 41), (18, 42)], darkpage)
    if n == 8:
        for y, slope in [(14, -5), (17, 0), (20, 6)]:
            p.poly([(16, 15), (27, y + slope), (30, y + slope + 4),
                    (26, y + slope + 5), (18, 19)], page)
    if n == 9:
        p.poly([(17, 13), (27, 13), (30, 20), (24, 22), (29, 27), (24, 30),
                (30, 37), (26, 41), (18, 40)], page)
        for y in (20, 27, 35):
            p.line([(20, y), (25, y + 1)], mint)
    if n in (10, 11):
        p.rect(11, 12, 19, 3, oak_light, True)
        p.poly([(18, 14), (30, 14), (30, 42), (25, 45), (18, 42)], page)
        for y in (19, 23, 29, 34, 38):
            p.line([(20, y), (27 - y % 3, y)], mint if y % 2 else darkpage)
        if n == 11:
            p.rect(17, 40, 14, 6, darkpage, True)
            p.rect(19, 40, 10, 2, page)
            p.gem(10, 14, 3, 5, page, mint)
    if n >= 12:
        if n == 13:
            p.poly([(16, 22), (10, 16), (2, 16), (1, 34), (7, 34), (15, 40)], page)
            p.poly([(6, 32), (11, 34), (10, 44), (7, 47)], mint)
            p.line([(4, 21), (8, 21), (13, 25)], mint)
            p.line([(4, 26), (8, 27), (13, 30)], darkpage)
        else:
            p.poly([(16, 19), (11, 14), (11, 40), (16, 46)], oak_light)
        p.poly([(16, 20), (22, 15), (30, 15), (30, 36), (23, 38), (16, 43)], page)
        for y in (21, 25, 30, 34):
            p.line([(18, y + 3), (23, y), (28, y - 1)], mint if y % 2 else darkpage)
        if p.scale > 1:
            p.line([(17, 40), (23, 36), (29, 34)], darkpage, .6)
            p.line([(19, 20), (23, 17), (29, 17)], "#FFF3D4", .58)


def frost(p, n):
    blue, shade, white, gold = "#9FDCFF", "#5E9AC7", "#F4FBFF", "#E5C37B"
    h = (40, 42, 44, 46, 46, 48, 50, 52, 54, 56, 58, 60, 62, 64)[n]
    bottom = (54, 56, 58, 58, 58, 58, 60, 60, 60, 60, 62, 62, 62, 63)[n]
    top = max(0, bottom - h + 1)
    y = top + (3 if n < 4 else 8 if n < 8 else 11 if n < 10 else 13)
    width = 3 if n < 6 else 4 if n < 12 else 5
    points = [(16 - width, y), (16 + width, y)]
    if n >= 7:
        points += [(16 + width + k % 2, k) for k in range(y + 4, bottom - 4, 4)]
    points += [(17, bottom - 2), (16, bottom), (14, bottom - 3)]
    if n >= 7:
        points += [(16 - width - k % 3, k) for k in range(bottom - 5, y + 3, -4)]
    p.poly(points, shade)
    p.poly([(16 - width + 1, y + 2), (16, y + 2), (16, bottom - 3),
            (14, bottom - 7)], blue, False)
    if p.scale > 1:
        p.line([(16.6, y + 4), (16.6, bottom - 5)], white, .42)
    if n <= 3:
        p.rect(15, top, 3, 5, gold, True)
        arm = 5 if n < 3 else 7
        p.rect(16 - arm, y, 2 * arm, 3, gold, True)
        if n >= 1:
            for x in (16 - arm + 1, 16 + arm - 1):
                p.gem(x, y + 4, 1, 3, blue, white)
        if n == 3:
            for x in (9, 22):
                p.line([(x, y + 1), (x, y - 4)], gold, 2)
        if n >= 2:
            p.gem(16, y + 8, 5, 5, blue, white)
    else:
        arms = 6 if n < 8 else 8 if n == 8 else 12
        radius = 7 if n == 4 else 8 if n < 8 else 10
        for i in range(arms):
            angle = 2 * math.pi * i / arms + math.pi / 2
            length = radius if n <= 8 or i % 2 == 0 else radius - 3
            tx, ty = round(16 + length * math.cos(angle)), round(y + length * math.sin(angle))
            p.line([(16, y), (tx, ty)], p.ink, 4)
            p.line([(16, y), (tx, ty)], blue, 2)
            if n >= 5 and i % 2 == 0:
                p.line([(tx - 2, ty), (tx, ty), (tx, ty + 2)], white)
        p.gem(16, y, 3, 4, gold, white)
    for yy in [45, 48] if n < 3 else [43, 46, 49]:
        p.line([(16 - width + 1, yy), (16 + width - 1, yy + 1)], gold)
    if n >= 6:
        p.gem(16, 36, 5, 4, blue, white)
        p.rect(14, bottom - 4, 4, 3, gold, True)
    if n >= 10:
        for i, x in enumerate((8, 12, 16, 20, 24)):
            crown = max(2, top + abs(2 - i) * 2 + (3 if n == 10 else 0))
            p.gem(x, crown + 2, 2, 3 if i != 2 else 4, blue, white)
        for i in range(1 if n == 10 else 2 if n < 13 else 3):
            p.arc(16, y + 10 + i * 9, 12 + i, 4, 0, 360, "#D9C7FF" if i % 2 else blue)
    if n >= 12:
        for i, yy in enumerate((y + 19, y + 25, y + 31)):
            p.gem(16, yy, 2, 2, white if n == 12 or i else gold, white)
            p.poly([(11, yy), (8, yy + 1), (9, yy + 6)], blue)
            p.poly([(21, yy), (24, yy + 1), (23, yy + 6)], blue)


def knot(p, n):
    wood, bark, dark, gold, purple, pale = "#704456", "#A47384", "#42203C", "#CA8F44", "#8A5CFF", "#D9C7FF"
    oy = [29, 28, 25, 27, 24, 24, 24, 22, 22, 21, 16, 13, 13, 9][n]
    rr = 4 if n <= 2 else 5 if n <= 6 else 6 if n < 13 else 7
    collar = oy + rr + (3 if n < 10 else 7)
    sy = collar + (5 if n >= 11 else 0)
    bend = 2 if n < 6 else 5
    p.poly([(15, sy), (20, sy), (19, sy + 8), (16 - bend, 46), (18, 53),
            (17, 57 if n >= 5 else 62), (12, 57 if n >= 5 else 62),
            (13, 53), (12 - bend, 46), (14, sy + 8)], dark if n >= 6 else wood)
    p.line([(17, sy + 1), (16, sy + 7), (14 - bend, 46), (16, 52), (15, 56)], wood if n >= 6 else bark)
    if p.scale > 1:
        p.line([(18, sy + 2), (18, sy + 7), (15 - bend, 46),
                (17, 53), (16, 58)], bark, .42)
    p.rect(14, collar - 1, 7, 3, gold, True)
    if 5 <= n <= 12:
        p.poly([(13, 60), (17, 60), (16, 63), (12, 63)], dark if n >= 6 else wood)
    elif n == 13:
        p.line([(15, 59), (12, 61), (9, 63)], dark, 2)
    if n >= 7:
        p.arc(17, oy, 11 if n < 12 else 13, rr + (5 if n >= 9 else 3),
              260 if n < 9 else 225, 102 if n < 9 else 125, pale, 1 if n < 9 else 2)
    if n < 3:
        p.gem(17, oy, 3 if n < 2 else 4, 5 if n < 2 else 7, purple, pale)
    else:
        p.oval(17, oy, rr, rr, purple if n < 7 else "#4B2C83" if n < 9 else p.ink)
        if n >= 10:
            p.arc(17, oy, rr, rr, 0, 360, pale if n == 13 else purple, 2)
        if n < 4:
            p.line([(14, oy), (20, oy)], pale)
        else:
            p.line([(18, oy - 2), (17, oy + 1), (18, oy + 2)], pale)
    if n < 10:
        ext = (0, 3, 5, 2, 4, 4, 4, 4, 4, 4)[n]
        p.poly([(15, collar), (10 - ext, collar - 4), (9 - ext, oy + 2),
                (11 - ext, oy + (1 if n else 4)), (13 - ext, oy + 5), (18, collar - 2),
                (22 + ext, oy + 4), (23 + ext, oy + (0 if n >= 4 else 2)),
                (24 + ext, oy + 4), (23 + ext, collar - 2), (19, collar)],
               wood if n < 6 else dark)
    else:
        p.poly([(11 if n < 12 else 14, collar - 4), (17, collar - 3), (24 if n < 12 else 21, collar - 4),
                (21, collar), (13, collar)], dark)
        if n >= 12:
            for x, y in [(5, oy + 5), (28, oy + 5), (18, oy + 10)]:
                p.poly([(x - 2, y + 2), (x - 3, y - 1), (x, y - 4), (x + 2, y - 2),
                        (x, y + 3)], wood)
    if n >= 6:
        p.arc(22, collar + 5, 3, 4, 315, 170, gold)
    if n >= 8:
        p.poly([(6, 35), (8, 38), (7, 44), (5, 46), (4, 42)], dark)


DRAW = (forge, lightning, paper, frost, knot)


def center_art(img, size, maximum=None):
    trimmed = img.crop(img.getbbox())
    trimmed.thumbnail(maximum or (size - 8, size - 8), NEAREST)
    result = Image.new("RGBA", (size, size))
    result.alpha_composite(trimmed, ((size - trimmed.width) // 2, (size - trimmed.height) // 2))
    return result


def angled(im, degrees, flip):
    if flip:
        im = im.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    if degrees == 0:
        return im
    a = math.radians(degrees)
    sx, sy = math.cos(a), math.sin(a)
    # Lean about the grip. Compress wide heads just enough to keep them inside 32px.
    b = im.getbbox()
    left = min(sx * (x - 16) + sy * (y - 48) for x in (b[0], b[2] - 1) for y in (b[1], b[3] - 1))
    right = max(sx * (x - 16) + sy * (y - 48) for x in (b[0], b[2] - 1) for y in (b[1], b[3] - 1))
    scale_x = min(1, 15 / max(abs(left), abs(right), 1))
    return im.transform(im.size, Image.Transform.AFFINE, (
        sx / scale_x, -sy, 16 - 16 * sx / scale_x + 48 * sy,
        sy / scale_x, sx, 48 - 16 * sy / scale_x - 48 * sx
    ), NEAREST)


def main():
    atlas = Image.new("RGBA", (14 * 48 + 112, 5 * 81 + 42), "#171827")
    canvas = ImageDraw.Draw(atlas)
    font = ImageFont.load_default()
    canvas.text((12, 10), "WANDS  /  HAND SPRITES AT 1x", "#D9C7A9", font=font)
    manifest = {"source": "docs/wands/*.md", "handCanvas": [32, 64], "grip": [16, 48],
                "filter": "point", "mipmaps": False, "sprites": {}}
    for row, (family, renderer) in enumerate(zip(FAMILIES, DRAW)):
        folder = ROOT / family
        folder.mkdir(exist_ok=True)
        sheet = Image.new("RGBA", (32 * 14, 64 * 6))
        inspect = Image.new("RGBA", (192 * 7, 192 * 2))
        icons = Image.new("RGBA", (64 * 14, 64))
        stage_meta = []
        canvas.text((12, 67 + row * 81), family.upper(), "#D9C7A9", font=font)
        for n in range(14):
            sprite = PixelWand(family, 1)
            renderer(sprite, n)
            for d, (direction, flip, tilt) in enumerate((("down", False, 0), ("right", False, -4), ("up", True, 0))):
                for pose, angle in enumerate((tilt, tilt - 13 if direction != "up" else 13)):
                    sheet.alpha_composite(angled(sprite.image, angle, flip), (n * 32, (d * 2 + pose) * 64))
            portrait = PixelWand(family, 3)
            renderer(portrait, n)
            inspect.alpha_composite(center_art(portrait.image, 192, (158, 168)), (192 * (n % 7), 192 * (n // 7)))
            x0, y0, x1, y1 = portrait.image.getbbox()
            head = portrait.image.crop((x0, y0, x1, min(y1, y0 + 65)))
            icons.alpha_composite(center_art(head, 64, (52, 52)), (64 * n, 0))
            atlas.alpha_composite(sprite.image, (103 + 48 * n, 34 + row * 81))
            canvas.text((113 + n * 48, 28), f"+{n}", "#A9A2B5", font=font)
            bounds = sprite.image.getbbox()
            head_y = (bounds[1] * 2 + bounds[3]) // 3
            stage_meta.append({"stage": n, "head": [16, head_y], "mutations": [[12, head_y + 3], [20, head_y + 3]] +
                               ([[16, head_y - 1]] if family == "chorong" else []), "expansion": [18, 49]})
        sheet.save(folder / "hand.png")
        inspect.save(folder / "inspection.png")
        icons.save(folder / "icon.png")
        manifest["sprites"][family] = {"hand": {"file": f"{family}/hand.png", "cell": [32, 64], "columns": 14,
            "rows": ["down_hold", "down_aim", "right_hold", "right_aim", "up_hold", "up_aim"]},
            "inspection": {"file": f"{family}/inspection.png", "cell": [192, 192], "columns": 7, "rows": 2},
            "icon": {"file": f"{family}/icon.png", "cell": [64, 64], "columns": 14}, "stages": stage_meta}
    manifest["detachedEffects"] = detached_effects()
    atlas.save(ROOT / "preview.png")
    (ROOT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")


def detached_effects():
    folder = ROOT / "parts"
    folder.mkdir(exist_ok=True)
    exports = {}

    def sheet(name, width, height, sprites, anchor):
        atlas = Image.new("RGBA", (width * len(sprites), height))
        for i, sprite in enumerate(sprites):
            atlas.alpha_composite(sprite, (i * width, 0))
        atlas.save(folder / f"{name}.png")
        exports[name] = {"file": f"parts/{name}.png", "cell": [width, height],
                         "frames": len(sprites), "anchor": anchor}

    sprites = []
    for phase in range(4):
        frame = Image.new("RGBA", (8, 16))
        d = ImageDraw.Draw(frame)
        points = [(4, 15), (5, 11), (3 + phase % 2, 8), (5, 5), (2 + phase % 3, 0)]
        d.line(points, "#FFF36B", width=3)
        d.line(points, "#FFFFFF", width=1)
        sprites.append(frame)
    sheet("chorong_max_lightning", 8, 16, sprites, [4, 15])
    sprites = []
    for phase in range(8):
        frame = Image.new("RGBA", (40, 16))
        d = ImageDraw.Draw(frame)
        d.ellipse((2, 3, 37, 12), outline="#5E9AC7")
        d.arc((2, 3, 37, 12), phase * 45, phase * 45 + 78, fill="#D9C7FF", width=2)
        x = round(20 + 16 * math.cos(phase * math.tau / 8))
        y = round(8 + 3 * math.sin(phase * math.tau / 8))
        d.polygon([(x, y - 3), (x + 2, y), (x, y + 3), (x - 2, y)],
                  fill="#9FDCFF", outline="#1E2238")
        d.point((x, y - 1), fill="#F4FBFF")
        sprites.append(frame)
    sheet("seonge_orbit", 40, 16, sprites, [20, 8])
    sprites = []
    for phase in range(3):
        frame = Image.new("RGBA", (20, 20))
        d = ImageDraw.Draw(frame)
        d.ellipse((2, 2, 17, 17), fill="#1B0D1B", outline="#8A5CFF", width=2)
        d.arc((2, 2, 17, 17), 150 + phase * 25, 286 + phase * 25, fill="#D9C7FF", width=2)
        d.line([(10, 6), (9 - phase, 10), (10, 13)], fill="#B25A9B")
        sprites.append(frame)
    sheet("geumeum_orb", 20, 20, sprites, [10, 10])
    return exports


if __name__ == "__main__":
    main()
