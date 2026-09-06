--[[
////////////////////
MV KILL TRACKER - counts enemy ships killed, per type and per cause
////////////////////

Causes are data, not branches: adding one is a single CAUSES entry. Same for the
hotkeys, which are a keycode -> handler registry.

Detection has to be polled. Hyperspace has no ship-destroyed event, and a
crew-wiped ship is not "destroyed" at all -- it stays intact as a lootable hulk.
So each tick we watch the current enemy and look for whichever transition comes
first, with a once-per-encounter guard so a crew wipe followed by blowing up the
hulk cannot count twice.

Persistence is Hyperspace's metaVariables map (string -> int), which survives
across runs. It cannot be iterated from Lua, so roster.lua ships the list of ship
type names and we probe the map by name on load.
]]--

mods = mods or {}
mods.killtracker = mods.killtracker or {}
local KT = mods.killtracker

-- SDL 1.2 keysyms. The trainer owns F5..F10; F11/F12 stay with the OS and Steam.
local KEYS = {
    F2 = 283,
    F3 = 284,
}

local TOTAL_HULL_VAR = "kt_total_hull"
local TOTAL_CREW_VAR = "kt_total_crew"

-- Each cause carries its own storage keys and wording.
local CAUSES = {
    hull = { prefix = "kt_h_", totalVar = TOTAL_HULL_VAR, label = "hull destroyed" },
    crew = { prefix = "kt_c_", totalVar = TOTAL_CREW_VAR, label = "crew wiped" },
}

-- Overlay geometry, in FTL's 1280x720 UI space.
local PANEL = {
    x = 946, y = 32, w = 324,
    rowHeight = 14, maxRows = 12,
    headerHeight = 34, footerHeight = 30,
    fontSize = 10,
}

----------------------------------------------------------------------
-- Store
----------------------------------------------------------------------

-- kills[typeName] = { hull = n, crew = n }
local kills = {}
local loaded = false

local function entry(typeName)
    local e = kills[typeName]
    if not e then
        e = { hull = 0, crew = 0 }
        kills[typeName] = e
    end
    return e
end

local function meta_get(key)
    return Hyperspace.metaVariables[key] or 0
end

-- Rebuild the table from the save by probing every known ship name. Cheap enough
-- to do once (a couple of thousand hash lookups) and the only way back in,
-- since the map cannot be enumerated.
local function ensure_loaded()
    if loaded then return end
    loaded = true
    local roster = KT.knownShips
    if not roster then
        log("[MV Kill Tracker] roster.lua missing - persisted per-type counts unavailable")
        return
    end
    local restored = 0
    for i = 1, #roster do
        local name = roster[i]
        local hull = meta_get(CAUSES.hull.prefix .. name)
        local crew = meta_get(CAUSES.crew.prefix .. name)
        if hull > 0 or crew > 0 then
            local e = entry(name)
            e.hull = hull
            e.crew = crew
            restored = restored + 1
        end
    end
    log("[MV Kill Tracker] restored " .. restored .. " ship type(s) from save")
end

local function record(typeName, causeName)
    ensure_loaded()
    local cause = CAUSES[causeName]
    if not cause then return end

    local e = entry(typeName)
    e[causeName] = e[causeName] + 1

    Hyperspace.metaVariables[cause.prefix .. typeName] = e[causeName]
    Hyperspace.metaVariables[cause.totalVar] = meta_get(cause.totalVar) + 1

    log(string.format("[MV Kill Tracker] %s (%s) -- %d hull / %d crew for this type",
        typeName, cause.label, e.hull, e.crew))
end

KT.kills = kills
KT.record = record

-- Sorted view: biggest killers first, ties broken by name so the panel is stable.
local function ranked()
    local rows = {}
    for name, e in pairs(kills) do
        local total = e.hull + e.crew
        if total > 0 then
            rows[#rows + 1] = { name = name, hull = e.hull, crew = e.crew, total = total }
        end
    end
    table.sort(rows, function(a, b)
        if a.total ~= b.total then return a.total > b.total end
        return a.name < b.name
    end)
    return rows
end

local function totals()
    return meta_get(TOTAL_HULL_VAR), meta_get(TOTAL_CREW_VAR)
end

----------------------------------------------------------------------
-- Detection
----------------------------------------------------------------------

-- State for the enemy currently on screen.
local enc = { typeName = nil, hadCrew = false, counted = false }

local function reset_encounter()
    enc.typeName = nil
    enc.hadCrew = false
    enc.counted = false
end

script.on_internal_event(Defines.InternalEvents.ON_TICK, function()
    if not Hyperspace.App.world.bStartedGame then return end

    local enemy = Hyperspace.ships.enemy
    if not enemy then
        reset_encounter()
        return
    end

    local typeName = tostring(enemy.myBlueprint.blueprintName)
    if enc.typeName ~= typeName then
        reset_encounter()
        enc.typeName = typeName
    end
    if enc.counted then return end

    local crew = enemy:CountCrew(false)
    if crew > 0 then enc.hadCrew = true end

    if enemy.bDestroyed or enemy.ship.hullIntegrity.first <= 0 then
        -- Blown apart.
        record(typeName, "hull")
        enc.counted = true
    elseif enc.hadCrew and crew == 0 and not enemy.bAutomated then
        -- Intact but everyone aboard is dead. Automated ships never qualify:
        -- they start crewless, which is not a kill.
        record(typeName, "crew")
        enc.counted = true
    end
end)

-- A new beacon means a new encounter, even against the same ship type.
script.on_internal_event(Defines.InternalEvents.JUMP_ARRIVE, function()
    reset_encounter()
end)

script.on_init(function()
    reset_encounter()
    ensure_loaded()
end)

----------------------------------------------------------------------
-- Overlay
----------------------------------------------------------------------

local showPanel = false
local frame = nil
local frameHeight = -1

local function panel_height(rowCount)
    return PANEL.headerHeight + rowCount * PANEL.rowHeight + PANEL.footerHeight
end

-- MV's type names are long; keep the panel narrow.
local function short_name(name)
    if #name <= 26 then return name end
    return string.sub(name, 1, 25) .. "."
end

local function draw_panel()
    if not showPanel then return end
    if not Hyperspace.App.world.bStartedGame then return end
    ensure_loaded()

    local rows = ranked()
    local shown = math.min(#rows, PANEL.maxRows)
    local height = panel_height(shown + (#rows > shown and 1 or 0))

    if not frame or frameHeight ~= height then
        frame = Hyperspace.WindowFrame(0, 0, PANEL.w, height)
        frameHeight = height
    end
    frame:Draw(PANEL.x, PANEL.y)

    local left = PANEL.x + 12
    local right = PANEL.x + PANEL.w - 12
    local y = PANEL.y + 18
    local fs = PANEL.fontSize

    Graphics.CSurface.GL_SetColor(Graphics.GL_Color(1, 1, 1, 1))
    Graphics.freetype.easy_print(fs, left, y, "SHIPS KILLED")
    Graphics.freetype.easy_printRightAlign(fs, right, y, "hull / crew")
    y = y + PANEL.headerHeight - 18

    if shown == 0 then
        Graphics.CSurface.GL_SetColor(Graphics.GL_Color(0.7, 0.7, 0.7, 1))
        Graphics.freetype.easy_print(fs, left, y, "no kills recorded yet")
        y = y + PANEL.rowHeight
    else
        Graphics.CSurface.GL_SetColor(Graphics.GL_Color(1, 1, 1, 1))
        for i = 1, shown do
            local r = rows[i]
            Graphics.freetype.easy_print(fs, left, y, short_name(r.name))
            Graphics.freetype.easy_printRightAlign(fs, right, y, r.hull .. " / " .. r.crew)
            y = y + PANEL.rowHeight
        end
        if #rows > shown then
            Graphics.CSurface.GL_SetColor(Graphics.GL_Color(0.7, 0.7, 0.7, 1))
            Graphics.freetype.easy_print(fs, left, y,
                "+" .. (#rows - shown) .. " more -- F3 dumps all to FTL.log")
            y = y + PANEL.rowHeight
        end
    end

    local hull, crew = totals()
    Graphics.CSurface.GL_SetColor(Graphics.GL_Color(1, 1, 1, 1))
    Graphics.freetype.easy_print(fs, left, y + 6, "TOTAL " .. (hull + crew))
    Graphics.freetype.easy_printRightAlign(fs, right, y + 6, hull .. " / " .. crew)
    Graphics.CSurface.GL_SetColor(Graphics.GL_Color(1, 1, 1, 1))
end

script.on_render_event(Defines.RenderEvents.GUI_CONTAINER, function() end, draw_panel)

----------------------------------------------------------------------
-- Hotkeys
----------------------------------------------------------------------

local function dump_to_log()
    ensure_loaded()
    local rows = ranked()
    local hull, crew = totals()
    log("[MV Kill Tracker] ---- kills by ship type (" .. #rows .. " types) ----")
    for i = 1, #rows do
        local r = rows[i]
        log(string.format("[MV Kill Tracker]   %-40s hull %-5d crew %-5d total %d",
            r.name, r.hull, r.crew, r.total))
    end
    log(string.format("[MV Kill Tracker] ---- total %d (hull %d / crew %d) ----",
        hull + crew, hull, crew))
end

local function toggle_panel()
    showPanel = not showPanel
    log("[MV Kill Tracker] panel " .. (showPanel and "shown" or "hidden"))
end

KT.dump = dump_to_log

local handlers = {}
local function bind(keyName, label, fn)
    local key = KEYS[keyName]
    if not key then
        log("[MV Kill Tracker] unknown key name: " .. tostring(keyName))
        return
    end
    handlers[key] = { keyName = keyName, label = label, fn = fn }
end

bind("F2", "toggle kill panel", toggle_panel)
bind("F3", "dump kills to log", dump_to_log)

-- Keys currently held, so OS key-repeat cannot fire a handler repeatedly.
local held = {}

script.on_internal_event(Defines.InternalEvents.ON_KEY_DOWN, function(key)
    local handler = handlers[key]
    if not handler or held[key] then return end
    held[key] = true

    local gui = Hyperspace.App.gui
    if gui.event_pause or gui.menu_pause then return end

    handler.fn()
end)

script.on_internal_event(Defines.InternalEvents.ON_KEY_UP, function(key)
    held[key] = nil
end)
