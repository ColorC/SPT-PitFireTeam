using System.Collections.Generic;
using EFT;

namespace pitTeam.Compat
{
    internal static class Spt5NativeSupplyDataPriceSource
    {
        internal static SupplyData Create(Dictionary<string, float> marketPrices)
        {
            var nativeMarketPrices = new Il2CppSystem.Collections.Generic.Dictionary<string, double>();
            if (marketPrices != null)
            {
                foreach (KeyValuePair<string, float> pair in marketPrices)
                {
                    if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0f)
                    {
                        nativeMarketPrices[pair.Key] = pair.Value;
                    }
                }
            }

            var source = new SupplyData();
            source.MarketPrices = nativeMarketPrices;
            source.CurrencyCourses = new Il2CppSystem.Collections.Generic.Dictionary<string, double>();
            return source;
        }

        internal static void AddHandbookFallback(SupplyData source, string templateId, double handbookFallback)
        {
            if (source == null || string.IsNullOrWhiteSpace(templateId) || handbookFallback <= 0d)
            {
                return;
            }

            var prices = source.MarketPrices;
            if (prices == null)
            {
                return;
            }

            if (!prices.TryGetValue(templateId, out double currentPrice) || currentPrice <= 0d)
            {
                prices[templateId] = handbookFallback;
            }
        }
    }
}