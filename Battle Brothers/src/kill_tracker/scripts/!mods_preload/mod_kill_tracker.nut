// Kill Tracker - records, per company brother, how many of every enemy TYPE they
// have personally killed (the type id comes from getType(), so modded enemies that
// register their own ::Const.EntityType / ::Const.Strings.EntityName are tracked too).
//
// Data lives in each brother's auto-serialized Flags container, keyed by type id, so
// it persists across saves with no custom serialization and old saves load cleanly.
//
// NOTE: everything is registered directly here rather than via ::IO.enumerateFiles +
// ::include of a mod folder — that enumerate step silently found no files in this
// setup, so the hooks never registered. Inlining guarantees they do.

::KillTracker <- {
	ID = "mod_kill_tracker",
	Name = "Kill Tracker",
	Version = "1.0.0",
	FlagPrefix = "mod_kt.k."   // per-type kill-count flag key prefix, e.g. "mod_kt.k.42"
};

::KillTracker.MH <- ::Hooks.register(::KillTracker.ID, ::KillTracker.Version, ::KillTracker.Name);

// Add one kill of entity-type id <_typeId> to brother <_actor>'s persistent record.
::KillTracker.recordKill <- function( _actor, _typeId )
{
	local f = _actor.getFlags();
	local key = ::KillTracker.FlagPrefix + _typeId;
	local cur = f.has(key) ? f.get(key) : 0;
	f.set(key, cur + 1);
}

// Returns { Total = <int>, List = [ { Type, Name, Count }, ... ] } sorted by Count desc.
::KillTracker.getBreakdown <- function( _actor )
{
	local f = _actor.getFlags();
	local names = ::Const.Strings.EntityName;
	local out = [];
	local total = 0;

	for (local i = 0; i < names.len(); i++)
	{
		local key = ::KillTracker.FlagPrefix + i;
		if (!f.has(key))
			continue;

		local c = f.get(key);
		if (c <= 0)
			continue;

		out.push({ Type = i, Name = names[i], Count = c });
		total += c;
	}

	out.sort(@(_a, _b) _b.Count - _a.Count);
	return { Total = total, List = out };
}

::KillTracker.MH.queue(function() {
	// --- record the kill: stats_collector.onTargetKilled fires on the killer ---
	::KillTracker.MH.hook("scripts/skills/special/stats_collector", function(q) {
		q.onTargetKilled = @(__original) function( _targetEntity, _skill )
		{
			__original(_targetEntity, _skill);

			if (_targetEntity == null)
				return;

			local actor = this.getContainer().getActor();
			if (actor == null || !actor.isPlayerControlled())
				return;

			if (_targetEntity.getXPValue() <= 0)
				return;

			::KillTracker.recordKill(actor, _targetEntity.getType());
		}
	});

	// --- tooltip: append the per-type breakdown to the brother's roster tooltip ---
	::KillTracker.MH.hook("scripts/entity/tactical/player", function(q) {
		q.getRosterTooltip = @(__original) function()
		{
			local ret = __original();

			local data = ::KillTracker.getBreakdown(this);
			if (data.Total > 0)
			{
				local text = "[b]Kill Record[/b] (" + data.Total + " total)";
				foreach (entry in data.List)
					text += "\n" + entry.Name + ": " + entry.Count;

				ret.push({ id = 1337, type = "description", text = text });
			}

			return ret;
		}
	});

	// --- panel backend: JS calls this synchronously with the selected brother's id ---
	::KillTracker.MH.hook("scripts/ui/screens/character/character_screen", function(q) {
		q.killtracker_queryRecord <- function( _entityID )
		{
			local bro = null;
			foreach (b in ::World.getPlayerRoster().getAll())
			{
				if (b.getID() == _entityID)
				{
					bro = b;
					break;
				}
			}

			if (bro == null)
				return { total = 0, rows = [] };

			local data = ::KillTracker.getBreakdown(bro);
			local rows = [];
			foreach (e in data.List)
				rows.push({ name = e.Name, count = e.Count });

			return { total = data.Total, rows = rows };
		}
	});

	// --- load the panel's UI files (paths relative to ui/mods/) ---
	::mods_registerJS("killtracker/kill_tracker_panel.js");
	::mods_registerCSS("killtracker/kill_tracker_panel.css");

	::logInfo("KillTracker: hooks registered and UI files queued");
});
