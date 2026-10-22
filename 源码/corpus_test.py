"""Run the production API probe in isolated, bounded per-PDF processes.

Development only. Pass the pinned corpus download directory and compiled probe.
"""
import argparse
import concurrent.futures
import ctypes as c
from ctypes import wintypes as w
import hashlib
import json
import os
from pathlib import Path
import subprocess
import tarfile
import time


class BasicLimits(c.Structure):
    _fields_ = [('user', c.c_longlong), ('job', c.c_longlong), ('flags', w.DWORD),
                ('minimum', c.c_size_t), ('maximum', c.c_size_t), ('active', w.DWORD),
                ('affinity', c.c_size_t), ('priority', w.DWORD), ('scheduling', w.DWORD)]


class IoCounters(c.Structure):
    _fields_ = [(name, c.c_ulonglong) for name in ('read', 'write', 'other', 'read_bytes', 'write_bytes', 'other_bytes')]


class ExtendedLimits(c.Structure):
    _fields_ = [('basic', BasicLimits), ('io', IoCounters), ('process_memory', c.c_size_t),
                ('job_memory', c.c_size_t), ('peak_process', c.c_size_t), ('peak_job', c.c_size_t)]


kernel = c.WinDLL('kernel32', use_last_error=True)
kernel.CreateJobObjectW.argtypes = [c.c_void_p, w.LPCWSTR]
kernel.CreateJobObjectW.restype = w.HANDLE
kernel.SetInformationJobObject.argtypes = [w.HANDLE, c.c_int, c.c_void_p, w.DWORD]
kernel.AssignProcessToJobObject.argtypes = [w.HANDLE, w.HANDLE]
kernel.QueryInformationJobObject.argtypes = [w.HANDLE, c.c_int, c.c_void_p, w.DWORD, c.c_void_p]
kernel.CloseHandle.argtypes = [w.HANDLE]


def isolated(command, cwd, timeout=45):
    job = kernel.CreateJobObjectW(None, None)
    limits = ExtendedLimits()
    limits.basic.flags = 0x100 | 0x2000
    limits.process_memory = 768 * 1024 * 1024
    if not job or not kernel.SetInformationJobObject(job, 9, c.byref(limits), c.sizeof(limits)):
        raise c.WinError(c.get_last_error())
    process = None
    try:
        process = subprocess.Popen(command, cwd=cwd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                   creationflags=subprocess.CREATE_NO_WINDOW)
        if not kernel.AssignProcessToJobObject(job, w.HANDLE(int(process._handle))):
            process.kill()
            raise c.WinError(c.get_last_error())
        timed_out = False
        try:
            process.wait(timeout=timeout)
        except subprocess.TimeoutExpired:
            timed_out = True
            process.kill()
            process.wait(timeout=5)
        kernel.QueryInformationJobObject(job, 9, c.byref(limits), c.sizeof(limits), None)
        return {'exit_code': process.returncode, 'timed_out': timed_out,
                'peak_commit_bytes': limits.peak_process}
    finally:
        if process is not None and process.poll() is None:
            process.kill()
        kernel.CloseHandle(job)


def pdfjs_options(root):
    with tarfile.open(root / 'downloads' / 'pdfjs.tgz', 'r:gz') as archive:
        member = next(m for m in archive if m.name.endswith('/test/test_manifest.json'))
        manifest = json.load(archive.extractfile(member))
    passwords = {}
    for entry in manifest:
        if entry.get('password'):
            passwords[entry['file'].removeprefix('pdfs/')] = entry['password']
    return passwords


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--workdir', type=Path, required=True)
    parser.add_argument('--probe', type=Path, required=True)
    parser.add_argument('--name', default='baseline')
    parser.add_argument('--workers', type=int, default=3)
    parser.add_argument('--limit', type=int, default=0)
    args = parser.parse_args()
    if not args.name or any(character not in 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-' for character in args.name):
        parser.error('--name must contain only letters, numbers, underscore or hyphen')
    root = args.workdir.resolve()
    run = root / 'runs' / args.name
    run.mkdir(parents=True, exist_ok=True)
    probe = args.probe.resolve()
    metadata = {name: hashlib.sha256(path.read_bytes()).hexdigest() for name, path in (
        ('probe_sha256', probe), ('core_sha256', probe.with_name('MiniPDF.Core.exe')),
        ('pdfium_sha256', probe.with_name('pdfium.dll')))}
    metadata.update(render_max_dimension=600, timeout_seconds_per_pdf=45, process_commit_limit_mib=768)
    stamp = run / 'probe-build.json'
    if stamp.exists():
        if json.loads(stamp.read_text(encoding='utf8')) != metadata:
            raise ValueError('Probe/build changed: choose a fresh --name instead of reusing cached results')
    elif (run / 'results.jsonl').exists():
        raise ValueError('Existing run lacks build metadata: choose a fresh --name')
    stamp.write_text(json.dumps(metadata, indent=2), encoding='utf8')
    options = pdfjs_options(root)
    cases, groups = [], {}
    for corpus in json.loads((root / 'downloads.json').read_text(encoding='utf8')):
        for entry in corpus['files']:
            if not entry['source'].endswith('.pdf'):
                continue
            identity = corpus['key'] + '/' + entry['source']
            case = {'id': hashlib.sha256(identity.encode()).hexdigest()[:20], 'corpus': corpus['key'],
                    'source': entry['source'], 'local': entry['local'], 'git_sha': entry['git_sha'],
                    'password': options.get(Path(entry['local']).name, '') if corpus['key'] == 'pdfjs' else ''}
            cases.append(case)
            groups.setdefault(entry['git_sha'], []).append(case)
    if args.limit:
        cases = cases[:args.limit]
        allowed = {e['id'] for e in cases}
        groups = {sha: [e for e in group if e['id'] in allowed] for sha, group in groups.items()}
        groups = {sha: group for sha, group in groups.items() if group}
    (run / 'cases.json').write_text(json.dumps(cases, ensure_ascii=False, indent=2), encoding='utf8')
    def test(group):
        case = next((e for e in group if e['password']), group[0])
        folder = run / case['id']
        folder.mkdir(parents=True, exist_ok=True)
        saved = folder / 'result.json'
        if saved.exists():
            outcome = json.loads(saved.read_text(encoding='utf-8-sig'))
        else:
            command = [str(args.probe.resolve()), str(root / case['local']), str(folder), case['password']]
            process = isolated(command, args.probe.resolve().parent)
            if saved.exists():
                outcome = json.loads(saved.read_text(encoding='utf-8-sig'))
            else:
                progress = folder / 'progress.txt'
                outcome = {'status': 'timeout' if process['timed_out'] else 'crash',
                           'phase': progress.read_text() if progress.exists() else 'startup'}
            outcome.update(process)
            saved.write_text(json.dumps(outcome, ensure_ascii=False, indent=2), encoding='utf8')
        if outcome.get('status') == 'ok' and outcome.get('merge_preserved', True) and outcome.get('annotation_preserved', True):
            for name in ('roundtrip.pdf', 'merged.pdf', 'annotated.pdf'):
                (folder / name).unlink(missing_ok=True)
        return group, case['id'], outcome
    began = time.monotonic()
    completed, statistics, results = 0, {}, []
    with (run / 'results.jsonl').open('w', encoding='utf8') as log, concurrent.futures.ThreadPoolExecutor(max_workers=args.workers) as pool:
        futures = [pool.submit(test, group) for group in groups.values()]
        for future in concurrent.futures.as_completed(futures):
            group, actual, outcome = future.result()
            completed += 1
            statistics[outcome['status']] = statistics.get(outcome['status'], 0) + len(group)
            if outcome.get('roundtrip_diagnostics'):
                print(json.dumps({'diagnostic_case': group[0]['source'],
                                  'roundtrip_diagnostics': outcome['roundtrip_diagnostics']}), flush=True)
            for case in group:
                row = {key: value for key, value in case.items() if key != 'password'}
                row.update({'tested_id': actual, 'status': outcome['status'], 'phase': outcome.get('phase'),
                            'pages': outcome.get('pages'), 'rendered_pages': outcome.get('rendered_pages', 0),
                            'differences': outcome.get('roundtrip_differences', []),
                            'roundtrip_diagnostics': outcome.get('roundtrip_diagnostics', []),
                            'merge_preserved': outcome.get('merge_preserved'),
                            'annotation_preserved': outcome.get('annotation_preserved'),
                            'native_error': outcome.get('native_error'), 'error': outcome.get('error'),
                            'elapsed_ms': outcome.get('elapsed_ms'), 'exit_code': outcome.get('exit_code'),
                            'peak_commit_bytes': outcome.get('peak_commit_bytes')})
                results.append(row)
                log.write(json.dumps(row, ensure_ascii=False) + '\n')
            log.flush()
            if completed % 25 == 0 or completed == len(groups):
                print(json.dumps({'completed_unique': completed, 'total_unique': len(groups),
                                  'logical_files': len(results), 'statuses': statistics,
                                  'elapsed_s': round(time.monotonic() - began)}), flush=True)
    summary = {'logical_pdfs': len(cases), 'unique_pdfs': len(groups), 'statuses': statistics,
               'elapsed_seconds': round(time.monotonic() - began, 3), 'workers': args.workers,
               'render_max_dimension': 600, 'timeout_seconds_per_pdf': 45,
               'process_commit_limit_mib': 768}
    (run / 'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf8')
    print(json.dumps(summary), flush=True)


if __name__ == '__main__':
    main()
