# Generates the winmm.dll proxy layer: one jmp stub per named export of the system winmm.dll,
# forwarding through a pointer table that tracker.c fills at load time.
# Usage: python gen_proxy.py <out_dir>
import os, subprocess, sys

out = sys.argv[1]
dump = subprocess.run(['objdump', '-p', r'C:\Windows\System32\winmm.dll'],
                      capture_output=True, text=True, check=True).stdout
names, on = [], False
for ln in dump.splitlines():
    if '[Ordinal/Name Pointer] Table' in ln:
        on = True
        continue
    if on:
        if not ln.strip():
            break
        names.append(ln.split()[-1])
assert 'PlaySoundA' in names, 'unexpected objdump output'

with open(os.path.join(out, 'winmm_stubs.S'), 'w') as f:
    f.write('    .text\n')
    for i, n in enumerate(names):
        f.write(f'    .globl proxy_{i}\nproxy_{i}:\n    jmp *real_winmm+{8 * i}(%rip)\n')
with open(os.path.join(out, 'winmm.def'), 'w') as f:
    f.write('LIBRARY winmm.dll\nEXPORTS\n')
    for i, n in enumerate(names):
        f.write(f'    {n}=proxy_{i}\n')
with open(os.path.join(out, 'winmm_names.h'), 'w') as f:
    f.write(f'#define WINMM_EXPORT_COUNT {len(names)}\n')
    f.write('static const char *const winmm_names[WINMM_EXPORT_COUNT] = {\n')
    f.write(''.join(f'    "{n}",\n' for n in names))
    f.write('};\n')
print(len(names), 'exports')
