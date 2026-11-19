import json, math, random
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageOps, ImageFilter, ImageChops
from fontTools.ttLib import TTFont

ROOT = Path(__file__).resolve().parents[1]
INK, DARK, WOOD, RIM = '#12131c', '#1f2130', '#2a2d40', '#3a3f5a'
CHALK, SLATE, PAPER, FOLD = '#f2ede0', '#8e9aae', '#e8ddbf', '#c9bb96'
YELLOW, GOLD, AMBER, GREEN = '#ffe94a', '#ffd24a', '#f2a33a', '#6fd36b'
BRASS, TAN, PALE_BRASS = '#836441', '#a98457', '#c8a24a'
BLUE, SILVER, LIME = '#4f8cff', '#9aa0ae', '#b8f04a'
ELEMENTS = {
    'fire': ('#ff8a1f','#ffe08a'), 'lightning': ('#fff36b','#ffffff'),
    'arcane': ('#4fe3c1','#d6fff3'), 'frost': ('#9fdcff','#f4fbff'),
    'shadow': ('#8a5cff','#d9c7ff'), 'neutral': (SLATE, CHALK),
}

def canvas(w,h=None):
    return Image.new('RGBA',(w,h or w))

def save(id, name, images, nine=None, interval=None, folder=None, metadata=None):
    if isinstance(images, Image.Image):
        images = [images]
    w,h=images[0].size
    assert all(img.size==(w,h) for img in images)
    assert all(img.getbbox() for img in images), (id, name)
    group = {'k':'kit','i':'icon','f':'font','s':'screen'}[id[0]]
    rel = Path(group)
    if group=='screen':
        rel /= folder or SCREEN_FOLDERS[id[2:4]]
    stem, _, suffix = name.rpartition('_')
    if stem and suffix in {'normal','hover','selected','pressed','on','disabled'}:
        name = f'{id}_{stem}_{w}x{h}_{suffix}'
    else:
        name = f'{id}_{name}_{w}x{h}'
    if len(images)>1:
        name += f'_f{len(images)}'
    rel /= f'{name}.png'
    path=ROOT/rel
    path.parent.mkdir(parents=True,exist_ok=True)
    sheet=canvas(w*len(images),h)
    for i,img in enumerate(images):
        sheet.alpha_composite(img,(i*w,0))
    sheet.save(path,optimize=True)
    record={'id':id.upper(),'file':str(rel),'width':w,'height':h,'frames':len(images)}
    if nine:
        record['nine_slice_ltrb']=[nine]*4 if isinstance(nine,int) else list(nine)
    if interval:
        record['frame_ms']=interval
    if metadata:
        record.update(metadata)
    ASSETS.append(record)
    return rel

SCREEN_FOLDERS={'02':'title','03':'profile','04':'offline','05':'menu','06':'expedition',
'08':'hud','09':'duel','10':'loot','11':'assembly','12':'enhance','13':'techniques',
'14':'training','16':'results','17':'duel_results','19':'settings','20':'plaza',
'21':'duel_rooms','23':'market','24':'mail_wardrobe','25':'rankings','26':'shared'}
ASSETS=[]

def clipped(d, bbox, r, fill):
    x0,y0,x1,y1=bbox
    r=min(r,(x1-x0)//2,(y1-y0)//2)
    d.polygon([(x0+r,y0),(x1-r,y0),(x1,y0+r),(x1,y1-r),
    (x1-r,y1),(x0+r,y1),(x0,y1-r),(x0,y0+r)], fill=fill)

def brad(d,x,y, size=4, color=TAN):
    d.rectangle((x,y,x+size-1,y+size-1),fill=INK)
    d.rectangle((x+1,y+1,x+size-2,y+size-2),fill=color)
    d.point((x+1,y+1),fill=PALE_BRASS)

def plank(w,h, kind='wood', state='normal', accent=None, brads=False, corners=5):
    im=canvas(w,h)
    d=ImageDraw.Draw(im)
    pressed=state=='pressed'
    offset=2 if pressed else 0
    paper=kind=='paper'
    on=state=='on'
    base=PAPER if paper else DARK if kind=='well' or pressed else RIM if on else WOOD
    light=FOLD if paper else RIM
    shadow=FOLD if paper else DARK
    grain='#dfd2ad' if paper else '#303448' if base==WOOD else '#292c3b'
    clipped(d,(0,offset,w-1,h-1),corners,INK)
    clipped(d,(2,2+offset,w-3,h-3),max(corners-1,1),base)
    d.line((corners+1,2+offset,w-corners-2,2+offset),fill=light)
    if not pressed and h>=12:
        d.line((corners,h-4,w-corners-1,h-4),fill=shadow,width=2)
    if h>=20 and w>=24:
        for y in (6, h-8):
            d.line((corners+3,y+offset,w-corners-5,y+offset),fill=grain)
            d.line((corners+6,y+1+offset,min(corners+11,w-7),y+1+offset),fill=grain)
    if paper and w>=32 and h>=24:
        d.polygon([(w-13,2+offset),(w-3,12+offset),(w-13,12+offset)],fill=FOLD)
        d.line((w-12,3+offset,w-4,11+offset),fill='#f4e9cc')
    if accent:
        d.line((corners,4+offset,w-corners-1,4+offset),fill=accent,width=2)
        d.line((3,corners+1+offset,3,h-corners-2),fill=accent)
        d.line((w-4,corners+1+offset,w-4,h-corners-2),fill=accent)
    if state in ('hover','editing','expanded'):
        d.line((corners+1,5+offset,w-corners-2,5+offset),fill=SLATE)
    if on:
        d.rectangle((3,h-7,w-4,h-4),fill=CHALK)
    if brads and w>=28 and h>=28:
        for x in (6,w-10):
            for y in (6+offset,h-10):
                brad(d,x,y)
    if state=='disabled':
        a=im.getchannel('A')
        im=ImageOps.grayscale(im).point(lambda p: int(p*.6)).convert('RGBA')
        im.putalpha(a)
    return im

def frame(w,h,c,thickness=2,variant='normal',offset=2):
    im=canvas(w,h)
    d=ImageDraw.Draw(im)
    x0,y0,x1,y1=offset,offset,w-offset-1,h-offset-1
    pts=[(x0+3,y0),(x1-3,y0),(x1,y0+3),(x1,y1-3),(x1-3,y1),(x0+3,y1),(x0,y1-3),(x0,y0+3)]
    if variant=='dash':
        for x in range(x0+2,x1-1,8):
            for y in (y0,y1-thickness+1):
                d.rectangle((x,y,min(x+3,x1-2),y+thickness-1),fill=c)
        for y in range(y0+2,y1-1,8):
            for x in (x0,x1-thickness+1):
                d.rectangle((x,y,x+thickness-1,min(y+3,y1-2)),fill=c)
    else:
        d.line(pts+[pts[0]],fill=INK,width=thickness+2)
        d.line(pts+[pts[0]],fill=c,width=thickness)
    if variant in ('rare','hero'):
        for x in (x0+4,x1-4):
            for y in (y0+4,y1-4):
                d.polygon([(x,y-3),(x+3,y),(x,y+3),(x-3,y)],fill=c)
    if variant=='hero':
        x=w//2
        d.polygon([(x-5,y0+1),(x,y0-2),(x+5,y0+1),(x,y0+6)],fill=c)
        d.point((x,y0+1),fill=CHALK)
    return im

def fillbar(w,h,color):
    im=canvas(w,h); d=ImageDraw.Draw(im)
    clipped(d,(0,0,w-1,h-1),3,INK)
    clipped(d,(1,1,w-2,h-2),2,color)
    if h>=8:
        d.line((3,2,w-4,2),fill=CHALK)
    return im

# Shapes specified in 24 logical units. Small variants are rasterized directly on their target grid.
class Pictogram:
    def __init__(self,w,h=None,fill=CHALK):
        self.im=canvas(w,h)
        self.d=ImageDraw.Draw(self.im)
        self.sx=w/24
        self.sy=(h or w)/24
        self.color=fill
    def pt(self,x,y):
        return (round(x*self.sx),round(y*self.sy))
    def shape(self,points,fill=None):
        self.d.polygon([self.pt(x,y) for x,y in points],fill=fill or self.color)
    def line(self,points,fill=None,width=2,edge=True):
        pts=[self.pt(x,y) for x,y in points]
        px=max(1,round(width*min(self.sx,self.sy)))
        if edge:
            self.d.line(pts,fill=INK,width=px+2,joint='curve')
        self.d.line(pts,fill=fill or self.color,width=px,joint='curve')
    def box(self,x0,y0,x1,y1,fill=None,edge=False):
        self.d.rectangle(self.pt(x0,y0)+self.pt(x1,y1),fill=fill or self.color,outline=INK if edge else None)
    def circle(self,x,y,r,fill=None,edge=False):
        self.d.ellipse(self.pt(x-r,y-r)+self.pt(x+r,y+r),fill=fill or self.color,outline=INK if edge else None)
    def ring(self,x,y,r,fill=None,weight=1):
        self.d.ellipse(self.pt(x-r,y-r)+self.pt(x+r,y+r),outline=fill or self.color,width=max(1,round(weight*self.sx)))
    def diamond(self,x,y,r,fill=None,ry=None):
        ry=ry or r
        self.shape([(x,y-ry),(x+r,y),(x,y+ry),(x-r,y)],fill)
    def sparkle(self,x,y,r,fill=None):
        self.shape([(x,y-r),(x+1,y-1),(x+r,y),(x+1,y+1),(x,y+r),(x-1,y+1),(x-r,y),(x-1,y-1)],fill)

def draw_element(size, name):
    c,hi=ELEMENTS[name]
    p=Pictogram(size,fill=c)
    if name=='fire':
        p.shape([(5,12),(9,9),(11,3),(14,6),(14,10),(17,8),(20,13),(20,17),(17,21),(8,21),(4,17)],INK)
        p.shape([(6,13),(10,10),(12,5),(13,8),(13,12),(17,10),(19,14),(18,18),(15,20),(9,20),(6,17)])
        p.shape([(10,18),(9,15),(13,10),(13,14),(16,15),(16,17),(14,19),(12,20)],hi)
    elif name=='lightning':
        p.shape([(15,2),(4,13),(11,13),(8,22),(20,10),(13,10),(17,2)],INK)
        p.shape([(14,4),(6,12),(12,12),(10,19),(18,11),(12,11),(15,4)])
        p.line([(13,6),(8,11),(13,11)],hi,1,False)
    elif name=='arcane':
        p.diamond(12,12,10,INK)
        p.diamond(12,12,8)
        p.diamond(12,12,5,'#163f48')
        p.diamond(12,12,3,hi)
    elif name=='frost':
        p.shape([(15,2),(20,7),(17,16),(8,22),(4,17),(6,9)],INK)
        p.shape([(15,4),(18,8),(16,15),(9,20),(6,17),(8,10)])
        p.shape([(15,4),(12,12),(6,17),(8,10)],hi)
        p.shape([(18,8),(16,15),(9,20),(12,12)],'#5296c8')
    elif name=='shadow':
        p.circle(12,12,9,INK); p.circle(12,12,8,hi); p.circle(12,12,6,'#2a1a5e')
        p.diamond(12,12,3,'#f1e9ff'); p.circle(12,12,1,CHALK)
    else:
        p.ring(12,12,7,INK,4); p.ring(12,12,7,c,2)
    return p.im

def glyph(size,name, color=CHALK):
    p=Pictogram(size,fill=color)
    line,box,shape,dot,ring=p.line,p.box,p.shape,p.circle,p.ring
    if name=='check':
        line([(5,12),(9,16),(19,6)],width=3)
    elif name in ('x','close'):
        line([(7,7),(17,17)],width=2);line([(17,7),(7,17)],width=2)
    elif name in ('plus','minus','medical'):
        line([(6,12),(18,12)],width=3)
        if name!='minus': line([(12,6),(12,18)],width=3)
    elif name in ('left','right','up','down','play','step'):
        shape([(8,4),(18,12),(8,20)],INK);shape([(9,6),(16,12),(9,18)])
        transform={'left':Image.Transpose.FLIP_LEFT_RIGHT,'up':Image.Transpose.ROTATE_90,'down':Image.Transpose.ROTATE_270}
        if name in transform: p.im=p.im.transpose(transform[name])
    elif name in ('lock','unlock'):
        line([(7,11),(7,7),(9,4),(15,4),(17,7),(17,11)] if name=='lock' else [(8,11),(8,7),(10,4),(16,4),(18,7)],SLATE,2)
        box(5,10,19,20,INK);box(6,11,18,19,color);box(11,14,12,17,INK)
    elif name in ('diamond','debt','spark','score','star'):
        if name=='debt':
            p.diamond(12,12,8,INK);p.diamond(12,12,6,SLATE);p.diamond(12,12,3,INK)
        elif name=='diamond':
            p.diamond(12,12,8,INK);p.diamond(12,12,6,color);p.diamond(12,11,2,CHALK)
        else:
            p.sparkle(12,12,10,INK);p.sparkle(12,12,8)
            if size>=24: dot(12,12,1,PAPER)
    elif name in ('warning','tri_up','tri_down'):
        shape([(12,3),(22,20),(2,20)],INK);shape([(12,6),(19,18),(5,18)])
        if name=='warning':box(11,10,12,13,INK);box(11,15,12,16,INK)
        if name=='tri_down':p.im=p.im.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    elif name in ('chain','broken','trade'):
        line([(11,8),(8,5),(4,5),(2,8),(2,13),(5,16),(8,16),(10,14)],width=2)
        line([(13,16),(16,19),(20,19),(22,16),(22,11),(19,8),(16,8),(14,10)],width=2)
        if name!='broken': line([(8,14),(16,10)],width=2)
        else: line([(11,3),(13,6)],AMBER,1)
    elif name in ('eye','eye_hidden','spectate'):
        line([(2,12),(7,7),(12,5),(17,7),(22,12),(17,17),(12,19),(7,17),(2,12)],width=2)
        dot(12,12,3,SLATE if name=='spectate' else color)
        if name=='eye_hidden': line([(4,20),(20,4)],color,2)
        elif name=='spectate': dot(12,12,1,INK)
    elif name in ('clock','timeout'):
        ring(12,12,8,INK,4);ring(12,12,8,color,2)
        line([(12,7),(12,12),(16,14)],width=2)
    elif name=='hourglass':
        line([(6,4),(18,4),(18,7),(13,12),(18,17),(18,20),(6,20),(6,17),(11,12),(6,7),(6,4)],width=2)
        shape([(8,18),(12,14),(16,18)],AMBER)
    elif name=='search':
        ring(10,9,5,INK,4);ring(10,9,5,color,2)
        line([(14,13),(20,19)],width=3)
    elif name=='sort':
        line([(5,6),(17,6)],width=2);line([(5,11),(14,11)],width=2);line([(5,16),(10,16)],width=2)
        shape([(15,15),(21,15),(18,20)])
    elif name=='filter':
        line([(4,5),(20,5),(14,12),(14,18),(10,20),(10,12),(4,5)],width=2)
    elif name=='refresh':
        line([(19,10),(17,6),(11,4),(6,6),(4,10)],width=2)
        shape([(15,10),(21,12),(21,5)])
        line([(5,14),(7,18),(13,20),(18,18),(20,14)],width=2)
        shape([(3,12),(3,19),(9,14)])
    elif name in ('back','return'):
        line([(19,17),(19,12),(15,9),(5,9)],width=2)
        line([(9,5),(5,9),(9,13)],width=2)
    elif name in ('info','question','K','rank_1','rank_2','rank_3'):
        font=ImageFont.truetype(str(ROOT/'font/neodgm.ttf'),max(8,round(size*.71)))
        ch={'info':'i','question':'?','rank_1':'1','rank_2':'2','rank_3':'3'}.get(name,name)
        if name.startswith('rank'): dot(12,12,9,INK);dot(12,12,7,color)
        a=Image.new('1',p.im.size)
        ImageDraw.Draw(a).text((size//2,size//2),ch,font=font,anchor='mm',fill=1)
        p.im.paste(INK if name.startswith('rank') else color,(0,0),a)
    elif name in ('pause','equal'):
        if name=='pause':box(6,5,9,19);box(14,5,17,19)
        else:box(6,9,18,10);box(6,14,18,15)
    elif name in ('copy','paper','report','script'):
        if name=='copy': line([(6,17),(4,17),(4,4),(15,4),(15,7)],SLATE,2)
        box(7,7,19,21,color,True)
        shape([(15,7),(19,11),(15,11)],SLATE)
        for y in (13,16):line([(10,y),(16,y)],INK,1,False)
    elif name=='trash':
        line([(4,7),(20,7)],width=2);line([(9,5),(9,3),(15,3),(15,5)],width=2)
        line([(6,8),(7,20),(17,20),(18,8)],width=2)
        line([(10,11),(10,17)],width=1);line([(14,11),(14,17)],width=1)
    elif name in ('pencil','wand','spear'):
        shape([(4,20),(6,15),(16,4),(20,8),(9,18)],INK)
        line([(7,17),(18,6)],color,3,False)
        if name=='wand':p.sparkle(18,5,4,CHALK)
        elif name=='spear':shape([(16,4),(21,3),(20,8)])
    elif name=='pin':
        shape([(8,4),(16,4),(15,9),(18,13),(18,15),(6,15),(6,13),(9,9)],color)
        line([(12,15),(12,21)],SLATE,2)
    elif name in ('talk','whisper'):
        shape([(3,4),(21,4),(21,15),(12,15),(7,20),(7,15),(3,15)],INK)
        shape([(5,6),(19,6),(19,13),(12,13),(9,16),(9,13),(5,13)],color)
        for x in (8,12,16):dot(x,10,1,INK)
        if name=='whisper':box(15,14,20,16,SLATE)
    elif name in ('person','friends','friend_add','follow','portrait'):
        if name in ('friends','follow'):
            dot(16,7,3,SLATE);shape([(12,11),(19,11),(22,17),(12,17)],SLATE)
        dot(9,7,3,color);shape([(6,11),(12,11),(16,17),(16,19),(2,19),(2,17)],color)
        if name=='friend_add': line([(16,6),(22,6)],GREEN,2);line([(19,3),(19,9)],GREEN,2)
    elif name in ('blocked','offline','online','idle'):
        if name=='blocked':ring(12,12,8,color,2);line([(7,17),(17,7)],width=2)
        elif name=='offline':ring(12,12,7,color,2);line([(8,12),(16,12)],width=2)
        elif name=='online': dot(12,12,8,INK);dot(12,12,5,GREEN);dot(11,10,1,CHALK)
        else: shape([(12,3),(18,5),(20,10),(17,10),(14,8),(14,5),(12,3),(7,5),(4,10),(4,15),(8,19),(14,20),(19,16),(20,12),(17,15),(13,14),(10,11),(10,6)])
    elif name in ('duel','duel_invite','duel_wait'):
        line([(4,4),(18,18)],width=3);line([(20,4),(6,18)],width=3)
        line([(4,13),(10,20)],width=2);line([(14,20),(20,13)],width=2)
        if name=='duel_invite':p.sparkle(19,6,4,AMBER)
        if name=='duel_wait':dot(12,18,3,SLATE)
    elif name in ('path','expedition'):
        shape([(3,16),(7,14),(9,11),(16,9),(18,4),(20,5),(18,11),(12,13),(11,17),(9,20),(3,20)],color)
        line([(4,12),(4,7),(7,4)],SLATE,1);line([(17,18),(19,15),(21,15)],SLATE,1)
    elif name in ('tent','square'):
        line([(3,18),(12,4),(21,18),(3,18)],width=2)
        shape([(12,10),(16,17),(8,17)],SLATE)
        line([(2,20),(22,20)],width=1)
    elif name in ('hammer','enhance'):
        shape([(4,8),(12,3),(19,9),(14,14),(11,12),(7,15),(3,11)],color)
        line([(13,12),(20,20)],SLATE,3)
    elif name in ('target','training','inspect'):
        ring(12,12,8,INK,3);ring(12,12,8,color,2);ring(12,12,4,SLATE,1)
        dot(12,12,1,color)
    elif name in ('library','book','record'):
        line([(3,5),(9,5),(12,8),(15,5),(21,5),(21,18),(15,18),(12,20),(9,18),(3,18),(3,5)],width=2)
        line([(12,8),(12,18)],SLATE,1)
        if name=='library':line([(5,9),(8,9)],width=1);line([(16,9),(19,9)],width=1)
        if name=='record':line([(5,13),(9,13)],width=1)
    elif name in ('settings','gear'):
        for a in range(0,360,45):
            r=math.radians(a); line([(12+math.cos(r)*6,12+math.sin(r)*6),(12+math.cos(r)*9,12+math.sin(r)*9)],width=3)
        ring(12,12,6,color,2);ring(12,12,2,color,1)
    elif name in ('hanger','wardrobe','cloak'):
        line([(9,7),(10,4),(14,4),(15,7),(12,10),(12,12),(3,18),(3,20),(21,20),(21,18),(12,12)],width=2)
        if name=='cloak':
            shape([(12,9),(8,12),(4,21),(20,21),(16,12)],SLATE);line([(12,12),(12,20)],width=1)
    elif name in ('shop','market','cart'):
        if name=='cart':
            line([(2,5),(5,5),(7,14),(19,14),(21,8),(6,8)],width=2)
            dot(8,19,2);dot(18,19,2)
        else:
            line([(4,11),(4,20),(20,20),(20,11)],width=2);box(9,13,14,20,SLATE)
            shape([(5,4),(19,4),(22,10),(2,10)],color)
            if name=='market':
                for x in (5,11,17): box(x,7,x+2,10,SLATE)
    elif name in ('mail','mail_open','mail_attach','mail_claimed'):
        box(3,7,21,19,color,True)
        if name=='mail_open':
            shape([(3,7),(12,3),(21,7),(12,13)],SLATE)
            line([(4,18),(9,13),(12,15),(15,13),(20,18)],INK,1,False)
        else:line([(4,8),(12,14),(20,8)],INK,1,False)
        if name=='mail_attach':p.diamond(18,16,4,AMBER)
        if name=='mail_claimed':line([(14,16),(17,19),(22,12)],GREEN,2)
    elif name in ('podium','ranking','il'):
        box(3,12,7,19);box(9,5,14,19);box(16,9,20,19);line([(2,20),(21,20)],SLATE,1,False)
        if name=='il':p.sparkle(18,4,3)
    elif name in ('calendar','season','rounds'):
        box(4,6,20,20,INK);box(5,7,19,19,color)
        box(5,9,19,11,SLATE)
        for x in (8,15):line([(x,4),(x,8)],width=2)
        if name=='rounds':box(9,13,15,17,INK)
        else:p.sparkle(12,15,3,INK)
    elif name=='exit':
        line([(13,4),(5,4),(5,20),(13,20)],width=2)
        line([(9,12),(21,12),(17,8)],width=2);line([(21,12),(17,16)],width=2)
    elif name in ('display','screen'):
        line([(3,5),(21,5),(21,16),(3,16),(3,5)],width=2)
        line([(12,17),(12,20)],width=2);line([(7,20),(17,20)],width=2)
    elif name=='audio':
        shape([(3,9),(7,9),(12,5),(12,19),(7,15),(3,15)])
        line([(15,9),(17,12),(15,15)],width=1);line([(18,6),(21,12),(18,18)],width=2)
    elif name in ('keyboard','input'):
        box(2,6,22,18,color,True)
        for y in (9,12):
            for x in (5,8,11,14,17):box(x,y,x+1,y,INK)
        box(7,15,17,15,INK)
    elif name in ('wired','wireless'):
        if name=='wired':
            line([(4,18),(4,14),(8,14),(8,17),(16,17),(16,12)],width=2)
            box(12,5,20,12);box(14,4,18,5,SLATE);box(14,7,18,10,INK)
        else:
            for y,x in ((5,3),(9,6),(13,9)):
                line([(x,y+2),(12,y),(24-x,y+2)],width=2)
            dot(12,18,1,color)
    elif name in ('latency_high','latency_mid','latency_low'):
        n={'latency_high':1,'latency_mid':2,'latency_low':3}[name]
        for i in range(3):
            box(3+i*7,15-i*4,7+i*7,20,color if i<n else RIM,True)
    elif name in ('rope','branch','circuit'):
        line([(4,12),(20,12)],width=2)
        for x in (4,12,20):p.diamond(x,12,3,color)
    elif name in ('heart','health'):
        shape([(12,7),(9,4),(5,4),(3,7),(3,12),(6,16),(12,21),(18,16),(21,12),(21,7),(19,4),(15,4)],INK)
        shape([(12,9),(8,6),(5,7),(5,12),(12,19),(19,12),(19,7),(16,6)])
    elif name in ('foot','footprints','move'):
        shape([(5,9),(7,7),(9,10),(9,15),(7,20),(4,19),(4,15)],color)
        shape([(13,5),(15,3),(17,6),(17,11),(15,15),(12,14),(12,11)],SLATE)
    elif name in ('thorns','danger'):
        shape([(2,18),(7,7),(9,14),(13,4),(16,14),(19,9),(22,18)],color)
        line([(2,20),(22,20)],SLATE,1)
    elif name in ('gear_limit','il_limit','enhance_limit','hero_limit'):
        line([(4,6),(20,6)],SLATE,2);p.diamond(12,14,6,color)
        if name=='il_limit':line([(12,10),(12,18)],INK,2,False)
        if name=='hero_limit':p.sparkle(12,14,4,INK)
    elif name=='floor':
        shape([(12,5),(22,10),(12,15),(2,10)],color)
        shape([(2,12),(12,17),(22,12),(22,15),(12,20),(2,15)],SLATE)
    elif name in ('leader','crown'):
        shape([(3,8),(8,12),(12,5),(16,12),(21,8),(19,18),(5,18)],color)
        p.diamond(12,13,2,INK)
    elif name in ('hat','hood'):
        shape([(3,19),(7,14),(9,6),(15,4),(18,7),(16,11),(15,14),(20,18),(20,20),(3,20)])
        line([(7,15),(16,15)],SLATE,2)
    elif name in ('ribbon','badge'):
        shape([(7,4),(17,4),(17,20),(12,17),(7,20)],color);p.diamond(12,10,3,INK)
    elif name in ('chair','sit'):
        line([(8,4),(7,13),(17,13),(17,16),(8,16),(7,19)],width=2)
        line([(6,20),(11,21),(17,20),(20,18)],width=2)
        line([(16,15),(18,19)],width=2)
        if name=='chair':line([(11,5),(10,12)],SLATE,1)
    elif name=='horn':
        shape([(5,18),(10,19),(16,17),(19,12),(18,9),(14,12),(12,8),(14,4),(8,6),(4,11)])
        line([(6,16),(10,17),(15,15)],SLATE,1)
    elif name=='feather':
        shape([(4,21),(7,15),(7,10),(12,5),(20,3),(19,10),(17,15),(13,17),(8,17)],color)
        line([(5,20),(15,8)],SLATE,1,False)
    elif name in ('key_drop','lost_input'):
        line([(2,5),(9,5)],width=2);line([(14,5),(21,5)],width=2);line([(12,7),(12,11)],SLATE,1)
        shape([(8,12),(19,10),(21,18),(10,21)],color)
        shape([(11,14),(17,13),(18,17),(12,18)],DARK)
    elif name in ('mouse_left','mouse_right','mouse_wheel'):
        shape([(8,3),(16,3),(20,7),(20,17),(17,21),(7,21),(4,17),(4,7)],INK)
        shape([(8,5),(16,5),(18,8),(18,17),(16,19),(8,19),(6,17),(6,8)],SLATE)
        if name=='mouse_left': box(7,6,11,11,CHALK)
        if name=='mouse_right': box(13,6,17,11,CHALK)
        if name=='mouse_wheel': box(11,6,13,11,CHALK)
        line([(6,12),(18,12)],INK,1,False)
    elif name=='infinity':
        line([(12,12),(8,8),(5,8),(3,10),(3,14),(5,16),(8,16),(16,8),(19,8),(21,10),(21,14),(19,16),(16,16),(12,12)],width=2)
    elif name=='flag':
        line([(6,4),(6,21)],width=2)
        shape([(7,5),(18,5),(15,9),(18,13),(7,13)])
    else:
        raise ValueError(name)
    return p.im

def campfire(size, phase):
    p=Pictogram(size)
    p.line([(5,19),(18,21)],BRASS,3)
    p.line([(7,21),(18,18)],TAN,2)
    top=[6,5,6,4,7,5][phase%6]
    lean=[-1,1,0,1,-1,-2][phase%6]
    p.shape([(5,16),(7,11),(10,13),(12+lean,top),(15,9),(15,12),(18,10),(19,17),(17,19),(8,19)],INK)
    p.shape([(7,16),(8,13),(10,15),(12+lean,top+2),(14,11),(14,15),(17,12),(18,16),(16,18),(9,18)],ELEMENTS['fire'][0])
    p.shape([(10,17),(12,12),(13,14),(15,15),(15,17),(13,18)],ELEMENTS['fire'][1])
    p.box(8+phase%3,7-phase%2,8+phase%3,7-phase%2,AMBER)
    return p.im

def kit():
    for id,name,w,h,nine,kind,brads in [
        ('01','board',48,48,16,'wood',True),('02','recess',48,48,16,'well',False),
        ('03','paper',48,48,16,'paper',False),('04','heading_strip',96,48,(24,12,24,12),'wood',True),
        ('05','screen_heading',96,80,(24,16,24,16),'wood',True),('06','control_strip',96,48,(16,8,16,8),'well',False),
        ('08','tooltip',24,24,8,'well',False),('11','toast',48,48,16,'paper',False)]:
        save('k-'+id,name,plank(w,h,kind,brads=brads),nine)
    for name,size in [('horizontal',(16,4)),('vertical',(4,16))]:
        im=canvas(*size)
        d=ImageDraw.Draw(im)
        d.line((0,1,15,1) if name=='horizontal' else (1,0,1,15),fill=RIM,width=2)
        save('k-07','divider_'+name,im)
    tail=Pictogram(12,8);tail.shape([(1,2),(23,2),(12,23)],INK);tail.shape([(6,5),(18,5),(12,18)],DARK)
    save('k-09','tooltip_tail',tail.im)
    p=Pictogram(32);p.shape([(8,23),(9,12),(15,5),(22,2),(22,9),(19,16),(12,19)],INK)
    p.shape([(10,20),(11,12),(15,6),(21,3),(20,11),(16,16),(12,16)],RIM);p.line([(8,22),(17,7)],SLATE,1,False)
    save('k-12','crow_feather',p.im)
    p=Pictogram(32);p.shape([(6,5),(9,4),(18,19),(15,21)],TAN);p.shape([(11,4),(14,3),(21,18),(18,20)],BRASS)
    p.line([(10,10),(15,9),(17,12)],SLATE,2);save('k-12','clothespin',p.im)
    p=Pictogram(32);p.line([(3,16),(8,12),(13,17),(17,8),(22,11)],CHALK,1,False);p.sparkle(18,6,3,CHALK)
    save('k-12','chalk_scrap',p.im)
    for id,name,w,h,accent,states in [
        ('20','button_large',48,64,None,('normal','hover','pressed','disabled')),
        ('21','button_medium',48,48,None,('normal','hover','pressed','disabled')),
        ('22','button_small',32,32,None,('normal','hover','pressed','disabled')),
        ('23','button_primary',48,64,CHALK,('normal','hover','pressed','disabled')),
        ('24','button_caution',48,48,AMBER,('normal','hover','pressed','disabled')),
        ('25','tab',48,48,None,('normal','hover','on','disabled')),
        ('26','tab_vertical',48,56,None,('normal','hover','on')),
        ('31','dropdown',48,40,None,('normal','hover','expanded')),
        ('33','input',48,40,None,('normal','editing','error','disabled'))]:
        for state in states:
            im=plank(w,h,'well' if id in ('31','33') else 'wood',state,AMBER if state=='error' else accent)
            if id=='31':
                im.alpha_composite(glyph(16,'up' if state=='expanded' else 'down'),(w-21,12))
            save('k-'+id,name+'_'+state,im,12 if w==48 else 8)
    for state in ('off','on','disabled'):
        im=plank(48,24,'well',state,corners=7)
        knob=plank(17,18,'wood','hover' if state=='on' else state,corners=4)
        im.alpha_composite(knob,(27 if state=='on' else 4,3))
        save('k-27','toggle_'+state,im)
    for id,name in [('28','checkbox'),('29','radio')]:
        for state in ('empty','on','disabled'):
            im=plank(24,24,'well',state) if id=='28' else canvas(24)
            if id=='29':
                d=ImageDraw.Draw(im)
                d.ellipse((2,2,21,21),fill=INK,outline=SLATE if state!='disabled' else RIM,width=2)
                if state=='on': d.ellipse((7,7,16,16),fill=CHALK)
            elif state=='on':
                im.alpha_composite(glyph(18,'check'),(3,2))
            save('k-'+id,name+'_'+state,im)
    for id,name,dimensions in [('30','slider',(16,8)),('34','scroll',(8,16)),('36','progress',(16,16))]:
        w,h=dimensions
        save('k-'+id,name+'_rail',plank(w,h,'well',corners=2),min(w//3,h//3))
        if id!='34':
            save('k-'+id,name+'_fill',fillbar(w,h,SLATE),(w//4,h//4,w//4,h//4))
        if id!='36':
            for state in ('normal','hover','pressed'):
                save('k-'+id,name+'_thumb_'+state,plank(w,24,'wood',state,corners=2),min(w//3,6) if id=='34' else None)
    for id,name,size,icon,states in [
        ('32','step',24,'left',('normal','hover','disabled')),
        ('35','close',32,'x',('normal','hover','pressed')),
        ('38','page',32,'left',('normal','hover','disabled'))]:
        for state in states:
            im=plank(size,size,'wood',state,corners=4) if id!='32' else canvas(size)
            shade=RIM if state=='disabled' else SLATE if id=='32' and state=='normal' else CHALK
            sym=glyph(size-6,icon,shade)
            im.alpha_composite(sym,(3,5 if state=='pressed' else 3))
            save('k-'+id,name+'_'+state,im)
    for size in (16,24,32):
        corner=[]
        for _ in (0,2):
            p=canvas(size);d=ImageDraw.Draw(p)
            arm=size-4
            # The second frame translates the identical corner 2px away from the target at runtime.
            d.rectangle((0,0,7,arm+3),fill=INK)
            d.rectangle((0,0,arm+3,7),fill=INK)
            d.rectangle((2,2,5,arm+1),fill=YELLOW)
            d.rectangle((2,2,arm+1,5),fill=YELLOW)
            corner.append(p)
        save('k-40','selection_corner',corner,interval=250,metadata={'frame_offsets':[[-4,-4],[-6,-6]],'flip_for_other_corners':True})
    for size in (16,24):
        save('k-41','confirmation',glyph(size,'check'))
        save('k-43','lock',glyph(size,'lock'))
    dots=[]
    for radius in (3,4):
        d=Pictogram(12);d.circle(12,12,radius+3,INK);d.circle(12,12,radius+2,YELLOW)
        dots.append(d.im)
    save('k-42','unread_dot',dots,interval=250)
    save('k-44','loading_campfire',[campfire(32,i) for i in range(6)],interval=100)
    save('k-45','count_tag',plank(16,16,'well',corners=3),4)
    save('k-46','label',plank(24,16,'wood',corners=4),4)
    for id,size in [('50',24),('51',48)]:
        for state in ('normal','pressed'):
            save('k-'+id,'keycap_'+state,plank(size,size,'wood',state,SLATE,corners=4),size//4)
    for icon in ('mouse_left','mouse_right','mouse_wheel'):
        save('k-52',icon,glyph(24,icon))
    for size in (16,24):
        for name in ('forward','back','side','reverse_side'):
            icon={'forward':'up','back':'back','side':'right','reverse_side':'left'}[name]
            im=glyph(size,icon)
            if name=='reverse_side':
                d=ImageDraw.Draw(im);d.rectangle((size-5,size-6,size-3,size-4),fill=SLATE)
            save('k-53','direction_'+name,im)
        save('k-54','input_k',glyph(size,'K'))
        save('k-54','input_just',glyph(size,'spark'))
    for name in ('connected','gap'):
        im=canvas(8,16);d=ImageDraw.Draw(im);d.rectangle((0,7,7,8),fill=INK)
        d.rectangle((0,7,7 if name=='connected' else 2,7),fill=CHALK)
        if name=='gap':d.line((5,7,7,7),fill=SLATE)
        save('k-55','input_link_'+name,im)
    for id,name,size,variants in [
        ('60','item_slot',64,('empty','filled','locked','unavailable')),
        ('61','small_slot',32,('empty','filled')),
        ('62','wand_slot',80,('empty','filled','expedition_locked')),
        ('63','core_slot',72,('empty','filled')),
        ('64','extension_slot',64,('closed','open')),
        ('80','mutation_slot',48,('empty','filled','next_to_replace')),
    ]:
        for state in variants:
            c=LIME if id=='80' and state!='empty' else GOLD if id=='64' else SLATE if id=='63' else None
            im=plank(size,size,'wood' if state in ('filled','open') else 'well',accent=c,corners=6)
            if state in ('closed','next_to_replace','unavailable'):
                im.alpha_composite(frame(size,size,c or SLATE,2,'dash',4))
            if state in ('locked','expedition_locked','unavailable'):
                im.alpha_composite(glyph(24,'blocked' if state=='unavailable' else 'lock'),(size-28,size-28))
            if id=='63': im.alpha_composite(glyph(16,'tri_up'),(28,0))
            save('k-'+id,name+'_'+state,im)
    for state in ('normal','flow'):
        seq=[]
        for i in range(2 if state=='flow' else 1):
            im=canvas(16,8);d=ImageDraw.Draw(im)
            d.line((0,3,15,3),fill=INK,width=4);d.line((0,3,15,3),fill=SLATE,width=2)
            if state=='flow':d.line((2+i*7,3,5+i*7,3),fill=CHALK,width=2)
            seq.append(im)
        save('k-65','circuit_link_'+state,seq,interval=120 if len(seq)>1 else None)
    for id,size in [('66',64),('67',32),('68',64)]:
        for state,c in [('normal',SILVER),('rare',BLUE),('hero',GOLD)]:
            save('k-'+id,'rarity_'+state,frame(size,size,c,2,state,3),24 if id=='68' else None)
    save('k-69','borrowed_slot',frame(64,64,SLATE,2,'dash',3))
    save('k-69','borrowed_card',frame(64,64,SLATE,2,'dash',4),16)
    save('k-70','hud_circuit_off',plank(40,40,'well'))
    save('k-70','hud_circuit_on',[plank(40,40,'wood','on',c) for c in (CHALK,SLATE)],interval=110)
    for size in (16,24):save('k-71','assembly_warning',glyph(size,'warning',AMBER))
    save('k-73','element_dot',glyph(8,'diamond'))
    for name in ('gain','cost'):save('k-81','mutation_'+name,glyph(16,'tri_up' if name=='gain' else 'tri_down'))

def block_plate(size,kind):
    p=Pictogram(size)
    if kind=='projectile':
        outer=[(8,2),(16,2),(22,8),(22,16),(16,22),(8,22),(2,16),(2,8)]
        inner=[(8,3),(16,3),(21,8),(21,16),(16,21),(8,21),(3,16),(3,8)]
    elif kind=='control':
        outer=[(7,2),(17,2),(23,12),(17,22),(7,22),(1,12)]
        inner=[(7,3),(17,3),(22,12),(17,21),(7,21),(2,12)]
    else:
        outer=[(5,2),(19,2),(22,5),(22,19),(19,22),(5,22),(2,19),(2,5)]
        inner=[(5,3),(19,3),(21,5),(21,19),(19,21),(5,21),(3,19),(3,5)]
    p.shape(outer,INK);p.shape(inner,WOOD)
    p.line(inner[:2],RIM,1,False)
    return p.im

BLOCKS=[
    ('magic_bolt','arcane','projectile'),('fireball','fire','projectile'),('chain_lightning','lightning','projectile'),
    ('boomerang','arcane','projectile'),('ice_spike','frost','projectile'),('shadow_orb','shadow','projectile'),
    ('split_two','neutral','control'),('split_three','neutral','control'),('on_hit_trigger','neutral','control'),
    ('wall_bounce','neutral','modifier'),('double_size','fire','modifier'),('pierce','arcane','modifier'),
    ('frost','frost','modifier'),('homing','lightning','modifier'),('triple_heat','fire','modifier'),('corrosion','shadow','modifier'),
]

def block_icon(size,name,elem,kind):
    main,light=ELEMENTS[elem] if elem!='neutral' else (CHALK,SLATE)
    p=Pictogram(size,fill=main)
    p.im=block_plate(size,kind);p.d=ImageDraw.Draw(p.im)
    line,shape,circle,ring=p.line,p.shape,p.circle,p.ring
    if name=='magic_bolt':
        line([(5,16),(9,13),(13,10)],light,2);p.diamond(14,9,5,main);p.diamond(14,9,2,light)
    elif name=='fireball':
        p.im.alpha_composite(draw_element(round(size*.78),'fire'),(round(size*.09),round(size*.065)))
    elif name=='chain_lightning':
        line([(5,14),(10,8),(14,13),(19,6)],main,2)
        for x,y in [(5,14),(10,8),(19,6)]:circle(x,y,1,light)
    elif name=='boomerang':
        shape([(5,13),(10,5),(13,5),(18,13),(16,16),(11,10),(7,17)],main)
        line([(6,18),(12,19),(17,16)],light,1)
    elif name=='ice_spike':
        shape([(5,16),(8,9),(16,5),(19,8),(16,14),(8,18)],main)
        shape([(5,16),(8,9),(16,5),(12,11)],light)
        line([(7,17),(17,9)],'#5296c8',1,False)
    elif name=='shadow_orb':
        circle(12,11,7,light);circle(12,11,5,'#2a1a5e');p.diamond(12,11,2,'#f1e9ff')
    elif name.startswith('split'):
        line([(12,19),(12,12),(6,6)],main,2)
        line([(12,12),(18,6)],main,2)
        if name=='split_three':line([(12,12),(12,4)],main,2)
    elif name=='on_hit_trigger':
        line([(7,5),(5,7),(5,10),(3,12),(5,14),(5,17),(7,19)],light,1)
        ring(11,13,3,main,1)
        line([(11,13),(18,6)],main,2);shape([(14,6),(19,5),(18,10)],main)
    elif name=='wall_bounce':
        line([(6,5),(6,19)],light,2)
        line([(17,17),(9,12),(17,7)],main,2);shape([(13,5),(19,5),(18,10)])
    elif name=='double_size':
        ring(12,11,7,main,2);circle(12,11,2,light)
    elif name=='pierce':
        ring(8,13,4,main,1);ring(15,10,4,main,1)
        line([(5,15),(18,7)],light,2);shape([(14,6),(19,6),(18,11)],light)
    elif name=='frost':
        for coords in [[(6,6),(17,17)],[(17,6),(6,17)],[(11,4),(11,19)]]:
            line(coords,main,2)
        circle(11,11,2,light)
    elif name=='homing':
        circle(16,9,3,main);line([(5,18),(5,13),(9,9),(14,9)],light,2)
        shape([(11,6),(16,9),(11,12)],light)
    elif name=='triple_heat':
        shape([(4,7),(17,7),(20,12),(17,17),(4,17)],'#a04a25')
        for x in (5,9,13):line([(x,9),(x+3,12),(x,15)],CHALK,1)
    elif name=='corrosion':
        circle(12,10,6,main)
        shape([(5,16),(7,13),(10,15),(12,12),(14,16),(17,13),(19,17),(18,20),(6,20)],'#392754')
        p.box(7,10,8,11,light);p.box(14,13,15,14,light)
    if elem!='neutral':
        badge=round(size*.28)
        p.im.alpha_composite(draw_element(badge,elem),(size-badge-1,size-badge-1))
    return p.im

def technique(size,name,elem=None,ultimate=False):
    main,hi=ELEMENTS[elem] if elem else (CHALK,SLATE)
    p=Pictogram(size,fill=main)
    line,shape,circle,ring=p.line,p.shape,p.circle,p.ring
    if name=='burst':
        ring(12,12,8,main,2);circle(12,12,2,hi)
    elif name=='recoil':
        line([(12,13),(19,6)],main,2);shape([(15,6),(20,5),(19,10)])
        for x,y in [(6,18),(8,14)]:p.box(x-1,y-1,x+1,y+2,hi)
    elif name=='fan':
        for x in (4,12,20):line([(12,19),(x,6)],main,2)
        line([(4,6),(8,4),(16,4),(20,6)],hi,1)
    elif name=='wings':
        shape([(11,16),(5,14),(3,8),(5,7),(9,10),(12,14)])
        shape([(13,16),(19,14),(21,8),(19,7),(15,10),(12,14)])
    elif name=='afterimage':
        for x,y,c in [(6,9,RIM),(12,9,SLATE),(18,9,main)]:
            circle(x,y-2,2,c);shape([(x-1,y+1),(x+2,y+1),(x+3,y+6),(x+1,y+8),(x-3,y+8)],c)
    elif name in ('spear','thunder_spear'):
        p.im.alpha_composite(glyph(size,'spear',main))
        if name=='thunder_spear':p.sparkle(15,7,5,CHALK)
    elif name=='vent':
        line([(5,14),(5,8),(10,5),(14,5)],main,2);line([(19,12),(18,17),(14,19),(9,19)],main,2)
        for pts in [[(3,7),(1,5)],[(18,5),(21,3)],[(20,18),(23,20)],[(5,19),(2,21)]]:
            line(pts,hi,2)
        p.diamond(12,12,3,main)
    elif name=='octastar':
        p.sparkle(12,12,9,main)
        shape([(6,6),(12,9),(18,6),(15,12),(18,18),(12,15),(6,18),(9,12)],hi)
        p.diamond(12,12,3,main)
    elif name=='bloom':
        for x,y in [(12,6),(17,9),(17,15),(12,18),(7,15),(7,9)]:
            circle(x,y,3,main)
        ring(12,12,4,hi,2);circle(12,12,1,main)
    elif name=='kiln':
        shape([(4,17),(4,10),(7,6),(17,6),(20,10),(20,17)],main)
        shape([(7,17),(7,11),(10,9),(14,9),(17,11),(17,17)],INK)
        line([(6,13),(9,13)],hi,1,False);line([(15,13),(18,13)],hi,1,False)
        for x in (4,9,14,19):p.box(x,19,x+2,19,main)
    elif name=='bolt_dash':
        line([(3,13),(9,13),(13,7),(13,12),(20,12)],main,3);shape([(17,8),(22,12),(17,16)],hi)
    elif name=='copy_circle':
        ring(9,12,6,hi,1);ring(15,12,6,main,2)
        p.diamond(9,12,1,hi);p.diamond(15,12,1,main)
    elif name=='ice_screen':
        for i,x in enumerate((3,8,13,18)):
            y=5 if i%2==0 else 7
            shape([(x,y),(x+4,y+2),(x+4,17),(x,15)],main)
            line([(x,y),(x+4,y+2)],hi,1,False)
    elif name=='shadow_hand':
        p.shape([(3,17),(7,15),(18,15),(21,17),(19,20),(5,20)],'#452e73')
        p.shape([(8,17),(6,12),(7,10),(10,12),(9,7),(11,6),(12,11),(12,5),(14,5),(14,11),(16,7),(18,8),(16,15),(14,18)],main)
        line([(11,16),(13,13),(16,11)],hi,1,False)
    if ultimate:
        p.sparkle(4,4,2,CHALK);p.sparkle(20,20,2,CHALK)
    return p.im

def material(size,name):
    p=Pictogram(size)
    if name in ('stone_low','stone_mid','stone_high'):
        if name=='stone_low':
            outer=[(3,16),(5,9),(11,4),(18,6),(21,15),(18,20),(7,20)]
            inner=[(5,15),(7,9),(11,6),(17,8),(19,15),(16,18),(8,18)]
            c,hi='#646778','#a2a5a9'
            cracks=[[(8,12),(12,10),(14,11),(15,8)]]
        elif name=='stone_mid':
            outer=[(4,8),(11,3),(18,5),(21,11),(19,18),(12,21),(5,18)]
            inner=[(6,9),(11,5),(17,7),(19,11),(17,17),(12,19),(7,17)]
            c,hi='#656880','#b3b5c0'
            cracks=[[(9,10),(12,7),(15,8)],[(9,14),(12,12),(15,13)]]
        else:
            outer=[(12,2),(19,6),(22,12),(16,20),(8,20),(2,12),(5,6)]
            inner=[(12,4),(18,8),(20,12),(15,18),(9,18),(4,12),(6,8)]
            c,hi='#a88f71','#f4e8c8'
            cracks=[[(8,9),(12,6),(15,8)],[(8,12),(12,9),(15,11)],[(8,15),(12,12),(15,14)]]
        p.shape(outer,INK);p.shape(inner,c)
        p.shape([inner[0],inner[1],inner[2],(12,13)],hi)
        if name!='stone_low':p.line([(6,15),(11,18),(17,16)],PALE_BRASS if name=='stone_mid' else GOLD,2)
        for pts in cracks:p.line(pts,CHALK,1,False)
    elif name=='safeguard_charm':
        p.shape([(7,2),(16,2),(19,5),(18,21),(11,20),(6,22),(5,7)],INK)
        p.shape([(8,4),(15,4),(17,6),(16,19),(11,18),(8,20),(7,8)],PAPER)
        p.line([(9,10),(14,8),(11,13),(15,13),(10,16)],BRASS,1,False)
        p.box(10,3,13,5,TAN)
    elif name in ('material_bundle','crow_bundle','expedition_purse'):
        p.shape([(7,4),(16,4),(14,9),(19,12),(21,18),(17,21),(7,21),(3,18),(5,12),(10,9)],INK)
        p.shape([(8,5),(15,5),(13,9),(18,13),(19,18),(16,20),(8,20),(5,18),(7,13),(11,9)],TAN)
        p.shape([(8,19),(7,15),(9,12),(11,11),(10,16),(12,20)],BRASS)
        p.line([(8,9),(16,9)],CHALK,2)
        if name=='crow_bundle':
            p.shape([(13,9),(15,4),(22,2),(21,6),(17,9)],RIM);p.line([(13,10),(20,4)],SLATE,1,False)
        elif name=='expedition_purse':
            p.circle(18,17,4,INK);p.circle(18,17,3,GOLD);p.box(17,15,18,17,CHALK)
    elif name=='season_fabric':
        p.shape([(5,6),(16,4),(21,8),(19,18),(8,21),(4,16)],INK)
        p.shape([(6,7),(16,5),(19,8),(18,17),(8,19),(5,15)],'#5d486f')
        for i,c in enumerate(('#f2a33a','#cfbb62','#76a772','#70a7c6','#aa80b8')):
            p.line([(7+i*2,8),(7+i*2,14)],c,2,False)
        p.line([(8,18),(16,16)],PAPER,1,False)
    elif name=='wand_box':
        p.shape([(3,12),(16,5),(21,8),(21,13),(8,20),(3,17)],INK)
        p.shape([(4,12),(16,6),(20,8),(8,15)],TAN)
        p.shape([(8,15),(20,8),(20,12),(8,19)],BRASS)
        p.line([(10,9),(14,12),(14,15)],PAPER,2,False)
    elif name in ('gold','season_coin'):
        color=GOLD if name=='gold' else PALE_BRASS
        p.circle(12,12,9,INK);p.circle(12,12,8,BRASS);p.circle(12,11,7,color)
        p.ring(12,11,5,'#a9763f',1)
        if name=='gold':
            p.line([(10,8),(10,13),(15,13),(15,15),(9,15)],'#8c5e36',1,False)
            p.line([(9,16),(14,16),(17,14)],'#8c5e36',1,False)
        else:
            p.box(10,9,14,13,INK);p.box(11,10,13,12,DARK)
        p.box(7,7,9,7,CHALK)
    elif name=='starlight':
        p.sparkle(12,12,10,INK);p.sparkle(12,12,8,CHALK)
        p.diamond(12,12,3,'#ffffff')
    return p.im

def league(size,name,overlay=False):
    p=Pictogram(size)
    rim,base,light={'sprout':(TAN,'#4a3b31','#b4c782'),
        'beast':(SLATE,'#404758','#d7d7ca'),'heir':(GOLD,'#815f37','#fff0a4')}[name]
    if overlay:
        for i in range(4):
            y=8+i*3;x=3 if i<2 else 4+i
            for side in (1,-1):
                lx=x if side==1 else 24-x
                p.shape([(lx,y-2),(lx+2*side,y),(lx+2*side,y+2),(lx,y+1)],light)
        return p.im
    p.shape([(4,3),(20,3),(22,5),(21,16),(17,20),(12,23),(7,20),(3,16),(2,5)],INK)
    p.shape([(5,4),(19,4),(20,6),(19,15),(16,19),(12,21),(8,19),(5,15),(4,6)],rim)
    p.shape([(6,6),(18,6),(18,14),(15,18),(12,19),(9,18),(6,14)],base)
    if name=='sprout':
        p.line([(12,16),(12,12),(9,9)],light,2,False)
        p.shape([(12,13),(13,9),(16,8),(17,8),(16,11),(13,13)],light)
        p.shape([(11,12),(8,12),(7,10),(7,8),(10,8),(12,11)],light)
    elif name=='beast':
        p.shape([(6,8),(10,9),(10,13),(8,16)],light)
        p.shape([(18,8),(14,9),(14,13),(16,16)],light)
        p.line([(13,8),(11,14)],rim,1,False)
    else:
        p.line([(9,8),(9,14),(15,14),(15,16),(8,16)],light,2,False)
        p.line([(8,17),(13,18),(17,16)],light,1,False)
        p.sparkle(15,9,3,light)
    return p.im

def emote(size,name):
    p=Pictogram(size)
    settings={
        'greet':(((7,10),(4,7),(3,4)),((16,10),(20,13)),((9,16),(7,21)),((15,16),(17,21))),
        'clap':(((7,10),(9,12),(11,9)),((17,10),(15,12),(13,9)),((9,16),(8,21)),((15,16),(16,21))),
        'taunt':(((7,10),(5,8),(3,10)),((17,10),(19,8),(21,10)),((9,16),(8,21)),((15,16),(16,21))),
        'bow':(((8,11),(12,12),(17,14)),((12,10),(16,11),(19,13)),((8,15),(6,20)),((11,15),(11,20))),
        'sit':(((8,11),(7,15),(12,16)),((16,11),(17,15),(12,16)),((9,16),(5,18),(8,20)),((15,16),(19,18),(16,20))),
        'dance':(((8,10),(5,6)),((16,10),(20,6)),((9,16),(7,20)),((15,16),(19,16),(18,20))),
        'raise_wand':(((7,10),(4,13)),((17,10),(19,6),(18,3)),((9,16),(8,21)),((15,16),(16,21))),
        'despair':(((8,10),(5,12),(8,6)),((16,10),(19,12),(16,6)),((9,16),(7,21)),((15,16),(18,21))),
    }
    if name=='bow':
        p.circle(17,7,3,CHALK,True);p.shape([(7,10),(13,8),(15,12),(11,17),(6,16)],SLATE)
    else:
        p.circle(12,6,3,CHALK,True);p.shape([(8,10),(16,10),(17,17),(7,17)],SLATE)
    for points in settings[name]:p.line(points,CHALK,2)
    if name=='raise_wand':p.line([(17,5),(21,2)],TAN,2);p.sparkle(21,2,2,CHALK)
    if name in ('clap','dance','greet'):p.sparkle(12 if name=='clap' else 20 if name=='dance' else 3,4 if name!='clap' else 3,2,CHALK)
    return p.im

def passive(name, element):
    c,hi=ELEMENTS[element]
    p=Pictogram(48,fill=c)
    if name=='overflow':
        p.shape([(3,11),(21,11),(20,17),(17,21),(7,21),(4,17)],INK)
        p.shape([(5,12),(19,12),(18,17),(16,19),(8,19),(6,17)],c)
        p.line([(4,11),(19,11)],hi,2,False)
        p.shape([(7,10),(10,5),(12,8),(14,4),(17,8),(16,11),(12,10)],hi)
        p.sparkle(19,7,2,hi)
    elif name=='lightning_rod':
        p.line([(4,20),(19,20)],SLATE,2)
        p.line([(9,19),(12,9),(16,8)],hi,2)
        p.shape([(18,2),(13,6),(17,6),(14,10),(21,5),(17,5),(20,2)],c)
        p.line([(5,12),(9,10)],c,1);p.line([(8,16),(12,14)],c,1)
    elif name=='underline':
        p.shape([(4,4),(17,4),(20,8),(20,19),(4,19)],INK)
        p.shape([(6,6),(16,6),(18,8),(18,17),(6,17)],'#2b5964')
        p.line([(8,9),(15,9)],hi,1,False)
        p.line([(8,12),(13,12)],hi,1,False)
        p.line([(5,17),(20,17)],c,2)
        p.sparkle(19,16,3,hi)
    elif name=='thin_ice':
        p.shape([(2,13),(12,7),(22,13),(12,20)],INK)
        p.shape([(4,13),(12,9),(20,13),(12,18)],c)
        p.line([(5,13),(12,11),(13,14),(17,14)],hi,1,False)
        p.line([(13,14),(11,16),(12,18)],'#5296c8',1,False)
        p.sparkle(16,7,3,hi)
    else:
        p.shape([(4,4),(18,4),(20,7),(20,18),(17,21),(5,21),(5,18),(7,17),(7,8),(4,7)],INK)
        p.shape([(6,5),(17,5),(18,7),(18,17),(8,17),(8,7)],'#594587')
        p.line([(10,8),(15,8)],hi,1,False)
        p.line([(10,11),(15,11)],hi,1,False)
        p.circle(15,16,4,c,True)
        p.diamond(15,16,2,hi)
    return p.im

def participation_badge(size):
    p=Pictogram(size,fill=PALE_BRASS)
    p.shape([(7,3),(17,3),(21,7),(21,16),(17,21),(7,21),(3,16),(3,7)],INK)
    p.shape([(8,4),(16,4),(20,8),(20,15),(16,20),(8,20),(4,15),(4,8)])
    p.shape([(9,6),(15,6),(18,9),(18,14),(15,18),(9,18),(6,14),(6,9)],BRASS)
    p.shape([(8,14),(10,10),(12,11),(13,7),(16,12),(16,15),(14,17),(10,17)],PAPER)
    p.diamond(12,15,2,TAN)
    return p.im

def icons():
    for id,size in [('i-b1',48),('i-b2',24)]:
        for name,elem,kind in BLOCKS: save(id,'block_'+name,block_icon(size,name,elem,kind),metadata={'element':elem,'block_kind':kind})
        for kind in ('projectile','control','modifier'):save('i-b3','plate_'+kind,block_plate(size,kind))
    for id,size in [('i-e1',32),('i-e2',64),('i-e3',16)]:
        for name in ELEMENTS:
            if id=='i-e2' and name=='neutral':
                continue
            shared = {'shared_with': ['effects.md §20.3', 'monsters.md §9.5']} if size==16 and name!='neutral' else None
            save(id,'element_'+name,draw_element(size,name),metadata=shared)
    for name,c in [('weakness',SLATE),('resistance',RIM)]:
        save('i-e4',name,plank(24,24,'well',accent=c),8)
    for i,(name,c) in enumerate([('0_to_3',SLATE),('4_to_6',SLATE),('7_to_9',SLATE),('10_to_12',CHALK),('13',GOLD)]):
        im=frame(80,80,c,2,'hero' if i==4 else 'normal',4)
        d=ImageDraw.Draw(im)
        for x,y in [(8,8),(68,68),(8,68),(68,8)][:0 if i==0 else 2 if i==1 else 4 if i<4 else 0]:
            brad(d,x,y)
        save('i-g1','wand_tier_'+name,im,metadata={'tint_edge':i==3})
    im=draw_element(24,'neutral')
    p=Pictogram(24);p.im=im;p.d=ImageDraw.Draw(im);p.line([(17,3),(15,8),(17,11),(14,13)],CHALK,2)
    save('i-g2','scar',im)
    for id,size in [('i-l1',24),('i-l2',48),('i-l3',128)]:
        for name in ('sprout','beast','heir'):
            save(id,'league_'+name,league(size,name))
            save('i-l4','league_laurel_'+name,league(size,name,True))
        if size<128:
            save('i-l5','season_participation',participation_badge(size))
    save('i-l6','promotion',[glyph(32,'up',c) for c in (CHALK,SLATE)],interval=250)
    save('i-l7','heir',glyph(32,'chair',GOLD));save('i-l7','disciple',glyph(32,'cloak'))
    for i,c in enumerate((GOLD,SILVER,PALE_BRASS),1):save('i-l8',f'place_{i}',glyph(32,f'rank_{i}',c))
    for name,g,c in [('up','tri_up',CHALK),('down','tri_down',SLATE),('flat','minus',SLATE)]:save('i-l9','rank_'+name,glyph(16,g,c))
    techniques=[
        ('burst',None),('recoil',None),('fan',None),('wings',None),('afterimage',None),
        ('spear',None),('vent',None),('octastar',None),('thunder_spear',None),('bloom',None),
        ('kiln','fire'),('bolt_dash','lightning'),('copy_circle','arcane'),('ice_screen','frost'),('shadow_hand','shadow'),
    ]
    for id,size in [('i-t1',48),('i-t2',24)]:
        for name,elem in techniques:save(id,name,technique(size,name,elem,name in ('thunder_spear','bloom')),metadata={'element':elem,'tint':elem is None})
    for state,c in [('empty',SLATE),('filled',CHALK)]:
        p=Pictogram(8);p.ring(12,12,6,c,2)
        if state=='filled':p.circle(12,12,5,c)
        save('i-t3','difficulty_'+state,p.im)
    for name in ('diamond','debt'):save('i-t4',name,glyph(16,name))
    for id,size in [('i-m1',48),('i-m2',24)]:
        for name in ('stone_low','stone_mid','stone_high'):save(id,name,material(size,name))
    for id,name,sizes in [
        ('i-m3','safeguard_charm',(24,48)),('i-m4','material_bundle',(48,)),
        ('i-m4','crow_bundle',(48,)),('i-m5','season_fabric',(24,48)),('i-m6','wand_box',(48,))
    ]:
        for size in sizes:save(id,name,material(size,name))
    for name,g in [
        ('headwear','hat'),('cloak','cloak'),('footprints','footprints'),('seat','chair'),('wand','wand'),('trail','spear'),
        ('kill_effect','spark'),('ritual','gear'),('emote','talk'),('border','copy'),('crest','badge'),('title','flag')
    ]:
        save('i-m7','cosmetic_'+name,glyph(32,g))
    for name,g in [('library_expansion','library'),('rename','pencil')]:save('i-m9',name,glyph(48,g))
    for id,size in [('i-c1',16),('i-c2',24),('i-c3',48)]:
        for name in ('gold','expedition_purse','season_coin','starlight'):save(id,name,material(size,name))
    for name,g in [
        ('health','heart'),('move_speed','foot'),('stance_speed','refresh'),('crystal_cap','gear_limit'),
        ('combo_per_crystal','diamond'),('cooling','down'),('heat_threshold','up'),('lock_time','lock'),
        ('graze','target'),('link_slots','circuit'),('crystal_loss','debt')
    ]:save('i-s1',name,glyph(24,g))
    for name,elem in zip(
        ('overflow','lightning_rod','underline','thin_ice','shadow_contract'),
        list(ELEMENTS)[:5],
    ):
        save('i-s2',name,passive(name,elem),metadata={'element':elem})
    for name,g in [('heat_at_80','up'),('three_mutations','branch'),('depth_decay','down'),('keep_crystal','diamond'),('on_credit','debt')]:
        save('i-s3',name,glyph(48,g))
    for name,c in [('rail',DARK),('fill',SLATE)]:save('i-s4','stat_'+name,fillbar(16,8,c),(4,2,4,2))
    save('i-s5','item_level',glyph(24,'il'))
    for id,size,names in [
        ('i-f1',32,'expedition duel square enhance library training record settings wardrobe shop mail ranking season friends market exit'),
        ('i-f2',24,'search sort filter refresh back plus minus info warning question eye eye_hidden clock hourglass play pause spectate copy trash pencil pin'),
        ('i-f3',24,'whisper friend_add person blocked report trade duel follow inspect duel_invite'),
        ('i-f4',16,'online offline expedition duel trade idle duel_wait spectate'),
        ('i-f5',16,'online broken hammer'),('i-f6',24,'wired wireless latency_low latency_mid latency_high'),
        ('i-f7',32,'mail mail_open mail_attach mail_claimed'),
        ('i-f8',16,'talk tent trade whisper friends duel paper'),
        ('i-f9',16,'lock eye gear_limit expedition thorns rounds clock heart enhance_limit il_limit spark hero_limit floor'),
        ('i-f11',32,'display audio keyboard duel whisper person')
    ]:
        for name in names.split():
            c={'warning':AMBER,'latency_high':AMBER,'latency_mid':YELLOW,'latency_low':GREEN}.get(name,CHALK)
            save(id,name,glyph(size,name,c))
    for name in ('greet','clap','taunt','bow','sit','dance','raise_wand','despair'):
        save('i-f10',name,emote(48,name))
    for name in ('offline','server'):
        im=plank(160,40,'paper');p=Pictogram(32)
        if name=='offline':p.im=glyph(32,'paper',BRASS)
        else:
            p.shape([(2,17),(22,17),(22,21),(2,21)],FOLD)
            p.shape([(5,12),(11,9),(13,5),(17,4),(20,6),(23,6),(20,8),(20,11),(16,16),(8,16),(3,18)],WOOD)
            p.box(18,6,18,6,PAPER)
            p.line([(11,15),(10,18),(15,18),(15,15)],BRASS,1,False)
        im.alpha_composite(p.im,(7,3))
        save('i-f12','profile_'+name,im)
    for name,g,c in [('record','spark',CHALK),('pending','hourglass',SLATE),('verified','check',GREEN),('deadline','clock',AMBER)]:
        im=plank(96,32,'wood',accent=c,corners=5)
        im.alpha_composite(glyph(22,g,c),(6,4));save('i-f13',name,im)

def digit_row(w,h,chars):
    f=ImageFont.truetype(str(ROOT/'font/neodgm.ttf'),16)
    rows=[]
    for ch in chars:
        im=canvas(10,16);mask=Image.new('1',im.size)
        d=ImageDraw.Draw(mask)
        if ch=='−':d.line((2,8,7,8),fill=1,width=2)
        else:d.text((5,12),ch,font=f,anchor='ms',fill=1)
        im.paste(CHALK,(0,0),mask)
        rows.append(im.resize((w,h),Image.Resampling.NEAREST))
    out=canvas(w*len(rows),h)
    for i,im in enumerate(rows):out.alpha_composite(im,(i*w,0))
    return out

def bitmap_text(text,size,color=CHALK,source='Galmuri11.ttf'):
    f=ImageFont.truetype(str(ROOT/'font'/source),size)
    bbox=f.getbbox(text)
    im=canvas(bbox[2]-bbox[0],bbox[3]-bbox[1]);mask=Image.new('1',im.size)
    ImageDraw.Draw(mask).text((-bbox[0],-bbox[1]),text,font=f,fill=1)
    im.paste(color,(0,0),mask)
    return im

def fonts():
    chars='0123456789,.:%+−×/'
    for id,w,h,seq in [('f-n1',10,16,chars),('f-n2',20,32,chars),('f-n3',80,128,'0123456789%')]:
        save(id,'numbers',digit_row(w,h,seq),metadata={'cell_width':w,'cell_height':h,'characters':seq})
    im=plank(64,32,'paper');txt=bitmap_text('상한',12,'#2b2a33')
    im.alpha_composite(txt,((64-txt.width)//2,(32-txt.height)//2))
    save('f-n4','probability_cap',im)
    chars=''.join(map(chr,range(32,127)))+''.join(map(chr,range(0x3131,0x3164)))+'…·—−×÷▲▼◀▶●○◆◇★→←'+''.join(map(chr,range(0xac00,0xd7a4)))
    for id,sz,source in [('f-12',12,'Galmuri11.ttf'),('f-16',16,'neodgm.ttf')]:
        font=ImageFont.truetype(str(ROOT/'font'/source),sz)
        cmap=TTFont(ROOT/'font'/source).getBestCmap()
        image=canvas(2048);cell=sz+2;columns=2048//cell
        condensed=ImageFont.truetype(str(ROOT/'font/Galmuri11-Condensed.ttf'),sz) if sz==12 else font
        advances=[];offsets=[];baseline=sz-3 if sz==16 else 11
        for i,ch in enumerate(chars):
            mask=Image.new('1',(sz,sz));d=ImageDraw.Draw(mask)
            width=sz if ord(ch)>=0x3131 else sz//2
            y=baseline
            if ord(ch) in cmap:
                # Leave the atlas gutters transparent, and record the original baseline if the glyph overhangs.
                glyph_font=condensed if ch.isascii() else font
                left,top,right,bottom=glyph_font.getbbox(ch,anchor='ls')
                if not ch.isascii():
                    width=max(width,right-left)
                x=max(-left,round((width-(right-left))/2)-left)
                y=min(baseline,sz-bottom)
                d.text((x,y),ch,font=glyph_font,anchor='ls',fill=1)
            elif ch in '−★→←':
                if ch=='−':d.line((1,8,6,8),fill=1,width=2)
                elif ch=='★':
                    d.polygon([(8,3),(9,6),(13,6),(10,9),(11,13),(8,11),(5,13),(6,9),(3,6),(7,6)],fill=1);width=sz
                else:
                    d.line([(2,8),(12,8)],fill=1,width=2)
                    if ch=='→':d.line([(9,5),(12,8),(9,11)],fill=1,width=2)
                    else:d.line([(5,5),(2,8),(5,11)],fill=1,width=2)
                    width=sz
            else:
                raise ValueError((source,ch))
            if ch!=' ' and not mask.getbbox():
                raise ValueError((source,ch,'empty glyph'))
            x=1+(i%columns)*cell;y=1+(i//columns)*cell
            image.paste(CHALK,(x,y,x+sz,y+sz),mask)
            advances.append(width)
            offsets.append(baseline-y)
        save(id,'hangul_atlas',image,metadata={'cell':cell,'columns':columns,'baseline':baseline,'glyph_map':id+'_glyphs.json'})
        glyph_meta={'pixel_size':sz,'source':source,'columns':columns,'cell_size':cell,'gutter':1,'baseline':baseline,'characters':chars,'advances':advances,'glyph_vertical_offsets':offsets}
        (ROOT/'font'/f'{id}_glyphs.json').write_text(json.dumps(glyph_meta,ensure_ascii=False),encoding='utf-8')
