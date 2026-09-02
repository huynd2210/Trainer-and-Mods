using UnityEngine;

namespace MagicalPrincessTrainer
{
	/// <summary>
	/// Thin, defensive access layer over the game's singletons. Everything here can be
	/// called at any time (title screen, loading, battle) and simply does nothing when the
	/// save data is not live yet.
	/// </summary>
	internal static class Game
	{
		private static MyData _data;
		private static SoundController _sound;
		private static float _nextProbe;

		/// <summary>MyData, or null. Probed at most once a second so we never spam FindObject.</summary>
		internal static MyData Data
		{
			get
			{
				if (_data != null)
				{
					return _data;
				}
				if (Time.unscaledTime < _nextProbe)
				{
					return null;
				}
				_nextProbe = Time.unscaledTime + 1f;
				_data = Object.FindFirstObjectByType<MyData>();
				return _data;
			}
		}

		/// <summary>True when a run is actually loaded and its status block is safe to touch.</summary>
		internal static bool Ready
		{
			get
			{
				MyData d = Data;
				return d != null && d.isInited && d.status != null && d.periodDataCurrent != null;
			}
		}

		internal static BasicStatusData Status
		{
			get { return Ready ? Data.status : null; }
		}

		/// <summary>
		/// The cross-run data (achievement points and their unlocks) lives outside a run, so it is
		/// editable on the title screen too - a looser gate than <see cref="Ready"/> on purpose.
		/// </summary>
		internal static bool MetaReady
		{
			get { return Data != null && Data.gstatus != null; }
		}

		internal static GrobalStatusData Global
		{
			get { return MetaReady ? Data.gstatus : null; }
		}

		/// <summary>Full recalculation (levels, derived values) + UI refresh. Silent: no level-up popups.</summary>
		internal static void Recalculate()
		{
			try
			{
				Data.UpdateStatus(true);
			}
			catch (System.Exception e)
			{
				Plugin.Log.LogWarning("UpdateStatus failed: " + e.Message);
				RefreshUI();
			}
		}

		/// <summary>Cheap UI-only refresh, for values changed every frame by the freeze toggles.</summary>
		internal static void RefreshUI()
		{
			try
			{
				NTEventDispatcher.Dispatch(GameEvent.UPDATE_STATUS);
			}
			catch (System.Exception)
			{
				// Dispatcher not up yet - nothing to refresh.
			}
		}

		internal static void Beep(SoundTypeBase type)
		{
			try
			{
				if (_sound == null)
				{
					_sound = Object.FindFirstObjectByType<SoundController>();
				}
				if (_sound != null)
				{
					_sound.Play(type);
				}
			}
			catch (System.Exception)
			{
				// Audio feedback is a nicety; never let it break a cheat.
			}
		}
	}
}
