// ============================================================================
//  Custom Enemy - Tactical actor (the monster you fight on the battle map)
// ----------------------------------------------------------------------------
//  A "beast"-style monster: inherits the vanilla actor base, has its own body
//  sprite (no equipment/appearance layering), and attacks with a natural attack
//  (Fists / hand_to_hand) so it works with ZERO custom art for now.
//
//  SPRITES: the body/head brushes come from ::CustomEnemy (set in the preload).
//  Until you drop in your own compiled .brush, they default to the vanilla bear
//  so the monster renders and is fully fightable. To use your art, compile your
//  PNG into a .brush (BBEdit -> data/BB-Edit-standalone-*.exe), ship it under
//  brushes/ + gfx/, and point ::CustomEnemy.BodyBrush / .HeadBrush at it.
//
//  STATS come from ::Const.Tactical.Actor.CustomEnemy (defined in the preload).
//  SKILLS are granted in onInit() using stock vanilla skill scripts.
//
//  Structure mirrors the readable Legends "legend_warbear" so it stays close to
//  a known-good monster definition.
// ============================================================================
this.custom_enemy <- this.inherit("scripts/entity/tactical/actor", {
	m = {},

	function setName( _n ) { this.m.Name = _n; }
	function getName()     { return this.m.Name; }

	function create()
	{
		this.m.Name      = ::CustomEnemy.EnemyName;
		this.m.Type      = ::Const.EntityType.CustomEnemy;
		this.m.XP        = ::Const.Tactical.Actor.CustomEnemy.XP;
		this.m.BloodType = ::Const.BloodType.Red;
		this.actor.create();

		this.m.BloodSplatterOffset    = this.createVec(0, 0);
		this.m.DecapitateSplatterOffset = this.createVec(-4, -25);

		// --- Sounds (placeholder = vanilla bear sounds; safe if a file is missing). ---
		this.m.Sound[this.Const.Sound.ActorEvent.Death]          = [ "sounds/enemies/bear_dead.wav" ];
		this.m.Sound[this.Const.Sound.ActorEvent.DamageReceived] = [ "sounds/enemies/bear_hit1.wav", "sounds/enemies/bear_hit2.wav" ];
		this.m.Sound[this.Const.Sound.ActorEvent.Idle]           = [ "sounds/enemies/bear_idle1.wav", "sounds/enemies/bear_idle2.wav" ];
		this.m.Sound[this.Const.Sound.ActorEvent.Move]           = this.m.Sound[this.Const.Sound.ActorEvent.Idle];
		this.m.SoundVolume[this.Const.Sound.ActorEvent.Death]    = 0.7;

		// Melee AI brain (stock). Swap to another agent under scripts/ai/tactical/agents/ if desired.
		this.m.AIAgent = this.new("scripts/ai/tactical/agents/bandit_melee_agent");
		this.m.AIAgent.setActor(this);
	}

	function onInit()
	{
		this.actor.onInit();

		// ---- Base stats (edit the block in the preload to tune) ----
		local b = this.m.BaseProperties;
		b.setValues(::Const.Tactical.Actor.CustomEnemy);
		this.m.ActionPoints     = b.ActionPoints;
		this.m.Hitpoints        = b.Hitpoints;
		this.m.ActionPointCosts = this.Const.DefaultMovementAPCost;
		this.m.FatigueCosts     = this.Const.DefaultMovementFatigueCost;

		// ---- Sprites ----
		this.m.Items.getAppearance().Body = ::CustomEnemy.BodyBrush;
		local socket = this.addSprite("socket");
		if (this.doesBrushExist(::CustomEnemy.TacticalBase))
			socket.setBrush(::CustomEnemy.TacticalBase);

		local body = this.addSprite("body");
		body.setBrush(::CustomEnemy.BodyBrush);
		body.setHorizontalFlipping(this.isAlliedWithPlayer());

		local head = this.addSprite("head");
		head.setBrush(::CustomEnemy.HeadBrush);
		head.Color      = body.Color;
		head.Saturation = body.Saturation;
		head.setHorizontalFlipping(this.isAlliedWithPlayer());

		// Optional injury overlay, only wired up if a matching "<body>_injured" brush exists.
		local injury = this.addSprite("injury");
		injury.Visible = false;
		if (this.doesBrushExist(::CustomEnemy.BodyBrush + "_injured"))
			injury.setBrush(::CustomEnemy.BodyBrush + "_injured");

		this.addDefaultStatusSprites();

		// ---- Skills (stock vanilla scripts -> no Legends dependency) ----
		// hand_to_hand = a basic natural melee attack so the monster can fight unarmed.
		this.m.Skills.add(this.new("scripts/skills/actives/hand_to_hand"));
		// fearless_trait keeps a lone monster from instantly routing.
		this.m.Skills.add(this.new("scripts/skills/traits/fearless_trait"));
		// To give it a real weapon instead, equip one, e.g.:
		//   this.m.Items.equip(this.new("scripts/items/weapons/arming_sword"));

		this.m.CurrentProperties = clone b;
	}

	function onDeath( _killer, _skill, _tile, _fatalityType )
	{
		if (_tile != null)
		{
			local flip = this.Math.rand(0, 100) < 50;
			this.m.IsCorpseFlipped = flip;

			// Spawn a death decal only if the art provides a "<body>_dead" brush;
			// this keeps onDeath crash-proof for custom sprites that don't ship one.
			local bodyName = this.getSprite("body").getBrush().Name;
			if (this.doesBrushExist(bodyName + "_dead"))
			{
				local decal = _tile.spawnDetail(bodyName + "_dead", this.Const.Tactical.DetailFlag.Corpse, flip);
				decal.setBrightness(0.9);
				decal.Scale = 0.95;
			}

			this.spawnTerrainDropdownEffect(_tile);

			local corpse = clone this.Const.Corpse;
			corpse.CorpseName     = this.getName();
			corpse.IsHeadAttached = _fatalityType != this.Const.FatalityType.Decapitated;
			corpse.IsResurrectable = false;
			_tile.Properties.set("Corpse", corpse);
			this.Tactical.Entities.addCorpse(_tile);
		}

		this.actor.onDeath(_killer, _skill, _tile, _fatalityType);
	}

	function onFactionChanged()
	{
		this.actor.onFactionChanged();

		local flip = this.isAlliedWithPlayer();
		this.getSprite("body").setHorizontalFlipping(flip);
		this.getSprite("head").setHorizontalFlipping(flip);

		// Recolor the ground "socket" to the controlling faction's base, when valid.
		if (!this.Tactical.State.isScenarioMode())
		{
			local f = this.World.FactionManager.getFaction(this.getFaction());
			if (f != null && this.doesBrushExist(f.getTacticalBase()))
				this.getSprite("socket").setBrush(f.getTacticalBase());
		}
		else
		{
			local base = this.Const.FactionBase[this.getFaction()];
			if (this.doesBrushExist(base))
				this.getSprite("socket").setBrush(base);
		}
	}

});
