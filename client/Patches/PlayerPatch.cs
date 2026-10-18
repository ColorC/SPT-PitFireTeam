using Comfort.Common;
using EFT;
using EFT.Counters;
using EFT.HealthSystem;
using EFT.InventoryLogic;
using EFT.Quests;
using pitTeam.Components;
using pitTeam.Compat;
using pitTeam.Modules;
using HarmonyLib;
using Newtonsoft.Json;
using SPTushonka.Common.Http;
using SPTushonka.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;

using GestureData = BotReceiverGestus;
using EventInfo = GlobalEventDispatcher.PhraseDelegateInfo;

namespace pitTeam.Patches
{
    internal static class BossGestureCommandRouter
    {
        public static bool IsBossCommandGesture(EInteraction gesture)
        {
            return gesture == EInteraction.ComeWithMeGesture ||
                   gesture == EInteraction.ThereGesture ||
                   gesture == EInteraction.HoldGesture ||
                   gesture == (EInteraction)CustomGestures.OverThere;
        }

        public static bool TryPlayOverThereGesture(Player player)
        {
            if (player == null)
            {
                return false;
            }

            pitAIBossPlayer? boss = BossPlayers.GetBoss(player.ProfileId);
            if (boss != null)
            {
                try
                {
                    InteractableObjects.CheckSeenEnemies(boss.Player());
                }
                catch (Exception ex)
                {
                    pitTeam.Modules.Logger.LogError($"Over There legacy seen-enemy scan failed; continuing with boss gesture command. {ex}");
                }

                boss.GestusShown(new GestureData
                {
                    Gesture = (EInteraction)CustomGestures.OverThere,
                    Player = player
                });
            }

            if (!player.HandsController.IsInInteractionStrictCheck())
            {
                player.MovementContext.SetInteractInHands(EInteraction.ThereGesture);
            }

            return true;
        }
    }

    internal static class RegisteredBossAIDataBinder
    {
        internal sealed class Snapshot
        {
            public int Attempts { get; set; }
            public int Successes { get; set; }
            public int Pending { get; set; }
            public int Errors { get; set; }
            public int CleanupErrors { get; set; }
            public string LastProfileId { get; set; } = string.Empty;
            public string AIDataPointer { get; set; } = string.Empty;
            public string ExpectedBossPointer { get; set; } = string.Empty;
            public string ReadbackBossPointer { get; set; } = string.Empty;
            public string LastError { get; set; } = string.Empty;
            public string LastCleanupProfileId { get; set; } = string.Empty;
            public string LastCleanupAIDataPointer { get; set; } = string.Empty;
            public string LastCleanupExpectedBossPointer { get; set; } = string.Empty;
            public string LastCleanupReadbackBossPointer { get; set; } = string.Empty;
            public string LastCleanupError { get; set; } = string.Empty;
        }

        private static readonly object Sync = new object();
        private static readonly Snapshot State = new Snapshot();

        [Il2CppInterop.Runtime.Attributes.HideFromIl2Cpp]
        internal static Snapshot GetProbeSnapshot()
        {
            lock (Sync)
            {
                return new Snapshot {
                    Attempts = State.Attempts, Successes = State.Successes, Pending = State.Pending, Errors = State.Errors, CleanupErrors = State.CleanupErrors,
                    LastProfileId = State.LastProfileId, AIDataPointer = State.AIDataPointer,
                    ExpectedBossPointer = State.ExpectedBossPointer, ReadbackBossPointer = State.ReadbackBossPointer, LastError = State.LastError,
                    LastCleanupProfileId = State.LastCleanupProfileId, LastCleanupAIDataPointer = State.LastCleanupAIDataPointer,
                    LastCleanupExpectedBossPointer = State.LastCleanupExpectedBossPointer, LastCleanupReadbackBossPointer = State.LastCleanupReadbackBossPointer, LastCleanupError = State.LastCleanupError
                };
            }
        }

        private static void RecordAttempt(string profileId, IntPtr aiData, IntPtr expectedBoss, IntPtr readbackBoss, string error = null, bool success = false, bool pending = false)
        {
            lock (Sync)
            {
                State.Attempts++;
                State.LastProfileId = profileId ?? string.Empty;
                State.AIDataPointer = aiData == IntPtr.Zero ? string.Empty : aiData.ToString("X");
                State.ExpectedBossPointer = expectedBoss == IntPtr.Zero ? string.Empty : expectedBoss.ToString("X");
                State.ReadbackBossPointer = readbackBoss == IntPtr.Zero ? string.Empty : readbackBoss.ToString("X");
                State.LastError = error ?? string.Empty;
                if (success) State.Successes++;
                if (pending) State.Pending++;
                if (!string.IsNullOrEmpty(error)) State.Errors++;
            }
        }

        private static void RecordCleanupFailure(string profileId, IntPtr aiData, IntPtr expectedBoss, IntPtr readbackBoss, Exception error)
        {
            string message = "Prior boss cleanup failed after replacement setter/readback succeeded for " + profileId + "; current native boss readback=" + readbackBoss.ToString("X") + "; error: " + error;
            lock (Sync)
            {
                State.CleanupErrors++;
                State.Errors++;
                State.LastError = message;
                State.LastCleanupProfileId = profileId ?? string.Empty;
                State.LastCleanupAIDataPointer = aiData == IntPtr.Zero ? string.Empty : aiData.ToString("X");
                State.LastCleanupExpectedBossPointer = expectedBoss == IntPtr.Zero ? string.Empty : expectedBoss.ToString("X");
                State.LastCleanupReadbackBossPointer = readbackBoss == IntPtr.Zero ? string.Empty : readbackBoss.ToString("X");
                State.LastCleanupError = message;
            }
            pitTeam.Modules.Logger.LogError(message);
        }
        private static bool SameBoss(AIBossPlayer? left, AIBossPlayer? right)
        {
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null)) return ReferenceEquals(left, right);
            return pitTeam.Compat.Il2CppBoundary.SameNativeObject(left, right);
        }

        internal static void BindMappedBoss(Player player, pitAIBossPlayer? expectedBoss = null)
        {
            if (ReferenceEquals(player, null)) return;
            string profileId = string.Empty;
            IntPtr aiDataPointer = IntPtr.Zero;
            IntPtr expectedPointer = IntPtr.Zero;
            IntPtr readbackPointer = IntPtr.Zero;
            try { profileId = player.ProfileId ?? string.Empty; }
            catch (Exception ex) { RecordAttempt(profileId, aiDataPointer, expectedPointer, readbackPointer, "ProfileId read failed: " + ex); pitTeam.Modules.Logger.LogError("Boss AIData binding could not read player ProfileId: " + ex); return; }
            if (string.IsNullOrEmpty(profileId)) return;
            pitAIBossPlayer? mappedBoss;
            try { mappedBoss = BossPlayers.GetBoss(profileId); }
            catch (Exception ex) { RecordAttempt(profileId, aiDataPointer, expectedPointer, readbackPointer, "Boss map lookup failed: " + ex); pitTeam.Modules.Logger.LogError("Boss AIData binding map lookup failed for " + profileId + ": " + ex); return; }
            if (ReferenceEquals(mappedBoss, null)) return;
            if (!ReferenceEquals(expectedBoss, null) && !pitTeam.Compat.Il2CppBoundary.SameNativeObject(mappedBoss, expectedBoss)) return;
            expectedPointer = mappedBoss.Pointer;
            if (expectedPointer == IntPtr.Zero) { RecordAttempt(profileId, aiDataPointer, expectedPointer, readbackPointer, "Mapped boss has a zero native pointer."); pitTeam.Modules.Logger.LogError("Boss AIData binding refused zero-pointer mapped boss for " + profileId); return; }
            AIData? aiData;
            try
            {
                var nativeAIData = player.AIData;
                if (ReferenceEquals(nativeAIData, null)) { RecordAttempt(profileId, aiDataPointer, expectedPointer, readbackPointer, null, pending: true); return; }
                aiData = nativeAIData.TryCast<AIData>();
                if (ReferenceEquals(aiData, null)) { RecordAttempt(profileId, aiDataPointer, expectedPointer, readbackPointer, null, pending: true); return; }
                aiDataPointer = aiData.Pointer;
            }
            catch (Exception ex) { RecordAttempt(profileId, aiDataPointer, expectedPointer, readbackPointer, "AIData access/cast failed: " + ex); pitTeam.Modules.Logger.LogError("Boss AIData binding could not resolve AIData for " + profileId + ": " + ex); return; }
            if (aiDataPointer == IntPtr.Zero) { RecordAttempt(profileId, aiDataPointer, expectedPointer, readbackPointer, "Resolved AIData has a zero native pointer."); pitTeam.Modules.Logger.LogError("Boss AIData binding refused zero-pointer AIData for " + profileId); return; }
            AIBossPlayer? previousBoss;
            try { previousBoss = aiData._AIBossPlayer_k__BackingField; }
            catch (Exception ex) { RecordAttempt(profileId, aiDataPointer, expectedPointer, readbackPointer, "Current boss getter failed: " + ex); pitTeam.Modules.Logger.LogError("Boss AIData binding could not read current boss for " + profileId + ": " + ex); return; }
            if (SameBoss(previousBoss, mappedBoss)) { readbackPointer = ReferenceEquals(previousBoss, null) ? IntPtr.Zero : previousBoss.Pointer; RecordAttempt(profileId, aiDataPointer, expectedPointer, readbackPointer, success: true); return; }
            if (!ReferenceEquals(previousBoss, null) && previousBoss.Pointer == IntPtr.Zero) { RecordAttempt(profileId, aiDataPointer, expectedPointer, readbackPointer, "Current boss has a zero native pointer; preserving it."); pitTeam.Modules.Logger.LogError("Boss AIData binding preserved zero-pointer current boss for " + profileId); return; }
            try
            {
                aiData._AIBossPlayer_k__BackingField = mappedBoss;
                AIBossPlayer? readback = aiData._AIBossPlayer_k__BackingField;
                readbackPointer = ReferenceEquals(readback, null) ? IntPtr.Zero : readback.Pointer;
                if (!SameBoss(readback, mappedBoss)) throw new InvalidOperationException("AIData boss setter readback did not match the registered boss native pointer.");
            }
            catch (Exception writeError)
            {
                string restoreError = string.Empty;
                try
                {
                    aiData._AIBossPlayer_k__BackingField = previousBoss;
                    AIBossPlayer? restored = aiData._AIBossPlayer_k__BackingField;
                    readbackPointer = ReferenceEquals(restored, null) ? IntPtr.Zero : restored.Pointer;
                    if (!SameBoss(restored, previousBoss)) throw new InvalidOperationException("Previous boss pointer did not restore on readback.");
                }
                catch (Exception ex) { restoreError = " Restore failed: " + ex; }
                string message = "Boss AIData binding write failed for " + profileId + "; previous native pointer was preserved/restored when possible. Write error: " + writeError + restoreError;
                RecordAttempt(profileId, aiDataPointer, expectedPointer, readbackPointer, message);
                pitTeam.Modules.Logger.LogError(message);
                return;
            }
            RecordAttempt(profileId, aiDataPointer, expectedPointer, readbackPointer, success: true);
            if (!ReferenceEquals(previousBoss, null))
            {
                try { previousBoss.Dispose(); }
                catch (Exception ex)
                {
                    Exception cleanupError = ex;
                    try
                    {
                        AIBossPlayer? afterCleanup = aiData._AIBossPlayer_k__BackingField;
                        readbackPointer = ReferenceEquals(afterCleanup, null) ? IntPtr.Zero : afterCleanup.Pointer;
                        if (!SameBoss(afterCleanup, mappedBoss))
                            cleanupError = new InvalidOperationException("New boss pointer no longer matches after prior boss cleanup failure; readback=" + readbackPointer.ToString("X"), ex);
                    }
                    catch (Exception readbackError)
                    {
                        cleanupError = new AggregateException("Prior boss cleanup failed and new boss readback also failed.", ex, readbackError);
                    }
                    RecordCleanupFailure(profileId, aiDataPointer, expectedPointer, readbackPointer, cleanupError);
                }
            }
        }
    }

    internal class AIDataContructPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            pitTeam.Compat.NativeConstructorPatchTargets.EnsureInstalled();
            return AccessTools.Constructor(typeof(AIData), new Type[] { typeof(BotOwner), typeof(Player) });
        }

        [PatchPostfix]
        private static void PatchPostfix(AIData __instance, BotOwner owner, Player player)
        {
            pitTeam.Compat.NativeConstructorPatchTargets.RecordAIDataCallback(__instance.Pointer);
            if (player != null) RegisteredBossAIDataBinder.BindMappedBoss(player);
        }
    }
    /** Handle firing TeamStatus and boss gesture commands inside PlayPhraseOrGesture so enemies do not hear command-only traffic. **/
    internal class PlayPhraseOrGesturePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GamePlayerOwner), "PlayPhraseOrGesture");
        }

        [PatchPrefix]
        private static bool PatchPrefix(GamePlayerOwner __instance, int actionId, bool aggressive)
        {
            if ((EPhraseTrigger)actionId == (EPhraseTrigger)CustomPhrases.ViewBackpack)
            {
                TeammateBackpackInspection.TryOpenFromQuickInteraction(__instance);
                return false;
            }

            pitAIBossPlayer? boss = BossPlayers.GetBoss(__instance.Player.ProfileId);
            bool isGesture = EFT.UI.Gestures.GestureCommands.IsPlayerGesture(actionId);
            EPhraseTrigger phrase = (EPhraseTrigger)actionId;

            if (!isGesture && IsLootCommandPhrase(phrase))
            {
                UnityEngine.Object liveTarget = __instance.Player?.InteractableObject as UnityEngine.Object;
                Logger.LogInfo(
                    $"[LootCommand][Route] result=menuAction phrase={phrase} " +
                    $"liveTarget='{DescribeLootTarget(liveTarget)}' " +
                    $"storedPickup='{DescribeLootTarget(InteractableObjects.GetCurLootItem())}' " +
                    $"storedBody='{DescribeLootTarget(InteractableObjects.GetCurBodyLootTarget())}' " +
                    $"storedContainer='{DescribeLootTarget(InteractableObjects.GetCurLootContainerTarget())}'");
                if (phrase == EPhraseTrigger.CheckHim || phrase == EPhraseTrigger.LootBody)
                {
                    try
                    {
                        Spt5BodyLootDiagnostics.RecordRoute("PlayPhraseOrGesture", "menuAction", () =>
                        {
                            string? requesterId = __instance.Player?.ProfileId;
                            string? bossId = boss?.realPlayer?.ProfileId;
                            var corpse = InteractableObjects.GetCurBodyLootTarget();
                            string? corpsePointer = corpse == null ? null : corpse.Pointer.ToInt64().ToString("X");
                            return new Spt5BodyLootDiagnostics.RouteFields(
                                phrase.ToString(),
                                requesterId,
                                bossId,
                                requesterId != null && bossId != null ? string.Equals(requesterId, bossId, StringComparison.Ordinal) : (bool?)null,
                                corpsePointer);
                        });
                    }
                    catch { }
                }
            }

            if (!isGesture &&
                phrase == (EPhraseTrigger)CustomPhrases.TeamStatus)
            {

                if (boss != null)
                {
                    EventInfo info = new EventInfo
                    {
                        phrase = (EPhraseTrigger)CustomPhrases.TeamStatus,
                        PlayerRequester = __instance.Player
                    };

                    boss.PhraseSaid(info);

                }
                return false;

            }
            else
            {
                // fix for boss gestures not propagating to followers
                EInteraction gesture = (EInteraction)actionId;
                if (gesture == (EInteraction)CustomGestures.OverThere)
                {
                    BossGestureCommandRouter.TryPlayOverThereGesture(__instance.Player);
                    return false;
                }

                if (boss != null && BossGestureCommandRouter.IsBossCommandGesture(gesture))
                {
                    boss.GestusShown(new GestureData
                    {
                        Gesture = gesture,
                        Player = __instance.Player
                    });
                }

            }

            return true;
        }

        private static bool IsLootCommandPhrase(EPhraseTrigger phrase)
        {
            return phrase == EPhraseTrigger.LootGeneric ||
                   phrase == EPhraseTrigger.LootWeapon ||
                   phrase == EPhraseTrigger.LootKey ||
                   phrase == EPhraseTrigger.LootMoney ||
                   phrase == EPhraseTrigger.CheckHim ||
                   phrase == EPhraseTrigger.LootBody ||
                   phrase == EPhraseTrigger.LootContainer;
        }

        private static string DescribeLootTarget(UnityEngine.Object target)
        {
            return target != null
                ? $"{target.name}#{target.GetInstanceID()}"
                : "none";
        }
    }

    internal class QuickMumbleStartViewBackpackPatch : ModulePatch
    {

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GamePlayerOwner), nameof(GamePlayerOwner.QuickMumbleStart));
        }

        [PatchPrefix]
        private static bool PatchPrefix(GamePlayerOwner __instance)
        {
            if (__instance == null) return true;

            EPhraseTrigger viewBackpackPhrase = (EPhraseTrigger)CustomPhrases.ViewBackpack;
            EFT.UI.IBattleUIScreenController battleUi = __instance.BattleUIScreenController;
            var quickPanel = battleUi?.GesturesQuickPanel;
            if (quickPanel?.PrioritizedCommand != viewBackpackPhrase)
            {
                return true;
            }

            Player player = __instance.Player;
            if (player == null || !TeammateBackpackInspection.CanShowQuickInteraction(player))
            {
                return true;
            }

            quickPanel.ActivateCommand();
            if (!TeammateBackpackInspection.TryOpenFromQuickInteraction(__instance))
            {
                return true;
            }
            return false;
        }
    }

    /** Have follower kills grant the same raid XP counters used by the legacy plugin. **/
    internal class PlayerKilledPatch : ModulePatch
    {
        private const string KillMessageRoute = "/singleplayer/pitfireteam/postraid/kill-message";
        private static readonly HashSet<string> RecordedKillMessageVictims = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> PlayerDamagedPmcVictims = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> FriendlyBeforePlayerDamageVictims = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> RecordedDeadSquadmates = new HashSet<string>(StringComparer.Ordinal);
        private static readonly object RecordedKillMessageLock = new object();
        private static readonly object RecordedDeadSquadmatesLock = new object();
        private static FieldInfo _questControllerProfileField;
        private static bool _questControllerProfileFieldPrepared;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Player), "OnBeenKilledByAggressor");
        }

        [PatchPrefix]
        private static void PatchPrefix(Player __instance, IPlayer aggressor)
        {
            try
            {
                global::pitTeam.Utils.PingTeamates.TryRememberEnemyDown(__instance, aggressor);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        [PatchPostfix]
        private static void PatchPostfix(Player __instance, IPlayer aggressor, EFT.Ballistics.DamageInfo damageInfo, EBodyPart bodyPart, EDamageType lethalDamageType)
        {
            pitTeam.Compat.Spt5RuntimeInstallProbe.RecordKill(__instance, aggressor);
            try
            {
                TryRecordDeadSquadmate(__instance, aggressor, bodyPart, lethalDamageType);
                TryRecordPlayerKillMessage(__instance, aggressor);

                if (aggressor?.Profile?.Info?.Settings == null || __instance?.GameWorld == null)
                {
                    return;
                }

                Player followerPlayer = __instance.GameWorld.GetAlivePlayerByProfileID(aggressor.ProfileId);
                if (followerPlayer == null)
                {
                    return;
                }

                BotFollowerPlayer follower = BossPlayers.GetFollowers().Find(fl => fl.GetBot().ProfileId == followerPlayer.ProfileId);
                if (follower == null || !follower.IsSquadMate)
                {
                    return;
                }

                SquadRaidKillReport.RecordTeammateKill(followerPlayer, __instance);
                TryCreditFollowerKillQuestProgress(__instance, followerPlayer, follower, damageInfo, bodyPart);

                EPlayerSide killedPlayerSide = __instance.Side;
                EFT.Counters.CountersCollection sessionCounters = followerPlayer.Profile.EftStats.SessionCounters;
                var experienceSettings = Singleton<EFT.GlobalConfiguration>.Instance.Experience;

                float headshotMultiplier = 0f;
                if (bodyPart == EBodyPart.Head)
                {
                    if (killedPlayerSide - EPlayerSide.Usec > 1)
                    {
                        if (killedPlayerSide == EPlayerSide.Savage)
                        {
                            headshotMultiplier = experienceSettings.Kill.BotHeadShotMult;
                        }
                    }
                    else
                    {
                        headshotMultiplier = experienceSettings.Kill.PmcHeadShotMult;
                    }
                }

                int killExperience = __instance.Profile.Info.Settings.Experience;
                int baseKillExperience = killExperience;
                switch (killedPlayerSide)
                {
                    case EPlayerSide.Usec:
                    case EPlayerSide.Bear:
                        baseKillExperience = experienceSettings.Kill.VictimLevelExp;
                        break;
                    case EPlayerSide.Savage:
                        if (baseKillExperience < 0)
                        {
                            baseKillExperience = experienceSettings.Kill.VictimBotLevelExp;
                        }

                        break;
                }

                int killCount = sessionCounters.GetInt(EFT.Counters.PredefinedCounters.Kills);
                sessionCounters.AddInt(1, EFT.Counters.PredefinedCounters.Kills);
                float streakMultiplier = (float)experienceSettings.Kill.GetKillingBonusPercent(killCount) / 100f;
                int bodyPartBonus = (int)(baseKillExperience * headshotMultiplier);
                int streakBonus = (int)(baseKillExperience * streakMultiplier);

                if (baseKillExperience > 0)
                {
                    sessionCounters.AddInt(baseKillExperience, EFT.Counters.PredefinedCounters.ExpKillBase);
                }

                if (bodyPartBonus > 0)
                {
                    sessionCounters.AddInt(bodyPartBonus, EFT.Counters.PredefinedCounters.ExpKillBodyPartBonus);
                }

                if (streakBonus > 0)
                {
                    sessionCounters.AddInt(streakBonus, EFT.Counters.PredefinedCounters.ExpKillStreakBonus);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        internal static void TryRecordDeadSquadmate(
            Player player,
            IPlayer aggressor,
            EBodyPart bodyPart,
            EDamageType lethalDamageType)
        {
            try
            {
                if (player == null || string.IsNullOrWhiteSpace(player.ProfileId))
                {
                    return;
                }

                lock (RecordedDeadSquadmatesLock)
                {
                    if (!RecordedDeadSquadmates.Add(player.ProfileId))
                    {
                        return;
                    }
                }

                BotFollowerPlayer deadFollower = BossPlayers.GetFollowers()
                    .Find(follower => follower?.GetBot()?.ProfileId == player.ProfileId);
                if (deadFollower == null || !deadFollower.IsSquadMate)
                {
                    lock (RecordedDeadSquadmatesLock)
                    {
                        RecordedDeadSquadmates.Remove(player.ProfileId);
                    }

                    return;
                }

                // A pending hands switch may finish after follower roster/chat cleanup begins.
                // Preserve exact follower identity for the OnWeaponTaken death guard before any
                // cleanup can make BossPlayers.IsFollower(...) return false.
                FollowerWeaponSwitchPolicyRuntime.MarkDeadFollowerWeaponCallbacks(deadFollower.GetBot());
                BattleRecorder.RecordFollowerDeath(
                    deadFollower,
                    player,
                    aggressor,
                    bodyPart,
                    lethalDamageType);
                NpcMessage.RemoveNpc(player.ProfileId, true);
                FollowerDeathEscapeResolver.RecordFallenSquadmate(player);
            }
            catch (Exception ex)
            {
                Modules.Logger.LogError("Failed to record dead squadmate for loadout loss");
                Modules.Logger.LogError(ex);
            }
        }

        internal static void ReleaseDeadSquadmateRecord(Player player)
        {
            if (player == null || string.IsNullOrWhiteSpace(player.ProfileId))
            {
                return;
            }

            lock (RecordedDeadSquadmatesLock)
            {
                RecordedDeadSquadmates.Remove(player.ProfileId);
            }
        }

        internal static void TryRememberFriendlyPmcBeforePlayerDamage(EFT.Ballistics.DamageInfo damageInfo, Player target)
        {
            IPlayer attacker = damageInfo.Player?.iPlayer;
            if (attacker == null ||
                !attacker.IsYourPlayer ||
                target == null ||
                !target.IsAI ||
                target.Side != attacker.Side ||
                !IsPmc(target.Side) ||
                string.IsNullOrWhiteSpace(target.ProfileId) ||
                !IsFriendlyPmcKillMessageContextEnabled())
            {
                return;
            }

            BotOwner targetBot = target.AIData?.BotOwner;
            BotsGroup targetGroup = targetBot?.BotsGroup;
            if (targetBot == null || targetGroup == null || targetBot.EnemiesController == null)
            {
                return;
            }

            lock (RecordedKillMessageLock)
            {
                // The first direct player hit is the relationship boundary. Later hits must not
                // turn a bot that was already hostile into an eligible friendly-fire victim.
                if (!PlayerDamagedPmcVictims.Add(target.ProfileId))
                {
                    return;
                }

                bool wasAlreadyEnemy =
                    targetGroup.IsEnemy(attacker) ||
                    targetBot.EnemiesController.IsEnemy(attacker) ||
                    string.Equals(
                        targetBot.Memory?.GoalEnemy?.Person?.ProfileId,
                        attacker.ProfileId,
                        StringComparison.Ordinal);

                if (!wasAlreadyEnemy)
                {
                    FriendlyBeforePlayerDamageVictims.Add(target.ProfileId);
                }
            }
        }

        internal static void ResetKillMessageRaidState()
        {
            lock (RecordedKillMessageLock)
            {
                RecordedKillMessageVictims.Clear();
                PlayerDamagedPmcVictims.Clear();
                FriendlyBeforePlayerDamageVictims.Clear();
            }
        }

        private static void TryRecordPlayerKillMessage(Player victim, IPlayer aggressor)
        {
            try
            {
                if (victim == null || aggressor == null || !aggressor.IsYourPlayer || !victim.IsAI)
                {
                    return;
                }

                string messageKind = GetKillMessageKind(victim, aggressor);
                if (string.IsNullOrEmpty(messageKind))
                {
                    return;
                }

                bool messagesEnabled = pitFireTeam.npcSendMessage?.Value == true;
                string messageText = messagesEnabled ? GetKillMessageText(messageKind) : string.Empty;
                if (messagesEnabled && string.IsNullOrWhiteSpace(messageText))
                {
                    return;
                }

                string victimProfileId = victim.ProfileId;
                if (string.IsNullOrEmpty(victimProfileId))
                {
                    return;
                }

                lock (RecordedKillMessageLock)
                {
                    if (!RecordedKillMessageVictims.Add(victimProfileId))
                    {
                        return;
                    }
                }

                string json = System.Text.Json.JsonSerializer.Serialize(new
                {
                    victimProfileId,
                    victimAccountId = victim.AccountId,
                    messageKind,
                    messageText,
                });

                string victimNickname = victim.Profile?.Info?.Nickname;
                Task.Run(() =>
                {
                    try
                    {
                        RequestHandler.PostJson(KillMessageRoute, json);
                    }
                    catch (Exception ex)
                    {
                        Modules.Logger.LogError($"Failed to record post-raid kill message for {victimNickname}");
                        Modules.Logger.LogError(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                Modules.Logger.LogError("Failed to classify post-raid kill message");
                Modules.Logger.LogError(ex);
            }
        }

        private static string GetKillMessageKind(Player victim, IPlayer aggressor)
        {
            if (!IsFriendlyPmcKillMessageContextEnabled() ||
                !WasFriendlyBeforePlayerDamage(victim.ProfileId))
            {
                return string.Empty;
            }

            BotFollowerPlayer follower = BossPlayers.GetFollowerByProfileId(victim.ProfileId);
            if (follower != null)
            {
                return follower.IsSquadMate ? string.Empty : "traitor";
            }

            return IsPmc(victim.Side) && victim.Side == aggressor.Side ? "jerk" : string.Empty;
        }

        private static bool WasFriendlyBeforePlayerDamage(string victimProfileId)
        {
            if (string.IsNullOrWhiteSpace(victimProfileId))
            {
                return false;
            }

            lock (RecordedKillMessageLock)
            {
                return FriendlyBeforePlayerDamageVictims.Contains(victimProfileId);
            }
        }

        private static bool IsFriendlyPmcKillMessageContextEnabled()
        {
            return pitFireTeam.pitFireTeamFLAG?.Value == true && pitFireTeam.badGuy?.Value != true;
        }

        private static bool IsPmc(EPlayerSide side)
        {
            return side == EPlayerSide.Bear || side == EPlayerSide.Usec;
        }

        private static string GetKillMessageText(string messageKind)
        {
            string[] messages = messageKind == "traitor"
                ? pitFireTeam.optionsLang?.traitorKillMessages
                : pitFireTeam.optionsLang?.jerkKillMessages;

            if (messages == null || messages.Length == 0)
            {
                return string.Empty;
            }

            return messages[UnityEngine.Random.Range(0, messages.Length)];
        }

        private static void TryCreditFollowerKillQuestProgress(
            Player victim,
            Player followerPlayer,
            BotFollowerPlayer follower,
            EFT.Ballistics.DamageInfo damageInfo,
            EBodyPart bodyPart)
        {
            pitAIBossPlayer boss = follower.GetBoss();
            if (boss?.realPlayer?.QuestController == null)
            {
                return;
            }

            if (Vector3.Distance(followerPlayer.Position, boss.Position) > 80f)
            {
                return;
            }

            List<string> targets = BuildQuestKillTargets(victim.Side);
            if (targets.Count == 0)
            {
                return;
            }

            EFT.Quests.QuestController questController = boss.realPlayer.QuestController;
            Profile originalProfile = null;

            try
            {
                FieldInfo profileField = GetQuestControllerProfileField();
                if (profileField != null)
                {
                    originalProfile = profileField.GetValue(questController) as Profile;
                    profileField.SetValue(questController, followerPlayer.Profile);
                }

                string location = followerPlayer.Location;
                float distance = Vector3.Distance(followerPlayer.Position, victim.Position);
                HealthEffects victimEffects = victim.HealthController?.BodyPartEffects;
                HealthEffects followerEffects = followerPlayer.HealthController?.BodyPartEffects;
                string[] followerBuffs = followerPlayer.HealthController?.ActiveBuffsNames() ?? Array.Empty<string>();
                var victimZones = victim.TriggerZones ?? new Il2CppSystem.Collections.Generic.List<string>();
                var victimEquipment = victim.Inventory?.EquippedInSlotsTemplateIds ?? new Il2CppSystem.Collections.Generic.List<string>();
                string victimRole = victim.Profile?.Info?.Settings?.Role.ToStringNoBox<WildSpawnType>() ?? string.Empty;

                foreach (string target in targets)
                {
                    questController.CheckKillConditionCounter(
                        target,
                        victim.ProfileId,
                        victimEquipment,
                        damageInfo.Weapon,
                        bodyPart,
                        location,
                        distance,
                        victimRole,
                        victim.CurrentHour,
                        victimEffects,
                        followerEffects,
                        victimZones,
                        followerBuffs);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
            finally
            {
                try
                {
                    FieldInfo profileField = GetQuestControllerProfileField();
                    if (profileField != null && originalProfile != null)
                    {
                        profileField.SetValue(questController, originalProfile);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex);
                }
            }
        }

        private static List<string> BuildQuestKillTargets(EPlayerSide victimSide)
        {
            List<string> targets = new List<string> { "Any" };
            switch (victimSide)
            {
                case EPlayerSide.Usec:
                    targets.Add("Usec");
                    targets.Add("AnyPmc");
                    break;
                case EPlayerSide.Bear:
                    targets.Add("Bear");
                    targets.Add("AnyPmc");
                    break;
                case EPlayerSide.Savage:
                    targets.Add("Savage");
                    targets.Add("Bot");
                    break;
            }

            return targets;
        }

        private static FieldInfo GetQuestControllerProfileField()
        {
            if (_questControllerProfileFieldPrepared)
            {
                return _questControllerProfileField;
            }

            _questControllerProfileFieldPrepared = true;
            _questControllerProfileField = typeof(EFT.Quests.QuestController).GetField("Profile", BindingFlags.Public | BindingFlags.Instance);
            if (_questControllerProfileField == null)
            {
                return null;
            }

            FieldInfo attributesField = typeof(FieldInfo).GetField("m_fieldAttributes", BindingFlags.NonPublic | BindingFlags.Instance);
            attributesField?.SetValue(_questControllerProfileField, _questControllerProfileField.Attributes & ~FieldAttributes.InitOnly);
            return _questControllerProfileField;
        }
    }

    internal sealed class PlayerDeadFallbackPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Player), "OnDead");
        }

        [PatchPostfix]
        private static void PatchPostfix(Player __instance, EDamageType damageType)
        {
            try
            {
                PlayerKilledPatch.TryRecordDeadSquadmate(
                    __instance,
                    aggressor: null,
                    bodyPart: EBodyPart.Common,
                    lethalDamageType: damageType);
            }
            finally
            {
                PlayerKilledPatch.ReleaseDeadSquadmateRecord(__instance);
            }
        }
    }

    /** Report the exact target line when the player fires during a fight. **/
    internal sealed class PlayerMakingShotPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Player), nameof(Player.OnMakingShot));
        }

        [PatchPostfix]
        private static void PatchPostfix(Player __instance)
        {
            pitTeam.Compat.Spt5RuntimeInstallProbe.RecordShot(__instance);
            pitAIBossPlayer? bossPlayer = BossPlayers.GetBoss(__instance.ProfileId);
            if (bossPlayer == null)
            {
                return;
            }

            Vector3 shotOrigin = __instance.PlayerBones?.WeaponRoot?.position ??
                                 (__instance.Position + Vector3.up * 1.2f);
            Vector3 shotDirection = __instance.LookDirection;
            if (__instance.HandsController is Player.FirearmController firearmController)
            {
                shotOrigin = firearmController.FireportPosition;
                shotDirection = firearmController.WeaponDirection;
            }

            bossPlayer.MarkBossShot(shotOrigin, shotDirection);
        }
    }

    /** Have followers increase weapon skills when they shoot. **/
    internal class PlayerShotPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Player), "ExecuteShotSkill");
        }

        [PatchPrefix]
        private static bool PatchPrefix(Player __instance, Item weapon)
        {
            pitAIBossPlayer? bossPlayer = BossPlayers.GetBoss(__instance.ProfileId);

            foreach (var follower in BossPlayers.GetFollowers())
            {
                if (!follower.IsSquadMate || follower.GetBot().ProfileId != __instance.ProfileId)
                {
                    continue;
                }

                if (weapon is not EFT.InventoryLogic.ThrowWeap)
                {
                    __instance.Skills.WeaponShotAction.Complete(weapon, 1f);
                }

                return false;
            }

            return true;
        }
    }
}
