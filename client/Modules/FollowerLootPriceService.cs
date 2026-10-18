using Comfort.Common;
using EFT;
using EFT.HandBook;
using EFT.InventoryLogic;
using JsonType;
using System;
using System.Collections.Generic;
using System.Linq;
using SPTushonka.Common.Http;
using UnityEngine;
using pitTeam.Compat;

namespace pitTeam.Modules
{
    internal static class FollowerLootPriceService
    {
        private const float MarketPricesRefreshIntervalSeconds = 300f;

        private static bool _marketPricesRequestInFlight;
        private static float _marketPricesUpdatedAt = -MarketPricesRefreshIntervalSeconds;
        private static Dictionary<string, float>? _marketPrices;

        public static void RequestMarketPricesIfNeeded()
        {
            try
            {
                if (_marketPricesRequestInFlight)
                {
                    return;
                }

                if (_marketPrices != null &&
                    Time.realtimeSinceStartup - _marketPricesUpdatedAt <= MarketPricesRefreshIntervalSeconds)
                {
                    return;
                }

                if (!Singleton<ClientApplication<EFT.IEftSession>>.Instantiated)
                {
                    return;
                }

                EFT.IEftSession session = Singleton<ClientApplication<EFT.IEftSession>>.Instance?.GetClientBackEndSession();
                if (session == null)
                {
                    return;
                }

                _marketPricesRequestInFlight = true;
                RequestMarketPrices(session, prices => HandleMarketPricesReceived(prices), error => { _marketPricesRequestInFlight = false; Logger.LogInfo($"[LootCommand] Failed to request market prices: {error.Message}"); });
            }
            catch (Exception ex)
            {
                _marketPricesRequestInFlight = false;
                Logger.LogInfo($"[LootCommand] Failed to request market prices: {ex.Message}");
            }
        }

        internal static void RequestMarketPrices(IEftSession session, Action<Il2CppSystem.Collections.Generic.Dictionary<string, float>> onSuccess, Action<Exception> onFailure)
        {
            RequestMarketPricesTask(session, onSuccess, onFailure);
        }
        private static async void RequestMarketPricesTask(IEftSession session, Action<Il2CppSystem.Collections.Generic.Dictionary<string, float>> onSuccess, Action<Exception> onFailure)
        {
            SendRequest request = null;
            Il2CppSystem.Object requestParams = null;
            ClientBackendSession endpoint = null;
            try
            {
                endpoint = Il2CppBoundary.CastNativeOrNull<ClientBackendSession>(session);
                if (endpoint == null || endpoint._backendUrls?.Main == null) throw new InvalidOperationException("Native backend session unavailable.");
                requestParams = SptJsonBoundary.ParseNativeJObject("{}");
                request = new SendRequest();
                if (request.Pointer == IntPtr.Zero || requestParams == null || requestParams.Pointer == IntPtr.Zero) throw new InvalidOperationException("Market price request pointer is zero.");
                request.Url = endpoint._backendUrls.Main + "/client/items/prices";
                request.Method = "POST"; request.Params = requestParams; request.UseCache = false;
                request.Retries = new Il2CppSystem.Nullable<byte>(SendRequest.DefaultRetries);
                var prices = await endpoint.Send<Il2CppSystem.Collections.Generic.Dictionary<string, float>>(request);
                if (prices == null) throw new InvalidOperationException("Market price response was null.");
                onSuccess?.Invoke(prices);
            }
            catch (Exception error) { onFailure?.Invoke(error); }
            finally { GC.KeepAlive(request); GC.KeepAlive(requestParams); GC.KeepAlive(endpoint); }
        }

        public static bool PassesPriceThreshold(Item item)
        {
            if (IsMoneyItem(item))
            {
                return true;
            }

            int min = Mathf.Max(0, pitFireTeam.lootMinimumPrice?.Value ?? 0);
            int max = Mathf.Max(0, pitFireTeam.lootMaximumPrice?.Value ?? 0);

            if (min <= 0 && max <= 0)
            {
                return true;
            }

            double price = CalculateItemTreeRoublePrice(item);
            return (min <= 0 || price >= min) &&
                   (max <= 0 || price <= max);
        }

        public static double CalculateItemTreeRoublePrice(Item item)
        {
            if (item == null)
            {
                return 0.0;
            }

            try
            {
                double total = 0.0;
                foreach (Item treeItem in CollectDeepItemTree(item))
                {
                    if (ShouldIgnorePriceItem(treeItem))
                    {
                        continue;
                    }

                    total += CalculateSingleItemRoublePrice(treeItem);
                }

                return Math.Max(0.0, Math.Floor(total));
            }
            catch (Exception ex)
            {
                Logger.LogInfo($"[LootCommand] Failed to price loot item '{item.TemplateId}': {ex.Message}");
                return 0.0;
            }
        }

        public static int GetBackpackAndPocketFreeArea(InventoryEquipment equipment)
        {
            if (equipment == null)
            {
                return 0;
            }

            return GetFreeArea(equipment.GetSlot(EquipmentSlot.Backpack)?.ContainedItem) +
                   GetFreeArea(equipment.GetSlot(EquipmentSlot.Pockets)?.ContainedItem);
        }

        private static void HandleMarketPricesReceived(Il2CppSystem.Collections.Generic.Dictionary<string, float> prices)
    {
        if (prices == null)
        {
            HandleMarketPricesReceived((Dictionary<string, float>)null);
            return;
        }

        var managed = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var pair in prices)
        {
            managed[pair.Key] = pair.Value;
        }

        HandleMarketPricesReceived(managed);
    }
    private static void HandleMarketPricesReceived(Dictionary<string, float> prices)
        {
            _marketPricesRequestInFlight = false;
            if (prices == null)
            {
                Logger.LogInfo("[LootCommand] Failed to load market prices; body/container loot will use handbook fallback prices.");
                return;
            }

            _marketPrices = new Dictionary<string, float>(prices, StringComparer.Ordinal);
            _marketPricesUpdatedAt = Time.realtimeSinceStartup;
        }
        private static IEnumerable<Item> CollectDeepItemTree(Item item)
        {
            if (item == null)
            {
                return Enumerable.Empty<Item>();
            }

            List<Item> items = new List<Item>();
            item.GetAllItemsNonAlloc(items, false, true);
            return items;
        }

        private static bool ShouldIgnorePriceItem(Item item)
        {
            return item == null ||
                   item is InventoryEquipment ||
                   item is EFT.InventoryLogic.Pockets ||
                   item is EFT.InventoryLogic.BuiltInInserts ||
                   string.IsNullOrWhiteSpace(item.TemplateId);
        }

        private static bool IsMoneyItem(Item item)
        {
            if (item == null)
            {
                return false;
            }

            if (item is EFT.InventoryLogic.Money)
            {
                return true;
            }

            try
            {
                return !string.IsNullOrWhiteSpace(item.TemplateId) &&
                       EFT.InventoryLogic.CurrencyUtil.IsCurrencyId(new MongoID(item.TemplateId));
            }
            catch
            {
                return false;
            }
        }

        private static double CalculateSingleItemRoublePrice(Item item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.TemplateId))
            {
                return 0.0;
            }

            double unitPrice = GetTemplateRoublePrice(item.TemplateId);
            int count = Mathf.Max(1, item.StackObjectsCount);
            return unitPrice * count;
        }

        private static double GetTemplateRoublePrice(string templateId)
        {
            if (string.IsNullOrWhiteSpace(templateId))
            {
                return 0.0;
            }

            if (string.Equals(templateId, EFT.InventoryLogic.CurrencyUtil.ROUBLE_ID.ToString(), StringComparison.Ordinal) ||
                string.Equals(templateId, EFT.InventoryLogic.CurrencyUtil.ROUBLE_STACK_ID.ToString(), StringComparison.Ordinal))
            {
                return 1.0;
            }

            if (_marketPrices != null &&
                _marketPrices.TryGetValue(templateId, out float marketPrice) &&
                marketPrice > 0f)
            {
                return marketPrice;
            }

            try
            {
                if (Singleton<EFT.HandBook.Handbook>.Instantiated)
                {
                    return Singleton<EFT.HandBook.Handbook>.Instance.GetBasePrice(new MongoID(templateId));
                }
            }
            catch
            {
                // fall through to zero
            }

            return 0.0;
        }

        private static int GetFreeArea(Item item)
        {
            if (item is not EFT.InventoryLogic.SearchableItem searchable || searchable.Grids == null)
            {
                return 0;
            }

            int freeArea = GetDirectGridFreeArea(searchable);
            foreach (Item child in searchable.GetAllItems())
            {
                if (child != null && child != searchable && child is EFT.InventoryLogic.SearchableItem nested)
                {
                    freeArea += GetDirectGridFreeArea(nested);
                }
            }

            return freeArea;
        }

        private static int GetDirectGridFreeArea(EFT.InventoryLogic.SearchableItem searchable)
        {
            if (searchable?.Grids == null)
            {
                return 0;
            }

            int freeArea = 0;
            foreach (EFT.InventoryLogic.Grid grid in searchable.Grids)
            {
                if (grid == null)
                {
                    continue;
                }

                int usedArea = 0;
                foreach (Item child in grid.Items.AsManaged())
                {
                    usedArea += GetItemArea(child);
                }

                freeArea += Mathf.Max(0, grid.GridHeight * grid.GridWidth - usedArea);
            }

            return freeArea;
        }

        private static int GetItemArea(Item item)
        {
            if (item == null)
            {
                return 0;
            }

            try
            {
                IntVec2 size = item.CalculateCellSize();
                return Mathf.Max(1, size.X) * Mathf.Max(1, size.Y);
            }
            catch
            {
                return 1;
            }
        }
    }
}