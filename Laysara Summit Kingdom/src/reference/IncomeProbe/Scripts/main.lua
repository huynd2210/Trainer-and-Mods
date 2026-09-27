-- Temporary probe: learns the live layout of the money system. Read-only except F9.
local TAG = "[IncomeProbe] "
local dir = debug.getinfo(1, "S").source:match("^@(.*[\\/])") or ""
local OUT = dir .. "..\\probe.txt"
local fh = io.open(OUT, "a")
local function w(s)
    fh:write(os.date("%H:%M:%S "), s, "\n"); fh:flush()
end
w("==== probe start")

local function fmt(v)
    local t = type(v)
    if t ~= "userdata" then return tostring(v) end
    local ok, s = pcall(function() return v:ToString() end)
    if ok and s then return '"' .. s .. '"' end
    ok, s = pcall(function() return v:GetFullName() end)
    if ok and s then return s end
    ok, s = pcall(function() return "#" .. v:GetArrayNum() end)
    if ok then return "TArray" .. s end
    return "<userdata>"
end

local function dumpObj(obj, label, stopAtEngine)
    w("== " .. label .. " " .. obj:GetFullName())
    local c = obj:GetClass()
    while c and c:IsValid() do
        local cname = c:GetFullName()
        if stopAtEngine and not (cname:find("/Script/AS%.") or cname:find("/Game/")) then break end
        w(" -- class " .. cname)
        c:ForEachProperty(function(p)
            local n = p:GetFName():ToString()
            local ok, v = pcall(function() return obj[n] end)
            w(string.format("   %s : %s = %s", n, p:GetClass():GetFName():ToString(), ok and fmt(v) or "?"))
        end)
        c:ForEachFunction(function(f) w("   fn " .. f:GetFName():ToString()) end)
        c = c:GetSuperStruct()
    end
end

local function widgetText(wd)
    local ok, s = pcall(function() return wd:GetText():ToString() end)
    if ok then return s end
    ok, s = pcall(function() return wd.Text:ToString() end)
    if ok then return s end
    return nil
end

local function dumpTree(wd, depth)
    if not wd or not wd:IsValid() then return end
    local pad = string.rep("  ", depth)
    local line = pad .. wd:GetClass():GetFName():ToString() .. " " .. wd:GetFName():ToString()
    local ok, vis = pcall(function() return wd:GetVisibility() end)
    if ok then line = line .. " vis=" .. tostring(vis) end
    local slot = wd.Slot
    if slot and slot:IsValid() then
        local sc = slot:GetClass():GetFName():ToString()
        line = line .. " slot=" .. sc
        if sc == "GridSlot" then
            line = line .. string.format(" r=%s c=%s rs=%s cs=%s ha=%s va=%s pad=(%s,%s,%s,%s)",
                tostring(slot.Row), tostring(slot.Column), tostring(slot.RowSpan), tostring(slot.ColumnSpan),
                tostring(slot.HorizontalAlignment), tostring(slot.VerticalAlignment),
                tostring(slot.Padding.Left), tostring(slot.Padding.Top), tostring(slot.Padding.Right), tostring(slot.Padding.Bottom))
        end
    end
    local t = widgetText(wd)
    if t then line = line .. ' text="' .. t .. '"' end
    w(line)
    -- user widget: descend into its own tree
    local okT, tree = pcall(function() return wd.WidgetTree end)
    if okT and tree and tree:IsValid() and tree.RootWidget and tree.RootWidget:IsValid() then
        dumpTree(tree.RootWidget, depth + 1)
    end
    local okC, n = pcall(function() return wd:GetChildrenCount() end)
    if okC and n then
        for i = 0, n - 1 do dumpTree(wd:GetChildAt(i), depth + 1) end
    end
    local okN, content = pcall(function() return wd:GetContent() end)
    if okN and content and content:IsValid() and not okC then dumpTree(content, depth + 1) end
end

local state = { mm = nil, dumpedMM = false, last = "", hookedTip = false, tipDumps = 0, dumpedMW = false, dumpedText = false }

local function snapshot()
    local mm = FindFirstOf("ASMoneyManager")
    if not mm or not mm:IsValid() then state.mm = nil; return end
    if not state.dumpedMM or state.mm ~= mm:GetAddress() then
        state.mm = mm:GetAddress()
        state.dumpedMM = true
        dumpObj(mm, "MoneyManager", true)
        local gm = FindFirstOf("ASGameModeBase")
        if gm and gm:IsValid() then
            dumpObj(gm, "GameMode", true)
            w("world=" .. fmt(gm:GetWorld()))
        end
        local om = FindFirstOf("ASObjectiveManager")
        if om and om:IsValid() then w("ObjectiveManager " .. om:GetFullName() .. " bShowingMoneyTooltip=" .. tostring(om.bShowingMoneyTooltip)) end
    end
    local gm = FindFirstOf("ASGameModeBase")
    local cnt, prog = "?", "?"
    if gm and gm:IsValid() then
        pcall(function() cnt = gm:GetBaseTimeUnitCounterInt() end)
        pcall(function() prog = string.format("%.2f", gm:GetBaseUnitTimeProgress()) end)
    end
    local om = FindFirstOf("ASObjectiveManager")
    local tip = om and om:IsValid() and tostring(om.bShowingMoneyTooltip) or "?"
    local s = string.format("Money=%s Balance=%s Rev=%s Exp=%s Base=%s unit=%s tip=%s",
        fmt(mm.Money), fmt(mm.Balance), fmt(mm.TotalRevenue), fmt(mm.TotalExpenses), fmt(mm.BaseIncome), tostring(cnt), tip)
    if s ~= state.last then
        w(s .. " prog=" .. prog); state.last = s
    end

    if not state.hookedTip then
        local t = FindFirstOf("BP_ASMoneyTooltip_C")
        if t and t:IsValid() then
            state.hookedTip = true
            w("tooltip found " .. t:GetFullName())
            local ok, err = pcall(RegisterHook, "/Game/UI/Tooltips/BP_ASMoneyTooltip.BP_ASMoneyTooltip_C:FillData", function(ctx)
                local self = ctx:get()
                if state.tipDumps < 2 then
                    state.tipDumps = state.tipDumps + 1
                    w("---- FillData tree #" .. state.tipDumps)
                    dumpTree(self.WidgetTree.RootWidget, 0)
                end
                if not state.dumpedText then
                    state.dumpedText = true
                    dumpObj(self, "Tooltip", true)
                    local v = self.AS_Text_RevenueValue
                    if v and v:IsValid() then dumpObj(v, "AS_Text", true) end
                end
            end)
            w("hook FillData ok=" .. tostring(ok) .. " " .. tostring(err))
        end
    end
    if not state.dumpedMW then
        local mw = FindFirstOf("BP_ASMoneyWidget_C")
        if mw and mw:IsValid() then
            state.dumpedMW = true
            dumpObj(mw, "MoneyWidget", true)
            w("---- MoneyWidget tree")
            dumpTree(mw.WidgetTree.RootWidget, 0)
        end
    end
end

LoopAsync(500, function()
    ExecuteInGameThread(function()
        local ok, err = pcall(snapshot)
        if not ok then w("ERR " .. tostring(err)) end
    end)
    return false
end)

RegisterKeyBind(Key.F9, function()
    ExecuteInGameThread(function()
        local mm = FindFirstOf("ASMoneyManager")
        if not mm or not mm:IsValid() then return end
        local before = mm.Money
        local ok, err = pcall(function() mm:DebugAddMoney(10) end)
        w(string.format("F9 DebugAddMoney(10) ok=%s err=%s money %s -> %s", tostring(ok), tostring(err), fmt(before), fmt(mm.Money)))
    end)
end)

print(TAG .. "loaded, writing " .. OUT .. "\n")
