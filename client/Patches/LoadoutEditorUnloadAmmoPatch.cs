using EFT.InventoryLogic;
using SPTushonka.Reflection.Patching;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using pitTeam.Compat;

namespace pitTeam.Patches
{
    internal sealed class LoadoutEditorUnloadAmmoPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(EFT.InventoryLogic.InventoryEquipmentExtension).GetMethod(nameof(EFT.InventoryLogic.InventoryEquipmentExtension.GetPrioritizedGridsForUnloadedObject));
        }

        [PatchPostfix]
        private static void PatchPostfix(InventoryEquipment equipment, ref Il2CppSystem.Collections.Generic.IEnumerable<EFT.InventoryLogic.Grid> __result)
        {
            if (OtherPlayerProfileScreenPatch.LoadoutEditorOverlayRoot == null
                || equipment == null
                || !ReferenceEquals(equipment, OtherPlayerProfileScreenPatch.LoadoutEditorProfile?.Inventory?.Equipment))
            {
                return;
            }

            List<EFT.InventoryLogic.Grid> prioritizedGrids = __result == null ? new List<EFT.InventoryLogic.Grid>() : __result.AsManaged().ToList();
            HashSet<string> seenGridIds = prioritizedGrids
                .Where(grid => grid != null)
                .Select(grid => grid.ID)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet();

            AppendUniqueGrids(prioritizedGrids, seenGridIds, GetSlotGrids(equipment, EquipmentSlot.Backpack));
            AppendUniqueGrids(prioritizedGrids, seenGridIds, OtherPlayerProfileScreenPatch.LoadoutEditorProfile?.Inventory?.Stash?.Grids);
            __result = prioritizedGrids.ToNativeList();
        }

        private static IEnumerable<EFT.InventoryLogic.Grid> GetSlotGrids(InventoryEquipment equipment, EquipmentSlot slot)
        {
            Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<EFT.InventoryLogic.Grid> nativeGrids = (equipment.GetSlot(slot)?.ContainedItem as CompoundItem)?.Grids;
            if (nativeGrids == null)
            {
                return Enumerable.Empty<EFT.InventoryLogic.Grid>();
            }

            List<EFT.InventoryLogic.Grid> managedGrids = new List<EFT.InventoryLogic.Grid>();
            for (int i = 0; i < nativeGrids.Length; i++)
            {
                EFT.InventoryLogic.Grid grid = nativeGrids[i];
                if (grid != null)
                {
                    managedGrids.Add(grid);
                }
            }

            return managedGrids;
        }

        private static void AppendUniqueGrids(
            ICollection<EFT.InventoryLogic.Grid> destination,
            ISet<string> seenGridIds,
            IEnumerable<EFT.InventoryLogic.Grid> grids)
        {
            if (grids == null)
            {
                return;
            }

            foreach (EFT.InventoryLogic.Grid grid in grids)
            {
                if (grid == null)
                {
                    continue;
                }

                string gridId = grid.ID;
                if (!string.IsNullOrWhiteSpace(gridId) && !seenGridIds.Add(gridId))
                {
                    continue;
                }

                destination.Add(grid);
            }
        }
    }
}
