from Level import *
import Spells
import random


def get_temp_hp_buffs(unit):
	return [b for b in getattr(unit, 'buffs', []) if getattr(b, 'temp_hp', 0) > 0]


def get_temp_hp(unit):
	return sum(getattr(b, 'temp_hp', 0) for b in get_temp_hp_buffs(unit))


def spend_temp_hp(unit, amount):
	remaining = amount
	absorbed = 0
	for buff in list(get_temp_hp_buffs(unit)):
		if remaining <= 0: break
		spent = min(buff.temp_hp, remaining)
		buff.temp_hp -= spent
		absorbed += spent
		remaining -= spent
		if hasattr(buff, 'update_name'):
			buff.update_name()
		if buff.temp_hp <= 0 and getattr(buff, 'applied', False):
			unit.remove_buff(buff)
	return absorbed, remaining


def try_spell_shield(unit, amount, damage_type, source):
	if amount <= 0: return False
	for buff in list(getattr(unit, 'buffs', [])):
		if not getattr(buff, 'is_spell_shield_buff', False): continue
		if buff.try_block_damage(amount, damage_type, source):
			return True
	return False


def patch_temp_hp_damage_order():
	if getattr(Level, '_custom_spells_temp_hp_patched', False):
		return

	def deal_damage(self, x, y, amount, damage_type, source, flash=True, redirect=False):

		# Auto make effects if none were already made
		if flash:
			effect = Effect(x, y, damage_type.color, Color(0, 0, 0), 12)
			if amount == 0: effect.minor = True
			self.apply_effect_pressure(effect)
			self.effects.append(effect)

		cloud = self.tiles[x][y].cloud
		if cloud and amount > 0: cloud.on_damage(damage_type)

		unit = self.get_unit_at(x, y)
		if not unit: return 0
		if not unit.is_alive(): return 0
		is_invulnerable = getattr(unit, 'is_invulnerable', None)
		if amount > 0 and is_invulnerable and is_invulnerable():
			self.log((getattr(text, 'INVULNERABLE_BLOCK', "{unit} ignores {amount} damage from {source}."), {
				"unit": color_entity(log_name(unit), log_color(unit)),
				"amount": amount,
				"source": source.name,
			}))
			self.show_effect(unit.x, unit.y, Tags.Shield_Expire)
			return 0

		# Existing game redirection hooks run before normal damage layers.
		if not redirect and hasattr(unit, "on_pre_damage_redirect"):
			result = unit.on_pre_damage_redirect(amount, damage_type, source)
			if result is not None: return result

		unit_id = id(unit)
		if self.damage_instances[unit_id] >= DAMAGE_INSTANCE_CAP: return 0

		orig_amount = amount

		resist = unit.resists.get(damage_type, 0)
		resist = min(resist, 100)
		multiplier = (100 - resist) / 100.0
		amount = int(math.ceil(amount * multiplier))

		limiter = getattr(unit, 'damage_limit_buff', None)
		if amount > 0 and limiter:
			remaining = max(0, limiter.damage_limit - limiter.damage_counter)
			if remaining <= 0: return 0
			amount = min(amount, remaining)

		if self.event_manager.has_handlers(EventOnPreDamaged, unit):
			pre_damage_event = EventOnPreDamaged(unit, orig_amount, amount, damage_type, source)
			self.event_manager.raise_event(pre_damage_event, unit)

		unit_str = color_entity(log_name(unit), log_color(unit))
		source_str = source.name
		owner_str = color_entity(log_name(source.owner), log_color(source.owner)) if source.owner else source.name

		dtype_label = tag_label(damage_type)
		dtype_key = tag_key(damage_type)

		if amount > 0 and unit.shields > 0:
			unit.shields = unit.shields - 1
			log_txt = (text.DMG_BLOCKED, {"unit": unit_str, "amount": amount, "dtype_label": dtype_label, "dtype_key": dtype_key, "source": source_str})
			self.log(log_txt)
			self.show_effect(unit.x, unit.y, Tags.Shield_Expire)
			evt = EventOnShieldRemoved(unit, source)
			self.event_manager.raise_event(evt, entity=unit)
			return 0

		if amount > 0 and get_temp_hp(unit) > 0:
			absorbed, amount = spend_temp_hp(unit, amount)
			if absorbed:
				self.log(("{unit}'s temporary HP absorbed {amount} damage.", {"unit": unit_str, "amount": absorbed}))
				self.show_effect(unit.x, unit.y, Tags.Shield_Expire)
			if amount <= 0:
				return 0

		if amount > 0 and try_spell_shield(unit, amount, damage_type, source):
			return 0

		resolve_lethal_damage = getattr(unit, 'resolve_lethal_damage', None)
		if amount > 0 and resolve_lethal_damage:
			amount = resolve_lethal_damage(amount, damage_type, source)

		# Cap damage to current hp, cap healing to missing hp
		if amount > 0: amount = min(amount, unit.cur_hp)
		elif amount < 0: amount = max(amount, unit.cur_hp - unit.max_hp)

		unit.cur_hp = unit.cur_hp - amount

		is_temp_buff = isinstance(source, Buff) and source.buff_type in (BUFF_TYPE_BLESS, BUFF_TYPE_CURSE)

		if amount > 0:
			if source.owner and not is_temp_buff: self.log((text.DMG_DEALS, {"owner": owner_str, "amount": amount, "dtype_label": dtype_label, "dtype_key": dtype_key, "unit": unit_str, "source": source_str}))
			else: self.log((text.DMG_TAKES, {"unit": unit_str, "amount": amount, "dtype_label": dtype_label, "dtype_key": dtype_key, "source": source_str}))
		elif amount < 0:
			if not is_temp_buff: self.log((text.HEAL_BY, {"owner": owner_str, "unit": unit_str, "amount": -amount, "source": source_str}))
			else: self.log((text.HEAL_FROM, {"unit": unit_str, "amount": -amount, "source": source_str}))

		if amount < 0:
			evt = EventOnHealed(unit, amount, source)
			self.event_manager.raise_event(evt, unit)

		elif amount > 0:
			if self.player_unit:
				if are_hostile(unit, self.player_unit):
					key = source_str
					if source.owner and source.owner.source and not (isinstance(source, Buff) and source.buff_type == BUFF_TYPE_CURSE):
						key = source.owner.name

					self.damage_dealt_sources[key] += amount
					self.turn_summary.damage_dealt[key] += amount
				else:
					if isinstance(source, Buff) and source.buff_type == BUFF_TYPE_CURSE:
						key = source_str
					elif source.owner:
						key = source.owner.name
					else:
						key = source_str

					if unit == self.player_unit:
						self.damage_taken_sources[key] += amount
						self.turn_summary.self_damage_taken[key] += amount
					else:
						self.turn_summary.ally_damage_taken[key] += amount

			damage_event = EventOnDamaged(unit, amount, damage_type, source)
			self.event_manager.raise_event(damage_event, unit)

			if (unit.cur_hp <= 0):
				unit.kill(damage_event=damage_event)
				if unit.killed:
					if source.owner: self.log((text.KILLED_BY_UNIT, {"unit": unit_str, "owner": owner_str, "source": source_str}))
					else: self.log((text.KILLED_BY_ENV, {"unit": unit_str, "source": source_str}))

			if (unit.cur_hp > unit.max_hp):
				unit.cur_hp = unit.max_hp
		else:
			amount = 0

		if (unit.cur_hp > unit.max_hp):
			unit.cur_hp = unit.max_hp

		self.damage_instances[unit_id] += 1
		if self.damage_instances[unit_id] == DAMAGE_INSTANCE_CAP:
			self.log((text.DMG_CAP, {"unit": unit_str}))

		return amount

	Level.deal_damage = deal_damage
	Level._custom_spells_temp_hp_patched = True


class ScorchingBlast(Spell):

	def on_init(self):
		self.name = "Scorching Blast"
		self.tags = [Tags.Fire, Tags.Sorcery]
		self.damage_type = [Tags.Fire]
		self.asset = ['UI', 'spell skill icons', 'fireball']

		self.level = 1
		self.max_charges = 30
		self.range = 6
		self.damage = 10
		self.can_target_empty = False

		self.scorching_stacks = 0
		self.aoe_threshold = 50
		self.bounce_threshold = 100
		self.true_damage_threshold = 200
		self.bounce_range = 4

	def get_stat(self, attr, base=None):
		value = Spell.get_stat(self, attr, base)
		if attr == 'damage':
			value += getattr(self, 'scorching_stacks', 0)
		return value

	def fmt_dict(self):
		d = Spell.fmt_dict(self)
		stacks = getattr(self, 'scorching_stacks', 0)
		d['scorching_stacks'] = stacks
		d['aoe_remaining'] = max(0, self.aoe_threshold - stacks)
		d['bounce_remaining'] = max(0, self.bounce_threshold - stacks)
		d['true_damage_remaining'] = max(0, self.true_damage_threshold - stacks)
		return d

	def get_description(self):
		return (
			"Deal [{damage}:damage] [Fire] damage to the target.\n"
			"Each enemy killed by Scorching Blast grants 1 scorching stack.\n"
			"Scorching stacks: [{scorching_stacks}:damage]\n"
			"At 50 stacks, Scorching Blast hits a 3x3 square.\n"
			"At 100 stacks, Scorching Blast bounces to another enemy within 4 tiles.\n"
			"At 200 stacks, Scorching Blast deals true damage and directly hit enemies at or below 10% HP die immediately."
		)

	def has_aoe(self):
		return getattr(self, 'scorching_stacks', 0) >= self.aoe_threshold

	def has_bounce(self):
		return getattr(self, 'scorching_stacks', 0) >= self.bounce_threshold

	def has_true_damage(self):
		return getattr(self, 'scorching_stacks', 0) >= self.true_damage_threshold

	def get_blast_points(self, x, y):
		if not self.has_aoe():
			return [Point(x, y)]

		points = []
		level = self.caster.level
		for dx in [-1, 0, 1]:
			for dy in [-1, 0, 1]:
				px = x + dx
				py = y + dy
				if level.is_point_in_bounds(Point(px, py)):
					points.append(Point(px, py))
		return points

	def get_impacted_tiles(self, x, y):
		return self.get_blast_points(x, y)

	def can_harm(self, target_unit):
		if self.has_true_damage(): return True
		return Spell.can_harm(self, target_unit)

	def can_cast(self, x, y):
		if not Spell.can_cast(self, x, y): return False
		unit = self.caster.level.get_unit_at(x, y)
		return bool(unit and are_hostile(self.caster, unit))

	def deal_blast_damage(self, x, y):
		level = self.caster.level
		unit = level.get_unit_at(x, y)
		if unit is self.caster: return 0

		if not self.has_true_damage():
			return level.deal_damage(x, y, self.get_stat('damage'), Tags.Fire, self)

		if not unit: return level.deal_damage(x, y, 0, Tags.Fire, self)

		old_shields = unit.shields
		old_resist = unit.resists.get(Tags.Fire, 0)
		missing_limiter = object()
		old_limiter = getattr(unit, 'damage_limit_buff', missing_limiter)

		unit.shields = 0
		unit.resists[Tags.Fire] = 0
		unit.damage_limit_buff = None
		try:
			return level.deal_damage(x, y, self.get_stat('damage'), Tags.Fire, self, redirect=True)
		finally:
			unit.shields = old_shields
			unit.resists[Tags.Fire] = old_resist
			if old_limiter is missing_limiter:
				try: delattr(unit, 'damage_limit_buff')
				except AttributeError: pass
			else:
				unit.damage_limit_buff = old_limiter

	def execute_if_low(self, unit):
		if not self.has_true_damage(): return
		if not unit or not unit.is_alive(): return
		if unit is self.caster: return
		if not are_hostile(self.caster, unit): return
		if unit.cur_hp * 10 > unit.max_hp: return

		level = self.caster.level
		old_shields = unit.shields
		old_resist = unit.resists.get(Tags.Fire, 0)
		missing_limiter = object()
		old_limiter = getattr(unit, 'damage_limit_buff', missing_limiter)

		unit.shields = 0
		unit.resists[Tags.Fire] = 0
		unit.damage_limit_buff = None
		try:
			level.deal_damage(unit.x, unit.y, unit.cur_hp, Tags.Fire, self, redirect=True)
		finally:
			unit.shields = old_shields
			unit.resists[Tags.Fire] = old_resist
			if old_limiter is missing_limiter:
				try: delattr(unit, 'damage_limit_buff')
				except AttributeError: pass
			else:
				unit.damage_limit_buff = old_limiter

	def get_bounce_target(self, origin, hit_units):
		level = self.caster.level
		candidates = [
			u for u in level.get_units_in_ball(origin, self.bounce_range)
			if u.is_alive()
			and u not in hit_units
			and are_hostile(self.caster, u)
		]
		if not candidates: return None
		return min(candidates, key=lambda u: level.unit_distance(origin, u))

	def hit_blast(self, x, y, hit_units):
		level = self.caster.level
		direct_target = level.get_unit_at(x, y)
		if direct_target and direct_target.is_alive() and are_hostile(self.caster, direct_target):
			hit_units.add(direct_target)

		for p in self.get_blast_points(x, y):
			unit = level.get_unit_at(p.x, p.y)
			if unit is self.caster: continue
			if unit and unit.is_alive() and are_hostile(self.caster, unit):
				hit_units.add(unit)
			self.deal_blast_damage(p.x, p.y)
			yield

		self.execute_if_low(direct_target)
		if direct_target and are_hostile(self.caster, direct_target): hit_units.add(direct_target)

	def cast(self, x, y):
		level = self.caster.level
		hit_units = set()
		initial_point = Point(x, y)

		yield from self.hit_blast(x, y, hit_units)

		if self.has_bounce():
			bounce_target = self.get_bounce_target(initial_point, hit_units)
			if bounce_target:
				level.show_beam(initial_point, bounce_target, Tags.Fire)
				yield
				yield from self.hit_blast(bounce_target.x, bounce_target.y, hit_units)

		kills = sum(1 for u in hit_units if not u.is_alive())
		if kills:
			self.scorching_stacks += kills
			self.do_ui_flash(Tags.Fire)


if not any(getattr(spell_class, '__name__', None) == "ScorchingBlast" for spell_class in Spells.all_player_spell_constructors):
	Spells.all_player_spell_constructors.append(ScorchingBlast)


class TimeStopped(Buff):

	def on_init(self):
		self.name = "Time Stopped"
		self.buff_type = BUFF_TYPE_CURSE
		self.stack_type = STACK_DURATION
		self.color = Tags.Arcane.color
		self.asset = ['status', 'stun']
		self.description = "Cannot act while time is stopped"
		self.freeze_animation = True

	def on_attempt_advance(self):
		return False


class TimeStop(Spell):

	def on_init(self):
		self.name = "Time Stop"
		self.tags = [Tags.Arcane, Tags.Sorcery]
		self.asset = ['UI', 'spell skill icons', 'temporal_transfusion']
		self.level = 9
		self.max_charges = 3
		self.range = 0
		self.duration = 4

	def get_description(self):
		return "Stop time for [{duration}:duration] rounds after this one. The caster is unaffected."

	def cast(self, x, y):
		level = self.caster.level
		# Buffs tick down after a unit misses an action, so +1 prevents the casting round
		# from being counted as one of the listed stopped rounds.
		duration = self.get_stat('duration') + 1
		for unit in list(level.units):
			if unit is self.caster: continue
			if not unit.is_alive(): continue
			was_debuff_immune = unit.debuff_immune
			unit.debuff_immune = False
			try:
				unit.apply_buff(TimeStopped(), duration)
			finally:
				unit.debuff_immune = was_debuff_immune
			level.show_effect(unit.x, unit.y, Tags.Arcane, minor=True)
			yield


if not any(getattr(spell_class, '__name__', None) == "TimeStop" for spell_class in Spells.all_player_spell_constructors):
	Spells.all_player_spell_constructors.append(TimeStop)


class CosmicBinding(Spell):

	def on_init(self):
		self.name = "Cosmic Binding"
		self.tags = [Tags.Arcane, Tags.Sorcery]
		self.damage_type = [Tags.Arcane]
		self.asset = ['UI', 'spell skill icons', 'arcane_warding']
		self.level = 1
		self.max_charges = 15
		self.range = 5
		self.damage = 10
		self.duration = 3
		self.can_target_empty = False

		self.upgrades['bounded_fate'] = (1, 3, "Bounded Fate", "Every enemy caught in the 3x3 square behind the target takes damage and is [stunned].")
		self.upgrades['chaining_bind'] = (1, 3, "Chaining Bind", "If an enemy is caught in the 3x3 square, Cosmic Binding repeats one more time in another 3x3 square behind that enemy.")

	def get_description(self):
		return ("Deal [{damage}:damage] [Arcane] damage to a target.\n"
				"If enemies are in the 3x3 square directly behind the target, affected enemies take [{damage}:damage] [Arcane] damage and are [stunned] for [{duration}:duration] rounds. Every enemy that is stunned by this spell also takes its damage.")

	def can_cast(self, x, y):
		if not Spell.can_cast(self, x, y): return False
		unit = self.caster.level.get_unit_at(x, y)
		return bool(unit and are_hostile(self.caster, unit))

	def get_behind_direction(self, target):
		dx = target.x - self.caster.x
		dy = target.y - self.caster.y
		step_x = 0 if dx == 0 else (1 if dx > 0 else -1)
		step_y = 0 if dy == 0 else (1 if dy > 0 else -1)
		return step_x, step_y

	def get_behind_points(self, target, direction=None):
		step_x, step_y = direction if direction else self.get_behind_direction(target)
		center = Point(target.x + 2 * step_x, target.y + 2 * step_y)
		points = []
		level = self.caster.level
		for dx in [-1, 0, 1]:
			for dy in [-1, 0, 1]:
				p = Point(center.x + dx, center.y + dy)
				if level.is_point_in_bounds(p):
					points.append(p)
		return points

	def get_impacted_tiles(self, x, y):
		points = [Point(x, y)]
		if self.caster:
			direction = self.get_behind_direction(Point(x, y))
			points.extend(self.get_behind_points(Point(x, y), direction))
			if self.get_stat('chaining_bind'):
				chain_center = Point(x + 2 * direction[0], y + 2 * direction[1])
				points.extend(self.get_behind_points(chain_center, direction))
		return points

	def get_caught_enemies(self, anchor, direction, excluded=None):
		level = self.caster.level
		excluded = excluded or set()
		candidates = []
		seen = set()
		for p in self.get_behind_points(anchor, direction):
			unit = level.get_unit_at(p.x, p.y)
			if not unit or unit in excluded or unit in seen: continue
			if not unit.is_alive(): continue
			if not are_hostile(self.caster, unit): continue
			seen.add(unit)
			candidates.append(unit)
		return candidates

	def get_binding_targets(self, anchor, direction, excluded=None):
		candidates = self.get_caught_enemies(anchor, direction, excluded)
		if not candidates: return [], []
		damage_target = min(candidates, key=lambda u: self.caster.level.unit_distance(anchor, u))
		stun_targets = candidates if self.get_stat('bounded_fate') else [damage_target]
		# Damage and stun target sets must stay identical: Bounded Fate expands
		# both effects to every enemy caught in the square.
		return stun_targets, stun_targets

	def stun_bound_units(self, units):
		for unit in units:
			if unit and unit.is_alive():
				unit.apply_buff(Stun(), self.get_stat('duration'))

	def hit_binding_square(self, anchor, direction, stunned_units, excluded):
		level = self.caster.level
		damage_targets, caught_targets = self.get_binding_targets(anchor, direction, excluded)
		if not damage_targets: return []

		for target in damage_targets:
			level.show_beam(anchor, target, Tags.Arcane)
			level.deal_damage(target.x, target.y, self.get_stat('damage'), Tags.Arcane, self)
			yield

		excluded.update(caught_targets)
		stunned_units.update(caught_targets)
		self.stun_bound_units(stunned_units)
		return caught_targets

	def cast(self, x, y):
		level = self.caster.level
		primary = level.get_unit_at(x, y)
		if not primary: return

		level.show_beam(self.caster, primary, Tags.Arcane)
		level.deal_damage(primary.x, primary.y, self.get_stat('damage'), Tags.Arcane, self)
		yield

		direction = self.get_behind_direction(primary)
		stunned_units = {primary}
		excluded = {primary}

		targets = yield from self.hit_binding_square(primary, direction, stunned_units, excluded)
		if targets and self.get_stat('chaining_bind'):
			chain_anchor = min(targets, key=lambda u: level.unit_distance(primary, u))
			yield from self.hit_binding_square(chain_anchor, direction, stunned_units, excluded)


if not any(getattr(spell_class, '__name__', None) == "CosmicBinding" for spell_class in Spells.all_player_spell_constructors):
	Spells.all_player_spell_constructors.append(CosmicBinding)


class SpellShieldBuff(Buff):

	def on_init(self):
		self.name = "Spell Shield"
		self.is_spell_shield_buff = True
		self.buff_type = BUFF_TYPE_BLESS
		self.stack_type = STACK_REPLACE
		self.color = Tags.Arcane.color
		self.asset = ['status', 'protection']
		self.description = "Incoming damage is prevented by spending 1 charge from a random charged spell."

	def get_charge_candidates(self):
		return [s for s in self.owner.spells if s.cur_charges > 0 and s.get_stat('max_charges') > 0]

	def try_block_damage(self, amount, damage_type, source):
		if amount <= 0: return False

		candidates = self.get_charge_candidates()
		if not candidates: return False

		spell = random.choice(candidates)
		spell.drain_charges(1)
		self.owner.level.show_effect(self.owner.x, self.owner.y, Tags.Arcane)
		self.owner.level.log(("[{unit}:{color}]'s Spell Shield spent a charge of {spell} and prevented {amount} damage.", {
			"unit": self.owner.name,
			"color": log_color(self.owner),
			"spell": spell.name,
			"amount": amount,
		}))
		return True


class SpellShield(Spell):

	def on_init(self):
		self.name = "Spell Shield"
		self.tags = [Tags.Arcane, Tags.Enchantment]
		self.asset = ['UI', 'spell skill icons', 'arcane_warding']
		self.level = 1
		self.max_charges = 1
		self.range = 0

	def get_description(self):
		return "Gain [Spell Shield]. After shields and temporary HP, incoming damage is prevented by spending 1 charge from a random charged spell. If no spell has charges, damage is taken normally."

	def cast_instant(self, x, y):
		self.caster.apply_buff(SpellShieldBuff())


if not any(getattr(spell_class, '__name__', None) == "SpellShield" for spell_class in Spells.all_player_spell_constructors):
	Spells.all_player_spell_constructors.append(SpellShield)


class BarrierBuff(Buff):

	def __init__(self, amount):
		self.temp_hp = amount
		Buff.__init__(self)

	def on_init(self):
		self.update_name()
		self.buff_type = BUFF_TYPE_BLESS
		self.stack_type = STACK_NONE
		self.color = Tags.Arcane.color
		self.asset = ['status', 'protection']

	def update_name(self):
		self.name = "Barrier (%d)" % self.temp_hp

	def add_temp_hp(self, amount):
		self.temp_hp += amount
		self.update_name()

	def get_description(self):
		return "Absorbs [{temp_hp}:heal] damage after shields and before HP."

	def fmt_dict(self):
		return {'temp_hp': self.temp_hp}


class Barrier(Spell):

	def on_init(self):
		self.name = "Barrier"
		self.tags = [Tags.Arcane, Tags.Enchantment]
		self.asset = ['UI', 'spell skill icons', 'arcane_warding']
		self.level = 2
		self.max_charges = 5
		self.range = 0
		self.temp_hp = 20
		self.duration = 3
		self.stats.append('temp_hp')

		self.upgrades['quick_cast'] = (1, 2, "Emergency", "Barrier gains [quickcast:quick_cast].")
		self.upgrades['durable'] = (1, 2, "Durable", "Barrier lasts until you leave the level.")

	def get_description(self):
		desc = "Gain [{temp_hp}:heal] temporary HP for [{duration}:duration] turns."
		if self.get_stat('durable'):
			desc = "Gain [{temp_hp}:heal] temporary HP until you leave the level."
		return desc

	def cast_instant(self, x, y):
		duration = 0 if self.get_stat('durable') else self.get_stat('duration')
		existing = self.caster.get_buff(BarrierBuff)
		if existing:
			existing.add_temp_hp(self.get_stat('temp_hp'))
			if duration == 0:
				existing.turns_left = 0
			elif existing.turns_left:
				existing.turns_left = max(existing.turns_left, duration)
			else:
				existing.turns_left = duration
			self.caster.level.show_effect(self.caster.x, self.caster.y, Tags.Buff_Apply, existing.color)
		else:
			self.caster.apply_buff(BarrierBuff(self.get_stat('temp_hp')), duration)


if not any(getattr(spell_class, '__name__', None) == "Barrier" for spell_class in Spells.all_player_spell_constructors):
	Spells.all_player_spell_constructors.append(Barrier)


class RadianceBuff(Buff):

	def on_init(self):
		self.name = "Radiance"
		self.buff_type = BUFF_TYPE_CURSE
		self.stack_type = STACK_INTENSITY
		self.color = Tags.Holy.color
		self.asset = ['status', 'sanction']
		self.description = "If current HP is less than or equal to Radiance stacks, the unit dies."


class RadiantBlast(Spell):

	def on_init(self):
		self.name = "Radiant Blast"
		self.tags = [Tags.Holy, Tags.Sorcery]
		self.damage_type = [Tags.Holy]
		self.asset = ['UI', 'spell skill icons', 'heavenly_blast']
		self.level = 1
		self.max_charges = 20
		self.range = 10
		self.damage = 10
		self.radiance = 10
		self.spread_range = 4
		self.can_target_empty = False
		self.stats.append('radiance')
		self.stats.append('spread_range')

		self.upgrades['spread'] = (1, 3, "Spread", "If Radiant Blast kills an enemy, that enemy's Radiance stacks spread to the nearest enemy within [{spread_range}:range] tiles.")

	def get_description(self):
		return ("Stack [{radiance}:holy] [Radiance:holy] on the target, then deal [{damage}:damage] [Holy] damage.\n"
				"If the target's current HP is less than or equal to its [Radiance:holy] stacks, it dies.")

	def can_cast(self, x, y):
		if not Spell.can_cast(self, x, y): return False
		unit = self.caster.level.get_unit_at(x, y)
		return bool(unit and are_hostile(self.caster, unit))

	def get_radiance_stacks(self, unit):
		return unit.get_buff_stacks(RadianceBuff) if unit else 0

	def add_radiance(self, unit, stacks):
		for _ in range(stacks):
			unit.apply_buff(RadianceBuff())

	def get_nearest_enemy(self, origin, excluded=None):
		level = self.caster.level
		excluded = excluded or set()
		candidates = [
			unit for unit in level.get_units_in_ball(origin, self.get_stat('spread_range'))
			if unit.is_alive()
			and unit not in excluded
			and are_hostile(self.caster, unit)
		]
		if not candidates: return None
		return min(candidates, key=lambda unit: level.unit_distance(origin, unit))

	def kill_by_radiance(self, unit):
		if not unit or not unit.is_alive(): return False
		stacks = self.get_radiance_stacks(unit)
		if unit.cur_hp > stacks: return False

		level = self.caster.level
		level.show_effect(unit.x, unit.y, Tags.Holy)
		level.log(("[{unit}:{color}] is consumed by Radiance.", {
			"unit": unit.name,
			"color": log_color(unit),
		}))
		unit.kill(EventOnDamaged(unit, unit.cur_hp, Tags.Holy, self))
		return True

	def spread_radiance(self, origin, stacks, excluded):
		if not self.get_stat('spread'): return
		if stacks <= 0: return

		target = self.get_nearest_enemy(origin, excluded)
		if not target: return

		self.caster.level.show_beam(origin, target, Tags.Holy)
		yield
		yield from self.apply_radiance_and_resolve(target, stacks, excluded)

	def apply_radiance_and_resolve(self, unit, stacks, excluded):
		if not unit or not unit.is_alive(): return

		excluded.add(unit)
		self.add_radiance(unit, stacks)
		self.caster.level.show_effect(unit.x, unit.y, Tags.Holy, minor=True)

		spread_stacks = self.get_radiance_stacks(unit)
		if self.kill_by_radiance(unit):
			yield from self.spread_radiance(unit, spread_stacks, excluded)

	def cast(self, x, y):
		level = self.caster.level
		unit = level.get_unit_at(x, y)
		if not unit: return

		excluded = {unit}
		self.add_radiance(unit, self.get_stat('radiance'))
		level.show_effect(unit.x, unit.y, Tags.Holy, minor=True)

		level.deal_damage(unit.x, unit.y, self.get_stat('damage'), Tags.Holy, self)
		yield

		spread_stacks = self.get_radiance_stacks(unit)
		killed = not unit.is_alive() or self.kill_by_radiance(unit)
		if killed:
			yield from self.spread_radiance(unit, spread_stacks, excluded)


if not any(getattr(spell_class, '__name__', None) == "RadiantBlast" for spell_class in Spells.all_player_spell_constructors):
	Spells.all_player_spell_constructors.append(RadiantBlast)


patch_temp_hp_damage_order()
