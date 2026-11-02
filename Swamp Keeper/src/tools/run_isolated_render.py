"""Run the automated probe on a private Windows desktop; never switch the user's desktop."""
import ctypes as c
from ctypes import wintypes as w
from pathlib import Path
import time, sys

root = Path(__file__).resolve().parents[2]
save_folder = Path.home() / 'AppData/LocalLow/MaetdolGames/Swamp Keeper'
original_files = {save_folder / name: (save_folder / name).read_bytes() if (save_folder / name).exists() else None
                  for name in ('Save.dat', 'Settings.dat', 'Inputs.dat')}
u, k = c.WinDLL('user32', use_last_error=True), c.WinDLL('kernel32', use_last_error=True)
class Startup(c.Structure):
    _fields_ = [('cb',w.DWORD),('reserved',w.LPWSTR),('desktop',w.LPWSTR),('title',w.LPWSTR),
                ('x',w.DWORD),('y',w.DWORD),('width',w.DWORD),('height',w.DWORD),
                ('xc',w.DWORD),('yc',w.DWORD),('fill',w.DWORD),('flags',w.DWORD),
                ('show',w.WORD),('reserved2size',w.WORD),('reserved2',c.c_void_p),
                ('stdin',w.HANDLE),('stdout',w.HANDLE),('stderr',w.HANDLE)]
class Process(c.Structure):
    _fields_=[('process',w.HANDLE),('thread',w.HANDLE),('pid',w.DWORD),('tid',w.DWORD)]
u.CreateDesktopW.argtypes=[w.LPCWSTR,w.LPCWSTR,c.c_void_p,w.DWORD,w.DWORD,c.c_void_p]
u.CreateDesktopW.restype=w.HANDLE
u.CloseDesktop.argtypes=[w.HANDLE]
k.CreateProcessW.argtypes=[w.LPCWSTR,w.LPWSTR,c.c_void_p,c.c_void_p,w.BOOL,w.DWORD,c.c_void_p,w.LPCWSTR,c.POINTER(Startup),c.POINTER(Process)]
k.WaitForSingleObject.argtypes=[w.HANDLE,w.DWORD]
k.TerminateProcess.argtypes=[w.HANDLE,w.UINT]
k.CloseHandle.argtypes=[w.HANDLE]
name='CodexSwampVerification'
desktop=u.CreateDesktopW(name,None,None,0,0x10000000,None)
if not desktop: raise c.WinError(c.get_last_error())
si=Startup(); si.cb=c.sizeof(si); si.desktop='winsta0\\'+name
pi=Process()
extra = ' -swamp-ui-audit' if '--audit' in sys.argv else ''
cmd=c.create_unicode_buffer(f'"{root / "Swamp Keeper.exe"}" -screen-fullscreen 0 -screen-width 1280 -screen-height 800 -force-d3d11 -swamp-mod-test{extra} -logFile "{root / "ModsSource/isolated-render-player.log"}"')
if not k.CreateProcessW(None,cmd,None,None,False,0,None,str(root),c.byref(si),c.byref(pi)):
    u.CloseDesktop(desktop)
    raise c.WinError(c.get_last_error())
print(f'Private desktop probe PID {pi.pid}',flush=True)
try:
    if k.WaitForSingleObject(pi.process,90000)==258:
        k.TerminateProcess(pi.process,1)
        k.WaitForSingleObject(pi.process,10000)
        raise TimeoutError('Private desktop probe timed out')
finally:
    k.CloseHandle(pi.thread); k.CloseHandle(pi.process); u.CloseDesktop(desktop)
    for path, original in original_files.items():
        if original is None:
            if path.exists(): path.unlink()
        elif not path.exists() or path.read_bytes() != original:
            path.write_bytes(original)
    print('Original game save/settings restored',flush=True)
print('Probe exited',flush=True)
