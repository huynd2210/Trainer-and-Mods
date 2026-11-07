extends "res://features/common/tools/tools_manager.gd"
## The shelf counter shows held / normal capacity, e.g. 7/5.
const Caps = preload("res://mods-unpacked/Huynd-UnlimitedSlots/caps.gd")

func _updateCounter() -> void:
	Caps.begin()
	super()
	Caps.end()
