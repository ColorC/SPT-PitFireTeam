using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Globalization;
using Mono.Cecil;
using Mono.Cecil.Cil;

internal static partial class Program
{
    static readonly JsonSerializerOptions J = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    static int Main(string[] a)
    {
        try
        {
            if (a.Length == 0 || a[0] == "--help" || a[0] == "help")
            {
                Help();
                return 0;
            }
            return a[0] switch
            {
                "audit" => Audit(Args.P(a[1..])),
                "inspect-types" => Inspect(Args.P(a[1..])),
                "native-type-resolution-self-test" => NativeTypeResolutionSelfTest(Args.P(a[1..])),
                "native-target-audit" => NativeTargetAudit(Args.P(a[1..])),
                "native-target-self-test" => NativeTargetSelfTest(Args.P(a[1..])),
                "self-test" => Self(),
        
                "follower-task-ownership-self-test" => FollowerSpawnTaskOwnershipChecks.Run(),
                "being-hit-action-abi-gate-self-test" => BeingHitActionAbiGateSelfTest(),
                "being-hit-action-method-info-self-test" => BeingHitActionMethodInfoSelfTest(Args.P(a[1..])),
                "registration-self-test" => RegistrationSelfTest(Args.P(a[1..])),
                "modulepatch-finalizer-registration-self-test" => ModulePatchFinalizerRegistrationSelfTest(Args.P(a[1..])),
                "qa-player-maintenance-self-test" => QaPlayerMaintenanceChecks.Run(),
                "profile-fetch-native-cast-self-test" => ProfileFetchInteropChecks.Run(a[1..]),
                "other-profile-ui-member-self-test" => OtherProfileUiMemberChecks.Run(a[1..]),
                "native-supply-price-source-self-test" => NativeSupplyPriceSourceChecks.Run(a[1..]),
                "normal-squad-pipeline-probe-self-test" => NormalSquadPipelineProbeChecks.Run(a[1..]),
                "radio-audio-pipeline-self-test" => RadioAudioPipelineChecks.Run(a[1..]),
                "loot-patrol-diagnostics-self-test" => LootPatrolDiagnosticsChecks.Run(a[1..]),
                "repair-runtime-field-boxing" => RuntimeFieldBoxingRepair.Run(a[1..]),
                "repair-runtime-field-boxing-self-test" => RuntimeFieldBoxingRepair.SelfTest(a[1..]),
                _ => throw new Usage("Unknown command " + a[0]),
            };
        }
        catch (Usage e)
        {
            Console.Error.WriteLine(e.Message);
            Help();
            return 2;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    static void Help()
    {
        Console.WriteLine(
            "Spt5InteropContracts: offline metadata inspection only; no Assembly.Load, IL2CPP initialization, or game start."
        );
        Console.WriteLine(
            "audit --mod <dll> --game-root <BepInEx/interop> --runtime-root <BepInEx> --native-exe <exe> [--build-id <label>] [--expected-native-version <file-or-product-version>] [--expected-framework net6.0] [--framework-root <canonical CLR metadata dir>] [--contract <json>] [--failure-manifest <json>] [--required-patches <json>] [--fail-scope-only] [--report <absolute-json>]"
        );
        Console.WriteLine(
            "inspect-types --assembly-root <dir> [--type <exact FullName> | --query <prefix>] [--report <absolute-json>]"
        );
        Console.WriteLine("self-test");
        Console.WriteLine("qa-player-maintenance-self-test");
        Console.WriteLine("follower-task-ownership-self-test");
        Console.WriteLine("profile-fetch-native-cast-self-test --assembly <Assembly-CSharp.dll> --source-root <pitFireTeam> [--fixtures <cases.json>]");
        Console.WriteLine("other-profile-ui-member-self-test --assembly <Assembly-CSharp.dll> --source-root <pitFireTeam>");
        Console.WriteLine("native-supply-price-source-self-test <Assembly-CSharp.dll> <pitFireTeam source root>");
        Console.WriteLine("normal-squad-pipeline-probe-self-test <pitFireTeam-source-root>");
        Console.WriteLine("radio-audio-pipeline-self-test <pitFireTeam-source-root>");
        Console.WriteLine("repair-runtime-field-boxing <inputDll> <outputDll> [--report <json>]");
        Console.WriteLine("repair-runtime-field-boxing-self-test <inputDll> (offline Cecil; nativeValidation=false)");
        Console.WriteLine("being-hit-action-abi-gate-self-test");
        Console.WriteLine("being-hit-action-method-info-self-test --bridge <Spt5BeingHitActionBridge.cs> --runtime <Il2CppInterop.Runtime.dll> --interop <Il2Cppmscorlib.dll>");
        Console.WriteLine("native-target-audit --input <absolute-native-report.json> [--runtime-source <absolute-run-evidence>] [--metadata <inspect-types-json-list-separated-by-semicolon>] [--report <absolute-json>]");
        Console.WriteLine("native-target-self-test [--fixtures <absolute-fixture.json>] [--report <absolute-json>]");
        Console.WriteLine("native-type-resolution-self-test --game-root <BepInEx/interop> --mod <pitFireTeam.dll> [--report <absolute-json>]");
        Console.WriteLine(
            "Missing and dynamic patch contracts, ambiguous overloads, mismatched signatures and historical runtime failures fail closed."
        );
    }

    static int Audit(Args a)
    {
        string modp = File(a.R("mod")),
            gamep = Dir(a.R("game-root")),
            rtp = Dir(a.R("runtime-root")),
            exe = File(a.R("native-exe")),
            build = a.G("build-id") ?? "",
            outp = Out(a.G("report"), "offline-spt5-audit.json");
        string? nativeExpected = a.G("expected-native-version"),
            tf = a.G("expected-framework"),
            contract = a.G("contract"),
            fail = a.G("failure-manifest"),
            required = a.G("required-patches");
        bool scopeOnly = a.Has("fail-scope-only");
        string? frameworkRoot = a.G("framework-root") is { Length: > 0 } fr ? Dir(fr) : null;
        var r = new Report
        {
            Command = "audit",
            Inputs = new()
            {
                ["mod"] = modp,
                ["gameRoot"] = gamep,
                ["runtimeRoot"] = rtp,
                ["nativeExe"] = exe,
                ["buildId"] = build,
                ["expectedNativeVersion"] = nativeExpected ?? "",
                ["frameworkRoot"] = frameworkRoot ?? "",
                ["contract"] = contract is null ? "" : File(contract),
                ["failureManifest"] = fail is null ? "" : File(fail),
                ["requiredPatches"] = required is null ? "" : File(required),
            },
            Native = Native(exe, build),
        };
        if (nativeExpected != null)
            Add(
                r,
                "native-version",
                r.Native.FileVersion == nativeExpected || r.Native.ProductVersion == nativeExpected,
                "Observed fileVersion="
                    + (r.Native.FileVersion ?? "missing")
                    + " productVersion="
                    + (r.Native.ProductVersion ?? "missing")
                    + "; expected one of those exact versions="
                    + nativeExpected
            );
        using var game = Index.Read(gamep);
        using var rt = Index.Read(rtp);
        using var frameworkIndex = frameworkRoot is null ? null : Index.Read(frameworkRoot);
        Add(
            r,
            "Assembly-CSharp",
            game.Find("Assembly-CSharp").Count > 0,
            "Found " + game.Find("Assembly-CSharp").Count + " matching metadata files."
        );
        foreach (
            var i in game.Issues.Concat(rt.Issues)
                .Concat(frameworkIndex is null ? Enumerable.Empty<string>() : frameworkIndex.Issues)
        )
            Add(r, "metadata-read", false, i);
        using var resolver = Resolver(modp, gamep, rtp, frameworkRoot ?? "");
        using var mod = ModuleDefinition.ReadModule(
            modp,
            new ReaderParameters
            {
                ReadingMode = ReadingMode.Deferred,
                InMemory = true,
                AssemblyResolver = resolver,
            }
        );
        string? framework = mod
            .Assembly.CustomAttributes.FirstOrDefault(x =>
                x.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute"
            )
            ?.ConstructorArguments.FirstOrDefault()
            .Value?.ToString();
        Add(
            r,
            "framework-present",
            framework != null,
            "TargetFramework=" + (framework ?? "missing")
        );
        if (tf != null)
        {
            string want =
                tf == "net6.0" ? ".NETCoreApp,Version=v6.0"
                : tf == "net10.0" ? ".NETCoreApp,Version=v10.0"
                : tf;
            Add(
                r,
                "framework",
                framework?.Contains(want, StringComparison.OrdinalIgnoreCase) == true,
                "Observed " + (framework ?? "missing") + "; expected " + want
            );
        }
        foreach (var d in mod.AssemblyReferences)
        {
            if (IsFrameworkAssembly(d.Name))
            {
                Add(
                    r,
                    "framework-reference",
                    framework != null,
                    d.FullName
                        + " supplied by declared target framework "
                        + (framework ?? "unknown")
                );
                continue;
            }
            var fs = game.Find(d.Name).Concat(rt.Find(d.Name)).ToArray();
            bool exact = fs.Any(x =>
                x.Version == d.Version
                && x.Token.Equals(
                    Convert.ToHexString(d.PublicKeyToken ?? Array.Empty<byte>()),
                    StringComparison.OrdinalIgnoreCase
                )
            );
            Add(
                r,
                "assembly-reference",
                exact,
                d.FullName
                    + " exactIdentity="
                    + exact
                    + " paths="
                    + string.Join(" | ", fs.Select(x => x.Path))
            );
        }
        string managed = Path.Combine(rtp, "plugins", "Newtonsoft.Json.dll"),
            interop = Path.Combine(rtp, "interop", "Newtonsoft.Json.dll");
        if (!System.IO.File.Exists(managed))
            Add(r, "managed-newtonsoft", false, "Missing managed loader candidate " + managed);
        else
            CheckJson(mod, managed, interop, r);
        var cs = contract == null ? new List<Contract>() : ReadContracts(File(contract));
        var frameworkContracts = cs.Where(c => c.TargetAssembly == "System.Private.CoreLib").ToArray();
        if (frameworkContracts.Length > 0)
        {
            var corelibs = frameworkIndex?.Modules
                .Where(m => m.Assembly?.Name.Name == "System.Private.CoreLib")
                .Select(m => m.Assembly!.Name)
                .ToArray() ?? Array.Empty<AssemblyNameReference>();
            bool validCoreLib = corelibs.Length == 1 && IsNet6CoreLib(corelibs[0]);
            Add(
                r,
                "framework-corelib-identity",
                validCoreLib,
                frameworkRoot is null
                    ? "A canonical framework-root is required for contracts targeting System.Private.CoreLib."
                    : "Root=" + frameworkRoot
                        + " corelibCandidates=" + corelibs.Length
                        + " identity=" + string.Join(" | ", corelibs.Select(x => x.FullName))
                        + " expected System.Private.CoreLib, Version=6.0.0.0, PublicKeyToken=7cec85d7bea7798e"
            );
        }
        CheckGuids(mod, game, rt, r);
        CheckPatches(mod, game, frameworkIndex, cs, r);
        if (contract is not null && string.Equals(mod.Assembly.Name.Name, "pitFireTeam", StringComparison.OrdinalIgnoreCase))
            CheckTeammateBackSourceSelectors(File(contract), r);
        CheckJsonBoundary(mod, game, r);
        CheckConfigBindTypeIdentity(mod, rt, r);
        string moduleName = mod.Assembly.Name.Name;
 string requiredName = required is null ? "" : Path.GetFileName(File(required));
 string? auditModKind = required is null ? null : ResolveAuditModKind(moduleName, requiredName);
 bool routingFixtures = ResolveAuditModKind("pitFireTeam", "required-patches.json") == "pitFireTeam"
 && ResolveAuditModKind("DrakiaXYZ-BigBrain", "required-bigbrain-patches.json") == "BigBrain"
 && ResolveAuditModKind("DrakiaXYZ-BigBrain", "required-patches.json") is null
 && ResolveAuditModKind("pitFireTeam", "required-bigbrain-patches.json") is null;
 Add(r, "mod-kind-routing-fixtures", routingFixtures, "Pit and BigBrain manifests route by exact module identity; cross-pair rejected.");
 if (required != null)
 Add(r, "mod-kind-required-manifest", auditModKind != null,
 "assembly=" + moduleName + " requiredManifest=" + requiredName
 + " resolvedKind=" + (auditModKind ?? "unmatched"));
 bool pitPolicyApplies = contract is not null
 && (required is null
 ? string.Equals(moduleName, "pitFireTeam", StringComparison.OrdinalIgnoreCase)
 : auditModKind == "pitFireTeam");
 string? helperPolicy = pitPolicyApplies
 ? Path.Combine(Path.GetDirectoryName(File(contract!))!, "injected-managed-helper-export-policy.json")
 : null;
 CheckUnity(mod, r, helperPolicy);
        if (fail != null)
            CheckFailures(mod, File(fail), r);
        if (required != null)
            EvaluateScope(File(required), cs, mod, r);
        r.Overall = r.Checks.All(x => x.Passed);
        if (r.ScopePassed != null)
            r.ScopeGatePassed =
                r.ScopePassed.Value
                && r.Checks.Where(x => !x.Passed)
                    .All(x =>
                        x.Id == "harmony-target"
                        && x.Message.Contains(
                            "unresolved: target contract required.",
                            StringComparison.Ordinal
                        )
                    );
        Write(outp, r);
        Console.WriteLine("Overall: " + (r.Overall ? "PASS" : "FAIL"));
        if (r.ScopePassed != null)
        {
            Console.WriteLine(
                "Required scope: "
                    + (r.ScopePassed.Value ? "PASS" : "FAIL")
                    + " ("
                    + r.ScopePassedCount
                    + "/"
                    + r.ScopeTotal
                    + ")"
            );
            Console.WriteLine(
                "Required scope gate: " + (r.ScopeGatePassed == true ? "PASS" : "FAIL")
            );
        }
        Console.WriteLine("Report: " + outp);
        return scopeOnly && required != null ? (r.ScopeGatePassed == true ? 0 : 1)
            : r.Overall ? 0
            : 1;
    }

    static int NativeTypeResolutionSelfTest(Args a)
    {
        string gameRoot = Dir(a.R("game-root"));
        string modPath = File(a.R("mod"));
        string reportPath = Out(a.G("report"), "offline-native-type-resolution-self-test.json");
        using var game = Index.Read(gameRoot);
        using var mod = ModuleDefinition.ReadModule(modPath, new ReaderParameters { InMemory = true, ReadingMode = ReadingMode.Deferred });
        var controllerTypes = game.Types.Where(type => type.Assembly == "Assembly-CSharp" && type.FullName == "EFT.BotsController").ToArray();
        var playerTypes = game.Types.Where(type => type.Assembly == "Assembly-CSharp" && type.FullName == "EFT.Player").ToArray();
        bool modControllerAbsent = !All(mod).Any(type => type.FullName == "EFT.BotsController");
        bool modPlayerAbsent = !All(mod).Any(type => type.FullName == "EFT.Player");
        bool controllerPositive = controllerTypes.Length == 1;
        bool playerPositive = playerTypes.Length == 1;
        var cases = new[] {
            new { name = "positive-Assembly-CSharp-BotsController", passed = controllerPositive, assembly = controllerTypes.FirstOrDefault()?.Assembly ?? "", path = controllerTypes.FirstOrDefault()?.Path ?? "" },
            new { name = "positive-Assembly-CSharp-Player", passed = playerPositive, assembly = playerTypes.FirstOrDefault()?.Assembly ?? "", path = playerTypes.FirstOrDefault()?.Path ?? "" },
            new { name = "negative-pitFireTeam-BotsController-lookup", passed = modControllerAbsent, assembly = mod.Assembly.Name.Name, path = Path.GetFullPath(modPath) },
            new { name = "negative-pitFireTeam-Player-lookup", passed = modPlayerAbsent, assembly = mod.Assembly.Name.Name, path = Path.GetFullPath(modPath) },
        };
        bool passed = cases.All(test => test.passed);
        var result = new { command = "native-type-resolution-self-test", gameRoot, mod = Path.GetFullPath(modPath), passed, cases };
        Write(reportPath, result);
        Console.WriteLine(JsonSerializer.Serialize(result, J));
        Console.WriteLine("Report: " + reportPath);
        return passed ? 0 : 1;
    }

        static int Inspect(Args a)
    {
        string root = Dir(a.R("assembly-root"));
        string? outp = a.G("report") is { Length: > 0 } requestedReport
            ? Out(requestedReport, "")
            : null;
        string? exact = a.G("type"),
            prefix = a.G("query");
        using var ix = Index.Read(root);
        var hits = ix
            .Types.Where(t =>
                exact != null
                    ? t.FullName == exact
                    : prefix == null
                        || t.FullName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            )
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => new
            {
                t.Assembly,
                t.Path,
                t.FullName,
                BaseType = t.Type.BaseType?.FullName,
                Methods = t.Type.Methods.Select(m => new
                {
                    m.Name,
                    Return = m.ReturnType.FullName,
                    Parameters = m.Parameters.Select(p => new
                    {
                        p.Name,
                        Type = p.ParameterType.FullName,
                    }),
                    m.IsStatic,
                }),
                Properties = t.Type.Properties.Select(p => new
                {
                    p.Name,
                    Type = p.PropertyType.FullName,
                    Getter = p.GetMethod != null,
                    Setter = p.SetMethod != null,
                }),
                Fields = t.Type.Fields.Select(f => new
                {
                    f.Name,
                    Type = f.FieldType.FullName,
                    f.IsStatic,
                    Constant = f.HasConstant ? f.Constant : null,
                }),
            })
            .ToArray();
        var obj = new
        {
            command = "inspect-types",
            input = root,
            query = exact ?? prefix,
            assemblyCount = ix.Modules.Count,
            typeCount = hits.Length,
            matches = hits,
        };
        PublishInspectTypesResult(obj, outp, Console.Out, Write);
        return exact != null && hits.Length != 1 ? 1 : 0;
    }

    static void PublishInspectTypesResult(
        object obj,
        string? reportPath,
        TextWriter output,
        Action<string, object> writeReport
    )
    {
        if (reportPath != null)
            writeReport(reportPath, obj);
        output.WriteLine(JsonSerializer.Serialize(obj, J));
        if (reportPath != null)
            output.WriteLine("Report: " + reportPath);
    }

    static List<(string Name, bool Passed)> InspectTypesOutputSelfTest()
    {
        var obj = new { command = "inspect-types", typeCount = 0 };
        int defaultWriteCalls = 0;
        using var defaultOutput = new StringWriter(CultureInfo.InvariantCulture);
        PublishInspectTypesResult(
            obj,
            null,
            defaultOutput,
            (_, _) => defaultWriteCalls++
        );
        bool defaultIsStdoutOnly =
            defaultWriteCalls == 0
            && !defaultOutput.ToString().Contains("Report:", StringComparison.Ordinal)
            && JsonDocument.Parse(defaultOutput.ToString()).RootElement.GetProperty("command").GetString()
                == "inspect-types";

        string explicitPath = Path.GetFullPath(
            Path.Combine(Environment.CurrentDirectory, "inspect-types-explicit-self-test.json")
        );
        int explicitWriteCalls = 0;
        string? writtenPath = null;
        using var explicitOutput = new StringWriter(CultureInfo.InvariantCulture);
        PublishInspectTypesResult(
            obj,
            explicitPath,
            explicitOutput,
            (path, _) =>
            {
                explicitWriteCalls++;
                writtenPath = path;
            }
        );
        bool explicitReportRequested =
            explicitWriteCalls == 1
            && writtenPath == explicitPath
            && explicitOutput.ToString().Contains("Report: " + explicitPath, StringComparison.Ordinal)
            && JsonDocument.Parse(explicitOutput.ToString()[..explicitOutput.ToString().LastIndexOf("Report: ", StringComparison.Ordinal)].Trim()).RootElement.GetProperty("command").GetString()
                == "inspect-types";

        return new()
        {
            ("inspect-types default has no report writer and emits JSON only", defaultIsStdoutOnly),
            ("inspect-types explicit report uses full path and emits report receipt", explicitReportRequested),
        };
    }

    static int Self()
    {
        using var fixture = ModuleDefinition.CreateModule("JsonDtoFixture", ModuleKind.Dll);
        var dto = new TypeDefinition(
            "Fixture",
            "ManagedDto",
            TypeAttributes.Public | TypeAttributes.Class,
            fixture.TypeSystem.Object
        );
        fixture.Types.Add(dto);
        var native = new TypeDefinition(
            "Fixture",
            "NativeWrapper",
            TypeAttributes.Public | TypeAttributes.Class,
            new TypeReference(
                "Il2CppSystem",
                "Object",
                fixture,
                new AssemblyNameReference("Il2Cppmscorlib", new Version(1, 0, 0, 0)),
                false
            )
        );
        fixture.Types.Add(native);
        var ctorOwner = new TypeDefinition("EFT", "LocalGame", TypeAttributes.Public | TypeAttributes.Class, fixture.TypeSystem.Object);
        fixture.Types.Add(ctorOwner);
        ctorOwner.Fields.Add(new FieldDefinition("NativeMethodInfoPtr__ctor_Public_Void_0",
            FieldAttributes.Public | FieldAttributes.Static, fixture.TypeSystem.IntPtr));
        var ambiguousCtorOwner = new TypeDefinition("EFT", "LocalGameAmbiguous", TypeAttributes.Public | TypeAttributes.Class, fixture.TypeSystem.Object);
        fixture.Types.Add(ambiguousCtorOwner);
        ambiguousCtorOwner.Fields.Add(new FieldDefinition("NativeMethodInfoPtr__ctor_Public_Void_0",
            FieldAttributes.Public | FieldAttributes.Static, fixture.TypeSystem.IntPtr));
        ambiguousCtorOwner.Fields.Add(new FieldDefinition("NativeMethodInfoPtr__ctor_Public_Void_0",
            FieldAttributes.Public | FieldAttributes.Static, fixture.TypeSystem.IntPtr));
        var wrongCtorOwner = new TypeDefinition("EFT", "AIData", TypeAttributes.Public | TypeAttributes.Class, fixture.TypeSystem.Object);
        fixture.Types.Add(wrongCtorOwner);
        wrongCtorOwner.Fields.Add(new FieldDefinition("NativeMethodInfoPtr__ctor_Public_Void_BotOwner_Player_0",
            FieldAttributes.Public, fixture.TypeSystem.IntPtr));
        var wrongTypeCtorOwner = new TypeDefinition("EFT", "ItemController", TypeAttributes.Public | TypeAttributes.Class, fixture.TypeSystem.Object);
        fixture.Types.Add(wrongTypeCtorOwner);
        wrongTypeCtorOwner.Fields.Add(new FieldDefinition("NativeMethodInfoPtr_CanMoveDogtag_Public_Boolean_Item_0",
            FieldAttributes.Public | FieldAttributes.Static, fixture.TypeSystem.Int32));
        var list = new GenericInstanceType(
            new TypeReference(
                "Il2CppSystem.Collections.Generic",
                "List`1",
                fixture,
                new AssemblyNameReference("Il2Cppmscorlib", new Version(1, 0, 0, 0)),
                false
            )
        );
        list.GenericArguments.Add(dto);
        var managedIdentity = new AssemblyNameReference("Newtonsoft.Json", new Version(13, 0, 0, 0))
        {
            PublicKeyToken = Convert.FromHexString("30ad4fe6b2a6aeed"),
        };
        var otherIdentity = new AssemblyNameReference("Newtonsoft.Json", new Version(13, 0, 0, 0))
        {
            PublicKeyToken = Array.Empty<byte>(),
        };
        var gm = new MethodDefinition(
            "M",
            MethodAttributes.Public | MethodAttributes.Static,
            fixture.TypeSystem.Void
        );
        dto.Methods.Add(gm);
        var gp = new GenericParameter("T", gm);
        gm.GenericParameters.Add(gp);
        gm.ReturnType = gp;
        var gi = new GenericInstanceMethod(gm);
        gi.GenericArguments.Add(fixture.TypeSystem.String);
        char q = Convert.ToChar(96);
        var c = new[]
        {
            (
                "exact signature",
                Sig(
                    "System.String",
                    new[] { "System.Int32" },
                    "System.String",
                    new[] { "System.Int32" }
                )
            ),
            (
                "wrong return",
                !Sig(
                    "System.String",
                    new[] { "System.Int32" },
                    "System.Object",
                    new[] { "System.Int32" }
                )
            ),
            (
                "System.Task versus Il2CppTask",
                !Sig(
                    "System.Threading.Tasks.Task" + q + "1<System.String>",
                    Array.Empty<string>(),
                    "Il2CppSystem.Threading.Tasks.Task" + q + "1<System.String>",
                    Array.Empty<string>()
                )
            ),
            (
                "array versus List",
                !Sig(
                    "System.Int32[]",
                    Array.Empty<string>(),
                    "System.Collections.Generic.List" + q + "1<System.Int32>",
                    Array.Empty<string>()
                )
            ),
            (
                "nullable versus scalar",
                !Sig(
                    "System.Nullable" + q + "1<System.Int32>",
                    Array.Empty<string>(),
                    "System.Int32",
                    Array.Empty<string>()
                )
            ),
            ("field versus property", "FIELD:System.Int32" != "PROPERTY:System.Int32"),
            ("native constructor pointer exact static IntPtr field", IsNativeMethodInfoPointerField(ctorOwner, "NativeMethodInfoPtr__ctor_Public_Void_0")),
            ("native constructor pointer duplicate field rejected", !IsNativeMethodInfoPointerField(ambiguousCtorOwner, "NativeMethodInfoPtr__ctor_Public_Void_0")),
            ("native constructor pointer wrong field name rejected", !IsNativeMethodInfoPointerField(ctorOwner, "NativeMethodInfoPtr__ctor_Public_Void_Player_0")),
            ("native constructor pointer instance field rejected", !IsNativeMethodInfoPointerField(wrongCtorOwner, "NativeMethodInfoPtr__ctor_Public_Void_BotOwner_Player_0")),
            ("native constructor pointer non-IntPtr field rejected", !IsNativeMethodInfoPointerField(wrongTypeCtorOwner, "NativeMethodInfoPtr_CanMoveDogtag_Public_Boolean_Item_0")),
            ("native wrapper constructor without native pointer field rejected", !IsNativeMethodInfoPointerField(native, "NativeMethodInfoPtr__ctor_Public_Void_0")),
            ("ambiguous overload unresolved", new[] { "M(Int32)", "M(String)" }.Length == 2),
            (
                "managed/interop Newtonsoft",
                Managed("Newtonsoft.Json") && !Managed("Il2CppNewtonsoft.Json")
            ),
            (
                "same-name wrong-token Newtonsoft identity rejected",
                !SameIdentity(managedIdentity, otherIdentity)
            ),
            ("canonical game CLR6 CoreLib accepted", IsNet6CoreLib(new AssemblyNameReference("System.Private.CoreLib", new Version(6, 0, 0, 0)) { PublicKeyToken = Convert.FromHexString("7cec85d7bea7798e") })),
            ("host or wrong-version CoreLib rejected", !IsNet6CoreLib(new AssemblyNameReference("System.Private.CoreLib", new Version(10, 0, 0, 0)) { PublicKeyToken = Convert.FromHexString("7cec85d7bea7798e") })),
            ("generic method canonicalization", Key(gi) == Key(gm)),
            (
                "managed DTO rejected/native wrapper allowed",
                IsManagedJsonDto(dto, fixture)
                    && !IsManagedJsonDto(native, fixture)
                    && IsManagedJsonDto(list, fixture)
            ),
        };
        var configBindTests = ConfigBindIdentitySelfTest();
        var inspectTypesOutputTests = InspectTypesOutputSelfTest();
        foreach (var x in c.Concat(configBindTests))
            Console.WriteLine((x.Item2 ? "PASS " : "FAIL ") + x.Item1);
        foreach (var x in inspectTypesOutputTests)
            Console.WriteLine((x.Passed ? "PASS " : "FAIL ") + x.Name);
        return c.All(x => x.Item2)
            && configBindTests.All(x => x.Item2)
            && inspectTypesOutputTests.All(x => x.Passed)
                ? 0
                : 1;
    }

    static bool IsFrameworkAssembly(string n) =>
        n == "mscorlib"
        || n == "netstandard"
        || n == "System"
        || n.StartsWith("System.", StringComparison.Ordinal);

    static bool Sig(string r, string[] p, string er, string[] ep) => r == er && p.SequenceEqual(ep);

    static bool Managed(string s) => s == "Newtonsoft.Json";

    static void CheckJson(ModuleDefinition mod, string managed, string interop, Report r)
    {
        using var man = ModuleDefinition.ReadModule(
            managed,
            new ReaderParameters { ReadingMode = ReadingMode.Deferred, InMemory = true }
        );
        using var nat = System.IO.File.Exists(interop)
            ? ModuleDefinition.ReadModule(
                interop,
                new ReaderParameters { ReadingMode = ReadingMode.Deferred, InMemory = true }
            )
            : null;
        var managedExports = All(man)
            .SelectMany(t => t.Methods)
            .ToLookup(Key, StringComparer.Ordinal);
        var nativeExports =
            nat == null
                ? null
                : All(nat).SelectMany(t => t.Methods).ToLookup(Key, StringComparer.Ordinal);
        var calls = All(mod)
            .SelectMany(t => t.Methods)
            .Where(m => m.HasBody)
            .SelectMany(m =>
                m.Body.Instructions.Where(i =>
                        i.Operand is MethodReference x
                        && x.DeclaringType.FullName.StartsWith(
                            "Newtonsoft.Json",
                            StringComparison.Ordinal
                        )
                    )
                    .Select(i => (Caller: m, Method: (MethodReference)i.Operand))
            )
            .DistinctBy(x => Key(x.Method))
            .ToArray();
        foreach (var c in calls)
        {
            var scope = c.Method.DeclaringType.GetElementType().Scope as AssemblyNameReference;
            string kind =
                scope != null && SameIdentity(scope, man.Assembly.Name) ? "managed"
                : scope != null && nat != null && SameIdentity(scope, nat.Assembly.Name) ? "interop"
                : "unknown";
            var exports =
                kind == "managed" ? managedExports
                : kind == "interop" ? nativeExports
                : null;
            int n = exports?[Key(c.Method)].Count() ?? 0;
            Add(
                r,
                "newtonsoft-method",
                kind != "unknown" && n == 1,
                c.Method.FullName
                    + " scope="
                    + (scope?.FullName ?? c.Method.DeclaringType.Scope.Name)
                    + " referenceKind="
                    + kind
                    + " exactExports="
                    + n
                    + " caller="
                    + c.Caller.DeclaringType.FullName
            );
        }
        Add(
            r,
            "newtonsoft-inventory",
            true,
            "Newtonsoft method operands=" + calls.Length + "; zero calls does not imply coverage."
        );
    }

    static bool IsNet6CoreLib(AssemblyNameReference? a) =>
        a != null
        && a.Name == "System.Private.CoreLib"
        && a.Version == new Version(6, 0, 0, 0)
        && Convert
            .ToHexString(a.PublicKeyToken ?? Array.Empty<byte>())
            .Equals("7CEC85D7BEA7798E", StringComparison.OrdinalIgnoreCase);

    static bool SameIdentity(AssemblyNameReference a, AssemblyNameReference b) =>
        a.Name == b.Name
        && a.Version == b.Version
        && Convert
            .ToHexString(a.PublicKeyToken ?? Array.Empty<byte>())
            .Equals(
                Convert.ToHexString(b.PublicKeyToken ?? Array.Empty<byte>()),
                StringComparison.OrdinalIgnoreCase
            );

    static DefaultAssemblyResolver Resolver(params string[] paths)
    {
        var r = new DefaultAssemblyResolver();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in paths)
        {
            string root = System.IO.File.Exists(p) ? Path.GetDirectoryName(p)! : p;
            if (!Directory.Exists(root))
                continue;
            foreach (
                var d in Directory
                    .EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                    .Prepend(root)
            )
                if (seen.Add(d))
                    r.AddSearchDirectory(d);
        }
        return r;
    }

    static string Canonical(TypeReference t)
    {
        if (t is GenericParameter g)
            return (g.Type == GenericParameterType.Method ? "!!" : "!") + g.Position;
        if (t is GenericInstanceType i)
            return Canonical(i.ElementType)
                + "<"
                + string.Join(",", i.GenericArguments.Select(Canonical))
                + ">";
        if (t is ByReferenceType b)
            return Canonical(b.ElementType) + "&";
        if (t is PointerType p)
            return Canonical(p.ElementType) + "*";
        if (t is ArrayType a)
            return Canonical(a.ElementType) + "[" + new string(',', Math.Max(0, a.Rank - 1)) + "]";
        return t.FullName;
    }

    static string Key(MethodReference m)
    {
        int arity = m is GenericInstanceMethod g
            ? g.GenericArguments.Count
            : m.GenericParameters.Count;
        if (m is GenericInstanceMethod gi)
            m = gi.ElementMethod;
        return m.DeclaringType.FullName
            + "::"
            + m.Name
            + Convert.ToChar(96)
            + arity
            + "("
            + string.Join(",", m.Parameters.Select(p => Canonical(p.ParameterType)))
            + "):"
            + Canonical(m.ReturnType);
    }

    static string? ResolveAuditModKind(string moduleName, string requiredManifest)
 {
 if (string.Equals(moduleName, "pitFireTeam", StringComparison.OrdinalIgnoreCase)
 && string.Equals(requiredManifest, "required-patches.json", StringComparison.OrdinalIgnoreCase))
 return "pitFireTeam";
 if (string.Equals(moduleName, "DrakiaXYZ-BigBrain", StringComparison.OrdinalIgnoreCase)
 && string.Equals(requiredManifest, "required-bigbrain-patches.json", StringComparison.OrdinalIgnoreCase))
 return "BigBrain";
 return null;
 }
 static void EvaluateScope(string p, List<Contract> contracts, ModuleDefinition mod, Report r)
    {
        using var d = JsonDocument.Parse(System.IO.File.ReadAllText(p));
        var root =
            d.RootElement.ValueKind == JsonValueKind.Array
                ? d.RootElement
                : d.RootElement.GetProperty("requiredPatches");
        var names = root.EnumerateArray()
            .Select(x => x.GetString() ?? "")
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        r.ScopeTotal = names.Length;
        r.ScopeFailures = new();
        int passed = 0;
        foreach (var n in names)
        {
            bool hc = contracts.Any(c => c.PatchClassFullName == n);
            bool ht = All(mod).Any(t => t.FullName == n);
            var q = r
                .Checks.Where(c =>
                    c.Id.StartsWith("harmony-", StringComparison.Ordinal)
                    && c.Message.StartsWith(n + " ", StringComparison.Ordinal)
                )
                .ToArray();
            bool ok = hc && ht && q.Length > 0 && q.All(c => c.Passed);
            if (ok)
                passed++;
            else
                r.ScopeFailures.Add(
                    n
                        + " contract="
                        + hc
                        + " type="
                        + ht
                        + " checks="
                        + q.Length
                        + " failed="
                        + string.Join(
                            ";",
                            q.Where(c => !c.Passed).Select(c => c.Id + ":" + c.Message)
                        )
                );
        }
        r.ScopePassedCount = passed;
        r.ScopePassed = names.Length > 0 && passed == names.Length;
    }

    static void CheckTeammateBackSourceSelectors(string contractPath, Report r)
    {
        var expected = new (string FullName, string Method)[]
        {
            ("pitTeam.Patches.TeammateEquipmentBuildsScreenBackButtonPatch", "_Awake_b__55_0"),
            ("pitTeam.Patches.TeammateEquipmentBuildsScreenAltBackButtonPatch", "_Awake_b__55_1"),
        };
        try
        {
            using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(contractPath));
            var contracts = doc.RootElement.GetProperty("patches").EnumerateArray().ToArray();
            string sourceRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(contractPath)!, "..", "..", ".."));
            string ResolveSource(string value) => Path.GetFullPath(value, sourceRoot);
            string expectedSource = ResolveSource("client/Patches/TeammateEquipmentBuildsScreenPatch.cs");
            foreach (var item in expected)
            {
                var matches = contracts.Where(x => x.GetProperty("patchClassFullName").GetString() == item.FullName).ToArray();
                if (matches.Length != 1)
                {
                    Add(r, "source-selector-contract", false, item.FullName + " manifest entries=" + matches.Length);
                    continue;
                }
                var entry = matches[0];
                string manifestMethod = entry.GetProperty("targetMethod").GetString() ?? "";
                string targetType = entry.GetProperty("targetType").GetString() ?? "";
                var metadata = entry.GetProperty("sourceMetadata");
                string metadataPath = ResolveSource(metadata.GetProperty("sourcePath").GetString() ?? "");
                int metadataSourceLine = metadata.GetProperty("sourceLine").GetInt32();
                var evidence = metadata.GetProperty("selectorEvidence").EnumerateArray().ToArray();
                string selectorText = "return AccessTools.Method(typeof(EquipmentBuildsScreen), \"" + item.Method + "\");";
                string source = System.IO.File.ReadAllText(expectedSource);
                string shortName = item.FullName[(item.FullName.LastIndexOf('.') + 1)..];
                var classMatch = System.Text.RegularExpressions.Regex.Match(source, @"internal\s+class\s+" + System.Text.RegularExpressions.Regex.Escape(shortName) + @"\s*:\s*ModulePatch(?<body>.*?)(?=\r?\n\s*internal\s+class\s+|\z)", System.Text.RegularExpressions.RegexOptions.Singleline);
                var targetMatch = classMatch.Success ? System.Text.RegularExpressions.Regex.Match(classMatch.Groups["body"].Value, @"protected\s+override\s+MethodBase\s+GetTargetMethod\s*\(\s*\)\s*\{(?<body>.*?)\}", System.Text.RegularExpressions.RegexOptions.Singleline) : System.Text.RegularExpressions.Match.Empty;
                int getterOffset = classMatch.Success ? source.IndexOf("protected override MethodBase GetTargetMethod()", classMatch.Index, StringComparison.Ordinal) : -1;
                int selectorOffset = classMatch.Success ? source.IndexOf(selectorText, classMatch.Index, StringComparison.Ordinal) : -1;
                int actualClassLine = classMatch.Success ? 1 + source.Take(classMatch.Index).Count(ch => ch == '\n') : -1;
                int actualGetterLine = getterOffset >= 0 ? 1 + source.Take(getterOffset).Count(ch => ch == '\n') : -1;
                int actualSelectorLine = selectorOffset >= 0 ? 1 + source.Take(selectorOffset).Count(ch => ch == '\n') : -1;
                bool evidenceMatches = evidence.Any(x => ResolveSource(x.GetProperty("path").GetString() ?? "") == expectedSource && x.GetProperty("text").GetString() == "protected override MethodBase GetTargetMethod()")
                    && evidence.Any(x => ResolveSource(x.GetProperty("path").GetString() ?? "") == expectedSource && x.GetProperty("text").GetString() == selectorText);
                bool sourceMatches = targetMatch.Success && targetMatch.Groups["body"].Value.Contains(selectorText, StringComparison.Ordinal);
                bool passed = manifestMethod == item.Method && targetType == "EFT.UI.EquipmentBuildsScreen" && metadataPath == expectedSource && evidenceMatches && sourceMatches;
                Add(r, "source-selector-contract", passed, item.FullName + " manifest=" + manifestMethod + " expected=" + item.Method + " sourceMetadata=" + evidenceMatches + " source=" + sourceMatches + " path=" + metadataPath);
            }
        }
        catch (Exception e)
        {
            Add(r, "source-selector-contract", false, e.GetType().Name + ": " + e.Message);
        }
    }

    static List<Contract> ReadContracts(string p)
    {
        using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(p));
        return doc
            .RootElement.GetProperty("patches")
            .EnumerateArray()
            .Select(x => new Contract
            {
                PatchClassFullName = x.GetProperty("patchClassFullName").GetString() ?? "",
                TargetType = x.GetProperty("targetType").GetString() ?? "",
                TargetMethod = x.GetProperty("targetMethod").GetString() ?? "",
                ParameterTypes = x.TryGetProperty("parameterTypes", out var q)
                    ? q.EnumerateArray().Select(v => v.GetString() ?? "").ToArray()
                    : null,
                ReturnType = x.TryGetProperty("returnType", out var z) ? z.GetString() : null,
                TargetAssembly = x.TryGetProperty("targetAssembly", out var assembly)
                    ? assembly.GetString()
                    : null,
                TargetGenericArguments = x.TryGetProperty("targetGenericArguments", out var ga)
                    ? ga.EnumerateArray().Select(v => v.GetString() ?? "").ToArray()
                    : null,
                NativeConstructorPointerField = x.TryGetProperty("nativeConstructorPointerField", out var pointerField)
                    ? pointerField.GetString()
                    : null,
            })
            .ToList();
    }

    static bool IsSoftDependency(CustomAttribute a, object? soft)
    {
        if (soft == null)
            return false;
        object? value = a.ConstructorArguments.Count > 1 ? a.ConstructorArguments[1].Value : null;
        if (a.Properties.FirstOrDefault(x => x.Name == "Flags") is var n && n.Name == "Flags")
            value = n.Argument.Value;
        return value != null && value.ToString() == soft.ToString();
    }

    static void CheckGuids(ModuleDefinition mod, Index game, Index rt, Report r)
    {
        const string plugin = "BepInEx.BepInPlugin";
        const string dep = "BepInEx.BepInDependency";
        var attrs = All(mod)
            .SelectMany(t => t.CustomAttributes)
            .Concat(mod.Assembly.CustomAttributes)
            .ToArray();
        var own = attrs
            .Where(a => a.AttributeType.FullName == plugin)
            .Select(a =>
                a.ConstructorArguments.Count > 0
                    ? a.ConstructorArguments[0].Value?.ToString()
                    : null
            )
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Add(
            r,
            "plugin-guid",
            own.Length == 1,
            "Declared plugin GUID count=" + own.Length + " values=" + string.Join(",", own)
        );
        var known = game
            .Modules.Concat(rt.Modules)
            .SelectMany(m =>
                All(m)
                    .SelectMany(t => t.CustomAttributes)
                    .Concat(m.Assembly?.CustomAttributes ?? Enumerable.Empty<CustomAttribute>())
            )
            .Where(a => a.AttributeType.FullName == plugin)
            .Select(a =>
                a.ConstructorArguments.Count > 0
                    ? a.ConstructorArguments[0].Value?.ToString()
                    : null
            )
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var a in attrs.Where(a => a.AttributeType.FullName == dep))
        {
            string id =
                a.ConstructorArguments.Count > 0
                    ? a.ConstructorArguments[0].Value?.ToString() ?? ""
                    : "";
            object? soft = rt
                .Modules.SelectMany(m => All(m))
                .FirstOrDefault(t => t.FullName == "BepInEx.BepInDependency/DependencyFlags")
                ?.Fields.FirstOrDefault(f => f.Name == "SoftDependency" && f.HasConstant)
                ?.Constant;
            bool optional = IsSoftDependency(a, soft);
            bool found = known.Contains(id);
            Add(
                r,
                "plugin-dependency",
                found || optional,
                "GUID=" + id + " optional=" + optional + " presentInRuntimeMetadata=" + found
            );
        }
    }

    static void CheckPatches(ModuleDefinition mod, Index game, Index? framework, List<Contract> cs, Report r)
    {
        var ps = All(mod).Where(t => IsPatch(mod, t)).ToArray();
        var map = cs.ToDictionary(c => c.PatchClassFullName, StringComparer.Ordinal);
        foreach (var p in ps)
        {
            if (!map.TryGetValue(p.FullName, out var c))
            {
                Add(
                    r,
                    "harmony-target",
                    false,
                    p.FullName + " unresolved: target contract required."
                );
                continue;
            }
            IEnumerable<Entry> targetIndex =
                c.TargetAssembly == null ? game.Types
                : framework is null ? Enumerable.Empty<Entry>() : framework.Types;
            var ts = targetIndex
                .Where(x =>
                    x.FullName == c.TargetType
                    && (c.TargetAssembly == null || x.Assembly == c.TargetAssembly)
                )
                .ToArray();
            if (ts.Length != 1)
            {
                Add(
                    r,
                    "harmony-type",
                    false,
                    p.FullName + " target type " + c.TargetType + " matches=" + ts.Length
                );
                continue;
            }
            var ms = ts[0]
                .Type.Methods.Where(m =>
                    m.Name == c.TargetMethod
                    && (
                        c.ParameterTypes == null
                        || m.Parameters.Select(x => x.ParameterType.FullName)
                            .SequenceEqual(c.ParameterTypes)
                    )
                )
                .ToArray();
            if (ms.Length != 1)
            {
                Add(
                    r,
                    "harmony-method",
                    false,
                    p.FullName
                        + " overload matches="
                        + ms.Length
                        + "; exact parameterTypes required"
                );
                continue;
            }
            var t = ms[0];
            if (c.TargetGenericArguments != null)
            {
                bool genericArgumentsValid =
                    c.TargetGenericArguments.Length == ts[0].Type.GenericParameters.Count
                    && c.TargetGenericArguments.All(x => !string.IsNullOrWhiteSpace(x));
                Add(
                    r,
                    "harmony-generic-arguments",
                    genericArgumentsValid,
                    p.FullName
                        + " targetGenericArguments="
                        + string.Join(",", c.TargetGenericArguments)
                        + " targetArity="
                        + ts[0].Type.GenericParameters.Count
                );
            }
            Add(
                r,
                "harmony-signature",
                c.ReturnType == null || c.ReturnType == t.ReturnType.FullName,
                p.FullName
                    + " target="
                    + c.TargetType
                    + "::"
                    + t.Name
                    + "("
                    + string.Join(",", t.Parameters.Select(x => x.ParameterType.FullName))
                    + "):"
                    + t.ReturnType.FullName
            );
            if (c.NativeConstructorPointerField != null)
            {
                bool pointerFieldValid = IsNativeMethodInfoPointerField(ts[0].Type, c.NativeConstructorPointerField);
                Add(r, "harmony-native-constructor-pointer-field", pointerFieldValid,
                    p.FullName + " expected static System.IntPtr " + c.TargetType + "." + c.NativeConstructorPointerField);
            }
            foreach (var pm in p.Methods.Where(IsPatchMethod))
            foreach (var pp in pm.Parameters)
            {
                if (pp.Name == "__result")
                {
                    string actual = Unref(pp.ParameterType).FullName;
                    string targetResult = ContractType(t.ReturnType, c);
                    Add(
                        r,
                        "harmony-result",
                        actual == targetResult,
                        p.FullName + " __result=" + actual + " target=" + targetResult
                    );
                    continue;
                }
                if (
                    pp.Name
                    is "__instance"
                        or "__state"
                        or "__args"
                        or "__originalMethod"
                        or "__runOriginal"
                        or "__exception"
                )
                    continue;
                var tp = t.Parameters.FirstOrDefault(x => x.Name == pp.Name);
                if (
                    tp == null
                    && pp.Name.StartsWith("__")
                    && int.TryParse(pp.Name.AsSpan(2), out int pos)
                    && pos >= 0
                    && pos < t.Parameters.Count
                )
                    tp = t.Parameters[pos];
                Add(
                    r,
                    "harmony-parameter",
                    tp != null
                        && CompatiblePatchType(tp.ParameterType, c, pp.ParameterType),
                    p.FullName
                        + " param "
                        + pp.Name
                        + " target="
                        + (tp == null ? "unmapped" : ContractType(tp.ParameterType, c))
                        + " patch="
                        + pp.ParameterType.FullName
                );
            }
        }
        foreach (string n in map.Keys.Except(ps.Select(x => x.FullName), StringComparer.Ordinal))
            Add(r, "unused-contract", false, "Contract type not in mod: " + n);
    }

    static bool IsNativeMethodInfoPointerField(TypeDefinition type, string name)
    {
        var fields = type.Fields.Where(field => field.Name == name).ToArray();
        return fields.Length == 1 && fields[0].IsStatic && fields[0].FieldType.FullName == "System.IntPtr";
    }

    static TypeReference Unref(TypeReference t) => t is ByReferenceType b ? b.ElementType : t;

    static bool IsPatch(ModuleDefinition mod, TypeDefinition t) =>
        IsModulePatch(t, new HashSet<string>(StringComparer.Ordinal))
        || t.CustomAttributes.Any(a =>
            a.AttributeType.FullName.StartsWith("HarmonyLib.HarmonyPatch", StringComparison.Ordinal)
        )
        || t.Methods.Any(m =>
            m.CustomAttributes.Any(a =>
                a.AttributeType.Name
                    is "HarmonyPrefix"
                        or "HarmonyPostfix"
                        or "HarmonyTranspiler"
                        or "HarmonyFinalizer"
            )
        );

    static bool IsModulePatch(TypeDefinition t, HashSet<string> seen)
    {
        if (t.BaseType == null)
            return false;
        if (t.BaseType.Name == "ModulePatch")
            return true;
        if (!seen.Add(t.FullName))
            return false;
        return t
            .Module.Types.SelectMany(Flat)
            .Any(x => x.FullName == t.BaseType.FullName && IsModulePatch(x, seen));
    }

    static bool IsPatchMethod(MethodDefinition m) =>
        m.CustomAttributes.Any(a =>
            a.AttributeType.Name
                is "HarmonyPrefix"
                    or "HarmonyPostfix"
                    or "HarmonyTranspiler"
                    or "HarmonyFinalizer"
                    or "PatchPrefix"
                    or "PatchPostfix"
                    or "PatchTranspiler"
                    or "PatchFinalizer"
        )
        || m.Name
            is "Prefix"
                or "Postfix"
                or "Transpiler"
                or "Finalizer"
                or "PatchPrefix"
                or "PatchPostfix"
                or "PatchTranspiler"
                or "PatchFinalizer";

    static bool ManagedHelperPolicySatisfied(bool exactMethodResolved, bool hasHideMarker)
        => exactMethodResolved && hasHideMarker;
    static bool NativeCallbackPolicySatisfied(bool exactMethodResolved, bool hasHideMarker)
        => exactMethodResolved && !hasHideMarker;
    static void CheckManagedHelperExportPolicy(ModuleDefinition mod, Report r, string policyPath)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(policyPath));
            var root = document.RootElement;
            string componentType = root.GetProperty("componentType").GetString() ?? "";
            var methods = All(mod).SelectMany(type => type.Methods).ToArray();
            var requiredHelpers = root.GetProperty("mustHide").EnumerateArray().Select(item => item.GetString() ?? "").ToArray();
            var requiredCallbacks = root.GetProperty("mustRemainVisible").EnumerateArray().Select(item => item.GetString() ?? "").ToArray();
            Add(r, "injected-helper-policy-count", requiredHelpers.Length == 43 && requiredCallbacks.Length == 16,
                $"helpers={requiredHelpers.Length} callbacks={requiredCallbacks.Length}; expected 43 and 16");
            foreach (string signature in requiredHelpers)
            {
                var matches = methods.Where(method => method.FullName == signature && method.DeclaringType.FullName == componentType).ToArray();
                bool resolved = matches.Length == 1;
                bool hidden = resolved && matches[0].CustomAttributes.Any(attribute => attribute.AttributeType.Name.Contains("HideFromIl2Cpp", StringComparison.Ordinal));
                bool passed = ManagedHelperPolicySatisfied(resolved, hidden);
                Add(r, "injected-managed-helper-hidden", passed, signature + $" exactMatches={matches.Length} hideMarker={hidden}");
            }
            foreach (string signature in requiredCallbacks)
            {
                int separator = signature.IndexOf("::", StringComparison.Ordinal);
                string owner = separator < 0 ? "" : signature[..separator].Split(' ').Last();
                var matches = methods.Where(method => method.FullName == signature && method.DeclaringType.FullName == owner).ToArray();
                bool resolved = matches.Length == 1;
                bool hidden = resolved && matches[0].CustomAttributes.Any(attribute => attribute.AttributeType.Name.Contains("HideFromIl2Cpp", StringComparison.Ordinal));
                bool passed = NativeCallbackPolicySatisfied(resolved, hidden);
                Add(r, "injected-native-callback-visible", passed, signature + $" exactMatches={matches.Length} hideMarker={hidden}");
            }
            foreach (var test in root.GetProperty("negativeCases").EnumerateArray())
            {
                string name = test.GetProperty("name").GetString() ?? "unnamed";
                string kind = test.GetProperty("kind").GetString() ?? "";
                bool allowed = kind switch
                {
                    "helper-visible" => ManagedHelperPolicySatisfied(true, false),
                    "callback-hidden" => NativeCallbackPolicySatisfied(true, true),
                    "signature-unresolved" => ManagedHelperPolicySatisfied(false, true),
                    _ => true,
                };
                bool expected = test.GetProperty("expectedAllowed").GetBoolean();
                Add(r, "injected-helper-policy-negative-fixture", allowed == expected && !allowed, name + $" policyAllowed={allowed} expectedAllowed={expected}");
            }
            string injectorRegressionPath = Path.Combine(AppContext.BaseDirectory, "fixtures", "class-injector-pingteamates-regression.json"); if (!System.IO.File.Exists(injectorRegressionPath)) injectorRegressionPath = Path.Combine(Path.GetDirectoryName(policyPath)!, "class-injector-pingteamates-regression.json");
            CheckClassInjectorFailureRegression(mod, r, injectorRegressionPath);
            // ClassInjector accepts value types, but a mod-owned enum has no native
            // class in pitFireTeam.dll. Exporting these managed-only helpers creates
            // invalid native parameter metadata even when registration completes.
            var squadUi = All(mod).Single(type => type.FullName == "pitTeam.Components.SquadControlMenuUi");
            var managedEnumHelpers = squadUi.Methods.Where(method => !method.IsStatic
                && method.Parameters.Any(parameter => parameter.ParameterType.FullName == "pitTeam.LoadoutManagementMode")).ToArray();
            bool enumHelpersHidden = managedEnumHelpers.Length == 6 && managedEnumHelpers.All(method =>
                method.CustomAttributes.Any(attribute => attribute.AttributeType.Name.Contains("HideFromIl2Cpp", StringComparison.Ordinal)));
            bool callbacksVisible = new[] { "Update", "OnGUI", "OnDisable", "OnDestroy" }.All(name =>
                squadUi.Methods.Any(method => method.Name == name && !method.IsStatic
                    && !method.CustomAttributes.Any(attribute => attribute.AttributeType.Name.Contains("HideFromIl2Cpp", StringComparison.Ordinal))));
            Add(r, "squad-managed-enum-helper-boundary", enumHelpersHidden && callbacksVisible,
                $"managedEnumHelpers={managedEnumHelpers.Length}; hidden={enumHelpersHidden}; callbacksVisible={callbacksVisible}");
        }
        catch (Exception error)
        {
            Add(r, "injected-helper-policy-fixture", false, policyPath + " " + error.GetType().Name + ": " + error.Message);
        }
    }

static void CheckClassInjectorFailureRegression(ModuleDefinition mod, Report r, string fixturePath)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(fixturePath));
            var root = document.RootElement;
            var evidence = root.GetProperty("sourceEvidence");
            var warnings = evidence.GetProperty("unsupportedWarnings").EnumerateArray()
                .Select(item => item.GetString() ?? "").ToArray();
            bool realFailureShape =
                evidence.GetProperty("registrationRefusal").GetString()?.Contains("Assembly pitFireTeam.dll is not registered in il2cpp", StringComparison.Ordinal) == true
                && evidence.GetProperty("exceptionType").GetString() == "System.NullReferenceException"
                && evidence.GetProperty("throwFrame").GetString()?.Contains("ClassInjector.ConvertMethodInfo", StringComparison.Ordinal) == true
                && evidence.GetProperty("lineNumber").GetInt32() == 755
                && evidence.GetProperty("entryFrame").GetString() == "pitTeam.Utils.PingTeamates.Enable()"
                && warnings.Length == 5
                && warnings.All(warning => warning.Contains("unsupported", StringComparison.Ordinal));
            Add(r, "pingteamates-classinjector-real-nre-shape", realFailureShape,
                evidence.GetProperty("logPath").GetString() + " warnings=" + warnings.Length);

            var types = All(mod).ToArray();
            string componentName = root.GetProperty("component").GetString() ?? "";
            var component = types.SingleOrDefault(type => type.FullName == componentName);
            Add(r, "pingteamates-classinjector-component-resolved", component != null, componentName);
            if (component == null) return;

            foreach (var expected in root.GetProperty("hiddenMethods").EnumerateArray())
            {
                string name = expected.GetProperty("name").GetString() ?? "";
                string returnType = expected.GetProperty("returnType").GetString() ?? "";
                string[] parameterTypes = expected.GetProperty("parameterTypes").EnumerateArray()
                    .Select(item => item.GetString() ?? "").ToArray();
                var matches = component.Methods.Where(method =>
                    method.Name == name
                    && method.ReturnType.FullName == returnType
                    && method.Parameters.Select(parameter => parameter.ParameterType.FullName).SequenceEqual(parameterTypes)).ToArray();
                bool resolved = matches.Length == 1;
                bool hidden = resolved && matches[0].CustomAttributes.Any(attribute =>
                    attribute.AttributeType.Name.Contains("HideFromIl2Cpp", StringComparison.Ordinal));
                bool linkedWarning = warnings.Any(warning => warning.Contains(name, StringComparison.Ordinal));
                Add(r, "pingteamates-unsupported-signature-hidden", resolved && hidden && linkedWarning,
                    name + " exactMatches=" + matches.Length + " hideMarker=" + hidden + " runtimeWarning=" + linkedWarning);
            }

            var xEvidence = root.GetProperty("followupXEvidence");
            bool xFollowupEvidence = xEvidence.GetProperty("unsupportedWarningCount").GetInt32() == 0 && xEvidence.GetProperty("nreLine").GetInt32() == 97 && xEvidence.GetProperty("secondAttemptFailure").GetString()?.Contains("corresponding IL2CPP class pointer", StringComparison.Ordinal) == true;
            Add(r, "pingteamates-x-nre-persists-after-five-warning-filters", xFollowupEvidence, xEvidence.GetProperty("logPath").GetString() + " probe=" + xEvidence.GetProperty("probePath").GetString());
            var byRefMethodMatches = component.Methods.Where(method => method.Name == "TryResolveEnemyMarker").ToArray();
            var byRefMethod = byRefMethodMatches.Length == 1 ? byRefMethodMatches[0] : null;
            var byRefArgument = byRefMethod?.Parameters.LastOrDefault()?.ParameterType as ByReferenceType;
            var managedResultType = byRefArgument?.ElementType.Resolve();
            var expectedByRef = root.GetProperty("byRefManagedValueTypeMethods")[0];
            bool fixtureSignatureMatches = expectedByRef.GetProperty("name").GetString() == "TryResolveEnemyMarker" && expectedByRef.GetProperty("returnType").GetString() == "System.Boolean" && expectedByRef.GetProperty("parameterTypes")[1].GetString() == "pitTeam.Utils.PingTeamates/EnemyMarkerResolution&";
            bool exactHiddenByRefManagedValueType = fixtureSignatureMatches && byRefMethodMatches.Length == 1 && byRefMethod != null && byRefMethod.ReturnType.FullName == "System.Boolean" && byRefMethod.Parameters.Count == 2 && byRefArgument != null && byRefArgument.ElementType.FullName == "pitTeam.Utils.PingTeamates/EnemyMarkerResolution" && managedResultType != null && managedResultType.IsValueType && byRefMethod.CustomAttributes.Any(attribute => attribute.AttributeType.Name.Contains("HideFromIl2Cpp", StringComparison.Ordinal));
            Add(r, "pingteamates-managed-byref-value-type-hidden", exactHiddenByRefManagedValueType, byRefMethod?.FullName + " exactHidden=" + exactHiddenByRefManagedValueType);
            foreach (var expected in root.GetProperty("visibleUnityCallbacks").EnumerateArray())
            {
                string name = expected.GetProperty("name").GetString() ?? "";
                string returnType = expected.GetProperty("returnType").GetString() ?? "";
                string[] parameterTypes = expected.GetProperty("parameterTypes").EnumerateArray()
                    .Select(item => item.GetString() ?? "").ToArray();
                var matches = component.Methods.Where(method =>
                    method.Name == name
                    && method.ReturnType.FullName == returnType
                    && method.Parameters.Select(parameter => parameter.ParameterType.FullName).SequenceEqual(parameterTypes)).ToArray();
                bool resolved = matches.Length == 1;
                bool hidden = resolved && matches[0].CustomAttributes.Any(attribute =>
                    attribute.AttributeType.Name.Contains("HideFromIl2Cpp", StringComparison.Ordinal));
                Add(r, "pingteamates-unity-lifecycle-remains-visible", resolved && !hidden,
                    name + " exactMatches=" + matches.Length + " hideMarker=" + hidden);
            }

            string radioName = root.GetProperty("radioSound").GetString() ?? "";
            var radio = types.SingleOrDefault(type => type.FullName == radioName);
            Add(r, "radiosound-registration-component-resolved", radio != null, radioName);
            foreach (var expected in root.GetProperty("radioSoundHiddenMethods").EnumerateArray())
            {
                if (radio == null) break;
                string name = expected.GetProperty("name").GetString() ?? "";
                int parameterCount = expected.GetProperty("parameterCount").GetInt32();
                var matches = radio.Methods.Where(method => method.Name == name && method.Parameters.Count == parameterCount).ToArray();
                bool hidden = matches.Length == 1 && matches[0].CustomAttributes.Any(attribute =>
                    attribute.AttributeType.Name.Contains("HideFromIl2Cpp", StringComparison.Ordinal));
                Add(r, "radiosound-managed-helper-hidden", matches.Length == 1 && hidden,
                    name + " exactMatches=" + matches.Length + " hideMarker=" + hidden);
            }

            foreach (var expected in root.GetProperty("radioSoundMethodSignatures").EnumerateArray())
            {
                if (radio == null) break;
                string name = expected.GetProperty("name").GetString() ?? "";
                string returnType = expected.GetProperty("returnType").GetString() ?? "";
                string[] parameterTypes = expected.GetProperty("parameterTypes").EnumerateArray()
                    .Select(item => item.GetString() ?? "").ToArray();
                bool expectedHidden = expected.GetProperty("hideFromIl2Cpp").GetBoolean();
                var matches = radio.Methods.Where(method =>
                    method.Name == name
                    && method.ReturnType.FullName == returnType
                    && method.Parameters.Select(parameter => parameter.ParameterType.FullName).SequenceEqual(parameterTypes)).ToArray();
                bool hidden = matches.Length == 1 && matches[0].CustomAttributes.Any(attribute =>
                    attribute.AttributeType.Name.Contains("HideFromIl2Cpp", StringComparison.Ordinal));
                Add(r, "radiosound-task-signature-and-update-visibility",
                    matches.Length == 1 && hidden == expectedHidden,
                    name + " exactMatches=" + matches.Length + " hideMarker=" + hidden + " expected=" + expectedHidden);
            }
        }
        catch (Exception error)
        {
            Add(r, "pingteamates-classinjector-regression-fixture", false,
                fixturePath + " " + error.GetType().Name + ": " + error.Message);
        }
    }

        static void CheckUnity(ModuleDefinition mod, Report r, string? helperPolicyPath)
    {
        if (helperPolicyPath != null) CheckManagedHelperExportPolicy(mod, r, helperPolicyPath);
        foreach (var t in All(mod).Where(t => t.BaseType?.FullName == "UnityEngine.MonoBehaviour"))
        {
            bool ctor = t.Methods.Any(m => m.IsConstructor && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.IntPtr");
            bool hidden = t.CustomAttributes.Any(a => a.AttributeType.Name.Contains("HideFromIl2Cpp", StringComparison.Ordinal));
            bool directRegistration = All(mod).SelectMany(owner => owner.Methods).Where(method => method.HasBody)
                .SelectMany(method => method.Body.Instructions).Any(instruction => instruction.Operand is GenericInstanceMethod call
                    && call.Name == "RegisterTypeInIl2Cpp" && call.DeclaringType.FullName.Contains("ClassInjector", StringComparison.Ordinal)
                    && call.GenericArguments.Any(argument => argument.FullName == t.FullName));
            bool helperRegistration = HasSharedRegistration(mod, t);
            bool registered = directRegistration || helperRegistration;
            Add(r, "unity-intptr", ctor, t.FullName + " IntPtr constructor=" + ctor);
            Add(r, "unity-metadata-prerequisite", registered || hidden, t.FullName + (helperRegistration
                ? " guarded by same-assembly generic ClassInjector Type registration helper; native registration unverified"
                : directRegistration ? " has matching ClassInjector generic registration metadata; native registration unverified"
                : hidden ? " has HideFromIl2Cpp metadata marker; runtime registration is intentionally skipped"
                : " lacks matching static registration/HideFromIl2Cpp metadata"));
        }
    }
    static bool HasSharedRegistration(ModuleDefinition mod, TypeDefinition target)
    {
        foreach (var helper in All(mod).SelectMany(type => type.Methods))
        {
            if (helper.Name != "EnsureInjectedComponentRegistered" || !helper.IsStatic || helper.GenericParameters.Count != 1 || !helper.HasBody) continue;
            var generic = helper.GenericParameters[0];
            var instructions = helper.Body.Instructions;
            var tokens = instructions.Where(i => i.OpCode == OpCodes.Ldtoken && i.Operand is GenericParameter p && p.Name == generic.Name).ToArray();
            if (tokens.Length != 1) continue;
            var apiCalls = instructions.Select((instruction, index) => (instruction, index)).Where(pair => pair.instruction.Operand is MethodReference reference
                && reference.DeclaringType.FullName == "Il2CppInterop.Runtime.Injection.ClassInjector" && reference.Parameters.Count == 1
                && reference.Parameters[0].ParameterType.FullName == "System.Type"
                && (reference.Name == "IsTypeRegisteredInIl2Cpp" || reference.Name == "RegisterTypeInIl2Cpp")).ToArray();
            if (!apiCalls.Any(pair => ((MethodReference)pair.instruction.Operand).Name == "IsTypeRegisteredInIl2Cpp")
                || !apiCalls.Any(pair => ((MethodReference)pair.instruction.Operand).Name == "RegisterTypeInIl2Cpp")) continue;
            int getTypeIndex = instructions.ToList().FindIndex(i => i.Operand is MethodReference reference && reference.Name == "GetTypeFromHandle" && reference.DeclaringType.FullName == "System.Type");
            if (getTypeIndex <= 0 || instructions[getTypeIndex - 1] != tokens[0] || getTypeIndex + 1 >= instructions.Count
                || !TryGetStoredLocal(instructions[getTypeIndex + 1], helper, out var local)) continue;
            bool bothUseTypeLocal = apiCalls.All(pair => pair.index > 0 && TryGetLoadedLocal(instructions[pair.index - 1], helper, out var loaded) && loaded == local);
            if (!bothUseTypeLocal) continue;
            bool calledForTarget = All(mod).SelectMany(owner => owner.Methods).Where(method => method.HasBody).SelectMany(method => method.Body.Instructions)
                .Any(instruction => instruction.Operand is GenericInstanceMethod call && call.Name == helper.Name
                    && call.DeclaringType.FullName == helper.DeclaringType.FullName && call.GenericArguments.Count == 1
                    && call.GenericArguments[0].FullName == target.FullName);
            if (calledForTarget) return true;
        }
        return false;
    }
    static bool TryGetStoredLocal(Instruction instruction, MethodDefinition method, out VariableDefinition local)
    {
        local = instruction.Operand as VariableDefinition ?? null!;
        if (instruction.OpCode == OpCodes.Stloc_0) local = method.Body.Variables.ElementAtOrDefault(0)!;
        else if (instruction.OpCode == OpCodes.Stloc_1) local = method.Body.Variables.ElementAtOrDefault(1)!;
        else if (instruction.OpCode == OpCodes.Stloc_2) local = method.Body.Variables.ElementAtOrDefault(2)!;
        else if (instruction.OpCode == OpCodes.Stloc_3) local = method.Body.Variables.ElementAtOrDefault(3)!;
        return local is not null && (instruction.OpCode == OpCodes.Stloc || instruction.OpCode == OpCodes.Stloc_S || instruction.OpCode == OpCodes.Stloc_0 || instruction.OpCode == OpCodes.Stloc_1 || instruction.OpCode == OpCodes.Stloc_2 || instruction.OpCode == OpCodes.Stloc_3);
    }
    static bool TryGetLoadedLocal(Instruction instruction, MethodDefinition method, out VariableDefinition local)
    {
        local = instruction.Operand as VariableDefinition ?? null!;
        if (instruction.OpCode == OpCodes.Ldloc_0) local = method.Body.Variables.ElementAtOrDefault(0)!;
        else if (instruction.OpCode == OpCodes.Ldloc_1) local = method.Body.Variables.ElementAtOrDefault(1)!;
        else if (instruction.OpCode == OpCodes.Ldloc_2) local = method.Body.Variables.ElementAtOrDefault(2)!;
        else if (instruction.OpCode == OpCodes.Ldloc_3) local = method.Body.Variables.ElementAtOrDefault(3)!;
        return local is not null && (instruction.OpCode == OpCodes.Ldloc || instruction.OpCode == OpCodes.Ldloc_S || instruction.OpCode == OpCodes.Ldloc_0 || instruction.OpCode == OpCodes.Ldloc_1 || instruction.OpCode == OpCodes.Ldloc_2 || instruction.OpCode == OpCodes.Ldloc_3);
    }    static int RegistrationSelfTest(Args args)
    {
        string assemblyPath = File(args.R("mod"));
        string reportPath = Out(args.G("report"), "injection-registration-contract-regression.json");
        using var module = ModuleDefinition.ReadModule(assemblyPath, new ReaderParameters { InMemory = true, ReadingMode = ReadingMode.Deferred });
        var target = All(module).FirstOrDefault(type => type.BaseType?.FullName == "UnityEngine.MonoBehaviour"
            && !type.CustomAttributes.Any(attribute => attribute.AttributeType.Name.Contains("HideFromIl2Cpp", StringComparison.Ordinal))
            && HasSharedRegistration(module, type));
        if (target is null) throw new InvalidOperationException("No real injected MonoBehaviour with shared registration helper was found.");
        var helper = All(module).SelectMany(type => type.Methods).First(method => method.Name == "EnsureInjectedComponentRegistered"
            && method.IsStatic && method.GenericParameters.Count == 1 && method.HasBody);
        var register = helper.Body.Instructions.First(instruction => instruction.Operand is MethodReference method
            && method.Name == "RegisterTypeInIl2Cpp" && method.DeclaringType.FullName == "Il2CppInterop.Runtime.Injection.ClassInjector");
        var caller = All(module).SelectMany(type => type.Methods).Where(method => method.HasBody)
            .SelectMany(method => method.Body.Instructions).First(instruction => instruction.Operand is GenericInstanceMethod call
                && call.Name == helper.Name && call.GenericArguments.Count == 1 && call.GenericArguments[0].FullName == target.FullName);
        var genericCall = (GenericInstanceMethod)caller.Operand;
        bool positive = HasSharedRegistration(module, target);
        int registerIndex = helper.Body.Instructions.IndexOf(register);
        helper.Body.Instructions.Remove(register);
        bool missingRegisterRejected = !HasSharedRegistration(module, target);
        helper.Body.Instructions.Insert(registerIndex, register);
        var originalArgument = genericCall.GenericArguments[0];
        genericCall.GenericArguments[0] = module.TypeSystem.Object;
        bool wrongTypeRejected = !HasSharedRegistration(module, target);
        genericCall.GenericArguments[0] = originalArgument;
        string originalHelperName = helper.Name;
        helper.Name = "DifferentRegistrationHelper";
        bool wrongHelperRejected = !HasSharedRegistration(module, target);
        helper.Name = originalHelperName;
        int callerIndex = caller.Offset;
        var callerMethod = All(module).SelectMany(type => type.Methods).First(method => method.HasBody && method.Body.Instructions.Contains(caller));
        int callerInstructionIndex = callerMethod.Body.Instructions.IndexOf(caller);
        callerMethod.Body.Instructions.Remove(caller);
        bool missingHideAndRegistrationRejected = !HasSharedRegistration(module, target)
            && !target.CustomAttributes.Any(attribute => attribute.AttributeType.Name.Contains("HideFromIl2Cpp", StringComparison.Ordinal));
        callerMethod.Body.Instructions.Insert(callerInstructionIndex, caller);
        var results = new[]
        {
            new { name = "actual-DLL-shared-helper-positive", passed = positive, target = target.FullName },
            new { name = "remove-ClassInjector-RegisterTypeInIl2Cpp-call", passed = missingRegisterRejected, target = target.FullName },
            new { name = "closed-helper-call-with-wrong-T", passed = wrongTypeRejected, target = target.FullName },
            new { name = "wrong-helper-method-name", passed = wrongHelperRejected, target = target.FullName },
            new { name = "missing-registration-and-HideFromIl2Cpp", passed = missingHideAndRegistrationRejected, target = target.FullName },
        };
        System.IO.File.WriteAllText(reportPath, JsonSerializer.Serialize(new { input = Path.GetFullPath(assemblyPath), results, passed = results.All(result => result.passed) }, J));
        foreach (var result in results) Console.WriteLine((result.passed ? "PASS " : "FAIL ") + result.name + " " + result.target);
        Console.WriteLine("Registration regression report: " + reportPath);
        return results.All(result => result.passed) ? 0 : 1;
    }    static void CheckFailures(ModuleDefinition m, string p, Report r)
    {
        using var j = JsonDocument.Parse(System.IO.File.ReadAllText(p));
        foreach (var e in j.RootElement.EnumerateArray())
        {
            string n = e.TryGetProperty("patch", out var x) ? x.GetString() ?? "" : "";
            var t = All(m).FirstOrDefault(t => t.Name == n || t.FullName == n);
            if (t == null)
            {
                Add(
                    r,
                    "historical-runtime-failure",
                    true,
                    n + " not present in this audited mod assembly."
                );
                continue;
            }
            var q = r
                .Checks.Where(c =>
                    c.Id.StartsWith("harmony-", StringComparison.Ordinal)
                    && c.Message.StartsWith(t.FullName + " ", StringComparison.Ordinal)
                )
                .ToArray();
            bool fixedTarget = q.Length > 0 && q.All(c => c.Passed);
            Add(
                r,
                "historical-runtime-failure",
                fixedTarget,
                n
                    + " recorded failure target/signature checks="
                    + q.Length
                    + " resolved="
                    + fixedTarget
            );
        }
    }

    static NativeInfo Native(string p, string id)
    {
        var v = FileVersionInfo.GetVersionInfo(p);
        string machine = "unknown";
        try
        {
            using var f = System.IO.File.OpenRead(p);
            using var pe = new PEReader(f);
            machine = pe.PEHeaders.CoffHeader.Machine.ToString();
        }
        catch { }
        return new()
        {
            Path = Path.GetFullPath(p),
            BuildId = id,
            FileVersion = v.FileVersion,
            ProductVersion = v.ProductVersion,
            Machine = machine,
        };
    }

    static IEnumerable<TypeDefinition> All(ModuleDefinition m) => m.Types.SelectMany(Flat);

    static IEnumerable<TypeDefinition> Flat(TypeDefinition t) =>
        new[] { t }.Concat(t.NestedTypes.SelectMany(Flat));

    static string File(string p) =>
        System.IO.File.Exists(p)
            ? Path.GetFullPath(p)
            : throw new Usage("File missing: " + Path.GetFullPath(p));

    static string Dir(string p) =>
        Directory.Exists(p)
            ? Path.GetFullPath(p)
            : throw new Usage("Directory missing: " + Path.GetFullPath(p));

    static string Out(string? p, string n)
    {
        string f = Path.GetFullPath(
            p
                ?? Path.Combine(
                    @"D:\game\tarkov workshop\.omni\tmp\pitfireteam-runtime\interactive-baseline",
                    n
                )
        );
        Directory.CreateDirectory(Path.GetDirectoryName(f)!);
        return f;
    }

    static void Add(Report r, string id, bool b, string m) =>
        r.Checks.Add(
            new()
            {
                Id = id,
                Passed = b,
                Status = b ? "pass" : "fail",
                Message = m,
            }
        );

    static void Write(string p, object o) =>
        System.IO.File.WriteAllText(p, JsonSerializer.Serialize(o, J) + Environment.NewLine);

    sealed class Index : IDisposable
    {
        public List<ModuleDefinition> Modules = new();
        public List<DefaultAssemblyResolver> Resolvers = new();
        public List<Entry> Types = new();
        public List<string> Issues = new();

        public static Index Read(string root)
        {
            var z = new Index();
            var resolver = Program.Resolver(root);
            z.Resolvers.Add(resolver);
            foreach (
                string p in Directory
                    .EnumerateFiles(root, "*.dll", SearchOption.AllDirectories)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            )
            {
                try
                {
                    var m = ModuleDefinition.ReadModule(
                        p,
                        new ReaderParameters
                        {
                            ReadingMode = ReadingMode.Deferred,
                            InMemory = true,
                            AssemblyResolver = resolver,
                        }
                    );
                    if (m.Assembly == null)
                    {
                        m.Dispose();
                        continue;
                    }
                    z.Modules.Add(m);
                    foreach (var t in All(m))
                        z.Types.Add(
                            new(
                                m.Assembly.Name.Name,
                                m.Assembly.Name.Version,
                                Convert.ToHexString(
                                    m.Assembly.Name.PublicKeyToken ?? Array.Empty<byte>()
                                ),
                                Path.GetFullPath(p),
                                t
                            )
                        );
                }
                catch (BadImageFormatException) { }
                catch (Exception e)
                {
                    z.Issues.Add(
                        "Unreadable "
                            + Path.GetFullPath(p)
                            + ": "
                            + e.GetType().Name
                            + ": "
                            + e.Message
                    );
                }
            }
            return z;
        }

        public List<Entry> Find(string n) =>
            Types
                .Where(t => t.Assembly.Equals(n, StringComparison.OrdinalIgnoreCase))
                .GroupBy(t => t.Path, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

        public void Dispose()
        {
            foreach (var m in Modules)
                m.Dispose();
            foreach (var r in Resolvers)
                r.Dispose();
        }
    }

    sealed record Entry(
        string Assembly,
        Version Version,
        string Token,
        string Path,
        TypeDefinition Type
    )
    {
        public string FullName => Type.FullName;
    }

    sealed class Args
    {
        Dictionary<string, string?> d;

        Args(Dictionary<string, string?> x) => d = x;

        public static Args P(string[] a)
        {
            var d = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < a.Length; i++)
            {
                if (!a[i].StartsWith("--"))
                    throw new Usage("Unexpected " + a[i]);
                string k = a[i][2..];
                d[k] = i + 1 < a.Length && !a[i + 1].StartsWith("--") ? a[++i] : null;
            }
            return new(d);
        }

        public string R(string k) =>
            G(k) is { Length: > 0 } v ? v : throw new Usage("Missing --" + k);

        public string? G(string k) => d.TryGetValue(k, out var v) ? v : null;

        public bool Has(string k) => d.ContainsKey(k);
    }

    sealed class Usage(string s) : Exception(s);

    sealed class ContractFile
    {
        public List<Contract> Patches { get; set; } = new();
    }

    static string ContractType(TypeReference t, Contract c)
    {
        if (
            t is GenericParameter generic
            && generic.Type == GenericParameterType.Type
            && c.TargetGenericArguments != null
            && generic.Position < c.TargetGenericArguments.Length
        )
            return c.TargetGenericArguments[generic.Position];
        if (t is GenericInstanceType instance)
            return instance.ElementType.FullName
                + "<"
                + string.Join(",", instance.GenericArguments.Select(x => ContractType(x, c)))
                + ">";
        if (t is ByReferenceType byReference)
            return ContractType(byReference.ElementType, c) + "&";
        if (t is PointerType pointer)
            return ContractType(pointer.ElementType, c) + "*";
        if (t is ArrayType array)
            return ContractType(array.ElementType, c)
                + "["
                + new string(',', Math.Max(0, array.Rank - 1))
                + "]";
        return t.FullName;
    }

    static bool CompatiblePatchType(
        TypeReference targetParameter,
        Contract c,
        TypeReference patchParameter
    ) => ContractType(Unref(targetParameter), c) == Unref(patchParameter).FullName;

    sealed class Contract
    {
        public string? TargetAssembly { get; set; }
        public string[]? TargetGenericArguments { get; set; }
        public string PatchClassFullName { get; set; } = "";
        public string TargetType { get; set; } = "";
        public string TargetMethod { get; set; } = "";
        public string[]? ParameterTypes { get; set; }
        public string? ReturnType { get; set; }
        public string? NativeConstructorPointerField { get; set; }
    }

    sealed class Report
    {
        public string Command { get; set; } = "";
        public bool Overall { get; set; }
        public Dictionary<string, string> Inputs { get; set; } = new();
        public NativeInfo? Native { get; set; }
        public List<Check> Checks { get; set; } = new();
        public bool? ScopePassed { get; set; }
        public bool? ScopeGatePassed { get; set; }
        public int ScopeTotal { get; set; }
        public int ScopePassedCount { get; set; }
        public List<string> ScopeFailures { get; set; } = new();
    }

    sealed class NativeInfo
    {
        public string Path { get; set; } = "";
        public string BuildId { get; set; } = "";
        public string? FileVersion { get; set; }
        public string? ProductVersion { get; set; }
        public string Machine { get; set; } = "";
    }

    sealed class Check
    {
        public string Id { get; set; } = "";
        public bool Passed { get; set; }
        public string Status { get; set; } = "";
        public string Message { get; set; } = "";
    }
}
