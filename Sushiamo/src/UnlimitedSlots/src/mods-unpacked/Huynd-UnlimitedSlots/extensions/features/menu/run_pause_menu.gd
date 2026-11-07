extends "res://features/menu/run_pause_menu.gd"
## The run bonus lines show the normal capacity.
const Caps = preload("res://mods-unpacked/Huynd-UnlimitedSlots/caps.gd")

func _updateRunInfo() -> void:
	Caps.begin()
	super()
	Caps.end()
