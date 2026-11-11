"""Composite representative exported sprites at game scale over the field art."""
from pathlib import Path
import json

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).parent
NEAREST = Image.Resampling.NEAREST
FAMILIES = ("dalgu", "chorong", "galpi", "seonge", "geumeum")


def read(folder):
    return json.loads((ROOT / folder / "manifest.json").read_text())


def cut(path, rect):
    x, y, w, h = rect
    return Image.open(path).convert("RGBA").crop((x, y, x + w, y + h))


def characters():
    sheet = Image.new("RGBA", (960, 192), "#171827")
    for i, name in enumerate(FAMILIES):
        sheet.alpha_composite(Image.open(ROOT / "players" / name / "selection_192x192.png"), (i * 192, 0))
    sheet.save(ROOT / "players/selection_preview.png")


def combat():
    players, monsters, effects = read("players"), read("monsters"), read("effects")
    canvas = Image.open(ROOT / "world/backplates/field.png").convert("RGBA").resize((1200, 675), NEAREST)
    canvas.alpha_composite(Image.new("RGBA", canvas.size, (20, 18, 37, 30)))
    entities = []

    def entity(foot, sprites, shadow_width):
        x, y = foot
        entities.append((y, x, sprites, shadow_width))

    def apprentice(name, action, index, foot, stage):
        entry = players["characters"][name]
        meta = entry["clips"][action]["frames"][index]
        body = cut(ROOT / "players" / entry["body"], meta["rect"])
        x, y = foot[0] - meta["feet"][0], foot[1] - meta["feet"][1]
        direction = action.rsplit("_", 1)[1]
        row = {"down": 0, "right": 2, "up": 4}[direction]
        aim = action.startswith(("fire", "special", "signature", "stance"))
        wand = cut(ROOT / "wands" / name / "hand.png", [stage * 32, (row + aim) * 64, 32, 64])
        wand_xy = x + meta["hand"][0] - 16, y + meta["hand"][1] - 48
        composition = [(wand, wand_xy), (body, (x, y))] if direction == "up" else [(body, (x, y)), (wand, wand_xy)]
        entity(foot, composition, 34)

    def enemy(name, foot):
        entry = monsters["entries"][name]
        first = next(iter(entry["clips"].values()))[0]
        sprite = cut(ROOT / "monsters" / entry["path"], first["rect"])
        ox, oy = first.get("origin", entry["origin"])
        x, y = foot
        entity(foot, [(sprite, (x-ox, y-oy))], sprite.width * .54)

    def effect(group, name, frame, at, rotation=0):
        data = effects[group][name]
        art = cut(ROOT / "effects" / data["path"], data["frames"][frame])
        if rotation:
            art = art.rotate(rotation, resample=NEAREST, expand=True)
        canvas.alpha_composite(art, (at[0] - art.width // 2, at[1] - art.height // 2))

    apprentice("dalgu", "stance_walk_right", 1, (402, 409), 6)
    apprentice("chorong", "idle_down", 0, (473, 476), 8)
    apprentice("galpi", "fire_right", 1, (565, 455), 10)
    apprentice("seonge", "idle_down", 0, (341, 466), 4)
    apprentice("geumeum", "idle_right", 0, (469, 382), 13)
    for name, foot in [
        ("enemy.wolf_bramble", (663, 360)), ("enemy.slime_moss", (735, 435)),
        ("enemy.golem_menhir", (818, 392)), ("enemy.shaman_cap", (722, 328)),
        ("enemy.toad_drum", (860, 460)), ("enemy.bat_dusk", (634, 290)),
    ]:
        enemy(name, foot)
    effect("special", "cast_seal_03", 1, (562, 466))
    effect("enemy", "impact_circle", 1, (765, 420))
    effect("loot", "coin", 0, (649, 478))
    effect("loot", "block_heroic", 1, (670, 470))
    shade = Image.new("RGBA", canvas.size)
    draw = ImageDraw.Draw(shade)
    for y, x, _, w in entities:
        draw.ellipse((x - w / 2, y - 4, x + w / 2, y + 5), fill=(14, 12, 22, 103))
    canvas.alpha_composite(shade)
    for _, _, sprites, _ in sorted(entities):
        for sprite, pos in sprites:
            canvas.alpha_composite(sprite, pos)
    effect("player", "fireball", 0, (478, 401))
    effect("player", "ice_lance", 0, (399, 436), 70)
    effect("player", "shadow_orb", 1, (563, 353))
    effect("player", "mana_bolt", 0, (630, 430))
    effect("enemy", "round_small", 1, (732, 366))
    effect("enemy", "round_small", 0, (712, 370))
    effect("player", "hit_arcane", 0, (702, 421))
    draw = ImageDraw.Draw(canvas)
    draw.rounded_rectangle((16, 16, 184, 41), radius=4, fill=(15, 17, 26, 212))
    draw.text((25, 22), "SPRITES AT GAME SCALE", fill="#EEE1C6", font=ImageFont.load_default())
    canvas.convert("RGB").save(ROOT / "combat_preview.png")


if __name__ == "__main__":
    characters()
    combat()
