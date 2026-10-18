using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using EFT.UI;
using EFT.UI.Matchmaker;
using EFT.UI.DragAndDrop;
using EFT.UI.Gestures;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace pitTeam.Compat
{
    // Only native controls observed in this process may be activated.
    internal static class Spt5RuntimeUiProbe
    {
        private sealed class Control
        {
            public string path { get; set; } = "";
            public string type { get; set; } = "";
            public int instanceId { get; set; }
            public bool active { get; set; }
            public bool interactable { get; set; }
            public bool hasEvent { get; set; }
            public string eventName { get; set; } = "";
            public int? persistentListenerCount { get; set; }
            public string persistentListenerStatus { get; set; } = "not-applicable";
            public string persistentListenerError { get; set; } = "";
            public bool persistentListenersTruncated { get; set; }
            public object[] persistentListeners { get; set; } = Array.Empty<object>();
            public string text { get; set; } = "";
            public bool? selected { get; set; }
            public string nativeRole { get; set; } = "";
            public string nativeRoleStatus { get; set; } = "not-applicable";
            public string nativeRoleOwnerPath { get; set; } = "";
            public string nativeRoleError { get; set; } = "";
            public string nativeComponentType { get; set; } = "";
            public string nativeComponentTypeStatus { get; set; } = "unavailable";
            public string nativeComponentTypeError { get; set; } = "";
            public string nativeItemStatus { get; set; } = "not-applicable";
            public string nativeItemError { get; set; } = "";
            public string nativeItemType { get; set; } = "";
            public string nativeItemTypeStatus { get; set; } = "unavailable";
            public string nativeItemTypeError { get; set; } = "";
            public string nativeItemId { get; set; } = "";
            public string templateId { get; set; } = "";
            public object layoutObservation { get; set; }
        }

        private sealed class ItemViewObservation
        {
            public string path;
            public int instanceId;
            public string nativeComponentType;
            public string itemId;
            public string templateId;
            public DateTime observedAtUtc;
        }

        private static readonly object itemViewObservationLock = new object();
        private static Dictionary<string, ItemViewObservation> lastItemViewObservations = new Dictionary<string, ItemViewObservation>(StringComparer.Ordinal);

        public static object Inspect(bool initialized)
        {
            var controls = new List<Control>();
            var currentItemViewObservations = new Dictionary<string, ItemViewObservation>(StringComparer.Ordinal);
            Component observedControl = null;
            foreach (var b in UnityEngine.Object.FindObjectsOfType<DefaultUIButton>())
            {
                if (observedControl == null) observedControl = b;
                string typeStatus, typeError; var componentType = NativeComponentType(b, out typeStatus, out typeError);
                var row = Row(b, typeof(DefaultUIButton).FullName!, b.enabled, b.Interactable, b.OnClick != null, "OnClick", null);
                row.nativeComponentType = componentType; row.nativeComponentTypeStatus = typeStatus; row.nativeComponentTypeError = typeError;
                ObservePersistentListeners(row, b.OnClick);
                controls.Add(row);
            }
            foreach (var tab in UnityEngine.Object.FindObjectsOfType<global::Tab>())
            {
                if (observedControl == null) observedControl = tab;
                controls.Add(Row(tab, typeof(global::Tab).FullName!, tab.enabled, tab.Interactable,
                    true, "IPointerClickHandler.OnPointerClick/IPointerEnterHandler.OnPointerEnter", null));
            }
            foreach (var view in UnityEngine.Object.FindObjectsOfType<EFT.UI.Builds.EquipmentBuildListView>())
            {
                var build = view.BuildWrapper?.Build;
                controls.Add(Row(view, typeof(EFT.UI.Builds.EquipmentBuildListView).FullName!, view.enabled,
                    view.Interactable && build != null, true, "IPointerClickHandler.OnPointerClick",
                    view._selectedBackground != null && view._selectedBackground.activeSelf,
                    nativeRole: build?.Id.ToString() ?? "", nativeRoleStatus: build == null ? "unavailable" : "ok"));
            }
            foreach (var button in UnityEngine.Object.FindObjectsOfType<DefaultUiButtonNewStyle>())
            {
                controls.Add(Row(button, typeof(DefaultUiButtonNewStyle).FullName!, button.enabled, button.IsInteractive,
                    button.OnClick != null, "IBindableEvent.OnClick (observation only)", null));
            }
            foreach (var b in UnityEngine.Object.FindObjectsOfType<AnimatedToggle>())
            {
                if (observedControl == null) observedControl = b;
                controls.Add(Row(b, typeof(AnimatedToggle).FullName!, b.enabled, b.IsInteractable(),
                    true, "OnPointerDown/OnPointerClick", b.isOn));
            }
            foreach (var b in UnityEngine.Object.FindObjectsOfType<Toggle>())
            {
                if (observedControl == null) observedControl = b;
                DescribeFactoryTimeToggle(b, out var role, out var roleOwnerPath, out var roleStatus, out var roleError);
                controls.Add(Row(b, typeof(Toggle).FullName!, b.enabled, b.IsInteractable(),
                    true, "IPointerClickHandler.OnPointerClick", b.isOn, role, roleOwnerPath, roleStatus, roleError));
            }
            foreach (var b in UnityEngine.Object.FindObjectsOfType<Button>())
            {
                if (observedControl == null) observedControl = b;
                string typeStatus, typeError; var componentType = NativeComponentType(b, out typeStatus, out typeError);
                var row = Row(b, typeof(Button).FullName!, b.enabled, b.IsInteractable(), b.onClick != null, "onClick", null);
                row.nativeComponentType = componentType; row.nativeComponentTypeStatus = typeStatus; row.nativeComponentTypeError = typeError;
                ObservePersistentListeners(row, b.onClick);
                controls.Add(row);
            }
            foreach (var inputField in UnityEngine.Object.FindObjectsOfType<TMP_InputField>())
            {
                if (observedControl == null) observedControl = inputField;
                string typeStatus, typeError; var componentType = NativeComponentType(inputField, out typeStatus, out typeError);
                var row = Row(inputField, typeof(TMP_InputField).FullName!, inputField.enabled, inputField.interactable,
                    inputField.onValueChanged != null, "TMP_InputField.SetText(sendCallback)/onValueChanged", null);
                row.nativeComponentType = componentType; row.nativeComponentTypeStatus = typeStatus; row.nativeComponentTypeError = typeError;
                controls.Add(row);
            }
foreach (var group in UnityEngine.Object.FindObjectsOfType<GesturesAudioItem>())
            {
                if (observedControl == null) observedControl = group;
                controls.Add(Row(group, typeof(GesturesAudioItem).FullName!, group.enabled, group.enabled,
                    true, "IPointerEnterHandler.OnPointerEnter", null));
            }
            foreach (var phrase in UnityEngine.Object.FindObjectsOfType<GestureBaseItem>())
            {
                if (observedControl == null) observedControl = phrase;
                controls.Add(Row(phrase, typeof(GestureBaseItem).FullName!, phrase.enabled, phrase.enabled,
                    true, "IPointerEnterHandler.OnPointerEnter/IPointerClickHandler.OnPointerClick", null));
            }
            foreach (var itemView in UnityEngine.Object.FindObjectsOfType<ItemView>())
            {
                var control = ItemViewRow(itemView, out var observation);
                controls.Add(control);
                if (observation != null)
                    currentItemViewObservations[ObservationKey(observation.path, observation.instanceId)] = observation;
            }
            foreach (var scroll in UnityEngine.Object.FindObjectsOfType<ScrollRect>())
                controls.Add(Row(scroll, typeof(ScrollRect).FullName!, scroll.enabled, scroll.IsActive(), true, "IScrollHandler.OnScroll", null));
            lock (itemViewObservationLock) lastItemViewObservations = currentItemViewObservations;
            return new
            {
                initialized,
                factoryTimeObservation = CaptureFactoryTimeObservation(),
                runtimeRoots = new
                {
                    currentDirectory = Environment.CurrentDirectory,
                    aiDataAssociationMode = string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_PIT_BUSINESS_STATE"), "initialized", StringComparison.Ordinal) ? "post-construction" : "not-applied",
                    pitBusiness = new
                    {
                        mode = Environment.GetEnvironmentVariable("PIT_SPT5_PIT_BUSINESS_MODE") ?? "normal",
                        state = Environment.GetEnvironmentVariable("PIT_SPT5_PIT_BUSINESS_STATE") ?? "not-reported",
                        initialized = string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_PIT_BUSINESS_STATE"), "initialized", StringComparison.Ordinal),
                        skipped = string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_PIT_BUSINESS_STATE"), "skipped-observe-core", StringComparison.Ordinal) || string.Equals(Environment.GetEnvironmentVariable("PIT_SPT5_PIT_BUSINESS_STATE"), "skipped-observe-ctor", StringComparison.Ordinal)
                    },
                    bepInEx = new
                    {
                        root = BepInEx.Paths.BepInExRootPath,
                        config = BepInEx.Paths.ConfigPath,
                        plugin = BepInEx.Paths.PluginPath,
                        cache = BepInEx.Paths.CachePath
                    },
                    unity = new
                    {
                        dataPath = Application.dataPath,
                        persistentDataPath = Application.persistentDataPath,
                        temporaryCachePath = Application.temporaryCachePath
                    },
                    eftSettings = new
                    {
                        settingsFolderPath = EFT.Settings.SettingsManager.SettingsFolderPath,
                        oldSettingsFolderPath = EFT.Settings.SettingsManager.OldSettingsFolderPath,
                        redirectEvidenceJson = Environment.GetEnvironmentVariable("PIT_SPT5_SETTINGS_REDIRECT_EVIDENCE")
                    },
                    environment = new
                    {
                        appData = Environment.GetEnvironmentVariable("APPDATA"),
                        localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA"),
                        temp = Environment.GetEnvironmentVariable("TEMP"),
                        tmp = Environment.GetEnvironmentVariable("TMP")
                    },
                    sptRegistry = InspectSptRegistry(),
                    knownFolderIsolationVerified = false,
                    note = "Observed process and Unity paths do not verify Windows Known Folder isolation."
                },
                controls = controls.ToArray(),
                profileLayout = CaptureProfileLayout(),
                nativeProfileValues = Spt5RuntimeWorldProbe.CaptureProfileUiValues(),
                nativeBuildValues = Spt5RuntimeWorldProbe.CaptureBuildUiValues(),
                scenarioStamina = Spt5RuntimeWorldProbe.CaptureScenarioStamina(),
                nativeCastChecks = new { uiControlTransform = InspectUiControlCast(observedControl), cameraTransform = InspectCameraCast() },
                text = UnityEngine.Object.FindObjectsOfType<TMP_Text>()
                    .Select(t => new { path = PathOf(t.transform), text = t.text }).ToArray()
            };
        }

        private static object[] CaptureProfileLayout()
        {
            var rows = new List<object>();
            foreach (var screen in UnityEngine.Object.FindObjectsOfType<OtherPlayerProfileScreen>())
            {
                var seen = new HashSet<long>();
                void Observe(Transform transform)
                {
                    if (transform == null || rows.Count >= 256) return;
                    var rect = Il2CppBoundary.CastNativeOrNull<RectTransform>(transform);
                    if (rect == null || !seen.Add(rect.Pointer.ToInt64())) return;
                    rows.Add(new
                    {
                        path = PathOf(rect), active = rect.gameObject.activeInHierarchy,
                        anchorMin = new { x = rect.anchorMin.x, y = rect.anchorMin.y },
                        anchorMax = new { x = rect.anchorMax.x, y = rect.anchorMax.y },
                        pivot = new { x = rect.pivot.x, y = rect.pivot.y },
                        sizeDelta = new { x = rect.sizeDelta.x, y = rect.sizeDelta.y },
                        offsetMin = new { x = rect.offsetMin.x, y = rect.offsetMin.y },
                        offsetMax = new { x = rect.offsetMax.x, y = rect.offsetMax.y },
                        geometry = CaptureLayoutObservation(rect)
                    });
                }
                Observe(screen.transform);
                foreach (var rect in screen.GetComponentsInChildren<RectTransform>(true))
                {
                    int depth = 0;
                    for (Transform parent = rect; parent != null && parent.Pointer != screen.transform.Pointer; parent = parent.parent) depth++;
                    if (depth <= 3 || rect.name == "Options" || rect.name == "Lower Part"
                        || rect.name.Contains("Proficiency") || rect.name.Contains("Aggression")) Observe(rect);
                }
                foreach (var scroll in screen.GetComponentsInChildren<ScrollRect>(true))
                {
                    Observe(scroll.transform);
                    Observe(scroll.viewport);
                    Observe(scroll.content);
                }
            }
            return rows.ToArray();
        }

        private static object InspectUiControlCast(Component control)
        {
            if (control == null) return new { available = false, status = "no-observed-ui-control" };
            Transform source = null;
            try { source = control.transform; } catch (Exception ex) { return new { available = false, status = "transform-error", error = ex.Message }; }
            if (source == null) return new { available = false, status = "null-transform" };
            RectTransform nativeRect = null; string nativeStatus = "available", nativeError = null;
            try { nativeRect = Il2CppBoundary.CastNativeOrNull<RectTransform>(source); } catch (Exception ex) { nativeStatus = "error"; nativeError = ex.GetType().FullName + ": " + ex.Message; }
            RectTransform componentRect = null; string componentStatus = "available", componentError = null;
            try { componentRect = control.GetComponent<RectTransform>(); } catch (Exception ex) { componentStatus = "error"; componentError = ex.GetType().FullName + ": " + ex.Message; }
            string nativePointer = nativeRect == null ? null : "0x" + nativeRect.Pointer.ToInt64().ToString("X");
            string componentPointer = componentRect == null ? null : "0x" + componentRect.Pointer.ToInt64().ToString("X");
            bool? samePointer = nativeRect == null || componentRect == null ? (bool?)null : Il2CppBoundary.SameNativeObject(nativeRect, componentRect);
            return new { available = true, status = "observed", proxyType = source.GetType().FullName, clrIsRectTransform = source is RectTransform, nativeCastStatus = nativeStatus, nativeCastError = nativeError, nativeRectTransformPointer = nativePointer, componentLookupStatus = componentStatus, componentLookupError = componentError, componentRectTransformPointer = componentPointer, sameNativePointer = samePointer, parentChildren = InspectParentChildren(source) };
        }
        private static object InspectParentChildren(Transform source)
        {
            Transform parent;
            try { parent = source.parent; }
            catch (Exception ex) { return new { available = false, status = "parent-error", error = ex.GetType().FullName + ": " + ex.Message }; }
            if (parent == null) return new { available = false, status = "no-parent", sourceChildCount = (int?)null, enumerationCount = (int?)null, firstChildAvailable = false };
            int count;
            try { count = parent.childCount; }
            catch (Exception ex) { return new { available = false, status = "child-count-error", error = ex.GetType().FullName + ": " + ex.Message }; }
            if (count == 0) return new { available = false, status = "no-children", sourceChildCount = 0, enumerationCount = 0, firstChildAvailable = false };
            if (count > 256) return new { available = false, status = "child-count-over-limit", sourceChildCount = count, maximumChildren = 256 };
            int seen = 0; string first = null; string directFirst = null; bool ordered = true;
            try
            {
                foreach (Transform child in Il2CppBoundary.EnumerateNativeChildren(parent))
                {
                    int index = seen;
                    if (index >= count || index >= 256) return new { available = false, status = "enumeration-exceeded-source-count", sourceChildCount = count, enumerationCount = seen, maximumChildren = 256 };
                    Transform direct = parent.GetChild(index);
                    bool same = child != null && direct != null && Il2CppBoundary.SameNativeObject(child, direct);
                    ordered &= same;
                    if (index == 0) { first = child == null ? null : "0x" + child.Pointer.ToInt64().ToString("X"); directFirst = direct == null ? null : "0x" + direct.Pointer.ToInt64().ToString("X"); }
                    seen++;
                }
            }
            catch (Exception ex) { return new { available = false, status = "enumeration-error", error = ex.GetType().FullName + ": " + ex.Message, sourceChildCount = count, enumerationCount = seen, orderVerified = ordered, maximumChildren = 256 }; }
            return new { available = seen == count, status = seen == count ? "observed" : "enumeration-count-mismatch", sourceChildCount = count, enumerationCount = seen, firstChildAvailable = seen > 0, firstChildPointer = first, directFirstChildPointer = directFirst, firstChildMatchesDirect = seen == 0 ? (bool?)null : ordered, orderVerified = ordered, maximumChildren = 256 };
        }

        private static object InspectCameraCast()
        {
            Camera camera = null;
            try { camera = UnityEngine.Object.FindObjectOfType<Camera>(); } catch (Exception ex) { return new { available = false, status = "camera-search-error", error = ex.GetType().FullName + ": " + ex.Message }; }
            if (camera == null) return new { available = false, status = "no-existing-camera" };
            Transform source = null;
            try { source = camera.transform; } catch (Exception ex) { return new { available = false, status = "camera-transform-error", error = ex.GetType().FullName + ": " + ex.Message }; }
            if (source == null) return new { available = false, status = "null-camera-transform" };
            try
            {
                var rect = Il2CppBoundary.CastNativeOrNull<RectTransform>(source);
                return new { available = true, status = "observed", proxyType = source.GetType().FullName, clrIsRectTransform = source is RectTransform, nativeRectTransformPointer = rect == null ? null : "0x" + rect.Pointer.ToInt64().ToString("X"), expectedNull = rect == null };
            }
            catch (Exception ex) { return new { available = true, status = "native-cast-error", proxyType = source.GetType().FullName, clrIsRectTransform = source is RectTransform, expectedNull = (bool?)null, error = ex.GetType().FullName + ": " + ex.Message }; }
        }
        private static object InspectSptRegistry()
        {
            const string assemblyName = "SPTushonka.Custom";
            const string typeName = "SPTushonka.Custom.Patches.SaveRegistryLocallyPatches";
            const string directoryFieldName = "_sptRegistryPath";
            const string fileFieldName = "_registryFilePath";
            var envBase = Environment.GetEnvironmentVariable("PIT_SPT5_SPT_REGISTRY_BASE");
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name == assemblyName).ToArray();
                if (assemblies.Length != 1)
                {
                    var status = assemblies.Length == 0 ? "assembly-missing" : "assembly-ambiguous";
                    var message = "Expected exactly one already-loaded SPTushonka.Custom assembly.";
                    return new { status, assemblyName, assemblyCount = assemblies.Length, typeName, envBase, registryDirectory = (string)null, registryFile = (string)null, directoryFieldStatus = "not-read", fileFieldStatus = "not-read", directoryFieldError = message, fileFieldError = message, error = message };
                }

                var type = assemblies[0].GetType(typeName, false);
                if (type == null)
                {
                    const string message = "Exact registry patch type was not found in the loaded assembly.";
                    return new { status = "type-missing", assemblyName, assemblyCount = 1, typeName, envBase, registryDirectory = (string)null, registryFile = (string)null, directoryFieldStatus = "not-read", fileFieldStatus = "not-read", directoryFieldError = message, fileFieldError = message, error = message };
                }

                const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly;
                var declaredFields = type.GetFields(flags);
                var directoryCandidates = declaredFields.Where(field => field.Name == directoryFieldName).ToArray();
                var fileCandidates = declaredFields.Where(field => field.Name == fileFieldName).ToArray();
                var directoryField = directoryCandidates.Length == 1 ? directoryCandidates[0] : null;
                var fileField = fileCandidates.Length == 1 ? fileCandidates[0] : null;
                string directoryStatus = directoryCandidates.Length == 0 ? "field-missing" : directoryCandidates.Length > 1 ? "field-ambiguous" : "not-read";
                string fileStatus = fileCandidates.Length == 0 ? "field-missing" : fileCandidates.Length > 1 ? "field-ambiguous" : "not-read";
                string directoryPath = null;
                string filePath = null;
                string directoryError = directoryStatus == "not-read" ? null : "Expected exactly one declared " + directoryFieldName + " field.";
                string fileError = fileStatus == "not-read" ? null : "Expected exactly one declared " + fileFieldName + " field.";
                string directoryType = directoryField == null ? null : directoryField.FieldType.FullName;
                string fileType = fileField == null ? null : fileField.FieldType.FullName;
                if (directoryField != null)
                {
                    if (!directoryField.IsStatic || directoryField.FieldType != typeof(string))
                    {
                        directoryStatus = "field-shape-invalid";
                        directoryError = directoryFieldName + " must be a static System.String.";
                    }
                    else
                    {
                        try { directoryPath = directoryField.GetValue(null) as string; directoryStatus = directoryPath == null ? "value-null" : "ok"; if (directoryPath == null) directoryError = "Directory field value is null."; }
                        catch (Exception exception) { directoryStatus = "read-error"; directoryError = exception.GetType().FullName + ": " + exception.Message; }
                    }
                }
                if (fileField != null)
                {
                    if (!fileField.IsStatic || fileField.FieldType != typeof(string))
                    {
                        fileStatus = "field-shape-invalid";
                        fileError = fileFieldName + " must be a static System.String.";
                    }
                    else
                    {
                        try { filePath = fileField.GetValue(null) as string; fileStatus = filePath == null ? "value-null" : "ok"; if (filePath == null) fileError = "File field value is null."; }
                        catch (Exception exception) { fileStatus = "read-error"; fileError = exception.GetType().FullName + ": " + exception.Message; }
                    }
                }
                var success = directoryStatus == "ok" && fileStatus == "ok";
                return new { status = success ? "ok" : "field-check-failed", assemblyName, assemblyCount = 1, typeName, envBase, registryDirectory = directoryPath, registryFile = filePath, directoryFieldName, directoryFieldType = directoryType, directoryFieldStatus = directoryStatus, directoryFieldError = directoryError, fileFieldName, fileFieldType = fileType, fileFieldStatus = fileStatus, fileFieldError = fileError, error = success ? null : "Both distinct registry directory and file fields must be read as static strings." };
            }
            catch (Exception inspectionException)
            {
                var message = inspectionException.GetType().FullName + ": " + inspectionException.Message;
                return new { status = "inspect-error", assemblyName, assemblyCount = -1, typeName, envBase, registryDirectory = (string)null, registryFile = (string)null, directoryFieldStatus = "inspect-error", fileFieldStatus = "inspect-error", directoryFieldError = message, fileFieldError = message, error = message };
            }
        }

        private static object CaptureFactoryTimeObservation()
        {
            var observedToggles = new List<Toggle>();
            foreach (var toggle in UnityEngine.Object.FindObjectsOfType<Toggle>(true))
                if (toggle != null) observedToggles.Add(toggle);
            var panels = new List<object>();
            foreach (var panel in UnityEngine.Object.FindObjectsOfType<LocationConditionsPanelFactory>(true))
            {
                if (panel == null) { panels.Add(new { status = "native-null-panel" }); continue; }
                try
                {
                    var day = panel._amTimeToggle;
                    var night = panel._pmTimeToggle;
                    panels.Add(new { status = "observed", path = PathOf(panel.transform), instanceId = panel.GetInstanceID(), pointer = panel.Pointer.ToInt64(), activeInHierarchy = panel.gameObject.activeInHierarchy, enabled = panel.enabled, day = DescribeFactoryTimeToggleObservation(day, observedToggles), night = DescribeFactoryTimeToggleObservation(night, observedToggles) });
                }
                catch (Exception ex) { panels.Add(new { status = "inspect-error", error = ex.GetType().FullName + ": " + ex.Message }); }
            }
            var locations = new List<object>();
            foreach (var screen in UnityEngine.Object.FindObjectsOfType<MatchMakerSelectionLocationScreen>(true))
            {
                if (screen == null) { locations.Add(new { status = "native-null-screen" }); continue; }
                string? screenPath = null;
                string? screenPathError = null;
                try { screenPath = PathOf(screen.transform); }
                catch (Exception ex) { screenPathError = ex.GetType().FullName + ": " + ex.Message; }
                int? screenInstanceId = null;
                string? screenInstanceIdError = null;
                try { screenInstanceId = screen.GetInstanceID(); }
                catch (Exception ex) { screenInstanceIdError = ex.GetType().FullName + ": " + ex.Message; }
                long? screenPointer = null;
                string screenPointerStatus = "unavailable";
                string? screenPointerError = null;
                try { var pointer = screen.Pointer; screenPointer = pointer == IntPtr.Zero ? (long?)null : pointer.ToInt64(); screenPointerStatus = pointer == IntPtr.Zero ? "zero" : "ok"; }
                catch (Exception ex) { screenPointerError = ex.GetType().FullName + ": " + ex.Message; screenPointerStatus = "error"; }
                bool? screenActive = null;
                string? screenActiveError = null;
                try { screenActive = screen.gameObject.activeInHierarchy; }
                catch (Exception ex) { screenActiveError = ex.GetType().FullName + ": " + ex.Message; }
                var locationObservation = new Dictionary<string, object?> { ["status"] = "not-read" };
                JsonType.LocationSettings.Location? selected = null;
                try
                {
                    selected = screen.SelectedLocation;
                    locationObservation["status"] = selected == null ? "property-null" : "observed";
                }
                catch (Exception ex)
                {
                    locationObservation["status"] = "getter-error";
                    locationObservation["error"] = ex.GetType().FullName + ": " + ex.Message;
                }
                if (selected != null)
                {
                    try { var pointer = selected.Pointer; locationObservation["nativePointer"] = pointer == IntPtr.Zero ? null : pointer.ToInt64(); locationObservation["nativePointerStatus"] = pointer == IntPtr.Zero ? "zero" : "ok"; }
                    catch (Exception ex) { locationObservation["nativePointerStatus"] = "error"; locationObservation["nativePointerError"] = ex.GetType().FullName + ": " + ex.Message; }
                    try { locationObservation["id"] = selected.Id; locationObservation["idStatus"] = "ok"; }
                    catch (Exception ex) { locationObservation["idStatus"] = "error"; locationObservation["idError"] = ex.GetType().FullName + ": " + ex.Message; }
                    try { locationObservation["enabled"] = selected.Enabled; locationObservation["enabledStatus"] = "ok"; }
                    catch (Exception ex) { locationObservation["enabledStatus"] = "error"; locationObservation["enabledError"] = ex.GetType().FullName + ": " + ex.Message; }
                    try { locationObservation["locked"] = selected.Locked; locationObservation["lockedStatus"] = "ok"; }
                    catch (Exception ex) { locationObservation["lockedStatus"] = "error"; locationObservation["lockedError"] = ex.GetType().FullName + ": " + ex.Message; }
                    try { locationObservation["lockedByQuest"] = selected.LockedByQuest; locationObservation["lockedByQuestStatus"] = "ok"; }
                    catch (Exception ex) { locationObservation["lockedByQuestStatus"] = "error"; locationObservation["lockedByQuestError"] = ex.GetType().FullName + ": " + ex.Message; }
                    try { locationObservation["hiddenWhenLockedByQuest"] = selected.HiddenWhenLockedByQuest; locationObservation["hiddenWhenLockedByQuestStatus"] = "ok"; }
                    catch (Exception ex) { locationObservation["hiddenWhenLockedByQuestStatus"] = "error"; locationObservation["hiddenWhenLockedByQuestError"] = ex.GetType().FullName + ": " + ex.Message; }
                    try { locationObservation["requiredPlayerLevelMin"] = selected.RequiredPlayerLevelMin; locationObservation["requiredPlayerLevelMinStatus"] = "ok"; }
                    catch (Exception ex) { locationObservation["requiredPlayerLevelMinStatus"] = "error"; locationObservation["requiredPlayerLevelMinError"] = ex.GetType().FullName + ": " + ex.Message; }
                    try { locationObservation["requiredPlayerLevelMax"] = selected.RequiredPlayerLevelMax; locationObservation["requiredPlayerLevelMaxStatus"] = "ok"; }
                    catch (Exception ex) { locationObservation["requiredPlayerLevelMaxStatus"] = "error"; locationObservation["requiredPlayerLevelMaxError"] = ex.GetType().FullName + ": " + ex.Message; }
                    try
                    {
                        var access = selected.access;
                        locationObservation["accessPresent"] = access != null;
                        if (access == null)
                        {
                            locationObservation["accessStatus"] = "property-null";
                            locationObservation["menuPvePresent"] = null;
                            locationObservation["menuPveCount"] = null;
                        }
                        else
                        {
                            locationObservation["accessStatus"] = "ok";
                            try
                            {
                                var menuPve = access.menu_PVE;
                                locationObservation["menuPvePresent"] = menuPve != null;
                                locationObservation["menuPveCount"] = menuPve == null ? (int?)null : menuPve.Count;
                                locationObservation["menuPveStatus"] = menuPve == null ? "property-null" : "ok";
                            }
                            catch (Exception ex) { locationObservation["menuPveStatus"] = "error"; locationObservation["menuPveError"] = ex.GetType().FullName + ": " + ex.Message; }
                        }
                    }
                    catch (Exception ex) { locationObservation["accessStatus"] = "error"; locationObservation["accessError"] = ex.GetType().FullName + ": " + ex.Message; }
                }
                var conditionsObservation = new Dictionary<string, object?> { ["status"] = "not-read" };
                LocationConditionsPanel? conditions = null;
                try { conditions = screen._conditions; conditionsObservation["status"] = conditions == null ? "property-null" : "observed"; }
                catch (Exception ex) { conditionsObservation["status"] = "getter-error"; conditionsObservation["error"] = ex.GetType().FullName + ": " + ex.Message; }
                if (conditions != null)
                {
                    try { var pointer = conditions.Pointer; conditionsObservation["nativePointer"] = pointer == IntPtr.Zero ? null : pointer.ToInt64(); conditionsObservation["nativePointerStatus"] = pointer == IntPtr.Zero ? "zero" : "ok"; }
                    catch (Exception ex) { conditionsObservation["nativePointerStatus"] = "error"; conditionsObservation["nativePointerError"] = ex.GetType().FullName + ": " + ex.Message; }
                    try { conditionsObservation["managedProxyType"] = conditions.GetType().FullName; conditionsObservation["managedProxyTypeStatus"] = "ok"; }
                    catch (Exception ex) { conditionsObservation["managedProxyTypeStatus"] = "error"; conditionsObservation["managedProxyTypeError"] = ex.GetType().FullName + ": " + ex.Message; }
                    try { conditionsObservation["path"] = PathOf(conditions.transform); conditionsObservation["pathStatus"] = "ok"; }
                    catch (Exception ex) { conditionsObservation["pathStatus"] = "error"; conditionsObservation["pathError"] = ex.GetType().FullName + ": " + ex.Message; }
                    try { conditionsObservation["active"] = conditions.gameObject.activeInHierarchy; conditionsObservation["activeStatus"] = "ok"; }
                    catch (Exception ex) { conditionsObservation["activeStatus"] = "error"; conditionsObservation["activeError"] = ex.GetType().FullName + ": " + ex.Message; }
                }
                var infoObservation = new Dictionary<string, object?> { ["status"] = "not-read" };
                LocationInfoPanel? infoPanel = null;
                try { infoPanel = screen._infoPanel; infoObservation["status"] = infoPanel == null ? "property-null" : "observed"; }
                catch (Exception ex) { infoObservation["status"] = "getter-error"; infoObservation["error"] = ex.GetType().FullName + ": " + ex.Message; }
                if (infoPanel != null)
                {
                    try { var pointer = infoPanel.Pointer; infoObservation["nativePointer"] = pointer == IntPtr.Zero ? null : pointer.ToInt64(); infoObservation["nativePointerStatus"] = pointer == IntPtr.Zero ? "zero" : "ok"; }
                    catch (Exception ex) { infoObservation["nativePointerStatus"] = "error"; infoObservation["nativePointerError"] = ex.GetType().FullName + ": " + ex.Message; }
                    try { infoObservation["managedProxyType"] = infoPanel.GetType().FullName; infoObservation["managedProxyTypeStatus"] = "ok"; }
                    catch (Exception ex) { infoObservation["managedProxyTypeStatus"] = "error"; infoObservation["managedProxyTypeError"] = ex.GetType().FullName + ": " + ex.Message; }
                    try { infoObservation["path"] = PathOf(infoPanel.transform); infoObservation["pathStatus"] = "ok"; }
                    catch (Exception ex) { infoObservation["pathStatus"] = "error"; infoObservation["pathError"] = ex.GetType().FullName + ": " + ex.Message; }
                    try { infoObservation["active"] = infoPanel.gameObject.activeInHierarchy; infoObservation["activeStatus"] = "ok"; }
                    catch (Exception ex) { infoObservation["activeStatus"] = "error"; infoObservation["activeError"] = ex.GetType().FullName + ": " + ex.Message; }
                }
                var locationStatus = locationObservation["status"] as string ?? "not-read";
                var status = locationStatus == "property-null" ? "selected-location-null" : locationStatus == "observed" ? "ok" : locationStatus;
                var id = locationObservation.TryGetValue("id", out var locationIdValue) ? locationIdValue as string : null;
                if (status == "ok" && locationObservation.TryGetValue("nativePointerStatus", out var pointerStatus) && Equals(pointerStatus, "zero")) status = "selected-location-pointer-unavailable";
                if (status == "ok" && string.IsNullOrWhiteSpace(id)) status = "selected-location-id-empty";
                locations.Add(new { status, screenPath, screenPathError, screenInstanceId, screenInstanceIdError, screenPointer, screenPointerStatus, screenPointerError, screenActiveInHierarchy = screenActive, screenActiveError, selectedLocationId = id, selectedLocation = locationObservation, conditions = conditionsObservation, infoPanel = infoObservation });
            }
            return new { status = "observed", panelCount = panels.Count, panels, selectedLocationScreenCount = locations.Count, selectedLocations = locations };
        }

        private static object DescribeFactoryTimeToggleObservation(Toggle toggle, List<Toggle> observedToggles)
        {
            if (toggle == null) return new { status = "property-null" };
            try
            {
                var pointer = toggle.Pointer;
                if (pointer == IntPtr.Zero) return new { status = "native-pointer-unavailable", activeInHierarchy = toggle.gameObject.activeInHierarchy };
                var matches = observedToggles.Count(x => x != null && x.Pointer != IntPtr.Zero && x.Pointer == pointer);
                return new { status = matches == 1 ? "observed" : matches > 1 ? "ambiguous-native-pointer" : "not-in-toggle-observation", path = PathOf(toggle.transform), instanceId = toggle.GetInstanceID(), pointer = pointer.ToInt64(), activeInHierarchy = toggle.gameObject.activeInHierarchy, enabled = toggle.enabled, interactable = toggle.IsInteractable(), selected = toggle.isOn, nativeTogglePointerMatchCount = matches };
            }
            catch (Exception ex) { return new { status = "inspect-error", error = ex.GetType().FullName + ": " + ex.Message }; }
        }

        private static void DescribeFactoryTimeToggle(Toggle candidate, out string nativeRole, out string ownerPath, out string status, out string error)
        {
            nativeRole = ""; ownerPath = ""; status = "not-applicable"; error = "";
            if (candidate == null) return;
            try
            {
                var pointer = candidate.Pointer;
                if (pointer == IntPtr.Zero) { status = "native-pointer-unavailable"; return; }
                int matches = 0;
                foreach (var panel in UnityEngine.Object.FindObjectsOfType<LocationConditionsPanelFactory>())
                {
                    if (panel == null || panel.Pointer == IntPtr.Zero) continue;
                    var day = panel._amTimeToggle;
                    var night = panel._pmTimeToggle;
                    if (day != null && day.Pointer != IntPtr.Zero && day.Pointer == pointer)
                    { matches++; nativeRole = "FactoryDay"; ownerPath = PathOf(panel.transform); }
                    if (night != null && night.Pointer != IntPtr.Zero && night.Pointer == pointer)
                    { matches++; nativeRole = "FactoryNight"; ownerPath = PathOf(panel.transform); }
                }
                if (matches == 1) { status = "observed"; return; }
                nativeRole = ""; ownerPath = ""; status = matches > 1 ? "ambiguous" : "not-applicable";
            }
            catch (Exception ex)
            { nativeRole = ""; ownerPath = ""; status = "inspect-error"; error = ex.GetType().FullName + ": " + ex.Message; }
        }

        private static object CaptureLayoutObservation(Component component)
        {
            try
            {
                if (component == null) return new { status = "unavailable", error = "component-null" };
                var rect = Il2CppBoundary.CastNativeOrNull<RectTransform>(component.transform);
                if (rect == null || rect.Pointer == IntPtr.Zero) return new { status = "unavailable", error = "native-rect-transform-unavailable" };
                var canvas = component.GetComponentInParent<Canvas>(); var root = canvas == null ? null : canvas.rootCanvas;
                Camera camera = root == null || root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
                var corners = GetWorldCornersOnScreen(rect, camera, out var cornerError);
                float x0 = corners.Min(p => p.x), x1 = corners.Max(p => p.x), y0 = corners.Min(p => p.y), y1 = corners.Max(p => p.y);
                var bounds = Rect.MinMaxRect(x0,y0,x1,y1); var viewport = new Rect(0,0,Screen.width,Screen.height);
                Rect maskIntersection=bounds; bool maskUnknown=false; string maskError=""; object[] masks=Array.Empty<object>(); if(cornerError=="") masks=CaptureMaskChain(rect,camera,bounds,out maskIntersection,out maskUnknown,out maskError);
                var visible = IntersectRect(IntersectRect(bounds,viewport),maskIntersection);
                string relation = ClassifyRect(bounds,viewport);
                string visibility = cornerError != "" ? "unknown-degenerate-geometry" : maskUnknown ? "unknown-mask-shape" : maskError != "" ? "mask-observation-error" : visible.width <= 0 || visible.height <= 0 ? "outside-or-clipped" : visible.width < bounds.width || visible.height < bounds.height ? "partially-clipped" : relation;
                return new { status = cornerError != "" ? "degenerate" : maskError == "" ? "ok" : "partial-error", error = cornerError != "" ? cornerError : maskError, nativePointer = rect.Pointer.ToInt64(), path = PathOf(rect), rect = new { x=rect.rect.x,y=rect.rect.y,width=rect.rect.width,height=rect.rect.height }, anchoredPosition = new { x=rect.anchoredPosition.x,y=rect.anchoredPosition.y }, localScale = new { x=rect.localScale.x,y=rect.localScale.y,z=rect.localScale.z }, lossyScale = new { x=rect.lossyScale.x,y=rect.lossyScale.y,z=rect.lossyScale.z }, screen = new { width=Screen.width,height=Screen.height,renderWidth=Screen.currentResolution.width,renderHeight=Screen.currentResolution.height }, canvas = root == null ? null : new { pointer=root.Pointer.ToInt64(),renderMode=root.renderMode.ToString(),pixelRect=new { x=root.pixelRect.x,y=root.pixelRect.y,width=root.pixelRect.width,height=root.pixelRect.height },scaleFactor=root.scaleFactor,worldCamera=camera == null ? "overlay-or-none" : camera.name }, screenCorners = corners.Select(p => new { x=p.x,y=p.y }).ToArray(), screenBounds=new { x=bounds.x,y=bounds.y,width=bounds.width,height=bounds.height }, viewportRelation=relation, viewportIntersection=new { x=IntersectRect(bounds,viewport).x,y=IntersectRect(bounds,viewport).y,width=IntersectRect(bounds,viewport).width,height=IntersectRect(bounds,viewport).height }, maskIntersection=new { x=maskIntersection.x,y=maskIntersection.y,width=maskIntersection.width,height=maskIntersection.height }, visibleIntersection=new { x=visible.x,y=visible.y,width=visible.width,height=visible.height }, visibilityEstimate=visibility, maskChain=masks };
            }
            catch(Exception ex) { return new { status="error",error=ex.GetType().FullName+": "+ex.Message }; }
        }
        private static Vector2[] GetWorldCornersOnScreen(RectTransform rect, Camera camera, out string error)
        {
            error=""; var native=new Il2CppStructArray<Vector3>(4); rect.GetWorldCorners(native);
            var corners=new[]{RectTransformUtility.WorldToScreenPoint(camera,native[0]),RectTransformUtility.WorldToScreenPoint(camera,native[1]),RectTransformUtility.WorldToScreenPoint(camera,native[2]),RectTransformUtility.WorldToScreenPoint(camera,native[3])};
            if(corners.Any(p=>!IsFinite(p.x)||!IsFinite(p.y))){error="WorldToScreenPoint returned non-finite corner";return new Vector2[4];}
            var x0=corners.Min(p=>p.x);var x1=corners.Max(p=>p.x);var y0=corners.Min(p=>p.y);var y1=corners.Max(p=>p.y);
            if(x1<=x0||y1<=y0)error="GetWorldCorners returned zero-area screen geometry"; return corners;
        }
        private static bool IsFinite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);

        private static Rect IntersectRect(Rect a,Rect b) { float x0=Mathf.Max(a.xMin,b.xMin),y0=Mathf.Max(a.yMin,b.yMin),x1=Mathf.Min(a.xMax,b.xMax),y1=Mathf.Min(a.yMax,b.yMax); return Rect.MinMaxRect(x0,y0,Mathf.Max(x0,x1),Mathf.Max(y0,y1)); }
        private static string ClassifyRect(Rect r,Rect v) { if(r.xMax<=v.xMin||r.xMin>=v.xMax||r.yMax<=v.yMin||r.yMin>=v.yMax)return "outside-viewport"; return r.xMin>=v.xMin&&r.xMax<=v.xMax&&r.yMin>=v.yMin&&r.yMax<=v.yMax ? "fully-inside" : "partially-outside"; }
        private static object[] CaptureMaskChain(RectTransform target,Camera camera,Rect bounds,out Rect intersection,out bool unknown,out string error)
        {
            var rows=new List<object>(); intersection=bounds; unknown=false; error=""; Transform current=target; int depth=0;
            while(current!=null&&depth<32)
            {
                depth++; try { var clip=current.GetComponent<RectMask2D>(); var mask=current.GetComponent<Mask>();
                    if(clip!=null) { var rt=Il2CppBoundary.CastNativeOrNull<RectTransform>(clip.transform); if(rt==null){error="RectMask2D rect transform unavailable";rows.Add(new {kind="RectMask2D",path=PathOf(current),status="error"});} else {var pts=GetWorldCornersOnScreen(rt,camera,out var maskGeometryError);if(maskGeometryError!=""){unknown=true;if(error=="")error=maskGeometryError;rows.Add(new{kind="RectMask2D",path=PathOf(current),status="degenerate",pointer=clip.Pointer.ToInt64(),error=maskGeometryError});}else{var r=Rect.MinMaxRect(pts.Min(p=>p.x),pts.Min(p=>p.y),pts.Max(p=>p.x),pts.Max(p=>p.y));bool active=clip.enabled&&clip.gameObject.activeInHierarchy;if(active)intersection=IntersectRect(intersection,r);rows.Add(new {kind="RectMask2D",path=PathOf(current),status=active?"active":"inactive",pointer=clip.Pointer.ToInt64(),screenBounds=new{x=r.x,y=r.y,width=r.width,height=r.height},padding=new{x=clip.padding.x,y=clip.padding.y,z=clip.padding.z,w=clip.padding.w},softness=new{x=clip.softness.x,y=clip.softness.y},intersectionAfter=new{x=intersection.x,y=intersection.y,width=intersection.width,height=intersection.height},geometry="axis-aligned-estimate"});}}}
                    if(mask!=null){bool active=mask.enabled&&mask.gameObject.activeInHierarchy;unknown|=active;rows.Add(new{kind="Mask",path=PathOf(current),status=active?"active":"inactive",pointer=mask.Pointer.ToInt64(),graphicType=mask.graphic==null?"":mask.graphic.GetType().FullName,shape="unknown"});}}
                catch(Exception ex){if(error=="")error=ex.GetType().FullName+": "+ex.Message;rows.Add(new{kind="ancestor",path="unavailable",status="error",error=ex.GetType().FullName+": "+ex.Message});} current=current.parent; }
            if(current!=null){unknown=true;rows.Add(new{kind="ancestor-limit",status="truncated",maxDepth=32});} return rows.ToArray();
        }
        private static Control Row(Component c, string type, bool enabled, bool interactable,
            bool hasEvent, string eventName, bool? selected, string nativeRole = "", string nativeRoleOwnerPath = "",
            string nativeRoleStatus = "not-applicable", string nativeRoleError = "")
        {
            return new Control
            {
                path = PathOf(c.transform), type = type, instanceId = c.GetInstanceID(),
                active = c.gameObject.activeInHierarchy && enabled, interactable = interactable,
                hasEvent = hasEvent, eventName = eventName, selected = selected,
                nativeRole = nativeRole, nativeRoleOwnerPath = nativeRoleOwnerPath, nativeRoleStatus = nativeRoleStatus, nativeRoleError = nativeRoleError,
                text = string.Join(" ", c.GetComponentsInChildren<TMP_Text>().Select(t => t.text)),
                layoutObservation = CaptureLayoutObservation(c)
            };
        }

        public static object SetInputText(string path, string type, int instanceId, string value, bool commit = false)
        {
            ManagedFramePump.RequireOwnerThread();
            if (type != typeof(TMP_InputField).FullName)
                throw new NotSupportedException("Text input is supported only for TMPro.TMP_InputField.");
            if (instanceId == 0) throw new ArgumentOutOfRangeException(nameof(instanceId), "SetInputText requires the exact nonzero instanceId from a current inspect result.");
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (value.Length > 128) throw new ArgumentOutOfRangeException(nameof(value), "Text input is limited to 128 UTF-16 characters.");
            if (value.Any(char.IsControl)) throw new ArgumentException("Text input cannot contain control characters.", nameof(value));
            var input = Single(UnityEngine.Object.FindObjectsOfType<TMP_InputField>(), path, instanceId);
            if (!input.enabled || !input.gameObject.activeInHierarchy || !input.interactable)
                throw new InvalidOperationException("Exact TMP_InputField must be enabled, active, and interactable.");
            string nativeStatus, nativeError; var nativeType = NativeComponentType(input, out nativeStatus, out nativeError);
            if (nativeStatus != "ok" || (nativeType != typeof(TMP_InputField).FullName && nativeType != "CustomTextMeshProInputField"))
                throw new InvalidOperationException("Exact TMP_InputField native class identity is unavailable or mismatched: " + nativeStatus + ": " + nativeType + " " + nativeError);
            if (input.characterLimit > 0 && value.Length > input.characterLimit)
                throw new ArgumentOutOfRangeException(nameof(value), "Text exceeds the observed TMP_InputField characterLimit.");
            int beforeLength = (input.text ?? "").Length;
            input.SetText(value, true);
            string readback = input.text ?? "";
            bool matched = string.Equals(readback, value, StringComparison.Ordinal);
            if (commit)
            {
                if (!matched) throw new InvalidOperationException("Native input did not accept the requested value; end-edit was not dispatched.");
                if (input.onEndEdit == null) throw new InvalidOperationException("Observed input has no native end-edit event.");
                input.onEndEdit.Invoke(readback);
            }
            return new
            {
                path = PathOf(input.transform), type, instanceId = input.GetInstanceID(), requestedInstanceId = instanceId,
                nativeComponentType = nativeType, nativeComponentTypeStatus = nativeStatus, nativeComponentTypeError = nativeError,
                uiEventDispatch = true, physical = false, nativeInput = false,
                setter = "TMP_InputField.SetText(sendCallback:true)", readbackMatched = matched,
                endEditDispatched = commit, committedText = commit ? input.text : null,
                valueLength = value.Length, beforeLength, readbackLength = readback.Length,
                characterLimit = input.characterLimit, layoutObservation = CaptureLayoutObservation(input)
            };
        }

        public static object Click(string path, string type, int instanceId, string mouseButton = "left")
        {
            ManagedFramePump.RequireOwnerThread();
            if (instanceId == 0) throw new ArgumentOutOfRangeException(nameof(instanceId), "Click requires the exact nonzero instanceId from a current inspect result.");
            string buttonName = (mouseButton ?? "left").Trim().ToLowerInvariant();
            if (buttonName != "left" && buttonName != "right") throw new ArgumentException("mouseButton must be left or right.", nameof(mouseButton));
            if (buttonName == "right" && type != typeof(Button).FullName && type != typeof(ItemView).FullName)
                throw new NotSupportedException("Right-button dispatch is supported only for UnityEngine.UI.Button and observed EFT.UI.DragAndDrop.ItemView targets.");
if (type == typeof(GestureBaseItem).FullName)
            {
                if (buttonName != "left") throw new NotSupportedException("Gesture phrase items support only their native left-click handler.");
                var phrase = Single(UnityEngine.Object.FindObjectsOfType<GestureBaseItem>(), path, instanceId);
                if (!phrase.enabled || EventSystem.current == null)
                    throw new InvalidOperationException("GestureBaseItem requires an active phrase item and live EventSystem.");
                var dispatch = DispatchPointerClick(phrase.gameObject,
                    Il2CppBoundary.CastNativeOrNull<RectTransform>(phrase.transform), PointerEventData.InputButton.Left, -1);
                return new { invoked = dispatch.dispatched, dispatched = dispatch.dispatched, mouseButton = "left", path = PathOf(phrase.transform), type, instanceId = phrase.GetInstanceID(), requestedInstanceId = instanceId, eventName = "ExecuteEvents.Execute<IPointerClickHandler>", screenPosition = dispatch.screenPosition };
            }
            if (type == typeof(ItemView).FullName)
            {
                var itemView = Single(UnityEngine.Object.FindObjectsOfType<ItemView>(), path, instanceId);
                if (!itemView.enabled || !itemView.IsInteractable)
                    throw new InvalidOperationException("ItemView is not active and interactable.");
                ValidateItemViewObservation(itemView, path, instanceId);
                if (buttonName == "right")
                    return DispatchRightClick(itemView.gameObject, itemView.RectTransform, path, type, instanceId);
                throw new NotSupportedException("Left dispatch for ItemView is not exposed by this probe.");
            }
            if (type == typeof(global::Tab).FullName)
            {
                if (buttonName != "left") throw new NotSupportedException("Tab supports only its native left-click handler.");
                var tab = Single(UnityEngine.Object.FindObjectsOfType<global::Tab>(), path, instanceId);
                if (!tab.enabled || !tab.gameObject.activeInHierarchy || !tab.Interactable || EventSystem.current == null)
                    throw new InvalidOperationException("Tab requires an active interactable control and live EventSystem.");
                var requestedInstanceId = instanceId;
                var observedPath = PathOf(tab.transform);
                var observedType = typeof(global::Tab).FullName!;
                var observedInstanceId = tab.GetInstanceID();
                var dispatch = DispatchPointerClick(tab.gameObject,
                    Il2CppBoundary.CastNativeOrNull<RectTransform>(tab.transform), PointerEventData.InputButton.Left, -1);
                return new { invoked = dispatch.dispatched, dispatched = dispatch.dispatched, mouseButton = "left",
                    path = observedPath, type = observedType, instanceId = observedInstanceId, requestedInstanceId,
                    eventName = "ExecuteEvents.Execute<IPointerClickHandler>", screenPosition = dispatch.screenPosition };
            }
            if (type == typeof(EFT.UI.Builds.EquipmentBuildListView).FullName)
            {
                var view = Single(UnityEngine.Object.FindObjectsOfType<EFT.UI.Builds.EquipmentBuildListView>(), path, instanceId);
                if (!view.enabled || !view.gameObject.activeInHierarchy || !view.Interactable
                    || view.BuildWrapper?.Build == null || EventSystem.current == null)
                    throw new InvalidOperationException("Build selection requires a populated active native row and live EventSystem.");
                string buildId = view.BuildWrapper.Build.Id.ToString();
                var dispatch = DispatchPointerClick(view.gameObject,
                    Il2CppBoundary.CastNativeOrNull<RectTransform>(view.transform), PointerEventData.InputButton.Left, -1);
                return new { invoked = dispatch.dispatched, dispatched = dispatch.dispatched, path, type,
                    instanceId, buildId, eventName = "ExecuteEvents.Execute<IPointerClickHandler>",
                    screenPosition = dispatch.screenPosition, requiresUiReadback = true };
            }
            if (type == typeof(DefaultUIButton).FullName)
            {
                var b = Single(UnityEngine.Object.FindObjectsOfType<DefaultUIButton>(), path, instanceId);
                if (!b.enabled || !b.Interactable || EventSystem.current == null)
                    throw new InvalidOperationException("DefaultUIButton requires an active interactable control and live EventSystem.");
                if (buttonName != "left") throw new NotSupportedException("DefaultUIButton supports only native left-click dispatch.");
                var requestedInstanceId = instanceId;
                var observedPath = PathOf(b.transform);
                var observedType = typeof(DefaultUIButton).FullName!;
                var observedInstanceId = b.GetInstanceID();
                // DefaultUIButton's ButtonFeedback handles sounds. Its backing TweenAnimatedButton
                // owns the actual click and can live on a child object in cloned templates.
                var inputButton = b._button;
                if (inputButton == null || !inputButton.enabled || !inputButton.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("DefaultUIButton has no active native backing button.");
                if (!Il2CppBoundary.SameNativeObject(inputButton.transform, b.transform) && !inputButton.transform.IsChildOf(b.transform))
                    throw new InvalidOperationException("DefaultUIButton backing button is outside the observed control.");
                string dispatchTargetPath = PathOf(inputButton.transform);
                int dispatchTargetInstanceId = inputButton.GetInstanceID();
                var dispatch = DispatchPointerSequence(inputButton.gameObject, inputButton.GetComponent<RectTransform>(), PointerEventData.InputButton.Left, -1);
                return new { invoked = dispatch.pointerClickHandlerDispatched, dispatched = dispatch.anyHandlerDispatched,
                    mouseButton = "left", path = observedPath, type = observedType, instanceId = observedInstanceId, requestedInstanceId,
                    dispatchTargetPath, dispatchTargetInstanceId,
                    eventName = "ExecuteEvents.PointerEnter/PointerDown/PointerUp/PointerClick",
                    pointerEnterHandlerDispatched = dispatch.pointerEnterHandlerDispatched,
                    pointerDownHandlerDispatched = dispatch.pointerDownHandlerDispatched,
                    pointerUpHandlerDispatched = dispatch.pointerUpHandlerDispatched,
                    pointerClickHandlerDispatched = dispatch.pointerClickHandlerDispatched, screenPosition = dispatch.screenPosition };
            }
            if (type == typeof(AnimatedToggle).FullName)
            {
                var b = Single(UnityEngine.Object.FindObjectsOfType<AnimatedToggle>(), path, instanceId);
                if (!b.enabled || !b.IsInteractable() || EventSystem.current == null)
                    throw new InvalidOperationException("AnimatedToggle requires an interactable control and live EventSystem.");
                var requestedInstanceId = instanceId;
                var observedPath = PathOf(b.transform);
                var observedType = typeof(AnimatedToggle).FullName!;
                var observedInstanceId = b.GetInstanceID();
                bool before = b.isOn;
                Vector2 togglePosition = ScreenCenterOf(b.gameObject, b.GetComponent<RectTransform>());
                var evt = new PointerEventData(EventSystem.current)
                {
                    button = PointerEventData.InputButton.Left,
                    position = togglePosition,
                    pressPosition = togglePosition,
                    clickCount = 1
                };
                b.OnPointerDown(evt);
                b.OnPointerClick(evt);
                return new { invoked = true, dispatched = true, mouseButton = buttonName, path = observedPath, type = observedType,
                    instanceId = observedInstanceId, requestedInstanceId, eventName = "OnPointerDown/OnPointerClick",
                    selectedBefore = before, selectedAfter = b.isOn, screenPosition = new { x = togglePosition.x, y = togglePosition.y } };
            }
            if (type == typeof(Toggle).FullName)
            {
                var toggle = Single(UnityEngine.Object.FindObjectsOfType<Toggle>(), path, instanceId);
                if (!toggle.enabled || !toggle.IsInteractable() || EventSystem.current == null)
                    throw new InvalidOperationException("Toggle requires an active interactable control and live EventSystem.");
                var requestedInstanceId = instanceId;
                var observedPath = PathOf(toggle.transform);
                var observedType = typeof(Toggle).FullName!;
                var observedInstanceId = toggle.GetInstanceID();
                bool before = toggle.isOn;
                var dispatch = DispatchPointerClick(toggle.gameObject, toggle.GetComponent<RectTransform>(),
                    PointerEventData.InputButton.Left, -1);
                return new { invoked = true, dispatched = dispatch.dispatched, mouseButton = "left", path = observedPath, type = observedType,
                    instanceId = observedInstanceId, requestedInstanceId, eventName = "ExecuteEvents.Execute<IPointerClickHandler>",
                    selectedBefore = before, selectedAfter = toggle.isOn, screenPosition = dispatch.screenPosition };
            }
            if (type == typeof(Button).FullName)
            {
                var b = Single(UnityEngine.Object.FindObjectsOfType<Button>(), path, instanceId);
                if (!b.enabled || !b.IsInteractable())
                    throw new InvalidOperationException("Unity Button is not interactable.");
                if (buttonName == "right") return DispatchRightClick(b, path, instanceId);
                var requestedInstanceId = instanceId;
                var observedPath = PathOf(b.transform);
                var observedType = typeof(Button).FullName!;
                var observedInstanceId = b.GetInstanceID();
                var dispatch = DispatchPointerSequence(b.gameObject, b.GetComponent<RectTransform>(), PointerEventData.InputButton.Left, -1);
                return new { invoked = dispatch.pointerClickHandlerDispatched, dispatched = dispatch.anyHandlerDispatched,
                    mouseButton = "left", path = observedPath, type = observedType, instanceId = observedInstanceId, requestedInstanceId,
                    eventName = "ExecuteEvents.PointerEnter/PointerDown/PointerUp/PointerClick",
                    pointerEnterHandlerDispatched = dispatch.pointerEnterHandlerDispatched,
                    pointerDownHandlerDispatched = dispatch.pointerDownHandlerDispatched,
                    pointerUpHandlerDispatched = dispatch.pointerUpHandlerDispatched,
                    pointerClickHandlerDispatched = dispatch.pointerClickHandlerDispatched, screenPosition = dispatch.screenPosition };
            }
            throw new ArgumentException("Unsupported native UI control type: " + type);
        }

        public static object QuickMoveEditorItem(string path, string type, int instanceId)
        {
            ManagedFramePump.RequireOwnerThread();
            if (type != typeof(ItemView).FullName || instanceId == 0) throw new ArgumentException("Editor quick move requires an observed native ItemView.");
            var view = Single(UnityEngine.Object.FindObjectsOfType<ItemView>(), path, instanceId);
            if (!view.enabled || !view.IsInteractable) throw new InvalidOperationException("ItemView is not interactable.");
            ValidateItemViewObservation(view, path, instanceId);
            return Spt5RuntimeWorldProbe.QuickMoveEditorItem(view);
        }

        public static object Scroll(string path, string type, int instanceId, float deltaY)
        {
            ManagedFramePump.RequireOwnerThread();
            if (type != typeof(ScrollRect).FullName || instanceId == 0 || !float.IsFinite(deltaY) || deltaY == 0f || Math.Abs(deltaY) > 5f)
                throw new ArgumentException("Scroll requires an observed ScrollRect and finite nonzero delta within +/-5.");
            var scroll = Single(UnityEngine.Object.FindObjectsOfType<ScrollRect>(), path, instanceId);
            if (!scroll.enabled || !scroll.IsActive() || EventSystem.current == null)
                throw new InvalidOperationException("Scroll target is not active.");
            var before = scroll.normalizedPosition;
            scroll.StopMovement();
            scroll.OnScroll(new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0f, deltaY) });
            var after = scroll.normalizedPosition;
            return new { passed = true, path, type, instanceId, deltaY, before = new { before.x, before.y }, after = new { after.x, after.y } };
        }

        public static object Hover(string path, string type, int instanceId)
        {
            ManagedFramePump.RequireOwnerThread();
            if (instanceId == 0) throw new ArgumentOutOfRangeException(nameof(instanceId), "Hover requires the exact nonzero instanceId from a current inspect result.");
            Component target;
            bool interactable;
            if (type == typeof(GesturesAudioItem).FullName)
            {
                var group = Single(UnityEngine.Object.FindObjectsOfType<GesturesAudioItem>(), path, instanceId);
                target = group; interactable = group.enabled;
            }
            else if (type == typeof(GestureBaseItem).FullName)
            {
                var phrase = Single(UnityEngine.Object.FindObjectsOfType<GestureBaseItem>(), path, instanceId);
                target = phrase; interactable = phrase.enabled;
            }
            else if (type == typeof(global::Tab).FullName)
            {
                var tab = Single(UnityEngine.Object.FindObjectsOfType<global::Tab>(), path, instanceId);
                target = tab; interactable = tab.Interactable;
            }
            else if (type == typeof(Button).FullName)
            {
                var button = Single(UnityEngine.Object.FindObjectsOfType<Button>(), path, instanceId);
                target = button; interactable = button.IsInteractable();
            }
            else if (type == typeof(DefaultUIButton).FullName)
            {
                var button = Single(UnityEngine.Object.FindObjectsOfType<DefaultUIButton>(), path, instanceId);
                target = button; interactable = button.Interactable;
            }
            else
                throw new NotSupportedException("Hover is supported only for observed native gesture components, UnityEngine.UI.Button, EFT.UI.DefaultUIButton, and global::Tab.");
            if (!(target is Behaviour behaviour) || !behaviour.enabled || !target.gameObject.activeInHierarchy || !interactable)
                throw new InvalidOperationException("Exact hover target must be enabled, active in hierarchy, and interactable.");
            if (EventSystem.current == null)
                throw new InvalidOperationException("Hover requires a live EventSystem.");
            var dispatch = DispatchPointerEnter(target);
            return new { invoked = dispatch.dispatched, dispatched = dispatch.dispatched, path = PathOf(target.transform),
                type, instanceId = target.GetInstanceID(), requestedInstanceId = instanceId,
                eventName = "ExecuteEvents.Execute<IPointerEnterHandler>", screenPosition = dispatch.screenPosition };
        }
        private static (bool dispatched, object screenPosition) DispatchPointerEnter(Component target)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) throw new InvalidOperationException("Pointer-enter dispatch requires a live EventSystem.");
            if (target == null) throw new InvalidOperationException("Gesture pointer-enter target is null.");
            var rect = Il2CppBoundary.CastNativeOrNull<RectTransform>(target.transform);
            if (rect == null) throw new InvalidOperationException("Gesture pointer-enter target has no native RectTransform.");
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("Gesture pointer-enter target has no parent Canvas.");
            var rootCanvas = canvas.rootCanvas;
            if (rootCanvas == null) throw new InvalidOperationException("Gesture pointer-enter Canvas has no rootCanvas.");
            Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && eventCamera == null)
                throw new InvalidOperationException("Gesture pointer-enter Canvas has no event camera; refusing an unmapped pointer position.");
            Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
            Vector2 screenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, worldCenter);
            var eventData = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                pointerId = -1,
                position = screenPosition,
                pressPosition = screenPosition
            };
            bool dispatched = ExecuteEvents.Execute<IPointerEnterHandler>(target.gameObject, eventData, ExecuteEvents.pointerEnterHandler);
            var screen = new { x = screenPosition.x, y = screenPosition.y };
            return (dispatched, (object)screen);
        }
        private static object DispatchRightClick(Button button, string path, int instanceId)
        {
            return DispatchRightClick(button.gameObject, button.GetComponent<RectTransform>(), path, typeof(Button).FullName, instanceId);
        }

        private static object DispatchRightClick(GameObject target, RectTransform rect, string path, string type, int instanceId)
        {
            var dispatch = DispatchPointerClick(target, rect, PointerEventData.InputButton.Right, -2);
            var canvas = target.GetComponentInParent<Canvas>();
            return new { dispatch.dispatched, mouseButton = "right", path, type, instanceId,
                eventName = "ExecuteEvents.Execute<IPointerClickHandler>", pointerButton = "Right", pointerId = -2,
                screenPosition = dispatch.screenPosition, canvasRenderMode = canvas.rootCanvas.renderMode.ToString() };
        }

        private static (bool pointerEnterHandlerDispatched, bool pointerDownHandlerDispatched, bool pointerUpHandlerDispatched, bool pointerClickHandlerDispatched, bool anyHandlerDispatched, object screenPosition) DispatchPointerSequence(GameObject target, RectTransform rect, PointerEventData.InputButton button, int pointerId)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) throw new InvalidOperationException("Pointer-sequence dispatch requires a live EventSystem.");
            if (target == null || rect == null) throw new InvalidOperationException("Pointer-sequence target requires a live GameObject and RectTransform.");
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas == null) throw new InvalidOperationException("Pointer-sequence target requires a parent root Canvas.");
            Camera camera = canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            if (canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && camera == null) throw new InvalidOperationException("Pointer-sequence Canvas has no event camera.");
            Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var data = new PointerEventData(eventSystem) { button = button, pointerId = pointerId, position = position, pressPosition = position, clickCount = 1, eligibleForClick = true, pointerEnter = target, pointerPress = target, rawPointerPress = target, pointerClick = target };
            bool enter = ExecuteEvents.Execute<IPointerEnterHandler>(target, data, ExecuteEvents.pointerEnterHandler);
            bool down = ExecuteEvents.Execute<IPointerDownHandler>(target, data, ExecuteEvents.pointerDownHandler);
            bool up = ExecuteEvents.Execute<IPointerUpHandler>(target, data, ExecuteEvents.pointerUpHandler);
            bool click = ExecuteEvents.Execute<IPointerClickHandler>(target, data, ExecuteEvents.pointerClickHandler);
            var screen = new { x = position.x, y = position.y };
            return (enter, down, up, click, enter || down || up || click, screen);
        }
        private static Vector2 ScreenCenterOf(GameObject target, RectTransform rect)
        {
            if (target == null || rect == null) throw new InvalidOperationException("Pointer-click target requires a live GameObject and RectTransform.");
            var canvas = target.GetComponentInParent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("Pointer-click target has no parent Canvas.");
            var rootCanvas = canvas.rootCanvas;
            if (rootCanvas == null) throw new InvalidOperationException("Pointer-click target Canvas has no rootCanvas.");
            Camera eventCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            if (rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay && eventCamera == null)
                throw new InvalidOperationException("Camera/world-space Canvas has no worldCamera; refusing an unmapped pointer position.");
            Vector3 worldCenter = rect.TransformPoint(rect.rect.center);
            return RectTransformUtility.WorldToScreenPoint(eventCamera, worldCenter);
        }
        private static (bool dispatched, object screenPosition) DispatchPointerClick(GameObject target, RectTransform rect,
            PointerEventData.InputButton button, int pointerId)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) throw new InvalidOperationException("Pointer-click dispatch requires a live EventSystem.");
            Vector2 screenPosition = ScreenCenterOf(target, rect);
            var eventData = new PointerEventData(eventSystem)
            {
                button = button,
                pointerId = pointerId,
                position = screenPosition,
                pressPosition = screenPosition,
                clickCount = 1
            };
            bool dispatched = ExecuteEvents.Execute<IPointerClickHandler>(target, eventData, ExecuteEvents.pointerClickHandler);
            var screen = new { x = screenPosition.x, y = screenPosition.y };
            return (dispatched, (object)screen);
        }

        private static Control ItemViewRow(ItemView itemView, out ItemViewObservation observation)
        {
            observation = null;
            var path = PathOf(itemView.transform);
            var instanceId = itemView.GetInstanceID();
            string componentType = "";
            string componentStatus = "unavailable", componentError = "";
            try
            {
                var nativeClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(itemView.Pointer);
                if (nativeClass != IntPtr.Zero)
                {
                    var name = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name_(nativeClass);
                    var ns = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_namespace_(nativeClass);
                    componentType = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                    componentStatus = string.IsNullOrWhiteSpace(componentType) ? "unavailable" : "ok";
                }
            }
            catch (Exception ex) { componentStatus = "error"; componentError = ex.GetType().FullName + ": " + ex.Message; }

            string nativeItemStatus = "unavailable", nativeItemError = "", itemId = "", templateId = "";
            string nativeItemType = "", nativeItemTypeStatus = "unavailable", nativeItemTypeError = "";
            try
            {
                var item = itemView.Item;
                if (item != null)
                {
                    itemId = item.Id ?? "";
                    templateId = item.StringTemplateId ?? "";
                    nativeItemStatus = !string.IsNullOrWhiteSpace(itemId) && !string.IsNullOrWhiteSpace(templateId) ? "ok" : "unavailable";
                    try
                    {
                        var nativeItemClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(item.Pointer);
                        if (nativeItemClass != IntPtr.Zero)
                        {
                            var nativeItemClassName = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name_(nativeItemClass);
                            var nativeItemNamespace = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_namespace_(nativeItemClass);
                            nativeItemType = string.IsNullOrEmpty(nativeItemNamespace) ? nativeItemClassName : nativeItemNamespace + "." + nativeItemClassName;
                            nativeItemTypeStatus = string.IsNullOrWhiteSpace(nativeItemType) ? "unavailable" : "ok";
                        }
                    }
                    catch (Exception ex) { nativeItemTypeStatus = "error"; nativeItemTypeError = ex.GetType().FullName + ": " + ex.Message; }
                }
            }
            catch (Exception ex) { nativeItemStatus = "error"; nativeItemError = ex.GetType().FullName + ": " + ex.Message; }

            bool active = itemView.gameObject.activeInHierarchy && itemView.enabled;
            bool interactable = false;
            try { interactable = itemView.IsInteractable; } catch { }
            if (active && interactable && instanceId != 0 && componentStatus == "ok" && nativeItemStatus == "ok")
                observation = new ItemViewObservation { path = path, instanceId = instanceId, nativeComponentType = componentType, itemId = itemId, templateId = templateId, observedAtUtc = DateTime.UtcNow };
            return new Control
            {
                path = path, type = typeof(ItemView).FullName!, instanceId = instanceId, active = active,
                interactable = interactable, hasEvent = true, eventName = "IPointerClickHandler", text = TextOf(itemView, 128),
                nativeComponentType = componentType, nativeComponentTypeStatus = componentStatus, nativeComponentTypeError = componentError,
                nativeItemStatus = nativeItemStatus, nativeItemError = nativeItemError, nativeItemType = nativeItemType,
                nativeItemTypeStatus = nativeItemTypeStatus, nativeItemTypeError = nativeItemTypeError, nativeItemId = itemId, templateId = templateId,
                layoutObservation = CaptureLayoutObservation(itemView)
            };
        }

        private static string ObservationKey(string path, int instanceId) => path + "\0" + instanceId.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static void ObservePersistentListeners(Control row, UnityEngine.Events.UnityEventBase click)
        {
            row.hasEvent = click != null;
            row.persistentListenerStatus = "ok";
            try
            {
                int count = click == null ? 0 : click.GetPersistentEventCount();
                row.persistentListenerCount = count;
                var listeners = new List<object>();
                for (int i = 0; click != null && i < Math.Min(count, 8); i++)
                {
                    var target = click.GetPersistentTarget(i);
                    listeners.Add(new { index = i, targetType = target == null ? "" : (target.GetType().FullName ?? target.GetType().Name), method = click.GetPersistentMethodName(i) ?? "" });
                }
                row.persistentListeners = listeners.ToArray();
                row.persistentListenersTruncated = count > listeners.Count;
            }
            catch (Exception ex)
            {
                row.persistentListenerStatus = "error";
                row.persistentListenerError = ex.GetType().FullName + ": " + ex.Message;
            }
        }

        private static string NativeComponentType(Component component, out string status, out string error)
        {
            status = "unavailable"; error = ""; string value = "";
            try
            {
                var klass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(component.Pointer);
                if (klass != IntPtr.Zero)
                {
                    var name = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name_(klass);
                    var ns = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_namespace_(klass);
                    value = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
                    status = string.IsNullOrWhiteSpace(value) ? "unavailable" : "ok";
                }
            }
            catch (Exception ex) { status = "error"; error = ex.GetType().FullName + ": " + ex.Message; }
            return value;
        }
        private static string TextOf(Component component, int maxLength)
        {
            if (component == null || maxLength <= 0) return "";
            string value = string.Join(" ", component.GetComponentsInChildren<TMP_Text>().Where(t => t != null).Select(t => t.text).Where(t => !string.IsNullOrWhiteSpace(t)));
            return value.Length > maxLength ? value.Substring(0, maxLength) : value;
        }
        private static void ValidateItemViewObservation(ItemView itemView, string path, int instanceId)
        {
            ItemViewObservation observed;
            lock (itemViewObservationLock)
                if (!lastItemViewObservations.TryGetValue(ObservationKey(path, instanceId), out observed))
                    throw new InvalidOperationException("ItemView was not present in the latest successful inspect result.");
            if (DateTime.UtcNow - observed.observedAtUtc > TimeSpan.FromSeconds(120))
                throw new InvalidOperationException("ItemView inspect observation is older than 120 seconds; inspect again.");
            var item = itemView.Item;
            var nativeClass = Il2CppInterop.Runtime.IL2CPP.il2cpp_object_get_class(itemView.Pointer);
            var name = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_name_(nativeClass);
            var ns = Il2CppInterop.Runtime.IL2CPP.il2cpp_class_get_namespace_(nativeClass);
            var componentType = string.IsNullOrEmpty(ns) ? name : ns + "." + name;
            if (item == null || item.Id != observed.itemId || item.StringTemplateId != observed.templateId || componentType != observed.nativeComponentType)
                throw new InvalidOperationException("ItemView native item or component identity changed after inspect; refusing dispatch.");
        }

        private static T Single<T>(IEnumerable<T> controls, string path, int instanceId) where T : Component
        {
            if (instanceId == 0) throw new ArgumentOutOfRangeException(nameof(instanceId), "Exact nonzero observed instanceId is required.");
            var matches = controls.Where(c => PathOf(c.transform) == path &&
                c.GetInstanceID() == instanceId && c.gameObject.activeInHierarchy).ToArray();
            if (matches.Length != 1)
                throw new InvalidOperationException("Expected one observed active native control; matches=" + matches.Length);
            return matches[0];
        }

        private static string PathOf(Transform t)
        {
            string path = t.name;
            for (var parent = t.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return path;
        }
    }
}
