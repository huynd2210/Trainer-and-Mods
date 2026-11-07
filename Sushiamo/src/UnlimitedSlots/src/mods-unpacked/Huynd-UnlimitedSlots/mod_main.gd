extends Node
## Unlimited Slots: hold any number of tools and sauces. See caps.gd for how.

const MOD_ID := "Huynd-UnlimitedSlots"
const LOG := "UnlimitedSlots"
const DIR := "res://mods-unpacked/Huynd-UnlimitedSlots/extensions/"
const Caps = preload("res://mods-unpacked/Huynd-UnlimitedSlots/caps.gd")

## Scripts whose use of the caps means "the normal capacity". Each extension wraps the methods concerned
## in Caps.begin()/end(); its path under extensions/ mirrors the game's own.
## Node scripts: installed as script extensions. The game has already cached the scene each one lives in
## (script -> scene below), so that scene is refreshed to pick up the extension.
const NODE_EXTENSIONS := {
	"features/common/tools/tools_manager.gd": "features/common/tools/tools.tscn",
	"features/common/sauces/sauces_manager.gd": "features/common/sauces/sauces.tscn",
	"features/menu/run_pause_menu.gd": "features/menu/run_pause_menu.tscn",
	"features/round/components/boss/round_boss.gd": "features/round/components/boss/round_boss.tscn",
}
## Item scripts: the game loads every item resource at startup, before script extensions apply, so the
## extension is swapped onto the cached resources in _itemLists() instead.
const ITEM_EXTENSIONS: Array[String] = [
	"resources/tools/logic/zen_garden_tool.gd",
	"resources/tools/logic/fukubukuro_tool.gd",
	"resources/sauces/logic/sake_res.gd",
	"resources/reward_buffs/logic/fill_sauce_slots.gd",
	"resources/reward_buffs/logic/extra_max_tools.gd",
	"resources/reward_buffs/logic/extra_max_sauces.gd",
]


func _init() -> void:
	for script: String in NODE_EXTENSIONS:
		ModLoaderMod.install_script_extension(DIR + script)
		ModLoaderMod.refresh_scene("res://" + NODE_EXTENSIONS[script])


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	var missing := _extendItems()
	if missing.is_empty():
		ModLoaderLog.info("Loaded: tools and sauces have no cap.", LOG)
	else:
		ModLoaderLog.error("Loaded, but these items were not found, so they act on the raised cap: %s" % ", ".join(missing), LOG)


func _process(_delta: float) -> void:
	if Caps.unwindIfStuck():
		ModLoaderLog.error("A capacity check did not finish; restored the offset.", LOG)
	Caps.apply()


## Swaps each ITEM_EXTENSIONS script onto the cached item resources that use the vanilla script.
## Returns the scripts that matched no resource.
func _extendItems() -> Array[String]:
	var byVanilla := {}
	for path in ITEM_EXTENSIONS:
		byVanilla["res://" + path] = load(DIR + path)
	var found := {}
	for list: Array in _itemLists():
		for res: Resource in list:
			var script: Script = res.get_script()
			if script != null and byVanilla.has(script.resource_path):
				_swapScript(res, byVanilla[script.resource_path])
				found[script.resource_path] = true
	var missing: Array[String] = []
	for path: String in byVanilla:
		if not found.has(path):
			missing.append(path.get_file())
	return missing


static func _itemLists() -> Array[Array]:
	return [ResourcesDB.allTools, ResourcesDB.allSauces, ResourcesDB.allRewardBuffs]


## Replaces a resource's script, keeping the values of its script variables.
static func _swapScript(res: Resource, script: Script) -> void:
	var values := {}
	for p in res.get_property_list():
		if p.usage & PROPERTY_USAGE_SCRIPT_VARIABLE:
			values[p.name] = res.get(p.name)
	res.set_script(script)
	for key: String in values:
		res.set(key, values[key])
