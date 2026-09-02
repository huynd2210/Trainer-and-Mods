using BepInEx.Configuration;
using BepInEx.Logging;

namespace QM.MapReveal;

/// <summary>
/// Plain static holder for the settings and the logger.
///
/// The Harmony patches must NOT reach these through the plugin's MonoBehaviour. `plugin != null`
/// is a Unity null check, and it evaluates to false the moment the underlying component is
/// destroyed — which would silently switch every patch off while leaving the mod looking loaded.
/// ConfigEntry and ManualLogSource are ordinary managed objects with no such trapdoor.
/// </summary>
internal static class Cfg
{
	public static ManualLogSource Log;

	public static ConfigEntry<bool> Enabled;
	public static ConfigEntry<bool> ShowExits;
	public static ConfigEntry<bool> ShowObjectives;
	public static ConfigEntry<KeyboardShortcut> ToggleKey;
	public static ConfigEntry<bool> VerboseLogging;

	/// <summary>True once Awake has bound everything; guards against patches firing mid-startup.</summary>
	public static bool Ready;

	public static bool Active => Ready && Enabled.Value;

	public static bool Verbose => Ready && VerboseLogging.Value;
}
