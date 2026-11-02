using System;
using System.Collections.Generic;

namespace DD2.KillTracker.Core
{
	/// <summary>
	/// The counting model for one DD2 save profile: exact per-monster aggregates (lifetime and current run)
	/// plus a capped log of individual kills. Knows nothing about Unity or the game, so it can be unit-tested.
	/// </summary>
	internal sealed class KillLedger
	{
		public const int FormatVersion = 1;
		public const int DefaultMaxRecords = 5000;

		private readonly Dictionary<string, MonsterStats> _monsters = new Dictionary<string, MonsterStats>(StringComparer.Ordinal);
		private readonly List<KillRecord> _kills = new List<KillRecord>();
		private int _maxRecords = DefaultMaxRecords;

		public string ProfileId { get; set; }

		/// <summary>The run the "run" counters belong to; null until the game reports one.</summary>
		public string RunId { get; private set; }

		public long LifetimeTotal { get; private set; }
		public long RunTotal { get; private set; }

		/// <summary>
		/// True when the run id was made up by us at "run started" (the game had no id yet). A real game id that
		/// shows up later is adopted without resetting the run counters.
		/// </summary>
		public bool RunIdSynthetic { get; private set; }

		/// <summary>True when there are changes that have not been written to disk.</summary>
		public bool Dirty { get; set; }

		/// <summary>Set when the existing file could not be read; saving is refused so it is never overwritten.</summary>
		public bool PersistBlocked { get; set; }

		/// <summary>Kills dropped from the raw log because of the cap. The aggregates still include them.</summary>
		public long DroppedRecords { get; private set; }

		public IReadOnlyCollection<MonsterStats> Monsters => _monsters.Values;
		public IReadOnlyList<KillRecord> Kills => _kills;

		/// <summary>Bumped on every change so the UI can cheaply tell when to rebuild its rows.</summary>
		public int Revision { get; private set; }

		public int MaxRecords
		{
			get => _maxRecords;
			set
			{
				_maxRecords = Math.Max(0, value);
				Trim();
			}
		}

		public MonsterStats Find(string monsterId) =>
			monsterId != null && _monsters.TryGetValue(monsterId, out MonsterStats s) ? s : null;

		/// <summary>
		/// Starts a new run: the run counters go back to zero. A repeat call with the same id does nothing, so
		/// the game firing "run started" and a kill record carrying the same id cannot double-reset.
		/// </summary>
		public void BeginRun(string runId, bool synthetic = false)
		{
			if (string.Equals(RunId, runId, StringComparison.Ordinal))
			{
				return;
			}

			RunId = runId;
			RunIdSynthetic = synthetic;
			RunTotal = 0;
			foreach (MonsterStats m in _monsters.Values)
			{
				m.Run = 0;
			}
			Touch();
		}

		/// <summary>
		/// Called with the id the game reports for the current run. A different id starts a new run, except that an
		/// id we invented earlier is replaced by the real one without clearing the counters.
		/// </summary>
		public void ObserveRunId(string gameRunId)
		{
			if (string.IsNullOrEmpty(gameRunId) || string.Equals(RunId, gameRunId, StringComparison.Ordinal))
			{
				return;
			}

			if (RunIdSynthetic)
			{
				RunId = gameRunId;
				RunIdSynthetic = false;
				Touch();
				return;
			}

			BeginRun(gameRunId);
		}

		/// <summary>Refreshes the source/name of a monster we have already seen, without counting a kill.</summary>
		public void UpdateSource(string monsterId, SourceInfo source)
		{
			MonsterStats m = Find(monsterId);
			if (m == null || source == null || m.Source.SameAs(source))
			{
				return;
			}
			m.Source = source;
			Touch();
		}

		public void Record(KillRecord rec, SourceInfo source)
		{
			if (rec == null || string.IsNullOrEmpty(rec.MonsterId))
			{
				return;
			}

			ObserveRunId(rec.RunId);

			if (!_monsters.TryGetValue(rec.MonsterId, out MonsterStats m))
			{
				m = new MonsterStats { Id = rec.MonsterId, FirstKillUtc = rec.TimeUtc };
				_monsters[rec.MonsterId] = m;
			}

			m.Lifetime++;
			m.Run++;
			m.LastKillUtc = rec.TimeUtc;
			if (!string.IsNullOrEmpty(rec.MonsterName))
			{
				m.Name = rec.MonsterName;
			}
			if (source != null)
			{
				m.Source = source;
				rec.ContentSource = source.Label;
			}

			LifetimeTotal++;
			RunTotal++;

			if (_maxRecords > 0)
			{
				_kills.Add(rec);
				Trim();
			}
			else
			{
				DroppedRecords++;
			}

			Touch();
		}

		private void Trim()
		{
			int excess = _kills.Count - _maxRecords;
			if (excess > 0)
			{
				_kills.RemoveRange(0, excess);
				DroppedRecords += excess;
			}
		}

		private void Touch()
		{
			Dirty = true;
			Revision++;
		}

		// ------------------------------------------------------------------ persistence

		public string Serialize()
		{
			var monsters = new Dictionary<string, object>();
			foreach (MonsterStats m in _monsters.Values)
			{
				var o = new Dictionary<string, object>
				{
					["lifetime"] = m.Lifetime,
					["run"] = m.Run,
					["source"] = m.Source.ToJson(),
				};
				if (m.Name != null) o["name"] = m.Name;
				if (m.FirstKillUtc != null) o["first"] = m.FirstKillUtc;
				if (m.LastKillUtc != null) o["last"] = m.LastKillUtc;
				monsters[m.Id] = o;
			}

			var kills = new List<object>(_kills.Count);
			foreach (KillRecord k in _kills)
			{
				kills.Add(k.ToJson());
			}

			var root = new Dictionary<string, object>
			{
				["version"] = (long)FormatVersion,
				["profileId"] = ProfileId,
				["runId"] = RunId,
				["runIdSynthetic"] = RunIdSynthetic,
				["totals"] = new Dictionary<string, object> { ["lifetime"] = LifetimeTotal, ["run"] = RunTotal },
				["droppedRecords"] = DroppedRecords,
				["monsters"] = monsters,
				["kills"] = kills,
			};
			return Json.Write(root);
		}

		/// <summary>Parses a ledger file. Returns null (never throws) when the text is not a valid ledger.</summary>
		public static KillLedger Deserialize(string text)
		{
			if (!Json.TryParse(text, out object parsed) || !(parsed is Dictionary<string, object> root))
			{
				return null;
			}

			// A ledger must at least declare its version and a monsters object; anything else is not ours.
			if (Json.Long(root, "version", -1) < 1 || Json.Obj(root, "monsters") == null)
			{
				return null;
			}

			var ledger = new KillLedger
			{
				ProfileId = Json.Str(root, "profileId"),
				RunId = Json.Str(root, "runId"),
				RunIdSynthetic = Json.Bool(root, "runIdSynthetic"),
				DroppedRecords = Json.Long(root, "droppedRecords"),
			};

			long sumLifetime = 0, sumRun = 0;
			foreach (KeyValuePair<string, object> kv in Json.Obj(root, "monsters"))
			{
				if (!(kv.Value is Dictionary<string, object> o))
				{
					return null;
				}

				var m = new MonsterStats
				{
					Id = kv.Key,
					Name = Json.Str(o, "name"),
					Lifetime = Math.Max(0, Json.Long(o, "lifetime")),
					Run = Math.Max(0, Json.Long(o, "run")),
					FirstKillUtc = Json.Str(o, "first"),
					LastKillUtc = Json.Str(o, "last"),
					Source = SourceInfo.FromJson(Json.Obj(o, "source")),
				};
				ledger._monsters[m.Id] = m;
				sumLifetime += m.Lifetime;
				sumRun += m.Run;
			}

			// The per-monster counts are the source of truth; the stored totals are a convenience copy.
			ledger.LifetimeTotal = sumLifetime;
			ledger.RunTotal = sumRun;

			List<object> kills = Json.Arr(root, "kills");
			if (kills != null)
			{
				foreach (object item in kills)
				{
					KillRecord rec = KillRecord.FromJson(item as Dictionary<string, object>);
					if (rec != null)
					{
						ledger._kills.Add(rec);
					}
				}
			}

			return ledger;
		}
	}

	/// <summary>
	/// Remembers the last few kill keys (one per actor instance) so the same death reported by two different
	/// hooks counts once. Bounded: old keys fall out, which is fine because a death is reported within moments.
	/// </summary>
	internal sealed class DeathGuard
	{
		private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
		private readonly Queue<string> _order = new Queue<string>();
		private readonly int _capacity;

		public DeathGuard(int capacity = 512)
		{
			_capacity = Math.Max(1, capacity);
		}

		/// <summary>True the first time a key is offered, false for repeats.</summary>
		public bool FirstTime(string key)
		{
			if (key == null)
			{
				return true;
			}
			if (!_seen.Add(key))
			{
				return false;
			}
			_order.Enqueue(key);
			while (_order.Count > _capacity)
			{
				_seen.Remove(_order.Dequeue());
			}
			return true;
		}

		public void Clear()
		{
			_seen.Clear();
			_order.Clear();
		}
	}
}
