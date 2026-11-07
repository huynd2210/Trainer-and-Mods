extends "res://resources/tools/logic/zen_garden_tool.gd"
## Empty slots count against the normal capacity; holding more than that leaves none (never negative).
const Caps = preload("res://mods-unpacked/Huynd-UnlimitedSlots/caps.gd")

func _getEmptySlots() -> int:
	Caps.begin()
	var n: int = super()
	Caps.end()
	return maxi(n, 0)
