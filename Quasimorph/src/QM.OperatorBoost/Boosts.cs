using System.Collections.Generic;
using BepInEx.Configuration;

namespace QM.OperatorBoost;

/// <summary>
/// One configurable flat bonus. Everything the mod adds goes through one of these, so the
/// status overlay and the startup log can enumerate the boosts instead of restating them.
///
/// <see cref="Value"/> already folds in the master switch: when the mod is off every boost
/// reports zero, which is what makes the patches one-liners and what makes the max-health
/// refresh put health back exactly where the unmodded game would have it.
/// </summary>
internal abstract class Boost
{
	protected Boost(string key, string unit)
	{
		Key = key;
		Unit = unit;
	}

	public string Key { get; }

	/// <summary>Suffix used when printing the value, e.g. "%" or "" or " HP".</summary>
	public string Unit { get; }

	public abstract bool IsZero { get; }

	/// <summary>The configured amount as the player typed it, for display only.</summary>
	public abstract string Display { get; }
}

internal sealed class IntBoost : Boost
{
	private readonly ConfigEntry<int> _entry;

	public IntBoost(ConfigFile config, string section, string key, int defaultValue, string description,
		int min, int max, string unit = "")
		: base(key, unit)
	{
		_entry = config.Bind(section, key, defaultValue,
			new ConfigDescription(description, new AcceptableValueRange<int>(min, max)));
		Boosts.Register(this);
	}

	/// <summary>Zero while the mod is disabled, so no patch needs its own guard.</summary>
	public int Value => Cfg.Active ? _entry.Value : 0;

	public int Configured => _entry.Value;

	public override bool IsZero => Configured == 0;

	public override string Display => $"{Configured:+0;-0;0}{Unit}";
}

internal sealed class FloatBoost : Boost
{
	private readonly ConfigEntry<float> _entry;
	private readonly float _scale;

	/// <param name="scale">
	/// Divides the configured number on the way in. Stats the game keeps as a 0..1 fraction are
	/// configured in percentage points — the same units the operator screen prints — so they use
	/// a scale of 100.
	/// </param>
	public FloatBoost(ConfigFile config, string section, string key, float defaultValue, string description,
		float min, float max, float scale = 1f, string unit = "")
		: base(key, unit)
	{
		_entry = config.Bind(section, key, defaultValue,
			new ConfigDescription(description, new AcceptableValueRange<float>(min, max)));
		_scale = scale;
		Boosts.Register(this);
	}

	/// <summary>Zero while the mod is disabled, so no patch needs its own guard.</summary>
	public float Value => Cfg.Active ? _entry.Value / _scale : 0f;

	public float Configured => _entry.Value;

	public override bool IsZero => Configured == 0f;

	public override string Display => $"{Configured:+0.##;-0.##;0}{Unit}";
}

/// <summary>
/// The full set of stats this mod can boost. Every one of them is added on top of whatever the
/// game already computed — base profile value, perks, implants, augmentations, wounds and buffs
/// all still apply; nothing here replaces a stat.
/// </summary>
internal static class Boosts
{
	private static readonly List<Boost> _all = new List<Boost>();

	public static IReadOnlyList<Boost> All => _all;

	internal static void Register(Boost boost) => _all.Add(boost);

	// --- survivability ---------------------------------------------------------
	public static IntBoost MaxHealth;
	public static FloatBoost AllResists;
	public static FloatBoost Dodge;
	public static IntBoost HealthRegenPerTurn;
	public static IntBoost PainRegen;

	// --- offence ---------------------------------------------------------------
	public static FloatBoost MeleeAccuracy;
	public static FloatBoost RangeAccuracy;
	public static FloatBoost CritChance;
	public static FloatBoost CritDamage;
	public static FloatBoost ArmorPenetration;
	public static FloatBoost MeleeDamage;
	public static FloatBoost RangeDamage;
	public static IntBoost MeleeFlatDamage;

	// --- utility ---------------------------------------------------------------
	public static IntBoost ActionPoints;
	public static IntBoost SightRange;
	public static IntBoost FirearmRange;

	// --- inventory -------------------------------------------------------------
	public static IntBoost BackpackWidth;
	public static IntBoost BackpackHeight;
	public static IntBoost VestSlots;

	public static void Bind(ConfigFile config)
	{
		const string survivability = "2. Survivability";
		const string offence = "3. Offence";
		const string utility = "4. Utility";
		const string inventory = "5. Inventory";

		MaxHealth = new IntBoost(config, survivability, "MaxHealth", 0,
			"Extra maximum hit points, added on top of the operator's own health, implants and wounds.",
			-500, 5000, " HP");

		AllResists = new FloatBoost(config, survivability, "AllResists", 0f,
			"Extra resistance points against every damage type, added on top of armour and perks. "
			+ "These are the same points the operator screen shows per damage type, not a percentage: "
			+ "the game runs them through its own diminishing-returns curve.",
			-100f, 500f);

		Dodge = new FloatBoost(config, survivability, "DodgeChance", 0f,
			"Extra dodge chance in percentage points. 10 means +10% on top of the operator's own dodge.",
			-100f, 100f, 100f, "%");

		HealthRegenPerTurn = new IntBoost(config, survivability, "HealthRegenPerTurn", 0,
			"Hit points regenerated at the start of each of your turns, added to any regeneration "
			+ "the operator already has. Only applies in a raid.",
			-50, 500, " HP/turn");

		PainRegen = new IntBoost(config, survivability, "PainRegen", 0,
			"Extra pain threshold recovered per turn, added to the operator's own pain regeneration.",
			-50, 500);

		MeleeAccuracy = new FloatBoost(config, offence, "MeleeAccuracy", 0f,
			"Extra melee accuracy in percentage points, added after the game has applied the weapon, "
			+ "wounds and movement penalties. Ignored while an effect has zeroed your accuracy outright.",
			-100f, 200f, 100f, "%");

		RangeAccuracy = new FloatBoost(config, offence, "RangeAccuracy", 0f,
			"Extra ranged accuracy in percentage points, added after the game has applied the weapon, "
			+ "wounds and movement penalties. Ignored while an effect has zeroed your accuracy outright.",
			-100f, 200f, 100f, "%");

		CritChance = new FloatBoost(config, offence, "CritChance", 0f,
			"Extra critical hit chance in percentage points. Applies to melee, ranged and thrown attacks. "
			+ "The game still caps the final chance at 100%.",
			-100f, 100f, 100f, "%");

		CritDamage = new FloatBoost(config, offence, "CritDamage", 0f,
			"Extra critical damage in percentage points, added to the operator's own crit damage bonus.",
			-100f, 500f, 100f, "%");

		ArmorPenetration = new FloatBoost(config, offence, "ArmorPenetration", 0f,
			"Extra armour penetration in percentage points, added to whatever the weapon and ammunition "
			+ "already penetrate.",
			-100f, 100f, 100f, "%");

		MeleeDamage = new FloatBoost(config, offence, "MeleeDamage", 0f,
			"Extra melee damage in percentage points. This is the same additive bonus perks grant, so 25 "
			+ "means +25% melee damage on top of your perks rather than a separate multiplier.",
			-100f, 500f, 100f, "%");

		RangeDamage = new FloatBoost(config, offence, "RangeDamage", 0f,
			"Extra ranged damage in percentage points. This is the same additive bonus perks grant, so 25 "
			+ "means +25% ranged damage on top of your perks rather than a separate multiplier.",
			-100f, 500f, 100f, "%");

		MeleeFlatDamage = new IntBoost(config, offence, "MeleeFlatDamage", 0,
			"Flat damage added to every melee hit, before resistances.",
			-50, 500);

		ActionPoints = new IntBoost(config, utility, "ActionPoints", 0,
			"Extra action points per turn, added to the operator's own AP. Applies to every movement "
			+ "stance. Large values make turns very long; 1 or 2 is already a big change.",
			-5, 20, " AP");

		SightRange = new IntBoost(config, utility, "SightRange", 0,
			"Extra sight radius in tiles, added to the operator's own line of sight. Blindness still "
			+ "overrides this.",
			-10, 40, " tiles");

		FirearmRange = new IntBoost(config, utility, "FirearmRange", 0,
			"Extra effective range in tiles for firearms, added to the weapon's own range before "
			+ "damage falloff is worked out.",
			-10, 40, " tiles");

		// Unlike every other boost, these change saved state: a grid's size is stored, and so are
		// the item positions inside it. Read the note in the README before lowering them.
		BackpackWidth = new IntBoost(config, inventory, "BackpackWidth", 0,
			"Extra backpack columns, added to whatever backpack the operator has equipped (or to "
			+ "their bare-backed default when they have none). Unlike the other boosts this is "
			+ "written into your save, and the mod will not shrink a grid that still has items in "
			+ "it — empty the backpack first if you lower this.",
			-6, 12, " cols");

		BackpackHeight = new IntBoost(config, inventory, "BackpackHeight", 0,
			"Extra backpack rows, on the same terms as BackpackWidth.",
			-6, 12, " rows");

		VestSlots = new IntBoost(config, inventory, "VestSlots", 0,
			"Extra vest slots, added on top of the equipped vest and any perk or wound bonuses. "
			+ "Same save-state caveat as BackpackWidth.",
			-6, 12, " slots");
	}

	/// <summary>The boosts the player has actually set, for the overlay and the startup log.</summary>
	public static List<string> ActiveSummary()
	{
		var result = new List<string>();
		foreach (Boost boost in _all)
		{
			if (!boost.IsZero)
			{
				result.Add($"{boost.Key} {boost.Display}");
			}
		}
		return result;
	}
}
