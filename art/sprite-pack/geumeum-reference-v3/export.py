from pathlib import Path
from PIL import Image, ImageDraw
import numpy as np, json, shutil, zipfile
ROOT=Path(__file__).resolve().parent
HEX=['1B0D1B','561F4C','7A336A','B25A9B','E2ACFA','222641','3E4675','6271AE','CA8F44','F8C062','704456','A47384','854972','8539BF','D68CF9','FDF6FF']
PAL=np.array([tuple(bytes.fromhex(c)) for c in HEX],dtype=np.int32)
combat=[('idle',4,160,True),('walk',6,100,True),('aim_walk',4,130,True),('fire',3,90,False),('cast',4,110,False),('shadow_hand',6,100,False),('hurt',2,100,False)]
social=[('death',6,140,False),('sit_idle',2,300,True)]+[(x,4,140,x=='dance') for x in ['wave','clap','taunt','bow','sit_down','dance','raise_staff','despair']]+[('upgrade_wait',2,250,True),('upgrade_charge',2,180,True),('upgrade_success',4,160,False),('upgrade_fail',4,180,False),('duel_win',4,160,False),('duel_lose',4,180,False)]
manifest={'character':'geumeum','reference':'idle-hood-and-hem.png','frame_size':[64,64],'palette':['#'+c for c in HEX],'provenance':'Built-in image generation, then nearest-neighbor export and fixed palette mapping; not native 64px generation.','attachment_status':'Hand, head, shoulder sockets require manual registration; not claimed production-ready.','clips':{}}
allframes=[]
for direction in ['down','right','up','social']:
 im=Image.open(ROOT/'source'/f'{direction}.png').convert('RGBA'); rows=10 if direction=='social' else 5; cw,ch=im.width/6,im.height/rows
 cells=[]
 for i in range(rows*6):
  col,row=i%6,i//6
  cell=im.crop((round(col*cw),round(row*ch),round((col+1)*cw),round((row+1)*ch)))
  alpha=cell.getchannel('A').point(lambda x:255 if x>=128 else 0)
  cell.putalpha(alpha); cells.append(cell)
 # Consistent scale within each sheet. Reference body height 57px.
 boxes=[c.getbbox() for c in cells]
 baseh=np.median([b[3]-b[1] for b in boxes[:4] if b])
 scale=57/baseh
 defs=social if direction=='social' else combat
 offset=0
 for action,count,ms,loop in defs:
  name=action if direction=='social' else action+'_'+direction
  out=ROOT/'frames'/name; out.mkdir(parents=True,exist_ok=True)
  frames=[]
  for j in range(count):
   cell=cells[offset+j]; box=cell.getbbox(); frame=Image.new('RGBA',(64,64))
   if box and not(action=='death' and j==5):
    crop=cell.crop(box)
    w,h=max(1,round(crop.width*scale)),max(1,round(crop.height*scale))
    if w>60 or h>60:
     s=min(60/w,60/h); w,h=round(w*s),round(h*s)
    crop=crop.resize((w,h),Image.Resampling.NEAREST)
    arr=np.array(crop); rgb=arr[:,:,:3].astype(np.int32)
    idx=((rgb[:,:,None,:]-PAL[None,None,:,:])**2).sum(3).argmin(2)
    arr[:,:,:3]=PAL[idx]; arr[:,:,3]=np.where(arr[:,:,3]>=128,255,0)
    crop=Image.fromarray(arr)
    frame.alpha_composite(crop,(30-w//2,60-h))
   frame.save(out/f'{j:02}.png'); frames.append(frame); allframes.append((name,j,frame))
  strip=Image.new('RGBA',(64*count,64))
  for j,f in enumerate(frames):strip.alpha_composite(f,(64*j,0))
  (ROOT/'sheets').mkdir(exist_ok=True); strip.save(ROOT/'sheets'/f'{name}_f{count}.png')
  (ROOT/'previews').mkdir(exist_ok=True)
  gifs=[]
  for f in frames:
   bg=Image.new('RGBA',(64,64),'#19202b'); bg.alpha_composite(f)
   gifs.append(bg.convert('RGB').resize((256,256),Image.Resampling.NEAREST))
  gifs[0].save(ROOT/'previews'/f'{name}.gif',save_all=True,append_images=gifs[1:],duration=ms,loop=0,disposal=2)
  manifest['clips'][name]={'count':count,'duration_ms':ms,'loop':loop,'pivot':[30,60],'sheet':f'sheets/{name}_f{count}.png','source_cells':[offset+1,offset+count]}
  offset+=count
assert len(allframes)==147,len(allframes)
atlas=Image.new('RGBA',(64*12,64*13))
for i,(name,j,f) in enumerate(allframes):
 atlas.alpha_composite(f,(i%12*64,i//12*64))
atlas.save(ROOT/'body_64x64_f147.png')
(ROOT/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2))
preview=Image.new('RGB',(768,13*150),'#19202b'); draw=ImageDraw.Draw(preview)
for i,(name,j,f) in enumerate(allframes):
 x,y=i%12*64,i//12*150
 preview.paste(f,(x,y+20),f)
 draw.text((x+2,y+90),str(i+1),fill='white')
atlasbg=Image.new('RGBA',atlas.size,'#19202b');atlasbg.alpha_composite(atlas);atlasbg.resize((1536,1664),Image.Resampling.NEAREST).convert('RGB').save(ROOT/'overview.png')
html=['<!doctype html><meta charset="utf-8"><title>그믐 147프레임</title><style>body{background:#19202b;color:#eee;font-family:sans-serif}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:16px}img{width:192px;image-rendering:pixelated}article{background:#222c3b;padding:16px}a{color:#dab0fa}</style><h1>그믐 · 64×64 · 147프레임</h1><p>생성 이미지에서 정규화한 스프라이트. 부착점과 게임 내 재생은 추가 검수가 필요합니다.</p><main>']
for name,clip in manifest['clips'].items():html.append(f'<article><h3>{name}</h3><img src="previews/{name}.gif"><p>{clip["count"]} frames · {clip["duration_ms"]} ms</p><a href="{clip["sheet"]}">PNG sheet</a></article>')
html.append('</main>');(ROOT/'index.html').write_text('\n'.join(html))
for name,j,f in allframes:
 assert f.size==(64,64)
 assert set(f.getchannel('A').getdata())<={0,255}
 assert len({p[:3] for p in f.getdata() if p[3]})<=16
print('Verified',len(allframes),'64x64 frames,',len(manifest['clips']),'clips, binary alpha, fixed 16-color palette')
