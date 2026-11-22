#!/usr/bin/env python3
"""Import the user's numbered Cambridge listening PDF without inventing missing content.
Development dependency: pypdf. The application only needs the generated JSON.
"""
import argparse, hashlib, json, pathlib, re, unicodedata
POS = re.compile(r'(?<![a-z])(?:n|v|adj|adv|vt|vi|prep|pron|conj|int|num|art|aux|abbr)\.')

def parse_pages(pages):
    rows=[]
    for page,text in enumerate(pages,1):
        text=re.sub(r'\n\d+ of \d+\n花生酱罐罐.*','',text,flags=re.S)
        parts=re.split(r'(?m)^(\d+) ',text)
        for number,raw in zip(parts[1::2],parts[2::2]):
            match=POS.search(raw)
            if match: head,meaning=raw[:match.start()],raw[match.start():]
            elif int(number)==545: head,meaning=raw.rsplit(' 社交',1)[0],'社交'
            elif raw.strip()=='situational': head,meaning=raw,''
            else: raise ValueError('Unrecognized row '+number+': '+raw)
            # The first line can also include a single phonetic or the two accent columns.
            first=head.strip().splitlines()[0]
            word=re.split(r'\s*(?:英:|美:|/)',first,maxsplit=1)[0].strip()
            word=unicodedata.normalize('NFKC',word)
            phonetic=head.strip()[len(re.split(r'\s*(?:英:|美:|/)',first,maxsplit=1)[0]):]
            phonetic=unicodedata.normalize('NFKC',' '.join(phonetic.split()))
            meaning='\n'.join(line.strip() for line in meaning.strip().splitlines())
            assert re.fullmatch(r"[A-Za-z][A-Za-z '\-]*",word),(number,word)
            rows.append(dict(number=int(number),page=page,word=word,phonetic=phonetic,meaning=meaning,partOfSpeech=match.group() if match else '',missingMeaning=not bool(meaning)))
    assert [r['number'] for r in rows]==list(range(1,950)),'Missing or duplicated source row'
    return rows

if __name__=='__main__':
    from pypdf import PdfReader
    parser=argparse.ArgumentParser();parser.add_argument('pdf');parser.add_argument('--output',default='Data/cambridge-listening.json');args=parser.parse_args()
    path=pathlib.Path(args.pdf);reader=PdfReader(path);rows=parse_pages([p.extract_text() for p in reader.pages])
    data=dict(id='cambridge-listening-4-19',name='剑雅4–19听力答案词',category='exam',subtitle='用户导入 · 花生酱罐罐 · 原表顺序',sourceFile=path.name,sourceSHA256=hashlib.sha256(path.read_bytes()).hexdigest(),sourcePages=len(reader.pages),sourceRows=len(rows),version='2026-11-22',rows=rows)
    pathlib.Path(args.output).write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n')
    unique={r['word'].casefold() for r in rows}
    print(f'{len(rows)} source rows, {len(unique)} unique terms; missing meanings: '+str([r['number'] for r in rows if r['missingMeaning']]))
