using Mono.Cecil;
using System.Text.Json;

internal static class NativeSupplyPriceSourceChecks
{
    public static int Run(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Expected <Assembly-CSharp.dll> <pitFireTeam source root>.");
        string assemblyPath = Path.GetFullPath(args[0]);
        string sourceRoot = Path.GetFullPath(args[1]);
        string patchPath = Path.Combine(sourceRoot, "client", "Patches", "TeammateEquipmentBuildsScreenPatch.cs");
        string helperPath = Path.Combine(sourceRoot, "client", "Compat", "Spt5NativeSupplyDataPriceSource.cs");
        string fixturePath = Path.Combine(sourceRoot, "tests", "Spt5InteropContracts", "fixtures", "native-supply-price-source-cases.json");
        string patch = File.ReadAllText(patchPath);
        string helper = File.ReadAllText(helperPath);
        using ModuleDefinition module = ModuleDefinition.ReadModule(assemblyPath);
        TypeDefinition supply = FindType(module, "EFT.SupplyData");
        TypeDefinition sourceInterface = FindType(module, "EFT.IBasePriceSource");
        TypeDefinition itemType = FindType(module, "EFT.InventoryLogic.Item");
        string dictionaryType = "Il2CppSystem.Collections.Generic.Dictionary`2<System.String,System.Double>";
        MethodDefinition? ctor = supply.Methods.SingleOrDefault(method => method.IsConstructor && !method.IsStatic && method.Parameters.Count == 0);
        MethodDefinition? getBasePrice = supply.Methods.SingleOrDefault(method => method.Name == "GetBasePrice" && method.Parameters.Select(parameter => parameter.ParameterType.FullName).SequenceEqual(new[] { "EFT.MongoID" }));
        MethodDefinition? interfaceGetBasePrice = sourceInterface.Methods.SingleOrDefault(method => method.Name == "GetBasePrice" && method.Parameters.Select(parameter => parameter.ParameterType.FullName).SequenceEqual(new[] { "EFT.MongoID" }));
        bool nativeInterfaceShape = supply.Interfaces.Any(item => item.InterfaceType.FullName == "EFT.IBasePriceSource") && ctor != null && getBasePrice != null && getBasePrice.ReturnType.FullName == "System.Double" && getBasePrice.IsVirtual && sourceInterface.IsInterface && interfaceGetBasePrice != null && interfaceGetBasePrice.IsVirtual && interfaceGetBasePrice.ReturnType.FullName == "System.Double";
        bool nativeDataShape = new[] { "MarketPrices", "CurrencyCourses" }.All(name => { PropertyDefinition? property = supply.Properties.SingleOrDefault(item => item.Name == name); return property != null && property.PropertyType.FullName == dictionaryType && property.GetMethod != null && property.SetMethod != null; });
        using JsonDocument fixture = JsonDocument.Parse(File.ReadAllText(fixturePath));
        JsonElement root = fixture.RootElement;
        bool failureShape = root.GetProperty("nativeFailure").GetProperty("observedException").GetString() == "MethodAccessException" && root.GetProperty("nativeFailure").GetProperty("failedMethod").GetString() == "EFT.IBasePriceSource.GetBasePrice" && root.GetProperty("nativeFailure").GetProperty("sourceObject").GetString() == "managed MarketPriceSource";
        bool templateIdentity = itemType.Properties.SingleOrDefault(property => property.Name == "Id")?.PropertyType.FullName == "System.String" && itemType.Properties.SingleOrDefault(property => property.Name == "TemplateId")?.PropertyType.FullName == "EFT.MongoID";
        JsonElement identityCase = root.GetProperty("cases").EnumerateArray().Single(item => item.GetProperty("caseId").GetString() == "instance-versus-template-key");
        bool identityFixtureValid = identityCase.GetProperty("instanceId").GetString() == "6581800bfca8fa7cdc4a6914" && identityCase.GetProperty("templateId").GetString() == "5580223e4bdc2d1c128b457f" && identityCase.GetProperty("expectedLookupKey").GetString() == "templateId";
        bool casesValid = root.GetProperty("cases").EnumerateArray().Select(item => item.GetProperty("caseId").GetString()).ToHashSet(StringComparer.Ordinal).SetEquals(new[] { "market-price-precedence", "handbook-fallback", "missing-price-zero", "native-stack-and-custom-price-path", "instance-versus-template-key" });
        int priceMethodStart = patch.IndexOf("private static double CalculateSingleItemMarketRoublePrice(Item item)", StringComparison.Ordinal);
        int priceMethodEnd = patch.IndexOf("private static double CalculateKitLoadoutItemMarketRoublePrice", priceMethodStart, StringComparison.Ordinal);
        string priceMethod = priceMethodStart >= 0 && priceMethodEnd > priceMethodStart ? patch.Substring(priceMethodStart, priceMethodEnd - priceMethodStart) : string.Empty;
        bool sourceShape = priceMethod.Contains("string templateId = item.TemplateId.ToString();", StringComparison.Ordinal) && priceMethod.Contains("GetBasePrice(item.TemplateId)", StringComparison.Ordinal) && !priceMethod.Contains("item.Id", StringComparison.Ordinal) && patch.Contains("Spt5NativeSupplyDataPriceSource.Create(_marketPrices)", StringComparison.Ordinal) && patch.Contains("Spt5NativeSupplyDataPriceSource.AddHandbookFallback(_nativeBuildMarketPriceSource, templateId, handbookFallback)", StringComparison.Ordinal) && patch.Contains("CalculateBuyoutBasePriceForSingleItem(", StringComparison.Ordinal) && patch.Contains("ApplyCustomPriceIfNeeded(item, price)", StringComparison.Ordinal) && patch.Contains("_nativeBuildMarketPriceSourceMarketPrices, _marketPrices", StringComparison.Ordinal) && !patch.Contains("class MarketPriceSource : Il2CppSystem.Object, IBasePriceSource", StringComparison.Ordinal);
        bool helperShape = helper.Contains("new SupplyData()", StringComparison.Ordinal) && helper.Contains("source.MarketPrices = nativeMarketPrices", StringComparison.Ordinal) && helper.Contains("source.CurrencyCourses = new Il2CppSystem.Collections.Generic.Dictionary<string, double>()", StringComparison.Ordinal) && helper.Contains("nativeMarketPrices[pair.Key] = pair.Value", StringComparison.Ordinal) && helper.Contains("prices[templateId] = handbookFallback", StringComparison.Ordinal);
        var checks = new[] { new { check = "metadata-native-interface", passed = nativeInterfaceShape }, new { check = "metadata-native-price-dictionaries", passed = nativeDataShape }, new { check = "source-uses-native-supplydata-and-preserves-price-flow", passed = sourceShape && helperShape }, new { check = "r183-real-failure-and-preservation-cases", passed = failureShape && templateIdentity && identityFixtureValid && casesValid } };
        bool passed = checks.All(item => item.passed);
        Console.WriteLine(JsonSerializer.Serialize(new { test = "native-supply-price-source-contract", passed, assemblyPath, patchPath, helperPath, fixturePath, nativeInterface = supply.Interfaces.Select(item => item.InterfaceType.FullName), checks, runtimeNativeAdoptionVerified = false }, new JsonSerializerOptions { WriteIndented = true }));
        return passed ? 0 : 1;
    }

    private static TypeDefinition FindType(ModuleDefinition module, string name)
        => module.Types.SingleOrDefault(type => type.FullName == name) ?? throw new InvalidOperationException("Missing metadata type " + name);
}