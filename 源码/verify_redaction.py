"""Independent fixtures and structure/pixel checks; never shipped with the reader."""
from pathlib import Path
import io
import json
import sys

def fixtures(root):
    from reportlab.pdfgen import canvas
    from reportlab.pdfbase import pdfmetrics
    from reportlab.pdfbase.cidfonts import UnicodeCIDFont
    from reportlab.lib.utils import ImageReader
    from PIL import Image, ImageDraw, ImageFont
    from pypdf import PdfReader, PdfWriter
    root.mkdir(parents=True,exist_ok=True)
    pdfmetrics.registerFont(UnicodeCIDFont('STSong-Light'))
    scan=Image.new('RGB',(700,240),'white')
    ImageDraw.Draw(scan).text((20,60),'SCAN_PRIVATE_84KQ',fill='black',font=ImageFont.truetype('C:/Windows/Fonts/arial.ttf',46))
    buffer=io.BytesIO(); c=canvas.Canvas(buffer,pagesize=(400,320))
    c.setFont('Helvetica',18);c.drawString(30,280,'PUBLIC ALPHA KEEP')
    c.setFont('Helvetica',12);c.drawString(30,255,'INLINE_LEFT PRIVATE_INLINE_X8R2 INLINE_RIGHT')
    c.setFont('Helvetica',20);c.drawString(30,220,'PRIVATE_9Z7Q_4836')
    text=c.beginText(30,185);text.setFont('Helvetica',16);text.setTextRenderMode(3);text.textLine('OCR_PRIVATE_5K3J');c.drawText(text)
    c.setFont('STSong-Light',16);c.drawString(30,145,'公开测试')
    c.setFont('STSong-Light',12);c.drawString(30,115,'机密甲乙丙丁')
    c.drawImage(ImageReader(scan),180,30,210,72)
    c.textAnnotation('COMMENT_PRIVATE_79TZ',Rect=(365,270,385,290))
    c.showPage();c.setPageRotation(90)
    c.beginForm('nested-secret',0,0,400,320);c.setFont('Helvetica',18);c.drawString(35,210,'NESTED_PRIVATE_46BX');c.drawString(35,265,'PUBLIC ROTATED KEEP');c.endForm();c.doForm('nested-secret');c.showPage()
    c.setPageRotation(0);c.setFont('Helvetica',18);c.drawString(45,240,'PUBLIC CROP KEEP');c.drawString(45,175,'CROP_PRIVATE_71SD');c.save()
    writer=PdfWriter(clone_from=PdfReader(io.BytesIO(buffer.getvalue())))
    writer.pages[2].cropbox.lower_left=(15,20);writer.pages[2].cropbox.upper_right=(380,300)
    writer.add_metadata({'/Title':'META_PRIVATE_38NP','/Author':'AUTHOR_PRIVATE_63VJ'})
    writer.add_attachment('private.txt',b'ATTACHMENT_PRIVATE_51LR')
    writer.add_js('var privateValue = "JAVASCRIPT_PRIVATE_74HM";')
    writer.add_outline_item('OUTLINE_PRIVATE_27CS',0)
    writer.write(root/'redaction-source.pdf')
    large=canvas.Canvas(str(root/'redaction-a4-source.pdf'),pagesize=(595.276,841.89));large.setFont('Helvetica',18);large.drawString(60,760,'PUBLIC A4 HIGH RESOLUTION');large.drawString(60,700,'A4_PRIVATE_61QX');large.save()
    (root/'redaction-private-tokens.json').write_text(json.dumps(['PRIVATE_9Z7Q_4836','PRIVATE_INLINE_X8R2','机密甲乙丙丁','OCR_PRIVATE_5K3J','SCAN_PRIVATE_84KQ','NESTED_PRIVATE_46BX','CROP_PRIVATE_71SD','COMMENT_PRIVATE_79TZ','META_PRIVATE_38NP','AUTHOR_PRIVATE_63VJ','ATTACHMENT_PRIVATE_51LR','JAVASCRIPT_PRIVATE_74HM','OUTLINE_PRIVATE_27CS']),encoding='utf-8')
    print('Redaction fixtures generated: text/OCR/image/form, rotated/cropped pages, hidden payloads.')

def verify(root):
    from pypdf import PdfReader
    import fitz
    checks=[]
    def check(ok,name):
        if not ok:raise AssertionError(name)
        checks.append(name)
    secrets=json.loads((root/'redaction-private-tokens.json').read_text())
    source=PdfReader(root/'redaction-source.pdf')
    source_catalog=source.trailer['/Root']
    check(len(source.attachments)>0 and ('/OpenAction' in source_catalog or '/JavaScript' in source_catalog.get('/Names',{})),'fixture really has attachment and JavaScript')
    regions=json.loads((root/'redaction-regions.json').read_text(encoding='utf-8-sig'))
    for name,keep in [('redaction-image.pdf',False),('redaction-search.pdf',True),('redaction-worker.pdf',True)]:
        path=root/name;r=PdfReader(path);catalog=r.trailer['/Root'];raw=path.read_bytes()
        check(len(r.pages)==3,name+': correct page count')
        check(not r.attachments and not r.metadata,name+': no attachment or source metadata')
        check(all(key not in catalog for key in ('/OpenAction','/AA','/AcroForm','/Metadata','/Names','/Outlines','/OCProperties','/Perms')),name+': catalog contains no hidden/action/layer/form/signature payload')
        check(raw.count(b'%%EOF')==1 and b'/Prev' not in raw,name+': fresh file has no incremental history')
        text=''.join(p.extract_text() or '' for p in r.pages)
        for token in secrets:check(token not in text and token.encode() not in raw,name+': original token is absent: '+token)
        if keep:
            check('PUBLIC' in text and 'KEEP' in text,name+': unredacted text remains extractable')
            check('INLINE_LEFT' in text and 'INLINE_RIGHT' in text,name+': text on both sides of a redaction survives')
            check('公开测试' in text and 'PUBLIC ROTATED KEEP' in text,name+': CJK and rotated words retain order')
        else:check(not text.strip(),name+': image mode contains no text')
        for number,page in enumerate(r.pages):
            check('/Annots' not in page and '/PieceInfo' not in page,name+': page annotations/private data absent '+str(number+1))
            images=page['/Resources']['/XObject'];check(len(images)==1,name+': one baked page image '+str(number+1))
            image=next(iter(images.values())).get_object();data=image.get_data();width=image['/Width'];height=image['/Height']
            check(len(data)==width*height*3,name+': RGB stream decompresses exactly '+str(number+1))
            baseline=PdfReader(root/'redaction-image.pdf').pages[number]['/Resources']['/XObject'];baseline_image=next(iter(baseline.values())).get_object()
            check(data==baseline_image.get_data(),name+': text mode does not change the baked page pixels '+str(number+1))
            with fitz.open(root/'redaction-source.pdf') as original:
                expected=original[number].rect
                check(abs(float(page.mediabox.width)-expected.width)<.02 and abs(float(page.mediabox.height)-expected.height)<.02,name+': page size and rotation flatten consistently '+str(number+1))
        with fitz.open(path) as pdf:
            for region in regions:
                index=region['page'];l,b,right,t=region['bounds'];original=fitz.open(root/'redaction-source.pdf');srcpage=original[index]
                # Use the original page's full transformation (crop and rotation) to locate the mask.
                rectangle=fitz.Rect(l,b,right,t)*srcpage.transformation_matrix*srcpage.rotation_matrix
                rectangle=rectangle & pdf[index].rect
                if rectangle.width>4 and rectangle.height>4:
                    rectangle=fitz.Rect(rectangle.x0+1,rectangle.y0+1,rectangle.x1-1,rectangle.y1-1)
                    pix=pdf[index].get_pixmap(matrix=fitz.Matrix(1,1),clip=rectangle,alpha=False)
                    check(len(pix.samples)>0 and max(pix.samples)<10,name+': masked pixels are baked black on page '+str(index+1))
                    if keep:
                        chars=[ch for block in pdf[index].get_text('rawdict')['blocks'] if 'lines' in block for line in block['lines'] for span in line['spans'] for ch in span['chars']]
                        check(not any((fitz.Rect(ch['bbox']) & rectangle).get_area()>.01 for ch in chars if not ch['c'].isspace()),name+': rebuilt characters cannot overlap the interior of the mask on page '+str(index+1))
                original.close()
            rebuilt=''.join(p.get_text() for p in pdf)
            for token in secrets:check(token not in rebuilt,name+': second text extractor cannot find '+token)
            check(('PUBLIC' in rebuilt) if keep else not rebuilt.strip(),name+': second extractor agrees with selected text mode')
    a4=PdfReader(root/'redaction-a4-600.pdf');image=next(iter(a4.pages[0]['/Resources']['/XObject'].values())).get_object()
    check(image['/Width']>=4960 and image['/Height']>=7016,'600 dpi: A4 output exceeds 24 megapixels without reducing precision')
    check(not (a4.pages[0].extract_text() or '').strip(),'600 dpi: image copy contains no underlying A4 text')
    result={'passed':len(checks),'checks':checks}
    (root/'redaction-independent.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'redaction_independent_checks':len(checks),'failures':0}))

if __name__=='__main__':
    {'fixtures':fixtures,'verify':verify}[sys.argv[1]](Path(sys.argv[2]))
