#!/usr/bin/env python3
"""Build attributed offline learning details; never infer etymologies from spelling."""
import argparse, collections, csv, hashlib, json, pathlib, re, unicodedata, zipfile
from urllib.parse import quote
ROOT = pathlib.Path(__file__).resolve().parents[1]
POS = {'noun':'名词','verb':'动词','adj':'形容词','adv':'副词','pron':'代词','prep':'介词','conj':'连词','intj':'感叹词','prefix':'前缀','suffix':'后缀','name':'专有名词','det':'限定词','num':'数词','article':'冠词','phrase':'短语','abbrev':'缩写','unknown':''}
TAGS = {'obsolete':'已废弃','archaic':'古语','rare':'罕用','figuratively':'比喻','slang':'俚语','informal':'非正式','formal':'正式','vulgar':'粗俗','transitive':'及物','intransitive':'不及物','uncountable':'不可数','countable':'可数','UK':'英式','US':'美式'}
FORM_LABELS = {'p':'过去式','d':'过去分词','i':'现在分词','3':'第三人称单数','s':'复数','r':'比较级','t':'最高级','0':'原形'}

def tokens(text):
    return re.findall(r"[a-z0-9]+(?:[-'][a-z0-9]+)*", unicodedata.normalize('NFKC', text).casefold().replace('’', "'"))

def blank():
    return dict(dictionaryMeaning='',englishDefinition='',senses=[],etymologies=[],examples=[],forms=[])

def vocabulary():
    result = {w['id']: w for w in json.loads((ROOT/'Data/exams.json').read_text())['words']}
    for book in ['automotive','industrial','materials']:
        with (ROOT/f'Data/{book}.tsv').open() as f:
            for row in csv.DictReader(f, delimiter='\t'):
                key=row['word'].strip().casefold()
                result.setdefault(key,dict(id=key,spelling=row['word'].strip(),meaning=row['meaning'].strip()))
    return result

def load_ecdict(path, words):
    output={key:blank() for key in words}
    found=set()
    with open(path,encoding='utf-8-sig',newline='') as f:
        for row in csv.DictReader(f):
            key=row['word'].strip().casefold()
            if key not in output or key in found: continue
            found.add(key); detail=output[key]
            detail['dictionaryMeaning']=row['translation'].replace('\\n','\n').strip()
            detail['englishDefinition']=row['definition'].replace('\\n','\n').strip()
            for item in row.get('exchange','').split('/'):
                label,sep,value=item.partition(':')
                if sep and label in FORM_LABELS and value.strip():
                    detail['forms'].append(dict(label=FORM_LABELS[label],text=value.strip()))
    return output

def add_unique(items, value):
    if value not in items: items.append(value)

def wiki_import(paths, output):
    source_rows=0
    for path in paths:
        with open(path,encoding='utf-8') as f:
            for number,line in enumerate(f,1):
                # Fail rather than silently accepting a truncated download.
                row=json.loads(line)
                assert row.get('lang_code')=='en', (path,number,'unexpected language')
                source_rows+=1
                key=row['word'].strip().casefold()
                if key not in output: continue
                detail=output[key]
                url='https://zh.wiktionary.org/wiki/'+quote(row['word'],safe='')
                for text in row.get('etymology_texts',[]):
                    if text.strip(): add_unique(detail['etymologies'],dict(text=text.strip(),sourceURL=url))
                for sense in row.get('senses',[]):
                    gloss='；'.join(g.strip() for g in sense.get('glosses',[]) if g.strip())
                    labels=sense.get('raw_tags',[])+[TAGS[t] for t in sense.get('tags',[]) if t in TAGS]
                    if labels: gloss='（'+'、'.join(dict.fromkeys(labels))+'）'+gloss if gloss else ''
                    if gloss: add_unique(detail['senses'],dict(partOfSpeech=POS.get(row['pos'],row['pos']),text=gloss,sourceURL=url))
                    for example in sense.get('examples',[]):
                        en=example.get('text','').strip(); zh=example.get('translation','').strip()
                        # Use dictionary usage examples, not third-party book/news quotations.
                        if en and zh and not example.get('ref') and len(en)<=300 and re.search('[\u3400-\u9fff]',zh):
                            ident='wiki-'+hashlib.sha256((url+en+zh).encode()).hexdigest()[:20]
                            add_unique(detail['examples'],dict(id=ident,english=en,chinese=zh,attribution='维基词典贡献者 · CC BY-SA 4.0',sourceURL=url))
    return source_rows

def example_index(words, details):
    index=collections.defaultdict(set)
    for key,word in words.items():
        variants=[word['spelling']]+[f['text'] for f in details[key]['forms']]
        for variant in variants:
            term=tuple(tokens(variant))
            if term: index[term].add(key)
    return index

def sentence_matches(english,index):
    parts=tokens(english); result=set()
    lengths={len(k) for k in index}
    for length in lengths:
        for start in range(len(parts)-length+1): result.update(index.get(tuple(parts[start:start+length]),()))
    return result

def tatoeba_import(path,words,details):
    index=example_index(words,details)
    # Compute lengths once for a full import, avoiding repeated index scans per sentence.
    lengths=sorted({len(k) for k in index})
    rows=0
    with zipfile.ZipFile(path) as archive:
        for line in archive.read('cmn.txt').decode('utf-8').splitlines():
            en,zh,attribution=line.split('\t'); rows+=1
            parts=tokens(en)
            if not 3<=len(parts)<=30 or len(en)>240: continue
            ids=re.findall(r'#(\d+)',attribution)
            assert len(ids)==2 and 'CC-BY 2.0' in attribution
            example=dict(id='tatoeba-'+'-'.join(ids),english=en,chinese=zh,attribution=attribution,sourceURL='https://tatoeba.org/en/sentences/show/'+ids[0])
            matches=set()
            for length in lengths:
                for start in range(len(parts)-length+1): matches.update(index.get(tuple(parts[start:start+length]),()))
            for key in matches: details[key]['examples'].append(example)
    return rows

def select_examples(examples,maximum=3):
    chosen=[]; seen=set()
    for item in sorted(examples,key=lambda e:(abs(len(tokens(e['english']))-9),len(e['english']),e['english'],e['chinese'])):
        key=item['english'].casefold()
        if key in seen: continue
        seen.add(key); chosen.append(item)
        if len(chosen)==maximum:break
    return chosen

def build(ecdict,wiki,tatoeba):
    words=vocabulary()
    expected=json.loads((ROOT/'Data/exams.json').read_text())['sha256']
    assert hashlib.sha256(pathlib.Path(ecdict).read_bytes()).hexdigest()==expected, 'ECDICT version mismatch'
    details=load_ecdict(ecdict,words)
    wiki_rows=wiki_import(wiki,details)
    tatoeba_rows=tatoeba_import(tatoeba,words,details)
    for key,detail in details.items(): detail['examples']=select_examples(detail['examples'])
    counts=dict(words=len(details),dictionaryMeaning=sum(bool(d['dictionaryMeaning']) for d in details.values()),
        etymologies=sum(bool(d['etymologies']) for d in details.values()),exampleWords=sum(bool(d['examples']) for d in details.values()),
        examples=sum(len(d['examples']) for d in details.values()),supplementarySenses=sum(len(d['senses']) for d in details.values()),wikiRows=wiki_rows,tatoebaRows=tatoeba_rows)
    sources=[dict(file=pathlib.Path(p).name,sha256=hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()) for p in [ecdict,*wiki,tatoeba]]
    result=dict(formatVersion=1,generated='2026-10-19',sources=sources,counts=counts,words=details)
    (ROOT/'Data/learning-details.json').write_text(json.dumps(result,ensure_ascii=False,sort_keys=True,indent=2)+'\n')
    print(json.dumps(counts,ensure_ascii=False))

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--ecdict',required=True);parser.add_argument('--wiki',nargs='+',required=True);parser.add_argument('--tatoeba',required=True)
    args=parser.parse_args();build(args.ecdict,args.wiki,args.tatoeba)
