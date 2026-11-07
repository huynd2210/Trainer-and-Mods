extends "res://features/round/components/boss/round_boss.gd"
## A paid boss reroll still needs a normal-capacity slot to give up, as unmodded. (Losing that slot
## no longer destroys a tool: holding more tools than the capacity is allowed.)
const Caps = preload("res://mods-unpacked/Huynd-UnlimitedSlots/caps.gd")

func _shouldShowRerollButton() -> bool:
	Caps.begin()
	var r: bool = super()
	Caps.end()
	return r
