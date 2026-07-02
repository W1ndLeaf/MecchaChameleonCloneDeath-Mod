--==============================================================================
-- MECCHA CHAMELEON - No-Decoy-Death  (host-side; lobby toggle)
--
-- When a Hunter shoots a Survivor's DECOY, this makes the decoy POP like the
-- Survivor pressed X, and stops the owner from being converted into a Hunter.
--
-- TOGGLE:  Shift+F7 in the LOBBY (after Create Server, before Start Game).
--   * Only works in the lobby - ignored in the main menu and during a match.
--   * Locks the moment the countdown starts; unlocks back in the lobby.
--   * The "Decoy Protection: ON/OFF" text is shown ONLY in the lobby and is
--     click-through (it never blocks the menu). Plus a click sound.
--   * Default = ON.
--==============================================================================

local MOD_ENABLED  = true
local MATCH_LOCKED = false                  -- true once the countdown starts
local TOGGLE_KEY   = Key.F7
local TOGGLE_MODS  = { ModifierKey.SHIFT }  -- => Shift+F7
local DEBUG        = false

local TAG = "[NDD] "
local function log(s) print(TAG .. s .. "\n") end

local HUNTER   = "/Game/BluePrints/cLeon/BP_FirstPersonCharacter_cLeon_Character_Hunter.BP_FirstPersonCharacter_cLeon_Character_Hunter_C"
local GAMEMODE = "/Game/BluePrints/cLeon/BP_GameMode_cLeon.BP_GameMode_cLeon_C"
local CLICK_CUE = "/Game/audios/CHAPTER2/SE/CopyActorDelete_Cue.CopyActorDelete_Cue"

local UEHelpers = require("UEHelpers")

local function unwrap(p) local v=p; pcall(function() v=p:get() end); return v end
local function fullname(o) local s="<nil>"; pcall(function() if o and o.IsValid and o:IsValid() and o.GetFullName then s=o:GetFullName() end end); return s end
local function shortname(o) local s=fullname(o); return s:match("([^%.]+)$") or s end
local function readVec(v) local x,y,z; local ok=pcall(function() x=v.X; y=v.Y; z=v.Z end); if ok and x~=nil then return x,y,z end end
local function actorLoc(a) local x,y,z; pcall(function() local v=a:K2_GetActorLocation(); x=v.X; y=v.Y; z=v.Z end); return x,y,z end
local function d3(ax,ay,az,bx,by,bz) local dx,dy,dz=ax-bx,ay-by,az-bz; return math.sqrt(dx*dx+dy*dy+dz*dz) end
local function decoyOwner(dd) local o; pcall(function() o = dd.RuntimePaintCopy.ReplicatedSourceActor end); return o end

-- =============================== the overlay ==================================
-- A tiny text overlay. Key point: SetVisibility(3) = HitTestInvisible so it is
-- drawn but NEVER captures mouse clicks (that was the "can't click" bug).
local VIS_HITINVIS, VIS_COLLAPSED = 3, 1
local _ov, _ovtb
local function ensureOverlay()
    -- reuse only if it's still ATTACHED to the current viewport. After you leave
    -- a lobby, the world reloads and the widget is dropped from the viewport even
    -- though the object stays valid -> we must rebuild it for the new lobby.
    local ready = false
    pcall(function() ready = _ov and _ov:IsValid() and _ov:IsInViewport() end)
    if ready then return true end
    pcall(function() if _ov and _ov:IsValid() then _ov:RemoveFromParent() end end)
    _ov, _ovtb = nil, nil
    local ok = pcall(function()
        local gi = UEHelpers.GetGameInstance(); assert(gi)
        _ov = StaticConstructObject(StaticFindObject("/Script/UMG.UserWidget"), gi)
        local wt = StaticConstructObject(StaticFindObject("/Script/UMG.WidgetTree"), _ov)
        _ov.WidgetTree = wt
        _ovtb = StaticConstructObject(StaticFindObject("/Script/UMG.TextBlock"), wt)
        wt.RootWidget = _ovtb
        _ov:AddToViewport(30000)
        _ov:SetVisibility(VIS_HITINVIS)          -- click-through
    end)
    return ok and _ov and _ov:IsValid()
end
local function stateMsg() return MOD_ENABLED and "Decoy Protection: ON" or "Decoy Protection: OFF" end
local function showState()
    if ensureOverlay() then
        pcall(function()
            local ktl = StaticFindObject("/Script/Engine.Default__KismetTextLibrary")
            _ovtb:SetText(ktl:Conv_StringToText(stateMsg()))
            _ov:SetVisibility(VIS_HITINVIS)
        end)
    end
end
local function hideOverlay() if _ov and _ov:IsValid() then pcall(function() _ov:SetVisibility(VIS_COLLAPSED) end) end end

local function playClick(isOn)
    pcall(function()
        local cue = StaticFindObject(CLICK_CUE); if not (cue and cue:IsValid()) then return end
        local gs  = StaticFindObject("/Script/Engine.Default__GameplayStatics")
        gs:PlaySound2D(UEHelpers.GetPlayerController(), cue, 1.0, isOn and 1.4 or 0.8, 0.0, nil, nil, true)
    end)
end

-- ============================ lobby detection =================================
-- We're "in the lobby" when a real cLeon session exists (host has created the
-- server) AND the match hasn't started. Nothing exists in the main menu.
local function inLobby()
    if MATCH_LOCKED then return false end
    local found = false
    for _,cls in ipairs({ "BP_GameState_cLeon_C", "BP_GameMode_cLeon_C" }) do
        pcall(function()
            for _,o in ipairs(FindAllOf(cls) or {}) do
                local n = ""; pcall(function() n = o:GetFullName() end)
                if o and n ~= "" and not n:find("Default__") then found = true end
            end
        end)
        if found then break end
    end
    return found
end

local function setEnabled(v)
    MOD_ENABLED = v and true or false
    log(stateMsg())
    showState()
    playClick(MOD_ENABLED)
end

local function onToggle()
    if not inLobby() then
        log(MATCH_LOCKED and "toggle ignored - match in progress" or "toggle ignored - only works in the lobby")
        return
    end
    setEnabled(not MOD_ENABLED)
end

-- =============================== the fix ======================================
local function onAntiChatTrace(self, End, Target)
    if not MOD_ENABLED then return end
    pcall(function()
        local ex,ey,ez = readVec(unwrap(End)); if ex == nil then return end
        local tgt = unwrap(Target); local tname = fullname(tgt)
        local tlx,tly,tlz = actorLoc(tgt)
        local bodyDist = (tlx and d3(ex,ey,ez, tlx,tly,tlz)) or 1e9
        local decoys = {}; pcall(function() decoys = FindAllOf("BP_cLeonDecoy_Base_C") or {} end)
        local nearest, nearestDecoy = 1e9, nil
        for _,dd in ipairs(decoys) do
            if fullname(decoyOwner(dd)) == tname then
                local dx,dy,dz = actorLoc(dd)
                if dx then local q = d3(ex,ey,ez, dx,dy,dz); if q < nearest then nearest = q; nearestDecoy = dd end end
            end
        end
        if not (nearestDecoy and nearest < bodyDist and nearest <= 600) then return end
        log(string.format("DECOY-shot on %s -> pop + protect", shortname(tgt)))
        pcall(function() nearestDecoy:K2_DestroyActor() end)
        pcall(function() tgt:DestroyDecoy() end)
        pcall(function() End.X = ex + 1000000.0; End.Y = ey + 1000000.0; End.Z = ez + 1000000.0 end)
    end)
end

local function onLock()   if not MATCH_LOCKED then MATCH_LOCKED = true;  hideOverlay(); log("match started -> toggle locked") end end
local function onUnlock() if MATCH_LOCKED     then MATCH_LOCKED = false; log("back in lobby -> toggle unlocked") end end
local function onSurvivorToHunter(self, Ctrl) if DEBUG then pcall(function() log("SurvivorToHunter " .. shortname(unwrap(Ctrl))) end) end end

-- =============================== wiring =======================================
local hooks = {
    { HUNTER   .. ":AntiChatTrace",    onAntiChatTrace },
    { GAMEMODE .. ":CountDownStart",   onLock },
    { GAMEMODE .. ":GameStart",        onLock },
    { GAMEMODE .. ":GameEnd",          onUnlock },
    { GAMEMODE .. ":TeleportLobby",    onUnlock },
    { GAMEMODE .. ":SurvivorToHunter", onSurvivorToHunter },
}
local done = {}
local function tryHooks()
    for _,h in ipairs(hooks) do
        if not done[h[1]] then
            local ok, id = pcall(RegisterHook, h[1], h[2])
            if ok and id then done[h[1]] = true; if DEBUG then log("hooked " .. h[1]) end end
        end
    end
end

local okKB = pcall(function() RegisterKeyBind(TOGGLE_KEY, TOGGLE_MODS, onToggle) end)
if not okKB then log("WARN: could not register the Shift+F7 keybind") end

log("loaded. Shift+F7 in the lobby to toggle; hidden in menu/match.")
LoopAsync(1000, function()
    tryHooks()
    pcall(function() if inLobby() then showState() else hideOverlay() end end)   -- overlay only in the lobby, click-through
    return false
end)
