"""Launch a closed game minimized without activating another window."""
import ctypes
from ctypes import wintypes
import json
import os
from pathlib import Path
import subprocess
import time

root=Path(__file__).resolve().parent.parent
manifest=root/'package.nw/package.json'
original=manifest.read_bytes()
connection=Path(os.environ['LOCALAPPDATA'])/'GoldenKroneLLM/connection.json'
assert not connection.exists(),'An existing bridge is present; observe it instead of launching'
try:
    settings=json.loads(original);settings['window']['show']=False
    manifest.write_text(json.dumps(settings,indent=4),encoding='utf-8')
    child=subprocess.Popen([str(root/'gkh.exe')],cwd=root,creationflags=subprocess.CREATE_NO_WINDOW)
    deadline=time.monotonic()+45
    while not connection.exists() and time.monotonic()<deadline:
        if child.poll() is not None:raise RuntimeError('Game exited during startup')
        time.sleep(.1)
    assert connection.exists(),'Background game bridge did not initialize'
finally:
    manifest.write_bytes(original)
user32=ctypes.windll.user32
user32.GetWindowThreadProcessId.argtypes=[wintypes.HWND,ctypes.POINTER(wintypes.DWORD)]
user32.ShowWindow.argtypes=[wintypes.HWND,ctypes.c_int]
shown=[]
@ctypes.WINFUNCTYPE(wintypes.BOOL,wintypes.HWND,wintypes.LPARAM)
def visit(hwnd,param):
    pid=wintypes.DWORD();user32.GetWindowThreadProcessId(hwnd,ctypes.byref(pid))
    if pid.value==child.pid:
        user32.ShowWindow(hwnd,7) # SW_SHOWMINNOACTIVE
        shown.append(int(hwnd))
    return True
user32.EnumWindows(visit,0)
print(json.dumps({'pid':child.pid,'startedMinimized':bool(shown)}))
