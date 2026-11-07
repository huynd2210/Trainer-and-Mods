extends "res://resources/tools/logic/fukubukuro_tool.gd"
## Gives its tool only while you hold fewer tools than the normal capacity, as unmodded.
const Caps = preload("res://mods-unpacked/Huynd-UnlimitedSlots/caps.gd")

func executeOnRoundStart(me: Tool) -> Array[Event]:
	Caps.begin()
	var r: Array[Event] = super(me)
	Caps.end()
	return r
