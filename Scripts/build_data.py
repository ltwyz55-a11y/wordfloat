#!/usr/bin/env python3
"""Build a deterministic offline SQLite vocabulary from reviewed source files."""
import csv, json, pathlib, sqlite3, tempfile, os
ROOT = pathlib.Path(__file__).resolve().parents[1]
TARGET = ROOT/'App/Core/Resources/vocabulary.sqlite'
PROFESSIONAL = [('automotive','汽车设计','造型 · 布置 · 车身'), ('industrial','工业设计','产品 · 体验 · 制造'), ('materials','材料','性能 · 加工 · 表征')]

def build():
    data = json.loads((ROOT/'Data/exams.json').read_text())
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary = tempfile.mkstemp(suffix='.sqlite', dir=TARGET.parent)
    os.close(fd)
    db = sqlite3.connect(temporary)
    counts = {}
    try:
        db.executescript((ROOT/'Data/schema.sql').read_text())
        for word in data['words']:
            db.execute('INSERT INTO words VALUES (?,?,?,?,?,?)', [word[k] for k in ('id','spelling','phonetic','partOfSpeech','meaning','frequency')])
        for order, book in enumerate(data['books']):
            db.execute('INSERT INTO books VALUES (?,?,?,?,?,?,?)', [book[k] for k in ('id','name','category','subtitle','source','version')]+[order])
            db.executemany('INSERT INTO entries VALUES (?,?,NULL,?)', [(book['id'], key, i) for i,key in enumerate(book['words'])])
            counts[book['id']] = len(book['words'])
        for order, (key,name,subtitle) in enumerate(PROFESSIONAL, 6):
            path = ROOT/f'Data/{key}.tsv'
            if not path.exists():
                continue
            rows = list(csv.DictReader(path.open(encoding='utf-8'), delimiter='\t'))
            assert len(rows) >= 300, f'{key}: requires at least 300 core terms'
            assert len({r['word'].strip().casefold() for r in rows}) == len(rows), f'{key}: duplicate terms'
            db.execute('INSERT INTO books VALUES (?,?,?,?,?,?,?)', (key,name,'professional',subtitle,'浮词原创核心术语释义，2026-10', '1.0', order))
            for i,row in enumerate(sorted(rows, key=lambda r:r['word'].casefold())):
                spelling, meaning = row['word'].strip(), row['meaning'].strip()
                assert spelling and meaning and any('\u4e00' <= c <= '\u9fff' for c in meaning)
                wordid = spelling.casefold()
                db.execute('INSERT OR IGNORE INTO words VALUES (?,?,\'\',?,?,NULL)', (wordid,spelling,row['pos'],meaning))
                db.execute('INSERT INTO entries VALUES (?,?,?,?)', (key,wordid,meaning,i))
            counts[key] = len(rows)
        learning = json.loads((ROOT/'Data/learning-details.json').read_text())
        assert learning['formatVersion'] == 1
        assert set(learning['words']) == {r[0] for r in db.execute('SELECT id FROM words')}
        for word_id, detail in sorted(learning['words'].items()):
            assert set(detail) == {'dictionaryMeaning','englishDefinition','senses','etymologies','examples','forms'}
            assert len({e['id'] for e in detail['examples']}) == len(detail['examples'])
            for example in detail['examples']:
                assert example['english'].strip() and example['chinese'].strip()
                assert example['attribution'] and example['sourceURL'].startswith('https://')
            for item in detail['senses'] + detail['etymologies']:
                assert item['text'].strip() and item['sourceURL'].startswith('https://')
            db.execute('INSERT INTO learning_details VALUES (?,?)',
                (word_id,json.dumps(detail,ensure_ascii=False,sort_keys=True)))
        assert db.execute('PRAGMA integrity_check').fetchone()[0] == 'ok'
        assert not db.execute('PRAGMA foreign_key_check').fetchall()
        actual = dict(db.execute('SELECT book_id,count(*) FROM entries GROUP BY book_id'))
        assert actual == counts, (actual, counts)
        assert not db.execute("SELECT id FROM words WHERE trim(meaning)='' OR trim(spelling)=''").fetchall()
        db.commit()
        db.execute('VACUUM')
        db.close()
        os.replace(temporary,TARGET)
        (ROOT/'Data/counts.json').write_text(json.dumps(counts,ensure_ascii=False,indent=2)+'\n')
        print(json.dumps(counts,ensure_ascii=False))
    except BaseException:
        db.close()
        os.unlink(temporary)
        raise

if __name__ == '__main__': build()
