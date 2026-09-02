namespace SkullHordeTrainer;

using System.Security.Cryptography;

internal static class SaveEdits
{
    public static void SelfTest(string path)
    {
        byte[] original = File.ReadAllBytes(path);
        byte[] originalHash = SHA256.HashData(original);
        DefoldSave edited = DefoldSaveCodec.Parse(original);
        UnlockEverything(edited);

        SkillDefinition skill = GameCatalog.Skills.Single(value =>
            value.Character == "tank" && value.Id == "tank_max_chains");
        SetSkillLevel(edited, skill, 12);

        DefoldSave verified = DefoldSaveCodec.Parse(DefoldSaveCodec.Serialize(edited));
        VerifyEverythingUnlocked(verified);
        LuaTable root = verified.Root.GetTable("savefile")
            ?? throw new InvalidDataException("Full-mod test lost the savefile table.");
        if (GetSkillLevel(verified, skill) != 12)
            throw new InvalidDataException("An above-cap skill level did not survive serialization.");
        if (!originalHash.AsSpan().SequenceEqual(SHA256.HashData(File.ReadAllBytes(path))))
            throw new IOException("The full-mod test unexpectedly changed the source save.");
    }

    public static void VerifyEverythingUnlocked(DefoldSave save)
    {
        LuaTable root = save.Root.GetTable("savefile")
            ?? throw new InvalidDataException("Missing savefile table.");
        LuaTable unlocks = root.GetTable("unlocks")
            ?? throw new InvalidDataException("Missing unlocks table.");
        foreach (string reward in GameCatalog.UnlockRewards)
        {
            if (unlocks.Find(reward)?.Value is not { Type: LuaType.Boolean, Value: true })
                throw new InvalidDataException($"Reward '{reward}' was not unlocked.");
        }
        foreach (string character in GameCatalog.Characters)
        {
            if (unlocks.Find(character)?.Value is not { Type: LuaType.Boolean, Value: true })
                throw new InvalidDataException($"Character '{character}' was not unlocked.");
            if (root.GetTable("character_level")?.GetNumber(character) != 60)
                throw new InvalidDataException($"Character '{character}' did not receive progression.");
            if (root.GetTable("skill_points")?.GetNumber(character) != 999_999 ||
                root.GetTable("skill_points_cumulative")?.GetNumber(character) != 999_999)
                throw new InvalidDataException($"Character '{character}' did not receive unlimited skill points.");
        }

        foreach (string achievement in GameCatalog.Achievements)
        {
            if (root.GetTable("achievements")?.Find(achievement)?.Value is not
                { Type: LuaType.Boolean, Value: true })
                throw new InvalidDataException($"Achievement '{achievement}' was not unlocked.");
        }
    }

    public static void UnlockEverything(DefoldSave save)
    {
        LuaTable root = save.Root.GetTable("savefile")
            ?? throw new InvalidDataException("Missing savefile table.");
        LuaTable unlocks = root.GetOrCreateTable("unlocks");
        unlocks.SetBooleanLeaves(true);
        foreach (string id in GameCatalog.UnlockRewards) unlocks.SetBoolean(id, true);
        foreach (string character in GameCatalog.Characters) unlocks.SetBoolean(character, true);

        LuaTable achievements = root.GetOrCreateTable("achievements");
        achievements.SetBooleanLeaves(true);
        foreach (string id in GameCatalog.Achievements) achievements.SetBoolean(id, true);
        root.GetTable("beastiary")?.SetBooleanLeaves(true);
        root.GetTable("tutorial")?.SetBooleanLeaves(true);

        LuaTable levels = root.GetOrCreateTable("character_level");
        LuaTable xp = root.GetOrCreateTable("character_xp");
        LuaTable points = root.GetOrCreateTable("skill_points");
        LuaTable cumulative = root.GetOrCreateTable("skill_points_cumulative");
        foreach (string character in GameCatalog.Characters)
        {
            levels.SetNumber(character, 60);
            xp.SetNumber(character, 0);
            points.SetNumber(character, 999_999);
            cumulative.SetNumber(character, 999_999);
        }
    }

    public static int GetSkillLevel(DefoldSave save, SkillDefinition skill)
    {
        return GetSkills(save).Entries.Count(entry => IsSkillEntry(entry, skill));
    }

    public static void SetSkillLevel(DefoldSave save, SkillDefinition skill, int level)
    {
        if (level is < 0 or > 10_000) throw new ArgumentOutOfRangeException(nameof(level));
        LuaTable skills = GetSkills(save);
        skills.Entries.RemoveAll(entry => IsSkillEntry(entry, skill));
        for (int i = 0; i < level; i++)
        {
            var item = new LuaTable();
            item.SetString("character", skill.Character);
            item.SetString("id", skill.Id);
            item.SetString("item", skill.Item);
            skills.AppendArrayTable(item);
        }
    }

    private static LuaTable GetSkills(DefoldSave save) => save.Root.GetTable("savefile")?
        .GetOrCreateTable("skills") ?? throw new InvalidDataException("Missing savefile table.");

    private static bool IsSkillEntry(LuaEntry entry, SkillDefinition skill)
    {
        if (entry.Value.Type != LuaType.Table) return false;
        LuaTable data = (LuaTable)entry.Value.Value;
        return data.Find("character")?.Value is { Type: LuaType.String } character &&
               data.Find("id")?.Value is { Type: LuaType.String } id &&
               (string)character.Value == skill.Character && (string)id.Value == skill.Id;
    }
}
