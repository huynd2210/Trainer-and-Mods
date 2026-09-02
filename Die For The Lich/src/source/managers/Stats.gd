


class_name Stats extends Resource


static var PATH: String:
	get():
		return Data.PATH + "stats.tres"


static func Load() -> Stats:
	var stats: Stats = (ResourceLoader.load(PATH, "Stats") if ResourceLoader.exists(PATH) else Stats.new()) as Stats
	return stats


static func FixDictionary(_old: Dictionary, _new: Dictionary) -> Dictionary:
	var _new_return: Dictionary = _new.duplicate()
	for e in _old:
		if !_new.has(e): _new_return[e] = _old[e]
	for e in _new:
		if !_old.has(e): _new_return.erase(e)
	return _new_return


var can_save: bool = false
@export var locked_forever: int

@export var _play_timestamp: int

@export var play_time: int
@export var highscore: int
@export var runs_won: int
@export var enemies_death: Dictionary:
	set(v): enemies_death = FixDictionary(enemies_death, v)
@export var damage_overall: int
@export var damage_maximum: int
@export var armor_overall: int
@export var battles_won: int
@export var battles_lost: int
@export var battle_turns: int
@export var ankou_ignore_stags: int
@export var battles_won_no_shield_use: int

@export var dice_rolls: Dictionary:
	set(v): dice_rolls = FixDictionary(dice_rolls, v)
@export var rolls_count_trinkets: int
@export var rolls_count: int
@export var roll_amount_overall: int
@export var rolled_20s: int
@export var rolled_1s: int
@export var roll_dice_ghost: int
@export var roll_dice_negative: int
@export var roll_dice_lucky: int
@export var rolled_20s_ghost: int
@export var rolled_bones: int
@export var rerolls_count: int
@export var rerolls_dice_count: int
@export var rerolled_1s: int
@export var critical_count: int
@export var dice_predicted: int
@export var objects_drawn_from_bag: int

@export var coins_collected: int
@export var coins_spent: int
@export var coins_in_bag_maximum: int

@export var blood_shop: int
@export var chest_opened: int
@export var offers_taken: int


signal on_saved
signal on_loaded



func _init() -> void :

	for key in EnemyManager.enemies:
		enemies_death[key] = 0
	for type: int in Die.Type.size():
		dice_rolls[Die.TYPE_NAME[type]] = 0

	_on_load()


func save() -> int:
	if can_save:
		var error: Error = ResourceSaver.save(self, PATH)
		if error == Error.OK:
			on_saved.emit()
		return error
	else:
		return -1


func rolls_dice_count() -> int:
	var n: int = 0
	for key: String in dice_rolls:
		n += dice_rolls[key]
	return n


func enemies_death_count() -> int:
	var n: int = 0
	for key: String in enemies_death:
		n += enemies_death[key]
	return n








var subscribers: Dictionary[String, Array]


func subscribe(stat_property: String, method: Callable) -> void :
	Mads.error(Mads.get_nested(self, stat_property) == null, "StatsManager.subscribe(..) needs a 'stat_property' argument that exists inside Stats as property name")
	Mads.error(method.get_argument_count() == 0, "StatsManager.subscribe needs a 'method' argument with at least one argument")
	if !subscribers.has(stat_property):
		subscribers[stat_property] = []
	subscribers[stat_property].append(method)


func unsubscribe(stat_property: String, method: Callable) -> void :
	if subscribers.has(stat_property):
		subscribers[stat_property].erase(method)


func subscribers_emit(stat_property: String) -> void :
	if !subscribers.has(stat_property): return
	for method: Callable in subscribers[stat_property]:
		method.call_deferred(Mads.get_nested(self, stat_property))








func _on_load() -> void :
	GM.on_game_start.connect(_game_start)
	GM.on_game_end.connect(_game_end)
	GM.on_game_end_data.connect(_game_end_data)
	GM.on_map_node_complete.connect(on_map_node_complete)
	GM.on_chest_open.connect(_on_chest_open)
	GM.on_offer_taken.connect(_on_offer_taken)
	GM.battle.on_char_death_pre.connect(_on_char_death_pre)
	GM.battle.on_char_hurt_data.connect(_on_char_hurt_data)
	GM.battle.on_char_defense_gained.connect(_on_char_defense_gained)
	GM.battle.on_battle_end_winner.connect(_on_battle_end_winner)
	GM.battle.on_start_turn.connect(_on_battle_start_turn)
	GM.battle.on_die_predicted.connect(_on_die_predicted)
	GM.battle.on_roll_complete.connect(_on_roll_complete)
	GM.battle.on_crit_pre.connect(_on_battle_crit_pre)
	GM.battle.rerolls.on_reroll_obj.connect(_on_reroll_obj)
	GM.tray.on_draw_obj_complete.connect(_on_draw_obj_complete)
	Player.on_shop_enter_blood.connect(_on_shop_enter_blood)
	Player.on_coins_gained.connect(_on_coins_gained)
	Player.on_coins_spent.connect(_on_coins_spent)
	Daily.on_new_highscore.connect(_on_new_highscore)
	on_loaded.emit()


func delete() -> void :
	GM.on_game_start.disconnect(_game_start)
	GM.on_game_end.disconnect(_game_end)
	GM.on_game_end_data.disconnect(_game_end_data)
	GM.on_map_node_complete.disconnect(on_map_node_complete)
	GM.on_chest_open.disconnect(_on_chest_open)
	GM.on_offer_taken.disconnect(_on_offer_taken)
	GM.battle.on_char_death_pre.disconnect(_on_char_death_pre)
	GM.battle.on_char_hurt_data.disconnect(_on_char_hurt_data)
	GM.battle.on_char_defense_gained.disconnect(_on_char_defense_gained)
	GM.battle.on_battle_end_winner.disconnect(_on_battle_end_winner)
	GM.battle.on_start_turn.disconnect(_on_battle_start_turn)
	GM.battle.on_die_predicted.disconnect(_on_die_predicted)
	GM.battle.on_roll_complete.disconnect(_on_roll_complete)
	GM.battle.on_crit_pre.disconnect(_on_battle_crit_pre)
	GM.battle.rerolls.on_reroll_obj.disconnect(_on_reroll_obj)
	GM.tray.on_draw_obj_complete.disconnect(_on_draw_obj_complete)
	Player.on_shop_enter_blood.disconnect(_on_shop_enter_blood)
	Player.on_coins_gained.disconnect(_on_coins_gained)
	Player.on_coins_spent.disconnect(_on_coins_spent)
	Daily.on_new_highscore.disconnect(_on_new_highscore)






func on_map_node_complete() -> void :
	play_time += floori(float(Time.get_ticks_msec()) / 1000.0) - _play_timestamp
	_play_timestamp = floori(float(Time.get_ticks_msec()) / 1000.0)


func _game_start() -> void :
	_play_timestamp = floori(float(Time.get_ticks_msec()) / 1000.0)
	subscribers_emit("play_time")


func _game_end() -> void :
	play_time += floori(float(Time.get_ticks_msec()) / 1000.0) - _play_timestamp
	subscribers_emit("play_time")


func _game_end_data(won: bool) -> void :
	if won:
		runs_won += 1
		subscribers_emit("runs_won")
		save()


func _on_new_highscore(_highscore: int) -> void :
	highscore = _highscore
	subscribers_emit("highscore")
	save()


func _on_draw_obj_complete(_obj: TrayObj) -> void :
	objects_drawn_from_bag += 1
	subscribers_emit("objects_drawn_from_bag")


func _on_die_predicted(_die: Die) -> void :
	dice_predicted += 1
	subscribers_emit("dice_predicted")


func _on_char_defense_gained(_char: Character, def: int) -> void :
	if !_char.is_enemy:
		armor_overall += def
		subscribers_emit("armor_overall")


func _on_roll_complete(obj: TrayObj) -> void :

	rolls_count += 1
	subscribers_emit("rolls_count")

	roll_amount_overall += obj.number
	subscribers_emit("roll_amount_overall")

	if obj is Die:

		var type_name: String = Die.TYPE_NAME[obj.type]
		dice_rolls[type_name] += 1
		subscribers_emit("dice_rolls." + str(type_name))

		if obj.number == 1:
			rolled_1s += 1
			subscribers_emit("rolled_1s")
		elif obj.number == 20:
			rolled_20s += 1
			subscribers_emit("rolled_20s")

		if obj.get_meta("is_ghost", false):
			roll_dice_ghost += 1
			subscribers_emit("roll_dice_ghost")

			if obj.number == 20:
				rolled_20s_ghost += 1
				subscribers_emit("rolled_20s_ghost")

		elif obj.is_lucky:
			roll_dice_lucky += 1
			subscribers_emit("roll_dice_lucky")

		elif obj.roll_negative && obj.kind == TrayObj.Kind.DIE:
			roll_dice_negative += 1
			subscribers_emit("roll_dice_negative")

	elif obj is Trinket:

		rolls_count_trinkets += 1
		subscribers_emit("rolls_count_trinkets")

		if obj.get_meta("is_bone", false):
			rolled_bones += 1
			subscribers_emit("rolled_bones")


func _on_reroll_obj(obj: TrayObj) -> void :
	rerolls_count += 1
	subscribers_emit("rerolls_count")

	if obj is Die:
		rerolls_dice_count += 1
		subscribers_emit("rerolls_dice_count")

		if obj.number == 1:
			rerolled_1s += 1
			subscribers_emit("rerolled_1s")


func _on_coins_gained(amount: int) -> void :
	coins_collected += amount
	subscribers_emit("coins_collected")
	if Player.coins > coins_in_bag_maximum:
		coins_in_bag_maximum = Player.coins
		subscribers_emit("coins_in_bag_maximum")


func _on_coins_spent(amount: int) -> void :
	coins_spent += amount
	subscribers_emit("coins_spent")


func _on_chest_open() -> void :
	chest_opened += 1
	subscribers_emit("chest_opened")
	save()


func _on_offer_taken() -> void :
	offers_taken += 1
	subscribers_emit("offers_taken")
	save()


func _on_battle_crit_pre() -> void :
	critical_count += 1
	subscribers_emit("critical_count")


func _on_battle_start_turn() -> void :
	battle_turns += 1
	subscribers_emit("battle_turns")


func _on_battle_end_winner(player_win: bool) -> void :
	if player_win:
		battles_won += 1
		subscribers_emit("battles_won")
		if Player.run_record.battle_shield_uses == 0:
			battles_won_no_shield_use += 1
			subscribers_emit("battles_won_no_shield_use")
	else:
		battles_lost += 1
		subscribers_emit("battles_lost")
	save()


func _on_char_death_pre(_char: Character) -> void :
	if _char.is_enemy && enemies_death.has(_char.enemy_key):
		enemies_death[_char.enemy_key] += 1
		subscribers_emit("enemies_death." + str(_char.enemy_key))

		if _char.char_name == "Ankou" && !Player.run_record.battle_enemies_killed.has("Black Stag"):
			ankou_ignore_stags += 1
			subscribers_emit("ankou_ignore_stags")
		save()


func _on_char_hurt_data(hit: Character.Hit) -> void :
	if hit.target.is_enemy && hit.dmg > 0:

		damage_overall += hit.dmg
		subscribers_emit("damage_overall")

		if hit.dmg > damage_maximum:
			damage_maximum = hit.dmg
			subscribers_emit("damage_maximum")



func _on_shop_enter_blood() -> void :
	blood_shop += 1
	subscribers_emit("blood_shop")
	save()
