// =============================================================================
//  Custom Events framework - generic data-driven event class
// =============================================================================
//  One instance of this class is created for every definition registered with
//  ::CustomEvents.register(...) (see scripts/custom_events/definitions.nut).
//  It inherits the vanilla event base, so once configured it plugs straight
//  into the world event pool exactly like a hand-written event would.
//
//  The framework brain lives in scripts/!mods_preload/mod_custom_events.nut;
//  this file only adapts a data definition onto the vanilla event lifecycle.
// =============================================================================
this.custom_event <- this.inherit("scripts/events/event", {
	m = {
		Def = null,        // the data definition table this instance represents
		Subject = null,    // a brother chosen at fire time, stable for one firing
		Settlement = null  // the settlement just entered (settlement-trigger events)
	},

	// create() runs at instantiation, before we know which definition we are.
	// Keep it minimal - configure() does the real work right afterwards.
	function create()
	{
		this.m.ID = "event.custom_events.unconfigured";
		this.m.Title = "Custom Event";
		this.m.Cooldown = 0.0;
	}

	// Called by the framework immediately after this.new(...) with the data def.
	function configure( _def )
	{
		this.m.Def = _def;
		this.m.ID = _def.id;
		this.m.Title = ("title" in _def) ? _def.title : "An Event Occurs...";

		if ("cooldown" in _def && _def.cooldown != null)
			this.m.Cooldown = _def.cooldown * this.World.getTime().SecondsPerDay;

		this.buildScreens();
		return this;
	}

	// Translate the definition's screen data into vanilla screen tables.
	function buildScreens()
	{
		this.m.Screens = [];
		local def = this.m.Def;
		if (def == null || !("screens" in def))
			return;

		local hasEntry = false;
		foreach( sdef in def.screens )
		{
			local screen = ::CustomEvents.buildScreen(sdef);
			if (screen.ID == "A")
				hasEntry = true;
			this.m.Screens.push(screen);
		}

		// The vanilla event base opens the screen with id "A" first. Warn loudly
		// if an author forgot it, rather than failing silently at fire time.
		if (!hasEntry && this.m.Screens.len() > 0)
			::logWarning("CustomEvents: event '" + this.m.ID + "' has no entry screen with id \"A\" - it may not display correctly.");
	}

	// Per-firing context: pick a random brother and capture the current settlement.
	// Runs inside update(), which the engine calls before an event can be shown.
	function prepareContext()
	{
		local roster = this.World.getPlayerRoster().getAll();
		this.m.Subject = roster.len() > 0 ? roster[this.Math.rand(0, roster.len() - 1)] : null;

		if (this.m.Settlement == null)
			this.m.Settlement = ::CustomEvents.LastSettlement;
	}

	function onUpdateScore()
	{
		this.m.Score = 0;
		local def = this.m.Def;
		if (def == null)
			return;

		this.prepareContext();

		// Only "random" events join the random world-travel lottery. Settlement and
		// on-demand events keep a score of 0 (never auto-selected) but stay fireable
		// by id through ::CustomEvents.fireEventByID.
		local trigger = ("trigger" in def) ? def.trigger : "random";
		if (trigger != "random")
			return;

		if (!::CustomEvents.checkCondition(this, def))
			return;

		this.m.Score = ("weight" in def) ? def.weight : 10;
	}

	function onPrepareVariables( _vars )
	{
		// The base already provides %randombrother%, %companyname%, %randomname%,
		// %randomtown%, terrain images, etc. We add a stable %subject% (its pronouns
		// like %they_subject% are auto-added by the base because m.Subject is an
		// actor) and %settlement% for settlement-trigger events.
		if (this.m.Subject != null)
			_vars.push(["subject", this.m.Subject.getName()]);

		if (this.m.Settlement != null)
			_vars.push(["settlement", this.m.Settlement.getName()]);

		// Author-declared literal variables, e.g. variables = { merchant = "Aldo" }.
		if ("variables" in this.m.Def && this.m.Def.variables != null)
		{
			foreach( key, value in this.m.Def.variables )
				_vars.push([key, value]);
		}
	}

	function onClear()
	{
		this.m.Subject = null;
		this.m.Settlement = null;
	}

});
