"""Development-only fixtures and independent checks for signature/watermark fixes."""
import io
import json
import sys
from pathlib import Path


def fixtures(root):
    from PIL import Image, ImageDraw
    from reportlab.pdfgen import canvas
    from reportlab.lib.utils import ImageReader
    from pypdf import PdfReader, PdfWriter
    from pypdf.generic import DictionaryObject as D, NameObject as N, ArrayObject as A, NumberObject as I, TextStringObject as T, DecodedStreamObject
    root.mkdir(parents=True, exist_ok=True)
    image = Image.new('RGB', (300, 100), 'white')
    draw = ImageDraw.Draw(image)
    draw.line([(10, 75), (90, 20), (170, 80), (290, 15)], fill=(15, 35, 75), width=6)
    image.save(root / 'signature-image.png', dpi=(300, 300))
    output = io.BytesIO()
    c = canvas.Canvas(output, pagesize=(400, 600))
    c.beginForm('external-watermark', 0, 0, 240, 60)
    c.setFillColorRGB(.5, .5, .5)
    c.setFillAlpha(.25)
    c.setFont('Helvetica', 28)
    c.drawString(0, 10, 'FORM WATERMARK')
    c.endForm()
    for page in range(4):
        c.setFillColorRGB(0, 0, 0)
        c.setFillAlpha(1)
        c.setFont('Helvetica', 14)
        c.drawString(25, 565, f'BODY PAGE {page+1} KEEP')
        c.setFont('Helvetica', 12)
        c.drawString(25, 530, 'EXTERNAL DRAFT')  # Same text must survive at body size/location.
        c.saveState()
        c.translate(60, 230)
        c.rotate(28)
        c.setFillAlpha(.2)
        c.setFillColorRGB(.4, .4, .4)
        c.setFont('Helvetica', 38)
        c.drawString(0, 0, 'EXTERNAL DRAFT')
        c.restoreState()
        c.drawImage(ImageReader(image), 140 if page < 3 else 20, 80, width=120, height=40)
        if page < 2:
            c.saveState()
            c.translate(65, 150)
            c.doForm('external-watermark')
            c.restoreState()
        c.showPage()
    c.save()
    writer = PdfWriter()
    writer.append(PdfReader(output))
    font = writer._add_object(D({N('/Type'): N('/Font'), N('/Subtype'): N('/Type1'), N('/BaseFont'): N('/Helvetica')}))
    for index in (0, 1):
        appearance = DecodedStreamObject()
        appearance.set_data(b'q 0.8 0 0 RG 1 w 0 0 140 40 re S BT /F1 13 Tf 5 15 Td (THIRD PARTY STAMP) Tj ET Q')
        appearance.update({N('/Type'): N('/XObject'), N('/Subtype'): N('/Form'), N('/BBox'): A([I(0), I(0), I(140), I(40)]), N('/Resources'): D({N('/Font'): D({N('/F1'): font})})})
        stamp = D({N('/Type'): N('/Annot'), N('/Subtype'): N('/Stamp'), N('/Rect'): A([I(120), I(430), I(260), I(470)]), N('/Contents'): T('THIRD PARTY STAMP'), N('/F'): I(4), N('/AP'): D({N('/N'): writer._add_object(appearance)})})
        writer.add_annotation(index, stamp)
    with (root / 'external-watermarks.pdf').open('wb') as file:
        writer.write(file)
    print('Signature and external watermark fixtures generated')


def verify(root):
    import fitz
    from pypdf import PdfReader
    checks = []
    def check(ok, label):
        if not ok:
            raise AssertionError(label)
        checks.append(label)
    doc = fitz.open(root / 'external-watermarks-removed.pdf')
    check(len(doc) == 4, 'Removal preserves page count')
    for index, page in enumerate(doc):
        text = page.get_text()
        check(f'BODY PAGE {index+1} KEEP' in text, f'Page {index+1}: original body survives')
        check(text.count('EXTERNAL DRAFT') == 1, f'Page {index+1}: body text with same wording survives; large watermark is removed')
        check('FORM WATERMARK' not in text, f'Page {index+1}: external form watermark removed')
        check(len(page.get_images()) == (1 if index == 3 else 0), f'Page {index+1}: only content/position-matched image watermarks removed')
        check(not list(page.annots() or []), f'Page {index+1}: third-party stamp annotations removed')
    doc[0].get_pixmap(matrix=fitz.Matrix(1.3, 1.3)).save(root / 'external-removed-independent.png')
    reader = PdfReader(root / 'external-watermarks-removed.pdf', strict=True)
    check(all('BODY PAGE' in p.extract_text() for p in reader.pages), 'Independent page-tree/text parser accepts removal output')
    signed = fitz.open(root / 'signature-click.pdf')
    check(len(signed[0].get_images()) == 1, 'Click-placed name persists as exactly one image object')
    signed[0].get_pixmap(matrix=fitz.Matrix(1.3, 1.3)).save(root / 'signature-click-independent.png')
    (root / 'fix-independent-checks.json').write_text(json.dumps({'passed': len(checks), 'checks': checks}, indent=2), encoding='utf8')
    print(f'Independent signature/watermark fix checks: {len(checks)} passed')


if __name__ == '__main__':
    globals()[sys.argv[1]](Path(sys.argv[2]))
