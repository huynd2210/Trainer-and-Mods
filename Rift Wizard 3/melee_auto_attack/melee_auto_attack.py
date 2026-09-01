import Game
import text
from mods.auto_attack_runtime import enable_melee


enable_melee()

help_line = "Move into an adjacent enemy to use your 1 [Physical] damage Basic Attack"
if help_line not in text.how_to_play_controls:
	text.how_to_play_controls = text.how_to_play_controls.rstrip() + "\n\n" + help_line + "\n"
