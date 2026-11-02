"""Verify exported ZIPs, live script parity, and rebuilding from exported source."""
import argparse
from pathlib import Path
import subprocess
import sys
import tempfile
import zipfile

ROOT = Path(__file__).resolve().parent.parent
NAME = 'GKH-SeedAids-LLMBridge-2026.10.12'
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('export', type=Path)
args = parser.parse_args()
export = args.export.resolve()
with tempfile.TemporaryDirectory(prefix='gkh-export-rebuild-') as temp:
    temp = Path(temp)
    subprocess.run([sys.executable, '-B', str(export / 'src/mod-release/package.py'),
                    '--output', str(temp)], check=True, capture_output=True)
    for suffix in ('mod', 'source'):
        filename = NAME + '-' + suffix + '.zip'
        local, uploaded = ROOT / 'dist' / filename, export / 'dist' / filename
        assert local.read_bytes() == uploaded.read_bytes()
        with zipfile.ZipFile(local) as z, zipfile.ZipFile(temp / filename) as rebuilt:
            assert z.testzip() is None and set(z.namelist()) == set(rebuilt.namelist())
            for entry in z.namelist():
                assert z.read(entry) == rebuilt.read(entry), entry
                assert not any(bad in entry.lower() for bad in
                               ('connection.json', 'play-session', 'winning-', 'current-seed', '__pycache__', 'node_modules'))
            for name in ('seed-aids.js', 'llm-bridge.js', 'trainer.js'):
                entry = 'package.nw/js/' + name
                assert z.read(entry) == (ROOT / entry).read_bytes() == (export / 'src' / entry).read_bytes()
            if suffix == 'mod':
                z.extractall(temp / 'mod')
                game = temp / 'game'
                (game / 'package.nw/js').mkdir(parents=True)
                (game / 'package.nw/index.html').write_text(
                    '<html><head><script src="js/main.js"></script></head><body>custom</body></html>', encoding='utf-8')
                (game / 'package.nw/js/main.js').write_text('// fixture', encoding='utf-8')
                subprocess.run([sys.executable, '-B', str(temp / 'mod/install.py'),
                                '--game-dir', str(game)], check=True, capture_output=True)
    for line in (export / 'dist/SHA256SUMS.txt').read_text(encoding='utf-8').splitlines():
        import hashlib
        checksum, filename = line.split('  ', 1)
        assert hashlib.sha256((export / 'dist' / filename).read_bytes()).hexdigest() == checksum
print('PASS: ZIP integrity, exported/live script parity, source rebuild equivalence, runtime-artifact exclusion, extracted-mod installation, checksums')
