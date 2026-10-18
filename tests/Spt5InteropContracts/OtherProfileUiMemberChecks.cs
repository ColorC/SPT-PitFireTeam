using Mono.Cecil;
using System.Text.Json;
internal static class OtherProfileUiMemberChecks
{
    public static int Run(string[] args)
    {
        string? assemblyPath = null;
        string? sourceRoot = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--assembly" && i + 1 < args.Length) assemblyPath = args[++i];
            else if (args[i] == "--source-root" && i + 1 < args.Length) sourceRoot = args[++i];
            else throw new ArgumentException("Expected --assembly and --source-root.");
        }
        if (string.IsNullOrWhiteSpace(assemblyPath) || string.IsNullOrWhiteSpace(sourceRoot))
            throw new ArgumentException("Expected --assembly <Assembly-CSharp.dll> --source-root <pitFireTeam>");
        assemblyPath = Path.GetFullPath(assemblyPath);
        sourceRoot = Path.GetFullPath(sourceRoot);
        using ModuleDefinition module = ModuleDefinition.ReadModule(assemblyPath);
        TypeDefinition screen = FindType(module, "EFT.UI.OtherPlayerProfileScreen");
        string patchPath = Path.Combine(sourceRoot, "client", "Patches", "OtherPlayerProfileScreenPatch.cs");
        string source = File.ReadAllText(patchPath);
        var expected = new Dictionary<string, (string MetadataType, string CSharpType)>
        {
            ["_playerModelWithStatsWindow"] = ("EFT.UI.InventoryPlayerModelWithStatsWindow", "InventoryPlayerModelWithStatsWindow"),
            ["_hideoutButton"] = ("EFT.UI.DefaultUiButtonNewStyle", "DefaultUiButtonNewStyle"),
            ["_reportPanel"] = ("EFT.UI.ReportPanel", "ReportPanel"),
            ["_overallStatsPanel"] = ("EFT.UI.OverallStatsPanel", "OverallStatsPanel"),
            ["_achievementsProgressBlock"] = ("UnityEngine.GameObject", "GameObject"),
            ["_achievementsBlockPlaceholder"] = ("UnityEngine.GameObject", "GameObject"),
            ["_weaponsBlockPlaceholder"] = ("UnityEngine.GameObject", "GameObject"),
            ["_nonWeaponItemsBlockPlaceholder"] = ("UnityEngine.GameObject", "GameObject"),
            ["_weaponsGridLayoutGroup"] = ("UnityEngine.UI.GridLayoutGroup", "GridLayoutGroup"),
            ["_nonWeaponItemsGridLayoutGroup"] = ("UnityEngine.UI.GridLayoutGroup", "GridLayoutGroup"),
        };
        var checks = new List<object>();
        bool pass = true;
        foreach (var pair in expected)
        {
            PropertyDefinition? property = screen.Properties.SingleOrDefault(p => p.Name == pair.Key);
            bool shape = property != null && property.PropertyType.FullName == pair.Value.MetadataType
                && property.GetMethod != null && !property.GetMethod.IsStatic
                && property.GetMethod.Parameters.Count == 0
                && property.GetMethod.ReturnType.FullName == pair.Value.MetadataType;
            bool legacyFieldAbsent = !screen.Fields.Any(f => f.Name == pair.Key);
            string declaration = "NativeUiMemberAccessor.ForProperty(typeof(OtherPlayerProfileScreen), \"" + pair.Key + "\", typeof(" + pair.Value.CSharpType + "), false, false)";
            bool sourceUsesAccessor = source.Contains(declaration, StringComparison.Ordinal);
            bool oldLookupAbsent = !source.Contains("AccessTools.Field(typeof(OtherPlayerProfileScreen), \"" + pair.Key + "\")", StringComparison.Ordinal);
            bool ok = shape && legacyFieldAbsent && sourceUsesAccessor && oldLookupAbsent;
            pass &= ok;
            checks.Add(new { member = pair.Key, metadataType = property?.PropertyType.FullName, propertyShape = shape, legacyFieldAbsent, sourceUsesAccessor, oldLookupAbsent, passed = ok });
        }
        var relatedExpected = new (string TypeName, string Member, string MetadataType, string CSharpType, string MemberType, string MemberExpression, bool RequiresSetter)[]
        {
            ("EFT.UI.InventoryPlayerModelWithStatsWindow", "_clothingPanel", "EFT.UI.InventoryClothingSelectionPanel", "InventoryPlayerModelWithStatsWindow", "InventoryClothingSelectionPanel", "\"_clothingPanel\"", false),
            ("EFT.UI.InventoryPlayerModelWithStatsWindow", "_nicknameLabel", "CustomTextMeshProUGUI", "InventoryPlayerModelWithStatsWindow", "CustomTextMeshProUGUI", "\"_nicknameLabel\"", false),
            ("Arena.UI.NicknameField", "_inputField", "TMPro.TMP_InputField", "NicknameField", "TMP_InputField", "\"_inputField\"", false),
            ("Arena.UI.NicknameField", "_statusLabel", "TMPro.TMP_Text", "NicknameField", "TMP_Text", "\"_statusLabel\"", false),
            ("Arena.UI.NicknameField", "_usedSymbolsCount", "EFT.UI.LocalizedText", "NicknameField", "LocalizedText", "\"_usedSymbolsCount\"", false),
            ("EFT.UI.Settings.SettingsScreen", "_gameSettingsScreen", "EFT.UI.Settings.GameSettingsTab", "SettingsScreen", "GameSettingsTab", "\"_gameSettingsScreen\"", false),
            ("EFT.UI.Settings.GameSettingsTab", "_floatSliderTemplate", "EFT.UI.Settings.SettingFloatSlider", "GameSettingsTab", "SettingFloatSlider", "\"_floatSliderTemplate\"", false),
            ("EFT.UI.NumberSlider", "_valueInput", "TMPro.TMP_InputField", "NumberSlider", "TMP_InputField", "\"_valueInput\"", false),
            ("EFT.UI.SkillsScreen", "_listTab", "Tab", "SkillsScreen", "Tab", "\"_listTab\"", false),
            ("EFT.UI.SkillsScreen", "_thumbsTab", "Tab", "SkillsScreen", "Tab", "\"_thumbsTab\"", false),
            ("EFT.UI.SkillsScreen", "_skillViewTabGroup", "EFT.UI.TabGroup", "SkillsScreen", "TabGroup", "\"_skillViewTabGroup\"", false),
            ("EFT.UI.SkillsAndMasteringScreen", "_skillsScreen", "EFT.UI.SkillsScreen", "SkillsAndMasteringScreen", "SkillsScreen", "\"_skillsScreen\"", false),
            ("EFT.UI.InventoryScreen", "_skillsAndMasteringScreen", "EFT.UI.SkillsAndMasteringScreen", "InventoryScreen", "SkillsAndMasteringScreen", "\"_skillsAndMasteringScreen\"", false),
            ("EFT.SkillManager", "Skills", "Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1<EFT.Skill>", "SkillManager", "Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<EFT.Skill>", "nameof(SkillManager.Skills)", true),
            ("EFT.SkillManager", "DisplayList", "Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1<EFT.Skill>", "SkillManager", "Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<EFT.Skill>", "nameof(SkillManager.DisplayList)", true),
            ("EFT.UI.TransferItemsScreen", "_stashPanel", "EFT.UI.SimpleStashPanel", "TransferItemsScreen", "SimpleStashPanel", "\"_stashPanel\"", false),
            ("EFT.UI.InventoryClothingSelectionPanel", "_upperSelector", "EFT.UI.InventoryCustomizationSelector", "InventoryClothingSelectionPanel", "InventoryCustomizationSelector", "\"_upperSelector\"", false),
            ("EFT.UI.InventoryClothingSelectionPanel", "_lowerSelector", "EFT.UI.InventoryCustomizationSelector", "InventoryClothingSelectionPanel", "InventoryCustomizationSelector", "\"_lowerSelector\"", false),
            ("EFT.UI.InventoryScreen", "_itemsPanel", "EFT.UI.ItemsPanel", "InventoryScreen", "ItemsPanel", "\"_itemsPanel\"", false),
            ("EFT.UI.ItemsPanel", "_complexStashPanel", "EFT.UI.DragAndDrop.ComplexStashPanel", "ItemsPanel", "ComplexStashPanel", "\"_complexStashPanel\"", false),
            ("EFT.UI.DragAndDrop.ComplexStashPanel", "_containersPanel", "EFT.UI.ContainersPanel", "ComplexStashPanel", "ContainersPanel", "\"_containersPanel\"", false),
            ("EFT.UI.ContainersPanel", "_slotViews", "Il2CppSystem.Collections.Generic.Dictionary`2<EFT.InventoryLogic.EquipmentSlot,EFT.UI.DragAndDrop.SlotView>", "ContainersPanel", "Il2CppSystem.Collections.Generic.Dictionary<EquipmentSlot, SlotView>", "\"_slotViews\"", false)
        };
        string patchesRoot = Path.Combine(sourceRoot, "client", "Patches");
        string allProfileSource = string.Join("\\n", Directory.GetFiles(patchesRoot, "OtherPlayerProfileScreenPatch*.cs").Select(File.ReadAllText));
        foreach (var item in relatedExpected)
        {
            TypeDefinition relatedType = FindType(module, item.TypeName);
            PropertyDefinition? property = relatedType.Properties.SingleOrDefault(p => p.Name == item.Member);
            bool propertyShape = property != null && property.PropertyType.FullName == item.MetadataType && property.GetMethod != null && !property.GetMethod.IsStatic && property.GetMethod.Parameters.Count == 0 && property.GetMethod.ReturnType.FullName == item.MetadataType && (!item.RequiresSetter || property.SetMethod != null);
            bool sameNameFieldAbsent = !relatedType.Fields.Any(f => f.Name == item.Member);
            string declaration = "NativeUiMemberAccessor.ForProperty(typeof(" + item.CSharpType + "), " + item.MemberExpression + ", typeof(" + item.MemberType + "), false, " + (item.RequiresSetter ? "true" : "false") + ")";
            bool sourceUsesAccessor = source.Contains(declaration, StringComparison.Ordinal);
            bool oldFieldLookupAbsent = !allProfileSource.Contains("AccessTools.Field(typeof(" + item.CSharpType + "), \\\"" + item.Member + "\\\")", StringComparison.Ordinal);
            bool ok = propertyShape && sameNameFieldAbsent && sourceUsesAccessor && oldFieldLookupAbsent;
            pass &= ok;
            checks.Add(new { member = item.TypeName + "." + item.Member, metadataType = property?.PropertyType.FullName, propertyShape, sameNameFieldAbsent, sourceUsesAccessor, oldFieldLookupAbsent, passed = ok });
        }

        // Settings moved from individual controls to native template wrappers in SPT5.
        // Check both sides of that boundary against this runtime's metadata.
        string squadSettingsSource = File.ReadAllText(Path.Combine(sourceRoot, "client", "Components", "SquadControlMenuUi.cs"));
        string squadAssetsSource = File.ReadAllText(Path.Combine(sourceRoot, "client", "Components", "SquadControlMenuUi.Assets.cs"));
        foreach (var item in new[]
        {
            (Member: "_toggleTemplate", Wrapper: "SettingToggle", Control: "Toggle", ControlType: "UpdatableToggle", Legacy: "_enableBlockInvites"),
            (Member: "_floatSliderTemplate", Wrapper: "SettingFloatSlider", Control: "Slider", ControlType: "NumberSlider", Legacy: "_fov")
        })
        {
            PropertyDefinition? templateProperty = FindType(module, "EFT.UI.Settings.GameSettingsTab").Properties.SingleOrDefault(p => p.Name == item.Member);
            PropertyDefinition? controlProperty = FindType(module, "EFT.UI.Settings." + item.Wrapper).Properties.SingleOrDefault(p => p.Name == item.Control);
            bool nativeTemplateShape = templateProperty?.PropertyType.FullName == "EFT.UI.Settings." + item.Wrapper
                && templateProperty.GetMethod != null && !templateProperty.GetMethod.IsStatic;
            bool nativeControlShape = controlProperty?.PropertyType.FullName == "EFT.UI." + item.ControlType
                && controlProperty.GetMethod != null && !controlProperty.GetMethod.IsStatic;
            bool usesNativeTemplate = squadSettingsSource.Contains("NativeUiMemberAccessor.ForProperty(typeof(GameSettingsTab), \"" + item.Member + "\", typeof(" + item.Wrapper + "), false, false)", StringComparison.Ordinal)
                && squadAssetsSource.Contains("template?." + item.Control, StringComparison.Ordinal);
            bool oldLookupAbsent = !squadSettingsSource.Contains("AccessTools.Field(typeof(GameSettingsTab), \"" + item.Legacy + "\")", StringComparison.Ordinal);
            bool passed = nativeTemplateShape && nativeControlShape && usesNativeTemplate && oldLookupAbsent;
            pass &= passed;
            checks.Add(new { member = "My Squad Settings " + item.Member + "." + item.Control, nativeTemplateShape, nativeControlShape, usesNativeTemplate, oldLookupAbsent, passed });
        }

        var propertyOnlyConsumers = new (string FileName, string TypeName, string Member, string MetadataType, string CSharpType, string AccessorName)[]
        {
            ("AddTeammateHeadSelectionPatch.cs", "Arena.UI.NicknameField", "_statusLabel", "TMPro.TMP_Text", "TMP_Text", "StatusLabelAccessor"),
            ("ChatFriendsPanelPatch.cs", "EFT.UI.Chat.ChatFriendsPanel", "_friendsButton", "FriendsListContentButton", "FriendsListContentButton", "FriendsButtonAccessor"),
            ("ChatFriendsPanelPatch.cs", "FriendsListContentButton", "_buttonLabel", "CustomTextMeshProUGUI", "CustomTextMeshProUGUI", "ButtonLabelAccessor"),
            ("ChatFriendsPanelPatch.cs", "EFT.UI.Chat.ChatFriendsPanel", "_friendsInputField", "EFT.UI.ValidationInputField", "ValidationInputField", "FriendsInputAccessor"),
            ("ChatFriendsPanelPatch.cs", "EFT.UI.Chat.ChatFriendsPanel", "_chatFriendsListPanel", "EFT.UI.Chat.ChatFriendsListPanel", "ChatFriendsListPanel", "FriendsListPanelAccessor"),
            ("ChatFriendsPanelPatch.cs", "EFT.UI.Chat.ChatFriendsListPanel", "_friendsLabel", "TMPro.TextMeshProUGUI", "TextMeshProUGUI", "FriendsLabelAccessor"),
            ("GestureMenuPatch.cs", "EFT.UI.Gestures.GesturesQuickPanel", "_textField", "CustomTextMeshProUGUI", "CustomTextMeshProUGUI", "TextAccessor"),
            ("GestureMenuPatch.cs", "EFT.UI.Gestures.GesturesQuickPanel", "_quickCommandObject", "UnityEngine.GameObject", "GameObject", "QuickCommandObjectAccessor"),
            ("RaidStartPatch.cs", "EFT.UI.Matchmaker.PartyInfoPanel", "_profile", "EFT.Profile", "Profile", "ProfileAccessor"),
            ("TeammateBackpackInteractionPatch.cs", "EFT.UI.SimpleStashPanel", "_simpleGridName", "TMPro.TextMeshProUGUI", "TextMeshProUGUI", "SimpleGridNameAccessor"),
            ("TeammateBackpackInteractionPatch.cs", "EFT.UI.SimpleStashPanel", "_containerName", "TMPro.TextMeshProUGUI", "TextMeshProUGUI", "ContainerNameAccessor"),
            ("TeammateBackpackInteractionPatch.cs", "EFT.UI.SimpleStashPanel", "_containerNamePanel", "UnityEngine.GameObject", "UnityEngine.GameObject", "ContainerNamePanelAccessor")
        };
        foreach (var item in propertyOnlyConsumers)
        {
            TypeDefinition nativeType = FindType(module, item.TypeName);
            PropertyDefinition? property = nativeType.Properties.SingleOrDefault(p => p.Name == item.Member);
            bool propertyShape = property != null && property.PropertyType.FullName == item.MetadataType && property.GetMethod != null && !property.GetMethod.IsStatic && property.GetMethod.Parameters.Count == 0;
            bool sameNameFieldAbsent = !nativeType.Fields.Any(f => f.Name == item.Member);
            string path = Path.Combine(sourceRoot, "client", "Patches", item.FileName);
            string consumerSource = File.ReadAllText(path);
            string typeShortName = item.TypeName.Contains(".") ? item.TypeName[(item.TypeName.LastIndexOf('.') + 1)..] : item.TypeName;
            string declaration = "NativeUiMemberAccessor.TryForProperty(typeof(" + typeShortName + "), \"" + item.Member + "\", typeof(" + item.CSharpType + "), false, false)";
            bool sourceUsesOptionalAccessor = consumerSource.Contains(declaration, StringComparison.Ordinal) && consumerSource.Contains(item.AccessorName + "?.GetValue(", StringComparison.Ordinal);
            bool oldFieldLookupAbsent = !consumerSource.Contains("AccessTools.Field(typeof(" + typeShortName + "), \"" + item.Member + "\")", StringComparison.Ordinal);
            bool passed = propertyShape && sameNameFieldAbsent && sourceUsesOptionalAccessor && oldFieldLookupAbsent;
            pass &= passed;
            checks.Add(new { member = item.TypeName + "." + item.Member, metadataType = property?.PropertyType.FullName, propertyShape, sameNameFieldAbsent, sourceUsesOptionalAccessor, oldFieldLookupAbsent, passed });
        }

        TypeDefinition tooltipType = FindType(module, "EFT.UI.SimpleTooltip");
        MethodDefinition? tooltipShow = tooltipType.Methods.SingleOrDefault(m => m.Name == "Show" && m.Parameters.Count == 5);
        bool tooltipShowShape = tooltipShow != null
            && tooltipShow.ReturnType.FullName == "Il2CppSystem.Threading.CancellationToken"
            && tooltipShow.Parameters[0].ParameterType.FullName == "System.String"
            && tooltipShow.Parameters[1].ParameterType.FullName == "Il2CppSystem.Nullable`1<UnityEngine.Vector2>"
            && tooltipShow.Parameters[2].ParameterType.FullName == "System.Single"
            && tooltipShow.Parameters[3].ParameterType.FullName == "Il2CppSystem.Nullable`1<System.Single>"
            && tooltipShow.Parameters[4].ParameterType.FullName == "System.Boolean"
            && tooltipShow.Parameters[4].IsOptional
            && (!tooltipShow.Parameters[4].HasConstant || Equals(tooltipShow.Parameters[4].Constant, false));
        string squadTooltipSource = File.ReadAllText(Path.Combine(sourceRoot, "client", "Components", "SquadControlMenuUi.cs"));
        string profileTooltipSource = File.ReadAllText(Path.Combine(sourceRoot, "client", "Patches", "OtherPlayerProfileScreenPatch.cs"));
        const string emptyWidthExpression = "((float?)null).ToNativeNullable()";
        bool squadTooltipUsesNativeNullable = squadTooltipSource.Contains("Show(tooltipText, ((Vector2?)tooltipOffset).ToNativeNullable(), 0f, " + emptyWidthExpression + ")", StringComparison.Ordinal);
        bool profileTooltipUsesNativeNullable = profileTooltipSource.Contains("Show(tooltipText, new Il2CppSystem.Nullable<Vector2>(tooltipOffset), 0f, " + emptyWidthExpression + ")", StringComparison.Ordinal);
        string tooltipFixturePath = Path.Combine(sourceRoot, "tests", "Spt5InteropContracts", "fixtures", "simple-tooltip-nullable-cases.json");
        using JsonDocument tooltipFixture = JsonDocument.Parse(File.ReadAllText(tooltipFixturePath));
        JsonElement tooltipCases = tooltipFixture.RootElement.GetProperty("cases");
        JsonElement tooltipProvenance = tooltipFixture.RootElement.GetProperty("provenance");
        bool bhRealFailureShape = tooltipProvenance.GetProperty("path").GetString() == @"D:\game\tarkov workshop\.omni\tmp\pitfireteam-runtime\interactive-baseline\funchook-shadow-20261015bh-exit-LogOutput.log"
            && tooltipProvenance.GetProperty("observedLine").GetInt32() == 84
            && tooltipProvenance.GetProperty("nativeFailureReproduced").GetBoolean();
        bool oldManagedNullRejected = bhRealFailureShape && tooltipCases.EnumerateArray().Any(c =>
            c.GetProperty("id").GetString() == "bh-real-managed-null-maxwidth"
            && c.GetProperty("maxWidthExpression").GetString() == "null"
            && !c.GetProperty("expectedSafe").GetBoolean()
            && c.GetProperty("observedFailure").GetProperty("exceptionType").GetString() == "System.NullReferenceException"
            && c.GetProperty("observedFailure").GetProperty("frames").EnumerateArray().Any(f => f.GetString()!.Contains("Il2CppObjectBaseToPtrNotNull", StringComparison.Ordinal)));
        bool explicitEmptyNullableAccepted = tooltipCases.EnumerateArray().Any(c =>
            c.GetProperty("id").GetString() == "explicit-empty-native-nullable"
            && c.GetProperty("maxWidthExpression").GetString() == emptyWidthExpression
            && c.GetProperty("expectedSafe").GetBoolean());
        bool tooltipBoundaryPassed = tooltipShowShape && squadTooltipUsesNativeNullable && profileTooltipUsesNativeNullable && oldManagedNullRejected && explicitEmptyNullableAccepted;
        pass &= tooltipBoundaryPassed;
        checks.Add(new { member = "SimpleTooltip.Show nullable argument boundary", metadataSignature = tooltipShow?.FullName, tooltipShowShape, squadTooltipUsesNativeNullable, profileTooltipUsesNativeNullable, oldManagedNullRejected, explicitEmptyNullableAccepted, tooltipFixturePath, passed = tooltipBoundaryPassed, nativeHoverRetestPending = true });
        bool optionalAccessorReturnsNull = File.ReadAllText(Path.Combine(sourceRoot, "client", "Compat", "NativeUiMemberAccessor.cs")).Contains("if (property == null || property.PropertyType != valueType) return null;", StringComparison.Ordinal);
        pass &= optionalAccessorReturnsNull;
        checks.Add(new { member = "NativeUiMemberAccessor.TryForProperty optional shape", optionalAccessorReturnsNull, passed = optionalAccessorReturnsNull });
        TypeDefinition backendControllerType = FindType(module, "EFT.InventoryLogic.BackEndInventoryController");
        bool backendControllerNativeBase = backendControllerType.BaseType?.FullName == "EFT.InventoryLogic.InventoryController";
        string equipmentSource = File.ReadAllText(Path.Combine(sourceRoot, "client", "Patches", "TeammateEquipmentBuildsScreenPatch.cs"));
        bool backendControllerUsesNativeCast = equipmentSource.Contains("Il2CppBoundary.CastNativeOrNull<EFT.InventoryLogic.BackEndInventoryController>(inventoryController)", StringComparison.Ordinal)
            && !equipmentSource.Contains("inventoryController as EFT.InventoryLogic.BackEndInventoryController", StringComparison.Ordinal);
        bool backendControllerFailureIncludesNativeIdentity = equipmentSource.Contains("OtherPlayerProfileScreenPatch.DescribeNativeClass(inventoryController)", StringComparison.Ordinal)
            && equipmentSource.Contains("inventoryController.Pointer.ToInt64()", StringComparison.Ordinal)
            && File.ReadAllText(Path.Combine(sourceRoot, "client", "Patches", "OtherPlayerProfileScreenPatch.cs")).Contains("internal static string DescribeNativeClass(", StringComparison.Ordinal);
        bool backendControllerBoundaryPassed = backendControllerNativeBase && backendControllerUsesNativeCast && backendControllerFailureIncludesNativeIdentity;
        pass &= backendControllerBoundaryPassed;
        checks.Add(new { member = "BackEndInventoryController proxy cast", backendControllerNativeBase, backendControllerUsesNativeCast, backendControllerFailureIncludesNativeIdentity, passed = backendControllerBoundaryPassed });
        PropertyDefinition? back = screen.Properties.SingleOrDefault(p => p.Name == "_backButton");
        PropertyDefinition? hideout = screen.Properties.SingleOrDefault(p => p.Name == "_hideoutButton");
        bool buttonTemplateShape = back?.PropertyType.FullName == "EFT.UI.DefaultUIButton"
            && hideout?.PropertyType.FullName == "EFT.UI.DefaultUiButtonNewStyle"
            && source.Contains("DefaultUIButton template = BackButtonField?.GetValue(screen) as DefaultUIButton;", StringComparison.Ordinal);
        pass &= buttonTemplateShape;
        checks.Add(new { member = "edit-loadout-button-template", backButtonType = back?.PropertyType.FullName, hideoutButtonType = hideout?.PropertyType.FullName, usesCompatibleTemplate = buttonTemplateShape, passed = buttonTemplateShape });
        TypeDefinition? current = screen;
        PropertyDefinition? uiProperty = null;
        string? uiDeclaringType = null;
        for (int i = 0; current != null && i < 12; i++)
        {
            uiProperty = current.Properties.SingleOrDefault(p => p.Name == "UI");
            if (uiProperty != null) { uiDeclaringType = current.FullName; break; }
            TypeReference? baseType = current.BaseType;
            string? baseName = baseType is GenericInstanceType generic ? generic.ElementType.FullName : baseType?.FullName;
            current = baseName == null ? null : module.Types.SingleOrDefault(t => t.FullName == baseName);
        }
        bool inheritedUiShape = uiProperty?.PropertyType.FullName == "EFT.UI.UIParent"
            && uiProperty.GetMethod != null && !uiProperty.GetMethod.IsStatic && uiProperty.GetMethod.Parameters.Count == 0;
        bool uiSourceUsesAccessor = source.Contains("NativeUiMemberAccessor.ForProperty(typeof(OtherPlayerProfileScreen), \"UI\", typeof(EFT.UI.UIParent), false, false)", StringComparison.Ordinal)
            && source.Contains("UiAccessor.GetValue(__instance)", StringComparison.Ordinal)
            && !source.Contains("AccessTools.Field(typeof(OtherPlayerProfileScreen), \"UI\")", StringComparison.Ordinal);
        bool inheritedUiNegativeCovered = !screen.Fields.Any(f => f.Name == "UI") && !screen.Properties.Any(p => p.Name == "UI") && inheritedUiShape;
        bool uiOk = inheritedUiShape && uiSourceUsesAccessor && inheritedUiNegativeCovered;
        pass &= uiOk;
        checks.Add(new { member = "UI", declaringType = uiDeclaringType, metadataType = uiProperty?.PropertyType.FullName, inheritedUiShape, directMemberAbsent = inheritedUiNegativeCovered, sourceUsesInheritedAccessor = uiSourceUsesAccessor, passed = uiOk });
        bool showGateRemoved = source.Contains("PlayerModelWindowAccessor.GetValue(__instance) as InventoryPlayerModelWithStatsWindow", StringComparison.Ordinal)
            && !source.Contains("AccessTools.Field(typeof(OtherPlayerProfileScreen), \"_playerModelWithStatsWindow\")", StringComparison.Ordinal)
            && source.Contains("PlayerModelWindowAccessor.GetValue(__instance)", StringComparison.Ordinal);
        pass &= showGateRemoved;
        checks.Add(new { member = "show-postfix-and-close-cleanup", accessorReadbackWired = showGateRemoved, passed = showGateRemoved });
        var nativeTransformCastCases = new (string FileName, string OldMarker, string NewMarker, int ExpectedCount)[]
        {
            ("TeammateEquipmentBuildsScreenPatch.cs", "RectTransform buttonRect = buttonTransform as RectTransform;", "RectTransform buttonRect = Il2CppBoundary.CastNativeOrNull<RectTransform>(buttonTransform);", 1),
            ("TeammateEquipmentBuildsScreenPatch.cs", "RectTransform parent = buttonTransform?.parent as RectTransform;", "RectTransform parent = Il2CppBoundary.CastNativeOrNull<RectTransform>(buttonTransform?.parent);", 1),
            ("TeammateEquipmentBuildsScreenPatch.cs", "if (closeButton.transform is RectTransform closeRect)", "Il2CppBoundary.CastNativeOrNull<RectTransform>(closeButton.transform) is RectTransform closeRect", 2),
            ("TeammateEquipmentBuildsScreenPatch.cs", "RectTransform rect = toggle.transform as RectTransform;", "RectTransform rect = Il2CppBoundary.CastNativeOrNull<RectTransform>(toggle.transform);", 1),
            ("TeammateEquipmentBuildsScreenPatch.cs", "if (purchasedItemsContainer?.parent is RectTransform contentRect)", "Il2CppBoundary.CastNativeOrNull<RectTransform>(purchasedItemsContainer?.parent) is RectTransform contentRect", 1),
            ("TeammateEquipmentBuildsScreenPatch.cs", "RectTransform rect = button.transform as RectTransform;", "RectTransform rect = Il2CppBoundary.CastNativeOrNull<RectTransform>(button.transform);", 1),
            ("GestureMenuPatch.cs", "if (quickCommandObject?.transform is RectTransform quickRect)", "Il2CppBoundary.CastNativeOrNull<RectTransform>(quickCommandObject?.transform) is RectTransform quickRect", 1),
            ("GestureMenuPatch.cs", "if (textField.transform.parent is RectTransform parentRect)", "Il2CppBoundary.CastNativeOrNull<RectTransform>(textField.transform.parent) is RectTransform parentRect", 1),
            ("ChatFriendsPanelPatch.cs", "RectTransform parent = (FriendsInputField?.GetValue(panel) as Component)", "Component inputComponent = pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<Component>(FriendsInputAccessor?.GetValue(panel) as Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase);", 1),
            ("ChatFriendsPanelPatch.cs", "?? panel.transform as RectTransform;", "?? pitTeam.Compat.Il2CppBoundary.CastNativeOrNull<RectTransform>(panel.transform);", 1)
        };
        foreach (var item in nativeTransformCastCases)
        {
            string castSource = File.ReadAllText(Path.Combine(patchesRoot, item.FileName));
            int replacementCount = castSource.Split(new[] { item.NewMarker }, StringSplitOptions.None).Length - 1;
            bool passed = replacementCount == item.ExpectedCount && !castSource.Contains(item.OldMarker, StringComparison.Ordinal);
            pass &= passed;
            checks.Add(new { member = item.FileName + " native Transform-to-RectTransform boundary", expectedReplacementCount = item.ExpectedCount, replacementCount, oldClrCastAbsent = !castSource.Contains(item.OldMarker, StringComparison.Ordinal), passed });
        }

        string profileOptionsPath = Path.Combine(patchesRoot, "OtherPlayerProfileScreenPatch.ProfileOptions.cs");
        string profileOptionsSource = File.ReadAllText(profileOptionsPath);
        string boundaryPath = Path.Combine(sourceRoot, "client", "Compat", "Il2CppBoundary.NativeObject.cs");
        string boundarySource = File.ReadAllText(boundaryPath);
        string skillsPath = Path.Combine(patchesRoot, "OtherPlayerProfileScreenPatch.Skills.cs");
        string skillsSource = File.ReadAllText(skillsPath);
        string childFixturePath = Path.Combine(sourceRoot, "tests", "Spt5InteropContracts", "fixtures", "native-transform-child-enumeration-cases.json");
        using JsonDocument childFixtureDocument = JsonDocument.Parse(File.ReadAllText(childFixturePath));
        JsonElement childFixture = childFixtureDocument.RootElement;
        JsonElement failureShape = childFixture.GetProperty("positiveFailureShape");
        bool actualFailureShape = failureShape.GetProperty("exceptionType").GetString() == "System.InvalidCastException"
            && failureShape.GetProperty("currentType").GetString() == "Il2CppSystem.Object"
            && failureShape.GetProperty("targetType").GetString() == "UnityEngine.Transform"
            && failureShape.GetProperty("sourceMethod").GetString() == "OtherPlayerProfileScreenPatch.ClearProfileRightSideContent";
        bool directLoopRemoved = !profileOptionsSource.Contains("foreach (Transform child in rightSide)", StringComparison.Ordinal);
        bool adapterLoopUsed = profileOptionsSource.Contains("foreach (Transform child in pitTeam.Compat.Il2CppBoundary.EnumerateNativeChildren(rightSide))", StringComparison.Ordinal);
        bool helperUsesNativeIndexing = boundarySource.Contains("int childCount = parent.childCount;", StringComparison.Ordinal)
            && boundarySource.Contains("yield return parent.GetChild(index);", StringComparison.Ordinal);
        bool nullGuardPreserved = profileOptionsSource.Contains("if (rightSide != null)", StringComparison.Ordinal);
        bool typedArrayNegativePreserved = skillsSource.Contains("foreach (Transform child in root.GetComponentsInChildren<Transform>(true))", StringComparison.Ordinal);
        bool childEnumerationPassed = actualFailureShape && directLoopRemoved && adapterLoopUsed && helperUsesNativeIndexing && nullGuardPreserved && typedArrayNegativePreserved;
        pass &= childEnumerationPassed;
        checks.Add(new { member = "OtherPlayerProfileScreenPatch.ClearProfileRightSideContent native child enumeration", actualFailureShape, directLoopRemoved, adapterLoopUsed, helperUsesNativeIndexing, nullGuardPreserved, typedArrayNegativePreserved, passed = childEnumerationPassed });
        TypeDefinition itemContextType = FindType(module, "EFT.InventoryLogic.ItemContext");
        TypeDefinition defaultItemContextType = FindType(module, "EFT.InventoryLogic.DefaultItemContext");
        TypeDefinition inventoryEquipmentType = FindType(module, "EFT.InventoryLogic.InventoryEquipment");
        MethodDefinition? abstractCreateChild = itemContextType.Methods.SingleOrDefault(m => m.Name == "CreateChild" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "EFT.InventoryLogic.Item");
        MethodDefinition? nativeCreateChild = defaultItemContextType.Methods.SingleOrDefault(m => m.Name == "CreateChild" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "EFT.InventoryLogic.Item");
        MethodDefinition? publicRootCtor = defaultItemContextType.Methods.SingleOrDefault(m => m.IsConstructor && !m.IsStatic && m.IsPublic && m.Parameters.Count == 2 && m.Parameters[0].ParameterType.FullName == "EFT.InventoryLogic.Item" && m.Parameters[1].ParameterType.FullName == "EFT.InventoryLogic.EItemViewType");
        bool itemContextNativeAbstract = itemContextType.Fields.Any(field => field.Name == "NativeMethodInfoPtr_CreateChild_Public_Abstract_Virtual_New_ItemContext_Item_0");
        bool defaultContextNativeCreateChild = defaultItemContextType.Fields.Any(field => field.Name == "NativeMethodInfoPtr_CreateChild_Public_Virtual_ItemContext_Item_0");
        bool defaultContextNativePublicCtor = defaultItemContextType.Fields.Any(field => field.Name == "NativeMethodInfoPtr__ctor_Public_Void_Item_EItemViewType_0");
        bool itemContextContract = itemContextNativeAbstract;
        bool defaultContextContract = defaultItemContextType.BaseType?.FullName == "EFT.InventoryLogic.ItemContext" && publicRootCtor != null && defaultContextNativePublicCtor && nativeCreateChild != null && nativeCreateChild.IsPublic && nativeCreateChild.IsVirtual && !nativeCreateChild.IsAbstract && defaultContextNativeCreateChild;
        var equipmentBaseChain = new List<string>();
        TypeDefinition? equipmentBase = inventoryEquipmentType;
        bool equipmentDerivesFromItem = false;
        for (int depth = 0; equipmentBase != null && depth < 8; depth++)
        {
            equipmentBaseChain.Add(equipmentBase.FullName);
            if (equipmentBase.BaseType?.FullName == "EFT.InventoryLogic.Item") { equipmentBaseChain.Add("EFT.InventoryLogic.Item"); equipmentDerivesFromItem = true; break; }
            string? nextBaseName = equipmentBase.BaseType?.FullName;
            equipmentBase = nextBaseName == null ? null : module.Types.SingleOrDefault(t => t.FullName == nextBaseName);
        }
        string contextSourcePath = Path.Combine(sourceRoot, "client", "Patches", "LoadoutEditorEquipmentRootContext.cs");
        string contextSource = File.ReadAllText(contextSourcePath);
        string loadoutUiSourcePath = Path.Combine(sourceRoot, "client", "Patches", "OtherPlayerProfileScreenPatch.LoadoutUi.cs");
        string loadoutUiSource = File.ReadAllText(loadoutUiSourcePath);
        string identitySource = File.ReadAllText(Path.Combine(sourceRoot, "client", "Patches", "OtherPlayerProfileScreenPatch.cs"));
        string contextBoundarySource = File.ReadAllText(Path.Combine(sourceRoot, "client", "Compat", "Il2CppBoundary.NativeObject.cs"));
        string fixturePath = Path.Combine(sourceRoot, "tests", "Spt5InteropContracts", "fixtures", "loadout-editor-item-context-cases.json");
        using JsonDocument contextFixtureDocument = JsonDocument.Parse(File.ReadAllText(fixturePath));
        JsonElement contextFixture = contextFixtureDocument.RootElement;
        bool failureShapePresent = contextFixture.GetProperty("provenance").GetProperty("failureShape").GetString()?.Contains("EntryPointNotFoundException", StringComparison.Ordinal) == true && contextFixture.GetProperty("provenance").GetProperty("failureReceipt").GetString()?.EndsWith("probe-be-loadout-editor-r74-17.json", StringComparison.Ordinal) == true;
        bool factoryWired = contextBoundarySource.Contains("CreateDefaultItemContext(", StringComparison.Ordinal) && contextBoundarySource.Contains("new EFT.InventoryLogic.DefaultItemContext(item, viewType)", StringComparison.Ordinal) && contextBoundarySource.Contains("SameNativeObject(item, context.Item)", StringComparison.Ordinal) && loadoutUiSource.Contains("Il2CppBoundary.CreateDefaultItemContext(equipmentView, EItemViewType.InventoryDuringMatching)", StringComparison.Ordinal);
        int diagnosticIndex = loadoutUiSource.IndexOf("LogLoadoutEditorContextBeforeShow(followerInventoryController, equipmentContext, equipmentView)", StringComparison.Ordinal);
        int showIndex = loadoutUiSource.IndexOf("panelRoot.Show(", diagnosticIndex, StringComparison.Ordinal);
        bool diagnosticsBeforeShow = diagnosticIndex >= 0 && showIndex > diagnosticIndex && loadoutUiSource.Contains("controllerNative=", StringComparison.Ordinal) && loadoutUiSource.Contains("contextNative=", StringComparison.Ordinal) && loadoutUiSource.Contains("equipmentNative=", StringComparison.Ordinal);
        bool oldManagedContextRemoved = !contextSource.Contains("class LoadoutEditorEquipmentRootContext", StringComparison.Ordinal) && !loadoutUiSource.Contains("new LoadoutEditorEquipmentRootContext", StringComparison.Ordinal);
        bool sourceIdentityByPointer = identitySource.Contains("SameNativeObject(current, LoadoutEditorEquipmentContext)", StringComparison.Ordinal) && identitySource.Contains("SameNativeObject(indexedItem, item)", StringComparison.Ordinal) && identitySource.Contains("SameNativeObject(item, root)", StringComparison.Ordinal) && identitySource.Contains("SameNativeObject(parent, root)", StringComparison.Ordinal) && identitySource.Contains("visitedNativePointers", StringComparison.Ordinal) && identitySource.Contains("depth < 64", StringComparison.Ordinal);
        bool otherNativeIdentityByPointer = loadoutUiSource.Contains("SameNativeObject(activeInventoryController.Inventory?.Stash, savedStash)", StringComparison.Ordinal) && loadoutUiSource.Contains("SameNativeObject(savedStash.Parent?.GetOwner() as Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase, activeInventoryController)", StringComparison.Ordinal) && loadoutUiSource.Contains("SameNativeObject(backendController.Profile as Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase, session.Profile as Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase)", StringComparison.Ordinal) && loadoutUiSource.Contains("SameNativeObject(ActiveProfileBackendInventoryController.Profile as Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase, activeProfile)", StringComparison.Ordinal);
        var negativeCases = contextFixture.GetProperty("negative");
        bool typedNegativeCases = negativeCases.GetArrayLength() == 4 && contextFixture.GetProperty("positive").GetProperty("rootType").GetString() == "EFT.InventoryLogic.DefaultItemContext" && negativeCases.EnumerateArray().All(c => !c.GetProperty("expected").GetBoolean());
        JsonElement samePointerCase = contextFixture.GetProperty("identityCases").GetProperty("samePointerDifferentWrappers");
        JsonElement mismatchedPointerCase = contextFixture.GetProperty("identityCases").GetProperty("mismatchedPointer");
        JsonElement zeroPointerCase = contextFixture.GetProperty("identityCases").GetProperty("nullPointer");
        bool pointerIdentityCases = samePointerCase.GetProperty("leftNativePointer").GetString() == samePointerCase.GetProperty("rightNativePointer").GetString() && samePointerCase.GetProperty("expected").GetBoolean() && mismatchedPointerCase.GetProperty("leftNativePointer").GetString() != mismatchedPointerCase.GetProperty("rightNativePointer").GetString() && !mismatchedPointerCase.GetProperty("expected").GetBoolean() && zeroPointerCase.GetProperty("leftNativePointer").GetString() == "0x0" && !zeroPointerCase.GetProperty("expected").GetBoolean() && contextBoundarySource.Contains("return pointer != System.IntPtr.Zero && pointer == b.Pointer;", StringComparison.Ordinal);
        bool loadoutContextPassed = itemContextContract && defaultContextContract && equipmentDerivesFromItem && failureShapePresent && factoryWired && diagnosticsBeforeShow && oldManagedContextRemoved && sourceIdentityByPointer && otherNativeIdentityByPointer && typedNegativeCases && pointerIdentityCases;
        pass &= loadoutContextPassed;
        checks.Add(new { member = "native loadout equipment ItemContext root", itemContextNativeAbstract = itemContextNativeAbstract, nativeAbstractCreateChildField = "NativeMethodInfoPtr_CreateChild_Public_Abstract_Virtual_New_ItemContext_Item_0", defaultContextBase = defaultItemContextType.BaseType?.FullName, publicItemViewTypeConstructor = publicRootCtor != null, nativePublicItemViewConstructorField = defaultContextNativePublicCtor, concreteVirtualCreateChild = nativeCreateChild != null && nativeCreateChild.IsVirtual && !nativeCreateChild.IsAbstract, nativeCreateChildField = defaultContextNativeCreateChild, equipmentBaseChain, equipmentDerivesFromItem, failureShapePresent, factoryWired, diagnosticsBeforeShow, oldManagedContextRemoved, sourceIdentityByPointer, otherNativeIdentityByPointer, typedNegativeCases, pointerIdentityCases, nativeAdoptionVerified = false, passed = loadoutContextPassed });
        string menuInteropRoot = Path.GetDirectoryName(assemblyPath)!;
        string menuCoreRoot = Path.GetFullPath(Path.Combine(menuInteropRoot, "..", "core"));
        using ModuleDefinition unityModule = ModuleDefinition.ReadModule(Path.Combine(menuInteropRoot, "UnityEngine.CoreModule.dll"));
        using ModuleDefinition il2CppModule = ModuleDefinition.ReadModule(Path.Combine(menuInteropRoot, "Il2Cppmscorlib.dll"));
        using ModuleDefinition runtimeModule = ModuleDefinition.ReadModule(Path.Combine(menuCoreRoot, "Il2CppInterop.Runtime.dll"));
        TypeDefinition rect = unityModule.GetType("UnityEngine.RectTransform")!; TypeDefinition transformType = unityModule.GetType("UnityEngine.Transform")!; TypeDefinition componentType = unityModule.GetType("UnityEngine.Component")!; TypeDefinition unityObjectType = unityModule.GetType("UnityEngine.Object")!; TypeDefinition il2CppObjectType = il2CppModule.GetType("Il2CppSystem.Object")!; TypeDefinition il2CppBaseType = runtimeModule.GetType("Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase")!;
        TypeDefinition[] equalityChain = { rect, transformType, componentType, unityObjectType, il2CppObjectType, il2CppBaseType };
        bool baseChain = rect.BaseType?.FullName == "UnityEngine.Transform" && transformType.BaseType?.FullName == "UnityEngine.Component" && componentType.BaseType?.FullName == "UnityEngine.Object" && unityObjectType.BaseType?.FullName == "Il2CppSystem.Object" && il2CppObjectType.BaseType?.FullName == "Il2CppInterop.Runtime.InteropTypes.Il2CppObjectBase" && il2CppBaseType.BaseType?.FullName == "System.Object";
        bool equatableRect = equalityChain.SelectMany(type => type.Interfaces).Any(iface => iface.InterfaceType is GenericInstanceType generic && generic.ElementType.FullName == "System.IEquatable`1" && generic.GenericArguments.Any(argument => argument.FullName == "UnityEngine.RectTransform"));
        bool clrEqualsOverride = equalityChain.SelectMany(type => type.Methods).Any(method => method.Name == "Equals" && method.Parameters.Count == 1 && method.Parameters[0].ParameterType.FullName == "System.Object" && method.IsVirtual && !method.IsNewSlot);
        MethodDefinition? unityEquals = unityObjectType.Methods.SingleOrDefault(method => method.Name == "Equals" && method.Parameters.Count == 1); MethodDefinition? il2CppEquals = il2CppObjectType.Methods.SingleOrDefault(method => method.Name == "Equals" && method.Parameters.Count == 1);
        bool separateIl2CppSlot = unityEquals?.Parameters[0].ParameterType.FullName == "Il2CppSystem.Object" && unityEquals.IsVirtual && !unityEquals.IsNewSlot && il2CppEquals?.Parameters[0].ParameterType.FullName == "Il2CppSystem.Object" && il2CppEquals.IsVirtual && il2CppEquals.IsNewSlot && !il2CppBaseType.Methods.Any(method => method.Name == "Equals");
        string squadSource = File.ReadAllText(Path.Combine(sourceRoot, "client", "Components", "SquadControlMenuUi.cs")); string assetsSource = File.ReadAllText(Path.Combine(sourceRoot, "client", "Components", "SquadControlMenuUi.Assets.cs")); string menuSource = File.ReadAllText(Path.Combine(sourceRoot, "client", "Patches", "MenuScreenSquadControlPatch.cs"));
        string driftFixturePath = Path.Combine(sourceRoot, "tests", "Spt5InteropContracts", "fixtures", "squad-menu-position-drift-cases.json"); using JsonDocument driftFixture = JsonDocument.Parse(File.ReadAllText(driftFixturePath)); JsonElement drift = driftFixture.RootElement;
        bool r85Shape = drift.GetProperty("provenance").GetProperty("receipt").GetString()?.EndsWith("probe-be-quit-exit-r85-03.json", StringComparison.Ordinal) == true && !drift.GetProperty("provenance").GetProperty("runtimePointerComparisonObserved").GetBoolean() && drift.GetProperty("observedAcrossRuns").GetProperty("exitButtonCurrentY").GetSingle() == -947.1299f && drift.GetProperty("observedAcrossRuns").GetProperty("mySquadCurrentY").GetSingle() == 192.03284f && drift.GetProperty("regressionScenario").GetProperty("sameNativeTransformMultipleClrWrappers").GetBoolean();
        bool pointerCache = squadSource.Contains("Dictionary<long, OriginalButtonPosition>", StringComparison.Ordinal) && assetsSource.Contains("TryGetValue(pointer", StringComparison.Ordinal) && assetsSource.Contains("originalButtonPositionParentPointer != parentPointer", StringComparison.Ordinal) && assetsSource.Contains("rect.Pointer.ToInt64() == pair.Key", StringComparison.Ordinal);
        bool repeatPath = menuSource.Contains("GetOrCreate(__instance).Initialize", StringComparison.Ordinal) && squadSource.Contains("CaptureOriginalButtonPositions(playerRect)", StringComparison.Ordinal) && squadSource.Contains("RestoreOriginalButtonPositions()", StringComparison.Ordinal) && assetsSource.Contains("sibling.anchoredPosition -= new Vector2(0f, verticalStep)", StringComparison.Ordinal);
        bool menuDriftPassed = baseChain && !equatableRect && !clrEqualsOverride && separateIl2CppSlot && r85Shape && pointerCache && repeatPath; pass &= menuDriftPassed;
        checks.Add(new { member = "SquadControlMenuUi native-pointer position cache", baseChain, equatableRectTransform = equatableRect, clrEqualsOverride, unityEqualsParameter = unityEquals?.Parameters[0].ParameterType.FullName, unityEqualsNewSlot = unityEquals?.IsNewSlot, il2CppEqualsNewSlot = il2CppEquals?.IsNewSlot, separateIl2CppSlot, r85Shape, pointerCache, repeatPath, passed = menuDriftPassed });
        string layoutFixturePath = Path.Combine(sourceRoot, "tests", "Spt5InteropContracts", "fixtures", "ui-layout-observation-cases.json"); using JsonDocument layoutDoc = JsonDocument.Parse(File.ReadAllText(layoutFixturePath)); JsonElement layoutCases = layoutDoc.RootElement.GetProperty("cases"); string[] layoutIds = { "fully-visible", "partly-offscreen", "fully-offscreen", "rectmask-intersection", "stencil-shape-unknown", "missing-native-rect" }; bool layoutFixtureShapes = layoutCases.GetArrayLength() == layoutIds.Length && layoutIds.All(id => layoutCases.EnumerateArray().Any(item => item.GetProperty("id").GetString() == id)) && !layoutDoc.RootElement.GetProperty("provenance").GetProperty("nativeGeometryCaptured").GetBoolean(); string uiProbeSource = File.ReadAllText(Path.Combine(sourceRoot, "client", "Compat", "Spt5RuntimeUiProbe.cs")); string qaProjection = File.ReadAllText(Path.Combine(sourceRoot, "tests", "Invoke-Spt5Qa.ps1")); string profileOptions = File.ReadAllText(Path.Combine(patchesRoot, "OtherPlayerProfileScreenPatch.ProfileOptions.cs")); string renameSource = File.ReadAllText(Path.Combine(patchesRoot, "OtherPlayerProfileScreenPatch.RenameUi.cs")); bool layoutProducer = uiProbeSource.Contains("CaptureLayoutObservation(Component component)", StringComparison.Ordinal) && uiProbeSource.Contains("private static Vector2[] GetWorldCornersOnScreen(RectTransform rect, Camera camera, out string error)", StringComparison.Ordinal) && uiProbeSource.Contains("using Il2CppInterop.Runtime.InteropTypes.Arrays;", StringComparison.Ordinal) && uiProbeSource.Contains("new Il2CppStructArray<Vector3>(4)", StringComparison.Ordinal) && uiProbeSource.Contains("rect.GetWorldCorners(native)", StringComparison.Ordinal) && uiProbeSource.Contains("WorldToScreenPoint(camera,native[0])", StringComparison.Ordinal) && uiProbeSource.Contains("WorldToScreenPoint(camera,native[1])", StringComparison.Ordinal) && uiProbeSource.Contains("WorldToScreenPoint(camera,native[2])", StringComparison.Ordinal) && uiProbeSource.Contains("WorldToScreenPoint(camera,native[3])", StringComparison.Ordinal) && uiProbeSource.Contains("GetWorldCorners returned zero-area screen geometry", StringComparison.Ordinal) && !uiProbeSource.Contains("new Vector3[4]", StringComparison.Ordinal) && uiProbeSource.Contains("CaptureMaskChain(rect,camera,bounds", StringComparison.Ordinal) && uiProbeSource.Contains("cornerError != \"\" ? \"unknown-degenerate-geometry\" : maskUnknown ? \"unknown-mask-shape\"", StringComparison.Ordinal) && uiProbeSource.Contains("maskIntersection", StringComparison.Ordinal); bool layoutProjection = qaProjection.Contains("layoutObservation=(Get-SafeField $c ", StringComparison.Ordinal); bool pointerCaches = source.Contains("Dictionary<long, Vector2> ProfileScaleOriginalPositions", StringComparison.Ordinal) && source.Contains("ProfileScaleOriginalPositions.TryGetValue(pointer", StringComparison.Ordinal) && source.Contains("Keys.Where(pointer => !currentPointers.Contains(pointer))", StringComparison.Ordinal) && source.Contains("Dictionary<long, HiddenRightSideRootState> HiddenRightSideRoots", StringComparison.Ordinal) && profileOptions.Contains("HiddenRightSideRoots.TryGetValue(rootPointer", StringComparison.Ordinal) && renameSource.Contains("KeyValuePair<long, HiddenRightSideRootState>", StringComparison.Ordinal); string gestureFixturePath = Path.Combine(sourceRoot, "tests", "Fixtures", "Spt5GesturePointerFixtures.json"); using JsonDocument gestureFixtureDoc = JsonDocument.Parse(File.ReadAllText(gestureFixturePath)); JsonElement cornerRegression = gestureFixtureDoc.RootElement.GetProperty("layoutObservationRegression"); bool zeroCornerNegative = cornerRegression.GetProperty("nativeType").GetString() == "UnityEngine.RectTransform" && cornerRegression.GetProperty("method").GetString() == "GetWorldCorners" && cornerRegression.GetProperty("expectedActualStatus").GetString() == "degenerate" && cornerRegression.GetProperty("actualCorners").EnumerateArray().All(corner => corner.EnumerateArray().All(value => value.GetDouble() == 0)) && cornerRegression.GetProperty("validExpectedBounds").GetProperty("width").GetDouble() > 0 && cornerRegression.GetProperty("validExpectedBounds").GetProperty("height").GetDouble() > 0; bool geometryCachePassed = layoutFixtureShapes && layoutProducer && layoutProjection && pointerCaches && zeroCornerNegative; pass &= geometryCachePassed; checks.Add(new { member = "OtherPlayerProfileScreen layout and native-pointer cache", layoutFixturePath, layoutFixtureShapes, layoutProducer, layoutProjection, pointerCaches, gestureFixturePath, zeroCornerNegative, passed = geometryCachePassed, nativeLayoutAdoptionVerified = false });
        string profileOptionsFixturePath = Path.Combine(sourceRoot, "tests", "Spt5InteropContracts", "fixtures", "profile-options-ui-cases.json");
        using JsonDocument profileOptionsFixture = JsonDocument.Parse(File.ReadAllText(profileOptionsFixturePath));
        JsonElement profileOptionsObserved = profileOptionsFixture.RootElement.GetProperty("observed");
        JsonElement profileOptionsExpected = profileOptionsFixture.RootElement.GetProperty("expected");
        string profileMainSource = File.ReadAllText(patchPath);
        string proficiencyUiSource = File.ReadAllText(Path.Combine(patchesRoot, "OtherPlayerProfileScreenPatch.ProficiencyUi.cs"));
        bool profileOptionsFailureShape = profileOptionsFixture.RootElement.GetProperty("provenance").GetProperty("nativeGeometryCaptured").GetBoolean() && profileOptionsObserved.GetProperty("lowerPanelOverlapPixels").GetInt32() == 67 && profileOptionsObserved.GetProperty("nativeClothingPanels").GetInt32() == 2 && profileOptionsObserved.GetProperty("clonedGenericSelectorPanels").GetInt32() == 1 && profileOptionsExpected.GetProperty("profileActionButtons").GetInt32() == 4 && profileOptionsExpected.GetProperty("genericLoadoutSelectorPanels").GetInt32() == 0;
        bool profileActionHostWired = profileMainSource.Contains("CreateProfileActionsContainer(__instance, rightSide)", StringComparison.Ordinal) && profileMainSource.Contains("DisplayLoadoutOptions(__instance, profile, inventoryController, session, actionRoot", StringComparison.Ordinal) && profileMainSource.Contains("CreateEditLoadoutButton(__instance, actionRoot", StringComparison.Ordinal) && profileMainSource.Contains("CreateProficiencyButton(__instance, actionRoot", StringComparison.Ordinal) && profileMainSource.Contains("CreateEditNameButton(__instance, actionRoot", StringComparison.Ordinal) && profileMainSource.Contains("rect.SetAsFirstSibling()", StringComparison.Ordinal) && profileMainSource.Contains("rect.sizeDelta = new Vector2(0f, 112f)", StringComparison.Ordinal);
        bool genericSelectorRemoved = !profileMainSource.Contains("GameObject.Instantiate(clothingPanel, parent, true)", StringComparison.Ordinal) && !profileOptionsSource.Contains("panel.Show(loadoutItemsIl2Cpp", StringComparison.Ordinal) && !profileOptionsSource.Contains("Il2CppSystem.Collections.Generic.List<dropDownItem> loadoutItemsIl2Cpp", StringComparison.Ordinal);
        bool ordinaryScrollableOptions = profileOptionsSource.Contains("DefaultUIButton AddProfileOptionsRow", StringComparison.Ordinal) && profileOptionsSource.Contains("AddProfileOptionsRow(buttonTemplate, contentRect", StringComparison.Ordinal) && profileOptionsSource.Contains("VerticalLayoutGroup", StringComparison.Ordinal) && profileOptionsSource.Contains("ContentSizeFitter.FitMode.PreferredSize", StringComparison.Ordinal) && profileOptionsSource.Contains("ScrollRect scrollRect", StringComparison.Ordinal) && profileOptionsSource.Contains("RequestHandler.PostJson(LoadoutRoute", StringComparison.Ordinal) && profileOptionsSource.Contains("RequestHandler.PostJson(TacticRoute", StringComparison.Ordinal);
        bool actionCallbacksPreserved = profileMainSource.Contains("TeammateEquipmentBuildsScreenFlow.Open", StringComparison.Ordinal) && profileMainSource.Contains("ShowLoadoutEditorOverlay", StringComparison.Ordinal) && proficiencyUiSource.Contains("CreateProfileActionButton", StringComparison.Ordinal) && proficiencyUiSource.Contains("ShowProficiencyOverlay", StringComparison.Ordinal) && renameSource.Contains("CreateProfileActionButton", StringComparison.Ordinal) && renameSource.Contains("ShowRenameOverlay", StringComparison.Ordinal);
        bool refreshAndCleanupUseNewHost = loadoutUiSource.Contains("private static void RefreshCurrentTeammateLoadoutSelector", StringComparison.Ordinal) && loadoutUiSource.Contains("ProfileActionsContainer", StringComparison.Ordinal) && !loadoutUiSource.Contains("GetComponent<InventoryClothingSelectionPanel>()", StringComparison.Ordinal) && profileMainSource.Contains("ClearProfileActionUi();", StringComparison.Ordinal) && profileMainSource.Contains("internal static void ClearProfileActionUi()", StringComparison.Ordinal);
        JsonElement observedLayoutFailures = profileOptionsFixture.RootElement.GetProperty("observedLayoutFailures");
        JsonElement expectedCompactLayout = profileOptionsFixture.RootElement.GetProperty("expectedCompactLayout");
        bool profileLayoutFailureShape = observedLayoutFailures.GetProperty("actionButtonRowGapPixels").GetDouble() > 300d && observedLayoutFailures.GetProperty("actionButtonSurface").GetString() == "caption-only" && observedLayoutFailures.GetProperty("optionsTitlePlacement").GetString() == "panel-middle" && observedLayoutFailures.GetProperty("optionsButtonSurface").GetString() == "not-visible" && expectedCompactLayout.GetProperty("actionMaxCenterGapPixels").GetDouble() < observedLayoutFailures.GetProperty("actionButtonRowGapPixels").GetDouble();
        bool compactActionGridWired = profileOptions.Contains("ConfigureCompactProfileActionButton(EditLoadoutButton, 0)", StringComparison.Ordinal) && profileOptions.Contains("ConfigureCompactProfileActionButton(LoadoutTacticButton, 1)", StringComparison.Ordinal) && proficiencyUiSource.Contains("ConfigureCompactProfileActionButton(button, 2)", StringComparison.Ordinal) && renameSource.Contains("ConfigureCompactProfileActionButton(button, slot)", StringComparison.Ordinal) && profileOptions.Contains("hostLayout.preferredHeight = ProfileActionsHeight", StringComparison.Ordinal) && profileOptions.Contains("? 162f : 112f", StringComparison.Ordinal) && profileOptions.Contains("hostLayout.flexibleHeight = 0f", StringComparison.Ordinal) && profileOptions.Contains("rect.sizeDelta = new Vector2(220f, 44f)", StringComparison.Ordinal) && profileOptions.Contains("row * 50f", StringComparison.Ordinal);
        bool optionsOverlayLayoutWired = profileOptions.Contains("CreateProfileOptionsTitle(panelObject.transform)", StringComparison.Ordinal) && profileOptions.Contains("label.verticalAlignment = VerticalAlignmentOptions.Middle", StringComparison.Ordinal) && profileOptions.Contains("scrollRectTransform.offsetMin = new Vector2(24f, 68f)", StringComparison.Ordinal) && profileOptions.Contains("closeRect.sizeDelta = new Vector2(160f, 42f)", StringComparison.Ordinal) && profileOptions.Contains("layout.minHeight = 44f", StringComparison.Ordinal);
        JsonElement r187TitleBoundary = profileOptionsFixture.RootElement.GetProperty("r187TitleGraphicBoundary");
        bool r187DoubleGraphicFailureShape = r187TitleBoundary.GetProperty("provenance").GetProperty("nativeFailureObserved").GetBoolean() && r187TitleBoundary.GetProperty("observed").GetProperty("sameGameObjectHasImageAndTmpGraphic").GetBoolean() && r187TitleBoundary.GetProperty("observed").GetProperty("error").GetString() == "NullReferenceException";
        JsonElement r187ExpectedTitle = r187TitleBoundary.GetProperty("expected");
        string titleBuilder = profileOptions.Substring(profileOptions.IndexOf("private static void CreateProfileOptionsTitle", StringComparison.Ordinal), profileOptions.IndexOf("private static void AddProfileOptionsHeader", StringComparison.Ordinal) - profileOptions.IndexOf("private static void CreateProfileOptionsTitle", StringComparison.Ordinal));
        bool splitTitleStructure = titleBuilder.Contains("pitFireTeam_ProfileOptionsTitleBackground", StringComparison.Ordinal) && titleBuilder.Contains("pitFireTeam_ProfileOptionsTitleLabel", StringComparison.Ordinal) && titleBuilder.Contains("backgroundObject.transform", StringComparison.Ordinal) && titleBuilder.Contains("CreateOverlayText(", StringComparison.Ordinal) && titleBuilder.Contains("Il2CppType.Of<Image>());", StringComparison.Ordinal) && !titleBuilder.Contains("Il2CppType.Of<Image>(),", StringComparison.Ordinal) && titleBuilder.Split("Vector2.zero").Length >= 3 && titleBuilder.Contains("backgroundRect.offsetMin = new Vector2(18f, -56f)", StringComparison.Ordinal) && titleBuilder.Contains("backgroundRect.offsetMax = new Vector2(-18f, -12f)", StringComparison.Ordinal);
        bool r187TitleGeometryPreserved = r187ExpectedTitle.GetProperty("headerHeightLogical").GetInt32() == 44 && r187ExpectedTitle.GetProperty("offsetMin")[0].GetInt32() == 18 && r187ExpectedTitle.GetProperty("offsetMin")[1].GetInt32() == -56 && r187ExpectedTitle.GetProperty("offsetMax")[0].GetInt32() == -18 && r187ExpectedTitle.GetProperty("offsetMax")[1].GetInt32() == -12;
        bool r187TitleRepairPassed = r187DoubleGraphicFailureShape && splitTitleStructure && r187TitleGeometryPreserved;
        bool profileButtonSurfaceWired = profileOptions.Contains("background.enabled = true", StringComparison.Ordinal) && profileOptions.Contains("ApplyProfileButtonSurface(button)", StringComparison.Ordinal) && profileOptions.Contains("ApplyProfileButtonSurface(closeButton)", StringComparison.Ordinal) && renameSource.Contains("ApplyProfileButtonSurface(button)", StringComparison.Ordinal);
        bool adjacentOverlayGraphicsRemainSplit = proficiencyUiSource.Contains("pitFireTeam_ProficiencyHeader", StringComparison.Ordinal) && proficiencyUiSource.Contains("CreateOverlayText(", StringComparison.Ordinal) && renameSource.Contains("pitFireTeam_RenameHeader", StringComparison.Ordinal) && renameSource.Contains("pitFireTeam_RenameTitle", StringComparison.Ordinal);
        bool profileCompactLayoutPassed = profileLayoutFailureShape && compactActionGridWired && optionsOverlayLayoutWired && r187TitleRepairPassed && adjacentOverlayGraphicsRemainSplit && profileButtonSurfaceWired;
        checks.Add(new { member = "R117/R118/R187 compact profile layout and title graphic boundary", profileOptionsFixturePath, profileLayoutFailureShape, compactActionGridWired, optionsOverlayLayoutWired, r187DoubleGraphicFailureShape, splitTitleStructure, r187TitleGeometryPreserved, adjacentOverlayGraphicsRemainSplit, profileButtonSurfaceWired, passed = profileCompactLayoutPassed, nativeAdoptionVerified = false });
        JsonElement missingBack = profileOptionsFixture.RootElement.GetProperty("r117MissingTeammateBackControl");
        bool missingBackObserved = missingBack.GetProperty("observedActiveBackControls").GetInt32() == 0 && missingBack.GetProperty("teammateBackOverridePrepared").GetBoolean() && missingBack.GetProperty("escapeReturnedToRoster").GetBoolean() && !missingBack.GetProperty("strangerProfileShouldGetBack").GetBoolean();
        bool teammateBackRouteWired = profileOptions.Contains("CreateTeammateProfileBackButton(screen, actionContainer)", StringComparison.Ordinal) && profileOptions.Contains("screen.TranslateCommand(EFT.InputSystem.ECommand.Escape);", StringComparison.Ordinal) && profileOptions.Contains("ActiveBackOverrideAction == null", StringComparison.Ordinal) && profileOptions.Contains("pitFireTeam_TeammateProfileBackButton", StringComparison.Ordinal);
        bool teammateBackPassed = missingBackObserved && teammateBackRouteWired && profileMainSource.Contains("ClearProfileActionUi();", StringComparison.Ordinal);
        checks.Add(new { member = "R117 teammate profile visible Back and native Escape route", missingBackObserved, teammateBackRouteWired, passed = teammateBackPassed, nativeAdoptionVerified = false });
        bool profileActionUiPassed = profileOptionsFailureShape && profileActionHostWired && genericSelectorRemoved && ordinaryScrollableOptions && actionCallbacksPreserved && refreshAndCleanupUseNewHost && profileCompactLayoutPassed && teammateBackPassed;
        pass &= profileActionUiPassed;
        checks.Add(new { member = "profile page actions and ordinary loadout/tactic selector", profileOptionsFixturePath, profileOptionsFailureShape, profileActionHostWired, genericSelectorRemoved, ordinaryScrollableOptions, actionCallbacksPreserved, refreshAndCleanupUseNewHost, passed = profileActionUiPassed, nativeAdoptionVerified = false });
        string healthFactoryPath = Path.Combine(sourceRoot, "client", "Compat", "Spt5FollowerHealthControllerFactory.cs");
        string healthFactory = File.ReadAllText(healthFactoryPath);
        string followerSkillsSource = File.ReadAllText(Path.Combine(patchesRoot, "OtherPlayerProfileScreenPatch.Skills.cs"));
        string healthProducer = File.ReadAllText(Path.Combine(sourceRoot, "server", "Services", "FriendlyTeammateService.cs"));
        string healthFixturePath = Path.Combine(sourceRoot, "tests", "Spt5InteropContracts", "fixtures", "follower-health-controller-cases.json");
        using JsonDocument healthFixture = JsonDocument.Parse(File.ReadAllText(healthFixturePath));
        JsonElement healthContract = healthFixture.RootElement.GetProperty("constructor");
        TypeDefinition offlineHealthType = FindType(module, healthContract.GetProperty("type").GetString() ?? string.Empty);
        string[] expectedHealthParameters = healthContract.GetProperty("parameters").EnumerateArray().Select(value => value.GetString() ?? string.Empty).ToArray();
        MethodDefinition? offlineHealthConstructor = offlineHealthType.Methods.SingleOrDefault(method => method.IsConstructor && method.Parameters.Select(parameter => parameter.ParameterType.FullName).SequenceEqual(expectedHealthParameters));
        JsonElement healthIdentityCases = healthFixture.RootElement.GetProperty("identityCases");
        bool healthIdentityCasesValid = healthIdentityCases.GetArrayLength() == 10 && healthIdentityCases.EnumerateArray().All(item =>
        {
            bool inventoryPresent = item.TryGetProperty("inventoryProfileId", out JsonElement inventoryId) && inventoryId.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(inventoryId.GetString());
            string nativeId = item.TryGetProperty("nativeProfileId", out JsonElement nativeIdElement) && nativeIdElement.ValueKind == JsonValueKind.String ? nativeIdElement.GetString() ?? string.Empty : string.Empty;
            string snapshotId = item.TryGetProperty("snapshotProfileId", out JsonElement snapshotIdElement) && snapshotIdElement.ValueKind == JsonValueKind.String ? snapshotIdElement.GetString() ?? string.Empty : string.Empty;
            string followerAid = item.GetProperty("followerAid").GetString() ?? string.Empty;
            int? snapshotAid = item.TryGetProperty("snapshotAid", out JsonElement aidElement) && aidElement.ValueKind == JsonValueKind.Number ? aidElement.GetInt32() : null;
            string resolvedId = string.Empty;
            string resolution = string.Empty;
            string inventoryAid = item.TryGetProperty("inventoryAid", out JsonElement inventoryAidElement) && inventoryAidElement.ValueKind == JsonValueKind.String ? inventoryAidElement.GetString() ?? string.Empty : string.Empty;
            bool resolved = inventoryPresent && pitTeam.Compat.Spt5FollowerHealthIdentityResolver.TryResolve(nativeId, snapshotId, followerAid, snapshotAid, inventoryId.GetString(), inventoryAid, out resolvedId, out resolution, out _);
            string expected = item.GetProperty("expected").GetString() ?? string.Empty;
            bool matches = expected == (resolved ? "accept" : "reject");
            if (resolved) matches &= item.GetProperty("resolution").GetString() == resolution && resolvedId == snapshotId;
            return matches;
        });
        JsonElement healthValueCases = healthFixture.RootElement.GetProperty("valueCases");
        bool healthValueCasesValid = healthValueCases.GetArrayLength() == 4 && healthValueCases.EnumerateArray().All(item =>
        {
            double? current = item.TryGetProperty("current", out JsonElement currentElement) && currentElement.ValueKind == JsonValueKind.Number ? currentElement.GetDouble() : null;
            double? minimum = item.TryGetProperty("minimum", out JsonElement minimumElement) && minimumElement.ValueKind == JsonValueKind.Number ? minimumElement.GetDouble() : null;
            double? maximum = item.TryGetProperty("maximum", out JsonElement maximumElement) && maximumElement.ValueKind == JsonValueKind.Number ? maximumElement.GetDouble() : null;
            bool resolved = pitTeam.Compat.FollowerHealthValuePolicy.TryResolve(current, minimum, maximum, out float currentValue, out float? minimumValue, out float maximumValue, out string missingFields);
            bool expected = item.GetProperty("expected").GetString() == "accept";
            bool matches = resolved == expected && minimumValue.HasValue == item.GetProperty("minimumProvided").GetBoolean() && missingFields == item.GetProperty("missingFields").GetString();
            if (resolved) matches &= currentValue == (float)current.Value && maximumValue == (float)maximum.Value && (!minimum.HasValue || minimumValue == (float)minimum.Value);
            return matches;
        });
        string healthIdentityResolver = File.ReadAllText(Path.Combine(sourceRoot, "client", "Compat", "Spt5FollowerHealthIdentityResolver.cs"));
        bool followerHealthContractPassed = offlineHealthConstructor != null && !healthContract.GetProperty("regeneration").GetBoolean() && healthIdentityCasesValid && healthValueCasesValid && healthFactory.Contains("FollowerHealthValuePolicy.TryResolve", StringComparison.Ordinal) && healthFactory.Contains("CastNativeOrNull<Profile>(inventory.Profile)", StringComparison.Ordinal) && healthFactory.Contains("inventoryOwner?.AccountId", StringComparison.Ordinal) && healthFactory.Contains("CastNativeOrNull<OtherPlayerProfile>(inventory.Profile)?.AccountId", StringComparison.Ordinal) && healthFactory.Contains("inventoryContextAid, out string id", StringComparison.Ordinal) && healthIdentityResolver.Contains("inventory-snapshot-aid-mismatch", StringComparison.Ordinal) && healthIdentityResolver.Contains("snapshot-aid-fallback", StringComparison.Ordinal) && healthIdentityResolver.Contains("explicit-profile-id-mismatch", StringComparison.Ordinal) && healthProducer.Contains("FollowerHealth = CreateFollowerHealthSnapshot(teammate)", StringComparison.Ordinal) && healthProducer.Contains("var health = teammate.Health", StringComparison.Ordinal) && healthFactory.Contains("inventory.Profile.ProfileId", StringComparison.Ordinal) && healthFactory.Contains("inventoryContextMatchesFollower", StringComparison.Ordinal) && healthFactory.Contains("followerProfileId=", StringComparison.Ordinal) && healthFactory.Contains("snapshotProfileId=", StringComparison.Ordinal) && healthFactory.Contains("snapshotAid=", StringComparison.Ordinal) && healthFactory.Contains("new OfflineHealthController(nativeHealth, inventory, follower.Skills, false)", StringComparison.Ordinal) && healthFactory.Contains("nativeController as IHealthController", StringComparison.Ordinal) && healthFactory.Contains("if (minimum.HasValue) value.Minimum = minimum.Value", StringComparison.Ordinal) && followerSkillsSource.Contains("skillsProfile.Health = followerHealth", StringComparison.Ordinal) && followerSkillsSource.Contains("TryCreate(profile, inventoryController, options?.FollowerHealth", StringComparison.Ordinal) && followerSkillsSource.Contains("healthDiagnostic", StringComparison.Ordinal) && followerSkillsSource.Contains("healthError + diagnosticSuffix", StringComparison.Ordinal) && !followerSkillsSource.Contains("MainPlayer.ActiveHealthController", StringComparison.Ordinal);
        pass &= followerHealthContractPassed;
        checks.Add(new { member = "follower-owned native skills health controller", healthFixturePath, offlineHealthConstructor = offlineHealthConstructor?.FullName, healthIdentityCasesValid, healthValueCasesValid, passed = followerHealthContractPassed, nativeRuntimeVerified = false });
        string clientPluginSource = File.ReadAllText(Path.Combine(sourceRoot, "client", "friendlyPlugin.cs"));
        bool languageOptionsClrDto = clientPluginSource.Contains("public class LanguageOptions", StringComparison.Ordinal) && !clientPluginSource.Contains("LanguageOptions : Il2CppSystem.Object", StringComparison.Ordinal) && clientPluginSource.Contains("JsonConvert.DeserializeObject<LanguageOptions>", StringComparison.Ordinal) && clientPluginSource.Contains("ApplyLanguageFallback", StringComparison.Ordinal);
        bool obsoleteManagedHealthAdapterRemoved = !source.Contains("ProfileSkillsHealthController", StringComparison.Ordinal) && !source.Contains("CreateProfileHealthController", StringComparison.Ordinal) && !followerSkillsSource.Contains("ProfileSkillsHealthController", StringComparison.Ordinal);
        bool managedInteropInheritancePassed = languageOptionsClrDto && obsoleteManagedHealthAdapterRemoved;
        pass &= managedInteropInheritancePassed;
        checks.Add(new { member = "CLR language DTO and retired managed native health adapter", languageOptionsClrDto, obsoleteManagedHealthAdapterRemoved, passed = managedInteropInheritancePassed });
        Console.WriteLine(JsonSerializer.Serialize(new { test = "other-player-profile-ui-member-boundary", passed = pass, assemblyPath, patchPath, checks, nativeUiAdoptionVerified = false }, new JsonSerializerOptions { WriteIndented = true }));
        return pass ? 0 : 1;
    }
    private static TypeDefinition FindType(ModuleDefinition module, string name)
        => module.Types.SingleOrDefault(t => t.FullName == name) ?? throw new InvalidOperationException("Missing metadata type " + name);
}
