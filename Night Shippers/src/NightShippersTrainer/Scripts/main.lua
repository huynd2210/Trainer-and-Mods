--[[
    Night Shippers Trainer
    Grants gold on a keypress.

    Night Shippers keeps the live in-shift gold on the replicated coop GameState
    (BP_GameState_Coop_C.Gold). The game's own "give me gold" path is:

        BP_PC_Coop_C : Add gold to game state(Value)
            -> Server_Add Gold to Gamestate(Value)          [server RPC]
                -> BP_GameState_Coop_C : Add Gold Only(Add Value)
                    -> Gold += Value, replicates, OnRep_Gold refreshes the HUD

    We call the top of that chain, so the grant is authoritative and shows on the
    HUD whether you are hosting or joined someone else's lobby.

    Confirmation is the gold counter on the HUD; details go to ue4ss/UE4SS.log.
    (UKismetSystemLibrary::PrintString is compiled out of Shipping builds, so
    there is no on-screen debug text to use here.)
]]

local UEHelpers = require("UEHelpers")

local ModName = "NightShippersTrainer"

local Defaults = {
    GoldAmount   = 1000,
    GiveGoldKey  = "F10",
    ModifierKeys = {},
}

local function log(message)
    print(string.format("[%s] %s\n", ModName, message))
end

local function loadConfig()
    local ok, config = pcall(require, "config")
    if not ok or type(config) ~= "table" then
        log("config.lua missing or invalid, using defaults")
        return Defaults
    end
    for key, value in pairs(Defaults) do
        if config[key] == nil then config[key] = value end
    end
    return config
end

local Config = loadConfig()

---------------------------------------------------------------------------
-- object lookup
---------------------------------------------------------------------------

local function isUsable(object)
    return object ~= nil and object:IsValid()
end

local function getGameState()
    local gameState = FindFirstOf("BP_GameState_Coop_C")
    if isUsable(gameState) then return gameState end
    return nil
end

local function getLocalPlayerController()
    local controller = UEHelpers.GetPlayerController()
    if isUsable(controller) then return controller end

    -- On a listen server every player's controller exists here, so pick ours.
    for _, candidate in ipairs(FindAllOf("BP_PC_Coop_C") or {}) do
        if isUsable(candidate) then
            local ok, isLocal = pcall(function() return candidate:IsLocalPlayerController() end)
            if ok and isLocal then return candidate end
        end
    end
    return nil
end

---------------------------------------------------------------------------
-- grant strategies, tried in order until one runs without erroring
---------------------------------------------------------------------------

local function callUFunction(object, functionName, argument)
    return pcall(function()
        local ufunction = object[functionName]
        if ufunction == nil then error(functionName .. " not found on object", 0) end
        if argument == nil then ufunction(object) else ufunction(object, argument) end
    end)
end

local GrantStrategies = {
    {
        name = "player controller server RPC",
        run = function(amount)
            local controller = getLocalPlayerController()
            if not isUsable(controller) then return false, "no local player controller" end
            local ok, err = callUFunction(controller, "Add gold to game state", amount)
            if not ok then return false, tostring(err) end
            return true
        end,
    },
    {
        name = "game state direct",
        run = function(amount)
            local gameState = getGameState()
            if not isUsable(gameState) then return false, "no coop game state" end

            local ok, err = callUFunction(gameState, "Add Gold Only", amount)
            if ok then return true end

            -- Last resort: write the property, then run the replication callback
            -- by hand so the HUD picks the new value up.
            local written = pcall(function() gameState.Gold = gameState.Gold + amount end)
            if not written then return false, tostring(err) end
            callUFunction(gameState, "OnRep_Gold")
            return true
        end,
    },
}

local function reportResultLater(amount, goldBefore)
    -- As host the grant lands this frame; as a client it arrives with the next
    -- replication, so read the value back once it has had a chance to land.
    ExecuteWithDelay(500, function()
        ExecuteInGameThread(function()
            local gameState = getGameState()
            local goldAfter = isUsable(gameState) and gameState.Gold or goldBefore
            if goldAfter == goldBefore then
                log(string.format("requested +%d gold but the total is still %d "
                    .. "(as a client the host decides, so this can lag or be refused)",
                    amount, goldBefore))
            else
                log(string.format("+%d gold (%d -> %d)", amount, goldBefore, goldAfter))
            end
        end)
    end)
end

local function giveGold()
    local amount = Config.GoldAmount
    local gameState = getGameState()
    if not isUsable(gameState) then
        log("not in a shift yet - load into a run and try again")
        return
    end

    local goldBefore = gameState.Gold
    local failures = {}

    for _, strategy in ipairs(GrantStrategies) do
        local ok, reason = strategy.run(amount)
        if ok then
            log(string.format("granted via %s", strategy.name))
            reportResultLater(amount, goldBefore)
            return
        end
        failures[#failures + 1] = string.format("%s: %s", strategy.name, reason)
    end

    log("could not grant gold:")
    for _, failure in ipairs(failures) do log("  " .. failure) end
end

---------------------------------------------------------------------------
-- keybind
---------------------------------------------------------------------------

local function registerKeybind()
    local keyName = tostring(Config.GiveGoldKey)
    local key = Key[keyName]
    if key == nil then
        log(string.format("unknown key '%s' in config.lua, falling back to F10", keyName))
        keyName, key = "F10", Key.F10
    end

    local modifiers, modifierNames = {}, {}
    for _, name in ipairs(Config.ModifierKeys or {}) do
        local modifier = ModifierKey[tostring(name):upper()]
        if modifier then
            modifiers[#modifiers + 1] = modifier
            modifierNames[#modifierNames + 1] = tostring(name):upper()
        else
            log(string.format("unknown modifier '%s' in config.lua, ignored", tostring(name)))
        end
    end

    local handler = function() ExecuteInGameThread(giveGold) end
    if #modifiers > 0 then
        RegisterKeyBind(key, modifiers, handler)
    else
        RegisterKeyBind(key, handler)
    end

    local label = keyName
    if #modifierNames > 0 then label = table.concat(modifierNames, "+") .. "+" .. keyName end
    log(string.format("ready - press %s for +%d gold", label, Config.GoldAmount))
end

registerKeybind()
