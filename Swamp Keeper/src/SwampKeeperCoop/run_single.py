"""Private-desktop regression/menu capture; no live desktop interaction."""
from pathlib import Path
import sys
source=Path(__file__).resolve().parents[1]/'tools/run_isolated_render.py'
code=source.read_text(encoding='utf-8')
code=code.replace('root = Path(__file__).resolve().parents[2]',"root = Path(__file__).resolve().parents[1] / 'coop-investigation/sandbox'")
if '--menu' in sys.argv:code=code.replace('-swamp-mod-test{extra}','-coop-menu-audit')
code=code.replace('root / "ModsSource/isolated-render-player.log"','root / "single-player.log"')
exec(compile(code,str(source),'exec'))
