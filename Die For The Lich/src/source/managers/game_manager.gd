extends Node


const VERSION: String = "v0.8.2"
const COLOR_HIGHLIGHT: Color = Color.ORANGE


enum State{
	NULL, 
	TITLE, 
	INTERACTION, 
	BATTLE, 
	GAME_OVER, 
}
const STATE_NAME: Array = [
	"TITLE", 
	"INTERACTION", 
	"BATTLE", 
	"GAME_OVER", 
]


var await_is_running: bool:
	get():
		return _await_count > 0
var _await_count: int = 0
func await_started() -> void :
	_await_count += 1
func await_ended() -> void :
	_await_count -= 1
func await_method(method: Callable) -> void :
	await_started()
	await method.call()
	await_ended()
func await_signal(sig: Signal) -> void :
	await_started()
	await sig
	await_ended()

var screen_size: Vector2 = Vector2(ProjectSettings.get_setting("display/window/size/viewport_width"), ProjectSettings.get_setting("display/window/size/viewport_height"))
var game_is_ready: bool
var game: Game
var act: int
var ui: UI
var battle: Battle
var tray: Tray
var map: Map
var weapons: WeapController
var state: State = State.NULL:
	set(v):
		on_state_change.emit.call_deferred(v, state)
		state = v
var is_paused: bool = false
var _paused_count: int = 0
var playing: bool
var is_daily: bool

var random: RandomNumberGenerator:
	get():


		return random
signal on_state_change(state: State, state_prev: State)
signal on_game_ready
signal on_game_start
signal on_game_end
signal on_game_end_data(won: bool)
signal on_game_end_type(type: String)
signal on_await_game_end(data: Dictionary)
signal on_game_clear
signal on_title_appear
signal on_title_disappear
signal on_score_appear
signal on_score_disappear
signal on_shop_appear
signal on_shop_disappear
signal on_reward_appear
signal on_reward_disappear
signal on_abilities_appear
signal on_abilities_disappear
signal on_healing_appear
signal on_healing_disappear
signal on_dice_combine_complete(new_obj: TrayObj)
signal on_dice_combine_appear
signal on_dice_combine_disappear
signal on_map_appear
signal on_map_disappear
signal on_chest_open
signal on_offer_taken
signal on_act_start(act_id: int)
signal on_act_start_pre(act_id: int)
signal on_act_complete(act_id: int)
signal on_map_node_complete


func _init() -> void :
	await Data.on_user_data_loaded
	Mads.fullscreen_toggle.call_deferred(Data.user_data.fullscreen)


func _ready() -> void :

	random = RandomNumberGenerator.new()
	await PRELOADS.await_load()
	process_mode = PROCESS_MODE_ALWAYS

	Act.CreateEntries()


func init_game() -> void :
	game_is_ready = true
	on_game_ready.emit.call_deferred()

	on_reward_disappear.connect(_foo)
	on_dice_combine_disappear.connect(_foo)
	on_healing_disappear.connect(_foo)
	on_shop_disappear.connect(_foo)
	trigger_title()

	await transition_end()

	if !DevManager.enable:
		ui.menu.trigger("message")


func _foo() -> void :
	if playing:
		on_map_node_complete.emit()


func trigger_title() -> void :
	state = State.TITLE
	ui.main_menu.toggle(true)
	if is_daily:
		ui.menu.trigger("daily", ["play"])
	is_daily = false
	on_title_appear.emit()



func start_run(data: RunData) -> void :
	state = State.INTERACTION
	# Map rewards are generated before Player.run_start, so restore this run-local
	# state here. The guarantee itself is consumed only when a weapon chest opens.
	Player.legacy_weapon_chest_claimed = data.legacy_weapon_chest_claimed
	battle.on_battle_end_winner.connect(_on_battle_end_winner)
	_await_count = 0
	random.seed = Mads.generate_seed() if data.random_seed == 0 else data.random_seed
	is_daily = random.seed == Daily.get_daily_seed()
	Daily.run_start(data.score)
	await GM.transition_start()
	playing = true
	act = DevManager.act_start_force if DevManager.act_start_force >= 0 else data.act_id
	await Mads.await_emit(on_act_start_pre, act)
	map.generate(act, data.map_selected_act_grid_pos)
	ui.main_menu.toggle(false)
	on_title_disappear.emit()
	await Player.run_start(data)
	StatsManager.run_start(data.stats)
	await Mads.await_emit(on_game_start)
	on_act_start.emit(act)
	await GM.transition_end()


func trigger_battle(enemy_entries: Array[EnemyManager.MapEntry], is_elite: bool) -> void :
	state = State.BATTLE
	ui.map.toggle(false, false)
	GM.battle.setup_and_start(enemy_entries, is_elite)


func trigger_chest(items_group: Items.Group, _type: String = "common") -> void :
	state = State.INTERACTION
	if _type == "weap":
		items_group = Rewards.apply_legacy_weapon_chest(items_group)
	GM.ui.chest_reward.trigger(items_group, _type)


func trigger_angel(items_group: Items.Group) -> void :
	state = State.INTERACTION
	GM.ui.offering.trigger(items_group)


func trigger_shop(shop_bundle: Items.ShopBundle) -> void :
	state = State.INTERACTION
	GM.ui.shop.trigger(shop_bundle)


func _on_battle_end_winner(player_winner: bool) -> void :

	if player_winner:
		await ui.score.trigger_update_score()
	await GM.transition_start()
	state = State.INTERACTION

	if player_winner:

		var win_game: bool = Player.map_complete >= 1.0 && act == Act.count - 1

		if win_game:
			on_act_complete.emit(act)
			end_run("win")

		else:

			trigger_chest(ui.map.current_node.spot_data.items_group, ui.map.current_node.spot_data.chest_type)
			await GM.transition_end()
			await on_reward_disappear

			if Player.map_complete >= 1.0 && playing:
				on_act_complete.emit(act)
				act += 1
				await Mads.await_emit(on_act_start_pre, act)
				map.clear()
				map.generate(act)
				on_act_start.emit(act)

		await GM.transition_end()

	else:

		end_run("lose")
		await GM.transition_end()


func abandon_run() -> void :
	await GM.transition_start()
	await end_run("lose" if is_daily else "save&exit")
	await GM.transition_end()










func end_run(type: String) -> void :
	state = State.GAME_OVER
	playing = false
	await Mads.await_emit(on_await_game_end, {"type": type})
	on_game_end.emit()
	on_game_end_data.emit(type == "win")
	on_game_end_type.emit(type)





func clear_run() -> void :
	act = 0
	battle.on_battle_end_winner.disconnect(_on_battle_end_winner)
	battle.clear()
	tray.clear(true)
	weapons.clear()
	Abilities.clear()
	on_game_clear.emit()







func pause(enable: bool) -> void :
	if enable: _paused_count = _paused_count + 1
	else: _paused_count = maxi(_paused_count - 1, 0)
	is_paused = _paused_count > 0
	get_tree().paused = is_paused


func transition(callable: Callable) -> void :
	GM.ui.transition.trigger(callable)

func transition_start() -> void :
	await GM.ui.transition.start()

func transition_end() -> void :
	await GM.ui.transition.end()


func timeout(time: float, process_always: bool = false, process_in_phy: bool = false, ignore_time_scale: bool = false) -> void :
	await get_tree().create_timer(time, process_always, process_in_phy, ignore_time_scale).timeout
