# Golden Krone Hotel — Seed Aids and LLM Bridge

Mod scripts for the Windows Steam release of Golden Krone Hotel 1.10.1.
Requires an installed copy of the game. Python 3.9+ is needed for installation
and the CLI; the in-game panel needs no separate service.

## Install or update

1. Close the game and extract the mod ZIP.
2. From the extracted folder, run:

```powershell
python install.py --game-dir "C:\Program Files (x86)\Steam\steamapps\common\Golden Krone Hotel"
```

3. Launch the game normally. A living, non-tutorial run enables seed aids.

The installer copies only the mod scripts and adds their hooks to the existing
`package.nw/index.html`. It keeps a timestamped backup under
`mod-backup/seed-aids-release/`, including an undo script. For an update, close
the game and run the new installer again. Repeated installation does not add
duplicate hooks. Steam file verification or updates may require reinstalling.

To restore the state before a particular installation, close the game and run
`python undo.py` inside its backup folder. Restore refuses if an installed file
has since changed, so it cannot silently overwrite newer changes.

The optional F8 heal trainer is also supplied. Add `--trainer` when installing
to enable it; default installation adds only the seed harness and bridge.

## Use

- Ctrl+Z: undo. Ctrl+Y / Ctrl+Shift+Z: redo.
- Foresight previews the nine movement/wait inputs through the actual engine.
- Omniscience shows real terrain, monsters, items and carried potion identities.
- Ctrl+Shift+F / Ctrl+Shift+O toggle foresight / omniscience.
- History retains the last **48 positions per session**. Reloading clears it.

For LLM play, leave the game running; it may stay minimized. Run the supplied
CLI from this extracted folder:

```powershell
python llm-play/gkh.py state
python llm-play/gkh.py control takeover
python llm-play/gkh.py seed-state
python llm-play/gkh.py control release
```

Read `LLM-SEED-SOLVE-GUIDE.md` before controlling gameplay. Every seed-solving
gameplay input must use `seed-play` / `gkh_seed_act`; these check and refuse
death one move ahead. Ordinary `action` / `gkh_act` has no death guard.
Use a fresh observation revision and unique request ID for each decision.
Takeover locks local gameplay input; the Watch only panel's Take control button
returns it after accepted work finishes. Never automatically reacquire after
a human handoff. Actual actions affect saves, achievements and progression.

See `SEED-AIDS.md`, `LLM-PLAY.md` and `LLM-TOOL-GUIDE.md` for details, including
the provider-independent stdio MCP adapter. No model or API key is embedded.

## Performance update — 2026-10-12

Movement previews restore the active floor and affected shared objects rather
than repeatedly restoring all floors. Idle panels do not redraw, and hidden
panels skip display forecasts. Hypothetical spawns release their native graphics.
Undo history is capped at 48 positions to limit retained memory.

In isolated benchmarks, nine-way forecasts fell from approximately 1.4–1.6 s
to 30–142 ms across three floors. Guard, undo/redo, save/load, floor alias,
background execution and rendered panel checks passed. Timings vary by floor
and hardware. A real Normal Assassin run collected four rings and won through
Fane's forgiveness at turn 2384, with 120/120 HP.

## Source and verification

`src/` contains the mod JavaScript, installer, packaging script, CLI, controller
helpers and regression tests. `dist/` contains the installable folder and both
mod/source ZIPs. These packages exclude the base game, saves, connection tokens,
personal gameplay logs and generated caches.

Rebuild from the exported `src/` folder:

```powershell
python mod-release/package.py --output ../dist
```

Engine tests require the purchased game files. Copy the source's `mod-tests/`
and `llm-play/` folders into the game directory, install the mod, and run tests there. Some
scripts require `pip install playwright` and `python -m playwright install chromium`.
Serve `package.nw` on localhost port 8765 for the browser tests. Native fixture
tests require the bundled Windows NW.js runtime; they use isolated fixture
folders and do not control the live game or access Steam.

The source includes the previous mod-only script as the `--baseline` benchmark
reference. No original game JavaScript, executable, artwork or audio is bundled.
For baseline benchmarks, also copy `mod-backup/before-seed-performance/` from
the source package into the game directory. Installer checks run without game
assets: `python mod-release/verify-package.py`.

