# Sephiria stat buff (v1.0.33)

The buff is enabled by default for the local host player. Press **F8** to turn it on or off. Edit `SephiriaStatBuff.cfg` in the game folder and press **F9** to reload the values without restarting. Reload also applies the file's `Enabled` setting.

The values add to the game's existing stats. `AllDamageBonus`, `AttackSpeed`, and `DamageReduction` are percentage-point bonuses. `HPRegen`, `MPRegen`, `Luck`, and the other entries use the game's stat units. Set an entry to `0` to leave that stat alone. The mod removes only its own contributions when turned off.

Run `install.ps1` or `uninstall.ps1` with the game closed. This mod is separate from the currency trainer and its uninstall process preserves that trainer. The config file is retained on uninstall. It applies to solo play or the local player hosting a session; it does not modify a remote server's stats.
