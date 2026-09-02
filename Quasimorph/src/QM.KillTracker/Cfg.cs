using BepInEx.Configuration;
using BepInEx.Logging;

namespace QM.KillTracker;

/// <summary>
/// Plain static holder for the settings and the logger.
///
/// Quasimorph destroys BepInEx's plugin component shortly after startup, so nothing may reach the
/// settings through that MonoBehaviour: `plugin != null` is a Unity null check and would report
/// false from then on. ConfigEntry and ManualLogSource are ordinary managed objects and stay valid.
/// </summary>
internal static class Cfg
{
	public static ManualLogSource Log;

	public static ConfigEntry<KeyboardShortcut> ToggleKey;
	public static ConfigEntry<float> SaveIntervalSeconds;
	public static ConfigEntry<bool> TrackAllyKills;

	public static bool Ready;
}
