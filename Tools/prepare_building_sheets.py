"""Extract metadata and sampled alpha masks, without changing authored PNG pixels."""
from pathlib import Path
import json, base64, shutil, sys
import numpy as np
from PIL import Image
ROOT=Path(__file__).resolve().parents[1]
styles=sys.argv[1:] or ('human','orc','dwarf','elf','beast','insect')
sheets={
 'architecture':('house','watchtower','smithy'),
 'settlement':('hall_1','granary','market'),
 'civic':('hall_2','hall_3','storage'),
 'industry':('farm_shed','mine','lumber_camp'),
 'food':('bakery','pasture','beehive'),
 'founding':('bonfire','tent','hut'),
 'publicworks':('well','windmill','inn'),
 'advanced':('manor','stable','barracks'),
 'naval':('fishing_hut','docks','shipyard'),
 'culture':('library','school','temple'),
 'religion':('grand_temple','healers_house','graveyard'),
 'monuments':('statue_god','monument','pyre'),
 'fortifications':('wall','gate'),
 'habitats':('nest','hive','ruins'),
}
for race in styles:
 for sheet,buildings in sheets.items():
  path=ROOT/'Art/Final/buildings'/f'{race}_{sheet}.png'
  if not path.exists(): continue
  rgba=np.array(Image.open(path).convert('RGBA'))
  alpha=rgba[:,:,3]>=128
  height,width=alpha.shape
  columns=len(buildings)
  assert width>=columns*64 and height>=192,(race,sheet,width,height)
  frames=[]
  for row in range(3):
   for col in range(columns):
    x0=round(col*width/columns);x1=round((col+1)*width/columns)
    y0=round(row*height/3);y1=round((row+1)*height/3)
    ys,xs=np.nonzero(alpha[y0:y1,x0:x1])
    assert len(xs)>500,(race,sheet,row,col,len(xs))
    left=int(xs.min()+x0);top=int(ys.min()+y0)
    frames.append(dict(x=left,y=top,width=int(xs.max()-xs.min()+1),height=int(ys.max()-ys.min()+1)))
  for col in range(columns):
   group=frames[col::columns];mw=max(f['width'] for f in group);mh=max(f['height'] for f in group)
   scale=60/max(mw,mh);ow=round(mw*scale)+4;oh=round(mh*scale)+4
   for f in group:
    w=max(1,round(f['width']*scale));h=max(1,round(f['height']*scale))
    xs=f['x']+np.minimum(f['width']-1,((np.arange(w)+.5)*f['width']/w).astype(int))
    ys=f['y']+np.minimum(f['height']-1,((np.arange(h)+.5)*f['height']/h).astype(int))
    mask=alpha[np.ix_(ys,xs)].astype(np.uint8)
    assert mask.sum()>100,(race,sheet,col)
    f.update(outputWidth=ow,outputHeight=oh,sampleWidth=w,sampleHeight=h,mask=base64.b64encode(mask.tobytes()).decode('ascii'))
  layout=path.with_suffix('.json')
  layout.write_text(json.dumps(dict(columns=columns,frames=frames),indent=2),encoding='utf-8')
  dest=ROOT/'Assets/StreamingAssets/Art/Final/buildings';dest.mkdir(parents=True,exist_ok=True)
  for source in (path,layout):shutil.copy2(source,dest/source.name)
  print(f'{race}/{sheet}: {len(frames)} isolated sprites, common scale per building, masks and runtime copies OK')
