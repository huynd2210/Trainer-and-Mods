class_name UIGameOver extends Control

const MetaProgression = preload("res://mods/meta_progression.gd")
const MetaFont = preload("res://fonts/FontDefault.tres")

@export var col_objs: UIItemSlotsCollection
@export var col_weap: UIItemSlotsCollection
@export var col_abilities: UIItemSlotsCollection
@onready var btn: UIBtnText = $Btn / Continue
@onready var ap: AnimationPlayer = $AP
@onready var item_display: UIItemDisplay = $ItemDisplay
@onready var label_score: Label = $Stats / Left / Score / Yours / Score
@onready var label_highscore: Label = $Stats / Left / Score / High / Score
@onready var label_score_new: RichTextLabel = $Stats / Left / Score / High / New
@onready var bg: TextureRect = $BG
var rewards: Array
var anim_name: String
var meta_reward_label: Label

func _ready() -> void :
	visible = false
	set_process(false)
	_setup_meta_reward_label()
	GM.on_game_ready.connect(_game_ready)
	GM.on_game_start.connect(_on_game_start)


func _process(_delta: float) -> void :
	bg.material.set("shader_parameter/offset", Vector2.ONE * (Time.get_ticks_msec() / 1000.0) * 16.0)


func _game_ready() -> void :
	GM.on_game_end_type.connect(_on_game_end_type)
	btn.pressed.connect(_pressed)
	Items.on_unlock.connect(_on_entry_unlocked)
	Chars.on_unlock.connect(_on_entry_unlocked)
	Daily.on_score_update.connect(_on_score_update)
	Daily.on_new_highscore.connect(_on_new_highscore)


func _on_game_start() -> void :
	rewards.clear()
	item_display.clear()
	meta_reward_label.visible = false
	label_score.text = str(0)
	label_highscore.text = str(StatsManager.stats.highscore)
	label_score_new.visible = false


func _on_game_end_type(type: String) -> void :
	visible = true
	set_process(true)
	meta_reward_label.visible = false
	if type != "save&exit" && !GM.is_daily:
		var meta_reward: Dictionary = MetaProgression.award_run(Player.overall_depth, type == "win")
		var earned: int = int(meta_reward["earned"])
		var total: int = int(meta_reward["total"])
		meta_reward_label.text = (
			"+%d SOUL SHARDS   |   TOTAL: %d" % [earned, total]
			if earned > 0
			else "NO SOUL SHARDS EARNED   |   TOTAL: %d" % total
		)
		meta_reward_label.visible = true
	$Audio / GameOver.play_stream()
	anim_name = "RESET"
	ap.play(anim_name)

	match type:
		"win":
			anim_name = "WIN"
			ap.play.call_deferred(anim_name)
		"lose":
			anim_name = "FAIL"
			ap.play.call_deferred(anim_name)
		"save&exit":
			anim_name = "SAVE_AND_EXIT"
			ap.play.call_deferred(anim_name)


func _setup_meta_reward_label() -> void :
	meta_reward_label = Label.new()
	meta_reward_label.name = "MetaReward"
	meta_reward_label.set_anchors_and_offsets_preset(Control.PRESET_CENTER_TOP)
	meta_reward_label.offset_left = -400.0
	meta_reward_label.offset_top = 76.0
	meta_reward_label.offset_right = 400.0
	meta_reward_label.offset_bottom = 126.0
	meta_reward_label.mouse_filter = Control.MOUSE_FILTER_IGNORE
	meta_reward_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	meta_reward_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	meta_reward_label.add_theme_font_override("font", MetaFont)
	meta_reward_label.add_theme_font_size_override("font_size", 26)
	meta_reward_label.add_theme_color_override("font_color", Color("e8b85a"))
	meta_reward_label.add_theme_color_override("font_outline_color", Color.BLACK)
	meta_reward_label.add_theme_constant_override("outline_size", 6)
	meta_reward_label.z_index = 50
	meta_reward_label.visible = false
	add_child(meta_reward_label)


func _pressed() -> void :
	match anim_name:
		"WIN", "FAIL", "SAVE_AND_EXIT":
			if !rewards.is_empty():
				trigger_rewards()
			else:
				trigger_stats()
		"REWARD":
			trigger_stats()
		"STATS":
			trigger_close()









func _on_score_update(score: int) -> void :
	label_score.text = str(score)


func _on_new_highscore(highscore: int) -> void :
	label_score_new.visible = true
	label_highscore.text = str(highscore)








func _on_entry_unlocked(entry) -> void :
	rewards.append(entry)


func trigger_rewards() -> void :
	item_display.populate(rewards)
	anim_name = "REWARD"
	ap.play(anim_name)









func trigger_stats() -> void :
	anim_name = "STATS"
	ap.play(anim_name)
	_stats_setup.call_deferred()


func trigger_close() -> void :
	await GM.transition_start()
	visible = false
	set_process(false)
	$Audio / GameOver.stop_stream()
	rewards.clear()
	item_display.clear()
	_stats_clear()
	GM.clear_run()
	GM.trigger_title()
	await GM.transition_end()


func _stats_setup() -> void :

	$Stats / Left / Char / TextureRect.texture = Player.char_entry.texture_body

	$Stats / Left / Depth / L.text = str(Player.overall_depth)

	var weaps: Array[CanvasItem]
	weaps.assign(GM.weapons.get_weaps())
	_populate_grid(col_weap, weaps, Vector2i(weaps.size(), 1))

	var tray_objs: Array[CanvasItem]
	tray_objs.assign(GM.tray.objs)
	var grid_size: Vector2i = Vector2i(7, 0)
	grid_size.y = maxi(4, ceili(float(tray_objs.size()) / float(grid_size.x)))
	_populate_grid(col_objs, tray_objs, grid_size)

	var abilities: Array[CanvasItem]
	abilities.assign(Abilities._abilities_owned)
	_populate_grid(col_abilities, abilities, Vector2i(abilities.size(), 1))
	for item: CanvasItem in abilities:
		item.scale = Vector2.ONE * 0.75
		item.get_node_or_null("Selectable/UIHoverInfo").pos = UIHoverInfo.Pos.LEFT

	$Stats / Right / C / H1 / Stats / M / V / PlayTime.text_right = Mads.format_time_sec(StatsManager.stats_run.play_time)
	$Stats / Right / C / H1 / Stats / M / V / DiceRolled.text_right = str(StatsManager.stats_run.rolls_dice_count())
	$Stats / Right / C / H1 / Stats / M / V / TrinketsRolled.text_right = str(StatsManager.stats_run.rolls_count_trinkets)
	$Stats / Right / C / H1 / Stats / M / V / Rolled1S.text_right = str(StatsManager.stats_run.rolled_1s)
	$Stats / Right / C / H1 / Stats / M / V / Criticals.text_right = str(StatsManager.stats_run.critical_count)
	$Stats / Right / C / H1 / Stats / M / V / EnemiesKilled.text_right = str(StatsManager.stats_run.enemies_death_count())
	$Stats / Right / C / H1 / Stats / M / V / DamageDealt.text_right = str(StatsManager.stats_run.damage_overall)
	$Stats / Right / C / H1 / Stats / M / V / ArmorGained.text_right = str(StatsManager.stats_run.armor_overall)
	$Stats / Right / C / H1 / Stats / M / V / ChestsOpened.text_right = str(StatsManager.stats_run.chest_opened)


func _stats_clear() -> void :
	col_weap.reset()

	col_weap.destroy_slots()
	col_objs.reset()

	col_objs.destroy_slots()
	col_abilities.reset()

	col_abilities.destroy_slots()


func _populate_grid(col: UIItemSlotsCollection, items: Array[CanvasItem], grid: Vector2i) -> void :
	col.populate_canvas_items(items)
	var width: float = col.size.x
	var height: float = col.size.y
	var w: float = width / float(grid.x)
	var h: float = height / float(grid.y)
	for i: int in col.item_slots.size():
		var x: float = - width * 0.5 + w * 0.5 + w * (i % grid.x) if grid.x > 1 else 0.0
		var y: float = - height * 0.5 + h * 0.5 + h * floor(float(i) / float(grid.x)) if grid.y > 1 else 0.9
		col.item_slots[i].global_position = (col.global_position + Vector2(width, height) * 0.5) + Vector2(x, y)
