// =============================================================================
//  Custom Events  -  YOUR EVENTS GO HERE
// =============================================================================
//  Add events by calling ::CustomEvents.register({ ... }) with a data table.
//  Edit/extend the examples below, then rebuild the mod zip (see build_mod.ps1).
//
//  ---------------------------------------------------------------------------
//  DEFINITION FORMAT
//  ---------------------------------------------------------------------------
//  {
//    id        = "event.my_mod.something"   // REQUIRED, unique. Convention: event.*
//    title     = "On the road..."           // shown as the event window title
//    trigger   = "random"                    // "random" | "settlement" | "onDemand"
//    weight    = 12                          // random pool weight (higher = likelier)
//    cooldown  = 15                          // days before it can fire again (optional)
//    menuLabel = "Inspect the troops"        // label in the on-demand menu (optional)
//    condition = { minDay = 5, minMoney = 0 }// optional gate, see CONDITIONS below
//    variables = { merchant = "Old Aldo" }   // optional literal %placeholders%
//    screens   = [ <screen>, <screen>, ... ] // REQUIRED. First/entry MUST have id "A"
//  }
//
//  SCREEN:
//  {
//    id      = "A"                            // entry screen MUST be "A"
//    image   = "gfx/ui/events/event_16.png"  // optional banner image
//    text    = "Some flavour text. %subject% steps forward."
//    effects = [ <effect>, ... ]             // applied when this screen is shown
//    options = [ <option>, ... ]             // player choices (omit on outcome screens)
//  }
//
//  OPTION:
//  {
//    text     = "Pay them off"
//    goto     = "Paid"      // screen id to show next; omit/0 to close the event
//    chance   = 60          // optional %: goto on success, gotoElse on failure
//    gotoElse = "Failed"
//    action   = function(_event) { ... }  // optional code run on selection
//  }
//
//  EFFECT (applied on the screen it sits on; each adds a row to the outcome list):
//    { type = "money",   value = -100 }                         // crowns (range ok: [50,150])
//    { type = "renown",  value = 25 }                           // business reputation
//    { type = "moral",   value = -1 }                           // moral reputation
//    { type = "hp",      target = "subject", value = 5 }        // permanent stat (see below)
//    { type = "resolve" | "fatigue" | "meleeskill" | "rangedskill"
//           | "meleedefense" | "rangeddefense" | "initiative", target=.., value=.. }
//    { type = "xp",      target = "all", value = 100 }          // experience + level-up
//    { type = "injury",  target = "subject", severity = "light" } // or "heavy"
//    { type = "mood",    target = "all", value = 0.5, note = ".." } // needs Legends/MSU
//    { type = "item",    id = "scripts/items/supplies/roborant_potion_item", count = 1 }
//    { type = "flag",    key = "mymod.metStranger", value = true } // persistent state
//    { type = "custom",  func = function(_event,_screen){ return {id=10,icon="..",text=".."}; } }
//
//    target: "subject"/"random" (the event's chosen brother), "all", "player", or an index.
//    value:  an integer, or a [min,max] range rolled each time.
//    note:   overrides the auto-generated outcome text for that row.
//
//  CONDITIONS (declarative table; ALL listed keys must pass):
//    minDay, maxDay, minMoney, maxMoney, minBrothers, maxBrothers,
//    hasFlag = "some.flag", notFlag = "some.flag"
//  ...or a predicate for full control:  condition = function(_event) { return <bool>; }
//
//  TEXT PLACEHOLDERS (a few of the built-ins; %subject% is this framework's):
//    %subject% (+ %they_subject%/%their_subject%/...), %settlement%, %companyname%,
//    %randombrother%, %randomname%, %randomtown%, %SPEECH_ON% .. %SPEECH_OFF%
//  Use {a | b | c} in text to pick one variant at random.
// =============================================================================


// -----------------------------------------------------------------------------
//  EXAMPLE 1 - "random" travel event with chance-based branching
//  A peddler on the road. Demonstrates: money, item, moral, injury, %subject%,
//  probabilistic options (chance/gotoElse) and a roster-size condition.
// -----------------------------------------------------------------------------
::CustomEvents.register({
	id = "event.custom_events.example_peddler",
	title = "A Peddler on the Road",
	trigger = "random",
	weight = 12,
	cooldown = 12,
	condition = { minBrothers = 3 },
	screens = [
		{
			id = "A",
			image = "gfx/ui/events/event_16.png",
			text = "A weathered peddler waves you down from beside a creaking handcart.%SPEECH_ON%Fine sirs! A trinket, a tonic, a bargain? I've wares fit for sellswords of your standing.%SPEECH_OFF%%subject% eyes the cart with suspicion.",
			options = [
				{ text = "Buy a tonic. (-80 crowns)", goto = "Bought" },
				{ text = "Haggle him down.", goto = "Haggled", chance = 60, gotoElse = "HaggleFail" },
				{ text = "Rob the old fool.", goto = "Robbed", chance = 70, gotoElse = "RobbedFail" },
				{ text = "Wave him off." }
			]
		},
		{
			id = "Bought",
			text = "You hand over the coin and the peddler presses a small vial into your palm.",
			effects = [
				{ type = "money", value = -80 },
				{ type = "item", id = "scripts/items/supplies/roborant_potion_item", note = "You gain a tonic" }
			],
			options = [ { text = "Onward." } ]
		},
		{
			id = "Haggled",
			text = "%subject% drives a hard bargain, and the peddler relents with a sigh.",
			effects = [
				{ type = "money", value = -40 },
				{ type = "item", id = "scripts/items/supplies/roborant_potion_item", note = "You gain a tonic" }
			],
			options = [ { text = "A fair deal." } ]
		},
		{
			id = "HaggleFail",
			text = "The peddler only laughs and packs up his cart. No deal today.",
			options = [ { text = "Worth a try." } ]
		},
		{
			id = "Robbed",
			text = "You take what you want. The peddler flees, cursing your name to anyone who'll listen.",
			effects = [
				{ type = "money", value = [60, 140] },
				{ type = "moral", value = -1, note = "Word spreads of your cruelty" }
			],
			options = [ { text = "Spoils of the road." } ]
		},
		{
			id = "RobbedFail",
			text = "The 'old fool' was faster than he looked - a hidden blade opens %subject%'s arm before he bolts.",
			effects = [
				{ type = "injury", target = "subject", severity = "light", note = "%subject% is cut in the scuffle" }
			],
			options = [ { text = "Let him go." } ]
		}
	]
});


// -----------------------------------------------------------------------------
//  EXAMPLE 2 - "settlement" event, fires on entering a town
//  Demonstrates: %settlement%, renown, mood (all brothers), a flag, %companyname%.
// -----------------------------------------------------------------------------
::CustomEvents.register({
	id = "event.custom_events.example_tavern",
	title = "Tavern Rumours",
	trigger = "settlement",
	weight = 10,
	cooldown = 6,
	screens = [
		{
			id = "A",
			image = "gfx/ui/events/event_20.png",
			text = "The tavern in %settlement% is loud and warm. Talk turns to the %companyname%, and a few locals raise their cups your way.",
			options = [
				{ text = "Buy a round for the house. (-120 crowns)", goto = "Round" },
				{ text = "Sit and listen to the rumours.", goto = "Listen" },
				{ text = "Keep to your own table." }
			]
		},
		{
			id = "Round",
			text = "Crowns well spent - the cheer is genuine, and your name travels a little further tonight.",
			effects = [
				{ type = "money", value = -120 },
				{ type = "renown", value = 20 },
				{ type = "mood", target = "all", value = 0.3, note = "A night of drink and song" }
			],
			options = [ { text = "To the company!" } ]
		},
		{
			id = "Listen",
			text = "Between tall tales you catch something useful - a rumour worth remembering.",
			effects = [
				{ type = "flag", key = "custom_events.heardRumour", value = true, note = "You file the rumour away" }
			],
			options = [ { text = "Interesting..." } ]
		}
	]
});


// -----------------------------------------------------------------------------
//  EXAMPLE 3 - "onDemand" event, triggered by the player (Ctrl+Shift+E)
//  Demonstrates: menuLabel, permanent stat gains, fatigue cost, XP, mood, choices.
// -----------------------------------------------------------------------------
::CustomEvents.register({
	id = "event.custom_events.example_drill",
	title = "Drill the Company",
	trigger = "onDemand",
	menuLabel = "Drill the company",
	screens = [
		{
			id = "A",
			image = "gfx/ui/events/event_82.png",
			text = "You call a halt and put the men through their paces. How hard do you push them?",
			options = [
				{ text = "Hard sparring - blades and bruises.", goto = "Hard" },
				{ text = "Light footwork drills.", goto = "Light" },
				{ text = "Let them rest instead." }
			]
		},
		{
			id = "Hard",
			text = "%subject% takes the worst of it but comes away sharper for the punishment.",
			effects = [
				{ type = "meleeskill", target = "subject", value = 2 },
				{ type = "xp", target = "all", value = 50 },
				{ type = "mood", target = "subject", value = -0.2, note = "Pushed hard in the dirt" }
			],
			options = [ { text = "Steel is forged in fire." } ]
		},
		{
			id = "Light",
			text = "A gentler session. The men loosen up and morale lifts.",
			effects = [
				{ type = "initiative", target = "all", value = 1 },
				{ type = "mood", target = "all", value = 0.2, note = "An easy day's training" }
			],
			options = [ { text = "Good work, lads." } ]
		}
	]
});


// -----------------------------------------------------------------------------
//  EXAMPLE 4 - "random" + CONDITION (only when the company is wealthy)
//  Demonstrates: a money threshold condition, big risk/reward branching, and a
//  custom-code effect (sets a flag and writes its own outcome row).
// -----------------------------------------------------------------------------
::CustomEvents.register({
	id = "event.custom_events.example_investment",
	title = "A Stranger's Proposition",
	trigger = "random",
	weight = 8,
	cooldown = 30,
	condition = { minMoney = 5000 },
	screens = [
		{
			id = "A",
			image = "gfx/ui/events/event_76.png",
			text = "A well-dressed stranger finds you at camp, having clearly heard your coffers are heavy.%SPEECH_ON%A venture, captain - double your stake by the next moon. All I need is the seed.%SPEECH_OFF%",
			options = [
				{ text = "Invest 2000 crowns.", goto = "Win", chance = 55, gotoElse = "Lose" },
				{ text = "Not a chance." }
			]
		},
		{
			id = "Win",
			text = "True to his word, the stranger returns your stake and more. Honest profit, for once.",
			effects = [
				{ type = "money", value = -2000 },
				{ type = "money", value = 4000, note = "The venture pays out handsomely" },
				{ type = "custom", func = function( _event, _screen ) {
					::World.Flags.set("custom_events.trustedStranger", true);
					return { id = 10, icon = "ui/icons/special.png", text = "You've found a contact worth keeping" };
				} }
			],
			options = [ { text = "A pleasure doing business." } ]
		},
		{
			id = "Lose",
			text = "The stranger, and your 2000 crowns, are gone by morning. A hard lesson.",
			effects = [
				{ type = "money", value = -2000, note = "Swindled" }
			],
			options = [ { text = "Damn him." } ]
		}
	]
});
