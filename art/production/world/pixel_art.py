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
