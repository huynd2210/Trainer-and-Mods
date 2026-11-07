extends RefCounted
## The run's stored tool and sauce caps carry a fixed OFFSET, so every "is there room?" check the game
## makes passes. Code that means the normal capacity (Zen Garden's empty slots, Sake filling slots, the
## shelf counters...) runs between begin() and end(), which take the offset off for that call only.
##
## The offset is stored in the save. Increases and decreases from rewards or boss rerolls still apply
## on top of it, so the normal capacity keeps evolving as in the unmodded game.

const OFFSET := 100
## A stored cap at or above this is already offset; the unmodded game never gets near it.
const THRESHOLD := 50
const FIELDS: Array[StringName] = [&"_maxTools", &"_maxSauces"]

static var _depth := 0
static var _removed: Array[StringName] = []


## Offsets any cap that is not offset yet (a new run, or a save made without the mod). Called every frame.
static func apply() -> void:
	if _depth > 0:
		return
	var d: RunData = RunData.getData()
	if d == null:
		return
	for f in FIELDS:
		var v: int = d.get(f)
		if v < THRESHOLD:
			d.set(f, v + OFFSET)


static func begin() -> void:
	_depth += 1
	if _depth > 1:
		return
	_removed.clear()
	var d: RunData = RunData.getData()
	if d == null:
		return
	for f in FIELDS:
		var v: int = d.get(f)
		if v >= THRESHOLD:
			d.set(f, v - OFFSET)
			_removed.append(f)


static func end() -> void:
	_depth -= 1
	if _depth > 0:
		return
	_depth = 0
	var d: RunData = RunData.getData()
	if d != null:
		for f in _removed:
			d.set(f, int(d.get(f)) + OFFSET)
	_removed.clear()


## Safety net: nothing wrapped awaits, so the depth must be 0 between frames.
static func unwindIfStuck() -> bool:
	if _depth == 0:
		return false
	_depth = 1
	end()
	return true
