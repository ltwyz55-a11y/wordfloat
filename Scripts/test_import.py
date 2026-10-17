import csv, pathlib, tempfile, unittest
from import_exam import extract

class ImportTests(unittest.TestCase):
    def test_all_tag_memberships_and_invalid_entries(self):
        with tempfile.TemporaryDirectory() as folder:
            path = pathlib.Path(folder)/'sample.csv'
            with path.open('w',newline='') as f:
                writer=csv.DictWriter(f,fieldnames=['word','translation','tag','frq','phonetic'])
                writer.writeheader()
                writer.writerows([
                    dict(word='draft',translation='n. 草稿',tag='zk cet4',frq='22',phonetic=''),
                    dict(word='Draft',translation='n. 草稿',tag='cet4 toefl',frq='22',phonetic=''),
                    dict(word='empty',translation='',tag='gk',frq='0',phonetic=''),
                    dict(word='unrelated',translation='无关',tag='gre',frq='5',phonetic='')])
            result=extract(path)
            counts={b['id']:len(b['words']) for b in result['books']}
            self.assertEqual(counts,dict(zk=1,gk=0,cet4=1,cet6=0,ielts=0,toefl=1))
            self.assertEqual(len(result['words']),1)
            self.assertEqual(len(result['excluded']),1)
            self.assertEqual(result['words'][0]['partOfSpeech'],'n.')

if __name__ == '__main__': unittest.main()
