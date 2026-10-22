"""Independent scan/OCR and cross-page selection fixtures; development only."""
from pathlib import Path
import io
import sys
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.cidfonts import UnicodeCIDFont
from reportlab.lib.utils import ImageReader
from PIL import Image
import fitz

root = Path(sys.argv[1]); root.mkdir(parents=True, exist_ok=True)
pdfmetrics.registerFont(UnicodeCIDFont('STSong-Light'))
stream = io.BytesIO()
visible = canvas.Canvas(stream, pagesize=(400, 600))
visible.setFont('STSong-Light', 18)
visible.drawString(40, 500, '扫描文字选择：自然科学与经验。')
visible.drawString(40, 465, '第二行：跨页复制和高亮测试。')
visible.setFont('Helvetica', 16); visible.drawString(40, 420, 'OCR TEXT SELECTION')
visible.showPage(); visible.save()
with fitz.open(stream=stream.getvalue(), filetype='pdf') as doc:
    pix = doc[0].get_pixmap(matrix=fitz.Matrix(2, 2), alpha=False)
    scan = Image.frombytes('RGB', (pix.width, pix.height), pix.samples)
result = canvas.Canvas(str(root / 'ocr-selection.pdf'), pagesize=(400, 600))
def layer(page):
    text = result.beginText(40, 500)
    text.setFont('STSong-Light', 18)
    text.setTextRenderMode(3 if page != 2 else 0)
    text.textLine('扫描文字选择：自然科学与经验。')
    text.setTextOrigin(40, 465); text.textLine('第二行：跨页复制和高亮测试。')
    text.setTextOrigin(40, 420); text.setFont('Helvetica', 16); text.textLine('OCR TEXT SELECTION')
    result.drawText(text)
for page in range(6):
    result.setPageRotation(0)
    if page == 1: layer(page)
    result.drawImage(ImageReader(scan), 0, 0, width=400, height=600)
    if page == 2:
        result.saveState(); result.setFillAlpha(0); layer(page); result.restoreState()
    elif page == 3:
        result.beginForm('NestedOCR', 0, 0, 400, 600); layer(page); result.endForm(); result.doForm('NestedOCR')
    elif page != 1: layer(page)
    result.showPage()
result.save()
# Rotate the completed physical page, without ReportLab swapping its media box.
from pypdf import PdfReader, PdfWriter
reader = PdfReader(root / 'ocr-selection.pdf')
writer = PdfWriter()
for index, page in enumerate(reader.pages):
    if index == 4:
        page.rotate(90)
    writer.add_page(page)
rotated = root / 'ocr-selection-rotated.pdf'
with rotated.open('wb') as stream:
    writer.write(stream)
rotated.replace(root / 'ocr-selection.pdf')
pages = canvas.Canvas(str(root / 'selection-pages.pdf'), pagesize=(400, 300))
for index in range(4):
    pages.setFont('Helvetica', 18)
    pages.drawString(40, 230, f'PAGE {index+1} SELECTION START')
    pages.drawString(40, 195, f'PAGE {index+1} SELECTION END')
    pages.showPage()
pages.save()
print('Selection fixtures generated: six OCR variants and four short reading pages.')

# Optional public fixtures come from the already verified, development-only corpus.
if len(sys.argv) > 2:
    import shutil
    corpus = Path(sys.argv[2]) / 'corpora' / 'pdfjs'
    fixtures = {'issue5747.pdf': 'public-ocr.pdf', 'jpx_numcomps.pdf': 'public-invalid-jpx.pdf'}
    for name in ('SimFang-variant', 'XiaoBiaoSong', 'issue18117', 'issue20489'):
        fixtures[name + '.pdf'] = 'font-source-' + name + '.pdf'
    for name in ('issue11144_reduced', 'issue12007_reduced', 'issue18823', 'issue269_1', 'issue269_2', 'visibility_expressions'):
        fixtures[name + '.pdf'] = 'layer-source-' + name + '.pdf'
    for source, target in fixtures.items():
        if (corpus / source).exists():
            shutil.copyfile(corpus / source, root / target)

    damaged = Path(sys.argv[2]) / 'corpora' / 'pdfium' / 'bug_455199.pdf'
    if damaged.exists():
        shutil.copyfile(damaged, root / 'public-dangling-content.pdf')

    # Equal layer names in different files must never merge their visibility states.
    from pypdf.generic import NameObject, TextStringObject
    for suffix, source_name in [('A','issue11144_reduced'),('B','issue269_2')]:
        source_path=root/('layer-source-'+source_name+'.pdf')
        if source_path.exists():
            writer=PdfWriter(clone_from=source_path)
            for group in writer._root_object['/OCProperties']['/OCGs']:
                group.get_object()[NameObject('/Name')]=TextStringObject('Shared layer name')
            with (root/('layer-source-same-name-'+suffix+'.pdf')).open('wb') as stream:
                writer.write(stream)
    source_path=root/'layer-source-issue11144_reduced.pdf'
    if source_path.exists():
        writer=PdfWriter(clone_from=source_path)
        writer.encrypt('layer-test-password',algorithm='AES-256')
        with (root/'layer-encrypted-source.pdf').open('wb') as stream:
            writer.write(stream)
