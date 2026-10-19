import unittest,tempfile,json,pathlib
from import_learning import blank,wiki_import,example_index,sentence_matches,select_examples

class LearningImportTests(unittest.TestCase):
    def test_word_boundaries_phrases_and_real_inflections(self):
        words={'he':{'spelling':'he'},'design':{'spelling':'design'},'glass transition':{'spelling':'glass transition'}}
        details={key:blank() for key in words};details['design']['forms']=[{'label':'过去式','text':'designed'}]
        index=example_index(words,details)
        self.assertEqual(sentence_matches('The glass transition was measured.',index),{'glass transition'})
        self.assertEqual(sentence_matches('He designed it.',index),{'he','design'})
        self.assertEqual(sentence_matches('The designer arrived.',index),set())
    def test_full_glosses_etymology_and_attributed_examples(self):
        row=dict(word='design',lang_code='en',pos='noun',etymology_texts=['来源说明'],senses=[dict(glosses=['含义一','含义二'],tags=['archaic'],examples=[dict(text='A design works.',translation='一个设计可行。'),dict(text='A quote.',translation='引文。',ref='Published book')])])
        with tempfile.TemporaryDirectory() as folder:
            path=pathlib.Path(folder)/'data.jsonl';path.write_text(json.dumps(row)+'\n')
            details={'design':blank()};wiki_import([path,path],details)
            self.assertEqual(len(details['design']['senses']),1)
            self.assertIn('含义一；含义二',details['design']['senses'][0]['text'])
            self.assertEqual(len(details['design']['etymologies']),1)
            self.assertEqual(len(details['design']['examples']),0)
            row['senses'][0]['tags']=[]
            path.write_text(json.dumps(row)+'\n')
            wiki_import([path],details)
            self.assertEqual(len(details['design']['examples']),1)
            path.write_text('{"word":')
            with self.assertRaises(ValueError):wiki_import([path],details)
    def test_example_deduplication(self):
        examples=[dict(english='This is a useful example.',chinese=zh) for zh in ['这是一个有用的例句。','这句很有用。']]
        self.assertEqual(len(select_examples(examples)),1)

if __name__=='__main__':unittest.main()
