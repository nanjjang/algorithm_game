from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parent
OUT.mkdir(parents=True, exist_ok=True)
INK = '#1B2440'
CREAM = '#FFF2A0'
WHITE = '#FFFFFF'
WARN = '#FF2E88'


class Pixels:
    def __init__(self, size, anchor=None):
        self.w, self.h = (n // 2 for n in size)
        self.margin = 12
        self.image = Image.new('RGBA', (self.w + 2*self.margin, self.h + 2*self.margin))
        self.draw = ImageDraw.Draw(self.image)
        self.anchor = anchor or (self.w // 2, self.h - 3)
        self.shift()

    def poly(self, xy, color, outline=INK):
        self.draw.polygon([(self.x + x, self.y + y) for x, y in xy], fill=color, outline=outline)

    def rect(self, box, color):
        l, t, r, b = box
        self.draw.rectangle((self.x+l, self.y+t, self.x+r, self.y+b), fill=color)

    def line(self, xy, color, width=1):
        self.draw.line([(self.x + x, self.y + y) for x, y in xy], fill=color, width=width)

    def shift(self, x=0, y=0):
        self.x = self.anchor[0] + x + self.margin
        self.y = self.anchor[1] + y + self.margin

    def png(self):
        bounds = self.image.getbbox()
        if bounds is None:
            raise ValueError('Empty pose')
        l,t,r,b = bounds
        art = self.image.crop(bounds)
        scale = min(1, (self.w-2)/art.width, (self.h-2)/art.height)
        width,height = round(art.width*scale), round(art.height*scale)
        left = max(1, min(self.w-1-width, l-self.margin))
        top = max(1, min(self.h-1-height, b-self.margin-height))
        frame = Image.new('RGBA', (self.w*2,self.h*2))
        frame.paste(art.resize((width*2,height*2),Image.Resampling.NEAREST), (left*2,top*2))
        self.origin = tuple(max(0,min(n*2-1, round((o+offset)*2))) for n,o,offset in zip(
            (self.w,self.h), self.anchor, (left-(l-self.margin),top-(b-self.margin-height))))
        return frame


def pebble(p, x, y, w, h, light='#9AA39A', dark='#5C6670', moss=False):
    p.poly([(x,y),(x+2,y-h+1),(x+w-3,y-h),(x+w-1,y-h+2),(x+w,y-1),(x+w-2,y)], light)
    p.poly([(x+1,y-1),(x+4,y-3),(x+w-3,y-3),(x+w-1,y-1)],dark,None)
    if w>=7 and h>=5:
        p.line([(x+2,y-h+2),(x+4,y-h+1),(x+min(w-3,6),y-h+1)],'#BBC2BD')
    if moss:
        p.line([(x+2,y-h+1),(x+4,y-h),(x+w-3,y-h)],'#9CCB6A')


def moss_slime(p, state, i):
    if state == 'death' and i == 1:
        pebble(p,-11,0,4,3,moss=True)
        pebble(p,-5,-1,8,6)
        pebble(p,5,0,5,4,moss=True)
        p.rect((9,-4,10,-3),'#9CCB6A')
        return
    end = state == 'death'
    right = 11 if i == 0 else 9
    left = -11 if i == 0 else -10
    pebble(p,left,0,6,5)
    pebble(p,right-5,0,5,6)
    p.poly([(-7,-2),(-7,-7),(-4,-11),(3,-12),(7,-10),(8,-5),(6,0),(-3,0)],'#9AA39A')
    p.poly([(-6,-4),(-2,-3),(7,-6),(6,-2),(4,-1),(-4,-1)],'#5C6670',None)
    p.line([(0,-5),(4,-6),(7,-6)],INK)
    if not end:
        p.rect((3,-6,6,-6),CREAM)
        p.line([(2,-9),(5,-8),(6,-8)],'#BBC2BD')
        tip = -13 if i == 0 else -14
        p.poly([(left,-2),(left,-6),(left+2,-9),(tip+3,-11),(-7,-11),(-7,-13),(-3,-12),(0,-14),(3,-13),(3,-11),(6,-12),(7,-9),(3,-9),(2,-7),(0,-8),(-2,-5),(-6,-5),(-7,-2)],'#9CCB6A')
        p.poly([(-10,-5),(-8,-8),(-4,-9),(-3,-11),(0,-10),(4,-11),(4,-10),(1,-9),(0,-8),(-2,-5),(-6,-5),(-7,-2)],'#5E8F4A',None)
        p.line([(-9,-8),(-7,-9),(-5,-9)],'#BBD793')
        p.rect((-5,-11,-4,-11),'#BBD793')
    else:
        p.poly([(-9,-4),(-7,-9),(-4,-10),(-3,-7),(-4,-5),(-6,-5)],'#5E8F4A')


def wolf(p, state, i):
    is_death = state == 'death'
    if is_death:
        p.shift(0,2)
        p.poly([(-13,-4),(-11,-6),(-7,-5),(-4,-7-i),(3,-7-i),(5,-5),(8,-6),(11,-4),(13,-2),(12,-1),(3,-1),(0,-2),(-8,-1),(-10,-2)],'#A89C8A')
        p.poly([(-9,-3),(-3,-6),(4,-5),(5,-3),(10,-2),(4,-2),(-5,-2)],'#6A6258',None)
        p.line([(7,-3),(9,-3)],INK)
        p.poly([(-5,-6),(-7,-9),(-4,-8),(-1,-8),(-1,-6)],'#98BF6E')
        if i == 0:
            p.rect((10,-2,11,-2),CREAM)
        return
    y = -1 if state in ('lunge','dodge') or (state == 'move' and i == 3) else 0
    p.shift(0,y)
    fur, shade = '#A89C8A', '#6A6258'
    steps = [(-3,2,3,-1),(-1,-2,1,2),(3,-2,-2,3),(-4,3,4,-2)]
    hind, hind_far, fore, fore_far = steps[i] if state == 'move' else ((-5,3,5,-2) if state == 'lunge' else (0,2,1,-1))
    low = 3 if state in ('warn','dodge') else (1 if state == 'stagger' else 0)
    p.poly([(-8,-11),(-11,-10),(-14,-11),(-14,-14),(-11,-14),(-9,-12),(-6,-13)],fur)
    p.poly([(-14,-11),(-14,-13),(-14,-15),(-13,-15),(-11,-14),(-12,-12)],'#98BF6E')
    p.line([(-13,-14),(-12,-14)],'#E6F2C0')
    for x, d, color in [(-6,hind_far,shade),(4,fore_far,shade),(-7,hind,fur),(5,fore,fur)]:
        p.poly([(x-1,-7),(x+2,-7),(x+d,-2),(x+d+2,-2),(x+d+2,0),(x+d-1,0),(x-2,-4)],color)
    p.poly([(-10,-11),(-7,-14),(-3,-15),(3,-14),(6,-12),(6,-7),(2,-5),(-6,-5),(-9,-7)],fur)
    p.poly([(-9,-9),(-6,-7),(-1,-7),(2,-9),(5,-10),(5,-7),(1,-6),(-6,-6)],shade,None)
    p.poly([(-8,-12),(-5,-13),(-1,-12),(2,-12),(1,-10),(-2,-9),(-4,-10),(-7,-10)],'#C4B29B',None)
    p.line([(-6,-10),(-5,-9),(-4,-9)],'#837665')
    p.line([(0,-8),(3,-9),(4,-10)],'#837665')
    for x, top in [(-8,-15),(-5,-17),(-2,-18),(1,-17),(4,-16)]:
        thorn = WARN if state == 'warn' else '#98BF6E'
        tip = WHITE if state == 'warn' else '#E6F2C0'
        p.poly([(x-1,top),(x+1,top+1),(x+2,top+5),(x,top+5)],thorn)
        p.rect((x,top+1,x,top+2),tip)
    p.poly([(3,-10+low),(5,-14+low),(6,-17+low),(8,-14+low),(10,-13+low),(11,-10+low),(14,-8+low),(14,-6+low),(11,-5+low),(7,-6+low),(5,-7+low)],fur)
    p.poly([(6,-10+low),(9,-8+low),(13,-8+low),(13,-6+low),(9,-6+low),(7,-7+low)],'#D2C3A8',None)
    p.rect((8,-12+low,10,-11+low),INK)
    p.rect((9,-11+low,10,-11+low),WHITE)
    p.line([(6,-15+low),(7,-13+low),(7,-12+low)],'#6A6258')
    p.rect((13,-8+low,14,-7+low),INK)
    if state == 'stagger':
        p.line([(10,-6+low),(13,-5+low)],INK)
        if i == 1:
            p.rect((11,-5+low,12,-4+low),'#C5A4A0')


def golem(p,state,i):
    stone, shade, moss = '#A9AEB8', '#5E6678', '#98BF6E'
    if state == 'death' and i >= 1:
        pebble(p,-17,0,12,7+i,stone,shade,True)
        pebble(p,-5,0,13,12-i*2,stone,shade)
        pebble(p,9,0,8,7,stone,shade,True)
        return
    tilt = i*2-1 if state == 'move' else 0
    p.shift(tilt)
    for j in (-1,1):
        foot = -1 if (i+j)%2 and state == 'move' else 0
        p.poly([(j*8-4,-9),(j*8+4,-9),(j*8+5,-1+foot),(j*8+3,foot),(j*8-4,foot)],shade)
        p.rect((j*8-3,-4+foot,j*8+3,-3+foot),stone)
    for j in (-1,1):
        x=j*14
        arm = -18
        if state == 'slam_warn': arm -= 12+i*5
        if state == 'slam': arm = -8
        if state == 'sweep_warn': arm += 4 if j < 0 else -8
        if state == 'sweep':
            x += 3
            arm += 4 if j < 0 else 8
        p.poly([(x-5,arm+1),(x-2,arm-4),(x+3,arm-4),(x+5,arm),(x+4,arm+8),(x-2,arm+9),(x-5,arm+5)],stone)
        p.rect((x-3,arm+4,x+3,arm+7),shade)
    p.poly([(-12,-8),(-12,-23),(-8,-31),(-5,-35),(6,-35),(10,-31),(12,-19),(14,-8),(9,-5),(-7,-5)],stone)
    p.poly([(-11,-9),(-9,-28),(-6,-31),(-6,-12),(10,-10),(12,-8),(9,-6),(-8,-6)],shade,None)
    p.poly([(-8,-29),(-5,-34),(6,-34),(8,-31),(5,-29),(-4,-28)],'#C4C9D0',None)
    p.poly([(-3,-28),(5,-29),(9,-31),(10,-27),(5,-26),(-3,-25)],'#808999',None)
    p.line([(-4,-33),(0,-32),(2,-33)],'#E0E1E0')
    p.poly([(-13,-22),(-8,-24),(-7,-20),(-11,-19),(-15,-19)],moss)
    p.poly([(8,-23),(12,-23),(15,-20),(14,-18),(9,-18)],moss)
    p.line([(-12,-22),(-12,-25),(-14,-26)],'#5E8F4A')
    p.line([(12,-22),(13,-26),(15,-27)],'#5E8F4A')
    if state == 'open' and i == 1:
        for x in (-1,1):
            p.poly([(x*2,-24),(x*11,-22),(x*12,-10),(x*7,-7),(x*3,-11)],stone)
        p.line([(-3,-13),(-1,-15),(0,-18),(2,-18)],CREAM,2)
    else:
        p.poly([(-10,-23),(1,-25),(10,-22),(12,-11),(8,-8),(-8,-8),(-10,-11)],'#BEC2CA')
        p.poly([(-9,-13),(0,-12),(9,-14),(11,-12),(8,-9),(-7,-9)],shade,None)
        p.line([(-8,-20),(-6,-22),(-1,-23)],'#DCE0E2')
        p.line([(7,-20),(9,-19),(9,-17)],'#919BA8')
        p.line([(-5,-12),(-6,-15),(-5,-19),(-2,-21),(3,-21),(6,-19),(6,-15),(4,-13),(0,-13),(-1,-15),(0,-17),(3,-17)],WARN if state in ('slam_warn','sweep_warn') else CREAM,2)
        if state in ('open','death'):
            p.line([(3,-24),(1,-19),(2,-15),(-2,-12),(-1,-8)],INK)
    if state == 'death':
        p.line([(-5,-34),(-4,-31),(-1,-30),(-2,-27),(1,-25)],INK)
        p.line([(10,-21),(13,-18),(11,-16),(12,-12)],INK)


def cap(p, x, y, w, h, pink=False, face=False, dark='#8A6F48'):
    q = max(2,w//6)
    p.poly([(x,y),(x+1,y-h//3),(x+q,y-h+4),(x+q*2,y-h+1),(x+w//2,y-h),(x+w-q,y-h+3),(x+w-2,y-h//2),(x+w,y-1),(x+w-3,y+1),(x+3,y+1)],'#CDB27E')
    if w>=16:
        p.poly([(x+q,y-4),(x+q*2,y-5),(x+w//2,y-4),(x+w-q,y-6),(x+w-2,y-3),(x+w-3,y-1),(x+q,y)],'#B09563',None)
    p.line([(x+2,y-1),(x+w-3,y-1)],WARN if pink else dark,2)
    if w >= 10:
        p.line([(x+3,y-h+3),(x+6,y-h+2),(x+w//2,y-h+2)],'#F2E6B8')
    if face:
        center=x+w//2
        p.rect((center-3,y-5,center-2,y-4),'#3A2E2A')
        p.rect((center+2,y-5,center+3,y-4),'#3A2E2A')
        p.line([(center-1,y-3),(center+1,y-3),(center+2,y-4)],'#3A2E2A')


def shaman(p,state,i):
    if state == 'death':
        p.shift(0,-1)
        p.poly([(-9,-1),(-7,-5),(-3,-4),(0,-1)],'#7A6248')
        cap(p,-1,-1,12,6+i,face=True)
        p.line([(-11,0),(-3,1),(1,0)],'#7A6248')
        return
    h = -1 if state == 'move' and i == 1 else 0
    p.shift(0,h)
    head = -15 if state == 'hide' else -19
    p.poly([(-3,head-1),(3,head-1),(3,-9),(5,-3),(2,-1),(-3,0),(-5,-2),(-3,-7)],'#EDE3C8')
    p.poly([(-3,-13),(3,-14),(3,-9),(5,-4),(0,-3),(-2,-5),(-4,-4)],'#8A6F48')
    p.line([(-2,-12),(-1,-10),(-2,-8)],'#C0A078')
    p.line([(2,-9),(2,-7),(4,-5)],'#6E5840')
    p.line([(-3,-12),(-6,-11),(-7,-9)],'#7A6248',2)
    p.line([(2,-11),(6,-10),(8,-12)],'#7A6248',2)
    p.line([(-2,-2),(-3 if i==0 else -4,0),(-5,0)],'#7A6248',2)
    p.line([(2,-2),(2 if i==0 else 4,0),(4,0)],'#7A6248',2)
    if state == 'fire':
        p.line([(7,-11),(12,-12),(14,-13)],INK,3)
        p.line([(7,-11),(12,-12),(14,-13)],'#7A6248')
    else:
        top = -24 if state == 'cast' else -15
        p.line([(8,-2),(8,-13),(10,top+2),(9,top)],INK,3)
        p.line([(8,-2),(8,-13),(10,top+2),(9,top)],'#7A6248')
    cap(p,-9,head,17,7, pink=state=='cast' and i==1, face=True)


def husk(p, x, y, w, h, lit=0):
    light = '#E9D98A' if lit < 2 else WARN
    shade = '#B8A560' if lit == 0 else WARN if lit == 1 else '#BD3471'
    p.poly([(x,y-2),(x-1,y-6),(x+1,y-h+4),(x+4,y-h+2),(x+w//2,y-h),(x+w-3,y-h+3),(x+w-1,y-h+5),(x+w,y-4),(x+w-2,y),(x+2,y)],light)
    p.poly([(x+2,y-3),(x+1,y-6),(x+2,y-8),(x+4,y-h+4),(x+w//2,y-h+2),(x+w//2,y-h+4),(x+3,y-5)],'#F7EAB3' if lit<2 else '#F67DB3',None)
    p.line([(x+2,y-1),(x+2,y-6),(x+4,y-h+4)],shade)
    p.line([(x+w//2,y-1),(x+w//2+1,y-5),(x+w//2,y-h+3)],shade)
    p.line([(x+w-2,y-2),(x+w-2,y-6),(x+w-4,y-h+4)],shade)
    if lit:
        p.rect((x+4,y-8,x+5,y-6),WHITE)


def beetle(p,state,i):
    p.shift(0, -1 if state == 'move' and i else 0)
    for n,x in enumerate((-7,-5,-2,0,3,5)):
        foot = (n+i)%2
        p.line([(x,-5),(x+foot,-3),(x+foot-1,-1+foot)],'#3A463E',2)
    p.poly([(-8,-5),(-7,-9),(-3,-11),(4,-10),(7,-7),(8,-4),(6,-2),(-5,-2)],'#8E9E7A')
    p.line([(-5,-4),(4,-4)],'#56664E',2)
    p.rect((6,-6,6,-5),CREAM)
    active = state == 'ignite'
    husk(p,-7-int(active),-6,11+int(active)*2,12+int(active)*2, i+1 if active else 0)


def hedgehog(p,state,i):
    if state == 'death' and i == 1:
        p.poly([(-9,-1),(-8,-5),(-5,-3),(-3,-6),(0,-4),(3,-5),(4,-2),(8,-2),(8,0),(-7,0)],'#93B87F')
        return
    low = 3 if state in ('recover','death') else 0
    p.poly([(-9,-2),(-10,-7+low),(-7,-11+low),(2,-12+low),(6,-9+low),(8,-6+low),(11,-4),(10,-2),(8,-1),(-6,0)],'#D9C7A3')
    p.line([(-8,-3),(1,-3),(4,-2)],'#997F65',2)
    p.rect((10,-4,11,-4),'#3A2E2A')
    if state != 'death':
        p.rect((6,-7+low,7,-7+low),WHITE)
        p.rect((-6+i*2,-1,-4+i*2,0),INK)
        p.rect((2-i*2,-1,4-i*2,0),INK)
    if state == 'recover':
        p.poly([(-9,-1),(-7,-3),(-4,-2),(0,-3),(3,-1),(2,0),(-6,0)],'#93B87F')
        return
    top = -16 if state != 'aim' else -19
    back = [(-11,-5+low),(-12,-10+low),(-9,-9+low),(-11,-14+low),(-7,-12+low),(-8,-16+low),(-4,-14+low)]
    if state != 'fire': back += [(-4,top+low),(-1,top+2+low),(0,top-1+low),(3,top+3+low)]
    back += [(5,-14+low),(6,-10+low),(3,-8+low),(2,-5+low),(-3,-4+low),(-7,-6+low),(-7,-4+low)]
    p.poly(back,'#93B87F')
    p.poly([(-10,-7+low),(-7,-7+low),(-5,-10+low),(-2,-8+low),(2,-11+low),(4,-10+low),(2,-6+low),(-4,-5+low),(-7,-6+low)],'#587A56',None)
    p.line([(-10,-12+low),(-7,-10+low),(-5,-10+low)],'#B5CFA2')
    p.line([(-6,-15+low),(-4,-12+low),(-2,-11+low)],'#B5CFA2')
    p.line([(0,-14+low),(2,-12+low),(3,-12+low)],'#B5CFA2')
    if state == 'aim' and i == 1:
        p.line([(0,top),(1,top+2),(2,top+3)],WARN,2)
        p.rect((0,top,0,top),WHITE)
    else:
        p.rect((-7,-14+low,-6,-14+low),'#E6F2C0')


def dirt(p,i):
    p.poly([(-8,0),(-8,-2),(-5,-4),(-3,-6-i),(3,-6-i),(5,-4),(8,-3),(8,0)],'#A8875A')
    p.line([(-7,-1),(-1,-2),(4,-1),(7,-2)],'#71583D',2)
    p.rect((-3,-4,-2,-4),'#C2A77E')


def flowerpot(p,x,y,plant='grass'):
    p.poly([(x-5,y),(x-6,y-3),(x-4,y-8),(x+3,y-8),(x+5,y-3),(x+6,y-1),(x+4,y+1),(x-4,y+1)],'#C9B8A0')
    p.line([(x-4,y-1),(x+4,y-1)],'#8A7A66',2)
    p.line([(x-4,y-5),(x-3,y-7),(x,y-7)],'#F2E0C9')
    p.line([(x+1,y-7),(x,y-5),(x+2,y-4),(x+1,y-1)],'#8A7A66')
    p.line([(x,y-8),(x,y-11),(x+2,y-13),(x+3,y-12)],'#98BF6E')
    if plant=='flower':
        p.poly([(x,y-13),(x,y-15),(x+2,y-15),(x+3,y-16),(x+5,y-15),(x+5,y-13),(x+3,y-12)],'#F4F1E6')


def weasel(p,state,i):
    if state == 'burrow':
        dirt(p,i)
        p.line([(0,-5-i),(0,-8-i),(2,-10-i),(3,-9-i)],'#98BF6E')
        return
    if state == 'rise' and i==0 or state == 'dive' and i==1:
        flowerpot(p,0,-4)
        dirt(p,0)
        return
    if state == 'death':
        if i==0:
            p.poly([(-10,-1),(-7,-2),(-4,-5),(3,-5),(7,-3),(9,-1),(4,0),(-6,0)],'#C09A78')
            p.line([(-4,-2),(2,-2),(5,-1)],'#E3C9A8')
            flowerpot(p,5,-2)
        else:
            flowerpot(p,0,-2)
        return
    head = -13 if state != 'dive' else -10
    pose = i if state == 'stand' else 0
    p.poly([(-3,-1),(-9,0),(-8,-2),(-4,-3),(-3,-8),(-3,head),(0,head-2),(4,head-2),(7,head+1),(6,head+3),(4,head+3),(3,-6),(4,-2),(6,-1),(5,0),(1,0),(0,-2),(-1,0),(-5,0)],'#C09A78')
    p.poly([(-1,-3),(0,-8),(0,head+2),(2,head+1),(5,head+1),(5,head+2),(3,head+3),(2,-3),(3,-2),(1,-1)],'#E3C9A8',None)
    p.line([(-2,-9),(0,-8),(1,-7)],'#7A5A44',2)
    p.rect((3-pose,head,3-pose,head),WHITE)
    p.rect((6,head+1,7,head+1),INK)
    flowerpot(p,-1-pose,head-1)
    if state in ('rise','dive'):
        dirt(p,0)


def bat(p,state,i,elite=False):
    wing,wingdark,fur='#A9B8E0','#56648C','#8496C8'
    p.shift(0,-6 if state not in ('death','dive') else -4)
    if state == 'death' and i==1:
        p.poly([(-7,0),(-7,-4),(-3,-7),(0,-6),(3,-7),(7,-4),(7,0)],wing)
        p.line([(-6,-2),(-2,-3),(2,-3),(6,-2)],wingdark,2)
        return
    for d in (-1,1):
        y=[-12,-9,-3][i] if state=='fly' else -1
        w=12 if state=='fly' else 6 if state=='death' else 9
        p.poly([(d*x,z) for x,z in [(2,-6),(w-4,y-2),(w,y-1),(w-1,y+4),(w-3,y+8),(w-5,y+6),(w-7,y+8),(3,-2)]],wing)
        p.line([(d*x,z) for x,z in [(3,-5),(w-4,y+1),(w-5,y+3)]],wingdark)
        p.line([(d*x,z) for x,z in [(w-3,y),(w-2,y+2),(w-3,y+4)]],'#CFD8EE')
        p.line([(d*x,z) for x,z in [(w-6,y+6),(w-4,y+5)]],'#8799C7')
        if elite:
            p.line([(d*(w-5),y+1),(d*(w-6),y+2),(d*(w-5),y+3)],WHITE)
        else:
            p.rect((d*(w-4),y+2,d*(w-4),y+2),WHITE)
    p.poly([(-4,-5),(-5,-10),(-4,-14),(-1,-11),(-1,-9),(2,-9),(3,-12),(5,-14),(6,-11),(5,-7),(4,-4),(3,-2),(1,0),(-2,0),(-4,-3)],fur)
    p.line([(-4,-11),(-3,-9)],wingdark)
    p.line([(4,-10),(4,-8)],wingdark)
    p.rect((-2,-6,-1,-6),WHITE if state!='death' else INK)
    p.rect((2,-6,3,-6),WHITE if state!='death' else INK)
    if state == 'fly' and not elite:
        p.rect((7,[ -10,-5,2][i],7,[-10,-5,2][i]),WHITE)


def flame(p, x, y, tail=0, width=0, danger=False, eyes=True):
    w=7+width
    p.poly([(x-w,y-6),(x-w,y-12),(x-w+3,y-15),(x-w+2,y-10),(x-2,y-14),(x,y-18),(x+2,y-15),(x+3,y-11),(x+w-1,y-14),(x+w,y-9),(x+w,y-5),(x+w-3,y-1),(x+2,y+1),(x+tail,y+5),(x+tail+2,y+7),(x+tail-2,y+6),(x+tail-3,y+2),(x-3,y-1)],'#FFF0A8',WARN if danger else INK)
    p.poly([(x-w+1,y-7),(x-w+3,y-5),(x-2,y-4),(x+4,y-5),(x+w-1,y-8),(x+w-1,y-5),(x+4,y-1),(x+tail,y+3),(x+tail-2,y+2),(x-4,y-1)],'#D8C070',None)
    p.poly([(x-4,y-8),(x-2,y-11),(x+2,y-11),(x+4,y-8),(x+4,y-5),(x+1,y-3),(x-3,y-4),(x-5,y-6)],'#7F8FA8')
    p.line([(x-3,y-5),(x+2,y-5)],'#4E5A72')
    p.line([(x-3,y-9),(x-2,y-10),(x+1,y-10)],'#A8B4C7')
    if eyes:
        p.rect((x-2,y-8,x-1,y-8),WHITE)
        p.rect((x+1,y-8,x+2,y-8),WHITE)


def wisp(p,state,i):
    p.shift(0,-7)
    if state == 'blink' and i==1:
        p.rect((-1,-8,0,-7),'#FFF0A8')
        return
    if state == 'death':
        p.shift(0,-1)
        pebble(p,-4,0,6,4,'#7F8FA8','#4E5A72')
        if i==1: pebble(p,4,0,3,3,'#7F8FA8','#4E5A72')
        else: p.rect((-1,-4,0,-4),CREAM)
        return
    if state == 'twin':
        flame(p,-5,-4,i-1,-2)
        flame(p,5,0,1-i,-2)
        return
    flame(p,0,-2,i-1, (i+1 if state=='cast' else -2 if state=='blink' else 0),state=='cast')


def thorn_vine(p,state,i):
    green, shade, tip = '#98BF6E','#5E8F4A','#F2E38C'
    if state=='death' and i==1:
        p.poly([(-14,-3),(-11,-5),(-7,-4),(-6,-2),(-1,-2),(1,-4),(5,-4),(8,-2),(10,-4),(12,-3),(13,-1),(4,0),(-6,0)],shade)
        p.poly([(8,-2),(9,-7),(12,-7),(14,-4),(13,-2),(12,0)],'#EDE3C8')
        return
    height = (-12 - i*13) if state=='emerge' else -17 if state=='death' else -33
    sway = -3 if state=='death' or state=='idle' and i==0 else 2 if state=='idle' else 0
    p.poly([(-11,0),(-7,-4),(-5,-4),(-4,-8),(-4,height+6),(-2+sway,height+1),(3+sway,height+1),(4,height+8),(3,-5),(7,-3),(11,0),(4,0),(0,-2),(-4,0)],green)
    p.line([(2+sway,height+5),(2,-9),(1,-4),(7,-1)],shade,2)
    p.line([(-2+sway,height+7),(-2,height+13),(-2,-14),(-1,-9)],'#BBD793',2)
    if height < -20:
        for d in (-1,1):
            p.poly([(d*x,y) for x,y in [(2,-13),(6,-15),(8,-19),(8,-24),(10,-25),(10,-21),(13,-23),(12,-19),(14,-17),(11,-17),(8,-13),(4,-11)]],green)
            p.rect((d*10,-24,d*10,-24),tip)
            p.rect((d*12,-22,d*12,-22),tip)
    x=sway
    if state=='cast':
        p.poly([(x-9,height-8),(x-6,height-8),(x-3,height-5),(x,height-6-i),(x+3,height-5),(x+6,height-9),(x+9,height-10),(x+8,height-4),(x+5,height),(x,height+2),(x-5,height),(x-8,height-4)],'#EDE3C8')
        p.poly([(x-5,height-5),(x,height-7-i),(x+5,height-5),(x+3,height-2),(x-2,height-2)],WARN,INK)
        p.rect((x-1,height-5-i,x+1,height-5-i),WHITE)
    else:
        p.poly([(x-7,height-5),(x-4,height-9),(x-1,height-10),(x+1,height-8),(x+4,height-9),(x+6,height-5),(x+5,height-1),(x+1,height+2),(x-4,height+1),(x-6,height-2)],'#EDE3C8')
        p.line([(x+4,height-5),(x+3,height-2),(x,height)],'#C3B994',2)


def boar(p,state,i):
    fur,shade,mane,tusk='#B08A6E','#6E5444','#5E5A66','#F0E6C8'
    if state=='death':
        h = -9 + i*3
        p.poly([(-19,-2),(-14,h+1),(-6,h),(2,h-2),(11,h),(16,-3),(19,-1),(12,0),(-16,0)],fur)
        p.poly([(-16,-1),(-11,-4),(2,-3),(8,-4),(16,-1),(11,-1),(-15,-1)],shade,None)
        p.line([(12,-4),(15,-4)],INK)
        p.poly([(16,-3),(19,-5),(19,-2),(17,0),(15,-1)],tusk)
        return
    h = -3 if state=='charge' and i==1 else 0
    p.shift(0,h)
    steps = [(-2,2,3,-2),(0,-2,1,3),(3,-1,-2,2),(-1,3,1,-1)]
    legs=steps[i] if state=='move' else ((-5,4,5,-3) if i==1 else (3,-2,-3,3)) if state=='charge' else (0,-2,2,-1)
    p.line([(-16,-12),(-19,-12),(-20,-15),(-18,-16)],INK,2)
    p.line([(-16,-12),(-18,-13),(-18,-15)],fur)
    for n,(x,dx) in enumerate(zip((-11,7,-8,11),legs)):
        fy = -3 if state=='warn' and n==3 and i==0 else 0
        p.poly([(x-2,-8),(x+2,-8),(x+dx+1,-3+fy),(x+dx+3,-1+fy),(x+dx+2,fy),(x+dx-2,fy),(x-3,-4)],shade if n<2 else fur)
    p.poly([(-17,-11),(-14,-15),(-9,-17),(-2,-19),(4,-22),(11,-22),(16,-17),(17,-11),(15,-6),(10,-4),(1,-5),(-9,-6),(-16,-8)],fur)
    p.poly([(-16,-10),(-10,-11),(-3,-9),(5,-10),(14,-12),(15,-9),(12,-6),(8,-5),(-9,-7),(-14,-8)],shade,None)
    p.poly([(-14,-13),(-10,-16),(-3,-17),(2,-19),(8,-20),(10,-19),(6,-16),(0,-15),(-5,-13),(-8,-14),(-11,-12)],'#C8A78C',None)
    p.line([(-9,-13),(-7,-12),(-5,-13)],'#8B6954')
    p.line([(1,-14),(3,-13),(5,-14)],'#8B6954')
    p.poly([(-10,-17),(-4,-19),(-3,-21),(0,-22),(2,-21),(4,-24),(7,-22),(9,-24),(12,-22),(11,-20),(7,-19),(3,-20)],mane)
    low=3 if state=='warn' or state=='stagger' and i==0 else -1 if state=='stagger' else 0
    p.poly([(7,-12+low),(8,-17+low),(10,-20+low),(13,-16+low),(15,-15+low),(17,-13+low),(20,-12+low),(20,-8+low),(17,-7+low),(12,-8+low)],fur)
    p.poly([(11,-12+low),(14,-10+low),(19,-10+low),(19,-8+low),(14,-8+low)],'#D0B79E',None)
    p.rect((14,-14+low,15,-14+low),CREAM)
    p.rect((19,-11+low,20,-10+low),INK)
    color=WARN if state=='warn' and i==1 else tusk
    p.poly([(15,-8+low),(18,-9+low),(18,-13+low),(20,-11+low),(20,-8+low),(18,-6+low),(15,-6+low)],color)
    p.poly([(11,-8+low),(13,-8+low),(14,-11+low),(15,-9+low),(14,-6+low),(12,-5+low)],color)
    if color==WARN: p.rect((19,-10+low,19,-9+low),WHITE)


def toad(p,state,i,mini=False):
    if state=='death' and i==1:
        p.poly([(-11,-1),(-8,-5),(-3,-6),(2,-5),(5,-6),(10,-3),(11,-1),(8,0),(-8,0)],'#A9B86A')
        p.line([(4,-3),(7,-3)],INK)
        return
    y=-3 if state=='hop' and i==1 else 1 if state=='death' else 0
    base=-3 if mini else 0
    p.shift(base,y)
    w=9 if mini else 12
    h=12 if mini else 19
    p.poly([(-w,-5),(-w+2,-11),(-w+4,-h+3),(-5,-h+1),(0,-h),(5,-h+1),(w-2,-h+5),(w,-8),(w-1,-4),(w-3,-1),(4,0),(-5,0),(-w+2,-2)],'#A9B86A')
    p.poly([(-w+2,-4),(-w+4,-9),(-5,-11),(-1,-10),(2,-4),(-2,-1),(-7,-1)],'#6A7A44',None)
    p.poly([(-2,-5),(1,-10),(6,-11),(w-1,-9),(w-1,-5),(w-4,-1),(1,-1)],'#E6D9A8')
    p.rect((-w+3,-h+6,-w+5,-h+7),'#7A8A4E')
    p.rect((-w+5,-h+4,-w+7,-h+5),'#7A8A4E')
    p.rect((-w+5,-h+5,-w+6,-h+5),'#C5D095')
    p.line([(-w+2,-h+8),(-w+3,-h+10),(-w+4,-h+9)],'#84964D')
    p.line([(0,-7),(2,-8),(4,-9)],'#F1E8CB')
    p.poly([(2,-h+6),(3,-h+2),(5,-h),(8,-h+1),(w-1,-h+4),(w-1,-h+6),(7,-h+7)],'#A9B86A')
    p.rect((5,-h+3,8,-h+3),CREAM if state!='death' else INK)
    p.line([(6,-h+7),(9,-h+7),(w,-h+6)],INK)
    if state=='call':
        spread=[1,2,2][i]
        p.poly([(2,-h+8),(6,-h+6),(w,-h+8),(w+spread,-h+11),(w+spread-1,-h+14),(w-3,-h+17),(3,-h+15),(1,-h+11)],'#F4F1E6')
        p.line([(5,-h+14),(w-2,-h+14),(w+spread-2,-h+12)],'#D3C8AA',2)
        if i==2: p.line([(-2,-4),(3,-8),(6,-8)],'#6A7A44',2)
    if state in ('tongue_warn','tongue'):
        p.poly([(6,-h+7),(w,-h+7),(w+1,-h+9),(8,-h+11),(6,-h+10)],INK)
        if state=='tongue':
            p.poly([(8,-h+8),(18,-h+8),(19,-h+7),(20,-h+6),(22,-h+6),(22,-h+4),(20,-h+3),(18,-h+5),(8,-h+10)],WARN)
            p.rect((20,-h+5,21,-h+5),WHITE)
        else:
            p.rect((8,-h+9,10,-h+9),WARN)
            p.rect((10,-h+9,10,-h+9),WHITE)


REGULAR = [
    ('enemy.slime_moss',(64,64),moss_slime,{'move':2,'death':2}),
    ('enemy.wolf_bramble',(64,64),wolf,{'move':4,'warn':1,'lunge':1,'stagger':2,'dodge':1,'death':2}),
    ('enemy.golem_menhir',(96,96),golem,{'move':2,'slam_warn':2,'slam':1,'sweep_warn':1,'sweep':1,'open':2,'death':3}),
    ('enemy.shaman_cap',(64,64),shaman,{'move':2,'cast':2,'fire':1,'hide':1,'death':2}),
    ('enemy.bug_lantern',(64,64),beetle,{'move':2,'ignite':2}),
    ('enemy.hog_needle',(64,64),hedgehog,{'move':2,'aim':2,'fire':1,'recover':1,'death':2}),
    ('enemy.weasel_pot',(64,64),weasel,{'burrow':2,'rise':2,'stand':2,'dive':2,'death':2}),
    ('enemy.bat_dusk',(64,64),bat,{'fly':3,'death':2}),
    ('enemy.wisp_stray',(64,64),wisp,{'hover':3,'cast':2,'blink':2,'death':2}),
    ('enemy.vine_thorn',(64,96),thorn_vine,{'idle':2,'cast':2,'emerge':2,'death':2}),
    ('enemy.boar_tusk',(96,64),boar,{'move':4,'warn':2,'charge':2,'stagger':2,'death':2}),
    ('enemy.toad_drum',(64,64),toad,{'hop':2,'call':3,'tongue_warn':1,'tongue':1,'death':2}),
]


def render(entries):
    manifest={}
    cards={}
    for asset_id,size,draw,clips in entries:
        width,height=size
        cols=min(6,sum(clips.values()))
        rows=(sum(clips.values())+cols-1)//cols
        atlas=Image.new('RGBA',(cols*width,rows*height))
        data={'path':f'{asset_id}.png','size':[width,height],'grid':[cols,rows],'clips':{}}
        for state,count in clips.items():
            rects=[]
            for i in range(count):
                if state == 'tongue' and draw is toad:
                    p=Pixels(size,(9,height//2-3))
                else:
                    p=Pixels(size)
                draw(p,state,i)
                frame=finish(p.png(),asset_id)
                if 'origin' not in data:
                    data['origin']=list(p.origin)
                x=len(data.get('_frames',[]))%cols*width
                y=len(data.get('_frames',[]))//cols*height
                if frame.getbbox() is None:
                    raise ValueError((asset_id,state,i,'empty'))
                if (state,i) == PREVIEW_POSES.get(asset_id,(next(iter(clips)),0)):
                    cards[asset_id]=frame
                atlas.paste(frame,(x,y))
                data.setdefault('_frames',[]).append((x,y,width,height))
                rects.append({'rect':[x,y,width,height],'origin':list(p.origin)})
            data['clips'][state]=rects
        del data['_frames']
        atlas.save(OUT/data['path'],optimize=True)
        manifest[asset_id]=data
    return manifest,cards


def preview(name,cards,cols=4):
    cell_w=max(frame.width for frame in cards.values())+32
    cell_h=max(frame.height for frame in cards.values())+34
    rows=(len(cards)+cols-1)//cols
    image=Image.new('RGB',(cols*cell_w+24,rows*cell_h+24),'#161F3A')
    d=ImageDraw.Draw(image)
    font=ImageFont.load_default(11)
    for i,(asset_id,sprite) in enumerate(cards.items()):
        col,row=i%cols,i//cols
        x=col*cell_w+12
        y=row*cell_h+12
        if (row+col)%2:
            d.rectangle((x,y,x+cell_w-1,y+cell_h-1),fill='#202A42')
        image.paste(sprite,(x+(cell_w-sprite.width)//2,y+(cell_h-sprite.height)//2-6),sprite)
        d.text((x+cell_w//2,y+cell_h-14),asset_id.split('.',1)[-1],anchor='mm',font=font,fill='#EAF2FF')
    image.resize((image.width*2,image.height*2),Image.Resampling.NEAREST).save(OUT/f'{name}.png')


def kiln_boar(p,state,i):
    fur,fur_dark='#A68A78','#6A5448'
    stone,stone_dark='#8E8A96','#5A5666'
    metal='#C8D0E0'
    if state=='death' and i>=2:
        pebble(p,-34,0,18,13-i,stone,stone_dark)
        pebble(p,-14,0,24,18-i*2,stone,stone_dark)
        pebble(p,13,0,19,12-i,stone,stone_dark)
        p.line([(-22,-3),(10,-12),(25,-13)],metal,2)
        p.line([(-15,-9),(-25,-14),(-26,-18)],metal,2)
        p.line([(-16,-9),(-22,-17),(-22,-20)],metal,2)
        return
    x = -4 if state=='stun' else 1 if state=='charge' else 0
    y = (-1,2,0)[i] if state=='stomp' else -1 if state=='charge' and i==0 else 1 if state=='stun' else 2+i*2 if state=='death' else 0
    p.shift(x,y)
    lower=7+i*6 if state=='death' else 6 if state in ('charge_warn','charge','stun') else -6 if state=='roar' or state=='stomp' and i==0 else 0
    foot = [[-4,4,4,-2],[-2,0,2,3],[2,-3,-2,4],[5,-4,-5,2]][i] if state=='walk' else ([5,-4,7,-5] if i==1 else [-5,6,-4,5]) if state=='charge' else [0,0,0,0]
    for j,(fx,dx) in enumerate(zip((-24,10,-20,16),foot)):
        toe = -11 if state=='stomp' and j%2 and i==0 else -2 if state=='charge' else 0
        p.poly([(fx-5,-21),(fx+5,-21),(fx+7,-13),(fx+dx+4,toe-5),(fx+dx+6,toe-2),(fx+dx+5,toe),(fx+dx-5,toe),(fx+dx-6,toe-4),(fx-7,-10)],fur_dark if j<2 else fur)
        p.line([(fx+dx-4,toe-2),(fx+dx+4,toe-2)],stone_dark,2)
    p.poly([(-36,-15),(-38,-24),(-34,-34),(-26,-41),(-15,-45),(4,-46),(19,-43),(29,-35),(31,-27),(27,-18),(20,-12),(8,-9),(-22,-10)],stone)
    p.poly([(-36,-22),(-34,-27),(-26,-24),(-12,-20),(9,-19),(25,-23),(28,-24),(25,-18),(19,-13),(8,-11),(-20,-11),(-33,-15)],stone_dark,None)
    p.line([(-30,-34),(-25,-39),(-17,-42),(-5,-43)],'#B4B0BD',3)
    p.line([(-25,-30),(-16,-27),(-8,-28),(0,-27)],stone_dark,2)
    p.line([(-31,-21),(-24,-18),(-18,-18)],'#B4B0BD')
    for (x,z) in [(-24,-37),(-24,-26),(-22,-16),(7,-42),(19,-38),(21,-25)]:
        p.line([(x-4,z),(x+2,z),(x+4,z-2)],stone_dark,2)
        p.line([(x-2,z-1),(x+1,z-1)],'#B4B0BD')
    glow='#FFF2A0' if state=='phase' and i==0 else '#FFF3CF' if state=='phase' else '#FFE9A8'
    p.poly([(-13,-29),(-13,-35),(-9,-39),(-3,-41),(5,-40),(10,-36),(11,-30),(8,-27),(-10,-27)],INK)
    p.poly([(-10,-31),(-9,-35),(-5,-38),(3,-38),(8,-34),(8,-30),(3,-29),(-8,-29)],glow,stone_dark)
    p.poly([(-6,-32),(-3,-35),(0,-33),(3,-35),(6,-32),(5,-30),(-6,-30)],'#C7A96E',None)
    for sx,sy,ex,ey in [(-16,-40,-23,-57),(4,-43,15,-56),(12,-42,27,-52)]:
        p.line([(sx,sy),(ex,ey)],INK,4)
        p.line([(sx,sy),(ex,ey)],metal,2)
    p.line([(-23,-57),(-25,-59),(-25,-60)],metal,2)
    p.line([(-22,-56),(-21,-58),(-22,-60)],metal,2)
    p.poly([(11,-24+lower),(15,-31+lower),(17,-36+lower),(23,-33+lower),(25,-29+lower),(33,-27+lower),(36,-24+lower),(40,-23+lower),(41,-18+lower),(38,-15+lower),(26,-15+lower),(21,-17+lower),(14,-17+lower)],fur)
    p.poly([(21,-27+lower),(26,-24+lower),(34,-23+lower),(39,-21+lower),(37,-16+lower),(28,-16+lower),(22,-18+lower)],'#CDB29E',None)
    p.rect((29,-25+lower,31,-24+lower),CREAM)
    p.rect((39,-22+lower,40,-20+lower),INK)
    tusk=WARN if state=='charge_warn' and i==1 else '#F0E6C8'
    p.poly([(25,-17+lower),(29,-16+lower),(31,-20+lower),(33,-19+lower),(32,-15+lower),(29,-13+lower),(26,-14+lower)],tusk)
    p.poly([(35,-16+lower),(38,-16+lower),(40,-20+lower),(42,-19+lower),(41,-16+lower),(38,-14+lower),(35,-14+lower)],tusk)
    if state in ('roar','stun'):
        p.line([(26,-17+lower),(34,-17+lower),(37,-18+lower)],INK,3 if i else 2)
    if state=='death':
        p.line([(-25,-40),(-20,-35),(-24,-31),(-18,-27),(-23,-20)],INK,2)


def phantom(p,state,i):
    silver,shadow,light='#AEB6C2','#5E6678','#F4F1E6'
    if state=='death' and i>=2:
        pieces=[(-11,-19,2),(-5,-23,3),(3,-22,3),(8,-17,2),(-2,-16,2),(-5,-12,1)] if i==2 else [(-4,-14,2),(3,-11,1),(7,-14,1)]
        for x,y,n in pieces:
            p.poly([(x-n,y),(x,y-n),(x+n,y),(x,y+n)],'#DDE3EA',None)
        p.poly([(-9+i,-18+i),(-4,-20+i),(1,-19+i),(7-i,-20+i),(10-i,-17+i),(5,-16+i),(-5,-16+i)],silver,None)
        return
    y=[-2,-1,0][i] if state=='hover' else 2 if state in ('open','death') else 0
    p.shift((i*4-2) if state=='open' else 0,y)
    head=3 if state=='look' or state=='open' else 0
    r = 10 if state in ('cast','burst','phase') else 7
    l = 10 if state in ('cast','burst','phase') else 6
    dy = (-6-i*3) if state in ('cast','burst') else 1 if state=='open' else 0
    right_y= -23 if state=='fire' and i==0 else -27 if state=='fire' else -16+dy
    right_x=14 if state=='fire' else r+3
    p.poly([(-4,-31),(-8,-27),(-10,-20),(-8,-17),(-10,-13),(-13,-9),(-15,-7),(-10,-7),(-8,-6),(-4,-8),(-2,-5),(2,-6),(4,-8),(9,-7),(13,-9),(9,-11),(8,-16),(10,-20),(8,-27),(4,-31)],silver)
    p.poly([(-9,-18),(-7,-25),(-2,-30),(2,-31),(5,-27),(2,-23),(3,-18),(0,-16),(2,-11),(-1,-8),(-5,-9),(-8,-7),(-10,-7),(-12,-8),(-8,-12)],shadow,None)
    p.poly([(-9,-19),(-l-4,-16+dy),(-l-3,-12+dy),(-7,-13),(-5,-17),(-6,-22)],silver)
    p.line([(-l-2,-14+dy),(-8,-14),(-6,-16)],shadow,2)
    p.poly([(6,-24),(10,-22),(right_x,right_y),(right_x+1,right_y+3),(right_x-2,right_y+5),(8,-16),(6,-17)],silver)
    p.line([(8,-19),(right_x-1,right_y+3)],shadow,2)
    p.rect((right_x,right_y+1,right_x+1,right_y+2),light)
    p.poly([(-6,-9),(-3,-10),(0,-8),(3,-9),(9,-9),(12,-7),(7,-6),(5,-5),(1,-6),(-2,-4),(-7,-5),(-9,-6),(-14,-6),(-10,-8)],'#DDE3EA',None)
    p.poly([(-6,-37+head),(-3,-41+head),(3,-41+head),(6,-39+head),(8,-35+head),(8,-29+head),(5,-25+head),(1,-24+head),(-4,-25+head),(-7,-29+head),(-8,-33+head)],'#8B98A8')
    glass = WHITE if state=='phase' and i==1 else light
    p.poly([(-5,-35+head),(-2,-39+head),(3,-39+head),(5,-37+head),(6,-34+head),(6,-30+head),(3,-27+head),(-2,-27+head),(-5,-29+head)],glass,shadow)
    p.line([(-3,-31+head),(2,-36+head),(4,-36+head)],'#B8C7D8',2)
    p.line([(-6,-34+head),(-5,-38+head),(-3,-40+head)],'#E3EAF1')
    p.line([(-11,-11),(-7,-14),(-5,-14)],'#8090A4')
    p.line([(6,-11),(8,-10),(9,-9)],'#E0E5EE')
    if state in ('death','crack') and not (state=='crack' and i==1):
        p.line([(-5,-36+head),(-1,-34+head),(0,-30+head),(4,-28+head)],INK)
        p.line([(0,-30+head),(-3,-28+head)],INK)
        if state=='death' and i==1:
            p.poly([(5,-37),(10,-39),(13,-36),(10,-32),(8,-33)],light)


def queen(p,state,i):
    stem,root,light='#EDE3C8','#8A6F48','#F2E6B8'
    if state=='death' and i>=2:
        for x,y,w,h in [(-21,-1,13,8),(-6,-2,20,10-i), (10,0,13,7)]:
            cap(p,x,y,w,h)
        p.line([(-5,1),(6,0),(14,2),(23,1)],root,2)
        return
    y=-6-i*4 if state=='jump' and i in (1,2) else 3 if state=='death' else 0
    p.shift(0,y)
    crown=-50
    if state=='ring': crown-=i*3+1
    if state=='jump': crown += (8,4,1,12)[i]
    if state=='call': crown+=3+i*2
    if state=='open': crown+=5
    if state=='death': crown+=8+i*6
    if state=='phase': crown-=i
    for n, x in enumerate((-21,-11,1,10,20)):
        kick= (n+i)%2*3 if state=='walk' else 3 if state=='phase' else (n+i)%3*2 if state=='jump' else 0
        p.poly([(x//2-4,crown+25),(x//2+4,crown+25),(x+2,-10-kick),(x+6,-4-kick),(x+6,-1-kick),(x+1,-2-kick),(x-4,-3-kick),(x-5,-10)],root)
        p.line([(x,-13),(x+2,-7-kick),(x+4,-4-kick)],'#CDB27E',2)
    p.poly([(-11,crown+5),(-13,crown+23),(-12,-20),(-16,-13),(-10,-9),(1,-12),(12,-9),(15,-14),(11,-20),(12,crown+19),(10,crown+4)],stem)
    p.poly([(-10,crown+15),(-7,crown+24),(-7,-21),(-10,-13),(-5,-12),(-3,-18),(-4,crown+24),(-3,crown+16)],'#C3A982',None)
    p.line([(7,crown+12),(6,crown+21),(7,-17),(10,-14)],'#C3A982',2)
    cap(p,-21,crown+18,41,12, pink=state=='cloud' and i==1)
    cap(p,-18,crown+9,36,13, pink=state=='ring' and i==2)
    skew=(-6 if i==0 else 6) if state=='open' else -5 if state=='death' and i==1 else 0
    cap(p,-25+skew,crown,50,17, pink=state=='ring' and i==2)
    for cx,cy in [(-11,-6),(9,-6),(-3,-2)]:
        p.line([(cx+skew,crown+cy+2),(cx+skew,crown+cy-1)],stem,2)
        cap(p,cx-4+skew,crown+cy-1,8,4)
    if state=='death':
        p.line([(-13+skew,crown-12),(-10+skew,crown-9),(-14+skew,crown-5),(-14+skew,crown+1)],root,2)


def owl(p,state,i):
    blue,shadow,body,tip='#8E9CC4','#56648C','#C8D0E0','#F4F1E6'
    ground=state=='land'
    p.shift(0,-2 if ground else -7)
    if state=='death' and i==3:
        p.poly([(-18,1),(-16,-7),(-10,-10),(0,-8),(10,-11),(15,-7),(18,1),(11,3),(-10,3)],blue)
        p.line([(-12,-2),(-5,-5),(6,-3),(12,-6)],shadow,3)
        return
    flap=[-36,-25,-8,-16][i] if state=='fly' else -20+i*5 if state=='dive' else -25 if state=='phase' else -12
    spread=41 if state not in ('death','land') else 31-i*5 if state=='death' else 18-i*3
    if state=='fan': spread=[26,31,43][i]
    if state=='phase': flap=-21-i*12; spread=39+i*4
    if state=='weather': flap=[-44,-9][i]
    for d in (-1,1):
        f=flap+(2 if d==1 else 0)
        w=spread
        shape=[(8,-20),(15,-29),(w-10,f-6),(w,f-7),(w-3,f+2),(w,f+5),(w-5,f+7),(w-8,f+7),(w-5,f+12),(w-10,f+14),(w-14,f+12),(w-15,f+17),(w-20,f+16),(w-22,f+13),(12,-10),(7,-10)]
        if ground:
            shape=[(7,-25),(14,-28),(w,-23),(w,-14),(w-3,-8),(w-5,-10),(w-8,-6),(9,-8),(5,-13)]
        p.poly([(d*x,y) for x,y in shape],blue)
        if not ground:
            p.poly([(d*x,y) for x,y in [(w-11,f-4),(w-2,f-5),(w-6,f+2),(w-3,f+4),(w-8,f+5),(w-12,f+3),(w-9,f+7),(w-8,f+11),(w-11,f+12),(w-14,f+10),(w-16,f+13),(w-17,f+10),(w-15,f+3)]],tip,None)
            p.line([(d*x,y) for x,y in [(14,-22),(20,-24),(22,-27),(27,-27),(29,-24)]],shadow,3)
            p.line([(d*x,y) for x,y in [(16,-22),(20,-22),(23,-24),(28,-23)]],body,2)
        else:
            p.line([(d*x,y) for x,y in [(9,-11),(w-5,-12),(w-3,-17),(w-2,-21)]],tip,2)
    h=1 if state=='dive' else 4 if state=='death' and i==2 else 0
    p.poly([(-12,-28+h),(-13,-13+h),(-9,-6+h),(-4,-4+h),(7,-4+h),(11,-8+h),(14,-15+h),(13,-28+h),(7,-34+h),(-7,-34+h)],body)
    p.line([(-10,-15+h),(-6,-12+h),(-2,-13+h)],blue,2)
    for x,y in [(-7,-21),(0,-20),(6,-19),(-4,-14),(3,-13)]:
        p.line([(x-2,y+h),(x,y+2+h),(x+2,y+1+h)],'#AAB6CA')
    p.line([(5,-10+h),(9,-13+h),(11,-16+h)],blue,2)
    p.poly([(-13,-28+h),(-13,-37+h),(-15,-44+h),(-11,-43+h),(-7,-40+h),(-3,-41+h),(7,-40+h),(13,-43+h),(15,-45+h),(15,-39+h),(14,-37+h),(16,-30+h),(13,-24+h),(7,-21+h),(-5,-21+h),(-11,-24+h)],blue)
    for x in (-7,7):
        p.poly([(x-6,-35+h),(x-2,-38+h),(x+3,-37+h),(x+5,-34+h),(x+4,-29+h),(x+1,-26+h),(x-4,-27+h),(x-6,-31+h)],body)
        if state=='death':
            p.line([(x-3,-31+h),(x+2,-31+h)],INK,2)
        else:
            p.poly([(x-4,-34+h),(x+1,-35+h),(x+3,-32+h),(x+1,-29+h),(x-3,-29+h)],CREAM)
            p.rect((x,-33+h,x+1,-31+h),INK)
    p.poly([(-2,-30+h),(0,-32+h),(3,-30+h),(1,-26+h)],'#3A2E2A')
    for x in (-6,6):
        p.poly([(x-3,-8+h),(x+2,-8+h),(x+2,-5+h),(x+5,-3+h),(x+4,0+h),(x+1,-2+h),(x-1,0+h),(x-3,-3+h)],'#A68F65')


def frost_goat(p,state,i):
    fur,shade,horn,hoof='#D8E4EC','#7C93A8','#8FA9C4','#56648C'
    if state=='death' and i>=2:
        p.poly([(-27,-4),(-21,-12),(-10,-14),(-3,-12),(5,-13),(9,-11),(19,-12),(27,-7),(25,-3),(15,-2),(7,-4),(-12,0),(-21,-1)],fur)
        p.poly([(-21,-4),(-12,-9),(0,-9),(11,-7),(23,-5),(20,-4),(6,-5),(-13,-1)],shade,None)
        p.line([(16,-8),(20,-8)],INK,2)
        p.poly([(9,-11),(1,-17),(3,-20),(11,-18),(15,-15),(15,-12)],horn)
        if i==2:
            p.poly([(5,-12),(-3,-15),(-10,-21),(-6,-22),(2,-18),(8,-16)],horn)
        else:
            p.poly([(-7,-14),(-12,-16),(-14,-15),(-12,-13),(-8,-12)],horn)
        return
    low = 7 if state in ('charge_warn','charge','impact','death') else -5 if state in ('pillars','breath') else 0
    if state=='sweep': low=[-2,6,2][i]
    if state=='pillars': low-=i*4
    if state=='impact': low+=i*5
    if state=='death': low+=i*6
    p.shift(0,-5 if state=='charge' and i==1 else i*2 if state=='death' else 0)
    steps=[(-3,3,4,-2),(-2,2,1,-3),(3,-2,-4,3),(0,4,3,-2)]
    foot = steps[i] if state=='walk' else [-7,6,6,-6] if state=='charge' and i==1 else [2,5,-1,-1] if state=='charge' else [0,0,0,0]
    for n,(x,dx) in enumerate(zip((-16,8,-12,13),foot)):
        y=-8 if state=='charge_warn' and n==3 and i==1 else -3 if state=='death' and n%2 else 0
        p.poly([(x-4,-23),(x+4,-23),(x+3,-10),(x+dx+4,y-5),(x+dx+4,y-1),(x+dx-2,y),(x+dx-5,y-3),(x-5,-12)],shade if n<2 else fur)
        p.poly([(x+dx-4,y-4),(x+dx+3,y-4),(x+dx+3,y-1),(x+dx-2,y),(x+dx-4,y-2)],hoof)
    p.poly([(-27,-24),(-24,-31),(-16,-34),(-9,-35),(-2,-34),(5,-39),(13,-41),(20,-35),(22,-28),(19,-21),(16,-17),(12,-13),(6,-13),(0,-15),(-10,-16),(-20,-17),(-25,-20)],fur)
    p.poly([(-25,-24),(-21,-22),(-11,-23),(0,-22),(7,-24),(10,-28),(14,-26),(19,-27),(18,-22),(14,-18),(11,-15),(6,-15),(0,-17),(-8,-17),(-21,-19)],shade,None)
    p.poly([(-22,-29),(-15,-32),(-7,-32),(-1,-30),(-7,-28),(-14,-29),(-20,-27)],'#EDF3F5',None)
    p.poly([(3,-37),(10,-45+low//2),(15,-44+low//2),(18,-39),(20,-34),(18,-29),(15,-25),(11,-27),(9,-24),(6,-29),(3,-29),(4,-32)],'#EDF3F5')
    p.poly([(4,-39+low),(5,-46+low),(0,-50+low),(-6,-51+low),(-12,-53+low),(-19,-59+low),(-17,-61+low),(-11,-58+low),(-2,-56+low),(7,-55+low),(13,-50+low),(13,-42+low)],horn)
    p.poly([(11,-41+low),(11,-48+low),(6,-55+low),(-1,-57+low),(-8,-58+low),(-14,-63+low),(-12,-65+low),(-5,-62+low),(4,-62+low),(12,-60+low),(18,-54+low),(20,-49+low),(19,-42+low)],horn)
    p.line([(-10,-63+low),(-3,-61+low),(6,-61+low),(11,-58+low),(15,-54+low)],fur,2)
    for x,y,n in [(-12,-59,4),(-6,-56,7),(0,-56,6),(5,-55,7),(11,-54,5)]:
        extra=2+i if state=='phase' else 0
        p.poly([(x-1,y+low),(x+1,y+low),(x+1,y+n+low+extra),(x,y+n+low+extra+1),(x-1,y+low+2)],WHITE)
    p.poly([(10,-46+low),(15,-48+low),(20,-46+low),(21,-42+low),(25,-39+low),(26,-34+low),(22,-31+low),(20,-30+low),(17,-25+low),(15,-22+low),(12,-27+low),(12,-31+low),(9,-34+low),(7,-39+low)],fur)
    p.poly([(11,-36+low),(15,-36+low),(19,-33+low),(23,-34+low),(23,-32+low),(18,-29+low),(16,-25+low),(14,-26+low),(13,-31+low)],shade,None)
    p.poly([(17,-42+low),(20,-44+low),(23,-42+low),(22,-40+low),(19,-40+low)],'#56648C')
    p.rect((19,-42+low,20,-41+low),WHITE if state!='death' else INK)
    p.line([(-18,-28),(-15,-27),(-13,-28)],'#B5C9D4')
    p.line([(-8,-25),(-5,-24),(-2,-25)],'#B5C9D4')
    p.rect((23,-36+low,26,-34+low),hoof)
    if state=='breath' and i>0:
        p.poly([(25,-35+low),(29,-37+low-i),(31,-35+low-i),(30,-33+low),(27,-32+low),(24,-33+low)],'#EDF3F5',None)
    if state=='charge_warn' and i==1:
        p.line([(10,-57+low),(15,-54+low),(17,-51+low)],WARN,2)
        p.rect((16,-52+low,16,-52+low),WHITE)


SCALES, SCALE_SHADE, BELLY, CREST = '#7386C6','#4A5890','#A9B8E0','#F4F1E6'


def serpent_head(p,state,i):
    if state=='death' and i==2:
        pebble(p,-16,0,11,7,SCALES,SCALE_SHADE)
        pebble(p,-3,0,9,11,SCALES,SCALE_SHADE)
        p.poly([(10,-8),(15,-4),(19,-5),(18,-1),(13,0),(9,-3)],CREST)
        p.rect((-14,-3,-13,-3),WHITE)
        return
    y = [-2,0][i] if state=='move' else 7+i*5 if state=='sink' else 11-i*10 if state=='rise' else 4 if state in ('rest','death') else 0
    if state=='death': y+=i*4
    anchor=(p.w//2,p.h//2+11)
    p.anchor=anchor
    p.shift(i-1 if state=='move' else 0,y)
    jaw = [4,8,1][i] if state=='bite' else [2,4][i] if state=='spit' else 0
    p.poly([(-20,2),(-20,-7),(-16,-11),(-11,-14),(-8,-19),(-2,-22),(6,-23),(11,-21),(17,-17),(20,-14),(21,-9),(20,-4),(17,-1),(10,0),(5,3),(-1,5),(-12,5)],SCALES)
    p.poly([(-19,-4),(-13,-3),(-7,-7),(0,-9),(8,-8),(15,-7),(20,-7),(18,-2),(10,-1),(4,2),(-1,4),(-12,4),(-19,1)],SCALE_SHADE,None)
    p.poly([(-9,-3),(-4,-6),(1,-5),(7,-3),(13,-3),(17,-5),(20,-4+jaw),(18,-1+jaw),(12,1+jaw),(6,1+jaw),(2,4),(-5,4),(-9,2)],BELLY)
    p.poly([(8,-6),(13,-6),(18,-8),(20,-7),(18,-3+jaw),(14,-1+jaw),(10,-2+jaw//2)],INK,None)
    if jaw>=4:
        p.poly([(15,-6),(17,-7),(17,-3),(16,-2)],CREST,None)
        p.poly([(11,-2+jaw//2),(15,jaw-2),(16,jaw-3),(13,jaw-5)],WARN,None)
    p.poly([(1,-21),(3,-25),(2,-28),(-3,-31),(-8,-31),(-12,-28),(-10,-27),(-6,-27),(-4,-25),(-4,-22),(-6,-20),(-3,-19)],WHITE if state=='phase' and i==1 else CREST)
    if state=='death' or state=='rest' and i==1:
        p.line([(8,-15),(13,-15)],INK,2)
    else:
        p.poly([(7,-17),(11,-19),(15,-17),(14,-14),(11,-12),(8,-13)],CREAM)
        p.rect((11,-17,11,-14),INK)
    p.rect((18,-13,19,-12),INK)
    for x,t in [(-14,-5),(-9,-10),(-5,-6),(-1,-15),(4,-12),(10,-8)]:
        p.line([(x,t),(x+2,t-2),(x+4,t-1)],'#596EAD')
    for x,t in [(-14,-8),(-10,-11),(-7,-8),(-3,-14),(2,-12)]:
        p.rect((x,t,x+1,t+1),WHITE)
    if state=='sink' or state=='rise' and i==0:
        p.rect((-25,-7-i*8,25,16),(0,0,0,0))


def serpent_piece(p,state,i,tail=False):
    p.anchor=(p.w//2,p.h//2+2)
    p.shift()
    if tail:
        end = (-7 if i==0 else 1) if state!='death' else 4
        p.poly([(-14,end),(-10,end+1),(-7,-1),(-2,-5),(7,-8),(13,-7),(14,-4),(14,4),(9,7),(0,7),(-6,5),(-9,end+4),(-13,end+3)],SCALES)
        p.line([(-10,end+3),(-6,3),(2,5),(11,5)],BELLY,2)
        for x,y in [(-3,1),(1,-1),(6,1),(9,-3)]: p.rect((x,y,x+1,y+1),WHITE)
        return
    low=i-1 if state=='move' else 5 if state=='death' else 0
    p.poly([(-15,0),(-14,-4),(-9,-7+low),(-1,-9+low),(8,-8+low),(13,-4),(14,1),(13,5),(8,7),(-7,7),(-13,4)],SCALES)
    p.poly([(-13,1),(-9,1),(-5,2),(3,2),(9,0),(13,0),(13,4),(8,6),(-6,6),(-12,4)],BELLY,None)
    p.line([(-12,1),(-8,1),(-4,2),(3,2),(9,0),(12,0)],SCALE_SHADE,2)
    if state=='warn':
        p.poly([(-9,-4),(-3,-8),(6,-7),(10,-4),(5,-2),(-5,-1)],WARN,INK)
        p.rect((0,-5,2,-4),WHITE)
    else:
        for x,t in [(-7,-3),(-4,-5),(0,-2),(6,-5),(8,-3)]:
            p.rect((x,t+low,x+1,t+low+1),WHITE)


def seedling(p,state,i):
    if state=='death':
        cap(p,-5,0,10,5)
        p.line([(-4,1),(5,1)],'#8A6F48')
        return
    p.poly([(-2,-6),(2,-6),(3,-2),(1,-1),(-2,-1),(-3,-3)],'#EDE3C8')
    p.line([(-2,-2),(-4+i,0)],'#8A6F48',2)
    p.line([(2,-2),(4-i,0)],'#8A6F48',2)
    cap(p,-5,-6,10,4)
    p.rect((-2,-9,2,-9),'#F2E6B8')


def tail(p,state,i):
    serpent_piece(p,state,i,tail=True)


BOSSES = [
    ('boss.kiln_boar',(192,128),kiln_boar,{'walk':4,'charge_warn':2,'charge':2,'stomp':3,'roar':2,'stun':2,'phase':2,'death':4}),
    ('boss.mirror_phantom',(64,96),phantom,{'hover':3,'fire':2,'cast':2,'burst':3,'open':2,'phase':2,'death':4,'crack':2,'look':1}),
    ('boss.spore_queen',(128,160),queen,{'walk':2,'ring':3,'cloud':2,'call':2,'jump':4,'open':2,'phase':3,'death':4}),
    ('boss.storm_owl',(192,128),owl,{'fly':4,'dive':2,'fan':3,'weather':2,'phase':2,'death':4}),
    ('boss.storm_owl.ground',(96,128),owl,{'land':2}),
    ('boss.frost_goat',(128,160),frost_goat,{'walk':4,'pillars':2,'sweep':3,'breath':3,'charge_warn':2,'charge':2,'impact':2,'phase':2,'death':4}),
    ('boss.night_serpent',(96,96),serpent_head,{'move':2,'bite':3,'spit':2,'sink':2,'rise':2,'rest':2,'phase':2,'death':3}),
    ('boss.night_serpent.segment',(64,64),serpent_piece,{'move':2,'warn':1,'death':1}),
    ('boss.night_serpent.tail',(64,64),tail,{'move':2,'death':1}),
    ('enemy.spore_seedling',(32,32),seedling,{'move':2,'death':1}),
]

PREVIEW_POSES = {
    'enemy.weasel_pot': ('stand',0),
    'enemy.bat_dusk': ('fly',1),
    'boss.storm_owl': ('fly',1),
}

# Color ramps follow the material palettes from monsters.md. A fine upper edge
# and the short cast shadow connect each broad pixel cluster to its neighbors.
RAMPS = {
    '#9CCB6A': ('#C1D991','#7FA755'), '#9AA39A': ('#BFC6BA','#747F79'),
    '#A89C8A': ('#CDBCA6','#837665'), '#98BF6E': ('#BBD793','#74A454'),
    '#A9AEB8': ('#D0D5D7','#808999'), '#BEC2CA': ('#DCE0E2','#919BA8'),
    '#CDB27E': ('#E5CF9F','#AA8A56'), '#EDE3C8': ('#F5EFDC','#C8B796'),
    '#8E9E7A': ('#B3C29B','#6B7D5F'), '#E9D98A': ('#F8EAB0','#C9B66F'),
    '#93B87F': ('#B5CFA2','#729768'), '#D9C7A3': ('#EFE0C2','#B69A71'),
    '#C09A78': ('#DBB995','#9F785A'), '#C9B8A0': ('#E6D6BC','#A99379'),
    '#A8875A': ('#CAAC82','#876A46'), '#8496C8': ('#B0BBDA','#687CAC'),
    '#A9B8E0': ('#CFD8EE','#8799C7'), '#7F8FA8': ('#A8B4C7','#697A93'),
    '#FFF0A8': ('#FFF7CE','#E8D37B'), '#B08A6E': ('#CEAE91','#8B6954'),
    '#A9B86A': ('#C5D095','#84964D'), '#E6D9A8': ('#F1E8CB','#C3B582'),
    '#A68A78': ('#C6AB97','#886C5B'), '#8E8A96': ('#B4B0BD','#746F7E'),
    '#C8D0E0': ('#E4EAF5','#AAB6CA'), '#AEB6C2': ('#D1D6DF','#909AAE'),
    '#8E9CC4': ('#B6C1DC','#6B7BA5'), '#D8E4EC': ('#EEF4F6','#AFBFCE'),
    '#8FA9C4': ('#B4C7D8','#718CA9'), '#7386C6': ('#9BAADD','#596EAD'),
}
def rgba(code):
    return tuple(bytes.fromhex(code[1:]))+(255,)


RAMPS = {rgba(k):tuple(rgba(c) for c in v) for k,v in RAMPS.items()}


def finish(frame, asset_id):
    original=frame.load()
    finished=frame.copy()
    dst=finished.load()
    ink=rgba(INK)
    bounds=frame.getbbox()
    if bounds is None: return finished
    l,t,r,b=bounds
    for y in range(max(1,t),min(frame.height-1,b)):
        for x in range(max(1,l),min(frame.width-1,r)):
            color=original[x,y]
            n,s,w,e=[original[x,y-1],original[x,y+1],original[x-1,y],original[x+1,y]]
            if color==ink:
                # Reduce the inner row of a two-pixel outline without changing
                # the stepped silhouette or the edge of a line drawn on a face.
                if n[3] and s[3] and w[3] and e[3]:
                    if n in RAMPS and s==ink and (w==ink or e==ink):
                        dst[x,y]=RAMPS[n][1]
                    elif w in RAMPS and e==ink and (n==ink or s==ink):
                        dst[x,y]=RAMPS[w][1]
                continue
            ramp=RAMPS.get(color)
            if ramp is None: continue
            if n==ink and s==color and (w==color or e==color) and (x < (l+r)*.28 or y < (t+b)*.30):
                dst[x,y]=ramp[0]
            elif (s==ink or e==ink) and w==color and n==color and y>(t+b)*.30:
                dst[x,y]=ramp[1]
    return finished


def flower(p,x,y):
    p.poly([(x-6,y),(x-5,y-4),(x-2,y-5),(x-2,y-8),(x+1,y-9),(x+3,y-7),(x+3,y-5),(x+6,y-6),(x+8,y-4),(x+7,y-1),(x+4,y),(x+4,y+3),(x+1,y+4),(x-2,y+1),(x-4,y+2)],'#F4F1E6')
    p.poly([(x-3,y-2),(x-1,y-4),(x+1,y-4),(x+3,y-2),(x+2,y),(x,y+1)],'#C8AF66')
    p.rect((x-1,y-3,x,y-2),'#F2E38C')


def crescent(p,x,y,w):
    p.poly([(x-4*w,y-4*w),(x-w,y-5*w),(x+2*w,y-3*w),(x+3*w,y),(x+2*w,y+2*w),(x,y+3*w),(x-3*w,y+2*w),(x-5*w,y),(x-2*w,y+w),(x+w,y),(x+w,y-2*w),(x-w,y-3*w)],'#F4F1E6')
    p.line([(x-3*w,y+w),(x,y+2*w),(x+2*w,y+w)],'#AEB6C2')


def elite_overlay(p,state,i,kind):
    p.shift(0,-2)
    if kind=='slime_moss':
        pebble(p,-9,0,18,6,moss=True)
        pebble(p,-7,-5,14,6,moss=True)
        pebble(p,-4,-10,8,5,moss=True)
    elif kind=='wolf_bramble':
        for x,h in [(-12,7),(-8,10),(-3,12),(2,11),(7,10),(11,6)]:
            p.poly([(x-3,-1),(x-h//3,-h),(x+1,-h+1),(x+3,-3),(x+2,0)],'#98BF6E')
            p.rect((x-h//3+1,-h+2,x-h//3+2,-h+3),'#E6F2C0')
        for x,y in [(-11,-5),(-4,-8),(3,-7),(9,-4)]:
            p.rect((x-1,y-1,x+1,y+1),'#F4F1E6')
            p.rect((x,y,x,y),'#C8AF66')
    elif kind=='golem_menhir':
        p.poly([(-19,-2),(-18,-7),(-12,-11),(5,-12),(15,-9),(18,-6),(17,-2),(10,0),(-11,0)],'#A9AEB8')
        p.poly([(-17,-5),(-7,-5),(4,-6),(17,-7),(15,-3),(9,-1),(-11,-1)],'#5E6678',None)
        p.poly([(-13,-8),(-8,-11),(-1,-11),(1,-9),(-3,-8),(-4,-6),(-8,-8)],'#98BF6E')
        p.line([(6,-10),(12,-8),(14,-8)],'#D0D5D7')
    elif kind=='shaman_cap':
        cap(p,-10,-1,19,7,face=True)
        cap(p,-7,-7,14,6,face=True)
    elif kind=='bug_lantern':
        husk(p,-13,-1,9,10)
        husk(p,4,-1,9,10)
        husk(p,-5,-3,10,12)
    elif kind=='hog_needle':
        for x,y in [(-9,-6),(-4,-9),(1,-10),(7,-7)]:
            p.poly([(x-5,y+4),(x-2,y-2),(x+2,y-4),(x+1,y+1),(x-1,y+4)],'#93B87F')
            p.poly([(x-3,y),(x-1,y-2),(x+1,y-2),(x+2,y+1),(x+1,y+4),(x-1,y+4),(x-3,y+2)],'#A8875A')
            p.line([(x-2,y),(x,y+1),(x+1,y+1)],'#6E5444')
            p.rect((x-1,y-1,x,y-1),'#C9B8A0')
    elif kind=='weasel_pot':
        flowerpot(p,-1,0,plant='flower')
    elif kind=='bat_dusk':
        crescent(p,-7,-3,1)
        crescent(p,6,-6,1)
        p.rect((2,-1,2,-1),WHITE)
    elif kind=='wisp_stray':
        p.shift(0,-5)
        for x,y in [(-4,-2),(4,0)]:
            p.poly([(x-3,y),(x-3,y-3),(x-1,y-7),(x,y-4),(x+3,y-5),(x+3,y-1),(x+1,y+2),(x-1,y+2)],'#FFF0A8')
            p.rect((x-1,y-2,x+1,y-1),'#7F8FA8')
    elif kind=='vine_thorn':
        flower(p,0,-4)
    elif kind=='boar_tusk':
        p.poly([(-8,-3),(-5,-1),(-1,-2),(3,-5),(5,-9),(7,-8),(7,-4),(4,0),(0,2),(-5,1)],'#F0E6C8')
        p.line([(-6,-1),(-2,0),(3,-2)],'#C9B8A0')
        p.line([(9,-9),(11,-7),(14,-7)],'#A68A78')
        p.line([(12,-10),(11,-8),(11,-5)],'#A68A78')
    elif kind=='toad_drum':
        for x, y in [(-7,0),(4,-2)]:
            p.poly([(x-5,y),(x-5,y-3),(x-4,y-6),(x-2,y-8),(x+2,y-7),(x+4,y-4),(x+4,y-1),(x+2,y+1),(x-3,y+1)],'#A9B86A')
            p.rect((x+1,y-5,x+3,y-5),CREAM)
            p.line([(x+1,y-3),(x+3,y-2),(x+1,y)],'#E6D9A8',2)
    else:
        raise ValueError(kind)


def elite_horn(p,state,i):
    p.shift(0,-1)
    for d in (-1,1):
        p.poly([(d*x,y) for x,y in [(0,-1),(1,-3),(2,-3),(2,-5),(3,-5),(3,-1),(2,0),(1,0)]],'#FFD24A')
        p.rect((d*2,-2,d*2,-2),'#FFF2A0')


def queen_bat(p,state,i):
    bat(p,'dive',i,elite=True)


def twin_wisp(p,state,i):
    wisp(p,'twin',i)


def overlay_drawer(kind):
    def draw(p,state,i):
        elite_overlay(p,state,i,kind)
    return draw


ELITES = [
    (f'elite.{enemy_id.split(".")[1]}.overlay',(96 if i in (2,10) else 64,32),
     overlay_drawer(enemy_id.split('.')[1]),{'overlay':1})
    for i,(enemy_id,_,_,_) in enumerate(REGULAR)
] + [
    ('elite.horn',(16,16),elite_horn,{'idle':1}),
    ('elite.bat_dusk.dive',(64,64),queen_bat,{'dive':1}),
    ('elite.wisp_stray.twin',(64,64),twin_wisp,{'hover':3}),
]


BOSS_PORTRAIT_CROPS = {
    'boss.kiln_boar': (90, 13, 184, 105),
    'boss.mirror_phantom': (14, 8, 51, 79),
    'boss.spore_queen': (8, 12, 121, 111),
    'boss.storm_owl': (42, 12, 150, 102),
    'boss.frost_goat': (34, 21, 119, 108),
    'boss.night_serpent': (4, 1, 94, 87),
}


def boss_icon(p, state, i, boss_id):
    p.shift(0,-2)
    if boss_id == 'boss.kiln_boar':
        p.poly([(-6,1),(-6,-3),(-5,-7),(-2,-10),(2,-10),(5,-7),(6,-3),(6,1)],'#8E8A96')
        p.poly([(-4,0),(-4,-4),(-2,-6),(2,-6),(4,-4),(4,0)],INK)
        p.poly([(-3,0),(-2,-2),(-1,-1),(0,-4),(2,-2),(3,0)],CREAM)
        p.line([(-3,-8),(-4,-12),(-6,-14)],'#C8D0E0')
        p.line([(4,-8),(5,-11),(6,-12)],'#C8D0E0')
    elif boss_id == 'boss.mirror_phantom':
        p.poly([(-5,-2),(-6,-6),(-5,-10),(-3,-13),(2,-13),(5,-11),(6,-8),(5,-3),(3,0),(-2,0)],'#AEB6C2')
        p.poly([(-3,-3),(-4,-7),(-3,-10),(-1,-11),(2,-11),(4,-8),(3,-4),(1,-2),(-1,-2)],'#F4F1E6','#5E6678')
        p.line([(-2,-5),(2,-9)],'#AEB6C2')
    elif boss_id == 'boss.spore_queen':
        p.poly([(-2,-5),(2,-5),(3,1),(1,2),(-3,1)],'#EDE3C8')
        cap(p,-7,-4,14,7)
        for x,y in [(-3,-7),(3,-7),(0,-5)]:
            p.rect((x,y,x+1,y),'#8A6F48')
        p.line([(-3,1),(-5,2),(4,2)],'#8A6F48')
    elif boss_id == 'boss.storm_owl':
        p.poly([(-6,-6),(-7,-12),(-3,-9),(3,-9),(7,-12),(6,-5),(3,-2),(-3,-2)],'#8E9CC4')
        p.poly([(-6,-6),(-3,-8),(-1,-6),(0,-4),(1,-6),(3,-8),(6,-6),(3,-3),(-3,-3)],'#C8D0E0',None)
        p.rect((-5,-6,-3,-5),CREAM)
        p.rect((3,-6,5,-5),CREAM)
        p.rect((0,-5,0,-3),'#3A2E2A')
    elif boss_id == 'boss.frost_goat':
        p.poly([(-2,-2),(-1,-7),(2,-8),(5,-5),(6,-2),(3,2),(1,3),(-1,1)],'#D8E4EC')
        p.poly([(-1,-6),(-2,-10),(-6,-10),(-7,-13),(-5,-12),(0,-12),(3,-10),(4,-7),(2,-6)],'#8FA9C4')
        for x,y in [(-5,-10),(-2,-9),(1,-8)]:
            p.rect((x,y,x,y+2),WHITE)
        p.rect((3,-5,4,-5),WHITE)
    elif boss_id == 'boss.night_serpent':
        p.poly([(-6,0),(-6,-5),(-2,-8),(2,-8),(5,-6),(7,-3),(5,0),(2,1),(-3,1)],SCALES)
        p.line([(-5,-1),(-1,0),(5,-1)],BELLY,2)
        p.poly([(-2,-8),(-1,-11),(-3,-13),(-6,-12),(-4,-10),(-5,-7),(-2,-6)],CREST)
        p.rect((3,-6,3,-4),CREAM)
        p.rect((-4,-4,-4,-4),WHITE)
        p.rect((-2,-3,-2,-3),WHITE)


def write_boss_ui(boss_cards):
    assets = {}
    portraits, icons = {}, {}
    for boss_id, box in BOSS_PORTRAIT_CROPS.items():
        crop = boss_cards[boss_id].crop(box)
        crop = crop.crop(crop.getbbox())
        crop.thumbnail((88,88),Image.Resampling.NEAREST)
        if max(crop.size)<48:
            crop = crop.resize(tuple(n*2 for n in crop.size),Image.Resampling.NEAREST)
        portrait = Image.new('RGBA',(96,96))
        portrait.paste(crop,((96-crop.width)//2,(96-crop.height)//2))
        portrait_name = f'{boss_id}.portrait.png'
        portrait.save(OUT / portrait_name,optimize=True)
        p = Pixels((32,32))
        boss_icon(p,'idle',0,boss_id)
        icon = p.png()
        icon_name = f'{boss_id}.hud.png'
        icon.save(OUT / icon_name,optimize=True)
        assets[boss_id]={'portrait':portrait_name,'portraitSize':[96,96],'hud':icon_name,'hudSize':[32,32]}
        portraits[boss_id]=portrait
        icons[boss_id]=icon
    preview('preview-portraits',portraits,3)
    preview('preview-boss-icons',icons,3)
    return assets


def main():
    regular, regular_cards = render(REGULAR)
    bosses, boss_cards = render(BOSSES)
    elites, elite_cards = render(ELITES)
    preview('preview-regular',regular_cards)
    preview('preview-bosses',{k:v for k,v in boss_cards.items() if k.count('.')==1 and k.startswith('boss')},3)
    preview('preview-elites',elite_cards)
    manifest={
        'schema':1,
        'artwork':'hand-authored pixel sprites',
        'scaleMode':'nearest',
        'entries':regular|bosses|elites,
        'bossUi':write_boss_ui(boss_cards),
    }
    (OUT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')


if __name__=='__main__':
    main()
