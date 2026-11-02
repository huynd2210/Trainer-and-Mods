using System;
using System.Collections.Generic;
using System.Reflection;
using Assets.Code.Actor;
using Assets.Code.Actor.Events;
using Assets.Code.Mod;
using Assets.Code.Resource;
using Assets.Code.Run.Events;
using Assets.Code.Source;
using HarmonyLib;

namespace DD2.KillTracker
{
	/// <summary>One Harmony patch. Applying it must never throw: a missing target is logged and the rest keep working.</summary>
	internal interface IGamePatch
	{
		string Name { get; }

		/// <summary>The game method this patch hooks, or null when it cannot be found (game update).</summary>
		MethodBase ResolveTarget();

		/// <summary>The static handler methods to attach, looked up by name on the patch class.</summary>
		string PrefixName { get; }
		string PostfixName { get; }
	}

	/// <summary>
	/// The list of patches. Adding a hook is one class plus one entry here; <see cref="Apply"/> never changes.
	/// </summary>
	internal static class Patches
	{
		public static readonly IGamePatch[] All =
		{
			new KillFunnelPatch(),
			new KillEventPatch(),
			new RunStartedPatch(),
			new BaseDataGatherPatch(),
			new GroupCsvFolderPatch(),
			new SingleTypeCsvFolderPatch(),
			new ModUnloadPatch(),
		};

		public static void Apply(Harmony harmony)
		{
			foreach (IGamePatch patch in All)
			{
				try
				{
					MethodBase target = patch.ResolveTarget();
					if (target == null)
					{
						Cfg.Log.LogError("Patch '" + patch.Name + "': target method not found. The game probably changed; this feature is off.");
						continue;
					}

					HarmonyMethod prefix = Handler(patch, patch.PrefixName);
					HarmonyMethod postfix = Handler(patch, patch.PostfixName);
					harmony.Patch(target, prefix, postfix);
					Cfg.Log.LogInfo("Patched " + target.DeclaringType?.FullName + "." + target + "  (" + patch.Name + ")");
				}
				catch (Exception e)
				{
					Cfg.Log.LogError("Patch '" + patch.Name + "' failed to apply: " + e);
				}
			}
		}

		private static HarmonyMethod Handler(IGamePatch patch, string name)
		{
			if (name == null) return null;
			MethodInfo method = AccessTools.Method(patch.GetType(), name);
			if (method == null) throw new MissingMethodException(patch.GetType().Name + "." + name);
			return new HarmonyMethod(method);
		}
	}

	/// <summary>
	/// THE kill funnel. Every actor death in the game ends in <c>ActorInstance.Kill(DeathType, SourceType,
	/// IReadOnlyList&lt;string&gt;, float, IReadOnlyList&lt;uint&gt;)</c> (decomp: ActorInstance.cs:6637): lethal damage, deathblows at
	/// death's door (<c>ApplyHealthDamage</c>), DoT ticks (DotInstance.Apply -> EffectApplyCombinedResult.Apply ->
	/// ApplyHealthDamage), chain deaths, skill kills, and the end-of-round/team clean-ups all call it, including deaths on the
	/// enemy's own turn. It flips m_IsLiving off, so a living actor passes through here exactly once per life.
	/// We hook Kill itself, not only the event it raises, because Kill is too big for the runtime to inline into its callers
	/// while the tiny event helper could be inlined into Kill and silently bypass a patch.
	/// </summary>
	internal sealed class KillFunnelPatch : IGamePatch
	{
		public string Name => "kill funnel (ActorInstance.Kill)";
		public string PrefixName => nameof(OnKill);
		public string PostfixName => null;

		public MethodBase ResolveTarget() => AccessTools.Method(typeof(ActorInstance), "Kill",
			new[] { typeof(DeathType), typeof(SourceType), typeof(IReadOnlyList<string>), typeof(float), typeof(IReadOnlyList<uint>) });

		private static void OnKill(ActorInstance __instance, DeathType deathType, SourceType sourceType,
			IReadOnlyList<string> sourceIds, IReadOnlyList<uint> killingActorGuids)
		{
			Guard.Run("kill funnel", () =>
			{
				// Kill does nothing for an actor that is already dead; so do we.
				if (__instance == null || !__instance.IsLiving) return;
				KillRecorder.OnActorDeath(__instance, killingActorGuids, deathType, sourceType, sourceIds);
			});
		}
	}

	/// <summary>
	/// Safety net for the funnel: <c>EventActorDeath.Trigger</c> is the only thing <c>ActorInstance.Kill</c> calls to announce a
	/// death (it has no other caller). If the Kill hook already handled this actor the event is skipped, so a death counts once;
	/// if Kill was bypassed for any reason, the event still gets the kill counted.
	/// </summary>
	internal sealed class KillEventPatch : IGamePatch
	{
		public string Name => "kill event backup (EventActorDeath.Trigger)";
		public string PrefixName => nameof(OnTrigger);
		public string PostfixName => null;

		public MethodBase ResolveTarget() => AccessTools.Method(typeof(EventActorDeath), "Trigger",
			new[] { typeof(IReadOnlyList<uint>), typeof(uint), typeof(string), typeof(DeathType), typeof(SourceType), typeof(IReadOnlyList<string>), typeof(float), typeof(bool), typeof(int) });

		private static void OnTrigger(IReadOnlyList<uint> killingActorGuids, uint dyingActorGuid, DeathType deathType,
			SourceType sourceType, IReadOnlyList<string> sourceIds)
		{
			Guard.Run("kill event", () =>
			{
				if (KillRecorder.WasJustHandled(dyingActorGuid)) return;
				ActorInstance dying = GameBridge.FindActor(dyingActorGuid);
				if (dying != null) KillRecorder.OnActorDeath(dying, killingActorGuids, deathType, sourceType, sourceIds);
			});
		}
	}

	/// <summary>
	/// A new run starts: reset the run counters. Continuing a saved run (isRunLoad) keeps them.
	/// <c>RunBhv.StartRun</c> is the only caller of <c>EventRunStarted.Trigger</c>.
	/// </summary>
	internal sealed class RunStartedPatch : IGamePatch
	{
		public string Name => "run started (EventRunStarted.Trigger)";
		public string PrefixName => null;
		public string PostfixName => nameof(OnTrigger);

		public MethodBase ResolveTarget() => AccessTools.Method(typeof(EventRunStarted), "Trigger");

		private static void OnTrigger(bool isRunLoad)
		{
			Guard.Run("run started", () =>
			{
				KillRecorder.ResetGuard();
				if (isRunLoad || Cfg.Ledger == null) return;
				var ledger = Cfg.Ledger.EnsureProfile(GameBridge.ProfileKey());
				// The game names a run by its save folder, which does not exist yet; use our own id until it does.
				ledger.BeginRun("new-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ"), synthetic: true);
				Cfg.Info("New run started; run counters reset.");
			});
		}
	}

	/// <summary>
	/// Attribution, channel 1: after the game reads a folder's <c>*.Group.csv</c> files
	/// (<c>ResourceGroupCsvDatabase.GatherResourcesFromFolder</c>, called for the base folder, each DLC folder and each
	/// loaded mod's folder, its Overrides folder and game-type folders), note which monster ids that folder defines.
	/// </summary>
	internal sealed class GroupCsvFolderPatch : IGamePatch
	{
		public string Name => "group csv folders (ResourceGroupCsvDatabase.GatherResourcesFromFolder)";
		public string PrefixName => null;
		public string PostfixName => nameof(After);

		public MethodBase ResolveTarget() => AccessTools.Method(typeof(ResourceGroupCsvDatabase), "GatherResourcesFromFolder",
			new[] { typeof(string), typeof(int), typeof(bool), typeof(List<string>) });

		private static void After(string folder, bool overrideCSV)
		{
			Guard.Run("group csv folder", () => Attribution.OnGroupFolderRead(folder, overrideCSV));
		}
	}

	/// <summary>
	/// Attribution, channel 2: single-type files (<c>name.ActorDataClass.csv</c>) read by
	/// <c>ResourceDatabaseText.AddResourcesFromFolder</c>. The element id is the file name up to its first dot.
	/// </summary>
	internal sealed class SingleTypeCsvFolderPatch : IGamePatch
	{
		public string Name => "single-type csv folders (ResourceDatabaseText.AddResourcesFromFolder)";
		public string PrefixName => null;
		public string PostfixName => nameof(After);

		public MethodBase ResolveTarget() => AccessTools.Method(typeof(ResourceDatabaseText), "AddResourcesFromFolder", new[] { typeof(string) });

		private static void After(ResourceDatabaseText __instance, string folder)
		{
			Guard.Run("single-type csv folder", () =>
				Attribution.OnSingleTypeFolderRead(folder, __instance.m_FileTypeFilters, __instance.m_FileExtension, folder.Contains("Overrides")));
		}
	}

	/// <summary>
	/// The game re-reads its base and DLC data (<c>ResourceGroupCsvDatabase.GatherResources</c>: at start-up, and again after
	/// <c>Clear()</c> each time the main menu is entered): forget what we saw, the folders are about to be reported again.
	/// Hooked here rather than on the tiny Clear() method, which the runtime may inline at its call sites.
	/// </summary>
	internal sealed class BaseDataGatherPatch : IGamePatch
	{
		public string Name => "base data re-read (ResourceGroupCsvDatabase.GatherResources)";
		public string PrefixName => nameof(Before);
		public string PostfixName => null;

		public MethodBase ResolveTarget() => AccessTools.Method(typeof(ResourceGroupCsvDatabase), "GatherResources", Type.EmptyTypes);

		private static void Before() => Guard.Run("base data re-read", Attribution.Reset);
	}

	/// <summary>Mods are unloaded when the main menu is entered (<c>ModMgr.UnloadMods</c>); their definitions leave with them.</summary>
	internal sealed class ModUnloadPatch : IGamePatch
	{
		public string Name => "mods unloaded (ModMgr.UnloadMods)";
		public string PrefixName => null;
		public string PostfixName => nameof(After);

		public MethodBase ResolveTarget() => AccessTools.Method(typeof(ModMgr), "UnloadMods", Type.EmptyTypes);

		private static void After() => Guard.Run("mods unloaded", Attribution.Reset);
	}
}
