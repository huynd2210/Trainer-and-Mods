// trainer.js - simple trainer mod for Golden Krone Hotel
// Adds debug/cheat hotkeys. Not part of the base game.
//
// Cheats are entries in the `cheats` array below, dispatched through a lookup
// table keyed by KeyboardEvent.key. Adding a new cheat = adding one entry;
// nothing else in this file changes.

(function(){
	"use strict";

	// --- cheat definitions -------------------------------------------------
	// key:     KeyboardEvent.key value that triggers it
	// label:   shown in the startup hint
	// inGame:  true = only runs while a game is actually being played
	// run:     does the thing; runs silently, nothing is written to the log
	var cheats = [
		{
			key: "F8",
			label: "Heal to full",
			inGame: true,
			run: function(){
				// gainHealth caps at maxHp, floats the green number over the
				// player, and clears Bleeding - same path potions use.
				// Already at full HP it is a no-op and stays silent.
				pc.gainHealth(pc.maxHp);
			}
		}
	];

	// --- dispatch table ----------------------------------------------------
	var byKey = {};
	cheats.forEach(function(cheat){
		byKey[cheat.key] = cheat;
	});

	function playerIsActive(){
		return typeof game !== "undefined" &&
			game.state === "running" &&
			window.pc &&
			pc.tile &&
			!pc.destroyed &&
			pc.hp > 0;
	}

	function typingInAField(target){
		if(!target) return false;
		var tag = (target.tagName || "").toLowerCase();
		return tag === "input" || tag === "textarea" || target.isContentEditable;
	}

	function onKeyDown(e){
		if(e.repeat) return;
		if(typingInAField(e.target)) return;

		var cheat = byKey[e.key];
		if(!cheat) return;

		if(cheat.inGame && !playerIsActive()){
			return;
		}

		cheat.run();

		// redraw map + HUD and resync the health-bar animation baseline
		if(typeof game !== "undefined" && game.draw && playerIsActive()){
			game.draw();
		}

		e.preventDefault();
		e.stopPropagation();
	}

	// Capture phase: the game binds keydown on <html> and calls
	// stopPropagation(), so a bubble-phase listener here would never fire.
	window.addEventListener("keydown", onKeyDown, true);

	console.log("[trainer] loaded - " + cheats.map(function(c){
		return c.key + ": " + c.label;
	}).join(", "));
})();
