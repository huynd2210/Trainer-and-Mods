extends "res://resources/reward_buffs/logic/fill_sauce_slots.gd"
## Fills the empty slots of the normal capacity, as unmodded.
const Caps = preload("res://mods-unpacked/Huynd-UnlimitedSlots/caps.gd")

func getAffectedItems() -> Array[ItemInstance]:
	Caps.begin()
	var r: Array[ItemInstance] = super()
	Caps.end()
	return r


func getOutcomeText() -> String:
	Caps.begin()
	var r: String = super()
	Caps.end()
	return r
