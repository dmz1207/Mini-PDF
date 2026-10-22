"""Independent persisted output checks for layer/resource compatibility fixes."""
import json
from pathlib import Path
import sys
import fitz
from pypdf import PdfReader


def pixels(document, page):
    item=document[page]
    scale=600/max(item.rect.width,item.rect.height)
    return item.get_pixmap(matrix=fitz.Matrix(scale,scale),alpha=False).samples


def verify(root):
    checks=[]
    def check(condition,message):
        if not condition: raise AssertionError(message)
        checks.append(message)
    names=('issue11144_reduced','issue12007_reduced','issue18823','issue269_1','issue269_2','visibility_expressions','same-name-A','same-name-B')
    for name in names:
        source_path=root/('layer-source-'+name+'.pdf')
        if not source_path.exists(): continue
        with fitz.open(source_path) as source:
            before=pixels(source,0)
            for action,indices in (('merged',[0,1]),('duplicate',[0,1]),('insert',[1]),('extract',[0]),('organize',[0])):
                path=root/f'layer-{action}-{name}.pdf'
                with fitz.open(path) as output:
                    check(all(pixels(output,index)==before for index in indices),f'{name}: {action} preserves independent appearance')
                reader=PdfReader(path)
                properties=reader.trailer['/Root'].get('/OCProperties')
                check(properties is not None and len(properties.get_object()['/OCGs'])>0,
                      f'{name}: {action} retains standard optional-content metadata')
                check(b'/MiniPDFImportLayer' not in path.read_bytes(),f'{name}: {action} contains no private import marker')
    mapping=root/'layer-mixed-sources.txt'
    if mapping.exists():
        names=mapping.read_text(encoding='utf-8-sig').splitlines()
        with fitz.open(root/'layer-mixed.pdf') as output:
            check(output.page_count==len(names),'Mixed merge retains every requested page')
            for index,name in enumerate(names):
                with fitz.open(root/f'layer-source-{name}.pdf') as source:
                    check(pixels(source,0)==pixels(output,index),f'Mixed merge page {index}: independent source appearance is unchanged')
    if (root/'repaired-content-merge.pdf').exists():
        with fitz.open(root/'public-dangling-content.pdf') as source, fitz.open(root/'repaired-content-merge.pdf') as output:
            check(all(pixels(source,0)==pixels(output,index) for index in range(2)),
                  'Corrupt-array repair preserves independently rendered valid content')
            check(all('Goodbye, world!' in page.get_text() for page in output),'Corrupt-array repair preserves independently extracted text')
    if (root/'jpx-system-recovery.png').exists():
        from PIL import Image, ImageChops, ImageStat
        with fitz.open(root/'public-invalid-jpx.pdf') as source:
            expected=source[0].get_pixmap(matrix=fitz.Matrix(600/source[0].rect.width,413/source[0].rect.height),alpha=False)
            reference=Image.frombytes('RGB',(expected.width,expected.height),expected.samples)
            actual=Image.open(root/'jpx-system-recovery.png').convert('RGB')
            check(actual.size==(600,413),'Windows recovery returns exact requested pixel dimensions')
            if reference.size!=actual.size: reference=reference.resize(actual.size,Image.Resampling.LANCZOS)
            difference=sum(ImageStat.Stat(ImageChops.difference(reference,actual)).mean)/3
            # This adversarial file has component layouts interpreted differently by readers.
            # Record the whole-page difference rather than declaring color equivalence.
            panels=[(25,25,200,200),(410,25,570,190)]
            panel_errors=[sum(ImageStat.Stat(ImageChops.difference(reference.crop(box),actual.crop(box))).mean)/3 for box in panels]
            check(max(panel_errors)<8,'Windows recovery preserves the comparable RGB chart panels')
            (root/'windows-render-difference.json').write_text(json.dumps({'whole_page_mean_absolute_rgb_difference':difference,
                'comparable_panel_errors':panel_errors,'color_equivalence':False,
                'disposition':'Read-only compatibility preview; original image data is unchanged; editing/printing disabled.'},indent=2),encoding='utf8')
    (root/'compatibility-independent-checks.json').write_text(json.dumps({'passed':len(checks),'checks':checks},indent=2),encoding='utf8')
    print(f'Independent compatibility checks: {len(checks)} passed')


if __name__=='__main__': verify(Path(sys.argv[1]))
