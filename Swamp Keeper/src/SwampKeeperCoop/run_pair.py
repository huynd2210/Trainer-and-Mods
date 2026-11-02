"""Two real game processes on a private desktop. Never activate or switch desktops."""
from pathlib import Path
import ctypes as c
from ctypes import wintypes as w
import shutil, os, time, sys, asyncio, threading, random

workspace=Path(__file__).resolve().parents[2]
host=workspace/'ModsSource/coop-investigation/sandbox'
guest=workspace/'ModsSource/coop-investigation/sandbox-client'
import zipfile
modes=[a[2:] for a in sys.argv[1:] if a.startswith('--') and '=' not in a]
if 'install' in modes:
    # Install test: two fresh game copies from the vanilla game files plus only dist/SwampKeeperCoop-Pack.zip.
    pack=Path(__file__).resolve().parent/'dist/SwampKeeperCoop-Pack.zip'
    host,guest=workspace/'ModsSource/coop-investigation/install-host',workspace/'ModsSource/coop-investigation/install-guest'
    for root in (host,guest):
        if root.exists():shutil.rmtree(root)
        root.mkdir(parents=True)
        for name in ['Swamp Keeper.exe','GameAssembly.dll','UnityPlayer.dll','baselib.dll','UnityCrashHandler32.exe']:shutil.copy2(workspace/name,root/name)
        shutil.copytree(workspace/'Swamp Keeper_Data',root/'Swamp Keeper_Data',copy_function=os.link)
        with zipfile.ZipFile(pack) as z:z.extractall(root)
guest.mkdir(exist_ok=True)
for f in ([] if 'install' in modes else host.iterdir()):
    if f.is_file() and (f.suffix in ('.exe','.dll') or f.name=='doorstop_config.ini'):
        shutil.copy2(f,guest/f.name)
if 'install' not in modes:
    for name in ['Swamp Keeper_Data','dotnet','BepInEx/core','BepInEx/interop','BepInEx/unity-libs']:
        if not (guest/name).exists():shutil.copytree(host/name,guest/name,copy_function=os.link)
    shutil.copytree(host/'BepInEx/config',guest/'BepInEx/config',dirs_exist_ok=True)
    shutil.copytree(host/'BepInEx/plugins/SwampKeeperCoop',guest/'BepInEx/plugins/SwampKeeperCoop',dirs_exist_ok=True)
    shutil.copytree(host/'BepInEx/plugins/SwampKeeperMods',guest/'BepInEx/plugins/SwampKeeperMods',dirs_exist_ok=True)
save_folder=Path.home()/'AppData/LocalLow/MaetdolGames/Swamp Keeper'
originals={save_folder/name:(save_folder/name).read_bytes() if (save_folder/name).exists() else None for name in ['Save.dat','Settings.dat','Inputs.dat']}
u,k=c.WinDLL('user32',use_last_error=True),c.WinDLL('kernel32',use_last_error=True)
class Startup(c.Structure):
    _fields_=[('cb',w.DWORD),('reserved',w.LPWSTR),('desktop',w.LPWSTR),('title',w.LPWSTR),('x',w.DWORD),('y',w.DWORD),('width',w.DWORD),('height',w.DWORD),('xc',w.DWORD),('yc',w.DWORD),('fill',w.DWORD),('flags',w.DWORD),('show',w.WORD),('reserved2size',w.WORD),('reserved2',c.c_void_p),('stdin',w.HANDLE),('stdout',w.HANDLE),('stderr',w.HANDLE)]
class Process(c.Structure):_fields_=[('process',w.HANDLE),('thread',w.HANDLE),('pid',w.DWORD),('tid',w.DWORD)]
u.CreateDesktopW.argtypes=[w.LPCWSTR,w.LPCWSTR,c.c_void_p,w.DWORD,w.DWORD,c.c_void_p];u.CreateDesktopW.restype=w.HANDLE
u.CloseDesktop.argtypes=[w.HANDLE]
k.CreateProcessW.argtypes=[w.LPCWSTR,w.LPWSTR,c.c_void_p,c.c_void_p,w.BOOL,w.DWORD,c.c_void_p,w.LPCWSTR,c.POINTER(Startup),c.POINTER(Process)]
k.WaitForSingleObject.argtypes=[w.HANDLE,w.DWORD];k.TerminateProcess.argtypes=[w.HANDLE,w.UINT];k.CloseHandle.argtypes=[w.HANDLE]
desktop=u.CreateDesktopW('CodexSwampOnlineVerification',None,None,0,0x10000000,None)
if not desktop:raise c.WinError(c.get_last_error())
processes=[]
# key=value arguments become -coop-key value (debug options such as path-from=4).
options=''.join(f' -coop-{a.split("=",1)[0]} {a.split("=",1)[1]}' for a in sys.argv[1:] if '=' in a and not a.startswith('--'))
menu='menu' in modes
extra="".join(" -coop-"+m+"-test" for m in modes if m!="install")+options
if "--progression" in sys.argv:extra+=" -coop-gameplay-test"
# Wall-clock bound per gate; each game process also enforces its own shorter limit.
limits={'install':900,'paths':1500,'well':960,'boss':900,'effects':420,'network':300,'menu':240}

class Proxy:
    """Guest<->host TCP relay with one-way latency and jitter. The host steers faults through a command file:
    'cut' resets every live link; 'stall' freezes them silently (half-open), so only heartbeats can notice."""
    def __init__(self,listen,target,delay,jitter,control,log):
        self.listen,self.target,self.delay,self.jitter,self.control,self.logfile=listen,target,delay,jitter,control,log
        self.pairs=[];self.lines=[]
    def log(self,text):
        self.lines.append(f'{time.strftime("%H:%M:%S")} {text}');self.logfile.write_text(chr(10).join(self.lines),encoding='utf-8')
    async def pipe(self,pair,reader,writer):
        # One ordered queue per direction: chunks keep their order, each delivered at its own due time.
        loop=asyncio.get_running_loop();queue=asyncio.Queue()
        async def deliver():
            while True:
                due,data=await queue.get()
                if data is None:return
                delay=due-loop.time()
                if delay>0:await asyncio.sleep(delay)
                if not pair['stalled'] and not pair['closed'] and not writer.is_closing():writer.write(data)
        sender=loop.create_task(deliver())
        try:
            while data:=await reader.read(65536):
                if not pair['stalled']:queue.put_nowait((loop.time()+self.delay+random.random()*self.jitter,data))
        except (ConnectionError,OSError):pass
        queue.put_nowait((0,None))
        try:await sender
        except (ConnectionError,OSError):pass
        if not pair['stalled']:self.close(pair)
    def close(self,pair):
        if pair['closed']:return
        pair['closed']=True
        for w in pair['writers']:w.transport.abort()
    async def handle(self,cr,cw):
        try:ur,uw=await asyncio.open_connection(*self.target)
        except OSError:cw.transport.abort();return
        pair={'writers':[cw,uw],'stalled':False,'closed':False};self.pairs.append(pair);self.log(f'link {len(self.pairs)} opened')
        await asyncio.gather(self.pipe(pair,cr,uw),self.pipe(pair,ur,cw))
    async def main(self):
        await asyncio.start_server(self.handle,*self.listen)
        self.log(f'relaying {self.listen} -> {self.target} with {self.delay*1000:.0f}+{self.jitter*1000:.0f} ms one-way delay')
        while True:
            await asyncio.sleep(.1)
            if self.control.exists():
                command=self.control.read_text().strip();self.control.unlink()
                live=[p for p in self.pairs if not p['stalled'] and not p['closed']]
                for p in live:
                    if command=='cut':self.close(p)
                    elif command=='stall':p['stalled']=True
                self.log(f'{command} applied to {len(live)} live link(s)')
    def start(self):threading.Thread(target=lambda:asyncio.run(self.main()),daemon=True).start()

control=host/'coop-proxy-command.txt'
if control.exists():control.unlink()
if 'network' in modes:Proxy(('127.0.0.1',27857),('127.0.0.1',27856),.1,.03,control,host/'coop-proxy-log.txt').start()
try:
    for role,root in [('host',host),('guest',guest)]:
        for name in [f'coop-{role}-report.txt',f'coop-{role}.png']:
            if (root/name).exists():(root/name).unlink()
        si=Startup();si.cb=c.sizeof(si);si.desktop='winsta0\\CodexSwampOnlineVerification';pi=Process()
        port=27857 if role=='guest' and 'network' in modes else 27856
        # Menu mode starts no session from the command line; the test drives the real F7 Host/Join buttons.
        session=f'-coop-menu-role {role}' if menu else f'-coop-role {role} -coop-address 127.0.0.1'
        command=c.create_unicode_buffer(f'"{root/"Swamp Keeper.exe"}" -screen-fullscreen 0 -screen-width 1280 -screen-height 800 -force-d3d11 {session} -coop-port {port} -coop-code TESTCODE12345678 -coop-integration-test{extra} -logFile "{root/"coop-player.log"}"')
        if not k.CreateProcessW(None,command,None,None,False,0,None,str(root),c.byref(si),c.byref(pi)):raise c.WinError(c.get_last_error())
        processes.append(pi);print(f'{role} private desktop PID {pi.pid}',flush=True)
        time.sleep(1)
        if 'install' in modes and role=='host':
            # A fresh BepInEx install generates its interop assemblies on first launch; let the host get listening first.
            ready=time.monotonic()+600
            while not (root/'coop-host-report.txt').exists():
                if time.monotonic()>ready or k.WaitForSingleObject(pi.process,0)!=258:raise TimeoutError('Fresh host install never started its session')
                time.sleep(1)
            print('host install loaded BepInEx and both plugins',flush=True)
    deadline=time.monotonic()+max([85]+[limits.get(m,145) for m in modes])
    first_exit=None
    while any(k.WaitForSingleObject(p.process,0)==258 for p in processes):
        if time.monotonic()>deadline:raise TimeoutError('Two-process co-op verification timed out')
        # A peer that quit has finished or failed; the survivor gets a grace period, not the whole deadline (e.g. a hung host).
        if first_exit is None and any(k.WaitForSingleObject(p.process,0)!=258 for p in processes):first_exit=time.monotonic()
        if first_exit is not None and time.monotonic()-first_exit>60:raise TimeoutError('Surviving game did not exit within 60 s of its peer')
        time.sleep(.25)
finally:
    for p in processes:
        if k.WaitForSingleObject(p.process,0)==258:k.TerminateProcess(p.process,1);k.WaitForSingleObject(p.process,10000)
        k.CloseHandle(p.thread);k.CloseHandle(p.process)
    u.CloseDesktop(desktop)
    for path,original in originals.items():
        if original is None:
            if path.exists():path.unlink()
        elif not path.exists() or path.read_bytes()!=original:path.write_bytes(original)
    print('Original saves/settings restored',flush=True)
for role,root in [('host',host),('guest',guest)]:
    report=(root/f'coop-{role}-report.txt').read_text(encoding='utf-8-sig')
    print(role+':\n'+report,flush=True)
    if 'SESSION FAILED' in report or 'INTEGRATION PASSED' not in report:raise RuntimeError(role+' integration failed')
# Keep the passing run's reports; sandbox copies are overwritten by the next run.
if options:print('Debug options in use; evidence not saved',flush=True);sys.exit(0)
evidence=Path(__file__).resolve().parent/'evidence'/('-'.join(modes) or 'basic-session')
evidence.mkdir(parents=True,exist_ok=True)
for path in [host/'coop-host-report.txt',guest/'coop-guest-report.txt',host/'coop-proxy-log.txt']:
    if path.exists() and (path.name!='coop-proxy-log.txt' or 'network' in modes):shutil.copy2(path,evidence/path.name)
print('Evidence saved to',evidence,flush=True)
