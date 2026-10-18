using Comfort.Common;
using Diz.Binding;
using EFT;
using EFT.InventoryLogic;
using EFT.UI.SessionEnd;
using HarmonyLib;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using pitTeam.Modules;
using SPTushonka.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace pitTeam.Patches
{
    internal sealed class SessionResultKillListInjectedContent : MonoBehaviour
    {
        private static bool _registered;

        public SessionResultKillListInjectedContent(IntPtr pointer) : base(pointer) { }

        [HideFromIl2Cpp]
        internal static void RegisterIfNeeded()
        {
            if (_registered) return;
            Il2CppBoundary.EnsureInjectedComponentRegistered<SessionResultKillListInjectedContent>();
            _registered = true;
        }

                private readonly List<GameObject> _objects = new List<GameObject>();

        [HideFromIl2Cpp]
        internal void Track(IEnumerable<GameObject> objects)
        {
            Clear();
            _objects.AddRange(objects);
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnDestroy()
        {
            Clear();
        }

        [HideFromIl2Cpp]
        private void Clear()
        {
            foreach (GameObject gameObject in _objects)
            {
                if (gameObject == null)
                {
                    continue;
                }

                gameObject.SetActive(false);
                UnityEngine.Object.Destroy(gameObject);
            }

            _objects.Clear();
        }
    }

    internal sealed class SessionResultKillListShowPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(SessionResultKillList), nameof(SessionResultKillList.Show), new[] { typeof(UpdatableBindableList<VictimStats>), typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<DogtagComponent>), typeof(EFT.IEftSession) });
        }

        [PatchPostfix]
        private static void PatchPostfix(SessionResultKillList __instance, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<DogtagComponent> tags, EFT.IEftSession session)
        {
            List<GameObject> injectedObjects = new List<GameObject>();

            try
            {
                IReadOnlyList<SquadRaidKillGroup> groups = SquadRaidKillReport.CreateResultSnapshot();
                if (groups.Count == 0 ||
                    __instance?._container == null ||
                    __instance._victimTemplate == null)
                {
                    return;
                }

                HashSet<string> knownVictimProfileIds = BuildKnownVictimProfileIds(tags);

                GameObject playerHeader = CreateSectionHeader(
                    __instance._container,
                    SquadRaidKillReport.GetPlayerNickname(),
                    __instance._victimTemplate._name);
                playerHeader.transform.SetSiblingIndex(0);
                injectedObjects.Add(playerHeader);

                foreach (SquadRaidKillGroup group in groups)
                {
                    GameObject teammateHeader = CreateSectionHeader(
                        __instance._container,
                        group.TeammateNickname,
                        __instance._victimTemplate._name);
                    injectedObjects.Add(teammateHeader);

                    int killNumber = 1;
                    foreach (VictimStats victim in group.Victims)
                    {
                        KillListVictim row = UnityEngine.Object.Instantiate(
                            __instance._victimTemplate,
                            __instance._container);
                        injectedObjects.Add(row.gameObject);
                        row.Show(victim, knownVictimProfileIds.Contains(victim.ProfileId), killNumber++, session);
                    }
                }

                __instance._noKillsObject?.SetActive(false);
                SessionResultKillListInjectedContent.RegisterIfNeeded();
                SessionResultKillListInjectedContent cleanup =
                    __instance.GetComponentViaIl2CppType<SessionResultKillListInjectedContent>() ??
                    __instance.gameObject.AddComponentViaIl2CppType<SessionResultKillListInjectedContent>();
                cleanup.Track(injectedObjects);
            }
            catch (Exception ex)
            {
                DestroyInjectedObjects(injectedObjects);
                Logger.LogError("[RaidKillReport] Failed to add grouped teammate kills to the session result list.");
                Logger.LogError(ex);
            }
        }

        private static HashSet<string> BuildKnownVictimProfileIds(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<DogtagComponent> tags)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.Ordinal);
            if (tags == null)
            {
                return result;
            }

            foreach (DogtagComponent tag in tags)
            {
                if (tag != null && !string.IsNullOrWhiteSpace(tag.ProfileId))
                {
                    result.Add(tag.ProfileId);
                }
            }

            return result;
        }

        private static GameObject CreateSectionHeader(
            Transform parent,
            string title,
            TMP_Text styleSource)
        {
            GameObject headerObject = new GameObject(
                "pitFireTeam_RaidKillListHeader",
                Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(),
                Il2CppInterop.Runtime.Il2CppType.Of<LayoutElement>());
            headerObject.transform.SetParent(parent, false);

            LayoutElement layout = headerObject.GetComponent<LayoutElement>();
            layout.preferredHeight = 46f;
            layout.flexibleWidth = 1f;

            GameObject labelObject = new GameObject("Label", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(), Il2CppInterop.Runtime.Il2CppType.Of<TextMeshProUGUI>());
            labelObject.transform.SetParent(headerObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 2f);
            labelRect.offsetMax = new Vector2(-6f, -7f);

            TextMeshProUGUI label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = title;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.fontSize = 20f;
            label.fontWeight = FontWeight.Bold;
            label.color = new Color(0.92f, 0.82f, 0.63f, 1f);
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            if (styleSource != null)
            {
                label.font = styleSource.font;
                label.fontSharedMaterial = styleSource.fontSharedMaterial;
            }

            GameObject dividerObject = new GameObject("Divider", Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(), Il2CppInterop.Runtime.Il2CppType.Of<Image>());
            dividerObject.transform.SetParent(headerObject.transform, false);
            RectTransform dividerRect = dividerObject.GetComponent<RectTransform>();
            dividerRect.anchorMin = new Vector2(0f, 0f);
            dividerRect.anchorMax = new Vector2(1f, 0f);
            dividerRect.pivot = new Vector2(0.5f, 0f);
            dividerRect.offsetMin = new Vector2(6f, 0f);
            dividerRect.offsetMax = new Vector2(-6f, 1f);

            Image divider = dividerObject.GetComponent<Image>();
            divider.color = new Color(0.54f, 0.45f, 0.29f, 0.45f);
            divider.raycastTarget = false;

            return headerObject;
        }

        private static void DestroyInjectedObjects(List<GameObject> objects)
        {
            foreach (GameObject gameObject in objects)
            {
                if (gameObject == null)
                {
                    continue;
                }

                gameObject.SetActive(false);
                UnityEngine.Object.Destroy(gameObject);
            }

            objects.Clear();
        }
    }
}
