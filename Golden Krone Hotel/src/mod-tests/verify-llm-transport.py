"""Production HTTP server + CLI/MCP round trips. Engine fixture; no desktop/save access."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

ROOT = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('gkh', ROOT / 'llm-play/gkh.py')
gkh = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gkh)

with tempfile.TemporaryDirectory(prefix='gkh-llm-test-') as temp:
    server = subprocess.Popen(['node', str(ROOT / 'mod-tests/llm-transport-fixture.cjs'), temp], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    try:
        connection = Path(temp) / 'GoldenKroneLLM/connection.json'
        deadline = time.monotonic() + 10
        while not connection.exists() and time.monotonic() < deadline:
            if server.poll() is not None:
                raise AssertionError(server.stderr.read().decode())
            time.sleep(0.05)
        bridge = gkh.Bridge(connection)
        state = bridge.call('/state')
        assert state['state'] == 'updateSplash'
        info = json.loads(connection.read_text())
        def rejected(endpoint, body=None, headers=None, expected=403):
            request = urllib.request.Request(info['url'] + endpoint, data=body, headers=headers or {})
            try:
                urllib.request.urlopen(request)
                raise AssertionError('Unauthorized request accepted')
            except urllib.error.HTTPError as exc:
                assert exc.code == expected, (exc.code, expected)
        rejected('/state')
        auth = {'Authorization': 'Bearer ' + info['token']}
        rejected('/state', headers={**auth, 'Origin': 'https://example.com'})
        rejected('/state', headers={**auth, 'Host': 'example.com'})
        rejected('/action', b'x' * 9000, auth, 413)
        rejected('/action', b'{invalid', auth, 400)
        rejected('/control', b'null', auth, 400)
        rejected('/seed/state', headers=auth, expected=400)
        rejected('/seed/forecast?revision=0', headers=auth, expected=400)
        payload = {'type': 'command', 'command': 'WAIT', 'expectedRevision': state['revision'], 'requestId': 'transport-turn'}
        result = bridge.call('/action', payload)
        assert result['settled'] and result['state']['turn'] == 1
        assert bridge.call('/action', payload) == result
        assert bridge.call('/state')['turn'] == 1
        bridge.call('/control', {'enabled': False})
        rejected('/action', json.dumps({**payload, 'requestId': 'paused'}).encode(), auth, 400)
        bridge.call('/control', {'enabled': True})
        for invalid in ({'enabled':True,'exclusive':'yes'},{'enabled':True,'exclusive':False},{'exclusive':True}):
            rejected('/control',json.dumps(invalid).encode(),auth,400)
        claimed=bridge.call('/control',{'enabled':True,'exclusive':True})
        assert claimed['exclusive'] and claimed['enabled']
        assert json.loads((Path(temp)/'GoldenKroneLLM/control.json').read_text())=={'exclusive':True}
        assert bridge.call('/control',{'enabled':False})['exclusive']
        released=bridge.call('/control',{'enabled':False,'exclusive':False})
        assert not released['enabled'] and not released['exclusive'] and not released['releasing']
        bridge.call('/control',{'enabled':True})
        cli = subprocess.run([sys.executable, str(ROOT / 'llm-play/gkh.py'), '--connection', str(connection), 'state'], capture_output=True, text=True, check=True)
        assert json.loads(cli.stdout)['turn'] == 1
        mcp = subprocess.Popen([sys.executable, str(ROOT / 'llm-play/gkh.py'), '--connection', str(connection), 'mcp'], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True, encoding='utf-8')
        def rpc(method, params=None, rid=1):
            mcp.stdin.write(json.dumps({'jsonrpc': '2.0', 'id': rid, 'method': method, 'params': params or {}}) + '\n')
            mcp.stdin.flush()
            return json.loads(mcp.stdout.readline())
        assert rpc('initialize', {'protocolVersion': '2025-03-26'})['result']['capabilities']['tools'] == {}
        mcp.stdin.write('{"jsonrpc":"2.0","method":"notifications/initialized"}\n')
        mcp.stdin.flush()
        names = {t['name'] for t in rpc('tools/list')['result']['tools']}
        assert names == {'gkh_observe','gkh_act','gkh_control','gkh_seed_observe','gkh_seed_forecast','gkh_seed_control','gkh_seed_act'}
        observed = rpc('tools/call', {'name': 'gkh_observe', 'arguments': {}})['result']
        state = json.loads(observed['content'][0]['text'])
        result = rpc('tools/call', {'name': 'gkh_act', 'arguments': {**payload, 'requestId': 'mcp-turn', 'expectedRevision': state['revision']}})['result']
        assert not result.get('isError') and json.loads(result['content'][0]['text'])['state']['turn'] == 2
        assert rpc('tools/call', {'name': 'missing'})['result']['isError']
        assert rpc('tools/call', {'name': 'gkh_act', 'arguments': {}})['result']['isError']
        assert rpc('tools/call', {'name': 'gkh_seed_observe', 'arguments': {}})['result']['isError']
        assert rpc('tools/call', {'name': 'gkh_seed_forecast', 'arguments': {'expectedRevision': -1}})['result']['isError']
        assert rpc('tools/call', {'name': 'gkh_seed_forecast', 'arguments': {'expectedRevision': state['revision']}})['result']['isError']
        assert rpc('tools/call', {'name': 'gkh_seed_control', 'arguments': {'operation': 'undo', 'expectedRevision':state['revision'], 'requestId':'no-harness'}})['result']['isError']
        assert rpc('tools/call', {'name': 'gkh_seed_act', 'arguments': {**payload,'requestId':'seed-no-harness','expectedRevision':state['revision']}})['result']['isError']
        assert {t['name'] for t in gkh.mode_tools('normal')} == {'gkh_observe','gkh_act','gkh_control'}
        assert {t['name'] for t in gkh.mode_tools('seed')} == {'gkh_seed_observe','gkh_seed_act','gkh_seed_forecast','gkh_seed_control','gkh_control'}
        blocked=gkh.rpc(bridge, {'jsonrpc':'2.0','id':5,'method':'tools/call','params':{'name':'gkh_seed_observe','arguments':{}}}, 'normal')
        assert blocked['result']['isError']
        for tool_mode in ('normal','seed'):
            messages=[{'jsonrpc':'2.0','id':1,'method':'tools/list'},
                      {'jsonrpc':'2.0','id':2,'method':'tools/call','params':{'name':'gkh_seed_observe' if tool_mode=='normal' else 'gkh_act','arguments':payload}}]
            filtered=subprocess.run([sys.executable,str(ROOT/'llm-play/gkh.py'),'--connection',str(connection),'mcp','--tool-mode',tool_mode],
                input=''.join(json.dumps(m)+'\n' for m in messages),capture_output=True,text=True,check=True)
            replies=[json.loads(line) for line in filtered.stdout.splitlines()]
            assert {t['name'] for t in replies[0]['result']['tools']} == {t['name'] for t in gkh.mode_tools(tool_mode)}
            assert replies[1]['result']['isError'] and not filtered.stderr
        blocked=gkh.rpc(bridge, {'jsonrpc':'2.0','id':6,'method':'tools/call','params':{'name':'gkh_act','arguments':payload}}, 'seed')
        assert blocked['result']['isError']
        assert rpc('missing')['error']['code'] == -32601
        mcp.stdin.write('invalid\n')
        mcp.stdin.flush()
        assert json.loads(mcp.stdout.readline())['error']['code'] == -32700
        mcp.stdin.close()
        assert mcp.wait(timeout=5) == 0
        assert not mcp.stderr.read()
        cli=subprocess.run([sys.executable,str(ROOT/'llm-play/gkh.py'),'--connection',str(connection),'control','takeover'],capture_output=True,text=True,check=True)
        assert json.loads(cli.stdout)['exclusive']
        # Reopen the production bridge against its persisted ownership preference.
        server.stdin.close();assert server.wait(timeout=5)==0
        assert not server.stderr.read()
        server=subprocess.Popen(['node',str(ROOT/'mod-tests/llm-transport-fixture.cjs'),temp],stdin=subprocess.PIPE,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
        deadline=time.monotonic()+10
        while not connection.exists() and time.monotonic()<deadline:time.sleep(.05)
        restored=bridge.call('/state')
        assert restored['exclusive'] and not restored['enabled'],'Restart lost watch-only protection'
        cli=subprocess.run([sys.executable,str(ROOT/'llm-play/gkh.py'),'--connection',str(connection),'control','release'],capture_output=True,text=True,check=True)
        assert not json.loads(cli.stdout)['exclusive']
        print('PASS: real loopback transport, authentication, Host/Origin rejection, size/malformed inputs, idempotency, pause, CLI, MCP handshake/list/observe/action/errors')
    finally:
        server.stdin.close()
        try:
            server.wait(timeout=5)
        except subprocess.TimeoutExpired:
            server.terminate()
            server.wait(timeout=5)
        assert not server.stderr.read(), 'Transport fixture emitted an error'
