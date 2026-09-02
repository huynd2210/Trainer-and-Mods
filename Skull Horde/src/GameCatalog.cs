using System.Globalization;

namespace SkullHordeTrainer;

internal sealed record SkillDefinition(string Character, string Id, string Item)
{
    public string DisplayName => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Id.Replace('_', ' '));
    public override string ToString() => $"{DisplayName}  [{Id}]";
}

internal static class GameCatalog
{
    public static readonly string[] Characters = ["base", "tank", "low_tier", "yorick", "zombie"];

    private const string SkillIds = """
base:base_frenzy,base_active_ability_cooldown,base_power,base_shielded,base_vanguard_damage,base_vanguard_armor,base_vanguard_respawn,base_vanguard_cost,base_start_with_pikeman,base_start_with_tower_shield,base_player_attack_rate,base_player_attack_damage,base_player_attack_additional_bolt,base_player_attack_additional_bolt_attack_rate,base_player_attack_aoe,base_player_attack_aoe_damage,base_player_attack_deal_burn,base_player_attack_fire_tornado,endgame_damage,endgame_armor,endgame_dodge,endgame_corpuscle,endgame_money,endgame_health,endgame_attack_rate,endgame_ability_cooldown
tank:tank_heal_on_chain,tank_active_ability_cooldown,tank_invincible_on_chain,tank_max_chains,tank_chained_damage,tank_chained_attack_rate,tank_chained_crit_chance,tank_chained_armor,tank_chained_heal_per_second,tank_chained_lose_health,tank_chained_explode_on_death,tank_player_thorns,tank_thorns_knockback,tank_reduce_player_damage,tank_player_immunity,tank_deal_brittle,tank_deal_feeble,tank_deal_rooted,tank_player_ability_cooldown_per_hit,endgame_damage,endgame_armor,endgame_dodge,endgame_corpuscle,endgame_money,endgame_health,endgame_attack_rate,endgame_ability_cooldown
low_tier:low_tier_damage_from_units,low_tier_damage_from_queue,low_tier_attack_rate_from_queue,low_tier_extra_projectiles_on_unit_death,low_tier_flat_damage,low_tier_flat_armor,low_tier_flat_dodge,low_tier_reduce_unit_cost,low_tier_money_on_death,low_tier_xp_on_death,low_tier_num_units_impacted_by_player_ability,low_tier_reduce_ability_cooldown,low_tier_automatically_trigger_ability,endgame_damage,endgame_armor,endgame_dodge,endgame_corpuscle,endgame_money,endgame_health,endgame_attack_rate,endgame_ability_cooldown
yorick:yorick_num_units_impacted_by_player_ability,yorick_active_ability_cooldown,yorick_rat,yorick_bone_golem,yorick_flag_bearer,yorick_troupe_damage,yorick_troupe_dodge,yorick_troupe_crit,yorick_troupe_cost,yorick_troupe_respawn,yorick_dancer,yorick_war_drummer,yorick_jester,yorick_attack_rate,yorick_attack_damage,yorick_note_lifetime,yorick_notes_deal_brittle,yorick_notes_deal_feeble,endgame_damage,endgame_armor,endgame_dodge,endgame_corpuscle,endgame_money,endgame_health,endgame_attack_rate,endgame_ability_cooldown
zombie:zombie_num_units_impacted_by_player_ability,zombie_reduce_ability_cooldown,zombie_friendlies_start_with_rot,zombie_friendlies_rot_slower,zombie_friendlies_rot_higher_cap,zombie_zombies_deal_rot,zombie_enemies_rot_faster,zombie_psychoactive_fungus,zombie_attack_rate,zombie_attack_damage,zombie_player_rot_on_hit,zombie_flies,endgame_damage,endgame_armor,endgame_dodge,endgame_corpuscle,endgame_money,endgame_health,endgame_attack_rate,endgame_ability_cooldown
""";

    private static readonly Dictionary<string, string> ItemOverrides = new(StringComparer.Ordinal)
    {
        ["base_player_attack_additional_bolt_attack_rate"] = "base_player_attack_rate",
        ["base_player_attack_aoe_damage"] = "base_player_attack_damage",
        ["low_tier_num_units_impacted_by_player_ability"] = "num_units_impacted_by_player_ability",
        ["yorick_num_units_impacted_by_player_ability"] = "num_units_impacted_by_player_ability",
        ["yorick_active_ability_cooldown"] = "base_active_ability_cooldown",
        ["yorick_rat"] = "start_with_rat",
        ["yorick_bone_golem"] = "start_with_bone_golem",
        ["yorick_flag_bearer"] = "start_with_flag_bearer",
    };

    public static readonly IReadOnlyList<SkillDefinition> Skills = ParseSkills();

    public static readonly string[] Achievements = "tank,win_run_with_no_oaths,thespian,max_money,max_xp,lucky,top_tier_squad_shop,have_zombies,dungeon_crawler,win_run_fast,win_with_zombie,win_with_low_tier,stand_still_60,haunted,unholy_hymn,recruit_three_top_tier_rats,recruit_five_top_tier_petards,reroll_loupe,fire_tornadoes,freeze_an_enemy,plague_enemies,recruit_two_top_tier_mages,recruit_top_tier_cleric,timber,crit_kill,nimble,archer_kills,legendary_items,deadeye,get_rooted_items,get_thorns,thorns_kills,win_with_blood_bond,invulnerable_friendlies,incisive_stacks,cloaked_stacks,dodges_1000,crits_1000,burn_kills,freeze_many,plague_kills,rot_kills,many_zombies,many_oaths,powerful_melee,powerful_ranged,powerful_aoe,powerful_magic,win_endless,boss_dungeon,boss_sewer,boss_cemetery,boss_arena,boss_cathedral_of_flesh,enter_goat_cult,enter_vault,tangled,murderer,death_tome,complete_cemetery_arcane,win_run_few_deaths,chosen_one,kills_1000,kills_2000,kills_3000,cursed,hexed".Split(',');

    public static readonly string[] UnlockRewards = "tank,low_tier,yorick,cutpurse,carrion_strike,rabbits_foot,coin_pouch,zombie,rampage,rotborn,malthusian_charts,concentration,unfinished_business,understudy,infested_nest,parting_gift,appraisers_loupe,fire_mage,ice_mage,plague_mage,mage_armor,survivors_guilt,rending_swipe,sharpshooter,dancer,ballista,flag_bearer,spring_lever,druid,quill_hog,bone_golem,shallow_grave,iron_shroud,spectacles,shadow_veil,retiarius,assassin,black_ledger,necromancer".Split(',');

    private static IReadOnlyList<SkillDefinition> ParseSkills()
    {
        var result = new List<SkillDefinition>();
        foreach (string line in SkillIds.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string[] parts = line.Split(':', 2);
            foreach (string id in parts[1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                result.Add(new SkillDefinition(parts[0], id, ItemOverrides.GetValueOrDefault(id, id)));
        }
        return result;
    }
}
