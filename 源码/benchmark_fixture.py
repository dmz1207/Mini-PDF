import os,math,random,sys
from reportlab.pdfgen import canvas
from reportlab.lib.utils import ImageReader
from PIL import Image,ImageDraw
root=sys.argv[1]
os.makedirs(root,exist_ok=True)
r=random.Random(1729)
im=Image.new('RGB',(1600,1200),'white');d=ImageDraw.Draw(im)
for i in range(12000):
 x=r.randrange(1600);y=r.randrange(1200);d.line((x,y,x+r.randrange(-60,60),y+r.randrange(-60,60)),fill=(r.randrange(256),r.randrange(256),r.randrange(256)),width=2)
c=canvas.Canvas(os.path.join(root,'bench.pdf'),pagesize=(595.276,841.89),pageCompression=1)
for page in range(12):
 c.setFont('Helvetica',17);c.drawString(30,805,'Mini PDF rendering benchmark '+str(page+1))
 c.drawImage(ImageReader(im),30,390,width=535,height=400)
 for y in range(55,380,13):
  c.setFont('Helvetica',9);c.drawString(30,y,'Text, equations, images, vector strokes: ABCDEFG 0123456789 '+str(y))
 for i in range(600):
  c.setStrokeColorRGB((i%7)/7,(i%13)/13,(i%23)/23);c.setLineWidth(.25);x=i%100*5.2+30
  c.line(x,390,x+math.sin(i)*25,600+i%170)
 c.showPage()
c.save()
c=canvas.Canvas(os.path.join(root,'bench-large.pdf'),pageCompression=1)
for page in range(1000):c.drawString(72,720,'Page '+str(page+1));c.showPage()
c.save()
