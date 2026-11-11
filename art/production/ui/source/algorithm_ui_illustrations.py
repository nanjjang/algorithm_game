"""Authored landscapes and props. Compose on a fixed pixel grid with nearest-neighbor export."""
from algorithm_ui_kit import canvas, save, campfire, Image, ImageDraw, ImageOps, random, math, INK

SKIES = {
    'dusk': ('#1e243e','#292e4d','#373955','#4c4257','#6a4c59','#87655c'),
    'night': ('#111a31','#19233e','#25324f','#313b59','#414862','#575471'),
    'dawn': ('#232942','#353b51','#4b495d','#6d5665','#96716c','#b58a72'),
    'day': ('#394754','#4f606b','#627279','#748785','#8c998c','#a6ad93'),
}
LAND = {
    'dusk': ('#242831','#2e3237','#383a3b','#46433e','#565046','#686151','#1c2430','#293437','#39423d'),
    'night': ('#151d2c','#1d2634','#232d3d','#29333e','#363f49','#50515a','#101a29','#1a2937','#273a45'),
    'dawn': ('#282c37','#343641','#424048','#4b484b','#60554e','#706454','#232b36','#303b3c','#495047'),
    'day': ('#313b3b','#414744','#4b5350','#555951','#67695d','#7a7968','#273533','#3d5046','#566551'),
}

def pixel_resize(im,size):
    return im.resize(size,Image.Resampling.NEAREST)

def ridge(w,xp, rough, rng):
    points=[]
    for (x0,y0),(x1,y1) in zip(xp,xp[1:]):
        for x in range(x0,x1,4):
            y=y0+(y1-y0)*(x-x0)/(x1-x0)
            points.append((x,round(y)+rng.choice((-rough,0,0,rough))))
    points.append((w,xp[-1][1]))
    return points

def terrain(mode, scene):
    w,h=480,270
    im=canvas(w,h);d=ImageDraw.Draw(im)
    rng=random.Random({'camp':133,'dawn':212,'trail':312,'field':408,'square':491}[scene])
    sky=SKIES[mode];grass=LAND[mode]
    for i,y in enumerate((0,16,35,57,79,97)):
        d.rectangle((0,y,w-1,h-1),fill=sky[i])
    # Long flattened clouds have separate blue shadow and lit edge.
    clouds=[(6,21,82),(69,16,111),(265,12,127),(378,43,118),(183,48,94),
            (29,59,128),(296,68,98),(92,83,150),(225,89,187)]
    for x,y,length in clouds:
        color=sky[1] if y<40 else sky[3] if y<65 else sky[4]
        d.polygon([(x,y+6),(x+15,y+4),(x+29,y+4),(x+36,y+1),(x+55,y+1),(x+64,y-2),
            (x+length-10,y-2),(x+length,y),(x+length-7,y+4),(x+29,y+7)],fill=color)
        for i in range(4):
            sx=x+rng.randrange(max(1,length-12));sy=y+rng.randrange(0,5)
            d.line((sx,sy,sx+rng.randrange(4,18),sy),fill=sky[0] if y<42 else sky[2])
        if y>48:d.line((x+35,y+7,x+length-16,y+7),fill=sky[-1])
    if mode in ('night','dusk'):
        moon=(361,38) if scene=='trail' else (321,39)
        for x,y in ((42,21),(127,36),(156,11),(236,29),(281,17),(354,14),(405,28),(439,13)):
            d.point((x,y),fill='#a1a3ac' if (x+y)%2 else sky[4])
        x,y=moon
        d.ellipse((x-8,y-8,x+8,y+8),fill='#8c91a6')
        d.ellipse((x-5,y-8,x+7,y+5),fill='#bbb8bb')
        d.line((x-5,y+4,x+2,y+4),fill='#a2a3b1')
        d.line((x+3,y-4,x+6,y-4),fill='#8c91a6')
    for i,(outline,color) in enumerate([
        ([(0,97),(42,72),(79,86),(122,95),(159,82),(194,90),(233,106),(277,83),(327,77),(374,101),(407,93),(447,71),(480,88)],sky[3]),
        ([(0,113),(43,105),(77,106),(128,117),(181,97),(223,109),(257,118),(304,100),(348,101),(381,112),(422,91),(480,114)],sky[2]),
        ([(0,118),(39,110),(88,121),(130,124),(186,119),(222,127),(249,126),(271,119),(319,117),(371,126),(437,108),(480,122)],grass[0]),
    ]):
        pts=ridge(w,outline,i+1,rng)
        d.polygon([*pts,(w,h),(0,h)],fill=color)
        if i<2:
            for j in range(1,len(pts)-4,6):
                x,y=pts[j]
                d.line((x,y+3,x+8,y+5),fill=sky[4] if i==0 else sky[3])
    d.polygon([(0,133),(55,129),(110,133),(184,130),(242,137),(320,128),(390,133),(480,126),(480,h),(0,h)],fill=grass[1])
    # Strong perspective below the horizon. Close clusters are wider and separated.
    for _ in range(4000):
        x=rng.randrange(w);y=rng.randrange(137,h)
        wide=1+(y-130)//44
        c=rng.choices(grass[:5],weights=(7,13,9,5,2))[0]
        d.line((x,y,x+rng.randrange(1,wide+2),y),fill=c)
        if rng.randrange(9)==0:d.point((x+1,y-1),fill=grass[4])
    paths={
        'trail':[(239,129),(249,135),(260,145),(252,157),(240,173),(227,196),(229,218),(257,246),(296,270),(153,270),(174,251),(176,230),(178,209),(197,187),(212,166),(227,151),(235,139)],
        'camp':[(194,153),(227,141),(276,144),(308,162),(350,198),(353,223),(337,253),(295,270),(116,270),(104,240),(123,209),(144,191)],
        'dawn':[(29,201),(82,180),(160,189),(220,213),(209,256),(164,270),(0,270)],
        'field':[(237,131),(246,139),(260,153),(249,166),(223,186),(217,211),(253,240),(310,270),(210,270),(180,239),(179,211),(195,189),(227,162),(236,149)],
        'square':[(116,161),(213,144),(311,147),(374,176),(405,224),(382,270),(76,270),(74,228)],
    }
    path_mask=Image.new('1',im.size)
    ImageDraw.Draw(path_mask).polygon(paths[scene],fill=1)
    floor=canvas(w,h);fd=ImageDraw.Draw(floor)
    fd.rectangle((0,0,w-1,h-1),fill=grass[3])
    for _ in range(5800):
        x=rng.randrange(w);y=rng.randrange(128,h)
        c=rng.choices(grass[1:6],weights=(3,4,14,5,1))[0]
        fd.line((x,y,x+rng.randrange(1,2+(y-125)//34),y),fill=c)
    im.paste(floor,(0,0),path_mask)
    d=ImageDraw.Draw(im)
    for x,y,wide in [(25,143,62),(95,143,58),(370,141,54),(449,139,56),(5,189,87),(472,189,84)]:
        brush(d,x,y,wide,grass,rng)
    for x,y,wide in [(41,244,98),(427,248,102),(-3,261,89),(489,262,91)]:
        brush(d,x,y,wide,grass,rng)
    if scene=='trail':
        for x,y,wide in [(93,232,84),(403,211,90),(126,191,68),(363,175,65),(157,159,48),(330,154,54)]:
            brush(d,x,y,wide,grass,rng)
        menhir(d,272,142,16,27)
    else:
        tree(d,32,164,117,grass,rng,lean=-1)
        tree(d,445,167,123,grass,rng,lean=1)
    if scene in ('camp','square'):
        tower(d,92,143,50,107,sky,lit=True)
        clothesline(d,127,147,206,143)
        fence(d,347,149,428,159,grass)
        camp_site(im,309 if scene=='camp' else 259,195 if scene=='camp' else 189,lit=scene=='square')
        log(d,167,212,55,12)
        log(d,363,204,55,13)
    elif scene=='dawn':
        camp_site(im,94,221,False,embers=True)
    elif scene=='field':
        menhir(d,253,158,10,21)
        log(d,350,198,42,10)
    for _ in range(270):
        y=rng.randrange(175,h);x=rng.randrange(w)
        if path_mask.getpixel((x,y)) and rng.randrange(5):continue
        c=rng.choice((grass[5],grass[8],grass[0]))
        d.line((x,y,x,y-rng.randrange(1,6)),fill=c)
        d.point((x-1,y-1),fill=c)
    return im

def brush(d,x,y,w,colors,rng):
    parts=[(-.36,-.04,.19,.12),(-.20,-.14,.20,.15),(0,-.18,.24,.19),(.18,-.11,.23,.15),(.34,-.03,.21,.13)]
    bottom=y+round(w*.14)
    for j,(ox,oy,rx,ry) in enumerate(parts):
        cx=x+round(ox*w);cy=y+round(oy*w);a=max(3,round(rx*w));b=max(3,round(ry*w))
        pts=[(cx-a,cy+b//2),(cx-a,cy-b//3),(cx-a//2,cy-b*3//4),(cx-3,cy-b),
             (cx+a//4,cy-b),(cx+a//2,cy-b//2),(cx+a*3//4,cy-b//3),(cx+a,cy+b//3),
             (cx+a*2//3,bottom),(cx-a//2,bottom)]
        d.polygon(pts,fill=colors[6 if j%2==0 else 0])
        top=[(cx-a+3,cy+b//3),(cx-a//2,cy-b//2),(cx,cy-b+2),
             (cx+a//4,cy-b+2),(cx+a//2,cy-b//3),(cx+a-4,cy+b//3),(cx,cy+b//3)]
        d.polygon(top,fill=colors[7+j%2])
        for _ in range(max(2,w//3)):
            px=cx+rng.randint(-a//2,a//2);py=cy+rng.randint(-b//2,b//2)
            c=rng.choice((colors[8],colors[7],colors[6]))
            d.line((px,py,px+rng.randrange(1,4),py),fill=c)
            if rng.randrange(4)==0:d.point((px+1,py-1),fill=colors[4])

def tree(d,x,y,h,colors,rng,lean):
    end=x+lean*21
    bark='#302937';light='#4d3b42';dark='#1b1b2a'
    d.polygon([(x-13,y+4),(x-7,y-30),(x-6,y-57),(end-4,y-h),
       (end+9,y-h),(x+9,y-58),(x+11,y-26),(x+13,y),(x+25,y+6),(x+3,y+4),(x-18,y+9)],fill=dark)
    d.polygon([(x-9,y),(x-5,y-34),(x-2,y-62),(end,y-h+6),(end+4,y-h+6),
               (x+5,y-62),(x+5,y-26),(x+10,y),(x+17,y+3)],fill=bark)
    d.line((x-3,y-3,x-1,y-48,end+2,y-h+13),fill=light,width=2)
    for side in (-1,1):
        for i,dh in enumerate((38,54,68,79)):
            xx=x+side*(33+i*3);yy=y-h+dh//3+i*7
            d.line((x,y-dh,xx,yy),fill=dark,width=7-i)
            d.line((x+2,y-dh-1,xx,yy-2),fill=bark,width=3)
            brush(d,xx,yy-3,49+i*4,colors,rng)
    brush(d,end,y-h+15,81,colors,rng)
    brush(d,end+lean*24,y-h+5,72,colors,rng)

def menhir(d,x,y,w,h):
    points=[(x-w//2,y),(x-w//2+1,y-h+5),(x-w//4,y-h),(x+w//3,y-h+2),(x+w//2,y-h+7),(x+w//2,y)]
    d.polygon(points,fill='#242735')
    d.polygon([(x-w//2+2,y-1),(x-w//2+3,y-h+5),(x-w//4,y-h+1),(x+w//4,y-h+4),
        (x+w//4,y-1)],fill='#4b5160')
    d.line((x-w//2+3,y-4,x-w//2+4,y-h+6,x-w//4,y-h+2),fill='#737989')

def masonry(d,region,colors,seed):
    rng=random.Random(seed)
    x0,y0,x1,y1=region
    d.rectangle(region,fill=colors[0])
    for row,y in enumerate(range(y0,y1,12)):
        x=x0-(row%2)*17
        while x<x1:
            w=rng.randrange(15,41)
            left=max(x,x0);right=min(x+w,x1);bottom=min(y+11,y1)
            if right-left>2:
                d.polygon([(left,y+3),(left+2,y+1),(right-2,y+1),(right,y+3),
                    (right,bottom-2),(right-2,bottom),(left+2,bottom),(left,bottom-1)],fill=rng.choice(colors[1:4]))
                d.line((left+3,y+2,right-2,y+2),fill=colors[4])
                for _ in range(3):
                    tx=rng.randint(left+1,right-1);ty=rng.randrange(y+4,y+10)
                    if ty<bottom:d.line((tx,ty,min(tx+rng.randrange(1,7),right-1),ty),fill=colors[3])
            x+=w+2

def tower(d,x,y,w,h,sky,lit=False):
    top=y-h
    palette=('#21232e','#3d3b4a','#46444f','#383746','#53515e')
    d.polygon([(x-6,y),(x-3,y-4),(x+2,top+11),(x-3,top+5),(x-3,top),(x+w+3,top),(x+w+3,top+5),
               (x+w-2,top+11),(x+w+5,y)],fill=palette[0])
    masonry(d,(x+2,top+8,x+w-2,y-3),palette,23)
    d.rectangle((x+w-7,top+8,x+w-2,y-3),fill='#363443')
    d.rectangle((x-1,top+4,x+w+2,top+8),fill='#514858')
    for xx in range(x-1,x+w+1,8):d.rectangle((xx,top-4,xx+5,top+4),fill='#4b4655')
    for off in (20,44,67):
        cx=x+(w//2 if off!=44 else w//3);yy=top+off
        d.polygon([(cx-5,yy+8),(cx-5,yy+2),(cx-2,yy-2),(cx+2,yy-2),(cx+4,yy+2),(cx+4,yy+8)],fill='#222532')
        if lit:
            d.rectangle((cx-2,yy+1,cx+1,yy+6),fill='#a66b40')
            d.line((cx-2,yy+3,cx+1,yy+3),fill='#d9a05d')
        d.line((cx-5,yy+9,cx+4,yy+9),fill=palette[-1])
    d.polygon([(x+w//2-7,y-3),(x+w//2-7,y-18),(x+w//2-4,y-22),(x+w//2+4,y-22),
               (x+w//2+7,y-18),(x+w//2+7,y-3)],fill=palette[0])
    d.line((x-4,y-2,x+w+4,y-2),fill=palette[-1])

def clothesline(d,x0,y0,x1,y1):
    for x,y in [(x0,y0),(x1,y1)]:
        d.line((x,y,x-1,y-34),fill='#382c35',width=4)
        d.line((x,y-1,x-1,y-33),fill='#795d49')
    d.line((x0,y0-32,x0+25,y0-26,x1-24,y1-25,x1,y1-32),fill='#9a7659')
    for x,y,c in [(x0+10,y0-29,'#69566a'),(x0+28,y0-25,'#9f8269'),(x0+46,y0-24,'#70647c'),(x0+61,y0-27,'#675b63')]:
        d.polygon([(x,y),(x+12,y+1),(x+10,y+13),(x+3,y+12)],fill=c)
        d.line((x+2,y,x+3,y+11),fill='#504657')

def fence(d,x0,y0,x1,y1,colors):
    for x in range(x0,x1+1,14):
        y=y0+(y1-y0)*(x-x0)//(x1-x0)
        d.line((x,y-13,x+1,y+3),fill='#50404a',width=3)
    d.line((x0,y0-8,x1,y1-8),fill='#6b5448',width=2)
    d.line((x0,y0-4,x1,y1-4),fill='#493e45',width=2)

def log(d,x,y,w,h):
    d.polygon([(x,y+3),(x+5,y),(x+w-6,y),(x+w,y+4),(x+w,y+h),(x+3,y+h),(x,y+h-3)],fill='#292630')
    d.rectangle((x+4,y+2,x+w-6,y+h-2),fill='#5d483e')
    d.line((x+6,y+3,x+w-9,y+3),fill='#80624d')
    d.line((x+7,y+h-3,x+w-11,y+h-3),fill='#46353a')
    d.ellipse((x+w-10,y+1,x+w-1,y+h-1),fill='#8e6d53')
    d.ellipse((x+w-8,y+3,x+w-3,y+h-3),outline='#5a4138')

def camp_site(im,x,y,lit,embers=False):
    d=ImageDraw.Draw(im)
    if lit:
        d.ellipse((x-33,y-9,x+33,y+15),fill='#514035')
        d.ellipse((x-24,y-7,x+24,y+12),fill='#654931')
    d.ellipse((x-19,y-7,x+19,y+8),fill='#26222a')
    for dx,dy in [(-16,-5),(-8,-7),(1,-8),(10,-6),(16,-3),(16,5),(7,7),(-3,8),(-12,5),(-17,2)]:
        d.polygon([(x+dx-4,y+dy-1),(x+dx-2,y+dy-3),(x+dx+3,y+dy-2),(x+dx+4,y+dy+1),(x+dx+1,y+dy+3),(x+dx-4,y+dy+2)],fill='#58504c')
        d.line((x+dx-2,y+dy-2,x+dx+2,y+dy-2),fill='#777065')
    if lit:
        im.alpha_composite(campfire(37,2),(x-19,y-28))
    elif embers:
        d.line((x-6,y+3,x+5,y-3),fill='#9c653d',width=2)
        d.line((x-7,y-2,x+5,y+2),fill='#7d523c',width=2)
        d.point((x,y),fill='#e5ad6d')
        d.point((x+4,y-1),fill='#be8457')
    else:
        d.line((x-7,y+1,x+5,y-3),fill='#453735',width=2)
        d.line((x-7,y-2,x+5,y+2),fill='#372c2e',width=2)

def ranked_wall():
    im=canvas(480,270);d=ImageDraw.Draw(im)
    masonry(d,(0,0,479,269),('#1a2030','#252e40','#283144','#2c3445','#343c51'),718)
    # Side columns and upper lintel frame a quiet central surface for the rankings.
    for x in (0,419):
        masonry(d,(x,0,x+60,269),('#141b2a','#202b3a','#25323e','#283440','#374052'),x+43)
        d.line((x+10,0,x+10,269),fill='#151c2b',width=3)
        d.line((x+49,0,x+49,269),fill='#111929',width=4)
    for y in (12,55,236,256):
        d.rectangle((0,y,479,y+4),fill='#1b2232')
        d.line((4,y+1,476,y+1),fill='#454657')
    for x in (85,394):
        d.line((x,14,x,234),fill='#1e2434',width=2)
        d.line((x-1,17,x-1,235),fill='#333c52')
    for x,y in ((38,75),(435,74)):
        d.rectangle((x-8,y,x+8,y+13),fill='#1c2530')
        d.rectangle((x-6,y+1,x+6,y+12),outline='#76634b',width=2)
        d.line((x-2,y+3,x-2,y+10),fill='#bd8853',width=2)
        d.line((x+2,y+4,x+2,y+9),fill='#8b653f',width=2)
        d.line((x,y-12,x,y),fill='#514650',width=2)
    rng=random.Random(37)
    for x,y in ((67,19),(392,56),(442,34),(60,249),(451,245)):
        brush(d,x,y,29,LAND['night'],rng)
    return im

def market_street():
    im=canvas(480,270);d=ImageDraw.Draw(im);rng=random.Random(384)
    ground=LAND['night']
    d.rectangle((0,0,479,269),fill=ground[1])
    d.polygon([(211,0),(303,0),(301,37),(275,79),(258,127),(265,163),(295,205),(310,270),
        (169,270),(162,237),(166,205),(165,172),(179,143),(198,102),(218,50)],fill=ground[3])
    for _ in range(11000):
        x=rng.randrange(480);y=rng.randrange(270)
        d.line((x,y,x+rng.randrange(1,4),y),fill=rng.choice(ground[:5]))
    for i,(x,y,s,side) in enumerate(((31,44,57,0),(98,95,71,0),(9,145,75,0),(87,226,89,0),(363,7,65,1),
                                     (332,63,65,1),(395,116,84,1),(338,177,82,1),(430,227,65,1))):
        draw_stall(d,x,y,s,i,side)
    for i,(x,y) in enumerate(((134,40),(171,86),(147,148),(297,63),(309,161),(187,246),(304,220))):
        d.ellipse((x-4,y-3,x+4,y+3),fill='#302932')
        d.line((x-3,y-2,x+2,y-2),fill='#64554e')
        if i%2==0:
            d.line((x,y-6,x,y-14),fill='#52434a',width=2)
            d.rectangle((x-3,y-12,x+3,y-6),fill='#1e2535')
            d.rectangle((x-2,y-11,x+2,y-7),fill='#ad774d')
            d.point((x-1,y-10),fill='#e1ad65')
    return im

def draw_stall(d,x,y,w,index,side):
    colors=(('#585365','#454054','#6c6172'),('#555d64','#404f5a','#69727b'),('#775a53','#5d464b','#937265'))
    c,low,hi=colors[index%3]
    d.polygon([(x-7,y+8),(x+w+5,y+7),(x+w+14,y+45),(x+w-10,y+59),(x-7,y+54)],fill='#151e2c')
    d.polygon([(x+5,y+25),(x+w-8,y+26),(x+w-5,y+49),(x+6,y+52)],fill=low)
    for k in range(28,w//2+25,4):
        d.line((x+7,y+k,x+w-8,y+k),fill=c)
    d.rectangle((x+5,y+34,x+20,y+45),fill='#51423b',outline='#262632')
    d.line((x+7,y+36,x+18,y+36),fill='#88694b')
    d.rectangle((x+w-25,y+33,x+w-11,y+45),fill='#634e3f',outline='#2f2b30')
    d.line((x+w-23,y+35,x+w-13,y+35),fill='#8a6a4d')
    pts=[(x+1,y),(x+w-7,y-2),(x+w,y+5),(x+w-1,y+24),(x+5,y+25),(x-3,y+16)]
    d.polygon(pts,fill='#272633')
    d.polygon([(x+2,y+1),(x+w-9,y),(x+w-2,y+6),(x+w-4,y+21),(x+6,y+23),(x-1,y+16)],fill=c)
    for offset in range(5,w-10,11):
        d.polygon([(x+offset,y+1),(x+offset+3,y+1),(x+offset+5,y+20),(x+offset+1,y+22)],fill=low)
    d.line((x+3,y+2,x+w-9,y+1),fill=hi)
    d.line((x+6,y+22,x+w-6,y+20),fill=hi)
    d.line((x+6,y+24,x+6,y+45),fill='#7f6656',width=2)
    d.line((x+w-8,y+22,x+w-8,y+42),fill='#51434a',width=2)

def sitter(d,x,y,style):
    # Rear-view apprentice vignettes, deliberately drawn on their own native grid.
    colors={'kettle':('#785140','#a06f4e','#c59a69'),'lantern':('#473c58','#6c5473','#927089'),
            'book':('#675144','#96785c','#c09d73'),'frost':('#526c85','#8091b0','#afbdd2'),
            'hood':('#4b2e56','#754467','#9f6487')}
    low,c,hi=colors[style]
    d.polygon([(x-13,y+7),(x-10,y-10),(x+9,y-11),(x+13,y+7),(x+5,y+11),(x-8,y+10)],fill='#201f2d')
    d.polygon([(x-10,y+5),(x-8,y-8),(x+7,y-10),(x+10,y+5),(x+5,y+8),(x-6,y+8)],fill='#3c415a')
    d.line((x-7,y-4,x+6,y-7),fill='#656881',width=2)
    if style=='kettle':
        pts=[(-11,-12),(-10,-21),(-5,-26),(3,-26),(8,-22),(10,-17),(16,-22),(16,-16),(11,-12)]
        d.rectangle((x-2,y-28,x+3,y-25),fill=hi)
    elif style=='lantern':
        pts=[(-12,-11),(-7,-18),(-7,-22),(-1,-27),(5,-26),(8,-23),(11,-22),(10,-18),(7,-21),(7,-16),(10,-12)]
        d.line((x+9,y-22,x+10,y-13),fill='#a17658')
        d.rectangle((x+9,y-16,x+12,y-13),fill='#e1b16c')
    elif style=='book':
        pts=[(-14,-13),(-11,-17),(-3,-23),(2,-25),(13,-18),(16,-13),(3,-15),(-1,-17),(-8,-15)]
    elif style=='frost':
        pts=[(-10,-11),(-10,-18),(-6,-22),(-1,-23),(3,-21),(7,-24),(10,-19),(10,-12)]
        for xx,yy in [(-10,-25),(-6,-29),(0,-30),(6,-29),(11,-25)]:
            d.rectangle((x+xx,y+yy,x+xx+1,y+yy+3),fill='#a3bed0')
    else:
        pts=[(-12,-11),(-9,-17),(-5,-24),(-3,-27),(3,-27),(7,-25),(10,-22),(7,-21),(5,-24),(4,-20),(11,-13),(11,-11)]
    d.polygon([(x+px,y+py) for px,py in pts],fill=c,outline=low,width=2)
    d.line((x-6,y-14,x+5,y-16),fill=hi)

def profile(server):
    im=terrain('night','square');d=ImageDraw.Draw(im)
    log(d,202,218,99,12)
    if server:
        for x,y,style in [(208,213,'kettle'),(236,219,'lantern'),(269,217,'book'),(299,213,'hood')]:
            sitter(d,x,y,style)
    else:
        sitter(d,259,219,'hood')
    return ImageOps.fit(im.crop((65,80,458,270)),(248,120),Image.Resampling.NEAREST)

def duel_card():
    im=canvas(240,157);d=ImageDraw.Draw(im)
    masonry(d,(0,0,239,156),('#242e3d','#333e4b','#3d4650','#4b5158','#58616b'),94)
    for i,x in enumerate(range(10,234,24)):
        menhir(d,x,33-(i%3)*2,19,32+(i%2)*4)
    for i,(x,y) in enumerate(((9,83),(13,123),(225,81),(223,129))):
        menhir(d,x,y,24,37+(i%2)*7)
    d.line((124,30,124,156),fill='#b1ac9e',width=2)
    d.line((125,31,125,156),fill='#817f7f')
    for x,y,style in [(83,108,'kettle'),(166,107,'hood')]:
        d.ellipse((x-14,y+5,x+14,y+13),fill='#292e39')
        sitter(d,x,y,style)
        direction=1 if x<124 else -1
        d.line((x+direction*9,y+2,x+direction*17,y-11),fill='#9c7959',width=2)
        d.rectangle((x+direction*17-1,y-13,x+direction*17+1,y-10),fill='#bca3d0')
    return im

def wardrobe():
    im=canvas(140,220);d=ImageDraw.Draw(im);rng=random.Random(30)
    d.rectangle((0,0,139,219),fill='#282531')
    for x,w in ((0,12),(12,13),(25,11),(36,13),(49,11),(60,13),(73,14),(87,14),(101,14),(115,14),(129,11)):
        c=rng.choice(('#413138','#483539','#3a2d38','#4d383b'))
        d.rectangle((x+1,0,x+w-1,185),fill=c)
        d.line((x+1,0,x+1,185),fill='#544044')
        for _ in range(12):
            tx=x+rng.randrange(2,w);ty=rng.randrange(0,181)
            d.line((tx,ty,tx,ty+rng.randrange(1,8)),fill='#513d41')
    for y in (7,82,178):
        d.rectangle((0,y,139,y+6),fill='#302731')
        d.line((0,y,139,y),fill='#604945')
        for x in (5,134):d.rectangle((x,y+2,x+2,y+4),fill='#987954')
    d.polygon([(0,29),(34,42),(73,46),(108,41),(139,30),(139,32),(108,43),(73,48),(34,44),(0,31)],fill='#8f7458')
    for x,y,c in ((9,33,'#685665'),(25,40,'#524a60'),(110,39,'#81654f'),(127,34,'#66536e')):
        d.polygon([(x,y+2),(x+12,y+1),(x+14,y+27),(x+9,y+25),(x+1,y+27)],fill=c)
        d.line((x+2,y+3,x+3,y+22),fill='#ab8776')
        d.rectangle((x+4,y-1,x+5,y+5),fill='#a78662')
    d.rectangle((0,185,139,219),fill='#382c35')
    for y in range(187,220,7):
        d.line((0,y,139,y),fill='#53403e')
        d.line((30-(y%3)*7,y,30-(y%3)*7,y+7),fill='#2b2630')
        d.line((93-(y%3)*9,y,93-(y%3)*9,y+7),fill='#2b2630')
    for x,y in [(8,160),(129,157)]:
        d.rectangle((x-5,y,x+5,y+13),fill='#282431')
        d.rectangle((x-4,y+1,x+4,y+10),outline='#98724d',width=2)
        d.rectangle((x-1,y+3,x+1,y+8),fill='#d2a767')
    return im

def shop_strip(chair):
    im=canvas(300,24);d=ImageDraw.Draw(im)
    if chair:
        d.rectangle((0,0,299,23),fill='#1d2130')
        for i,x in enumerate(range(155,300,9)):
            d.rectangle((x,0,x+7,22),fill=('#413237','#362d37','#392e38')[i%3])
            d.line((x,0,x,22),fill='#563d43')
        d.polygon([(0,17),(149,16),(300,19),(300,24),(0,24)],fill='#282734')
        for x,y in ((193,5),(196,4),(199,3),(202,3),(205,4),(208,5)):
            d.line((x,y,x+3,y+11),fill='#8d6b53',width=2)
        d.line((192,5,198,2,204,2,210,5),fill='#a9865f',width=2)
        d.line((194,6,198,16,216,16,216,19,198,19,196,22),fill='#6e5145',width=2)
        d.line((213,18,219,22),fill='#8f694f',width=2)
        d.line((192,21,199,23,217,22,224,19),fill='#a48160',width=2)
        d.polygon([(211,4),(216,5),(219,14),(225,19),(220,21),(215,19),(213,14)],fill='#624459')
        d.line((214,7,216,14,222,19),fill='#896271')
        d.rectangle((241,15,248,22),fill='#b08052')
        d.rectangle((243,16,246,21),fill='#d4ac6e')
    else:
        d.rectangle((0,0,299,23),fill='#222534')
        d.polygon([(147,15),(169,7),(282,6),(299,14),(299,23),(151,23)],fill='#2e2732')
        d.rectangle((165,12,274,19),fill='#634a3e',outline='#2e2732')
        d.line((165,13,273,13),fill='#9c7653')
        for x in range(173,272,16):d.line((x,14,x,18),fill='#4b3740')
        d.line((165,19,144,18),fill='#83634e',width=2)
        d.line((168,17,168,4),fill='#82614e',width=2)
        d.line((269,18,269,3),fill='#82614e',width=2)
        d.polygon([(163,4),(175,1),(264,1),(274,5),(271,8),(169,8)],fill='#705460')
        for x in range(171,270,11):
            d.line((x,3,x+1,7),fill='#9a7080')
        d.ellipse((242,13,256,27),fill='#2d2732',outline='#a27d59',width=2)
        d.line((244,20,255,20),fill='#8f6d54');d.line((249,14,249,24),fill='#8f6d54')
    return im

def app_badge():
    im=canvas(64);d=ImageDraw.Draw(im)
    d.rectangle((0,0,63,63),fill='#151827')
    d.polygon([(0,48),(18,39),(47,40),(63,50),(63,63),(0,63)],fill='#1e2237')
    for x,y,c in [(6,11,'#494565'),(51,6,'#57446a'),(12,43,'#403a59'),(53,51,'#4a425e')]:
        d.rectangle((x,y,x+1,y+1),fill=c)
    d.polygon([(8,48),(16,36),(28,36),(40,34),(50,46),(47,55),(17,58),(7,54)],fill='#202137')
    d.polygon([(10,49),(19,39),(40,37),(47,47),(44,54),(20,55),(11,53)],fill='#394466')
    d.line((13,50,21,46,29,43,39,41),fill='#5d6487',width=2)
    hood=[(12,36),(11,29),(16,23),(22,17),(19,15),(16,14),(13,16),(12,20),(8,20),(8,13),(13,8),(20,6),(26,7),(35,11),(43,16),(48,23),(49,33),(45,40),(27,43),(17,40)]
    d.polygon(hood,fill='#743e68',outline='#221b30',width=2)
    d.line((10,16,15,10,22,9,31,12,41,17,45,23),fill='#a2648d',width=2)
    d.polygon([(19,32),(22,26),(28,22),(38,22),(44,26),(46,33),(43,38),(25,39),(18,35)],fill='#ac6a9b')
    d.polygon([(22,32),(26,27),(31,25),(38,25),(42,28),(43,33),(39,36),(26,37),(22,35)],fill='#21182a')
    d.rectangle((29,30,30,31),fill='#d7a5ef');d.rectangle((37,30,38,31),fill='#d7a5ef')
    d.rectangle((33,40,37,43),fill='#a98052');d.point((34,40),fill='#e2b778')
    d.line((48,55,52,41,55,30),fill='#453245',width=4);d.line((48,54,53,39,55,32),fill='#8a615e',width=2)
    d.polygon([(55,20),(59,25),(58,32),(55,36),(51,31),(52,24)],fill='#4a3568')
    d.polygon([(55,22),(57,26),(56,32),(54,34),(52,29),(53,25)],fill='#9a66d7')
    d.line((55,23,53,28),fill='#e2c3f3',width=2)
    return im

def illustrations():
    for id,name,im,size,props in [
        ('s-0501','camp_background',terrain('dusk','camp'),(1920,1080),{'fire_anchor':[1236,780]}),
        ('s-1602','expedition_dawn',terrain('dawn','dawn'),(1920,1080),{}),
        ('s-2307','market_street',market_street(),(1920,1080),{}),
        ('s-2506','tower_wall',ranked_wall(),(1920,1080),{}),
        ('s-2601','brush_trail',terrain('night','trail'),(1920,1080),{}),
        ('s-0301','offline_card',profile(False),(496,240),{}),
        ('s-0301','server_card',profile(True),(496,240),{}),
        ('s-0502','mode_expedition',ImageOps.fit(terrain('day','field'),(184,120),Image.Resampling.NEAREST),(368,240),{}),
        ('s-0502','mode_duel',ImageOps.fit(duel_card(),(184,120),Image.Resampling.NEAREST),(368,240),{}),
        ('s-0502','mode_square',ImageOps.fit(terrain('night','square').crop((58,60,421,270)),(184,120),Image.Resampling.NEAREST),(368,240),{}),
        ('s-2402','wardrobe_preview',wardrobe(),(560,880),{}),
        ('s-2305','shop_banner_cart',shop_strip(False),(1200,96),{}),
        ('s-2305','shop_banner_chair',shop_strip(True),(1200,96),{}),
        ('s-0203','launcher_icon',app_badge(),(256,256),{}),
        ('s-0203','launcher_icon',app_badge(),(1024,1024),{}),
    ]:
        save(id,name,pixel_resize(im,size),metadata={'art':'authored pixel raster','logical_resolution':list(im.size),**props})
