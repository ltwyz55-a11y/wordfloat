#!/usr/bin/env python3
"""Extract every valid entry tagged for one of the six supported exams."""
import csv, hashlib, json, pathlib, re, sys
ROOT = pathlib.Path(__file__).resolve().parents[1]
REVISION = 'bc015ed2e24a7abef49fc6dbbb7fe32c1dadaf8b'
BOOKS = [('zk','中考','基础英语'), ('gk','高考','高中英语'), ('cet4','四级','大学英语'),
         ('cet6','六级','进阶英语'), ('ielts','雅思','学术与生活'), ('toefl','托福','学术英语')]

def extract(path):
    words, memberships = {}, {key: set() for key, _, _ in BOOKS}
    excluded = []
    with open(path, encoding='utf-8-sig', newline='') as handle:
        for row in csv.DictReader(handle):
            tags = set(row['tag'].split()) & memberships.keys()
            if not tags:
                continue
            spelling = row['word'].strip()
            meaning = row['translation'].replace('\\n', '\n').strip()
            if not spelling or not meaning:
                excluded.append({'word': spelling, 'tags': sorted(tags)})
                continue
            key = spelling.casefold()
            frequency = int(row['frq']) if row['frq'].isdigit() and int(row['frq']) > 0 else None
            pos = re.match(r'^(n|v|vt|vi|adj|adv|prep|pron|conj|interj|num|art)\.', meaning)
            words.setdefault(key, dict(id=key, spelling=spelling, phonetic=row['phonetic'],
                partOfSpeech=pos.group(0) if pos else '', meaning=meaning, frequency=frequency))
            for tag in tags:
                memberships[tag].add(key)
    books = [dict(id=key, name=name, category='exam', subtitle=subtitle,
        source='https://github.com/skywind3000/ECDICT', version=REVISION,
        words=sorted(memberships[key], key=lambda w: (words[w]['frequency'] or 10**9, w))) for key, name, subtitle in BOOKS]
    result = dict(revision=REVISION, sha256=hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest(),
        excluded=excluded, books=books, words=list(words.values()))
    return result

if __name__ == '__main__':
    result = extract(sys.argv[1])
    (ROOT/'Data/exams.json').write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n')
    print(json.dumps({b['name']: len(b['words']) for b in result['books']}, ensure_ascii=False))
    print('Excluded entries without spelling/meaning:', len(result['excluded']))
