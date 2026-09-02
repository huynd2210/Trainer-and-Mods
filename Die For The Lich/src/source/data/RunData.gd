class_name RunData extends Resource


static var PATH: String:
	get():
		return Data.PATH + "run_data.tres"


static func Load() -> RunData:
	var data: RunData = ResourceLoader.load(PATH, "RunData", ResourceLoader.CACHE_MODE_IGNORE) if ResourceLoader.exists(PATH) else null
	return data


@export_storage var version: String = "none"
@export_storage var random_seed: int = 0
@export_storage var act_id: int = 0
@export_storage var score: int
@export_storage var map_selected_act_grid_pos: Array[Vector3i]
@export_storage var record: RunRecord = null
@export_storage var stats: Stats = null

@export_storage var char_name: String = ""
@export_storage var char_hp: int = 0
@export_storage var char_hp_max: int = 0
@export_storage var char_level: int = 1
@export_storage var char_xp: int = 0
@export_storage var tray_objs: Array[String]
@export_storage var weaps: Array[String]
@export_storage var abilities: Array[String]
@export_storage var weaps_curses: Array[Dictionary]
@export_storage var legacy_weapon_chest_claimed: bool = false


func setup_pre_save() -> void :

	version = GM.VERSION
	random_seed = GM.random.seed
	act_id = GM.act
	score = Daily.score_current
	map_selected_act_grid_pos = GM.map.selected_act_grid_pos.duplicate()
	record = Player.run_record
	char_name = Player.char.char_name
	char_hp = Player.char.hp
	char_hp_max = Player.char.hp_max
	char_level = Player.level
	char_xp = Player.xp
	legacy_weapon_chest_claimed = Player.legacy_weapon_chest_claimed

	tray_objs.clear()
	for obj: TrayObj in GM.tray.objs:
		if !obj.is_temp:
			tray_objs.append(obj.item_entry.item_name)

	weaps.clear()
	for weap: Weapon in GM.weapons.get_weaps():
		weaps.append(weap.item_entry.item_name)
		if weap.cursed:
			weaps_curses.append({"weap_slot_id": weap.slot.id, "name": weap.curse.mark_name, "stacks": weap.curse.curse_stacks})

	abilities.clear()
	for ability: Ability in Abilities._abilities_owned:
		abilities.append(ability.item_entry.item_name)

	stats = StatsManager.stats_run



func save() -> int:
	var error: Error = ResourceSaver.save(self, PATH, ResourceSaver.FLAG_CHANGE_PATH)
	return error


func delete() -> bool:
	var error: Error = DirAccess.remove_absolute(PATH)
	return error == Error.OK
