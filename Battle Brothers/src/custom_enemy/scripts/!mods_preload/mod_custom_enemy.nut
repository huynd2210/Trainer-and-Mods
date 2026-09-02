// ============================================================================
//  CUSTOM ENEMY  -  adds a new monster, its own faction, and its own roaming
//                   parties to Battle Brothers.
// ----------------------------------------------------------------------------
//  Everything that has to be REGISTERED (entity type, faction, stat block,
//  spawn templates) and every HOOK lives here, inlined, so there are no
//  enumerate-the-folder surprises (see the killtracker notes). The actual
//  monster + faction CLASSES live in their own files and are loaded on demand:
//      scripts/entity/tactical/custom_enemy.nut
//      scripts/factions/custom_enemy_faction.nut
//
//  >>> EVERYTHING YOU'LL WANT TO CHANGE IS IN THE ::CustomEnemy BLOCK BELOW <<<
// ============================================================================

::CustomEnemy <- {
	ID      = "mod_custom_enemy",
	Name    = "Custom Enemy",
	Version = "0.1.0",

	// ===== IDENTITY (rename / retheme freely) ==============================
	EnemyName       = "Gloomspawn",     // singular, shown in tooltips / kill log
	EnemyNamePlural = "Gloomspawn",     // plural
	FactionName     = "The Gloom",      // faction display name

	// ===== SPRITES ========================================================
	//  Placeholders are stock vanilla brushes so the mod runs RIGHT NOW.
	//  When your art is ready: compile the PNG into a .brush (use
	//  data/BB-Edit-standalone-*.exe), ship brushes/<name>.brush + gfx/<name>.png
	//  in this zip, then point these at your brush names.
	BodyBrush    = "bear_01",          // tactical body sprite  <-- your monster art
	HeadBrush    = "bear_head_01",     // tactical head sprite  (or reuse BodyBrush)
	TacticalBase = "bust_base_beasts", // ground "socket" disc under the unit
	WorldFigure  = "figure_hexe_01",   // the party blob on the WORLD map
	PartyBanner  = "banner_beasts_01", // banner shown over the world party

	// ===== ROAMING SPAWN BEHAVIOUR ========================================
	MaxParties        = 2,     // how many of this faction's parties may roam at once
	SpawnCheckInterval = 5.0,  // virtual seconds between spawn attempts
	MinDistFromPlayer = 12,    // don't spawn a party closer than this to the player
	MinBudget         = 80,    // floor on troop-strength budget (>= a couple of units)

	// ===== runtime (do not edit) ==========================================
	TypeID = -1
};

::CustomEnemy.MH <- ::Hooks.register(::CustomEnemy.ID, ::CustomEnemy.Version, ::CustomEnemy.Name);

// Run AFTER Legends + MSU so their extended Const tables (EntityType.addNew,
// EntityFaction, faction arrays, World.Spawn, etc.) are fully in place.
::CustomEnemy.MH.queue(">mod_legends", ">mod_msu", function() {

	::logInfo("CustomEnemy: registering content...");

	// ---------------------------------------------------------------------
	// 1) FACTION constants  (a new FactionType + a new Faction instance slot)
	//    Replicates Legends' Factions.add() inline so it stays self-contained.
	// ---------------------------------------------------------------------
	if (!("CustomEnemy" in ::Const.FactionType))
		::Const.FactionType.CustomEnemy <- ::Const.FactionType.COUNT++;

	if (!("CustomEnemy" in ::Const.Faction))
	{
		local newID = ::Const.Faction.COUNT;
		while (::Const.FactionColor.len()    <= newID) ::Const.FactionColor.push(::createColor("#ffffff"));
		while (::Const.FactionBase.len()     <= newID) ::Const.FactionBase.push("");
		while (::Const.FactionAlliance.len() <= newID) ::Const.FactionAlliance.push([]);
		::Const.Faction.COUNT++;
		::Const.FactionBase[newID] = ::CustomEnemy.TacticalBase;
		::Const.FactionAlliance[newID].push(newID);   // self-ally => no infighting
		::Const.Faction.CustomEnemy <- newID;
	}

	// ---------------------------------------------------------------------
	// 2) ENTITY TYPE  (name / plural / icon / default faction).
	//    Uses Legends' addNew (keeps every parallel array in sync); falls back
	//    to the vanilla string arrays if Legends is somehow absent.
	// ---------------------------------------------------------------------
	if (!("CustomEnemy" in ::Const.EntityType))
	{
		if ("addNew" in ::Const.EntityType)
		{
			::Const.EntityType.CustomEnemy <- ::Const.EntityType.addNew(
				"bear_orientation",            // UI/world orientation icon (placeholder)
				::CustomEnemy.EnemyName,
				::CustomEnemy.EnemyNamePlural,
				::Const.FactionType.CustomEnemy);
		}
		else
		{
			local id = ::Const.Strings.EntityName.len();
			::Const.Strings.EntityName.push(::CustomEnemy.EnemyName);
			::Const.Strings.EntityNamePlural.push(::CustomEnemy.EnemyNamePlural);
			::Const.EntityType.CustomEnemy <- id;
		}
	}
	::CustomEnemy.TypeID = ::Const.EntityType.CustomEnemy;

	// ---------------------------------------------------------------------
	// 3) STAT BLOCK  (read by the actor's onInit via setValues). TUNE HERE.
	// ---------------------------------------------------------------------
	::Const.Tactical.Actor.CustomEnemy <- {
		XP                  = 100,
		ActionPoints        = 9,
		Hitpoints           = 90,
		Bravery             = 60,
		Stamina             = 130,
		MeleeSkill          = 60,
		RangedSkill         = 0,
		MeleeDefense        = 5,
		RangedDefense       = 0,
		Initiative          = 100,
		FatigueEffectMult   = 1.0,
		MoraleEffectMult    = 1.0,
		FatigueRecoveryRate = 15,
		Armor = [
			0,   // head armor
			0    // body armor
		]
	};

	// ---------------------------------------------------------------------
	// 4) TROOP DEFINITION  (ties an entity type + its tactical script into the
	//    spawn system; Cost is "how much budget one of them eats").
	// ---------------------------------------------------------------------
	::Const.World.Spawn.Troops.CustomEnemy <- {
		ID       = ::CustomEnemy.TypeID,
		Variant  = 0,
		Strength = 40,
		Cost     = 40,
		Row      = 1,
		Script   = "scripts/entity/tactical/custom_enemy"
	};

	// ---------------------------------------------------------------------
	// 5) PARTY TEMPLATE  (what a roaming world party of this faction contains;
	//    assignTroops fills it from Troops[] using the strength budget).
	// ---------------------------------------------------------------------
	::Const.World.Spawn.CustomEnemyParty <- {
		Name             = "CustomEnemyParty",
		IsDynamic        = true,
		MovementSpeedMult = 1.0,
		VisibilityMult   = 1.0,
		VisionMult       = 1.0,
		Body             = ::CustomEnemy.WorldFigure,
		MinR             = 40,
		Fixed            = [],
		Troops = [
			{
				Weight = 100,
				Types = [
					{ Type = ::Const.World.Spawn.Troops.CustomEnemy, Cost = 40 }
				]
			}
		]
	};

	// ---------------------------------------------------------------------
	// 6) FACTION INSTANCE  -  create it at world generation.
	//    (legacy hook: proven path for vanilla world classes.)
	// ---------------------------------------------------------------------
	::mods_hookNewObject("factions/faction_manager", function(o) {
		local createFactions = o.createFactions;
		o.createFactions = function() {
			createFactions();
			if (this.getFactionOfType(::Const.FactionType.CustomEnemy) == null)
			{
				local f = this.new("scripts/factions/custom_enemy_faction");
				f.setID(this.m.Factions.len());
				f.setName(::CustomEnemy.FactionName);
				f.setDiscovered(true);
				this.m.Factions.push(f);
				::logInfo("CustomEnemy: faction '" + ::CustomEnemy.FactionName + "' created (id " + f.getID() + ").");
			}
		}
	});

	// ---------------------------------------------------------------------
	// 7) ROAMING SPAWNER  -  periodically emit a party near a far-off town and
	//    give it a roam order (mirrors Legends' beast-roamer / free-company).
	// ---------------------------------------------------------------------
	::mods_hookExactClass("entity/world/entity_manager", function(o) {
		o.m.CustomEnemyParties   <- [];
		o.m.CustomEnemyLastSpawn <- 0.0;

		local update = o.update;
		o.update = function() {
			update();
			this.manageCustomEnemyParties();
		}

		local clear = o.clear;
		o.clear = function() {
			clear();
			this.m.CustomEnemyParties   = [];
			this.m.CustomEnemyLastSpawn = 0.0;
		}

		o.manageCustomEnemyParties <- function() {
			// drop dead/removed parties from the tracking list
			local garbage = [];
			foreach (i, p in this.m.CustomEnemyParties)
				if (p == null || p.isNull() || !p.isAlive())
					garbage.push(i);
			garbage.reverse();
			foreach (g in garbage)
				this.m.CustomEnemyParties.remove(g);

			// throttle
			if (this.m.CustomEnemyLastSpawn + ::CustomEnemy.SpawnCheckInterval > this.Time.getVirtualTimeF())
				return;
			this.m.CustomEnemyLastSpawn = this.Time.getVirtualTimeF();

			if (this.m.CustomEnemyParties.len() >= ::CustomEnemy.MaxParties)
				return;

			local faction = this.World.FactionManager.getFactionOfType(::Const.FactionType.CustomEnemy);
			if (faction == null)
				return;   // faction not present (e.g. a save started before this mod) -> do nothing

			// pick a connected settlement that's far enough from the player
			local playerTile = this.World.State.getPlayer().getTile();
			local candidates = [];
			foreach (s in this.World.EntityManager.getSettlements())
			{
				if (s.isIsolated())
					continue;
				if (s.getTile().getDistanceTo(playerTile) <= ::CustomEnemy.MinDistFromPlayer)
					continue;
				candidates.push(s);
			}
			if (candidates.len() == 0)
				return;

			local start = candidates[this.Math.rand(0, candidates.len() - 1)];

			// strength budget scales with the player's company
			local r = this.World.State.getPlayer().getStrength();
			r = this.Math.rand(this.Math.max(::CustomEnemy.MinBudget, (r * 0.7).tointeger()), (r * 1.2 + 50).tointeger());

			local party = faction.spawnEntity(start.getTile(), ::CustomEnemy.FactionName, false, ::Const.World.Spawn.CustomEnemyParty, r);
			party.setPos(this.createVec(party.getPos().X - 40, party.getPos().Y - 40));
			party.setDescription("A roaming band of " + ::CustomEnemy.EnemyNamePlural + ".");
			party.getSprite("banner").setBrush(::CustomEnemy.PartyBanner);
			party.setSlowerAtNight(false);
			if ("Ghouls" in this.Const.World.FootprintsType)
				party.setFootprintType(this.Const.World.FootprintsType.Ghouls);

			local roam = this.new("scripts/ai/world/orders/roam_order");
			roam.setAllTerrainAvailable();
			roam.setTerrain(this.Const.World.TerrainType.Ocean, false);
			roam.setTerrain(this.Const.World.TerrainType.Shore, false);
			party.getController().addOrder(roam);

			this.m.CustomEnemyParties.push(party);
			::logInfo("CustomEnemy: spawned a roaming party near " + start.getName() + " (budget " + r + ").");
		}
	});

	::logInfo("CustomEnemy: registration complete (type id " + ::CustomEnemy.TypeID + ").");
});
