# No UE4SS loader in this folder

There is an `ExtraIncome-Pack.zip`: UE4SS plus the mod, self-contained, so there is
no separate loader install to get wrong. It is **not** in this repo, because of the
rule in `INVENTORY.md` that no mod loader is bundled anywhere here.

The Pack is 8.7 MB, and almost all of that is `UE4SS.dll`.

## Rebuilding it

`src/package.ps1` builds it from a working UE4SS install:

```powershell
.\package.ps1 -GameDir "C:\Program Files (x86)\Steam\steamapps\common\Laysara Summit Kingdom"
```

That writes all three zips, including the Pack. Use `-SkipPack` to build only the mod
and source zips.

## Which UE4SS

Laysara is UE 4.27. The mod was developed and tested against the build that reports
`UE4SS v3.0.1 Beta (Git SHA 2bfa839f)` (the same build used for Night Shippers).
Tagged UE4SS releases also support 4.27, so a regular release should work too, but
that has not been tested.

UE4SS is MIT licensed, Copyright (c) 2022 Narknon.
