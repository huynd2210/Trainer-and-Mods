class_name Battle extends Node2D

const MetaProgression = preload("res://mods/meta_progression.gd")


enum BoardState{
	NORMAL, 
	BLACK_JACK, 
	FAIL, 
}

@onready var rolls: BattleRolls = $Rolls
@onready var rerolls: BattleRerolls = $Rerolls
@onready var audio_reroll: AudioPlay = $Audio / Reroll
var background: BattleBackground
var playing: bool = false
var board_state: BoardState:
	set(value):
		board_state = value
		var was_crit: bool = is_black_jack && board_state != BoardState.BLACK_JACK
		failed = board_state == BoardState.FAIL
		is_black_jack = board_state == BoardState.BLACK_JACK
		on_board_state_update.emit(board_state)
		if was_crit:
			on_crit_end.emit()
var counter_amount: int = 0:
	set(value):
		var prev: int = counter_amount
		counter_amount = value
		on_counter_update.emit()
		on_counter_update_data.emit(counter_amount - prev)
var counter_bj: int = 0:
	set(value):
		counter_bj = value
		on_counter_bj_update.emit()
var is_black_jack: bool = false
var failed: bool = false
var char_all: Array[Character]
var char_player: Character:
	set(value):
		char_player = value
		Player.char = char_player
var char_enemies: Array[Character]
var char_enemies_pos: Array[Character]
var turn_count: int = 0:
	set(v):
		turn_count = v
		on_turn_update.emit(turn_count)
var is_enemy_turn: bool = false
var is_displaying_text: bool = false
var input_enable: bool = false
var _input_locked_count: int = 0:
	set(v):
		_input_locked_count = v

var disable_tray_obj_kind_rollable: Dictionary[TrayObj.Kind, int]
var roll_only_meta: Dictionary
var keep_armor_player: int = 0
var weap_using: bool = false
var weap_used_successful: bool = false
var clearing_board: bool = false
var _data: Dictionary

signal on_battle_start
signal on_battle_start_data(data: Dictionary)
signal on_start_turn
signal on_player_end_turn
signal on_battle_end
signal on_battle_end_data(data: Dictionary)
signal on_battle_end_winner(is_player: bool)
signal on_battle_end_winner_pre_clear(is_player: bool)
signal on_crit_pre
signal on_crit
signal on_crit_after
signal on_crit_end
signal on_roll_fail_pre
signal on_roll_fail
signal on_counter_update
signal on_counter_update_data(number: int)
signal on_counter_bj_update
signal on_board_state_update(state: BoardState)
signal on_start_action
signal on_action_complete
signal on_char_hover_start(char: Character)
signal on_char_hover_end(char: Character)
signal on_char_death(char: Character)
signal on_char_death_pre(char: Character)
signal on_await_char_death(char: Character)
signal on_char_hurt(char: Character)
signal on_char_hurt_data(hit: Character.Hit)
signal on_char_defense_gained(char: Character, def: int)
signal on_weap_selected(weap: Weapon)
signal on_turn_update(turn: int)
signal on_draw_complete
signal on_roll_start(obj: TrayObj)
signal on_roll_start_trinket(trinket: Trinket)
signal on_roll_floored(obj: TrayObj)
signal on_roll_complete(obj: TrayObj)
signal on_roll_complete_all
signal on_die_predicted(die: Die)
signal on_turn_start_enemy
signal on_turn_start_player




func _ready() -> void :
	GM.battle = self
	GM.on_game_ready.connect(_on_game_ready)

	GM.weapons.on_weap_selected.connect(_on_weap_selected)
	GM.on_act_start_pre.connect(_on_act_start_pre)

	for value: int in TrayObj.Kind.values():
		disable_tray_obj_kind_rollable[value] = 0
	is_black_jack = false

	on_crit_pre.connect($Audio / Critical.play_stream)
	on_roll_fail_pre.connect($Audio / Fail.play_stream)







func setup_and_start(enemy_entries: Array[EnemyManager.MapEntry], _is_elite: bool) -> void :

	GM.await_started()

	input_reset()
	input_toggle(false)
	input_lock()
	playing = true
	turn_count = 0

	background.randomize_bg()

	for entry: EnemyManager.MapEntry in enemy_entries:
		var _enemy: CharacterEnemy = await GM.battle.char_add_ps(entry.enemy.ps, entry.hp, entry.buffs)
		_enemy.enemy_key = entry.enemy.key
	await Mads.await_emit(on_battle_start)

	_data = {
		"act_id": GM.act, 
		"is_elite": _is_elite, 
		"is_boss": Player.map_complete == 1.0, 
		"is_win": false, 
	}
	await Mads.await_emit(on_battle_start_data, _data)

	GM.await_ended()

	enemies_setup_prompts()

	player_start_turn()


func end(is_win: bool) -> void :
	if !playing: return
	GM.await_started()

	input_toggle(false)
	input_lock()
	playing = false
	_data.set("is_win", is_win)

	await Player.await_leveling()

	await GM.tray.await_movement()

	for _char: Character in char_all:
		if char: await _char.await_char()

	await GM.weapons.await_using()

	await Mads.await_emit(on_battle_end_winner_pre_clear, is_win)

	turn_count = 0
	clearing_board = true
	await GM.tray.clear()
	clearing_board = false
	GM.weapons.weap_unequip()
	if is_win:
		char_player.defense_clear()
		await char_player.await_action_preform()
		await char_player.buff_con.clear_buffs()

	if is_win:
		clear_enemies()

	on_battle_end.emit()
	on_battle_end_data.emit(_data)
	on_battle_end_winner.emit(is_win)
	GM.await_ended()


func clear() -> void :
	playing = false
	input_reset()
	input_toggle(false)
	GM.weapons.weap_unequip()
	clear_enemies()
	char_remove(char_player)
	char_all.clear()
	background.clear()
	background = null





func player_start_turn() -> void :
	GM.await_started()

	turn_count += 1
	$Audio / EndTurn.play_stream()
	await GM.ui.battle_text("Player's Turn", "Turn " + str(turn_count), 0.5)

	await Mads.await_emit(on_turn_start_player)

	if char_player.defense > 0:
		if !keep_armor_player:
			char_player.defense_clear()
			await char_player.await_action_preform()
		else:
			keep_armor_player = keep_armor_player - 1

	var draw_amount: int = Player.draw_count
	if turn_count == 1 && !GM.is_daily && MetaProgression.get_level("full_bag_draw") > 0:
		# draw_objs already stops when the tray is full or the bag is empty.
		draw_amount = GM.tray.objs.size()
	await GM.tray.draw_objs(draw_amount)
	await Mads.await_emit(on_draw_complete)

	on_start_turn.emit()

	input_unlock()
	input_toggle(true)
	GM.await_ended()


func player_weap_selected(weap: Weapon) -> void :
	if !playing: return
	GM.await_started()
	input_toggle(false)
	input_lock()

	GM.weapons.weap_equip(weap)

	clearing_board = true
	await clear_board()
	clearing_board = false

	counter_bj = weap.crit
	counter_amount = weap.counter_start_value

	await weap.await_movement()

	on_weap_selected.emit(weap)

	input_unlock()
	input_toggle(true)
	GM.await_ended()


func player_select_target(_char: Character) -> void :

	GM.await_started()
	input_toggle(false)
	input_lock()
	weap_using = true
	weap_used_successful = counter_amount > 0

	for c: Character in char_all:
		await c.await_buffs()

	await Mads.await_emit(_char.on_selected)

	var amount: int = 0 if failed else counter_amount
	await GM.weapons.weap_use(amount, _char)

	if Player.alive:
		await char_player.await_action_preform()

	weap_using = false
	weap_used_successful = false

	GM.weapons.weap_unequip()

	clearing_board = true
	await clear_board()
	clearing_board = false

	await Mads.await_emit(on_action_complete)
	GM.await_ended()

	if get_enemies(true).size() > 0:
		input_unlock()
		input_toggle(true)


func player_end_turn() -> void :
	GM.await_started()

	input_toggle(false)
	input_lock()

	GM.weapons.weap_unequip()

	clearing_board = true
	await clear_board()
	clearing_board = false

	counter_amount = 0
	counter_bj = 0

	on_player_end_turn.emit()

	for _char: Character in char_all:
		await _char.await_char()
	GM.await_ended()

	enemies_start_turn()


func clear_board() -> void :

	var objs: Array[TrayObj] = GM.tray.get_objs_on_board()
	if objs.size() > 0:
		var _board_state: BoardState = board_state
		objs.reverse()

		for obj: TrayObj in objs:

			if !is_instance_valid(obj): continue
			obj.clear_from_board(_board_state)
			await GM.timeout(0.05)
		await GM.tray.await_movement()







func enemies_setup_prompts() -> void :
	GM.await_started()

	for _char: Character in get_enemies(true):

		_char.setup_prompts()
	GM.await_ended()


func enemies_start_turn() -> void :
	GM.await_started()

	is_enemy_turn = true

	$Audio / EndTurn.play_stream()
	GM.ui.battle_text("Enemy Turn", "", 0.5)
	await GM.timeout(0.5)

	await Mads.await_emit(on_turn_start_enemy)

	for _char: Character in char_all:
		if is_instance_valid(_char):
			await _char.await_char()

	for _char: Character in get_enemies(true):
		_char.defense_clear()
		await _char.await_char()

	var player_died: bool = false
	for _char: Character in get_enemies(true):
		await _char.execute_prompts()
		await get_tree().process_frame
		if is_instance_valid(_char):
			await _char.await_char()

		player_died = !Player.alive
		if player_died:
			break
	is_enemy_turn = false
	GM.await_ended()

	if !player_died && get_enemies(true).size() > 0:

		enemies_setup_prompts()

		player_start_turn()








func char_add_ps(char_packed_scene: PackedScene, hp_max_preset: int = 0, buffs: Array[BuffRes] = []) -> Character:
	var _char: Character = char_packed_scene.instantiate()
	await char_add(_char, hp_max_preset, buffs)
	return _char


func char_add(char: Character, hp_max_preset: int = 0, buffs: Array[BuffRes] = []) -> void :

	if hp_max_preset > 0:
		char.hp_max_preset = hp_max_preset

	if char.is_enemy:
		background.enemies_con.add_child(char)
		char_enemies.append(char)

		if char_enemies_pos.has(null):
			var id: int = char_enemies_pos.find(null)
			char_enemies_pos.set(id, char)

		else:
			char_enemies_pos.append(char)

		var child_count: int = background.enemies_con.get_child_count()
		var child_with: float = 200.0
		for i: int in char_enemies_pos.size():
			if char_enemies_pos[i] == null:
				continue
			var child: Control = char_enemies_pos[i]
			var p: Vector2 = Vector2( - child_with * child_count * 0.5 + child_with * i + child_with * 0.5, child.position.y)
			Mads.tween_create(child).tween_property(child, "position", p, 0.2)

	else:
		GM.ui.inv.char_con_player.add_child(char)
		char_player = char

	if !char.is_node_ready():
		await char.ready

	if !buffs.is_empty():
		for res: BuffRes in buffs:
			char.buff_con.add_name(res.name, res.get_stacks())

	char_all.append(char)

	char.selectable.on_hover_start.connect(_on_char_hover_start.bind(char))
	char.selectable.on_hover_end.connect(_on_char_hover_end.bind(char))
	char.selectable.on_clicked.connect(_on_char_click.bind(char))
	char.on_hurt.connect(_on_char_hurt.bind(char))
	char.on_hurt_data.connect(_on_char_hurt_data)
	char.on_defense_gained.connect(_on_char_defense_gained.bind(char))
	char.on_death.connect(_on_char_death.bind(char))
	char.on_death_pre.connect(_on_char_death_pre.bind(char))


func char_remove(char: Character) -> void :
	if char == null: return
	char_all.erase(char)
	if char == char_player:
		char_player = null
	else:
		char_enemies_pos[char_enemies_pos.find(char)] = null
		char_enemies.erase(char)

	char.selectable.on_clicked.disconnect(_on_char_click.bind(char))
	char.on_hurt.disconnect(_on_char_hurt.bind(char))
	char.on_hurt_data.disconnect(_on_char_hurt_data)
	char.on_defense_gained.disconnect(_on_char_defense_gained.bind(char))
	char.on_death.disconnect(_on_char_death.bind(char))
	char.on_death_pre.disconnect(_on_char_death_pre.bind(char))
	char.destroy()


func get_enemies(only_alive: bool = false, aggro_filter: bool = false) -> Array[Character]:
	var chars: Array[Character] = []
	var chars_with_aggro: bool = false
	for char: Character in char_enemies_pos:
		if char == null:
			continue
		if aggro_filter:
			chars_with_aggro = chars_with_aggro || char.has_aggro
		if only_alive:
			if char.alive:
				chars.append(char)

		else:
			chars.append(char)

	if chars_with_aggro:
		chars = chars.filter( func(_char: Character): return _char.has_aggro)
	return chars


func get_enemies_adjacent(enemy: Character) -> Array[Character]:
	var chars: Array[Character] = []
	var enemy_id: int = char_enemies_pos.find(enemy)
	if enemy_id != -1:

		if enemy_id - 1 >= 0 && char_enemies_pos[enemy_id - 1]:
			chars.append(char_enemies_pos[enemy_id - 1])

		if enemy_id + 1 < char_enemies_pos.size() && char_enemies_pos[enemy_id + 1]:
			chars.append(char_enemies_pos[enemy_id + 1])
	return chars


func clear_enemies() -> void :

	while get_enemies().size() > 0:
		char_remove(char_enemies[0])
	char_enemies.clear()
	char_enemies_pos.clear()







func input_lock() -> void :
	_input_locked_count += 1


func input_unlock() -> void :
	_input_locked_count += -1


func input_toggle(enable: bool = input_enable) -> void :
	if _input_locked_count: return
	input_enable = enable && playing

	update_weapons_useable(input_enable)
	update_chars_selectable(input_enable)
	update_objs_rollable(input_enable)
	update_end_btn_useable(input_enable)


func input_reset() -> void :
	_input_locked_count = 0


func set_roll_only_meta(meta_key: String, amount: int) -> void :
	roll_only_meta.set(meta_key, roll_only_meta.get(meta_key, 0) + amount)
	if roll_only_meta.get(meta_key, 0) <= 0:
		roll_only_meta.erase(meta_key)


func obj_is_rollable(obj: TrayObj, preset_board_state: BoardState = board_state, preset_obj_state: TrayObj.State = obj.state) -> bool:

	if preset_obj_state == TrayObj.State.TRAY:

		var ok: bool = !obj.is_locked

		if ok && !roll_only_meta.is_empty():
			ok = false
			for key: String in roll_only_meta.keys():
				if obj.has_meta(key):
					ok = true
					break

		if ok && GM.tray.has_force_roll_in_tray():
			ok = GM.tray.is_force_roll(obj)

		if ok && disable_tray_obj_kind_rollable[obj.kind] > 0:
			ok = false

		elif ok && preset_board_state == BoardState.BLACK_JACK:
			ok = obj.rollable_at_crit || (obj.kind == TrayObj.Kind.DIE && rolls.die_is_rollable_at_crit(obj))

		elif ok && preset_board_state == BoardState.FAIL:
			ok = true

		return ok

	elif preset_obj_state == TrayObj.State.BOARD:

		return rerolls.obj_can_reroll(obj)

	else:
		return false


func update_objs_rollable(_enable: bool) -> void :

	var enable: bool = _enable && is_instance_valid(GM.weapons.weap_equipped) && !Weapon.Attacking && Tray.DISCARDING == 0
	var objs: Array[TrayObj] = []
	objs.append_array(GM.tray.objs_by_state(TrayObj.State.BOARD).duplicate())
	objs.append_array(GM.tray.objs_by_state(TrayObj.State.TRAY).duplicate())
	objs.append_array(GM.tray.objs_by_state(TrayObj.State.GRAVEYARD).duplicate())

	if enable:
		for obj: TrayObj in objs:
			var is_rollable: bool = !obj.moving && obj_is_rollable(obj)
			obj.rollable_toggle(is_rollable)
	else:

		for obj: TrayObj in objs:
			obj.rollable_toggle(false)


func update_weapons_useable(_enable: bool) -> void :
	var weapons: WeapController = GM.weapons
	var enable: bool = _enable && !rolls.in_action && ((GM.tray.objs_by_state(TrayObj.State.BOARD).size() == 0 && board_state != BoardState.BLACK_JACK) || board_state == BoardState.FAIL) && !Weapon.Attacking
	if enable:
		for slot: WeapSlot in weapons.slots:
			if !slot.has_weap: continue
			var weap: Weapon = slot.weap
			weap.selectable.is_selectable = !weap.used && ( !weap.is_equipped || failed)
	else:
		for slot: WeapSlot in weapons.slots:
			slot.get_selectable().is_selectable = false


func update_chars_selectable(_enable: bool) -> void :

	var enable: bool = _enable && !GM.tray.objs_are_moving && !rolls.in_action && board_state != BoardState.FAIL && (GM.tray.objs_by_state(TrayObj.State.BOARD).size() > 0 || board_state == BoardState.BLACK_JACK) && !Weapon.Attacking && Weapon.WorkingCount == 0
	var enable_player: bool = enable && is_instance_valid(GM.weapons.weap_equipped) && GM.weapons.weap_equipped.is_defensive()
	var enable_enemies: bool = enable && is_instance_valid(GM.weapons.weap_equipped) && GM.weapons.weap_equipped.is_offensive()

	if is_instance_valid(char_player):
		char_player.selectable.is_selectable = enable_player

	for _char: Character in get_enemies(true, true):
		_char.selectable.is_selectable = enable_enemies


func update_end_btn_useable(_enable: bool) -> void :
	var enable: bool = _enable && !rolls.in_action && ((GM.tray.objs_by_state(TrayObj.State.BOARD).size() == 0 && board_state != BoardState.BLACK_JACK) || board_state == BoardState.FAIL) && !Weapon.Attacking
	GM.ui.inv.end_turn_disable( !enable)







func display_text_critical() -> void :
	await GM.ui.battle_text("[pulse freq=4.5 color=orange ease=.5][shake rate=15.0 level=10 connected=0]CRITICAL", "", 0.75)


func display_text_fail() -> void :
	await GM.ui.battle_text("[pulse freq=4.5 color=red ease=.5][shake rate=15.0 level=15 connected=1]FAILED ROLL", "", 0.75)







func _on_game_ready() -> void :

	GM.ui.inv.on_btn_end_turn_pressed.connect(player_end_turn)


func _on_act_start_pre(act_id: int) -> void :

	if background:
		background.clear()
		background = null

	BattleBackground.Setup(act_id)


func _on_weap_selected(weap: Weapon) -> void :
	player_weap_selected(weap)


func _on_char_hover_start(char: Character) -> void :
	on_char_hover_start.emit(char)


func _on_char_hover_end(char: Character) -> void :
	on_char_hover_end.emit(char)


func _on_char_click(char: Character) -> void :
	player_select_target(char)


func _on_char_hurt(char: Character) -> void :
	on_char_hurt.emit(char)


func _on_char_hurt_data(hit: Character.Hit) -> void :
	on_char_hurt_data.emit(hit)

func _on_char_death_pre(_char: Character) -> void :
	on_char_death_pre.emit(_char)


func _on_char_death(char: Character) -> void :
	on_char_death.emit(char)
	await GM.await_method(Mads.await_emit.bind(on_await_char_death, char))

	if char.is_enemy:
		char_remove(char)
		if get_enemies(true).size() == 0:
			end.call_deferred(true)

	else:
		char_remove(char)
		end.call_deferred(false)


func _on_char_defense_gained(def: int, char: Character) -> void :
	on_char_defense_gained.emit(char, def)
