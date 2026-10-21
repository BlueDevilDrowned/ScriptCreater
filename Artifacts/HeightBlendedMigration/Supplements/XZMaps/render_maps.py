from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import csv,json,hashlib
root=Path.cwd(); out=root/'Artifacts/HeightBlendedMigration'; sup=out/'Supplements/XZMaps'
rows=list(csv.DictReader((sup/'samples.csv').open(encoding='utf-8-sig',newline='')))
assert len(rows)==256*256
font='C:/Windows/Fonts/msyh.ttc'
def ft(s):return ImageFont.truetype(font,s)
low=(26,75,154); mid=(239,242,238); high=(201,70,43)
def palette(t):
 t=max(0,min(1,t)); a,b,k=(low,mid,t*2) if t<=.5 else (mid,high,t*2-1)
 return tuple(round(x+(y-x)*k) for x,y in zip(a,b))
def render(name,third):
 image=Image.new('RGB',(1460,760),(250,250,248)); d=ImageDraw.Draw(image)
 d.text((40,24),'XZ 世界场参数',font=ft(30),fill=(28,37,45))
 d.text((40,72),'Seed 12345 · X / Z = −512～511 体素 · 256 × 256 个实际采样点',font=ft(19),fill=(75,85,95))
 specs=[('C','C · 大陆性',-1,1),('E','E · 侵蚀度',-1,1),('P' if third=='P' else 'R','P · 峰谷（参考，未接入地形）' if third=='P' else 'R · 当前山脊 |noise|',-1 if third=='P' else 0,1)]
 for i,(field,title,vmin,vmax) in enumerate(specs):
  left=64+i*474; top=154; size=402
  d.text((left,113),title,font=ft(21),fill=(28,37,45))
  tile=Image.new('RGB',(256,256)); pixels=tile.load()
  vals=[]
  for iz in range(256):
   for ix in range(256):
    row=rows[iz*256+ix]; value=float(row[field]); vals.append(value)
    pixels[ix,255-iz]=palette((value-vmin)/(vmax-vmin))
  image.paste(tile.resize((size,size),Image.Resampling.NEAREST),(left,top))
  d.rectangle((left,top,left+size-1,top+size-1),outline=(100,110,120),width=1)
  for fraction,label in [(0,'−512'),(.5,'0'),(1,'511')]:
   xx=left+fraction*(size-1); d.line((xx,top+size,xx,top+size+5),fill=(70,80,90),width=1)
   d.text((xx,top+size+9),label,font=ft(15),fill=(65,75,85),anchor='mt')
  d.text((left+size-1,top+size+31),'+X',font=ft(15),fill=(65,75,85),anchor='rt')
  d.text((left-8,top),'511',font=ft(15),fill=(65,75,85),anchor='rt')
  d.text((left-8,top+size//2),'0',font=ft(15),fill=(65,75,85),anchor='rm')
  d.text((left-8,top+size),'−512',font=ft(15),fill=(65,75,85),anchor='rb')
  d.text((left-8,top-23),'+Z',font=ft(15),fill=(65,75,85),anchor='rt')
  bar_y=top+size+64
  for x in range(size):d.line((left+x,bar_y,left+x,bar_y+12),fill=palette(x/(size-1)))
  for fraction,value in [(0,vmin),(.5,(vmin+vmax)/2),(1,vmax)]:
   d.text((left+fraction*(size-1),bar_y+18),f'{value:g}',font=ft(16),fill=(65,75,85),anchor='mt')
  d.text((left,bar_y+45),f'采样最值：{min(vals):.3f} ～ {max(vals):.3f}',font=ft(17),fill=(65,75,85))
 d.text((40,719),'固定色阶：蓝色为低值，浅色为中值，橙色为高值；三张图使用同一组 XZ 坐标。',font=ft(18),fill=(65,75,85))
 file=sup/name; image.save(file)
 with Image.open(file) as im:im.verify()
 return str(file)
files=[render('xz-parameters.png','R'),render('xz-cep-reference.png','P')]
with (out/'VERIFICATION.txt').open('a',encoding='utf-8') as f:
 f.write('XZ_MAPS_RENDER: Python Pillow; source Supplements/XZMaps/samples.csv, 65536 C# sampled rows, seed=12345; X/Z [-512,511]; +Z up, +X right; fixed scales C/E/P [-1,1], R [0,1]\n')
 for file in files:
  p=Path(file); f.write(str(p)+' SHA256 '+hashlib.sha256(p.read_bytes()).hexdigest()+'; image verify=PASS; size=1460x760\n')
print(json.dumps(files,ensure_ascii=False))
