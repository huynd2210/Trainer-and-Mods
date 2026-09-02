// ============================================================================
//  Custom Enemy - Faction class
// ----------------------------------------------------------------------------
//  A brand new, permanently-hostile faction (modeled on Legends' dummy/free
//  company factions). Inherits the vanilla faction base, so it plugs straight
//  into FactionManager. The faction instance is created at world generation by
//  the hook in scripts/!mods_preload/mod_custom_enemy.nut.
//
//  "Always an enemy" is achieved exactly like the vanilla beast/undead-style
//  pattern: a deeply negative PlayerRelation that never decays and can never be
//  changed (the addPlayerRelation overrides are no-ops).
// ============================================================================
this.custom_enemy_faction <- this.inherit("scripts/factions/faction", {
	m = {},

	function create()
	{
		this.faction.create();

		this.m.Type            = ::Const.FactionType.CustomEnemy;
		this.m.Base            = "";                                  // no world HQ/town
		this.m.TacticalBase    = ::CustomEnemy.TacticalBase;         // ground "socket" under units
		this.m.PlayerRelation  = -200.0;                             // always hostile
		this.m.IsHidden        = true;                               // not shown in the diplomacy screen
		this.m.IsRelationDecaying = false;                           // never drifts back to neutral
	}

	// Lock the relation: nothing can make this faction friendly.
	function addPlayerRelation( _r, _reason = "" ) {}
	function addPlayerRelationEx( _r, _reason = "" ) {}

	function onSerialize( _out )
	{
		this.faction.onSerialize(_out);
	}

	function onDeserialize( _in )
	{
		this.faction.onDeserialize(_in);
	}

});
