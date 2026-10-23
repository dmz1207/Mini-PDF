"""Download pinned public test resources, preferring direct connections.

Development only. All archives and extracted files stay in --workdir.
Builds inventory.json from pinned corpus_sources.json and verifies every extracted Git blob.
"""
import argparse
import concurrent.futures
import hashlib
import gzip
import http.client
import json
from pathlib import Path
import re
import tarfile
import time
import urllib.error
import urllib.request


def fetch(url, output, label):
    failure = None
    for route, handler in [('direct', urllib.request.ProxyHandler({})),
                           ('system-proxy', urllib.request.ProxyHandler())]:
        started = time.monotonic()
        opener = urllib.request.build_opener(handler)
        request = urllib.request.Request(url, headers={'User-Agent': 'MiniPDF-Compatibility-Test/1.0', 'Accept-Encoding': 'gzip'})
        try:
            with opener.open(request, timeout=15) as response, output.with_suffix('.partial').open('wb') as stream:
                reader = gzip.GzipFile(fileobj=response) if response.headers.get('Content-Encoding') == 'gzip' else response
                count, last = 0, time.monotonic()
                while True:
                    chunk = reader.read(256 * 1024)
                    if not chunk:
                        break
                    stream.write(chunk)
                    count += len(chunk)
                    if time.monotonic() - last >= 8:
                        print(json.dumps({'download': label, 'route': route,
                                          'MiB': round(count / 1048576, 1)}), flush=True)
                        last = time.monotonic()
            output.with_suffix('.partial').replace(output)
            return {'route': route, 'archive_bytes': count,
                    'seconds': round(time.monotonic() - started, 2)}
        except (OSError, urllib.error.URLError, http.client.HTTPException, EOFError) as error:
            failure = type(error).__name__
            print(json.dumps({'download': label, 'route': route, 'retry_reason': failure}), flush=True)
    raise OSError(f'{label}: direct and configured network routes failed ({failure})')


def local_name(relative):
    parts = Path(relative).parts
    if relative.startswith(('/', '\\')) or any(p in ('..', '.') for p in parts):
        raise ValueError('Unsafe corpus path')
    return Path(*[re.sub(r'[<>:"\\|?*]', lambda m: '_u%04x_' % ord(m.group()), p).rstrip(' .')
                  for p in parts])


def blob_hash(path, size):
    digest = hashlib.sha1(('blob ' + str(size) + chr(0)).encode())
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(chunk)
    return digest.hexdigest()


def obtain(target, root):
    archive = root / 'downloads' / (target['key'] + '.tgz')
    route = {'route': 'verified-local-cache', 'archive_bytes': archive.stat().st_size} if archive.exists() else fetch(
        'https://codeload.github.com/' + target['repo'] + '/tar.gz/' + target['commit'], archive, target['key'])
    destination = root / 'corpora' / target['key']
    destination.mkdir(parents=True, exist_ok=True)
    expected = {entry['path']: entry for entry in target['files']}
    extracted, names = {}, {}
    with tarfile.open(archive, 'r:gz') as package:
        for member in package:
            pieces = member.name.split('/', 1)
            if len(pieces) != 2 or pieces[1] not in expected:
                continue
            original = pieces[1]
            if not member.isfile():
                raise ValueError('Unexpected non-file corpus entry')
            entry = expected[original]
            if member.size != entry['size']:
                raise ValueError('Archive size mismatch: ' + original)
            relative = local_name(original[len(target['prefix']):])
            collision = str(relative).casefold()
            if collision in names and names[collision] != original:
                relative = relative.with_name(relative.stem + '-' + entry['sha'][:10] + relative.suffix)
            names[str(relative).casefold()] = original
            output = destination / relative
            if not output.resolve().is_relative_to(destination.resolve()):
                raise ValueError('Extraction escaped corpus root')
            output.parent.mkdir(parents=True, exist_ok=True)
            with package.extractfile(member) as source, output.open('wb') as stream:
                for chunk in iter(lambda: source.read(1024 * 1024), b''):
                    stream.write(chunk)
            if blob_hash(output, member.size) != entry['sha']:
                raise ValueError('Git blob hash mismatch: ' + original)
            extracted[original] = {'source': original, 'local': str(output.relative_to(root)),
                                   'bytes': member.size, 'git_sha': entry['sha']}
    if set(extracted) != set(expected):
        raise ValueError('Missing archive entries for ' + target['key'])
    result = {'key': target['key'], 'repo': target['repo'], 'commit': target['commit'], **route,
              'extracted_bytes': sum(e['bytes'] for e in extracted.values()), 'files': list(extracted.values())}
    (root / (target['key'] + '-download.json')).write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf8')
    print(json.dumps({'complete': target['key'], 'route': route['route'], 'verified_files': len(extracted),
                      'PDFs': sum(p.endswith('.pdf') for p in extracted),
                      'MiB': round(result['extracted_bytes'] / 1048576, 2)}), flush=True)
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--workdir', required=True, type=Path)
    args = parser.parse_args()
    root = args.workdir.resolve()
    (root / 'downloads').mkdir(parents=True, exist_ok=True)
    inventory = root / 'inventory.json'
    if not inventory.exists():
        targets = json.loads(Path(__file__).with_name('corpus_sources.json').read_text(encoding='utf8'))
        for target in targets:
            metadata = root / 'downloads' / (target['key'] + '-tree.json')
            fetch('https://api.github.com/repos/' + target['repo'] + '/git/trees/' + target['commit'] + '?recursive=1',
                  metadata, target['key'] + '-inventory')
            tree = json.loads(metadata.read_text(encoding='utf8'))
            if tree.get('truncated'):
                raise ValueError('Truncated repository inventory')
            target['files'] = [{'path': entry['path'], 'sha': entry['sha'], 'size': entry['size']}
                               for entry in tree['tree'] if entry['type'] == 'blob' and entry['path'].startswith(target['prefix'])]
        inventory.write_text(json.dumps(targets), encoding='utf8')
    targets = json.loads(inventory.read_text(encoding='utf8'))
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        results = list(pool.map(lambda target: obtain(target, root), targets))
    (root / 'downloads.json').write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding='utf8')
    print('All archives downloaded and every selected Git blob verified.', flush=True)


if __name__ == '__main__':
    main()
