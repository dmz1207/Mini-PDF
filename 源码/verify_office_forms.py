import io,sys
from pathlib import Path
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.cidfonts import UnicodeCIDFont
from reportlab.lib.utils import ImageReader
from PIL import Image,ImageDraw
def fixtures(root):
 root.mkdir(parents=True,exist_ok=True)
 pdfmetrics.registerFont(UnicodeCIDFont('STSong-Light'))
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
