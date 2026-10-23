"""Developer-only source fixtures and independent persisted text-edit checks."""
import io,json,sys
from pathlib import Path
sys.dont_write_bytecode=True

def fixtures(root):
 from reportlab.pdfgen import canvas
 from reportlab.pdfbase import pdfmetrics
 from reportlab.pdfbase.ttfonts import TTFont
 from reportlab.pdfbase.cidfonts import UnicodeCIDFont
 from pypdf import PdfReader,PdfWriter
 from pypdf.generic import DecodedStreamObject,NameObject
 root.mkdir(parents=True,exist_ok=True)
 def save(name,draw):
  b=io.BytesIO();c=canvas.Canvas(b,pagesize=(600,800));draw(c);c.showPage();c.save();(root/name).write_bytes(b.getvalue())
 def basic(c):
  c.setFillColorRGB(.12,.3,.6);c.rect(460,40,80,70,fill=1,stroke=0)
  c.setFillColorRGB(0,0,0);c.setFont('Times-Roman',20)
  c.saveState();tx=c.beginText(45,710);tx.setCharSpace(1.6);tx.textOut('TRACKED ABC 123 ABC');c.drawText(tx);c.restoreState()
  c.setFont('Times-Roman',16);c.drawString(45,650,'Original serif text 1234')
  c.setFont('Helvetica',12);c.drawString(45,585,'Fragment');c.line(590,790,591,790);c.drawString(45+pdfmetrics.stringWidth('Fragment','Helvetica',12)+4,585,'line')
  c.drawString(340,585,'OTHER COLUMN 1234567890')
  for y,text in [(520,'Paragraph line one 0123456789'),(502,'Paragraph line two ABCDEFGHIJ'),(484,'Paragraph line three KLMNOPQRST'),(464,'FORMULA KEEP 123')]:c.drawString(45,y,text)
  c.setFont('Times-Roman',11);c.drawString(350,35,'UNCHANGED FOOTER')
 save('text-source-basic.pdf',basic)
 pdfmetrics.registerFont(TTFont('TextEditSerif',r'C:/Windows/Fonts/times.ttf'))
 def embedded(c):
  c.setFont('TextEditSerif',17);c.drawString(45,700,'Embedded original 1234');c.drawString(45,660,'abcdefghijklmnopqrstuvwxyz ABCDEFGHIJKLMNOPQRSTUVWXYZ 0123456789')
 save('text-source-embedded.pdf',embedded)
 pdfmetrics.registerFont(UnicodeCIDFont('STSong-Light'))
 def chinese(c):
  c.setFont('STSong-Light',18);c.drawString(45,700,'原始中文编辑测试文字和高亮')
 save('text-source-chinese.pdf',chinese)
 def clipped(c):
  c.saveState();path=c.beginPath();path.rect(45,650,130,80);c.clipPath(path,stroke=0,fill=0)
  c.setFont('Times-Roman',20);c.drawString(45,690,'Clipped original abc123');c.restoreState()
 save('text-source-clipped.pdf',clipped)
 def rotated(c):
  c.saveState();c.translate(300,300);c.rotate(90);c.setFont('Times-Italic',18);c.drawString(0,0,'Rotated original 123');c.restoreState()
 save('text-source-rotated.pdf',rotated)
 def stroked(c,mode):
  c.setFillColorRGB(0,0,1);c.setStrokeColorRGB(.8,.1,.1);c.setFillAlpha(.8);c.setStrokeAlpha(.4);c.setLineWidth(.7)
  tx=c.beginText(45,700);tx.setFont('Times-Roman',22);tx.setTextRenderMode(mode);tx.textOut('Stroke original 123');c.drawText(tx)
 save('text-source-stroke.pdf',lambda c:stroked(c,1))
 save('text-source-dual-color.pdf',lambda c:stroked(c,2))
 def hidden(c):
  c.setFillColorRGB(.8,.9,1);c.rect(30,650,400,100,fill=1,stroke=0)
  tx=c.beginText(45,700);tx.setFont('Helvetica',18);tx.setTextRenderMode(3);tx.textOut('Hidden original 123');c.drawText(tx)
 save('text-source-hidden.pdf',hidden)
 reader=PdfReader(root/'text-source-embedded.pdf');writer=PdfWriter(clone_from=reader);page=writer.pages[0]
 stream=DecodedStreamObject();stream.set_data(b'/Span << /MCID 7 >> BDC\n'+page.get_contents().get_data()+b'\nEMC')
 page[NameObject('/Contents')]=writer._add_object(stream)
 with (root/'text-source-marked.pdf').open('wb') as f:writer.write(f)
 reader=PdfReader(root/'text-source-embedded.pdf');writer=PdfWriter(clone_from=reader);page=writer.pages[0]
 stream=DecodedStreamObject();stream.set_data(b'/Span << /ActualText (ACCESSIBLE ALTERNATIVE) >> BDC\n'+page.get_contents().get_data()+b'\nEMC');page[NameObject('/Contents')]=writer._add_object(stream)
 with (root/'text-source-actual-text.pdf').open('wb') as f:writer.write(f)
 reader=PdfReader(root/'text-source-basic.pdf');writer=PdfWriter(clone_from=reader);writer.pages[0].rotate(90)
 with (root/'text-source-page-rotation.pdf').open('wb') as f:writer.write(f)
 print(json.dumps({'text_fixture_files':11}))

def verify(root):
 import fitz
 from pypdf import PdfReader
 checks=[];skipped=[]
 def check(value,name):
  if not value:raise AssertionError(name)
  checks.append(name)
 for label in ['basic','embedded','chinese','clipped','rotated','hidden','marked','page-rotation','stroke']:
  with fitz.open(root/f'text-source-{label}.pdf') as before,fitz.open(root/f'text-edited-{label}.pdf') as after:
   check(after.page_count==before.page_count,label+': page count survives')
   expected={'basic':'TRACKED ABC 321 ABC','embedded':'Embedded revised 4321','chinese':'原始中文测试文字和编辑','clipped':'Clipped revised abc321','rotated':'Rotated revised 321','hidden':'Hidden revised 321','marked':'Embedded revised 4321','page-rotation':'TRACKED ABC 321 ABC','stroke':'Stroke revised 321'}[label]
   extracted=PdfReader(root/f'text-edited-{label}.pdf').pages[0].extract_text() if label=='clipped' else after[0].get_text()
   check(expected in extracted,label+': independent text extraction sees replacement')
   if label=='clipped':
    a=before[0].get_pixmap(matrix=fitz.Matrix(2,2),clip=fitz.Rect(176,0,600,800),alpha=False);b=after[0].get_pixmap(matrix=fitz.Matrix(2,2),clip=fitz.Rect(176,0,600,800),alpha=False)
    check(a.samples==b.samples,'clipped: modified text cannot paint outside the original clipping edge')
   if label in ('basic','page-rotation','hidden'):
    a=before[0].get_pixmap(matrix=fitz.Matrix(2,2),alpha=False);b=after[0].get_pixmap(matrix=fitz.Matrix(2,2),alpha=False)
    if label=='hidden':check(a.samples==b.samples,'hidden: image remains pixel-identical')
    else:
     # The target is the first baseline only; all other regions must remain identical.
     clip=fitz.Rect(0,140,600,800) if label=='basic' else fitz.Rect(0,0,640,600)
     a=before[0].get_pixmap(matrix=fitz.Matrix(2,2),clip=clip,alpha=False);b=after[0].get_pixmap(matrix=fitz.Matrix(2,2),clip=clip,alpha=False)
     check(a.samples==b.samples,label+': unrelated page content remains pixel-identical')
   if label=='embedded':
    def programs(path):
     fonts=PdfReader(path).pages[0]['/Resources']['/Font'].get_object();result=[]
     for ref in fonts.values():
      font=ref.get_object();desc=font.get('/FontDescriptor');
      if desc:
       desc=desc.get_object()
       for key in ('/FontFile','/FontFile2','/FontFile3'):
        if key in desc:result.append(desc[key].get_object().get_data())
     return result
    check(programs(root/'text-source-embedded.pdf')==programs(root/'text-edited-embedded.pdf'),'embedded: font program bytes stay unchanged')
   if label=='marked':check(b'/MCID 7' in PdfReader(root/'text-edited-marked.pdf').pages[0].get_contents().get_data(),'marked: marked-content identity survives')
 if (root/'text-edited-hidden-font.pdf').exists():
  with fitz.open(root/'text-source-hidden.pdf') as a,fitz.open(root/'text-edited-hidden-font.pdf') as b:
   check('隐藏文字更新' in b[0].get_text(),'hidden font: replacement Chinese text is independently extractable')
   check(a[0].get_pixmap().samples==b[0].get_pixmap().samples,'hidden font: font replacement leaves visible graphics unchanged')
  with fitz.open(root/'text-edited-system-font.pdf') as doc:check('原始中文新增字符' in doc[0].get_text(),'system font: saved new Chinese glyphs preserve Unicode')
 else:
  check((root/'system-cjk-skipped.txt').exists(),'missing CJK outputs have an explicit missing-font reason')
  skipped.append('System-font Chinese replacement needs Microsoft YaHei; preserved original Chinese text was still tested.')
 with fitz.open(root/'text-edited-wrap.pdf') as doc:
  text=doc[0].get_text();check('WRAPPED' in text and 'CONTENT' in text,'wrap: complete words remain readable')
  lines=[line for block in doc[0].get_text('dict')['blocks'] if block['type']==0 for line in block['lines'] if any('WRAPPED' in s['text'] or 'CONTENT' in s['text'] for s in line['spans'])]
  check(len(lines)>=2,'wrap: text actually spans multiple lines')
 with fitz.open(root/'text-edited-line.pdf') as doc:check('Joined line' in doc[0].get_text() and 'OTHER COLUMN' in doc[0].get_text(),'line: selected fragments merge and neighboring column survives')
 report={'independent_checks':len(checks),'passed':checks,'skipped':skipped};(root/'text-edit-independent.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({'independent_text_checks':len(checks),'failures':0,'skipped':len(skipped)}))

if __name__=='__main__':
 (fixtures if sys.argv[1]=='fixtures' else verify)(Path(sys.argv[2]))
