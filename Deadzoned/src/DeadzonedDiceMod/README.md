# Deadzoned Dice Mod - workspace

User-facing docs: `dist\DeadzonedDiceMod\README.md`.

    src\dzm.gml            the mod: roll overrides + the "dice rolls" settings page
    tools\patch.csx        UndertaleModTool script: adds dzm.gml, applies 48 anchored edits
    tools\verify.csx       decompiles the patched entries back out (DZM_OUT) and checks registration
    tools\dumpall.csx      full decompile (DZ_OUT) - decomp\ is vanilla
    test\dzmtest.gml       in-game test driver (menu via real key handlers + 200-attack trials)
    test\testbuild.csx     build\data.win + driver, renamed DeadzonedDiceTest (own save sandbox),
                           Steam achievements/stats/leaderboards stubbed out
    backup\                verified vanilla data.win (SHA256 2E738E38...46D9)
    build\                 patched data.win

    .\build.ps1            vanilla + src -> build\data.win
    .\install-mod.ps1      copy build\data.win into the game
    .\restore-vanilla.ps1  put the vanilla file back
    .\package.ps1          dist\DeadzonedDiceMod.zip + dist\DeadzonedDiceMod-Source.zip

## Running the in-game test

Build `test\data.win` with testbuild.csx (DZM_TEST=test), copy it into `test\run\` next to a copy of
the game exe, the Steam DLLs, `options.ini`, `steam_appid.txt` (1602620) and hardlinks to the two
`AG_*.dat` files, then start `test\run\Deadzoned.exe`. It quits itself; results land in
`%LOCALAPPDATA%\DeadzonedDiceTest\dzm_test.log` with screenshots `t1..t5*.png`. The real save folder
(`%LOCALAPPDATA%\Deadzoned`) is never touched.

## Gotchas

- GML arrays are copy-on-write: writing to an array you received (or stored in a global) inside a
  function silently writes a copy. Return the array instead.
- Functions in a new global script register as `gml_Script_<name>`; the importer does not add the
  file-level script asset, so patch.csx adds `dzm` itself to mirror vanilla scripts like `sCRT`.
