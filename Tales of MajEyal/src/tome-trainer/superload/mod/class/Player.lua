local _M = loadPrevious(...)
local Trainer = require "mod.class.TrainerState"
local Archery = require "mod.class.interface.Archery"

local function pack(...) return { n = select("#", ...), ... } end

local function protectedFromDeath(self)
	return Trainer.no_death and Trainer.isPlayerCharacter(self)
end

local function lifeFloor(self)
	return math.max(1, self.die_at + 1)
end

-------------------------------------------------------------------------------
-- Fatal hit leaves 1 HP
-------------------------------------------------------------------------------

-- Clamp after every shield/resist/callback in onTakeHit has run: the same spot the game's
-- own "unstoppable" clamp uses, so value is exactly what is about to leave self.life.
local base_onTakeHit = _M.onTakeHit
function _M:onTakeHit(value, src, death_note)
	value = base_onTakeHit(self, value, src, death_note)
	if value and value > 0 and protectedFromDeath(self) and self.life - value <= self.die_at then
		value = math.max(0, self.life - lifeFloor(self))
		game:delayedLogMessage(self, nil, "trainer_no_death", "#GOLD#[Trainer]#LAST# A fatal hit leaves #Source# at 1 HP.")
	end
	return value
end

-- Backstop for deaths that bypass takeHit (instakills, swallow, ...). Deaths the player chose
-- (endgame sacrifices, Eidolon "let me die") carry special_death_msg and still go through.
local base_die = _M.die
function _M:die(src, death_note)
	if protectedFromDeath(self) and not (death_note and death_note.special_death_msg) then
		self.dead = false
		self.life = lifeFloor(self)
		self.changed = true
		game:delayedLogMessage(self, nil, "trainer_no_death", "#GOLD#[Trainer]#LAST# A fatal hit leaves #Source# at 1 HP.")
		return false
	end
	return base_die(self, src, death_note)
end

-------------------------------------------------------------------------------
-- Never miss
-------------------------------------------------------------------------------

-- Target evasion (Evasion, Armour of Shadows, ...) is rolled by the attacker: self:checkEvasion(target).
local base_checkEvasion = _M.checkEvasion
function _M:checkEvasion(target)
	if Trainer.never_miss then return nil end
	return base_checkEvasion(self, target)
end

-- Melee: auto_melee_hit is the game's own "this blow connects" flag (used by talents such as
-- Staff Combat); it skips both the accuracy roll and the can't-see-target penalty.
local base_attackTargetWith = _M.attackTargetWith
function _M:attackTargetWith(...)
	if not Trainer.never_miss then return base_attackTargetWith(self, ...) end
	local prev = self.turn_procs.auto_melee_hit
	self.turn_procs.auto_melee_hit = true
	local r = pack(base_attackTargetWith(self, ...))
	self.turn_procs.auto_melee_hit = prev
	return unpack(r, 1, r.n)
end

-- Ranged: the archery hit roll is the first self:checkHit() inside Archery's archery_projectile.
-- __trainer_force_hit makes that one roll succeed; blind_fight covers the can't-see penalty.
-- checkHit is otherwise untouched: it is also used for saving throws where self is the defender.
local base_checkHit = _M.checkHit
function _M:checkHit(atk, def, ...)
	if self.__trainer_force_hit then
		self.__trainer_force_hit = nil
		return true, 100
	end
	return base_checkHit(self, atk, def, ...)
end

local function wrapArcheryProjectile(orig)
	return function(tx, ty, tg, self, tmp)
		if not (Trainer.never_miss and Trainer.isPlayerCharacter(self)) then return orig(tx, ty, tg, self, tmp) end
		local prev_blind_fight = self.blind_fight
		self.__trainer_force_hit = true
		self.blind_fight = (prev_blind_fight or 0) + 1
		local r = pack(orig(tx, ty, tg, self, tmp))
		self.__trainer_force_hit = nil
		self.blind_fight = prev_blind_fight
		return unpack(r, 1, r.n)
	end
end

-- archery_projectile is a file-local of Archery.lua, handed to self:projectile() by archeryShoot.
-- It is one shared upvalue, so replacing it through any function that closes over it replaces
-- it for every class. Scan all of Archery's functions in case another addon wrapped archeryShoot.
local function hookArchery()
	for _, f in pairs(Archery) do
		if type(f) == "function" then
			local i = 1
			while true do
				local name, v = debug.getupvalue(f, i)
				if not name then break end
				if name == "archery_projectile" and type(v) == "function" then
					local wrapped = wrapArcheryProjectile(v)
					debug.setupvalue(f, i, wrapped)
					Archery.archery_projectile = wrapped
					return true
				end
				i = i + 1
			end
		end
	end
	return false
end

if not Trainer.archery_hooked then
	Trainer.archery_hooked = hookArchery()
	if not Trainer.archery_hooked then print("[Trainer] archery_projectile not found: ranged attacks can still miss") end
end

return _M
