using EFT;
using EFT.InventoryLogic;
using HarmonyLib;
using SPTushonka.Reflection.Patching;
using System;
using System.Reflection;

namespace pitTeam.Patches
{
    internal static class TeammateCorpseDogtagGuard
    {
        public static bool IsTeammateCorpseDogtag(Item item)
        {
            if (item == null)
            {
                return false;
            }

            try
            {
                Slot sourceSlot = item.CurrentAddress?.Container as Slot;
                if (sourceSlot == null ||
                    sourceSlot.ParentItem is not InventoryEquipment corpseEquipment ||
                    !Il2CppBoundary.SameNativeObject(sourceSlot.ContainedItem, item) ||
                    !string.Equals(sourceSlot.ID, EquipmentSlot.Dogtag.ToString(), StringComparison.Ordinal) ||
                    !TeammateCorpseIdentity.IsTeammateCorpseEquipment(corpseEquipment))
                {
                    return false;
                }

                // Only query component metadata after the native address identifies a
                // teammate corpse dogtag slot, so a broken collection bridge cannot
                // lock ordinary inventory items.
                return item.GetItemComponentOrNull<DogtagComponent>() != null;
            }
            catch (pitTeam.Compat.UnsupportedNativeItemComponentsInterfaceException ex)
            {
                pitFireTeam.Log?.LogError("[Loot] Native Item.Components IList cast failed for teammate corpse dogtag slot; blocking to preserve the lock.");
                pitFireTeam.Log?.LogError(ex);
                return true;
            }
            catch (Exception ex)
            {
                pitFireTeam.Log?.LogError("[Loot] Failed to check teammate corpse dogtag.");
                pitFireTeam.Log?.LogError(ex);
                return false;
            }
        }
    }

    internal sealed class TeammateCorpseDogtagMovePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(EFT.InventoryLogic.ItemManipulator),
                nameof(EFT.InventoryLogic.ItemManipulator.Move),
                new[] { typeof(Item), typeof(ItemAddress), typeof(ItemController), typeof(bool) });
        }

        [PatchPrefix]
        private static bool PatchPrefix(Item item, ItemAddress to, ItemController itemController, bool simulate,
            ref Diz.LanguageExtensions.OperationResult<EFT.InventoryLogic.MoveResult> __result, out int __state)
        {
            int probeCall = TeammateCorpseDogtagProbe.Begin(itemController, item);
            __state = probeCall == 0 ? -1 : probeCall;
            TeammateCorpseDogtagProbe.Trace(__state, "guard.before");
            bool locked = TeammateCorpseDogtagGuard.IsTeammateCorpseDogtag(item);
            TeammateCorpseDogtagProbe.Trace(__state, "guard.after", "locked=" + locked);
            if (!locked)
            {
                TeammateCorpseDogtagProbe.Trace(__state, "prefix.allow");
                return true;
            }
            __result = new EFT.InventoryLogic.ItemManipulator.ItemManuallyLockedError(item);
            TeammateCorpseDogtagProbe.Trace(__state, "prefix.block");
            return false;
        }

        [PatchPostfix]
        private static void PatchPostfix(Diz.LanguageExtensions.OperationResult<EFT.InventoryLogic.MoveResult> __result, int __state)
        {
            TeammateCorpseDogtagProbe.Trace(__state, "postfix.completed", "result=returned");
        }
        [HarmonyFinalizer]
        private static Exception PatchFinalizer(Exception __exception,
            ref Diz.LanguageExtensions.OperationResult<EFT.InventoryLogic.MoveResult> __result, int __state)
        {
            if (__state == 0 || !pitTeam.Compat.NativeOrphanParentMoveFailure.Matches(__exception))
            {
                return __exception;
            }

            // No Move error type represents this orphan-parent failure truthfully.
            // Return a non-null generic Error-backed OperationResult without calling it locked.
            __result = new Diz.LanguageExtensions.OperationResult<EFT.InventoryLogic.MoveResult>(
                new Diz.LanguageExtensions.Error());
            if (__state > 0)
            {
                TeammateCorpseDogtagProbe.Trace(__state, "native-parent-failure.returned", "result=generic-failure");
            }
            pitFireTeam.Log?.LogWarning("[Loot] ItemManipulator.Move rejected an item whose native parent is missing; returned a failure result.");
            return null;
        }

    }

    internal sealed class TeammateCorpseDogtagRemovePatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(
                typeof(EFT.InventoryLogic.ItemManipulator),
                nameof(EFT.InventoryLogic.ItemManipulator.Remove),
                new[] { typeof(Item), typeof(EFT.InventoryLogic.ItemController), typeof(bool) });
        }

        [PatchPrefix]
        private static bool PatchPrefix(Item item, bool simulate, ref Diz.LanguageExtensions.OperationResult<EFT.InventoryLogic.RemoveResult> __result)
        {
            if (!TeammateCorpseDogtagGuard.IsTeammateCorpseDogtag(item))
            {
                return true;
            }

            __result = new EFT.InventoryLogic.ItemManipulator.ItemManuallyLockedError(item);
            return false;
        }
    }
}
