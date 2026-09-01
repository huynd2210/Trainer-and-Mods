import os
import sys
import textwrap
import time

import dill as pickle

import Game as GameModule
import SteamAdapter
from Level import EventOnDeath, are_hostile


RUN_HISTORY_FILE = 'run_history.dat'
RUN_HISTORY_LIMIT = 100
RUN_HISTORY_TARGET = 'run_history'
PAGE_WIDTH = 92
PAGE_LINES = 35


def load_history():
	if not os.path.exists(RUN_HISTORY_FILE):
		return []

	try:
		with open(RUN_HISTORY_FILE, 'rb') as history_file:
			history = pickle.load(history_file)
	except Exception:
		return []

	if isinstance(history, dict):
		history = history.get('runs', [])
	if not isinstance(history, list):
		return []
	return [entry for entry in history if isinstance(entry, dict)]


def save_history(history):
	history = history[-RUN_HISTORY_LIMIT:]
	try:
		with open(RUN_HISTORY_FILE, 'wb') as history_file:
			pickle.dump(history, file=history_file)
	except Exception:
		pass


def ensure_game_history(game):
	if not hasattr(game, 'run_history_kills') or not isinstance(game.run_history_kills, dict):
		game.run_history_kills = {}


def record_death(game, evt):
	unit = getattr(evt, 'unit', None)
	if not unit or getattr(unit, 'is_player_controlled', False):
		return

	player = getattr(game, 'p1', None)
	if player and not are_hostile(unit, player):
		return

	name = getattr(unit, 'name', None)
	if not name:
		return

	ensure_game_history(game)
	game.run_history_kills[name] = game.run_history_kills.get(name, 0) + 1


def install_death_tracker(game):
	if not game or not getattr(game, 'cur_level', None):
		return
	if not getattr(game.cur_level, 'event_manager', None):
		return

	ensure_game_history(game)
	event_manager = game.cur_level.event_manager
	if getattr(event_manager, 'run_history_registered', False):
		return

	def on_death(evt, tracked_game=game):
		record_death(tracked_game, evt)

	event_manager.register_global_trigger(EventOnDeath, on_death)
	event_manager.run_history_registered = True


def get_names(objects):
	names = []
	for obj in objects or []:
		name = getattr(obj, 'name', None)
		if name:
			names.append(name)
	return names


def get_upgrade_names(player):
	upgrades = []
	for buff in getattr(player, 'buffs', []):
		prereq = getattr(buff, 'prereq', None)
		if not prereq:
			continue
		name = getattr(buff, 'name', None)
		if not name:
			continue
		prereq_name = getattr(prereq, 'name', None)
		if prereq_name:
			upgrades.append("%s: %s" % (prereq_name, name))
		else:
			upgrades.append(name)
	return sorted(upgrades)


def summarize_run(game, outcome, include_current_turns=False):
	ensure_game_history(game)
	player = getattr(game, 'p1', None)
	kills = dict(getattr(game, 'run_history_kills', {}))
	realm_reached = getattr(game, 'level_num', 0)
	turns = getattr(game, 'total_turns', 0)
	if include_current_turns and getattr(game, 'cur_level', None):
		turns += getattr(game.cur_level, 'turn_no', 0)

	if outcome == 'VICTORY':
		levels_completed = realm_reached
	elif getattr(game, 'level_cleared', False):
		levels_completed = realm_reached
	else:
		levels_completed = max(0, realm_reached - 1)

	entry = {
		'version': 1,
		'run_number': getattr(game, 'run_number', None),
		'finished_at': int(time.time()),
		'outcome': outcome,
		'trial': getattr(game, 'trial_name', None),
		'realm_reached': realm_reached,
		'levels_completed': levels_completed,
		'total_turns': turns,
		'total_kills': sum(kills.values()),
		'kills': kills,
		'spells': get_names(getattr(player, 'spells', [])),
		'upgrades': get_upgrade_names(player) if player else [],
		'equipment': get_names(getattr(player, 'equipment', [])),
		'components': get_names(getattr(player, 'components', [])),
		'xp': getattr(player, 'xp', None),
	}
	return entry


def record_run(game, outcome, include_current_turns=False):
	if not game or getattr(game, 'run_history_recorded', False):
		return

	entry = summarize_run(game, outcome, include_current_turns=include_current_turns)
	history = load_history()
	history.append(entry)
	save_history(history)
	game.run_history_recorded = True


def get_latest_save_file():
	if not os.path.exists('saves'):
		return None
	run_folders = os.listdir('saves')
	run_folders.sort(reverse=True, key=lambda f: GameModule.safe_int(f))
	for folder in run_folders:
		filename = os.path.join('saves', folder, 'game.dat')
		if os.path.exists(filename):
			return filename
	return None


def load_latest_saved_game():
	filename = get_latest_save_file()
	if not filename:
		return None
	try:
		with open(filename, 'rb') as save_file:
			return pickle.load(save_file)
	except Exception:
		return None


def format_date(timestamp):
	try:
		return time.strftime('%Y-%m-%d %H:%M', time.localtime(timestamp))
	except Exception:
		return 'Unknown time'


def format_list(values):
	return ", ".join(values) if values else "None"


def wrap_lines(lines, width=PAGE_WIDTH):
	wrapped = []
	for line in lines:
		if not line:
			wrapped.append("")
			continue
		parts = textwrap.wrap(line, width=width, subsequent_indent="  ")
		wrapped.extend(parts or [""])
	return wrapped


def paginate(title, lines):
	lines = wrap_lines(lines)
	pages = []
	current = [title, ""]
	for line in lines:
		if len(current) >= PAGE_LINES:
			pages.append("\n".join(current))
			current = [title + " (continued)", ""]
		current.append(line)
	if len(current) > 2 or not pages:
		pages.append("\n".join(current))
	return pages


def sorted_history():
	history = load_history()
	return sorted(history, key=lambda entry: entry.get('finished_at', 0), reverse=True)


def build_overview_page(history):
	lines = ["Recorded runs: %d" % len(history)]
	lines.append("Press any key or click to page through run details.")
	lines.append("")
	for entry in history[:12]:
		run_no = entry.get('run_number', '?')
		outcome = entry.get('outcome', 'UNKNOWN')
		realm = entry.get('realm_reached', 0)
		kills = entry.get('total_kills', 0)
		when = format_date(entry.get('finished_at'))
		lines.append("Run %s | %s | Realm %s | %s kills | %s" % (run_no, outcome, realm, kills, when))
	if len(history) > 12:
		lines.append("")
		lines.append("Showing the 12 most recent runs in this overview; details include all stored runs.")
	return paginate("Run History", lines)[0]


def build_run_pages(entry):
	run_no = entry.get('run_number', '?')
	title = "Run History: Run %s" % run_no
	trial = entry.get('trial') or "None"
	kills = entry.get('kills') or {}
	kill_rows = sorted(kills.items(), key=lambda item: (-item[1], item[0]))

	lines = [
		"Finished: %s" % format_date(entry.get('finished_at')),
		"Outcome: %s" % entry.get('outcome', 'UNKNOWN'),
		"Trial: %s" % trial,
		"Realm reached: %s" % entry.get('realm_reached', 0),
		"Levels completed: %s" % entry.get('levels_completed', 0),
		"Total turns: %s" % entry.get('total_turns', 0),
		"Total kills: %s" % entry.get('total_kills', 0),
		"SP remaining: %s" % entry.get('xp') if entry.get('xp') is not None else "SP remaining: Unknown",
		"",
		"Build",
		"Spells: %s" % format_list(entry.get('spells', [])),
		"Upgrades: %s" % format_list(entry.get('upgrades', [])),
		"Items: %s" % format_list(entry.get('equipment', [])),
		"Components: %s" % format_list(entry.get('components', [])),
		"",
		"Individual kills:",
	]

	if kill_rows:
		for name, count in kill_rows:
			lines.append("%4d  %s" % (count, name))
	else:
		lines.append("None recorded")

	return paginate(title, lines)


def build_history_pages():
	history = sorted_history()
	if not history:
		return ["Run History\n\nNo finished runs recorded yet.\nNew victories, defeats, and abandoned saves will appear here."]

	pages = [build_overview_page(history)]
	for entry in history:
		pages.extend(build_run_pages(entry))
	return pages


def show_run_history(view):
	pages = build_history_pages()
	view.state = view.run_history_message_state
	view.center_message = False
	view.message = pages[0]
	view.next_messages = pages[1:]
	view.examine_target = None


def patch_game():
	if not getattr(GameModule.Game, '_run_history_patched', False):
		orig_subscribe_mutators = GameModule.Game.subscribe_mutators
		orig_finalize_save = GameModule.Game.finalize_save

		def subscribe_mutators(self):
			result = orig_subscribe_mutators(self)
			install_death_tracker(self)
			return result

		def finalize_save(self, victory):
			result = orig_finalize_save(self, victory)
			record_run(self, 'VICTORY' if victory else 'DEFEAT')
			return result

		GameModule.Game.subscribe_mutators = subscribe_mutators
		GameModule.Game.finalize_save = finalize_save
		GameModule.Game._run_history_patched = True

	if not getattr(GameModule, '_run_history_abort_patched', False):
		orig_abort_game = GameModule.abort_game

		def abort_game():
			game = load_latest_saved_game()
			if game:
				record_run(game, 'ABANDONED', include_current_turns=True)
			return orig_abort_game()

		GameModule.abort_game = abort_game
		GameModule._run_history_abort_patched = True
		return orig_abort_game, abort_game

	return None, GameModule.abort_game


def patch_title_menu(main):
	if not main or not hasattr(main, 'PyGameView'):
		return
	if getattr(main.PyGameView, '_run_history_menu_patched', False):
		return

	orig_get_title_menu_entries = main.PyGameView._get_title_menu_entries
	orig_process_title_input = main.PyGameView.process_title_input

	def get_title_menu_entries(self):
		entries = list(orig_get_title_menu_entries(self))
		if RUN_HISTORY_TARGET in entries:
			return entries
		try:
			index = entries.index(main.TITLE_SELECTION_BESTIARY) + 1
		except ValueError:
			index = len(entries) - 1
		entries.insert(index, RUN_HISTORY_TARGET)
		return entries

	def draw_title(self):
		screen_width = self.screen.get_width()
		screen_height = self.screen.get_height()

		cur_y = screen_height // 2 + main.SPRITE_SIZE * 3
		cur_frame = (main.cloud_frame_clock // main.SUB_FRAMES[main.ANIM_IDLE]) % 4
		self.screen.blit(self.title_frames[cur_frame], (0, 0))

		entries = self._get_title_menu_entries()
		labels = {
			main.TITLE_SELECTION_LOAD: "CONTINUE RUN",
			main.TITLE_SELECTION_ABANDON: "ABANDON RUN",
			main.TITLE_SELECTION_NEW: "NEW GAME",
			main.TITLE_SELECTION_OPTIONS: "OPTIONS",
			main.TITLE_SELECTION_HELP: "HOW TO PLAY",
			main.TITLE_SELECTION_BESTIARY: "BESTIARY",
			RUN_HISTORY_TARGET: "RUN HISTORY",
			main.TITLE_SELECTION_DISCORD: "DISCORD",
			main.TITLE_SELECTION_MODS: "MODS",
			main.TITLE_SELECTION_CREDITS: "CREDITS",
			main.TITLE_SELECTION_EXIT: "QUIT",
		}
		rect_w = max(self.font.size(labels[option])[0] for option in entries)
		cur_x = screen_width // 2 - rect_w // 2

		for option in entries:
			text = labels[option]
			line_w = self.font.size(text)[0]
			self.draw_string(text, self.screen, cur_x, cur_y, (255, 255, 255), mouse_content=option, content_width=line_w)
			cur_y += self.linesize + 2

	def process_title_input(self):
		m_loc = self.get_mouse_pos()
		for evt in self.events:
			if evt.type == main.pygame.KEYDOWN and evt.key in self.key_binds[main.KEY_BIND_CONFIRM]:
				if self.examine_target == RUN_HISTORY_TARGET:
					self.play_sound('menu_confirm')
					show_run_history(self)
					return
			elif evt.type == main.pygame.MOUSEBUTTONDOWN and evt.button == main.pygame.BUTTON_LEFT:
				target = self.get_ui_target(*m_loc)
				if target == RUN_HISTORY_TARGET:
					self.play_sound('menu_confirm')
					self.examine_target = target
					show_run_history(self)
					return
		orig_process_title_input(self)

	main.PyGameView._get_title_menu_entries = get_title_menu_entries
	main.PyGameView.draw_title = draw_title
	main.PyGameView.process_title_input = process_title_input
	main.PyGameView.run_history_message_state = main.STATE_MESSAGE
	main.PyGameView.show_run_history = show_run_history
	main.PyGameView._run_history_menu_patched = True


orig_abort, patched_abort = patch_game()
main = sys.modules.get('__main__') or sys.modules.get('RiftWizard3')
if main and orig_abort and getattr(main, 'abort_game', None) is orig_abort:
	main.abort_game = patched_abort
patch_title_menu(main)
