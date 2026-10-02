-- Trainer state and hotkey actions.
-- Toggles live only in this Lua state: they start OFF on every launch and never enter the savefile.

local Trainer = {
	no_death = false,
	never_miss = false,
	archery_hooked = false,
}

function Trainer.say(fmt, ...)
	game.log("#GOLD#[Trainer]#LAST# "..fmt, ...)
end

-- The characters the trainer acts for: whoever is controlled, plus the main character.
function Trainer.isPlayerCharacter(actor)
	if not game or not actor then return false end
	return actor == game.player or actor == game:getPlayer(true)
end

local function flyer(p, text, color)
	if not (game.level and game.level.map and p.x and p.y) then return end
	local sx, sy = game.level.map:getTileToScreen(p.x, p.y, true)
	game.flyers:add(sx, sy, 30, 0, -2, text, color, true)
end

local function toggle(field, label)
	return function(p)
		Trainer[field] = not Trainer[field]
		local on = Trainer[field]
		Trainer.say("%s: %s", label, on and "#LIGHT_GREEN#ON#LAST#" or "#LIGHT_RED#OFF#LAST#")
		flyer(p, label..(on and " ON" or " OFF"), on and {0, 255, 0} or {255, 80, 80})
	end
end

-- One entry per hotkey. Adding a function = adding an entry here; hooks/load.lua and the
-- Game superload iterate this list and never change.
-- Key strings: "sym:<key>:<ctrl>:<shift>:<alt>:<meta>". F9-F12 are unbound in stock ToME.
Trainer.actions = {
	{
		type = "TRAINER_HEAL_FULL",
		key = "sym:_F9:false:false:false:false",
		name = "Trainer: heal to full",
		run = function(p)
			p.life = p.max_life
			p.changed = true
			Trainer.say("Healed to full (%d HP).", p.max_life)
			flyer(p, "FULL HEAL", {0, 255, 0})
		end,
	},
	{
		type = "TRAINER_TOGGLE_NO_DEATH",
		key = "sym:_F10:false:false:false:false",
		name = "Trainer: toggle fatal hit leaves 1 HP",
		run = toggle("no_death", "Fatal hit leaves 1 HP"),
	},
	{
		type = "TRAINER_ADD_EXP",
		key = "sym:_F11:false:false:false:false",
		name = "Trainer: add 1000 EXP",
		run = function(p)
			-- Go through p:gainExp so addon rules still apply (Possessors banks EXP while possessing),
			-- but undo the birth EXP multiplier so the net gain is exactly 1000. Levels up as needed.
			local old_level = p.level
			local mult = game.state and game.state.birth and game.state.birth.exp_multiplier or 1
			p:gainExp(1000 / mult)
			Trainer.say("+1000 EXP (level %d%s).", p.level, p.level > old_level and (", was %d"):format(old_level) or "")
			flyer(p, "+1000 EXP", {255, 215, 0})
		end,
	},
	{
		type = "TRAINER_TOGGLE_NEVER_MISS",
		key = "sym:_F12:false:false:false:false",
		name = "Trainer: toggle never miss",
		run = toggle("never_miss", "Never miss"),
	},
}

function Trainer.defineKeys(KeyBind)
	for _, a in ipairs(Trainer.actions) do
		KeyBind:defineAction{ default = { a.key }, type = a.type, group = "trainer", name = a.name }
	end
end

function Trainer.binds(g)
	local t = {}
	for _, a in ipairs(Trainer.actions) do
		t[a.type] = function()
			local p = g.player
			if p and not p.dead then a.run(p) end
		end
	end
	return t
end

return Trainer
