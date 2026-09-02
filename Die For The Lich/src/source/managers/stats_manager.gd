extends Node

var stats: Stats
var stats_run: Stats
signal on_loaded


func _ready() -> void :

	GM.on_game_ready.connect(_game_ready)



func _game_ready() -> void :

	stats = Stats.Load()
	stats.can_save = true
	stats.on_loaded.connect(on_loaded.emit)


func run_start(_stats_run_continue: Stats) -> void :
	if is_instance_valid(_stats_run_continue):
		stats_run = _stats_run_continue
	else:
		stats_run = Stats.new()


func get_stat(stat_property: String) -> Variant:
	var stat: Variant = Mads.get_nested(stats, stat_property)
	if stat == null: return 0
	return stat


func get_stat_name(sn: String) -> String:

	# These are exact internal/stat keys. The original used substring matching,
	# so hiding the obsolete "enemies_death.imp" entry also hid the valid
	# "imp_brother" and "imp_sister" enemies from the statistics screen.
	if [
		"locked_forever",
		"dice_rolls.NULL",
		"ankou_ignore_stags",
		"battles_won_no_shield_use",
		"blood_shop",
		"roll_dice_ghost",
		"roll_dice_negative",
		"roll_dice_lucky",
		"objects_drawn_from_bag",
		"rolled_20s_ghost",
		"enemies_death.giant",
		"enemies_death.imp",
		"enemies_death.devil",
	].has(sn):
		return ""

	if sn.contains("enemies_death"):
		return "Defeated " + sn.replace("enemies_death.", "").capitalize().replace("_", " ")

	elif sn.contains("dice_rolls"):
		return "Rolled " + sn.replace("dice_rolls.", "").capitalize().replace("D ", "D") + "s"

	else:
		return sn.replace("_", " ").replace(".", " ").capitalize()
