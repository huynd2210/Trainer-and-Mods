import sys

from Level import StunImmune, Tags


EQUIPMENT_IMMUNITIES = {
	'ColdIronHeart': 'Fear',
	'SunforgedIris': 'Blind',
	'RunespeakersGorget': 'Silence',
	'MoltenCarapace': 'Freeze',
	'PlagueDoctorsmask': 'Necrosis',
}

CLARITY_IMMUNITIES = ('Stun', 'Freeze', 'Fear', 'Petrify', 'Silence', 'Sleep')


def get_immunities(player):
	"""Return the effects the player is currently guaranteed to ignore."""
	immunities = []

	if getattr(player, 'debuff_immune', False):
		immunities.append('All debuffs')
	else:
		if player.has_buff(StunImmune):
			immunities.extend(CLARITY_IMMUNITIES)

		for item in getattr(player, 'equipment', ()):
			immunity = EQUIPMENT_IMMUNITIES.get(type(item).__name__)
			if immunity: immunities.append(immunity)

	# These resistances also prevent the corresponding status from applying.
	if player.resists.get(Tags.Poison, 0) >= 100:
		immunities.append('Poison')
	if player.resists.get(Tags.Dark, 0) >= 100:
		immunities.append('Necrosis')

	if getattr(player, 'is_invulnerable', lambda: False)():
		immunities.append('All damage')
	else:
		for tag in Tags:
			if tag == Tags.Heal: continue
			if player.resists.get(tag, 0) >= 100:
				immunities.append('%s damage' % tag.name)

	# Preserve the useful grouping above while removing overlaps from gear/buffs.
	return list(dict.fromkeys(immunities))


def wrap_items(font, items, width, max_lines=3):
	lines = []
	current = ''
	for item in items:
		candidate = item if not current else current + ', ' + item
		if current and font.size(candidate)[0] > width:
			lines.append(current)
			current = item
			if len(lines) == max_lines: break
		else:
			current = candidate
	if current and len(lines) < max_lines: lines.append(current)

	shown = sum(line.count(',') + 1 for line in lines)
	if shown < len(items) and lines:
		while lines[-1] and font.size(lines[-1] + ' ...')[0] > width:
			lines[-1] = lines[-1].rsplit(', ', 1)[0] if ', ' in lines[-1] else lines[-1][:-1]
		lines[-1] += ' ...'
	return lines


def patch_immunities_ui(main):
	if not main or not hasattr(main, 'PyGameView'): return
	if getattr(main.PyGameView, '_immunities_ui_patched', False): return

	original_draw_character = main.PyGameView.draw_character

	def draw_character(self):
		original_draw_character(self)

		player = self.game.p1
		immunities = get_immunities(player)
		if not immunities: return

		panel = self.character_display
		margin = self.border_margin
		shrink_level = getattr(player, 'shrink_text', 0)
		if hasattr(self, 'get_font_variant'):
			font, linesize = self.get_font_variant(shrink_level)
		else:
			font, linesize = self.font, self.linesize
		font_kwargs = {'font': font, 'linesize_override': linesize} if hasattr(self, 'get_font_variant') else {}

		x = max(panel.get_width() * 48 // 100, margin)
		width = panel.get_width() - margin - x
		y = margin + linesize
		tooltip = main.TooltipExamineTarget('Current immunities:\n' + '\n'.join(immunities))

		self.draw_string('Immunities', panel, x, y, color=(255, 255, 255), mouse_content=tooltip,
			content_width=width, pre_resolved=True, **font_kwargs)
		y += linesize
		for line in wrap_items(font, immunities, width):
			self.draw_string(line, panel, x, y, color=(190, 220, 255), mouse_content=tooltip,
				content_width=width, pre_resolved=True, **font_kwargs)
			y += linesize

		self.screen.blit(panel, (0, 0))

	main.PyGameView.draw_character = draw_character
	main.PyGameView._immunities_ui_patched = True


main = sys.modules.get('__main__')
if not main or not hasattr(main, 'PyGameView'):
	main = sys.modules.get('RiftWizard3')
patch_immunities_ui(main)
