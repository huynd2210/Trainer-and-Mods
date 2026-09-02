extends RefCounted


const SAVE_VERSION: int = 3
const SAVE_FILE: String = "meta_progression.json"
const TEST_UPGRADE_COST: int = 1
const UPGRADE_ORDER: Array[String] = [
	"vitality",
	"bulwark",
	"scholar",
	"starting_die",
	"starting_trinket",
	"starting_ability",
	"legendary_weapon_chest",
	"lucky_d6",
	"lucky_d20",
	"starting_coins",
	"starting_clovers",
	"full_bag_draw",
	"rerolls",
]
const UPGRADE_DEFINITIONS: Dictionary = {
	"vitality": {
		"name": "Vitality",
		"description": "+5 maximum HP at the start of a new run per level",
		"base_cost": 5,
		"max_level": 5,
	},
	"bulwark": {
		"name": "Bulwark",
		"description": "+2 armor at the start of every fight per level",
		"base_cost": 4,
		"max_level": 5,
	},
	"scholar": {
		"name": "Scholar",
		"description": "+10% experience gained during a run per level",
		"base_cost": 6,
		"max_level": 5,
	},
	"starting_die": {
		"name": "Loaded Bag",
		"description": "+1 random starting die: Common / Uncommon / Rare",
		"base_cost": 8,
		"max_level": 3,
	},
	"starting_trinket": {
		"name": "Heirloom",
		"description": "+1 random starting trinket: Common / Uncommon / Rare",
		"base_cost": 10,
		"max_level": 3,
	},
	"starting_ability": {
		"name": "Arcane Legacy",
		"description": "+1 random starting ability: Uncommon / Rare / Legendary",
		"base_cost": 15,
		"max_level": 3,
	},
	"legendary_weapon_chest": {
		"name": "Fated Arsenal",
		"description": "First weapon chest offers a Legendary weapon and shield",
		"base_cost": 25,
		"max_level": 1,
	},
	"lucky_d6": {
		"name": "Lucky Sixes",
		"description": "Add 1 / 2 / 3 Lucky D6 to each new run",
		"base_cost": 8,
		"max_level": 3,
	},
	"lucky_d20": {
		"name": "Lucky Twenties",
		"description": "Add 1 / 2 / 3 Lucky D20 to each new run",
		"base_cost": 14,
		"max_level": 3,
	},
	"starting_coins": {
		"name": "Inheritance",
		"description": "Start each new run with 3 / 6 / 9 coins",
		"base_cost": 6,
		"max_level": 3,
	},
	"starting_clovers": {
		"name": "Clover Patch",
		"description": "Add 1 / 2 / 3 clovers to each new run",
		"base_cost": 8,
		"max_level": 3,
	},
	"full_bag_draw": {
		"name": "Bottomless Draw",
		"description": "Fill the tray from the bag on turn 1 of every fight",
		"base_cost": 30,
		"max_level": 1,
	},
	"rerolls": {
		"name": "Second Chance",
		"description": "+1 free die reroll each fight per level",
		"base_cost": 7,
		"max_level": 5,
	},
}


static func _save_path() -> String:
	return Data.PATH + SAVE_FILE


static func _default_data() -> Dictionary:
	return {
		"version": SAVE_VERSION,
		"shards": 0,
		"total_shards_earned": 0,
		"runs_rewarded": 0,
		"highest_depth": 0,
		"upgrades": {
			"vitality": 0,
			"bulwark": 0,
			"scholar": 0,
			"starting_die": 0,
			"starting_trinket": 0,
			"starting_ability": 0,
			"legendary_weapon_chest": 0,
			"lucky_d6": 0,
			"lucky_d20": 0,
			"starting_coins": 0,
			"starting_clovers": 0,
			"full_bag_draw": 0,
			"rerolls": 0,
		},
	}


static func load_data() -> Dictionary:
	var data: Dictionary = _default_data()
	var path: String = _save_path()
	if FileAccess.file_exists(path):
		var file: FileAccess = FileAccess.open(path, FileAccess.READ)
		if file:
			var parsed: Variant = JSON.parse_string(file.get_as_text())
			if parsed is Dictionary:
				data.merge(parsed, true)
				if parsed.get("upgrades", null) is Dictionary:
					data["upgrades"].merge(parsed["upgrades"], true)

	data["version"] = SAVE_VERSION
	data["shards"] = maxi(0, int(data.get("shards", 0)))
	data["total_shards_earned"] = maxi(0, int(data.get("total_shards_earned", 0)))
	data["runs_rewarded"] = maxi(0, int(data.get("runs_rewarded", 0)))
	data["highest_depth"] = maxi(0, int(data.get("highest_depth", 0)))
	for key: String in UPGRADE_ORDER:
		var definition: Dictionary = UPGRADE_DEFINITIONS[key]
		data["upgrades"][key] = clampi(
			int(data["upgrades"].get(key, 0)),
			0,
			int(definition["max_level"]),
		)
	return data


static func save_data(data: Dictionary) -> bool:
	var file: FileAccess = FileAccess.open(_save_path(), FileAccess.WRITE)
	if !file:
		push_error("Could not save meta-progression data.")
		return false
	file.store_string(JSON.stringify(data, "\t"))
	return true


static func get_definition(key: String) -> Dictionary:
	return UPGRADE_DEFINITIONS.get(key, {})


static func get_level(key: String) -> int:
	return int(load_data()["upgrades"].get(key, 0))


static func get_cost(key: String, level: int = -1) -> int:
	if TEST_UPGRADE_COST > 0:
		return TEST_UPGRADE_COST

	var definition: Dictionary = get_definition(key)
	if definition.is_empty():
		return 0
	if level < 0:
		level = get_level(key)
	return int(definition["base_cost"]) * (level + 1)


static func purchase(key: String) -> bool:
	var definition: Dictionary = get_definition(key)
	if definition.is_empty():
		return false

	var data: Dictionary = load_data()
	var level: int = int(data["upgrades"].get(key, 0))
	if level >= int(definition["max_level"]):
		return false

	var cost: int = get_cost(key, level)
	if int(data["shards"]) < cost:
		return false

	data["shards"] = int(data["shards"]) - cost
	data["upgrades"][key] = level + 1
	return save_data(data)


static func get_respec_refund(data: Dictionary = {}) -> int:
	if data.is_empty():
		data = load_data()
	var refund: int = 0
	for key: String in UPGRADE_ORDER:
		var level: int = int(data["upgrades"].get(key, 0))
		for purchased_level: int in level:
			refund += get_cost(key, purchased_level)
	return refund


static func respec() -> int:
	var data: Dictionary = load_data()
	var refund: int = get_respec_refund(data)
	if refund <= 0:
		return 0

	data["shards"] = int(data["shards"]) + refund
	for key: String in UPGRADE_ORDER:
		data["upgrades"][key] = 0
	if !save_data(data):
		return 0
	return refund


static func award_run(depth: int, won: bool) -> Dictionary:
	var earned: int = 0
	if depth > 0:
		earned = maxi(1, ceili(float(depth) / 2.0))
		if won:
			earned += 5

	var data: Dictionary = load_data()
	if earned > 0:
		data["shards"] = int(data["shards"]) + earned
		data["total_shards_earned"] = int(data["total_shards_earned"]) + earned
		data["runs_rewarded"] = int(data["runs_rewarded"]) + 1
		data["highest_depth"] = maxi(int(data["highest_depth"]), depth)
		save_data(data)

	return {
		"earned": earned,
		"total": int(data["shards"]),
	}
