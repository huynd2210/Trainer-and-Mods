using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DD2.KillTracker.Core;

namespace DD2.KillTracker.Tests
{
	/// <summary>Tiny test runner: every test is a named lambda; a failed Assert throws; the exit code is the failure count.</summary>
	internal static class Program
	{
		private static readonly List<(string name, Action body)> Tests = new List<(string, Action)>();

		private static int Main(string[] args)
		{
			JsonTests.Register(Tests);
			LedgerTests.Register(Tests);
			StoreTests.Register(Tests);
			AttributionTests.Register(Tests);
			MiscTests.Register(Tests);

			int failed = 0;
			foreach ((string name, Action body) in Tests)
			{
				try
				{
					body();
					Console.WriteLine("PASS  " + name);
				}
				catch (Exception e)
				{
					failed++;
					Console.WriteLine("FAIL  " + name + "\n      " + e.GetType().Name + ": " + e.Message);
				}
			}

			Console.WriteLine();
			Console.WriteLine((Tests.Count - failed) + " passed, " + failed + " failed, " + Tests.Count + " total");
			return failed;
		}
	}

	internal static class Assert
	{
		public static void True(bool condition, string message = "expected true")
		{
			if (!condition) throw new Exception(message);
		}

		public static void Equal<T>(T expected, T actual, string what = "value")
		{
			if (!EqualityComparer<T>.Default.Equals(expected, actual))
				throw new Exception(what + ": expected <" + expected + "> but was <" + actual + ">");
		}

		public static void Null(object value, string what = "value")
		{
			if (value != null) throw new Exception(what + ": expected null but was <" + value + ">");
		}

		public static void NotNull(object value, string what = "value")
		{
			if (value == null) throw new Exception(what + ": expected a value but was null");
		}
	}

	/// <summary>A scratch directory removed afterwards.</summary>
	internal sealed class TempDir : IDisposable
	{
		public readonly string Path;

		public TempDir()
		{
			Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dd2kt-test-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(Path);
		}

		public string File(string name) => System.IO.Path.Combine(Path, name);

		public void Dispose()
		{
			try { Directory.Delete(Path, true); } catch (IOException) { }
		}
	}

	internal static class Make
	{
		public static KillRecord Kill(string monster, string name = null, string run = null, string t = "2026-10-06T12:00:00Z") =>
			new KillRecord { MonsterId = monster, MonsterName = name, RunId = run, TimeUtc = t, SourceType = "skill", SourceId = "slash" };
	}
}
