namespace pitTeam.Patches {
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppSystem;
using Il2CppSystem.Threading;
internal class CancelToken : Object, ITokenGetter, IIl2CppObjectBase
{
	private bool cancelled;

	public CancelToken()
	{
		cancelled = false;
	}

	public CancellationToken GetCancelToken()
	{
		return new CancellationToken(cancelled);
	}

	public void Cancel()
	{
		cancelled = true;
	}
}
}

namespace pitTeam.Patches {
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.Jobs;
using EFT;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Il2CppSystem.Diagnostics;
using Il2CppSystem.Threading;
using Il2CppSystem.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SPTushonka.Common.Http;
using SPTushonka.Reflection.Patching;
using UnityEngine;
using pitTeam.Compat;
using pitTeam.Components;
using pitTeam.Modules;
using pitTeam.Utils;
internal class BotsControllerPatch : ModulePatch
{
	private sealed class NormalBossLifecyclePairProgress
	{
		public int RaidInitializationStage;

		public string LocationId;

		public bool FactoryDistanceProfile;

		public pitAIBossPlayer PlayerBoss;

		public bool SpawnedPlayerAppended;

		public bool PrefetchAttempted;

		public bool PrefetchCompleted;
	}

	public static BotsControllerPatch Instance;

	public static BotsController Controller = null;

	public static int NormalAddActivePlayerPostfixCalls;

	public static int NormalAddActivePlayerCompletedCalls;

	public static int NormalAddActivePlayerErrors;

	public static string NormalAddActivePlayerStage = "not-observed";

	public static string NormalAddActivePlayerFirstError;

	private static int _normalAddActivePlayerEntryLogged;

	private static int _normalAddActivePlayerFirstErrorLogged;

	public static System.Collections.Generic.List<pitAIBossPlayer> spawnedPlayers = new System.Collections.Generic.List<pitAIBossPlayer>();

	public static System.Collections.Generic.Dictionary<string, System.Threading.Tasks.Task<System.Collections.Generic.Dictionary<string, Profile>>> followerCreationTask;

	public static System.Collections.Generic.Dictionary<string, System.Threading.Tasks.Task<BotCreationData>> alliesCreationTask;

	private static int NormalFollowerFetchCalls;

	private static int NormalFollowerTransitCacheHits;

	private static int NormalFollowerBackendStarted;

	private static int NormalFollowerBackendResponses;

	private static int NormalFollowerBackendFailures;

	private static int NormalFollowerDecoded;

	private static int NormalFollowerErrors;

	private static int NormalFollowerCanceled;
	private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Threading.Tasks.TaskCompletionSource<bool>, byte> PendingNormalFollowerInitializations = new System.Collections.Concurrent.ConcurrentDictionary<System.Threading.Tasks.TaskCompletionSource<bool>, byte>();
	public static void CancelPendingNormalFollowerInitializations()
	{
		foreach (System.Threading.Tasks.TaskCompletionSource<bool> pending in PendingNormalFollowerInitializations.Keys)
		{
			pending.TrySetCanceled();
		}
		PendingNormalFollowerInitializations.Clear();
	}

	private static int NormalFollowerSpawnTasksStarted;

	private static int NormalFollowerSpawnTasksCompleted;

	private static int NormalFollowerSpawnTasksFaulted;

	private static int NormalFollowerSpawnTasksCanceled;

	private static string NormalFollowerFetchStage = "not-observed";

	private static string NormalFollowerFetchStatus = "not-observed";

	private static string NormalFollowerFetchEndpoint;

	private static string NormalFollowerFetchFirstError;

	private static string NormalFollowerFetchLastError;

	private static string NormalFollowerSpawnTaskStatus = "not-observed";

	private static string NormalFollowerSpawnTaskFirstError;

	private static string NormalFollowerSpawnTaskLastError;

	public static System.Collections.Generic.Dictionary<WildSpawnType, System.Threading.Tasks.Task<BotCreationData>> bossCreationTask;

	private static readonly NormalBossLifecyclePairGate NormalBossLifecycleGate = new NormalBossLifecyclePairGate();

	private static readonly System.Collections.Generic.Dictionary<string, NormalBossLifecyclePairProgress> NormalBossLifecycleProgressByPair = new System.Collections.Generic.Dictionary<string, NormalBossLifecyclePairProgress>(System.StringComparer.Ordinal);

	private static LocalGame NormalObservedLocalGame;

	private static BotsController NormalObservedController;

	private static System.IntPtr NormalObservedLocalGamePointer = System.IntPtr.Zero;

	private static System.IntPtr NormalObservedControllerPointer = System.IntPtr.Zero;

	private static int NormalLocalGameTaskCallbacks;

	private static int NormalLocalGameTaskCompleted;

	private static int NormalLocalGameTaskCanceled;

	private static int NormalLocalGameTaskErrors;

	private static bool NormalLocalGameTaskReady;

	private static int NormalGameWorldStartedCallbacks;

	private static int NormalGameWorldStartedSpawnRequested;

	private static int NormalBossRegistrationCalls;

	private static int NormalBossRegistrationSuccesses;

	private static int NormalBossRegistrationDuplicates;

	private static int NormalBossRegistrationErrors;

	private static System.IntPtr NormalGameWorldStartedPointer = System.IntPtr.Zero;

	private static string NormalBossLifecycleStatus = "not-observed";

	private static string NormalBossLifecycleLastStatus = "not-observed";

	private static string NormalBossLifecycleLastPair;

	private static string NormalBossLifecycleFirstError;

	private static string NormalBossLifecycleLastError;

	private static string NormalBossLifecycleCurrentStage;

	private static string SanitizeBackendAuthority(string value)
	{
		if (!System.Uri.TryCreate(value, System.UriKind.Absolute, out System.Uri uri) || string.IsNullOrWhiteSpace(uri.Host))
		{
			return "unavailable";
		}
		return uri.Scheme.ToLowerInvariant() + "://" + uri.Host + ":" + uri.Port;
	}

	private static void RecordNormalFollowerFetchStage(string stage, string backendBase = null)
	{
		System.Threading.Interlocked.Exchange(ref NormalFollowerFetchStage, stage);
		string text;
		switch (stage)
		{
		default:
			text = "running";
			break;
		case "profile-complete":
		case "transit-cache-completed":
			text = "completed";
			break;
		case "decode-profile-response":
			text = "decoding";
			break;
		case "backend-response-received":
			text = "response-received";
			break;
		case "backend-request-await":
			text = "pending";
			break;
		}
		string status = text;
		System.Threading.Interlocked.Exchange(ref NormalFollowerFetchStatus, status);
		if (!string.IsNullOrWhiteSpace(backendBase))
		{
			System.Threading.Interlocked.Exchange(ref NormalFollowerFetchEndpoint, SanitizeBackendAuthority(backendBase));
		}
	}

	private static void RecordNormalFollowerFetchFailure(string stage, System.Exception error)
	{
		string message = stage + ": " + error.GetType().FullName;
		if (stage == "backend-request-await")
		{
			System.Threading.Interlocked.Increment(ref NormalFollowerBackendFailures);
		}
		if (message.Length > 2048)
		{
			message = message.Substring(0, 2048);
		}
		System.Threading.Interlocked.Increment(ref NormalFollowerErrors);
		if (error is System.OperationCanceledException)
		{
			System.Threading.Interlocked.Increment(ref NormalFollowerCanceled);
		}
		System.Threading.Interlocked.Exchange(ref NormalFollowerFetchStage, stage);
		System.Threading.Interlocked.Exchange(ref NormalFollowerFetchStatus, (error is System.OperationCanceledException) ? "canceled" : "faulted");
		System.Threading.Interlocked.CompareExchange(ref NormalFollowerFetchFirstError, message, null);
		System.Threading.Interlocked.Exchange(ref NormalFollowerFetchLastError, message);
	}

	internal static void RecordNormalFollowerSpawnTask(System.Threading.Tasks.Task task)
	{
		if (task == null)
		{
			return;
		}
		System.Threading.Interlocked.Increment(ref NormalFollowerSpawnTasksStarted);
		System.Threading.Interlocked.Exchange(ref NormalFollowerSpawnTaskStatus, task.Status.ToString());
		task.ContinueWith((System.Threading.Tasks.Task completed) =>
		{
			if (completed.IsCanceled)
			{
				System.Threading.Interlocked.Increment(ref NormalFollowerSpawnTasksCanceled);
				System.Threading.Interlocked.Exchange(ref NormalFollowerSpawnTaskStatus, "Canceled");
			}
			else if (completed.IsFaulted)
			{
				System.Threading.Interlocked.Increment(ref NormalFollowerSpawnTasksFaulted);
				string text = completed.Exception?.GetBaseException()?.GetType().FullName ?? "SpawnGroupBots task faulted.";
				if (text.Length > 2048)
				{
					text = text.Substring(0, 2048);
				}
				System.Threading.Interlocked.CompareExchange(ref NormalFollowerSpawnTaskFirstError, text, null);
				System.Threading.Interlocked.Exchange(ref NormalFollowerSpawnTaskLastError, text);
				System.Threading.Interlocked.Exchange(ref NormalFollowerSpawnTaskStatus, "Faulted");
			}
			else
			{
				System.Threading.Interlocked.Increment(ref NormalFollowerSpawnTasksCompleted);
				System.Threading.Interlocked.Exchange(ref NormalFollowerSpawnTaskStatus, "RanToCompletion");
			}
		}, System.Threading.CancellationToken.None, System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously, System.Threading.Tasks.TaskScheduler.Default);
	}

	private static void ResetNormalFollowerPipelineDiagnostics()
	{
		NormalFollowerFetchCalls = (NormalFollowerTransitCacheHits = (NormalFollowerBackendStarted = (NormalFollowerBackendResponses = (NormalFollowerBackendFailures = (NormalFollowerDecoded = (NormalFollowerErrors = (NormalFollowerCanceled = 0)))))));
		NormalFollowerSpawnTasksStarted = (NormalFollowerSpawnTasksCompleted = (NormalFollowerSpawnTasksFaulted = (NormalFollowerSpawnTasksCanceled = 0)));
		NormalFollowerFetchStage = (NormalFollowerFetchStatus = (NormalFollowerFetchEndpoint = (NormalFollowerFetchFirstError = (NormalFollowerFetchLastError = null))));
		NormalFollowerSpawnTaskStatus = "not-observed";
		NormalFollowerSpawnTaskFirstError = (NormalFollowerSpawnTaskLastError = null);
	}

	public static object GetNormalFollowerPipelineProbeSnapshot()
	{
		return new
		{
			fetchCalls = NormalFollowerFetchCalls,
			transitCacheHits = NormalFollowerTransitCacheHits,
			backendRequestsStarted = NormalFollowerBackendStarted,
			backendRequestsPending = System.Math.Max(0, NormalFollowerBackendStarted - NormalFollowerBackendResponses - NormalFollowerBackendFailures),
			backendResponses = NormalFollowerBackendResponses,
			backendFailures = NormalFollowerBackendFailures,
			profilesDecoded = NormalFollowerDecoded,
			errors = NormalFollowerErrors,
			canceled = NormalFollowerCanceled,
			currentStage = (NormalFollowerFetchStage ?? "not-observed"),
			currentStatus = (NormalFollowerFetchStatus ?? "not-observed"),
			endpointAuthority = NormalFollowerFetchEndpoint,
			firstError = NormalFollowerFetchFirstError,
			lastError = NormalFollowerFetchLastError,
			spawnTasksStarted = NormalFollowerSpawnTasksStarted,
			spawnTasksPending = System.Math.Max(0, NormalFollowerSpawnTasksStarted - NormalFollowerSpawnTasksCompleted - NormalFollowerSpawnTasksFaulted - NormalFollowerSpawnTasksCanceled),
			spawnTasksCompleted = NormalFollowerSpawnTasksCompleted,
			spawnTasksFaulted = NormalFollowerSpawnTasksFaulted,
			spawnTasksCanceled = NormalFollowerSpawnTasksCanceled,
			spawnTaskStatus = NormalFollowerSpawnTaskStatus,
			spawnTaskFirstError = NormalFollowerSpawnTaskFirstError,
			spawnTaskLastError = NormalFollowerSpawnTaskLastError,
			controllerPointer = ((Controller == null) ? null : NormalPointer(Controller.Pointer))
		};
	}

	public BotsControllerPatch()
	{
		if (Instance == null)
		{
			Instance = this;
		}
		followerCreationTask = new System.Collections.Generic.Dictionary<string, System.Threading.Tasks.Task<System.Collections.Generic.Dictionary<string, Profile>>>();
		alliesCreationTask = new System.Collections.Generic.Dictionary<string, System.Threading.Tasks.Task<BotCreationData>>();
		bossCreationTask = new System.Collections.Generic.Dictionary<WildSpawnType, System.Threading.Tasks.Task<BotCreationData>>();
	}

	private static System.Collections.Generic.List<BotDetails> DeserializeFollowerDetails(string json)
	{
		if (string.IsNullOrWhiteSpace(json))
		{
			return new System.Collections.Generic.List<BotDetails>();
		}
		JToken token = JToken.Parse(json);
		if (token.Type == JTokenType.Array)
		{
			return token.ToObject<System.Collections.Generic.List<BotDetails>>() ?? new System.Collections.Generic.List<BotDetails>();
		}
		FriendlyTeammateBodyResponse<System.Collections.Generic.List<BotDetails>> response = token.ToObject<FriendlyTeammateBodyResponse<System.Collections.Generic.List<BotDetails>>>();
		if (response == null || response.err != 0)
		{
			throw new System.InvalidOperationException($"Follower details request failed with err={response?.err}: {response?.errmsg}");
		}
		return response?.data ?? new System.Collections.Generic.List<BotDetails>();
	}

	private AICorePoint GetClosestCorePoint(BotsController _botsController, Vector3 position)
	{
		return _botsController.CoversData.GetClosest(position).CorePointInGame;
	}

	private BotsGroup GetPlayerGroup(pitAIBossPlayer player, BotOwner bt, BotZone zn, int groupSize = 0)
	{
		if (player.bossGroup != null)
		{
			return player.bossGroup;
		}
		BotSpawner botSpawnerClass = Controller.BotSpawner;
		IBotGame botGame = botSpawnerClass.BotGame;
		BotZoneGroupsDictionary spawnGroups = botSpawnerClass.Groups;
		DeadBodiesController deadBodiesController = botSpawnerClass._deadBodiesController;
		Il2CppSystem.Collections.Generic.List<Player> allPlayers = botSpawnerClass._allPlayers;
		bool _freeForAll = true;
		WildSpawnType sptBear = WildSpawnType.pmcBEAR;
		WildSpawnType sptUsec = WildSpawnType.pmcUSEC;
		bool sameSideFriendly = false;
		WildSpawnType roleh = ((player.realPlayer.Side == EPlayerSide.Bear) ? sptBear : ((player.realPlayer.Side != EPlayerSide.Usec) ? WildSpawnType.assault : sptUsec));
		GetSameSideFriendly(roleh, player.realPlayer.Side, out sameSideFriendly);
		EPlayerSide side = player.realPlayer.Side;
		System.Collections.Generic.List<BotOwner> list = new System.Collections.Generic.List<BotOwner>();
		BotsGroup botsGroup;
		if (side != EPlayerSide.Savage)
		{
			bt.Settings.FileSettings.Mind.ENEMY_BY_GROUPS_PMC_PLAYERS = side == EPlayerSide.Savage;
			bt.Settings.FileSettings.Mind.ENEMY_BY_GROUPS_SAVAGE_PLAYERS = side != EPlayerSide.Savage;
			Il2CppStructArray<EBotEnemyCause> old_reasons = bt.Settings.FileSettings.Mind.VALID_REASONS_TO_ADD_ENEMY;
			bt.Settings.FileSettings.Mind.USE_ADD_TO_ENEMY_VALIDATION = true;
			bt.Settings.FileSettings.Mind.VALID_REASONS_TO_ADD_ENEMY = new EBotEnemyCause[0];
			bt.Settings.FileSettings.Mind.DEFAULT_SAVAGE_BEHAVIOUR = EWarnBehaviour.AlwaysEnemies;
			if (!sameSideFriendly)
			{
				bt.Settings.FileSettings.Mind.DEFAULT_BEAR_BEHAVIOUR = EWarnBehaviour.AlwaysEnemies;
				bt.Settings.FileSettings.Mind.DEFAULT_USEC_BEHAVIOUR = EWarnBehaviour.AlwaysEnemies;
				Il2CppSystem.Collections.Generic.HashSet<WildSpawnType> enemyTypes = bt.Settings.GetEnemyBotTypes();
				if (!enemyTypes.Contains(WildSpawnType.pmcBEAR))
				{
					enemyTypes.Add(WildSpawnType.pmcBEAR);
				}
				if (!enemyTypes.Contains(WildSpawnType.pmcUSEC))
				{
					enemyTypes.Add(WildSpawnType.pmcUSEC);
				}
			}
			else
			{
				bt.Settings.FileSettings.Mind.DEFAULT_BEAR_BEHAVIOUR = ((bt.Side == EPlayerSide.Bear) ? EWarnBehaviour.AlwaysFriends : EWarnBehaviour.AlwaysEnemies);
				bt.Settings.FileSettings.Mind.DEFAULT_USEC_BEHAVIOUR = ((bt.Side == EPlayerSide.Usec) ? EWarnBehaviour.AlwaysFriends : EWarnBehaviour.AlwaysEnemies);
				Il2CppSystem.Collections.Generic.HashSet<WildSpawnType> enemyTypes2 = bt.Settings.GetEnemyBotTypes();
				if (bt.Side == EPlayerSide.Bear)
				{
					if (enemyTypes2.Contains(WildSpawnType.pmcBEAR))
					{
						enemyTypes2.Remove(WildSpawnType.pmcBEAR);
					}
					if (!enemyTypes2.Contains(WildSpawnType.pmcUSEC))
					{
						enemyTypes2.Add(WildSpawnType.pmcUSEC);
					}
				}
				else
				{
					if (enemyTypes2.Contains(WildSpawnType.pmcUSEC))
					{
						enemyTypes2.Remove(WildSpawnType.pmcUSEC);
					}
					if (!enemyTypes2.Contains(WildSpawnType.pmcBEAR))
					{
						enemyTypes2.Add(WildSpawnType.pmcBEAR);
					}
				}
			}
			System.Collections.Generic.List<WildSpawnType> toremove = new System.Collections.Generic.List<WildSpawnType>();
			foreach (WildSpawnType type in bt.Settings.GetEnemyBotTypes())
			{
				if (Props.friendlyBotTypes.Contains(type))
				{
					toremove.Add(type);
				}
			}
			foreach (WildSpawnType type2 in toremove)
			{
				bt.Settings.GetEnemyBotTypes().Remove(type2);
			}
			Il2CppSystem.Collections.Generic.IEnumerator<BotOwner> enumerator3 = botSpawnerClass.GetBotEnemiesList(bt).GetEnumerator();
			try
			{
				while (enumerator3.MoveNext())
				{
					BotOwner item2 = enumerator3.Current;
					if (!Props.friendlyBotTypes.Contains(item2.Profile.Info.Settings.Role))
					{
						list.Add(item2);
					}
				}
			}
			finally
			{
				if (enumerator3 is System.IDisposable disposable)
				{
					disposable.Dispose();
				}
			}
			botsGroup = new BotsGroupPlayer(zn, botGame, bt, list, deadBodiesController, allPlayers.AsManaged().ToList(), player);
			if (groupSize != 0)
			{
				botsGroup.TargetMembersCount = groupSize;
			}
			if (_freeForAll)
			{
				spawnGroups.AddNoKey(botsGroup, zn);
			}
			else
			{
				spawnGroups.Add(zn, side, botsGroup, isBossOrFollower: false);
			}
			BossPlayers.AddGroupToBoss(player, botsGroup);
			bt.Settings.FileSettings.Mind.USE_ADD_TO_ENEMY_VALIDATION = false;
			bt.Settings.FileSettings.Mind.VALID_REASONS_TO_ADD_ENEMY = old_reasons;
		}
		else
		{
			Il2CppSystem.Collections.Generic.IEnumerator<BotOwner> enumerator3 = botSpawnerClass.GetBotEnemiesList(bt).GetEnumerator();
			try
			{
				while (enumerator3.MoveNext())
				{
					BotOwner item3 = enumerator3.Current;
					list.Add(item3);
				}
			}
			finally
			{
				if (enumerator3 is System.IDisposable disposable2)
				{
					disposable2.Dispose();
				}
			}
			botsGroup = new BotsGroupPlayer(zn, botGame, bt, list, deadBodiesController, allPlayers.AsManaged().ToList(), player);
			if (groupSize != 0)
			{
				botsGroup.TargetMembersCount = groupSize;
			}
			if (_freeForAll)
			{
				spawnGroups.AddNoKey(botsGroup, zn);
			}
			else
			{
				spawnGroups.Add(zn, side, botsGroup, isBossOrFollower: false);
			}
			BossPlayers.AddGroupToBoss(player, botsGroup);
		}
		return botsGroup;
	}

	private void GetSameSideFriendly(WildSpawnType role, EPlayerSide side, out bool isFriends)
	{
		isFriends = true;
		if (pitTeam.Utils.Utils.FlagGet("isBadGuy"))
		{
			isFriends = false;
		}
	}

	private async System.Threading.Tasks.Task<bool> ActivateBotAtPosition(BotCreatorClient botCreator, Profile profile, PositionNote position, BotZone zone, bool shallBeGroup, System.Func<BotOwner, BotZone, BotsGroup> GroupAction, System.Action<BotOwner> OnActivate, Il2CppSystem.Threading.CancellationToken token, bool propagateFailure = false)
	{
		try
		{
			Il2CppSystem.Func<BotOwner, BotZone, BotSpawnBossAndGroupParams, BotsGroup> nativeGroupAction = Il2CppBoundary.ToIl2CppFunc((BotOwner botOwner, BotZone botZone, BotSpawnBossAndGroupParams groupParams) => GroupAction(botOwner, botZone));
			Il2CppSystem.Action<BotOwner> nativeCallback = OnActivate.ToIl2CppAction();
			await botCreator.ActivateBot(profile, position, zone, new BotSpawnBossAndGroupParams(isBoss: false, shallBeGroup), nativeGroupAction, nativeCallback, token);
		}
		catch (System.Exception error)
		{
			pitTeam.Modules.Logger.LogError("Failed to activate bot at debug position");
			pitTeam.Modules.Logger.LogError(error);
			if (propagateFailure)
			{
				throw;
			}
			return false;
		}
		await System.Threading.Tasks.Task.Yield();
		return true;
	}

	private static int ScoreDebugSpawnProfile(Profile candidate, EPlayerSide expectedSide, WildSpawnType expectedRole)
	{
		if (candidate == null)
		{
			return int.MinValue;
		}
		int score = 0;
		if (candidate.Info != null)
		{
			if (candidate.Info.Side == expectedSide)
			{
				score += 1000;
			}
			if (candidate.Info.Settings != null)
			{
				if (candidate.Info.Settings.Role == expectedRole)
				{
					score += 1000;
				}
				if (candidate.Info.Settings.BotDifficulty == BotDifficulty.hard)
				{
					score += 150;
				}
				if (candidate.Info.Settings.BotDifficulty == BotDifficulty.normal)
				{
					score += 75;
				}
			}
			score += Mathf.Clamp(candidate.Info.Level, 0, 100);
		}
		return score;
	}

	private static Profile PickBestDebugSpawnProfile(Profile[] loadedProfiles, EPlayerSide expectedSide, WildSpawnType expectedRole, string logPrefix)
	{
		if (loadedProfiles == null || loadedProfiles.Length == 0)
		{
			return null;
		}
		Profile best = null;
		int bestScore = int.MinValue;
		foreach (Profile p in loadedProfiles)
		{
			if (p != null)
			{
				int score = ScoreDebugSpawnProfile(p, expectedSide, expectedRole);
				if (score > bestScore)
				{
					bestScore = score;
					best = p;
				}
			}
		}
		return best;
	}

	private static async System.Threading.Tasks.Task<Profile> GenerateDebugSpawnProfile(BotCreatorClient botCreator, GetProfileDataParams profileData, EPlayerSide expectedSide, WildSpawnType expectedRole, bool multiPickPmc, CancelToken token, string logPrefix)
	{
		BotCreationData generationData = BotCreationData.CreateWithoutProfile(profileData);
		BotProfileClient presets = botCreator?._creatorProfile as BotProfileClient;
		IClientSession session = presets?._backEndSession;
		if (session != null)
		{
			int requestCount = ((!multiPickPmc) ? 1 : 4);
			Il2CppSystem.Collections.Generic.List<CountTypeBotWave> waves = (profileData.PrepareToLoadBackend(requestCount)?.ToList() ?? new System.Collections.Generic.List<CountTypeBotWave>()).ToNativeList();
			if (waves.Count > 0 && presets != null)
			{
				waves = presets.OptimizeBotWaves(waves, out var _);
			}
			Profile loadedProfile = PickBestDebugSpawnProfile((await session.LoadBots(waves)).ToArray(), expectedSide, expectedRole, logPrefix);
			if (loadedProfile != null)
			{
				await Singleton<ObjectsFactory>.Instance.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid, ObjectsFactory.AssemblyType.Local, loadedProfile.GetAllPrefabPaths(allCustomization: false).AsManaged().ToNativeList(), JobYieldPriority.General, null, ObjectsFactory.DefaultCancellationToken);
				generationData.AddProfile(loadedProfile);
			}
		}
		else
		{
			Profile generatedProfile = await botCreator.GenerateProfile(generationData, token.GetCancelToken(), withDelete: true);
			if (generatedProfile != null)
			{
				generationData.AddProfile(generatedProfile);
			}
		}
		return generationData?.Profiles?.AsManaged().FirstOrDefault((Profile p) => p != null);
	}

	private static ClientBackendSession ResolveProfileEndpoint(BotCreatorClient botCreator, out BotProfileClient botPresets)
	{
		botPresets = Il2CppBoundary.CastNativeOrNull<BotProfileClient>(botCreator?._creatorProfile);
		IEftSession creatorSession = botPresets?._backEndSession;
		ClientBackendSession endpoint = Il2CppBoundary.CastNativeOrNull<ClientBackendSession>(creatorSession);
		if (endpoint != null)
		{
			return endpoint;
		}
		bool appAvailable = Singleton<ClientApplication<IEftSession>>.Instantiated;
		IEftSession sharedSession = ((!appAvailable) ? null : Singleton<ClientApplication<IEftSession>>.Instance?.GetClientBackEndSession());
		endpoint = Il2CppBoundary.CastNativeOrNull<ClientBackendSession>(sharedSession);
		if (endpoint != null)
		{
			return endpoint;
		}
		string creatorState;
		if (botCreator == null)
		{
			creatorState = "bot-creator-null";
		}
		else if (botPresets == null)
		{
			creatorState = "creator-profile-native-cast-unavailable";
		}
		else
		{
			creatorState = ((creatorSession == null) ? "creator-session-null" : "creator-session-native-cast-unavailable");
		}
		string sharedState;
		if (appAvailable)
		{
			sharedState = ((sharedSession == null) ? "shared-session-null" : "shared-session-native-cast-unavailable");
		}
		else
		{
			sharedState = "client-application-not-instantiated";
		}
		throw new System.InvalidOperationException($"Follower profile phase=resolve-backend-session failed; creator={creatorState}; shared={sharedState}.");
	}

	private async System.Threading.Tasks.Task<Profile> FetchMemberProfile(string aid, Profile boss, BotCreatorClient botCreator, EPlayerSide side, WildSpawnType role, BotSpawnParams spawnParams)
	{
		System.Threading.Interlocked.Increment(ref NormalFollowerFetchCalls);
		RecordNormalFollowerFetchStage("fetch-member-profile");
		if (FollowerTransitStateCache.TryConsumeProfile(aid, role, out Profile transitProfile))
		{
			System.Threading.Interlocked.Increment(ref NormalFollowerTransitCacheHits);
			RecordNormalFollowerFetchStage("transit-cache-profile-bundles");
			await LoadFollowerProfileBundles(transitProfile);
			RecordNormalFollowerFetchStage("transit-cache-completed");
			return transitProfile;
		}
		GetProfileDataParams data = new GetProfileDataParams(side, role, BotDifficulty.hard, 0f, spawnParams);
		System.Collections.Generic.Dictionary<string, dynamic> customization = new System.Collections.Generic.Dictionary<string, object>();
		// Scenario guards already store the installed Labs body-part maxima. Preserve those
		// values for this explicitly allowlisted leader rather than applying a global QA multiplier.
		customization["Health"] = !string.IsNullOrWhiteSpace(aid) && side != EPlayerSide.Savage
			&& ScenarioStaminaSupport.IsConfiguredProfile(boss?.Id)
			? 1f : pitFireTeam.heatlhMultiplier.Value;
		customization["English"] = pitFireTeam.englishBear.Value;
		RecordNormalFollowerFetchStage("resolve-backend-session");
		ClientBackendSession profileEndpoint;
		BotProfileClient botPresets;
		try
		{
			profileEndpoint = ResolveProfileEndpoint(botCreator, out botPresets);
		}
		catch (System.Exception error)
		{
			RecordNormalFollowerFetchFailure("resolve-backend-session", error);
			throw;
		}
		RecordNormalFollowerFetchStage("prepare-backend-request", profileEndpoint?._backendUrls?.Main);
		Il2CppSystem.Collections.Generic.List<CountTypeBotWave> limit = (data.PrepareToLoadBackend(1)?.ToList() ?? new System.Collections.Generic.List<CountTypeBotWave>()).ToNativeList();
		if (limit.Count > 0 && botPresets != null)
		{
			limit = botPresets.OptimizeBotWaves(limit, out var _);
		}
		string scavId = null;
		if (side == EPlayerSide.Savage)
		{
			scavId = aid;
			aid = null;
		}
		string phase = "native-params-parse";
		RecordNormalFollowerFetchStage(phase, profileEndpoint?._backendUrls?.Main);
		Il2CppReferenceArray<ProfileDescriptor> result;
		try
		{
			Il2CppSystem.Object requestParams = SptJsonBoundary.ParseNativeJObject(((object)BuildFollowerRequestParams(limit, aid, scavId, customization)).ToString());
			phase = "native-params-pointer";
			RecordNormalFollowerFetchStage(phase, profileEndpoint?._backendUrls?.Main);
			if (requestParams.Pointer == System.IntPtr.Zero)
			{
				throw new System.InvalidOperationException("Follower profile native params pointer was zero.");
			}
			phase = "native-request-create";
			RecordNormalFollowerFetchStage(phase, profileEndpoint?._backendUrls?.Main);
			SendRequest sendRequest = new SendRequest();
			phase = "native-request-pointer";
			RecordNormalFollowerFetchStage(phase, profileEndpoint?._backendUrls?.Main);
			if (sendRequest.Pointer == System.IntPtr.Zero)
			{
				throw new System.InvalidOperationException("Follower profile native request pointer was zero.");
			}
			phase = "native-request-url";
			RecordNormalFollowerFetchStage(phase, profileEndpoint?._backendUrls?.Main);
			sendRequest.Url = profileEndpoint._backendUrls.Main + "/client/game/bot/followergenerate";
			phase = "native-request-params";
			RecordNormalFollowerFetchStage(phase, profileEndpoint?._backendUrls?.Main);
			sendRequest.Params = requestParams;
			sendRequest.Retries = new Il2CppSystem.Nullable<byte>(SendRequest.DefaultRetries);
			phase = "backend-request-await";
			RecordNormalFollowerFetchStage(phase, profileEndpoint?._backendUrls?.Main);
			try
			{
				System.Threading.Interlocked.Increment(ref NormalFollowerBackendStarted);
				result = await profileEndpoint.Send<Il2CppReferenceArray<ProfileDescriptor>>(sendRequest);
				System.Threading.Interlocked.Increment(ref NormalFollowerBackendResponses);
				RecordNormalFollowerFetchStage("backend-response-received", profileEndpoint?._backendUrls?.Main);
			}
			finally
			{
				System.GC.KeepAlive(sendRequest);
				System.GC.KeepAlive(requestParams);
			}
		}
		catch (System.Exception ex)
		{
			RecordNormalFollowerFetchFailure(phase, ex);
			pitTeam.Modules.Logger.LogError("Follower profile phase=" + phase + " failed");
			pitTeam.Modules.Logger.LogError(ex);
			throw new System.InvalidOperationException("Follower profile phase=" + phase + " failed for AID: " + (aid ?? scavId ?? "generated-ally"), ex);
		}
		RecordNormalFollowerFetchStage("decode-profile-response", profileEndpoint?._backendUrls?.Main);
		try
		{
			System.Collections.Generic.List<Profile> candidateProfiles = result.Select((ProfileDescriptor descriptor) => new Profile(descriptor)).ToList();
			Profile profile = candidateProfiles.FirstOrDefault();
			if (profile == null)
			{
				throw new System.InvalidOperationException("Follower profile phase=decode-profile-response was empty for AID: " + (aid ?? scavId ?? "generated-ally"));
			}
			System.Threading.Interlocked.Increment(ref NormalFollowerDecoded);
			RecordNormalFollowerFetchStage("load-profile-bundles", profileEndpoint?._backendUrls?.Main);
			await LoadFollowerProfileBundles(profile);
			RecordNormalFollowerFetchStage("profile-complete", profileEndpoint?._backendUrls?.Main);
			return profile;
		}
		catch (System.Exception error2)
		{
			RecordNormalFollowerFetchFailure("decode-profile-response", error2);
			throw;
		}
	}

	private static JObject BuildFollowerRequestParams(Il2CppSystem.Collections.Generic.List<CountTypeBotWave> waves, string memberId, string scavId, System.Collections.Generic.Dictionary<string, dynamic> customization)
	{
		JObject jObject = JObject.Parse(JsonSerializer.Serialize(new
		{
			MemberId = memberId,
			ScavId = scavId,
			Custom = (System.Collections.Generic.Dictionary<string, object>)customization
		}));
		jObject.Add("Info", JToken.Parse(SptJsonBoundary.Serialize(waves)));
		return jObject;
	}

	private static Il2CppSystem.Threading.Tasks.Task LoadFollowerProfileBundles(Profile profile)
	{
		return Singleton<ObjectsFactory>.Instance.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid, ObjectsFactory.AssemblyType.Local, profile.GetAllPrefabPaths(allCustomization: false).AsManaged().ToNativeList(), JobYieldPriority.General, null, ObjectsFactory.DefaultCancellationToken);
	}

	private async System.Threading.Tasks.Task<System.Collections.Generic.Dictionary<string, Profile>> CreateProfilesJob(pitAIBossPlayer player)
	{
		ConcurrentDictionary<string, Profile> profiles = new ConcurrentDictionary<string, Profile>();
		BotCreatorClient botCreator = Il2CppBoundary.CastNativeOrNull<BotCreatorClient>(Controller.BotSpawner._botCreator);
		EPlayerSide side = player.realPlayer.Side;
		_ = player.Position;
		WildSpawnType sptBear = WildSpawnType.pmcBEAR;
		WildSpawnType sptUsec = WildSpawnType.pmcUSEC;
		WildSpawnType type = ((side != EPlayerSide.Bear) ? sptUsec : sptBear);
		int memberCount = SpawnHelper.spawnMemberIds.Count;
		BotSpawnParams @params = new BotSpawnParams();
		@params.ShallBeGroup = new ShallBeGroupParams(group: true, bossGroup: false, memberCount + 1);
		Profile playerProfile = player.realPlayer.Profile;
		System.Collections.Generic.List<System.Threading.Tasks.Task> profileTasks = new System.Collections.Generic.List<System.Threading.Tasks.Task>();
		if (SpawnHelper.spawnMemberIds.Count > 0)
		{
			foreach (string aid in SpawnHelper.spawnMemberIds)
			{
				string id = aid.ToString();
				profileTasks.Add(FetchMemberProfile(id, playerProfile, botCreator, side, type, @params).ContinueWith((System.Threading.Tasks.Task<Profile> dt) =>
				{
					Profile value2 = dt.GetAwaiter().GetResult() ?? throw new System.InvalidOperationException("Follower profile phase=decode-profile-response returned no profile for AID: " + id);
					profiles.TryAdd(id, value2);
				}));
			}
		}
		await System.Threading.Tasks.Task.WhenAll(profileTasks);
		foreach (System.Collections.Generic.KeyValuePair<string, Profile> item2 in profiles)
		{
			Profile value = item2.Value;
			value.Info.GroupId = player.realPlayer.GroupId;
			value.Info.TeamId = player.realPlayer.Profile.Info.TeamId;
		}
		return profiles.ToDictionary((System.Collections.Generic.KeyValuePair<string, Profile> kvp) => kvp.Key, (System.Collections.Generic.KeyValuePair<string, Profile> kvp) => kvp.Value);
	}

	public System.Threading.Tasks.Task<System.Collections.Generic.Dictionary<string, Profile>> CreateFollowerProfiles(pitAIBossPlayer player)
	{
		if (Controller == null)
		{
			return null;
		}
		if (followerCreationTask.ContainsKey(player.realPlayer.ProfileId))
		{
			return followerCreationTask[player.realPlayer.ProfileId];
		}
		followerCreationTask[player.realPlayer.ProfileId] = CreateProfilesJob(player);
		return followerCreationTask[player.realPlayer.ProfileId];
	}

	public System.Threading.Tasks.Task<BotCreationData> PreFetchBossProfiles(pitAIBossPlayer player, WildSpawnType? type = null)
	{
		if (Controller == null)
		{
			return null;
		}
		EPlayerSide side = player.Player().Side;
		BotCreatorClient botCreator = Il2CppBoundary.CastNativeOrNull<BotCreatorClient>(Controller.BotSpawner._botCreator);
		WildSpawnType[] bosses = new WildSpawnType[3]
		{
			WildSpawnType.bossKnight,
			WildSpawnType.followerBigPipe,
			WildSpawnType.followerBirdEye
		};
		BotSpawnParams @params = new BotSpawnParams();
		@params.ShallBeGroup = new ShallBeGroupParams(group: true, bossGroup: false);
		if (type.HasValue)
		{
			GetProfileDataParams botData = new GetProfileDataParams(side, type.Value, BotDifficulty.normal, 5f, @params);
			BotCreationData botCreation = BotCreationData.CreateWithoutProfile(botData);
			return FetchMemberProfile(null, player.realPlayer.Profile, botCreator, side, type.Value, @params).ContinueWith((System.Threading.Tasks.Task<Profile> t) =>
			{
				botCreation.AddProfile(t.Result);
				return botCreation;
			});
		}
		WildSpawnType[] array = bosses;
		foreach (WildSpawnType boss in array)
		{
			GetProfileDataParams botData2 = new GetProfileDataParams(side, boss, BotDifficulty.normal, 0f, @params);
			BotCreationData botCreation2 = BotCreationData.CreateWithoutProfile(botData2);
			bossCreationTask[boss] = FetchMemberProfile(null, player.realPlayer.Profile, botCreator, side, boss, @params).ContinueWith((System.Threading.Tasks.Task<Profile> t) =>
			{
				botCreation2.AddProfile(t.Result);
				return botCreation2;
			});
		}
		return null;
	}

	private System.Threading.Tasks.Task<BotCreationData> GetBossProfile(pitAIBossPlayer player, WildSpawnType boss)
	{
		if (bossCreationTask.ContainsKey(boss))
		{
			return bossCreationTask[boss];
		}
		return PreFetchBossProfiles(player, boss);
	}

	public void PreFetchScavProfiles(pitAIBossPlayer player)
	{
		if (Controller == null)
		{
			return;
		}
		BotCreatorClient botCreator = Il2CppBoundary.CastNativeOrNull<BotCreatorClient>(Controller.BotSpawner._botCreator);
		int memberCount = ((SpawnHelper.spawnMemberIdsScav.Count > 0) ? SpawnHelper.spawnMemberIdsScav.Count : SpawnHelper.ScavSquadSize);
		BotSpawnParams @params = new BotSpawnParams();
		@params.ShallBeGroup = new ShallBeGroupParams(group: true, bossGroup: false, memberCount + 1);
		GetProfileDataParams data = new GetProfileDataParams(EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.hard, 5f, @params);
		BotCreationData botCreation = BotCreationData.CreateWithoutProfile(data);
		System.Collections.Generic.List<System.Threading.Tasks.Task<Profile>> tasks = new System.Collections.Generic.List<System.Threading.Tasks.Task<Profile>>();
		for (int i = 0; i < memberCount; i++)
		{
			string aid = null;
			try
			{
				if (SpawnHelper.spawnMemberIdsScav.Count > 0)
				{
					aid = SpawnHelper.spawnMemberIdsScav[i];
				}
			}
			catch
			{
			}
			tasks.Add(FetchMemberProfile(aid, player.realPlayer.Profile, botCreator, EPlayerSide.Savage, WildSpawnType.assault, @params));
		}
		alliesCreationTask[player.realPlayer.ProfileId] = System.Threading.Tasks.Task.WhenAll(tasks).ContinueWith((System.Threading.Tasks.Task<Profile[]> task) =>
		{
			Profile[] result = task.Result;
			foreach (Profile profile in result)
			{
				botCreation.AddProfile(profile);
			}
			return botCreation;
		});
	}

	public System.Threading.Tasks.Task<BotCreationData> GetScavProfiles(pitAIBossPlayer player)
	{
		if (alliesCreationTask.ContainsKey(player.realPlayer.ProfileId))
		{
			return alliesCreationTask[player.realPlayer.ProfileId];
		}
		PreFetchScavProfiles(player);
		return alliesCreationTask[player.realPlayer.ProfileId];
	}

	private static bool HasFika()
	{
		return System.Type.GetType("Fika.Core.Coop.GameMode.CoopGame, Fika.Core") != null;
	}

	public async System.Threading.Tasks.Task SpawnBossFollower(pitAIBossPlayer player, WildSpawnType boss = WildSpawnType.bossKnight, CancelToken cancelToken = null)
	{
		try
		{
			CancelToken token = ((cancelToken != null) ? cancelToken : new CancelToken());
			BotSpawner botSpawnerClass = Controller.BotSpawner;
			BotCreatorClient botCreator = Il2CppBoundary.CastNativeOrNull<BotCreatorClient>(botSpawnerClass._botCreator);
			Vector3 position = player.Position;
			EPlayerSide side = player.Player().Side;
			BotZone zone = botSpawnerClass.GetClosestZone(position, out var _);
			BotSpawnParams @params = new BotSpawnParams();
			@params.ShallBeGroup = new ShallBeGroupParams(group: true, bossGroup: false);
			new GetProfileDataParams(side, boss, BotDifficulty.hard, 0f, @params);
			new System.Collections.Generic.List<GetProfileDataParams>();
			BotCreationData bossAlly = null;
			foreach (WildSpawnType type in Props.BossFollowersType)
			{
				if (type == boss)
				{
					BotCreationData ally = await GetBossProfile(player, type);
					if (bossAlly == null)
					{
						bossAlly = ally;
					}
					else
					{
						bossAlly.AddProfiles(ally.Profiles);
					}
					break;
				}
			}
			if (SpawnHelper.spawnMemberIdsBoss.Contains(WildSpawnType.bossKnight) && bossAlly.Profiles.Find((System.Func<Profile, bool>)((Profile prf) => prf.Info.Settings.Role == WildSpawnType.bossKnight)) == null)
			{
				BotCreationData knight = await GetBossProfile(player, WildSpawnType.bossKnight);
				if (bossAlly == null)
				{
					bossAlly = knight;
				}
				else
				{
					bossAlly.AddProfiles(knight.Profiles);
				}
			}
			if (SpawnHelper.spawnMemberIdsBoss.Contains(WildSpawnType.followerBigPipe) && bossAlly.Profiles.Find((System.Func<Profile, bool>)((Profile prf) => prf.Info.Settings.Role == WildSpawnType.followerBigPipe)) == null)
			{
				BotCreationData bigPipe = await GetBossProfile(player, WildSpawnType.followerBigPipe);
				if (bossAlly == null)
				{
					bossAlly = bigPipe;
				}
				else
				{
					bossAlly.AddProfiles(bigPipe.Profiles);
				}
			}
			if (SpawnHelper.spawnMemberIdsBoss.Contains(WildSpawnType.followerBirdEye) && bossAlly.Profiles.Find((System.Func<Profile, bool>)((Profile prf) => prf.Info.Settings.Role == WildSpawnType.followerBirdEye)) == null)
			{
				BotCreationData birdEye = await GetBossProfile(player, WildSpawnType.followerBirdEye);
				if (bossAlly == null)
				{
					bossAlly = birdEye;
				}
				else
				{
					bossAlly.AddProfiles(birdEye.Profiles);
				}
			}
			if (bossAlly == null)
			{
				return;
			}
			AICorePoint closestCorePoint = GetClosestCorePoint(Controller, position);
			bossAlly.AddPosition(position, closestCorePoint.Id);
			bossAlly.Profiles.AsManaged().ToList().ForEach(async (Profile profile) =>
			{
				InteractableObjects.StoreEquipment(profile);
				if (!FollowerTransitStateCache.IsTransitSpawnProfile(profile.ProfileId))
				{
					foreach (EBodyPart part in System.Enum.GetValues(typeof(EBodyPart)))
					{
						profile.Health.BodyParts.TryGetValue(part, out var bodyPart);
						if (bodyPart != null)
						{
							float adjuster;
							switch (part)
							{
							case EBodyPart.Head:
								adjuster = 1.5f;
								break;
							case EBodyPart.LeftArm:
							case EBodyPart.RightArm:
								adjuster = 1.3f;
								break;
							case EBodyPart.LeftLeg:
							case EBodyPart.RightLeg:
								adjuster = 1.7f;
								break;
							default:
								adjuster = 1f;
								break;
							}
							bodyPart.Health.Minimum = Mathf.Round(adjuster * bodyPart.Health.Minimum);
							bodyPart.Health.Maximum = Mathf.Round(adjuster * bodyPart.Health.Maximum);
							bodyPart.Health.Current = Mathf.Round(adjuster * bodyPart.Health.Current);
						}
					}
				}
				WildSpawnType botRole = profile.Info.Settings.Role;
				profile.Info.Side = side;
				profile.Info.GroupId = player.realPlayer.GroupId;
				profile.Info.TeamId = player.Player().Profile.Info.TeamId;
				Stopwatch stopWatch = new Stopwatch();
				stopWatch.Start();
				System.Action<BotOwner> OnActivate = (BotOwner owner) =>
				{
					System.Action<BotOwner> value = (BotOwner me) =>
					{
						BotOwnerManualUpdatePatch.BotOwnerUpdate.Remove(me.ProfileId);
						try
						{
							me.Memory.DeleteInfoAboutEnemy(player.Player());
							if (!FollowerTransitStateCache.IsTransitSpawnProfile(me.ProfileId))
							{
								me.GetPlayer.ActiveHealthController.RestoreFullHealth();
							}
							me.Memory.IsPeace = true;
							if (me.Side != side)
							{
								me.GetPlayer.Profile.Info.Side = side;
							}
							if (!me.IsRole(botRole))
							{
								me.GetPlayer.Profile.Info.Settings.Role = botRole;
							}
							if (me.IsRole(WildSpawnType.followerBigPipe) && me.Boss != null)
							{
								if (me.Boss.BossLogic != null)
								{
									me.Boss.BossLogic.Dispose();
								}
								me.Boss.BossLogic = new BossKnight(me, me.Boss);
								me.Boss.NeedProtection = false;
							}
							BossPlayers.AddFollower(me, player, squadMate: false, botRole);
							me.BotTalk.SetSilence(0f);
							pitTeam.Utils.Utils.SetTimeout(() =>
							{
								if (player.Followers.Count > 0)
								{
									player.Followers.PickRandom().BotTalk.TrySay(EPhraseTrigger.MumblePhrase);
								}
							}, 1500);
						}
						catch (System.Exception error2)
						{
							pitTeam.Modules.Logger.LogError("Failed to add " + me.Profile.Nickname + " as ally");
							pitTeam.Modules.Logger.LogError(error2);
						}
					};
					BotOwnerManualUpdatePatch.BotOwnerUpdate.Add(owner.ProfileId, value);
					if (owner.Side != side)
					{
						owner.GetPlayer.Profile.Info.Side = side;
					}
					botSpawnerClass.ActivateBotCallback(owner, bossAlly, (System.Action<BotOwner>)((BotOwner follower) =>
					{
					}), shallBeGroup: true, stopWatch);
				};
				System.Func<BotOwner, BotZone, BotsGroup> GroupAction = (BotOwner bt, BotZone zn) => GetPlayerGroup(player, bt, zn, bossAlly.Count);
				BossPlayers.ShallBeFollower(profile.ProfileId);
				await ActivateBotAtPosition(botCreator, profile, new PositionNote(position, closestCorePoint.Id, isUsed: false), zone, shallBeGroup: true, GroupAction, OnActivate, token.GetCancelToken());
			});
		}
		catch (System.Exception error)
		{
			pitTeam.Modules.Logger.LogError("Failed to spawn boss follower");
			pitTeam.Modules.Logger.LogError(error);
		}
	}

	public async System.Threading.Tasks.Task SpawnGroupBots(pitAIBossPlayer player)
	{
		CancelToken token = new CancelToken();
		BotSpawner botSpawnerClass = Controller.BotSpawner;
		BotCreatorClient botCreator = Il2CppBoundary.CastNativeOrNull<BotCreatorClient>(botSpawnerClass._botCreator);
		EPlayerSide side = player.Player().Side;
		Vector3 position = player.Position;
		BotZone zone = botSpawnerClass.GetClosestZone(position, out var _);
		WildSpawnType sptBear = WildSpawnType.pmcBEAR;
		WildSpawnType sptUsec = WildSpawnType.pmcUSEC;
		WildSpawnType type;
		if (side == EPlayerSide.Bear)
		{
			type = sptBear;
		}
		else if (side == EPlayerSide.Usec)
		{
			type = sptUsec;
		}
		else
		{
			type = WildSpawnType.assault;
		}
		int memberCount = 0;
		System.Collections.Generic.Dictionary<string, string> profileTactic = new System.Collections.Generic.Dictionary<string, string>();
		System.Collections.Generic.Dictionary<string, float> profileAggression = new System.Collections.Generic.Dictionary<string, float>();
		System.Collections.Generic.Dictionary<string, FollowerProficiencyModifierValues> profileProficiency = new System.Collections.Generic.Dictionary<string, FollowerProficiencyModifierValues>();
		BotCreationData botsData;
		if (side == EPlayerSide.Savage)
		{
			BotCreationData botCreationData = await GetScavProfiles(player);
			botsData = botCreationData;
			double fenceStanding = player.realPlayer.Profile.FenceInfo.Standing;
			string[] tactis = new string[6] { "Default", "Guard", "Marksman", "Pusher", "Holder", "Assist" };
			botsData.Profiles.AsManaged().ToList().ForEach((Profile profile2) =>
			{
				if (fenceStanding < 3.0)
				{
					profileTactic[profile2.Id] = tactis[5];
				}
				else
				{
					profileTactic[profile2.Id] = tactis[System.Random.Shared.Next(tactis.Length)];
				}
			});
			memberCount = botsData.Profiles.Count;
		}
		else
		{
			memberCount = SpawnHelper.spawnMemberIds.Count;
			BotSpawnParams @params = new BotSpawnParams();
			@params.ShallBeGroup = new ShallBeGroupParams(group: true, bossGroup: false, memberCount + 1);
			GetProfileDataParams data = new GetProfileDataParams(side, type, BotDifficulty.hard, (side == EPlayerSide.Savage) ? 5f : 0f, @params);
			System.Collections.Generic.Dictionary<string, Profile> botsProfile;
			try
			{
				botsProfile = await CreateFollowerProfiles(player);
			}
			catch (System.Exception error)
			{
				pitTeam.Modules.Logger.LogError(error);
				throw;
			}
			System.Collections.Generic.Dictionary<string, string> botsTactic = new System.Collections.Generic.Dictionary<string, string>();
			System.Collections.Generic.Dictionary<string, float> botsAggression = new System.Collections.Generic.Dictionary<string, float>();
			System.Collections.Generic.Dictionary<string, FollowerProficiencyModifierValues> botsProficiency = new System.Collections.Generic.Dictionary<string, FollowerProficiencyModifierValues>();
			try
			{
				foreach (BotDetails item in DeserializeFollowerDetails(RequestHandler.GetJson("/client/game/bot/followerdetails")))
				{
					botsTactic[item.Aid] = item.Tactic;
					botsAggression[item.Aid] = item.Aggression;
					botsProficiency[item.Aid] = item.Proficiency ?? new FollowerProficiencyModifierValues();
				}
			}
			catch (System.Exception error2)
			{
				pitTeam.Modules.Logger.LogError(error2);
			}
			botsData = await BotCreationData.Create(data, botCreator, 0, botSpawnerClass);
			followerCreationTask.Remove(player.realPlayer.ProfileId);
			foreach (string aid in SpawnHelper.spawnMemberIds)
			{
				if (botsProfile.TryGetValue(aid, out Profile profile))
				{
					botsData.AddProfile(profile);
					if (botsTactic.TryGetValue(aid, out string tactic))
					{
						profileTactic[profile.Id] = tactic;
					}
					else
					{
						profileTactic[profile.Id] = "Default";
					}
					if (botsAggression.TryGetValue(aid, out var aggression))
					{
						profileAggression[profile.Id] = aggression;
					}
					else
					{
						profileAggression[profile.Id] = 50f;
					}
					profileProficiency[profile.Id] = (botsProficiency.TryGetValue(aid, out FollowerProficiencyModifierValues proficiency) ? proficiency : new FollowerProficiencyModifierValues());
				}
			}
		}
		System.Func<BotOwner, BotZone, BotsGroup> GroupAction = (BotOwner bt, BotZone zn) => GetPlayerGroup(player, bt, zn, memberCount);
		AICorePoint closestCorePoint = GetClosestCorePoint(Controller, position);
		botsData.AddPosition(position, closestCorePoint.Id);
		await FollowerSpawnTaskOwnership.StartAndJoin(botsData.Profiles.AsManaged().ToList(), SpawnFollowerProfileAsync);
		async System.Threading.Tasks.Task SpawnFollowerProfileAsync(Profile profile2)
		{
			InteractableObjects.StoreEquipment(profile2);
			System.Threading.Tasks.TaskCompletionSource<bool> followerInitialization = new System.Threading.Tasks.TaskCompletionSource<bool>(System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
			PendingNormalFollowerInitializations.TryAdd(followerInitialization, 0);
			string callbackProfileId = null;
			System.Action<BotOwner> OnActivate = (BotOwner owner) =>
			{
				bool shallBeGroup = botsData.SpawnParams?.ShallBeGroup != null;
				Stopwatch stopwatch = new Stopwatch();
				stopwatch.Start();
				System.Action<BotOwner> value = (BotOwner me) =>
				{
					BotOwnerManualUpdatePatch.BotOwnerUpdate.Remove(me.ProfileId);
					if (followerInitialization.Task.IsCompleted) return;
					try
					{
						me.Memory.DeleteInfoAboutEnemy(player.Player());
						if (!FollowerTransitStateCache.IsTransitSpawnProfile(me.ProfileId))
						{
							me.GetPlayer.ActiveHealthController.RestoreFullHealth();
						}
						string value2 = null;
						profileTactic.TryGetValue(me.Profile.ProfileId, out value2);
						if (value2 == null)
						{
							value2 = "Default";
						}
						profileAggression.TryGetValue(me.Profile.ProfileId, out var value3);
						if (value3 <= 0f && !profileAggression.ContainsKey(me.Profile.ProfileId))
						{
							value3 = 50f;
						}
						profileProficiency.TryGetValue(me.Profile.ProfileId, out FollowerProficiencyModifierValues value4);
						WildSpawnType role = type;
						ProfileInfo info = me.Profile.Info;
						if (info != null)
						{
							ProfileSettings settings = info.Settings;
							if (settings != null)
							{
								_ = settings.Role;
								if (true)
								{
									role = me.Profile.Info.Settings.Role;
								}
							}
						}
						BotFollowerPlayer registeredFollower = BossPlayers.AddFollower(me, player, squadMate: true, role, value2, value3, value4, throwOnInitFailure: true);
						if (registeredFollower == null) throw new System.InvalidOperationException("Follower initialization returned null without an exception.");
						followerInitialization.TrySetResult(true);
						try
						{
							TrySayFollowerReadyControlled(me, 2000);
						}
						catch (System.Exception readyError)
						{
							pitTeam.Modules.Logger.LogError("Follower initialized, but ready notification scheduling failed for " + me.ProfileId);
							pitTeam.Modules.Logger.LogError(readyError);
						}
					}
					catch (System.Exception error4)
					{
						followerInitialization.TrySetException(error4);
						pitTeam.Modules.Logger.LogError("Failed to initialize follower " + (me == null ? "<unknown-profile>" : me.ProfileId));
						pitTeam.Modules.Logger.LogError(error4);
					}
				};
				callbackProfileId = owner.ProfileId;
				if (followerInitialization.Task.IsCompleted) return;
				BotOwnerManualUpdatePatch.BotOwnerUpdate.Add(owner.ProfileId, value);
				if (owner.Side != side)
				{
					owner.GetPlayer.Profile.Info.Side = side;
				}
				botSpawnerClass.ActivateBotCallback(owner, botsData, (System.Action<BotOwner>)((BotOwner follower) =>
				{
				}), shallBeGroup, stopwatch);
			};
			int _inSpawnProcess = botSpawnerClass._inSpawnProcess;
			botSpawnerClass._inSpawnProcess = _inSpawnProcess + 1;
			BossPlayers.ShallBeFollower(profile2.ProfileId);
			System.Threading.CancellationTokenSource timeoutCancellation = new System.Threading.CancellationTokenSource();
			System.Threading.Tasks.Task deadline = System.Threading.Tasks.Task.Delay(System.TimeSpan.FromSeconds(120), timeoutCancellation.Token);
			try
			{
				System.Threading.Tasks.Task<bool> activationTask = ActivateBotAtPosition(botCreator, profile2, new PositionNote(position, botsData.GetPosition().CorePointId, isUsed: false), zone, shallBeGroup: true, GroupAction, OnActivate, token.GetCancelToken(), propagateFailure: true);
				System.Threading.Tasks.Task firstCompletion = await System.Threading.Tasks.Task.WhenAny(activationTask, followerInitialization.Task, deadline);
				if (firstCompletion == deadline) throw new System.TimeoutException("Follower activation or OnBotState initialization did not complete within 120 seconds.");
				if (firstCompletion == followerInitialization.Task) await followerInitialization.Task;
				firstCompletion = await System.Threading.Tasks.Task.WhenAny(activationTask, deadline);
				if (firstCompletion == deadline) throw new System.TimeoutException("Follower activation did not complete within 120 seconds after initialization.");
				bool activated = await activationTask;
				if (!activated) throw new System.InvalidOperationException("Follower activation returned false.");
				firstCompletion = await System.Threading.Tasks.Task.WhenAny(followerInitialization.Task, deadline);
				if (firstCompletion == deadline) throw new System.TimeoutException("Follower OnBotState initialization did not complete within 120 seconds.");
				await followerInitialization.Task;
			}
			catch (System.Exception error3)
			{
				followerInitialization.TrySetException(error3);
				pitTeam.Modules.Logger.LogError(error3);
				throw;
			}
			finally
			{
				timeoutCancellation.Cancel();
				timeoutCancellation.Dispose();
				PendingNormalFollowerInitializations.TryRemove(followerInitialization, out _);
				if (callbackProfileId != null) BotOwnerManualUpdatePatch.BotOwnerUpdate.Remove(callbackProfileId);
			}
		}
	}

	public async System.Threading.Tasks.Task<bool> SpawnDebugFollower(pitAIBossPlayer player, EPlayerSide spawnSide, System.Action<string> onFailure = null)
	{
		if (player == null || Controller == null || Controller.BotSpawner == null)
		{
			onFailure?.Invoke("player/controller/bot spawner not ready");
			return false;
		}
		CancelToken token = new CancelToken();
		BotSpawner botSpawnerClass = Controller.BotSpawner;
		BotCreatorClient botCreator = Il2CppBoundary.CastNativeOrNull<BotCreatorClient>(botSpawnerClass._botCreator);
		if (botCreator == null)
		{
			onFailure?.Invoke("bot creator is null");
			return false;
		}
		Vector3 position = player.Position;
		BotZone zone = botSpawnerClass.GetClosestZone(position, out var _);
		if (zone == null)
		{
			onFailure?.Invoke("no bot zone found near player");
			return false;
		}
		WildSpawnType role = spawnSide switch
		{
			EPlayerSide.Bear => WildSpawnType.pmcBEAR,
			EPlayerSide.Usec => WildSpawnType.pmcUSEC,
			_ => WildSpawnType.assault,
		};
		BotSpawnParams spawnParams = new BotSpawnParams
		{
			ShallBeGroup = new ShallBeGroupParams(group: true, bossGroup: false, Mathf.Max(2, player.Followers.Count + 2))
		};
		GetProfileDataParams data = new GetProfileDataParams(spawnSide, role, BotDifficulty.hard, (spawnSide == EPlayerSide.Savage) ? 5f : 0f, spawnParams);
		BotCreationData botsData = BotCreationData.CreateWithoutProfile(data);
		Profile profile = null;
		try
		{
			bool multiPickPmc = spawnSide == EPlayerSide.Bear || spawnSide == EPlayerSide.Usec;
			profile = await GenerateDebugSpawnProfile(botCreator, data, spawnSide, role, multiPickPmc, token, "SpawnDebugFollower");
		}
		catch (System.Exception error)
		{
			pitTeam.Modules.Logger.LogError("SpawnDebugFollower profile generation failed for requested side/role");
			pitTeam.Modules.Logger.LogError(error);
		}
		if (profile == null)
		{
			try
			{
				GetProfileDataParams safeData = new GetProfileDataParams(EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.normal, 0f, spawnParams);
				profile = await GenerateDebugSpawnProfile(botCreator, safeData, EPlayerSide.Savage, WildSpawnType.assault, multiPickPmc: false, token, "SpawnDebugFollower");
				_ = profile;
			}
			catch (System.Exception error2)
			{
				pitTeam.Modules.Logger.LogError("SpawnDebugFollower fallback profile generation failed");
				pitTeam.Modules.Logger.LogError(error2);
			}
		}
		if (profile == null)
		{
			onFailure?.Invoke("game profile generation returned no valid profile (primary+fallback)");
			return false;
		}
		profile.Info.GroupId = player.realPlayer.GroupId;
		profile.Info.TeamId = player.realPlayer.Profile.Info.TeamId;
		botsData.AddProfile(profile);
		AICorePoint closestCorePoint = GetClosestCorePoint(Controller, position);
		if (closestCorePoint == null)
		{
			onFailure?.Invoke("no valid AI core point found near player");
			return false;
		}
		botsData.AddPosition(position, closestCorePoint.Id);
		System.Func<BotOwner, BotZone, BotsGroup> groupAction = (BotOwner bt, BotZone zn) => GetPlayerGroup(player, bt, zn, Mathf.Max(2, player.Followers.Count + 2));
		System.Action<BotOwner> onActivate = (BotOwner owner) =>
		{
			bool shallBeGroup = botsData.SpawnParams?.ShallBeGroup != null;
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			System.Action<BotOwner> value = (BotOwner me) =>
			{
				BotOwnerManualUpdatePatch.BotOwnerUpdate.Remove(me.ProfileId);
				try
				{
					me.Memory.DeleteInfoAboutEnemy(player.Player());
					me.GetPlayer.ActiveHealthController.RestoreFullHealth();
					WildSpawnType valueOrDefault = (me.Profile.Info?.Settings?.Role).GetValueOrDefault(role);
					BossPlayers.AddFollower(me, player, squadMate: true, valueOrDefault);
					TrySayFollowerReadyControlled(me, 500);
				}
				catch (System.Exception error3)
				{
					pitTeam.Modules.Logger.LogError("Failed to convert debug-spawned bot to follower");
					pitTeam.Modules.Logger.LogError(error3);
				}
			};
			BotOwnerManualUpdatePatch.BotOwnerUpdate[owner.ProfileId] = value;
			if (owner.Side != spawnSide)
			{
				owner.GetPlayer.Profile.Info.Side = spawnSide;
			}
			botSpawnerClass.ActivateBotCallback(owner, botsData, (System.Action<BotOwner>)((BotOwner _) =>
			{
			}), shallBeGroup, stopwatch);
		};
		botSpawnerClass._inSpawnProcess++;
		bool activated;
		try
		{
			activated = await ActivateBotAtPosition(botCreator, profile, new PositionNote(position, botsData.GetPosition().CorePointId, isUsed: false), zone, shallBeGroup: true, groupAction, onActivate, token.GetCancelToken());
		}
		catch
		{
			botSpawnerClass._inSpawnProcess = Mathf.Max(0, botSpawnerClass._inSpawnProcess - 1);
			throw;
		}
		if (!activated)
		{
			botSpawnerClass._inSpawnProcess = Mathf.Max(0, botSpawnerClass._inSpawnProcess - 1);
			onFailure?.Invoke("bot activation failed");
			return false;
		}
		return true;
	}

	public async System.Threading.Tasks.Task<bool> SpawnDebugScavEnemy(Vector3 position, System.Action<string> onFailure = null)
	{
		if (Controller == null || Controller.BotSpawner == null)
		{
			onFailure?.Invoke("bots controller/spawner not ready");
			return false;
		}
		CancelToken token = new CancelToken();
		BotSpawner botSpawnerClass = Controller.BotSpawner;
		BotCreatorClient botCreator = Il2CppBoundary.CastNativeOrNull<BotCreatorClient>(botSpawnerClass._botCreator);
		if (botCreator == null)
		{
			onFailure?.Invoke("bot creator is null");
			return false;
		}
		BotZone zone = botSpawnerClass.GetClosestZone(position, out var _);
		if (zone == null)
		{
			onFailure?.Invoke("no bot zone found near target");
			return false;
		}
		AICorePoint closestCorePoint = GetClosestCorePoint(Controller, position);
		if (closestCorePoint == null)
		{
			onFailure?.Invoke("no valid AI core point found near target");
			return false;
		}
		BotSpawnParams spawnParams = new BotSpawnParams();
		GetProfileDataParams data = new GetProfileDataParams(EPlayerSide.Savage, WildSpawnType.assault, BotDifficulty.normal, 0f, spawnParams);
		BotCreationData botsData = BotCreationData.CreateWithoutProfile(data);
		Profile profile = null;
		try
		{
			profile = await GenerateDebugSpawnProfile(botCreator, data, EPlayerSide.Savage, WildSpawnType.assault, multiPickPmc: false, token, "SpawnDebugScav");
		}
		catch (System.Exception error)
		{
			pitTeam.Modules.Logger.LogError("SpawnDebugScav profile generation failed");
			pitTeam.Modules.Logger.LogError(error);
		}
		if (profile == null)
		{
			onFailure?.Invoke("game profile generation returned no valid scav profile");
			return false;
		}
		profile.Info.Side = EPlayerSide.Savage;
		if (profile.Info.Settings != null)
		{
			profile.Info.Settings.Role = WildSpawnType.assault;
			profile.Info.Settings.BotDifficulty = BotDifficulty.normal;
		}
		botsData.AddProfile(profile);
		botsData.AddPosition(position, closestCorePoint.Id);
		System.Func<BotOwner, BotZone, BotsGroup> groupAction = (BotOwner bot, BotZone botZone) => botSpawnerClass.GetGroupAndSetEnemies(bot, botZone, new BotSpawnBossAndGroupParams(false, false));
		System.Action<BotOwner> onActivate = (BotOwner owner) =>
		{
			Stopwatch stopwatch = new Stopwatch();
			stopwatch.Start();
			if (owner.Side != EPlayerSide.Savage)
			{
				owner.GetPlayer.Profile.Info.Side = EPlayerSide.Savage;
			}
			botSpawnerClass.ActivateBotCallback(owner, botsData, (System.Action<BotOwner>)((BotOwner spawned) =>
			{
			}), shallBeGroup: false, stopwatch);
		};
		botSpawnerClass._inSpawnProcess++;
		bool activated;
		try
		{
			activated = await ActivateBotAtPosition(botCreator, profile, new PositionNote(position, closestCorePoint.Id, isUsed: false), zone, shallBeGroup: false, groupAction, onActivate, token.GetCancelToken());
		}
		catch
		{
			botSpawnerClass._inSpawnProcess = Mathf.Max(0, botSpawnerClass._inSpawnProcess - 1);
			throw;
		}
		if (!activated)
		{
			botSpawnerClass._inSpawnProcess = Mathf.Max(0, botSpawnerClass._inSpawnProcess - 1);
			onFailure?.Invoke("bot activation failed");
			return false;
		}
		return true;
	}

	private static string NormalPointer(System.IntPtr pointer)
	{
		if (!(pointer == System.IntPtr.Zero))
		{
			return pointer.ToInt64().ToString("X");
		}
		return null;
	}

	private static void RecordNormalBossLifecycleStatus(string status)
	{
		System.Threading.Interlocked.Exchange(ref NormalBossLifecycleStatus, status);
		System.Threading.Interlocked.Exchange(ref NormalBossLifecycleLastStatus, status);
	}

	private static void RecordNormalBossLifecycleError(string stage, System.Exception error)
	{
		System.Threading.Interlocked.Increment(ref NormalBossRegistrationErrors);
		string message = stage + ": " + error;
		if (message.Length > 4096)
		{
			message = message.Substring(0, 4096);
		}
		System.Threading.Interlocked.Exchange(ref NormalBossLifecycleLastError, message);
		System.Threading.Interlocked.CompareExchange(ref NormalBossLifecycleFirstError, message, null);
		RecordNormalBossLifecycleStatus("error:" + stage);
		pitFireTeam.Log.LogWarning("[NormalBossLifecycle] " + message);
	}

	internal static void ObserveLocalGameBotsControllerInitialization(LocalGame localGame, Il2CppSystem.Threading.Tasks.Task nativeTask)
	{
		System.Threading.Interlocked.Increment(ref NormalLocalGameTaskCallbacks);
		try
		{
			if (localGame == null)
			{
				RecordNormalBossLifecycleStatus("local-game-task-missing-instance");
				return;
			}
			if (nativeTask == null)
			{
				RecordNormalBossLifecycleStatus("local-game-task-missing-result");
				return;
			}
			System.IntPtr gamePointer = localGame.Pointer;
			if (NormalObservedLocalGamePointer != System.IntPtr.Zero && NormalObservedLocalGamePointer != gamePointer)
			{
				NormalBossLifecycleGate.Clear();
				NormalBossLifecycleProgressByPair.Clear();
				NormalObservedController = null;
				NormalObservedControllerPointer = System.IntPtr.Zero;
			}
			NormalObservedLocalGame = localGame;
			NormalObservedLocalGamePointer = gamePointer;
			NormalLocalGameTaskReady = false;
			Il2CppTaskBridge.FromNative(nativeTask).ContinueWith((System.Threading.Tasks.Task completedTask) =>
			{
				ManagedFramePump.Enqueue(() =>
				{
					try
					{
						ManagedFramePump.RequireOwnerThread();
						if (NormalObservedLocalGamePointer != localGame.Pointer)
						{
							RecordNormalBossLifecycleStatus("local-task-stale-local-game");
							return true;
						}
						if (completedTask.IsCanceled)
						{
							System.Threading.Interlocked.Increment(ref NormalLocalGameTaskCanceled);
							RecordNormalBossLifecycleStatus("local-task-canceled");
							return true;
						}
						if (completedTask.IsFaulted)
						{
							System.Threading.Interlocked.Increment(ref NormalLocalGameTaskErrors);
							RecordNormalBossLifecycleError("local-task", (System.Exception)(((object)completedTask.Exception) ?? ((object)new System.InvalidOperationException("Native task faulted."))));
							return true;
						}
						System.Threading.Interlocked.Increment(ref NormalLocalGameTaskCompleted);
						NormalLocalGameTaskReady = true;
						if (!TryReadExistingBotsController(localGame, out BotsController controller, out string status))
						{
							RecordNormalBossLifecycleStatus(status);
							return true;
						}
						NormalObservedController = controller;
						NormalObservedControllerPointer = controller.Pointer;
						RecordNormalBossLifecycleStatus("local-game-controller-observed");
						TryRegisterNormalMainPlayer(Singleton<GameWorld>.Instance, controller, "LocalGame.LocalBotsControllerInitialization");
					}
					catch (System.Exception error2)
					{
						RecordNormalBossLifecycleError("local-task-continuation", error2);
					}
					return true;
				});
			}, System.Threading.CancellationToken.None, System.Threading.Tasks.TaskContinuationOptions.ExecuteSynchronously, System.Threading.Tasks.TaskScheduler.Default);
		}
		catch (System.Exception error)
		{
			System.Threading.Interlocked.Increment(ref NormalLocalGameTaskErrors);
			RecordNormalBossLifecycleError("observe-local-task", error);
		}
	}

	private static bool TryReadExistingBotsController(LocalGame localGame, out BotsController controller, out string status)
	{
		controller = null;
		status = "controller-read-unavailable";
		if (localGame == null)
		{
			status = "controller-read-missing-local-game";
			return false;
		}
		IBotGame botGame = localGame.TryCast<IBotGame>();
		if (botGame == null)
		{
			status = "controller-read-native-ibotgame-cast-null";
			return false;
		}
		controller = botGame.BotsController;
		if (controller == null || controller.Pointer == System.IntPtr.Zero)
		{
			controller = null;
			status = "controller-read-existing-getter-null";
			return false;
		}
		status = "controller-read-existing-getter-available";
		return true;
	}

	internal static void ObserveGameWorldStarted(GameWorld world)
	{
		System.Threading.Interlocked.Increment(ref NormalGameWorldStartedCallbacks);
		try
		{
			if (world == null || world.Pointer == System.IntPtr.Zero)
			{
				RecordNormalBossLifecycleStatus("game-world-missing");
				return;
			}
			if (System.Threading.Interlocked.Exchange(ref NormalGameWorldStartedPointer, world.Pointer) != world.Pointer)
			{
				System.Threading.Interlocked.Exchange(ref NormalGameWorldStartedSpawnRequested, 0);
			}
			LocalGame localGame = NormalObservedLocalGame;
			if (localGame == null || NormalObservedLocalGamePointer == System.IntPtr.Zero)
			{
				RecordNormalBossLifecycleStatus("local-game-not-observed");
				return;
			}
			if (!NormalLocalGameTaskReady)
			{
				RecordNormalBossLifecycleStatus("local-game-controller-task-not-ready");
				return;
			}
			if (!TryReadExistingBotsController(localGame, out BotsController controller, out string status))
			{
				RecordNormalBossLifecycleStatus(status);
				return;
			}
			NormalObservedController = controller;
			NormalObservedControllerPointer = controller.Pointer;
			TryRegisterNormalMainPlayer(world, controller, "GameWorld.OnGameStarted");
		}
		catch (System.Exception error)
		{
			RecordNormalBossLifecycleError("game-world-prefix", error);
		}
	}

	private static void TryRegisterNormalMainPlayer(GameWorld world, BotsController controller, string source)
	{
		if (world == null || world.Pointer == System.IntPtr.Zero)
		{
			RecordNormalBossLifecycleStatus(source + ":game-world-unavailable");
			return;
		}
		if (controller == null || controller.Pointer == System.IntPtr.Zero)
		{
			RecordNormalBossLifecycleStatus(source + ":controller-unavailable");
			return;
		}
		if (Controller != null && Controller.Pointer != controller.Pointer)
		{
			RecordNormalBossLifecycleStatus(source + ":active-controller-pointer-mismatch");
			return;
		}
		Player player = world.MainPlayer;
		if (player == null || player.Pointer == System.IntPtr.Zero)
		{
			RecordNormalBossLifecycleStatus(source + ":main-player-unavailable");
		}
		else if (!player.IsYourPlayer)
		{
			RecordNormalBossLifecycleStatus(source + ":main-player-not-local");
		}
		else
		{
			ProcessNormalBossPlayer(controller, player, world, source);
		}
	}

	private static void ProcessNormalBossPlayer(BotsController controller, Player player, GameWorld world, string source)
	{
		System.Threading.Interlocked.Increment(ref NormalBossRegistrationCalls);
		string pair = null;
		NormalBossLifecyclePairGate.Lease lease = null;
		try
		{
			if (controller == null || controller.Pointer == System.IntPtr.Zero)
			{
				RecordNormalBossLifecycleStatus(source + ":controller-unavailable");
				return;
			}
			if (Controller != null && Controller.Pointer != controller.Pointer)
			{
				RecordNormalBossLifecycleStatus(source + ":active-controller-pointer-mismatch");
				return;
			}
			if (player == null || player.Pointer == System.IntPtr.Zero)
			{
				RecordNormalBossLifecycleStatus(source + ":player-unavailable");
				return;
			}
			if (world == null || world.Pointer == System.IntPtr.Zero)
			{
				RecordNormalBossLifecycleStatus(source + ":game-world-unavailable");
				return;
			}
			string profileId = player.ProfileId;
			if (string.IsNullOrWhiteSpace(profileId))
			{
				RecordNormalBossLifecycleStatus(source + ":profile-id-unavailable");
				return;
			}
			pair = NormalPointer(world.Pointer) + "/" + NormalPointer(controller.Pointer) + "/" + NormalPointer(player.Pointer) + "/" + profileId;
			System.Threading.Interlocked.Exchange(ref NormalBossLifecycleLastPair, pair);
			lease = NormalBossLifecycleGate.TryAcquire(pair, out var alreadyCompleted);
			if (lease == null)
			{
				System.Threading.Interlocked.Increment(ref NormalBossRegistrationDuplicates);
				if (alreadyCompleted)
				{
					if (source == "AddActivePLayer")
					{
						System.Threading.Interlocked.Increment(ref NormalAddActivePlayerCompletedCalls);
					}
					RecordNormalBossLifecycleStatus(source + ":already-registered-this-raid");
					TrySpawnFollowersAfterGameWorldStarted(world, controller, player);
				}
				else
				{
					RecordNormalBossLifecycleStatus(source + ":registration-already-in-flight");
				}
				return;
			}
			if (!NormalBossLifecycleProgressByPair.TryGetValue(pair, out NormalBossLifecyclePairProgress progress))
			{
				progress = new NormalBossLifecyclePairProgress();
				NormalBossLifecycleProgressByPair.Add(pair, progress);
			}
			bool addActive = source == "AddActivePLayer";
			System.Action<string> stage = (string value) =>
			{
				System.Threading.Interlocked.Exchange(ref NormalBossLifecycleCurrentStage, source + ":" + value);
				if (addActive)
				{
					RecordNormalAddActivePlayerStage(value);
				}
			};
			System.Action<int, string, System.Action> action = (int index, string name, System.Action action2) =>
			{
				RunNormalBossInitializationStage(progress, index, name, action2, stage);
			};
			if (progress.RaidInitializationStage == 0 && Controller != null)
			{
				progress.RaidInitializationStage = 15;
			}
			action(0, "reset-kill-message", () =>
			{
				PlayerKilledPatch.ResetKillMessageRaidState();
			});
			action(1, "begin-raid-report", () =>
			{
				SquadRaidKillReport.BeginRaid(pitTeam.Utils.Utils.FlagGet("RaidTransit"));
			});
			action(2, "construct-boss-players", () =>
			{
				new BossPlayers();
			});
			action(3, "construct-interactable-objects", () =>
			{
				new InteractableObjects();
			});
			action(4, "construct-npc-message", () =>
			{
				new NpcMessage();
			});
			action(5, "enable-team-pings", () =>
			{
				PingTeamates.Enable();
			});
			action(6, "reset-props", () =>
			{
				Props.Reset();
			});
			action(7, "reset-squad-spawned", () =>
			{
				BotsEventsControllerSpawnPatch.squadSpawned = false;
				BotsEventsControllerSpawnPatch.ResetNormalSpawnDiagnostics();
				ResetNormalFollowerPipelineDiagnostics();
				System.Threading.Interlocked.Exchange(ref NormalGameWorldStartedSpawnRequested, 0);
			});
			action(8, "assign-controller", () =>
			{
				Controller = controller;
			});
			action(9, "read-world-location", () =>
			{
				progress.LocationId = world.LocationId;
			});
			action(10, "update-map-distance-config", () =>
			{
				CombatDistanceConfiguration.Instance.UpdateForCurrentMap(progress.LocationId);
			});
			action(11, "check-factory-distance-profile", () =>
			{
				progress.FactoryDistanceProfile = CombatDistanceConfiguration.UsesFactoryDistanceProfile(progress.LocationId);
			});
			action(12, "apply-factory-map-settings", () =>
			{
				if (progress.FactoryDistanceProfile)
				{
					Props.FactoryMapSett();
				}
			});
			action(13, "log-raid-start", () =>
			{
			});
			action(14, "start-battle-recorder", () =>
			{
			});
			if (player.IsYourPlayer)
			{
				stage("set-local-player-nickname");
				SquadRaidKillReport.SetPlayerNickname(player.Profile?.Nickname);
			}
			if (progress.PlayerBoss == null)
			{
				stage("register-player-as-boss");
				progress.PlayerBoss = BossPlayers.GetBoss(profileId);
				if (progress.PlayerBoss == null)
				{
					progress.PlayerBoss = BossPlayers.AddPlayerAsBoss(player, controller);
				}
			}
			if (!progress.SpawnedPlayerAppended)
			{
				stage("append-spawned-player");
				if (!spawnedPlayers.Contains(progress.PlayerBoss))
				{
					spawnedPlayers.Add(progress.PlayerBoss);
				}
				progress.SpawnedPlayerAppended = true;
			}
			if (!progress.PrefetchAttempted)
			{
				stage("prefetch-gate");
				if (!HasFika() && pitFireTeam.botPrefetch.Value)
				{
					if (progress.PlayerBoss.Player().Side != EPlayerSide.Savage)
					{
						if (SpawnHelper.spawnMemberIdsBoss.Count > 0 && Instance != null)
						{
							stage("prefetch-boss-profiles");
							progress.PrefetchAttempted = true;
							Instance.PreFetchBossProfiles(progress.PlayerBoss);
							progress.PrefetchCompleted = true;
						}
						else if (SpawnHelper.spawnMemberIds.Count > 0 && Instance != null)
						{
							stage("create-follower-profiles");
							progress.PrefetchAttempted = true;
							Instance.CreateFollowerProfiles(progress.PlayerBoss);
							progress.PrefetchCompleted = true;
						}
					}
					else if (progress.PlayerBoss.Player().Side == EPlayerSide.Savage && SpawnHelper.ScavSquad && Instance != null)
					{
						stage("prefetch-scav-profiles");
						progress.PrefetchAttempted = true;
						Instance.PreFetchScavProfiles(progress.PlayerBoss);
						progress.PrefetchCompleted = true;
					}
				}
				if (!progress.PrefetchAttempted)
				{
					progress.PrefetchCompleted = true;
				}
			}
			else if (!progress.PrefetchCompleted)
			{
				RecordNormalBossLifecycleError(source + ":prefetch-outcome-unknown", new System.InvalidOperationException("A prior prefetch invocation threw; it will not be repeated because its side effects are unknown."));
				return;
			}
			stage("postfix-completed");
			lease.Complete();
			System.Threading.Interlocked.Increment(ref NormalBossRegistrationSuccesses);
			RecordNormalBossLifecycleStatus(source + ":registered");
			if (addActive)
			{
				System.Threading.Interlocked.Increment(ref NormalAddActivePlayerCompletedCalls);
			}
			TrySpawnFollowersAfterGameWorldStarted(world, controller, player);
		}
		catch (System.Exception error)
		{
			if (source == "AddActivePLayer")
			{
				RecordNormalAddActivePlayerError(error);
				pitTeam.Modules.Logger.LogError(error);
			}
			string failedStage = System.Threading.Interlocked.CompareExchange(ref NormalBossLifecycleCurrentStage, null, null);
			RecordNormalBossLifecycleError(string.IsNullOrWhiteSpace(failedStage) ? source : failedStage, error);
		}
		finally
		{
			lease?.Dispose();
		}
	}

	private static void TrySpawnFollowersAfterGameWorldStarted(GameWorld world, BotsController controller, Player player)
	{
		try
		{
			if (!(world == null) && !(world.Pointer == System.IntPtr.Zero) && !(world.Pointer != NormalGameWorldStartedPointer))
			{
				GameWorld currentWorld = Singleton<GameWorld>.Instance;
				if (!(currentWorld == null) && !(currentWorld.Pointer != world.Pointer) && controller != null && !(controller.Pointer == System.IntPtr.Zero) && !(controller.Pointer != NormalObservedControllerPointer) && Controller != null && !(Controller.Pointer != controller.Pointer) && !(player == null) && !(player.Pointer == System.IntPtr.Zero) && player.IsYourPlayer && System.Threading.Interlocked.CompareExchange(ref NormalGameWorldStartedSpawnRequested, 1, 0) == 0)
				{
					RecordNormalBossLifecycleStatus("GameWorld.OnGameStarted:normal-spawn-dispatch");
					BotsEventsControllerSpawnPatch.SpawnFollowersFromGameWorldStarted();
				}
			}
		}
		catch (System.Exception error)
		{
			RecordNormalBossLifecycleError("GameWorld.OnGameStarted:normal-spawn-dispatch", error);
		}
	}

	private static void RunNormalBossInitializationStage(NormalBossLifecyclePairProgress progress, int index, string name, System.Action action, System.Action<string> stage)
	{
		if (progress.RaidInitializationStage <= index)
		{
			if (progress.RaidInitializationStage < index)
			{
				throw new System.InvalidOperationException("Normal boss raid initialization stage out of order: expected " + progress.RaidInitializationStage + ", requested " + index + ".");
			}
			stage(name);
			action();
			progress.RaidInitializationStage = index + 1;
		}
	}

	internal static void ResetNormalBossLifecycleForController(BotsController controller)
	{
		if (controller != null && !(controller.Pointer == System.IntPtr.Zero) && (!(NormalObservedControllerPointer != controller.Pointer) || !(Controller?.Pointer != controller.Pointer)))
		{
			ClearNormalBossLifecycleState();
		}
	}

	internal static void ResetNormalBossLifecycleForLocalGame(LocalGame localGame)
	{
		if (!(localGame == null) && !(localGame.Pointer == System.IntPtr.Zero) && !(NormalObservedLocalGamePointer != localGame.Pointer))
		{
			ClearNormalBossLifecycleState();
		}
	}

	private static void ClearNormalBossLifecycleState()
	{
		NormalBossLifecycleGate.Clear();
		NormalBossLifecycleProgressByPair.Clear();
		NormalObservedLocalGame = null;
		NormalObservedController = null;
		NormalObservedLocalGamePointer = System.IntPtr.Zero;
		NormalObservedControllerPointer = System.IntPtr.Zero;
		NormalGameWorldStartedPointer = System.IntPtr.Zero;
		NormalGameWorldStartedSpawnRequested = 0;
		NormalLocalGameTaskCallbacks = 0;
		NormalLocalGameTaskCompleted = 0;
		NormalLocalGameTaskCanceled = 0;
		NormalLocalGameTaskErrors = 0;
		NormalLocalGameTaskReady = false;
		NormalGameWorldStartedCallbacks = 0;
		NormalBossRegistrationCalls = 0;
		NormalBossRegistrationSuccesses = 0;
		NormalBossRegistrationDuplicates = 0;
		NormalBossRegistrationErrors = 0;
		NormalBossLifecycleStatus = "reset";
		NormalBossLifecycleLastStatus = "reset";
		NormalBossLifecycleLastPair = null;
		NormalBossLifecycleFirstError = null;
		NormalBossLifecycleLastError = null;
		NormalBossLifecycleCurrentStage = null;
	}

	public static object GetNormalBossLifecycleProbeSnapshot()
	{
		return new
		{
			localGameTaskCallbacks = NormalLocalGameTaskCallbacks,
			localGameTaskCompleted = NormalLocalGameTaskCompleted,
			localGameTaskCanceled = NormalLocalGameTaskCanceled,
			localGameTaskErrors = NormalLocalGameTaskErrors,
			localGameTaskReady = NormalLocalGameTaskReady,
			gameWorldStartedCallbacks = NormalGameWorldStartedCallbacks,
			gameWorldStartedSpawnRequested = NormalGameWorldStartedSpawnRequested,
			gameWorldStartedPointer = NormalPointer(NormalGameWorldStartedPointer),
			registrationCalls = NormalBossRegistrationCalls,
			registrationSuccesses = NormalBossRegistrationSuccesses,
			registrationDuplicates = NormalBossRegistrationDuplicates,
			registrationErrors = NormalBossRegistrationErrors,
			pairCount = NormalBossLifecycleGate.CompletedCount,
			inFlightPairCount = NormalBossLifecycleGate.InFlightCount,
			lastPair = NormalBossLifecycleLastPair,
			currentStage = NormalBossLifecycleCurrentStage,
			status = NormalBossLifecycleStatus,
			lastStatus = NormalBossLifecycleLastStatus,
			firstError = NormalBossLifecycleFirstError,
			lastError = NormalBossLifecycleLastError,
			localGamePointer = NormalPointer(NormalObservedLocalGamePointer),
			actualControllerExists = (NormalObservedController != null),
			actualControllerPointer = ((NormalObservedController == null) ? null : NormalPointer(NormalObservedController.Pointer)),
			activeControllerPointer = ((Controller == null) ? null : NormalPointer(Controller.Pointer))
		};
	}

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(BotsController), "AddActivePLayer");
	}

	private static void RecordNormalAddActivePlayerStage(string stage)
	{
		System.Threading.Interlocked.Exchange(ref NormalAddActivePlayerStage, stage);
	}

	private static void RecordNormalAddActivePlayerError(System.Exception error)
	{
		System.Threading.Interlocked.Increment(ref NormalAddActivePlayerErrors);
		string message = NormalAddActivePlayerStage + ": " + error;
		if (message.Length > 4096)
		{
			message = message.Substring(0, 4096);
		}
		System.Threading.Interlocked.CompareExchange(ref NormalAddActivePlayerFirstError, message, null);
		if (System.Threading.Interlocked.CompareExchange(ref _normalAddActivePlayerFirstErrorLogged, 1, 0) == 0)
		{
			pitFireTeam.Log.LogWarning("[NormalSquadPipeline] AddActivePLayer postfix first error: " + message);
		}
	}

	[PatchPostfix]
	private static void PatchPostfix(BotsController __instance, Player player)
	{
		int num = System.Threading.Interlocked.Increment(ref NormalAddActivePlayerPostfixCalls);
		RecordNormalAddActivePlayerStage("postfix-entry");
		if (num == 1 && System.Threading.Interlocked.CompareExchange(ref _normalAddActivePlayerEntryLogged, 1, 0) == 0)
		{
			pitFireTeam.Log.LogInfo("[NormalSquadPipeline] AddActivePLayer postfix entered; playerIsLocal=" + (player?.IsYourPlayer ?? false));
		}
		ProcessNormalBossPlayer(__instance, player, Singleton<GameWorld>.Instance, "AddActivePLayer");
	}

	private static void TrySayFollowerReadyControlled(BotOwner bot, int delayMs)
	{
		if (bot == null || bot.IsDead || bot.BotState != EBotState.Active)
		{
			return;
		}
		FollowerForcedPhraseGate.Arm(bot, EPhraseTrigger.Ready, 1.5f);
		pitTeam.Utils.Utils.SetTimeout(() =>
		{
			if (!(bot == null) && !bot.IsDead && bot.BotState == EBotState.Active)
			{
				bot.BotTalk.TrySay(EPhraseTrigger.Ready, withGroupDelay: false);
				FollowerForcedPhraseGate.Clear(bot);
			}
		}, delayMs);
	}
}
}

namespace pitTeam.Patches {
using System.Collections.Generic;
using System.Reflection;
using Comfort.Common;
using EFT;
using EFT.Quests;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using pitTeam.Utils;
internal class BossSpawnWaveManagerClassPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(BossSpawnScenario), "Run");
	}

	[PatchPrefix]
	private static bool PatchPrefix(BossSpawnScenario __instance)
	{
		if (!Singleton<AbstractGame>.Instantiated || GamePlayerOwner.MyPlayer == null)
		{
			return true;
		}
		if (GamePlayerOwner.MyPlayer.HealthController == null || !GamePlayerOwner.MyPlayer.HealthController.IsAlive)
		{
			return true;
		}
		Player player = GamePlayerOwner.MyPlayer;
		if (player.Side == EPlayerSide.Savage)
		{
			return true;
		}
		foreach (BossLocationSpawn wave in __instance.BossSpawnWaves)
		{
			if (wave.BossType == WildSpawnType.bossKnight && (SpawnHelper.spawnMemberIdsBoss.Contains(WildSpawnType.followerBigPipe) || SpawnHelper.spawnMemberIdsBoss.Contains(WildSpawnType.followerBirdEye) || SpawnHelper.spawnMemberIdsBoss.Contains(WildSpawnType.bossKnight)))
			{
				wave.ShallSpawn = false;
				wave.ForceSpawn = false;
			}
			if (!pitTeam.Utils.Utils.FlagGet("questGoons"))
			{
				continue;
			}
			foreach (QuestStatusData quest in player.Profile.QuestsData)
			{
				foreach (KeyValuePair<WildSpawnType, List<string>> item in Props.QuestBosses)
				{
					if (item.Key == wave.BossType && item.Value.Contains(quest.Id) && quest.Status == EQuestStatus.Started)
					{
						wave.ShallSpawn = true;
						break;
					}
				}
			}
		}
		return true;
	}
}
}

namespace pitTeam.Patches {
using System;
using EFT;
using HarmonyLib;
using pitTeam.Compat;
[HarmonyPatch(typeof(LocalGame), MethodType.Constructor, new Type[] { })]
internal class LocalGameCtorPatch
{
	public static LocalGame Instance;

	public static void Postfix(LocalGame __instance)
	{
		NativeConstructorPatchTargets.RecordLocalGameCallback(__instance.Pointer);
		Instance = __instance;
	}
}
}

namespace pitTeam.Patches {
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EFT;
using HarmonyLib;
using SPTushonka.Common.Utils;
using SPTushonka.Reflection.Patching;
using pitTeam.Components;
using pitTeam.Modules;
using pitTeam.Utils;
internal class BotsEventsControllerSpawnPatch : ModulePatch
{
	public static bool squadSpawned = false;

	private static int NormalSpawnActionCallbacks;

	private static int NormalSpawnActionCompleted;

	private static int NormalSpawnActionErrors;

	private static int NormalSpawnFollowersCalls;

	private static int NormalSpawnFollowersStarted;

	private static int NormalSpawnSkippedAlready;

	private static int NormalSpawnSkippedControllerNull;

	private static string NormalSpawnStage = "not-observed";

	private static string NormalSpawnFirstError;

	private static string NormalSpawnLastError;

	private static string NormalSpawnInvocationOrigin = "not-observed";

	private static string NormalSpawnStartedOrigin;

	private static int NormalGameWorldStartedSpawnCalls;

	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(BotsEventsController), "SpawnAction");
	}

	[PatchPostfix]
	private static void PatchPostfix(BotsEventsController __instance)
	{
		Interlocked.Increment(ref NormalSpawnActionCallbacks);
		SetNormalSpawnStage("spawn-action-postfix");
		pitTeam.Utils.Utils.FlagSet("RaidTransit", value: false);
		try
		{
			SpawnFollowers();
			Interlocked.Increment(ref NormalSpawnActionCompleted);
			SetNormalSpawnStage("spawn-action-completed");
		}
		catch (Exception ex)
		{
			Interlocked.Increment(ref NormalSpawnActionErrors);
			string message = ex.GetType().FullName;
			Interlocked.CompareExchange(ref NormalSpawnFirstError, message, null);
			Interlocked.Exchange(ref NormalSpawnLastError, message);
			SetNormalSpawnStage("spawn-action-error");
			pitTeam.Modules.Logger.LogError(ex);
		}
	}

	private static void SetNormalSpawnStage(string stage)
	{
		Interlocked.Exchange(ref NormalSpawnStage, stage);
	}

	internal static void ResetNormalSpawnDiagnostics()
	{
		NormalSpawnActionCallbacks = (NormalSpawnActionCompleted = (NormalSpawnActionErrors = (NormalSpawnFollowersCalls = (NormalSpawnFollowersStarted = (NormalSpawnSkippedAlready = (NormalSpawnSkippedControllerNull = 0))))));
		NormalSpawnStage = "not-observed";
		NormalSpawnFirstError = (NormalSpawnLastError = null);
		NormalSpawnInvocationOrigin = "not-observed";
		NormalSpawnStartedOrigin = null;
		NormalGameWorldStartedSpawnCalls = 0;
	}

	public static object GetNormalSpawnActionProbeSnapshot()
	{
		return new
		{
			callbacks = NormalSpawnActionCallbacks,
			completed = NormalSpawnActionCompleted,
			errors = NormalSpawnActionErrors,
			spawnFollowersCalls = NormalSpawnFollowersCalls,
			started = NormalSpawnFollowersStarted,
			skippedAlreadySpawned = NormalSpawnSkippedAlready,
			skippedControllerNull = NormalSpawnSkippedControllerNull,
			gameWorldStartedSpawnCalls = NormalGameWorldStartedSpawnCalls,
			invocationOrigin = NormalSpawnInvocationOrigin,
			startedOrigin = NormalSpawnStartedOrigin,
			stage = NormalSpawnStage,
			firstError = NormalSpawnFirstError,
			lastError = NormalSpawnLastError,
			squadSpawned = squadSpawned,
			controllerPointer = ((BotsControllerPatch.Controller == null) ? null : BotsControllerPatch.Controller.Pointer.ToInt64().ToString("X"))
		};
	}

	public static void SpawnFollowers()
	{
		SpawnFollowersCore("BotsEventsControllerSpawnPatch.SpawnAction");
	}

	internal static void SpawnFollowersFromGameWorldStarted()
	{
		Interlocked.Increment(ref NormalGameWorldStartedSpawnCalls);
		SpawnFollowersCore("GameWorld.OnGameStarted");
	}

	private static void SpawnFollowersCore(string origin)
	{
		Interlocked.Exchange(ref NormalSpawnInvocationOrigin, origin);
		Interlocked.Increment(ref NormalSpawnFollowersCalls);
		if (squadSpawned)
		{
			Interlocked.Increment(ref NormalSpawnSkippedAlready);
			SetNormalSpawnStage("spawn-followers-skipped-already-spawned");
			return;
		}
		if (BotsControllerPatch.Controller == null)
		{
			Interlocked.Increment(ref NormalSpawnSkippedControllerNull);
			SetNormalSpawnStage("spawn-followers-skipped-controller-null");
			return;
		}
		squadSpawned = true;
		Interlocked.Exchange(ref NormalSpawnStartedOrigin, origin);
		Interlocked.Increment(ref NormalSpawnFollowersStarted);
		SetNormalSpawnStage("spawn-followers-started");
		if (SpawnHelper.spawnMemberIds.Count == 0 && SpawnHelper.spawnMemberIdsBoss.Count == 0 && !SpawnHelper.ScavSquad)
		{
			SetNormalSpawnStage("spawn-followers-no-configured-members");
		}
		List<Task> squadSpawners = new List<Task>();
		List<string> scavPlayers = new List<string>();
		BotsControllerPatch.spawnedPlayers.ForEach((pitAIBossPlayer playerBoss) =>
		{
			if (playerBoss.realPlayer.Side == EPlayerSide.Savage && SpawnHelper.ScavSquad)
			{
				Task task = BotsControllerPatch.Instance.SpawnGroupBots(playerBoss);
				BotsControllerPatch.RecordNormalFollowerSpawnTask(task);
				squadSpawners.Add(task);
				scavPlayers.Add(playerBoss.realPlayer.ProfileId);
			}
		});
		if (SpawnHelper.spawnMemberIds.Count > 0)
		{
			SetNormalSpawnStage("start-squad-spawn");
			BotsControllerPatch.spawnedPlayers.ForEach((pitAIBossPlayer playerBoss) =>
			{
				if (!scavPlayers.Contains(playerBoss.realPlayer.ProfileId))
				{
					Task task = BotsControllerPatch.Instance.SpawnGroupBots(playerBoss);
					BotsControllerPatch.RecordNormalFollowerSpawnTask(task);
					squadSpawners.Add(task);
				}
			});
			Task.WhenAll(squadSpawners).ContinueWith((Task t) =>
			{
				BotsControllerPatch.alliesCreationTask.Clear();
			}, TaskScheduler.FromCurrentSynchronizationContext()).HandleExceptions();
		}
		else
		{
			if (SpawnHelper.spawnMemberIdsBoss.Count <= 0)
			{
				return;
			}
			SetNormalSpawnStage("start-boss-ally-spawn");
			BotsControllerPatch.Controller.BotSpawner.SetBlockedRoles(new string[3] { "bossKnight", "followerBirdEye", "followerBigPipe" });
			BotsControllerPatch.alliesCreationTask.Clear();
			List<Task> bossSpawners = new List<Task>();
			BotsControllerPatch.spawnedPlayers.ForEach((pitAIBossPlayer playerBoss) =>
			{
				if (scavPlayers.Contains(playerBoss.realPlayer.ProfileId))
				{
					return;
				}
				try
				{
					WildSpawnType boss = WildSpawnType.bossKnight;
					if (!SpawnHelper.spawnMemberIdsBoss.Contains(WildSpawnType.bossKnight))
					{
						if (SpawnHelper.spawnMemberIdsBoss.Contains(WildSpawnType.followerBigPipe))
						{
							boss = WildSpawnType.followerBigPipe;
						}
						else if (SpawnHelper.spawnMemberIdsBoss.Contains(WildSpawnType.followerBirdEye))
						{
							boss = WildSpawnType.followerBirdEye;
						}
					}
					bossSpawners.Add(BotsControllerPatch.Instance.SpawnBossFollower(playerBoss, boss));
				}
				catch (Exception error)
				{
					pitTeam.Modules.Logger.LogError("Failed to spawn Boss Ally");
					pitTeam.Modules.Logger.LogError(error);
				}
			});
			Task.WhenAll(bossSpawners).ContinueWith((Task t) =>
			{
				BotsControllerPatch.bossCreationTask.Clear();
			}, TaskScheduler.FromCurrentSynchronizationContext()).HandleExceptions();
		}
	}
}
}

namespace pitTeam.Patches {
using System.Reflection;
using EFT;
using HarmonyLib;
using Il2CppSystem.Threading.Tasks;
using SPTushonka.Reflection.Patching;
internal class LocalGameBotsControllerInitializationPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(LocalGame), "LocalBotsControllerInitialization");
	}

	[PatchPostfix]
	private static void PatchPostfix(LocalGame __instance, Task __result)
	{
		BotsControllerPatch.ObserveLocalGameBotsControllerInitialization(__instance, __result);
	}
}
}

namespace pitTeam.Patches {
using System.Reflection;
using EFT;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
internal class GameWorldStartedNormalBossRegistrationPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(GameWorld), "OnGameStarted");
	}

	[PatchPrefix]
	private static void PatchPrefix(GameWorld __instance)
	{
		BotsControllerPatch.ObserveGameWorldStarted(__instance);
	}
}
}

namespace pitTeam.Patches {
using System;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.Quests;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using pitTeam.Components;
using pitTeam.Modules;
using pitTeam.Utils;
internal class BotsControllerStopPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(BotsController), "StopGettingInfo");
	}

	[PatchPrefix]
	private static bool PatchPrefix(BotsController __instance)
	{
		bool raidTransit = pitTeam.Utils.Utils.FlagGet("RaidTransit");
		try
		{
			BossStandingSave(__instance);
		}
		catch (Exception error)
		{
			pitTeam.Modules.Logger.LogError("Failed to save boss standing");
			pitTeam.Modules.Logger.LogError(error);
		}
		InteractableObjects.Dispose();
		NpcMessage.Dispose();
		BossPlayers.Dispose();
		BotsControllerPatch.ResetNormalBossLifecycleForController(__instance);
		BotsControllerPatch.spawnedPlayers.Clear();
		BotsControllerPatch.followerCreationTask.Clear();
		BotsControllerPatch.alliesCreationTask.Clear();
		BotsControllerPatch.bossCreationTask.Clear();
		BotsControllerPatch.Controller = null;
		BotsControllerPatch.CancelPendingNormalFollowerInitializations();
		BotOwnerManualUpdatePatch.BotOwnerUpdate.Clear();
		PingTeamates.Disable();
		Enemy.ClearEnemiesLocations();
		pitTeam.Utils.Utils.FlagsClear();
		pitTeam.Utils.Utils.ValuesClear();
		if (!raidTransit)
		{
			TeammateAutoJoinRuntime.ClearAllSuppression();
			MainMenuControllerPatch.GroupPlayers.Clear();
			MainMenuControllerPatch.TransitPlayers.Clear();
			SpawnHelper.spawnMemberIds.Clear();
			SpawnHelper.spawnMemberIdsScav.Clear();
			SpawnHelper.spawnMemberIdsBoss.Clear();
			FollowerTransitStateCache.Clear();
		}
		BotsEventsControllerSpawnPatch.squadSpawned = false;
		LocalGameCtorPatch.Instance = null;
		if (raidTransit)
		{
			pitTeam.Utils.Utils.FlagSet("RaidTransit", value: true);
			pitTeam.Utils.Utils.FlagSet("isBadGuy", pitFireTeam.badGuy.Value || SpawnHelper.spawnMemberIdsBoss.Count > 0);
			pitTeam.Utils.Utils.FlagSet("pitFireTeam", pitFireTeam.pitFireTeamFLAG.Value);
		}
		return true;
	}

	private static void BossStandingSave(BotsController controller)
	{
		pitAIBossPlayer boss = null;
		controller.Players.ExecuteForEach((Action<IPlayer>)((IPlayer player) =>
		{
			if (boss == null)
			{
				boss = BossPlayers.GetBoss(player.ProfileId);
			}
		}));
		if (boss == null)
		{
			return;
		}
		Profile profile = boss.realPlayer.Profile;
		bool knightIncrease = false;
		bool pipeIncrease = false;
		bool birdEyeIncrease = false;
		double maxStanding = 2.0;
		foreach (QuestStatusData quest in profile.QuestsData)
		{
			foreach (KeyValuePair<string, List<string>> item in Props.Quests)
			{
				if (item.Key == "Knight" && quest.Id == item.Value[0] && quest.Status == EQuestStatus.Success)
				{
					knightIncrease = true;
				}
				if (item.Key == "BigPipe" && quest.Id == item.Value[0] && quest.Status == EQuestStatus.Success)
				{
					pipeIncrease = true;
				}
				if (item.Key == "BirdEye" && quest.Id == item.Value[0] && quest.Status == EQuestStatus.Success)
				{
					birdEyeIncrease = true;
				}
			}
		}
		if (!boss.realPlayer.Profile.TryGetTraderInfo(Props.KnightTrader, out var traderInfo) || traderInfo.Disabled)
		{
			return;
		}
		foreach (BotOwner bot in boss.Followers)
		{
			if ((bot.IsRole(WildSpawnType.bossKnight) & knightIncrease) || (bot.IsRole(WildSpawnType.followerBigPipe) & pipeIncrease) || (bot.IsRole(WildSpawnType.followerBirdEye) & birdEyeIncrease))
			{
				double standing = boss.realPlayer.Profile.GetTraderStanding(Props.KnightTrader);
				if (standing < maxStanding)
				{
					traderInfo.SetStanding(Math.Min(maxStanding, standing + 0.05));
				}
			}
		}
	}
}
}

namespace pitTeam.Patches {
using System;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using HarmonyLib;
using Il2CppSystem.Collections.Generic;
using SPTushonka.Reflection.Patching;
using pitTeam.Components;
using pitTeam.Modules;
internal class LocalGameCleanupPatch : ModulePatch
{
	protected override MethodBase GetTargetMethod()
	{
		return AccessTools.Method(typeof(LocalGame), "CleanUp");
	}

	[PatchPrefix]
	private static bool PatchPrefix(LocalGame __instance)
	{
		BotsControllerPatch.ResetNormalBossLifecycleForLocalGame(__instance);
		try
		{
			PropertyInfo propertyInfo = AccessTools.Property(typeof(LocalGame), "_bots");
			if (propertyInfo == null)
			{
				throw new MissingMemberException(typeof(LocalGame).FullName, "_bots property");
			}
			if (propertyInfo.GetValue(__instance) is Il2CppSystem.Collections.Generic.Dictionary<string, Player> dictionary_2)
			{
				System.Collections.Generic.List<string> keysToRemove = new System.Collections.Generic.List<string>();
				foreach (string key in dictionary_2.Keys)
				{
					if (!dictionary_2.TryGetValue(key, out var value) || value == null)
					{
						keysToRemove.Add(key);
					}
				}
				foreach (string key2 in keysToRemove)
				{
					dictionary_2.Remove(key2);
				}
			}
			foreach (BotFollowerPlayer follower in BossPlayers.GetFollowers())
			{
				if (follower?.GetBot() != null)
				{
					SainAddonBridge.RaiseFollowerLifecycleEvent(follower.GetBot(), FollowerLifecycleEvent.OnRaidEnd);
				}
			}
		}
		catch (Exception error)
		{
			pitTeam.Modules.Logger.LogError("Raid CleanUp Failed");
			pitTeam.Modules.Logger.LogError(error);
		}
		return true;
	}
}
}
