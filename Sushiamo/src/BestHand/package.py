"""Builds dist/Huynd-BestHand-<version>.zip (the mod) and dist/Huynd-BestHand-Source.zip.

Usage: python package.py [--install]   (--install also copies the mod zip into the game's mods folder)
"""
import json, pathlib, shutil, sys, zipfile

ROOT = pathlib.Path(__file__).resolve().parent
MOD_DIR = ROOT / "src" / "mods-unpacked" / "Huynd-BestHand"
GAME_MODS = pathlib.Path(r"C:\Games\Sushiamo\game\mods")

version = json.loads((MOD_DIR / "manifest.json").read_text(encoding="utf8"))["version_number"]
dist = ROOT / "dist"
dist.mkdir(exist_ok=True)

mod_zip = dist / f"Huynd-BestHand-{version}.zip"
with zipfile.ZipFile(mod_zip, "w", zipfile.ZIP_DEFLATED) as z:
    for f in sorted(MOD_DIR.iterdir()):
        z.write(f, f"mods-unpacked/Huynd-BestHand/{f.name}")  # forward slashes: Godot needs them
    z.write(ROOT / "README.md", "mods-unpacked/Huynd-BestHand/README.md")

src_zip = dist / "Huynd-BestHand-Source.zip"
with zipfile.ZipFile(src_zip, "w", zipfile.ZIP_DEFLATED) as z:
    for f in sorted(ROOT.rglob("*")):
        rel = f.relative_to(ROOT)
        if f.is_file() and rel.parts[0] != "dist" and "__pycache__" not in rel.parts:
            z.write(f, rel.as_posix())

print("built", mod_zip.name, src_zip.name)
if "--install" in sys.argv:
    for old in GAME_MODS.glob("Huynd-BestHand-*.zip"):
        old.unlink()
    shutil.copy2(mod_zip, GAME_MODS / mod_zip.name)
    print("installed to", GAME_MODS / mod_zip.name)
