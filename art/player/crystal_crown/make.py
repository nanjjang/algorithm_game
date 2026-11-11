#!/usr/bin/env python3
"""Crystal Crown kid wizard - 32x32 pixel sprite generator.
Every pixel is authored in the ASCII grids / pixel lists below.
Run: python3 make.py  -> sheet.png, preview.png, idle.gif, walk.gif
"""
import os
from PIL import Image

OUT = os.path.dirname(os.path.abspath(__file__))
W = H = 32
BG = (0x16, 0x21, 0x3A, 255)

PAL = {
    '.': (0, 0, 0, 0),
    'k': (0x0D, 0x0B, 0x1A, 255),  # outline
    'h': (0xE9, 0xE0, 0xFF, 255),  # hair light (lavender white)
    'H': (0xA9, 0x94, 0xE0, 255),  # hair shade
    's': (0xFF, 0xDC, 0xB8, 255),  # skin
    'S': (0xCF, 0x92, 0x64, 0xFF),  # skin shade / far boot
    'c': (0xFF, 0xF3, 0xD0, 255),  # cape cream
    'C': (0xD6, 0xB8, 0x84, 255),  # cape shade / near boot
    'b': (0x7F, 0xD6, 0xFF, 255),  # cape lining sky blue
    'B': (0x3D, 0x6F, 0xD1, 255),  # tunic blue
    'y': (0xFF, 0xC9, 0x4A, 255),  # gold (wand, clasp)
    'w': (0xFF, 0xFF, 0xFF, 255),  # sparkle white
    'm': (0x8F, 0xF5, 0xE0, 255),  # crystal mint
}
assert len(PAL) - 1 <= 12

# ---------------------------------------------------------------- FRONT (idle)
FRONT = [
    "................................",  # 0
    "................................",  # 1
    "................................",  # 2
    "................................",  # 3
    "................................",  # 4
    "................kkk.............",  # 5
    "..............kkhhhk............",  # 6
    ".............khhhhk.............",  # 7
    "............khhhhHk.............",  # 8
    "...........khhhhhhhkkk..........",  # 9
    "..........khhhhhhhhhhhk.........",  # 10
    "..........khhhhhhhhhhhk.........",  # 11
    "..........khHssshsssHhk.........",  # 12
    "..........khssssssssshk.........",  # 13
    "..........khskkssskkshk.........",  # 14
    "..........khSsssssssShk.........",  # 15
    "...........kssssSssssk..........",  # 16
    "............kkssssskk...........",  # 17
    "...........kccccycccck..........",  # 18
    "..........kcccbBBBbccckk........",  # 19
    ".........kccccbBBBbccccssk......",  # 20
    ".........kccccbBBBbccccssk......",  # 21
    "........kcccccbBBBbccckk........",  # 22
    "........kccccCbBBBbccck.........",  # 23
    ".......kcccccbBBBBBbcck.........",  # 24
    ".......kccccCbBBBBBbcck.........",  # 25
    "......kcccccbbkskskbcck.........",  # 26
    "......kccccCbbkCkCkbcck.........",  # 27
    "......kcccCCbkCCkCCkcck.........",  # 28
    "......kkkkkkkkkkkkkkkkk.........",  # 29
    "................................",  # 30
    "................................",  # 31
]

# ---------------------------------------------------------------- SIDE (walk, facing right), passing pose
SIDE = [
    "................................",  # 0
    "................................",  # 1
    "................................",  # 2
    "................................",  # 3
    "................................",  # 4
    "..............kk................",  # 5
    ".............khhkk..............",  # 6
    "..............khhhk.............",  # 7
    "..............kHhhhk............",  # 8
    "...........kkkHhhhhhkkk.........",  # 9
    "..........kHhhhhhhhhhhk.........",  # 10
    "..........kHhhhhhhhsssk.........",  # 11
    "..........kHhhhhhhssssk.........",  # 12
    "..........kHhhhhhsssssk.........",  # 13
    "..........kHhhhhhskkssk.........",  # 14
    "..........kHhhhhHsssssk.........",  # 15
    "...........kHhhHsssssk..........",  # 16
    "............kkkkssskk...........",  # 17
    "............kcccyBBk............",  # 18
    "...........kccccBBBkkkkk........",  # 19
    "..........kcccccBBBBBBBssk......",  # 20
    "..........kcccccBBBkkkkssk......",  # 21
    ".........kccccccBBBk...k........",  # 22
    ".........kcccccCBBBk............",  # 23
    "........kccccccbBBBBk...........",  # 24
    ".......kccccccCbBBBBk...........",  # 25
    "......kccccccCbbkssk............",  # 26
    "......kcccccCbbbkCCk............",  # 27
    ".....kcccccCbbbbkCCCk...........",  # 28
    ".....kkkkkkkkkkkkkkkk...........",  # 29
    "................................",  # 30
    "................................",  # 31
]

# leg / hem rows (26..29) for each walk frame
WALK_LEGS = {
    0: [  # contact: near leg forward, far leg back
        "......kccccccCbkSkksk...........",
        ".....kccccccCbkSSkkCCk..........",
        ".....kcccccCbbkSSkkCCCk.........",
        ".....kkkkkkkkkkkkkkkkkk.........",
    ],
    1: [  # passing: near leg planted
        "......kccccccCbbkssk............",
        "......kcccccCbbbkCCk............",
        ".....kcccccCbbbbkCCCk...........",
        ".....kkkkkkkkkkkkkkkk...........",
    ],
    2: [  # contact: far leg forward, near leg back
        "......kccccccCbkskkSk...........",
        ".....kccccccCbkCCkkSSk..........",
        ".....kcccccCbbkCCkkSSSk.........",
        ".....kkkkkkkkkkkkkkkkkk.........",
    ],
    3: [  # passing: far leg planted
        "......kccccccCbbkSSk............",
        "......kcccccCbbbkSSk............",
        ".....kcccccCbbbbkSSSk...........",
        ".....kkkkkkkkkkkkkkkk...........",
    ],
}

# wand: thin upright sceptre, drawn BEHIND the body grid (the fist covers it)
WAND = [(24, 10, 'w'), (23, 11, 'y'), (24, 11, 'w'), (25, 11, 'y'), (24, 12, 'y')] + \
       [(24, y, 'y') for y in range(13, 26)]

# crown gems: (x, top_y); each gem is 3 px tall: w / m / b
GEMS_FRONT = [(8, 4), (12, 2), (16, 0), (20, 2), (24, 4)]
GEMS_SIDE = [(7, 4), (11, 2), (15, 0), (19, 2), (23, 4)]   # trails 1px behind when walking
GEM = ['w', 'm', 'b']

TOP, CUT = 5, 23   # bob: rows TOP..CUT-1 move down 1, row CUT is swallowed


def check(g):
    assert len(g) == H, len(g)
    for i, r in enumerate(g):
        assert len(r) == W, (i, len(r), r)
        for ch in r:
            assert ch in PAL, ch
    return [list(r) for r in g]


def bob(g):
    n = [row[:] for row in g]
    for y in range(CUT, TOP, -1):
        n[y] = g[y - 1][:]
    n[TOP] = ['.'] * W
    return n


def compose(body, body_dy, gems, gem_dys):
    g = [['.'] * W for _ in range(H)]
    for x, y, ch in WAND:
        g[y + body_dy][x] = ch
    b = bob(body) if body_dy else body
    for y in range(H):
        for x in range(W):
            if b[y][x] != '.':
                g[y][x] = b[y][x]
    for (x, ty), dy in zip(gems, gem_dys):
        for i, ch in enumerate(GEM):
            assert g[ty + dy + i][x] == '.', "gem overlaps body"
            g[ty + dy + i][x] = ch
    return g


def frames():
    out = []
    front = check(FRONT)
    # idle: body bob 0,0,1,1 ; gems ripple across the crown
    idle_body = [0, 0, 1, 1]
    idle_gems = [
        [1, 1, 0, 0, 1],
        [1, 1, 1, 0, 0],
        [0, 1, 1, 1, 0],
        [0, 0, 1, 1, 1],
    ]
    for f in range(4):
        out.append(compose(front, idle_body[f], GEMS_FRONT, idle_gems[f]))
    # walk: body down on contact frames; gems follow one frame late
    walk_body = [1, 0, 1, 0]
    for f in range(4):
        side = SIDE[:26] + WALK_LEGS[f] + SIDE[30:]
        side = check(side)
        lag = walk_body[(f - 1) % 4]
        out.append(compose(side, walk_body[f], GEMS_SIDE, [lag] * 5))
    return out


def to_img(g):
    im = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    px = im.load()
    for y in range(H):
        for x in range(W):
            px[x, y] = PAL[g[y][x]]
    return im


def main():
    imgs = [to_img(g) for g in frames()]
    sheet = Image.new('RGBA', (W * 8, H), (0, 0, 0, 0))
    for i, im in enumerate(imgs):
        sheet.paste(im, (i * W, 0))
    sheet.save(os.path.join(OUT, 'sheet.png'))

    S, GAP = 8, 8
    pw = GAP + 8 * (W * S + GAP)
    ph = GAP + H * S + GAP + H * 2 + GAP
    prev = Image.new('RGBA', (pw, ph), BG)
    for i, im in enumerate(imgs):
        big = im.resize((W * S, H * S), Image.NEAREST)
        prev.alpha_composite(big, (GAP + i * (W * S + GAP), GAP))
        sm = im.resize((W * 2, H * 2), Image.NEAREST)
        prev.alpha_composite(sm, (GAP + i * (W * 2 + GAP), GAP + H * S + GAP))
    prev.save(os.path.join(OUT, 'preview.png'))

    def gif(fr, name):
        fs = []
        for im in fr:
            c = Image.new('RGBA', (W * S, H * S), BG)
            c.alpha_composite(im.resize((W * S, H * S), Image.NEAREST))
            fs.append(c.convert('RGB').convert('P', palette=Image.ADAPTIVE, colors=32))
        fs[0].save(os.path.join(OUT, name), save_all=True, append_images=fs[1:],
                   duration=150, loop=0, disposal=2)
    gif(imgs[:4], 'idle.gif')
    gif(imgs[4:], 'walk.gif')

    used = set()
    for im in imgs:
        used |= {im.getpixel((x, y)) for x in range(W) for y in range(H) if im.getpixel((x, y))[3]}
    print('colors used:', len(used))


if __name__ == '__main__':
    main()
