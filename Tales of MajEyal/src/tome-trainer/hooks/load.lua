-- Register the trainer hotkeys before the game creates its key handler, so they are bound
-- and show up (rebindable) under Key Bindings > Trainer.
local KeyBind = require "engine.KeyBind"
local Trainer = require "mod.class.TrainerState"

Trainer.defineKeys(KeyBind)
