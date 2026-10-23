import io,sys
from pathlib import Path
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.cidfonts import UnicodeCIDFont
from reportlab.lib.utils import ImageReader
from PIL import Image,ImageDraw

def word_fixtures(root):
 b=io.BytesIO();c=canvas.Canvas(b,pagesize=(600,800))
 c.saveState();c.translate(40,748);c.scale(12,12);c.setFont('Times-Roman',1);c.drawString(0,0,'Matrix-scaled twelve point text');c.restoreState()
 c.setFont('Times-BoldItalic',12);c.drawString(40,715,'Bold italic stays editable')
 c.setFont('STSong-Light',12);c.drawString(40,685,'中文正文保持可编辑')
 c.setFont('Times-Italic',12);c.drawString(60,600,'x')
 c.setFont('Times-Italic',8);c.drawString(66,597,'i')
 c.setFont('Times-Roman',8);c.drawString(66,606,'2')
 c.setFont('Times-Roman',12);c.drawString(79,600,'=')
 c.setFont('Times-Italic',8);c.drawString(96,606,'a+b');c.drawString(101,591,'c')
 c.setLineWidth(.4);c.line(94,602,114,602)
 c.setFont('Times-Roman',12);c.drawString(60,555,'a=b+c')
 c.setFont('Symbol',12)
 for x,y,t in [(60,505,'α'),(74,505,'β'),(60,491,'γ'),(74,491,'δ')]:c.drawString(x,y,t)
 c.saveState();text=c.beginText(40,450);text.setFont('Times-Roman',12);text.setTextRenderMode(3);text.textOut('HIDDEN VALUE MUST NOT EXPORT');c.drawText(text);c.restoreState()
 c.setFont('Times-Roman',12);c.drawString(40,415,'COVERED VALUE MUST NOT EXPORT');c.setFillColorRGB(0,0,0);c.rect(38,411,300,18,stroke=0,fill=1)
 c.setFillColorRGB(.1,.4,.7);c.circle(80,320,25,stroke=0,fill=1);c.showPage()
 c.setFont('Times-Roman',8)
 for i in range(12):
  for start,text in [(36,f'LEFT{i:02d}: this column must be read before the right column.'),(316,f'RIGHT{i:02d}: this column must follow all the left column.')]:
   x=start
   for word in text.split():
    c.drawString(x,740-i*11,word+' ');x+=pdfmetrics.stringWidth(word+' ','Times-Roman',8)
 c.showPage()
 scan=Image.new('RGB',(240,320),'white');draw=ImageDraw.Draw(scan);draw.rectangle((10,10,230,310),outline='black',width=2);draw.text((20,60),'Raster-only fixture',fill='black');c.drawImage(ImageReader(scan),60,80,width=480,height=640);c.showPage()
 c.setPageRotation(90);c.setFont('Times-Roman',12);c.drawString(60,710,'Rotated page keeps its appearance');c.showPage();c.save()
 (root/'word-layout-source.pdf').write_bytes(b.getvalue())

def research_fixtures(root):
 b=io.BytesIO();c=canvas.Canvas(b,pagesize=(600,800))
 c.setFont('Times-Roman',8)
 for i in range(12):
  c.drawString(36,740-i*13,f'RESEARCH-LEFT-{i:02d} ordinary editable scientific paragraph.')
  c.drawString(316,735.7-i*13,f'RESEARCH-RIGHT-{i:02d} ordinary editable scientific paragraph.')
 c.setFont('Symbol',12);c.drawString(60,525,'α');c.setFont('Times-Italic',8);c.drawString(68,522,'i');c.setFont('Times-Roman',8);c.drawString(68,531,'2');c.setFont('Times-Roman',12);c.drawString(81,525,'=');c.setFont('Symbol',12);c.drawString(96,525,'β');c.setFont('Times-Roman',12);c.drawString(108,525,'+');c.setFont('Symbol',12);c.drawString(120,525,'γ')
 c.setFont('Symbol',18);c.drawString(60,465,'∑');c.setFont('Times-Italic',12);c.drawString(84,465,'x');c.setFont('Times-Italic',8);c.drawString(91,462,'i');c.setFont('Times-Roman',12);c.drawString(106,465,'= 0');c.drawString(245,465,'(17)')
 c.setStrokeColorRGB(.1,.4,.7);c.setLineWidth(1);c.rect(325,360,220,130,stroke=1,fill=0);c.line(330,365,530,475);c.setFont('Helvetica',8);c.drawString(338,476,'Plot label must stay with figure');c.setStrokeColorRGB(0,0,0)
 c.setFont('Times-Roman',9);c.drawString(36,310,'RESEARCH-LEFT-END');c.drawString(316,305.7,'RESEARCH-RIGHT-END');c.showPage()
 c.setFont('Times-Roman',10)
 for i in range(12):c.drawString(36,740-i*14,'FULL-WIDTH '+str(i)+' ordinary editable paragraph spanning the middle of the paper without a column gutter.')
 c.showPage();c.save();(root/'word-research-source.pdf').write_bytes(b.getvalue())

def fixtures(root):
 root.mkdir(parents=True,exist_ok=True)
 pdfmetrics.registerFont(UnicodeCIDFont('STSong-Light'))
 word_fixtures(root)
 research_fixtures(root)
 b=io.BytesIO();c=canvas.Canvas(b,pagesize=(600,800));c.setFont('Times-Roman',20);c.drawString(45,750,'Mini PDF export sample')
 c.setFont('STSong-Light',14);c.drawString(45,714,'可编辑文字与表格')
 c.setFont('Helvetica',12)
 for y,row in [(660,['Item','Qty','Price','Note']),(635,['Apple','2','12.50','=2+2']),(610,['Orange','15','3.25','00123']),(585,['Lemon','3','7.5','A&B <C>'])]:
  for x,value in zip([45,260,370,460],row):c.drawString(x,y,value)
 image=Image.new('RGB',(120,80),'white');draw=ImageDraw.Draw(image);draw.rectangle((5,5,115,75),fill='#1588be');draw.ellipse((30,10,90,70),fill='#f3b527');c.drawImage(ImageReader(image),45,430,width=120,height=80)
 c.showPage();scan=Image.new('RGB',(400,600),'white');draw=ImageDraw.Draw(scan);draw.rectangle((20,20,380,580),outline='black',width=3);draw.text((45,100),'Image only - no OCR',fill='black');c.drawImage(ImageReader(scan),30,40,width=540,height=720);c.showPage();c.save();(root/'office-source.pdf').write_bytes(b.getvalue())
 if not (root/'form.pdf').exists():
  b=io.BytesIO();c=canvas.Canvas(b,pagesize=(400,600));c.drawString(45,500,'FORM FIELD MUST SURVIVE SIGNING');c.acroForm.textfield(name='customer',tooltip='Customer',x=45,y=400,width=230,height=30,value='PRESERVED VALUE');c.showPage();c.save();(root/'form.pdf').write_bytes(b.getvalue())
 from pypdf import PdfReader,PdfWriter
 from pypdf.generic import NameObject,DictionaryObject,ArrayObject,DecodedStreamObject
 writer=PdfWriter(clone_from=PdfReader(root/'word-research-source.pdf'))
 for page in writer.pages:
  for ref in page['/Resources']['/Font'].values():
   font=ref.get_object()
   if str(font.get('/BaseFont',''))=='/Times-Roman':font[NameObject('/BaseFont')]=NameObject('/CMR10')
 writer.write(root/'word-latex-source.pdf')
 writer=PdfWriter(clone_from=PdfReader(root/'office-source.pdf'));writer.encrypt('reader',owner_password='owner');writer.write(root/'forms-encrypted.pdf')
 writer=PdfWriter(clone_from=PdfReader(root/'office-source.pdf'));stream=DecodedStreamObject();stream.set_data(b'<xdp:xdp xmlns:xdp="http://ns.adobe.com/xdp/"><template/></xdp:xdp>');writer._root_object[NameObject('/AcroForm')]=DictionaryObject({NameObject('/Fields'):ArrayObject(),NameObject('/XFA'):writer._add_object(stream)});writer.write(root/'forms-xfa.pdf')
 print('Office, encryption and XFA fixtures generated.')
 
def verify(root):
 import json,zipfile,xml.etree.ElementTree as E
 from pypdf import PdfReader
 import fitz
 checks=[]
 def check(ok,label):
  if not ok:raise AssertionError(label)
  checks.append(label)
 W='{http://schemas.openxmlformats.org/wordprocessingml/2006/main}'
 S='{http://schemas.openxmlformats.org/spreadsheetml/2006/main}'
 for name in ('office-result.docx','office-text-only.docx','office-result.xlsx','forms-export.docx','forms-export.xlsx'):
  with zipfile.ZipFile(root/name) as z:
   check(z.testzip() is None,name+': package CRCs are valid')
   for item in z.namelist():
    if item.endswith(('.xml','.rels')):E.fromstring(z.read(item))
   check(True,name+': all XML parts parse independently')
 with zipfile.ZipFile(root/'office-result.docx') as z:
  d=E.fromstring(z.read('word/document.xml'));text=''.join(d.itertext())
  check('可编辑文字与表格' in text and 'Apple' in text and '00123' in text,'Word: editable Unicode, table text and IDs survive')
  check(len(d.findall('.//'+W+'tbl'))==1,'Word: aligned rows become an actual editable table')
  check(len(d.findall('.//'+W+'tr'))==4 and len(d.findall('.//'+W+'tc'))==16,'Word: table has four rows and four columns')
  check(sum(n.startswith('word/media/') for n in z.namelist())==2,'Word: illustration and unrecognized scan remain images')
  for e in d.findall('.//'+W+'pgSz'):check(e.attrib[W+'w'].isdigit() and e.attrib[W+'h'].isdigit(),'Word: section dimensions use valid integer twips')
 with zipfile.ZipFile(root/'office-text-only.docx') as z:check(not any(n.startswith('word/media/') for n in z.namelist()),'Word: image exclusion is honored')
 with zipfile.ZipFile(root/'office-result.xlsx') as z:
  d=E.fromstring(z.read('xl/worksheets/sheet1.xml'));cells={c.attrib['r']:c for c in d.findall('.//'+S+'c')}
  check(cells['B4'].find(S+'v').text=='2' and cells['C4'].find(S+'v').text=='12.50','Excel: numeric table cells retain source values')
  check(cells['D4'].attrib['t']=='inlineStr' and ''.join(cells['D4'].itertext())=='=2+2' and cells['D4'].find(S+'f') is None,'Excel: formula-looking PDF text stays literal text')
  check(cells['D5'].attrib['t']=='inlineStr' and ''.join(cells['D5'].itertext())=='00123','Excel: leading-zero ID remains text')
  check(not E.fromstring(z.read('xl/worksheets/sheet2.xml')).findall('.//'+S+'c'),'Excel: an image-only page produces no invented OCR values')
 M='{http://schemas.openxmlformats.org/officeDocument/2006/math}'
 V='{urn:schemas-microsoft-com:vml}'
 WP='{http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing}'
 with zipfile.ZipFile(root/'word-layout.docx') as z:
  check(z.testzip() is None,'Word layout: all ZIP checksums pass')
  for item in z.namelist():
   if item.endswith(('.xml','.rels')):E.fromstring(z.read(item))
  d=E.fromstring(z.read('word/document.xml'));text=''.join(d.itertext())
  check('Matrix-scaled twelve point text' in text and '中文正文保持可编辑' in text,'Word layout: editable source text survives independently')
  check('HIDDEN VALUE' not in text and 'COVERED VALUE' not in text,'Word layout: hidden and covered source text is not leaked')
  check(len(d.findall('.//'+W+'sectPr'))==5,'Word layout: selected pages and repeated first page have five explicit sections')
  check(not d.findall('.//'+M+'oMath'),'Word layout: formula conversion is opt-in')
  check(len(d.findall('.//'+WP+'anchor'))>=5,'Word layout: artwork uses positioned page anchors')
  check(all(float(s.attrib.get('style','').split('margin-left:')[1].split('pt')[0])>=0 for s in d.findall('.//'+V+'shape')),'Word layout: editable text uses nonnegative page coordinates')
  normal=d.find('.//'+W+'rPr/'+W+'sz');check(normal is not None,'Word layout: explicit font sizes exist')
  settings=E.fromstring(z.read('word/settings.xml'));check(settings.find(W+'doNotAutoCompressPictures') is not None,'Word layout: disable destructive Office image recompression')
  from PIL import Image as PILImage
  for name in ['word/media/page1.png','word/media/page3.png','word/media/page4.png','word/media/page5.png']:
   image=PILImage.open(io.BytesIO(z.read(name)));check(image.width*image.height<=16000000,name+': image memory is bounded')
  check(z.read('word/media/page1.png')==z.read('word/media/page5.png'),'Word layout: repeated page artwork is identical')
 with zipfile.ZipFile(root/'word-equations.docx') as z:
  d=E.fromstring(z.read('word/document.xml'))
  check(len(d.findall('.//'+M+'oMath'))>=2,'Word equations: real OfficeMath objects are present')
  check(d.find('.//'+M+'sSubSup') is not None and d.find('.//'+M+'f') is not None,'Word equations: sub/superscript and fraction structures are native OMML')
  fraction=d.find('.//'+M+'f');check(''.join(fraction.find(M+'num').itertext())=='a+b' and ''.join(fraction.find(M+'den').itertext())=='c','Word equations: independently confirm numerator and denominator semantics')
  script=d.find('.//'+M+'sSubSup');check(''.join(script.find(M+'sub').itertext())=='i' and ''.join(script.find(M+'sup').itertext())=='2','Word equations: independently confirm subscript and superscript semantics')
  images=[PILImage.open(io.BytesIO(z.read(n))) for n in z.namelist() if n.startswith('word/media/') and n.endswith('.png')]
  check(any(80<i.width<115 and 95<i.height<135 for i in images),'Word equations: uncertain matrix retains a high-resolution raster region')
 with zipfile.ZipFile(root/'word-continuous.docx') as z:
  check(z.testzip() is None,'Continuous Word: valid package checksums')
  d=E.fromstring(z.read('word/document.xml'))
  check(not d.findall('.//'+V+'shape'),'Continuous Word: no positioned text boxes')
  paragraphs=[''.join(p.itertext()) for p in d.findall('./'+W+'body/'+W+'p')]
  text='\n'.join(paragraphs)
  check(any('LEFT00' in p and 'LEFT05' in p for p in paragraphs),'Continuous Word: source lines merge into ordinary editable paragraphs')
  check(text.index('LEFT11')<text.index('RIGHT00'),'Continuous Word: down-column reading order is retained')
  check('中文正文保持可编辑' in text and 'HIDDEN VALUE' not in text and 'COVERED VALUE' not in text,'Continuous Word: native Unicode body with hidden/covered text excluded')
  check(d.find('.//'+M+'f') is not None and d.find('.//'+M+'sSubSup') is not None,'Continuous Word: real editable equations share the ordinary document body')
  check(len(d.findall('.//'+W+'sectPr'))==1,'Continuous Word: selected source pages form one flowing section')
 with zipfile.ZipFile(root/'word-research.docx') as z:
  check(z.testzip() is None,'Research Word: package checksums are valid')
  d=E.fromstring(z.read('word/document.xml'));text=''.join(d.itertext())
  check(text.index('RESEARCH-LEFT-END')<text.index('RESEARCH-RIGHT-00'),'Research Word: formula and figure extraction cannot interleave staggered columns')
  check(text.index('RESEARCH-RIGHT-END')<text.index('FULL-WIDTH'),'Research Word: selected source pages flow in order')
  check('Plot label' not in text and 'RESEARCH-LEFT-00' in text,'Research Word: chart labels remain artwork while ordinary scientific prose stays editable')
  math=d.findall('.//'+M+'oMath');check(any('α' in ''.join(m.itertext()) and m.find('.//'+M+'sSubSup') is not None for m in math),'Research Word: Greek equation has real structured OfficeMath')
  check(all('∑' not in ''.join(m.itertext()) for m in math),'Research Word: unsupported large operator is not guessed as editable math')
  extents=[(int(e.attrib['cx'])/12700,int(e.attrib['cy'])/12700) for e in d.iter(WP+'extent')]
  check(any(180<w<230 and 10<h<50 for w,h in extents),'Research Word: complex equation and number form one physical crop')
  check(any(220<w<250 and 130<h<160 for w,h in extents),'Research Word: full figure retains its physical dimensions')
  check(len(d.findall('.//'+W+'sectPr'))==1 and not d.findall('.//'+V+'shape'),'Research Word: native article uses one continuous section without positioned text boxes')
 with zipfile.ZipFile(root/'word-latex.docx') as z:
  d=E.fromstring(z.read('word/document.xml'));text=''.join(d.itertext())
  check('RESEARCH-LEFT-00' in text and 'RESEARCH-RIGHT-END' in text and 'FULL-WIDTH' in text,'Research Word: Computer Modern Roman body stays editable across columns and pages')
  check(text.index('RESEARCH-LEFT-END')<text.index('RESEARCH-RIGHT-00'),'Research Word: Computer Modern prose retains column order')
  check(not d.findall('.//'+M+'oMath') and not d.findall('.//'+V+'shape'),'Research Word: formula conversion stays opt-in in continuous LaTeX prose')
 with zipfile.ZipFile(root/'word-columns.docx') as z:
  text=''.join(E.fromstring(z.read('word/document.xml')).itertext());check(text.index('LEFT11')<text.index('RIGHT00'),'Word columns: flowing text does not interleave the two columns')
 created=PdfReader(root/'forms-created.pdf');filled=PdfReader(root/'forms-filled.pdf');managed=PdfReader(root/'forms-managed.pdf');f=created.get_fields();g=filled.get_fields();m=managed.get_fields()
 has_cjk=not (root/'forms-cjk-skipped.txt').exists();first='张三 Alice' if has_cjk else 'Alice';second='李四 Bob' if has_cjk else 'Bob'
 check(f['Group.Name']['/V']==first and g['Group.Name']['/V']==second,'forms: independent parser confirms edited field values')
 check(str(f['Agree']['/V'])=='/Yes' and str(g['Agree']['/V'])=='/Off','forms: checkbox stores and updates the real field value')
 check(str(f['Choice']['/V'])=='/B' and len(f['Choice']['/Kids'])==2,'forms: radio widgets share a group with one selected state')
 check(f['Country']['/Opt'][0][0]=='CN' and g['Country']['/V']=='US','forms: dropdown exports values independently of labels')
 check(list(f['Colors']['/V'])==['Red','Blue'],'forms: multiselect stores an array')
 check(f['Locked']['/V']==g['Locked']['/V']=='Read only','forms: import preserves read-only fields')
 check('Private' not in m and m['Copy.Name']['/V']==second,'forms: delete and duplicate persist independently')
 for name in ('forms-created.pdf','forms-filled.pdf','forms-managed.pdf','forms-merged.pdf','forms-duplicated.pdf','forms-extracted.pdf','forms-inserted.pdf'):
  r=PdfReader(root/name)
  for p in r.pages:
   for ref in p.get('/Annots',[]):
    w=ref.get_object()
    if w.get('/Subtype')=='/Widget':
     parent=w.get('/Parent');check(parent is not None and '/FT' in parent.get_object(),name+': widget has a valid terminal field parent')
     check('/AP' in w and '/N' in w['/AP'],name+': widget has a saved appearance')
     check('/MiniPDFImportField' not in w and '/MiniPDFImportDR' not in w,name+': no private import tags remain')
 with fitz.open(root/'office-source.pdf') as a,fitz.open(root/'forms-created.pdf') as b:
  clip=fitz.Rect(0,0,600,420);check(a[0].get_pixmap(clip=clip).samples==b[0].get_pixmap(clip=clip).samples,'forms: unrelated original text and image pixels remain unchanged')
 for name,indices in [('forms-merged.pdf',[0,1]),('forms-duplicated.pdf',[0,0,1]),('forms-extracted.pdf',[0])]:
  with fitz.open(root/'forms-created.pdf') as a,fitz.open(root/name) as b:
   check(len(b)==len(indices),name+': correct page count')
   for i,source in enumerate(indices):check(a[source].get_pixmap().samples==b[i].get_pixmap().samples,name+': form pixels preserve the selected source page')
 for name,part,ns in [('forms-export.docx','word/document.xml',W),('forms-export.xlsx','xl/worksheets/sheet1.xml',S)]:
  with zipfile.ZipFile(root/name) as z:
   text=''.join(E.fromstring(z.read(part)).itertext());check(second in text or (name.endswith('xlsx') and 'First line' in text),'forms: filled values are included in '+name);check('Secret123' not in text,'forms: password values are masked in '+name)
 data=E.fromstring((root/'forms-data.xfdf').read_text(encoding='utf-8'));values={e.attrib['name']:e for e in data.iter() if e.tag.endswith('field')};check('Group.Name' in values and 'Private' not in values,'XFDF: standard namespaced data respects NoExport')
 report={'independent_checks':len(checks),'passed':checks,'system_cjk_appearance_skipped':not has_cjk};(root/'office-forms-independent.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8');print(json.dumps({'office_form_checks':len(checks),'failures':0,'cjk_skipped':not has_cjk}))

if __name__=='__main__':
 (fixtures if sys.argv[1]=='fixtures' else verify)(Path(sys.argv[2]))
