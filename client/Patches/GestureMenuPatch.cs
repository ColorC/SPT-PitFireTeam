using EFT.UI.Gestures;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using EFT.UI;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UI.BattleUI.Gestures;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GestureAction = EFT.UI.Gestures.GestureBaseItem.PointerClick;

namespace pitTeam.Patches
{
    internal static class CustomGestureText
    {
        public static string ViewBackpackTextUpper()
        {
            return pitFireTeam.GetGestureText("ViewBackpack").ToUpperInvariant();
        }

        public static void FitViewBackpackQuickText(CustomTextMeshProUGUI textField, GameObject? quickCommandObject = null)
        {
            if (textField == null)
            {
                return;
            }

            textField.enableWordWrapping = false;
            textField.overflowMode = TextOverflowModes.Overflow;

            float textWidth = Mathf.Max(84f, textField.GetPreferredValues(textField.text, 170f, 40f).x + 5f);
            EnsureMinWidth(textField.rectTransform, textWidth);

            LayoutElement textLayout = textField.GetComponent<LayoutElement>() ?? textField.gameObject.AddComponent<LayoutElement>();
            textLayout.minWidth = Mathf.Max(textLayout.minWidth, textWidth);
            textLayout.preferredWidth = Mathf.Max(textLayout.preferredWidth, textWidth);

            if (pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(quickCommandObject?.transform) is RectTransform quickRect)
            {
                EnsureMinWidth(quickRect, textWidth + 35f);
            }

            if (pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(textField.transform.parent) is RectTransform parentRect)
            {
                EnsureMinWidth(parentRect, textWidth);
            }
        }

        private static void EnsureMinWidth(RectTransform rectTransform, float width)
        {
            if (rectTransform == null || width <= 0f)
            {
                return;
            }

            if (rectTransform.rect.width + 0.5f < width)
            {
                rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            }
        }
    }

    internal static class GestureMenuNativeMembers
    {
        internal static readonly pitTeam.Compat.NativeUiMemberAccessor AudioItems = pitTeam.Compat.NativeUiMemberAccessor.ForProperty(typeof(GesturesMenu), "_audioItems", typeof(Il2CppSystem.Collections.Generic.List<GesturesAudioItem>), false, false);
        internal static readonly pitTeam.Compat.NativeUiMemberAccessor GestureItemTemplate = pitTeam.Compat.NativeUiMemberAccessor.ForProperty(typeof(GesturesMenu), "_gestureItemTemplate", typeof(GesturesMenuItem), false, false);
        internal static readonly pitTeam.Compat.NativeUiMemberAccessor AllItems = pitTeam.Compat.NativeUiMemberAccessor.ForProperty(typeof(GesturesMenu), "_allItems", typeof(Il2CppSystem.Collections.Generic.List<GestureBaseItem>), false, false);
        internal static readonly pitTeam.Compat.NativeUiMemberAccessor AvailablePhrases = pitTeam.Compat.NativeUiMemberAccessor.ForProperty(typeof(GesturesMenu), "_availablePhrases", typeof(Il2CppSystem.Collections.Generic.HashSet<EPhraseTrigger>), false, false);
        internal static readonly pitTeam.Compat.NativeUiMemberAccessor GestureContainer = pitTeam.Compat.NativeUiMemberAccessor.ForProperty(typeof(GesturesMenu), "_gestureContainer", typeof(BaseTransformSolver), false, false);
        internal static readonly pitTeam.Compat.NativeUiMemberAccessor GestureItems = pitTeam.Compat.NativeUiMemberAccessor.ForProperty(typeof(GesturesMenu), "_gestureItems", typeof(Il2CppSystem.Collections.Generic.List<GesturesMenuItem>), false, false);
    }

    // Add new prhases to the menu
    internal class GestureMenuPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GesturesMenu), "InitPhraseGroups");
        }
        [PatchPostfix]
        private static void PatchPostfix(GesturesMenu __instance)
        {
            if (__instance == null) throw new ArgumentNullException(nameof(__instance));
            var list_0 = GestureMenuNativeMembers.AudioItems.GetValue(__instance) as Il2CppSystem.Collections.Generic.List<GesturesAudioItem>;
            var list_1 = GestureMenuNativeMembers.AllItems.GetValue(__instance) as Il2CppSystem.Collections.Generic.List<GestureBaseItem>;
            if (list_0 == null) throw new InvalidOperationException("GesturesMenu._audioItems property returned null.");
            if (list_1 == null) throw new InvalidOperationException("GesturesMenu._allItems property returned null.");

            if (!pitFireTeam.hideUnsupportedCommands.Value)
            {
                for (int i = 0; i < list_0.Count; i++)
                {
                    var item = list_0[i];
                    if (item == null) throw new InvalidOperationException("GesturesMenu._audioItems contains a null native item.");
                    if (item.gameObject.name == "ENEMY")
                    {
                        List<EPhraseTrigger> enemyPhrases = new List<EPhraseTrigger>
                        {
                            EPhraseTrigger.OnRepeatedContact
                        };

                        enemyPhrases.ForEach(phrase =>
                        {
                            GestureBaseItem gestureBaseItem = item.CreateNewPhrase(phrase, false);
                            gestureBaseItem.OnPointerClicked.Subscribe(new Action<GestureAction>(click => __instance.PointerClickedHandler(click, false)));
                            list_1.Add(gestureBaseItem);
                        });
                    }
                    else if (item.gameObject.name == "TEAM STATUS")
                    {
                        List<CustomPhrases> statusPhrases = new List<CustomPhrases> { CustomPhrases.TeamStatus };

                        statusPhrases.ForEach(phrase =>
                        {
                            GestureBaseItem gestureBaseItem = item.CreateNewPhrase((EPhraseTrigger)phrase, false);
                            gestureBaseItem.OnPointerClicked.Subscribe(new Action<GestureAction>(click => __instance.PointerClickedHandler(click, false)));
                            list_1.Add(gestureBaseItem);
                        });
                    }
                }
            }

        }



    }
    internal class CustomGestureCaptionPatch : ModulePatch
    {
        private static int _diagnosticCount;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GesturesAudioSubItem), nameof(GesturesAudioSubItem.Show), new[]
            {
                typeof(Sprite),
                typeof(Sprite),
                typeof(Color),
                typeof(Color),
                typeof(GestureCommands)
            });
        }

        [PatchPostfix]
        private static void PatchPostfix(GesturesAudioSubItem __instance)
        {
            if (__instance == null) return;

            EPhraseTrigger phraseTrigger;
            try
            {
                phraseTrigger = __instance.PhraseTrigger;
            }
            catch (Exception exception)
            {
                if (System.Threading.Interlocked.Increment(ref _diagnosticCount) <= 4)
                {
                    string? errorPointer = null;
                    try { errorPointer = "0x" + __instance.Pointer.ToInt64().ToString("X"); } catch { errorPointer = "unavailable"; }
                    pitFireTeam.Log?.LogWarning($"[UI][GestureCaption] status=phrase-trigger-error phraseTrigger=unavailable proxyType={__instance.GetType().FullName} nativePointer={errorPointer} before=<unavailable> after=<unavailable> error={exception.GetType().FullName}: {exception.Message}");
                }
                return;
            }

            int triggerValue = (int)phraseTrigger;
            string? phraseKey = triggerValue == 10001 ? "TeamStatus" : triggerValue == 10002 ? "ViewBackpack" : null;
            if (phraseKey == null) return;
            bool logDiagnostic = System.Threading.Interlocked.Increment(ref _diagnosticCount) <= 4;

            string? nativePointer = null;
            string? proxyType = null;
            string? before = null;
            string? after = null;
            try
            {
                proxyType = __instance.GetType().FullName;
                nativePointer = "0x" + __instance.Pointer.ToInt64().ToString("X");
                CustomTextMeshProUGUI textField = __instance._textField;
                if (textField == null)
                {
                    if (logDiagnostic) pitFireTeam.Log?.LogWarning($"[UI][GestureCaption] status=missing-text-field phraseTrigger={triggerValue} proxyType={proxyType} nativePointer={nativePointer} before=<unavailable> after=<unavailable>");
                    return;
                }

                before = textField.text;
                string replacement = pitFireTeam.GetGestureText(phraseKey);
                textField.text = replacement;
                after = textField.text;
                if (logDiagnostic) pitFireTeam.Log?.LogInfo($"[UI][GestureCaption] status=assigned phraseTrigger={triggerValue} phrase={phraseKey} proxyType={proxyType} nativePointer={nativePointer} before={before} after={after}");
            }
            catch (Exception exception)
            {
                if (logDiagnostic) pitFireTeam.Log?.LogWarning($"[UI][GestureCaption] status=error phraseTrigger={triggerValue} phrase={phraseKey} proxyType={proxyType} nativePointer={nativePointer} before={before} after={after} error={exception.GetType().FullName}: {exception.Message}");
            }
        }
    }
    // Hide Unsupported Gesture Commands
    internal class CreatePhraseGroupPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GesturesMenu), "CreatePhraseGroup", new[] { typeof(string), typeof(GesturesAudioItem), typeof(Il2CppStructArray<EPhraseTrigger>) });
        }
        [PatchPrefix]
        private static bool PatchPrefix(GesturesMenu __instance, string localizationKey, GesturesAudioItem groupTemplate, ref Il2CppStructArray<EPhraseTrigger> phrases)
        {
            if (pitFireTeam.hideUnsupportedCommands.Value)
            {
                // modify phrases here
                if (localizationKey == "COMMAND")
                {
                    phrases = new Il2CppStructArray<EPhraseTrigger>(new EPhraseTrigger[] {
                        EPhraseTrigger.Suppress,
                        EPhraseTrigger.Regroup,
                        EPhraseTrigger.HoldPosition,
                        EPhraseTrigger.Look,
                        EPhraseTrigger.Gogogo,
                        EPhraseTrigger.GoForward,
                        EPhraseTrigger.FollowMe,
                        EPhraseTrigger.CoverMe,
                        EPhraseTrigger.Stop,
                        //EPhraseTrigger.Silence,
                        EPhraseTrigger.OnYourOwn
                    });
                }
                else if (localizationKey == "HELP")
                {
                    phrases = new Il2CppStructArray<EPhraseTrigger>(new EPhraseTrigger[] {
                        EPhraseTrigger.NeedHelp,
                        EPhraseTrigger.NeedSniper
                    });
                }
                else if (localizationKey == "CONTACT")
                {
                    phrases = new Il2CppStructArray<EPhraseTrigger>(new EPhraseTrigger[] {
                        EPhraseTrigger.RightFlank,
                        EPhraseTrigger.InTheFront,
                        EPhraseTrigger.OnSix,
                        EPhraseTrigger.LeftFlank
                    });
                }
                else if (localizationKey == "ENEMY")
                {
                    phrases = new Il2CppStructArray<EPhraseTrigger>(new EPhraseTrigger[] {
                        EPhraseTrigger.OnRepeatedContact
                    });
                }
                else if (localizationKey == "TEAM STATUS")
                {
                    phrases = new Il2CppStructArray<EPhraseTrigger>(new EPhraseTrigger[] {
                        (EPhraseTrigger)CustomPhrases.TeamStatus
                    });
                }
                else if (localizationKey == "SITUATIONAL")
                {
                    phrases = new Il2CppStructArray<EPhraseTrigger>(new EPhraseTrigger[] {
                        EPhraseTrigger.LootKey,
                        EPhraseTrigger.LootMoney,
                        EPhraseTrigger.LootWeapon,
                        EPhraseTrigger.LootGeneric,
                        EPhraseTrigger.OpenDoor
                    });
                }
                else if (localizationKey == "HEALTH STATUS" || localizationKey == "REACTION")
                {
                    return false;
                }

            }

            return true;
        }
    }

    internal class CreateGesturesPatch : ModulePatch
    {
        private const string OverThereSpriteFileName = "gesture_over_there.png";
        private static Sprite overThereSprite;

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GesturesMenu), nameof(GesturesMenu.InitGestures));
        }

        [PatchPostfix]
        private static void PatchPostfix(GesturesMenu __instance)
        {
            if (__instance == null) throw new ArgumentNullException(nameof(__instance));
            EInteraction gesture = (EInteraction)CustomGestures.OverThere;
            var gestureItemTemplate = GestureMenuNativeMembers.GestureItemTemplate.GetValue(__instance) as GesturesMenuItem;
            var gestureContainer = GestureMenuNativeMembers.GestureContainer.GetValue(__instance) as BaseTransformSolver;
            var gestureItems = GestureMenuNativeMembers.GestureItems.GetValue(__instance) as Il2CppSystem.Collections.Generic.List<GesturesMenuItem>;
            var list_2 = GestureMenuNativeMembers.AllItems.GetValue(__instance) as Il2CppSystem.Collections.Generic.List<GestureBaseItem>;
            if (gestureItemTemplate == null) throw new InvalidOperationException("GesturesMenu._gestureItemTemplate property returned null.");
            if (gestureContainer == null) throw new InvalidOperationException("GesturesMenu._gestureContainer property returned null.");
            if (gestureItems == null) throw new InvalidOperationException("GesturesMenu._gestureItems property returned null.");
            if (list_2 == null) throw new InvalidOperationException("GesturesMenu._allItems property returned null.");

            GesturesMenuItem gesturesMenuItem = global::UnityEngine.Object.Instantiate<GesturesMenuItem>(gestureItemTemplate, gestureContainer.transform);
            gestureContainer.SetChild(gesturesMenuItem.transform);
            gesturesMenuItem.gameObject.name = gesture.ToString();
            gesturesMenuItem.Gesture = gesture;

            Sprite sprite = LoadOverThereSprite();
            if (sprite != null)
            {
                gesturesMenuItem.Icon = sprite;
            }

            gesturesMenuItem.gameObject.SetActive(true);
            gestureItems.Add(gesturesMenuItem);
            list_2.Add(gesturesMenuItem);
            gesturesMenuItem.OnPointerClicked.Subscribe(new Action<GestureBaseItem.PointerClick>(__instance._CreateGesture_b__44_0));
        }

        private static Sprite LoadOverThereSprite()
        {
            if (overThereSprite != null)
            {
                return overThereSprite;
            }

            string pluginDirectory = Path.GetDirectoryName(typeof(pitFireTeam).Assembly.Location) ?? string.Empty;
            string[] candidates =
            {
                Path.Combine(pluginDirectory, OverThereSpriteFileName),
                Path.Combine(pluginDirectory, "resources", OverThereSpriteFileName),
                Path.Combine(Directory.GetParent(pluginDirectory)?.FullName ?? pluginDirectory, "resources", OverThereSpriteFileName),
                Path.Combine(Environment.CurrentDirectory, "BepInEx", "plugins", "pitFireTeam", OverThereSpriteFileName),
                Path.Combine(Environment.CurrentDirectory, "BepInEx", "plugins", "pitFireTeam", "resources", OverThereSpriteFileName)
            };

            string iconPath = candidates.FirstOrDefault(File.Exists);
            if (string.IsNullOrEmpty(iconPath))
            {
                pitFireTeam.Log?.LogWarning($"[UI] Over There gesture icon could not be found: {OverThereSpriteFileName}");
                return null;
            }

            byte[] fileData = File.ReadAllBytes(iconPath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            if (!texture.LoadImage(fileData))
            {
                UnityEngine.Object.Destroy(texture);
                pitFireTeam.Log?.LogWarning($"[UI] Failed to decode Over There gesture icon '{iconPath}'.");
                return null;
            }

            texture.name = "pitFireTeam_OverThereGestureIcon";
            overThereSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 200f);
            overThereSprite.name = "pitFireTeam_OverThereGestureIcon";
            return overThereSprite;
        }
    }

    internal class GestureMenuAvailablePhrasesPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GesturesMenu), "Init");
        }
        [PatchPostfix]
        private static void PatchPostfix(GesturesMenu __instance)
        {
            if (__instance == null) throw new ArgumentNullException(nameof(__instance));
            GestureMenuUnsupportedCommandVisibility.Track(__instance);
            var hashSet_1 = GestureMenuNativeMembers.AvailablePhrases.GetValue(__instance) as Il2CppSystem.Collections.Generic.HashSet<EPhraseTrigger>;
            if (hashSet_1 == null) throw new InvalidOperationException("GesturesMenu._availablePhrases property returned null.");
            hashSet_1.Add((EPhraseTrigger)CustomPhrases.TeamStatus);
            hashSet_1.Add((EPhraseTrigger)CustomPhrases.ViewBackpack);
        }
    }

    internal static class GestureMenuUnsupportedCommandVisibility
    {
        private static readonly List<WeakReference<GesturesMenu>> TrackedMenus = new List<WeakReference<GesturesMenu>>();

        public static void Track(GesturesMenu menu)
        {
            if (menu == null)
            {
                return;
            }

            for (int index = TrackedMenus.Count - 1; index >= 0; index--)
            {
                if (!TrackedMenus[index].TryGetTarget(out GesturesMenu tracked) || tracked == null)
                {
                    TrackedMenus.RemoveAt(index);
                    continue;
                }

                if (ReferenceEquals(tracked, menu))
                {
                    return;
                }
            }

            TrackedMenus.Add(new WeakReference<GesturesMenu>(menu));
        }

        public static void RefreshTrackedMenus()
        {
            for (int index = TrackedMenus.Count - 1; index >= 0; index--)
            {
                if (!TrackedMenus[index].TryGetTarget(out GesturesMenu menu) || menu == null)
                {
                    TrackedMenus.RemoveAt(index);
                    continue;
                }

                RefreshMenu(menu);
            }
        }

        public static void ClearTrackedMenus()
        {
            TrackedMenus.Clear();
        }

        private static void RefreshMenu(GesturesMenu menu)
        {
            try
            {
                bool wasShowing = menu.IsShowing;
                if (wasShowing)
                {
                    menu.Close();
                }

                ClearPhraseGroups(menu);
                menu.InitPhraseGroups();

                if (wasShowing)
                {
                    menu.Show();
                }
            }
            catch (Exception ex)
            {
                pitFireTeam.Log?.LogWarning($"[UI] Failed to refresh gesture command visibility after Hide Unsupported Commands changed: {ex}");
            }
        }

        private static void ClearPhraseGroups(GesturesMenu menu)
        {
            if (menu == null) throw new ArgumentNullException(nameof(menu));
            var audioGroups = GestureMenuNativeMembers.AudioItems.GetValue(menu) as Il2CppSystem.Collections.Generic.List<GesturesAudioItem>;
            var allItems = GestureMenuNativeMembers.AllItems.GetValue(menu) as Il2CppSystem.Collections.Generic.List<GestureBaseItem>;
            if (audioGroups == null) throw new InvalidOperationException("GesturesMenu._audioItems property returned null while clearing phrase groups.");
            if (allItems == null) throw new InvalidOperationException("GesturesMenu._allItems property returned null while clearing phrase groups.");

            for (int index = audioGroups.Count - 1; index >= 0; index--)
            {
                GesturesAudioItem group = audioGroups[index];
                if (group != null)
                {
                    group.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(group.gameObject);
                }
            }
            audioGroups.Clear();

            for (int index = allItems.Count - 1; index >= 0; index--)
            {
                GestureBaseItem item = allItems[index];
                if (item == null || item is GesturesAudioSubItem) allItems.RemoveAt(index);
            }
        }
    }

    internal class ViewBackpackQuickPanelTextPatch : ModulePatch
    {
        private static readonly pitTeam.Compat.NativeUiMemberAccessor? TextAccessor = pitTeam.Compat.NativeUiMemberAccessor.TryForProperty(typeof(GesturesQuickPanel), "_textField", typeof(CustomTextMeshProUGUI), false, false);
        private static readonly pitTeam.Compat.NativeUiMemberAccessor? QuickCommandObjectAccessor = pitTeam.Compat.NativeUiMemberAccessor.TryForProperty(typeof(GesturesQuickPanel), "_quickCommandObject", typeof(GameObject), false, false);

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GesturesQuickPanel), nameof(GesturesQuickPanel.UpdateQuickPanelLabel));
        }

        [PatchPostfix]
        private static void PatchPostfix(GesturesQuickPanel __instance)
        {
            if (__instance.PrioritizedCommand != (EPhraseTrigger)CustomPhrases.ViewBackpack)
            {
                return;
            }

            if (TextAccessor?.GetValue(__instance) is CustomTextMeshProUGUI textField)
            {
                textField.text = CustomGestureText.ViewBackpackTextUpper();
                CustomGestureText.FitViewBackpackQuickText(textField, QuickCommandObjectAccessor?.GetValue(__instance) as GameObject);
            }
        }
    }

    internal class ViewBackpackQuickPanelItemTextPatch : ModulePatch
    {
        private static readonly FieldInfo LabelField = AccessTools.Field(typeof(GesturesQuickPanelItem), "_label");

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GesturesQuickPanelItem), nameof(GesturesQuickPanelItem.Show));
        }

        [PatchPostfix]
        private static void PatchPostfix(GesturesQuickPanelItem __instance, EPhraseTrigger trigger)
        {
            if (trigger != (EPhraseTrigger)CustomPhrases.ViewBackpack)
            {
                return;
            }

            if (LabelField.GetValue(__instance) is CustomTextMeshProUGUI label)
            {
                label.text = CustomGestureText.ViewBackpackTextUpper();
                CustomGestureText.FitViewBackpackQuickText(label, __instance.gameObject);
            }
        }
    }

    internal class CustomPlayerGestureIntPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EFT.UI.Gestures.GestureCommands), nameof(EFT.UI.Gestures.GestureCommands.IsPlayerGesture), new[] { typeof(int) });
        }

        [PatchPrefix]
        private static bool PatchPrefix(int index, ref bool __result)
        {
            if (index != (int)(EInteraction)CustomGestures.OverThere)
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    internal class CustomPlayerGestureInteractionPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EFT.UI.Gestures.GestureCommands), nameof(EFT.UI.Gestures.GestureCommands.IsPlayerGesture), new[] { typeof(EInteraction) });
        }

        [PatchPrefix]
        private static bool PatchPrefix(EInteraction gesture, ref bool __result)
        {
            if (gesture != (EInteraction)CustomGestures.OverThere)
            {
                return true;
            }

            __result = true;
            return false;
        }
    }

    internal class GestureCommandNamePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(EFT.UI.Gestures.GestureCommands), nameof(EFT.UI.Gestures.GestureCommands.GetCommandName), new[] { typeof(int) });
        }

        [PatchPrefix]
        private static bool PatchPrefix(int index, ref string __result)
        {
            if (index == (int)EPhraseTrigger.OnRepeatedContact)
            {
                __result = GetGestureText("OnRepeatedContact");
                return false;
            }

            if (index == (int)(EPhraseTrigger)CustomPhrases.TeamStatus)
            {
                __result = GetGestureText("TeamStatus");
                return false;
            }

            if (index == (int)(EPhraseTrigger)CustomPhrases.ViewBackpack)
            {
                __result = GetGestureText("ViewBackpack");
                return false;
            }

            if (index == (int)(EInteraction)CustomGestures.OverThere)
            {
                __result = GetGestureText("OverThere");
                return false;
            }

            return true;
        }

        private static string GetGestureText(string key)
        {
            return pitFireTeam.GetGestureText(key);
        }
    }


    internal class EPhraseTriggerPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(Enum), "ToString", new Type[] { });
        }
        [PatchPrefix]
        private static bool PatchPrefix(Enum __instance, ref string __result)
        {
            if (__instance is EPhraseTrigger trigger)
            {
                if (trigger == EPhraseTrigger.OnRepeatedContact)
                {
                    __result = pitFireTeam.GetGestureText("OnRepeatedContact");
                    return false;
                }
                else if (trigger == (EPhraseTrigger)CustomPhrases.TeamStatus)
                {
                    __result = pitFireTeam.GetGestureText("TeamStatus");
                    return false;
                }
                else if (trigger == (EPhraseTrigger)CustomPhrases.ViewBackpack)
                {
                    __result = pitFireTeam.GetGestureText("ViewBackpack");
                    return false;
                }
            }
            else if (__instance is EInteraction trigger2)
            {
                if (trigger2 == (EInteraction)CustomGestures.OverThere)
                {
                    __result = pitFireTeam.GetGestureText("OverThere");
                    return false;
                }
            }

            return true;
        }
    }
}
