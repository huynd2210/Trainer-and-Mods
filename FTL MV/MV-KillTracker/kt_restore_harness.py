"""Restore test: seed metaVariables BEFORE the tracker loads, then prove the
per-type breakdown is rebuilt from the save by probing the shipped roster."""
import sys

tracker_path, roster_path, out_path = sys.argv[1], sys.argv[2], sys.argv[3]
tracker_src = open(tracker_path, encoding='utf-8').read()
roster_src = open(roster_path, encoding='utf-8').read()

STUBS = r"""
HANDLERS = {}
RENDER = nil
INIT = nil
LOGS = {}

Defines = {
    InternalEvents = {
        ON_TICK = "tick", ON_KEY_DOWN = "down", ON_KEY_UP = "up",
        JUMP_ARRIVE = "jump",
    },
    RenderEvents = { GUI_CONTAINER = "gui" },
}
script = {
    on_internal_event = function(ev, fn)
        HANDLERS[ev] = HANDLERS[ev] or {}
        table.insert(HANDLERS[ev], fn)
    end,
    on_render_event = function(_, _, post) RENDER = post end,
    on_init = function(fn) INIT = fn end,
    on_load = function(fn) end,
}
function log(msg) LOGS[#LOGS + 1] = msg end

Graphics = {
    GL_Color = function() return {} end,
    CSurface = { GL_SetColor = function() end },
    freetype = {
        easy_print = function() end,
        easy_printRightAlign = function() end,
    },
}

Hyperspace = {
    ships = { player = {}, enemy = nil },
    App = {
        world = { bStartedGame = true },
        gui = { event_pause = false, menu_pause = false },
    },
    WindowFrame = function() return { Draw = function() end } end,
}
Hyperspace.metaVariables = setmetatable({}, { __index = function() return 0 end })

-- Pre-existing save data, written by a previous play session.
Hyperspace.metaVariables["kt_h_ELITE_SHIP_MANTIS"]  = 7
Hyperspace.metaVariables["kt_c_ELITE_SHIP_MANTIS"]  = 4
Hyperspace.metaVariables["kt_h_ELITE_SHIP_REBEL"] = 12
Hyperspace.metaVariables["kt_total_hull"]      = 19
Hyperspace.metaVariables["kt_total_crew"]      = 4
"""

TESTS = r"""
--------------------------------------------------------------------
local failures = 0
local function check(name, got, want)
    if got ~= want then
        print(string.format("FAIL  %-48s got=%s want=%s", name, tostring(got), tostring(want)))
        failures = failures + 1
    else
        print(string.format("ok    %-48s %s", name, tostring(got)))
    end
end

local KT = mods.killtracker

-- Nothing is read until the game actually starts.
check("roster shipped", #KT.knownShips > 1000, true)

INIT()  -- run start -> ensure_loaded()

local ms = KT.kills["ELITE_SHIP_MANTIS"]
local rf = KT.kills["ELITE_SHIP_REBEL"]
check("restored ELITE_SHIP_MANTIS hull", ms and ms.hull, 7)
check("restored ELITE_SHIP_MANTIS crew", ms and ms.crew, 4)
check("restored ELITE_SHIP_REBEL hull", rf and rf.hull, 12)
check("restored ELITE_SHIP_REBEL crew", rf and rf.crew, 0)

-- Types with no saved kills must not appear.
local absent = 0
for name, e in pairs(KT.kills) do
    if e.hull == 0 and e.crew == 0 then absent = absent + 1 end
end
check("no empty entries restored", absent, 0)
check("exactly two types restored", (function()
    local n = 0
    for _ in pairs(KT.kills) do n = n + 1 end
    return n
end)(), 2)

-- A further kill must build on the restored count, not reset it.
local function tick() for _, f in ipairs(HANDLERS["tick"]) do f() end end
local enemy = {
    bDestroyed = false, bAutomated = false,
    myBlueprint = { blueprintName = "ELITE_SHIP_MANTIS" },
    ship = { hullIntegrity = { first = 8, second = 8 } },
    _crew = 2,
}
function enemy:CountCrew() return self._crew end
Hyperspace.ships.enemy = enemy
tick()
enemy.bDestroyed = true
tick()
check("new kill increments restored count", KT.kills["ELITE_SHIP_MANTIS"].hull, 8)
check("new kill written back to save", Hyperspace.metaVariables["kt_h_ELITE_SHIP_MANTIS"], 8)
check("running total incremented", Hyperspace.metaVariables["kt_total_hull"], 20)

print("")
if failures == 0 then
    print("ALL RESTORE CHECKS PASSED")
else
    print(failures .. " CHECK(S) FAILED")
end
"""

open(out_path, 'w', encoding='utf-8').write(
    STUBS + "\n" + roster_src + "\n" + tracker_src + "\n" + TESTS)
print("wrote", out_path)
