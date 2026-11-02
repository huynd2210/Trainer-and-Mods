"""Hidden bundled NW.js, real game, no RAF or Steam, isolated storage."""
import importlib.util
import ctypes
from ctypes import wintypes
import json
import os
from pathlib import Path
import subprocess
import tempfile
import time

ROOT=Path(__file__).resolve().parent.parent
spec=importlib.util.spec_from_file_location('gkh',ROOT/'llm-play/gkh.py')
gkh=importlib.util.module_from_spec(spec);spec.loader.exec_module(gkh)
with tempfile.TemporaryDirectory(prefix='gkh-background-test-') as temp:
    folder=Path(temp);app=folder/'package.nw';app.mkdir()
    for source in ROOT.iterdir():
        if source.is_file() and (source.suffix.lower() in ('.dll','.pak','.dat','.bin') or source.name=='gkh.exe'):
            os.link(source,folder/source.name)
    for name in ('locales','swiftshader'):
        for source in (ROOT/name).rglob('*'):
            target=folder/source.relative_to(ROOT)
            if source.is_dir():target.mkdir(parents=True,exist_ok=True)
            else:target.parent.mkdir(parents=True,exist_ok=True);os.link(source,target)
    # Never link the user's saves/cloud/morgues or native Steam integrations.
    for parent,dirs,files in os.walk(ROOT/'package.nw'):
        relative=Path(parent).relative_to(ROOT/'package.nw')
        dirs[:]=[d for d in dirs if d!='node_modules' and not (relative==Path('.') and d in ('cloud','morgue','saves','lib','lib_win64'))]
        target=app/relative;target.mkdir(parents=True,exist_ok=True)
        for name in files:
            if (relative==Path('.') and name in ('index.html','package.json')) or (relative==Path('js') and name=='steam.js'):continue
            os.link(Path(parent)/name,target/name)
    (app/'js/steam.js').write_text("steam={initialized:true,username:null,init:function(){return true},getUsername:function(){return 'BACKGROUND_TEST'},setCheevo:function(){},clearCheevo:function(){}};",encoding='utf-8')
    manifest=json.loads((ROOT/'package.nw/package.json').read_text())
    manifest.update(name='gkh-background-fixture',**{'single-instance':False,'window':{'show':False,'fullscreen':False,'width':1440,'height':1000},
        'chromium-args':'--in-process-gpu --disable-transparency --use-gl=angle --use-angle=swiftshader --enable-webgl'})
    (app/'package.json').write_text(json.dumps(manifest))
    html=(ROOT/'package.nw/index.html').read_text(encoding='utf-8')
    html=html.replace('<head>','<head><script>window.requestAnimationFrame=function(){return 0};</script>',1)
    html=html.replace('</body>','<script src="fixture-driver.js"></script></body>',1)
    (app/'index.html').write_text(html,encoding='utf-8')
    (app/'fixture-driver.js').write_text('''(function(){
        var timers=require('timers'),fs=require('fs'),path=require('path');
        function file(name){return path.join(process.env.LOCALAPPDATA,name)}
        var started=false,fatal=false;window.__backgroundFrames=0;
        var original=advanceGameFrame;
        advanceGameFrame=function(t,remote){if(remote)window.__backgroundFrames++;return original(t,remote)};
        timers.setTimeout(function(){nw.App.quit()},180000);
        timers.setInterval(function(){
            try{
                if(!started&&game.imageLoaded&&game.state==='updateSplash'){
                    started=true;seedAids.foresight=false;titleScreen();game.seed='nw-background-fixture';player.class=0;startGame(false,true);
                }
                fs.writeFileSync(file('fixture-startup.json'),JSON.stringify({started:started,state:game.state,imageLoaded:game.imageLoaded,bridge:!!window.gkhLLM}));
                if(window.pc&&pc.tile){
                    // Boot the isolated fixture, then suspend all unsolicited frames.
                    if(!seedAids.active||gkhLLM.observe().busy){
                        if(!fs.existsSync(file('fixture-status.json'))){advanceGameFrame(performance.now(),true);return;}
                    }
                    if(!fatal&&fs.existsSync(file('fixture-fatal.json'))){fatal=true;pc.hp=.1;player.hunger=-1;}
                    fs.writeFileSync(file('fixture-status.json'),JSON.stringify({state:game.state,turn:game.turnCount,frames:window.__backgroundFrames,
                        hidden:document.hidden,fatal:fatal,queue:game.actionQueue.length,hp:pc.hp}));
                }
            }catch(e){fs.writeFileSync(file('fixture-error.txt'),String(e.stack||e))}
        },100);
    }());''',encoding='utf-8')
    child=subprocess.Popen([str(folder/'gkh.exe'),'--user-data-dir='+str(folder/'profile')],cwd=temp,
        env=dict(os.environ,LOCALAPPDATA=temp),stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL,creationflags=subprocess.CREATE_NO_WINDOW)
    try:
        connection=folder/'GoldenKroneLLM/connection.json';status=folder/'fixture-status.json';deadline=time.monotonic()+60
        while (not connection.exists() or not status.exists()) and time.monotonic()<deadline:
            if (folder/'fixture-error.txt').exists():raise AssertionError((folder/'fixture-error.txt').read_text())
            if child.poll() is not None:raise AssertionError('Hidden game exited: '+str(child.returncode))
            time.sleep(.1)
        assert connection.exists() and status.exists(),'Hidden game failed to initialize: '+((folder/'fixture-startup.json').read_text() if (folder/'fixture-startup.json').exists() else 'no driver')+' '+((folder/'debug.log').read_text(errors='replace')[-5000:] if (folder/'debug.log').exists() else '')
        bridge=gkh.Bridge(connection)
        owned=bridge.call('/control',{'enabled':True,'exclusive':True})
        assert owned['exclusive'] and owned['enabled']
        s=bridge.call('/state');print('Hidden NW initialized',flush=True)
        assert s['state']=='running' and s['seedHarness']['active'] and not s['busy'],{k:s[k] for k in ('state','seedHarness','busy','turn','menu')}
        # NW.js may report document.hidden=false even for show:false. Verify
        # actual native-window visibility and focus instead of that DOM flag.
        process_env=dict(os.environ,GKH_LLM_FIXTURE_EXE=str(folder/'gkh.exe'))
        process_ids=set(json.loads(subprocess.check_output(['powershell','-NoProfile','-Command',
            "ConvertTo-Json -Compress -InputObject @(Get-CimInstance Win32_Process -Filter \"Name = 'gkh.exe'\" | Where-Object { $_.ExecutablePath -eq $env:GKH_LLM_FIXTURE_EXE } | ForEach-Object { $_.ProcessId })"],env=process_env,text=True)))
        user32=ctypes.windll.user32
        user32.GetForegroundWindow.restype=wintypes.HWND
        user32.IsWindowVisible.argtypes=[wintypes.HWND]
        user32.GetWindowThreadProcessId.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.DWORD)]
        def assert_hidden():
            visible=[]
            @ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HWND,wintypes.LPARAM)
            def visit(hwnd,param):
                pid=wintypes.DWORD();user32.GetWindowThreadProcessId(hwnd,ctypes.byref(pid))
                if pid.value in process_ids and user32.IsWindowVisible(hwnd):visible.append(int(hwnd))
                return True
            user32.EnumWindows(visit,0)
            pid=wintypes.DWORD();user32.GetWindowThreadProcessId(user32.GetForegroundWindow(),ctypes.byref(pid))
            assert not visible and pid.value not in process_ids,'Fixture became visible or took focus'
        assert_hidden()
        def act(extra,rid):
            observed=bridge.call('/state')
            result=bridge.call('/seed/control' if extra['type']=='seed' else '/seed/action',
                {'expectedRevision':observed['revision'],'requestId':rid,**extra})
            assert_hidden();return result
        initial=s['turn']
        result=act({'type':'command','command':'WAIT'},'background-wait')
        assert result['settled'] and result['state']['turn']==initial+1
        result=act({'type':'seed','operation':'undo'},'background-undo')
        assert result['settled'] and result['state']['turn']==initial
        result=act({'type':'seed','operation':'redo'},'background-redo')
        assert result['settled'] and result['state']['turn']==initial+1
        print('Hidden WAIT settled',flush=True)
        act({'type':'command','command':'MENU'},'background-menu')
        result=act({'type':'choose','index':0},'background-choice')
        assert result['settled'] and result['state']['menu']['id']=='character sheet' and 'Sorina' in result['state']['menu']['title']+result['state']['menu']['text'],result['state']['menu']
        result=act({'type':'command','command':'ESCAPE'},'background-close')
        assert result['settled'] and not result['state']['menu']['open']
        print('Hidden menu/choice/close settled',flush=True)
        current=bridge.call('/seed/state');forecast=bridge.call('/seed/forecast?revision='+str(current['state']['revision']))
        assert len(forecast['forecasts'])==9
        print('Hidden forecasts complete',flush=True)
        before=bridge.call('/state')['turn'];time.sleep(.3)
        assert bridge.call('/state')['turn']==before,'Idle hidden game spent a turn'
        (folder/'fixture-fatal.json').write_text('{}')
        deadline=time.monotonic()+5
        while not json.loads(status.read_text()).get('fatal') and time.monotonic()<deadline:time.sleep(.1)
        try:act({'type':'command','command':'WAIT'},'background-fatal');raise AssertionError('Fatal hidden action executed')
        except RuntimeError as e:assert 'leads to death' in str(e),str(e)
        final=bridge.call('/state');info=json.loads(status.read_text())
        assert final['player']['hp']==.1 and final['turn']==before and info['frames']>0
        assert final['exclusive'] and not final['releasing'],'Background actions lost exclusive ownership'
        print('PASS: actual hidden gkh.exe, full engine, RAF disabled, Node background frames, WAIT/undo/redo/menu/choice/close/forecast, idle purity, fatal refusal; native visibility/focus checks passed; no Steam access')
        print(json.dumps(info))
    except Exception:
        for name in ('fixture-status.json','fixture-startup.json','fixture-error.txt'):
            if (folder/name).exists():print(name,(folder/name).read_text(errors='replace'),flush=True)
        raise
    finally:
        cleanup_env=dict(os.environ,GKH_LLM_FIXTURE_EXE=str(folder/'gkh.exe'))
        subprocess.run(['powershell','-NoProfile','-Command',
            "Get-CimInstance Win32_Process -Filter \"Name = 'gkh.exe'\" | Where-Object { $_.ExecutablePath -eq $env:GKH_LLM_FIXTURE_EXE } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }"],
            env=cleanup_env,capture_output=True,check=True)
        child.wait(timeout=5);time.sleep(.3)
