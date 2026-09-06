--[[
////////////////////
MV TRAINER - resource cheats on F5..F10
////////////////////

Every cheat is one self-contained entry in the `cheats` registry, keyed by the
key that triggers it. Adding a cheat is a single register() call; the key
dispatcher below never needs to change.
]]--

mods = mods or {}
mods.trainer = {}
local T = mods.trainer

-- How full a "refill" makes each consumable.
T.fuelTarget = 30
T.missileTarget = 30
T.droneTarget = 30

-- SDL 1.2 keysyms. F11/F12 are left alone: F11 is the usual fullscreen toggle
-- and F12 is Steam's screenshot key.
local KEYS = {
    F4 = 285,
    F5 = 286,
    F6 = 287,
    F7 = 288,
    F8 = 289,
    F9 = 290,
    F10 = 291,
}

----------------------------------------------------------------------
-- Cheat registry
----------------------------------------------------------------------

local cheats = {}

-- register(keyName, label, apply) -- apply receives the player ShipManager.
local function register(keyName, label, apply)
    local key = KEYS[keyName]
    if not key then
        log("[MV Trainer] unknown key name: " .. tostring(keyName))
        return
    end
    cheats[key] = { keyName = keyName, label = label, apply = apply }
end

-- Amount needed to bring `current` up to `target`, never negative so a
-- refill can only ever help.
local function top_up(current, target)
    if current >= target then return 0 end
    return target - current
end

local function give_scrap(amount)
    return function(ship)
        -- income=true so it counts as earned scrap and shows the floating text.
        ship:ModifyScrapCount(amount, true)
    end
end

local function refill_fuel(ship)
    ship.fuel_count = ship.fuel_count + top_up(ship.fuel_count, T.fuelTarget)
end

local function refill_missiles(ship)
    ship:ModifyMissileCount(top_up(ship:GetMissileCount(), T.missileTarget))
end

local function refill_drones(ship)
    ship:ModifyDroneCount(top_up(ship:GetDroneCount(), T.droneTarget))
end

local function refill_everything(ship)
    refill_fuel(ship)
    refill_missiles(ship)
    refill_drones(ship)
end

-- Hull back to max, and every crew member of *this* ship back to full health.
-- The crew list also holds enemy boarders standing on your deck, so filter by
-- iShipId -- that keeps your own away team (aboard the enemy) healed and leaves
-- the intruders bleeding.
local function full_heal(ship)
    local hull = ship.ship.hullIntegrity
    if hull.first < hull.second then
        hull.first = hull.second
    end

    local crewList = ship.vCrewList
    local healed = 0
    for i = 0, crewList:size() - 1 do
        local crew = crewList[i]
        if crew and crew.iShipId == ship.iShipId then
            -- Same call Multiverse uses to top a crew member up; it clamps.
            crew:DirectModifyHealth(9999)
            healed = healed + 1
        end
    end
    return healed
end

register("F4", "hull repaired and crew healed", full_heal)
register("F5", "+100 scrap", give_scrap(100))
register("F6", "+1000 scrap", give_scrap(1000))
register("F7", "fuel refilled", refill_fuel)
register("F8", "missiles refilled", refill_missiles)
register("F9", "drone parts refilled", refill_drones)
register("F10", "fuel, missiles and drone parts refilled", refill_everything)

T.cheats = cheats

----------------------------------------------------------------------
-- Input
----------------------------------------------------------------------

-- Keys currently held, so OS key-repeat can't fire a cheat over and over.
local held = {}

-- The only real requirement is a player ship to act on. Deliberately NOT
-- blocked on event_pause/menu_pause: Multiverse guards its own hotkeys that way
-- because they fire game events, but handing yourself scrap while a store or
-- event box is open is exactly when you want it, and it conflicts with nothing.
-- Returns nil when allowed, or the reason it was refused.
local function refusal_reason(ship)
    if not Hyperspace.App.world.bStartedGame then return "no run in progress" end
    if not ship then return "no player ship" end
    return nil
end

script.on_internal_event(Defines.InternalEvents.ON_KEY_DOWN, function(key)
    local cheat = cheats[key]
    if not cheat or held[key] then return end
    held[key] = true

    local ship = Hyperspace.ships.player
    local refused = refusal_reason(ship)
    if refused then
        -- Always say something. A silent no-op is indistinguishable from a
        -- broken mod, which is exactly how this went wrong the first time.
        log("[MV Trainer] " .. cheat.keyName .. " ignored: " .. refused)
        return
    end

    cheat.apply(ship)
    log("[MV Trainer] " .. cheat.keyName .. ": " .. cheat.label)
end)

script.on_internal_event(Defines.InternalEvents.ON_KEY_UP, function(key)
    held[key] = nil
end)
