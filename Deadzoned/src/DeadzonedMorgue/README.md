# Deadzoned Morgue - workspace

User-facing docs: `dist\DeadzonedMorgue\README.md`.

    src\mrg.gml            the mod: run tracker + morgue writer
    tools\patch.csx        UndertaleModTool script: adds mrg.gml, 9 anchored edits + 3 appends
    tools\verify.csx       decompiles the touched entries (MRG_OUT) and checks registration
    tools\utmt             junction to ..\Deadzoned-DiceMod\tools\utmt (UTMT 0.9.2.0)
    test\mrgtest.gml       in-game test: turns, kills, save/load, all four run endings
    test\testbuild.csx     dice+morgue build + driver, renamed DeadzonedMorgueTest (own save
                           sandbox), achievements stubbed, leaderboard upload removed
    backup\                verified vanilla data.win (SHA256 2E738E38...46D9)
    build\data.win             vanilla + morgue
    build\data.win.with-dice   ..\Deadzoned-DiceMod\build\data.win + morgue

    .\build.ps1            build both variants
    .\install-mod.ps1      installs the with-dice variant if the game has the dice mod, else plain
    .\restore-vanilla.ps1  put the vanilla file back (removes every mod)
    .\package.ps1          dist\DeadzonedMorgue.zip + dist\DeadzonedMorgue-Source.zip

The dice and morgue patchers touch disjoint anchors (EscapeMenu step is shared, different
lines), so each applies on top of the other; both orders were built and checked.

## Test notes

- Stub `sStm_UploadLeaderboardStuff` by removing only the `steam_upload_score` call. Emptying the
  whole function leaves `vOnlineRecord` unset and the game-over / win screens crash on it.
- A character level-up opens a menu that eats the skip-turn key; test turns before kills.
