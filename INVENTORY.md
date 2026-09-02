# Inventory

Every mod and trainer found under `C:\Games`, with where it came from.
Layout per game: `dist/` = packaged for distribution, `src/` = source code.

Generated 2026-09-02.

## Collected from C:\Games

| Game | Mods / trainers | dist | src | Source location |
|---|---|---|---|---|
| Battle Brothers | custom_enemy, custom_events, kill_tracker (Squirrel `.nut`) | 3 zips | yes | `Battle Brothers\_modsrc\{custom_enemy,custom_events_build,build}` |
| Breachway | BreachwayTrainer v1.4.5 | 1 zip (built here) | **trainer source missing** — only `save_editor.py` | `Breachway\BepInEx\plugins` |
| CULTIC | GodMode, KillTracker, LighterBrighter, Minimap, Replay, SuperHot, Hotkey Trainer | 14 zips | yes | `CULTIC.v2026.01.10\CULTIC\*Source` |
| Cat Mail Co | BoatHeightUnlocker, BreakRoomKey, PackageCounter, SpeedBoost, WhosItFor, FriendsModPack | 15 zips | yes | `Cat.Mail.Co\game\mods` + `game\dist` |
| Cursed Words | CursedAI (autonomous player) | 1 zip (built here) | yes | `Cursed.Words.CursedAI` |
| Dead Weight | Dead Weight Trainer 1.1 | launcher + note; 902 MB patched exe excluded | **source missing** | `Trainers and Mods\Dead Weight Trainer` |
| Die For The Lich | Meta-Progression mod (Godot) | source zip + BUILD-NOTE | yes | `Die.For.The.Lich\game\forward_plus\meta_progression_mod` |
| Disfigure | DisfigureTrainer | 1 zip (built here) | yes | `Disfigure\DisfigureTrainer` |
| He is Coming | HicDraft | 2 zips | yes | `He.is.Coming.v0.9.22\moddev` |
| MENACE | MenaceTrainer | 1 zip (built here) | yes | `C:\Games\MenaceTrainer` (standalone folder) |
| Magical Princess | MagicalPrincessTrainer v1.1.0 | 2 zips | yes | `Magical Princess\MagicalPrincessTrainer` |
| Nuclear Option | KillCostTracker | 1 zip (built here) | yes | `Nuclear.Option.v0.33.4\game\ModSource\KillCostTracker` |
| PEAK | JetpackInfiniteFuel | 1 zip (built here) | yes | `PEAK.v2.02.a_LinkNeverDie.Com\Mods\JetpackInfiniteFuel` |
| Quasimorph | KillTracker, MapReveal, OperatorBoost, Spawner, WarpDrive | 11 zips | yes | `Quasimorph.v1.0\Quasimorph\ModSource` |
| Rift Wizard 3 | 10 Python mods (already present) | source *is* the distribution | yes | `Rift.Wizard.3\game\mods` |
| Sir We Have an Orc Problem | Godot trainer (`trainer.gd` + pck inject tooling) | 1 zip (built here) | yes | `Sir.We.Have.an.Orc.Problem\mod` |
| Skull Horde | Skull Horde Trainer (external save editor) | 1 zip (built here) | yes | `Skull Horde\SkullHordeTrainer` |
| Starless Abyss | StarlessCheats, StarlessBestiary, StarlessShips, StarlessSpeed | 4 zips (built here) | **source missing** | `Starless.Abyss.v1.011\...\BepInEx\plugins` |
| The Last Spell | TLSTrainer, TLSKillTracker | 2 zips (built here) | yes | `The.Last.Spell.v1.3.32.10\_trainer_dev` |
| War on the Sea | WoTSTrainer | 2 zips | yes (added) | `War.on.the.Sea.v1.09a\wots-trainer` |

## Already in this repo, game not on this machine

Bloodletter, Drone Sector, Hellcard, Shogun Showdown, TYPECAST AutoFire,
Tears of Metal Trainer. Left exactly as they were. Bloodletter, Drone Sector
and Shogun Showdown are zip-only — no source anywhere under `C:\Games`.

## Deliberately excluded

Third-party or vanilla, not our work:

- **HD2Mod** — downloaded Nexus mods for Helldivers 2, plus a third-party AutoHotkey radial menu.
- **Heroes of Hammerwatch II** — `mods\*.bin`, all downloaded.
- **Heart of the Machine** — `XMLMods\NuclearInfantry` and `SimpleEconomy` are the developer's own example mods (`author="Chris (x-4000)"`).
- **PEAK** — `ItemSpawnerPeak`, `The_Wall` (Nexus/Thunderstore downloads). Only JetpackInfiniteFuel is ours.
- **Cat Mail Co** — `CatMailCoParcelLink.dll` (Thunderstore download).
- **Battle Brothers** — `legends`, `msu`, `stdlib`, `modern_hooks`, `vanilla_ui` under `_modsrc` are extracted third-party mods kept for reference; `mod-stage` holds downloaded mod packs.
- **HiC.CT** — a Cheat Engine table for *He is Coming*, third-party.
- **RPG Maker / NW.js games** (EsKno, Holy Grail City, Fallen Priestess, …) — `js\plugins` is stock engine content.
- **No Plan B** — `Mods\` is empty.

Not mods — reverse-engineering and analysis work:

- **IRON.NEST** `_decompile\` — decompiler output and probe scripts.
- **temppp** `FlightModelReplica` — Nuclear Option flight-model analysis.
- **Steambird Clone** — a game reimplementation, not a mod.

Judgment call:

- **Rift Wizard 2** `RiftWizMod\` — a git repo containing a full modified fork of the
  game's Python source (added spells, bestiary, run history, kill tracking). It is real
  mod work, but it embeds the entire game source rather than shipping loadable mods, so
  it is not published here. Say the word and it goes in.

## Notes

- **No BepInEx is bundled anywhere.** Every zip was opened and checked; the 18 that
  carried a BepInEx loader (`-Pack` variants, plus `CULTIC\Minimap.zip` and
  `QuasimorphMods-Bundle.zip`, which were packs despite their names) were moved to
  `C:\Games\Trainers and Mods\_bepinex-packs-removed\` rather than deleted, since a few
  exist nowhere else on this machine. Repo went from ~448 MB to ~82 MB.
  Every mod still has a distribution: the `BepInEx\plugins\<mod>.dll` layout in the
  remaining zips is just the install path, not a bundled loader.
- `QuasimorphMods-Bundle.zip` was rebuilt from the five mod-only zips — all five DLLs
  and READMEs, no loader. `CULTIC\Minimap-Plugin-Only.zip` was renamed `Minimap.zip`
  to match its siblings.
- **Cat Mail Co's Friends Mod Pack was dropped, not rebuilt.** Its only purpose was
  shipping BepInEx so friends did not have to install it, and it carried just Package
  Counter 1.1.0 and Boat Height Unlocker 1.0.0 — both already here as individual zips.
  Its docs are kept in `src\FriendsModPack`.
- **Largest files left**, none of them BepInEx: the pre-existing
  `Hellcard\HELLCARD-Trainer-v1.0.zip` (58 MB — a 66 MB standalone trainer exe, already
  committed and pushed), `Die For The Lich\src\screenshots` (15 MB of full-size PNGs,
  worth downscaling), and `Cursed Words\src\simulator\data\dictionary-EnglishDefault.txt`
  (3.7 MB, needed by the solver).
- **Die For The Lich** ships as a full replacement `Die for the Lich.pck` (84 MB) —
  the game's entire data package. That was left out; `dist\` carries the GDScript
  source plus `BUILD-NOTE.md` explaining how to rebuild and install.
- **Sir We Have an Orc Problem** likewise excludes the patched 20 MB `swhaop.pck`;
  `dist\` has `trainer.gd` and the pck extract/inject tooling.
- **Dead Weight** ships as a 902 MB patched copy of the whole game executable. Only the
  README and launcher scripts were copied; see `Dead Weight\dist\BINARY-EXCLUDED.md`.
- **Quasimorph** `src\` omits the `lib\` folder of referenced game assemblies, so the
  `.csproj` files will not build until those are restored from the game install.
- **CULTIC** — `MOD-RELEASE-MANIFEST.txt` refers to a `Minimap-Pack.zip`; on disk that
  file is named `Minimap.zip` and the loader-free build was `Minimap-Plugin-Only.zip`,
  the reverse of every other CULTIC mod. The loader-free one is what is here.
- Build output (`bin`, `obj`, `__pycache__`, `.venv`, `.git`) was excluded from every
  copied source tree, as were redistributed Unity/BepInEx assemblies.
