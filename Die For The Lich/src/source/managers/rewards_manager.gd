extends Node

const MetaProgression = preload("res://mods/meta_progression.gd")

enum Type{
	CHEST_INIT, 
	CHEST_REGULAR, 
	CHEST_WEAP, 
	CHEST_TRINKET, 
	ANGEL_OFFERING, 
	CHEST_BOSS, 
}



func generate(map_spot_data: Map.SpotData) -> Items.Group:
	var ig: Items.Group = Items.Group.new()
	match map_spot_data.reward_type:
		Type.CHEST_INIT:

			if map_spot_data.act.id == 0:

				var dice: Array[Items.Entry] = []
				dice.append_array(Items.fetch().set_force_locked(GM.is_daily).set_drops(Items.Drops.CHEST).set_type([Items.Type.DIE]).pick_random_rarity(2, Items.FetchRarity.Create(90, 10, 0, 0)))
				dice.append_array(Items.fetch().set_force_locked(GM.is_daily).set_drops(Items.Drops.CHEST).set_type([Items.Type.DIE]).pick_random_rarity(1, Items.FetchRarity.Create(0, 0, 100, 0)))
				ig.add_bundle(dice)

				var weap: Items.Entry = Items.fetch().set_force_locked(GM.is_daily).set_drops(Items.Drops.CHEST).set_type([Items.Type.WEAPON]).pick_random_rarity(1, Items.FetchRarity.Create(70, 30, 0, 0))[0]
				ig.add_single(weap)






























			elif map_spot_data.act.id == 2:

				var abyss_dice_count: int = 3
				var abyss_dice: Array[Items.Entry] = Items.fetch().set_metadata({"abyss_die": true}).get_entries()
				var bundles: Array = [[], []]
				for i: int in bundles.size():
					for ii: int in abyss_dice_count:
						bundles[i].append(Mads.array_pick_random(abyss_dice, GM.random))
					var b: Array[Items.Entry] = []
					b.assign(bundles[i])
					ig.add_bundle(b)
		Type.CHEST_REGULAR:

			var fr: Items.FetchRarity = map_spot_data.act.get_fetch_rarity(Type.CHEST_REGULAR, map_spot_data.depth_perc)
			var dice: Array[Items.Entry] = Items.fetch().set_force_locked(GM.is_daily).set_type([Items.Type.DIE]).set_drops(Items.Drops.CHEST).pick_random_rarity(2, fr)
			ig.add_single_array(dice)

			if map_spot_data.act.id == 2:
				ig.add_single(Items.fetch().set_metadata({"abyss_die": true}).pick_random(1)[0])
			else:

				var coins: Array[Items.Entry] = []

				match map_spot_data.enemy_difficulty:
					EnemyManager.Difficulty.EASY:
						coins.append(Items.coin)

					EnemyManager.Difficulty.NORMAL:
						coins.append_array([Items.coin, Items.coin])
					EnemyManager.Difficulty.HARD:
						coins.append_array([Items.coin, Items.coin, Items.coin])




				if map_spot_data.act.id > 0:
					coins.append(Items.coin)

				ig.add_bundle(coins)










		Type.CHEST_WEAP:
			var fr: Items.FetchRarity = map_spot_data.act.get_fetch_rarity(Type.CHEST_WEAP, map_spot_data.depth_perc)
			ig.add_single_array(Items.fetch().set_force_locked(GM.is_daily).set_type([Items.Type.WEAPON]).set_weap_use_type([Weapon.UseType.OFFENSIVE]).set_drops(Items.Drops.CHEST).pick_random_rarity(1, fr))
			ig.add_single_array(Items.fetch().set_force_locked(GM.is_daily).set_type([Items.Type.WEAPON]).set_weap_use_type([Weapon.UseType.DEFENSIVE]).set_drops(Items.Drops.CHEST).pick_random_rarity(1, fr))
		Type.CHEST_TRINKET:
			var fr: Items.FetchRarity = map_spot_data.act.get_fetch_rarity(Type.CHEST_TRINKET, map_spot_data.depth_perc)
			ig.add_single_array(Items.fetch().set_force_locked(GM.is_daily).set_type([Items.Type.TRINKET]).set_drops(Items.Drops.CHEST).pick_random_rarity(2, fr))
			ig.add_bundle([Items.coin, Items.coin])
		Type.ANGEL_OFFERING:
			var fr: Items.FetchRarity = map_spot_data.act.get_fetch_rarity(Type.ANGEL_OFFERING, map_spot_data.depth_perc)
			var weap_off: Items.Entry = Items.fetch().set_force_locked(GM.is_daily).set_weap_use_type([Weapon.UseType.OFFENSIVE]).set_drops(Items.Drops.OFFERING).pick_random_rarity(1, fr)[0]
			var weap_def: Items.Entry = Items.fetch().set_force_locked(GM.is_daily).set_weap_use_type([Weapon.UseType.DEFENSIVE]).set_drops(Items.Drops.OFFERING).pick_random_rarity(1, fr)[0]

			var curses: Array[String] = ["Halved Curse", "Debt Curse", "Dice Bound Curse", "Unlucky Curse"]
			ig.add_single(weap_off, Mads.array_pick_random(curses, GM.random), GM.random.randi_range(3, 5))
			ig.add_single(weap_def, Mads.array_pick_random(curses, GM.random), GM.random.randi_range(3, 5))
		Type.CHEST_BOSS:
			var weap_off: Items.Entry = Items.fetch().set_force_locked(GM.is_daily).set_weap_use_type([Weapon.UseType.OFFENSIVE]).set_rarity([Items.Rarity.RARE]).pick_random(1)[0]
			var weap_def: Items.Entry = Items.fetch().set_force_locked(GM.is_daily).set_weap_use_type([Weapon.UseType.DEFENSIVE]).set_rarity([Items.Rarity.RARE]).pick_random(1)[0]
			ig.add_single(weap_off)
			ig.add_single(weap_def)



	return ig


func apply_legacy_weapon_chest(items_group: Items.Group) -> Items.Group:
	if (
		GM.is_daily
		|| MetaProgression.get_level("legendary_weapon_chest") <= 0
		|| Player.legacy_weapon_chest_claimed
	):
		return items_group

	Player.legacy_weapon_chest_claimed = true
	var legendary_group: Items.Group = Items.Group.new()
	legendary_group.add_single_array(Items.fetch().set_force_locked(true).set_type([Items.Type.WEAPON]).set_weap_use_type([Weapon.UseType.OFFENSIVE]).set_drops(Items.Drops.CHEST).set_rarity([Items.Rarity.LEGENDARY]).pick_random(1))
	legendary_group.add_single_array(Items.fetch().set_force_locked(true).set_type([Items.Type.WEAPON]).set_weap_use_type([Weapon.UseType.DEFENSIVE]).set_drops(Items.Drops.CHEST).set_rarity([Items.Rarity.LEGENDARY]).pick_random(1))
	return legendary_group
