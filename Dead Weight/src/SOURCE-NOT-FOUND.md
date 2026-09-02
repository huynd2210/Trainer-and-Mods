# Binary not included, and no source exists

`Dead_weight.trainer.exe` is 902 MB - the entire Dead Weight game executable
(v1.0.1.10 / Godot 4.4.1) with the trainer patched into it. Publishing it would
redistribute the whole game, so it is left out.

What is here:
- `../README.md` - hotkeys and usage
- `Launch Dead Weight Trainer.cmd` / `.ps1` - the launcher, which backs up saves
  before starting the patched exe

The patched exe lives locally at
`C:\Games\Trainers and Mods\Dead Weight Trainer\Dead_weight.trainer.exe`, together
with the Steam/analytics DLLs it hard-links from the game install.

No source or patch tooling for this trainer was found anywhere under `C:\Games`.
