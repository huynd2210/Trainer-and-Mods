import Game
import text
from mods.auto_attack_runtime import enable_ranged


enable_ranged()

help_line = "[X:shields]: Target your 1 [Physical] damage, range 5 Ranged Attack"
if help_line not in text.how_to_play_controls:
	text.how_to_play_controls = text.how_to_play_controls.rstrip() + "\n\n" + help_line + "\n"
