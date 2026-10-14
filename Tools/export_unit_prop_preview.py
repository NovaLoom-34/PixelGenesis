"""Export code-authored prop pixels and hand anchors for the browser preview; check all anchors."""
import base64
import json
import re
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
src = (ROOT / 'Assets/_Project/Render/UnitPropAtlas.cs').read_text(encoding='utf-8')
motion = (ROOT / 'Assets/_Project/Render/UnitPropMotion.cs').read_text(encoding='utf-8')
constants = dict(Outline=28, Body=84, BodyLight=112, Wood=140, Gold=196, Steel=224)
props = {}
for name, block in re.findall(r'case UnitProp\.(\w+):([\s\S]*?)break;', src):
    pixels = [0] * 256
    def put(x, y, color):
        if 0 <= x < 16 and 0 <= y < 16:
            pixels[(15-y)*16+x] = color // 28
    def rect(x,y,w,h,c):
        for yy in range(y,y+h):
            for xx in range(x,x+w): put(xx,yy,c)
    def stroke(x,y,x1,y1,c):
        dx,sx=abs(x1-x),(1 if x<x1 else -1)
        dy,sy=-abs(y1-y),(1 if y<y1 else -1)
        error=dx+dy
        while True:
            put(x,y,28);put(x+1,y,28);put(x,y+1,28);put(x,y,c)
            if x==x1 and y==y1: break
            twice=2*error
            if twice>=dy: error+=dy;x+=sx
            if twice<=dx: error+=dx;y+=sy
    for op, args in re.findall(r'\b(Stroke|Rect|Set)\(pixels,\s*([^;]+?)\);',block):
        values=[int(v.strip()) if v.strip().isdigit() else constants[v.strip()] for v in args.split(',')]
        {'Stroke':stroke,'Rect':rect,'Set':put}[op](*values)
    props[name]={'pixels':pixels,'grip':[6.5,10.5]}
pending=[]
grip_block=motion.split('public static Vector2 Grip')[1].split('public static float Angle')[0]
for line in grip_block.splitlines():
    pending.extend(re.findall(r'case UnitProp\.(\w+):',line))
    match=re.search(r'pixel = new Vector2\((\d+),(\d+)\)',line)
    if match:
        for name in pending: props[name]['grip']=[int(match[1])+.5,15.5-int(match[2])]
        pending=[]
hands={}
for name, points in re.findall(r'Vector2\[\] (\w+) = \{(.*?)\};',motion):
    hands[name]=[[int(x)+.5,int(y)+.5] for x,y in re.findall(r'new Vector2\((\d+),(\d+)\)',points)]
checked=0
for key,points in hands.items():
    name=key.replace('Female','_female').lower()
    base=ROOT/'Art/Final/units'/f'{name}_detail'
    layout=json.loads(base.with_suffix('.json').read_text())
    image=Image.open(base.with_suffix('.png')).convert('RGBA')
    for point,frame in zip(points,[0,1,4,5,6,7]):
        f=layout['frames'][frame];w=round(f['width']*layout['scale']);h=round(f['height']*layout['scale'])
        x=int(point[0])-(40-w)//2;y=int(point[1])-(layout['baseline']-h)
        mask=base64.b64decode(f['mask'])
        assert 0<=x<w and 0<=y<h and mask[y*w+x],f'{name} frame {frame}: grip is not on the character'
        checked+=1
out=ROOT/'Docs/previews/unit-props.json'
out.write_text(json.dumps(dict(props=props,hands=hands),separators=(',',':'))+'\n',encoding='utf-8')
print(f'PASS: {checked} hand anchors lie on visible source poses. Exported {len(props)} props.')
