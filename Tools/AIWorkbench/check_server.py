import copy
import json
import tempfile
import threading
import unittest
import urllib.error
import urllib.request
from pathlib import Path
import server

class BridgeChecks(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp=tempfile.TemporaryDirectory()
        cls.defaults=json.loads((server.HERE/'defaults.json').read_text())
        server.HERE=Path(cls.temp.name)
        server.CONFIG=server.HERE/'AgentTuning.json'
        cls.http=server.ThreadingHTTPServer(('127.0.0.1',0),server.Handler)
        cls.url=f'http://127.0.0.1:{cls.http.server_port}'
        threading.Thread(target=cls.http.serve_forever,daemon=True).start()
    @classmethod
    def tearDownClass(cls):
        cls.http.shutdown();cls.http.server_close();cls.temp.cleanup()
    def setUp(self): server.CONFIG.write_text(json.dumps(self.defaults))
    def post(self,body,**headers):
        data=json.dumps(body).encode()
        req=urllib.request.Request(self.url+'/api/config',data=data,headers={'Content-Type':'application/json','Origin':self.url,'X-Workbench-Token':server.TOKEN,**headers})
        try:
            with urllib.request.urlopen(req) as response:return response.status,json.load(response)
        except urllib.error.HTTPError as error:return error.code,json.load(error)
    def config(self):
        with urllib.request.urlopen(self.url+'/api/config') as response:return json.load(response)
    def test_valid_write_and_conflict(self):
        original=self.config();config=copy.deepcopy(original['config']);config['warden']['rearDelay']=1.25
        code,_=self.post({'config':config,'revision':original['revision']});self.assertEqual(code,200)
        self.assertEqual(json.loads(server.CONFIG.read_text())['warden']['rearDelay'],1.25)
        self.assertEqual(self.post({'config':config,'revision':original['revision']})[0],409)
    def test_reject_invalid_and_unauthorized(self):
        original=self.config();bad=copy.deepcopy(original['config']);bad['relay']['healAmount']=-1
        self.assertEqual(self.post({'config':bad,'revision':original['revision']})[0],400)
        self.assertEqual(self.post({'config':original['config'],'revision':original['revision']},Origin='https://example.com')[0],403)
        self.assertEqual(self.post({'config':original['config'],'revision':original['revision']},**{'X-Workbench-Token':'wrong'})[0],403)
        self.assertEqual(self.config()['revision'],original['revision'])
    def test_schema_limits(self):
        for value in [float('nan'),float('inf'),True,-2,99999]:
            config=copy.deepcopy(self.defaults);config['warden']['rearDelay']=value
            with self.assertRaises(ValueError):server.validate(config)
        config=copy.deepcopy(self.defaults);config['relay']['unknown']=1
        with self.assertRaises(ValueError):server.validate(config)

if __name__=='__main__': unittest.main(verbosity=2)
