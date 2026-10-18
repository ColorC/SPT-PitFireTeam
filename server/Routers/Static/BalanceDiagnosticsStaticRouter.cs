using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers.Items;
using SPTarkov.Server.Core.Helpers.Profile;
using SPTarkov.Server.Core.Helpers.Traders;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Services.Ragfair;
using SPTarkov.Server.Core.Utils;

namespace pitTeam.Server.Routers.Static;

/// <summary>Fixed, read-only native balance inputs; never returns player/session data.</summary>
[Injectable(TypePriority = OnLoadOrder.Routers + 1)]
public class BalanceDiagnosticsStaticRouter(
    JsonUtil jsonUtil,
    HttpResponseUtil response,
    RagfairConfig ragfair,
    LocationConfig location,
    TemplateTable templates,
    TradersTable traders,
    HandbookHelper handbook,
    ItemHelper items,
    TraderHelper traderHelper,
    RagfairPriceService priceService
) : StaticRouter(
    jsonUtil,
    [
        new RouteAction<EmptyRequestData>(
            "/singleplayer/pitfireteam/diagnostics/balance",
            (url, info, sessionId, output, cancellationToken) => new ValueTask<string>(
                response.GetBody(Snapshot(ragfair, location, templates, traders, handbook, items, traderHelper, priceService))
            )
        )
    ]
)
{
    private static readonly string[] PriceTemplates =
    [
        "5c0a840b86f7742ffa4f2482",
        "59fb023c86f7746d0d4b423c",
        "64d4b23dc1b37504b41ac2b6"
    ];

    private static object Snapshot(
        RagfairConfig ragfair,
        LocationConfig location,
        TemplateTable templates,
        TradersTable traders,
        HandbookHelper handbook,
        ItemHelper items,
        TraderHelper traderHelper,
        RagfairPriceService priceService
    )
    {
        var generation = ragfair.Dynamic.GenerateBaseFleaPrices;
        var rows = PriceTemplates.Select(text =>
        {
            var tpl = new MongoId(text);
            var handbookPrice = handbook.GetTemplatePrice(tpl);
            var buyers = traders.Select(pair =>
            {
                var first = pair.Value.Base.LoyaltyLevels?.FirstOrDefault();
                var percent = 100d - (first?.BuyPriceCoefficient ?? 100d);
                return new
                {
                    TraderId = pair.Key.ToString(),
                    BuyPriceCoefficient = first?.BuyPriceCoefficient,
                    SellPercent = percent,
                    Price = handbookPrice * percent / 100d
                };
            }).ToArray();
            return new
            {
                Template = text,
                HandbookPrice = handbookPrice,
                TemplatePrice = templates.Prices.GetValueOrDefault(tpl),
                FleaPrice = priceService.GetFleaPriceForItem(tpl),
                CachedHighestTraderPrice = traderHelper.GetHighestSellToTraderPrice(tpl),
                CurrentHighestTraderPrice = buyers.Length == 0 ? 1d : buyers.Max(x => x.Price),
                Buyers = buyers,
                ConfiguredBaseClassMembership = generation.ItemTypeMultiplierOverride.Select(pair => new
                {
                    BaseClass = pair.Key.ToString(),
                    Multiplier = pair.Value,
                    Matches = items.IsOfBaseclass(tpl, pair.Key)
                }).ToArray()
            };
        }).ToArray();
        return new
        {
            NativeCoreVersion = typeof(RagfairConfig).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            ReadOnly = true,
            PriceGeneration = generation,
            PriceRows = rows,
            LocationMultipliers = new
            {
                Loose = location.LooseLootMultiplier,
                Static = location.StaticLootMultiplier
            }
        };
    }
}
