from collections import OrderedDict
from pathlib import Path
import colorsys
import json
import math
import random
import numpy as np
from PIL import Image, ImageOps, ImageFilter, ImageDraw, ImageFont

from pixel_art import make_sources, make_backplates

root = Path(__file__).resolve().parent
sources = make_sources()
make_backplates(root/'backplates')
nearest = Image.Resampling.NEAREST
entries = OrderedDict()
frames_by_id = {}
for folder in ('sprites', 'tiles', 'maps'):
    (root/folder).mkdir(exist_ok=True)

def remove_danger(im):
    px = np.array(im)
    colors = np.unique(px[px[:,:,3]>0,:3],axis=0)
    for rgb in colors:
        r,g,b = map(int,rgb)
        if r<179 or r<g or r<b or r-min(g,b)<r*.70:
            continue
        h,s,v = colorsys.rgb_to_hsv(r/255,g/255,b/255)
        if h*360 >= 330 or h*360 <= 15:
            target = (29/360,min(s,.66),v) if 15 >= h*360 else (h,min(s,.66),v)
            px[np.all(px[:,:,:3]==rgb, axis=2),:3] = [round(c*255) for c in colorsys.hsv_to_rgb(*target)]
    return Image.fromarray(px)

def slot(name,index):
    im=sources[name]
    x,y=index%4,index//4
    return im.crop((round(x*im.width/4),round(y*im.height/4),round((x+1)*im.width/4),round((y+1)*im.height/4)))

def isolated(name,index,alpha=152,style='solid'):
    image=slot(name,index)
    arr=np.array(image)
    r,g,b,a=arr.transpose(2,0,1).astype(int)
    if style=='warm':
        valid=(a>=alpha)&(r-b>=30)&(g-b>=12)
    elif style=='blue':
        valid=(a>=alpha)&(b-r>=10)&(b-g>=2)
    elif style=='chalk':
        valid=(a>=alpha)&(r-b>=12)&(r<205)
    else:
        valid=a>=alpha
    mask=Image.fromarray(valid.astype('uint8')*255)
    # Eliminate isolated matte specks while preserving crisp silhouettes.
    clean=mask.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))
    if clean.getbbox():
        mask=clean
    image.putalpha(mask)
    bbox=mask.getbbox()
    if not bbox:
        raise ValueError(f'Empty generated sprite {name}.{index} ({style})')
    return image.crop(bbox)

def fit(im,size,extent=None,anchor='bottom',fill=False,pixel=1,colors=28):
    width,height=size
    target_w,target_h=extent or (width-4,height-4)
    if fill:
        scaled_w,scaled_h=target_w,target_h
    else:
        ratio=min(target_w/im.width,target_h/im.height)
        scaled_w=max(1,round(im.width*ratio))
        scaled_h=max(1,round(im.height*ratio))
    logical_size=(max(1,round(scaled_w/pixel)),max(1,round(scaled_h/pixel)))
    reduced=im.resize(logical_size,nearest)
    alpha=reduced.getchannel('A')
    rgb=reduced.convert('RGB').quantize(colors=colors,dither=Image.Dither.NONE).convert('RGBA')
    rgb.putalpha(alpha)
    scaled=remove_danger(rgb).resize((min(width,logical_size[0]*pixel),min(height,logical_size[1]*pixel)),nearest)
    result=Image.new('RGBA',size)
    y=(height-scaled.height)//2 if anchor=='center' else height-2-scaled.height
    result.alpha_composite(scaled,((width-scaled.width)//2,y))
    return result

def sprite(name,index,size,**kw):
    alpha=kw.pop('alpha',152)
    style=kw.pop('style','solid')
    return fit(isolated(name,index,alpha,style),size,**kw)

def transformed(im,dx=0,dy=0,scale=1,flip=False):
    copy=ImageOps.mirror(im) if flip else im
    bbox=copy.getchannel('A').getbbox()
    content=copy.crop(bbox)
    scaled=content.resize((max(1,round(content.width*scale)),max(1,round(content.height*scale))),nearest)
    result=Image.new('RGBA',im.size)
    result.alpha_composite(scaled,(round((bbox[0]+bbox[2]-scaled.width)/2)+dx,bbox[3]-scaled.height+dy))
    return result

def opacity(im,value):
    new=im.copy()
    new.putalpha(im.getchannel('A').point(lambda alpha:round(alpha*value)))
    return new

def glow(im,rgb,value):
    new=Image.new('RGBA',im.size,rgb+(0,))
    new.putalpha(im.getchannel('A').point(lambda alpha:round(alpha*value)))
    return new

def register(id,frames,rate=None,source_kind='hand-drawn pixel grid',anchor=None,**properties):
    size=frames[0].size
    assert all(im.size==size and im.getchannel('A').getbbox() for im in frames), id
    columns=min(8,len(frames))
    rows=math.ceil(len(frames)/columns)
    packed=Image.new('RGBA',(size[0]*columns,size[1]*rows))
    for i,im in enumerate(frames):
        packed.alpha_composite(im,((i%columns)*size[0],(i//columns)*size[1]))
    path=f'sprites/{id}.png'
    packed.save(root/path,optimize=True)
    entries[id]={'file':path,'frameSize':list(size),'frameCount':len(frames),'columns':columns,
                 'origin':anchor or [size[0]//2,size[1]-2],'source':source_kind,**properties}
    if rate:
        entries[id]['fps']=rate
    frames_by_id[id]=frames

def from_slots(id,name,ids,size,rate=None,source_kind='hand-drawn pixel grid',properties=None,**options):
    frames=[sprite(name,index,size,**options) for index in ids]
    register(id,frames,rate,source_kind,**(properties or {}))
    return frames

def hex_color(code):
    return tuple(bytes.fromhex(code.lstrip('#')))

palette={
    'day':{'grass':'#2F4A3A','dirt':'#4A4034','packed':'#494237','slate':'#2C3442','paving':'#363C40','hedge':'#1E3328'},
    'dusk':{'grass':'#3A2E3E','dirt':'#4A3830','packed':'#463632','slate':'#3B3346','paving':'#39323E','hedge':'#2A2233'},
    'night':{'grass':'#161F3A','dirt':'#222842','packed':'#252B40','slate':'#202942','paving':'#252F44','hedge':'#0E1428'}
}

yy,xx=np.indices((64,64),dtype=float)
dist=np.minimum.reduce([xx,yy,63-xx,63-yy])
blend=np.minimum(dist/5,1)
grain=2*np.sin(xx*2*np.pi/64)*np.sin(yy*2*np.pi/64)

def base_ground(index,kind,phase):
    rgb=np.array(slot('terrain',index).convert('RGB').resize((64,64),nearest),dtype=float)
    brightness=rgb.mean(axis=2)
    delta=np.clip((brightness-np.median(brightness))*.67,-15,14)*blend+grain
    target=hex_color(palette[phase][kind])
    array=np.clip(np.round(np.array(target)[None,None,:]+delta[:,:,None]),0,89).astype(np.uint8)
    minimum=np.ceil(array.max(axis=2,keepdims=True)*.65).astype(np.uint8)
    return Image.fromarray(np.maximum(array,minimum)).convert('RGBA')

def mix(outer,inner,mask):
    return Image.composite(inner,outer,Image.fromarray(np.uint8(np.clip(mask,0,1)*255)))

edge_names=['n','e','s','w']
corner_names=['nw','ne','se','sw']

def transitions(grass,dirt,mottled):
    result={}
    wav=(np.sin(xx*2*np.pi/64)*4+np.sin(xx*6*np.pi/64)*2)
    north=np.clip((31+wav-yy)/8+.5,0,1)
    for i,edge in enumerate(edge_names):
        weight=np.rot90(north,-i)
        result[f'edge.{edge}']=mix(grass[i],dirt[i],weight)
    outer=np.clip((31.5-np.hypot(xx,yy))/8+.5,0,1)
    inner=1-np.rot90(outer,2)
    for i,corner in enumerate(corner_names):
        result[f'outer.{corner}']=mix(grass[i],dirt[i],np.rot90(outer,-i))
        result[f'inner.{corner}']=mix(grass[i],dirt[i],np.rot90(inner,-i))
    if mottled:
        for i,(x,y) in enumerate([(26,25),(38,23),(24,39),(38,39)]):
            r=np.sqrt((xx-x)**2+(yy-y)**2)
            mask=np.clip((16+2*np.sin(xx*.35)+2*np.cos(yy*.30)-r)/4,0,1)
            result[f'mottle.{i+1:02}']=mix(grass[i],dirt[i],mask)
    return result

def tile_atlas(area,phase,tiles):
    columns=8
    img=Image.new('RGBA',(columns*64,math.ceil(len(tiles)/columns)*64))
    path=f'tiles/{area}.{phase}.png'
    for i,(key,tile) in enumerate(tiles.items()):
        x,y=(i%columns)*64,(i//columns)*64
        px=np.array(tile)
        min_ratio=.70 if phase=='day' and (key.startswith('grass.') or area=='field' and not key.startswith('dirt.')) else .65
        pixel_floor=np.ceil(px[:,:,:3].max(axis=2,keepdims=True)*min_ratio).astype(np.uint8)
        px[:,:,:3]=np.maximum(px[:,:,:3],pixel_floor)
        img.alpha_composite(Image.fromarray(px),(x,y))
        entries.setdefault(f'{area}.tile.{key}',{'size':[64,64],'variants':{},'source':'direct pixel drawing terrain normalized to shared palettes'})['variants'][phase]={'file':path,'rect':[x,y,64,64]}
    img.save(root/path,optimize=True)

for phase in palette:
    grass=[base_ground(i,'grass',phase) for i in range(4)]
    dirt=[base_ground(i+4,'dirt',phase) for i in range(4)]
    packed=[base_ground(i+8,'packed',phase) for i in range(4)]
    slate=[base_ground(i+12,'slate',phase) for i in range(4)]
    paving=[base_ground((i%4)+12,'paving',phase) for i in range(6)]
    paving[4]=ImageOps.mirror(paving[4])
    paving[5]=ImageOps.flip(paving[5])
    tile_atlas('field',phase,dict([(f'grass.{i+1:02}',t) for i,t in enumerate(grass)]+[(f'dirt.{i+1:02}',t) for i,t in enumerate(dirt)]+list(transitions(grass,dirt,True).items())))
    tile_atlas('plaza',phase,dict([(f'packed.{i+1:02}',t) for i,t in enumerate(packed)]+[(f'paving.{i+1:02}',t) for i,t in enumerate(paving)]+list(transitions(packed,paving,False).items())))
    tile_atlas('arena',phase,{f'slate.{i+1:02}':t for i,t in enumerate(slate)})

for i,name in enumerate(('sign','potion.round','potion.triangular','discarded_chair','target','mushroom_ring','discarded_tongs','discarded_pot','discarded_hook','discarded_slate','discarded_pouch','discarded_rope')):
    size=(128,64) if i in (3,5) else (64,64)
    from_slots(f'field.prop.{name}','field',[i],size,extent=(size[0]-10,51),properties={'collision':False, **({'displayText':'탑 ← 3000걸음'} if name=='sign' else {})})

for i in range(3):
    from_slots(f'field.wall.rocks.{i+1:02}','field',[12+i],(64,64),extent=(64,48))
    for area,index,size in (('field',i,(64,64)),('arena',8+i,(64,96))):
        sprite_width,sprite_height=(64,36) if area=='field' else (64,55)
        wall=sprite('walls',index,size,extent=(sprite_width,sprite_height),fill=True)
        register(f'{area}.wall.n.{i+1:02}',[wall])
        register(f'{area}.wall.s.{i+1:02}',[ImageOps.mirror(wall)],source_kind='mirrored variant')
for area,index,size in (('field',3,(64,64)),('arena',11,(64,96))):
    for i,k in enumerate([index,5 if area=='field' else index]):
        wall=sprite('walls',k,size,extent=(44,size[1]-2),fill=True)
        if area=='arena' and i:
            wall=ImageOps.mirror(wall)
        register(f'{area}.wall.w.{i+1:02}',[wall],mirrorFor='e')
    index=4 if area=='field' else 12
    corner=sprite('walls',index,(64,64),extent=(62,62),fill=True,anchor='center')
    for turns,direction in enumerate(('sw','nw','ne','se')):
        rotated=corner.transpose((Image.Transpose.ROTATE_270,Image.Transpose.ROTATE_180,Image.Transpose.ROTATE_90)[turns-1]) if turns else corner
        placed=Image.new('RGBA',size)
        placed.alpha_composite(rotated,(0,size[1]-64))
        register(f'{area}.wall.corner.{direction}',[placed],source_kind='direct pixel drawing corner rotated on grid')

opening=sprite('actions',3,(128,64),extent=(126,62),fill=True,anchor='center',alpha=190)
shakes=[sprite('actions',i,(128,64),extent=(126,62),fill=True,anchor='center',alpha=190) for i in range(4)]
for direction in ('n','s','w'):
    arr=[opening]+shakes
    if direction=='s':
        arr=[ImageOps.mirror(im) for im in arr]
    if direction=='w':
        arr=[im.transpose(Image.Transpose.ROTATE_270) for im in arr]
    register(f'field.spawn.{direction}',arr,8,source_kind='direct pixel drawing, mirrored or rotated as needed',openFrame=0,shakeFrames=[1,2,3,4],mirrorFor='e' if direction=='w' else None)

from_slots('field.tower','actions',[0+4,1+4,2+4,3+4,1+4],(512,128),6,alpha=191,extent=(498,126),fill=True,properties={'idleFrame':0,'laundryFrames':[1,2,3,4]})
window_closed=sprite('scenery',14,(64,64),extent=(60,60))
window_open=sprite('people',14,(64,64),extent=(60,60))
window_toss=sprite('people',15,(64,64),extent=(62,60))
register('field.tower.window',[window_closed,window_open,transformed(window_open,dy=-1),window_toss],6,poseFrames={'closed':0,'look':[1,2],'throw':3})
from_slots('field.campfire','fire',[1,2,3,0],(64,64),8,extent=(50,58),alpha=221)
from_slots('facility.campfire','fire',range(4,10),(128,128),8,extent=(110,112),alpha=221)
from_slots('field.moon','scenery',[10],(96,96),extent=(86,86),anchor='center',alpha=212)
moon=sprite('effects',8,(256,256),style='blue',alpha=230,extent=(250,250),anchor='center')
register('field.moonlight',[opacity(moon,.16)],anchor=[128,128])
puddles=[opacity(sprite('fire',i,(64,32),style='blue',alpha=215,extent=(56,19),anchor='center'),.40) for i in (10,11,12)]
register('field.weather.puddle',puddles,5,anchor=[32,16])

rains=[]
for i in range(4):
    raw=sprite('fire',13+i%3,(160,128),style='blue',alpha=155,extent=(142,114),anchor='center')
    strip=Image.new('RGBA',(1920,128))
    for j in range(12):
        strip.alpha_composite(opacity(raw,.33),((j*160+27*i)%1920,0))
    rains.append(strip)
register('field.weather.rain',rains,9,anchor=[0,0],source_kind='direct pixel drawing marks tiled to full screen strip')

fogs=[]
for i in (10,11):
    im=sprite('effects',i,(512,192),style='blue',alpha=191,extent=(502,110),fill=True,anchor='center')
    fogs.append(opacity(im.filter(ImageFilter.GaussianBlur(4)),.24))
register('field.weather.fog',fogs,3,anchor=[0,0])
ring=[]
for i,radius in enumerate((6,9,12,14)):
    im=Image.new('RGBA',(32,32))
    pixels=np.array(im)
    y,x=np.indices((32,32))
    dx,dy=x-15.5,y-15.5
    distance=np.hypot(dx,dy)
    angle=np.mod(np.arctan2(dy,dx)+2*np.pi, np.pi/2)
    rim=(abs(distance-radius)<.85)&(angle>.16)&(angle<1.41)
    pixels[rim]=[231,193,119,205]
    pixels[rim&(abs(distance-radius)<.45)]=[255,230,172,255]
    ring.append(Image.fromarray(pixels))
streak=sprite('effects',4,(32,32),style='warm',alpha=210,extent=(12,12),anchor='center',colors=12)
star=sprite('effects',5,(32,32),style='warm',alpha=210,extent=(23,23),anchor='center',colors=12)
crystal=sprite('effects',6,(32,32),style='warm',alpha=216,extent=(18,19),anchor='center',colors=12)
glint=sprite('effects',7,(32,32),style='warm',alpha=216,extent=(22,20),anchor='center',colors=12)
register('field.meteor.ring',ring,6,anchor=[16,16],source_kind='pixel-aligned radius overlay using direct pixel drawing gold palette')
register('field.meteor.fall',[transformed(streak,dx=-4,dy=-6,scale=.7),transformed(streak,dx=-2,dy=-4),transformed(star,dx=-1,dy=-2,scale=.75),star,transformed(crystal,dy=-2),crystal],12,anchor=[16,16],source_kind='direct pixel drawing poses with geometric in-betweens')
register('field.meteor.sparkle',[crystal,glint,transformed(glint,scale=.9),transformed(crystal,dy=-1)],8,anchor=[16,16])
bird=[sprite('people',i,(32,32),extent=(28,23),anchor='center',colors=10,alpha=188) for i in (7,8,9,10)]
note=sprite('people',11,(32,32),extent=(17,14),anchor='center',colors=12,alpha=188)
register('field.crow',[bird[0],bird[1],bird[2],bird[1]],9,anchor=[16,16])
register('field.crow.note',[note,transformed(note,flip=True,dy=2)],7,anchor=[16,16])

from_slots('facility.log_seat','plaza',[1,2,3],(64,64),extent=(56,54))
from_slots('facility.market.mat','plaza',[4,5,6],(128,64),extent=(120,54),fill=True)
from_slots('facility.market.crate','plaza',[7,8],(128,64),extent=(85,56))
from_slots('facility.duel_board','actions',[12,13,14],(128,128),6,extent=(119,115),alpha=194)
from_slots('facility.arena_path','plaza',[10],(128,128),extent=(126,117),fill=True)
monument=sprite('plaza',11,(96,128),extent=(89,120))
highlights=[]
for i in range(4):
    stamp=glow(monument,(223,191,116),.05*(i+1))
    highlights.append(Image.alpha_composite(monument,stamp))
register('facility.heir_monument',[monument]+highlights,7,source_kind='direct pixel drawing with light phases')

master=[sprite('people',i,(96,96),extent=(78,79),pixel=2,colors=16,alpha=178) for i in range(5)]
master_sequence=[
    master[0],transformed(master[0],dy=1),master[0],transformed(master[0],dy=-1),
    master[1],master[2],master[2],transformed(master[2],dy=1),
    master[3],transformed(master[3],dx=1),transformed(master[3],dx=1,dy=-1),master[3]
]
register('facility.master_chair',master_sequence,6,source_kind='five drawn direct pixel drawing poses with held and nudged frames',poseFrames={'sleep':[0,1,2,3],'wake':[4,5],'speak':[6,7],'reward':[8,9,10,11]})
from_slots('facility.training_dummy','actions',[8,9,10,11],(64,96),10,extent=(62,85),alpha=197)
cart=sprite('plaza',14,(128,96),extent=(102,90))
merchant=[sprite('people',i,(64,64),extent=(55,57),pixel=2,colors=16,alpha=184) for i in (5,6)]
register('npc.merchant',[merchant[0],transformed(merchant[0],dy=-1),merchant[1],transformed(merchant[1],dy=-1)],6,source_kind='direct pixel drawing poses with body-shift frames',poseFrames={'idle':[0,1],'gesture':[2,3]})
cart_states=[cart]
for body in frames_by_id['npc.merchant']:
    state=Image.new('RGBA',(128,96))
    state.alpha_composite(cart,(0,0))
    state.alpha_composite(body,(62,24))
    cart_states.append(state)
register('facility.merchant_cart',cart_states,6,source_kind='direct pixel drawing NPC composited with cart',poseFrames={'cart':0,'idle':[1,2],'gesture':[3,4]})
mail=sprite('plaza',12,(64,96),extent=(57,88))
mail_states=[mail]
for rise in (0,2):
    state=mail.copy()
    state.alpha_composite(note,(16,47-rise))
    mail_states.append(state)
for rise in (0,1):
    state=mail.copy()
    state.alpha_composite(bird[3],(16,0-rise))
    mail_states.append(state)
register('facility.crow_mail',mail_states,6,source_kind='direct pixel drawing base, note and crow',poseFrames={'empty':0,'letter':[1,2],'crow':[3,4]})
from_slots('facility.field_gate','actions',[3,0,1,2],(192,128),8,extent=(188,110),fill=True,alpha=192)
from_slots('plaza.tower','scenery',[1],(640,192),extent=(602,188),fill=True)
for index,name in enumerate(('tent','laundry','cauldron','firewood','basket','blanket','barrel','stakes'),2):
    from_slots(f'plaza.prop.{name}','scenery',[index],(64,64),extent=(61,57))

def portrait_from_body(index,box):
    original=isolated('people',index,183)
    w,h=original.size
    return fit(original.crop((round(w*box[0]),round(h*box[1]),round(w*box[2]),round(h*box[3]))),(128,128),extent=(111,120),pixel=3,colors=16)
master_portrait=sprite('people',12,(128,128),extent=(111,120),pixel=3,colors=16,alpha=182)
merchant_portrait=sprite('people',13,(128,128),extent=(111,120),pixel=3,colors=16,alpha=182)
master_sleep=portrait_from_body(0,(.10,.02,.88,.68))
master_one=portrait_from_body(1,(.09,.02,.88,.69))
master_speaks=portrait_from_body(2,(.08,.03,.80,.69))
merchant_speaks=portrait_from_body(6,(.00,.00,.83,.63))
register('S-2405',[master_sleep,master_one,master_portrait,master_speaks,transformed(master_speaks,dy=-1)],6,source_kind='direct pixel drawing portrait and cropped face poses',poseFrames={'sleep':0,'oneEye':1,'awake':2,'talk':[3,4]})
register('S-2407',[merchant_portrait,merchant_speaks,transformed(merchant_speaks,dy=-1)],6,source_kind='direct pixel drawing portrait and cropped face poses',poseFrames={'idle':0,'talk':[1,2]})

chalk=Image.new('RGBA',(64,64))
chalk_brush=ImageDraw.Draw(chalk)
for low,high,opacity_value in ((0,12,162),(14,28,136),(30,47,153),(50,63,144)):
    chalk_brush.line((31,low,31,high),fill=(196,184,169,opacity_value),width=2)
register('arena.chalk.center',[chalk],anchor=[32,32],source_kind='pixel-aligned overlay using direct pixel drawing chalk palette')
marker=Image.new('RGBA',(64,64))
chalk_brush=ImageDraw.Draw(marker)
chalk_ink=(196,184,169,178)
chalk_brush.line((25,31,38,31),fill=chalk_ink,width=2)
chalk_brush.line((31,25,31,38),fill=chalk_ink,width=2)
for x,y,sx,sy in ((19,19,1,1),(44,19,-1,1),(19,44,1,-1),(44,44,-1,-1)):
    chalk_brush.line((x+5*sx,y,x,y,x,y+5*sy),fill=chalk_ink,width=2)
register('arena.chalk.start',[marker,ImageOps.mirror(marker)],anchor=[32,32],source_kind='pixel-aligned overlay using direct pixel drawing chalk palette')
from_slots('arena.pillar','walls',[13],(64,96),extent=(59,84),properties={'footprintTiles':[1,1]})
chips=sprite('walls',15,(64,96),extent=(29,28),anchor='center')
register('arena.pillar.chips',[transformed(chips,dx=1,dy=3,scale=.67),chips,opacity(transformed(chips,dx=-2,dy=-5,scale=1.15),.52)],12,source_kind='direct pixel drawing stones, in-between transforms')
crowd=isolated('scenery',15,164)
crowd_parts=[]
for i in range(3):
    piece=crowd.crop((round(i*crowd.width/3),0,round((i+1)*crowd.width/3),crowd.height))
    piece=piece.crop(piece.getchannel('A').getbbox())
    pose=fit(piece,(64,64),extent=(40,54),colors=9)
    crowd_parts.extend([pose,ImageOps.mirror(pose)])
for i,pose in enumerate(crowd_parts,1):
    register(f'arena.spectator.{i:02}',[pose,transformed(pose,dy=-1)],3,source_kind='direct pixel drawing silhouettes with mirrored and body-shift variants')
ritual_frames=[fit(isolated('people',i,180),(64,64),extent=(53,57),colors=16) for i in (1,1,2,3,4,0,0)]
ritual_frames[1]=transformed(ritual_frames[1],dy=-1)
ritual_frames[6]=transformed(ritual_frames[6],dy=1)
register('ritual.master.reaction',ritual_frames,7,poseFrames={'oneEye':[0,1],'awake':[2,3,4],'doze':[5,6]},
         source_kind='direct pixel poses with chair movement')
register('arena.master_applause',[master[4],transformed(master[4],dy=-1)],7,source_kind='direct pixel drawing pose with movement in-between')
overlay=Image.new('RGBA',(1920,1080),(122,143,168,13))
register('arena.overcast',[overlay],anchor=[0,0],source_kind='tint overlay')
for phase,color in (('dusk',(180,131,60)),('night',(90,108,168))):
    rims=[]
    for i in range(3):
        original=frames_by_id[f'field.wall.n.{i+1:02}'][0]
        source_alpha=np.array(original.getchannel('A'))
        selection=np.zeros_like(source_alpha)
        for x in range(source_alpha.shape[1]):
            valid=np.flatnonzero(source_alpha[:,x]>0)
            if len(valid):
                top=valid[0]
                selection[top:top+2,x]=160
        rim=Image.new('RGBA',original.size,color+(0,))
        rim.putalpha(Image.fromarray(selection))
        rims.append(rim)
    register(f'field.wall.rim.{phase}',rims,source_kind='edge mask from direct pixel drawing hedge')

for area in ('field','plaza','arena'):
    path=f'backplates/{area}.png'
    im=Image.open(root/path)
    entries[f'{area}.backplate']={'file':path,'size':list(im.size),'source':'hand-drawn pixel grid','gridAlignment':'scenic composition; use modular assets for exact gameplay geometry'}

spawn_x=(4,11,18)
spawn_y=(2,6,10)
field_entrances=[
    {'side':s,'tile':[x,y],'tileExtent':extent,'sprite':f'field.spawn.{s if s!="e" else "w"}', 'mirrorX':s=='e'}
    for s,coords,extent in (
        ('n',[(x,0) for x in spawn_x],[2,1]), ('s',[(x,13) for x in spawn_x],[2,1]),
        ('w',[(0,y) for y in spawn_y],[1,2]), ('e',[(23,y) for y in spawn_y],[1,2])
    ) for x,y in coords
]

def field_layout(seed):
    rng=random.Random(seed)
    center_x,center_y=11.8+(seed%3-1)*.4,7.1+(seed%2-.5)*.4
    radius_x,radius_y=7.0+seed%3*.25,4.0+seed%2*.2
    def inside(x,y):
        angle=math.atan2((y-center_y)/radius_y,(x-center_x)/radius_x)
        skew=.93+.10*math.sin(3*angle+seed)+.04*math.cos(5*angle+seed*.5)
        return ((x-center_x)/radius_x)**2+((y-center_y)/radius_y)**2 < skew
    field=[]
    for y in range(14):
        line=[]
        for x in range(24):
            vertices=[inside(x+dx,y+dy) for dx,dy in [(0,0),(1,0),(1,1),(0,1)]]
            count=sum(vertices)
            if count==1:
                suffix=f'outer.{corner_names[vertices.index(True)]}'
            elif count==2:
                edge=next((i for i in range(4) if vertices[i] and vertices[(i+1)%4]),None)
                suffix=f'edge.{edge_names[edge]}' if edge is not None else f'mottle.{rng.randint(1,4):02}'
            elif count==3:
                suffix=f'inner.{corner_names[(vertices.index(False)+2)%4]}'
            elif count==4:
                suffix=f'dirt.{rng.randint(1,4):02}'
            elif abs(x-center_x)<radius_x+1 and abs(y-center_y)<radius_y+1 and rng.random()<.17:
                suffix=f'mottle.{rng.randint(1,4):02}'
            else:
                suffix=f'grass.{rng.randint(1,4):02}'
            line.append(f'field.tile.{suffix}')
        field.append(line)
    wall=[]
    blocked=set()
    for e in field_entrances:
        x,y=e['tile']
        width,height=e['tileExtent']
        blocked.update((x+dx,y+dy) for dx in range(width) for dy in range(height))
    for y in range(14):
        for x in range(24):
            if (x,y) in blocked or (0<x<23 and 0<y<13):
                continue
            direction={(0,0):'nw',(23,0):'ne',(0,13):'sw',(23,13):'se'}.get((x,y))
            if direction:
                wall.append({'tile':[x,y],'sprite':f'field.wall.corner.{direction}'})
                continue
            side='n' if y==0 else 's' if y==13 else 'w' if x==0 else 'e'
            wall.append({'tile':[x,y],'sprite':f'field.wall.{"w" if side=="e" else side}.{rng.randint(1,2 if side in "ew" else 3):02}','mirrorX':side=='e'})
    prop_locations=[(x,y) for x in range(1,23) for y in range(1,13) if (y in (1,12) or x in (1,22))]
    rng.shuffle(prop_locations)
    props=[]
    prop_ids=[asset for asset in entries if asset.startswith('field.prop.')]
    for name in rng.sample(prop_ids,8):
        width=entries[name]['frameSize'][0]//64
        loc=next((loc for loc in prop_locations if width==1 or (loc[0]<22 and loc[1] in (1,12) and (loc[0]+1,loc[1]) in prop_locations)),None)
        assert loc
        x,y=loc
        for dx in range(width):
            prop_locations.remove((x+dx,y))
        props.append({'tile':[x,y],'sprite':name})
    return {'id':f'field.{seed:02}','tilePx':64,'sizeTiles':[24,14],
            'ground':field,'walls':wall,'spawns':field_entrances,'props':props,
            'wallCollision':'continuous outer boundary; spawn openings use the same movement/shot restriction',
            'campfireCenter':[12,7]}

def arena_layout():
    rng=random.Random(204)
    rows=[[f'arena.tile.slate.{rng.randint(1,4):02}' for _ in range(24)] for _ in range(14)]
    wall=[]
    for y in range(14):
        for x in range(24):
            if 0<x<23 and 0<y<13:
                continue
            direction={(0,0):'nw',(23,0):'ne',(0,13):'sw',(23,13):'se'}.get((x,y))
            if direction:
                wall.append({'tile':[x,y],'sprite':f'arena.wall.corner.{direction}'})
            else:
                side='n' if y==0 else 's' if y==13 else 'w' if x==0 else 'e'
                wall.append({'tile':[x,y],'sprite':f'arena.wall.{"w" if side=="e" else side}.{rng.randint(1,2 if side in "ew" else 3):02}','mirrorX':side=='e'})
    return {'id':'arena','tilePx':64,'sizeTiles':[24,14],'ground':rows,'walls':wall,'pillars':[
                {'tile':point,'sprite':'arena.pillar','footprintTiles':[1,1]}
                for point in ([9,3],[14,3],[9,10],[14,10])],
            'chalk':{'centerX':12.0,'centerLineSprite':'arena.chalk.center',
                     'starts':[[6.0,7.0],[18.0,7.0]],'startSprite':'arena.chalk.start'},
            'matchmakingPalette':'day','roomPalettes':['day','dusk','night']}

def plaza_layout():
    rng=random.Random(998)
    rows=[]
    for y in range(24):
        row=[]
        for x in range(40):
            distance=((x-19.5)/7.5)**2+((y-11.5)/4.5)**2
            if (x in (19,20) or (y in (11,12) and x>=21)) and distance>1.18:
                id=f'plaza.tile.paving.{rng.randint(1,6):02}'
            elif distance<1.15:
                id=f'plaza.tile.packed.{rng.randint(1,4):02}'
            else:
                id=f'field.tile.grass.{rng.randint(1,4):02}'
            row.append(id)
        rows.append(row)
    slots=[
        ('facility.campfire',20,12),('facility.market.mat',7,10),('facility.merchant_cart',7,17),
        ('facility.duel_board',32,9),('facility.arena_path',38,11),('facility.heir_monument',19,5),
        ('facility.master_chair',23,5),('facility.training_dummy',20,18),('facility.crow_mail',24,21),('facility.field_gate',20,23)
    ]
    decoration_slots=[
        ('facility.log_seat',17,12,1),('facility.log_seat',23,12,1),('facility.log_seat',20,14,0),
        ('facility.market.mat',9,12,1),('facility.market.mat',6,14,2),
        ('facility.market.crate',10,10,0),('facility.market.crate',5,12,1),
        ('plaza.prop.tent',4,6,0),('plaza.prop.laundry',16,4,0),
        ('plaza.prop.cauldron',14,13,0),('plaza.prop.firewood',24,14,0),
        ('plaza.prop.basket',26,6,0),('plaza.prop.blanket',19,15,0),
        ('plaza.prop.barrel',13,6,0),('plaza.prop.stakes',29,7,0)
    ]
    return {'id':'plaza','tilePx':64,'sizeTiles':[40,24],'ground':rows,
            'facilities':[{'id':id,'position':[x,y]} for id,x,y in slots],
            'decorations':[{'id':id,'position':[x,y],'frame':frame} for id,x,y,frame in decoration_slots]}

layouts=[field_layout(i) for i in range(1,7)]+[plaza_layout(),arena_layout()]
for layout in layouts:
    (root/f'maps/{layout["id"]}.json').write_text(json.dumps(layout,ensure_ascii=False,separators=(',',':'))+'\n')

manifest={'tilePx':64,'textureFilter':'nearest','sheetOrder':'row-major',
          'paletteTargets':palette,'layouts':[f'maps/{data["id"]}.json' for data in layouts],
          'shared':{'plaza.grass':'field.tile.grass.*','plaza.alert_crow':'field.crow','arena.master_idle':'facility.master_chair'},
          'assets':entries}
(root/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n')

def preview_map(layout,phase):
    width,height=layout['sizeTiles']
    scene=Image.new('RGBA',(width*64,height*64))
    atlas_cache={}
    for y,row in enumerate(layout['ground']):
        for x,id in enumerate(row):
            item=entries[id]['variants'][phase]
            filename=item['file']
            if filename not in atlas_cache:
                atlas_cache[filename]=Image.open(root/filename)
            tx,ty,w,h=item['rect']
            scene.alpha_composite(atlas_cache[filename].crop((tx,ty,tx+w,ty+h)),(x*64,y*64))
    for placement in layout.get('walls',[])+layout.get('spawns',[])+layout.get('props',[])+layout.get('pillars',[]):
        frame=frames_by_id[placement['sprite']][0]
        if placement.get('mirrorX',False):
            frame=ImageOps.mirror(frame)
        x,y=placement['tile']
        _,tall=placement.get('tileExtent',[1,1])
        scene.alpha_composite(frame,(x*64,(y+tall)*64-frame.height))
    if 'chalk' in layout:
        line=frames_by_id['arena.chalk.center'][0]
        for y in range(1,13):
            scene.alpha_composite(line,(12*64-32,y*64))
        for i,(x,y) in enumerate(layout['chalk']['starts']):
            scene.alpha_composite(frames_by_id['arena.chalk.start'][i],(round(x*64-32),round(y*64-32)))
    for facility in sorted(layout.get('facilities',[])+layout.get('decorations',[]),key=lambda placement:placement['position'][1]):
        frame=frames_by_id[facility['id']][facility.get('frame',0)]
        x,y=facility['position']
        scene.alpha_composite(frame,(x*64-frame.width//2,y*64-frame.height+2))
    if layout['id']=='plaza':
        frame=frames_by_id['plaza.tower'][0]
        scene.alpha_composite(frame,(1280-frame.width//2,0))
    return scene

preview=Image.new('RGB',(1560,785),'#131826')
draw=ImageDraw.Draw(preview)
font=ImageFont.load_default(size=19)
for i,area in enumerate(('field','plaza','arena')):
    x=18+514*i
    draw.text((x,14),area.upper(),fill='#E8DCD0',font=font)
    backdrop=Image.open(root/f'backplates/{area}.png')
    preview.paste(backdrop.resize((496,279),Image.Resampling.LANCZOS),(x,40))
    layout=layouts[0 if area=='field' else 6 if area=='plaza' else 7]
    map_im=preview_map(layout,'day')
    map_im.thumbnail((496,288),nearest)
    draw.text((x,331),'64PX TILE LAYOUT',fill='#B2BBCD',font=font)
    preview.paste(map_im,(x,357),map_im)
sample_ids=['field.prop.sign','field.prop.potion.round','field.campfire','facility.master_chair','npc.merchant','facility.training_dummy',
            'facility.crow_mail','facility.duel_board','facility.heir_monument','S-2405','S-2407','arena.pillar','field.crow','field.moon']
draw.text((18,665),'SAMPLE SPRITES · GAME SCALE',fill='#B2BBCD',font=font)
for i,id in enumerate(sample_ids):
    im=frames_by_id[id][0]
    im.thumbnail((100,100),nearest)
    preview.paste(im,(20+i*109+(100-im.width)//2,777-im.height),im)
preview.save(root/'preview.png',optimize=True)

for id,item in entries.items():
    if 'frameCount' in item:
        im=Image.open(root/item['file'])
        for n in range(item['frameCount']):
            w,h=item['frameSize']
            x,y=(n%item['columns'])*w,(n//item['columns'])*h
            cut=im.crop((x,y,x+w,y+h))
            assert cut.getbbox(),(id,n)
            for r,g,b,a in cut.get_flattened_data():
                if a:
                    h_deg,s,v=colorsys.rgb_to_hsv(r/255,g/255,b/255)
                    assert not ((h_deg*360>=330 or h_deg*360<=15) and s>=.70 and v>=.70),(id,n,r,g,b,a)
    if 'variants' in item:
        for variant in item['variants'].values():
            im=Image.open(root/variant['file'])
            x,y,w,h=variant['rect']
            cut=im.crop((x,y,x+w,y+h))
            assert cut.getchannel('A').getextrema()==(255,255)
            assert all(s<=.35000001 and v<=.35 for r,g,b,a in cut.get_flattened_data() for _,s,v in [colorsys.rgb_to_hsv(r/255,g/255,b/255)]),id
for layout in layouts:
    width,height=layout['sizeTiles']
    assert len(layout['ground'])==height
    assert all(len(row)==width and all(id in entries for id in row) for row in layout['ground'])
    assert all(place['sprite'] in entries for layer in ('walls','spawns','props','pillars') for place in layout.get(layer,[]))
    if 'spawns' in layout:
        assert len(layout['spawns'])==12
        for p in layout['props']:
            x,y=p['tile']
            assert x in (1,22) or y in (1,12)
print(f'{len(entries)} mapped entries, {sum("variants" in item for item in entries.values())} tiles, {sum(item.get("frameCount",0) for item in entries.values())} frame cells, {len(layouts)} layout files')
