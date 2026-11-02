using System;
using System.IO;
using System.Text;

namespace DD2.KillTracker.Core
{
	/// <summary>
	/// Reads and writes ledger files. Writes go to a temp file that then replaces the real one (the previous
	/// version is kept as <c>.bak</c>); a file that cannot be parsed is moved aside, never overwritten.
	/// </summary>
	internal sealed class LedgerStore
	{
		private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

		private readonly string _directory;
		private readonly Action<string> _warn;
		private readonly Action<string> _info;

		public LedgerStore(string directory, Action<string> info, Action<string> warn)
		{
			_directory = directory;
			_info = info ?? (_ => { });
			_warn = warn ?? (_ => { });
		}

		public string Directory => _directory;

		public string PathFor(string profileKey) => System.IO.Path.Combine(_directory, "ledger_" + SafeName(profileKey) + ".json");

		/// <summary>Profile keys become file names: keep letters, digits, '-' and '_', replace everything else.</summary>
		public static string SafeName(string key)
		{
			if (string.IsNullOrEmpty(key)) return "default";
			var sb = new StringBuilder(key.Length);
			foreach (char c in key)
			{
				sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
			}
			return sb.ToString();
		}

		/// <summary>Loads the ledger for a profile. Never returns null and never throws.</summary>
		public KillLedger Load(string profileKey)
		{
			string path = PathFor(profileKey);
			string bak = path + ".bak";
			try
			{
				if (File.Exists(path))
				{
					string text;
					try
					{
						text = File.ReadAllText(path, Utf8NoBom);
					}
					catch (Exception e)
					{
						// Unreadable (locked, permissions) is not the same as corrupt: do not touch the file, and do
						// not let a later save replace it with an empty ledger.
						_warn("Could not read " + path + ": " + e.Message + ". Kills this session will not be saved over it.");
						return new KillLedger { ProfileId = profileKey, PersistBlocked = true };
					}

					KillLedger ledger = KillLedger.Deserialize(text);
					if (ledger != null)
					{
						ledger.ProfileId = profileKey;
						return ledger;
					}

					string aside = path + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
					File.Move(path, aside);
					_warn("Ledger file was not valid JSON and has been kept as " + aside + ".");
				}
			}
			catch (Exception e)
			{
				_warn("Ledger load failed: " + e.Message);
				return new KillLedger { ProfileId = profileKey, PersistBlocked = true };
			}

			// Main file missing or just moved aside: fall back to the backup when it is good.
			try
			{
				if (File.Exists(bak))
				{
					KillLedger fromBak = KillLedger.Deserialize(File.ReadAllText(bak, Utf8NoBom));
					if (fromBak != null)
					{
						fromBak.ProfileId = profileKey;
						fromBak.Dirty = true; // write the restored data back as the main file on the next flush
						_warn("Restored the ledger from " + bak + ".");
						return fromBak;
					}
					_warn("Backup " + bak + " is not valid either; starting from an empty ledger.");
				}
			}
			catch (Exception e)
			{
				_warn("Backup read failed: " + e.Message);
			}

			return new KillLedger { ProfileId = profileKey };
		}

		/// <summary>Atomically writes the ledger. Returns false (and logs) on failure; the ledger stays dirty.</summary>
		public bool Save(KillLedger ledger)
		{
			if (ledger == null || ledger.PersistBlocked)
			{
				return false;
			}

			string path = PathFor(ledger.ProfileId);
			string tmp = path + ".tmp";
			try
			{
				System.IO.Directory.CreateDirectory(_directory);
				File.WriteAllText(tmp, ledger.Serialize(), Utf8NoBom);

				if (File.Exists(path))
				{
					try
					{
						File.Replace(tmp, path, path + ".bak");
					}
					catch (Exception)
					{
						// File.Replace can fail on exotic file systems or while a scanner holds the file. Fall back to
						// copy-then-move, which keeps a backup but is not a single atomic step.
						File.Copy(path, path + ".bak", true);
						File.Delete(path);
						File.Move(tmp, path);
					}
				}
				else
				{
					File.Move(tmp, path);
				}

				ledger.Dirty = false;
				return true;
			}
			catch (Exception e)
			{
				_warn("Could not save " + path + ": " + e.Message);
				return false;
			}
		}
	}

	/// <summary>
	/// Owns the ledger of the profile currently in use and the flush policy. Switching profile flushes the old
	/// ledger first, so nothing is lost and nothing is written into the wrong file.
	/// </summary>
	internal sealed class LedgerManager
	{
		private readonly LedgerStore _store;
		private int _maxRecords;

		public LedgerManager(LedgerStore store, int maxRecords)
		{
			_store = store;
			_maxRecords = maxRecords;
		}

		public KillLedger Current { get; private set; }
		public string CurrentProfileKey { get; private set; }

		public void SetMaxRecords(int max)
		{
			_maxRecords = max;
			if (Current != null) Current.MaxRecords = max;
		}

		/// <summary>Makes <paramref name="profileKey"/> the active profile, loading its ledger. No-op when unchanged.</summary>
		public KillLedger EnsureProfile(string profileKey)
		{
			string key = string.IsNullOrEmpty(profileKey) ? "default" : profileKey;
			if (Current != null && key == CurrentProfileKey)
			{
				return Current;
			}

			Flush();
			Current = _store.Load(key);
			Current.MaxRecords = _maxRecords;
			CurrentProfileKey = key;
			return Current;
		}

		/// <summary>Writes the current ledger if it has unsaved changes. Returns true when a file was written.</summary>
		public bool Flush()
		{
			if (Current == null || !Current.Dirty)
			{
				return false;
			}
			return _store.Save(Current);
		}
	}
}
