"""Remove the player's learned-spell cap."""

import Game


def player_spell_limit_reached(self):
	"""The player may always learn another spell."""
	return False


Game.Game.player_spell_limit_reached = player_spell_limit_reached
