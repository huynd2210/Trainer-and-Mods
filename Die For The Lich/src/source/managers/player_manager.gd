
extends Node

const MetaProgression = preload("res://mods/meta_progression.gd")
const META_REROLL_KEY: String = "legacy_second_chance"

static var IS_LEVELING_UP: bool = false



var char: Character:
	set(value):
		char = value

		if is_instance_valid(char):
			if !char.is_node_ready():
				await char.ready
			char.on_armor_lost.connect(on_armor_lost.emit)
			char.on_heal.connect(on_char_heal.emit)
			on_char_ready.emit()
var char_entry: Chars.Entry
var alive: bool = false
var coins: int = 0:
	set(value):
		var coins_gained: int = value - coins
		coins = value
		on_coins_updated.emit(coins, coins_gained)
var _coins_array: Array[TrinketCoin]
var xp: int
var xp_max: int
var xp_max_start: int = 50
var xp_multiply: float = 2.0
var level: int
var level_ups: int = 0
var overall_depth: int
var map_depth: int
var map_complete: float
var draw_count: int = 5

var _hit_adjacent_count: int = 0
var hit_adjacent: bool = false:
	set(v):
		_hit_adjacent_count += 1 if v else -1
		hit_adjacent = _hit_adjacent_count > 0
var weap_action_extra: int = 0
var hit_stun: int = 0
var draw_again_meta: Dictionary
var run_record: RunRecord
var legacy_weapon_chest_claimed: bool = false
signal on_coins_updated(coins: int, coins_gained: int)
signal on_xp_update(_gained: int, _amount: int, _max: int)
signal on_level_up(level: int)
signal on_char_ready
signal on_armor_lost(amount: int)
signal on_map_node_entered(map_depth: int, map_complete: float)
signal on_shop_enter_blood
signal on_coins_gained(amount: int)
signal on_coins_spent(amount: int)
signal on_char_heal(amount: int)
signal on_level_up_complete


func await_char_ready() -> void :
	if !is_instance_valid(char):
		await on_char_ready


func _ready() -> void :

	await PRELOADS.await_load()
	GM.on_game_ready.connect(_on_game_ready)

	GM.on_game_end.connect(_on_game_end)
	GM.on_act_start.connect(_on_act_start)


func _on_game_ready() -> void :
	GM.map.on_node_pressed.connect(_on_map_node_pressed)
	GM.battle.on_battle_start.connect(_on_battle_start)


func run_start(data: RunData) -> void :
	IS_LEVELING_UP = false
	map_depth = 0
	overall_depth = 0
	coins = 0
	level_ups = 0
	level = data.char_level
	xp_max = floori(float(xp_max_start) * xp_multiply * level) - xp_max_start
	xp = data.char_xp

	char_entry = Mads.array_pick_random([Chars.get_entry("Undertaker"), Chars.get_entry("Dante"), Chars.get_entry("The Forsaken")], GM.random) if data.char_name == "random" else Chars.get_entry(data.char_name)
	await GM.battle.char_add_ps(char_entry.packed_scene)
	char.on_level_up.connect(_trigger_level_up_screen)
	char.player_setup.call_deferred(char_entry, data)

	if is_instance_valid(data.record):
		run_record = data.record
		run_record.run_start(true)
	else:
		run_record = RunRecord.new()
		run_record.run_start(false)


func _on_act_start(act_id: int) -> void :
	if act_id > 0:


		pass


func _on_game_end() -> void :
	GM.battle.rerolls.destroy_by_key(META_REROLL_KEY)
	legacy_weapon_chest_claimed = false
	alive = false
	coins = 0
	_coins_array.clear()


func _on_battle_start() -> void :
	GM.battle.rerolls.destroy_by_key(META_REROLL_KEY)
	if GM.is_daily || !is_instance_valid(char):
		return

	var bulwark_level: int = MetaProgression.get_level("bulwark")
	if bulwark_level > 0:
		# Battle clears armor at the start of a player turn. Retain this grant once
		# so it protects the player for the first full turn instead of disappearing.
		GM.battle.keep_armor_player = maxi(GM.battle.keep_armor_player, 1)
		await char.defense_add(bulwark_level * 2)
		await char.await_action_preform()

	var reroll_level: int = MetaProgression.get_level("rerolls")
	if reroll_level > 0:
		var reroll = GM.battle.rerolls.create("all_dice", char, reroll_level)
		reroll.key = META_REROLL_KEY


func _on_map_node_pressed(spot_data: Map.SpotData) -> void :
	map_depth = spot_data.grid_pos.y + 1
	overall_depth = Act.GetDepthOverall(GM.act, map_depth)
	map_complete = minf(1.0, float(map_depth) / float(GM.map.grid_height))
	on_map_node_entered.emit(map_depth, map_complete)


func coin_add(coin: TrinketCoin) -> void :
	coins += 1
	_coins_array.append(coin)
	on_coins_gained.emit(1)


func coins_remove(amount: int, shop_spent: bool) -> void :
	coins += - amount
	while _coins_array.size() > coins:
		var coin: TrinketCoin = _coins_array.pop_back()
		coin.destroy()
	if shop_spent:
		on_coins_spent.emit(amount)



func draw_again_meta_modify(key: String, value: int) -> void :
	draw_again_meta.set(key, draw_again_meta.get(key, 0) + value)
	if value < 0 && draw_again_meta.get(key, 0) <= 0:
		draw_again_meta.erase(key)


func draw_again_meta_exists(obj: TrayObj) -> bool:
	for key: String in Player.draw_again_meta:
		if Player.draw_again_meta[key] > 0 && obj.get_meta(key, false):
			return true
	return false


func xp_add(_amount: int) -> void :

	if !GM.is_daily:
		var scholar_level: int = MetaProgression.get_level("scholar")
		_amount = roundi(float(_amount) * (1.0 + 0.1 * scholar_level))

	xp += _amount

	if xp >= xp_max:

		xp = xp - xp_max
		xp_max = floori(float(xp_max) * xp_multiply)
		level += 1
		level_ups += 1
		on_level_up.emit(level)

	on_xp_update.emit(_amount, xp, xp_max)


func _trigger_level_up_screen() -> void :

	if level_ups > 0 && !IS_LEVELING_UP && alive:

		for _char: Character in GM.battle.get_enemies():
			if is_instance_valid(_char):
				await _char.await_char()

		IS_LEVELING_UP = true
		while level_ups:
			await GM.ui.abilities.trigger(level + 1 - level_ups, char.level_up_gain_hp_max)
			_level_up_gain_max_hp()
			await GM.on_abilities_disappear
			level_ups += -1

		level_ups = 0
		IS_LEVELING_UP = false
		on_level_up_complete.emit()


func await_leveling() -> void :
	if IS_LEVELING_UP || level_ups:
		await on_level_up_complete


func _level_up_gain_max_hp() -> void :
	char.hp_max += char.level_up_gain_hp_max
