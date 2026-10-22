"""Export per-path compatibility results and optional independent diagnoses.

Development only. No PDF or private passwords are copied into reports.
"""
import argparse
from collections import Counter
import concurrent.futures
import csv
import json
from pathlib import Path
import re
import sys
from corpus_test import isolated


def classification(row, reference):
    state = row['status']
    source = reference.get('source', {})
    if state == 'ok_signed':
        return 'signed_preservation_pass'
    if state == 'ok' and row.get('merge_preserved') is not False and row.get('annotation_preserved') is not False:
        return 'applicable_checks_pass'
    if state == 'ok' and row.get('merge_preserved') is False:
        if reference.get('merged_visual_equal') and reference.get('merged_text_equal'):
            return 'engine_only_merge_difference'
        return 'confirmed_layer_import_difference' if source.get('optional_content') else 'merge_difference'
    if state == 'roundtrip_difference':
        if reference.get('roundtrip_visual_equal') and reference.get('roundtrip_text_equal'):
            return 'engine_only_roundtrip_difference'
        return 'roundtrip_difference'
    if state == 'crash':
        return 'native_process_failure'
    if state == 'timeout':
        return 'probe_time_limit'
    if source.get('password_required'):
        return 'password_required'
    if source.get('xfa') and source.get('pages') == 0:
        return 'xfa_only_unsupported'
    if source.get('pages') == 0:
        return 'no_displayable_pages'
    if 'source_error' in reference:
        return 'reference_also_cannot_complete'
    if source.get('pages', 0) > 0:
        return 'engine_open_or_page_difference'
    return 'unresolved'


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--workdir', type=Path, required=True)
    parser.add_argument('--name', default='compatibility')
    parser.add_argument('--baseline')
    parser.add_argument('--references', action='store_true')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    root = args.workdir.resolve()
    run = root / 'runs' / args.name
    refs = root / ('references-' + args.name)
    refs.mkdir(exist_ok=True)
    rows = [json.loads(line) for line in (run / 'results.jsonl').read_text(encoding='utf8').splitlines()]
    cases = {item['id']: item for item in json.loads((run / 'cases.json').read_text(encoding='utf8'))}
    pending = {row['tested_id']: row for row in rows
               if not row['status'].startswith('ok') or row.get('merge_preserved') is False
               or row.get('annotation_preserved') is False}
    if args.references:
        def diagnose(item):
            identity, row = item
            target = refs / (identity + '.json')
            if target.exists():
                return
            case = cases[identity]
            command = [sys.executable, str(Path(__file__).with_name('corpus_reference.py')),
                       str(root / case['local']), str(target), '--case', str(run / identity)]
            if case['password']:
                command.extend(['--password', case['password']])
            outcome = isolated(command, refs, timeout=30)
            if not target.exists():
                target.write_text(json.dumps({'reference_process': outcome}), encoding='utf8')
        with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
            list(pool.map(diagnose, pending.items()))
    references = {}
    for identity in pending:
        target = refs / (identity + '.json')
        if target.exists():
            references[identity] = json.loads(target.read_text(encoding='utf8'))
    baseline = {}
    if args.baseline:
        baseline = {row['id']: row for row in (json.loads(line) for line in
                    (root / 'runs' / args.baseline / 'results.jsonl').read_text(encoding='utf8').splitlines())}
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    fields = ['corpus', 'source', 'git_sha', 'status', 'classification', 'pages', 'rendered_pages',
              'merge_preserved', 'annotation_preserved', 'native_error', 'phase', 'elapsed_ms',
              'peak_commit_bytes', 'baseline_status', 'baseline_merge_preserved']
    exceptions = []
    categories = Counter()
    with (output / 'compatibility-results.csv').open('w', newline='', encoding='utf-8-sig') as stream:
        writer = csv.DictWriter(stream, fieldnames=fields)
        writer.writeheader()
        for row in sorted(rows, key=lambda item: (item['corpus'], item['source'])):
            reference = references.get(row['tested_id'], {})
            category = classification(row, reference)
            categories[category] += 1
            record = {key: row.get(key) for key in fields}
            record['classification'] = category
            old = baseline.get(row['id'], {})
            record['baseline_status'] = old.get('status')
            record['baseline_merge_preserved'] = old.get('merge_preserved')
            writer.writerow(record)
            if category not in ('applicable_checks_pass', 'signed_preservation_pass'):
                detail = {**record, 'reference': reference}
                # MuPDF errors sometimes contain the development machine's absolute path.
                if 'source_error' in reference:
                    reference['source_error'] = re.sub(r"'[^']+[.]pdf'", "'<source.pdf>'", reference['source_error'])
                exceptions.append(detail)
    summary = json.loads((run / 'summary.json').read_text(encoding='utf8'))
    stamp = run / 'probe-build.json'
    if stamp.exists(): summary['probe_build'] = json.loads(stamp.read_text(encoding='utf8'))
    summary['classification'] = dict(categories)
    summary['merge_mismatches'] = sum(row.get('merge_preserved') is False for row in rows)
    summary['signed_checks_do_not_edit'] = True
    summary['permission_skipped_edit_checks'] = sum(row['status'] == 'ok' and row.get('merge_preserved') is None for row in rows)
    summary['sources'] = [{key: item[key] for key in ('key', 'repo', 'commit', 'route', 'archive_bytes', 'extracted_bytes')}
                          for item in json.loads((root / 'downloads.json').read_text(encoding='utf8'))]
    if baseline:
        values = list(baseline.values())
        summary['baseline'] = {'statuses': dict(Counter(row['status'] for row in values)),
                               'merge_mismatches': sum(row.get('merge_preserved') is False for row in values)}
    for name, data in [('compatibility-summary.json', summary), ('compatibility-exceptions.json', exceptions)]:
        (output / name).write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf8')
    print(json.dumps({'paths': len(rows), 'classification': categories, 'exceptions': len(exceptions)}))


if __name__ == '__main__':
    main()
