"""Install additive mod hooks; preserve originals and provide a checked restore."""
import argparse
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
import re
import shutil

SCRIPTS = ('seed-aids.js', 'llm-bridge.js')
UNDO = '''import hashlib, json
from pathlib import Path
b = Path(__file__).resolve().parent
m = json.loads((b / 'manifest.json').read_text(encoding='utf-8'))
root = Path(m['gameDirectory'])
for entry in m['files']:
    target = root / entry['path']
    if not target.is_file() or hashlib.sha256(target.read_bytes()).hexdigest() != entry['installedSha256']:
        raise SystemExit('Restore refused: file changed since installation: ' + str(target))
for entry in m['files']:
    target = root / entry['path']
    if entry['existed']:
        target.write_bytes((b / entry['path']).read_bytes())
    else:
        target.unlink()
print('Restored the pre-installation files. Restart the game.')
'''


def install(game_dir, payload_dir, trainer=False):
    root = Path(game_dir).resolve()
    payload = Path(payload_dir).resolve()
    index = root / 'package.nw/index.html'
    names = list(SCRIPTS) + (['trainer.js'] if trainer else [])
    if not index.is_file() or not (root / 'package.nw/js/main.js').is_file():
        raise ValueError('Game directory must contain package.nw/index.html and js/main.js')
    original = index.read_bytes()
    html = original.decode('utf-8')
    if not re.search(r'</head\s*>', html, re.I):
        raise ValueError('Cannot find closing head tag; no game files changed')
    newline = '\r\n' if '\r\n' in html else '\n'
    patterns = [r'<script\b[^>]*\bsrc\s*=\s*[\"\x27]js/' + re.escape(name) + r'[\"\x27][^>]*>\s*</script\s*>' for name in names]
    matches = [list(re.finditer(pattern, html, re.I)) for pattern in patterns]
    head_end = re.search(r'</head\s*>', html, re.I).start()
    ordered = all(len(m) == 1 for m in matches) and all(
        matches[i][0].start() < matches[i + 1][0].start() for i in range(len(matches) - 1))
    ordered = ordered and matches[-1][0].end() <= head_end
    # Leave correct existing hooks byte-identical; otherwise add them in dependency order.
    if not ordered:
        for pattern in patterns:
            html = re.sub(r'^[ \t]*' + pattern + r'[ \t]*(?:\r?\n|$)', '', html, flags=re.I | re.M)
            html = re.sub(r'[ \t]*' + pattern, '', html, flags=re.I)
        hooks = newline.join('        <script src="js/' + name + '"></script>' for name in names)
        html = re.sub(r'[ \t]*</head\s*>', lambda m: hooks + newline + m[0], html, count=1, flags=re.I)
    updates = {'package.nw/index.html': html.encode('utf-8')}
    for name in names:
        updates['package.nw/js/' + name] = (payload / 'package.nw/js' / name).read_bytes()
    if all((root / p).is_file() and (root / p).read_bytes() == content for p, content in updates.items()):
        return None
    stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S.%fZ')
    backup = root / 'mod-backup/seed-aids-release' / stamp
    backup.mkdir(parents=True)
    manifest = {'gameDirectory': str(root), 'files': []}
    for relative, content in updates.items():
        target = root / relative
        existed = target.is_file()
        if existed:
            saved = backup / relative
            saved.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(target, saved)
        manifest['files'].append({'path': relative, 'existed': existed,
                                  'installedSha256': hashlib.sha256(content).hexdigest()})
    (backup / 'manifest.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    (backup / 'undo.py').write_text(UNDO, encoding='utf-8')
    for relative, content in updates.items():
        target = root / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(content)
    return backup


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-dir', type=Path, required=True)
    parser.add_argument('--trainer', action='store_true', help='Also install the optional F8 heal trainer')
    args = parser.parse_args()
    try:
        backup = install(args.game_dir, Path(__file__).resolve().parent, args.trainer)
    except (OSError, ValueError, UnicodeError) as error:
        raise SystemExit(str(error))
    print('Installed. Close/restart the game. Backup: ' + str(backup) if backup else 'Already up to date.')

