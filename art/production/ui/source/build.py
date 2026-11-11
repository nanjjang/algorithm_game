"""Bake authored pixel UI assets and pack their atlases. Requires Pillow and fontTools."""
import json
from collections import defaultdict
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
from algorithm_ui_kit import ASSETS, ROOT, canvas, fonts, icons, kit
from algorithm_ui_screens import screens
from algorithm_ui_illustrations import illustrations

def pack(group,assets,size=2048):
    """Pack full image strips as single rectangles with a two-pixel outer gutter."""
    assets=sorted(assets,key=lambda a:(-a['height'],-a['width']*a['frames'],a['file']))
    usable=[]
    page=canvas(size)
    page_num=0
    x,y=2,2
    row=0

    def write_page():
        name=f'atlas/{group}-{page_num}.png'
        path=ROOT/name
        path.parent.mkdir(exist_ok=True)
        page.save(path,optimize=True)
        usable.append(name)

    for asset in assets:
        im=Image.open(ROOT/asset['file']).convert('RGBA')
        if im.width+4>size or im.height+4>size:
            continue
        if x+im.width+2>size:
            x=2
            y+=row+2
            row=0
        if y+im.height+2>size:
            write_page()
            page_num+=1
            page=canvas(size)
            x,y,row=2,2,0
        page.alpha_composite(im,(x,y))
        asset['atlas']={'page':f'atlas/{group}-{page_num}.png','rect':[x,y,im.width,im.height]}
        x+=im.width+2
        row=max(row,im.height)
    if row:
        write_page()
    return usable

def preview():
    by_id=defaultdict(list)
    for a in ASSETS:
        by_id[a['id']].append(a)
    out=Image.new('RGB',(1120,706),'#171924')
    d=ImageDraw.Draw(out)
    font_path=ROOT/'font/neodgm.ttf'

    def label(x,y,s,size=16,c='#e8ddbf'):
        d.text((x,y),s,font=ImageFont.truetype(str(font_path),size),fill=c)

    def put(asset,x,y,scale=1,frame_index=0):
        im=Image.open(ROOT/asset['file']).convert('RGBA')
        if asset['frames']>1:
            w=asset['width']
            im=im.crop((frame_index*w,0,(frame_index+1)*w,im.height))
        if scale!=1:
            im=im.resize((int(im.width*scale),int(im.height*scale)),Image.Resampling.NEAREST)
        out.paste(im,(x,y),im)

    label(24,19,'WILDERNESS APPRENTICES',30)
    label(25,57,'Interface sprites',16,'#8e9aae')
    for y,title in [(101,'Wood, brass & folded paper'),(260,'Spell blocks'),(367,'Element seals & league shields'),(503,'Ritual stones, charm & currency'),(645,'Profile badges, marks & cursor')]:
        label(26,y,title,20)
    top=[by_id[k][0] for k in ['K-01','K-02','K-03','K-23']]+[by_id['K-23'][2],by_id['K-24'][0],by_id['K-44'][0],by_id['K-66'][2]]
    for i,a in enumerate(top):
        put(a,26+i*119,129,2)
    for i,a in enumerate(by_id['I-B2']):
        put(a,27+i*67,296,2)
    for i,a in enumerate(by_id['I-E1']):
        put(a,27+i*79,408,2)
    for i,a in enumerate(by_id['I-L2']):
        put(a,541+i*105,393,2)
    mats=by_id['I-M1']+[by_id['I-M3'][1]]+by_id['I-M4']+by_id['I-C3']
    for i,a in enumerate(mats):
        put(a,22+i*107,534,2)
    for i,a in enumerate(by_id['I-F12']):
        put(a,26+i*167,670,.72)
    for i,a in enumerate(by_id['S-2603']):
        put(a,380+i*42,666)
    for i,a in enumerate(by_id['S-2602']):
        put(a,520+i*42,666)
    out.save(ROOT/'ui-preview.png',optimize=True)

    images=[by_id[key][0] for key in ('S-0501','S-0901','S-1602','S-2307','S-2506','S-2601')]
    images[0]=next(a for a in by_id['S-0501'] if a['width']==1920)
    scene=Image.new('RGB',(1180,516),'#171924')
    draw=ImageDraw.Draw(scene)
    headings=('Camp menu','Duel opening','Expedition results','Night market','Rankings','Loading trail')
    for i,a in enumerate(images):
        x=20+(i%3)*386
        y=18+(i//3)*248
        draw.text((x,y),headings[i],fill='#e8ddbf',font=ImageFont.truetype(str(font_path),18))
        im=Image.open(ROOT/a['file']).convert('RGB').resize((368,207),Image.Resampling.NEAREST)
        scene.paste(im,(x,y+28))
    scene.save(ROOT/'backgrounds-preview.png',optimize=True)

def main():
    kit()
    icons()
    fonts()
    screens()
    illustrations()
    groups=defaultdict(list)
    for asset in ASSETS:
        path=Path(asset['file'])
        if path.parts[0] in ('kit','icon'):
            groups[path.parts[0]].append(asset)
        if path.parts[0]=='screen' and asset['width']<1920 and asset['id']!='S-0203':
            groups[path.parts[1]].append(asset)
    atlases={}
    for group,assets in groups.items():
        atlases[group]=pack(group,assets)
    data={
        'basis':'docs/ui-assets.md',
        'reference_resolution':[1920,1080],
        'sampling':'nearest',
        'mipmaps':False,
        'texture_compression':False,
        'frames':'horizontal strips; width and height are per frame',
        'nine_slice':'pixel insets [left, top, right, bottom]',
        'atlases':atlases,
        'external':{
            'S-0201':{'file':'../backgrounds/title_1920x1080.png','variant':'selected title illustration'},
            'S-2405':{'file':'../world/sprites/S-2405.png','manifest':'../world/manifest.json'},
            'S-2407':{'file':'../world/sprites/S-2407.png','manifest':'../world/manifest.json'},
        },
        'assets':sorted(ASSETS,key=lambda a:(a['id'],a['file'])),
    }
    (ROOT/'manifest.json').write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    preview()
    print(f'{len(ASSETS)} PNG assets, {sum(a["frames"] for a in ASSETS)} frames')

if __name__=='__main__':
    main()
