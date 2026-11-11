#!/usr/bin/env python3
"""Kettle-helm apprentice wizard - 32x32 pixel sprite generator.
Every pixel comes from the ASCII grids / explicit pixel lists below.
Run: python3 make.py  -> sheet.png, preview.png, idle.gif, walk.gif
"""
import os
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
W = H = 32
BG = (0x16, 0x21, 0x3A)

PALETTE = {
    '.': None,
    'K': '#10121f',  # outline
    'C': '#c97b3c',  # copper mid
    'c': '#8c4f2a',  # copper dark / wood / hair
    'H': '#f6c078',  # copper highlight / wand glow
    'T': '#1e8f8c',  # teal mid
    't': '#136066',  # teal dark
    'L': '#5fd0bd',  # teal light
    'S': '#f8dcb4',  # skin
    's': '#dba878',  # skin shadow
    'W': '#f6f3e6',  # white (eye glint, boots, steam)
    'B': '#b5b9bf',  # pale grey (boot shade, thin steam)
}
assert len([k for k in PALETTE if k != '.']) <= 12

# ---------------------------------------------------------------- kettle
# rows 0..13 shared by both views (lid knob, lid, dented body, spout)
KETTLE = [
    "................................",  # 0
    "................................",  # 1
    "................................",  # 2
    "...............KK...............",  # 3
    "..............KHCK..............",  # 4
    "............KKKCcKKK............",  # 5
    "...........KHHHCCCCcK...........",  # 6
    "..........KccccccccccK..........",  # 7
    ".........KCHHCCCCCCcccK.KK......",  # 8
    ".........KCWHccCCCCCccKKHCK.....",  # 9
    ".........KCHcHHCCCCCccCCCcK.....",  # 10
    ".........KCHHCCCCCCcccCCcK......",  # 11
    ".........KCCCCCCCCccccccK.......",  # 12
    ".........KccCCCCCCccccKK........",  # 13
]

# ---------------------------------------------------------------- front (idle)
FRONT_BODY = KETTLE + [
    "......KKKHHHHHHCCCCCCccK........",  # 14 brim (+ wand cap)
    ".....KHWKKtsKssssssKstK.........",  # 15
    "......KHKKtSWKSSSSKWStK.........",  # 16
    "......KcKKtSKKSSSSKKStK.........",  # 17
    "......KcKKtSKKSSSSKKStK.........",  # 18
    "......KcKKtSSSSccSSSStK.........",  # 19
    "......KcKLLLLLLLLLLLLTtK........",  # 20
    "......KSSKTTTTTTTTTTTTttK.......",  # 21
    "......KSSKTTtTTTTTTtTTttK.......",  # 22
    "......KKKTTTtTTTTTTtTTTttK......",  # 23
    "......KLTTTTtTTTTTTtTTTttK......",  # 24
    "......KLLTLLTLLTLLTLLTLLtK......",  # 25
    "......KttttttttttttttttttK......",  # 26
]
FRONT_LEGS_DOWN = [  # starts at hem row (27)
    ".......KKKKWWBKKKKBWWKKKK.......",
    "..........KWWBK..KBWWK..........",
    "...........KKK....KKK...........",
]
FRONT_LEGS_UP = [  # body lifted 1px: starts at row 26
    ".......KKKKWWBKKKKBWWKKKK.......",
    "..........KWWBK..KBWWK..........",
    "..........KWWBK..KBWWK..........",
    "...........KKK....KKK...........",
]

# ---------------------------------------------------------------- side (walk right)
SIDE_BODY = KETTLE + [
    "........KHHHHHHCCCCCCccK........",  # 14
    "..........KtttssssKsssK.KK......",  # 15
    "..........KtTTSSSSWKSSKKWHK.....",  # 16
    "..........KtTTSSSSKKSSSKHK......",  # 17
    "..........KtTSSSSSKKSSKKcK......",  # 18
    "...........KSSSSSSSScSKKcK......",  # 19
    ".........KLLLLLLLLLLLTtKcK......",  # 20
    "........KLTTTTTTTTTTTtKSSK......",  # 21
    "........KLTTTtTTTtTTTtKSSK......",  # 22
    ".......KLTTTTtTTTtTTTttKK.......",  # 23
    ".......KLTTTTtTTTtTTTttK........",  # 24
    ".......KLLTLLTLLTLLTLLtK........",  # 25
    ".......KtttttttttttttttK........",  # 26
]
SIDE_LEGS = {
    'contact_near': [
        "........KKBBBKKKKKWWBKK.........",
        ".........KBBBK...KWWBBK.........",
        "..........KKK.....KKKK..........",
    ],
    'contact_far': [
        "........KKWWBKKKKKBBBKK.........",
        ".........KWWBK...KBBBBK.........",
        "..........KKK.....KKKK..........",
    ],
    'pass_near': [
        "........KKKKKKKKKKKKKKK.........",
        ".............KWWBKBBK...........",
        ".............KWWBBKK............",
        "..............KKKK..............",
    ],
    'pass_far': [
        "........KKKKKKKKKKKKKKK.........",
        ".............KBBBKWWK...........",
        ".............KBBBBKK............",
        "..............KKKK..............",
    ],
}

# steam puffs: (x, y, colour) in base (dy=0) coordinates, 4 phases
STEAM_FRONT = [
    [(25, 7, 'W')],
    [(25, 6, 'W'), (26, 6, 'W'), (25, 5, 'W'), (26, 5, 'W')],
    [(25, 7, 'B'), (26, 4, 'W'), (27, 4, 'W'), (26, 3, 'W'), (27, 3, 'B')],
    [(25, 7, 'W'), (27, 2, 'B'), (26, 1, 'B')],
]
STEAM_SIDE = [  # trails backwards (left) while moving right
    [(25, 7, 'W')],
    [(24, 6, 'W'), (25, 6, 'W'), (23, 5, 'W'), (24, 5, 'W')],
    [(25, 7, 'B'), (22, 4, 'W'), (21, 4, 'W'), (21, 3, 'W'), (20, 3, 'B')],
    [(25, 7, 'W'), (19, 2, 'B'), (18, 1, 'B')],
]


def check(rows):
    for i, r in enumerate(rows):
        assert len(r) == W, (i, len(r), r)
        for ch in r:
            assert ch in PALETTE, ch
    return rows


def compose(body, legs, dy, steam, steam_dy, patches=()):
    """body rows pasted at y+dy, legs follow directly under, then explicit pixels."""
    g = [['.'] * W for _ in range(H)]
    check(body); check(legs)
    for y, row in enumerate(body):
        yy = y + dy
        if 0 <= yy < H:
            g[yy] = list(row)
    y0 = len(body) + dy
    for i, row in enumerate(legs):
        g[y0 + i] = list(row)
    assert y0 + len(legs) == 30, "feet outline must end on y=29"
    for x, y, ch in patches:
        g[y + dy][x] = ch
    for x, y, ch in steam:
        yy = y + steam_dy
        if 0 <= yy < H and g[yy][x] == '.':
            g[yy][x] = ch
    return [''.join(r) for r in g]


def build_frames():
    idle_dy = [0, 0, -1, -1]
    idle = []
    for i in range(4):
        legs = FRONT_LEGS_DOWN if idle_dy[i] == 0 else FRONT_LEGS_UP
        idle.append(compose(FRONT_BODY, legs, idle_dy[i], STEAM_FRONT[i], idle_dy[i]))
    walk_dy = [0, -1, 0, -1]
    walk_legs = ['contact_near', 'pass_near', 'contact_far', 'pass_far']
    # hem flare at the back on contact frames
    flare = [(6, 26, 'K'), (7, 26, 't'), (7, 25, 'K'), (6, 25, '.')]
    walk = []
    for i in range(4):
        lag = walk_dy[i - 1]  # steam follows the body one frame late
        patches = flare if walk_dy[i] == 0 else ()
        walk.append(compose(SIDE_BODY, SIDE_LEGS[walk_legs[i]], walk_dy[i],
                            STEAM_SIDE[i], lag, patches))
    return idle, walk


def hex2rgb(h):
    h = h.lstrip('#')
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def render(grid):
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px = im.load()
    for y, row in enumerate(grid):
        for x, ch in enumerate(row):
            col = PALETTE[ch]
            if col:
                px[x, y] = hex2rgb(col) + (255,)
    return im


def on_bg(im, scale):
    big = im.resize((im.width * scale, im.height * scale), Image.NEAREST)
    out = Image.new('RGBA', big.size, BG + (255,))
    out.alpha_composite(big)
    return out.convert('RGB')


def main():
    idle, walk = build_frames()
    frames = [render(g) for g in idle + walk]
    sheet = Image.new('RGBA', (W * 8, H), (0, 0, 0, 0))
    for i, f in enumerate(frames):
        sheet.paste(f, (i * W, 0))
    sheet.save(os.path.join(HERE, 'sheet.png'))

    gap, S1, S2 = 8, 8, 2
    pw = gap + 8 * (W * S1 + gap)
    ph = gap + H * S1 + gap + H * S2 + gap
    prev = Image.new('RGB', (pw, ph), BG)
    for i, f in enumerate(frames):
        prev.paste(on_bg(f, S1), (gap + i * (W * S1 + gap), gap))
        prev.paste(on_bg(f, S2), (gap + i * (W * S2 + gap), gap + H * S1 + gap))
    prev.save(os.path.join(HERE, 'preview.png'))

    for name, fr in (('idle', frames[:4]), ('walk', frames[4:])):
        big = [on_bg(f, 8) for f in fr]
        big[0].save(os.path.join(HERE, name + '.gif'), save_all=True,
                    append_images=big[1:], duration=150, loop=0, disposal=2)
    print('ok', len(frames), 'frames')


if __name__ == '__main__':
    main()
