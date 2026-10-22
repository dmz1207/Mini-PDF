"""Independent persisted-selection checks; development only."""
import json
from pathlib import Path
import sys
import fitz
from pypdf import PdfReader


def verify(root):
    checks = []

    def check(condition, message):
        if not condition:
            raise AssertionError(message)
        checks.append(message)

    source = PdfReader(root / 'selection-pages.pdf')
    saved = PdfReader(root / 'cross-page-highlight.pdf')
    check(len(saved.pages) == len(source.pages) == 4, 'Cross-page markup retains the page count')
    for index, page in enumerate(saved.pages):
        check(page.extract_text() == source.pages[index].extract_text(),
              f'Page {index + 1}: original text is preserved')
        annotations = [item.get_object() for item in page.get('/Annots', [])]
        if index < 2:
            check(len(annotations) == 1 and annotations[0]['/Subtype'] == '/Highlight',
                  f'Page {index + 1}: a standard highlight persists')
            quads = annotations[0].get('/QuadPoints', [])
            check(len(quads) >= 8 and len(quads) % 8 == 0,
                  f'Page {index + 1}: highlight has complete PDF quadrilaterals')
        else:
            check(not annotations, f'Page {index + 1}: unselected page has no markup')
    with fitz.open(root / 'selection-pages.pdf') as before, fitz.open(root / 'cross-page-highlight.pdf') as after:
        for index in range(4):
            base = before[index].get_pixmap(alpha=False).samples
            output = after[index].get_pixmap(alpha=False).samples
            check((base != output) == (index < 2),
                  f'Page {index + 1}: independent raster changes only where highlighted')
    with fitz.open(root / 'ocr-selection.pdf') as document:
        for index, page in enumerate(document):
            check('自然科学与经验' in page.get_text() and len(page.get_images(full=True)) > 0,
                  f'OCR variant {index}: Chinese layer coexists with a scan image')
    for name in ('SimFang-variant', 'XiaoBiaoSong', 'issue18117', 'issue20489'):
        for action, marker in (('merged', 'MERGE'), ('duplicated', 'DUPLICATE'), ('inserted', 'INSERT')):
            path = root / f'font-{action}-{name}.pdf'
            if not path.exists():
                continue
            with fitz.open(path) as document:
                check(any('ADDED_AFTER_' + marker in page.get_text() for page in document),
                      f'{name}: {action} text is independently readable')
                check(all(page.get_pixmap(matrix=fitz.Matrix(.5, .5), alpha=False).width > 0
                          for page in document), f'{name}: every {action} page independently renders')
    (root / 'selection-independent-checks.json').write_text(
        json.dumps({'passed': len(checks), 'checks': checks}, indent=2), encoding='utf8')
    print(f'Independent selection/font checks: {len(checks)} passed')


if __name__ == '__main__':
    verify(Path(sys.argv[1]))
