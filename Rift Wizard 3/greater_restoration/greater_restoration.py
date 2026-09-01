import Spells
from Level import *


class GreaterRestoration(Spell):

	def on_init(self):
		self.name = "Greater Restoration"
		self.level = 3
		self.tags = [Tags.Holy, Tags.Enchantment]
		self.asset = ['status', 'clarity']
		self.range = 0
		self.max_charges = 3

	def get_description(self):
		return "Remove all debuffs and gain [clarity] for 1 turn."

	def cast_instant(self, x, y):
		for buff in list(self.caster.buffs):
			if buff.buff_type == BUFF_TYPE_CURSE:
				self.caster.remove_buff(buff)
		self.caster.apply_buff(StunImmune(), 1)


if not any(existing.__name__ == GreaterRestoration.__name__ for existing in Spells.all_player_spell_constructors):
	Spells.all_player_spell_constructors.append(GreaterRestoration)
