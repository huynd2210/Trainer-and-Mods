"""Build a self-contained Lua file: stubs + trainer.lua + assertions."""
import sys

trainer_path, out_path = sys.argv[1], sys.argv[2]
trainer_src = open(trainer_path, encoding='utf-8').read()

STUBS = r"""
HANDLERS = {}
LOGS = {}

Defines = { InternalEvents = { ON_KEY_DOWN = "down", ON_KEY_UP = "up" } }
script = { on_internal_event = function(ev, fn) HANDLERS[ev] = fn end }
function log(msg) LOGS[#LOGS + 1] = msg end

-- Stand-in for a Hyperspace std::vector binding: :size() plus 0-based indexing.
function make_vec(items)
    local v = { _n = #items }
    for i, item in ipairs(items) do v[i - 1] = item end
    function v:size() return self._n end
    return v
end

function make_crew(o)
    local c = {
        iShipId = o.shipId or 0,
        health = { first = o.hp or 30, second = o.maxHp or 100 },
    }
    function c:DirectModifyHealth(amount)
        local h = self.health
        h.first = math.min(h.second, h.first + amount)
        return true
    end
    return c
end

function make_ship(o)
    o = o or {}
    local s = {
        fuel_count = o.fuel or 5,
        currentScrap = o.scrap or 0,
        _missiles = o.missiles or 2,
        _drones = o.drones or 1,
        bJumping = o.jumping or false,
        iShipId = 0,
        ship = { hullIntegrity = { first = o.hull or 12, second = o.maxHull or 30 } },
        vCrewList = make_vec(o.crew or {}),
    }
    function s:ModifyScrapCount(n, income)
        self.currentScrap = self.currentScrap + n
        self.lastIncome = income
    end
    function s:GetMissileCount() return self._missiles end
    function s:ModifyMissileCount(n) self._missiles = self._missiles + n end
    function s:GetDroneCount() return self._drones end
    function s:ModifyDroneCount(n) self._drones = self._drones + n end
    return s
end

Hyperspace = {
    ships = { player = nil },
    App = {
        world = { bStartedGame = true },
        gui = { event_pause = false, menu_pause = false },
    },
}
"""

TESTS = r"""
--------------------------------------------------------------------
local down = HANDLERS["down"]
local up   = HANDLERS["up"]

local failures = 0
local function check(name, got, want)
    if got ~= want then
        print(string.format("FAIL  %-46s got=%s want=%s", name, tostring(got), tostring(want)))
        failures = failures + 1
    else
        print(string.format("ok    %-46s %s", name, tostring(got)))
    end
end

-- SDL 1.2 keysyms, taken from Hyperspace's luaDefines.h
local F4, F5, F6, F7, F8, F9, F10 = 285, 286, 287, 288, 289, 290, 291
local F11, F12 = 292, 293

local function fresh(o)
    local s = make_ship(o)
    Hyperspace.ships.player = s
    Hyperspace.App.gui.event_pause = false
    Hyperspace.App.gui.menu_pause = false
    Hyperspace.App.world.bStartedGame = true
    up(F4) up(F5) up(F6) up(F7) up(F8) up(F9) up(F10)
    return s
end

-- 1. bare F-key works, no modifier needed
local s = fresh()
down(F5)
check("F5 -> +100 scrap", s.currentScrap, 100)
check("F5 marks it as income", s.lastIncome, true)

-- 2. key repeat fires once
s = fresh()
down(F5) down(F5) down(F5)
check("F5 held (key repeat) -> once", s.currentScrap, 100)

-- 3. release and press again fires again
down(F5) -- still held, ignored
up(F5)
down(F5)
check("F5 pressed twice -> 200", s.currentScrap, 200)

-- 4. F6 grand
s = fresh()
down(F6)
check("F6 -> +1000 scrap", s.currentScrap, 1000)

-- 5. fuel tops up
s = fresh({ fuel = 5 })
down(F7)
check("F7 fuel 5 -> 30", s.fuel_count, 30)

-- 6. fuel never reduced
s = fresh({ fuel = 99 })
down(F7)
check("F7 fuel 99 stays 99", s.fuel_count, 99)

-- 7. missiles / drones
s = fresh({ missiles = 2, drones = 1 })
down(F8)
check("F8 missiles 2 -> 30", s:GetMissileCount(), 30)
down(F9)
check("F9 drones 1 -> 30", s:GetDroneCount(), 30)

-- 8. surplus not reduced
s = fresh({ missiles = 50, drones = 40 })
down(F8) down(F9)
check("F8 missiles 50 stays 50", s:GetMissileCount(), 50)
check("F9 drones 40 stays 40", s:GetDroneCount(), 40)

-- 9. refill all
s = fresh({ fuel = 1, missiles = 0, drones = 0 })
down(F10)
check("F10 fuel", s.fuel_count, 30)
check("F10 missiles", s:GetMissileCount(), 30)
check("F10 drones", s:GetDroneCount(), 30)

-- 9b. F4 full heal: hull to max
s = fresh({ hull = 4, maxHull = 30 })
down(F4)
check("F4 hull 4 -> 30", s.ship.hullIntegrity.first, 30)
check("F4 does not raise max hull", s.ship.hullIntegrity.second, 30)

-- undamaged hull is left exactly as it is
s = fresh({ hull = 30, maxHull = 30 })
down(F4)
check("F4 full hull stays 30", s.ship.hullIntegrity.first, 30)

-- crew are healed to their own max
local mine1 = make_crew({ shipId = 0, hp = 10, maxHp = 100 })
local mine2 = make_crew({ shipId = 0, hp = 3,  maxHp = 50 })
s = fresh({ hull = 5, crew = { mine1, mine2 } })
down(F4)
check("F4 heals crew member 1", mine1.health.first, 100)
check("F4 heals crew member 2 to its own max", mine2.health.first, 50)

-- enemy boarders on your deck are NOT healed
local mine3 = make_crew({ shipId = 0, hp = 20, maxHp = 100 })
local intruder = make_crew({ shipId = 1, hp = 15, maxHp = 100 })
s = fresh({ hull = 5, crew = { mine3, intruder } })
down(F4)
check("F4 heals own crew", mine3.health.first, 100)
check("F4 leaves enemy boarders wounded", intruder.health.first, 15)

-- crewless ship must not error
s = fresh({ hull = 2, crew = {} })
down(F4)
check("F4 works with no crew aboard", s.ship.hullIntegrity.first, 30)

-- 10. usable in the situations you actually want it
-- (this is the bug that made it look broken: MV's guard blocked all of these)
s = fresh()
Hyperspace.App.gui.event_pause = true
down(F5)
check("WORKS during an event/store box", s.currentScrap, 100)

s = fresh()
Hyperspace.App.gui.menu_pause = true
down(F5)
check("WORKS with the menu open", s.currentScrap, 100)

s = fresh({ jumping = true })
down(F5)
check("WORKS while jumping", s.currentScrap, 100)

-- 10b. the one genuine refusal, and it must be audible
s = fresh()
Hyperspace.App.world.bStartedGame = false
local before = #LOGS
down(F5)
check("refused outside a run", s.currentScrap, 0)
check("refusal is logged, not silent", #LOGS > before, true)
check("refusal names the reason", (function()
    local m = LOGS[#LOGS]
    return m and string.find(m, "no run in progress", 1, true) ~= nil
end)(), true)

s = fresh()
Hyperspace.ships.player = nil
before = #LOGS
up(F5) down(F5)
check("refused with no player ship", #LOGS > before, true)

-- 12. reserved / unbound keys stay inert
s = fresh()
down(F11) down(F12) down(115) -- F11, F12 (Steam screenshot), and 's'
check("F11 unbound -> inert", s.currentScrap, 0)
check("F12 left to Steam -> inert", s:GetMissileCount(), 2)
check("letter s -> inert", s.fuel_count, 5)

-- 13. every advertised key is actually bound
local bound = 0
for _, k in ipairs({ F4, F5, F6, F7, F8, F9, F10 }) do
    if mods.trainer.cheats[k] then bound = bound + 1 end
end
check("F4..F10 all bound", bound, 7)

print("")
if failures == 0 then
    print("ALL CHECKS PASSED")
else
    print(failures .. " CHECK(S) FAILED")
end
"""

open(out_path, 'w', encoding='utf-8').write(STUBS + "\n" + trainer_src + "\n" + TESTS)
print("wrote", out_path)
