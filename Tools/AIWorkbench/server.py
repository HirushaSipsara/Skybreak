"""Local, dependency-free workbench. Only its validated AI JSON can be written."""
import argparse
import hashlib
import json
import math
import os
import secrets
import tempfile
import threading
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler
from pathlib import Path
from urllib.parse import urlsplit, parse_qs

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
CONFIG = ROOT / 'Assets/Resources/AI/AgentTuning.json'
SCHEMA = json.loads((HERE / 'schema.json').read_text())
TOKEN = secrets.token_urlsafe(32)
SAVE_LOCK = threading.Lock()
SOURCES = {
 'warden': ROOT / 'Assets/Scripts/AI/RiftWarden/RiftWardenBrain.cs',
 'relay': ROOT / 'Assets/Scripts/AI/Relay/RelayBrain.cs',
 'movement': ROOT / 'Assets/Scripts/AgentMovement/NavMeshMotor.cs',
 'loader': ROOT / 'Assets/Scripts/AI/Shared/AgentTuning.cs',
}

def validate(value):
    if not isinstance(value, dict) or set(value) != {'schemaVersion', *SCHEMA} or type(value['schemaVersion']) is not int or value['schemaVersion'] != 1:
        raise ValueError('Expected schemaVersion 1 and all three tuning sections.')
    for group, fields in SCHEMA.items():
        if not isinstance(value[group], dict) or set(value[group]) != {f['key'] for f in fields}:
            raise ValueError(f'{group}: missing or unknown parameters.')
        for f in fields:
            n = value[group][f['key']]
            if isinstance(n, bool) or not isinstance(n, (int, float)) or not math.isfinite(n) or not f['min'] <= n <= f['max']:
                raise ValueError(f"{group}.{f['key']} must be between {f['min']} and {f['max']}.")
    return value

class Handler(BaseHTTPRequestHandler):
    def allowed_host(self):
        return self.headers.get('Host') in {f'127.0.0.1:{self.server.server_port}', f'localhost:{self.server.server_port}'}
    def respond(self, data, code=200, content='application/json; charset=utf-8'):
        raw = data if isinstance(data, bytes) else json.dumps(data).encode()
        self.send_response(code)
        self.send_header('Content-Type', content)
        self.send_header('Content-Length', str(len(raw)))
        self.send_header('Cache-Control', 'no-store')
        self.send_header('X-Content-Type-Options', 'nosniff')
        self.send_header('Content-Security-Policy', "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'")
        self.end_headers()
        self.wfile.write(raw)
    def do_GET(self):
        if not self.allowed_host(): return self.respond({'error':'Local host only'}, 403)
        url = urlsplit(self.path)
        if url.path == '/api/config':
            raw=CONFIG.read_bytes()
            return self.respond({'config':json.loads(raw), 'revision':hashlib.sha256(raw).hexdigest(), 'token':TOKEN})
        if url.path == '/api/source':
            source=SOURCES.get(parse_qs(url.query).get('agent',[''])[0])
            if source: return self.respond({'path':source.relative_to(ROOT).as_posix(), 'code':source.read_text()})
            return self.respond({'error':'Unknown source'},404)
        path = 'index.html' if url.path == '/' else url.path.lstrip('/')
        types={'index.html':'text/html; charset=utf-8', 'style.css':'text/css; charset=utf-8', 'app.js':'text/javascript; charset=utf-8', 'engine.js':'text/javascript; charset=utf-8', 'data.js':'text/javascript; charset=utf-8', 'schema.json':'application/json', 'defaults.json':'application/json'}
        if path not in types: return self.respond({'error':'Not found'},404)
        return self.respond((HERE/path).read_bytes(), content=types[path])
    def do_POST(self):
        try: length=int(self.headers.get('Content-Length','0'))
        except ValueError: return self.respond({'error':'Invalid request size'},400)
        if length <= 0 or length > 16000: return self.respond({'error':'Invalid request size'},413)
        raw_body=self.rfile.read(length)
        if not self.allowed_host() or self.path != '/api/config': return self.respond({'error':'Not allowed'},403)
        expected = 'http://' + self.headers.get('Host','')
        if self.headers.get('Origin') != expected or self.headers.get('X-Workbench-Token') != TOKEN:
            return self.respond({'error':'Open the local workbench to save tuning.'},403)
        if self.headers.get('Content-Type') != 'application/json': return self.respond({'error':'JSON required'},415)
        try:
            body=json.loads(raw_body)
            if not isinstance(body,dict): raise ValueError('Expected an object')
            with SAVE_LOCK:
                current=CONFIG.read_bytes()
                if body.get('revision') != hashlib.sha256(current).hexdigest():
                    return self.respond({'error':'The Unity configuration changed since you loaded it. Reload from Unity before saving.'},409)
                data=validate(body.get('config'))
                (HERE/'.previous-config.json').write_bytes(current)
                raw=(json.dumps(data,indent=2)+'\n').encode()
                # The asset's .meta stays untouched, preserving its Unity GUID.
                with tempfile.NamedTemporaryFile(dir=CONFIG.parent, suffix='.tmp', delete=False) as f:
                    f.write(raw); temporary=f.name
                os.replace(temporary,CONFIG)
            self.respond({'revision':hashlib.sha256(raw).hexdigest(), 'message':'Saved to Unity. Active Editor agents read tactical values within about one second. Health changes apply at next start/reset.'})
        except (ValueError, TypeError, KeyError, json.JSONDecodeError) as exc: self.respond({'error':str(exc)},400)
    def log_message(self, *_): pass

if __name__ == '__main__':
    parser=argparse.ArgumentParser(); parser.add_argument('--port',type=int,default=8765); args=parser.parse_args()
    server=ThreadingHTTPServer(('127.0.0.1',args.port),Handler)
    print(f'Skybreak AI Workbench: http://127.0.0.1:{args.port}',flush=True)
    server.serve_forever()
