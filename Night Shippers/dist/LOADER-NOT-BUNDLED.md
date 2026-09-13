# No UE4SS loader in this folder

A `NightShippersTrainer-Pack.zip` exists — UE4SS plus the trainer, self-contained,
so there is no separate loader install to get wrong. It is **not** in this repo,
following the rule in `INVENTORY.md`: no mod loader is bundled anywhere here.
That rule was what took the repo from ~448 MB to ~82 MB.

The Pack is 8.7 MB, almost all of it `UE4SS.dll`.

## Rebuilding it

`src/package.ps1` builds it from a working UE4SS install:

```powershell
.\package.ps1 -GameDir "C:\Program Files (x86)\Steam\steamapps\common\Night Shippers"
```

That writes all three zips, the Pack included. Use `-SkipPack` to build only the
mod and source zips.

## Why the loader build matters here

Night Shippers is UE 5.5 and the tagged UE4SS releases predate it — they will not
load. Only the experimental/CI builds work, which is the whole reason a Pack is
worth having: "go download UE4SS" sends people to a release that silently does
nothing. The build this was developed against reports
`v3.0.1 Beta (Git SHA 2bfa839f)`.

UE4SS is MIT licensed, Copyright (c) 2022 Narknon.
