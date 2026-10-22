"""Fail CI when previously supported, unchanged public PDFs regress."""
import argparse
import csv
import json
from pathlib import Path


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('baseline',type=Path)
    parser.add_argument('results',type=Path)
    args=parser.parse_args()
    with args.baseline.open(encoding='utf-8-sig',newline='') as stream:
        expected={(r['corpus'],r['source']):r for r in csv.DictReader(stream)}
    current=[json.loads(line) for line in args.results.read_text(encoding='utf8').splitlines()]
    regressions=[]
    for row in current:
        prior=expected.get((row['corpus'],row['source']))
        if prior is None or prior['git_sha']!=row['git_sha']:
            regressions.append({'source':row['source'],'reason':'Corpus identity changed; review/update the pinned baseline'})
            continue
        state=row['status']
        old=prior['status']
        reason=None
        if old in ('ok','ok_signed') and state!=old:
            reason='Previously opened/saved case no longer completes'
        elif state=='crash' and old!='crash':
            reason='New native process failure'
        elif prior['merge_preserved']=='True' and row.get('merge_preserved') is not True:
            reason='Previously preserved merged page changed'
        elif prior['annotation_preserved']=='True' and row.get('annotation_preserved') is not True:
            reason='Previously preserved annotation changed'
        if reason: regressions.append({'source':row['source'],'reason':reason,'old':old,'new':state})
    if len(current)!=len(expected): regressions.append({'reason':'Corpus path count changed'})
    report={'checked':len(current),'regressions':regressions}
    args.results.with_name('regression-comparison.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print(json.dumps(report))
    if regressions: raise SystemExit(1)


if __name__=='__main__': main()
