extends Node
## Best Hand: finds the highest-scoring set of cards to Make from the current hand.
##
## Every candidate is scored by the game's own ScoringCalculator on cloned cards, with the
## round state the real Make would see (hand consumed, played cards out of the hand) and the
## run's RNG rewound afterwards, so the number shown is what pressing Make would score.
## Only the scoring half of the calculation is run: the after-scoring hooks have real side
## effects (unlock discovery, card destruction) and do not change the hand's score.

const LOG := "BestHand"
const PANEL_SCENE := "res://features/custom_nodes/pixel_label_w_background.tscn"
const FRAME_BUDGET_USEC := 8000
const PERMUTE_TOP := 3
const RECHECK_MS := 200
const OVERLAY_Z := 29  # just under the scoring bubbles (Layers.CARD_BUBBLE = 30), far under tooltips

var _enabled := true
var _busy := false  # a Make or Scrap animation is running

var _hand: HandManager
var _processor: EventsProcessor
var _overlay: Control
var _summary: PixelLabelWBackground
var _badges: Array[PixelLabelWBackground] = []

var _fingerprint := 0
var _handIngs: Array[Ingredient] = []
var _queue: Array = []  # Array of Array[Ingredient] (play order)
var _queuePos := 0
var _phase := 0  # 0 idle, 1 subsets, 2 reorderings, 3 done
var _results: Array = []  # [{play, score}] from phase 1
var _best: Dictionary = {}
var _evalCount := 0
var _evalUsec := 0
var _errors := 0
var _computeStartMs := 0

var _selectedKey := "-"
var _nextCheckMs := 0
var _selectedScore: Variant = null

var _predicted: Variant = null
var _scoreBefore := 0.0
var _mirrorDummyRes: ToolRes


func _init() -> void:
	ModLoaderLog.info("Loaded.", LOG)


func _ready() -> void:
	_mirrorDummyRes = ToolRes.new()
	_mirrorDummyRes.id = "BEST_HAND_MIRROR_PLATE_STANDIN"


# ---------------------------------------------------------------- lifecycle

func _process(_delta: float) -> void:
	if not _roundReady():
		_teardown()
		return
	_ensureOverlay()
	_ensureSignals()
	if _busy or not _enabled:
		_overlay.visible = false
		return
	var now := Time.get_ticks_msec()
	if now >= _nextCheckMs:
		_nextCheckMs = now + RECHECK_MS
		var fp := _computeFingerprint()
		if fp != _fingerprint:
			_fingerprint = fp
			_restart()
	_work()
	_refreshSelectedScore()
	_updateOverlay()


func _roundReady() -> bool:
	if not RunData.isRunActive() or not RunData.isRoundActive():
		return false
	var rd := RunData.getRound()
	if rd.isTerminated or rd.getRemainingHands() <= 0:
		return false
	# Round already won: the win sequence is playing (Order Tablet keeps the round open on purpose).
	if rd.isTargetScoreReached() and not RunUtils.hasTool(IdDB.ToolID.ORDER_TABLET):
		return false
	var hm := rd.getHandManager()
	if hm == null or not is_instance_valid(hm) or not hm.is_inside_tree() or not hm.is_node_ready():
		return false
	if RunData.getMap() == null or RunData.getMap().getCurrentStop() == null:
		return false
	if SushiRand._runSeed == null:
		return false
	_hand = hm
	return true


func _teardown() -> void:
	if _overlay and is_instance_valid(_overlay):
		_overlay.queue_free()
	_overlay = null
	_summary = null
	_badges.clear()
	_hand = null
	_fingerprint = 0
	_phase = 0
	_queue.clear()
	_results.clear()
	_best = {}
	_selectedKey = "-"
	_selectedScore = null
	_busy = false


func _ensureSignals() -> void:
	var p := RunData.getEventsProcessor()
	if p != _processor and p != null and is_instance_valid(p):
		_processor = p
		p.onScoringStarted.connect(_onScoringStarted)
		p.onScoringFinished.connect(_onScoringFinished)
		p.onDiscardStarted.connect(_onDiscardStarted)
		p.onDiscardFinished.connect(_onDiscardFinished)


func _input(event: InputEvent) -> void:
	if not (event is InputEventKey) or not event.pressed or event.echo:
		return
	var key := event as InputEventKey
	if not key.ctrl_pressed or key.alt_pressed or key.shift_pressed:
		return
	if key.keycode == KEY_H:
		_enabled = not _enabled
		get_viewport().set_input_as_handled()
	elif key.keycode == KEY_B:
		if _enabled and not _busy and _phase >= 2 and _roundReady():
			_selectBest()
			get_viewport().set_input_as_handled()


# ---------------------------------------------------------------- search

func _restart() -> void:
	_handIngs.clear()
	for card: IngredientCardV2 in _hand.getAllCards():
		_handIngs.append(card.ingredient)
	_queue.clear()
	_queuePos = 0
	_results.clear()
	_best = {}
	_evalCount = 0
	_evalUsec = 0
	_errors = 0
	_selectedKey = "-"
	_computeStartMs = Time.get_ticks_msec()
	var maxPlay := mini(RunData.getMaxPlayableCards(), _handIngs.size())
	for size in range(1, maxPlay + 1):
		_combinations(size, 0, [], _queue)
	_phase = 1 if not _queue.is_empty() else 3


func _combinations(size: int, from: int, picked: Array, out: Array) -> void:
	if picked.size() == size:
		var play: Array[Ingredient] = []
		for idx: int in picked:
			play.append(_handIngs[idx])
		out.append(play)
		return
	for i in range(from, _handIngs.size() - (size - picked.size()) + 1):
		picked.append(i)
		_combinations(size, i + 1, picked, out)
		picked.pop_back()


func _work() -> void:
	if _phase == 0 or _phase == 3:
		return
	var start := Time.get_ticks_usec()
	while _queuePos < _queue.size() and Time.get_ticks_usec() - start < FRAME_BUDGET_USEC:
		var play: Array[Ingredient] = _queue[_queuePos]
		_queuePos += 1
		var t0 := Time.get_ticks_usec()
		var score: Variant = _evaluate(play)
		_evalUsec += Time.get_ticks_usec() - t0
		_evalCount += 1
		if score == null:
			_errors += 1
			continue
		if _phase == 1:
			_results.append({ "play": play, "score": score })
		if _best.is_empty() or float(score) > float(_best["score"]):
			_best = { "play": play, "score": score }
	if _queuePos < _queue.size():
		return
	if _phase == 1:
		_phase = 2
		_queue = _reorderCandidates()
		_queuePos = 0
		return
	_phase = 3
	ModLoaderLog.debug("Searched %d plays in %d ms (%.2f ms each, %d failed). Best %s." % [
		_evalCount, Time.get_ticks_msec() - _computeStartMs,
		(_evalUsec / 1000.0) / maxi(1, _evalCount), _errors,
		UtilsNumbers.formatNumber(float(_best.get("score", 0.0)), true)], LOG)


## Play order on the plate is the hand's left-to-right order, which the player can change by
## dragging. Order matters for some cards (neighbour and first-card effects), so the best few
## sets are also tried in every order.
func _reorderCandidates() -> Array:
	var ranked := _results.duplicate()
	ranked.sort_custom(func(a: Dictionary, b: Dictionary) -> bool: return float(a["score"]) > float(b["score"]))
	var out: Array = []
	var taken := 0
	for r: Dictionary in ranked:
		if taken >= PERMUTE_TOP:
			break
		var play: Array[Ingredient] = r["play"]
		if play.size() < 2:
			continue
		taken += 1
		var perms: Array = []
		_permutations(play, [], [], perms)
		for perm: Array[Ingredient] in perms:
			if perm != play:
				out.append(perm)
	return out


func _permutations(items: Array[Ingredient], current: Array, used: Array, out: Array) -> void:
	if current.size() == items.size():
		var perm: Array[Ingredient] = []
		perm.assign(current)
		out.append(perm)
		return
	for i in range(items.size()):
		if i in used:
			continue
		used.append(i)
		current.append(items[i])
		_permutations(items, current, used, out)
		current.pop_back()
		used.pop_back()


# ---------------------------------------------------------------- simulation

## Scores `play` (in plate order) exactly as a Make would, then restores everything it touched.
## Returns null when the game's scoring code failed for this play.
func _evaluate(play: Array[Ingredient]) -> Variant:
	var rd := RunData.getRound()
	var deck := rd.getDeck()
	var runSeed: RunSeed = SushiRand._runSeed
	var savedRng: Dictionary = runSeed.bucketPositions.duplicate()
	var savedHand: Array[Ingredient] = deck.handPile.duplicate()
	var savedDiscard: Array[Ingredient] = deck.discardPile.duplicate()
	var savedRemaining: int = rd._remainingHands
	var savedPlayed: int = rd._handsPlayedInRound
	var masterSize := RunData.getMasterDeck().size()
	var states := _snapshotStates()

	# What Round.onPlayButtonClicked does before scoring: consume a hand, move the cards out.
	rd.set_block_signals(true)
	rd._remainingHands = savedRemaining - 1
	rd._handsPlayedInRound = savedPlayed + 1
	for ing in play:
		deck.handPile.erase(ing)
		deck.discardPile.append(ing)

	var result: Variant = _scorePlay(play)

	rd._remainingHands = savedRemaining
	rd._handsPlayedInRound = savedPlayed
	deck.handPile.assign(savedHand)
	deck.discardPile.assign(savedDiscard)
	rd.set_block_signals(false)
	runSeed.bucketPositions = savedRng
	_restoreStates(states)
	if RunData.getMasterDeck().size() != masterSize:
		ModLoaderLog.error("Scoring simulation changed the pantry size; please report which tools/cards were held.", LOG)
	if typeof(result) != TYPE_FLOAT:
		return null
	return result


func _scorePlay(play: Array[Ingredient]) -> Variant:
	var events := _buildScoringEvents(play)
	if events == null:
		return null
	return _foldScore(events)


## Mirrors the first half of ScoringCalculator.calculate, up to the moment the hand's
## total is transferred to the round.
func _buildScoringEvents(originalIngredients: Array[Ingredient]) -> EventsList:
	var allIngredients: Array[Ingredient] = []
	for ing in originalIngredients:
		var clone := ing.duplicate() as Ingredient
		clone.instanceId = ing.instanceId
		clone.realItem = ing
		clone.state = ing.state.duplicate(true)
		clone.modifiers = ing.modifiers.duplicate()
		allIngredients.append(clone)
	var tools: Array[Tool] = []
	for original in _simTools():
		var clone := original.duplicate() as Tool
		clone.instanceId = original.instanceId
		clone.realItem = original
		clone.state = original.state.duplicate(true)
		tools.append(clone)
	var sauces: Array[Sauce] = []
	for original in RunData.getSauces():
		var clone := original.duplicate() as Sauce
		clone.instanceId = original.instanceId
		clone.realItem = original
		clone.state = original.state.duplicate(true)
		sauces.append(clone)
	var boss: Boss = RunData.getMap().getCurrentStop().boss

	var events := EventsList.new()
	events.add(Event.initializeScore(0.0, RunData.getBaseHype()))
	events.addList(ScoringCalculator.executeFirstBeforeScoringIngModifiers(allIngredients))
	events.addList(ScoringCalculator.executeBeforeScoringBoss(boss, allIngredients, tools, sauces))
	events.addList(ScoringCalculator.executeBeforeScoringTools(tools, allIngredients))
	var scorable: Array[Ingredient] = []
	for ing in allIngredients:
		if not ing.isDisabled:
			scorable.append(ing)
	events.addList(ScoringCalculator.executeBeforeScoringIngModifiers(allIngredients))
	for ing in scorable:
		events.addList(ScoringCalculator.processIngredient(ing, allIngredients, tools))
		for source: Variant in ScoringCalculator.getRetriggerSources(ing, tools):
			var retriggers := 0
			while true:
				var should: ShouldRetrigger = source.shouldRetriggerIngredient(ing, retriggers)
				events.addAll(should.events)
				if not should.shouldRetrigger:
					break
				events.add(Event.shiftBubbles(ing.instanceId))
				events.addList(ScoringCalculator.processIngredient(ing, allIngredients, tools))
				retriggers += 1
			ing.state["saved_retriggers_" + str(source.instanceId)] = retriggers
			ing.clearRetriggerState()
	for ing in scorable:
		events.add(Event.tallyIngBubble(ing.instanceId))
	events.addList(ScoringCalculator.executeIngModifiersOnScoring(allIngredients, tools))
	events.addList(ScoringCalculator.processSauces(sauces, allIngredients))
	events.addList(ScoringCalculator.processToolsOnScoring(tools, allIngredients))
	events.addList(ScoringCalculator.executeBossOnScoring(boss, allIngredients, tools, sauces))
	events.add(Event.calculateTotal())
	events.addList(ScoringCalculator.executeBossOnTotalCalculated(boss, allIngredients, tools, sauces))
	events.add(Event.transferToRound())
	return events


## Mirror Plate copies a card into the pantry directly while scoring (not via a deferred event),
## and has no effect on the score, so it is replaced by an inert stand-in at the same position.
func _simTools() -> Array[Tool]:
	var out: Array[Tool] = []
	for t in RunData.getTools():
		if t.data and t.data.id == IdDB.ToolID.MIRROR_PLATE:
			var standIn := Tool.new(_mirrorDummyRes)
			standIn.instanceId = t.instanceId
			standIn.edition = t.edition
			standIn.isDisabled = t.isDisabled
			standIn.state = t.state.duplicate(true)
			out.append(standIn)
		else:
			out.append(t)
	return out


## Replays the score arithmetic of EventsProcessor._processEvents / RoundScore / CardBubble.
func _foldScore(events: EventsList) -> Variant:
	var baseHype := RunData.getBaseHype()
	var flavour := 0.0
	var hype := baseHype
	var total := 0.0
	var bubbles: Dictionary = {}  # card id -> Array of bubble values, last is the live one
	for e: Event in events.getEvents():
		var a: Dictionary = e.args
		match e.type:
			Event.Type.INITIALIZE_SCORE:
				flavour = float(a["flavour"])
				hype = float(a["hype"])
			Event.Type.SHOW_SCORE_BUBBLE:
				if not bubbles.has(a["card_id"]):
					bubbles[a["card_id"]] = []
				bubbles[a["card_id"]].append(float(a["amount"]))
			Event.Type.ADD_ING_FLAVOUR:
				var list: Array = bubbles.get(a["card_id"], [])
				if not list.is_empty():
					list[-1] = float(list[-1]) + float(a["amount"])
			Event.Type.MULT_ING_FLAVOUR:
				var list: Array = bubbles.get(a["card_id"], [])
				if not list.is_empty():
					var v := float(list[-1])
					list[-1] = v * float(a["amount"]) if v >= 0 else v / float(a["amount"])
			Event.Type.SET_ING_FLAVOUR:
				var list: Array = bubbles.get(a["card_id"], [])
				if not list.is_empty():
					list[-1] = float(a["amount"])
			Event.Type.TALLY_SCORE_BUBBLE:
				for v: float in bubbles.get(a["card_id"], []):
					flavour += v
				bubbles.erase(a["card_id"])
			Event.Type.ADD_GLOBAL_FLAVOUR:
				flavour += float(a["amount"])
			Event.Type.MULT_GLOBAL_FLAVOUR:
				flavour *= float(a["amount"])
			Event.Type.ADD_HYPE:
				hype += float(a["amount"])
			Event.Type.MULT_HYPE:
				hype *= float(a["amount"])
			Event.Type.BALANCE_FLAVOUR_HYPE:
				var balanced := (flavour + hype) / 2.0
				flavour = balanced
				hype = balanced
			Event.Type.CALCULATE_TOTAL:
				total = maxf(flavour * hype, 1.0)
				flavour = 0.0
				hype = baseHype
			Event.Type.BOSS_MULT_TOTAL_SCORE:
				total *= float(a["amount"])
			Event.Type.BOSS_CLAMP_TOTAL_SCORE:
				var limit := RunData.getRound().getTargetScore() * float(a["percentage"])
				if total > limit:
					total = limit
			Event.Type.TRANSFER_TO_ROUND:
				return total
	return total


func _snapshotStates() -> Array:
	var items: Array = []
	items.append_array(RunData.getTools())
	items.append_array(RunData.getSauces())
	items.append_array(_handIngs)
	var boss: Boss = RunData.getMap().getCurrentStop().boss
	if boss:
		items.append(boss)
	var out: Array = []
	for item: Object in items:
		if item and is_instance_valid(item):
			out.append([item, (item.get("state") as Dictionary).duplicate(true)])
	return out


func _restoreStates(snapshot: Array) -> void:
	for pair: Array in snapshot:
		var item: Object = pair[0]
		if is_instance_valid(item):
			item.set("state", pair[1])


func _computeFingerprint() -> int:
	var rd := RunData.getRound()
	var deck := rd.getDeck()
	var parts: Array = []
	for card: IngredientCardV2 in _hand.getAllCards():
		var i := card.ingredient
		parts.append([i.instanceId, i.bonusScore, i.quality.id if i.quality else "", i.getModifierIds(),
			i.isDisabled, i.additionalTypes, i.data.id if i.data else "", i.state])
	for t in RunData.getTools():
		parts.append([t.instanceId, t.isDisabled, t.edition.id if t.edition else "", t.state])
	for s in RunData.getSauces():
		parts.append([s.instanceId, s.state])
	var boss: Boss = RunData.getMap().getCurrentStop().boss
	parts.append(boss.state if boss else {})
	parts.append([rd.getCurrentRoundScore(), rd.getTargetScore(), rd.getRemainingHands(),
		rd.getRemainingDiscards(), rd.getHandsPlayedInRound(), rd.getDiscardsUsedInRound(),
		deck.drawPile.size(), deck.discardPile.size(), RunData.getMasterDeck().size(),
		RunData.getBaseHype(), RunData.getMoney(), RunData.getMaxPlayableCards(),
		SushiRand._runSeed.bucketPositions])
	return hash(parts)


# ---------------------------------------------------------------- current selection

func _refreshSelectedScore() -> void:
	var selected := _hand.getSelectedIngredients()
	var key := _keyOf(selected)
	if key == _selectedKey:
		return
	_selectedKey = key
	_selectedScore = _evaluate(selected) if not selected.is_empty() else null


func _keyOf(ings: Array[Ingredient]) -> String:
	var ids := PackedStringArray()
	for i in ings:
		ids.append(str(i.instanceId))
	return ",".join(ids)


func _onScoringStarted() -> void:
	_busy = true
	_predicted = null
	if _hand and is_instance_valid(_hand) and RunData.isRoundActive():
		_predicted = _evaluate(_hand.getSelectedIngredients())
		_scoreBefore = RunData.getRound().getCurrentRoundScore()


func _onScoringFinished() -> void:
	_busy = false
	_fingerprint = 0
	if _predicted == null or not RunData.isRoundActive():
		return
	var actual := RunData.getRound().getCurrentRoundScore() - _scoreBefore
	var predicted := float(_predicted)
	var tolerance := maxf(1.0, absf(actual) * 0.001)
	if absf(actual - predicted) <= tolerance:
		ModLoaderLog.info("Make scored %s, as predicted." % UtilsNumbers.formatNumber(actual, true), LOG)
	else:
		ModLoaderLog.warning("Make scored %s but %s was predicted." % [
			UtilsNumbers.formatNumber(actual, true), UtilsNumbers.formatNumber(predicted, true)], LOG)


func _onDiscardStarted() -> void:
	_busy = true


func _onDiscardFinished() -> void:
	_busy = false
	_fingerprint = 0


func _selectBest() -> void:
	var best: Array[Ingredient] = _best.get("play", [] as Array[Ingredient])
	if best.is_empty():
		return
	_hand.deselectAll()
	var cards := _hand.getAllCards()
	var bestCards: Array[IngredientCardV2] = []
	var slots: Array[int] = []
	for ing in best:
		for i in range(cards.size()):
			if cards[i].ingredient == ing:
				bestCards.append(cards[i])
				slots.append(i)
	if bestCards.size() != best.size():
		return
	var sortedSlots := slots.duplicate()
	sortedSlots.sort()
	if sortedSlots != slots:
		var order: Array = cards.duplicate()
		for k in range(bestCards.size()):
			order[sortedSlots[k]] = bestCards[k]
		_hand.uiHand.reorderItems(order)
	for card in bestCards:
		if not card.isSelected:
			card.onCardInteracted()


# ---------------------------------------------------------------- overlay

func _ensureOverlay() -> void:
	if _overlay and is_instance_valid(_overlay) and _overlay.is_inside_tree():
		return
	var scene := get_tree().current_scene
	if scene == null:
		return
	_overlay = Control.new()
	_overlay.name = "BestHandOverlay"
	_overlay.top_level = true
	_overlay.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_overlay.z_index = OVERLAY_Z
	scene.add_child(_overlay)
	_summary = _newPanel()
	_badges.clear()


func _newPanel() -> PixelLabelWBackground:
	var panel := (load(PANEL_SCENE) as PackedScene).instantiate() as PixelLabelWBackground
	panel.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_overlay.add_child(panel)
	for child in panel.find_children("*", "Control", true, false):
		(child as Control).mouse_filter = Control.MOUSE_FILTER_IGNORE
	return panel


func _updateOverlay() -> void:
	if _overlay == null or _summary == null:
		return
	_overlay.visible = true
	var cards := _hand.getAllCards()
	var best: Array[Ingredient] = _best.get("play", [] as Array[Ingredient])
	var done := _phase == 3
	var shown := _phase >= 2  # every card set tried; only reorderings may still improve it

	# Badges: play position on the plate, on each card of the best hand.
	while _badges.size() < best.size():
		var badge := _newPanel()
		badge.color = Colors.GOLD
		badge.textColor = Colors.TEXT_COLOR
		_badges.append(badge)
	var topY := INF
	for card in cards:
		topY = minf(topY, card.uiSprite.get_global_rect().position.y)
	for k in range(_badges.size()):
		var badge := _badges[k]
		badge.visible = shown and k < best.size()
		if not badge.visible:
			continue
		var card: IngredientCardV2 = null
		for c in cards:
			if c.ingredient == best[k]:
				card = c
		if card == null or card.isDragged:
			badge.visible = false
			continue
		badge.text = str(k + 1)
		var r := card.uiSprite.get_global_rect()
		var sz := badge.get_combined_minimum_size()
		badge.size = sz
		badge.global_position = Vector2(r.get_center().x - sz.x / 2.0, r.position.y - sz.y - 2.0)

	# Summary line above the hand: the two numbers the Make decision is made on.
	if cards.is_empty() or _best.is_empty():
		_summary.visible = false
		return
	_summary.visible = true
	var rd := RunData.getRound()
	var bestScore := float(_best["score"])
	var text := "Best %s" % UtilsNumbers.formatNumber(bestScore, true)
	if not done:
		text += "..."
	elif UtilsNumbers.isScoreWon(rd.getCurrentRoundScore() + bestScore, rd.getTargetScore()):
		text += " (wins)"
	if _selectedScore != null and _selectedKey != "":
		var bestKey := _keyOf(best)
		if _selectedKey != bestKey:
			text += "   Selected %s" % UtilsNumbers.formatNumber(float(_selectedScore), true)
	if shown:
		text += "   [Ctrl+B]"
	_summary.text = text
	var handRect := _hand.uiHand.get_global_rect()
	var ssz := _summary.get_combined_minimum_size()
	_summary.size = ssz
	var badgeH := _badges[0].get_combined_minimum_size().y if not _badges.is_empty() else 12.0
	var y := (topY if topY != INF else handRect.position.y) - badgeH - ssz.y - 6.0
	_summary.global_position = Vector2(handRect.get_center().x - ssz.x / 2.0, y)
