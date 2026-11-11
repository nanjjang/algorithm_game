from algorithm_ui_kit import *

def earth(w,h,kind):
    # Deliberate clusters at a 4px game-art grid, for a portable pedestal sprite.
    p=Pictogram(w//4,h//4)
    c,hi,shadow={'dirt':('#41373a','#695445','#27232d'),
                 'stone':('#414757','#616779','#282b38') }[kind]
    p.shape([(1,16),(3,12),(6,10),(9,7),(13,8),(16,7),(20,9),(23,13),(23,17),(21,19),(4,19)],INK)
    p.shape([(2,15),(4,12),(8,11),(10,9),(14,9),(17,8),(20,10),(22,13),(21,17),(17,18),(5,18)],c)
    p.shape([(2,15),(5,13),(8,13),(10,11),(14,11),(16,9),(19,10),(20,12),(15,12),(12,14),(7,15)],hi)
    p.shape([(7,17),(12,15),(17,16),(21,14),(21,17),(17,18),(5,18)],shadow)
    rng=random.Random(w+h)
    for i in range(13):
        x=rng.randint(5,20);y=rng.randint(12,16)
        p.box(x,y,x+.25,y+.4,hi if i%3==0 else shadow)
    return p.im.resize((w,h),Image.Resampling.NEAREST)

def chalk_strokes(text,size=(960,320)):
    im=canvas(*size)
    mask=Image.new('L',size)
    fill=ImageDraw.Draw(mask)
    # The three words are kept in full Korean, with a shorter, chalk-written insert above.
    rows=[('엄청엄청',12,0,8,5),('위대한 마법사',12,0,90,10),('키우기',12,0,218,8)]
    for phrase,s,x,y,factor in rows:
        raw=bitmap_text(phrase,s).getchannel('A')
        rowscaled=raw.resize((raw.width*factor,raw.height*factor),Image.Resampling.NEAREST)
        mask.paste(rowscaled,((size[0]-rowscaled.width)//2+x,y))
    return mask

def title():
    size=(960,320)
    im=canvas(*size);full=chalk_strokes('엄청엄청 위대한 마법사 키우기')
    outlines=full.filter(ImageFilter.MaxFilter(9))
    im.paste('#191422',(0,4),outlines)
    im.paste('#55405a',(0,0),full.filter(ImageFilter.MaxFilter(5)))
    im.paste(CHALK,(0,0),full)
    d=ImageDraw.Draw(im)
    d.polygon([(363,70),(514,67),(590,70),(503,74),(365,73)],fill='#d0b4d4')
    for x,y,r in [(199,196,13),(765,207,12),(806,225,7),(151,217,5)]:
        d.polygon([(x,y-r),(x+2,y-2),(x+r,y),(x+2,y+2),(x,y+r),(x-2,y+2),(x-r,y),(x-2,y-2)],fill='#b7a0d1')
    save('s-0202','title_korean',im,metadata={'verbatim':'엄청엄청 위대한 마법사 키우기','suggested_anchor':[48,157]})
    for size in (32,64):
        p=Pictogram(size)
        p.d.rectangle((0,0,size-1,size-1),fill='#151421')
        p.shape([(3,20),(8,15),(15,15),(19,19),(18,22),(3,22)],'#29304f')
        p.shape([(3,7),(4,4),(7,3),(11,3),(13,4),(17,8),(20,11),(20,15),(17,18),(7,18),(4,15),(6,12),(9,9),(7,7),(6,7),(5,9),(3,9)],'#3f2143')
        p.shape([(4,6),(5,4),(8,3),(11,3),(14,5),(18,9),(20,12),(19,15),(17,17),(7,17),(5,15),(7,12),(10,8),(8,6),(6,6),(5,8),(4,8)],'#7e386f')
        p.shape([(8,13),(11,10),(15,10),(18,12),(18,15),(16,16),(9,16),(7,15)],'#bd689e')
        p.shape([(9,13),(12,11),(15,11),(17,13),(17,15),(9,15)],'#201322')
        p.box(11,13,11.9,13.9,'#d7a8f4');p.box(15,13,15.8,13.9,'#d7a8f4')
        p.shape([(5,19),(12,18),(14,19),(10,20),(7,22),(4,21)],'#4b5180')
        p.diamond(20,18,3,'#8a5cff',4);p.diamond(20,17,1.5,'#d9c7ff',2)
        p.box(12,17.5,13,18.5,PALE_BRASS)
        save('s-0203','launcher_icon',p.im)

def wheel(paper=False):
    size=360;mid=180
    base=canvas(size);d=ImageDraw.Draw(base)
    d.ellipse((3,3,356,356),fill=INK)
    d.ellipse((8,8,351,351),fill=BRASS if paper else RIM)
    for index in range(8):
        a=-112.5+index*45
        d.pieslice((12,12,347,347),a+1,a+44,fill=(PAPER if index%2==0 else FOLD) if paper else (WOOD if index%2==0 else DARK))
    d.ellipse((130,130,229,229),fill=INK)
    d.ellipse((136,136,223,223),fill=DARK)
    for a in range(0,360,45):
        x=round(mid+155*math.cos(math.radians(a)));y=round(mid+155*math.sin(math.radians(a)))
        d.rectangle((x-2,y-2,x+2,y+2),fill=BRASS if paper else SLATE)
    highlight=canvas(size);d=ImageDraw.Draw(highlight)
    d.pieslice((10,10,349,349),-111,-69,fill=CHALK)
    d.pieslice((15,15,344,344),-108,-72,fill=(232,221,191,95))
    d.ellipse((129,129,230,230),fill=(0,0,0,0))
    return base,highlight

def flag_or_tail(w,h,color=PAPER):
    p=Pictogram(w,h);p.shape([(2,2),(22,2),(13,20),(9,23),(9,15)],INK)
    p.shape([(5,3),(19,3),(11,18),(11,13)],color)
    return p.im

def duel_floor(w,h,central=True):
    base=canvas(w//4,h//4);d=ImageDraw.Draw(base)
    rng=random.Random(911)
    d.rectangle((0,0,base.width-1,base.height-1),fill='#272d3b')
    shades=['#2c3442','#303948','#2b323e','#343e4c','#303742','#353e4b']
    for y in range(0,base.height,14):
        x=-16 if (y//14)%2 else 0
        while x<base.width:
            dx=rng.choice((20,24,28,32));c=rng.choice(shades)
            clipped(d,(x,y,x+dx-1,y+12),3,c)
            d.line((x+4,y+1,x+dx-4,y+1),fill='#384352')
            for _ in range(3):
                sx=x+rng.randint(4,dx-4);sy=y+rng.randint(4,9)
                d.line((sx,sy,sx+rng.randint(1,3),sy),fill='#323b49')
            x+=dx+1
    if central:
        mid=base.width//2
        d.rectangle((mid-1,0,mid,base.height-1),fill='#84909a')
        for y in range(0,base.height,6):
            d.line((mid-1,y,mid-1,y+2),fill='#b7b9b2')
            if rng.random()<.24: d.point((mid,y+4),fill='#5a6570')
        d.ellipse((mid-30,base.height//2-30,mid+30,base.height//2+30),outline='#6c7984',width=1)
    return base.resize((w,h),Image.Resampling.NEAREST)

def cards(w,h,kind='paper',reverse=False):
    im=plank(w,h,kind,brads=kind!='paper',corners=10)
    d=ImageDraw.Draw(im)
    d.rectangle((12,12,w-13,h-13),outline=FOLD if kind=='paper' else RIM,width=2)
    if reverse:
        d.rectangle((18,18,w-19,h-19),outline=BRASS,width=2)
        d.line((22,24,w-23,h-25),fill=FOLD,width=2);d.line((w-23,24,22,h-25),fill=FOLD,width=2)
        im.alpha_composite(glyph(min(w//2,96),'diamond',TAN),((w-min(w//2,96))//2,(h-min(w//2,96))//2))
    else:
        for x,y in [(15,15),(w-20,15),(15,h-20),(w-20,h-20)]:
            d.rectangle((x,y,x+4,y+4),fill=FOLD if kind=='paper' else BRASS)
    return im

def ladder_connector(w,h,kind,state='off'):
    im=canvas(w,h);d=ImageDraw.Draw(im);c=CHALK if state=='on' else SLATE
    pts={'straight':[(w//2,0),(w//2,h-1)],'corner':[(w//2,0),(w//2,h//2),(w-1,h//2)],
        'head':[(w-1,1),(1,1),(1,h-1)],'tail':[(1,0),(1,h-2),(w-1,h-2)]}[kind]
    d.line(pts,fill=INK,width=4);d.line(pts,fill=c,width=2)
    return im

def bag(opened):
    p=Pictogram(190,30)
    p.shape([(0,16),(2,7),(5,5),(9,5),(11,3),(15,5),(19,5),(22,8),(23,16),(22,22),(2,22)],INK)
    p.shape([(1,16),(3,8),(8,7),(10,5),(15,6),(19,6),(21,9),(22,16),(21,21),(3,21)],'#664b3e')
    p.shape([(2,13),(4,8),(10,6),(14,6),(19,7),(21,11),(18,13),(7,13)],'#ad8256')
    if opened:
        p.shape([(3,13),(8,9),(17,9),(21,13),(21,17),(4,17)],INK)
        p.shape([(4,16),(6,12),(17,12),(20,15),(18,16)],'#2d2530')
        p.line([(3,18),(20,18)],TAN,1,False)
    else:
        p.line([(3,13),(21,13)],BRASS,2)
        p.shape([(10,12),(14,12),(14,17),(10,17)],TAN)
        p.box(11,13,13,15,PALE_BRASS)
    return p.im.resize((760,120),Image.Resampling.NEAREST)

def rug(locked):
    im=plank(640,520,'well',corners=14)
    d=ImageDraw.Draw(im)
    clipped(d,(8,8,631,511),10,'#453745' if locked else '#423b37')
    for y in range(14,508,6):
        for x in range(14+(y%4)*2,632,15):
            d.line((x,y,x+5,y),fill='#52434d' if locked else '#514640',width=1)
    d.rectangle((14,12,625,507),outline='#67505d' if locked else '#685241',width=3)
    for y in (22,497):
        for x in range(24,619,16):
            d.polygon([(x,y-5),(x+5,y),(x,y+5),(x-5,y)],fill=BRASS)
    if locked:
        d.line([(32,24),(29,497),(604,495),(607,25),(32,24)],fill='#261f26',width=11)
        d.line([(32,24),(29,497),(604,495),(607,25),(32,24)],fill=TAN,width=6)
        d.line([(32,24),(29,497),(604,495),(607,25),(32,24)],fill=PALE_BRASS,width=2)
    return im

def sculpted_lock(open_amount):
    p=Pictogram(96)
    p.line([(8,11),(8,8-open_amount),(10,5-open_amount),(14,5-open_amount),(16,8-open_amount),(16,11-open_amount)],'#727889',3)
    p.line([(9,10),(9,8-open_amount),(10,6-open_amount),(14,6-open_amount),(15,8-open_amount)],'#acadb3',1,False)
    p.shape([(4,11),(20,11),(21,13),(21,20),(19,22),(5,22),(3,20),(3,13)],INK)
    p.shape([(5,12),(19,12),(20,13),(20,19),(18,21),(6,21),(4,19),(4,13)],TAN)
    p.shape([(5,12),(19,12),(17,14),(5,14)],PALE_BRASS)
    p.shape([(6,19),(18,19),(20,17),(20,19),(18,21),(6,21)],BRASS)
    p.circle(12,16,1.5,INK);p.box(11.5,16,12.5,19,INK)
    return p.im

def monument():
    im=canvas(180,220);d=ImageDraw.Draw(im)
    outer=[(7,214),(10,178),(12,95),(16,49),(20,49),(21,38),(31,31),(36,20),(56,16),(65,9),(111,5),(131,11),(139,20),(151,24),(157,41),(165,48),(168,133),(171,169),(173,213)]
    d.polygon(outer,fill=INK)
    d.polygon([(11,210),(15,173),(17,93),(21,51),(27,47),(30,35),(36,33),(41,22),(61,19),(68,11),(111,8),(129,14),(135,22),(147,29),(153,43),(162,50),(164,135),(169,175),(169,211)],fill='#34394b')
    face=[(20,202),(22,169),(25,94),(29,53),(35,50),(37,39),(47,36),(51,25),(70,22),(76,16),(107,13),(122,19),(126,26),(141,32),(146,47),(152,51),(151,112),(155,144),(154,176),(158,201),(140,205),(63,206)]
    d.polygon(face,fill='#4e5969')
    d.line(face[4:16],fill='#778194',width=3)
    d.line([(21,197),(23,162),(26,97),(29,57)],fill='#666e81',width=2)
    d.line([(152,60),(151,110),(155,144),(154,177),(158,200),(141,206),(62,207)],fill='#2b3041',width=3)
    for pts in [[(47,23),(44,29),(48,34)],[(142,33),(137,36),(135,41)],[(154,132),(146,142),(149,149)],[(21,168),(29,161),(31,156)],[(85,17),(82,24),(85,27)],[(141,201),(137,193),(141,189)]]:
        d.line(pts,fill='#3b4355',width=2)
    rng=random.Random(4217)
    for _ in range(90):
        y=rng.randint(52,195)
        x=rng.choice((rng.randint(31,38),rng.randint(139,145)))
        d.line((x,y,x+rng.randint(1,3),y),fill=rng.choice(('#596478','#435263','#657184')))
    for pts in [
        [(12,209),(16,195),(23,190),(27,194),(26,199),(39,201),(51,211)],
        [(135,211),(143,204),(151,206),(159,195),(167,198),(170,211)],
        [(40,213),(51,207),(58,211),(64,212),(63,214)],
    ]:
        d.polygon(pts,fill='#475745');d.line(pts[:4],fill='#64704d',width=2)
    return im.resize((720,880),Image.Resampling.NEAREST)


def screens():
    title()
    save('s-0201','campfire_overlay',[campfire(128,i) for i in (0,2,4,5)],interval=125,metadata={
        'usage':'Optional overlay for a campfire title composition',
        'sprite_origin':[64,106],
    })
    im=canvas(16);d=ImageDraw.Draw(im);d.rectangle((5,5,10,10),fill=CHALK)
    save('s-0302','password_dot',im)
    for name in ('eye','eye_hidden'):save('s-0302','password_'+name,glyph(24,name))
    im=plank(48,48,'paper');im.alpha_composite(frame(48,48,BRASS,1,'dash',3))
    p=Pictogram(24);p.line([(2,9),(20,9)],TAN,2,False);p.line([(10,10),(10,19)],BRASS,2,False)
    im.alpha_composite(p.im,(1,1));save('s-0401','server_features_group',im,16)
    save('s-0501','campfire',[campfire(128,i) for i in (0,1,2,3)],interval=125,metadata={'composite_anchor':[1236,780],'sprite_origin':[64,100]})
    save('s-0503','dirt_pedestal',earth(320,96,'dirt'))
    for name in ('normal','selected'):
        im=cards(336,376,'wood')
        if name=='selected': ImageDraw.Draw(im).rectangle((5,363,330,370),fill=CHALK)
        save('s-0601','character_card_'+name,im,metadata={'tint_bottom_strip':name=='selected'})
    save('s-0602','wand_pedestal',earth(224,48,'stone'))
    for name,c in [('rail',DARK),('fill',CHALK)]:
        save('s-0801','wave_'+name,fillbar(24,12,c),(6,3,6,3))
    for name,c in [('normal',SLATE),('hit',CHALK)]:
        save('s-0802','portrait_'+name,frame(72,72,c,2,'rare',3))
    for name,c in [('rail',DARK),('fill',CHALK)]:
        save('s-0803','boss_health_'+name,fillbar(32,16,c),(8,4,8,4))
    im=canvas(4,16);ImageDraw.Draw(im).rectangle((1,0,2,15),fill=SLATE)
    save('s-0803','boss_health_tick',im)
    save('s-0803','enrage_ember',[draw_element(16,'fire'),glyph(16,'spark',AMBER)],interval=220)
    save('s-0804','score',glyph(24,'score'));save('s-0804','kills',glyph(24,'horn'))
    im=plank(64,24,'well',accent=AMBER)
    im.alpha_composite(glyph(16,'lock',AMBER),(3,4))
    save('s-0805','heat_lock',im)
    save('s-0901','duel_floor',duel_floor(1920,1080))
    frames=[]
    for count in (1,3,6):
        im=canvas(256,160);d=ImageDraw.Draw(im)
        segments=[[(49,37),(104,36)],[(51,38),(51,108),(109,106)],[(142,25),(141,128)],[(176,26),(175,129)],[(143,70),(174,69)],[(49,108),(107,104)]]
        for pts in segments[:count]:
            d.line(pts,fill=INK,width=13);d.line(pts,fill=CHALK,width=9);d.line(pts,fill=PAPER,width=2)
        frames.append(im)
    save('s-0902','versus_hangul',frames,interval=90,metadata={'verbatim':'대'})
    for name,c in [('rail',DARK),('fill',CHALK),('recent',SLATE)]:
        save('s-0903','duel_health_'+name,fillbar(32,32,c),8)
    im=canvas(4,32);ImageDraw.Draw(im).rectangle((1,0,2,31),fill=RIM);save('s-0903','duel_health_tick',im)
    p=Pictogram(16);p.ring(12,12,7,SLATE,2);save('s-0904','round_empty',p.im)
    p=Pictogram(16);p.circle(12,12,7,CHALK);save('s-0904','round_player',p.im)
    save('s-0904','round_opponent',glyph(16,'diamond'));save('s-0904','round_draw',glyph(16,'equal'))
    save('s-0905','thorn_warning',[glyph(24,'thorns',c) for c in (AMBER,PAPER)],interval=350)
    save('s-0906','dropped_input',glyph(24,'key_drop'))
    for id,name,paper in [('s-0907','phrase',False),('s-2003','emote',True)]:
        base,hi=wheel(paper)
        save(id,name+'_wheel',base);save(id,name+'_highlight',hi,metadata={'rotate_by_degrees':45,'rotation_pivot':[180,180]})
    front,back=cards(240,336),cards(240,336,reverse=True)
    save('s-1001','loot_front',front);save('s-1001','loot_back',back)
    frames=[]
    for img,width in [(back,168),(back,24),(front,156)]:
        im=canvas(240,336);im.alpha_composite(img.resize((width,336),Image.Resampling.NEAREST),((240-width)//2,0));frames.append(im)
    save('s-1002','loot_flip',frames,interval=33)
    for name in ('projectile','control','modifier','scope'):
        im=block_plate(32,'control' if name=='scope' else name)
        if name=='scope':im.alpha_composite(frame(32,32,SLATE,3,offset=1))
        save('s-1101','tree_node_'+name,im)
    for name,w,h,shape in [('line',8,16,'straight'),('head',8,8,'head'),('tail',8,8,'tail')]:
        save('s-1102','scope_'+name,ladder_connector(w,h,shape,'on'))
    for count in (2,3):
        im=canvas(96,24);d=ImageDraw.Draw(im)
        for x in ((16,80) if count==2 else (16,48,80)):
            pts=[(47,0),(47,8),(x,8),(x,23)]
            d.line(pts,fill=INK,width=4);d.line(pts,fill=SLATE,width=2)
        save('s-1103',f'split_{count}',im)
    save('s-1104','omitted_node',frame(32,32,SLATE,2,'dash',2))
    save('s-1105','tree_link_line',ladder_connector(4,16,'straight'))
    save('s-1105','tree_link_corner',ladder_connector(16,16,'corner'))
    save('s-1106','execution_number',plank(16,16,'well',corners=3))
    for name,g,c in [('heat','thorns',AMBER),('threshold','up',CHALK),('dps','target',CHALK)]:
        save('s-1107',name,glyph(24,g,c))
    for name in ('normal','empty'):
        save('s-1108','library_slot_'+name,plank(96,112,'wood' if name=='normal' else 'well'))
    for name,c in [('rail',DARK),('fill',CHALK),('boundary',SLATE)]:
        save('s-1201','odds_'+name,fillbar(16,24,c),4)
    for name,g in [('base','diamond'),('sacrifice','paper'),('same_element','spark'),('momentum','up')]:
        save('s-1202','odds_'+name,glyph(16,g))
    for name,c in [('empty',DARK),('filled',CHALK)]:
        save('s-1203','engraving_tick_'+name,fillbar(16,16,c))
    p=Pictogram(16,24);p.line([(7,3),(7,22)],CHALK,2);p.shape([(9,5),(19,5),(16,9),(19,13),(9,13)],PAPER)
    save('s-1204','enhance_goal',p.im)
    for name,c in [('unlocked',CHALK),('locked',SLATE),('candidate',AMBER)]:
        im=plank(64,64,'well',accent=c,corners=8)
        if name=='locked':im.alpha_composite(glyph(16,'lock',SLATE),(44,44))
        save('s-1301','trie_node_'+name,im)
    save('s-1301','trie_node_current',[frame(64,64,c,2,'normal',5) for c in (CHALK,SLATE)],interval=250)
    for name,c in [('off',RIM),('on',CHALK)]:
        p=Pictogram(24);p.circle(12,12,7,INK);p.circle(12,12,4,c)
        save('s-1301','trie_dot_'+name,p.im)
        save('s-1302','trie_line_'+name,ladder_connector(16,16,'straight',name),metadata={'rotate_for_horizontal':90})
        save('s-1302','trie_corner_'+name,ladder_connector(16,16,'corner',name))
    save('s-1303','unlock_candidate',cards(320,320,'wood'))
    save('s-1401','timing_rail',fillbar(16,16,DARK),4)
    for name,c in [('valid',CHALK),('late',AMBER)]:
        p=Pictogram(8,16);p.box(7,1,16,22,c,True);save('s-1401','timing_marker_'+name,p.im)
    save('s-1402','just_rail',fillbar(192,24,DARK))
    im=canvas(192,24);d=ImageDraw.Draw(im);d.rectangle((78,5,113,18),fill=SLATE);d.rectangle((89,5,102,18),fill=CHALK)
    save('s-1402','just_window',im)
    im=canvas(192,24);ImageDraw.Draw(im).rectangle((94,2,97,21),fill=CHALK)
    save('s-1402','just_marker',im,metadata={'movable_marker_center':[96,12]})
    p=Pictogram(24,16);p.im.alpha_composite(glyph(16,'infinity'),(4,0));save('s-1403','infinity',p.im)
    for name,open_ in [('closed',False),('open',True)]:save('s-1601','return_bag_'+name,bag(open_))
    save('s-1603','fallen_pedestal',earth(384,96,'dirt'))
    for name,c in [('victory',CHALK),('defeat',SLATE),('draw',PAPER)]:
        im=plank(1200,120,'well',accent=c,corners=20)
        d=ImageDraw.Draw(im)
        for x in (28,1164):
            d.polygon([(x,52),(x+8,60),(x,68),(x-8,60)],fill=c)
        save('s-1701','result_banner_'+name,im,metadata={'tint':name=='victory'})
    save('s-1901','options_field',duel_floor(424,240,False))
    for name in ('telegraph','enemy_bolt','player_bolt','hatching','shake','flash'):
        im=canvas(424,240);d=ImageDraw.Draw(im)
        if name=='telegraph':
            d.ellipse((171,71,291,191),fill=(242,163,58,45),outline=AMBER,width=3)
            d.arc((175,75,287,187),140,300,fill=CHALK,width=1)
        elif name=='enemy_bolt':
            for x,y in [(254,133),(276,139),(298,145)]:
                d.polygon([(x-8,y),(x,y-6),(x+9,y),(x,y+7)],fill=INK)
                d.polygon([(x-6,y),(x,y-4),(x+7,y),(x,y+5)],fill=AMBER)
                d.rectangle((x-2,y-2,x+1,y+1),fill=CHALK)
        elif name=='player_bolt':
            for x,y in [(133,120),(153,120),(173,120)]:
                im.alpha_composite(draw_element(20,'arcane'),(x-10,y-10))
        elif name=='hatching':
            alpha=Image.new('L',im.size)
            ImageDraw.Draw(alpha).ellipse((172,72,290,190),fill=210)
            for x in range(70,310,8):
                d.line((x,185,x+100,81),fill=AMBER,width=2)
            im.putalpha(ImageChops.multiply(im.getchannel('A'),alpha))
        elif name=='shake':
            for left in (40,370):
                d.line([(left,111),(left+4,99),(left+10,108),(left+16,100)],fill=SLATE,width=2)
        else:
            im.alpha_composite(glyph(46,'spark',CHALK),(206,111))
        save('s-1901','options_'+name,im)
    im=plank(1200,280,'wood',brads=True,corners=16);d=ImageDraw.Draw(im)
    d.rectangle((24,28,838,249),fill=DARK,outline=INK,width=4)
    d.rectangle((854,28,1169,249),fill=DARK,outline=INK,width=4)
    d.line((24,252,1167,252),fill=RIM,width=2)
    save('s-1902','keyboard_board',im)
    for name,c in [('ordinary',SLATE),('thirteen',GOLD),('heir',GOLD)]:
        im=plank(32,32,'well',accent=c,corners=5)
        if name=='heir':im.alpha_composite(glyph(12,'chair',GOLD),(3,13))
        save('s-1903','title_row_'+name,im,8)
    for name,c in [('other',DARK),('self',CHALK),('friend',SLATE)]:
        im=canvas(24,20);d=ImageDraw.Draw(im)
        clipped(d,(0,0,23,19),4,c)
        clipped(d,(1,1,22,18),3,(18,19,28,179))
        save('s-2001','nameplate_'+name,im,(8,5,8,5))
    for name,c in [('ordinary',SLATE),('thirteen',GOLD),('heir',GOLD),('disciple',PAPER)]:
        save('s-2002','title_ribbon_'+name,plank(32,16,'well',accent=c,corners=4),(8,4,8,4))
    frames=[]
    for n in (1,2,3):
        im=canvas(24,16);d=ImageDraw.Draw(im)
        for x in range(n):d.rectangle((4+x*6,8,6+x*6,10),fill=CHALK)
        frames.append(im)
    save('s-2004','typing_ellipsis',frames,interval=220)
    for name,c in [('nearby',PAPER),('system',FOLD)]:
        save('s-2005','bubble_'+name,plank(32,32,'paper',accent=c,corners=5),8)
        save('s-2005','bubble_tail_'+name,flag_or_tail(12,8,c))
    save('s-2006','item_nameplate',plank(24,24,'well'),8)
    save('s-2007','facility_sign',plank(48,32,'wood',brads=True),8)
    im=plank(96,96,'wood',brads=True,corners=8)
    for xy in ((8,9),(71,11),(7,69),(71,69)):
        note=plank(18,20,'paper',corners=3);ImageDraw.Draw(note).rectangle((8,3,9,5),fill=BRASS)
        im.alpha_composite(note,xy)
    save('s-2101','duel_board',im,32)
    for name in ('normal','full'):
        im=plank(48,56,'paper',accent=FOLD if name=='full' else None);brad(ImageDraw.Draw(im),7,6)
        save('s-2102','room_note_'+name,im,12)
    for name,c,g in [('not_ready',SLATE,'minus'),('ready',GREEN,'check')]:
        im=plank(96,32,'well',accent=c);im.alpha_composite(glyph(18,g,c),(6,7))
        save('s-2103','ready_'+name,im)
    save('s-2104','room_leader',glyph(16,'leader',PAPER))
    save('s-2105','waiting_banner',plank(240,48,'well',accent=SLATE))
    frames=[]
    for i in range(4):
        im=glyph(24,'hourglass');d=ImageDraw.Draw(im)
        d.point((12,9+i*2),fill=PAPER);frames.append(im)
    save('s-2105','waiting_hourglass',frames,interval=180)
    for name,b in [('open',False),('locked',True)]:save('s-2301','trade_mat_'+name,rug(b))
    for name,amount in [('open',4),('locked',0)]:save('s-2302','trade_lock_'+name,sculpted_lock(amount))
    save('s-2302','trade_locking',[sculpted_lock(i) for i in (3,2,1)],interval=100)
    save('s-2303','trade_changed_slot',[frame(72,72,c,3,'dash',3) for c in (CHALK,SLATE)],interval=220)
    save('s-2304','price_history_frame',plank(32,32,'well'),8)
    save('s-2306','catalog_card_normal',cards(216,280,'paper'))
    im=cards(216,280,'paper');d=ImageDraw.Draw(im)
    d.line((16,230,199,230),fill=FOLD,width=6)
    im.alpha_composite(glyph(30,'lock',BRASS),(92,211));save('s-2306','catalog_card_sold_out',im)
    im=plank(96,96,'wood',brads=True);d=ImageDraw.Draw(im)
    d.polygon([(4,24),(19,7),(76,7),(91,24),(84,28),(11,28)],fill=INK)
    d.polygon([(10,24),(21,11),(74,11),(85,24)],fill=BRASS)
    d.line((19,12,76,12),fill=TAN,width=2)
    save('s-2401','mailbox_board',im,32)
    for name in ('normal','equipped'):
        im=plank(96,96,'well',accent=SLATE if name=='equipped' else None)
        if name=='equipped':im.alpha_composite(glyph(24,'check'),(68,4))
        save('s-2403','cosmetic_slot_'+name,im)
    save('s-2404','dialogue_board',plank(96,96,'paper',corners=10),32)
    for name,c in [('rail',DARK),('expedition',CHALK),('pvp',SLATE)]:
        im=fillbar(16,24,c);d=ImageDraw.Draw(im)
        if name=='expedition':
            for x in (1,6,11):d.line((x,19,x+3,15),fill=SLATE)
        if name=='pvp':
            for x in (5,10):d.point((x,12),fill=CHALK)
        save('s-2406','heir_score_'+name,im,(4,6,4,6))
    for name,c in [('sprout',TAN),('beast',SLATE),('heir',GOLD)]:
        im=cards(528,640,'wood');im.alpha_composite(frame(528,640,c,3,'rare' if name=='beast' else 'hero' if name=='heir' else 'normal',10))
        save('s-2501','league_card_'+name,im)
    for name,c in [('rail',DARK),('fill',CHALK)]:save('s-2502','item_level_'+name,fillbar(16,32,c),(4,8,4,8))
    im=canvas(8,48);d=ImageDraw.Draw(im);d.rectangle((3,0,4,47),fill=SLATE)
    save('s-2502','item_level_tick',im)
    p=Pictogram(24,32);p.shape([(6,6),(18,6),(18,14),(12,21),(6,14)],CHALK);save('s-2502','item_level_you',p.im)
    for name,c in [('normal',None),('alternate',None),('you',CHALK),('top_three',GOLD)]:
        im=plank(48,56,'wood' if name!='alternate' else 'well',accent=c if name=='you' else None)
        if name=='top_three':ImageDraw.Draw(im).rectangle((4,9,11,45),fill=c)
        save('s-2503','leaderboard_row_'+name,im,12,metadata={'tint_rank_strip':name=='top_three'})
    save('s-2504','heir_monument',monument())
    for name,c in [('ordinary',RIM),('latest',SLATE)]:
        save('s-2505','monument_inscription_'+name,plank(48,72,'well',accent=c),12)
    for name,c in [('normal',CHALK),('click',PAPER),('drag',SLATE),('text',CHALK)]:
        p=Pictogram(32)
        if name=='text':
            p.line([(6,4),(17,4)],c,2);p.line([(12,4),(12,20)],c,2);p.line([(6,20),(17,20)],c,2)
        else:
            p.shape([(1,1),(19,8),(12,11),(9,18),(5,16)],INK)
            p.shape([(3,3),(16,8),(10,10),(8,15),(6,14)],c)
            p.line([(7,8),(9,9)],SLATE,1,False)
            if name=='click':p.sparkle(18,16,4,CHALK)
            if name=='drag':p.box(14,12,20,18,PAPER,True)
        save('s-2602','cursor_'+name,p.im,metadata={'hotspot':[2,2] if name!='text' else [16,16]})
    for name in ('whole','cracked','shattered'):
        p=Pictogram(32)
        if name!='shattered':
            p.shape([(7,3),(17,3),(21,8),(21,16),(16,21),(7,21),(3,16),(3,8)],INK)
            p.shape([(7,4),(16,4),(19,8),(19,16),(15,20),(8,20),(5,16),(5,8)],SLATE)
            p.shape([(8,6),(15,6),(17,9),(17,15),(14,18),(9,18),(7,15),(7,9)],'#424b67')
            p.line([(8,9),(12,7)],CHALK,1,False)
            if name=='cracked':
                p.line([(15,6),(11,10),(14,13),(11,18)],CHALK,1,False)
                p.line([(14,13),(18,12)],SLATE,1,False)
        else:
            for points in [[(5,4),(11,5),(9,10),(4,12)],[(14,4),(20,8),(16,11),(13,9)],[(5,16),(10,12),(13,16),(10,21)],[(15,15),(21,13),(18,20),(14,21)]]:
                p.shape(points,SLATE);p.line(points[:2],CHALK,1,False)
        save('s-2603','revenge_mirror_'+name,p.im)
