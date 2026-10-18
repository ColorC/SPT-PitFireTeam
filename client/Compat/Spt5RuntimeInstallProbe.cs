using System;
using System.Collections.Generic;
using System.Linq;
using EFT;
using HarmonyLib;
using UnityEngine;

namespace pitTeam.Compat
{
    // Observation only. No subscriptions or event storage in a normal installation.
    internal static class Spt5RuntimeInstallProbe
    {
        internal static bool Enabled => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PIT_SPT5_PROBE_ROOT"));
        private static readonly Queue<object> Events = new Queue<object>();
        private static readonly Dictionary<string, Spt5BeingHitActionSubscription> Subscriptions = new Dictionary<string, Spt5BeingHitActionSubscription>(StringComparer.Ordinal);
        private static readonly Dictionary<string, long> Shots = new Dictionary<string, long>(StringComparer.Ordinal);
        private static long sequence;
        private static IntPtr worldPointer;
        private static string? error;
        private static int dropped;
        private static object? completedRaid;

        private static void Add(string kind, string? attacker, string? victim, float amount, string? bodyPart)
        {
            lock (Events)
            {
                Events.Enqueue(new { sequence = ++sequence, kind, attackerProfileId = attacker, victimProfileId = victim, amount, bodyPart, timestamp = DateTimeOffset.UtcNow, realtime = Time.realtimeSinceStartup, frame = Time.frameCount });
                if (Events.Count > 2048) { Events.Dequeue(); dropped++; }
            }
        }
        internal static void RecordShot(Player player)
        {
            if (!Enabled || player == null) return;
            try
            {
                lock (Events) { Shots.TryGetValue(player.ProfileId, out long count); Shots[player.ProfileId] = count + 1; }
                Add("shot", player.ProfileId, null, 0, null);
            }
            catch (Exception ex) { error = ex.ToString(); }
        }
        internal static void RecordKill(Player victim, IPlayer attacker)
        {
            if (!Enabled) return;
            try { Add("kill", attacker?.ProfileId, victim?.ProfileId, 0, null); }
            catch (Exception ex) { error = ex.ToString(); }
        }
        public static object GetProbeSnapshot()
        {
            return Capture();
        }
        public static object ReportStatus()
        {
            if (!Enabled) throw new InvalidOperationException("shadow-probe-root-required");
            var player = EFT.GamePlayerOwner.MyPlayer ?? throw new InvalidOperationException("main-player-unavailable");
            var boss = pitTeam.Modules.BossPlayers.GetBoss(player.ProfileId) ?? throw new InvalidOperationException("boss-unavailable");
            boss.PhraseSaid(new GlobalEventDispatcher.PhraseDelegateInfo
            {
                phrase = (EPhraseTrigger)10001,
                PlayerRequester = boss.realPlayer
            });
            return new { dispatched = true, trigger = 10001, physical = false };
        }
        public static object LootBody()
        {
            if (!Enabled) throw new InvalidOperationException("shadow-probe-root-required");
            var player = EFT.GamePlayerOwner.MyPlayer ?? throw new InvalidOperationException("main-player-unavailable");
            if (player.ProfileId != "6ace6340c399ad84d0a90502") throw new InvalidOperationException("private-qa-player-required");
            var corpse = pitTeam.Modules.InteractableObjects.GetCurBodyLootTarget()
                ?? throw new InvalidOperationException("native-current-body-target-required");
            if (Vector3.Distance(player.Position, corpse.transform.position) > 6f) throw new InvalidOperationException("current-body-target-too-far");
            var boss = pitTeam.Modules.BossPlayers.GetBoss(player.ProfileId) ?? throw new InvalidOperationException("boss-unavailable");
            boss.PhraseSaid(new GlobalEventDispatcher.PhraseDelegateInfo { phrase = EPhraseTrigger.LootBody, PlayerRequester = player });
            return new { passed = true, dispatched = true, trigger = EPhraseTrigger.LootBody.ToString(), targetPointer = corpse.Pointer.ToString("X"),
                physical = false, method = "production boss phrase router; native camera-selected corpse; no inventory mutation by probe" };
        }

        public static object Command(string command)
        {
            Spt5RuntimeRaidProbe.RequireShadow();
            ManagedFramePump.RequireOwnerThread();
            var player = GamePlayerOwner.MyPlayer ?? throw new InvalidOperationException("main-player-unavailable");
            if (player.ProfileId != "6ace6340c399ad84d0a90502" || player.HealthController?.IsAlive != true)
                throw new InvalidOperationException("live-private-qa-player-required");
            var boss = pitTeam.Modules.BossPlayers.GetBoss(player.ProfileId) ?? throw new InvalidOperationException("boss-unavailable");
            if (command == "over-there")
                return new { passed = pitTeam.Patches.BossGestureCommandRouter.TryPlayOverThereGesture(player), command, physical = false };
            EPhraseTrigger phrase = command switch
            {
                "stop" => EPhraseTrigger.Stop, "follow" => EPhraseTrigger.FollowMe,
                "hold" => EPhraseTrigger.HoldPosition, "go" => EPhraseTrigger.Gogogo,
                "patrol" => EPhraseTrigger.OnYourOwn, "cover" => EPhraseTrigger.CoverMe,
                "regroup" => EPhraseTrigger.Regroup, "extract" => EPhraseTrigger.ExitLocated,
                "forward" => EPhraseTrigger.GoForward, "contact" => EPhraseTrigger.OnRepeatedContact,
                "look" => EPhraseTrigger.Look, "left" => EPhraseTrigger.LeftFlank,
                "right" => EPhraseTrigger.RightFlank, "front" => EPhraseTrigger.InTheFront,
                "behind" => EPhraseTrigger.OnSix, "sniper" => EPhraseTrigger.NeedSniper,
                "help" => EPhraseTrigger.NeedHelp, "suppress" => EPhraseTrigger.Suppress,
                "open-door" => EPhraseTrigger.OpenDoor, "loot-item" => EPhraseTrigger.LootGeneric,
                "loot-container" => EPhraseTrigger.LootContainer,
                _ => throw new ArgumentException("unsupported-qa-squad-command", nameof(command))
            };
            boss.PhraseSaid(new GlobalEventDispatcher.PhraseDelegateInfo { phrase = phrase, PlayerRequester = player });
            return new { passed = true, command, phrase = phrase.ToString(), physical = false,
                method = "production boss command router; validate resulting state/actions separately" };
        }

        private static object Inventory(EFT.InventoryLogic.InventoryEquipment? equipment)
        {
            if (equipment == null) return new { available = false };
            var items = new List<object>();
            var nativeItems = new List<EFT.InventoryLogic.Item>();
            equipment.GetAllItemsNonAlloc(nativeItems);
            foreach (var item in nativeItems)
            {
                if (item == null) continue;
                if (items.Count >= 512) break;
                items.Add(new { id = item.Id.ToString(), templateId = item.TemplateId.ToString(), stackCount = item.StackObjectsCount });
            }
            return new { available = true, items, limit = 512 };
        }

        private static object SquadState()
        {
            var rows = new List<object>();
            var player = GamePlayerOwner.MyPlayer;
            if (player != null && pitTeam.Modules.BossPlayers.Instance != null)
                foreach (var follower in pitTeam.Modules.BossPlayers.GetFollowersByBoss(player.ProfileId))
                {
                    var bot = follower.GetBot();
                    if (bot == null) continue;
                    follower.TryPeekActiveCommand(out var command, out var target, out var until);
                    rows.Add(new { profileId = bot.ProfileId, canPatrol = follower.CanPatrol, independent = follower.CombatIndependent,
                        command = command.ToString(), target = new { x = target.x, y = target.y, z = target.z }, until,
                        tightRegroup = follower.TightRegroupRequested, tactic = follower.CombatTactic.ToString(), aggression = follower.CombatAggression,
                        storedItemIds = pitTeam.Modules.InteractableObjects.GetStoredItems(bot.ProfileId)?.ToArray() ?? Array.Empty<string>(),
                        inventory = Inventory(bot.GetPlayer?.InventoryController?.Inventory?.Equipment) });
                }
            var corpse = pitTeam.Modules.InteractableObjects.GetCurBodyLootTarget();
            var corpseEquipment = Il2CppBoundary.CastNativeOrNull<EFT.InventoryLogic.InventoryEquipment>(corpse?.ItemOwner?.RootItem);
            return new { followers = rows, currentCorpsePointer = corpse?.Pointer.ToString("X"), corpseInventory = Inventory(corpseEquipment),
                bodyLoot = Spt5BodyLootDiagnostics.Snapshot(), mainInput = player == null ? null : (object)new {
                    x = player.InputDirection.x, y = player.InputDirection.y, ignored = GamePlayerOwner.IgnoreInput } };
        }
        private static object Capture()
        {
            if (!Enabled) return new { available = false, reason = "shadow-probe-root-required" };
            try
            {
                var world = Comfort.Common.Singleton<GameWorld>.Instance;
                IntPtr pointer = world?.Pointer ?? IntPtr.Zero;
                if (pointer != worldPointer)
                {
                    if (worldPointer != IntPtr.Zero && pointer == IntPtr.Zero)
                        lock (Events) completedRaid = new { worldPointer = worldPointer.ToString("X"), timestamp = DateTimeOffset.UtcNow,
                            shots = new Dictionary<string, long>(Shots), events = Events.ToArray(), dropped, error };
                    foreach (var subscription in Subscriptions.Values) subscription.Dispose();
                    Subscriptions.Clear();
                    lock (Events) { Events.Clear(); Shots.Clear(); sequence = 0; dropped = 0; }
                    worldPointer = pointer; error = null;
                }
                if (world?.AllAlivePlayersList != null)
                    foreach (var player in world.AllAlivePlayersList)
                    {
                        if (player == null || Subscriptions.ContainsKey(player.ProfileId)) continue;
                        string victimId = player.ProfileId;
                        Subscriptions.Add(victimId, Spt5BeingHitActionBridge.Subscribe(player, (info, part, amount) =>
                        {
                            try { Add("damage", info.Player?.iPlayer?.ProfileId, victimId, amount, part.ToString()); }
                            catch (Exception ex) { error = ex.ToString(); }
                        }));
                    }
                var target = AccessTools.Method(typeof(Player), nameof(Player.OnMakingShot));
                bool shotHookRegistered = Harmony.GetPatchInfo(target)?.Postfixes.Any(p => p.PatchMethod.DeclaringType == typeof(pitTeam.Patches.PlayerMakingShotPatch)) == true;
                var squad = SquadState();
                lock (Events) return new { available = true, timestamp = DateTimeOffset.UtcNow, realtime = Time.realtimeSinceStartup, frame = Time.frameCount, worldPointer = worldPointer.ToString("X"), shotHookRegistered, damageSubscriptions = Subscriptions.Keys.ToArray(), shots = new Dictionary<string, long>(Shots), events = Events.ToArray(), dropped, error, marker = pitTeam.Utils.PingTeamates.GetInstallProbeSnapshot(), squad, completedRaid };
            }
            catch (Exception ex) { error = ex.ToString(); return new { available = false, error }; }
        }
    }
}
