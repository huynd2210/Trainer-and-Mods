try:
	# Source-runtime path. The packaged executable does not expose this module.
	import Passives
	from Passives import Cleanse, DeathWard, LastDitch, SoulForge

	for passive in [Cleanse, DeathWard, LastDitch, SoulForge]:
		if not any(existing.__name__ == passive.__name__ for existing in Passives.all_player_passive_constructors):
			Passives.all_player_passive_constructors.append(passive)

except ModuleNotFoundError as exc:
	if exc.name != 'Passives': raise

	# Frozen-executable compatibility path. Permanent Upgrade instances provide
	# save-safe event subscriptions without occupying spell slots or being cast.
	from Level import *
	import Game
	import Spells


	def trigger_passive(passive):
		if passive.cur_charges <= 0: return False
		passive.cur_charges -= 1
		passive.owner.level.show_effect(passive.owner.x, passive.owner.y, Tags.Buff_Apply)
		return True


	class PackagedPassive(Upgrade):

		def refresh_charges(self, evt=None):
			self.cur_charges = self.max_charges

		def get_description(self):
			return "Charges: %d/%d\n%s" % (self.cur_charges, self.max_charges, self.description)


	class CleansePassive(PackagedPassive):

		def on_init(self):
			self.name = "Cleanse"
			self.level = 1
			self.tags = [Tags.Arcane]
			self.asset = ['status', 'clarity']
			self.max_charges = 3
			self.cur_charges = self.max_charges
			self.owner_triggers[EventOnBuffApply] = self.on_buff_apply
			self.owner_triggers[EventOnUnitAdded] = self.refresh_charges
			self.description = ("When you receive a disabling control debuff, spend 1 charge to remove it "
				"and gain clarity for 3 turns. Passive: triggering is not a cast and does not progress the turn.")

		def on_buff_apply(self, evt):
			if self.cur_charges <= 0: return
			if evt.buff.buff_type != BUFF_TYPE_CURSE: return
			if not isinstance(evt.buff, (Stun, Silence)): return

			self.owner.remove_buff(evt.buff)
			self.owner.apply_buff(StunImmune(), 3)
			trigger_passive(self)


	class PackagedInvulnerable(Buff):

		def on_init(self):
			self.name = "Invulnerable"
			self.buff_type = BUFF_TYPE_BLESS
			self.stack_type = STACK_REPLACE
			self.asset = ['status', 'protection']
			self.is_invulnerable_buff = True
			self.description = "Takes 0 damage."
			for tag in Tags:
				if tag != Tags.Heal:
					self.resists[tag] = 100


	class DeathWardPassive(PackagedPassive):

		def on_init(self):
			self.name = "Death Ward"
			self.level = 3
			self.tags = [Tags.Holy]
			self.asset = ['status', 'protection']
			self.max_charges = 1
			self.cur_charges = self.max_charges
			self.owner_triggers[EventOnDamaged] = self.on_damaged
			self.owner_triggers[EventOnUnitAdded] = self.refresh_charges
			self.description = ("When damage would kill you, spend 1 charge to remain at 1 HP and become invulnerable "
				"until the end of your next turn. Other lethal-damage passives resolve in purchase order. "
				"Passive: triggering is not a cast and does not progress the turn.")

		def on_damaged(self, evt):
			if self.cur_charges <= 0 or self.owner.cur_hp > 0: return
			self.owner.cur_hp = 1
			duration = 2 if getattr(self.owner.level, 'current_turn_unit', None) is self.owner else 1
			self.owner.apply_buff(PackagedInvulnerable(), duration)
			trigger_passive(self)


	class LastDitchPassive(PackagedPassive):

		def on_init(self):
			self.name = "Last Ditch"
			self.level = 3
			self.tags = [Tags.Dark]
			self.asset = ['status', 'necrosis']
			self.max_charges = 1
			self.cur_charges = self.max_charges
			if not hasattr(self, 'active'): self.active = False
			self.last_ditch_passive_state = True
			self.owner_triggers[EventOnDamaged] = self.on_damaged
			self.owner_triggers[EventOnUnitAdded] = self.refresh_charges
			self.global_triggers[EventOnDeath] = self.on_death
			self.description = ("When damage would kill you, spend 1 charge to heal to 100% HP, then lose 20% of your "
				"current max HP each turn. Directly killing an enemy whose max HP exceeds yours ends the loss and sets "
				"your HP to 20% of max HP. This is passive state, not a removable buff or debuff. Other lethal-damage "
				"passives resolve in purchase order.")

		def get_description(self):
			active = "\nStatus: Active" if self.active else ""
			return "Charges: %d/%d%s\n%s" % (self.cur_charges, self.max_charges, active, self.description)

		def on_damaged(self, evt):
			if self.cur_charges <= 0 or self.owner.cur_hp > 0: return
			self.owner.cur_hp = self.owner.max_hp
			self.active = True
			trigger_passive(self)

		def on_advance(self):
			if not self.active: return
			max_hp_loss = max(1, (self.owner.max_hp + 4) // 5)
			self.owner.max_hp = max(1, self.owner.max_hp - max_hp_loss)
			self.owner.cur_hp = min(self.owner.cur_hp, self.owner.max_hp)

		def on_death(self, evt):
			if not self.active: return
			if not are_hostile(self.owner, evt.unit): return
			if evt.unit.max_hp <= self.owner.max_hp: return
			if not evt.damage_event or not evt.damage_event.source: return
			if evt.damage_event.source.owner != self.owner: return
			self.active = False
			self.owner.cur_hp = max(1, self.owner.max_hp // 5)
			self.owner.level.show_effect(self.owner.x, self.owner.y, Tags.Heal)


	class SoulForgePassive(PackagedPassive):

		def on_init(self):
			self.name = "Soul Forge"
			self.level = 3
			self.tags = [Tags.Dark]
			self.asset = ['status', 'necrosis']
			self.max_charges = 0
			self.cur_charges = 0
			if not hasattr(self, 'deaths'): self.deaths = 0
			self.global_triggers[EventOnDeath] = self.on_death
			self.description = "Every 5 enemies that die grants you +1 max HP."

		def get_description(self):
			return "Progress: %d/5\n%s" % (self.deaths, self.description)

		def on_death(self, evt):
			if not are_hostile(self.owner, evt.unit): return
			self.deaths += 1
			if self.deaths < 5: return
			self.deaths -= 5
			self.owner.max_hp += 1
			self.owner.level.show_effect(self.owner.x, self.owner.y, Tags.Buff_Apply)


	class PassivePurchase(Spell):
		passive_class = None

		def on_init(self):
			passive = self.passive_class()
			self.name = passive.name
			self.level = passive.level
			self.tags = list(passive.tags)
			self.asset = passive.asset
			self.range = 0
			self.max_charges = 0
			self.is_passive_purchase = True

		def get_description(self):
			return self.passive_class().get_description()

		def can_cast(self, x, y):
			return False

		def cast_instant(self, x, y):
			return


	class Cleanse(PassivePurchase):
		passive_class = CleansePassive


	class DeathWard(PassivePurchase):
		passive_class = DeathWardPassive


	class LastDitch(PassivePurchase):
		passive_class = LastDitchPassive


	class SoulForge(PassivePurchase):
		passive_class = SoulForgePassive


	def owns_passive(game, purchase):
		return any(getattr(buff, 'packaged_passive', False) and buff.name == purchase.name for buff in game.p1.buffs)


	for passive_class in [CleansePassive, DeathWardPassive, LastDitchPassive, SoulForgePassive]:
		passive_class.packaged_passive = True

	if not getattr(Game.Game, '_passives_pack_compat_patched', False):
		original_has_upgrade = Game.Game.has_upgrade
		original_can_buy_upgrade = Game.Game.can_buy_upgrade
		original_buy_upgrade = Game.Game.buy_upgrade
		original_record_spell_purchased = Game.Game.record_spell_purchased

		def has_upgrade(self, upgrade):
			if getattr(upgrade, 'is_passive_purchase', False):
				return owns_passive(self, upgrade)
			return original_has_upgrade(self, upgrade)

		def can_buy_upgrade(self, upgrade):
			if getattr(upgrade, 'is_passive_purchase', False):
				return not owns_passive(self, upgrade) and self.p1.xp >= self.get_upgrade_cost(upgrade)
			return original_can_buy_upgrade(self, upgrade)

		def buy_upgrade(self, upgrade, free=False):
			if not getattr(upgrade, 'is_passive_purchase', False):
				return original_buy_upgrade(self, upgrade, free)
			if not free: self.p1.xp -= self.get_upgrade_cost(upgrade)
			self.p1.apply_buff(upgrade.passive_class())

		def record_spell_purchased(self, spell):
			if getattr(spell, 'is_passive_purchase', False): return
			return original_record_spell_purchased(self, spell)

		Game.Game.has_upgrade = has_upgrade
		Game.Game.can_buy_upgrade = can_buy_upgrade
		Game.Game.buy_upgrade = buy_upgrade
		Game.Game.record_spell_purchased = record_spell_purchased
		Game.Game._passives_pack_compat_patched = True

	if not getattr(Unit, '_passives_pack_invulnerability_patched', False):
		original_is_invulnerable = getattr(Unit, 'is_invulnerable', None)

		def is_invulnerable(self):
			if any(getattr(buff, 'is_invulnerable_buff', False) for buff in self.buffs): return True
			return bool(original_is_invulnerable and original_is_invulnerable(self))

		Unit.is_invulnerable = is_invulnerable
		Unit._passives_pack_invulnerability_patched = True

	if not getattr(Unit, '_passives_pack_removal_patched', False):
		original_remove_buff = Unit.remove_buff

		def remove_buff(self, buff):
			if getattr(buff, 'last_ditch_passive_state', False): return
			return original_remove_buff(self, buff)

		Unit.remove_buff = remove_buff
		Unit._passives_pack_removal_patched = True

	for passive in [Cleanse, DeathWard, LastDitch, SoulForge]:
		if not any(existing.__name__ == passive.__name__ for existing in Spells.all_player_spell_constructors):
			Spells.all_player_spell_constructors.append(passive)


import sys


def patch_passive_hud(main):
	if not main or not hasattr(main, 'PyGameView'): return
	if getattr(main.PyGameView, '_passives_pack_hud_patched', False): return

	original_draw_character = main.PyGameView.draw_character
	original_draw_examine_upgrade = main.PyGameView.draw_examine_upgrade

	def draw_character(self):
		original_draw_character(self)

		passives = [buff for buff in self.game.p1.buffs if getattr(buff, 'packaged_passive', False)]
		if not passives: return

		panel = self.character_display
		margin = self.border_margin
		shrink_level = getattr(self.game.p1, 'shrink_text', 0)
		if hasattr(self, 'get_font_variant'):
			font, linesize = self.get_font_variant(shrink_level)
		else:
			font, linesize = self.font, self.linesize
		font_kwargs = {"font": font, "linesize_override": linesize} if hasattr(self, 'get_font_variant') else {}

		bottom_rows = 3  # Menu, How to Play, Character Sheet
		if self.game.mutators: bottom_rows += 1
		if getattr(main, 'cheats_enabled', False): bottom_rows += 1
		draw_combat_log = getattr(self, 'draw_combat_log_button', False) or self.game.gameover or self.game.victory
		if draw_combat_log: bottom_rows += 1
		elif getattr(self.game, 'rift_rerolls', 0): bottom_rows += 1

		section_rows = len(passives) + 2
		cur_y = panel.get_height() - margin - (bottom_rows + section_rows) * linesize
		cur_y = max(margin, cur_y)
		width = panel.get_width() - 2 * margin

		# Keep the owned-passive list readable regardless of long equipment/status lists.
		background = main.pygame.Rect(0, cur_y, panel.get_width(), section_rows * linesize)
		panel.fill((0, 0, 0), background)
		self.draw_string("Passives", panel, margin, cur_y, content_width=width, **font_kwargs)
		cur_y += linesize

		icon_size = min(getattr(main, 'FONT_SIZE', linesize), linesize)
		name_x = getattr(self, 'spell_name_x', margin + icon_size + 6)
		icon_x = getattr(self, 'spell_icon_x', margin)
		charges_x = getattr(self, 'spell_charges_x', panel.get_width() - margin)
		for passive in passives:
			y_icon = cur_y + (linesize - icon_size) // 2
			if hasattr(self, 'draw_scaled_icon'):
				self.draw_scaled_icon(passive, panel, icon_x, y_icon, size=icon_size)

			name = passive.name
			if getattr(passive, 'active', False): name += " [ACTIVE]"
			name_width = max(0, charges_x - name_x - font.size(str(passive.cur_charges))[0] - 10)
			if hasattr(main, 'fit_text_to_width'):
				name = main.fit_text_to_width(font, name, name_width)
			self.draw_string(name, panel, name_x, cur_y, mouse_content=passive, content_width=name_width, pre_resolved=True, **font_kwargs)

			charges = "%d/%d" % (passive.cur_charges, passive.max_charges)
			charge_width = font.size(charges)[0]
			self.draw_string(charges, panel, charges_x - charge_width, cur_y, mouse_content=passive, pre_resolved=True, **font_kwargs)
			cur_y += linesize

		self.screen.blit(panel, (0, 0))

	def draw_examine_upgrade(self):
		passive = self.examine_target
		if not getattr(passive, 'packaged_passive', False):
			return original_draw_examine_upgrade(self)

		# The frozen examiner assumes every Upgrade has a spell prerequisite and
		# unconditionally reads prereq.name. Supply a temporary display-only
		# prerequisite without changing the saved passive state.
		original_prereq = getattr(passive, 'prereq', None)
		if original_prereq is None: passive.prereq = passive
		try:
			return original_draw_examine_upgrade(self)
		finally:
			passive.prereq = original_prereq

	main.PyGameView.draw_character = draw_character
	main.PyGameView.draw_examine_upgrade = draw_examine_upgrade
	main.PyGameView._passives_pack_hud_patched = True


main = sys.modules.get('__main__')
if not main or not hasattr(main, 'PyGameView'):
	main = sys.modules.get('RiftWizard3')
patch_passive_hud(main)
