extends Node
## Orc Trainer - Sir, We Have an Orc Problem
## Injected as an autoload singleton. Draws a small overlay and handles cheats.
##
## Hotkeys:
##   F1  +10000 upgrade marks      (currency type 0)
##   F2  +10000 level-finished marks (currency type 1)
##   F3  +10000 all-killed marks    (currency type 2)
##   F10 +100000 to ALL currencies  (the "big money" button)
##   F4  Heal base to full
##   F5  Skip wave
##   F6  Kill all enemies
##   F7  Toggle god mode (base can't die)
##   F8  Toggle pause
##   F9  Toggle overlay

const CURRENCY_NAMES := ["Upgrade", "Level Finish", "All Killed"]

var _overlay: CanvasLayer
var _panel: PanelContainer
var _title: Label
var _status: Label
var _lines: Array[Label] = []
var _god_mode := false
var _timer := 0.0
var _log_path := "C:/Games/Sir.We.Have.an.Orc.Problem/mod/trainer_log.txt"
var _flag_path := "C:/Games/Sir.We.Have.an.Orc.Problem/mod/selftest.flag"

func _ready() -> void:
	_build_overlay()
	_log("trainer ready")
	if FileAccess.file_exists(_flag_path):
		_run_self_test.call_deferred()


func _run_self_test() -> void:
	await get_tree().create_timer(4.0).timeout
	_log("--- STATS FIX VERIFY ---")
	var lpscene = load("res://tech_tree/hud/level_panel.tscn")
	var gm = get_node_or_null("/root/GameManager")
	if lpscene == null or gm == null:
		_log("could not load level_panel or GameManager")
	else:
		var st = gm.get("levels").get(1).get("stats")
		_log("levels[1].stats: times_survived=%s times_all_killed=%s" % [st.get("times_survived"), st.get("times_all_killed")])
		# Instantiate the panel, let the buggy game logic run, then apply the fix
		var lp = lpscene.instantiate()
		lp.set("level_id", 1)
		get_tree().root.add_child(lp)
		await get_tree().create_timer(1.0).timeout
		var before_sv = ""
		var before_ak = ""
		var sv = lp.find_child("SurvivedMarksLabel", true, false)
		var ak = lp.find_child("AllKilledMarksLabel", true, false)
		if sv is Label: before_sv = sv.text
		if ak is Label: before_ak = ak.text
		_log("BEFORE fix: SurvivedMarksLabel=%s AllKilledMarksLabel=%s" % [before_sv, before_ak])
		# Apply the fix
		_fix_level_panels()
		await get_tree().create_timer(0.5).timeout
		var after_sv = sv.text if (sv is Label and is_instance_valid(sv)) else "?"
		var after_ak = ak.text if (ak is Label and is_instance_valid(ak)) else "?"
		_log("AFTER fix: SurvivedMarksLabel=%s AllKilledMarksLabel=%s" % [after_sv, after_ak])
		if after_sv == str(st.get("times_survived")) and after_ak == str(st.get("times_all_killed")):
			_log("FIX VERIFIED: labels now match stats")
		else:
			_log("FIX NOT APPLIED - investigate")
		lp.queue_free()
	_log("--- STATS FIX VERIFY DONE ---")
	get_tree().quit()
	DirAccess.remove_absolute(_flag_path)


func _read_save_level1(save_dir: String) -> String:
	var out := ""
	for slot in [0, 1, 2]:
		var p = save_dir + "/save_%d.save" % slot
		if FileAccess.file_exists(p):
			var f = FileAccess.open(p, FileAccess.READ)
			if f:
				var content = f.get_as_text()
				f.close()
				var i = content.find("\"stats\"")
				var j = content.find("times_started")
				var seg = content.substr(j, 120) if j >= 0 else "no stats"
				out += "slot%d=%s | " % [slot, seg]
	return out if out != "" else "NO SAVE FILES"


func _obj_props(obj: Object) -> String:
	var sc = obj.get_script()
	if sc == null:
		return "no script"
	var parts := PackedStringArray()
	for p in sc.get_script_property_list():
		parts.append(p.name + "=" + str(obj.get(p.name)))
	return "; ".join(parts)


func _dump_node_compact(node: Node, depth: int) -> String:
	var indent := "  ".repeat(depth)
	var sc := ""
	if node.get_script():
		sc = str(node.get_script().resource_path)
	var s := indent + node.name + "  [" + node.get_class() + "]" + (" script=" + sc if sc else "") + "\n"
	for c in node.get_children():
		s += _dump_node_compact(c, depth + 1)
	return s


func _battle_stats(b: Node) -> String:
	var parts := PackedStringArray()
	for prop in ["total_enemies_to_spawn", "total_enemies_spawned", "total_enemies_killed",
			"total_enemies_at_base", "total_enemies_alive", "phase", "health"]:
		parts.append(prop + "=" + str(b.get(prop)))
	return ", ".join(parts)


# ---------- overlay UI ----------

func _build_overlay() -> void:
	_overlay = CanvasLayer.new()
	_overlay.layer = 100
	add_child(_overlay)

	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.08, 0.08, 0.10, 0.82)
	style.border_color = Color(0.62, 0.55, 0.28, 0.9)
	style.set_border_width_all(1)
	style.set_corner_radius_all(6)
	style.content_margin_left = 12.0
	style.content_margin_right = 12.0
	style.content_margin_top = 8.0
	style.content_margin_bottom = 8.0

	_panel = PanelContainer.new()
	_panel.add_theme_stylebox_override("panel", style)
	_panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_overlay.add_child(_panel)

	var vb := VBoxContainer.new()
	_panel.add_child(vb)

	_title = Label.new()
	_title.text = "ORC TRAINER"
	_title.add_theme_color_override("font_color", Color(0.95, 0.87, 0.55))
	_title.add_theme_font_size_override("font_size", 15)
	vb.add_child(_title)

	_status = Label.new()
	_status.text = ""
	_status.add_theme_color_override("font_color", Color(0.7, 0.9, 0.7))
	_status.add_theme_font_size_override("font_size", 12)
	vb.add_child(_status)

	_lines.clear()
	for h in [
		"F1-F3  +10k marks   F10  +100k all",
		"F4  Heal   F5  Skip wave",
		"F6  Kill all   F7  God mode",
		"F8  Pause   F9  Hide overlay",
	]:
		var l := Label.new()
		l.text = h
		l.add_theme_color_override("font_color", Color(0.8, 0.8, 0.85, 0.9))
		l.add_theme_font_size_override("font_size", 12)
		vb.add_child(l)
		_lines.append(l)

	_panel.position = Vector2(16, 16)


# ---------- per-frame ----------

func _process(_delta: float) -> void:
	_timer += _delta
	if _timer >= 0.4:
		_timer = 0.0
		_update_status()
		_fix_level_panels()
	if _god_mode:
		var battle := _battle()
		if battle:
			var mx = battle.get_max_health() if battle.has_method("get_max_health") else 0.0
			if battle.get("health") < mx:
				battle.set("health", mx)


# ---- stats-tracker fix ----
# The game's level_panel.gd fails to populate the "survived" and "all orcs killed"
# mark counters (always shows 0). We correct them from the actual level stats.
func _fix_level_panels() -> void:
	# The game's level_panel.gd fails to populate the "survived" and "all orcs killed"
	# mark counters (always shows 0). We correct them from the actual level stats.
	var gm = get_node_or_null("/root/GameManager")
	if gm == null:
		return
	var levels: Dictionary = gm.get("levels")
	if levels.is_empty():
		return
	for panel in _find_all_by_script(get_tree().root, "res://tech_tree/hud/level_panel.gd"):
		var ld = levels.get(int(panel.get("level_id")))
		if ld == null:
			continue
		var st = ld.get("stats")
		if st == null:
			continue
		var sv = panel.find_child("SurvivedMarksLabel", true, false)
		if sv is Label:
			sv.text = str(st.get("times_survived"))
		var ak = panel.find_child("AllKilledMarksLabel", true, false)
		if ak is Label:
			ak.text = str(st.get("times_all_killed"))


func _find_all_by_script(n: Node, path: String) -> Array[Node]:
	var out: Array[Node] = []
	if n.get_script() and str(n.get_script().resource_path) == path:
		out.append(n)
	for c in n.get_children():
		out.append_array(_find_all_by_script(c, path))
	return out


func _update_status() -> void:
	var gm := get_node_or_null("/root/GameManager")
	var battle := _battle()
	var spawner := _spawner()

	var parts: PackedStringArray = []
	if gm:
		var amounts: Dictionary = gm.get("currency_amounts")
		for t in range(3):
			parts.append(CURRENCY_NAMES[t] + ": " + str(amounts.get(t, 0)))
	if spawner:
		parts.append("Wave: " + str(spawner.get("current_wave")))
	if battle:
		var hp := str(battle.get("health"))
		var mx := str(battle.get_max_health()) if battle.has_method("get_max_health") else "?"
		parts.append("HP: " + hp + "/" + mx)
	parts.append("God: " + ("ON" if _god_mode else "off"))
	_title.text = "ORC TRAINER   " + " | ".join(parts)


# ---------- input ----------

func _unhandled_input(event: InputEvent) -> void:
	if not (event is InputEventKey and event.pressed and not event.echo):
		return
	var k := (event as InputEventKey).keycode
	if k == KEY_F1:
		_add_currency(0, 10000)
	elif k == KEY_F2:
		_add_currency(1, 10000)
	elif k == KEY_F3:
		_add_currency(2, 10000)
	elif k == KEY_F10:
		_add_all_currency(100000)
	elif k == KEY_F4:
		_heal()
	elif k == KEY_F5:
		_skip_wave()
	elif k == KEY_F6:
		_kill_all()
	elif k == KEY_F7:
		_flash("God mode: " + _set_god_mode())
	elif k == KEY_F8:
		_toggle_pause()
	elif k == KEY_F9:
		_panel.visible = not _panel.visible
		_flash("Overlay hidden" if not _panel.visible else "Overlay shown")


# ---------- helpers ----------

func _battle() -> Node:
	return _find_by_script(get_tree().root, "res://battle/battle.gd")


func _spawner() -> Node:
	return _find_by_script(get_tree().root, "res://battle/enemy_spawner.gd")


func _find_by_script(n: Node, path: String) -> Node:
	if n.get_script() and str(n.get_script().resource_path) == path:
		return n
	for c in n.get_children():
		var r = _find_by_script(c, path)
		if r:
			return r
	return null


func _add_currency(type: int, amount: int) -> void:
	var gm := get_node_or_null("/root/GameManager")
	if gm and gm.has_method("earn_currency"):
		gm.earn_currency(type, amount)
		_flash("+%d %s marks" % [amount, CURRENCY_NAMES[type]])
	else:
		_flash("GameManager unavailable")


func _add_all_currency(amount: int) -> void:
	var gm := get_node_or_null("/root/GameManager")
	if gm and gm.has_method("earn_currency"):
		for t in range(3):
			gm.earn_currency(t, amount)
		_flash("+%d to ALL marks" % amount)
	else:
		_flash("GameManager unavailable")


func _heal() -> void:
	var battle := _battle()
	if battle and battle.has_method("get_max_health"):
		battle.set("health", battle.get_max_health())
		_flash("Base healed to full")
	else:
		_flash("No battle to heal")


func _skip_wave() -> void:
	var spawner := _spawner()
	if spawner:
		spawner.set("current_wave", int(spawner.get("current_wave")) + 1)
		_flash("Wave skipped -> " + str(spawner.get("current_wave")))
	else:
		_flash("No enemy spawner found")


func _kill_all() -> void:
	var sim := get_node_or_null("/root/GPUSim")
	if sim and sim.has_method("remove_rigidbodies_in_area"):
		sim.remove_rigidbodies_in_area(Vector2.ZERO, 1.0e9)
		_flash("Cleared all bodies")
	else:
		_flash("GPUSim unavailable")


func _toggle_pause() -> void:
	var battle := _battle()
	if battle and battle.has_method("toggle_pause"):
		battle.toggle_pause()
		var p = battle.get("is_paused")
		_flash("Pause: " + ("ON" if p else "OFF"))
	else:
		_flash("No battle to pause")


func _set_god_mode() -> String:
	_god_mode = not _god_mode
	return "ON" if _god_mode else "OFF"


func _flash(msg: String) -> void:
	_log("action: " + msg)
	_status.text = msg
	var t := create_tween()
	t.tween_interval(1.5)
	t.tween_callback(func(): _status.text = "")


func _log(msg: String) -> void:
	var mode := FileAccess.READ_WRITE
	if not FileAccess.file_exists(_log_path):
		mode = FileAccess.WRITE
	var f := FileAccess.open(_log_path, mode)
	if f:
		if mode == FileAccess.READ_WRITE:
			f.seek_end()
		f.store_string(msg + "\n")
		f.close()
