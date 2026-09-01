import sys
from copy import copy

import LevelGen
import SteamAdapter


def migrate_bestiary_kills():
	SteamAdapter.default_vals.setdefault('bestiary_kills', {})
	stats = SteamAdapter.stats
	kills = stats.get('bestiary_kills', {})
	if not isinstance(kills, dict):
		try: kills = dict(kills)
		except Exception: kills = {}
		stats['bestiary_kills'] = kills

	changed = False
	for monster_name in stats.setdefault('bestiary', set()):
		if kills.get(monster_name, 0) >= 1: continue
		kills[monster_name] = 1
		changed = True

	if changed: SteamAdapter.save_stats()


def unlock_bestiary(monster_name, count_kill=True):
	if not monster_name: return

	stats = SteamAdapter.stats
	kills = stats.setdefault('bestiary_kills', {})
	bestiary = stats.setdefault('bestiary', set())

	new_slain = False
	if monster_name not in bestiary:
		bestiary.add(monster_name)
		new_slain = True

	if count_kill:
		kills[monster_name] = kills.get(monster_name, 0) + 1
	elif new_slain and kills.get(monster_name, 0) < 1:
		kills[monster_name] = 1

	SteamAdapter.save_stats()
	if new_slain: SteamAdapter.check_bestiary_ach()


def get_bestiary_kill_count(monster_name):
	return SteamAdapter.stats.setdefault('bestiary_kills', {}).get(monster_name, 0)


def get_total_bestiary_kills():
	kills = SteamAdapter.stats.setdefault('bestiary_kills', {})
	return sum(kills.get(m, 0) for m in LevelGen.all_monster_names)


def patch_steam_adapter():
	migrate_bestiary_kills()
	SteamAdapter.unlock_bestiary = unlock_bestiary
	SteamAdapter.get_bestiary_kill_count = get_bestiary_kill_count
	SteamAdapter.get_total_bestiary_kills = get_total_bestiary_kills


def patch_unit_sprite(main):
	if not main or not hasattr(main, 'UnitSprite'): return

	def on_death(self, evt):
		main_view = getattr(main, 'main_view', None)

		if evt.damage_event:
			s = copy(self)
			s.x = self.unit.x
			s.y = self.unit.y
			s.flash_type = main.FLASH_DEATH
			s.flash_sub_frame = 0
			s.finished = False

			if self.unit.is_player_controlled or self.unit.is_final_boss:
				main_view.effects = [e for e in main_view.effects if not ((e.x == self.unit.x) and (e.y == self.unit.y))]
			main_view.queue_effect(s, death_effect=True)

		if self.unit.is_player_controlled:
			main_view.play_music('lose_theme')
			main_view.play_sound('death_player')
		else:
			if self.unit.radius: main_view.play_sound('death_boss')
			else: main_view.play_sound('death_enemy')

		SteamAdapter.unlock_bestiary(self.unit.name)
		if self.unit.parent: SteamAdapter.unlock_bestiary(self.unit.parent.name, count_kill=False)

	main.UnitSprite.on_death = on_death


def patch_bestiary_view(main):
	if not main or not hasattr(main, 'PyGameView'): return

	def draw_bestiary_shop(self):
		self.shop_rects = []
		self.shop_rows_per_page = max(1, self.max_shop_objects - 2)
		self.character_display.fill((0, 0, 0))
		self.middle_menu_display.fill((0, 0, 0))

		panel = self.character_display
		name_x = self.border_margin
		cur_y = self.linesize
		content_width = panel.get_width() - 2 * self.border_margin
		shoptions = self.get_shop_options()

		self.draw_string(("Bestiary: {cur}/{total} Total Kills: {kills}", {
			"cur": SteamAdapter.get_num_slain(),
			"total": len(LevelGen.all_monsters),
			"kills": SteamAdapter.get_total_bestiary_kills(),
		}), panel, name_x, cur_y, content_width=content_width)

		search_y = cur_y + self.linesize * 2
		self.draw_shop_search_bar(panel, name_x, search_y)

		cur_y = search_y + self.linesize * 2

		if not shoptions: self.draw_string("No Results", panel, name_x, cur_y, content_width=content_width)

		rows_per_page = self.get_shop_rows_per_page()
		start_index = self.shop_page * rows_per_page
		end_index = start_index + rows_per_page

		for opt in shoptions[start_index:end_index]:
			fmt = opt.name
			cur_color = (255, 255, 255)
			if not SteamAdapter.has_slain(opt.name):
				fmt = "?????????????????????"
				cur_color = main.UNAVAILABLE_COLOR
			else:
				fmt = "%s x%d" % (opt.name, SteamAdapter.get_bestiary_kill_count(opt.name))

			self.draw_string(fmt, panel, name_x, cur_y, cur_color, mouse_content=opt, content_width=content_width)
			cur_y += self.linesize

		cur_y = self.linesize * (self.max_shop_objects+4)
		self.draw_shop_page_controls(panel, name_x, cur_y)

		self.screen.blit(self.character_display, (0, 0))
		self.screen.blit(self.middle_menu_display, (self.h_margin, 0))

	main.PyGameView.draw_bestiary_shop = draw_bestiary_shop


main = sys.modules.get('__main__') or sys.modules.get('RiftWizard3')
patch_steam_adapter()
patch_unit_sprite(main)
patch_bestiary_view(main)
