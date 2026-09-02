# The built .pck is not included

The mod ships as a full replacement `Die for the Lich.pck` (84 MB). That file is the
game's entire data package with the mod's GDScript patched in, so publishing it would
redistribute the whole game. It is deliberately left out of this repo.

To install:
1. Build a modded pck by applying `source\` over an extracted copy of your own
   `Die for the Lich.pck` (game v0.8.2 / Godot 4.4.1), or copy the prebuilt one from
   `C:\Games\Die.For.The.Lich\game\forward_plus\meta_progression_mod\build\`.
2. Put it at `meta_progression_mod\build\Die for the Lich.meta-progression.pck`.
3. Run `install.ps1`, and `uninstall.ps1` to revert. Keep a copy of your original pck.

Note: `install.ps1` verifies SHA-256
`B6054E5E5C4176AE8521C041AE5E6BF1EC2012D9E95558B2954F97337499DC37`, so a rebuilt pck
will fail that check until you update the expected hash.
