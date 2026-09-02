// =============================================================================
//  Custom Events  -  a data-driven framework for authoring Battle Brothers events
// =============================================================================
//  Define events as plain data tables (see scripts/custom_events/definitions.nut)
//  and this mod turns each one into a real world event. Four trigger styles are
//  supported:
//      "random"     - fires during world-map travel like a vanilla event
//      "settlement" - fires when the company enters a town/city
//      "onDemand"   - fired by the player via a hotkey (Ctrl+Shift+E, needs MSU)
//      conditional  - any event may carry a `condition` that gates when it can fire
//
//  Everything is registered directly from this preload file (no ::IO.enumerateFiles
//  bootstrap, which silently finds nothing in some installs). The one extra script,
//  scripts/custom_events/custom_event.nut, is loaded on demand by this.new(...).
// =============================================================================

::CustomEvents <- {
	ID = "mod_custom_events",
	Name = "Custom Events",
	Version = "1.0.0",
	Definitions = [],      // every registered event definition (data tables)
	LastSettlement = null, // most recently entered settlement (for context)
	Mod = null             // MSU mod handle, created later if MSU is present
};

::CustomEvents.MH <- ::Hooks.register(::CustomEvents.ID, ::CustomEvents.Version, ::CustomEvents.Name);

// -----------------------------------------------------------------------------
//  Registration
// -----------------------------------------------------------------------------
::CustomEvents.register <- function( _def )
{
	if (!("id" in _def))
	{
		::logError("CustomEvents.register: a definition is missing its 'id' field - skipped.");
		return;
	}

	// Replace in place if the same id is registered twice (e.g. file reloaded).
	foreach( i, d in ::CustomEvents.Definitions )
	{
		if (d.id == _def.id)
		{
			::CustomEvents.Definitions[i] = _def;
			return;
		}
	}

	::CustomEvents.Definitions.push(_def);
};

// Internal hidden event used as the on-demand chooser menu.
::CustomEvents.registerChooser <- function()
{
	::CustomEvents.register({
		id = "event.custom_events._menu",
		title = "Custom Events",
		trigger = "onDemand",
		hidden = true,
		screens = [
			{
				id = "A",
				text = "[img]gfx/ui/events/event_76.png[/img]No custom events are available right now.",
				options = [ { text = "Close" } ]
			}
		]
	});
};

// -----------------------------------------------------------------------------
//  Small helpers
// -----------------------------------------------------------------------------
::CustomEvents.posColor <- function() { return ::Const.UI.Color.PositiveEventValue; }
::CustomEvents.negColor <- function() { return ::Const.UI.Color.NegativeEventValue; }

// A value may be a plain integer or a [min, max] range that is rolled each time.
::CustomEvents.rollValue <- function( _v )
{
	if (typeof _v == "array" && _v.len() == 2)
		return ::Math.rand(_v[0], _v[1]);
	return _v;
};

::CustomEvents.isPlayerChar <- function( _bro )
{
	if ("Legends" in getroottable() && "Trait" in ::Legends && "Player" in ::Legends.Trait)
		return _bro.getSkills().hasTrait(::Legends.Trait.Player);
	return false;
};

// Resolve an effect's `target` spec into an array of brother actors.
//   "subject"/"random" -> the event's chosen brother
//   "all"              -> the whole roster
//   "player"           -> the main character (falls back to first brother)
//   <integer>          -> roster index
::CustomEvents.resolveTargets <- function( _event, _spec )
{
	local roster = ::World.getPlayerRoster().getAll();
	if (_spec == null) _spec = "subject";

	if (typeof _spec == "integer")
		return (_spec >= 0 && _spec < roster.len()) ? [roster[_spec]] : [];

	switch (_spec)
	{
		case "all":
			return clone roster;

		case "player":
			foreach( b in roster )
				if (::CustomEvents.isPlayerChar(b))
					return [b];
			return roster.len() > 0 ? [roster[0]] : [];

		case "subject":
		case "random":
		default:
			if (_event.m.Subject != null) return [_event.m.Subject];
			return roster.len() > 0 ? [roster[::Math.rand(0, roster.len() - 1)]] : [];
	}
};

::CustomEvents.addPortrait <- function( _screen, _bro )
{
	if (_bro == null) return;
	local p = _bro.getImagePath();
	foreach( c in _screen.Characters )
		if (c == p) return;
	_screen.Characters.push(p);
};

::CustomEvents.statEntry <- function( _name, _value, _icon, _label )
{
	return {
		id = 17,
		icon = _icon,
		text = _value > 0
			? _name + " gains [color=" + ::CustomEvents.posColor() + "]+" + _value + "[/color] " + _label
			: _name + " loses [color=" + ::CustomEvents.negColor() + "]" + _value + "[/color] " + _label
	};
};

// Permanent base-stat effects: effect type -> property/icon/label.
::CustomEvents.StatSpec <- {
	hp            = { prop = "Hitpoints",     icon = "ui/icons/health.png",         label = "Hitpoints" },
	hitpoints     = { prop = "Hitpoints",     icon = "ui/icons/health.png",         label = "Hitpoints" },
	resolve       = { prop = "Bravery",       icon = "ui/icons/bravery.png",        label = "Resolve" },
	fatigue       = { prop = "Stamina",       icon = "ui/icons/fatigue.png",        label = "Max Fatigue" },
	meleeskill    = { prop = "MeleeSkill",    icon = "ui/icons/melee_skill.png",    label = "Melee Skill" },
	rangedskill   = { prop = "RangedSkill",   icon = "ui/icons/ranged_skill.png",   label = "Ranged Skill" },
	meleedefense  = { prop = "MeleeDefense",  icon = "ui/icons/melee_defense.png",  label = "Melee Defense" },
	rangeddefense = { prop = "RangedDefense", icon = "ui/icons/ranged_defense.png", label = "Ranged Defense" },
	initiative    = { prop = "Initiative",    icon = "ui/icons/initiative.png",     label = "Initiative" }
};

// -----------------------------------------------------------------------------
//  Effect engine - applies a screen's effects and returns display rows
// -----------------------------------------------------------------------------
::CustomEvents.applyEffect <- function( _event, _screen, _eff )
{
	local entries = [];
	local type = ("type" in _eff) ? _eff.type : "";
	local note = ("note" in _eff) ? _eff.note : null;
	local targetSpec = ("target" in _eff) ? _eff.target : "subject";

	switch (type)
	{
		case "money":
		{
			local amt = ::CustomEvents.rollValue(_eff.value);
			if (amt < 0) amt = -::Math.min(::Math.abs(amt), ::World.Assets.getMoney()); // never go negative
			if (amt != 0)
			{
				::World.Assets.addMoney(amt);
				entries.push({
					id = 10,
					icon = "ui/icons/asset_money.png",
					text = note != null ? note : (amt > 0
						? "You gain [color=" + ::CustomEvents.posColor() + "]" + amt + "[/color] Crowns"
						: "You lose [color=" + ::CustomEvents.negColor() + "]" + (-amt) + "[/color] Crowns")
				});
			}
			break;
		}

		case "renown":
		case "reputation":   // business reputation, a.k.a. renown
		{
			local amt = ::CustomEvents.rollValue(_eff.value);
			::World.Assets.addBusinessReputation(amt);
			entries.push({
				id = 10,
				icon = "ui/icons/special.png",
				text = note != null ? note : (amt > 0
					? "You gain [color=" + ::CustomEvents.posColor() + "]" + amt + "[/color] renown"
					: "You lose [color=" + ::CustomEvents.negColor() + "]" + (-amt) + "[/color] renown")
			});
			break;
		}

		case "moral":
		case "moral_reputation":
		{
			local amt = ::CustomEvents.rollValue(_eff.value);
			::World.Assets.addMoralReputation(amt);
			entries.push({
				id = 10,
				icon = "ui/icons/asset_moral_reputation.png",
				text = note != null ? note : "The company's moral standing " + (amt >= 0 ? "improves" : "worsens")
			});
			break;
		}

		case "hp":
		case "hitpoints":
		case "resolve":
		case "fatigue":
		case "meleeskill":
		case "rangedskill":
		case "meleedefense":
		case "rangeddefense":
		case "initiative":
		{
			local spec = ::CustomEvents.StatSpec[type];
			local amt = ::CustomEvents.rollValue(_eff.value);
			foreach( bro in ::CustomEvents.resolveTargets(_event, targetSpec) )
			{
				bro.getBaseProperties()[spec.prop] += amt;
				::CustomEvents.addPortrait(_screen, bro);
				entries.push(::CustomEvents.statEntry(bro.getName(), amt, spec.icon, spec.label));
			}
			break;
		}

		case "xp":
		case "experience":
		{
			local amt = ::CustomEvents.rollValue(_eff.value);
			foreach( bro in ::CustomEvents.resolveTargets(_event, targetSpec) )
			{
				bro.addXP(amt);
				if ("updateLevel" in bro) bro.updateLevel();
				::CustomEvents.addPortrait(_screen, bro);
				entries.push({
					id = 10,
					icon = "ui/icons/xp_received.png",
					text = bro.getName() + " gains [color=" + ::CustomEvents.posColor() + "]" + amt + "[/color] experience"
				});
			}
			break;
		}

		case "injury":
		{
			local severity = ("severity" in _eff) ? _eff.severity : "light";
			foreach( bro in ::CustomEvents.resolveTargets(_event, targetSpec) )
			{
				::CustomEvents.addPortrait(_screen, bro);
				if (severity == "heavy")
				{
					bro.addHeavyInjury();
					entries.push({ id = 10, icon = "ui/icons/days_wounded.png", text = note != null ? note : bro.getName() + " suffers heavy wounds" });
				}
				else
				{
					bro.addLightInjury();
					entries.push({ id = 10, icon = "ui/icons/days_wounded.png", text = note != null ? note : bro.getName() + " suffers light wounds" });
				}
			}
			break;
		}

		case "mood":
		{
			// Mood is provided by Legends/MSU; apply only when those methods exist.
			local amt = ::CustomEvents.rollValue(_eff.value);
			foreach( bro in ::CustomEvents.resolveTargets(_event, targetSpec) )
			{
				if (!("improveMood" in bro) || !("worsenMood" in bro)) continue;
				if (amt > 0) bro.improveMood(amt, note != null ? note : "A pleasant turn of events");
				else if (amt < 0) bro.worsenMood(-amt, note != null ? note : "An unpleasant turn of events");
				::CustomEvents.addPortrait(_screen, bro);
			}
			break;
		}

		case "item":
		{
			local count = ("count" in _eff) ? _eff.count : 1;
			local stash = ::World.Assets.getStash();
			stash.makeEmptySlots(count);
			local first = null;
			for( local i = 0; i < count; i = i + 1 )
			{
				local item = ::new(_eff.id);
				stash.add(item);
				if (first == null) first = item;
			}
			if (first != null)
				entries.push({
					id = 1,
					icon = "ui/items/" + first.getIcon(),
					text = note != null ? note : ("You gain " + (count > 1 ? count + "x " : "") + first.getName())
				});
			break;
		}

		case "flag":
		{
			::World.Flags.set(_eff.key, ("value" in _eff) ? _eff.value : true);
			if (note != null)
				entries.push({ id = 10, icon = "ui/icons/special.png", text = note });
			break;
		}

		case "custom":
		{
			// Escape hatch: run arbitrary code. Return a row (or array of rows) to
			// display, or nothing.  func = function(_event, _screen) { ... }
			if ("func" in _eff && _eff.func != null)
			{
				local res = _eff.func(_event, _screen);
				if (typeof res == "array") entries.extend(res);
				else if (typeof res == "table") entries.push(res);
			}
			break;
		}

		default:
			::logWarning("CustomEvents: unknown effect type '" + type + "' in event '" + _event.getID() + "'");
	}

	return entries;
};

// Apply every effect of a screen and push the resulting rows into its List.
::CustomEvents.applyEffects <- function( _event, _screen, _effects )
{
	if (_effects == null) return;
	foreach( eff in _effects )
	{
		foreach( row in ::CustomEvents.applyEffect(_event, _screen, eff) )
			if (row != null) _screen.List.push(row);
	}
};

// -----------------------------------------------------------------------------
//  Condition engine - decides whether an event is currently allowed to fire
// -----------------------------------------------------------------------------
::CustomEvents.checkCondition <- function( _event, _def )
{
	if (!("condition" in _def) || _def.condition == null)
		return true;

	local c = _def.condition;

	// Power users may supply a predicate: function(_event) { return <bool>; }
	if (typeof c == "function")
		return c(_event);

	// Otherwise it's a declarative table of thresholds.
	local time = ::World.getTime();
	if ("minDay" in c && time.Days < c.minDay) return false;
	if ("maxDay" in c && time.Days > c.maxDay) return false;

	local money = ::World.Assets.getMoney();
	if ("minMoney" in c && money < c.minMoney) return false;
	if ("maxMoney" in c && money > c.maxMoney) return false;

	local n = ::World.getPlayerRoster().getAll().len();
	if ("minBrothers" in c && n < c.minBrothers) return false;
	if ("maxBrothers" in c && n > c.maxBrothers) return false;

	if ("hasFlag" in c && !::World.Flags.has(c.hasFlag)) return false;
	if ("notFlag" in c && ::World.Flags.has(c.notFlag)) return false;

	return true;
};

// -----------------------------------------------------------------------------
//  Cooldowns (persisted in company flags so they survive saves)
// -----------------------------------------------------------------------------
::CustomEvents.markFired <- function( _id, _cooldownDays )
{
	if (_cooldownDays == null || _cooldownDays <= 0) return;
	::World.Flags.set("mod_ce.cd." + _id, ::Time.getVirtualTimeF() + _cooldownDays * ::World.getTime().SecondsPerDay);
};

::CustomEvents.isOnCooldown <- function( _id )
{
	local key = "mod_ce.cd." + _id;
	if (!::World.Flags.has(key)) return false;
	return ::Time.getVirtualTimeF() < ::World.Flags.get(key);
};

// -----------------------------------------------------------------------------
//  Screen construction (used by custom_event.buildScreens at world load)
// -----------------------------------------------------------------------------
// Captured-by-parameter closures avoid the classic loop-variable capture bug.
::CustomEvents.makeStart <- function( _effects )
{
	return function( _event )
	{
		// `this` is the active screen clone (owns List/Characters); _event the event.
		::CustomEvents.applyEffects(_event, this, _effects);
	};
};

::CustomEvents.makeOptionResult <- function( _goto, _chance, _gotoElse, _action )
{
	return function( _event )
	{
		// Use the global ::Math (not this.Math): when the engine calls an option's
		// getResult it binds `this` to the option table, whose delegate chain to the
		// root state is not guaranteed for a closure created outside an event class.
		if (_action != null)
			_action(_event);

		if (_chance != null)
			return (::Math.rand(1, 100) <= _chance) ? _goto : _gotoElse;

		return _goto;
	};
};

::CustomEvents.makeFireAction <- function( _targetID )
{
	return function( _event )
	{
		// Fire the chosen target after the chooser has closed (avoids ActiveEvent clash).
		::Time.scheduleEvent(::TimeUnit.Real, 200, function( _tag )
		{
			::CustomEvents.fireEventByID(_targetID);
		}, null);
	};
};

::CustomEvents.buildScreen <- function( _sdef )
{
	local text = ("text" in _sdef) ? _sdef.text : "";
	if ("image" in _sdef && _sdef.image != null && _sdef.image != "")
		text = "[img]" + _sdef.image + "[/img]" + text;

	local screen = {
		ID = _sdef.id,
		Text = text,
		Image = "",
		List = [],
		Characters = [],
		Options = [],
		start = ::CustomEvents.makeStart(("effects" in _sdef) ? _sdef.effects : null)
	};

	foreach( odef in (("options" in _sdef) ? _sdef.options : []) )
	{
		screen.Options.push({
			Text = odef.text,
			getResult = ::CustomEvents.makeOptionResult(
				("goto" in odef) ? odef.goto : 0,
				("chance" in odef) ? odef.chance : null,
				("gotoElse" in odef) ? odef.gotoElse : 0,
				("action" in odef) ? odef.action : null
			)
		});
	}

	return screen;
};

// -----------------------------------------------------------------------------
//  Firing
// -----------------------------------------------------------------------------
::CustomEvents.weightedPick <- function( _defs )
{
	local total = 0;
	foreach( d in _defs ) total += ("weight" in d) ? d.weight : 10;
	if (total <= 0) return _defs[::Math.rand(0, _defs.len() - 1)];

	local pick = ::Math.rand(1, total);
	foreach( d in _defs )
	{
		local w = ("weight" in d) ? d.weight : 10;
		if (pick <= w) return d;
		pick -= w;
	}
	return _defs[_defs.len() - 1];
};

// Force a specific event to show now, mirroring the vanilla event_manager's own
// select-and-show tail. Returns false if something is already on screen.
::CustomEvents.fireEventByID <- function( _id )
{
	if (!("World" in getroottable()) || ::World == null || ::World.Events == null) return false;

	local mgr = ::World.Events;
	if (mgr.m.ActiveEvent != null) return false;

	local e = mgr.getEvent(_id);
	if (e == null)
	{
		::logWarning("CustomEvents: cannot fire unknown event '" + _id + "'");
		return false;
	}

	e.clear();
	e.update();
	mgr.m.ActiveEvent = e;
	e.fire();

	if (!::World.State.showEventScreen(e))
	{
		// Could not display right now (e.g. a blocking screen is open). Never leave a
		// zero-score event parked as ActiveEvent - the vanilla manager only retries
		// non-zero-score events, so it would block every future event. Clear it.
		e.clear();
		mgr.m.ActiveEvent = null;
		mgr.m.IsEventShown = false;
		return false;
	}

	mgr.m.IsEventShown = true;
	if (e.m.Def != null && ("cooldown" in e.m.Def))
		::CustomEvents.markFired(_id, e.m.Def.cooldown);

	return true;
};

// -----------------------------------------------------------------------------
//  Trigger: settlement entry
// -----------------------------------------------------------------------------
::CustomEvents.onSettlementEntered <- function( _settlement )
{
	::CustomEvents.LastSettlement = _settlement;
	if (::World.Events.m.ActiveEvent != null) return;

	local cands = [];
	foreach( def in ::CustomEvents.Definitions )
	{
		if ("hidden" in def && def.hidden) continue;
		if ((("trigger" in def) ? def.trigger : "random") != "settlement") continue;
		if (::CustomEvents.isOnCooldown(def.id)) continue;

		local e = ::World.Events.getEvent(def.id);
		if (e == null) continue;
		e.m.Settlement = _settlement;
		e.prepareContext();
		if (!::CustomEvents.checkCondition(e, def)) continue;
		cands.push(def);
	}

	if (cands.len() == 0) return;

	local targetID = ::CustomEvents.weightedPick(cands).id;
	// Defer slightly so the settlement screen has finished opening before the event.
	::Time.scheduleEvent(::TimeUnit.Real, 350, function( _tag )
	{
		::CustomEvents.fireEventByID(targetID);
	}, null);
};

// -----------------------------------------------------------------------------
//  Trigger: on-demand menu (hotkey)
// -----------------------------------------------------------------------------
::CustomEvents.openMenu <- function()
{
	if (!("World" in getroottable()) || ::World == null || ::World.State == null) return;
	if (::World.Events == null || ::World.Events.m.ActiveEvent != null) return;

	local cands = [];
	foreach( def in ::CustomEvents.Definitions )
	{
		if ("hidden" in def && def.hidden) continue;
		if ((("trigger" in def) ? def.trigger : "random") != "onDemand") continue;

		local e = ::World.Events.getEvent(def.id);
		if (e == null) continue;
		e.prepareContext();
		if (!::CustomEvents.checkCondition(e, def)) continue;
		cands.push(def);
	}

	if (cands.len() == 0)
	{
		::logInfo("CustomEvents: no on-demand events are currently available");
		return;
	}

	if (cands.len() == 1)
	{
		::CustomEvents.fireEventByID(cands[0].id);
		return;
	}

	::CustomEvents.showChooser(cands);
};

// Rebuild the hidden chooser event from the available on-demand events and fire it.
::CustomEvents.showChooser <- function( _cands )
{
	local chooser = ::World.Events.getEvent("event.custom_events._menu");
	if (chooser == null)
	{
		::logWarning("CustomEvents: on-demand chooser event is missing");
		return;
	}

	local options = [];
	foreach( def in _cands )
	{
		local label = ("menuLabel" in def) ? def.menuLabel : (("title" in def) ? def.title : def.id);
		options.push({
			text = label,
			action = ::CustomEvents.makeFireAction(def.id)  // closes chooser, then fires
		});
	}
	options.push({ text = "Never mind." });

	chooser.m.Def = {
		id = "event.custom_events._menu",
		title = "Custom Events",
		trigger = "onDemand",
		hidden = true,
		screens = [
			{
				id = "A",
				text = "[img]gfx/ui/events/event_76.png[/img]You gather your thoughts. Which turn of events would you set in motion?",
				options = options
			}
		]
	};
	chooser.buildScreens();

	::CustomEvents.fireEventByID("event.custom_events._menu");
};

// -----------------------------------------------------------------------------
//  Wire everything up after all classes are loaded
// -----------------------------------------------------------------------------
::CustomEvents.MH.queue(function()
{
	// 1. Load author/example definitions (calls ::CustomEvents.register for each).
	//    ::include paths are relative to scripts/ and omit the .nut extension.
	try
	{
		::include("custom_events/definitions");
	}
	catch (err)
	{
		::logError("CustomEvents: failed to load definitions.nut: " + err);
	}

	// 2. Ensure the hidden on-demand chooser exists.
	::CustomEvents.registerChooser();

	// 3. Inject our definitions into the world event pool as real event instances.
	::CustomEvents.MH.hook("scripts/events/event_manager", function(q)
	{
		q.create = @(__original) function()
		{
			__original();   // builds all vanilla (and Legends) events first

			local count = 0;
			foreach( def in ::CustomEvents.Definitions )
			{
				local e = this.new("scripts/custom_events/custom_event");
				if (e == null)
				{
					::logError("CustomEvents: failed to instantiate event for '" + def.id + "'");
					continue;
				}
				e.configure(def);
				this.m.Events.push(e);
				count = count + 1;
			}
			::logInfo("CustomEvents: injected " + count + " custom event(s) into the world event pool");
		};
	});

	// 4. Settlement-entry trigger.
	::CustomEvents.MH.hook("scripts/entity/world/settlement", function(q)
	{
		q.onEnter = @(__original) function()
		{
			local ret = __original();
			if (ret)
				::CustomEvents.onSettlementEntered(this);   // `this` is the settlement
			return ret;
		};
	});

	// 5. On-demand hotkey (requires MSU; framework still works without it).
	if ("MSU" in getroottable())
	{
		try
		{
			::CustomEvents.Mod = ::MSU.Class.Mod(::CustomEvents.ID, ::CustomEvents.Version, ::CustomEvents.Name);
			::CustomEvents.Mod.Keybinds.addSQKeybind(
				"OpenCustomEventsMenu",
				"ctrl+shift+e",
				::MSU.Key.State.World,
				function() { ::CustomEvents.openMenu(); },
				"Open Custom Events Menu",
				null,
				"Manually trigger one of your on-demand custom events."
			);
			::logInfo("CustomEvents: on-demand hotkey registered (Ctrl+Shift+E)");
		}
		catch (err)
		{
			::logWarning("CustomEvents: failed to register MSU keybind: " + err);
		}
	}
	else
	{
		::logInfo("CustomEvents: MSU not found - on-demand hotkey disabled (random & settlement events still work)");
	}

	::logInfo("CustomEvents: framework ready (" + ::CustomEvents.Definitions.len() + " definitions registered)");
});
