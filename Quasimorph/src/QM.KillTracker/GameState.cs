using System;
using System.Reflection;
using HarmonyLib;
using MGSC;

namespace QM.KillTracker;

/// <summary>
/// Reaches the game's DI container, which lives in a private static field on <see cref="Bootstrap"/>.
/// Everything here is null-tolerant: the state objects only exist once a campaign is loaded, and
/// they are replaced wholesale when a save is loaded, so nothing may be cached across calls.
/// </summary>
internal static class GameState
{
	private static FieldInfo _stateField;
	private static bool _lookupFailed;

	private static State Root
	{
		get
		{
			if (_lookupFailed)
			{
				return null;
			}
			try
			{
				_stateField ??= AccessTools.Field(typeof(Bootstrap), "_state");
				if (_stateField == null)
				{
					_lookupFailed = true;
					Cfg.Log.LogError("Could not find Bootstrap._state; the game version may have changed.");
					return null;
				}
				return _stateField.GetValue(null) as State;
			}
			catch (Exception e)
			{
				_lookupFailed = true;
				Cfg.Log.LogError("Failed to read Bootstrap._state: " + e);
				return null;
			}
		}
	}

	/// <summary>Returns the live registered instance of <typeparamref name="T"/>, or null if there is no campaign loaded.</summary>
	public static T Get<T>() where T : class
	{
		try
		{
			return Root?.Get<T>();
		}
		catch (Exception e)
		{
			Cfg.Log.LogError($"Failed to resolve {typeof(T).Name}: {e}");
			return null;
		}
	}
}
