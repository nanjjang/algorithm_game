"""Draw the title scene on a 480×270 pixel grid and export the screen texture."""
from pathlib import Path
from random import Random
import math
import sys

from PIL import Image, ImageDraw

ROOT = Path(__file__).parent
sys.path.insert(0, str(ROOT.parent / "players"))
from pixels import draw_selection

SIZE = (480, 270)
NEAREST = Image.Resampling.NEAREST


def star(d, x, y, color, arm=1):
    d.line((x-arm,y,x+arm,y), fill=color)
    d.line((x,y-arm,x,y+arm), fill=color)
    d.point((x,y), fill="#E6D8DF")


def sky(d):
    d.rectangle((0,0,479,269), fill="#1A2139")
    d.polygon([(0,55),(72,51),(155,55),(240,48),(326,43),(401,36),(479,41),(479,150),(0,150)], fill="#202943")
    d.polygon([(156,101),(224,97),(246,93),(291,92),(323,84),(375,78),(421,65),(479,63),
               (479,139),(329,141),(234,130),(190,124)], fill="#2B3450")
    d.polygon([(276,110),(322,102),(359,101),(390,86),(424,84),(455,76),(479,76),(479,134),
               (366,137),(295,126)], fill="#4B4059")
    d.polygon([(307,118),(339,117),(362,110),(400,102),(425,104),(449,95),(479,89),
               (479,116),(429,126),(390,132),(350,129)], fill="#795967")
    d.polygon([(383,113),(413,109),(433,109),(457,100),(479,99),(479,111),(441,121),(404,124)], fill="#AC7F79")
    for x,y,w,c in [(18,55,50,"#262D48"),(77,73,58,"#29304B"),(114,41,53,"#242D49"),
                    (205,76,54,"#313B55"),(264,46,35,"#303853"),(312,62,67,"#41415E"),
                    (343,95,54,"#69566C"),(403,89,77,"#695267"),(274,109,30,"#57495F")]:
        d.polygon([(x,y+4),(x+w//6,y+4),(x+w//3,y),(x+w//2,y),(x+w*3//5,y+2),(x+w-2,y+2),
                   (x+w,y+4),(x+w*3//4,y+5),(x+w//2,y+6),(x+w//4,y+6)], fill=c)
        d.line((x+w//3,y+7,x+w*3//4,y+7),fill=c)
    d.ellipse((327,39,358,70),fill="#625A75")
    d.ellipse((330,41,356,67),fill="#B5A8BF")
    d.ellipse((334,43,354,65),fill="#D4C2C6")
    d.ellipse((334,39,356,60),fill="#202943")
    d.polygon([(329,59),(336,63),(350,62),(359,60),(370,62),(355,65),(338,66),(326,64)],fill="#353A56")
    rng = Random(193)
    for i in range(79):
        x,y=rng.randrange(5,473),rng.randrange(6,107)
        if i%13==0:
            star(d,x,y,"#676C90",2)
        else:
            d.point((x,y), fill=rng.choice(("#535D81","#596382","#797590","#9591A7")))
    for x,y in [(33,27),(89,22),(182,15),(271,29),(299,47)]:
        star(d,x,y,"#7A7292",2)


def hills(d):
    profile = [
        ("#343650", [(0,143),(37,132),(76,134),(109,125),(143,123),(173,113),(210,118),
                      (239,103),(275,95),(298,104),(313,107),(344,119),(380,112),
                      (409,102),(435,110),(456,105),(479,109)]),
        ("#292F47", [(0,147),(46,144),(84,130),(107,136),(129,130),(162,134),(203,119),
                      (240,123),(260,119),(291,128),(316,130),(352,135),(376,123),
                      (403,121),(424,125),(457,118),(479,120)]),
        ("#222A3B", [(0,153),(53,151),(72,145),(115,147),(143,137),(176,140),(207,131),
                      (225,132),(257,137),(287,135),(310,139),(341,132),(377,141),
                      (415,134),(440,136),(479,130)]),
        ("#232C33", [(0,170),(52,159),(91,158),(119,162),(140,153),(177,156),(196,155),
                      (240,154),(265,158),(286,151),(315,153),(343,148),(383,156),
                      (430,145),(452,144),(479,151)]),
    ]
    for color, points in profile:
        d.polygon(points+[(479,269),(0,269)], fill=color)
    rng=Random(20)
    for i in range(148):
        x=rng.randrange(480)
        y=rng.randrange(138,163) + (5 if x<150 else -3)
        h=rng.randrange(3,11)
        col=("#202833","#28333C","#2D3741")[i%3]
        d.polygon([(x,y-h),(x-2,y-2),(x-4,y+1),(x+3,y+1),(x+1,y-3)],fill=col)


def ruins(d, x, y, size, far=False):
    # An open stone arch with offset block joints and a broken stair.
    inks = ("#24283D","#3D3A51","#55536B","#777185","#9F8A86") if far else (
        "#222530","#424254","#5E5B6C","#868082","#9D8E84")
    w=size
    top=y-w
    thickness=max(4,w//7)
    for side in (0,1):
        xx=x if side==0 else x+w-thickness
        d.rectangle((xx,y-w//2,xx+thickness,y),fill=inks[1],outline=inks[0])
        d.line((xx+1,y-w//2,xx+1,y-2),fill=inks[3],width=2)
        for yy in range(y-w//2+4,y,5 if far else 6):
            d.line((xx+1,yy,xx+thickness-1,yy),fill=inks[0])
            d.line((xx+1,yy+1,xx+thickness//2,yy+1),fill=inks[2])
    cx, cy = x+w//2, y-w//2
    bounds=(x,top,x+w,y)
    d.arc(bounds,180,360,fill=inks[0],width=thickness+3)
    d.arc((x+1,top+1,x+w-1,y-1),181,359,fill=inks[2],width=thickness)
    d.arc((x+2,top+2,x+w-2,y-2),198,277,fill=inks[3],width=2)
    for j in range(1,8):
        a=math.pi*(1+j/8)
        xa,ya=round(cx+w/2*math.cos(a)),round(cy+w/2*math.sin(a))
        xb,yb=round(cx+(w/2-thickness-1)*math.cos(a)),round(cy+(w/2-thickness-1)*math.sin(a))
        d.line((xa,ya,xb,yb),fill=inks[1])
    for i in range(3):
        yy=y+i*3
        d.polygon([(x-i*3,yy),(x+w+i*3,yy),(x+w+i*3+1,yy+2),(x-i*3-1,yy+2)],fill=inks[2],outline=inks[1])
        d.line((x-i*2+2,yy,x+w+i*2-1,yy),fill=inks[3])
    if far:
        d.line((cx,top+10,cx,y-4),fill=inks[3])
        d.line((x+7,cy,cx,cy+13),fill=inks[3])
        d.line((cx,cy+13,x+w-7,cy),fill=inks[3])
        star(d,cx,cy+13,inks[4],2)


def paths(d):
    d.polygon([(0,176),(52,169),(106,166),(168,172),(216,163),(261,165),
               (319,161),(356,166),(405,165),(479,161),(479,269),(0,269)],fill="#273337")
    d.polygon([(0,218),(43,200),(118,196),(188,200),(258,191),(304,180),(323,173),(364,171),
               (394,178),(367,185),(330,188),(312,201),(294,216),(259,236),(248,269),(132,269),
               (152,237),(93,224),(43,229),(0,233)],fill="#35383B")
    d.polygon([(148,269),(165,239),(195,227),(250,219),(281,206),(299,198),(320,189),(352,179),
               (377,176),(363,183),(321,195),(303,209),(274,229),(239,252),(226,269)],fill="#4B4548")
    d.polygon([(0,241),(70,230),(95,232),(122,245),(115,269),(0,269)],fill="#222D32")
    d.polygon([(308,249),(349,232),(377,230),(430,232),(479,216),(479,269),(299,269)],fill="#1D2931")
    rng=Random(87)
    for _ in range(1500):
        x,y=rng.randrange(480),rng.randrange(174,270)
        col=d._image.getpixel((x,y))
        if col in ((39,51,55),(34,45,50),(29,41,49)):
            color=rng.choice(("#313C3E","#3B4140","#354039","#414645"))
            width=rng.choice((1,1,2,2,3))
            d.line((x,y,min(479,x+width),y),fill=color)
        elif col in ((53,56,59),(75,69,72)):
            d.line((x,y,min(479,x+2),y),fill=rng.choice(("#454345","#575050","#615554","#3B3A41")))


def grass(d, x, y, h, light, ink):
    d.line([(x,y),(x-2,y-h//2),(x-5,y-h)],fill=ink,width=2)
    d.line([(x,y),(x+1,y-h+1),(x+4,y-h-2)],fill=ink,width=2)
    d.line([(x,y),(x+6,y-h//2),(x+9,y-h//2-1)],fill=ink,width=2)
    d.line([(x-1,y-1),(x-3,y-h//2),(x-5,y-h)],fill=light)
    d.line([(x+1,y-1),(x+2,y-h+1),(x+4,y-h-1)],fill=light)


def crag(d,x,y,w,h):
    p=[(x,y+h-3),(x+3,y+5),(x+w//3,y),(x+w-7,y+1),(x+w,y+7),
       (x+w-3,y+h-1),(x+6,y+h+2)]
    d.polygon(p, fill="#373748", outline="#191F2B")
    d.polygon([(x+3,y+6),(x+w//3,y+1),(x+w-8,y+2),(x+w-3,y+6),
               (x+w-9,y+9),(x+w//2,y+8),(x+8,y+9)], fill="#585669")
    d.line([(x+6,y+7),(x+w//3,y+3),(x+w-10,y+4)],fill="#797181")
    d.polygon([(x+w//2,y+8),(x+w-6,y+8),(x+w-6,y+h-2),(x+w//2-2,y+h)], fill="#292D3D")
    d.line([(x+6,y+8),(x+8,y+12),(x+6,y+h-2)], fill="#45475B")
    d.line([(x+w//2,y+8),(x+w//2-4,y+12),(x+w//2-1,y+h-1)],fill="#666474")


def torch(d, x, y):
    d.line((x,y-6,x,y+15),fill="#181B26",width=4)
    d.line((x-1,y+2,x-1,y+14),fill="#6B4A40")
    d.rectangle((x-4,y-5,x+4,y+4),fill="#573739",outline="#261F29")
    d.rectangle((x-3,y-4,x+3,y+3),fill="#DF944A")
    d.rectangle((x-1,y-3,x+1,y+2),fill="#FFD48B")
    d.point((x,y-1),fill="#FFF0B6")
    d.polygon([(x-5,y-5),(x-2,y-8),(x+2,y-8),(x+5,y-5)],fill="#7F533E",outline="#2B2430")
    d.line((x-4,y+4,x+4,y+4),fill="#2B2430",width=2)


def tent(d,x,y):
    d.polygon([(x-22,y+11),(x,y-19),(x+22,y+10),(x+21,y+16),(x-24,y+16)],
              fill="#685567",outline="#2A2535")
    d.polygon([(x-20,y+11),(x,y-18),(x+5,y+11),(x-10,y+12)],fill="#9A7E79")
    d.polygon([(x,y-17),(x+5,y+11),(x+18,y+12),(x+11,y+1)],fill="#755C68")
    d.polygon([(x-6,y+11),(x,y-10),(x+8,y+12),(x+1,y+14)],fill="#352A3E")
    d.line([(x,y-13),(x-10,y+9),(x-18,y+12)],fill="#CAA28A")
    d.line([(x,y-18),(x+6,y+10),(x+17,y+14)],fill="#AD857C")
    d.line([(x-14,y+6),(x-26,y+16)],fill="#67545E")
    d.line([(x+13,y+7),(x+27,y+16)],fill="#67545E")
    d.line([(x-3,y+12),(x,y-6),(x+4,y+12)],fill="#D09D67")
    torch(d,x+16,y+10)


def oak(d):
    bark="#382B39"
    d.polygon([(458,268),(454,237),(453,217),(456,191),(452,165),(449,141),(447,117),
               (438,90),(422,67),(412,53),(392,44),(374,40),(362,33),(328,28),
               (309,20),(280,15),(275,10),(278,7),(307,10),(329,18),(367,23),(389,27),
               (414,36),(431,47),(441,51),(445,35),(445,12),(452,0),(479,0),(479,269)],
              fill=bark,outline="#161C2A")
    d.polygon([(465,258),(463,229),(465,209),(462,184),(461,156),(456,132),(452,112),
               (443,90),(426,62),(413,50),(390,40),(367,36),(352,29),(331,27),(360,27),
               (397,34),(421,46),(441,61),(456,77),(466,100),(471,127),(474,160),
               (475,191),(471,224),(475,258)],fill="#5D4247")
    d.line([(472,248),(468,230),(471,211),(468,178),(465,153),(461,136),(458,117),
            (451,99),(439,81),(428,68),(417,55),(397,44),(378,40)],fill="#8D604F",width=2)
    d.line([(455,203),(456,190),(452,164),(453,152),(447,135),(447,122),(442,110)],fill="#513948",width=2)
    d.line([(465,161),(468,140),(463,121),(460,111),(465,102),(464,86)],fill="#382D3B",width=2)
    d.polygon([(400,44),(393,42),(385,40),(369,42),(357,49),(345,57),(332,58),(319,64),
               (322,59),(334,54),(347,50),(358,41),(372,34),(390,37)],fill="#322C39")
    d.line([(388,42),(373,40),(359,47),(346,54),(334,56)],fill="#694B4A")
    d.line((433,57,433,83),fill="#94654F")
    torch(d,433,89)
    rng=Random(906)
    leaves=[(295,10,45,11),(347,13,48,14),(398,15,48,17),(447,12,55,16),
            (459,35,32,17),(406,32,32,9),(343,45,31,8),(308,24,35,8),(475,57,22,15)]
    for x,y,rx,ry in leaves:
        for j in range(max(16,rx*ry//18)):
            a=rng.random()*math.tau
            r=math.sqrt(rng.random())
            u,v=round(x+math.cos(a)*rx*r),round(y+math.sin(a)*ry*r)
            k=rng.randrange(3,11)
            c=rng.choice(("#1D2931","#233033","#30393A","#394144","#4A4C42"))
            d.polygon([(u-k//2,v),(u-k//3,v-2),(u,v-3),(u+k//2,v-1),(u+k//2+2,v+2),
                       (u,v+3),(u-k//3,v+2)], fill=c)
            if j%4==0:
                d.line((u-k//3,v-1,u+1,v-2),fill="#4B5145")


def meadow_details(d):
    rng=Random(312)
    for i in range(170):
        x,y=rng.randrange(480),rng.randrange(168,271)
        if 158<x<339 and y>180 and i%3!=0:
            continue
        h=rng.randrange(4,12) if y<236 else rng.randrange(8,20)
        light, dark=rng.choice((("#52614F","#25323B"),("#435549","#25313B"),("#3C4B4D","#222E39")))
        grass(d,x,y,h,light,dark)
        if i%5==0:
            cx,cy=x+3,y-h+1
            d.rectangle((cx,cy,cx+2,cy+1),fill="#9B7998")
            d.point((cx+1,cy-1),fill="#BB99B0")
    for x,y,w,h in [(6,236,38,13),(30,246,28,15),(65,252,24,12),(431,239,43,19),(393,238,32,14),
                    (322,232,55,15),(375,248,51,22),(450,258,34,14),(111,253,27,10)]:
        crag(d,x,y,w,h)
    for x,y in ((17,266),(50,269),(74,269),(109,266),(310,263),(348,269),(429,263),(469,266)):
        grass(d,x,y,rng.randrange(15,29),"#4D5652","#192532")


def main():
    im=Image.new("RGB",SIZE)
    d=ImageDraw.Draw(im)
    sky(d)
    hills(d)
    ruins(d,274,130,32,True)
    paths(d)
    ruins(d,323,167,46)
    tent(d,421,164)
    for x,y in [(316,171),(376,171),(403,195)]:
        torch(d,x,y)
    for i,(x,y) in enumerate(((269,183),(284,179),(380,184),(383,205),(295,190))):
        crag(d,x,y,8+i%3*2,7)
    meadow_details(d)
    oak(d)
    d.ellipse((334,226,425,245),fill="#242633")
    character = draw_selection("geumeum").resize((132,132), NEAREST)
    im.paste(character,(307,114),character)
    d=ImageDraw.Draw(im)
    for x,y,r in [(431,146,1),(436,153,1),(423,143,2),(427,136,1)]:
        star(d,x,y,"#9578B4",r)
    for x,y in [(309,201),(317,217),(275,225),(345,207),(254,193),(415,225)]:
        d.point((x,y),fill="#B6996A")
    im.resize((1920,1080),NEAREST).save(ROOT/"title_1920x1080.png")
    im.resize((960,540),NEAREST).save(ROOT/"title_preview.png")


if __name__=="__main__":
    main()
