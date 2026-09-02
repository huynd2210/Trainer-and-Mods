using System;
using BepInEx.Configuration;

namespace MagicalPrincessTrainer
{
	/// <summary>
	/// One trainer entry: a label, the key that fires it, and what it does. The plugin only
	/// ever iterates this list - adding a cheat means adding an entry, never editing a branch.
	/// </summary>
	internal interface ITrainerCommand
	{
		string Label { get; }
		ConfigEntry<KeyboardShortcut> Key { get; }

		/// <summary>"ON"/"OFF" for toggles, null for one-shot actions.</summary>
		string State { get; }

		void Invoke();
	}

	/// <summary>A one-shot cheat. The action returns false when there is no live save to edit.</summary>
	internal sealed class ActionCommand : ITrainerCommand
	{
		private readonly Func<bool> _action;

		internal ActionCommand(string label, ConfigEntry<KeyboardShortcut> key, Func<bool> action)
		{
			Label = label;
			Key = key;
			_action = action;
		}

		public string Label { get; }
		public ConfigEntry<KeyboardShortcut> Key { get; }
		public string State => null;

		public void Invoke()
		{
			if (_action())
			{
				Plugin.Toast(Label);
			}
			else
			{
				Plugin.Toast("No save loaded");
			}
		}
	}

	/// <summary>An on/off cheat. State lives in the config file, so it survives a restart.</summary>
	internal sealed class ToggleCommand : ITrainerCommand
	{
		private readonly ConfigEntry<bool> _state;
		private readonly Action<bool> _onChanged;

		internal ToggleCommand(string label, ConfigEntry<KeyboardShortcut> key, ConfigEntry<bool> state,
			Action<bool> onChanged = null)
		{
			Label = label;
			Key = key;
			_state = state;
			_onChanged = onChanged;
		}

		public string Label { get; }
		public ConfigEntry<KeyboardShortcut> Key { get; }
		public string State => _state.Value ? "ON" : "OFF";

		public bool Value => _state.Value;

		public void Invoke()
		{
			_state.Value = !_state.Value;
			_onChanged?.Invoke(_state.Value);
			Game.Beep(SoundType.UI_SWITCH);
			Plugin.Toast(Label + ": " + State);
		}
	}
}
