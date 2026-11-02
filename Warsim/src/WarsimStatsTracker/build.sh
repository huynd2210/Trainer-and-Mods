#!/bin/sh
# Builds build/winmm.dll (the Warsim Stats Tracker). Needs MSYS2 ucrt64 gcc on PATH.
set -e
cd "$(dirname "$0")"
python src/gen_proxy.py build
python src/embed.py src/report.html build/report_html.h
gcc -O2 -Wall -Wextra -Wno-format-truncation -shared -static-libgcc -Isrc -Ibuild \
    -o build/winmm.dll src/tracker.c build/winmm_stubs.S build/winmm.def \
    -Wl,--enable-stdcall-fixup -s
objdump -p build/winmm.dll | grep "DLL Name"
