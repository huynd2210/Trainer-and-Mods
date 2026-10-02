local _M = loadPrevious(...)
local Trainer = require "mod.class.TrainerState"

local base_setupCommands = _M.setupCommands
function _M:setupCommands()
	base_setupCommands(self)
	-- The key handler binds keys when it is created; rebind if it predates our definitions.
	if not self.key:findBoundKeys(Trainer.actions[1].type) then self.key:bindKeys() end
	self.key:addBinds(Trainer.binds(self))
end

return _M
