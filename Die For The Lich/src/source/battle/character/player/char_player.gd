extends Character

const MetaProgression = preload("res://mods/meta_progression.gd")

@export var _level_up_gain_max_hp: int = 0
var char_entry: Chars.Entry



func _ready() -> void :
	super._ready()
	level_up_gain_hp_max = _level_up_gain_max_hp
	Player.alive = true
	on_death.connect(_on_death)


	GM.battle.on_crit_pre.connect($Audio / Critical.play_stream)
	$ProgressBar.on_level_up.connect(on_level_up.emit)


func player_setup(_char_entry: Chars.Entry, data: RunData) -> void :

	super.player_setup(_char_entry, data)
	char_entry = _char_entry
	if data.char_hp_max > 0:
		hp_max = data.char_hp_max
		hp = data.char_hp
	elif !GM.is_daily:
		var vitality_level: int = MetaProgression.get_level("vitality")
		hp_max += vitality_level * 5
		hp = hp_max








	# Starter arrays belong to the character definition. Always work on private copies so
	# legacy bonuses cannot permanently grow a character's loadout between new runs.
	var _weaps: Array[Items.Entry] = (Items.get_entries_by_names(data.weaps) if !data.weaps.is_empty() else char_entry.weaps).duplicate()
	var _tray_objs: Array[Items.Entry] = (Items.get_entries_by_names(data.tray_objs) if !data.tray_objs.is_empty() else char_entry.tray_objs).duplicate()
	var _ablities: Array[Items.Entry] = (Items.get_entries_by_names(data.abilities) if !data.abilities.is_empty() else char_entry.abilities).duplicate()

	if !GM.is_daily && !is_instance_valid(data.record):
		var bonus_die: Items.Entry = _get_legacy_starting_item(
			Items.Type.DIE,
			MetaProgression.get_level("starting_die"),
			Items.Rarity.COMMON,
		)
		if is_instance_valid(bonus_die):
			_tray_objs.append(bonus_die)

		var bonus_trinket: Items.Entry = _get_legacy_starting_item(
			Items.Type.TRINKET,
			MetaProgression.get_level("starting_trinket"),
			Items.Rarity.COMMON,
		)
		if is_instance_valid(bonus_trinket):
			_tray_objs.append(bonus_trinket)

		var bonus_ability: Items.Entry = _get_legacy_starting_item(
			Items.Type.ABILITY,
			MetaProgression.get_level("starting_ability"),
			Items.Rarity.UNCOMMON,
			_ablities,
		)
		if is_instance_valid(bonus_ability):
			_ablities.append(bonus_ability)

		_append_legacy_items(
			_tray_objs,
			Items.get_entry_die(PRELOADS.DIE_LUCKY, Die.Type.D6),
			MetaProgression.get_level("lucky_d6"),
		)
		_append_legacy_items(
			_tray_objs,
			Items.get_entry_die(PRELOADS.DIE_LUCKY, Die.Type.D20),
			MetaProgression.get_level("lucky_d20"),
		)
		_append_legacy_items(
			_tray_objs,
			Items.coin,
			MetaProgression.get_level("starting_coins") * 3,
		)
		_append_legacy_items(
			_tray_objs,
			Items.get_entry_trinket(PRELOADS.TRINKET_CLOVER),
			MetaProgression.get_level("starting_clovers"),
		)

	GM.weapons.setup(_weaps)
	GM.tray.setup(_tray_objs)
	Abilities.setup(_ablities)

	if !data.weaps_curses.is_empty():
		for curse_data: Dictionary in data.weaps_curses:
			var slot_id: int = curse_data.get("weap_slot_id", -1)
			var curse_name: String = curse_data.get("name", "")
			var curse_stack: int = curse_data.get("stacks", "")
			var weap: Weapon = GM.weapons.get_weap_from_slot_id(slot_id)
			if is_instance_valid(weap):
				WeaponMark.CurseWeap(weap, curse_name, curse_stack)


func _get_legacy_starting_item(
	type: Items.Type,
	level: int,
	first_rarity: Items.Rarity,
	excluded: Array[Items.Entry] = [],
) -> Items.Entry:
	if level <= 0:
		return null
	var rarity: Items.Rarity = clampi(
		int(first_rarity) + clampi(level, 1, 3) - 1,
		Items.Rarity.COMMON,
		Items.Rarity.LEGENDARY,
	) as Items.Rarity
	var fetch: Items.Fetch = Items.fetch()
	fetch.set_force_locked(true)
	fetch.set_type([type])
	fetch.set_rarity([rarity])
	var entries: Array[Items.Entry] = fetch.get_entries()
	for excluded_entry: Items.Entry in excluded:
		entries.erase(excluded_entry)
	return null if entries.is_empty() else Mads.array_pick_random(entries, GM.random)


func _append_legacy_items(
	target: Array[Items.Entry],
	entry: Items.Entry,
	count: int,
) -> void:
	if !is_instance_valid(entry):
		return
	for _index: int in count:
		target.append(entry)


func _on_death() -> void :
	Player.alive = false
