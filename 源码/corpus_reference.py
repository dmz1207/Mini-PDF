"""Independent MuPDF diagnosis of a corpus case, development only."""
import argparse
import hashlib
import json
from pathlib import Path
import fitz


def pixels(document, index, width=600, height=600):
    page = document[index]
    factor = 600 / max(page.rect.width, page.rect.height)
    image = page.get_pixmap(matrix=fitz.Matrix(factor, factor), alpha=False)
    return [image.width, image.height, hashlib.sha256(image.samples).hexdigest()]


def describe(path, password=''):
    with fitz.open(path) as document:
        if document.needs_pass and not document.authenticate(password):
            return {'password_required': True, 'pages': document.page_count}
        catalog = document.pdf_catalog()
        info = {'pages': document.page_count, 'repaired': document.is_repaired,
                'optional_content': document.xref_get_key(catalog, 'OCProperties')[0] != 'null',
                'tagged': document.xref_get_key(catalog, 'StructTreeRoot')[0] != 'null',
                'xfa': document.xref_get_key(catalog, 'AcroForm/XFA')[0] != 'null'}
        if document.page_count:
            info['first_pixels'] = pixels(document, 0)
            info['first_text'] = hashlib.sha256(document[0].get_text().encode()).hexdigest()
            info['last_pixels'] = pixels(document, document.page_count - 1)
            info['last_text'] = hashlib.sha256(document[-1].get_text().encode()).hexdigest()
        return info


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--password', default='')
    parser.add_argument('--case', type=Path)
    args = parser.parse_args()
    result = {}
    try:
        source = describe(args.source, args.password)
        result['source'] = source
        if args.case:
            for name in ('roundtrip', 'merged'):
                path = args.case / (name + '.pdf')
                if path.exists():
                    try:
                        target = describe(path, args.password if name == 'roundtrip' else '')
                        result[name] = target
                        result[name + '_visual_equal'] = all(source.get(key) == target.get(key) for key in ('first_pixels', 'last_pixels'))
                        result[name + '_text_equal'] = all(source.get(key) == target.get(key) for key in ('first_text', 'last_text'))
                    except Exception as error: result[name + '_error'] = str(error)
    except Exception as error:
        result['source_error'] = type(error).__name__ + ': ' + str(error)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')


if __name__ == '__main__':
    main()
