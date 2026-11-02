"""Check the actual bundled NW.js runtime in a hidden disposable fixture app."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time

ROOT = Path(__file__).resolve().parent.parent
spec = importlib.util.spec_from_file_location('gkh', ROOT / 'llm-play/gkh.py')
gkh = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gkh)
with tempfile.TemporaryDirectory(prefix='gkh-nw-llm-') as temp:
    folder = Path(temp)
    app = folder / 'package.nw'
    app.mkdir()
    # This packaged executable prefers package.nw beside itself over command-line paths.
    # Read-only hard links give it a disposable app without changing the installation.
    for binary in ROOT.iterdir():
        if binary.is_file() and (binary.suffix.lower() in ('.dll', '.pak', '.dat', '.bin') or binary.name == 'gkh.exe'):
            os.link(binary, folder / binary.name)
    for asset_dir in ('locales', 'swiftshader'):
        for source in (ROOT / asset_dir).rglob('*'):
            target = folder / source.relative_to(ROOT)
            if source.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                os.link(source, target)
    (app / 'package.json').write_text(json.dumps({'name': 'gkh-llm-runtime-test', 'main': 'index.html',
        'nodejs': True, 'single-instance': False, 'window': {'show': False, 'fullscreen': False},
        'chromium-args': '--disable-gpu'}))
    (app / 'llm-bridge.js').write_bytes((ROOT / 'package.nw/js/llm-bridge.js').read_bytes())
    (app / 'index.html').write_text('''<!doctype html><html><body><script>
    var game={state:'updateSplash',actionQueue:[],turnCount:0,consoleMessages:[]};
    var map={},menu={},magic={},abilities={},hints={},title={},player={};
    var options={getValue:function(){return false}};
    var input={defaultKeys:{WAIT:{}},queueCommand:function(){game.turnCount++}};
    var $=function(){return {is:function(){return false}}};
    setTimeout(function(){nw.App.quit()},20000);
    </script><script src="llm-bridge.js"></script></body></html>''', encoding='utf-8')
    env = dict(os.environ, LOCALAPPDATA=temp)
    child = subprocess.Popen([str(folder / 'gkh.exe'), '--user-data-dir=' + str(folder / 'profile')],
        cwd=temp, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        creationflags=subprocess.CREATE_NO_WINDOW)
    try:
        connection = folder / 'GoldenKroneLLM/connection.json'
        deadline = time.monotonic() + 15
        while not connection.exists() and time.monotonic() < deadline:
            if child.poll() is not None:
                log = (folder / 'debug.log').read_text(errors='replace') if (folder / 'debug.log').exists() else ''
                raise AssertionError('Hidden NW fixture exited before starting bridge: ' + str(child.returncode) + ' ' + log)
            time.sleep(0.1)
        assert connection.exists(), 'Bundled NW.js did not produce a connection file'
        bridge = gkh.Bridge(connection)
        state = bridge.call('/state')
        assert state['state'] == 'updateSplash', 'NW loaded an unexpected package: ' + state['state']
        result = bridge.call('/action', {'type':'command','command':'WAIT',
            'requestId':'nw-test-turn','expectedRevision':state['revision']})
        assert result['settled'] and result['state']['turn'] == 1
        print('PASS: bundled gkh.exe/NW.js loaded bridge and completed authenticated HTTP observation/action in a hidden disposable app')
        child.wait(timeout=25)
    finally:
        if child.poll() is None:
            subprocess.run(['taskkill', '/PID', str(child.pid), '/T', '/F'], capture_output=True)
            child.wait(timeout=5)
        # NW utility/renderer processes can outlive the main process briefly.
        # Match this fixture's exact executable path; never stop the user's game.
        cleanup_env = dict(os.environ, GKH_LLM_FIXTURE_EXE=str(folder / 'gkh.exe'))
        subprocess.run(['powershell', '-NoProfile', '-Command',
            "Get-CimInstance Win32_Process -Filter \"Name = 'gkh.exe'\" | Where-Object { $_.ExecutablePath -eq $env:GKH_LLM_FIXTURE_EXE } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"],
            env=cleanup_env, capture_output=True, check=True)
        time.sleep(0.3)
