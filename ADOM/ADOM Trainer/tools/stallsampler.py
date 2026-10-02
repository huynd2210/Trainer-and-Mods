"""Stall sampler for ADOM (diagnostics).

Samples every thread of adom.exe ~20x per second: where it is in the code (EIP plus return
addresses found on its stack) and how much CPU it used. The trainer's lag probe writes
<ADOM>/adomtrainer/lag.log when the game stalls; each new stall there dumps the samples that
cover it to stalls.txt, so you can see what the game was doing during the freeze.

Usage: python stallsampler.py [output_dir] [minutes]
Waits for adom.exe to start, exits when it closes or after `minutes` (default 60).
"""
import ctypes as C
import ctypes.wintypes as W
import collections
import os
import re
import sys
import time

import pefile

GAME_DIR = r"C:\Program Files (x86)\Steam\steamapps\common\ADOM"
LAG_LOG = os.environ.get("ADOM_LAG_LOG") or os.path.join(GAME_DIR, "adomtrainer", "lag.log")
OUT_DIR = sys.argv[1] if len(sys.argv) > 1 else "."
MINUTES = float(sys.argv[2]) if len(sys.argv) > 2 else 60
TARGET = sys.argv[3] if len(sys.argv) > 3 else "adom.exe"
INTERVAL = 0.05
MIN_STALL_MS = 700
KEEP_SECONDS = 60

k32 = C.WinDLL("kernel32", use_last_error=True)
psapi = C.WinDLL("psapi", use_last_error=True)


class THREADENTRY32(C.Structure):
    _fields_ = [("dwSize", W.DWORD), ("cntUsage", W.DWORD), ("th32ThreadID", W.DWORD),
                ("th32OwnerProcessID", W.DWORD), ("tpBasePri", W.LONG), ("tpDeltaPri", W.LONG),
                ("dwFlags", W.DWORD)]


class PROCESSENTRY32W(C.Structure):
    _fields_ = [("dwSize", W.DWORD), ("cntUsage", W.DWORD), ("th32ProcessID", W.DWORD),
                ("th32DefaultHeapID", C.c_void_p), ("th32ModuleID", W.DWORD), ("cntThreads", W.DWORD),
                ("th32ParentProcessID", W.DWORD), ("pcPriClassBase", W.LONG), ("dwFlags", W.DWORD),
                ("szExeFile", W.WCHAR * 260)]


class MODULEINFO(C.Structure):
    _fields_ = [("lpBaseOfDll", C.c_void_p), ("SizeOfImage", W.DWORD), ("EntryPoint", C.c_void_p)]


for fn, res, args in [
    ("CreateToolhelp32Snapshot", W.HANDLE, [W.DWORD, W.DWORD]),
    ("Thread32First", W.BOOL, [W.HANDLE, C.POINTER(THREADENTRY32)]),
    ("Thread32Next", W.BOOL, [W.HANDLE, C.POINTER(THREADENTRY32)]),
    ("Process32FirstW", W.BOOL, [W.HANDLE, C.POINTER(PROCESSENTRY32W)]),
    ("Process32NextW", W.BOOL, [W.HANDLE, C.POINTER(PROCESSENTRY32W)]),
    ("OpenProcess", W.HANDLE, [W.DWORD, W.BOOL, W.DWORD]),
    ("OpenThread", W.HANDLE, [W.DWORD, W.BOOL, W.DWORD]),
    ("CloseHandle", W.BOOL, [W.HANDLE]),
    ("Wow64SuspendThread", W.DWORD, [W.HANDLE]),
    ("ResumeThread", W.DWORD, [W.HANDLE]),
    ("Wow64GetThreadContext", W.BOOL, [W.HANDLE, C.c_void_p]),
    ("GetThreadTimes", W.BOOL, [W.HANDLE] + [C.POINTER(W.FILETIME)] * 4),
    ("ReadProcessMemory", W.BOOL, [W.HANDLE, C.c_void_p, C.c_void_p, C.c_size_t, C.POINTER(C.c_size_t)]),
    ("GetExitCodeProcess", W.BOOL, [W.HANDLE, C.POINTER(W.DWORD)]),
]:
    f = getattr(k32, fn)
    f.restype, f.argtypes = res, args
psapi.EnumProcessModulesEx.argtypes = [W.HANDLE, C.POINTER(W.HMODULE), W.DWORD, C.POINTER(W.DWORD), W.DWORD]
psapi.GetModuleInformation.argtypes = [W.HANDLE, W.HMODULE, C.POINTER(MODULEINFO), W.DWORD]
psapi.GetModuleFileNameExW.argtypes = [W.HANDLE, W.HMODULE, W.LPWSTR, W.DWORD]

TH32CS_SNAPPROCESS, TH32CS_SNAPTHREAD = 0x2, 0x4
INVALID = W.HANDLE(-1).value
CTX_SIZE, CTX_FLAGS = 716, 0x10003  # WOW64_CONTEXT, i386 | CONTROL | INTEGER
OFF_EBP, OFF_EIP, OFF_ESP = 180, 184, 196


def find_pid(name=None):
    name = (name or TARGET).lower()
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0)
    e = PROCESSENTRY32W(dwSize=C.sizeof(PROCESSENTRY32W))
    ok = k32.Process32FirstW(snap, C.byref(e))
    pid = None
    while ok:
        if e.szExeFile.lower() == name:
            pid = e.th32ProcessID
        ok = k32.Process32NextW(snap, C.byref(e))
    k32.CloseHandle(snap)
    return pid


def thread_ids(pid):
    snap = k32.CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0)
    e = THREADENTRY32(dwSize=C.sizeof(THREADENTRY32))
    ok = k32.Thread32First(snap, C.byref(e))
    out = []
    while ok:
        if e.th32OwnerProcessID == pid:
            out.append(e.th32ThreadID)
        ok = k32.Thread32Next(snap, C.byref(e))
    k32.CloseHandle(snap)
    return out


class Modules:
    """32-bit modules of the target, with export-name lookup for system DLLs."""

    def __init__(self, hproc):
        self.hproc = hproc
        self.mods = []  # (base, end, name, exports sorted [(rva, name)])
        self.refresh()

    def refresh(self):
        arr = (W.HMODULE * 1024)()
        need = W.DWORD()
        if not psapi.EnumProcessModulesEx(self.hproc, arr, C.sizeof(arr), C.byref(need), 1):
            return
        known = {m[0] for m in self.mods}
        for h in arr[: need.value // C.sizeof(W.HMODULE)]:
            mi = MODULEINFO()
            psapi.GetModuleInformation(self.hproc, h, C.byref(mi), C.sizeof(mi))
            base = mi.lpBaseOfDll or 0
            if base in known:
                continue
            buf = C.create_unicode_buffer(520)
            psapi.GetModuleFileNameExW(self.hproc, h, buf, 520)
            path = re.sub(r"(?i)\\system32\\", r"\\SysWOW64\\", buf.value)
            self.mods.append((base, base + mi.SizeOfImage, os.path.basename(path), self.exports(path)))
        self.mods.sort()

    @staticmethod
    def exports(path):
        name = os.path.basename(path).lower()
        if name in ("adom.exe",):
            return []
        try:
            pe = pefile.PE(path, fast_load=True)
            pe.parse_data_directories(directories=[pefile.DIRECTORY_ENTRY["IMAGE_DIRECTORY_ENTRY_EXPORT"]])
            ex = [(s.address, s.name.decode()) for s in pe.DIRECTORY_ENTRY_EXPORT.symbols if s.name]
            return sorted(ex)
        except Exception:
            return []

    def find(self, addr):
        for base, end, name, ex in self.mods:
            if base <= addr < end:
                return base, name, ex
        return None

    def sym(self, addr):
        m = self.find(addr)
        if not m:
            return f"?{addr:08x}"
        base, name, ex = m
        rva = addr - base
        if name.lower() == "adom.exe":
            return f"adom!{0x400000 + rva:06x}"
        best = None
        for r, n in ex:
            if r <= rva:
                best = (r, n)
            else:
                break
        if best and rva - best[0] < 0x4000:
            return f"{name}!{best[1]}+{rva - best[0]:x}"
        return f"{name}+{rva:x}"


_ret_cache = {}


def is_return_address(hproc, v):
    """True if the bytes before v are a CALL, i.e. v is a real return address, not stale data."""
    if v in _ret_cache:
        return _ret_cache[v]
    buf = C.create_string_buffer(8)
    got = C.c_size_t()
    ok = False
    if k32.ReadProcessMemory(hproc, v - 8, buf, 8, C.byref(got)) and got.value == 8:
        b = buf.raw
        ok = (b[3] == 0xE8                                   # call rel32
              or (b[6] == 0xFF and (b[7] & 0x38) == 0x10)    # call reg / [reg]
              or (b[5] == 0xFF and (b[6] & 0x38) == 0x10)    # call [reg+d8]
              or (b[2] == 0xFF and (b[3] & 0x38) == 0x10))   # call [abs] / [reg+d32]
    _ret_cache[v] = ok
    return ok


def filetime_ms(ft):
    return ((ft.dwHighDateTime << 32) | ft.dwLowDateTime) / 10000.0


def main():
    os.makedirs(OUT_DIR, exist_ok=True)
    out_path = os.path.join(OUT_DIR, "stalls.txt")
    deadline = time.time() + MINUTES * 60
    log = open(out_path, "a", encoding="utf-8")

    def say(s):
        log.write(s + "\n")
        log.flush()

    say(f"=== sampler started {time.strftime('%Y-%m-%d %H:%M:%S')}, waiting for adom.exe")
    pid = None
    while time.time() < deadline and not pid:
        pid = find_pid()
        if not pid:
            time.sleep(2)
    if not pid:
        say("adom.exe never started")
        return
    hproc = k32.OpenProcess(0x0410 | 0x1000, False, pid)
    mods = Modules(hproc)
    say(f"attached to pid {pid}")

    threads = {}  # tid -> [handle, last_cpu_ms]
    samples = collections.deque()  # (wall, tid, cpu_delta_ms, eip_sym, stack_syms)
    ctx = C.create_string_buffer(CTX_SIZE + 16)
    ctxp = (C.addressof(ctx) + 15) & ~15
    stackbuf = C.create_string_buffer(4096)
    lag_pos = os.path.getsize(LAG_LOG) if os.path.exists(LAG_LOG) else 0
    last_threads = last_mods = 0
    code = W.DWORD()

    while time.time() < deadline:
        now = time.time()
        if not k32.GetExitCodeProcess(hproc, C.byref(code)) or code.value != 259:
            say("adom.exe exited")
            break
        if now - last_threads > 2:
            last_threads = now
            live = set(thread_ids(pid))
            for tid in list(threads):
                if tid not in live:
                    k32.CloseHandle(threads.pop(tid)[0])
            for tid in live - threads.keys():
                h = k32.OpenThread(0x4A, False, tid)
                if h:
                    threads[tid] = [h, None]
        if now - last_mods > 10:
            last_mods = now
            mods.refresh()

        for tid, st in threads.items():
            h = st[0]
            t = [W.FILETIME() for _ in range(4)]
            if not k32.GetThreadTimes(h, *[C.byref(x) for x in t]):
                continue
            cpu = filetime_ms(t[2]) + filetime_ms(t[3])
            dcpu = 0 if st[1] is None else cpu - st[1]
            st[1] = cpu
            if k32.Wow64SuspendThread(h) == 0xFFFFFFFF:
                continue
            try:
                C.memset(ctxp, 0, CTX_SIZE)
                C.c_uint32.from_address(ctxp).value = CTX_FLAGS
                if not k32.Wow64GetThreadContext(h, ctxp):
                    continue
                eip = C.c_uint32.from_address(ctxp + OFF_EIP).value
                esp = C.c_uint32.from_address(ctxp + OFF_ESP).value
                got = C.c_size_t()
                k32.ReadProcessMemory(hproc, esp, stackbuf, 4096, C.byref(got))
            finally:
                k32.ResumeThread(h)
            frames = []
            for i in range(0, got.value - 3, 4):
                v = int.from_bytes(stackbuf.raw[i:i + 4], "little")
                if mods.find(v) and is_return_address(hproc, v):
                    frames.append(mods.sym(v))
                    if len(frames) >= 10:
                        break
            samples.append((now, tid, dcpu, mods.sym(eip), tuple(frames)))
        while samples and samples[0][0] < now - KEEP_SECONDS:
            samples.popleft()

        # new stalls reported by the in-game probe?
        if os.path.exists(LAG_LOG):
            size = os.path.getsize(LAG_LOG)
            if size < lag_pos:
                lag_pos = 0
            if size > lag_pos:
                with open(LAG_LOG, "r", encoding="utf-8", errors="replace") as f:
                    f.seek(lag_pos)
                    new = f.read()
                lag_pos = size
                for line in new.splitlines():
                    m = re.match(r"(\S+)\s+(\w+)\s+(\d+) ms\s*(.*)", line)
                    if m and int(m.group(3)) >= MIN_STALL_MS:
                        report(say, samples, now, int(m.group(3)), line)
        time.sleep(max(0.0, INTERVAL - (time.time() - now)))

    for st in threads.values():
        k32.CloseHandle(st[0])
    k32.CloseHandle(hproc)
    say(f"=== sampler stopped {time.strftime('%H:%M:%S')}")


def report(say, samples, now, ms, line):
    t0 = now - ms / 1000.0 - 0.3
    win = [s for s in samples if t0 <= s[0] <= now]
    say(f"\n##### STALL {line.strip()}  ({len(win)} samples)")
    by_tid = collections.defaultdict(list)
    for s in win:
        by_tid[s[1]].append(s)
    rows = sorted(by_tid.items(), key=lambda kv: -sum(x[2] for x in kv[1]))
    for tid, ss in rows:
        cpu = sum(x[2] for x in ss)
        where = collections.Counter((x[3], x[4][:7]) for x in ss)
        say(f"-- thread {tid}: cpu {cpu:.0f} ms over {len(ss)} samples")
        for (eip, frames), n in where.most_common(3):
            say(f"   {n:3d}x  {eip}")
            for fr in frames:
                say(f"          <- {fr}")


if __name__ == "__main__":
    main()
