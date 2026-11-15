"""Export the directly drawn apprentices and game animation coordinates."""
from pathlib import Path
import json
import math

from PIL import Image, ImageDraw, ImageFont

from pixels import ANCHORS, DIRECTIONS, FAMILIES, GROUND, INK, SOCKETS, draw_body, draw_selection

ROOT = Path(__file__).parent
NEAREST = Image.Resampling.NEAREST
FEET = GROUND
COSTUME = {
    "dalgu": ("#16655F", "#4FC2B0", "#F2CFA0"),
    "chorong": ("#8A6A14", "#C89A22", "#F0CFA8"),
    "galpi": ("#3A4658", "#55657C", "#F2D2AC"),
    "seonge": ("#C9B88A", "#F3E7C0", "#F4D6B4"),
    "geumeum": ("#222641", "#3E4675", "#7A336A"),
}
THREE_WAY = (
    ("idle", 4, 7), ("walk", 6, 10), ("stance_walk", 4, 9), ("fire", 3, 14),
    ("special", 4, 12), ("signature", 6, 12), ("hit", 2, 12)
)
FRONT = (
    ("death", 6, 10), ("sit", 2, 4), ("wave", 4, 7), ("clap", 4, 8),
    ("taunt", 4, 7), ("bow", 4, 8), ("sit_down", 4, 8), ("dance", 4, 9),
    ("lift_wand", 4, 8), ("frustrated", 4, 6), ("upgrade_wait", 2, 5),
    ("upgrade_charge", 2, 5), ("upgrade_success", 4, 8), ("upgrade_fail", 4, 8),
    ("duel_win", 4, 8), ("duel_loss", 4, 8)
)


def place(base, x=0, y=0, compression=0, lean=0):
    if compression == 0 and lean == 0:
        frame = Image.new("RGBA", (64, 64))
        frame.alpha_composite(base, (x, y))
        return frame
    sy = 1 - compression / 60
    return base.transform(base.size, Image.Transform.AFFINE,
                          (1, lean / 60, -x - lean,
                           0, 1 / sy, 60 - (60 + y) / sy), NEAREST)


def step(base, index, count, stance):
    phase = round(index * 6 / count) % 6
    dx = (0, 1, 1, 0, -1, -1)[phase]
    dy = (0, -1, -2, 0, -1, -2)[phase]
    height = 4 if stance else 0
    frame = place(base, dx, dy, height, 2 if stance else 0)
    # Raise each boot in turn. Leave the other foot near the ground contact.
    feet = base.crop((0, 54, 64, 61))
    frame.paste((0, 0, 0, 0), (0, 55, 64, 64))
    for start, stop, sign in [(0, 30, 1), (30, 64, -1)]:
        if phase == 0 or phase == 3:
            stride, lift = 0, 0
        elif phase < 3:
            stride, lift = (1 + phase % 2) * sign, -2 if sign == 1 else 0
        else:
            stride, lift = -(1 + phase % 2) * sign, -2 if sign == -1 else 0
        frame.alpha_composite(feet.crop((start, 0, stop, 7)), (start + dx + stride, 54 + lift))
    return frame, dx, dy + height // 2


def arm(frame, family, socket, target, width=6):
    if socket == target:
        return
    ink = INK[FAMILIES.index(family)]
    shadow, light, hand = COSTUME[family]
    d = ImageDraw.Draw(frame)
    x0, y0 = socket
    x1, y1 = target
    d.line([(x0, y0), (x1, y1)], fill=ink, width=width)
    d.line([(x0, y0 - 1), (x1, y1 - 1)], fill=shadow, width=width - 2)
    d.line([(x0, y0 - 2), ((x0 + x1) // 2, (y0 + y1) // 2 - 2)], fill=light, width=1)
    d.rectangle((x1 - 2, y1 - 2, x1 + 2, y1 + 2), fill=hand, outline=ink)


def body_pose(base, family, direction, action, i, count):
    hand = SOCKETS[family][DIRECTIONS.index(direction)]
    head = ANCHORS[family][DIRECTIONS.index(direction)]
    dx = dy = 0
    target = None
    if action in ("walk", "stance_walk"):
        frame, dx, dy = step(base, i, count, action == "stance_walk")
        if action == "stance_walk":
            target = (hand[0] + (4 if direction == "up" else 6), hand[1] + 2)
    elif action in ("idle", "upgrade_wait", "upgrade_charge"):
        dy = (0, -1, -1, 0)[i]
        frame = place(base, y=dy, compression=int(action == "upgrade_charge") * 2)
    elif action in ("fire", "special", "signature"):
        extent = {
            "fire": (0, 8, 4), "special": (1, 5, 8, 3),
            "signature": (0, 3, 6, 10, 7, 3),
        }[action][i]
        angles = {"dalgu": 1, "chorong": 2, "galpi": 1, "seonge": 2, "geumeum": 3}
        rise = angles[family] if action == "signature" else 1
        dx = -int(action == "fire" and i == 2)
        dy = -(i in (1, 2, 3))
        frame = place(base, dx, dy, lean=3 if action == "signature" else 1)
        target = (hand[0] + (extent // 2 if direction == "up" else extent),
                  hand[1] - (extent * rise) // 4)
    elif action == "hit":
        dx, dy = ((-3, -1), (1, 0))[i]
        frame = place(base, dx, dy, 2, -4 + i * 3)
    elif action == "death":
        if i == 5:
            return Image.new("RGBA", (64, 64)), {"feet": list(FEET), "hand": list(hand),
                "head": [30, 15], "shoulder": [30, 34], "wandOnly": True}
        dy = 0
        frame = place(base, i, 0, (0, 5, 13, 25, 41)[i], (0, 1, 4, 8, 12)[i])
        if i >= 3:
            a = frame.getchannel("A")
            mask = Image.new("L", frame.size)
            d = ImageDraw.Draw(mask)
            d.polygon([(7 + i, 44), (44 + i, 40), (58, 60), (12, 60)], fill=255)
            a = Image.composite(a, Image.new("L", frame.size), mask)
            frame.putalpha(a)
    else:
        squash = {
            "sit": (10, 11), "sit_down": (0, 4, 8, 10), "bow": (0, 5, 10, 5),
            "frustrated": (1, 4, 7, 6), "upgrade_fail": (0, 5, 8, 6), "duel_loss": (1, 4, 8, 10)
        }.get(action, (0, 0, 1, 0))[i]
        if action == "dance":
            dx, dy = ((-2, 0), (1, -3), (2, 0), (-1, -3))[i]
        if action in ("duel_win", "upgrade_success"):
            dy = (0, -4, -6, -2)[i]
        frame = place(base, dx, dy, squash, squash // 2)
        signs = {
            "wave": ((0, 0), (-8, -12), (-4, -15), (-9, -11)),
            "clap": ((3, -2), (9, -4), (10, -5), (6, -3)),
            "taunt": ((0, 0), (-6, -4), (-10, -5), (-7, -3)),
            "dance": ((-5, -3), (-8, -6), (-5, -4), (-7, -8)),
            "lift_wand": ((0, 0), (-5, -5), (-4, -11), (-3, -15)),
            "upgrade_success": ((0, 0), (-5, -8), (-7, -13), (-6, -8)),
            "duel_win": ((0, 0), (-7, -8), (-8, -15), (-6, -12)),
        }
        if action in signs:
            ax, ay = signs[action][i]
            target = (hand[0] + dx + ax, hand[1] + dy + ay)
        elif squash:
            dy += round(squash / 3)
    original = (hand[0] + dx, hand[1] + dy)
    if target:
        arm(frame, family, original, target)
    return frame, {"feet": list(FEET), "hand": list(target or original),
                   "head": [head[0] + dx, head[1] + dy], "shoulder": [30 + dx, 35 + dy]}


def size_center(im, size, extent):
    crop = im.crop(im.getbbox())
    width, height = crop.size
    s = extent / max(width, height)
    crop = crop.resize((max(1, round(width * s)), max(1, round(height * s))), NEAREST)
    result = Image.new("RGBA", (size, size))
    result.alpha_composite(crop, ((size - crop.width) // 2, (size - crop.height) // 2))
    return result


def make_portraits(base, family, folder):
    draw_selection(family).save(folder / "selection_192x192.png")
    portraits = Image.new("RGBA", (64 * 4, 64))
    for i, mood in enumerate(("neutral", "happy", "strained", "blink")):
        tile = draw_body(family, "down", mood=mood).crop((0, 0, 64, 41))
        portraits.alpha_composite(size_center(tile, 64, 54), (64 * i, 0))
    portraits.save(folder / "portraits_64x64_f4.png")
    silhouette = Image.new("RGBA", (32, 32), "black")
    silhouette.putalpha(size_center(base, 32, 29).getchannel("A"))
    silhouette.save(folder / "silhouette_32x32.png")


def main():
    manifest = {"source": "docs/world-characters.md §3, §10.1", "filter": "point", "mipmaps": False,
                "canvas": [64, 64], "pivotPx": list(FEET), "mirrorRightForLeft": True,
                "artworkSource": "pixels.py", "characters": {}}
    preview = Image.new("RGBA", (5 * 151 + 118, 3 * 153 + 49), "#171827")
    d = ImageDraw.Draw(preview)
    d.text((12, 12), "APPRENTICES  /  64 PX GAME SPRITES", "#D9C7A9", font=ImageFont.load_default())
    for row, direction in enumerate(DIRECTIONS):
        d.text((12, 101 + 153 * row), direction.upper(), "#A9A2B5", font=ImageFont.load_default())
    for col, family in enumerate(FAMILIES):
        folder = ROOT / family
        folder.mkdir(exist_ok=True)
        bases = {direction: draw_body(family, direction) for direction in DIRECTIONS}
        for row, base in enumerate(bases.values()):
            preview.alpha_composite(base.resize((128, 128), NEAREST), (103 + col * 151, 39 + row * 153))
        d.text((135 + col * 151, 498), family.upper(), "#D9C7A9", font=ImageFont.load_default())
        make_portraits(bases["down"], family, folder)
        poses = [(action, direction, count, fps) for action, count, fps in THREE_WAY for direction in DIRECTIONS]
        poses.extend((action, "down", count, fps) for action, count, fps in FRONT)
        count = sum(c for _, _, c, _ in poses)
        assert count == 147
        sheet = Image.new("RGBA", (64 * 12, 64 * math.ceil(count / 12)))
        clips = {}
        index = 0
        for action, direction, frames, fps in poses:
            key = f"{action}_{direction}"
            sequence = []
            for i in range(frames):
                expression = {"hit": "strained", "duel_win": "happy", "upgrade_success": "happy",
                              "duel_loss": "strained", "upgrade_fail": "strained"}.get(action)
                if action == "idle" and i == 2:
                    expression = "blink"
                base = draw_body(family, direction, mood=expression) if expression else bases[direction]
                sprite, sockets = body_pose(base, family, direction, action, i, frames)
                x, y = (index % 12) * 64, (index // 12) * 64
                sheet.alpha_composite(sprite, (x, y))
                sequence.append({"rect": [x, y, 64, 64], **sockets})
                index += 1
            clips[key] = {"fps": fps, "loop": action in ("idle", "walk", "stance_walk", "sit"), "frames": sequence}
        sheet.save(folder / "body.png")
        manifest["characters"][family] = {"body": f"{family}/body.png", "frames": count,
             "portrait": f"{family}/portraits_64x64_f4.png", "selection": f"{family}/selection_192x192.png",
             "silhouette": f"{family}/silhouette_32x32.png", "clips": clips}
    preview.save(ROOT / "preview.png")
    (ROOT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")


if __name__ == "__main__":
    main()
