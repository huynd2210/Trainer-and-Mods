# Turns src/report.html into a C string literal (report_html.h) so the DLL can write stats.html.
# Usage: python embed.py <report.html> <out.h>
import sys

src, out = sys.argv[1], sys.argv[2]
text = open(src, encoding='utf-8').read()
assert '/*WARSIM_DATA*/null' in text, 'report.html lost its data placeholder'
with open(out, 'w', encoding='ascii') as f:
    f.write('static const char report_html[] =\n')
    for line in text.encode('utf-8').splitlines(keepends=True):
        esc = ''.join(chr(b) if 32 <= b < 127 and b not in (34, 92, 63) else f'\\{b:03o}' for b in line)
        f.write(f'    "{esc}"\n')
    f.write(';\n')
