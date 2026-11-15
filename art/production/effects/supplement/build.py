"""Draw native pixel additions for effects.md §20 and export frame rectangles."""
import json
import math
import random
from pathlib import Path

from PIL import Image, ImageColor, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
UI = ROOT.parents[1] / "ui"
NEAREST = Image.Resampling.NEAREST
INK = "#151524"
DIM = "#353443"
SLATE = "#8790a2"
CHALK = "#f2ede0"
BRASS = "#ba8f52"
GREEN = ("#344321", "#b8f04a", "#eff8b6")
PALETTES = {
    "fire": ("#632e22", "#b46f46", "#ffe08a"),
    "lightning": ("#515163", "#9f9d92", "#fff36b"),
    "arcane": ("#474250", "#be9e6f", "#ead1a0"),
    "ice": ("#35466d", "#8db7cf", "#f4fbff"),
    "dark": ("#251731", "#624c83", "#d9c7ff"),
}
MANIFEST = {}


def canvas(w, h):
    return Image.new("RGBA", (w, h))


def shade(hex_color, alpha=255):
    return (*ImageColor.getrgb(hex_color), alpha)


def ring_point(cx, cy, radius, theta):
    return round(cx + radius * math.cos(theta)), round(cy + radius * math.sin(theta))


def save(key, spec, frames, *, fps=None, loop=False, **extra):
    width, height = frames[0].size
    columns = min(len(frames), max(1, 2048 // width))
    rows = math.ceil(len(frames) / columns)
    sheet = canvas(width * columns, height * rows)
    bounds = []
    for index, frame in enumerate(frames):
        assert frame.size == (width, height) and frame.getchannel("A").getbbox(), (key, index)
        x, y = index % columns * width, index // columns * height
        sheet.alpha_composite(frame, (x, y))
        bounds.append([x, y, width, height])
    filename = f"{key}_{width}x{height}_f{len(frames)}.png"
    sheet.save(ROOT / filename, optimize=True)
    data = {"path": filename, "spec": spec, "size": [width, height], "frames": bounds, "loop": loop, **extra}
    if fps:
        data["fps"] = fps
    MANIFEST[key] = data


def paint(w, h, count, fn):
    frames = []
    for i in range(count):
        image = canvas(w, h)
        fn(ImageDraw.Draw(image), i)
        frames.append(image)
    return frames


def circle_layer(radius, layer):
    im = canvas(240, 240)
    d = ImageDraw.Draw(im)
    center = 120
    if layer == 0:
        d.ellipse((center-radius, center-radius, center+radius, center+radius), outline=SLATE, width=2)
        for i in range(48):
            theta = i * math.tau / 48
            d.line((ring_point(center, center, radius-1, theta),
                    ring_point(center, center, radius-(7 if i % 4 == 0 else 4), theta)), CHALK)
    elif layer == 1:
        for i in range(8):
            angle = i * 45
            d.arc((center-radius, center-radius, center+radius, center+radius), angle+2, angle+40, fill=CHALK, width=2)
            x, y = ring_point(center, center, radius, math.radians(angle))
            d.polygon([(x-3,y),(x,y-3),(x+3,y),(x,y+3)], fill=SLATE)
    elif layer == 2:
        for i in range(18):
            theta = i * math.tau / 18
            x, y = ring_point(center, center, radius, theta)
            p = ring_point(center, center, radius-7, theta)
            d.line([(x-3,y), p, (x+3,y)], fill=SLATE, width=2)
            d.point((x,y), fill=CHALK)
    elif layer == 3:
        pts = [ring_point(center, center, radius, i * math.tau / 10) for i in range(10)]
        d.line([*pts, pts[0]], fill=SLATE, width=2)
        for x,y in pts:
            d.rectangle((x-2,y-2,x+2,y+2), fill=DIM, outline=CHALK)
    else:
        pts = [ring_point(center, center, radius, i * math.tau / 8) for i in range(8)]
        for i,p in enumerate(pts):
            d.line([p, pts[(i+3)%8]], fill=SLATE)
        d.ellipse((center-9,center-9,center+9,center+9),outline=CHALK,width=2)
        d.line((center-12,center,center+12,center),fill=CHALK)
        d.line((center,center-12,center,center+12),fill=CHALK)
    return im


def fragment(draw, index, element):
    rng = random.Random(771 + index)
    a = rng.randrange(7,19)
    b = rng.randrange(9,23)
    x,y = 32,32
    shadow,mid,light=PALETTES[element]
    edges = {
        "fire": [(x-a,y-2),(x-a+4,y-b),(x+4,y-b+3),(x+a,y-3),(x+a-4,y+b-6),(x-7,y+b)],
        "lightning": [(x-3,y-b),(x+a,y-b+7),(x+a-8,y+b),(x-a,y+2)],
        "arcane": [(x-a,y-b+2),(x+a-5,y-b),(x+a,y-b+7),(x+a-6,y+b-3),(x-4,y+b),(x-7,y+b-5),(x-a+4,y+b-2)],
        "ice": [(x+2,y-b),(x+a,y-4),(x+a-6,y+b),(x-a,y+4),(x-a+6,y-5)],
        "dark": [(x-a,y-5),(x-4,y-b),(x+a,y-b+4),(x+3,y-6),(x+1,y+5),(x+a,y+11),(x-4,y+b),(x-a+4,y+6)],
    }[element]
    draw.polygon(edges, fill=mid, outline=shadow, width=2)
    draw.polygon([edges[0],edges[1],(x+2,y-4),(x-4,y+6)],fill=light)
    draw.line([edges[-1],(x-4,y+6),(x+1,y+1),edges[2]],fill=shadow,width=2)
    if element == "arcane":
        draw.line([(x-3,y-6),(x+5,y-6),(x+3,y),(x+7,y)],fill=shadow)
    if element == "fire":
        draw.line([(x-4,y+2),(x-2,y+6),(x+6,y+7)],fill="#e4a65f",width=2)


def mutation(draw, index, key):
    dark,green,light=GREEN
    draw.polygon([(7,2),(25,2),(30,7),(30,25),(25,30),(7,30),(2,25),(2,7)], fill=INK)
    draw.polygon([(8,4),(24,4),(28,8),(28,24),(24,28),(8,28),(4,24),(4,8)], fill=dark, outline="#6d8f39")
    for x,y in [(6,6),(25,6),(6,25),(25,25)]:
        draw.point((x,y),fill=green)
    if key == "twisted_barrel":
        pts=[(8,23),(12,20),(12,14),(17,17),(18,11),(25,9)]
        draw.line(pts,fill=INK,width=6)
        draw.line(pts,fill=green,width=3)
        draw.line([(21,7),(26,7),(26,12)],fill=light,width=2)
        draw.line([(7,26),(9,24)],fill=light)
    elif key == "unstable_core":
        draw.polygon([(16,6),(24,16),(16,26),(8,16)],fill=green,outline=INK,width=2)
        draw.line([(18,8),(14,13),(17,16),(13,23)],fill=dark,width=2)
        for x,y in [(8,8),(24,8),(25,24),(8,24)]:
            draw.rectangle((x,y,x+1,y+1),fill=light)
    elif key == "sensitive_circuit":
        pts=[(6,21),(10,21),(12,10),(16,23),(20,8),(22,16),(26,16)]
        draw.line(pts,fill=INK,width=4)
        draw.line(pts,fill=green,width=2)
        draw.rectangle((24,14,27,17),outline=light)
    elif key == "gluttony":
        draw.polygon([(7,12),(11,9),(21,9),(25,12),(23,22),(19,25),(12,25),(8,21)],fill=green,outline=INK,width=2)
        draw.polygon([(9,14),(23,14),(23,17),(19,19),(13,19),(9,17)],fill=INK)
        for x in (11,16,21):
            draw.polygon([(x-1,14),(x+1,14),(x,17)],fill=light)
    elif key == "mirror":
        draw.line((16,7,16,25),fill=light,width=2)
        draw.polygon([(7,16),(12,10),(12,14),(15,14),(15,18),(12,18),(12,22)],fill=green)
        draw.polygon([(25,16),(20,10),(20,14),(17,14),(17,18),(20,18),(20,22)],fill=green)
    else:
        draw.polygon([(8,24),(12,19),(12,16),(18,9),(21,8),(24,10),(22,14),(15,21),(12,22),(9,26)],fill=green)
        draw.line([(18,9),(19,13),(17,15),(19,16)],fill=INK,width=2)
        draw.rectangle((24,6,25,7),fill=light)
        draw.rectangle((7,16,8,17),fill=light)


def mutation_loop(draw, i):
    dark,green,light=GREEN
    extent=11+i
    draw.ellipse((16-extent,16-extent,15+extent,15+extent),outline=dark,width=4)
    for start in (13,135,253):
        draw.arc((16-extent,16-extent,15+extent,15+extent),start+i*12,start+79+i*12,fill=green,width=2)
    draw.point((11+i*2,5),fill=light)


def glyphs(text, size):
    font = ImageFont.truetype(UI / "font" / "Galmuri11.ttf", size)
    bbox = font.getbbox(text)
    mask=Image.new("1",(bbox[2]-bbox[0],bbox[3]-bbox[1]))
    ImageDraw.Draw(mask).text((-bbox[0],-bbox[1]),text,font=font,fill=1)
    return mask


def stamped_message(text, protected):
    w,h=(96,48) if protected else (64,24)
    mask=glyphs(text,12)
    if protected:
        mask=mask.resize((mask.width*2,mask.height*2),NEAREST)
    frames=[]
    for i in range(3):
        im=canvas(w,h)
        d=ImageDraw.Draw(im)
        edge=("#735844","#ad875b","#d8b27b")[i] if protected else ("#5d5665","#797a87","#aaa8b3")[i]
        y=(-4,2,0)[i] if protected else (4,1,0)[i]
        if protected:
            d.polygon([(3,7+y),(91,4+y),(93,40+y),(6,43+y)],fill=INK,outline=edge,width=3)
            d.line((10,10+y,86,8+y),fill=edge)
        else:
            d.polygon([(2,7+y),(6,3+y),(58,3+y),(62,7+y),(59,18+y),(28,18+y),(22,22+y),(18,18+y),(4,18+y)],
                      fill=INK,outline=edge)
        im.paste(CHALK,((w-mask.width)//2,(h-mask.height)//2+y),mask)
        frames.append(im)
    return frames


def privacy(draw, _):
    draw.polygon([(1,8),(4,5),(8,3),(12,5),(14,8),(12,11),(8,13),(4,11)],fill=INK)
    draw.line([(2,8),(5,6),(8,5),(11,6),(13,8),(11,10),(8,11),(5,10),(2,8)],fill=SLATE,width=1)
    draw.rectangle((7,7,8,8),fill=CHALK)
    draw.line((2,13,13,2),fill=INK,width=3)
    draw.line((2,13,13,2),fill=CHALK,width=1)


def arrow(draw, i):
    draw.polygon([(4,12),(15,3),(19,3),(19,21),(15,21)],fill=INK)
    draw.polygon([(7,12),(17,5),(17,19)],fill=SLATE if i==0 else CHALK)
    draw.line((8,12,16,6),fill="#aeafbb" if i==0 else "#ffffff")


def fire_tint(draw, i):
    x,y=48,95
    offsets=(-8,5,1,-4)
    tip=offsets[i]
    draw.polygon([(x-31,y-7),(x-27,y-25),(x-19,y-18),(x-19,y-43),(x+tip,y-58),
        (x+4,y-34),(x+18,y-49),(x+19,y-29),(x+28,y-32),(x+31,y-13),(x+23,y+4),(x-21,y+4)],fill=shade("#606060",205))
    draw.polygon([(x-23,y-7),(x-16,y-25),(x-9,y-20),(x+tip,y-45),(x+5,y-23),(x+16,y-34),
        (x+16,y-17),(x+24,y-23),(x+23,y-7),(x+16,y),(x-17,y)],fill=shade("#b1b1b1",235))
    draw.polygon([(x-12,y-3),(x-8,y-12),(x+1,y-24),(x+4,y-11),(x+11,y-15),(x+12,y-1),(x+8,y+2),(x-9,y+2)],
        fill="#f0f0f0")
    for j in range(5):
        px=x-23+j*11+(i*j)%5
        py=y-58-(j*7+i*5)%26
        draw.rectangle((px,py,px+1+(j%2),py+1+(j%3)),fill=shade("#d9d9d9",130+j*20))


def scrape(draw, _):
    draw.polygon([(1,10),(13,7),(24,8),(42,5),(63,7),(54,10),(31,11),(14,13)],fill="#2c2832")
    draw.line([(2,10),(16,9),(26,10),(44,7),(59,7)],fill="#6d5e59",width=2)
    for x,y in [(7,7),(17,13),(29,6),(36,12),(47,4),(54,10)]:
        draw.rectangle((x,y,x+2,y),fill="#8b7870")


def white_core(draw, i):
    baseline=7+i
    draw.polygon([(0,baseline),(9,baseline-2),(21,baseline),(38,baseline-3),(52,baseline-1),(63,baseline-1),
                  (63,baseline+2),(52,baseline+2),(38,baseline+4),(22,baseline+3),(9,baseline+2),(0,baseline+2)],fill="#d3d5e6")
    draw.line([(0,baseline+1),(12,baseline),(22,baseline+1),(38,baseline),(53,baseline+1),(63,baseline+1)],
              fill="#ffffff",width=2)


def firefly(draw, i):
    draw.point((1,1),fill="#f8edba")
    if i==1:
        for x,y in [(0,1),(1,0),(1,2),(2,1)]:
            draw.point((x,y),fill=shade("#f6e6a3",120))
        draw.point((2,2),fill="#f8edba")


def raindrop(draw, _):
    draw.line([(3,0),(3,5),(2,9),(2,14),(1,18),(0,23)],fill=shade("#c8d8e5",28))
    draw.line((1,17,0,23),fill=shade("#e3eaf2",30))


def water_splash(draw, i):
    r=(2,4,7)[i]
    draw.ellipse((8-r,9-r//3,8+r,9+r//3),outline=shade("#abc3d5",100-i*22))
    for x,y in [(8-r,8-i),(8+r,8-i),(8,5-i)]:
        draw.point((x,y),fill=shade("#d5deec",110-i*18))
