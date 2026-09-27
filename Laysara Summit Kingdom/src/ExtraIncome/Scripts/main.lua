-- Extra Income for Laysara: Summit Kingdom (UE4SS Lua mod)
--
-- Pays a configurable amount into the current map's treasury every in-game month,
-- and shows it as its own row in the income breakdown (the tooltip on the treasury).
-- Hover the treasury and press + / - to change the amount; Shift for bigger steps.
-- The amount is remembered per map in amounts.txt.
--
-- Nothing is written into the save game except the money itself: the mod pays the
-- extra directly into the treasury when the game's month counter advances.

local TAG = "[ExtraIncome] "
local function log(msg) print(TAG .. msg .. "\n") end

local scriptDir = debug.getinfo(1, "S").source:match("^@(.*[\\/])") or ""
local modDir = scriptDir .. "..\\"
local AMOUNTS_FILE = modDir .. "amounts.txt"

local defaults = {
    DefaultAmount = 0,
    Step = 10,
    BigStep = 100,
    Label = "Extra income",
    RespectTreasuryCap = true,
}
local cfg = defaults
do
    local ok, user = pcall(dofile, scriptDir .. "config.lua")
    if ok and type(user) == "table" then
        cfg = {}
        for k, v in pairs(defaults) do
            if user[k] ~= nil then cfg[k] = user[k] else cfg[k] = v end
        end
    else
        log("config.lua not loaded, using defaults: " .. tostring(user))
    end
end

local TOOLTIP_CLASS = "/Game/UI/Tooltips/BP_ASMoneyTooltip.BP_ASMoneyTooltip_C"
local TEXT_CLASS = "/Game/UI/BasicWidgets/As_Text.AS_Text_C"
-- Row 5 of the tooltip's "Revenues" grid is unused by the game: it sits between
-- "Exporting goods" (row 4) and "Donations" (row 6).
local ROW = 5

---------------------------------------------------------------------------
-- per-map amounts

local amounts = {}

local function loadAmounts()
    local fh = io.open(AMOUNTS_FILE, "r")
    if not fh then return end
    for line in fh:lines() do
        local map, n = line:match("^%s*([^=#%s]+)%s*=%s*(-?%d+)%s*$")
        if map then amounts[map] = tonumber(n) end
    end
    fh:close()
end

local function saveAmounts()
    local keys = {}
    for k in pairs(amounts) do keys[#keys + 1] = k end
    table.sort(keys)
    local fh, err = io.open(AMOUNTS_FILE, "w")
    if not fh then log("cannot write " .. AMOUNTS_FILE .. ": " .. tostring(err)); return end
    fh:write("# Extra Income: monthly amount per map. Set in game by hovering the treasury\n")
    fh:write("# and pressing + / - (Shift for bigger steps).\n")
    for _, k in ipairs(keys) do fh:write(k, "=", tostring(amounts[k]), "\n") end
    fh:close()
end

loadAmounts()

---------------------------------------------------------------------------
-- game objects

local function valid(o) return o ~= nil and o:IsValid() end

local function gameMode()
    local gm = FindFirstOf("ASGameModeBase")
    if valid(gm) then return gm end
    return nil
end

-- "/Game/Maps/Map26.Map26:PersistentLevel..." -> "Map26"
local function mapKey(mm)
    local full = mm:GetFullName()
    return full:match("/Game/Maps/([^%.]+)%.") or full:match(" ([^%.:]+)%.") or "Unknown"
end

local function amountFor(map)
    local n = amounts[map]
    if n == nil then n = cfg.DefaultAmount end
    return n
end

local function signed(n)
    if n > 0 then return "+" .. n end
    return tostring(n)
end

---------------------------------------------------------------------------
-- monthly payout

local state = { mmAddr = nil, map = nil, unit = nil }

local function pay(mm, months)
    local amount = amountFor(state.map) * months
    if amount <= 0 then return end
    local before = mm.Money
    local target = before + amount
    if cfg.RespectTreasuryCap then
        local cap = mm.Capacity
        -- never take money away when the game already holds more than the cap
        if cap and target > cap then target = math.max(cap, before) end
    end
    if target ~= before then
        mm.Money = target
        pcall(function() mm:UpdateBalance() end)
    end
    log(string.format("%s: +%d for %d month(s), treasury %d -> %d (capacity %s)",
        state.map, amount, months, before, target, tostring(mm.Capacity)))
end

local function tick()
    local gm = gameMode()
    if not gm then state.mmAddr = nil; return end
    local mm = gm.MoneyManager
    if not valid(mm) then state.mmAddr = nil; return end

    local okU, unit = pcall(function() return gm:GetBaseTimeUnitCounterInt() end)
    if not okU or type(unit) ~= "number" then return end

    local addr = mm:GetAddress()
    if addr ~= state.mmAddr then
        -- new map or a loaded save: start counting from here, pay nothing yet
        state.mmAddr, state.map, state.unit = addr, mapKey(mm), unit
        log(string.format("map %s, extra income %d / month", state.map, amountFor(state.map)))
        return
    end

    if unit > state.unit then
        if not gm.bGameOver then pay(mm, unit - state.unit) end
        state.unit = unit
    elseif unit < state.unit then
        state.unit = unit -- counter went back (save reloaded on the same objects)
    end
end

LoopAsync(250, function()
    ExecuteInGameThread(function()
        local ok, err = pcall(tick)
        if not ok then log("tick error: " .. tostring(err)) end
    end)
    return false
end)

---------------------------------------------------------------------------
-- income breakdown row

local rows = {}          -- tooltip address -> { tip, label, value }
local lastTooltip = nil
local widgetLib = nil
local textClass = nil

local function copyTextLook(from, to)
    for _, p in ipairs({ "Style", "ColorStyle", "Justification", "Uppercase", "Auto-Wrap", "Auto-Padding" }) do
        pcall(function() to[p] = from[p] end)
    end
end

local function newText(tip, like)
    if not valid(widgetLib) then widgetLib = StaticFindObject("/Script/UMG.Default__WidgetBlueprintLibrary") end
    if not valid(textClass) then textClass = StaticFindObject(TEXT_CLASS) end
    if not valid(widgetLib) or not valid(textClass) then return nil end
    local w = widgetLib:Create(tip, textClass, nil)
    if not valid(w) then return nil end
    copyTextLook(like, w)
    return w
end

local function placeInGrid(grid, w, column, like)
    local slot = grid:AddChildToGrid(w, ROW, column)
    if not valid(slot) then return end
    local ref = like.Slot
    if valid(ref) then
        pcall(function() slot:SetHorizontalAlignment(ref.HorizontalAlignment) end)
        pcall(function() slot:SetVerticalAlignment(ref.VerticalAlignment) end)
        pcall(function()
            local p = ref.Padding
            slot:SetPadding({ Left = p.Left, Top = p.Top, Right = p.Right, Bottom = p.Bottom })
        end)
    end
end

local function ensureRow(tip)
    local addr = tip:GetAddress()
    local r = rows[addr]
    if r and valid(r.label) and valid(r.value) then return r end

    -- the game's "Assistance from The Capital" row is the template: label AS_Text_1
    -- in column 0, value AS_Text_AssistanceValue in column 2 of the Revenues grid
    local refValue = tip.AS_Text_AssistanceValue
    if not valid(refValue) or not valid(refValue.Slot) then return nil end
    local grid = refValue.Slot.Parent
    if not valid(grid) then return nil end
    local refLabel = refValue
    local refRow = refValue.Slot.Row
    for i = 0, grid:GetChildrenCount() - 1 do
        local c = grid:GetChildAt(i)
        if valid(c) and valid(c.Slot) and c.Slot.Row == refRow and c.Slot.Column == 0 then refLabel = c; break end
    end

    local label = newText(tip, refLabel)
    local value = newText(tip, refValue)
    if not label or not value then log("could not create breakdown row widgets"); return nil end
    label:SetText(FText(cfg.Label .. "  (+/-)"))
    placeInGrid(grid, label, 0, refLabel)
    placeInGrid(grid, value, 2, refValue)

    -- forget rows of tooltips the game has thrown away
    for k, old in pairs(rows) do
        if not valid(old.tip) then rows[k] = nil end
    end
    r = { tip = tip, label = label, value = value }
    rows[addr] = r
    return r
end

local function fillRow(tip)
    local gm = gameMode()
    if not gm then return end
    local mm = gm.MoneyManager
    if not valid(mm) then return end
    local extra = amountFor(state.map or mapKey(mm))

    local r = ensureRow(tip)
    if not r then return end
    r.value:SetText(FText(signed(extra)))

    -- the game filled these from its own numbers; add the extra to both totals
    if valid(tip.AS_Text_RevenueValue) then tip.AS_Text_RevenueValue:SetText(FText(signed(mm.TotalRevenue + extra))) end
    if valid(tip.AS_Text_BalanceValue) then tip.AS_Text_BalanceValue:SetText(FText(signed(mm.Balance + extra))) end
end

local hooked = false
local function tryHook()
    if hooked then return end
    local cls = StaticFindObject(TOOLTIP_CLASS)
    if not valid(cls) then return end
    local ok, err = pcall(RegisterHook, TOOLTIP_CLASS .. ":FillData", function(ctx)
        local tip = ctx:get()
        if not valid(tip) then return end
        lastTooltip = tip
        local okF, errF = pcall(fillRow, tip)
        if not okF then log("breakdown row error: " .. tostring(errF)) end
    end)
    hooked = ok
    log(ok and "hooked income breakdown" or ("hook failed: " .. tostring(err)))
end

-- the tooltip class loads with the HUD, i.e. once a map is open
LoopAsync(1000, function()
    ExecuteInGameThread(function() pcall(tryHook) end)
    return hooked
end)

---------------------------------------------------------------------------
-- + / - while hovering the treasury

local function tooltipOpen(gm)
    local om = gm.ObjectiveManager
    return valid(om) and om.bShowingMoneyTooltip == true
end

local function adjust(delta)
    ExecuteInGameThread(function()
        local gm = gameMode()
        if not gm or not tooltipOpen(gm) then return end
        local mm = gm.MoneyManager
        if not valid(mm) then return end
        local map = state.map or mapKey(mm)
        local n = math.max(0, amountFor(map) + delta)
        amounts[map] = n
        saveAmounts()
        log(string.format("%s: extra income set to %d / month", map, n))
        if valid(lastTooltip) then
            pcall(function() lastTooltip:FillData() end)
            pcall(fillRow, lastTooltip)
        end
    end)
end

local function bind(key, delta)
    RegisterKeyBind(key, function() adjust(delta) end)
    RegisterKeyBind(key, { ModifierKey.SHIFT }, function() adjust(delta > 0 and cfg.BigStep or -cfg.BigStep) end)
end
bind(Key.ADD, cfg.Step)
bind(Key.OEM_PLUS, cfg.Step)
bind(Key.SUBTRACT, -cfg.Step)
bind(Key.OEM_MINUS, -cfg.Step)

log("loaded")
