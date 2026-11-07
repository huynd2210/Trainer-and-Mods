extends "res://resources/reward_buffs/logic/extra_max_tools.gd"
## The reward text shows the normal capacity going up by one.
const Caps = preload("res://mods-unpacked/Huynd-UnlimitedSlots/caps.gd")

func getOutcomeText() -> String:
	Caps.begin()
	var r: String = super()
	Caps.end()
	return r
