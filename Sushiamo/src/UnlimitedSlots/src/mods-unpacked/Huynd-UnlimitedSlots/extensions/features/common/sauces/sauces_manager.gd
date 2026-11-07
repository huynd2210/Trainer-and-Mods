extends "res://features/common/sauces/sauces_manager.gd"
## The shelf counter shows held / normal capacity, e.g. 5/3.
const Caps = preload("res://mods-unpacked/Huynd-UnlimitedSlots/caps.gd")

func _updateCounter() -> void:
	Caps.begin()
	super()
	Caps.end()
