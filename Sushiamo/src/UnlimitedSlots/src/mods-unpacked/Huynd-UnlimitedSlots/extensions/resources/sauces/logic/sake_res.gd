extends "res://resources/sauces/logic/sake_res.gd"
## Fills the empty slots of the normal capacity, as unmodded.
const Caps = preload("res://mods-unpacked/Huynd-UnlimitedSlots/caps.gd")

func canUse(_me: Sauce, _ingredients: Array[Ingredient] = [], _selected: Array[Ingredient] = []) -> bool:
	Caps.begin()
	var r: bool = super(_me, _ingredients, _selected)
	Caps.end()
	return r


func executeUse(me: Sauce, _ingredients: Array[Ingredient] = [], _selected: Array[Ingredient] = []) -> Array[Event]:
	Caps.begin()
	var r: Array[Event] = super(me, _ingredients, _selected)
	Caps.end()
	return r
