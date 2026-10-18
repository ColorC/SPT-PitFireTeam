using System.Text.Json;
using pitTeam.Compat;
using Mono.Cecil;
internal static class NormalSquadPipelineProbeChecks
{
 public static int Run(string[] args)
 {
  string root=args.Length>0?args[0]:throw new ArgumentException("source root required");
  using var j=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"fixtures","normal-squad-pipeline-probe-cases.json")));
  var x=j.RootElement; var b=x.GetProperty("baseline"); var c=x.GetProperty("cases").EnumerateArray().ToArray();
  var bodyLoot=x.GetProperty("bodyLootRootCasting"); bool bodyLootEvidence=CheckBodyLootRootEvidence(bodyLoot,out bool bodyLootMetadata,out bool bodyLootOldCast,out string bodyLootDetail);
  string bodyLootSourceText=File.ReadAllText(Path.Combine(root,"client","BigBrain","Actions","GestureCommandAction.BodyLoot.cs")); int bodyLootStart=bodyLootSourceText.IndexOf("private bool TryGetBodyLootExecutionContext(",StringComparison.Ordinal); int bodyLootEnd=bodyLootStart<0?-1:bodyLootSourceText.IndexOf("private ",bodyLootStart+20,StringComparison.Ordinal); string bodyLootMethod=bodyLootStart>=0&&bodyLootEnd>bodyLootStart?bodyLootSourceText.Substring(bodyLootStart,bodyLootEnd-bodyLootStart):""; bool bodyLootSource=bodyLootMethod.Contains("CastNativeOrNull<InventoryEquipment>",StringComparison.Ordinal)&&!bodyLootMethod.Contains("as InventoryEquipment",StringComparison.Ordinal)&&!bodyLootMethod.Contains("is InventoryEquipment",StringComparison.Ordinal); string bodyLootCompiledPath=Path.Combine(root,"client","bin","Release","net6.0","pitFireTeam.dll"); bool bodyLootCompiled=CheckCompiledBodyLoot(bodyLootCompiledPath,out string bodyLootCompiledDetail); bool bodyLootFixture=bodyLoot.GetProperty("syntheticCases").EnumerateArray().Any(v=>v.GetProperty("name").GetString()=="null-item-owner-or-root"&&v.GetProperty("expectedEquipment").ValueKind==JsonValueKind.Null)&&bodyLoot.GetProperty("syntheticCases").EnumerateArray().Any(v=>v.GetProperty("name").GetString()=="base-item-wrapper-native-inventory-equipment"&&v.GetProperty("expectedResolve").GetBoolean());
  bool quickPanelCasting=CheckQuickPanelNativeCasts(root,out string quickPanelDetail);
  using var actualEvidence=JsonDocument.Parse(File.ReadAllText(x.GetProperty("evidencePath").GetString()!));
  using var afEvidence=JsonDocument.Parse(File.ReadAllText(x.GetProperty("afEvidencePath").GetString()!));
  var n=actualEvidence.RootElement.GetProperty("result").GetProperty("normalSquadPipeline");
  bool realAe=n.GetProperty("normalBossLifecycle").GetProperty("registrationSuccesses").GetInt32()==1&&n.GetProperty("bossPlayers").GetProperty("count").GetInt32()==1&&n.GetProperty("spawnMemberIds").GetProperty("count").GetInt32()==1&&!n.TryGetProperty("spawnAction",out _)&&!n.TryGetProperty("followerPipeline",out _);
  var af=afEvidence.RootElement.GetProperty("result").GetProperty("normalSquadPipeline");
  bool realAf=af.GetProperty("normalBossLifecycle").GetProperty("gameWorldStartedCallbacks").GetInt32()==1&&af.GetProperty("normalBossLifecycle").GetProperty("registrationSuccesses").GetInt32()==1&&af.GetProperty("normalBossLifecycle").GetProperty("pairCount").GetInt32()==1&&af.GetProperty("spawnAction").GetProperty("callbacks").GetInt32()==0&&af.GetProperty("spawnAction").GetProperty("spawnFollowersCalls").GetInt32()==0&&af.GetProperty("spawnAction").GetProperty("started").GetInt32()==0&&af.GetProperty("followerPipeline").GetProperty("backendResponses").GetInt32()==1&&af.GetProperty("followerPipeline").GetProperty("profilesDecoded").GetInt32()==1;
  string p=File.ReadAllText(Path.Combine(root,"client","Patches","BotsControllerPatch.cs"));
  string w=File.ReadAllText(Path.Combine(root,"client","Compat","Spt5RuntimeWorldProbe.cs"));
  string u=File.ReadAllText(Path.Combine(root,"client","Utils","Utils.cs"));
  string m=File.ReadAllText(Path.Combine(root,"client","Patches","FollowerMovementPatch.cs"));
  var timerCases=x.GetProperty("timerNativeCastCases").EnumerateArray().ToArray();
  bool timerFixture=timerCases.Length==2&&timerCases.Any(v=>v.GetProperty("name").GetString()=="ag-null-from-clr-as")&&timerCases.Any(v=>v.GetProperty("name").GetString()=="native-subtype-rejected-explicitly")&&timerCases.All(v=>v.GetProperty("observedFailureShape").GetString()!.Length>0);
  bool timerSource=u.Contains("CreateNativeTimer")&&u.Contains("TryCast<TimerManager.Timer>()")&&u.Contains("StaticManager.Instance is null")&&u.Contains("StaticManager.TimerManager receiver is null")&&u.Contains("MakeTimer returned native ITimer subtype")&&!u.Contains("as TimerManager.Timer")&&u.Contains("CreateNativeTimer(Il2CppSystem.TimeSpan.FromMilliseconds(timer), isLopped, nameof(SetTimeout))")&&u.Contains("CreateNativeTimer(Il2CppSystem.TimeSpan.FromSeconds(seconds), false, nameof(SetBotTimer))");
  bool ae=b.GetProperty("normalBossLifecycleRegistrationSuccesses").GetInt32()==1&&b.GetProperty("spawnMemberIdCount").GetInt32()==1&&b.GetProperty("spawnActionCallbackCount").ValueKind==JsonValueKind.Null;
  bool states=c.Length==4&&c.Any(v=>v.GetProperty("currentStage").GetString()=="spawn-followers-skipped-controller-null")&&c.Any(v=>v.GetProperty("backendRequestsPending").GetInt32()==1)&&c.Any(v=>v.GetProperty("currentStage").GetString()=="decode-profile-response");
  var lifecycle=x.GetProperty("lifecycleCases").EnumerateArray().ToArray();
  bool lifecycleCases=lifecycle.Length==3&&lifecycle.Any(v=>v.GetProperty("name").GetString()=="gameworld-starts-normal-spawn-after-pair-complete"&&v.GetProperty("gameWorldStartedSpawnCalls").GetInt32()==1&&v.GetProperty("legacyCallbacks").GetInt32()==0&&v.GetProperty("startedOrigin").GetString()=="GameWorld.OnGameStarted")&&lifecycle.Any(v=>v.GetProperty("name").GetString()=="late-legacy-action-is-idempotent"&&v.GetProperty("legacyCallbacks").GetInt32()==1&&v.GetProperty("started").GetInt32()==1&&v.GetProperty("skippedAlreadySpawned").GetInt32()==1)&&lifecycle.Any(v=>v.GetProperty("name").GetString()=="gameworld-first-local-task-completes-existing-pair"&&v.GetProperty("duplicateSource").GetString()=="LocalGame.LocalBotsControllerInitialization"&&v.GetProperty("gameWorldStartedSpawnCalls").GetInt32()==1&&v.GetProperty("legacyCallbacks").GetInt32()==0&&v.GetProperty("startedOrigin").GetString()=="GameWorld.OnGameStarted");
  int completeIndex=p.IndexOf("lease.Complete();",StringComparison.Ordinal);
  bool dispatchAfterCompletedPair=completeIndex>=0&&p.IndexOf("TrySpawnFollowersAfterGameWorldStarted(world, controller, player);",completeIndex,StringComparison.Ordinal)>completeIndex;
  int duplicateAt=p.IndexOf("if (alreadyCompleted)",StringComparison.Ordinal);
  int duplicateDispatchAt=duplicateAt<0?-1:p.IndexOf("TrySpawnFollowersAfterGameWorldStarted(world, controller, player);",duplicateAt,StringComparison.Ordinal);
  int duplicateReturnAt=duplicateDispatchAt<0?-1:p.IndexOf("return;",duplicateDispatchAt,StringComparison.Ordinal);
  bool completedDuplicateDispatch=duplicateAt>=0&&duplicateDispatchAt>duplicateAt&&duplicateReturnAt>duplicateDispatchAt&&!p.Substring(duplicateAt,duplicateReturnAt-duplicateAt).Contains("source == \"GameWorld.OnGameStarted\"");
  var enemyTargetCases=x.GetProperty("enemyTargetCases").EnumerateArray().ToArray();
  bool enemyTargetFixtureCases=enemyTargetCases.Length==5&&enemyTargetCases.All(v=>v.GetProperty("synthesized").GetBoolean()&&v.GetProperty("nativeShape").GetString()!.Length>0)&&enemyTargetCases.Any(v=>v.GetProperty("name").GetString()=="goal-enemy-is-main-player"&&v.GetProperty("expectedStatus").GetString()=="current-goal-enemy"&&v.GetProperty("expectedTargetsMainPlayer").GetBoolean())&&enemyTargetCases.Any(v=>v.GetProperty("name").GetString()=="no-goal-enemy"&&v.GetProperty("expectedStatus").GetString()=="no-goal-enemy"&&!v.GetProperty("expectedTargetsMainPlayer").GetBoolean())&&enemyTargetCases.Any(v=>v.GetProperty("name").GetString()=="unsupported-enemy-person"&&v.GetProperty("expectedStatus").GetString()=="unsupported-enemy-person-member"&&v.GetProperty("expectedTargetsMainPlayer").ValueKind==JsonValueKind.Null)&&enemyTargetCases.Any(v=>v.GetProperty("name").GetString()=="have-enemy-true-goal-null"&&v.GetProperty("expectedStatus").GetString()=="goal-enemy-null-have-enemy-true"&&!v.GetProperty("expectedAvailable").GetBoolean()&&v.GetProperty("expectedUnresolved").GetBoolean())&&enemyTargetCases.Any(v=>v.GetProperty("name").GetString()=="actor-read-error-counted-unresolved"&&v.GetProperty("expectedSummaryStatus").GetString()=="partial"&&v.GetProperty("expectedUnresolved").GetBoolean());
  bool enemyTargetProbeSource=w.Contains("CaptureBotEnemyTarget")&&w.Contains("GoalEnemy")&&w.Contains("Person")&&w.Contains("ProfileId")&&w.Contains("enemyPlayerPointer")&&w.Contains("enemyPlayerProfileId")&&w.Contains("targetsMainPlayer")&&w.Contains("mainPlayerTargetingBots = CaptureMainPlayerTargetingBots")&&w.Contains("targetingMainPlayerCount")&&w.Contains("unresolvedBotCount")&&w.Contains("goal-enemy-null-have-enemy-true")&&w.Contains("actor-read-error");
  bool source=p.Contains("GetNormalSpawnActionProbeSnapshot")&&p.Contains("GetNormalFollowerPipelineProbeSnapshot")&&p.Contains("backendRequestsPending")&&p.Contains("RecordNormalFollowerSpawnTask")&&p.Contains("SanitizeBackendAuthority")&&p.Contains("TrySpawnFollowersAfterGameWorldStarted(world, controller, player)")&&p.Contains("SpawnFollowersFromGameWorldStarted")&&p.Contains("NormalGameWorldStartedPointer")&&p.Contains("gameWorldStartedSpawnCalls")&&dispatchAfterCompletedPair&&completedDuplicateDispatch&&w.Contains("CaptureNormalPatchSnapshot(\"pitTeam.Patches.BotsEventsControllerSpawnPatch\"")&&w.Contains("CaptureNormalPatchSnapshot(\"pitTeam.Patches.BotsControllerPatch\"");
  const string oldFailureShape = "private async Task<bool> ActivateBotAtPosition(...) { catch (Exception ex) { return false; } }\npublic async Task SpawnGroupBots(...) {\n try { await ActivateBotAtPosition(...); } catch (Exception ex) { Modules.Logger.LogError(ex); }\n try { botsProfile = await CreateFollowerProfiles(player); } catch (Exception ex) { Modules.Logger.LogError(ex); return; }\n}\nprivate static int ScoreDebugSpawnProfile(...) { }\npublic async Task<bool> SpawnDebugFollower(...) { }";
  bool normalSpawnFailureWiring=IsSpawnGroupFailureWired(p,out string normalSpawnFailureDetail);
  bool oldFailureShapeRejected=!IsSpawnGroupFailureWired(oldFailureShape,out string oldFailureShapeDetail);
  var followerInitCases=x.GetProperty("followerInitializationCases").EnumerateArray().ToArray();
  bool followerInitWiring=IsFollowerInitializationWired(p,followerInitCases,out string followerInitDetail);
  const string oldFollowerInitShape = "public async Task SpawnGroupBots(...) { Action<BotOwner> OnBotState = owner => { try { BossPlayers.AddFollower(owner, player); TrySayFollowerReadyControlled(owner, 2000); } catch (Exception ex) { Logger.LogError(ex); } }; await ActivateBotAtPosition(...); }";
  bool oldFollowerInitShapeRejected=!IsFollowerInitializationWired(oldFollowerInitShape,followerInitCases,out string oldFollowerInitDetail);
  var movementPropertyCases=x.GetProperty("followerMovementPropertyCases").EnumerateArray().ToArray();
  bool movementPropertyWiring=IsFollowerMovementPropertyWired(m,movementPropertyCases,out string movementPropertyDetail);
  var valueResetCases=x.GetProperty("followerActionResultResetCases").EnumerateArray().ToArray();
  string followerSource=File.ReadAllText(Path.Combine(root,"client","Components","BotFollowerPlayer.cs"));
  bool valueResetWired=CheckValueReset(followerSource,valueResetCases,out string valueResetDetail,out bool oldDefaultRejected);
  const string oldFollowerMovementShape="private static readonly FieldInfo PlayerField = AccessTools.Field(typeof(MovementContext), \"_player\"); Player player = PlayerField?.GetValue(__instance.MovementContext) as Player;";
  bool oldFollowerMovementShapeRejected=!IsFollowerMovementPropertyWired(oldFollowerMovementShape,movementPropertyCases,out string oldMovementDetail);
  var nullableCases=x.GetProperty("nativeNullableReturnCases").EnumerateArray().ToArray();
  var nullableResults=nullableCases.Select(v=>new {name=v.GetProperty("name").GetString(),kind=v.GetProperty("kind").GetString(),expected=v.GetProperty("expected").GetString(),actual=NativeNullableReturnPolicy.Classify(v.GetProperty("resultPointer").GetInt64(),v.GetProperty("exceptionPointer").GetInt64(),v.GetProperty("actualClassPointer").GetInt64(),v.GetProperty("expectedClassPointer").GetInt64()).ToString()}).ToArray();
  bool nullablePolicyPassed=nullableResults.Length==6&&nullableResults.All(v=>v.expected==v.actual)&&nullableCases.Any(v=>v.GetProperty("kind").GetString()=="reconstructed"&&!v.GetProperty("rawPointerCaptured").GetBoolean());
  string nullableHelper=File.ReadAllText(Path.Combine(root,"client","Compat","NativeNullableReturn.cs"));
  string coverSource=File.ReadAllText(Path.Combine(root,"client","Utils","Covers.cs"));
  bool nullableNormalEntry=coverSource.Contains("NativeNullableReturn.InvokeNoArgumentNullable<Vector3>")&&!coverSource.Contains("botOwner.Covers.ClosestFriendCoverPoint(),")&&nullableHelper.Contains("NativeNullableReturnPolicy.Classify")&&nullableHelper.Contains("RaiseExceptionIfNecessary(error)")&&nullableHelper.Contains("Il2CppClassPointerStore<T>.NativeClassPtr");
  var coverArgFixture=x.GetProperty("coverSearchNullableArgument");
  bool coverArgFixtureValid=coverArgFixture.GetProperty("name").GetString()=="ap-cover-search-data-nullable-byvalue-arguments"&&coverArgFixture.GetProperty("nullableParameterIndexes").EnumerateArray().Select(v=>v.GetInt32()).SequenceEqual(new[]{7,8,13,14})&&coverArgFixture.GetProperty("nativeErrorShape").GetString()!.Contains("Il2CppObjectBaseToPtrNotNull",StringComparison.Ordinal)&&coverArgFixture.GetProperty("nativeEvidencePath").GetString()!.Length>0;
  bool coverArgMetadata=false; string coverArgMetadataDetail="";
  try { using var coverModule=ModuleDefinition.ReadModule(coverArgFixture.GetProperty("assemblyPath").GetString()!); var coverType=coverModule.Types.SingleOrDefault(t=>t.Name=="CoverSearchData"); var coverCtor=coverType?.Methods.SingleOrDefault(m=>m.IsConstructor&&m.Parameters.Count==16); bool nullableParameters=coverCtor!=null&&coverCtor.Parameters[7].ParameterType.FullName.Contains("Il2CppSystem.Nullable`1<UnityEngine.Vector3>",StringComparison.Ordinal)&&coverCtor.Parameters[8].ParameterType.FullName.Contains("Il2CppSystem.Nullable`1<UnityEngine.Vector3>",StringComparison.Ordinal)&&coverCtor.Parameters[13].ParameterType.FullName.Contains("Il2CppSystem.Nullable`1<System.Int32>",StringComparison.Ordinal)&&coverCtor.Parameters[14].ParameterType.FullName.Contains("Il2CppSystem.Nullable`1<System.Int32>",StringComparison.Ordinal); int notNullCalls=coverCtor?.Body.Instructions.Count(i=>i.Operand is MethodReference mr&&mr.Name=="Il2CppObjectBaseToPtrNotNull")??0; int unboxCalls=coverCtor?.Body.Instructions.Count(i=>i.Operand is MethodReference mr&&mr.Name=="il2cpp_object_unbox")??0; using var nullableModule=ModuleDefinition.ReadModule(coverArgFixture.GetProperty("nullableAssemblyPath").GetString()!); var nullableType=nullableModule.Types.SingleOrDefault(t=>t.FullName=="Il2CppSystem.Nullable`1"); var emptyCtor=nullableType?.Methods.SingleOrDefault(m=>m.IsConstructor&&!m.IsStatic&&!m.HasParameters); bool emptyWrapperAllocatesPointer=emptyCtor!=null&&emptyCtor.Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.Name=="il2cpp_object_new")&&emptyCtor.Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.FullName.Contains("Il2CppSystem.ValueType::.ctor(System.IntPtr)",StringComparison.Ordinal)); coverArgMetadata=nullableParameters&&notNullCalls>=4&&unboxCalls>=4&&emptyWrapperAllocatesPointer; coverArgMetadataDetail="params7_13_nullable="+nullableParameters+" nullableMarshalNotNull="+notNullCalls+" unbox="+unboxCalls+" emptyCtorAllocatesNativeObject="+emptyWrapperAllocatesPointer; } catch(Exception ex){coverArgMetadataDetail=ex.GetType().Name+": "+ex.Message;}
  var coverCtorArgs=ReadCoverSearchDataArguments(coverSource);
  bool coverArgSource=HasValidCoverSearchNullableSlots(coverCtorArgs); bool coverArgFirstSlotNativeReturn=coverCtorArgs.Length==2&&coverCtorArgs[0].Length==16&&coverCtorArgs[0][7].Contains("NativeNullableReturn.InvokeNoArgumentNullable<Vector3>",StringComparison.Ordinal)&&coverCtorArgs[0][7].EndsWith(".ToNativeNullable()",StringComparison.Ordinal);
  bool coverArgRawNullRejected=coverCtorArgs.Length==2;
  for(int callIndex=0;callIndex<coverCtorArgs.Length;callIndex++) foreach(int nullableSlot in new[]{7,8,13,14}) { var replay=coverCtorArgs.Select(args=>args.ToArray()).ToArray(); replay[callIndex][nullableSlot]="null"; if(HasValidCoverSearchNullableSlots(replay)) coverArgRawNullRejected=false; }
  bool coverNullableArgumentPassed=coverArgFixtureValid&&coverArgMetadata&&coverArgSource&&coverArgFirstSlotNativeReturn&&coverArgRawNullRejected;
     var bossProxyCases=x.GetProperty("bossToFollowResolutionCases").EnumerateArray().ToArray(); var anProxyCase=bossProxyCases.Single(v=>v.GetProperty("nativeEvidence").GetBoolean()); var oldProxyCase=bossProxyCases.Single(v=>!v.GetProperty("nativeEvidence").GetBoolean());
   using var anProxyEvidence=JsonDocument.Parse(File.ReadAllText(anProxyCase.GetProperty("evidencePath").GetString()!)); var qaProxy=anProxyEvidence.RootElement.GetProperty("result").GetProperty("players").EnumerateArray().Single(v=>v.GetProperty("profileId").GetString()==anProxyCase.GetProperty("profileId").GetString()); var bossProxy=qaProxy.GetProperty("brain").GetProperty("followerBossState");
   string bossPointer=bossProxy.GetProperty("bossToFollowPointer").GetString()??""; bool actualNativeBossProxy=bossProxy.GetProperty("supported").GetBoolean()&&bossProxy.GetProperty("available").GetBoolean()&&bossProxy.GetProperty("botState").GetString()==anProxyCase.GetProperty("expectedBotState").GetString()&&bossProxy.GetProperty("haveBoss").GetBoolean()&&bossProxy.GetProperty("bossToFollowType").GetString()==anProxyCase.GetProperty("expectedBossToFollowType").GetString()&&bossProxy.GetProperty("registryResolvedType").GetString()==anProxyCase.GetProperty("expectedRegistryResolvedType").GetString()&&!string.IsNullOrEmpty(bossPointer)&&bossPointer!="0";
   const string bossProxyPattern=@"BossToFollow\s+(?:is\s+(?:not\s+)?|as\s+)(?:Components\.)?pitAIBossPlayer"; bool oldProxyGuardRejected=oldProxyCase.GetProperty("expectedDirectProxyCastRejected").GetBoolean()&&System.Text.RegularExpressions.Regex.IsMatch(oldProxyCase.GetProperty("sourceShape").GetString()!,bossProxyPattern);
   string clientRoot=Path.Combine(root,"client"),worldProbePath=Path.GetFullPath(Path.Combine(root,"client","Compat","Spt5RuntimeWorldProbe.cs")),bossRegistryPath=Path.GetFullPath(Path.Combine(root,"client","Modules","BossPlayers.cs")); int directBossProxyCasts=Directory.EnumerateFiles(clientRoot,"*.cs",SearchOption.AllDirectories).Where(f=>!string.Equals(Path.GetFullPath(f),worldProbePath,StringComparison.OrdinalIgnoreCase)&&!string.Equals(Path.GetFullPath(f),bossRegistryPath,StringComparison.OrdinalIgnoreCase)).Sum(f=>System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(f),bossProxyPattern).Count);
   string followActionSource=File.ReadAllText(Path.Combine(clientRoot,"BigBrain","Actions","FollowAction.cs")),bossRegistrySource=File.ReadAllText(bossRegistryPath); bool bossProxyResolverWired=followActionSource.Contains("GetResolvedBossForFollower(BotOwner) is not pitAIBossPlayer boss || boss.realPlayer == null",StringComparison.Ordinal)&&bossRegistrySource.Contains("var bossPlayer = nativeBoss.Player();",StringComparison.Ordinal)&&bossRegistrySource.Contains("string profileId = bossPlayer.ProfileId;",StringComparison.Ordinal)&&bossRegistrySource.Contains("Instance._bosses.TryGetValue(profileId, out pitAIBossPlayer boss)",StringComparison.Ordinal);
   bool bossProxyRepairPassed=bossProxyCases.Length==2&&actualNativeBossProxy&&oldProxyGuardRejected&&directBossProxyCasts==0&&bossProxyResolverWired; string bossProxyRepairDetail="ANproxy="+actualNativeBossProxy+" oldGuardRejected="+oldProxyGuardRejected+" directCasts="+directBossProxyCasts+" resolverWired="+bossProxyResolverWired;
   var actionEndCases=x.GetProperty("actionEndProducerCases").EnumerateArray().ToArray();
 var aqActionEndCase=actionEndCases.Single(v=>v.GetProperty("nativeEvidence").GetBoolean()); var legacyActionEndCase=actionEndCases.Single(v=>!v.GetProperty("nativeEvidence").GetBoolean());
 string actionEndEvidenceSourcePath=aqActionEndCase.GetProperty("nativeEvidenceSourcePath").GetString()!; string actionEndMetadataPath=aqActionEndCase.GetProperty("nativeMetadataPath").GetString()!;
 JsonElement actionEndEvents=aqActionEndCase.GetProperty("nativeEvidenceEvents"); bool actionEndEventShape=actionEndEvents.ValueKind==JsonValueKind.Array&&actionEndEvents.GetArrayLength()==1&&actionEndEvents.EnumerateArray().Any(e=>e.GetProperty("Id").GetInt32()==1026&&e.GetProperty("Message").GetString()!.Contains("System.NullReferenceException",StringComparison.Ordinal)&&e.GetProperty("Message").GetString()!.Contains("ShallEndCurrentDecision",StringComparison.Ordinal)); bool actionEndFixture=actionEndCases.Length==2&&aqActionEndCase.GetProperty("name").GetString()=="aq-crash-empty-action-end"&&actionEndEventShape&&actionEndEvidenceSourcePath.Length>0&&aqActionEndCase.GetProperty("errorShape").GetString()!.Contains("ShallEndCurrentDecision",StringComparison.Ordinal)&&legacyActionEndCase.GetProperty("expectedRejected").GetBoolean();
 string[] actionEndFiles={"FollowerCombatCommon.cs","FollowerCombatGrenadierObjective.cs","FollowerCombatNeedSniperObjective.cs","FollowerCombatRegroupObjective.cs","FollowerCombatSniper.cs","FollowerCombatSuppressionObjective.cs"};
 string[] actionEndSources=actionEndFiles.Select(f=>File.ReadAllText(Path.Combine(root,"client","BigBrain",f))).ToArray();
 bool actionEndNoDefault=actionEndSources.All(v=>!System.Text.RegularExpressions.Regex.IsMatch(v,"(?:return\\s+default\\s*;|=>\\s*default\\s*;|:\\s*default\\s*[,;])"));
 int[] actionEndContinueCounts={0,4,2,3,3,3}; bool actionEndHelperCalls=actionEndSources.Skip(1).Select((v,i)=>System.Text.RegularExpressions.Regex.Matches(v,"FollowerCombatCommon\\.Continue\\(\\)").Count>=actionEndContinueCounts[i+1]).All(v=>v);
 bool actionEndContinueCtor=actionEndSources[0].Contains("public static AICoreActionEnd Continue() => new AICoreActionEnd(false);",StringComparison.Ordinal)&&actionEndSources[0].Contains("return Continue();",StringComparison.Ordinal);
 bool actionEndMetadata=false; string actionEndMetadataDetail="Cecil metadata not read"; try { using var actionEndModule=ModuleDefinition.ReadModule(actionEndMetadataPath); var actionEndType=actionEndModule.Types.SingleOrDefault(t=>t.Name=="AICoreActionEnd"); var actionEndCtor=actionEndType?.Methods.SingleOrDefault(m=>m.IsConstructor&&m.Parameters.Count==1&&m.Parameters[0].ParameterType.FullName=="System.Boolean"); var actionEndPointerCtor=actionEndType?.Methods.SingleOrDefault(m=>m.IsConstructor&&m.Parameters.Count==1&&m.Parameters[0].ParameterType.FullName=="System.IntPtr"); bool actionEndValueType=actionEndType?.BaseType?.FullName=="Il2CppSystem.ValueType"; bool ctorAllocatesNative=actionEndCtor!=null&&actionEndCtor.Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.Name=="il2cpp_object_new")&&actionEndCtor.Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.FullName.Contains("AICoreActionEnd::.ctor(System.IntPtr)",StringComparison.Ordinal))&&actionEndCtor.Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.Name=="il2cpp_runtime_invoke")&&actionEndCtor.Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.FullName.Contains("Il2CppException::RaiseExceptionIfNecessary",StringComparison.Ordinal)); bool pointerCtorUsesValueType=actionEndPointerCtor!=null&&actionEndPointerCtor.Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.FullName.Contains("Il2CppSystem.ValueType::.ctor(System.IntPtr)",StringComparison.Ordinal)); actionEndMetadata=actionEndValueType&&ctorAllocatesNative&&pointerCtorUsesValueType; actionEndMetadataDetail="valueType="+actionEndValueType+" boolCtorNativeAllocation="+ctorAllocatesNative+" pointerCtorBase="+pointerCtorUsesValueType; } catch(Exception e){actionEndMetadataDetail=e.GetType().Name+": "+e.Message;}
 bool actionEndSource=actionEndNoDefault&&actionEndHelperCalls&&actionEndContinueCtor; string actionEndReplay=actionEndSources[0].Replace("public static AICoreActionEnd Continue() => new AICoreActionEnd(false);","public static AICoreActionEnd Continue() => default;",StringComparison.Ordinal); bool actionEndOldReplayRejected=legacyActionEndCase.GetProperty("expectedRejected").GetBoolean()&&actionEndReplay.Contains("=> default;",StringComparison.Ordinal)&&System.Text.RegularExpressions.Regex.IsMatch(actionEndReplay,"=>\\s*default\\s*;"); bool actionEndPassed=actionEndFixture&&actionEndMetadata&&actionEndSource&&actionEndOldReplayRejected; string actionEndDetail="AQ crash shape="+actionEndFixture+" actual metadata="+actionEndMetadata+" source producers="+actionEndSource+" old default replay rejected="+actionEndOldReplayRejected;
 bool profileReturn=CheckProfileReturn(root,x.GetProperty("profileReturn"),out string profileReturnDetail); bool equipmentBackTargets=CheckEquipmentBuildBackTargets(root,out string equipmentBackTargetDetail);
  bool customActionMismatchPolicy=CustomActionMismatchChecks.Run(root,out string customActionMismatchDetail); bool pass=customActionMismatchPolicy&&ae&&realAe&&realAf&&states&&lifecycleCases&&source&&enemyTargetFixtureCases&&enemyTargetProbeSource&&timerFixture&&timerSource&&normalSpawnFailureWiring&&oldFailureShapeRejected&&followerInitWiring&&oldFollowerInitShapeRejected&&movementPropertyWiring&&oldFollowerMovementShapeRejected&&valueResetWired&&oldDefaultRejected&&nullablePolicyPassed&&nullableNormalEntry&&coverNullableArgumentPassed&&bodyLootEvidence&&bodyLootMetadata&&bodyLootOldCast&&bodyLootSource&&bodyLootCompiled&&bodyLootFixture&&quickPanelCasting&&bossProxyRepairPassed&&actionEndPassed&&profileReturn&&equipmentBackTargets;
  Console.WriteLine(JsonSerializer.Serialize(new{test="normal-squad-pipeline-probe-self-test",passed=pass,customActionMismatchPolicy=customActionMismatchPolicy,customActionMismatchDetail=customActionMismatchDetail,aeBaseline=ae,actualAeShape=realAe,actualAfMissingSpawnShape=realAf,distinctStates=states,lifecycleCases=lifecycleCases,dispatchAfterCompletedPair=dispatchAfterCompletedPair,completedDuplicateDispatch=completedDuplicateDispatch,sourceWiring=source,enemyTargetFixtureCases=enemyTargetFixtureCases,enemyTargetProbeSource=enemyTargetProbeSource,timerFixture=timerFixture,timerNativeCastSource=timerSource,normalSpawnFailureWiring=normalSpawnFailureWiring,normalSpawnFailureDetail,oldFailureShapeRejected=oldFailureShapeRejected,oldFailureShapeDetail,followerInitializationWiring=followerInitWiring,followerInitializationDetail=followerInitDetail,oldFollowerInitShapeRejected=oldFollowerInitShapeRejected,oldFollowerInitDetail,movementPropertyWiring=movementPropertyWiring,movementPropertyDetail=movementPropertyDetail,oldFollowerMovementShapeRejected=oldFollowerMovementShapeRejected,oldMovementDetail,aicoreActionResultResetWired=valueResetWired,aicoreActionResultResetDetail=valueResetDetail,oldDefaultResetShapeRejected=oldDefaultRejected,nativeNullablePolicyPassed=nullablePolicyPassed,nativeNullableNormalEntry=nullableNormalEntry,nativeNullablePolicyCases=nullableResults,coverSearchNullableArgumentPassed=coverNullableArgumentPassed,coverSearchNullableMetadata=coverArgMetadataDetail,coverSearchNullableSource=coverArgSource,coverSearchFirstSlotNativeReturn=coverArgFirstSlotNativeReturn,coverSearchNullableFixture=coverArgFixtureValid,coverSearchNullableArgumentRawNullReplayRejected=coverArgRawNullRejected,bodyLootRootEvidence=bodyLootEvidence,bodyLootMetadata=bodyLootMetadata,bodyLootOldShadowCastRejected=bodyLootOldCast,bodyLootSource=bodyLootSource,bodyLootCompiled=bodyLootCompiled,bodyLootCompiledPath,bodyLootCompiledDetail,bodyLootFixture=bodyLootFixture,bodyLootDetail,quickPanelNativeCasting=quickPanelCasting,quickPanelNativeCastingDetail=quickPanelDetail,bossToFollowProxyResolution=bossProxyRepairPassed,bossToFollowProxyDetail=bossProxyRepairDetail,aicoreActionEndProducerPassed=actionEndPassed,aicoreActionEndProducerDetail=actionEndDetail,aicoreActionEndMetadata=actionEndMetadataDetail,aicoreActionEndDefaultReplayRejected=actionEndOldReplayRejected,profileReturn=profileReturn,profileReturnDetail=profileReturnDetail,equipmentBuildBackTargets=equipmentBackTargets,equipmentBuildBackTargetDetail=equipmentBackTargetDetail,nativeNullableValidation=false,sourceChecksAreNotNativeValidation=true}));
  return pass?0:1;
 }

static bool CheckEquipmentBuildBackTargets(string root,out string detail){
 detail="";
 try{
  string fixturePath=Path.Combine(root,"tests","Spt5InteropContracts","fixtures","profile-options-ui-cases.json");
  using var doc=JsonDocument.Parse(File.ReadAllText(fixturePath));
  JsonElement spec=doc.RootElement.GetProperty("r161BackPatchTargets");
  string wrong=spec.GetProperty("observedWrongLifecycleTarget").GetString()!;
  var cases=spec.GetProperty("cases").EnumerateArray().ToArray();
  string sourcePath=Path.GetFullPath(Path.Combine(root,"client","Patches","TeammateEquipmentBuildsScreenPatch.cs"));
  string source=File.ReadAllText(sourcePath);
  string manifestPath=Path.Combine(root,"tests","Spt5InteropContracts","fixtures","patch-contracts-valid.json");
  using var manifestDoc=JsonDocument.Parse(File.ReadAllText(manifestPath));
  var manifest=manifestDoc.RootElement.GetProperty("patches").EnumerateArray().ToArray();
  bool all=true;
  bool sourceAwakeRejected=true, mapAwakeRejected=true;
  foreach(var c in cases){
   string className=c.GetProperty("patchClass").GetString()!;
   string fullName="pitTeam.Patches."+className;
   string expected=c.GetProperty("expectedNativeCallback").GetString()!;
   string expectedSelector="return AccessTools.Method(typeof(EquipmentBuildsScreen), \""+expected+"\");";
   string wrongSelector="return AccessTools.Method(typeof(EquipmentBuildsScreen), \""+wrong+"\");";
   int classStart=source.IndexOf("internal class "+className+" : ModulePatch",StringComparison.Ordinal);
   int classEnd=classStart<0?-1:source.IndexOf("\n    internal class ",classStart+1,StringComparison.Ordinal);
   if(classEnd<0&&classStart>=0)classEnd=source.Length;
   string body=classStart>=0&&classEnd>classStart?source.Substring(classStart,classEnd-classStart):"";
   int methodStart=body.IndexOf("protected override MethodBase GetTargetMethod()",StringComparison.Ordinal);
   int methodEnd=methodStart<0?-1:body.IndexOf("\n        }",methodStart,StringComparison.Ordinal);
   string targetBody=methodStart>=0&&methodEnd>methodStart?body.Substring(methodStart,methodEnd-methodStart):"";
   var entries=manifest.Where(x=>x.GetProperty("patchClassFullName").GetString()==fullName).ToArray();
   bool uniqueEntry=entries.Length==1;
   string mapped=uniqueEntry?entries[0].GetProperty("targetMethod").GetString()??"": "";
   string targetType=uniqueEntry?entries[0].GetProperty("targetType").GetString()??"": "";
   string metadataPath=uniqueEntry?Path.GetFullPath(entries[0].GetProperty("sourceMetadata").GetProperty("sourcePath").GetString()??""):"";
   var evidence=uniqueEntry?entries[0].GetProperty("sourceMetadata").GetProperty("selectorEvidence").EnumerateArray().ToArray():Array.Empty<JsonElement>();
   bool evidenceCorrect=evidence.Any(x=>Path.GetFullPath(x.GetProperty("path").GetString()??"")==sourcePath&&x.GetProperty("text").GetString()==expectedSelector);
   bool sourceCorrect=targetBody.Contains(expectedSelector,StringComparison.Ordinal);
   bool manifestCorrect=uniqueEntry&&mapped==expected&&targetType=="EFT.UI.EquipmentBuildsScreen"&&metadataPath==sourcePath&&evidenceCorrect;
   bool exactTarget=sourceCorrect&&manifestCorrect;
   string sourceDriftBody=targetBody.Replace(expectedSelector,wrongSelector,StringComparison.Ordinal);
   bool sourceDriftAccepted=mapped==expected&&evidenceCorrect&&sourceDriftBody.Contains(expectedSelector,StringComparison.Ordinal);
   bool manifestDriftAccepted=wrong==expected&&wrongSelector==expectedSelector&&targetBody.Contains(expectedSelector,StringComparison.Ordinal);
   sourceAwakeRejected &= !sourceDriftAccepted;
   mapAwakeRejected &= !manifestDriftAccepted;
   bool preservesBackBehavior=body.Contains("TeammateEquipmentBuildsScreenFlow.HandleBackToProfile()",StringComparison.Ordinal);
   all&=exactTarget&&preservesBackBehavior;
  }
  detail="R161 source/manifest exact callbacks="+all+" sourceAwakeReplayRejected="+sourceAwakeRejected+" mapAwakeReplayRejected="+mapAwakeRejected+" cases="+cases.Length;
  return cases.Length==2&&all&&sourceAwakeRejected&&mapAwakeRejected&&spec.GetProperty("observedFailureShape").GetString()!.Contains("Awake",StringComparison.Ordinal);
 }catch(Exception e){detail=e.GetType().Name+": "+e.Message;return false;}
}
static bool CheckProfileReturn(string root,JsonElement spec,out string detail){
 detail="";
 try{
  string backPath=spec.GetProperty("actualBackClickPath").GetString()!;
  string statePath=spec.GetProperty("actualSideSelectionPath").GetString()!;
  using var back=JsonDocument.Parse(File.ReadAllText(backPath));
  using var state=JsonDocument.Parse(File.ReadAllText(statePath));
  JsonElement click=back.RootElement.GetProperty("result");
  string expectedClickPath=spec.GetProperty("actualBackClickControlPath").GetString()!;
  bool actualBack=click.GetProperty("dispatched").GetBoolean()&&click.GetProperty("pointerClickHandlerDispatched").GetBoolean()&&click.GetProperty("mouseButton").GetString()=="left"&&click.GetProperty("path").GetString()==expectedClickPath&&click.GetProperty("type").GetString()=="EFT.UI.DefaultUIButton";
  JsonElement result=state.RootElement.GetProperty("result");
  string backControl=spec.GetProperty("actualSideBackControlPath").GetString()!;
  string nextControl=spec.GetProperty("actualSideNextControlPath").GetString()!;
  bool actualSide=result.GetProperty("initialized").GetBoolean()&&result.GetProperty("controls").EnumerateArray().Any(c=>c.GetProperty("path").GetString()==backControl&&c.GetProperty("type").GetString()=="EFT.UI.DefaultUIButton"&&c.GetProperty("active").GetBoolean())&&result.GetProperty("controls").EnumerateArray().Any(c=>c.GetProperty("path").GetString()==nextControl&&c.GetProperty("type").GetString()=="EFT.UI.DefaultUIButton"&&c.GetProperty("active").GetBoolean());
  var cases=spec.GetProperty("cases").EnumerateArray().ToArray();
  bool policyCases=cases.Length==4&&cases.All(c=>{
   int count=c.GetProperty("activeCount").GetInt32();
   string actual=count==0?"MainMenuFallback":count==1?"ExistingSideSelectionScreen":"RejectAmbiguous";
   return actual==c.GetProperty("expected").GetString();
  });
  string flow=File.ReadAllText(Path.Combine(root,"client","Modules","SquadSideSelectionFlow.cs"));
  string component=File.ReadAllText(Path.Combine(root,"client","Components","SquadControlMenuUi.cs"));
  string patch=File.ReadAllText(Path.Combine(root,"client","Patches","MatchMakerSideSelectionScreenPatch.cs"));
  bool sourceWired=flow.Contains("ResolveProfileReturnTarget(activeScreens.Count)",StringComparison.Ordinal)&&flow.Contains("screen.gameObject.activeInHierarchy",StringComparison.Ordinal)&&flow.Contains("CaptureOpeningGroupSnapshot(app);",StringComparison.Ordinal)&&flow.Contains("SquadModeActive = true;",StringComparison.Ordinal)&&flow.Contains("HideSquadScreenAlphaLabel();",StringComparison.Ordinal)&&flow.Contains("MatchMakerSideSelectionScreenShowPatch.ApplySquadMode(activeScreens[0]);",StringComparison.Ordinal)&&System.Text.RegularExpressions.Regex.IsMatch(flow,"case\\s+ProfileReturnTarget\\.MainMenuFallback:\\s*Open\\(\\);")&&component.Contains("SquadSideSelectionFlow.ReopenAfterProfile();",StringComparison.Ordinal)&&patch.Contains("ApplySquadMode(__instance);",StringComparison.Ordinal)&&patch.Contains("ScheduleTabsOverlay(__instance);",StringComparison.Ordinal)&&patch.Contains("WireBackButtonExit(__instance);",StringComparison.Ordinal);
  bool legacyRejected=spec.GetProperty("legacyAlwaysOpenShapeExpectedRejected").GetBoolean()&&sourceWired&&!component.Contains("SquadSideSelectionFlow.Open();",StringComparison.Ordinal);
  detail="actual profile Back="+actualBack+" actual stock side controls="+actualSide+" count policy="+policyCases+" normal source wiring="+sourceWired+" legacy Open-only path rejected="+legacyRejected;
  return actualBack&&actualSide&&policyCases&&sourceWired&&legacyRejected;
 }catch(Exception e){detail=e.GetType().Name+": "+e.Message;return false;}
}
static bool CheckQuickPanelNativeCasts(string root,out string detail){
 detail="";
 try{
 var fp=Path.Combine(root,"tests","Spt5InteropContracts","fixtures","quick-panel-interactable-cast-cases.json");
 using var j=JsonDocument.Parse(File.ReadAllText(fp));var x=j.RootElement;string a=x.GetProperty("assemblyPath").GetString()!;
 string s=File.ReadAllText(Path.Combine(root,"client","Patches","QuickPanelPatch.cs"));
 string h=File.ReadAllText(Path.Combine(root,"client","Compat","Il2CppBoundary.NativeObject.cs"));
 int b=s.IndexOf("private static bool PatchPrefix(GesturesQuickPanel __instance)",StringComparison.Ordinal);
 int e=b<0?-1:s.IndexOf("private static void EnsureViewBackpackQuickCommand",b,StringComparison.Ordinal);
 string m=b>=0&&e>b?s.Substring(b,e-b):"";
 string[] ns={"LootItem","Corpse","LootableContainer"};
 bool calls=ns.All(n=>m.Contains("CastNativeOrNull<"+n+">(player.InteractableObject)")&&!m.Contains("player.InteractableObject as "+n));
 bool boundary=h.Contains("if (value == null) return null;")&&h.Contains("return value.TryCast<T>();")&&!h.Contains("catch");
 bool metadata=false;
 using(var mod=ModuleDefinition.ReadModule(a)){
 var p=mod.Types.SingleOrDefault(t=>t.FullName=="EFT.Player");
 var g=p?.Properties.SingleOrDefault(t=>t.Name=="InteractableObject")?.GetMethod;
 metadata=g!=null&&g.Parameters.Count==0&&g.ReturnType.FullName=="EFT.Interactive.InteractableObject";
 foreach(var n in ns){var t=mod.Types.SingleOrDefault(z=>z.FullName=="EFT.Interactive."+n);bool ok=false;
 for(int i=0;t?.BaseType!=null&&i<16;i++){if(t.BaseType.FullName=="EFT.Interactive.InteractableObject"){ok=true;break;}t=mod.Types.SingleOrDefault(z=>z.FullName==t.BaseType.FullName);}metadata&=ok;}
 }
 var c=x.GetProperty("cases").EnumerateArray().ToArray();
 bool shapes=c.Length==3&&c.Any(v=>v.GetProperty("name").GetString()=="null-interactable"&&v.GetProperty("expectedNull").GetBoolean())&&c.Any(v=>v.GetProperty("name").GetString()=="base-proxy-native-corpse"&&v.GetProperty("proxyType").GetString()=="EFT.Interactive.InteractableObject"&&v.GetProperty("nativeType").GetString()=="EFT.Interactive.Corpse"&&v.GetProperty("expectedNativeMatch").GetBoolean()&&!v.GetProperty("oldClrAsWouldMatch").GetBoolean())&&c.Any(v=>v.GetProperty("name").GetString()=="wrong-native-type"&&v.GetProperty("nativeType").GetString()=="EFT.Player"&&v.GetProperty("expectedNull").GetBoolean());
 detail="metadata="+metadata+" source="+calls+" boundary="+boundary+" cases="+shapes+" assembly="+a;
 return metadata&&calls&&boundary&&shapes;
 }catch(Exception ex){detail=ex.GetType().Name+": "+ex.Message;return false;}
}
static bool CheckCompiledBodyLoot(string assemblyPath,out string detail)
{
 detail="assembly missing: "+assemblyPath;
 if(!File.Exists(assemblyPath))return false;
 try
 {
  using var module=ModuleDefinition.ReadModule(assemblyPath);
  var type=module.Types.SingleOrDefault(t=>t.FullName=="pitTeam.BigBrain.Actions.GestureCommandAction");
  var method=type?.Methods.SingleOrDefault(m=>m.Name=="TryGetBodyLootExecutionContext");
  if(method?.Body==null){detail="method missing in "+assemblyPath;return false;}
  bool nativeCast=method.Body.Instructions.Any(i=>i.Operand is GenericInstanceMethod gim
   && gim.ElementMethod.Name=="CastNativeOrNull"
   && gim.GenericArguments.Count==1
   && gim.GenericArguments[0].FullName=="EFT.InventoryLogic.InventoryEquipment");
  bool oldCast=method.Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Isinst
   && i.Operand?.ToString()=="EFT.InventoryLogic.InventoryEquipment");
  detail="assembly="+assemblyPath+" nativeGenericCast="+nativeCast+" oldClrIsinst="+oldCast;
  return nativeCast&&!oldCast;
 }
 catch(Exception e){detail="Cecil "+e.GetType().Name+": "+e.Message+"; assembly="+assemblyPath;return false;}
}
static bool CheckBodyLootRootEvidence(System.Text.Json.JsonElement body, out bool metadata, out bool oldCast, out string detail)
{
 metadata=false; oldCast=false; detail="unread";
 try
 {
  using var evidence=System.Text.Json.JsonDocument.Parse(File.ReadAllText(body.GetProperty("evidencePath").GetString()!));
  var samples=evidence.RootElement.GetProperty("result").GetProperty("corpses").GetProperty("localLootCorpses").GetProperty("samples").EnumerateArray().ToArray();
  var sample=samples.SingleOrDefault(v=>v.GetProperty("pointer").GetString()==body.GetProperty("corpsePointer").GetString());
  if(sample.ValueKind==System.Text.Json.JsonValueKind.Undefined)return false;
  var dog=sample.GetProperty("dogtag");
  bool actual=sample.GetProperty("profileId").GetString()==body.GetProperty("profileId").GetString()
   && dog.GetProperty("rootType").GetString()==body.GetProperty("rootWrapperType").GetString()
   && dog.GetProperty("rootPointer").GetString()==body.GetProperty("nativeEquipmentPointer").GetString()
   && dog.GetProperty("equipmentPointer").GetString()==body.GetProperty("nativeEquipmentPointer").GetString()
   && dog.GetProperty("dogtagItemId").GetString()==body.GetProperty("dogtagItemId").GetString()
   && dog.GetProperty("dogtagProfileId").GetString()==body.GetProperty("profileId").GetString();
  using(var interop=ModuleDefinition.ReadModule(body.GetProperty("assemblyPath").GetString()!))
  {
   var owner=interop.Types.SingleOrDefault(t=>t.FullName=="EFT.InventoryLogic.ItemController");
   var equipment=interop.Types.SingleOrDefault(t=>t.FullName=="EFT.InventoryLogic.InventoryEquipment");
   var bases=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
   var cursor=equipment;
   while(cursor?.BaseType!=null && bases.Add(cursor.BaseType.FullName))
    cursor=interop.Types.SingleOrDefault(t=>t.FullName==cursor.BaseType.FullName);
   metadata=owner?.Properties.SingleOrDefault(p=>p.Name=="RootItem")?.GetMethod?.ReturnType.FullName=="EFT.InventoryLogic.Item"
    && bases.Contains("EFT.InventoryLogic.CompoundItem") && bases.Contains("EFT.InventoryLogic.ContainerCollection");
  }
  var legacy=body.GetProperty("legacyCastObservation");
  var cases=body.GetProperty("syntheticCases").EnumerateArray().ToArray();
  var nullCase=cases.SingleOrDefault(v=>v.GetProperty("name").GetString()=="null-item-owner-or-root");
  var baseCase=cases.SingleOrDefault(v=>v.GetProperty("name").GetString()=="base-item-wrapper-native-inventory-equipment");
  oldCast=legacy.GetProperty("observed").GetBoolean()
   && legacy.GetProperty("typeFullName").GetString()=="pitTeam.BigBrain.Actions.GestureCommandAction"
   && legacy.GetProperty("methodName").GetString()=="TryGetBodyLootExecutionContext"
   && legacy.GetProperty("opcode").GetString()=="isinst"
   && legacy.GetProperty("operand").GetString()=="EFT.InventoryLogic.InventoryEquipment"
   && nullCase.ValueKind!=System.Text.Json.JsonValueKind.Undefined
   && nullCase.GetProperty("rootItemPointer").ValueKind==System.Text.Json.JsonValueKind.Null
   && baseCase.ValueKind!=System.Text.Json.JsonValueKind.Undefined
   && baseCase.GetProperty("rootWrapperType").GetString()=="EFT.InventoryLogic.Item"
   && baseCase.GetProperty("nativeClass").GetString()=="EFT.InventoryLogic.InventoryEquipment"
   && baseCase.GetProperty("expectedResolve").GetBoolean()
   && !baseCase.GetProperty("legacyClrCastWouldResolve").GetBoolean();
  detail="actualCorpseProfile/root/equipment/dogtag="+actual+" metadataRootItemGetterAndNativeBaseChain="+metadata+" capturedOldIsinstReplayRejected="+oldCast;
  return actual;
 }
 catch(Exception e){detail=e.GetType().Name+": "+e.Message;return false;}
}
static bool CheckValueReset(string source, System.Text.Json.JsonElement[] cases,out string detail,out bool oldRejected)
{
 oldRejected=false;
 bool fixture=cases.Length==1&&cases[0].GetProperty("name").GetString()=="ah-last-result-native-valuetype-reset"&&cases[0].GetProperty("typeFullName").GetString()=="AICoreActionResult`2"&&cases[0].GetProperty("expectedBaseType").GetString()=="Il2CppSystem.ValueType"&&cases[0].GetProperty("propertyName").GetString()=="_lastResult"&&cases[0].GetProperty("expectedCtorAllocation").GetString()=="il2cpp_object_new"&&cases[0].GetProperty("expectedSetterUnbox").GetBoolean()&&cases[0].GetProperty("expectedSetterCpblk").GetBoolean();
 bool metadata=false;
 try { using var m=ModuleDefinition.ReadModule(cases[0].GetProperty("metadataSourcePath").GetString()!); var r=m.Types.SingleOrDefault(t=>t.Name=="AICoreActionResult`2"); var a=m.Types.SingleOrDefault(t=>t.Name=="AICoreAgent`1"); var c=r?.Methods.SingleOrDefault(x=>x.Name==".ctor"&&!x.HasParameters); var s=a?.Properties.SingleOrDefault(x=>x.Name=="_lastResult")?.SetMethod; metadata=r?.BaseType?.FullName=="Il2CppSystem.ValueType"&&c!=null&&c.Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.FullName.Contains("il2cpp_object_new"))&&c.Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.FullName.Contains("Il2CppSystem.ValueType::.ctor(System.IntPtr)"))&&s!=null&&s.Body.Instructions.Any(i=>i.Operand is MethodReference mr&&mr.FullName.Contains("il2cpp_object_unbox"))&&s.Body.Instructions.Any(i=>i.OpCode.Code==Mono.Cecil.Cil.Code.Cpblk); } catch(Exception e){detail="Cecil failure: "+e.Message;return false;}
 const string good="var defaultResult = new AICoreActionResult<BotLogicDecision, CoreActionResultParams>();", bad="var defaultResult = default(AICoreActionResult<BotLogicDecision, CoreActionResultParams>);";
 bool sourceGood=source.Contains("private void ClearActiveLayerPointers(BaseBrain brain, AICoreAgent<BotLogicDecision> agent)")&&source.Contains(good)&&source.Contains("lastResultProperty.SetValue(agent, defaultResult);");
 string replay=source.Replace(good,bad,StringComparison.Ordinal); oldRejected=source.Split(good,StringSplitOptions.None).Length==2&&!replay.Contains(good);
 detail="fixture="+fixture+" actualCecilBaseCtorUnboxCpblk="+metadata+" newWrapperSource="+sourceGood+" oldDefaultReplayRejected="+oldRejected;
 return fixture&&metadata&&sourceGood&&oldRejected;
}

static bool IsFollowerMovementPropertyWired(string source, System.Text.Json.JsonElement[] cases, out string detail)
{
 bool fixture=cases.Length==1&&cases[0].GetProperty("name").GetString()=="ah-movementcontext-generated-property"&&cases[0].GetProperty("actualType").GetString()=="EFT.MovementContext"&&cases[0].GetProperty("propertyName").GetString()=="_player"&&cases[0].GetProperty("propertyType").GetString()=="EFT.Player"&&cases[0].GetProperty("hasGetter").GetBoolean()&&cases[0].GetProperty("hasSetter").GetBoolean()&&cases[0].GetProperty("expectedAccess").GetString()=="AccessTools.Property";
 bool actualMetadata=false;
 string metadataPath=cases.Length==1?cases[0].GetProperty("metadataSourcePath").GetString()??string.Empty:string.Empty;
 try
 {
  using var module=ModuleDefinition.ReadModule(metadataPath);
  var type=module.Types.SingleOrDefault(t=>t.FullName=="EFT.MovementContext");
  var property=type?.Properties.SingleOrDefault(p=>p.Name=="_player");
  actualMetadata=property!=null&&property.PropertyType.FullName=="EFT.Player"&&property.GetMethod!=null&&property.SetMethod!=null&&type!=null&&!type.Fields.Any(f=>f.Name=="_player");
 }
 catch(System.Exception metadataError){detail="Cecil metadata read failed: "+metadataError.GetType().Name+": "+metadataError.Message;return false;}
 bool access=source.Contains("PropertyInfo PlayerProperty = AccessTools.Property(typeof(MovementContext), \"_player\")",StringComparison.Ordinal)&&source.Contains("PlayerProperty?.GetValue(__instance.MovementContext) as Player",StringComparison.Ordinal)&&!source.Contains("AccessTools.Field(typeof(MovementContext), \"_player\")",StringComparison.Ordinal)&&!source.Contains("PlayerField?.GetValue(__instance.MovementContext)",StringComparison.Ordinal);
 if(!fixture||!actualMetadata||!access){detail="fixture="+fixture+" actualCecilGeneratedProperty="+actualMetadata+" sourcePropertyAccess="+access;return false;}
 detail="Cecil confirms EFT.MovementContext._player is a getter/setter EFT.Player property with no same-named field; normal sprint patch uses the property getter";
 return true;
}

static bool IsFollowerInitializationWired(string source, System.Text.Json.JsonElement[] cases, out string detail)
{
 source=source.Replace("System.Threading.Tasks.Task", "Task", StringComparison.Ordinal);
 string compact=System.Text.RegularExpressions.Regex.Replace(source, @"\s+", " ");
 int start=compact.IndexOf("public async Task SpawnGroupBots(",StringComparison.Ordinal);
 int end=start<0?-1:compact.IndexOf("public async Task<bool> SpawnDebugFollower",start,StringComparison.Ordinal);
 if(start<0||end<=start){detail="normal SpawnGroupBots boundary missing";return false;}
 string method=compact.Substring(start,end-start);
 bool fixture=cases.Length==3&&cases.Any(v=>v.GetProperty("name").GetString()=="ag-settings-init-nre-faults-normal-task"&&v.GetProperty("expectedFirstFailureStage").GetString()=="SetFollowerSettings"&&v.GetProperty("expectedNormalTaskStatus").GetString()=="Faulted"&&v.GetProperty("expectedOriginalExceptionPropagated").GetBoolean())&&cases.Any(v=>v.GetProperty("name").GetString()=="initialized-follower-ready-notification-fails"&&v.GetProperty("expectedNormalTaskStatus").GetString()=="RanToCompletion"&&v.GetProperty("expectedReadyNotificationErrorIsSeparate").GetBoolean())&&cases.Any(v=>v.GetProperty("name").GetString()=="callback-missing-or-raid-cleanup"&&v.GetProperty("expectedNormalTaskStatus").GetString()=="CanceledOrTimedOut"&&v.GetProperty("expectedWatcherCleaned").GetBoolean());
 int add=method.IndexOf("BossPlayers.AddFollower(",StringComparison.Ordinal);
 int setResult=method.IndexOf("followerInitialization.TrySetResult(true)",StringComparison.Ordinal);
 int ready=method.IndexOf("TrySayFollowerReadyControlled(",StringComparison.Ordinal);
 bool fault=method.Contains("followerInitialization.TrySetException(error4)",StringComparison.Ordinal)&&method.Contains("await followerInitialization.Task",StringComparison.Ordinal)&&method.Contains("throw;",StringComparison.Ordinal);
 bool bounded=method.Contains("Task.Delay(System.TimeSpan.FromSeconds(120)",StringComparison.Ordinal)&&method.Contains("Task.WhenAny(activationTask, followerInitialization.Task, deadline)",StringComparison.Ordinal)&&method.Contains("Task.WhenAny(activationTask, deadline)",StringComparison.Ordinal)&&method.Contains("Task.WhenAny(followerInitialization.Task, deadline)",StringComparison.Ordinal);
 bool cleanup=method.Contains("PendingNormalFollowerInitializations.TryRemove(followerInitialization, out _)",StringComparison.Ordinal)&&method.Contains("if (callbackProfileId != null) BotOwnerManualUpdatePatch.BotOwnerUpdate.Remove(callbackProfileId);",StringComparison.Ordinal)&&source.Contains("pending.TrySetCanceled()",StringComparison.Ordinal)&&source.Contains("CancelPendingNormalFollowerInitializations();",StringComparison.Ordinal);
 bool readySeparate=setResult>=0&&ready>setResult&&method.Contains("catch (System.Exception readyError)",StringComparison.Ordinal);
 if(!fixture||add<0||!method.Contains("throwOnInitFailure: true",StringComparison.Ordinal)||!fault||!bounded||!cleanup||!readySeparate){detail="AG fixture or normal callback fault/ready/deadline/cleanup wiring incomplete";return false;}
 detail="AG SetFollowerSettings failure reaches the awaited normal task; ready errors are isolated after init; activation and callback waits are bounded and cleanup cancels/removes pending work";
 return true;
}

static bool IsSpawnGroupFailureWired(string source, out string detail)
{
  source=source.Replace("System.Threading.Tasks.Task", "Task", StringComparison.Ordinal).Replace("System.Exception", "Exception", StringComparison.Ordinal).Replace("System.InvalidOperationException", "InvalidOperationException", StringComparison.Ordinal).Replace("System.Collections.Generic.", "", StringComparison.Ordinal);
  source=System.Text.RegularExpressions.Regex.Replace(source, @"\s+", " ");

 int h=source.IndexOf("private async Task<bool> ActivateBotAtPosition(",StringComparison.Ordinal);
 int he=source.IndexOf("private static int ScoreDebugSpawnProfile",h<0?0:h,StringComparison.Ordinal);
 int s=source.IndexOf("public async Task SpawnGroupBots(",StringComparison.Ordinal);
 int e=source.IndexOf("public async Task<bool> SpawnDebugFollower",s<0?0:s,StringComparison.Ordinal);
 if(h<0||he<=h){detail="activation helper boundary missing";return false;}
 if(s<0||e<=s){detail="SpawnGroupBots boundary missing";return false;}
 string a=source.Substring(h,he-h),n=source.Substring(s,e-s);
 var catchMatch=System.Text.RegularExpressions.Regex.Match(a,@"catch\s*\(\s*Exception\s+\w+\s*\)");
  int ac=catchMatch.Success?catchMatch.Index:-1;
 int g=a.IndexOf("if (propagateFailure)",StringComparison.Ordinal);
 int r=g<0?-1:a.IndexOf("throw;",g,StringComparison.Ordinal);
 int f=a.IndexOf("return false;",StringComparison.Ordinal);
 if(!a.Contains("bool propagateFailure = false",StringComparison.Ordinal)||ac<0||g<=ac||r<=g||f<=r){detail="activation helper does not preserve and rethrow normal failure";return false;}
 int pc=n.IndexOf("botsProfile = await CreateFollowerProfiles(player);",StringComparison.Ordinal);
 int pca=-1;if(pc>=0){var profileCatch=System.Text.RegularExpressions.Regex.Match(n.Substring(pc),@"catch\s*\(\s*Exception\s+\w+\s*\)");if(profileCatch.Success)pca=pc+profileCatch.Index;}
 int pe=pca<0?-1:n.IndexOf("Dictionary<string, string> botsTactic",pca,StringComparison.Ordinal);
 if(pc<0||pca<=pc||pe<=pca){detail="profile creation catch boundary missing";return false;}
 string profileBlock=n.Substring(pca,pe-pca);
 if(!profileBlock.Contains("throw;",StringComparison.Ordinal)||profileBlock.Contains("return;",StringComparison.Ordinal)){detail="profile creation failure is swallowed";return false;}
 int c=n.IndexOf("ActivateBotAtPosition(",StringComparison.Ordinal);
  bool directAwait=c>=0&&n.Contains("await ActivateBotAtPosition(",StringComparison.Ordinal);
  bool taskAwait=c>=0&&n.Contains("Task<bool> activationTask = ActivateBotAtPosition(",StringComparison.Ordinal)&&n.IndexOf("await activationTask",c,StringComparison.Ordinal)>c;
 int cc=-1;if(c>=0){var activationCatch=System.Text.RegularExpressions.Regex.Match(n.Substring(c),@"catch\s*\(\s*Exception\s+\w+\s*\)");if(activationCatch.Success)cc=c+activationCatch.Index;}
 int propagation=c<0?-1:n.IndexOf("propagateFailure: true",c,StringComparison.Ordinal);
  int falseGuard=c<0?-1:n.IndexOf("if (!activated)",c,StringComparison.Ordinal);
  int failureThrow=falseGuard<0?-1:n.IndexOf("throw new InvalidOperationException",falseGuard,StringComparison.Ordinal);
  if(c<0||propagation<=c||(!directAwait&&!taskAwait)||falseGuard<=c||failureThrow<=falseGuard){detail="normal activation result is ignored";return false;}
 if(cc<=c||n.IndexOf("throw;",cc,StringComparison.Ordinal)<=cc){detail="normal activation catch swallows the failure";return false;}
 detail="all normal SpawnGroupBots failure-propagation anchors found";return true;
}
static string[][] ReadCoverSearchDataArguments(string source) {
  var parsed=new List<string[]>(); foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(source,@"new CoverSearchData\s*\(")) { int open=source.IndexOf('(',match.Index); int depth=0,start=open+1; var args=new List<string>(); for(int i=open;i<source.Length;i++){ char ch=source[i]; if(ch=='(') depth++; else if(ch==')'){ depth--; if(depth==0){args.Add(source.Substring(start,i-start).Trim()); break;} } else if(ch==','&&depth==1){args.Add(source.Substring(start,i-start).Trim()); start=i+1;} } parsed.Add(args.ToArray()); } return parsed.ToArray();
}
static bool HasValidCoverSearchNullableSlots(string[][] calls) {
  if(calls.Length!=2||calls.Any(call=>call.Length!=16)) return false; int[] slots={7,8,13,14}; foreach(var call in calls) foreach(int slot in slots) { string value=call[slot]; if(!value.EndsWith(".ToNativeNullable()",StringComparison.Ordinal)) return false; } return true;
}
}
