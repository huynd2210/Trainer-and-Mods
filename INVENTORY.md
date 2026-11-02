# Inventory

Every mod and trainer found under `C:\Games`, with where it came from.
Layout per game: `dist/` = packaged for distribution, `src/` = source code.

Added 2026-10-12: **Golden Krone Hotel** — seed aids (omniscience, foresight,
48-position undo/redo), guarded local LLM bridge and optional F8 heal trainer.
`Golden Krone Hotel/dist/` contains the installable mod folder, mod/source ZIPs
and SHA-256 checksums; `Golden Krone Hotel/src/` contains the source, installer,
packaging tools and regression tests. Exported from the Steam installation.

Generated 2026-09-02.

## Collected from C:\Games

| Game | Mods / trainers | dist | src | Source location |
|---|---|---|---|---|
| ADOM | ADOM Bot — grinding bot inside the game's NotEye Lua layer: fights safe monsters, flees dangerous ones, eats/prays, loots, and walks to Terinyo to buy food / sell loot (Lua `.noe`; no loader, NotEye ships with the game) | 2 zips (mod + source) | yes | `C:\Games\Trainers and Mods\ADOM Bot` |
| Battle Brothers | custom_enemy, custom_events, kill_tracker (Squirrel `.nut`) | 3 zips | yes | `Battle Brothers\_modsrc\{custom_enemy,custom_events_build,build}` |
| Breachway | BreachwayTrainer v1.4.5 | 1 zip (built here) | **trainer source missing** — only `save_editor.py` | `Breachway\BepInEx\plugins` |
| CULTIC | GodMode, KillTracker, LighterBrighter, Minimap, Replay, SuperHot, Hotkey Trainer | 14 zips | yes | `CULTIC.v2026.01.10\CULTIC\*Source` |
| Cat Mail Co | BoatHeightUnlocker, BreakRoomKey, PackageCounter, SpeedBoost, WhosItFor, FriendsModPack | 15 zips | yes | `Cat.Mail.Co\game\mods` + `game\dist` |
| Cook Serve Delicious 2 | CSD2 Autoplay v1.7 — shift bot + campaign mode (GameMaker `data.win` patch) | 2 zips (mod + source) | yes | `C:\Users\Admin\CSD2-Autoplay-Mod` |
| Cursed Words | CursedAI (autonomous player) | 1 zip (built here) | yes | `Cursed.Words.CursedAI` |
| Darkest Dungeon II | DD2 Kill Tracker — counts every enemy killed per profile (lifetime + current run), saved to JSON, attributes each monster to base game / DLC / the mod that added or overrode it; F11 window (BepInEx 5) | 2 zips (mod + source) | yes | `C:\Games\Darkest Dungeon II\KillTracker` |
| Dead Weight | Dead Weight Trainer 1.1 | launcher + note; 902 MB patched exe excluded | **source missing** | `Trainers and Mods\Dead Weight Trainer` |
| Deadzoned | Dice Mod — normal/always/never for hits, crits, dodges (yours and enemies'), talent procs, hacking; Morgue — trad-roguelike character dump .txt at every run end (GameMaker `data.win` patches, stackable) | 4 zips (mod + source each) | yes | `C:\Users\Admin\Deadzoned-DiceMod`, `C:\Users\Admin\Deadzoned-MorgueMod` |
| Die For The Lich | Meta-Progression mod (Godot) | source zip + BUILD-NOTE | yes | `Die.For.The.Lich\game\forward_plus\meta_progression_mod` |
| Disfigure | DisfigureTrainer | 1 zip (built here) | yes | `Disfigure\DisfigureTrainer` |
| GnollHack | Auto-Identify (Ctrl+I identifies everything you carry: types, BUC, enchantment, charges); Reveal Secrets (Ctrl+E finds every secret door and corridor on the level); Map Level (Ctrl+F maps the whole level, traps included), all through the engine's own routines; .NET startup hooks, started by a shared `Play GnollHack (mods).cmd` | 6 zips (mod + source each) | yes | `steamapps\common\GnollHack\{autoidentify,revealsecrets,maplevel}` |
| He is Coming | HicDraft | 2 zips | yes | `He.is.Coming.v0.9.22\moddev` |
| It Has My Face | CloneSight — F3 outlines your clone through walls (pink; hostile other clones amber), single-player only; KillTracker — counts your kills by kind for this level, the current run (story/endless) and lifetime, saved to JSON, kill line under the crosshair and hold-F4 table (BepInEx 5) | 4 zips (mod + source each) | yes | `D:\Games\It.Has.My.Face.v1.0\{CloneSight,KillTracker}` |
| Laysara: Summit Kingdom | Extra Income — configurable monthly income per map, shown as its own row in the income breakdown (UE4SS, Lua) | 2 zips (mod + source) | yes | `steamapps\common\Laysara Summit Kingdom\mods\ExtraIncome` |
| MENACE | MenaceTrainer | 1 zip (built here) | yes | `C:\Games\MenaceTrainer` (standalone folder) |
| Magical Princess | MagicalPrincessTrainer v1.1.0 | 2 zips | yes | `Magical Princess\MagicalPrincessTrainer` |
| Nuclear Option | KillCostTracker, AMRAAM (AIM-120 missile, lofted ARH), Auto-missile Defense (auto-fires IR missile at incoming radar missiles), Manual Missile Control (F6: steer your own missile from a chase camera) | 5 zips (KillCostTracker; AMRAAM and AutoMissileDefense, mod + source each) + `dist\` MissileControl mod + source | yes | `Nuclear.Option.v0.33.4\game\ModSource\{KillCostTracker,AMRAAM,AutoMissileDefense}`; MissileControl: `steamapps\common\Nuclear Option\ModSource\MissileControl` |
| Night Shippers | NightShippersTrainer (UE4SS, Lua) | 2 zips (built here) | yes | `C:\Users\Admin\Documents\NightShippersTrainer` (standalone folder) |
| Paint the Town Red | Gun Spawner — F7 drops the next of the game's 15 built-in guns (AK47, M4, MP5, shotguns, crossbows, …) where you aim, in any level; Shift+F7 the previous one. Uses the game's own weapon spawner (BepInEx 6 IL2CPP) | 2 zips (mod + source) | yes | `Paint.the.Town.Red.v2025.07.26\mods\GunSpawner` |
| PEAK | JetpackInfiniteFuel | 1 zip (built here) | yes | `PEAK.v2.02.a_LinkNeverDie.Com\Mods\JetpackInfiniteFuel` |
| Probably Stolen | No Rent Increase — the landlord's weekly rent hike is skipped; Extra Customer — F8 opens a picker of ~150 regular buyers, sellers, barterers and service customers (grouped by what they trade, locked ones greyed out) and queues the one you click; Shift+F8 adds a random customer; plus 0–10 extra customers each morning (default 1); Fraud Detection — tooltips reveal every hidden/fake item condition, inspection pre-ticks the counterfeit's flaw; Auto Appraisal — tooltip shows the true appraisal value and the estimate box is pre-filled; Lottery Foresight — tooltips show the coming lottery draw and each ticket's prize, and scratch ticket prizes before scratching (draw uses the foreseen numbers); More Scavenging — +1000 (configurable) scavenging attempts a night, injury risk capped at the normal level by default; Recursive Storage — storage bays nest inside other storage bays to any depth (gunsmith/chemist bays keep their whitelists); Chemical Suppliers — a Chemical Merchant (alcohol, reducing agent, stabilizer-free N-Methylol Base, starch filler; reagent price configurable, 2x by default) and a Shady Mushroom Seller (wholesale Dream/Nightmare Caps, also walks in); called from Extra Customer's F8 list when that mod is running, otherwise with its own F7 / Shift+F7; Instant Evaporator — the Evaporator does a night's work every second at the same power per litre; Precise Temperature Control — Heating Plate target settable to the degree at any module count, held exactly. All by NyanDoggo, all use the game's built-in mod loader + Harmony, no external loader | 20 zips (mod + source each) | yes | `Probably.Stolen.Cyberpunk.Shopkeeper.Sim\ModSource\{NoRentIncrease,ExtraCustomer,AutoAppraisal,FraudDetection,LotteryForesight,MoreScavenging,RecursiveStorage,ChemicalSuppliers,InstantEvaporator,PreciseTemperature}` |
| Quasimorph | KillTracker, MapReveal, OperatorBoost, Spawner, WarpDrive | 11 zips | yes | `Quasimorph.v1.0\Quasimorph\ModSource` |
| Regiments | Theater Command 1.0.0 (WARNO Army General-style theater campaign inside War Paths: grid map, NATO counters, drag to move/attack, battles fought as Regiments battles or auto-resolved), Warpath OPs 1.1.0 (Operational Authority held at 99,999), Kill Tracker 1.0.0 (a .txt per battle of your kills and losses, soldiers credited to the platoon that fired, summary by type and platoon; allies left out) | 6 zips (mod + source each) | yes | `C:\Games\Regiments.v1.5.0\mod\{theater-command,regiments-warpathops,kill-tracker}` |
| ReStory: Chill Electronics Repairs | MoreMoney (F8 adds yen to wallet) | 2 zips (mod + source) | yes | `ReStory.Chill.Electronics.Repairs\MoreMoney` |
| Rift Wizard 3 | 10 Python mods (already present) | source *is* the distribution | yes | `Rift.Wizard.3\game\mods` |
| Sephiria | SephiriaTrainer (F6 saphires / F7 gold), SephiriaStatBuff (configurable stat bonuses, F8/F9), SephiriaExpandedInventory (1–16 extra scrollable inventory rows) — Mono.Cecil patches of `Assembly-CSharp.dll`, no loader | 6 zips (mod + source each) | yes | `C:\Games\Sephiria.v1.0.33_LinkNeverDie.Com\Sephiria{Trainer,StatBuff,ExpandedInventory}` |
| Sir We Have an Orc Problem | Godot trainer (`trainer.gd` + pck inject tooling) | 1 zip (built here) | yes | `Sir.We.Have.an.Orc.Problem\mod` |
| Skull Horde | Skull Horde Trainer (external save editor) | 1 zip (built here) | yes | `Skull Horde\SkullHordeTrainer` |
| Starless Abyss | StarlessCheats, StarlessBestiary, StarlessShips, StarlessSpeed | 4 zips (built here) | **source missing** | `Starless.Abyss.v1.011\...\BepInEx\plugins` |
| Swamp Keeper | Swamp Keeper Online Co-op — two players on two PCs share one run (host-authoritative TCP, separate characters, skills, items and kills; all stages, bosses and the Well; reconnect after drops; F7 Host/Join menu). Ships with the Swamp Keeper Mods 1.2.0 (Kill Tracker + Item Prepare) it requires (BepInEx 6 be.789 IL2CPP x86) | 2 zips (mod + source) | yes | `Swamp.Keeper.v1.05\game\ModsSource\{SwampKeeperCoop,SwampKeeperMods}` |
| Tales of Maj'Eyal | ToME Trainer — F9 heal to full, F10 toggle fatal hit leaves 1 HP, F11 +1000 EXP, F12 toggle never miss (native ToME addon `.teaa`, Lua; no loader needed) | 2 zips (mod + source) | yes | `C:\Games\Trainers and Mods\ToME Trainer` |
| The Last Spell | TLSTrainer, TLSKillTracker | 2 zips (built here) | yes | `The.Last.Spell.v1.3.32.10\_trainer_dev` |
| War on the Sea | Cheat Pack: WoTSTrainer, WoTSAceAviators, WoTSMoreTargets, WoTSFireControl | 2 zips (mod + source) | yes | `War.on.the.Sea.v1.09a\wots-{trainer,aceaviators,moretargets,firecontrol}` |
| Warsim: The Realm of Aslona | Warsim Stats Tracker — records every playthrough year by year (gold, men, land, opinion, battles, kills, losses, income/expenses) across new games and deleted saves, with lifetime totals, records and charts in a self-updating `stats.html` (`winmm.dll` proxy, no loader; also loads other proxy mods from `mods\`) | 2 zips (mod + source) | yes | `C:\Games\Trainers and Mods\Warsim Stats Tracker` |

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
- **War on the Sea was expanded from one trainer to a four-mod Cheat Pack** and its `dist/`
  now holds one combined `WoTSCheatPack.zip` plus `WoTSCheatPack-Source.zip`, replacing the
  old `WoTSTrainer.zip`. Its `src/` holds all four workspaces, each with a signature harness
  and a behaviour harness that gate the build. No `-Pack` here, per the loader rule above;
  `dist/LOADER-NOT-BUNDLED.md` says how to rebuild it.
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
- **Cook Serve Delicious 2** is GameMaker, not Unity or Unreal, so **no mod loader is
  involved at all** — no BepInEx, no UE4SS, and therefore no `-Pack` variant and no
  `dist\LOADER-NOT-BUNDLED.md`; there is nothing to rebuild. The mod is GML appended to six
  events in `data.win`, and `install.ps1` patches the copy already on the user's disk, backing
  up the original first and fetching UndertaleModTool itself (pinned by SHA256).
  **No `data.win` is published** — it is 834 MB of the developer's game. `src\` likewise
  excludes the decompiled game code the mod was written against (`decomp*\` in the
  workspace): that is Vertigo Gaming's source, not ours. The two flat `CSD2-Autoplay-*-1.6.zip`
  files that were here before are replaced by the `dist\`/`src\` layout — they were named
  `-src.zip` against the naming rule, and 1.6 stalls on the post-shift reward screens.
- **Deadzoned Dice Mod and Morgue** (added 2026-09-27) follow the CSD2 pattern: GameMaker, so
  no mod loader, no `-Pack` and no `LOADER-NOT-BUNDLED.md`. Each `install.ps1` patches the
  user's own `data.win` with a pinned UndertaleModTool. Unlike CSD2's installer, these patch
  the *current* file rather than a vanilla backup, so the two stack in either order; each
  uninstall restores its own pre-install copy and refuses if another mod was installed on
  top since. `src\` is per mod (`src\DeadzonedDiceMod`, `src\DeadzonedMorgue`). Neither
  `data.win` nor the decompiled game code (`decomp\` in the workspace) is published.
- **Nuclear Option AMRAAM** (added 2026-09-25): `dist\` has `AMRAAM.zip` and
  `AMRAAM-Source.zip`, and `dist\LOADER-NOT-BUNDLED.md` says how to rebuild the Pack.
  The source leaves out `assets\` (7 MB mesh + 3 MB texture, already in `AMRAAM.zip`)
  and the decompiled game/Mirage/TextMeshPro classes the mod was written against
  (`evidence\Missile.decompiled.txt`, `Unit.txt`, etc.). The original `.blend` model is
  outside `C:\Games` and is not here. Not flown in a live mission yet; see its README.
  The Kill and Cost Tracker zip here is **1.4.0**. 1.5.0 is installed in the game but
  was never repackaged.
- **Nuclear Option Auto-missile Defense** (added 2026-09-29): `dist\` has `AutoMissileDefense.zip` and `AutoMissileDefense-Source.zip`. `AutoMissileDefense-Pack.zip` (BepInEx 5 + mod, 0.6 MB) is built in the workspace `ModSource\AutoMissileDefense
elease\` and not published. Source leaves out `bin`, `obj` and `build.log`.
- Build output (`bin`, `obj`, `__pycache__`, `.venv`, `.git`) was excluded from every
  copied source tree, as were redistributed Unity/BepInEx assemblies.
- **Laysara Extra Income** (added 2026-09-27) is UE4SS on UE 4.27, published in the
  `dist\`/`src\` layout. `ExtraIncome-Pack.zip` (8.7 MB, bundling UE4SS v3.0.1 Beta
  2bfa839f) is not published here; `dist\LOADER-NOT-BUNDLED.md` says how to rebuild
  it. The breakdown row and the in-game +/- were tested live. The monthly payout
  was not seen with a non-zero amount during testing.
- **Night Shippers** is UE4SS, not BepInEx, and the same no-bundled-loader rule was
  applied: `NightShippersTrainer-Pack.zip` (8.7 MB, almost all of it `UE4SS.dll`) is
  not here. `Night Shippers\dist\LOADER-NOT-BUNDLED.md` says how to rebuild it.
  Worth knowing for this one: the game is UE 5.5 and the *tagged* UE4SS releases
  predate 5.5 and silently fail to load - only experimental/CI builds work, so
  "go download UE4SS" is not sufficient advice here.
- **Darkest Dungeon II Kill Tracker** (added 2026-10-07): `dist\` has `DD2.KillTracker.zip` and `DD2.KillTracker-Source.zip`. `DD2.KillTracker-Pack.zip` (BepInEx 5.4.23.5 + mod) is built in the workspace `dist\` and not published; `dist\LOADER-NOT-BUNDLED.md` says how to rebuild it. Source leaves out `decomp\` (decompiled game code), `vendor\` (BepInEx), `bin`/`obj`. All 7 hooks were confirmed applying in the live game; kills in actual combat and the F11 window were not yet observed live.
- **Sephiria** (added 2026-10-09) replaces the earlier combined `SephiriaMods-v1.0.33-mod.zip` / `-source.zip` with one mod zip and one `-Source.zip` per mod. No mod loader is involved (each mod is its own Cecil patcher), so there is no `-Pack` and no `LOADER-NOT-BUNDLED.md`. The zips are made by `distribution\package-per-mod.ps1` in the game folder from the existing Release builds. Checked on a copy of the game's assemblies: installing all three gives a byte-identical `Assembly-CSharp.dll` to the patched live game, uninstalling all three restores vanilla, and all six source projects build. ExpandedInventory's scrolling layout has not had a hands-on in-game test.
- **GnollHack Auto-Identify, Reveal Secrets and Map Level** (added 2026-10-16) involve no mod loader at all. Each is a .NET startup hook (`DOTNET_STARTUP_HOOKS`), so there is no `-Pack` and no `LOADER-NOT-BUNDLED.md`, and no game file is modified. Both ship the same `Play GnollHack (mods).cmd`, which loads every `GnollHack\<Mod>\` folder that contains it, so any copy starts all installed mods. It refuses to run while the game is already open, because a second start only switches to the open, mod-less game: that was the cause of the first failed Reveal Secrets test. Auto-Identify and Map Level switch the engine's wizard flag on only while `^I` (#wizidentify) or `^F` (#wizmap) runs. Reveal Secrets, for one key press, points the #wizdetect (`^E`) command-table entry at its own function, which calls the engine's `findone()` on every secret door and corridor of the level from the engine thread. Neither mod uses hardcoded addresses. They follow exported engine functions to what they need and check each instruction and constant first, so a game update that changes them makes the mod write nothing and log why. The user tested Ctrl+I, Ctrl+E and Ctrl+F in live games on the 2026-10-16 Steam build; the named-event triggers have not been exercised.
- **Nuclear Option Manual Missile Control** (added 2026-10-21): `Nuclear Option\dist\` has `MissileControl.zip` and `MissileControl-Source.zip`, and `src\MissileControl` has the same source unzipped. The self-contained `MissileControl-1.0.0-NO0.34.1-Dist.zip` (BepInEx 5.4.23.5 + Doorstop + mod) is built in the workspace `release\independent\MissileControl\` and not published. `dist\LOADER-NOT-BUNDLED.md` says how to rebuild it. Source leaves out `bin`, `obj` and the decompiled game code it was written against. It compiles against 0.34.1, its 19 steering-math tests pass, and every patched method and field was checked in the 0.34.1 code. **It has not been run in the game:** the PC had too little free disk and RAM for a test launch. Heads-up for the Nuclear Option folder: the earlier flat `AutoMissileDefense-1.0.1-0.34.1-Dist.zip` (committed in d4103f5) contains BepInEx, which breaks the no-loader rule above. Its `SHA256SUMS.txt` lists `-NO0.34.1-` file names that don't match the zips beside it. Both were left as they are.
- **Swamp Keeper Online Co-op** (added 2026-10-22): `dist\` has `SwampKeeperCoop.zip` (co-op plugin plus the Swamp Keeper Mods 1.2.0 it requires) and `SwampKeeperCoop-Source.zip`; `src\` is the same source unpacked. The player `README.md` and the Radmin VPN guide sit at the folder root. `SwampKeeperCoop-Pack.zip` (BepInEx 6.0.0-be.789 IL2CPP x86 + mods) is built in the game's `ModsSource\SwampKeeperCoop\dist\` and not published; `dist\LOADER-NOT-BUNDLED.md` says how to rebuild it. Every acceptance gate passed as two game processes on one PC; it has not been played on two separate PCs, and the fresh-install test of the Pack has not been run. **Updated 2026-10-23 (build `swamp105-coop-003`):** fixes the guest drawing every sprite upside down (Il2CppInterop misread `SpriteRenderer.flipX`/`flipY` as always true), and the guest's Esc now opens the co-op menu with **Quit to title** (its native pause menu is frozen during co-op). Build 003 refuses build 002, so both players must update. Re-run for this update: basic, menu, protocol and storage tests; the other gates were not re-run, and Esc itself and the in-game relaunch are untested.
- **It Has My Face CloneSight and KillTracker** (CloneSight added 2026-10-26, KillTracker the same day): `dist\` has `CloneSight.zip`, `KillTracker.zip` and a `-Source.zip` for each; `src\CloneSight\` and `src\KillTracker\` are the same sources unpacked, each with its own `package.ps1` and README. The `-Pack.zip` of each (BepInEx 5.4.23.5 + mod) is built in its workspace `dist\` and not published; `dist\LOADER-NOT-BUNDLED.md` says how to rebuild them. Sources leave out the decompiled game code, `vendor\` (BepInEx) and `bin`/`obj`. The user confirmed CloneSight's outline in a live level. KillTracker counted a clone kill and showed its table in a live level; the user then asked for it to be published after a fix to the table's text tags. Its save logic (reload, run reset, record cap, corrupt-file recovery) passed an offline test. Heads-up for anyone testing mods from the Claude desktop app: a game launched from its shell runs inside Claude's app container, so its saves land in `AppData\Local\Packages\Claude_*\LocalCache`, not where the user's own launches look.
- **Paint the Town Red Gun Spawner** (added 2026-10-28): `dist\` has `GunSpawner.zip` and `GunSpawner-Source.zip`; `src\GunSpawner\` is the same source unpacked, with `package.ps1`. `GunSpawner-Pack.zip` (33 MB, BepInEx 6.0.0-be.781 IL2CPP x64 + mod) is built in the workspace `mods\GunSpawner\dist\` and not published; `dist\LOADER-NOT-BUNDLED.md` says how to rebuild it. Source leaves out `bin`/`obj` and the decompiled interop code. No new gun assets: all 15 guns are prefabs the game already ships, mostly used in Beneath and the level editor. A self-test build (`-c SelfTest`) loaded Biker Bar, spawned all 15 (each appeared as a `WeaponGun`), picked one up and pulled the trigger (ammo 30 → 29). The user then confirmed guns spawn and shoot in a live level.
- **Regiments** (added 2026-10-30): `Regiments\dist\` has `TheaterCommand.zip`, `WarpathOPs.zip` and their `-Source.zip`; `src\` has both workspaces unzipped. The `-Pack` zips (BepInEx 5.4.23.2 + mod) are built in each workspace's `release\` and not published; `dist\LOADER-NOT-BUNDLED.md` says how to rebuild them. Theater Command was run in the game through moves, auto-resolve and two fought battles; heavy-casualty and defence battles are not yet seen in the game. Kill Tracker added 2026-10-31 (`dist\KillTracker.zip` + `-Source.zip`, `src\KillTracker\`); one War Path battle played, per-platoon vehicle kills and losses matched the game's save exactly.- **Probably Stolen** (updated 2026-11-02): added Chemical Suppliers 1.2.0, Instant Evaporator 1.0.0 and Precise Temperature Control 1.0.0, and Extra Customer moved to 1.3.0 (lists the Chemical Suppliers sellers when that mod is installed). The game has its own mod loader, so there is no `-Pack` and no `LOADER-NOT-BUNDLED.md`. `src\` is the full `ModSource` workspace minus `bin`/`obj`, `dist\` and the decompiled game code. All build cleanly against v0.46D; Chemical Suppliers 1.1.0 was seen loading in the game log. None of the three new mods has been played in-game yet: Chemical Suppliers' prices and stock, Instant Evaporator's per-second run and Precise Temperature's buttons and hold are untested.
- **Probably Stolen** (updated 2026-11-02, later): every mod now stands alone. Chemical Suppliers no longer needs Extra Customer to call its sellers: since 1.4.0 it checks at startup whether Extra Customer is running and uses its F8 list if so, else registers its own F7 / Shift+F7 key (only one key is ever active). No mod declares a prerequisite, and every manifest has a `<GameVersions>` tag for 0.46, so the game's Mods menu flags a version mismatch itself; the other nine got a patch bump for that tag only. Each `dist\<Mod>.zip` was extracted alone into an empty folder and checked: only `Mods\<Mod>\`, manifest ID matches, listed DLL present, no prerequisites, and the DLL references only assemblies the game ships (the checker was confirmed to fail with no game DLLs and a wrong game version). Not yet loaded in the game in this form.
