"""Build curated mod/source distributions and optionally copy into an upload folder."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import zipfile

ROOT = Path(__file__).resolve().parent.parent
NAME = 'GKH-SeedAids-LLMBridge-2026.10.12'
DOCS = ('SEED-AIDS.md', 'LLM-PLAY.md', 'LLM-TOOL-GUIDE.md', 'LLM-SEED-SOLVE-GUIDE.md')


def write_tree(folder, files):
    for relative, source in files.items():
        target = folder / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)


def archive(path, files):
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for relative, source in sorted(files.items()):
            z.write(source, relative)
    with zipfile.ZipFile(path) as z:
        assert z.testzip() is None
        assert set(z.namelist()) == set(files)


def build(output, export=None):
    output = Path(output).resolve()
    mod = {'README.md': ROOT / 'mod-release/README.md', 'install.py': ROOT / 'mod-release/install.py',
           'llm-play/gkh.py': ROOT / 'llm-play/gkh.py'}
    mod.update({p: ROOT / p for p in DOCS})
    mod.update({'package.nw/js/' + n: ROOT / 'package.nw/js' / n
                for n in ('seed-aids.js', 'llm-bridge.js', 'trainer.js')})
    source = {k: v for k, v in mod.items() if k not in ('README.md', 'install.py')}
    source.update({'mod-release/' + p.name: p for p in (ROOT / 'mod-release').glob('*.py')})
    source['mod-release/README.md'] = ROOT / 'mod-release/README.md'
    source['README.md'] = ROOT / 'mod-release/README.md'
    source['.gitignore'] = ROOT / 'mod-release/source.gitignore'
    source['mod-release/source.gitignore'] = ROOT / 'mod-release/source.gitignore'
    source.update({'llm-play/' + p.name: p for p in (ROOT / 'llm-play').glob('*.py')})
    source.update({'mod-tests/' + p.name: p for p in (ROOT / 'mod-tests').glob('verify*.py')})
    for p in ('profile-seed.py', 'llm-transport-fixture.cjs', 'seed-performance-before.json', 'seed-performance-after.json'):
        source['mod-tests/' + p] = ROOT / 'mod-tests' / p
    source['mod-backup/before-seed-performance/seed-aids.js'] = ROOT / 'mod-backup/before-seed-performance/seed-aids.js'
    for p in set(mod.values()) | set(source.values()):
        if not p.is_file():
            raise FileNotFoundError(p)
    output.mkdir(parents=True, exist_ok=True)
    write_tree(output / NAME, mod)
    mod_zip, src_zip = output / (NAME + '-mod.zip'), output / (NAME + '-source.zip')
    archive(mod_zip, mod)
    archive(src_zip, source)
    sums = '\n'.join(hashlib.sha256(p.read_bytes()).hexdigest() + '  ' + p.name for p in (mod_zip, src_zip)) + '\n'
    (output / 'SHA256SUMS.txt').write_text(sums, encoding='utf-8')
    if export:
        export = Path(export).resolve()
        write_tree(export / 'src', source)
        write_tree(export / 'dist' / NAME, mod)
        for p in (mod_zip, src_zip, output / 'SHA256SUMS.txt'):
            target = export / 'dist' / p.name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(p, target)
        shutil.copyfile(ROOT / 'mod-release/README.md', export / 'README.md')
        shutil.copyfile(ROOT / 'mod-release/source.gitignore', export / '.gitignore')
    print(json.dumps({'mod': str(mod_zip), 'source': str(src_zip), 'export': str(export) if export else None,
                      'modFiles': len(mod), 'sourceFiles': len(source)}, indent=2))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, default=ROOT / 'dist')
    parser.add_argument('--export', type=Path)
    args = parser.parse_args()
    build(args.output, args.export)
