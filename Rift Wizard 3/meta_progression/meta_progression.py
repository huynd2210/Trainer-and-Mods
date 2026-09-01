"""A permanent, purchasable progression system for Rift Wizard 3."""

import random
import sys

import Game as GameModule
import LevelGen
import SteamAdapter


STATS_KEY = 'meta_progression'
CURRENCY = 'Echoes'
VERSION = 2
MAX_RECORDED_RUNS = 500
MENU_TARGET = 'meta_progression_menu'

UPGRADES = (
	('heirloom', 'Wanderer\'s Heirloom', (10,), 'Begin each run with one random equipment.'),
	('starting_sp', 'Awakened Memory', (5, 10, 15, 20, 25), 'Gain +1 starting SP per rank.'),
	('extra_orb', 'Overflowing Memory', (20,), 'One additional SP orb spawns in every realm.'),
	('extra_component', 'Salvager\'s Instinct', (30,), 'One additional component spawns in every realm.'),
	('extra_rift', 'Fractured Horizon', (20,), 'One additional Rift spawns in every non-final realm.'),
	('realm_shield', 'Aegis of Return', (30,), 'Gain 1 SH at the start of every realm.'),
)


def _default_progress():
	return {
		'version': VERSION,
		'echoes': 0,
		'completed_realms': 0,
		'victories': 0,
		'recorded_runs': [],
		'upgrades': {},
	}


def get_progress():
	SteamAdapter.default_vals.setdefault(STATS_KEY, _default_progress())
	progress = SteamAdapter.stats.get(STATS_KEY)
	if not isinstance(progress, dict):
		progress = _default_progress()
		SteamAdapter.stats[STATS_KEY] = progress

	# Version 1 used an automatic-bonus currency named essence. Preserve it.
	if 'echoes' not in progress:
		progress['echoes'] = progress.get('essence', 0)
	for key in ('echoes', 'completed_realms', 'victories'):
		try: progress[key] = max(0, int(progress.get(key, 0)))
		except (TypeError, ValueError): progress[key] = 0

	runs = progress.get('recorded_runs', [])
	if not isinstance(runs, (list, tuple, set)): runs = []
	progress['recorded_runs'] = list(runs)[-MAX_RECORDED_RUNS:]
	if not isinstance(progress.get('upgrades'), dict): progress['upgrades'] = {}
	for key, _name, costs, _desc in UPGRADES:
		try: rank = int(progress['upgrades'].get(key, 0))
		except (TypeError, ValueError): rank = 0
		progress['upgrades'][key] = max(0, min(rank, len(costs)))
	progress['version'] = VERSION
	return progress


def rank(key):
	return get_progress()['upgrades'].get(key, 0)


def buy_upgrade(key):
	progress = get_progress()
	for upgrade_key, _name, costs, _desc in UPGRADES:
		if upgrade_key != key: continue
		current = progress['upgrades'][key]
		if current >= len(costs): return False
		cost = costs[current]
		if progress['echoes'] < cost: return False
		progress['echoes'] -= cost
		progress['upgrades'][key] = current + 1
		SteamAdapter.save_stats()
		return True
	return False


def completed_realms(game, victory):
	level_num = max(1, int(getattr(game, 'level_num', 1)))
	return level_num if victory or getattr(game, 'level_cleared', False) else max(0, level_num - 1)


def record_finished_run(game, victory):
	progress = get_progress()
	run_number = getattr(game, 'run_number', None)
	if run_number is None or str(run_number) in progress['recorded_runs']: return 0
	realms = completed_realms(game, victory)
	award = realms + (10 if victory else 0)
	progress['echoes'] += award
	progress['completed_realms'] += realms
	progress['victories'] += int(bool(victory))
	progress['recorded_runs'].append(str(run_number))
	progress['recorded_runs'] = progress['recorded_runs'][-MAX_RECORDED_RUNS:]
	SteamAdapter.save_stats()
	return award


def add_bonus_component(generator):
	pools = LevelGen.t1_components + LevelGen.t2_components + LevelGen.t3_components
	if pools: generator.components.append(generator.random.choice(pools)())


def patch_level_generation():
	if getattr(LevelGen.LevelGenerator, '_meta_progression_patched', False): return
	original_init = LevelGen.LevelGenerator.__init__

	def levelgen_init(self, *args, **kwargs):
		original_init(self, *args, **kwargs)
		if rank('extra_orb'): self.num_xp += 1
		if rank('extra_component'): add_bonus_component(self)
		if rank('extra_rift') and self.difficulty < GameModule.LAST_LEVEL: self.num_exits += 1

	LevelGen.LevelGenerator.__init__ = levelgen_init
	LevelGen.LevelGenerator._meta_progression_patched = True


def patch_game():
	if getattr(GameModule.Game, '_meta_progression_patched', False): return
	original_init = GameModule.Game.__init__
	original_finalize = GameModule.Game.finalize_save
	original_deploy = GameModule.Game.try_deploy

	def game_init(self, *args, **kwargs):
		original_init(self, *args, **kwargs)
		self.p1.xp += rank('starting_sp')
		if rank('heirloom') and self.all_player_items:
			self.p1.equip(random.choice(self.all_player_items))
		if rank('realm_shield'): self.p1.add_shields(1)
		if kwargs.get('save_enabled', False): self.save_game()

	def finalize_save(self, victory):
		record_finished_run(self, victory)
		return original_finalize(self, victory)

	def try_deploy(self, x, y):
		result = original_deploy(self, x, y)
		if result and rank('realm_shield'): self.p1.add_shields(1)
		return result

	GameModule.Game.__init__ = game_init
	GameModule.Game.finalize_save = finalize_save
	GameModule.Game.try_deploy = try_deploy
	GameModule.Game._meta_progression_patched = True


def patch_menu(main):
	if not main or not hasattr(main, 'PyGameView'): return
	if getattr(main.PyGameView, '_meta_progression_menu_patched', False): return
	original_entries = main.PyGameView._get_title_menu_entries
	original_draw = main.PyGameView.draw_title
	original_process = main.PyGameView.process_title_input

	def entries(self):
		result = list(original_entries(self))
		if MENU_TARGET not in result:
			try: result.insert(result.index(main.TITLE_SELECTION_MODS), MENU_TARGET)
			except ValueError: result.append(MENU_TARGET)
		return result

	def draw_meta_menu(self):
		progress = get_progress()
		self.screen.fill((0, 0, 0))
		width = self.screen.get_width()
		x = width // 8
		y = self.linesize * 3
		self.draw_string('LEGACY', self.screen, x, y, (255, 255, 255))
		y += self.linesize * 2
		self.draw_string('%d %s available' % (progress['echoes'], CURRENCY), self.screen, x, y, (255, 220, 80))
		y += self.linesize * 2
		for key, name, costs, desc in UPGRADES:
			current = progress['upgrades'][key]
			maximum = len(costs)
			if current >= maximum:
				label, color = '%s  (%d/%d)  OWNED' % (name, current, maximum), (120, 220, 120)
			else:
				cost = costs[current]
				label = '%s  (%d/%d)  %d %s' % (name, current, maximum, cost, CURRENCY)
				color = (255, 255, 255) if progress['echoes'] >= cost else (150, 150, 150)
			self.draw_string(label, self.screen, x, y, color, mouse_content=key)
			y += self.linesize
			self.draw_string(desc, self.screen, x + self.linesize, y, (180, 180, 180))
			y += self.linesize * 2
		self.draw_string('Confirm/click to purchase. Escape to return.', self.screen, x, y, (180, 180, 180))

	def draw_title(self):
		if getattr(self, 'meta_progression_open', False): return draw_meta_menu(self)
		# The vanilla renderer owns a closed label dictionary, so render the same
		# compact list here with our additional entry included.
		screen_width = self.screen.get_width()
		cur_y = self.screen.get_height() // 2 + main.SPRITE_SIZE * 3
		cur_frame = (main.cloud_frame_clock // main.SUB_FRAMES[main.ANIM_IDLE]) % 4
		self.screen.blit(self.title_frames[cur_frame], (0, 0))
		labels = {
			main.TITLE_SELECTION_LOAD: 'CONTINUE RUN', main.TITLE_SELECTION_ABANDON: 'ABANDON RUN',
			main.TITLE_SELECTION_NEW: 'NEW GAME', main.TITLE_SELECTION_OPTIONS: 'OPTIONS',
			main.TITLE_SELECTION_HELP: 'HOW TO PLAY', main.TITLE_SELECTION_BESTIARY: 'BESTIARY',
			main.TITLE_SELECTION_DISCORD: 'DISCORD', main.TITLE_SELECTION_MODS: 'MODS',
			main.TITLE_SELECTION_CREDITS: 'CREDITS', main.TITLE_SELECTION_EXIT: 'QUIT',
			MENU_TARGET: 'LEGACY',
		}
		menu_entries = self._get_title_menu_entries()
		# Other mods can add their own string targets to the title menu.  Keep
		# this renderer compatible with them instead of assuming every target is
		# one of the vanilla constants known above.
		for option in menu_entries:
			if option not in labels:
				labels[option] = str(option).replace('_', ' ').upper()
		rect_w = max(self.font.size(labels[option])[0] for option in menu_entries)
		cur_x = screen_width // 2 - rect_w // 2
		for option in menu_entries:
			label = labels[option]
			line_w = self.font.size(label)[0]
			self.draw_string(label, self.screen, cur_x, cur_y, (255, 255, 255), mouse_content=option, content_width=line_w)
			cur_y += self.linesize + 2

	def process_title_input(self):
		if getattr(self, 'meta_progression_open', False):
			mouse = self.get_mouse_pos()
			keys = [u[0] for u in UPGRADES]
			for evt in self.events:
				if evt.type == main.pygame.KEYDOWN and evt.key in self.key_binds[main.KEY_BIND_ABORT]:
					self.meta_progression_open = False; self.examine_target = MENU_TARGET; self.play_sound('menu_abort'); return
				if evt.type == main.pygame.KEYDOWN and evt.key in self.key_binds[main.KEY_BIND_UP]:
					index = keys.index(self.examine_target) if self.examine_target in keys else 0
					self.examine_target = keys[(index - 1) % len(keys)]; self.play_sound('menu_confirm'); return
				if evt.type == main.pygame.KEYDOWN and evt.key in self.key_binds[main.KEY_BIND_DOWN]:
					index = keys.index(self.examine_target) if self.examine_target in keys else 0
					self.examine_target = keys[(index + 1) % len(keys)]; self.play_sound('menu_confirm'); return
				if evt.type == main.pygame.KEYDOWN and evt.key in self.key_binds[main.KEY_BIND_CONFIRM]:
					if buy_upgrade(self.examine_target): self.play_sound('menu_confirm')
					return
				if evt.type == main.pygame.MOUSEBUTTONDOWN and evt.button == main.pygame.BUTTON_LEFT:
					target = self.get_ui_target(*mouse)
					if target and buy_upgrade(target): self.play_sound('menu_confirm')
					self.examine_target = target; return
			dx, dy = self.get_mouse_rel()
			if abs(dx) + abs(dy) > 1:
				target = self.get_ui_target(*mouse)
				if target in keys: self.examine_target = target
			return

		# Let the base title handler navigate normally, but intercept activation.
		was_target = self.examine_target
		for evt in self.events:
			confirmed = evt.type == main.pygame.KEYDOWN and evt.key in self.key_binds[main.KEY_BIND_CONFIRM]
			clicked = evt.type == main.pygame.MOUSEBUTTONDOWN and evt.button == main.pygame.BUTTON_LEFT and self.get_ui_target(*self.get_mouse_pos()) == MENU_TARGET
			if (confirmed and was_target == MENU_TARGET) or clicked:
				self.meta_progression_open = True
				self.examine_target = UPGRADES[0][0]
				self.play_sound('menu_confirm')
				return
		return original_process(self)

	main.PyGameView._get_title_menu_entries = entries
	main.PyGameView.draw_title = draw_title
	main.PyGameView.process_title_input = process_title_input
	main.PyGameView._meta_progression_menu_patched = True


get_progress()
patch_level_generation()
patch_game()
patch_menu(sys.modules.get('__main__') or sys.modules.get('RiftWizard3'))
