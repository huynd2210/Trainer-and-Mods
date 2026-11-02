"""Exercise release install/update/restore against disposable game fixtures."""
import importlib.util
from pathlib import Path
import subprocess
import sys
import tempfile

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location('release_install', HERE / 'install.py')
installer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(installer)

with tempfile.TemporaryDirectory(prefix='gkh-release-test-') as temp:
    temp = Path(temp)
    game = temp / 'game with spaces'
    payload = temp / 'payload'
    for p in (game, payload):
        (p / 'package.nw/js').mkdir(parents=True)
    original = b'<html>\r\n<head>\r\n<script src="js/main.js"></script>\r\n</head>\r\n<body>user custom content</body>\r\n</html>\r\n'
    index = game / 'package.nw/index.html'
    index.write_bytes(original)
    (game / 'package.nw/js/main.js').write_bytes(b'base engine')
    for name in (*installer.SCRIPTS, 'trainer.js'):
        (payload / 'package.nw/js' / name).write_bytes(('// mod ' + name).encode())
    backup = installer.install(game, payload)
    installed = index.read_bytes()
    assert b'user custom content' in installed
    assert installed.count(b'js/seed-aids.js') == installed.count(b'js/llm-bridge.js') == 1
    assert installed.index(b'js/main.js') < installed.index(b'js/seed-aids.js') < installed.index(b'js/llm-bridge.js')
    assert b'js/trainer.js' not in installed
    assert installer.install(game, payload) is None, 'Repeated install must be byte-idempotent'
    index.write_bytes(installed + b'<!-- later user edit -->')
    result = subprocess.run([sys.executable, str(backup / 'undo.py')], capture_output=True)
    assert result.returncode != 0 and b'Restore refused' in result.stderr
    assert index.read_bytes().endswith(b'<!-- later user edit -->')
    index.write_bytes(installed)
    subprocess.run([sys.executable, str(backup / 'undo.py')], check=True, capture_output=True)
    assert index.read_bytes() == original
    assert not (game / 'package.nw/js/seed-aids.js').exists()
    # Upgrade an existing mod while preserving the user's prior files/hooks.
    old = original.replace(b'</head>', b'<script src="js/llm-bridge.js"></script>\r\n<script src="js/seed-aids.js"></script>\r\n</head>')
    index.write_bytes(old)
    for name in installer.SCRIPTS:
        (game / 'package.nw/js' / name).write_bytes(b'old mod version')
    backup = installer.install(game, payload, trainer=True)
    assert index.read_bytes().count(b'js/trainer.js') == 1
    assert installer.install(game, payload, trainer=True) is None
    subprocess.run([sys.executable, str(backup / 'undo.py')], check=True, capture_output=True)
    assert index.read_bytes() == old
    assert all((game / 'package.nw/js' / n).read_bytes() == b'old mod version' for n in installer.SCRIPTS)
    assert not (game / 'package.nw/js/trainer.js').exists()
    index.write_bytes(b'<html><head><script src="js/main.js"></script></head><body>custom</body></html>')
    installer.install(game, payload)
    assert installer.install(game, payload) is None, 'Inline HTML install must also be byte-idempotent'
    # Reject a malformed installation before making backups or edits.
    index.write_bytes(b'no head tag')
    before = list((game / 'mod-backup/seed-aids-release').iterdir())
    try:
        installer.install(game, payload)
        raise AssertionError('Malformed HTML accepted')
    except ValueError:
        pass
    assert index.read_bytes() == b'no head tag'
    assert list((game / 'mod-backup/seed-aids-release').iterdir()) == before

print('PASS: additive install, script order, optional trainer, idempotent update, exact restore, changed-file refusal, malformed-install refusal')
