using Arena.UI;
using Comfort.Common;
using EFT;
using EFT.Builds;
using EFT.Communications;
using EFT.HealthSystem;
using EFT.InputSystem;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.UI.DragAndDrop;
using EFT.UI.Screens;
using EFT.UI.Settings;
using pitTeam.Compat;
using pitTeam.Modules;
using pitTeam.Utils;
using HarmonyLib;
using Newtonsoft.Json;
using SPTushonka.Common.Http;
using SPTushonka.Common.Utils;
using SPTushonka.Reflection.Patching;
using System;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Injection;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using dropDownItem = EFT.Customization.CustomizationSuite;
using OtherProfileResult = EFT.OtherPlayerProfileDescriptor;
using ResultProfile = EFT.OtherPlayerProfile;

namespace pitTeam.Patches
{
    internal class FriendlyProfileDropdownItem : dropDownItem
    {
    }

    internal class FriendlyTeammateBodyResponse<T>
    {
        public int err { get; set; }
        public string errmsg { get; set; }
        public T data { get; set; }
    }

    internal class FriendlyTeammateLoadoutOption
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    internal class FriendlyTeammateTacticOption
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }

    internal class FriendlyTeammateProfileOptions
    {
        public string CurrentLoadoutId { get; set; }
        public string CurrentTactic { get; set; }
        public float Aggression { get; set; } = 50f;
        public FollowerProficiencyModifierValues Proficiency { get; set; } = new FollowerProficiencyModifierValues();
        public List<FriendlyTeammateLoadoutOption> Loadouts { get; set; }
        public List<FriendlyTeammateTacticOption> Tactics { get; set; }
        public List<string> OwnedBodyCustomizationIds { get; set; }
        public List<string> OwnedFeetCustomizationIds { get; set; }
        public FriendlyTeammateProfileRecoveryNotice RecoveryNotice { get; set; }
        public FollowerHealthSnapshot FollowerHealth { get; set; }
    }

    internal sealed class FollowerHealthSnapshot
    {
        public string ProfileId { get; set; }
        public int? Aid { get; set; }
        public Dictionary<string, FollowerHealthValue> BodyParts { get; set; }
        public FollowerHealthValue Energy { get; set; }
        public FollowerHealthValue Hydration { get; set; }
        public FollowerHealthValue Temperature { get; set; }
        public FollowerHealthValue Poison { get; set; }
    }

    internal sealed class FollowerHealthValue
    {
        public double? Current { get; set; }
        public double? Minimum { get; set; }
        public double? Maximum { get; set; }
    }
    internal class FriendlyTeammateProfileRecoveryNotice
    {
        public bool Recovered { get; set; }
        public int RemovedItemCount { get; set; }
        public string Message { get; set; }
    }

    internal class FriendlyTeammateSuitRequest
    {
        public string aid { get; set; }
        public string[] suit { get; set; }
    }

    internal class FriendlyTeammateLoadoutRequest
    {
        public string aid { get; set; }
        public string loadoutId { get; set; }
    }

    internal class FriendlyTeammateRenameRequest
    {
        public string aid { get; set; }
        public string nickname { get; set; }
    }

    internal class FriendlyTeammateAggressionRequest
    {
        public string aid { get; set; }
        public float aggression { get; set; }
    }

    internal class FriendlyTeammateProficiencyRequest
    {
        public string aid { get; set; }
        public FollowerProficiencyModifierValues proficiency { get; set; }
    }

    internal class FriendlyTeammateTacticRequest
    {
        public string aid { get; set; }
        public string tactic { get; set; }
    }

    internal sealed class ProfileOverlayHeaderDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public ProfileOverlayHeaderDragHandle(IntPtr pointer) : base(pointer) { }
        public RectTransform Target;

        private bool _dragging;

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = Target != null;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || Target == null)
            {
                return;
            }

            Vector2 delta = eventData.delta;
            Target.offsetMin += delta;
            Target.offsetMax += delta;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
        }
    }

    internal sealed class ProfileTooltipHoverController : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public ProfileTooltipHoverController(IntPtr pointer) : base(pointer) { }
        private string tooltipText;
        private Vector2 tooltipOffset = new Vector2(5f, 7f);

        [HideFromIl2Cpp]
        public void Configure(string text, Vector2? offset = null)
        {
            tooltipText = text ?? string.Empty;
            tooltipOffset = offset ?? new Vector2(5f, 7f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (string.IsNullOrWhiteSpace(tooltipText))
            {
                return;
            }

            ItemUiContext instance = ItemUiContext.Instance;
            instance?.Tooltip?.Show(tooltipText, new Il2CppSystem.Nullable<Vector2>(tooltipOffset), 0f, ((float?)null).ToNativeNullable());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ItemUiContext instance = ItemUiContext.Instance;
            instance?.Tooltip?.Close();
        }

        private void OnDisable()
        {
            ItemUiContext instance = ItemUiContext.Instance;
            instance?.Tooltip?.Close();
        }
    }

    internal class FriendlyDropdownNamePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.PropertyGetter(typeof(EFT.Customization.BaseCustomizationItem), "NameLocalizationKey");
        }

        [PatchPrefix]
        private static bool PatchPrefix(EFT.Customization.BaseCustomizationItem __instance, ref string __result)
        {
            if (OtherPlayerProfileScreenPatch.CustomDropdownIds.Contains(__instance.Id))
            {
                __result = __instance.Name;
                return false;
            }

            return true;
        }
    }

    internal partial class OtherPlayerProfileScreenPatch : ModulePatch
    {
        private const string OptionsRoute = "/singleplayer/pitfireteam/teammate/profile/options";
        private const string SuitRoute = "/singleplayer/pitfireteam/teammate/profile/suit";
        private const string RenameRoute = "/singleplayer/pitfireteam/teammate/profile/rename";
        private const string LoadoutRoute = "/singleplayer/pitfireteam/teammate/profile/loadout";
        private const string DefaultEquipmentRoute = "/singleplayer/pitfireteam/teammate/profile/default-equipment";
        private const string RepairEquipmentRoute = "/singleplayer/pitfireteam/teammate/profile/repair-equipment";
        private const string AggressionRoute = "/singleplayer/pitfireteam/teammate/profile/aggression";
        private const string TacticRoute = "/singleplayer/pitfireteam/teammate/profile/tactic";
        private const string DefaultLoadoutId = "000000000000000000000000";
        private const string DefaultLoadoutName = "Default";

        internal static readonly NativeUiMemberAccessor PlayerModelWindowAccessor = NativeUiMemberAccessor.ForProperty(typeof(OtherPlayerProfileScreen), "_playerModelWithStatsWindow", typeof(InventoryPlayerModelWithStatsWindow), false, false);
        private static readonly NativeUiMemberAccessor ClothingPanelAccessor = NativeUiMemberAccessor.ForProperty(typeof(InventoryPlayerModelWithStatsWindow), "_clothingPanel", typeof(InventoryClothingSelectionPanel), false, false);
        private static readonly NativeUiMemberAccessor NicknameLabelAccessor = NativeUiMemberAccessor.ForProperty(typeof(InventoryPlayerModelWithStatsWindow), "_nicknameLabel", typeof(CustomTextMeshProUGUI), false, false);
        private static readonly NativeUiMemberAccessor NicknameFieldInputAccessor = NativeUiMemberAccessor.ForProperty(typeof(NicknameField), "_inputField", typeof(TMP_InputField), false, false);
        private static readonly NativeUiMemberAccessor NicknameFieldStatusLabelAccessor = NativeUiMemberAccessor.ForProperty(typeof(NicknameField), "_statusLabel", typeof(TMP_Text), false, false);
        private static readonly NativeUiMemberAccessor NicknameFieldUsedSymbolsAccessor = NativeUiMemberAccessor.ForProperty(typeof(NicknameField), "_usedSymbolsCount", typeof(LocalizedText), false, false);
        private static readonly NativeUiMemberAccessor BackButtonField = NativeUiMemberAccessor.ForProperty(typeof(OtherPlayerProfileScreen), "_backButton", typeof(DefaultUIButton), false, false);
        private static readonly NativeUiMemberAccessor HideoutButtonAccessor = NativeUiMemberAccessor.ForProperty(typeof(OtherPlayerProfileScreen), "_hideoutButton", typeof(DefaultUiButtonNewStyle), false, false);
        private static readonly NativeUiMemberAccessor SettingsScreenGameTabField = NativeUiMemberAccessor.ForProperty(typeof(SettingsScreen), "_gameSettingsScreen", typeof(GameSettingsTab), false, false);
        private static readonly NativeUiMemberAccessor GameSettingsSliderTemplateAccessor = NativeUiMemberAccessor.ForProperty(typeof(GameSettingsTab), "_floatSliderTemplate", typeof(SettingFloatSlider), false, false);
        private static readonly NativeUiMemberAccessor NumberSliderValueInputField = NativeUiMemberAccessor.ForProperty(typeof(NumberSlider), "_valueInput", typeof(TMP_InputField), false, false);
        private static RectTransform CachedSettingsSliderContainerTemplate { get; set; }
        private static readonly NativeUiMemberAccessor ReportPanelAccessor = NativeUiMemberAccessor.ForProperty(typeof(OtherPlayerProfileScreen), "_reportPanel", typeof(ReportPanel), false, false);
        private static readonly NativeUiMemberAccessor OverallStatsPanelAccessor = NativeUiMemberAccessor.ForProperty(typeof(OtherPlayerProfileScreen), "_overallStatsPanel", typeof(OverallStatsPanel), false, false);
        private static readonly NativeUiMemberAccessor AchievementsProgressBlockAccessor = NativeUiMemberAccessor.ForProperty(typeof(OtherPlayerProfileScreen), "_achievementsProgressBlock", typeof(GameObject), false, false);
        private static readonly NativeUiMemberAccessor AchievementsBlockPlaceholderAccessor = NativeUiMemberAccessor.ForProperty(typeof(OtherPlayerProfileScreen), "_achievementsBlockPlaceholder", typeof(GameObject), false, false);
        private static readonly NativeUiMemberAccessor WeaponsBlockPlaceholderAccessor = NativeUiMemberAccessor.ForProperty(typeof(OtherPlayerProfileScreen), "_weaponsBlockPlaceholder", typeof(GameObject), false, false);
        private static readonly NativeUiMemberAccessor NonWeaponItemsBlockPlaceholderAccessor = NativeUiMemberAccessor.ForProperty(typeof(OtherPlayerProfileScreen), "_nonWeaponItemsBlockPlaceholder", typeof(GameObject), false, false);
        private static readonly NativeUiMemberAccessor WeaponsGridLayoutGroupAccessor = NativeUiMemberAccessor.ForProperty(typeof(OtherPlayerProfileScreen), "_weaponsGridLayoutGroup", typeof(GridLayoutGroup), false, false);
        private static readonly NativeUiMemberAccessor NonWeaponItemsGridLayoutGroupAccessor = NativeUiMemberAccessor.ForProperty(typeof(OtherPlayerProfileScreen), "_nonWeaponItemsGridLayoutGroup", typeof(GridLayoutGroup), false, false);
        private static readonly NativeUiMemberAccessor SkillsScreenListTabAccessor = NativeUiMemberAccessor.ForProperty(typeof(SkillsScreen), "_listTab", typeof(Tab), false, false);
        private static readonly NativeUiMemberAccessor SkillsScreenThumbsTabAccessor = NativeUiMemberAccessor.ForProperty(typeof(SkillsScreen), "_thumbsTab", typeof(Tab), false, false);
        private static readonly NativeUiMemberAccessor SkillsScreenTabsControllerAccessor = NativeUiMemberAccessor.ForProperty(typeof(SkillsScreen), "_skillViewTabGroup", typeof(TabGroup), false, false);
        private static readonly MethodInfo SkillsScreenShowMethod = AccessTools.Method(typeof(SkillsScreen), "Show");
        private static readonly NativeUiMemberAccessor SkillsAndMasteringSkillsScreenAccessor = NativeUiMemberAccessor.ForProperty(typeof(SkillsAndMasteringScreen), "_skillsScreen", typeof(SkillsScreen), false, false);
        private static readonly NativeUiMemberAccessor InventorySkillsAndMasteringScreenAccessor = NativeUiMemberAccessor.ForProperty(typeof(InventoryScreen), "_skillsAndMasteringScreen", typeof(SkillsAndMasteringScreen), false, false);
        private static readonly NativeUiMemberAccessor SkillManagerSkillsAccessor = NativeUiMemberAccessor.ForProperty(typeof(SkillManager), nameof(SkillManager.Skills), typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<EFT.Skill>), false, true);
        private static readonly NativeUiMemberAccessor SkillManagerDisplayListAccessor = NativeUiMemberAccessor.ForProperty(typeof(SkillManager), nameof(SkillManager.DisplayList), typeof(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<EFT.Skill>), false, true);
        private static readonly NativeUiMemberAccessor UiAccessor = NativeUiMemberAccessor.ForProperty(typeof(OtherPlayerProfileScreen), "UI", typeof(EFT.UI.UIParent), false, false);
        private static readonly NativeUiMemberAccessor UpperSelectorAccessor = NativeUiMemberAccessor.ForProperty(typeof(InventoryClothingSelectionPanel), "_upperSelector", typeof(InventoryCustomizationSelector), false, false);
        private static readonly NativeUiMemberAccessor LowerSelectorAccessor = NativeUiMemberAccessor.ForProperty(typeof(InventoryClothingSelectionPanel), "_lowerSelector", typeof(InventoryCustomizationSelector), false, false);
        private static readonly NativeUiMemberAccessor TransferItemsScreenStashPanelAccessor = NativeUiMemberAccessor.ForProperty(typeof(TransferItemsScreen), "_stashPanel", typeof(SimpleStashPanel), false, false);
        private static readonly NativeUiMemberAccessor InventoryScreenItemsPanelAccessor = NativeUiMemberAccessor.ForProperty(typeof(InventoryScreen), "_itemsPanel", typeof(ItemsPanel), false, false);
        private static readonly NativeUiMemberAccessor ItemsPanelComplexStashPanelAccessor = NativeUiMemberAccessor.ForProperty(typeof(ItemsPanel), "_complexStashPanel", typeof(ComplexStashPanel), false, false);
        private static readonly NativeUiMemberAccessor ComplexStashPanelContainersPanelAccessor = NativeUiMemberAccessor.ForProperty(typeof(ComplexStashPanel), "_containersPanel", typeof(ContainersPanel), false, false);
        private static readonly NativeUiMemberAccessor ContainersPanelDictionaryAccessor = NativeUiMemberAccessor.ForProperty(typeof(ContainersPanel), "_slotViews", typeof(Il2CppSystem.Collections.Generic.Dictionary<EquipmentSlot, SlotView>), false, false);
        private static readonly string PluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
        private static readonly string GearIconPath = Path.Combine(PluginDirectory, "gear.png");
        private static readonly string BrainIconPath = Path.Combine(PluginDirectory, "brain.png");
        private static readonly Vector2 FactionBadgeFollowerOffset = new Vector2(0f, -60f);
        private static readonly EquipmentSlot[] LoadoutEditorContainerSlots =
        {
            EquipmentSlot.TacticalVest,
            EquipmentSlot.Pockets,
            EquipmentSlot.Backpack
        };
        private static readonly EquipmentSlot[] LoadoutEditorEquipmentSlots =
        {
            EquipmentSlot.Scabbard,
            EquipmentSlot.Holster,
            EquipmentSlot.FirstPrimaryWeapon,
            EquipmentSlot.SecondPrimaryWeapon,
            EquipmentSlot.Eyewear,
            EquipmentSlot.FaceCover,
            EquipmentSlot.Headwear,
            EquipmentSlot.Earpiece,
            EquipmentSlot.ArmorVest,
            EquipmentSlot.ArmBand,
            EquipmentSlot.Dogtag
        };
        private static readonly HashSet<ESkillId> HiddenFollowerSkills = new HashSet<ESkillId>
        {
            ESkillId.Charisma,
            ESkillId.Attention,
            ESkillId.Intellect,
            ESkillId.Search,
            ESkillId.WeaponTreatment,
            ESkillId.Crafting,
            ESkillId.HideoutManagement
        };

        public static ResultProfile ViewedProfile { get; set; }
        public static OtherPlayerProfileScreen ActiveProfileScreen { get; set; }
        public static InventoryController ActiveProfileInventoryController { get; set; }
        public static EFT.InventoryLogic.OfflineInventoryController ActiveProfileBackendInventoryController { get; set; }
        public static EFT.IEftSession ActiveProfileSession { get; set; }
        public static InventoryPlayerModelWithStatsWindow ActiveProfilePlayerModelWindow { get; set; }
        public static Transform LoadoutSelector { get; set; }
        public static RectTransform ProfileActionsContainer { get; set; }
        public static GameObject ProfileOptionsOverlayRoot { get; set; }
        public static DefaultUIButton LoadoutTacticButton { get; set; }
        public static DefaultUIButton EditLoadoutButton { get; set; }
        public static Transform EditLoadoutButtonRoot { get; set; }
        public static GameObject LoadoutEditorOverlayRoot { get; set; }
        public static GameObject ProfileRecoveryOverlayRoot { get; set; }
        public static SimpleStashPanel LoadoutEditorStashPanel { get; set; }
        public static ComplexStashPanel LoadoutEditorEquipmentPanel { get; set; }
        public static Profile LoadoutEditorProfile { get; set; }
        public static InventoryController LoadoutEditorInventoryController { get; set; }
        public static EFT.InventoryLogic.ItemContext LoadoutEditorEquipmentContext { get; set; }
        public static JsonType.FlatItem[] LoadoutEditorInitialEquipmentItems { get; set; }
        public static JsonType.FlatItem[] LoadoutEditorInitialStashItems { get; set; }
        private static Dictionary<string, EItemPinLockState> LoadoutEditorOriginalPinLockStates { get; } =
            new Dictionary<string, EItemPinLockState>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, Item> LoadoutEditorEquipmentItemsById { get; } =
            new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, Item> LoadoutEditorStashItemsById { get; } =
            new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        public static string ActiveTeammateLoadoutId { get; set; }
        public static string ActiveTeammateLoadoutName { get; set; }
        public static string LoadoutEditorSourceLoadoutId { get; set; }
        public static string LoadoutEditorSourceLoadoutName { get; set; }
        public static SkillsScreen SkillsPanel { get; set; }
        public static RectTransform SkillsPanelHost { get; set; }
        public static CustomTextMeshProUGUI OriginalNicknameLabel { get; set; }
        public static DefaultUIButton NicknameRenameButton { get; set; }
        public static Transform NicknameRenameButtonRoot { get; set; }
        public static GameObject RenameOverlayRoot { get; set; }
        public static NicknameField RenameOverlayField { get; set; }
        public static Vector2? OriginalNicknameAnchoredPosition { get; set; }
        public static RectTransform FactionBadgeIconsContainer { get; set; }
        public static Vector2? OriginalFactionBadgeAnchoredPosition { get; set; }
        public static GameObject OriginalHideoutButtonPanel { get; set; }
        public static bool? OriginalHideoutButtonPanelActive { get; set; }
        public static List<MongoID> CustomDropdownIds { get; } = new List<MongoID>();
        private static List<GameObject> HiddenRenameButtonDecorations { get; } = new List<GameObject>();
        private sealed class HiddenRightSideRootState { public GameObject root; public bool active; }
        private static Dictionary<long, HiddenRightSideRootState> HiddenRightSideRoots { get; } = new Dictionary<long, HiddenRightSideRootState>();
        private static Dictionary<long, Vector2> ProfileScaleOriginalPositions { get; } = new Dictionary<long, Vector2>();
        private static Coroutine PendingAggressionPersistCoroutine { get; set; }
        private static int PendingAggressionPersistRevision { get; set; }
        internal static Action PendingBackOverrideAction { get; set; }
        internal static Action ActiveBackOverrideAction { get; set; }
        private static string PendingRecruitProfileAccountId { get; set; }
        private static bool TaskBarDisabledForReturnOverride { get; set; }

        internal static bool IsLoadoutEditorEquipmentContext(EFT.InventoryLogic.ItemContext context)
        {
            if (context == null || LoadoutEditorEquipmentContext == null)
            {
                return false;
            }

            EFT.InventoryLogic.ItemContext current = context;
            var visitedNativePointers = new System.Collections.Generic.HashSet<long>();
            for (int depth = 0; current != null && depth < 64; depth++)
            {
                long pointer = current.Pointer.ToInt64();
                if (pointer == 0 || !visitedNativePointers.Add(pointer))
                {
                    return false;
                }

                if (pitTeam.Compat.Il2CppBoundary.SameNativeObject(current, LoadoutEditorEquipmentContext))
                {
                    return true;
                }

                EFT.InventoryLogic.ItemContext source = current.Source;
                if (source != null && pitTeam.Compat.Il2CppBoundary.SameNativeObject(current, source))
                {
                    return false;
                }

                current = source;
            }

            return false;
        }

        internal static bool IsLoadoutEditorEquipmentItem(Item item)
        {
            if (item == null || LoadoutEditorProfile?.Inventory?.Equipment == null || LoadoutEditorOverlayRoot == null)
            {
                return false;
            }

            if (IsItemInLoadoutEditorRoot(item, LoadoutEditorProfile.Inventory.Equipment))
            {
                return true;
            }

            RebuildLoadoutEditorItemIndexes();
            return TryGetIndexedLoadoutEditorItem(
                item.Id,
                LoadoutEditorEquipmentItemsById,
                LoadoutEditorProfile.Inventory.Equipment,
                out Item indexedItem)
                && pitTeam.Compat.Il2CppBoundary.SameNativeObject(indexedItem, item);
        }

        internal static bool TryGetLoadoutEditorEquipmentItem(string itemId, out Item item)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(itemId) || LoadoutEditorProfile?.Inventory?.Equipment == null || LoadoutEditorOverlayRoot == null)
            {
                return false;
            }

            if (TryGetIndexedLoadoutEditorItem(
                itemId,
                LoadoutEditorEquipmentItemsById,
                LoadoutEditorProfile.Inventory.Equipment,
                out item))
            {
                return true;
            }

            RebuildLoadoutEditorItemIndexes();
            return TryGetIndexedLoadoutEditorItem(
                itemId,
                LoadoutEditorEquipmentItemsById,
                LoadoutEditorProfile.Inventory.Equipment,
                out item);
        }

        internal static bool TryGetLoadoutEditorItem(string itemId, out Item item)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(itemId) || LoadoutEditorProfile?.Inventory == null || LoadoutEditorOverlayRoot == null)
            {
                return false;
            }

            if (TryGetLoadoutEditorEquipmentItem(itemId, out item))
            {
                return true;
            }

            if (TryGetIndexedLoadoutEditorItem(
                itemId,
                LoadoutEditorStashItemsById,
                LoadoutEditorProfile.Inventory.Stash,
                out item))
            {
                return true;
            }

            RebuildLoadoutEditorItemIndexes();
            if (TryGetIndexedLoadoutEditorItem(
                    itemId,
                    LoadoutEditorEquipmentItemsById,
                    LoadoutEditorProfile.Inventory.Equipment,
                    out item))
            {
                return true;
            }

            return TryGetIndexedLoadoutEditorItem(
                itemId,
                LoadoutEditorStashItemsById,
                LoadoutEditorProfile.Inventory.Stash,
                out item);
        }

        internal static bool IsLoadoutEditorStashItem(Item item)
        {
            if (IsItemInLoadoutEditorRoot(item, LoadoutEditorProfile?.Inventory?.Stash))
            {
                return true;
            }

            RebuildLoadoutEditorItemIndexes();
            return TryGetIndexedLoadoutEditorItem(
                item?.Id,
                LoadoutEditorStashItemsById,
                LoadoutEditorProfile?.Inventory?.Stash,
                out Item indexedItem)
                && pitTeam.Compat.Il2CppBoundary.SameNativeObject(indexedItem, item);
        }

        internal static bool IsLoadoutEditorItem(Item item)
        {
            if (IsItemInLoadoutEditorRoot(item, LoadoutEditorProfile?.Inventory?.Stash)
                || IsItemInLoadoutEditorRoot(item, LoadoutEditorProfile?.Inventory?.Equipment))
            {
                return true;
            }

            string itemId = item?.Id;
            return !string.IsNullOrWhiteSpace(itemId)
                && (LoadoutEditorStashItemsById.ContainsKey(itemId)
                    || LoadoutEditorEquipmentItemsById.ContainsKey(itemId));
        }

        internal static void RebuildLoadoutEditorItemIndexes()
        {
            LoadoutEditorEquipmentItemsById.Clear();
            LoadoutEditorStashItemsById.Clear();

            IndexLoadoutEditorItems(LoadoutEditorProfile?.Inventory?.Equipment, LoadoutEditorEquipmentItemsById);
            IndexLoadoutEditorItems(LoadoutEditorProfile?.Inventory?.Stash, LoadoutEditorStashItemsById);
        }

        internal static void ClearLoadoutEditorItemIndexes()
        {
            LoadoutEditorEquipmentItemsById.Clear();
            LoadoutEditorStashItemsById.Clear();
        }

        internal static bool IsLoadoutEditorPinLockInteraction(EItemInfoButton button)
        {
            return button == EItemInfoButton.SetPin
                || button == EItemInfoButton.SetLock
                || button == EItemInfoButton.SetUnPin
                || button == EItemInfoButton.SetUnLock;
        }

        internal static void CaptureLoadoutEditorOriginalPinLocks(Item root)
        {
            LoadoutEditorOriginalPinLockStates.Clear();
            foreach (Item item in EnumerateItemTree(root))
            {
                LoadoutEditorOriginalPinLockStates[item.Id.ToString()] = item.PinLockState;
            }
        }

        internal static void ApplyLoadoutEditorOriginalPinLocks(Item root)
        {
            if (LoadoutEditorOriginalPinLockStates.Count == 0)
            {
                return;
            }

            foreach (Item item in EnumerateItemTree(root))
            {
                if (LoadoutEditorOriginalPinLockStates.TryGetValue(item.Id.ToString(), out EItemPinLockState state))
                {
                    item.PinLockState = state;
                }
            }
        }

        internal static void ClearLoadoutEditorOriginalPinLocks()
        {
            LoadoutEditorOriginalPinLockStates.Clear();
        }

        internal static bool ShouldBlockLoadoutEditorContainerOpen(Item item)
        {
            return item is CompoundItem
                && TryFindLoadoutEditorLockedItemInPath(item, out _);
        }

        internal static bool TryFindLoadoutEditorLockedItemInPath(Item item, out Item lockedItem)
        {
            lockedItem = null;
            if (item == null || LoadoutEditorOverlayRoot == null || !IsLoadoutEditorItem(item))
            {
                return false;
            }

            if (IsLoadoutEditorLockedItem(item))
            {
                lockedItem = item;
                return true;
            }

            foreach (Item parent in item.GetAllParentItems())
            {
                if (parent == null)
                {
                    continue;
                }

                if (IsLoadoutEditorLockedItem(parent))
                {
                    lockedItem = parent;
                    return true;
                }
            }

            return false;
        }

        internal static bool TryFindLoadoutEditorLockedItemInAddress(ItemAddress address, out Item lockedItem)
        {
            lockedItem = null;
            if (address == null || LoadoutEditorOverlayRoot == null)
            {
                return false;
            }

            foreach (Item parent in address.GetAllParentItems())
            {
                if (parent == null || !IsLoadoutEditorItem(parent))
                {
                    continue;
                }

                if (IsLoadoutEditorLockedItem(parent))
                {
                    lockedItem = parent;
                    return true;
                }
            }

            return false;
        }

        private static bool IsLoadoutEditorLockedItem(Item item)
        {
            if (item == null)
            {
                return false;
            }

            return item.PinLockState == EItemPinLockState.Locked
                || (LoadoutEditorOriginalPinLockStates.TryGetValue(item.Id.ToString(), out EItemPinLockState originalState)
                    && originalState == EItemPinLockState.Locked);
        }

        private static bool IsItemInLoadoutEditorRoot(Item item, Item root)
        {
            if (item == null || root == null || LoadoutEditorOverlayRoot == null)
            {
                return false;
            }

            if (pitTeam.Compat.Il2CppBoundary.SameNativeObject(item, root) || string.Equals(item.Id, root.Id, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (Item parent in item.GetAllParentItems(false))
            {
                if (parent == null)
                {
                    continue;
                }

                if (pitTeam.Compat.Il2CppBoundary.SameNativeObject(parent, root) || string.Equals(parent.Id, root.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetIndexedLoadoutEditorItem(
            string itemId,
            Dictionary<string, Item> itemsById,
            Item root,
            out Item item)
        {
            item = null;
            if (string.IsNullOrWhiteSpace(itemId) || itemsById == null || root == null || LoadoutEditorOverlayRoot == null)
            {
                return false;
            }

            if (!itemsById.TryGetValue(itemId, out Item candidate) || candidate == null)
            {
                return false;
            }

            if (!IsItemInLoadoutEditorRoot(candidate, root))
            {
                return false;
            }

            item = candidate;
            return true;
        }

        private static void IndexLoadoutEditorItems(Item root, Dictionary<string, Item> destination)
        {
            if (root == null || destination == null)
            {
                return;
            }

            foreach (Item item in EnumerateLoadoutEditorItemTree(root))
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Id))
                {
                    continue;
                }

                destination[item.Id] = item;
            }
        }

        private static IEnumerable<Item> EnumerateItemTree(Item root)
        {
            return EnumerateLoadoutEditorItemTree(root);
        }

        private static IEnumerable<Item> EnumerateLoadoutEditorItemTree(Item root)
        {
            if (root == null)
            {
                yield break;
            }

            HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
            Stack<Item> pendingItems = new Stack<Item>();
            pendingItems.Push(root);

            while (pendingItems.Count > 0)
            {
                Item current = pendingItems.Pop();
                if (current == null || !seenIds.Add(current.Id))
                {
                    continue;
                }

                yield return current;

                if (current is EFT.InventoryLogic.ContainerCollection collection)
                {
                    foreach (Item item in collection.GetAllItemsFromCollection())
                    {
                        if (item != null)
                        {
                            pendingItems.Push(item);
                        }
                    }
                }

                foreach (RepairableComponent repairable in current.GetItemComponentsInChildren<RepairableComponent>(true))
                {
                    if (repairable?.Item != null)
                    {
                        pendingItems.Push(repairable.Item);
                    }
                }

                if (!current.TryGetItemComponent<ArmorHolderComponent>(out ArmorHolderComponent armorHolder)
                    || armorHolder?.ArmorSlots == null)
                {
                    continue;
                }

                foreach (ArmorSlot armorSlot in armorHolder.ArmorSlots)
                {
                    if (armorSlot?.ContainedItem != null)
                    {
                        pendingItems.Push(armorSlot.ContainedItem);
                    }
                }
            }
        }

        private static Il2CppSystem.Collections.Generic.Dictionary<EMenuType, EStateSwitcher> CreateSettingsTaskBarButton(EStateSwitcher state)
        {
            var buttons = new Il2CppSystem.Collections.Generic.Dictionary<EMenuType, EStateSwitcher>();
            buttons.Add(EMenuType.Settings, state);
            return buttons;
        }

        private static readonly Il2CppSystem.Collections.Generic.Dictionary<EMenuType, EStateSwitcher> DisabledSettingsTaskBarButton =
            CreateSettingsTaskBarButton(EStateSwitcher.Disabled);
        private static readonly Il2CppSystem.Collections.Generic.Dictionary<EMenuType, EStateSwitcher> EnabledSettingsTaskBarButton =
            CreateSettingsTaskBarButton(EStateSwitcher.Enabled);

        private static void MarkSquadRosterDirty(string accountId = null)
        {
            if (!string.IsNullOrWhiteSpace(accountId))
            {
                Components.SquadControlMenuUi.RequestRosterTileRefreshOnNextInject(accountId);
                return;
            }

            Components.SquadControlMenuUi.RequestRosterRefreshOnNextInject();
        }

        internal static void PrepareReturnOverride(Action callback)
        {
            PendingBackOverrideAction = callback;
        }

        internal static void ClearPendingReturnOverride()
        {
            PendingBackOverrideAction = null;
        }

        internal static void PreparePendingRecruitProfileView(string accountId)
        {
            PendingRecruitProfileAccountId = string.IsNullOrWhiteSpace(accountId) ? null : accountId;
        }

        internal static void ClearPendingRecruitProfileView(string accountId = null)
        {
            if (string.IsNullOrWhiteSpace(accountId)
                || string.Equals(PendingRecruitProfileAccountId, accountId, StringComparison.Ordinal))
            {
                PendingRecruitProfileAccountId = null;
            }
        }

        private static bool ConsumePendingRecruitProfileView(string accountId)
        {
            if (string.IsNullOrWhiteSpace(accountId)
                || !string.Equals(PendingRecruitProfileAccountId, accountId, StringComparison.Ordinal))
            {
                return false;
            }

            PendingRecruitProfileAccountId = null;
            return true;
        }

        internal static void RestoreMenuTaskBarForReturnOverride()
        {
            if (!TaskBarDisabledForReturnOverride)
            {
                return;
            }

            TaskBarDisabledForReturnOverride = false;

            try
            {
                if (!MonoBehaviourSingleton<PreloaderUI>.Instantiated)
                {
                    return;
                }

                PreloaderUI preloaderUi = MonoBehaviourSingleton<PreloaderUI>.Instance;
                if (preloaderUi?.MenuTaskBar == null)
                {
                    return;
                }

                preloaderUi.MenuTaskBar.SetButtonsInteractable(true);
                preloaderUi.MenuTaskBar.SetCustomButtonsAvailability(EnabledSettingsTaskBarButton);
            }
            catch (Exception ex)
            {
                pitFireTeam.Log.LogWarning($"[UI] Failed to restore bottom navigation buttons after teammate profile: {ex.Message}");
            }
        }

        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(OtherPlayerProfileScreen),
                "Show",
                new Type[] { typeof(ResultProfile), typeof(InventoryController), typeof(EItemViewType), typeof(EFT.IEftSession), typeof(bool) });
        }

        [PatchPrefix]
        private static void PrepareProfileText(OtherPlayerProfileScreen __instance, ResultProfile profile)
        {
            RestoreShortStatsLabelStyles();
            if (PendingBackOverrideAction != null || Modules.SquadSideSelectionFlow.SquadModeActive
                || (ViewedProfile != null && ViewedProfile.AccountId == profile?.AccountId))
                PrepareLocalizedTemplates(__instance.transform);
        }

        [PatchPostfix]
        private static void PatchPostfix(OtherPlayerProfileScreen __instance, ResultProfile profile, InventoryController inventoryController, EItemViewType viewType, EFT.IEftSession session)
        {
            Il2CppBoundary.EnsureInjectedComponentRegistered<ProfileOverlayHeaderDragHandle>();
            Il2CppBoundary.EnsureInjectedComponentRegistered<ProfileTooltipHoverController>();
            ConfigureBackOverride(__instance);
            ApplyProfileScaleCompensation(__instance);

            InventoryPlayerModelWithStatsWindow playerModelWindow =
                PlayerModelWindowAccessor.GetValue(__instance) as InventoryPlayerModelWithStatsWindow;
            if (playerModelWindow == null)
            {
                if (ActiveBackOverrideAction != null)
                    NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfileControlsUnavailable"), ENotificationDurationType.Default);
                return;
            }

            RestoreProfileRightSideContent(__instance);
            ResetTeammateProfileUi(playerModelWindow);

            if (session?.Profile != null
                && session.Profile.AccountId == profile.AccountId)
            {
                RestoreHideoutButtonVisuals(__instance, profile);
                return;
            }

            RestoreHideoutButtonVisuals(__instance, profile);

            if (ConsumePendingRecruitProfileView(profile.AccountId))
            {
                HideProfileActions(__instance);
                ClearProfileRightSideContent(__instance);
                DisplaySkillsPanel(__instance, profile, inventoryController, session, TryLoadProfileOptions(profile.AccountId));
                return;
            }

            FriendlyTeammateProfileOptions options = TryLoadProfileOptions(profile.AccountId);
            if (options == null)
            {
                if (ActiveBackOverrideAction != null)
                    NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfileControlsUnavailable"), ENotificationDurationType.Default);
                return;
            }

            if (options.Loadouts == null || options.Loadouts.Count == 0)
            {
                pitFireTeam.Log.LogWarning($"[UI] Teammate profile patch aborted: no loadout options returned for '{profile.AccountId}'.");
                NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfileControlsUnavailable"), ENotificationDurationType.Default);
                return;
            }

            if (!TryGetClothingPanel(__instance, playerModelWindow, out RectTransform clothingPanel, out InventoryClothingSelectionPanel clothingSelectionPanel, out Transform parent))
            {
                pitFireTeam.Log.LogWarning("[UI] Teammate profile patch aborted: clothing panel not found on profile screen.");
                NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfileControlsUnavailable"), ENotificationDurationType.Default);
                return;
            }

            Modules.Logger.LogInfo($"[UI] Applying teammate profile customization UI for '{profile.AccountId}'.");
            ViewedProfile = profile;
            TeammateEquipmentBuildsScreenFlow.FinishReturnIfMatches(profile.AccountId);
            ActiveProfileScreen = __instance;
            ActiveProfileInventoryController = inventoryController;
            RememberActiveBackendInventoryController(session, inventoryController);
            ActiveProfileSession = session;
            ActiveProfilePlayerModelWindow = playerModelWindow;
            RefreshTeammateShortStats(profile, playerModelWindow);
            playerModelWindow.OnCustomizationChanged -= ProfileCustomizationChangedHandler;
            playerModelWindow.OnCustomizationChanged += ProfileCustomizationChangedHandler;

            HideProfileActions(__instance);
            ClearProfileRightSideContent(__instance);
            ConfigureNicknameRenameUi(__instance, playerModelWindow, profile);
            MoveFactionBadgeForFollowerProfile(__instance, playerModelWindow);

            clothingPanel.gameObject.SetActive(true);
            try
            {
                DisplayClothingOptions(session.Profile.Info.GameVersion, profile.PlayerVisualRepresentation, playerModelWindow, inventoryController, clothingSelectionPanel, options);
            }
            catch (Exception ex)
            {
                clothingPanel.gameObject.SetActive(false);
                Modules.Logger.LogError($"[UI] Failed to initialize clothes for teammate '{profile.AccountId}'.");
                Modules.Logger.LogError(ex);
                NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfileClothingUnavailable"), ENotificationDurationType.Default);
            }

            EFT.UI.UIParent ui = UiAccessor.GetValue(__instance) as EFT.UI.UIParent;
            if (ui == null)
            {
                return;
            }

            if (LoadoutSelector != null)
            {
                GameObject.Destroy(LoadoutSelector.gameObject);
                LoadoutSelector = null;
            }

            ResetProficiencyUi();
            StopPendingAggressionPersist();

            try
            {
                Transform rightSide = __instance.transform.Find("RightSide")
                    ?? FindChildRecursive(__instance.transform, "RightSide");
                if (rightSide == null)
                {
                    pitFireTeam.Log.LogWarning("[UI] Profile actions skipped: RightSide was not found.");
                    return;
                }

                RectTransform actionRoot = CreateProfileActionsContainer(__instance, rightSide);
                if (actionRoot == null)
                {
                    pitFireTeam.Log.LogWarning("[UI] Profile actions skipped: action container could not be created.");
                    return;
                }

                DisplayLoadoutOptions(__instance, profile, inventoryController, session, actionRoot, playerModelWindow, options);
                CreateEditLoadoutButton(__instance, actionRoot, rightSide, profile, 0);
                CreateProficiencyButton(__instance, actionRoot, rightSide, profile, options);
                CreateEditNameButton(__instance, actionRoot, rightSide, profile, 3);
                DisplaySkillsPanel(__instance, profile, inventoryController, session, options);
                ShowProfileRecoveryOverlay(__instance, options.RecoveryNotice);
            }
            catch (Exception ex)
            {
                Modules.Logger.LogError("[UI] Failed to apply teammate profile elements.");
                Modules.Logger.LogError(ex);
                NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfileControlsUnavailable"), ENotificationDurationType.Default);
            }
        }


        private static RectTransform CreateProfileActionsContainer(OtherPlayerProfileScreen screen, Transform parent)
        {
            if (screen == null || parent == null)
            {
                return null;
            }

            CloseProfileOptionsOverlay();
            if (ProfileActionsContainer != null)
            {
                GameObject.Destroy(ProfileActionsContainer.gameObject);
                ProfileActionsContainer = null;
            }

            GameObject host = new GameObject(
                "pitFireTeam_ProfileActions",
                Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
            host.transform.SetParent(parent, false);
            RectTransform rect = host.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, 112f);
            rect.localScale = Vector3.one;
            rect.SetAsFirstSibling();
            host.SetActive(true);
            ProfileActionsContainer = rect;
            return rect;
        }

        private static DefaultUIButton CreateProfileActionButton(
            OtherPlayerProfileScreen screen,
            RectTransform parent,
            string objectName,
            string caption,
            int slot,
            Action onClick)
        {
            if (screen == null || parent == null || slot < 0 || slot > 4 || onClick == null)
            {
                return null;
            }

            DefaultUIButton template = BackButtonField?.GetValue(screen) as DefaultUIButton;
            if (template == null)
            {
                pitFireTeam.Log.LogWarning("[UI] Profile action skipped: stock button template was not found.");
                return null;
            }

            DefaultUIButton button = GameObject.Instantiate(template, parent, false);
            if (button == null)
            {
                pitFireTeam.Log.LogWarning("[UI] Profile action skipped: stock button clone failed.");
                return null;
            }

            button.name = objectName;
            button.gameObject.SetActive(true);
            button.Interactable = true;
            button.SetRawText(caption, 16);
            HideProfileButtonIconContainer(button);
            HideHideoutButtonDecorations(button);
            button.OnClick.RemoveAllListeners();
            button.OnClick.AddListener((System.Action)onClick);

            ConfigureCompactProfileActionButton(button, slot);
            return button;
        }

        private static void CloseProfileOptionsOverlay()
        {
            if (ProfileOptionsOverlayRoot != null)
            {
                GameObject.Destroy(ProfileOptionsOverlayRoot);
                ProfileOptionsOverlayRoot = null;
            }
        }


        internal static void ClearProfileActionUi()
        {
            ClearScenarioGuardUniformLabel();
            RestoreShortStatsLabelStyles();
            CloseProfileOptionsOverlay();
            if (ProfileActionsContainer != null)
            {
                GameObject.Destroy(ProfileActionsContainer.gameObject);
                ProfileActionsContainer = null;
            }

            if (LoadoutSelector != null)
            {
                GameObject.Destroy(LoadoutSelector.gameObject);
                LoadoutSelector = null;
            }

            EditLoadoutButton = null;
            EditLoadoutButtonRoot = null;
            LoadoutTacticButton = null;
            NicknameRenameButton = null;
            NicknameRenameButtonRoot = null;
        }

        private static bool IsMarksmanTactic(string tactic)
        {
            return string.Equals(tactic, "marksman", StringComparison.OrdinalIgnoreCase);
        }

        private static void SetProficiencyAggressionForTactic(bool isMarksman)
        {
            ActiveProfileTactic = isMarksman ? "Marksman" : "Rifleman";
            ActiveProfileAggression = isMarksman ? 30f : 50f;
            if (ProficiencyAggressionSlider != null)
            {
                ProficiencyAggressionSlider.UpdateValue(ActiveProfileAggression, false, new Il2CppSystem.Nullable<float>(0f), new Il2CppSystem.Nullable<float>(100f));
            }
        }

        private static void CreateAggressionRowContent(RectTransform rowRoot, ResultProfile profile, float aggressionValue, bool interactable)
        {
            if (rowRoot == null || profile == null)
            {
                return;
            }

            CanvasGroup rowCanvasGroup = rowRoot.GetComponent<CanvasGroup>() ?? rowRoot.gameObject.AddComponent<CanvasGroup>();
            rowCanvasGroup.alpha = 1f;

            CustomTextMeshProUGUI label = CreateAggressionLabel(
                "pitFireTeam_AggressionLabel",
                rowRoot,
                GetSocialUiText("ProfileAggression"),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(44f, 0f),
                new Vector2(190f, 28f),
                18f,
                TextAlignmentOptions.MidlineLeft);

            NumberSlider slider = CloneStockNumberSlider(rowRoot);
            if (slider == null)
            {
                return;
            }

            ProficiencyAggressionSlider = slider;

            RectTransform sliderRoot = pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(slider.transform);
            if (sliderRoot != null)
            {
                float sliderHeight = sliderRoot.sizeDelta.y > 0f ? sliderRoot.sizeDelta.y : 36f;
                sliderRoot.anchorMin = new Vector2(0f, 0.5f);
                sliderRoot.anchorMax = new Vector2(0f, 0.5f);
                sliderRoot.pivot = new Vector2(0f, 0.5f);
                sliderRoot.anchoredPosition = new Vector2(243f, 0f);
                sliderRoot.sizeDelta = new Vector2(300f, sliderHeight);
                sliderRoot.localScale = Vector3.one;
            }

            Transform captionRoot = sliderRoot?.Find("Caption");
            if (captionRoot != null)
            {
                GameObject.Destroy(captionRoot.gameObject);
            }

            slider.Show(0f, 100f, "0");
            slider.UpdateValue(Mathf.Round(aggressionValue), false, new Il2CppSystem.Nullable<float>(0f), new Il2CppSystem.Nullable<float>(100f));

            Slider stockSlider = slider.GetComponentInChildren<Slider>(true);
            TMP_InputField valueInput = NumberSliderValueInputField?.GetValue(slider) as TMP_InputField;
            ConfigureSliderValueInputChrome(valueInput);
            if (stockSlider != null)
            {
                stockSlider.interactable = interactable;
            }

            slider.enabled = interactable;

            if (valueInput != null)
            {
                valueInput.readOnly = !interactable;
                valueInput.interactable = interactable;
            }
            if (!interactable)
            {
                Color disabledColor = new Color(0.62f, 0.62f, 0.62f, 1f);
                if (label != null)
                {
                    label.color = disabledColor;
                }
            }

            slider.Bind((System.Action<float>)(value =>
            {
                int roundedValue = Mathf.RoundToInt(value);
                if (roundedValue == Mathf.RoundToInt(ActiveProfileAggression)) return;
                ActiveProfileAggression = roundedValue;
                if (interactable)
                {
                    ScheduleAggressionPersist(profile.AccountId, roundedValue);
                }
            }));
        }

        private static CustomTextMeshProUGUI CreateAggressionLabel(
            string name,
            RectTransform parent,
            string text,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 anchoredPosition,
            Vector2 size,
            float fontSize,
            TextAlignmentOptions alignment)
        {
            GameObject labelObject = new GameObject(name, Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>(), Il2CppInterop.Runtime.Il2CppType.Of<CustomTextMeshProUGUI>());
            labelObject.transform.SetParent(parent, false);

            RectTransform rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;

            CustomTextMeshProUGUI label = labelObject.GetComponent<CustomTextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = alignment;
            label.color = new Color(0.87f, 0.87f, 0.84f, 1f);
            label.raycastTarget = false;
            return label;
        }

        private static SettingsScreen ResolveSettingsScreenTemplate()
        {
            return Resources.FindObjectsOfTypeAll<SettingsScreen>()
                .FirstOrDefault(screen =>
                    screen != null &&
                    SettingsScreenGameTabField?.GetValue(screen) is GameSettingsTab);
        }

        private static GameSettingsTab ResolveGameSettingsTabTemplate()
        {
            SettingsScreen settingsScreen = ResolveSettingsScreenTemplate();
            return SettingsScreenGameTabField?.GetValue(settingsScreen) as GameSettingsTab;
        }

        private static RectTransform ResolveSettingsSliderContainerTemplate()
        {
            if (CachedSettingsSliderContainerTemplate != null)
            {
                return CachedSettingsSliderContainerTemplate;
            }

            GameSettingsTab gameSettingsTab = ResolveGameSettingsTabTemplate();
            if (gameSettingsTab == null)
            {
                return null;
            }

            Transform sliderRoot = gameSettingsTab.transform.Find("Image/Scroll View/Viewport/Other Settings/Scrolls/FOV");
            if (pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(sliderRoot) is RectTransform sliderRect)
            {
                CachedSettingsSliderContainerTemplate = sliderRect;
                return CachedSettingsSliderContainerTemplate;
            }

            NumberSlider fallbackSlider = ResolveSettingsSliderTemplate();
            CachedSettingsSliderContainerTemplate = pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(fallbackSlider?.transform);
            return CachedSettingsSliderContainerTemplate;
        }

        private static NumberSlider ResolveSettingsSliderTemplate()
        {
            GameSettingsTab gameSettingsTab = ResolveGameSettingsTabTemplate();
            SettingFloatSlider template = GameSettingsSliderTemplateAccessor.GetValue(gameSettingsTab) as SettingFloatSlider;
            return template?.Slider;
        }

        private static NumberSlider CloneStockNumberSlider(RectTransform parent)
        {
            RectTransform templateRoot = ResolveSettingsSliderContainerTemplate();
            if (templateRoot == null)
            {
                return null;
            }

            RectTransform sliderRoot = GameObject.Instantiate(templateRoot, parent, false);
            NumberSlider slider = sliderRoot.GetComponent<NumberSlider>() ?? sliderRoot.GetComponentInChildren<NumberSlider>(true);
            if (slider == null)
            {
                GameObject.Destroy(sliderRoot.gameObject);
                return null;
            }

            slider.name = "pitFireTeam_ProfileAggressionSlider";
            sliderRoot.localScale = Vector3.one;

            TMP_InputField valueInput = NumberSliderValueInputField?.GetValue(slider) as TMP_InputField;
            ConfigureSliderValueInputChrome(valueInput);
            HideStockLabelContainers(sliderRoot, valueInput?.transform);

            slider.gameObject.SetActive(true);
            return slider;
        }

        private static void ConfigureSliderValueInputChrome(TMP_InputField valueInput)
        {
            if (valueInput == null)
            {
                return;
            }

            Image background = valueInput.GetComponent<Image>();
            if (background != null)
            {
                background.color = Color.clear;
                background.raycastTarget = true;
                valueInput.targetGraphic = background;
            }

            ColorBlock colors = valueInput.colors;
            colors.normalColor = Color.clear;
            colors.highlightedColor = Color.clear;
            colors.pressedColor = Color.clear;
            colors.selectedColor = Color.clear;
            colors.disabledColor = Color.clear;
            valueInput.colors = colors;
        }

        private static void HideStockLabelContainers(Transform root, Transform exemptRoot)
        {
            if (root == null)
            {
                return;
            }

            HashSet<GameObject> hiddenObjects = new HashSet<GameObject>();

            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                HideStockLabelContainer(root, exemptRoot, label?.transform, hiddenObjects);
            }

            foreach (Text label in root.GetComponentsInChildren<Text>(true))
            {
                HideStockLabelContainer(root, exemptRoot, label?.transform, hiddenObjects);
            }

            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                DestroyStockLabelObject(exemptRoot, label?.transform);
            }

            foreach (Text label in root.GetComponentsInChildren<Text>(true))
            {
                DestroyStockLabelObject(exemptRoot, label?.transform);
            }
        }

        private static void HideStockLabelContainer(Transform root, Transform exemptRoot, Transform labelTransform, HashSet<GameObject> hiddenObjects)
        {
            if (root == null || labelTransform == null)
            {
                return;
            }

            if (exemptRoot != null && (labelTransform == exemptRoot || labelTransform.IsChildOf(exemptRoot)))
            {
                return;
            }

            Transform current = labelTransform;
            while (current != null && current != root)
            {
                if (current.TryGetComponent(out LayoutElement _))
                {
                    if (hiddenObjects.Add(current.gameObject))
                    {
                        current.gameObject.SetActive(false);
                    }

                    return;
                }

                current = current.parent;
            }
        }

        private static void DestroyStockLabelObject(Transform exemptRoot, Transform labelTransform)
        {
            if (labelTransform == null)
            {
                return;
            }

            if (exemptRoot != null && (labelTransform == exemptRoot || labelTransform.IsChildOf(exemptRoot)))
            {
                return;
            }

            GameObject.Destroy(labelTransform.gameObject);
        }

        private static void ScheduleAggressionPersist(string accountId, int aggression)
        {
            StopPendingAggressionPersist();

            if (string.IsNullOrWhiteSpace(accountId) || pitFireTeam.Instance == null)
            {
                return;
            }

            int revision = ++PendingAggressionPersistRevision;
            PendingAggressionPersistCoroutine = pitFireTeam.Instance.StartCoroutine(
                PersistAggressionDelayed(accountId, aggression, revision));
        }

        internal static void StopPendingAggressionPersist()
        {
            if (PendingAggressionPersistCoroutine != null && pitFireTeam.Instance != null)
            {
                pitFireTeam.Instance.StopCoroutine(PendingAggressionPersistCoroutine);
            }

            PendingAggressionPersistCoroutine = null;
        }

        private static System.Collections.IEnumerator PersistAggressionDelayed(string accountId, int aggression, int revision)
        {
            yield return new WaitForSecondsRealtime(0.35f);

            if (revision != PendingAggressionPersistRevision)
            {
                yield break;
            }

            PendingAggressionPersistCoroutine = null;

            try
            {
                string responseJson = RequestHandler.PostJson(AggressionRoute, SerializeBody(new FriendlyTeammateAggressionRequest
                {
                    aid = accountId,
                    aggression = aggression
                }));
                EnsureBodySuccess(responseJson);
                MarkSquadRosterDirty(accountId);
            }
            catch (Exception ex)
            {
                Modules.Logger.LogError("[UI] Failed to persist teammate aggression change.");
                Modules.Logger.LogError(ex);
                ShowProfileSaveFailure();
            }
        }

        private static bool IsDefaultTacticSelection(string tactic)
        {
            return string.IsNullOrWhiteSpace(tactic)
                || string.Equals(tactic, "default", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tactic, "rifleman", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tactic, "balanced", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetTacticDisplayName(string tactic)
        {
            if (string.IsNullOrWhiteSpace(tactic))
            {
                return GetSocialUiText("ProfileTactic");
            }

            if (IsDefaultTacticSelection(tactic))
            {
                return GetSocialUiText("ProfileTactic");
            }

            if (string.Equals(tactic, "marksman", StringComparison.OrdinalIgnoreCase))
            {
                return GetSocialUiText("ProfileTacticMarksman");
            }

            if (string.Equals(tactic, "protector", StringComparison.OrdinalIgnoreCase))
            {
                return GetSocialUiText("ProfileTacticProtector");
            }

            return tactic;
        }

        private static void CreateEditLoadoutButton(OtherPlayerProfileScreen screen, RectTransform actionContainer, Transform parent, ResultProfile profile, int slot = 0)
        {
            if (EditLoadoutButtonRoot != null)
            {
                GameObject.Destroy(EditLoadoutButtonRoot.gameObject);
                EditLoadoutButtonRoot = null;
            }

            EditLoadoutButton = null;
            if (screen == null || actionContainer == null || parent == null || profile == null)
            {
                return;
            }

            bool realTransferMode = pitFireTeam.IsFollowerLoadoutRealTransferMode();
            DefaultUIButton button = CreateProfileActionButton(
                screen,
                actionContainer,
                "pitFireTeam_EditLoadoutButton",
                GetSocialUiText("EditLoadout"),
                slot,
                () =>
                {
                    ShowLoadoutEditorOverlay(screen, profile);
                });

            EditLoadoutButton = button;
            EditLoadoutButtonRoot = button?.transform;
            if (realTransferMode)
                CreateProfileActionButton(screen, actionContainer, "pitFireTeam_BuyLoadoutButton", GetSocialUiText("BuyGearLoadout"), 4,
                    () => TeammateEquipmentBuildsScreenFlow.Open(profile, session: ActiveProfileSession, inventoryController: ActiveProfileInventoryController));
        }


        private static Vector2 GetProfileControlRowOffset(RectTransform reference, int rowOffset)
        {
            return new Vector2(0f, -72f * Mathf.Max(1, rowOffset) * ResolveUiScaleCompensation(reference));
        }

        private static void ApplyProfileScaleCompensation(OtherPlayerProfileScreen screen)
        {
            if (screen == null)
            {
                return;
            }

            float compensation = ResolveUiScaleCompensation(screen);
            DefaultUIButton backButton = BackButtonField?.GetValue(screen) as DefaultUIButton;
            var currentPointers = new HashSet<long>();
            ApplyProfileScaleCompensation(pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(backButton?.transform), compensation, currentPointers);
            ApplyProfileScaleCompensation(pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(screen.transform.Find("Background")), compensation, currentPointers);
            foreach (long stalePointer in ProfileScaleOriginalPositions.Keys.Where(pointer => !currentPointers.Contains(pointer)).ToArray()) ProfileScaleOriginalPositions.Remove(stalePointer);
        }

        private static void ApplyProfileScaleCompensation(RectTransform rect, float compensation, HashSet<long> currentPointers)
        {
            if (rect == null || rect.Pointer == IntPtr.Zero) return;
            long pointer = rect.Pointer.ToInt64(); currentPointers.Add(pointer);
            if (!ProfileScaleOriginalPositions.TryGetValue(pointer, out Vector2 originalPosition))
            {
                originalPosition = rect.anchoredPosition;
                ProfileScaleOriginalPositions[pointer] = originalPosition;
            }
            rect.anchoredPosition = new Vector2(originalPosition.x, originalPosition.y * compensation);
        }

        private static float ResolveUiScaleCompensation(Component reference)
        {
            Canvas canvas = reference != null ? reference.GetComponentInParent<Canvas>() : null;
            CanvasScaler scaler = canvas != null ? canvas.GetComponent<CanvasScaler>() : null;
            if (scaler == null || scaler.scaleFactor <= 0.001f || EFT.UI.UICanvasScalerController._scaleFactor <= 0.001f)
            {
                return 1f;
            }

            return Mathf.Clamp(EFT.UI.UICanvasScalerController._scaleFactor / scaler.scaleFactor, 0.5f, 2f);
        }

        private static void ConfigureBackOverride(OtherPlayerProfileScreen screen)
        {
            if (screen == null)
            {
                PendingBackOverrideAction = null;
                ActiveBackOverrideAction = null;
                return;
            }

            Action callback = PendingBackOverrideAction;
            PendingBackOverrideAction = null;
            ActiveBackOverrideAction = callback;

            if (callback == null)
            {
                return;
            }

            DisableMenuTaskBarForReturnOverride();
        }

        internal static void DisableMenuTaskBarForReturnOverride()
        {
            if (TaskBarDisabledForReturnOverride)
            {
                return;
            }

            try
            {
                if (!MonoBehaviourSingleton<PreloaderUI>.Instantiated)
                {
                    return;
                }

                PreloaderUI preloaderUi = MonoBehaviourSingleton<PreloaderUI>.Instance;
                if (preloaderUi?.MenuTaskBar == null)
                {
                    return;
                }

                if (!preloaderUi.MenuTaskBar.gameObject.activeSelf)
                {
                    return;
                }

                TaskBarDisabledForReturnOverride = true;
                preloaderUi.MenuTaskBar.SetButtonsInteractable(false, "Taskbar/Unavailable");
                preloaderUi.MenuTaskBar.SetCustomButtonsAvailability(DisabledSettingsTaskBarButton);
            }
            catch (Exception ex)
            {
                TaskBarDisabledForReturnOverride = false;
                pitFireTeam.Log.LogWarning($"[UI] Failed to disable bottom navigation buttons for teammate profile: {ex.Message}");
            }
        }

        private static bool TryGetClothingPanel(
            OtherPlayerProfileScreen screen,
            InventoryPlayerModelWithStatsWindow playerModelWindow,
            out RectTransform clothingPanel,
            out InventoryClothingSelectionPanel clothingSelectionPanel,
            out Transform parent)
        {
            clothingPanel = null;
            clothingSelectionPanel = null;
            parent = null;

            clothingSelectionPanel = ClothingPanelAccessor.GetValue(playerModelWindow) as InventoryClothingSelectionPanel;
            if (clothingSelectionPanel == null)
            {
                Transform playerModelTransform = playerModelWindow.transform;
                Transform hierarchyPanel = playerModelTransform.Find("ClothingPanel")
                    ?? playerModelTransform.Find("PlayerModelWithStats/ClothingPanel")
                    ?? screen?.transform.Find("PlayerModelWithStats/ClothingPanel")
                    ?? screen?.transform.Find("ClothingPanel");

                if (hierarchyPanel == null)
                {
                    hierarchyPanel = FindChildRecursive(playerModelTransform, "ClothingPanel")
                        ?? FindChildRecursive(screen?.transform, "ClothingPanel");
                }

                clothingSelectionPanel = hierarchyPanel?.GetComponent<InventoryClothingSelectionPanel>();
            }

            string nativeCastStatus = "not-run";
            try { clothingPanel = Il2CppBoundary.CastNativeOrNull<RectTransform>(clothingSelectionPanel?.transform); nativeCastStatus = clothingPanel == null ? "null" : "matched"; }
            catch (Exception ex) { nativeCastStatus = "error:" + ex.GetType().FullName; }
            parent = clothingPanel?.parent;
            bool success = clothingPanel != null && clothingSelectionPanel != null && parent != null;
            if (!success) pitFireTeam.Log.LogWarning($"[UI][ClothingPanelBinding] panelNull={clothingSelectionPanel == null}; panelProxy={clothingSelectionPanel?.GetType().FullName ?? "<null>"}; panelPtr={(clothingSelectionPanel == null ? "<null>" : "0x" + clothingSelectionPanel.Pointer.ToInt64().ToString("X"))}; transformProxy={clothingSelectionPanel?.transform?.GetType().FullName ?? "<null>"}; transformPtr={(clothingSelectionPanel == null ? "<null>" : "0x" + clothingSelectionPanel.transform.Pointer.ToInt64().ToString("X"))}; nativeType={DescribeNativeClass(clothingSelectionPanel?.transform)}; clrRect={clothingSelectionPanel?.transform is RectTransform}; nativeCast={nativeCastStatus}; rectPtr={(clothingPanel == null ? "<null>" : "0x" + clothingPanel.Pointer.ToInt64().ToString("X"))}; parentNull={parent == null}");
            return success;
        }

        internal static string DescribeNativeClass(Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase instance) { if (instance == null) return "<null>"; try { IntPtr c = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(instance.Pointer); if (c == IntPtr.Zero) return "<no-class>"; string ns = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_namespace_(c) ?? ""; string name = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name_(c) ?? "<unknown>"; return string.IsNullOrEmpty(ns) ? name : ns + "." + name; } catch (Exception ex) { return "error:" + ex.GetType().FullName; } }
        private static void ResetTeammateProfileUi(InventoryPlayerModelWithStatsWindow playerModelWindow)
        {
            CloseProfileRecoveryOverlay();
            ResetProficiencyUi();
            playerModelWindow.OnCustomizationChanged -= ProfileCustomizationChangedHandler;
            ViewedProfile = null;
            ActiveProfileScreen = null;
            ActiveProfileInventoryController = null;
            ActiveProfileSession = null;
            ActiveProfilePlayerModelWindow = null;
            ActiveTeammateLoadoutId = null;
            ActiveTeammateLoadoutName = null;
            RestoreFactionBadgePosition();
            ClearProfileActionUi();

            if (LoadoutSelector != null)
            {
                GameObject.Destroy(LoadoutSelector.gameObject);
                LoadoutSelector = null;
            }

            if (EditLoadoutButtonRoot != null)
            {
                GameObject.Destroy(EditLoadoutButtonRoot.gameObject);
                EditLoadoutButtonRoot = null;
            }

            EditLoadoutButton = null;

            if (NicknameRenameButtonRoot != null)
            {
                GameObject.Destroy(NicknameRenameButtonRoot.gameObject);
                NicknameRenameButtonRoot = null;
            }

            NicknameRenameButton = null;

            if (TryGetClothingPanel(null, playerModelWindow, out RectTransform clothingPanel, out _, out _)
                && clothingPanel != null)
            {
                clothingPanel.gameObject.SetActive(false);
            }
        }

        private static void MoveFactionBadgeForFollowerProfile(OtherPlayerProfileScreen screen, InventoryPlayerModelWithStatsWindow playerModelWindow)
        {
            RectTransform iconsContainer = FindFactionBadgeIconsContainer(screen, playerModelWindow);
            if (iconsContainer == null)
            {
                return;
            }

            if (!ReferenceEquals(FactionBadgeIconsContainer, iconsContainer))
            {
                RestoreFactionBadgePosition();
                FactionBadgeIconsContainer = iconsContainer;
                OriginalFactionBadgeAnchoredPosition = iconsContainer.anchoredPosition;
            }

            if (OriginalFactionBadgeAnchoredPosition == null)
            {
                OriginalFactionBadgeAnchoredPosition = iconsContainer.anchoredPosition;
            }

            iconsContainer.anchoredPosition = OriginalFactionBadgeAnchoredPosition.Value + FactionBadgeFollowerOffset;
        }

        public static void RestoreFactionBadgePosition()
        {
            if (FactionBadgeIconsContainer != null && OriginalFactionBadgeAnchoredPosition != null)
            {
                FactionBadgeIconsContainer.anchoredPosition = OriginalFactionBadgeAnchoredPosition.Value;
            }

            FactionBadgeIconsContainer = null;
            OriginalFactionBadgeAnchoredPosition = null;
        }

        private static RectTransform FindFactionBadgeIconsContainer(OtherPlayerProfileScreen screen, InventoryPlayerModelWithStatsWindow playerModelWindow)
        {
            Transform iconsContainer = playerModelWindow?.transform.Find("CharacterPanel/PlayerModelView/IconsContainer")
                ?? screen?.transform.Find("PlayerModelWithStats/CharacterPanel/PlayerModelView/IconsContainer")
                ?? FindChildRecursive(playerModelWindow?.transform, "IconsContainer")
                ?? FindChildRecursive(screen?.transform, "IconsContainer");

            return pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(iconsContainer);
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int index = 0; index < root.childCount; index++)
            {
                Transform child = root.GetChild(index);
                if (string.Equals(child.name, childName, StringComparison.Ordinal))
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        internal static readonly Il2CppSystem.Action<dropDownItem> ProfileCustomizationChangedHandler =
            (System.Action<dropDownItem>)PlayerModelWithStatsWindow_OnCustomizationChanged;

        public static void PlayerModelWithStatsWindow_OnCustomizationChanged(dropDownItem suit)
        {
            if (ViewedProfile == null)
            {
                return;
            }

            try
            {
                string body = ViewedProfile.Customization[EBodyModelPart.Body];
                string feet = ViewedProfile.Customization[EBodyModelPart.Feet];
                string responseJson = RequestHandler.PostJson(SuitRoute, SerializeBody(new FriendlyTeammateSuitRequest
                {
                    aid = ViewedProfile.AccountId,
                    suit = new string[] { body, feet }
                }));
                EnsureBodySuccess(responseJson);
                MarkSquadRosterDirty(ViewedProfile?.AccountId);
            }
            catch (Exception ex)
            {
                Modules.Logger.LogError("[UI] Failed to persist teammate suit change.");
                Modules.Logger.LogError(ex);
                ShowProfileSaveFailure();
            }
        }

        private static void ShowProfileSaveFailure()
        {
            NotificationManager.DisplayWarningNotification(GetSocialUiText("ProfileChangeSaveFailed"), ENotificationDurationType.Default);
        }

    }

    internal class OtherPlayerProfileScreenClosePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(OtherPlayerProfileScreen), "Close");
        }

        [PatchPostfix]
        private static void PatchPostfix(OtherPlayerProfileScreen __instance)
        {
            Action callback = OtherPlayerProfileScreenPatch.ActiveBackOverrideAction;
            OtherPlayerProfileScreenPatch.ActiveBackOverrideAction = null;
            OtherPlayerProfileScreenPatch.PendingBackOverrideAction = null;
            bool transitioningToBuildsScreen = TeammateEquipmentBuildsScreenFlow.ConsumeProfileTransition(callback);
            if (!transitioningToBuildsScreen)
            {
                TeammateEquipmentBuildsScreenFlow.ClearIfNotOpeningOrReturning();
                OtherPlayerProfileScreenPatch.RestoreMenuTaskBarForReturnOverride();
            }

            InventoryPlayerModelWithStatsWindow playerModelWindow =
                OtherPlayerProfileScreenPatch.PlayerModelWindowAccessor.GetValue(__instance)
                as InventoryPlayerModelWithStatsWindow;
            if (playerModelWindow != null)
            {
                playerModelWindow.OnCustomizationChanged -= OtherPlayerProfileScreenPatch.ProfileCustomizationChangedHandler;
            }

            OtherPlayerProfileScreenPatch.CloseLoadoutEditorOverlay();
            OtherPlayerProfileScreenPatch.ViewedProfile = null;
            OtherPlayerProfileScreenPatch.ActiveProfileScreen = null;
            OtherPlayerProfileScreenPatch.ActiveProfileInventoryController = null;
            OtherPlayerProfileScreenPatch.ActiveProfileSession = null;
            OtherPlayerProfileScreenPatch.ActiveProfilePlayerModelWindow = null;
            OtherPlayerProfileScreenPatch.ActiveTeammateLoadoutId = null;
            OtherPlayerProfileScreenPatch.ActiveTeammateLoadoutName = null;
            OtherPlayerProfileScreenPatch.ClearPendingRecruitProfileView();
            OtherPlayerProfileScreenPatch.CustomDropdownIds.Clear();
            OtherPlayerProfileScreenPatch.StopPendingAggressionPersist();
            OtherPlayerProfileScreenPatch.ResetProficiencyUi();
            OtherPlayerProfileScreenPatch.CloseRenameOverlay();
            OtherPlayerProfileScreenPatch.RestoreFactionBadgePosition();

            if (OtherPlayerProfileScreenPatch.OriginalNicknameLabel != null)
            {
                if (OtherPlayerProfileScreenPatch.OriginalNicknameAnchoredPosition != null
                    && pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(OtherPlayerProfileScreenPatch.OriginalNicknameLabel.transform) is RectTransform labelRect)
                {
                    labelRect.anchoredPosition = OtherPlayerProfileScreenPatch.OriginalNicknameAnchoredPosition.Value;
                }

                OtherPlayerProfileScreenPatch.OriginalNicknameLabel = null;
                OtherPlayerProfileScreenPatch.OriginalNicknameAnchoredPosition = null;
            }

            if (OtherPlayerProfileScreenPatch.NicknameRenameButton != null)
            {
                if (OtherPlayerProfileScreenPatch.NicknameRenameButton.name == "pitFireTeam_NicknameRenameButton")
                {
                    if (OtherPlayerProfileScreenPatch.NicknameRenameButtonRoot != null)
                    {
                        GameObject.Destroy(OtherPlayerProfileScreenPatch.NicknameRenameButtonRoot.gameObject);
                    }
                    else
                    {
                        GameObject.Destroy(OtherPlayerProfileScreenPatch.NicknameRenameButton.gameObject);
                    }
                }

                OtherPlayerProfileScreenPatch.NicknameRenameButtonRoot = null;
                OtherPlayerProfileScreenPatch.NicknameRenameButton = null;
            }

            if (OtherPlayerProfileScreenPatch.EditLoadoutButton != null)
            {
                GameObject.Destroy(OtherPlayerProfileScreenPatch.EditLoadoutButton.gameObject);
                OtherPlayerProfileScreenPatch.EditLoadoutButton = null;
            }

            OtherPlayerProfileScreenPatch.RestoreHideoutButtonVisuals(__instance);
            OtherPlayerProfileScreenPatch.RestoreProfileRightSideContent(__instance);
            OtherPlayerProfileScreenPatch.ClearProfileActionUi();

            if (OtherPlayerProfileScreenPatch.LoadoutSelector != null)
            {
                GameObject.Destroy(OtherPlayerProfileScreenPatch.LoadoutSelector.gameObject);
                OtherPlayerProfileScreenPatch.LoadoutSelector = null;
            }

            if (OtherPlayerProfileScreenPatch.EditLoadoutButtonRoot != null)
            {
                GameObject.Destroy(OtherPlayerProfileScreenPatch.EditLoadoutButtonRoot.gameObject);
                OtherPlayerProfileScreenPatch.EditLoadoutButtonRoot = null;
            }

            if (OtherPlayerProfileScreenPatch.SkillsPanel != null)
            {
                GameObject.Destroy(OtherPlayerProfileScreenPatch.SkillsPanel.gameObject);
                OtherPlayerProfileScreenPatch.SkillsPanel = null;
            }

            if (OtherPlayerProfileScreenPatch.SkillsPanelHost != null)
            {
                GameObject.Destroy(OtherPlayerProfileScreenPatch.SkillsPanelHost.gameObject);
                OtherPlayerProfileScreenPatch.SkillsPanelHost = null;
            }

            if (transitioningToBuildsScreen || callback == null)
            {
                return;
            }

            if (pitFireTeam.Instance == null)
            {
                callback();
                return;
            }

            pitFireTeam.Instance.StartCoroutine(InvokeBackOverrideNextFrame(callback));
        }

        private static System.Collections.IEnumerator InvokeBackOverrideNextFrame(Action callback)
        {
            yield return null;
            callback?.Invoke();
        }
    }
}
