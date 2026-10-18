using System;
using System.Collections.Generic;
using System.Linq;
using EFT.UI;
using EFT.UI.DragAndDrop;
using EFT.InventoryLogic;
using pitTeam.Compat;
using UnityEngine;

namespace pitTeam.Patches
{
    internal partial class OtherPlayerProfileScreenPatch
    {
        private static bool IsInEditorRootByPointer(Item item, Item root)
        {
            var visited = new HashSet<IntPtr>();
            for (int depth = 0; item != null && root != null && depth < 64 && visited.Add(item.Pointer); depth++)
            {
                if (Il2CppBoundary.SameNativeObject(item, root)) return true;
                item = item.Parent?.Container?.ParentItem;
            }
            return false;
        }

        public static object QuickMoveLoadoutItem(ItemView view)
        {
            if (!Spt5RuntimeInstallProbe.Enabled
                || !string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_SHADOW_MODE"), "true", StringComparison.OrdinalIgnoreCase)
                || !Uri.TryCreate(Environment.GetEnvironmentVariable("PIT_SPT5_BACKEND_URL"), UriKind.Absolute, out var backend)
                || backend.Scheme != Uri.UriSchemeHttps || !backend.IsLoopback || backend.Port != 6971
                || ActiveProfileSession?.Profile?.ProfileId is not ("6ace6340c399ad84d0a90502" or "6ad49565f2713c7c5c904b23"))
                throw new InvalidOperationException("Editor movement QA requires an allowlisted private shadow session.");
            if (LoadoutEditorOverlayRoot == null || !LoadoutEditorOverlayRoot.activeInHierarchy
                || !IsRealDefaultLoadoutEditorCommit() || view == null || !view.gameObject.activeInHierarchy
                || !view.transform.IsChildOf(LoadoutEditorOverlayRoot.transform)
                || !Il2CppBoundary.SameNativeObject(view.ItemController, LoadoutEditorInventoryController)
                || view.Item == null || view.Item.StackObjectsCount != 1
                || !Il2CppBoundary.SameNativeObject(view.ItemContext?.Item, view.Item)
                || Il2CppBoundary.SameNativeObject(LoadoutEditorProfile?.Inventory?.Stash, ActiveProfileSession.Profile.Inventory.Stash)
                || Il2CppBoundary.SameNativeObject(LoadoutEditorProfile?.Inventory?.Equipment, ViewedProfile?.Equipment))
                throw new InvalidOperationException("Observed item is not a single item in the active staged editor.");
            bool fromEquipment = IsInEditorRootByPointer(view.Item, LoadoutEditorProfile.Inventory.Equipment);
            var sourceRoot = fromEquipment ? (Item)LoadoutEditorProfile.Inventory.Equipment : LoadoutEditorProfile.Inventory.Stash;
            if (!IsInEditorRootByPointer(view.Item, sourceRoot)
                || !EnumerateLoadoutEditorItemTree(sourceRoot).Any(item => Il2CppBoundary.SameNativeObject(item, view.Item)))
                throw new InvalidOperationException("Item does not belong to the actual staged root; matching item IDs alone are insufficient.");
            string itemId = view.Item.Id.ToString();
            string templateId = view.Item.TemplateId.ToString();
            var possible = view.ItemUiContext.QuickFindAppropriatePlace(view.ItemContext, view.ItemController, false, true, true);
            if (possible.Error != null || possible.Value == null)
                return new { passed = false, itemId, templateId, reason = possible.Error?.ToString() ?? "No native move operation." };
            var move = Il2CppBoundary.CastNativeOrNull<MoveResult>(possible.Value);
            if (move == null || move.GetIl2CppType().FullName != typeof(MoveResult).FullName || move.ItemsDestroyRequired || move.DestroysMainItem)
                throw new NotSupportedException("QA accepts only an ordinary native MoveResult without destruction, swap, split or merge.");
            Item destination = move.To?.Container?.ParentItem;
            var targetRoot = fromEquipment ? (Item)LoadoutEditorProfile.Inventory.Stash : LoadoutEditorProfile.Inventory.Equipment;
            if (!Il2CppBoundary.SameNativeObject(move.Item, view.Item)
                || !Il2CppBoundary.SameNativeObject(move._itemController, LoadoutEditorInventoryController)
                || !IsInEditorRootByPointer(destination, targetRoot)
                || !view.ItemController.CanExecute(possible.Value))
                throw new InvalidOperationException("Native move cannot execute within the opposite staged inventory root.");
            view.ItemController.RunNetworkTransaction(possible.Value, null);
            return new { passed = true, itemId, templateId, fromEquipment, eventName = "native Ctrl-click Find/CanExecute/RunNetworkTransaction", requiresUiReadback = true };
        }

        public static object GetNativeProfileValues()
        {
            try
            {
                var profile = pitTeam.Patches.OtherPlayerProfileScreenPatch.ViewedProfile;
                var window = pitTeam.Patches.OtherPlayerProfileScreenPatch.ActiveProfilePlayerModelWindow;
                if (profile == null || window == null) return new { available = false };
                var counters = profile.OverallCounters;
                object Observe(string name, Func<object> read)
                {
                    try { return new { name, passed = true, value = read() }; }
                    catch (Exception ex) { return new { name, passed = false, error = ex.ToString() }; }
                }
                object Key(EFT.Counters.CountersCollection.Identifier key)
                {
                    var names = new List<string>();
                    foreach (var name in key.Set) names.Add(name);
                    return new { names, valueType = key.ValueType.ToString() };
                }
                object ReadItems(Item root) => EnumerateLoadoutEditorItemTree(root).Select(item => new {
                    id = item.Id.ToString(), template = item.TemplateId.ToString(), count = item.StackObjectsCount,
                    root = Il2CppBoundary.SameNativeObject(item, root),
                    parent = Il2CppBoundary.SameNativeObject(item, root) ? null : item.Parent?.Container?.ParentItem?.Id.ToString()
                }).ToArray();
                var raw = Observe("rawCounters", () =>
                {
                    var entries = new List<object>();
                    if (counters?._counters != null)
                    foreach (var entry in counters._counters)
                    {
                        if (entries.Count >= 512) break;
                        entries.Add(new { key = Key(entry.Key), rawValue = entry.Value });
                    }
                    return entries;
                });
                var queries = new List<object>();
                var predefined = EFT.Counters.PredefinedCounters.Kills;
                object ReadStatsCounters(EFT.Counters.CountersCollection source)
                {
                    if (source == null) return new { available = false };
                    var entries = new List<object>();
                    if (source._counters != null)
                        foreach (var entry in source._counters)
                            if (entries.Count < 512) entries.Add(new { key = Key(entry.Key), rawValue = entry.Value });
                    return new { available = true, pointer = source.Pointer.ToString("X"), entries,
                        killsLong = Observe("GetLong", () => source.GetLong(predefined)),
                        killsInt = Observe("GetInt", () => source.GetInt(predefined)) };
                }
                queries.Add(Observe("PredefinedKills.GetLong", () => counters.GetLong(predefined)));
                queries.Add(Observe("PredefinedKills.GetInt", () => counters.GetInt(predefined)));
                queries.Add(Observe("PredefinedKills.GetFloat", () => counters.GetFloat(predefined)));
                foreach (var tags in new[] { new[] { EFT.Counters.CounterTag.Kills }, new[] { EFT.Counters.CounterTag.Kills, EFT.Counters.CounterTag.Pmc },
                    new[] { EFT.Counters.CounterTag.KilledBear }, new[] { EFT.Counters.CounterTag.KilledUsec }, new[] { EFT.Counters.CounterTag.KilledSavage } })
                {
                    var args = tags.Select(tag => (Il2CppSystem.Object)Il2CppSystem.Enum.ToObject(Il2CppInterop.Runtime.Il2CppType.Of<EFT.Counters.CounterTag>(), (int)tag)).ToArray();
                    string name = string.Join("+", tags);
                    queries.Add(Observe(name + ".GetLong", () => counters.GetLong(args)));
                    queries.Add(Observe(name + ".GetInt", () => counters.GetInt(args)));
                    queries.Add(Observe(name + ".GetFloat", () => counters.GetFloat(args)));
                    queries.Add(Observe(name + ".GetAllInt", () => counters.GetAllInt(args)));
                }
                object ReadSkills()
                {
                var skills = new List<object>();
                foreach (var skill in profile.Skills.Skills)
                    if (skill != null) skills.Add(new { id = skill.Id.ToString(), skill.Locked, skill.Level, current = skill.Current,
                        elite = skill.IsEliteLevel, levelProgress = skill.LevelProgress, sessionProgress = skill.ProgressValue });
                bool prone = profile.Skills.TryGetSkill(EFT.ESkillId.ProneMovement, out var proneSkill);
                bool recoil = profile.Skills.TryGetSkill(EFT.ESkillId.RecoilControl, out var recoilSkill);
                return new { skills, legacySkills = new { prone, pronePropertyPresent = profile.Skills.ProneMovement != null,
                    recoil, recoilPropertyPresent = profile.Skills.RecoilControl != null } };
                }
                object ReadLeaderSkills()
                {
                    var skills = new List<object>();
                    var leader = ActiveProfileSession?.Profile;
                    if (leader?.Skills?.Skills != null)
                        foreach (var skill in leader.Skills.Skills)
                            if (skill != null) skills.Add(new { id = skill.Id.ToString(), skill.Locked, skill.Level, current = skill.Current,
                                elite = skill.IsEliteLevel, levelProgress = skill.LevelProgress, sessionProgress = skill.ProgressValue });
                    return new { profileId = leader?.ProfileId, skills };
                }
                object ReadMastering(EFT.SkillManager manager)
                {
                    var rows = new List<object>();
                    if (manager?.Mastering != null)
                        foreach (var pair in manager.Mastering)
                        {
                            var skill = pair.Value;
                            var group = skill?.MasteringGroup;
                            if (skill != null) rows.Add(new { id = pair.Key, current = skill.Current, skill.Level,
                                skill.LevelProgress, skill.LevelingThreshold, skill.Lvl1, skill.Lvl2,
                                group = group?.Id, level2 = group?.Level2, level3 = group?.Level3 });
                        }
                    return rows;
                }
                object ReadPreviewAnimator()
                {
                var loader = window._playerModelView?._playerModelLoader;
                var wrapper = Il2CppBoundary.CastNativeOrNull<AnimationSystem.UnityAnimatorWrapper>(loader?._weaponAnimator);
                var nativeAnimator = wrapper?._animator;
                var cachedParameters = wrapper?._parametersCache;
                var actualParameters = nativeAnimator?.parameters;
                return new { available = wrapper != null, weapon = loader?._weaponPrefab?._weaponData?.TemplateId.ToString(),
                    animatorName = nativeAnimator?.name, layer = nativeAnimator == null ? (int?)null : nativeAnimator.gameObject.layer,
                    controller = nativeAnimator?.runtimeAnimatorController?.name, cachedController = wrapper?._cachedRuntimeAnimatorController?.name,
                    actualCount = actualParameters?.Length, cachedCount = cachedParameters?.Length,
                    cached = cachedParameters == null ? null : Enumerable.Range(0, cachedParameters.Length).Select(index => new { index,
                        name = cachedParameters[index]?.name, hash = cachedParameters[index]?.nameHash }).ToArray(),
                    actual = actualParameters == null ? null : Enumerable.Range(0, actualParameters.Length).Select(index => new { index,
                        name = actualParameters[index]?.name, hash = actualParameters[index]?.nameHash }).ToArray() };
                }
                object ReadGridItems()
                {
                var skillGrid = pitTeam.Patches.OtherPlayerProfileScreenPatch.SkillsPanel?._skillThumbs;
                var gridItems = new List<object>();
                if (skillGrid?._skillsContainer != null)
                    foreach (var icon in skillGrid._skillsContainer.GetComponentsInChildren<SkillIcon>(true))
                        if (!Il2CppBoundary.SameNativeObject(icon, skillGrid._skillViewTemplate))
                            gridItems.Add(new { instanceId = icon.GetInstanceID(), active = icon.gameObject.activeInHierarchy, skill = icon._skill?.Id.ToString() });
                return gridItems;
                }
                return new { available = true, frame = Time.frameCount, accountId = profile.AccountId, nickname = profile.Info?.Nickname,
                    rawCounters = raw, predefinedKillsKey = Key(predefined), queries,
                    pmcCounters = Observe("pmcCounters", () => ReadStatsCounters(profile.PmcStats?.Eft?.OverallCounters)),
                    scavCounters = Observe("scavCounters", () => ReadStatsCounters(profile.ScavStats?.Eft?.OverallCounters)),
                    shortStats = Observe("shortStats", () =>
                    {
                        var text = window._shortStatsPanel;
                        return new { kills = text?._kills?.FormattedText, kd = text?._killsDeathRate?.FormattedText,
                            raids = text?._raids?.FormattedText, survival = text?._survivalRate?.FormattedText, left = text?._leaveRate?.FormattedText, online = text?._onlineTime?.FormattedText };
                    }),
                    shortStatsCorrection = ShortStatsProbe,
                    lifetimeFormatting = TeammateLifetimeFormatting.Snapshot(),
                    livePlayerStash = Observe("livePlayerStash", () => ReadItems(ActiveProfileSession?.Profile?.Inventory?.Stash)),
                    viewedEquipment = Observe("viewedEquipment", () => ReadItems(profile.Equipment)),
                    loadoutEditor = Observe("loadoutEditor", () => new {
                        active = LoadoutEditorOverlayRoot != null && LoadoutEditorOverlayRoot.activeInHierarchy,
                        realCommit = LoadoutEditorProfile != null && IsRealDefaultLoadoutEditorCommit(),
                        sourceId = LoadoutEditorSourceLoadoutId, sourceName = LoadoutEditorSourceLoadoutName,
                        stashIsLive = LoadoutEditorProfile != null && Il2CppBoundary.SameNativeObject(LoadoutEditorProfile.Inventory?.Stash, ActiveProfileSession?.Profile?.Inventory?.Stash),
                        equipmentIsLive = LoadoutEditorProfile != null && Il2CppBoundary.SameNativeObject(LoadoutEditorProfile.Inventory?.Equipment, profile.Equipment),
                        stash = ReadItems(LoadoutEditorProfile?.Inventory?.Stash),
                        equipment = ReadItems(LoadoutEditorProfile?.Inventory?.Equipment)
                    }),
                    previewCacheRepairs = UiWeaponPreviewCache.Snapshot(),
                    localizedTemplates = LocalizedTemplateProbe.ToArray(),
                    skills = Observe("skills", ReadSkills),
                    leaderSkills = Observe("leaderSkills", ReadLeaderSkills),
                    mastery = Observe("mastery", () => ReadMastering(profile.Skills)),
                    leaderMastery = Observe("leaderMastery", () => ReadMastering(ActiveProfileSession?.Profile?.Skills)),
                    skillConstants = Observe("skillConstants", () => new { maxLevel = EFT.Skill.MAX_LEVEL,
                        defaultExp = EFT.Skill.DEFAULT_EXP_LEVEL, additionalExp = EFT.Skill.ADDITIONAL_EXP_PER_LEVEL,
                        expPerLevel = EFT.BaseSkill.EXP_PER_LEVEL, maxLevelWithBuff = EFT.BaseSkill.MAX_LEVEL_W_BUFF }),
                    skillsClone = pitTeam.Patches.OtherPlayerProfileScreenPatch.SkillsCloneProbe,
                    gridItems = Observe("gridItems", ReadGridItems),
                    previewAnimator = Observe("previewAnimator", ReadPreviewAnimator),
                    previewEquipmentMatches = Observe("previewEquipmentMatches", () => Il2CppBoundary.SameNativeObject(profile.Equipment, profile.PlayerVisualRepresentation?.Equipment)) };
            }
            catch (Exception ex) { return new { available = false, error = ex.ToString() }; }
        }

    }
}
