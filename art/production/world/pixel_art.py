"""Draw the world source sheets and scenes on a native pixel grid."""
from random import Random
from pathlib import Path
from PIL import Image, ImageDraw, ImageOps

NEAREST = Image.Resampling.NEAREST
OUTLINE = '#1A1C28'
WOOD = ('#362A30', '#63483B', '#967151', '#BE9563')
SLATE = ('#202833', '#394351', '#5C6571', '#858A88')
MOSS = ('#202C2B', '#344639', '#526043', '#6D7250')
CLOTH = ('#3A2D43', '#67536B', '#9C7B85', '#D2AC9D')
PAPER = ('#786752', '#BB9E75', '#DFBF8A', '#F0D8AB')
INDIGO = ('#212739', '#3B425B', '#5E6B80', '#8089A2')


def polygon(draw, points, color, outline=None):
    draw.polygon(points, fill=color, outline=outline)


def box(draw, rect, color, outline=None):
    draw.rectangle(rect, fill=color, outline=outline)


def line(draw, points, color, width=1):
    draw.line(points, fill=color, width=width)


def ellipse(draw, rect, color, outline=None):
    draw.ellipse(rect, fill=color, outline=outline)


def timber(draw, x, y, w, h, vertical=False):
    box(draw, (x, y, x+w, y+h), WOOD[1], OUTLINE)
    line(draw, [(x+1,y+1), (x+w-1,y+1)], WOOD[3])
    line(draw, [(x+1,y+h-1), (x+w-1,y+h-1)], WOOD[0])
    if vertical:
        for xx in range(x+3, x+w, 3):
            line(draw, [(xx,y+2), (xx,y+h-2)], WOOD[(xx%2)+1])
    else:
        for yy in range(y+3,y+h,3):
            line(draw, [(x+2,yy), (x+w-2,yy)], WOOD[(yy%2)+1])


def stone(draw, x, y, w, h, seed=0, moss=True, standing=False):
    rng = Random(seed)
    cap = max(2, h//4)
    silhouette = [(x,y+h-3), (x+2,y+cap), (x+w//4,y+2), (x+w//2,y), (x+w-3,y+cap//2),
                  (x+w-1,y+cap+2), (x+w,y+h-4), (x+w-4,y+h-1), (x+4,y+h)]
    polygon(draw, silhouette, SLATE[1], OUTLINE)
    polygon(draw, [(x+2,y+cap+1),(x+w//4,y+3),(x+w//2,y+1),(x+w-4,y+cap//2+1),
                   (x+w-3,y+cap+4),(x+w//2,y+cap+5),(x+w//3,y+cap+3)], SLATE[2])
    polygon(draw, [(x+2,y+cap+2),(x+w//3,y+cap+4),(x+w//3-1,y+h-3),(x+3,y+h-3)], SLATE[0])
    line(draw, [(x+w//4,y+3),(x+w//2,y+2),(x+w-5,y+cap//2+2)], SLATE[3])
    if standing:
        line(draw, [(x+3,y+cap+7),(x+w//2,y+h//2),(x+w-3,y+h//2-4)], SLATE[0])
        line(draw, [(x+w//2,y+h//2+1),(x+w//2-1,y+h-5)], SLATE[2])
    for _ in range(w*h//96):
        xx, yy = rng.randint(x+4,max(x+4,x+w-4)), rng.randint(y+4,max(y+4,y+h-4))
        box(draw,(xx,yy,xx+rng.randint(0,2),yy),SLATE[rng.choice((1,2))])
    if moss:
        line(draw, [(x+3,y+h-1),(x+w//3,y+h-3),(x+w//2,y+h-1),(x+w-4,y+h-2)], MOSS[2], 2)
        line(draw, [(x+w//4,y+2),(x+w//2,y+1),(x+w//2+4,y+2)], MOSS[3])


def foliage(draw, x, y, w, h, seed, colors=MOSS, thorn=False):
    rng=Random(seed)
    silhouette=[]
    for i in range(12):
        xx=x+round(w*i/11)
        yy=y+rng.randrange(max(2,h//3))
        silhouette.append((xx,yy))
    silhouette += [(x+w,y+h-5),(x+w-5,y+h),(x+3,y+h),(x,y+h-3)]
    polygon(draw, silhouette, colors[0], OUTLINE)
    for i in range(max(8,w*h//26)):
        xx=rng.randint(x+3,x+w-3)
        yy=rng.randint(y+5,y+h-2)
        if i % 3:
            color=colors[1+int(rng.random()>.72)]
            line(draw, [(xx-2,yy),(xx,yy-2),(xx+2,yy-2),(xx+3,yy)], color, rng.choice([1,2]))
        else:
            box(draw,(xx,yy,xx+1,yy+1),colors[rng.choice((1,2,3))])
    if thorn:
        for i in range(max(3,w//8)):
            xx=x+3+(w-6)*i//max(3,w//8)
            yy=y+5+rng.randrange(max(2,h//4))
            pts=[(xx,y+h-2),(xx+3,y+h//2),(xx-2,yy+5),(xx+1,yy)]
            line(draw,pts,WOOD[0],2)
            line(draw,pts,WOOD[1])
            line(draw,[(xx+2,y+h//2),(xx+5,y+h//2-4),(xx+5,y+h//2-6)],WOOD[1])


def glow(draw, x, y, radius, hue):
    # Discrete light bands retain the square pixel grid.
    for i in range(3,0,-1):
        rr=radius*i//3
        ellipse(draw,(x-rr,y-rr//2,x+rr,y+rr//2),hue[3-i])


def lantern(draw, x, y, scale=1):
    box(draw,(x-3*scale,y,x+3*scale,y+7*scale),'#493034',OUTLINE)
    box(draw,(x-2*scale,y+scale,x+2*scale,y+6*scale),'#F5A84E')
    box(draw,(x-scale,y+2*scale,x+scale,y+5*scale),'#FFDB88')
    line(draw,[(x-4*scale,y),(x,y-2*scale),(x+4*scale,y)],WOOD[2])
    line(draw,[(x-4*scale,y+7*scale),(x+4*scale,y+7*scale)],WOOD[0],scale)
    line(draw,[(x,y),(x,y+7*scale)],WOOD[2],scale)


def paper(draw,x,y,w,h,seed=0):
    rng=Random(seed)
    polygon(draw,[(x,y+1),(x+w-1,y),(x+w,y+h-2),(x+w-2,y+h),(x+1,y+h-1)],PAPER[2],PAPER[0])
    line(draw,[(x+1,y+2),(x+w-2,y+1)],PAPER[3])
    for i in range(y+4,y+h-2,3):
        line(draw,[(x+2,i),(x+max(2,w-rng.randint(2,5)),i)],PAPER[1])


def roof(draw,x,y,w,h,seed=0):
    rng=Random(seed)
    polygon(draw,[(x,y+h-3),(x+8,y+h-6),(x+w//5,y+3),(x+w//2,y),(x+w*4//5,y+3),
                  (x+w-8,y+h-6),(x+w,y+h-3),(x+w-3,y+h),(x+3,y+h)],'#262A3D',OUTLINE)
    for i in range(4,w-3,5):
        xx=x+i
        start=x+w//2+(i-w//2)*2//3
        line(draw,[(start,y+3+abs(i-w//2)//12),(xx,y+h-4)],rng.choice(('#454B62','#51556B','#34374B')),2)
        line(draw,[(xx,y+h-4),(xx+1,y+h-1)],'#6E6A76')
    line(draw,[(x+2,y+h-2),(x+w//2,y+h-1),(x+w-2,y+h-2)],'#747282')


def tower(draw,x,y,w,h,seed=0,with_laundry=False):
    base=y+h
    polygon(draw,[(x+w//6,y+8),(x+w*5//6,y+8),(x+w-3,base-8),(x+w-1,base),
                  (x+2,base),(x+4,base-8)],SLATE[1],OUTLINE)
    rng=Random(seed)
    for yy in range(y+10,base-6,8):
        inset=max(4,(base-yy)//8)
        line(draw,[(x+inset,yy),(x+w-inset,yy)],SLATE[0])
        for xx in range(x+inset+3+((yy//8)%2)*5,x+w-inset-4,11):
            line(draw,[(xx,yy),(xx-1,yy+7)],SLATE[0])
            if rng.random()<.75:
                line(draw,[(xx+1,yy+1),(xx+rng.randrange(3,8),yy+1)],SLATE[2])
    roof(draw,x,y,w,max(7,h//6),seed)
    left=x+w//2-5
    box(draw,(left,base-26,left+10,base-5),'#2A222F',OUTLINE)
    for yy in range(base-23,base-7,3):
        line(draw,[(left+2,yy),(left+8,yy)],'#A4613E')
    line(draw,[(left+5,base-24),(left+5,base-7)],'#E4A45D')
    for i in range(3):
        yy=base-3+i*2
        box(draw,(x+3-i*2,yy,x+w-3+i*2,yy+1),SLATE[2],SLATE[0])
    if with_laundry:
        clothesline(draw,x+w-2,base-21,max(19,w//2),seed)


def clothesline(draw,x,y,w,seed):
    rng=Random(seed)
    timber(draw,x,y,2,23,True)
    timber(draw,x+w,y+1,2,22,True)
    line(draw,[(x+1,y+4),(x+w//2,y+7),(x+w,y+4)],PAPER[0])
    for i in range(3):
        xx=x+3+i*(w-5)//3
        height=rng.choice((9,10,13))
        yy=y+5+(i==1)*2
        color=(PAPER[2],CLOTH[2],INDIGO[2])[i]
        polygon(draw,[(xx,yy),(xx+(w-5)//3-1,yy+1),(xx+(w-5)//3-2,yy+height),
                      (xx+1,yy+height-1)],color,WOOD[0])
        line(draw,[(xx+2,yy+2),(xx+3,yy+height-2)],PAPER[1])


def pine(draw,x,y,w,h,seed,colors=MOSS):
    rng=Random(seed)
    line(draw,[(x,y+h),(x+1,y+9)],WOOD[0],max(2,w//9))
    line(draw,[(x+1,y+h-3),(x+1,y+14)],WOOD[1])
    for i in range(4,-1,-1):
        yy=y+i*h//7
        span=3+w*(i+1)//10
        pts=[(x,yy-3),(x-span//3,yy+h//8),(x-span,yy+h//4),(x-span//2,yy+h//4),
             (x-span//4,yy+h//4+3),(x+span//4,yy+h//4+4),(x+span,yy+h//4+1),
             (x+span//2,yy+h//7)]
        polygon(draw,pts,colors[i%2],OUTLINE)
        line(draw,[(x,yy-1),(x-span//3,yy+h//8+1),(x-span+2,yy+h//4-1)],colors[2])
        for _ in range(span//2):
            xx=x+rng.randint(-span*2//3,span*2//3)
            ty=yy+rng.randint(h//7,max(h//7,h//4))
            line(draw,[(xx-1,ty),(xx+1,ty)],colors[rng.choice((1,2,3))])


def fire(draw,x,y,size,frame):
    ring=size//2
    for i,(dx,dy) in enumerate(((-9,-1),(-5,-4),(0,-5),(5,-4),(9,-1),(8,4),(3,6),(-3,6),(-8,4))):
        stone(draw,x+dx*ring//10-3,y+dy*ring//10-2,6,4,i,moss=i%3==0)
    line(draw,[(x-7,y+3),(x+6,y-3)],WOOD[0],4)
    line(draw,[(x-5,y-2),(x+7,y+4)],WOOD[2],3)
    tip=(frame%3)-1
    polygon(draw,[(x-5,y+1),(x-6,y-6),(x-2,y-4),(x+tip,y-14-frame%3),(x+4,y-8),(x+6,y-3),(x+5,y+1)],'#B75B34',WOOD[0])
    polygon(draw,[(x-3,y+1),(x-4,y-4),(x,y-2),(x+1+tip,y-9),(x+3,y-4),(x+4,y)],'#F59A44')
    polygon(draw,[(x-2,y+1),(x-1,y-3),(x+1,y-5),(x+2,y)],'#FFDA8B')
    if frame%2:
        box(draw,(x+3,y-13,x+4,y-12),'#EFB46A')


def crate(draw,x,y,w,h,opened=False):
    polygon(draw,[(x,y+4),(x+6,y),(x+w-6,y),(x+w,y+4),(x+w,y+h),(x,y+h)],WOOD[1],OUTLINE)
    box(draw,(x+2,y+5,x+w-2,y+h-3),WOOD[0] if opened else WOOD[2],WOOD[0])
    for yy in range(y+6,y+h-3,4):
        line(draw,[(x+3,yy),(x+w-3,yy)],WOOD[1])
    for xx in (x+1,x+w-3):
        box(draw,(xx,y+2,xx+2,y+h-1),SLATE[1])
        box(draw,(xx+1,y+4,xx+1,y+5),SLATE[3])
    if not opened:
        box(draw,(x+w//2-2,y+h//2,x+w//2+2,y+h//2+4),PAPER[1],WOOD[0])
        box(draw,(x+w//2,y+h//2+1,x+w//2,y+h//2+2),WOOD[0])


def tent(draw,x,y,w,h,seed=0):
    ridge=x+w//2-3
    polygon(draw,[(x,y+h),(ridge,y),(x+w-3,y+h-4),(x+w,y+h),(x+w//2+3,y+h-2)],CLOTH[2],OUTLINE)
    polygon(draw,[(ridge,y+2),(ridge-3,y+h-2),(x+2,y+h-2)],PAPER[2],CLOTH[1])
    polygon(draw,[(ridge+1,y+4),(ridge+5,y+h-3),(x+w-9,y+h-5)],CLOTH[1])
    polygon(draw,[(ridge+1,y+h//3),(ridge+3,y+h-4),(ridge+12,y+h-3)],'#3C2B35',WOOD[0])
    line(draw,[(ridge,y+1),(x+2,y+h+6)],PAPER[1])
    line(draw,[(ridge+1,y),(x+w+4,y+h+5)],PAPER[1])
    line(draw,[(ridge-6,y+12),(ridge-12,y+h-4)],PAPER[3])
    line(draw,[(ridge+5,y+12),(ridge+15,y+h-5)],CLOTH[3])
    lantern(draw,x+w-4,y+h-11)


def log(draw,x,y,w,h):
    polygon(draw,[(x+3,y),(x+w-4,y),(x+w,y+3),(x+w,y+h-4),(x+w-4,y+h),
                  (x+3,y+h),(x,y+h-4),(x,y+3)],WOOD[1],OUTLINE)
    line(draw,[(x+3,y+2),(x+w-4,y+2)],WOOD[3],2)
    line(draw,[(x+3,y+5),(x+w-4,y+5)],WOOD[2])
    for yy in range(y+7,y+h-2,3):
        line(draw,[(x+4,yy),(x+w-2,yy)],WOOD[0])
    ellipse(draw,(x,y+1,x+8,y+h-1),WOOD[3],WOOD[0])
    ellipse(draw,(x+2,y+4,x+6,y+h-4),WOOD[2],WOOD[1])


def mat(draw,x,y,w,h,variant=0):
    colors=(PAPER,INDIGO,CLOTH)[variant%3]
    box(draw,(x,y,x+w,y+h),colors[1],colors[0])
    box(draw,(x+2,y+2,x+w-2,y+h-2),colors[2],colors[1])
    box(draw,(x+4,y+4,x+w-4,y+h-4),colors[1],colors[0])
    for i in range(2,h-2,4):
        line(draw,[(x-2,y+i),(x,y+i)],colors[2])
        line(draw,[(x+w,y+i),(x+w+2,y+i)],colors[2])
    for xx in range(x+8,x+w-5,8):
        polygon(draw,[(xx,y+6),(xx+3,y+h//2),(xx,y+h-6),(xx-3,y+h//2)],colors[2])


def board(draw,x,y,w,h,frame):
    timber(draw,x+3,y+14,5,h-8,True)
    timber(draw,x+w-8,y+14,5,h-8,True)
    box(draw,(x+4,y+12,x+w-4,y+h-4),WOOD[1],WOOD[0])
    for row,yy in enumerate(range(y+15,y+h-5,5)):
        line(draw,[(x+5,yy),(x+w-5,yy)],WOOD[0 if row%2 else 2])
    for i in range(5):
        xx=x+7+(i*11)%(w-14)
        yy=y+16+(i%2)*10
        paper(draw,xx,yy,7+i%3,9+(frame if i==3 else 0),i+frame)
        box(draw,(xx+3,yy+1,xx+3,yy+1),WOOD[0])
    roof(draw,x,y,w,14,0)


def hair(draw,cx,y,face,pose):
    ellipse(draw,(cx-13,y,cx+13,y+24),'#E9C29A',OUTLINE)
    polygon(draw,[(cx-13,y+6),(cx-10,y+4),(cx-9,y+11),(cx-12,y+14),(cx-15,y+13)],'#E4D7BD',OUTLINE)
    polygon(draw,[(cx+13,y+6),(cx+10,y+4),(cx+9,y+11),(cx+12,y+14),(cx+15,y+13)],'#E4D7BD',OUTLINE)
    line(draw,[(cx-8,y+6),(cx-2,y+6),(cx+3,y+7)],'#BD8B76')
    for dx in (-5,5):
        line(draw,[(cx+dx-2,y+9),(cx+dx+2,y+9)],'#FAE9CB',2)
        if pose==0 or pose==1 and dx<0:
            line(draw,[(cx+dx-1,y+13),(cx+dx+1,y+13)],'#422F3A')
        else:
            box(draw,(cx+dx-1,y+12,cx+dx+1,y+14),'#362732')
    if pose==2:
        box(draw,(cx-2,y+18,cx+2,y+20),'#633D43')
    if face:
        polygon(draw,[(cx-11,y+16),(cx-6,y+15),(cx,y+17),(cx+6,y+15),(cx+11,y+17),
                      (cx+9,y+28),(cx+4,y+34),(cx-3,y+34),(cx-9,y+29)],'#E5D9C4','#54454C')
        polygon(draw,[(cx-5,y+21),(cx-1,y+19),(cx+4,y+21),(cx+5,y+28),(cx+1,y+33),(cx-2,y+27)],'#F9E9D0')
        line(draw,[(cx-7,y+22),(cx-6,y+28),(cx-3,y+31)],'#AA9B91')
        if pose==2:
            box(draw,(cx-2,y+18,cx+2,y+20),'#633D43')


def master(draw,cx,y,pose,portrait=False):
    if portrait:
        polygon(draw,[(cx-18,y+25),(cx-13,y+20),(cx+12,y+20),(cx+17,y+25),(cx+20,y+43),(cx-20,y+43)],'#677059',OUTLINE)
        hair(draw,cx,y,True,pose)
        return
    timber(draw,cx-20,y+13,5,29,True)
    timber(draw,cx+14,y+23,4,18,True)
    line(draw,[(cx-23,y+40),(cx-17,y+45),(cx+11,y+45),(cx+19,y+41)],WOOD[0],4)
    line(draw,[(cx-22,y+40),(cx-17,y+43),(cx+10,y+43),(cx+17,y+41)],WOOD[3])
    polygon(draw,[(cx-13,y+20),(cx+10,y+20),(cx+14,y+26),(cx+16,y+38),(cx+10,y+42),
                  (cx-13,y+42),(cx-16,y+38),(cx-15,y+28)],'#657052',OUTLINE)
    line(draw,[(cx-13,y+25),(cx-12,y+35),(cx-7,y+40)],'#8B8962',2)
    hair(draw,cx,y,True,pose)
    timber(draw,cx-19,y+29,9,4)
    timber(draw,cx+9,y+30,10,4)
    ellipse(draw,(cx-14,y+27,cx-8,y+32),'#EBC5A0',WOOD[0])
    arm_y=y+(20 if pose in (2,3,4) else 28)
    ellipse(draw,(cx+11,arm_y,cx+17,arm_y+6),'#EBC5A0',WOOD[0])
    if pose==3:
        ellipse(draw,(cx+15,arm_y-4,cx+21,arm_y+1),'#C79348',WOOD[0])
    if pose==4:
        ellipse(draw,(cx+6,arm_y,cx+12,arm_y+5),'#EBC5A0',WOOD[0])


def merchant(draw,cx,y,pose,portrait=False):
    if not portrait:
        polygon(draw,[(cx-12,y+28),(cx+12,y+28),(cx+16,y+44),(cx+10,y+49),(cx-10,y+49),(cx-16,y+44)],INDIGO[1],OUTLINE)
        line(draw,[(cx-7,y+31),(cx+3,y+39),(cx+9,y+38)],'#B6AE9B',2)
        box(draw,(cx-12,y+48,cx-4,y+51),WOOD[1],OUTLINE)
        box(draw,(cx+4,y+48,cx+12,y+51),WOOD[1],OUTLINE)
        box(draw,(cx-11,y+39,cx+11,y+42),WOOD[0])
        box(draw,(cx+4,y+40,cx+10,y+46),WOOD[2],WOOD[0])
        if pose:
            line(draw,[(cx+12,y+32),(cx+19,y+38),(cx+22,y+30)],INDIGO[2],5)
            ellipse(draw,(cx+20,y+27,cx+25,y+32),'#E9C29A',OUTLINE)
    else:
        polygon(draw,[(cx-18,y+32),(cx+18,y+32),(cx+23,y+47),(cx-23,y+47)],INDIGO[1],OUTLINE)
        line(draw,[(cx-12,y+33),(cx-2,y+40),(cx+14,y+37)],'#C6BBA1',3)
    ellipse(draw,(cx-12,y+14,cx+12,y+33),'#DBAD88',OUTLINE)
    line(draw,[(cx-9,y+29),(cx-4,y+32),(cx+5,y+32),(cx+9,y+28)],WOOD[1],2)
    for x in (cx-5,cx+5):
        line(draw,[(x-1,y+23),(x+1,y+23)],'#322A31',2)
    line(draw,[(cx-2,y+27),(cx+3,y+27+(pose%2))],WOOD[0])
    polygon(draw,[(cx-22,y+17),(cx-9,y+9),(cx-2,y),(cx+3,y+1),(cx+10,y+10),
                  (cx+22,y+17),(cx+18,y+19),(cx-17,y+19)],PAPER[1],OUTLINE)
    line(draw,[(cx-2,y+2),(cx-11,y+12),(cx-20,y+17)],PAPER[2],2)
    for xx in (cx-10,cx-4,cx+4,cx+10):
        line(draw,[(cx,y+3),(xx,y+15)],PAPER[0])
    line(draw,[(cx-18,y+17),(cx,y+16),(cx+19,y+17)],PAPER[3])


def bird(draw,x,y,pose):
    if pose==0:
        polygon(draw,[(x+2,y+11),(x+1,y-3),(x+8,y+2),(x+12,y+9),(x+18,y-4),(x+23,y-6),(x+22,y+4),(x+16,y+13)],INDIGO[1],OUTLINE)
    elif pose==1:
        polygon(draw,[(x+2,y+9),(x-7,y+4),(x+7,y+3),(x+12,y+8),(x+19,y+5),(x+27,y+7),(x+23,y+12)],INDIGO[1],OUTLINE)
    elif pose==2:
        polygon(draw,[(x+3,y+10),(x+4,y+23),(x+10,y+18),(x+12,y+12),(x+18,y+23),(x+20,y+22),(x+19,y+12)],INDIGO[1],OUTLINE)
    polygon(draw,[(x-2,y+16),(x+4,y+9),(x+10,y+7),(x+12,y+2),(x+17,y+1),(x+21,y+4),(x+20,y+9),(x+16,y+13),(x+8,y+16)],INDIGO[0],OUTLINE)
    line(draw,[(x+2,y+13),(x+8,y+9),(x+14,y+9)],INDIGO[2],2)
    polygon(draw,[(x+19,y+5),(x+24,y+7),(x+19,y+8)],PAPER[0])
    box(draw,(x+17,y+4,x+17,y+4),'#EFE2A1')
    if pose==3:
        line(draw,[(x+12,y+15),(x+12,y+18),(x+15,y+18)],WOOD[2])


def moon(draw,cx,cy,r):
    ellipse(draw,(cx-r,cy-r,cx+r,cy+r),'#B2C2CE','#65788D')
    ellipse(draw,(cx-r+2,cy-r+2,cx+r-3,cy+r-3),'#D1D7D3')
    for dx,dy,rx,ry in ((-7,-4,5,4),(5,7,6,4),(8,-5,4,3),(-9,7,4,2),(0,1,3,4)):
        f=r/18
        xx,yy=cx+round(dx*f),cy+round(dy*f)
        ellipse(draw,(xx-max(1,round(rx*f)),yy-max(1,round(ry*f)),xx+max(1,round(rx*f)),yy+max(1,round(ry*f))),'#A4B5C4')


def potion(draw,cx,cy,triangular=False):
    if triangular:
        polygon(draw,[(cx-3,cy-13),(cx+4,cy-13),(cx+4,cy-6),(cx+12,cy+10),(cx+9,cy+15),(cx-12,cy+15),(cx-12,cy+11),(cx-3,cy-6)],'#57714D',OUTLINE)
        polygon(draw,[(cx-6,cy+3),(cx+6,cy+1),(cx+10,cy+12),(cx-10,cy+12)],'#777952')
    else:
        ellipse(draw,(cx-12,cy-4,cx+12,cy+17),'#436D69',OUTLINE)
        ellipse(draw,(cx-9,cy,cx+10,cy+14),'#51877D')
        box(draw,(cx-4,cy-14,cx+4,cy-1),'#60998A',OUTLINE)
    box(draw,(cx-5,cy-15,cx+5,cy-11),WOOD[2],WOOD[0])
    line(draw,[(cx-4,cy-7),(cx-8,cy+1),(cx-8,cy+6)],'#A5B8A0')
    line(draw,[(cx+10,cy+4),(cx+3,cy+3),(cx+1,cy+7),(cx-4,cy+5)],OUTLINE)
    line(draw,[(cx+6,cy+15),(cx+15,cy+18),(cx+19,cy+17)],'#49726A',2)


def field_prop(draw,i):
    x,y=47,48
    if i==0:
        timber(draw,x-2,y-19,4,38,True)
        polygon(draw,[(x-22,y-3),(x-13,y-12),(x+18,y-12),(x+20,y-7),(x+19,y+1),(x-13,y+2)],WOOD[2],OUTLINE)
        line(draw,[(x-12,y-9),(x+16,y-9)],WOOD[3])
        line(draw,[(x+7,y-5),(x-12,y-5),(x-7,y-9)],PAPER[3],2)
        line(draw,[(x-12,y-5),(x-7,y-1)],PAPER[3],2)
    elif i in (1,2):
        potion(draw,x,y,i==2)
    elif i==3:
        timber(draw,x-12,y-19,4,31,True)
        timber(draw,x+8,y-17,4,31,True)
        for dx in (-7,-2,3):
            timber(draw,x+dx,y-18,2,25,True)
        timber(draw,x-14,y+3,28,5)
        line(draw,[(x-20,y+10),(x-13,y+15),(x+12,y+15),(x+19,y+10)],WOOD[0],4)
        line(draw,[(x-19,y+10),(x-13,y+13),(x+12,y+13),(x+18,y+10)],WOOD[3])
    elif i==4:
        for r,c in ((16,PAPER[0]),(13,PAPER[2]),(10,'#825042'),(7,PAPER[2]),(4,'#825042')):
            ellipse(draw,(x-r,y-r,x+r,y+r),c,WOOD[0] if r==16 else None)
        line(draw,[(x-17,y+11),(x-8,y+15),(x+15,y+13)],MOSS[2],3)
    elif i==5:
        for k in range(8):
            import math
            a=k*math.tau/8
            xx=x+int(22*math.cos(a))
            yy=y+int(13*math.sin(a))
            timber(draw,xx-1,yy,2,5,True)
            ellipse(draw,(xx-5,yy-5,xx+5,yy+1),PAPER[2],WOOD[0])
            box(draw,(xx-3,yy-3,xx,yy-2),PAPER[3])
    elif i==6:
        line(draw,[(x-19,y+13),(x-12,y+9),(x+17,y-15)],SLATE[0],5)
        line(draw,[(x-19,y+5),(x-14,y+9),(x+21,y-8)],SLATE[1],5)
        line(draw,[(x-12,y+8),(x+16,y-14)],SLATE[3])
        ellipse(draw,(x-16,y+6,x-10,y+12),SLATE[2],SLATE[0])
    elif i==7:
        polygon(draw,[(x-13,y-12),(x-3,y-15),(x+11,y-11),(x+9,y-1),(x+14,y+11),(x+8,y+15),(x-9,y+13),(x-14,y+6)],'#8D5141',OUTLINE)
        line(draw,[(x-11,y-7),(x-3,y-8),(x+4,y-5),(x+8,y-8)],'#C18357',2)
        line(draw,[(x+6,y-8),(x+1,y-3),(x+4,y+2),(x-3,y+9)],WOOD[0],2)
    elif i==8:
        box(draw,(x-6,y-12,x+6,y+9),'#917544',OUTLINE)
        line(draw,[(x,y-5),(x,y+8),(x-6,y+14),(x-12,y+12),(x-13,y+6)],PAPER[1],3)
        line(draw,[(x,y+8),(x+6,y+14),(x+12,y+12),(x+13,y+6)],PAPER[1],3)
    elif i==9:
        stone(draw,x-19,y-13,25,25,8)
        stone(draw,x+7,y+4,13,11,4)
        line(draw,[(x-9,y-6),(x-9,y+6),(x-4,y+3),(x-2,y+4)],'#B4B494',2)
    elif i==10:
        polygon(draw,[(x-10,y-12),(x+9,y-12),(x+10,y-5),(x+16,y+8),(x+9,y+13),
                      (x-11,y+12),(x-15,y+7),(x-13,y-5)],PAPER[1],WOOD[0])
        line(draw,[(x-12,y-6),(x+11,y-5)],WOOD[2],3)
        line(draw,[(x-9,y),(x-10,y+7),(x-4,y+9)],PAPER[2],2)
        for xx,yy in ((x+12,y+10),(x+15,y+13),(x+9,y+15)):
            box(draw,(xx,yy,xx+2,yy+2),MOSS[2])
    elif i==11:
        for r in (14,11,8,5):
            ellipse(draw,(x-r,y-r//2,x+r,y+r//2),None,WOOD[3] if r%2==0 else PAPER[0])
        line(draw,[(x+8,y+4),(x+18,y+10),(x+21,y+7)],WOOD[3],2)
    elif i in (12,13,14):
        for k in range(3+i%3):
            stone(draw,x-20+k*9,y+(k%2)*4,13+k%2*4,10+k%3*3,k+i)
    else:
        fire(draw,x,y+8,39,1)


def scarecrow(draw,x,y,frame):
    timber(draw,x-3,y+20,6,26,True)
    dx=(0,5,-5,0)[frame%4]
    timber(draw,x-23+dx,y+16,45,4)
    polygon(draw,[(x+dx-12,y+10),(x+dx+12,y+10),(x+dx+9,y+34),(x+dx-10,y+34)],PAPER[1],WOOD[0])
    for k in (-1,1):
        for i in range(3):
            line(draw,[(x+dx+k*16,y+16+i),(x+dx+k*(22+i),y+19+i)],PAPER[2])
    ellipse(draw,(x+dx-8,y+15,x+dx+8,y+31),'#9C644A',WOOD[0])
    ellipse(draw,(x+dx-5,y+18,x+dx+5,y+28),PAPER[2])
    ellipse(draw,(x+dx-2,y+21,x+dx+2,y+25),'#986344')
    polygon(draw,[(x+dx-16,y+10),(x+dx-7,y+4),(x+dx-1,y),(x+dx+5,y+3),(x+dx+16,y+10),(x+dx+9,y+12),(x+dx-8,y+12)],PAPER[2],WOOD[0])
    line(draw,[(x+dx-13,y+10),(x+dx,y+9),(x+dx+12,y+10)],PAPER[3])


def mailbox(draw,x,y):
    timber(draw,x-3,y+18,6,33,True)
    box(draw,(x-14,y+14,x+14,y+33),WOOD[2],OUTLINE)
    box(draw,(x-7,y+20,x+7,y+24),WOOD[0],OUTLINE)
    line(draw,[(x-7,y+25),(x+7,y+25)],WOOD[3])
    roof(draw,x-18,y,36,16,2)


def cart(draw,x,y):
    for cx in (x+10,x+32):
        ellipse(draw,(cx-7,y+20,cx+7,y+34),WOOD[0],OUTLINE)
        ellipse(draw,(cx-4,y+23,cx+4,y+31),WOOD[2],WOOD[0])
        line(draw,[(cx,y+24),(cx,y+30)],WOOD[0])
    timber(draw,x+2,y+10,40,13)
    timber(draw,x+3,y-14,3,26,True)
    timber(draw,x+37,y-12,3,24,True)
    polygon(draw,[(x,y-12),(x+8,y-19),(x+35,y-18),(x+43,y-11),(x+41,y-6),(x+34,y-8),(x+9,y-7),(x+1,y-6)],'#777751',OUTLINE)
    line(draw,[(x+7,y-16),(x+35,y-15),(x+41,y-11)],'#ACA275')
    line(draw,[(x-14,y+21),(x+3,y+16)],WOOD[2],3)


def monument(draw,x,y):
    stone(draw,x-16,y,32,45,23,standing=True)
    box(draw,(x-19,y+42,x+19,y+46),SLATE[2],SLATE[0])
    box(draw,(x-23,y+47,x+23,y+50),SLATE[1],SLATE[0])
    line(draw,[(x,y+13),(x,y+30)],'#AAA58A')
    for dx in (-1,1):
        line(draw,[(x,y+24),(x+dx*6,y+20),(x+dx*6,y+18)],'#A3A18B')
    line(draw,[(x-8,y+35),(x+8,y+35)],'#8B8F83')


def window(draw,x,y,pose=-1):
    box(draw,(x-18,y,x+18,y+39),SLATE[1],SLATE[0])
    for yy in range(y+3,y+39,6):
        line(draw,[(x-17,yy),(x+17,yy)],SLATE[0])
    roof(draw,x-22,y-6,44,11)
    box(draw,(x-13,y+9,x+13,y+30),WOOD[0],OUTLINE)
    if pose>=0:
        head=Image.new('RGBA',(36,40))
        hair(ImageDraw.Draw(head),18,2,True,pose)
        head=head.crop((0,0,36,33)).resize((22,20),NEAREST)
        draw._image.alpha_composite(head,(x-11,y+12))
        if pose==2:
            line(draw,[(x+9,y+27),(x+20,y+20)],'#DFBA97',3)
            stone(draw,x+20,y+17,7,5,3,moss=False)
    else:
        for side in (-1,1):
            timber(draw,x+side*9-3,y+10,5,19,True)
    timber(draw,x-16,y+31,32,3)


def shrub_gate(draw,x,y,w,h,frame):
    left=w//2-12
    foliage(draw,x,y+frame%2,left,h,frame+14,thorn=True)
    foliage(draw,x+w-left,y+(frame+1)%2,left,h,frame+28,thorn=True)
    for dx in (7,w-14):
        stone(draw,x+dx,y+h-9,9,7,frame+dx)
    branch=x+left
    line(draw,[(branch-6,y+h-4),(branch+2,y+h//2),(branch+2+frame,y+h//2-5)],WOOD[1],2)
    branch=x+w-left
    line(draw,[(branch+6,y+h-4),(branch-3,y+h//2),(branch-4-frame,y+h//2-4)],WOOD[1],2)


def field_walls(draw,index):
    if index in (0,1,2,8,9,10):
        if index<8:
            foliage(draw,12,37-index*3,72,25+index*2,27+index,thorn=True)
        else:
            for i in range(5):
                stone(draw,9+15*i,39+(i%2)*3,16+(i%3),19+(index+i*5)%12,index+i,standing=True)
    elif index in (3,5,11):
        if index==11:
            for i in range(4):
                stone(draw,34+i%2*3,14+i*15,25,21+(i%2)*3,index+i,standing=True)
        else:
            foliage(draw,35,11,26,74,index,thorn=True)
    elif index in (4,12):
        if index==4:
            foliage(draw,16,11,23,71,index,thorn=True)
            foliage(draw,18,59,61,22,index+10,thorn=True)
        else:
            for i in range(4):
                stone(draw,16,12+i*15,20,21,index+i,standing=True)
            for i in range(3):
                stone(draw,29+i*16,61,18,19,index+i,standing=True)
    elif index in (6,7):
        shrub_gate(draw,9,34,78,28,index)
    elif index in (13,14):
        stone(draw,32,23,32,49 if index==13 else 37,index,standing=True)
    elif index==15:
        for i,(x,y) in enumerate(((31,47),(52,29),(60,54))):
            stone(draw,x,y,8,6,i,moss=False)


def plaza_asset(draw,index):
    if index==0:
        fire(draw,47,52,60,2)
    elif index in (1,2,3):
        if index==1:
            log(draw,18,41,61,15)
        else:
            im=Image.new('RGBA',(96,96))
            log(ImageDraw.Draw(im),18,41,61,15)
            draw._image.alpha_composite(im.rotate(90 if index==2 else 29,resample=NEAREST))
    elif index in (4,5,6):
        mat(draw,18,30,58,34,index-4)
    elif index in (7,8):
        crate(draw,18,29,59,36,opened=index==7)
    elif index==9:
        board(draw,17,24,62,44,0)
    elif index==10:
        stone(draw,21,24,17,40,21,standing=True)
        stone(draw,58,24,17,40,32,standing=True)
        for i in range(4):
            stone(draw,44-i,53+i*5,9+i,4,i,moss=False)
    elif index==11:
        monument(draw,48,20)
    elif index==12:
        mailbox(draw,48,23)
    elif index==13:
        scarecrow(draw,48,22,0)
    elif index==14:
        cart(draw,31,45)
    else:
        shrub_gate(draw,10,32,75,28,0)


def people_asset(draw,index):
    if index<5:
        master(draw,47,22,index)
    elif index<7:
        merchant(draw,46,23,index-5)
    elif index<11:
        bird(draw,39,38,index-7)
    elif index==11:
        paper(draw,30,38,33,22,3)
        line(draw,[(31,41),(47,52),(61,40)],WOOD[2])
        ellipse(draw,(42,48,51,56),'#5F8179',WOOD[0])
    elif index==12:
        master(draw,48,24,1,portrait=True)
    elif index==13:
        merchant(draw,47,22,0,portrait=True)
    elif index==14:
        window(draw,48,32,0)
    else:
        window(draw,48,32,2)


def scenery_asset(draw,index):
    if index in (0,1):
        tower(draw,19,31,57,28,index,with_laundry=index==0)
    elif index==2:
        tent(draw,23,20,53,42,3)
    elif index==3:
        clothesline(draw,23,34,49,3)
    elif index==4:
        timber(draw,24,31,4,29,True)
        timber(draw,69,31,4,29,True)
        timber(draw,22,30,53,4)
        line(draw,[(48,33),(48,43)],SLATE[3],2)
        ellipse(draw,(34,41,62,65),SLATE[1],OUTLINE)
        ellipse(draw,(36,41,60,48),SLATE[0],SLATE[2])
        line(draw,[(36,53),(42,57),(52,57)],SLATE[2],2)
    elif index==5:
        for i in range(3):
            log(draw,24+i*5,48-i*7,45-i*8,10)
    elif index==6:
        ellipse(draw,(30,43,66,66),PAPER[1],WOOD[0])
        for k in range(3):
            line(draw,[(34,49+k*5),(62,48+k*5)],WOOD[2])
        ellipse(draw,(31,36,67,51),PAPER[1],WOOD[0])
        ellipse(draw,(35,38,63,47),WOOD[0])
        polygon(draw,[(34,39),(42,39),(55,50),(51,59),(44,54)],PAPER[3],WOOD[2])
    elif index==7:
        box(draw,(32,34,64,55),CLOTH[2],CLOTH[0])
        for x in range(36,60,5):
            line(draw,[(x,35),(x+3,54)],CLOTH[1])
        ellipse(draw,(29,34,45,55),CLOTH[3],CLOTH[0])
        ellipse(draw,(33,39,42,51),CLOTH[1])
    elif index==8:
        box(draw,(35,34,62,58),WOOD[2],WOOD[0])
        ellipse(draw,(35,30,62,39),WOOD[3],WOOD[0])
        for y in (43,52):
            box(draw,(35,y,62,y+2),SLATE[1],SLATE[0])
        line(draw,[(42,40),(42,55)],WOOD[3])
    elif index==9:
        for x in (38,44,50,55):
            line(draw,[(x,36),(x-4,59)],WOOD[1],5)
            line(draw,[(x+1,35),(x-3,57)],WOOD[3])
        line(draw,[(36,46),(57,49)],PAPER[1],3)
    elif index==10:
        moon(draw,48,48,19)
    elif index==11:
        gem(draw,48,48,10)
    elif index==12:
        puddle(draw,48,48,1)
    elif index==13:
        fog(draw,48,48,0)
    elif index==14:
        window(draw,48,31)
    else:
        for i in range(3):
            xx=23+i*25
            yy=39+(i%2)*6
            polygon(draw,[(xx-10,yy+9),(xx-6,yy+5),(xx,yy-6),(xx+5,yy+5),(xx+10,yy+9),
                          (xx+7,yy+10),(xx+4,yy+11),(xx+8,yy+29),(xx-9,yy+29),(xx-4,yy+11)],
                    INDIGO[i%2],OUTLINE)
            line(draw,[(xx-5,yy+7),(xx+5,yy+7)],INDIGO[2])


def gem(draw,x,y,r):
    polygon(draw,[(x,y-r),(x+r//2,y),(x,y+r),(x-r//2,y)],'#D8AD6C','#7A5C56')
    polygon(draw,[(x,y-r+2),(x+1,y+2),(x-r//2+1,y)],'#FFF1C3')
    line(draw,[(x,y+r-1),(x+2,y+1),(x+r//2-1,y)],'#B08555')


def puddle(draw,x,y,frame):
    polygon(draw,[(x-27,y),(x-22,y-5),(x-8,y-8),(x+5,y-6),(x+19,y-8),(x+27,y-2),
                  (x+24,y+5),(x+7,y+8),(x-11,y+6),(x-21,y+7)],'#526E86')
    ellipse(draw,(x-9-frame*3,y-3-frame,x+9+frame*3,y+3+frame),None,'#88A5BD')
    line(draw,[(x-17,y+1),(x-11,y)],'#9FB3C9')


def fog(draw,x,y,frame):
    for i in range(4):
        ox=(-16,3,19,-2)[i]+frame
        oy=(0,-3,1,4)[i]
        ellipse(draw,(x-23+ox,y-8+oy,x+23+ox,y+8+oy),('#8A9AB1','#9AAAC0','#A5B6CB','#B2C2D2')[i])


def effect_asset(draw,index):
    x,y=48,48
    if index<4:
        r=7+index*5
        ellipse(draw,(x-r,y-r,x+r,y+r),None,'#E5BF82')
    elif index==4:
        line(draw,[(34,58),(59,34)],'#C6925B',3)
        line(draw,[(43,51),(59,34)],'#FFEAB4',2)
    elif index==5:
        line(draw,[(28,58),(63,33)],'#D3A169',3)
        gem(draw,44,45,11)
    elif index in (6,7):
        gem(draw,x,y,14)
        if index==7:
            line(draw,[(x+12,y-14),(x+12,y-6)],'#FFEEBC',2)
            line(draw,[(x+9,y-10),(x+15,y-10)],'#F8E1A5',2)
    elif index==8:
        ellipse(draw,(17,17,79,79),'#819FBD')
        ellipse(draw,(19,19,77,77),'#9BB3D1')
    elif index==9:
        moon(draw,x,y,19)
    elif index in (10,11):
        fog(draw,x,y,index-10)
    elif index==12:
        line(draw,[(48,28),(48,68)],PAPER[2],2)
    elif index in (13,14):
        line(draw,[(37,48),(59,48)],PAPER[2],2)
        line(draw,[(48,37),(48,59)],PAPER[2],2)
    else:
        for i,(xx,yy) in enumerate(((32,43),(50,33),(52,56))):
            stone(draw,xx,yy,8,6,i,False)


def weather_asset(draw,index):
    if index<4:
        fire(draw,47,53,44,index)
    elif index<10:
        fire(draw,47,54,62,index)
    elif index<13:
        puddle(draw,47,48,index-10)
    else:
        rng=Random(index)
        for i in range(12):
            x,y=rng.randint(14,82),rng.randint(10,80)
            line(draw,[(x,y),(x-2,y+7)],'#86A7C2')
            box(draw,(x,y,x,y+1),'#ADC4D7')


def action_asset(draw,index):
    if index<4:
        shrub_gate(draw,8,33,79,30,index)
    elif index<8:
        tower(draw,11,28,45,33,2,False)
        clothesline(draw,53,40,32,index)
    elif index<12:
        scarecrow(draw,47,22,index-8)
    else:
        board(draw,17,21,61,46,index-12)


def terrain_asset(draw,index):
    rng=Random(401+index)
    colors=(('#293D32','#304438','#34493C','#4B5942'),
            ('#3B342D','#433B30','#483E33','#525149'),
            ('#35332E','#413B34','#453E36','#4C4840'),
            ('#292E36','#343B46','#3A424B','#444B53'))[index//4]
    box(draw,(0,0,95,95),colors[1])
    if index>=12:
        for y in range(-10,100,16):
            for x in range(-16,100,24):
                shift=12 if y//16%2 else 0
                xx=x+shift+rng.randint(-2,2)
                polygon(draw,[(xx,y+4),(xx+5,y),(xx+19,y+1),(xx+24,y+4),
                              (xx+23,y+12),(xx+17,y+16),(xx+3,y+15),(xx-1,y+12)],colors[1+int(rng.random()>.47)],colors[0])
                line(draw,[(xx+4,y+2),(xx+17,y+3)],colors[3])
    for i in range(125):
        x,y=rng.randrange(96),rng.randrange(96)
        c=colors[0 if i%3 else 2]
        polygon(draw,[(x,y),(x+2,y),(x+3,y-1),(x+6,y-1),(x+6,y+1),(x+4,y+1),(x+4,y+2),(x,y+2)],c)
    if index<4:
        for i in range(8):
            x,y=rng.randrange(5,90),rng.randrange(6,92)
            line(draw,[(x-2,y-3),(x,y),(x+1,y-5)],colors[3])
            line(draw,[(x+2,y),(x+4,y-2)],colors[2])
    elif index<12:
        for i in range(13):
            x,y=rng.randrange(4,92),rng.randrange(4,92)
            box(draw,(x,y,x+rng.choice((1,2,3)),y+1),colors[3])


def make_sources():
    makers={'terrain':terrain_asset,'field':field_prop,'walls':field_walls,'plaza':plaza_asset,
            'people':people_asset,'scenery':scenery_asset,'effects':effect_asset,'fire':weather_asset,'actions':action_asset}
    result={}
    for key,make in makers.items():
        atlas=Image.new('RGBA',(384,384))
        for i in range(16):
            tile=Image.new('RGBA',(96,96))
            make(ImageDraw.Draw(tile),i)
            atlas.alpha_composite(tile,((i%4)*96,(i//4)*96))
        result[key]=atlas
    return result


SCENE_PALETTES={
    'field':{
        'sky':('#171B36','#262743','#383653','#5D4B62','#8C6071'),
        'mountains':('#2A2945','#323550','#303B4D'),
        'ground':('#253333','#303B35','#394337','#44463A','#4D4A3B'),
        'interior':('#363D34','#3D4236','#444436','#4A4537'),
        'pine':('#182833','#253A3B','#384C48','#586153')
    },
    'plaza':{
        'sky':('#191C37','#30304F','#54445C','#805766','#AF786B'),
        'mountains':('#29273F','#3A344E','#3D3E50'),
        'ground':('#293236','#383A37','#45433B','#524B40','#635343'),
        'interior':('#463C37','#51443C','#5B4A40','#635142'),
        'pine':('#1D2734','#2B3740','#3B4646','#5B5852')
    },
    'arena':{
        'sky':('#435367','#526174','#647185','#6D7988','#818794'),
        'mountains':('#384555','#445566','#43535A'),
        'ground':('#303941','#364349','#43514F','#4B5855','#5D675D'),
        'interior':('#303B4A','#394654','#465362','#515E6A'),
        'pine':('#273640','#354A4F','#465F5E','#69776B')
    }
}


def distant_hills(draw,width,palette):
    for i,(upper,color) in enumerate(zip((45,68,87),palette)):
        ridge=[(0,upper+10)]
        rng=Random(208+i)
        for x in range(12,width,14):
            wave=rng.randint(-11,9)
            ridge.append((x,upper+wave+((x//96)%2)*9))
        ridge.extend([(width,upper+12),(width,145),(0,145)])
        polygon(draw,ridge,color)
        for k in range(38):
            x=rng.randint(0,width)
            y=upper+13+rng.randrange(22)
            line(draw,[(x,y),(x+rng.randint(2,11),y)],color)


def tiled_court(draw,rect,palette):
    x0,y0,x1,y1=rect
    base,middle,light,highlight=palette
    box(draw,rect,middle)
    rng=Random(9182)
    rows=[y0,y0+17,y0+36,y0+56,y0+79,y0+102,y0+127,y0+153,y0+182,y0+211,y1]
    for row,(a,b) in enumerate(zip(rows,rows[1:])):
        xx=x0-(15 if row%2 else 0)
        while xx<x1:
            ww=rng.randint(27,54)
            left=max(x0,xx)
            right=min(x1,xx+ww)
            top=a+rng.randint(-1,2)
            bottom=b+rng.randint(-2,1)
            slope=rng.randint(-2,3)
            stone_points=[(left,top+4),(left+5,top),(right-4,top+1),(right,top+4),
                          (right-1,bottom-4),(right-7,bottom),(left+3,bottom-1),(left,bottom-4)]
            color=(middle,light,light,highlight)[rng.randrange(4)]
            if right-left>8:
                polygon(draw,stone_points,color,base)
                line(draw,[(left+5,top+2),(right-6,top+3)],light)
                line(draw,[(left+4,top+4),(left+5,bottom-5)],light)
                for _ in range(8):
                    tx=rng.randrange(left+5,right-3)
                    ty=rng.randint(min(top+5,bottom-4),max(top+5,bottom-4))
                    line(draw,[(tx,ty),(min(right-2,tx+rng.randint(1,5)),ty+slope//2)],rng.choice((color,light,middle)))
            xx+=ww


def landscape_flora(draw,area,seed,rect):
    rng=Random(seed)
    left,top,right,bottom=rect
    pal=SCENE_PALETTES[area]
    petals={'field':('#57465A','#786375','#9B8290'),
            'plaza':('#5C435D','#8E5979','#B67A8C'),
            'arena':('#5E6674','#798496','#9199A2')}[area]
    for i in range(230):
        x,y=rng.randint(4,635),rng.randint(108,357)
        if left<x<right and top<y<bottom:
            continue
        if i%3==0:
            line(draw,[(x,y),(x-2,y-3),(x-3,y-4)],pal['pine'][2])
            line(draw,[(x,y),(x+2,y-4)],pal['pine'][3])
        else:
            color=petals[rng.randrange(3)]
            line(draw,[(x-1,y-2),(x+1,y-2)],color)
            box(draw,(x,y-3,x,y-1),color)
    for x,y,w,h in ((-10,289,52,44),(34,313,42,25),(539,314,45,39),(590,283,53,48),
                    (6,184,34,33),(590,193,34,27),(67,108,27,14),(563,96,36,25)):
        stone(draw,x,y,w,h,rng.randint(0,300),standing=True)


def decorated_gate(draw,x,y,wide):
    timber(draw,x-wide//2+9,y+10,6,45,True)
    timber(draw,x+wide//2-15,y+10,6,45,True)
    timber(draw,x-wide//2+13,y+15,wide-25,5)
    roof(draw,x-wide//2,y,wide,19,44)
    for dx in (-1,1):
        lantern(draw,x+dx*(wide//2-22),y+23)
    for i in range(4):
        yy=y+49+i*4
        box(draw,(x-19-i*2,yy,x+19+i*2,yy+3),SLATE[1],SLATE[0])
        line(draw,[(x-17-i*2,yy),(x+17+i*2,yy)],SLATE[2])


def detail_scene(area):
    width,height=640,360
    result=Image.new('RGB',(width,height))
    draw=ImageDraw.Draw(result)
    pal=SCENE_PALETTES[area]
    for i,c in enumerate(pal['sky']):
        box(draw,(0,i*18,width,18*(i+1)),c)
    rng=Random(701+sum(ord(ch) for ch in area))
    for i in range(62):
        x,y=rng.randint(0,width),rng.randint(1,77)
        size=rng.randint(9,62)
        color=pal['sky'][max(0,min(4,y//18+(i%2)))]
        polygon(draw,[(x,y),(x+size//3,y-2),(x+size*2//3,y-2),(x+size,y),
                      (x+size*5//6,y+2),(x+size//4,y+3)],color)
    if area!='arena':
        for i in range(34):
            x,y=rng.randint(0,width),rng.randint(2,59)
            box(draw,(x,y,x,y),rng.choice(('#83768E','#9A8DA5','#B9A6B4')))
        if area=='field':
            moon(draw,462,36,14)
    distant_hills(draw,width,pal['mountains'])
    ground=pal['ground']
    polygon(draw,[(0,106),(45,98),(85,103),(129,92),(194,102),(244,87),(310,96),
                  (363,90),(411,99),(464,91),(507,100),(558,93),(640,105),(640,360),(0,360)],ground[1])
    for i in range(1900):
        x,y=rng.randrange(width),rng.randrange(102,360)
        c=ground[rng.choice((0,1,1,2,2,3))]
        line(draw,[(x,y),(x+rng.randint(1,6),y)],c)
    for x in range(0,660,15):
        if area in ('field','plaza') and 260<x<358:
            continue
        y=rng.randint(54,88)
        h=rng.randint(27,58)
        pine(draw,x+rng.randint(-3,3),y,rng.randint(13,33),h,x,pal['pine'])
    for x,y in ((16,125),(48,105),(576,107),(614,120),(32,243),(609,235),(31,285),(617,280)):
        pine(draw,x,y,44,89,x+y,pal['pine'])
    interior=(64,136,576,312)
    if area=='field':
        pts=[(62,147),(111,132),(189,134),(274,128),(370,131),(474,128),(561,143),(580,163),(579,281),
             (570,309),(525,320),(410,323),(279,317),(147,322),(79,313),(61,286)]
        polygon(draw,pts,pal['interior'][0],ground[2])
        polygon(draw,[(132,192),(183,175),(256,174),(327,164),(414,172),(481,188),(507,220),
                      (491,265),(457,284),(381,295),(295,294),(219,284),(149,267),(125,236)],pal['interior'][1])
        polygon(draw,[(172,205),(226,189),(298,191),(363,183),(427,194),(474,217),(467,247),(432,267),(380,275),
                      (315,271),(241,276),(191,256),(160,235)],pal['interior'][2])
        tower(draw,274,65,91,54,41,False)
        clothesline(draw,389,89,67,29)
        for x in (142,221,395,480):
            foliage(draw,x-65,128,58,22,x,thorn=True)
        for x in (133,244,379,511):
            foliage(draw,x-55,307,64,23,x+1,thorn=True)
        for y in (152,201,256,292):
            foliage(draw,40,y,26,21,y,thorn=True)
            foliage(draw,573,y+4,25,22,y+19,thorn=True)
        for x,y in ((134,134),(204,129),(392,137),(485,132),(154,312),(466,317)):
            stone(draw,x,y,22,14,x)
        field_prop(draw,0) if False else None
        timber(draw,86,291,3,17,True)
        timber(draw,73,289,26,6)
        line(draw,[(91,292),(79,292),(82,290)],PAPER[2])
        potion(draw,533,296,False)
    elif area=='plaza':
        polygon(draw,[(65,163),(117,145),(206,135),(327,134),(452,135),(530,154),(573,180),(572,276),
                      (515,305),(425,322),(317,323),(192,325),(106,304),(61,274)],pal['interior'][0])
        polygon(draw,[(123,184),(181,151),(275,146),(368,146),(467,153),(520,178),(547,215),(534,266),
                      (474,288),(380,307),(274,306),(182,297),(123,266),(104,227)],pal['interior'][1])
        polygon(draw,[(184,185),(244,166),(355,167),(432,174),(477,196),(490,223),(472,258),(412,278),
                      (328,288),(237,282),(177,262),(153,233)],pal['interior'][2])
        for i in range(42):
            import math
            theta=i*math.tau/42
            x=320+round(math.cos(theta)*148)
            y=223+round(math.sin(theta)*67)
            stone(draw,x-6,y-3,13,5,i,moss=i%3==0)
        tower(draw,270,52,112,77,16,False)
        clothesline(draw,401,95,69,9)
        tent(draw,55,113,81,50,23)
        tent(draw,507,174,64,39,12)
        for x,y,i in ((77,191,0),(89,217,2),(116,173,1)):
            mat(draw,x,y,34,17,i)
            crate(draw,x-8,y+4,14,11,i==1)
        for x in (103,213,427,549):
            foliage(draw,x-22,313,68,28,x,thorn=True)
        stone(draw,554,141,20,30,31,standing=True)
        stone(draw,585,144,18,31,37,standing=True)
        monument(draw,232,100)
    else:
        tiled_court(draw,(62,130,576,332),pal['interior'])
        interior=(64,140,576,323)
        decorated_gate(draw,320,75,99)
        for x in range(60,582,18):
            if not 292<x<343:
                stone(draw,x,117+(x%3),17,16+x%8,x,standing=True)
            stone(draw,x,323+(x%4),18,19+x%8,x+8,standing=True)
        for y in range(133,329,18):
            stone(draw,47,y,16,20,y,standing=True)
            stone(draw,574,y+3,16,19,y+77,standing=True)
        for i in range(18):
            x=18+rng.randrange(19)
            y=131+i*9
            line(draw,[(x,y),(x-3,y+10),(x-1,y+18)],'#5E788C',2)
            if i%4==0:
                line(draw,[(x+1,y+4),(x+3,y+10)],'#8CA2AC')
    if area!='arena':
        for i in range(1600):
            x,y=rng.randrange(75,568),rng.randrange(151,307)
            base=pal['interior'][rng.choice((0,0,1,1,2,2,2,3))]
            if rng.random()<.75:
                line(draw,[(x,y),(x+rng.randint(1,4),y)],base)
            else:
                line(draw,[(x,y),(x+3,y),(x+3,y+1)],base)
        for x,y in ((54,154),(582,153),(93,295),(554,289),(248,128),(392,128)):
            glow(draw,x,y+6,9,('#57473E','#795749','#A37250'))
            timber(draw,x-2,y-6,2,23,True)
            line(draw,[(x-1,y-4),(x+6,y-4)],WOOD[3])
            lantern(draw,x+5,y-1)
    landscape_flora(draw,area,1941,interior)
    for x,y,xx,yy in ((5,297,30,329),(616,312,631,353),(1,216,20,260),(611,200,623,242)):
        pine(draw,x+12,y,34,yy-y,x+y,pal['pine'])
    return result.resize((1920,1080),NEAREST)


def make_backplates(target):
    target.mkdir(exist_ok=True)
    for area in ('field','plaza','arena'):
        detail_scene(area).save(target/f'{area}.png',optimize=True)
