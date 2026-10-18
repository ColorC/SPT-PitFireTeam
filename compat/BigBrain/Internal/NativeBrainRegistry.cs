using DrakiaXYZ.BigBrain.Brains;
using EFT;
using System;
using System.Collections.Generic;

namespace DrakiaXYZ.BigBrain.Internal
{
    internal static class NativeBrainRegistry
    {
        private sealed class OwnerRegistration
        {
            internal readonly BotOwner Owner;
            internal readonly HashSet<IntPtr> LayerPointers = new HashSet<IntPtr>();
            internal readonly HashSet<IntPtr> LogicPointers = new HashSet<IntPtr>();

            internal OwnerRegistration(BotOwner owner)
            {
                Owner = owner;
            }
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<IntPtr, CustomLayerWrapper> Layers = new Dictionary<IntPtr, CustomLayerWrapper>();
        private static readonly Dictionary<IntPtr, CustomLogicWrapper> Logics = new Dictionary<IntPtr, CustomLogicWrapper>();
        private static readonly Dictionary<IntPtr, OwnerRegistration> Owners = new Dictionary<IntPtr, OwnerRegistration>();

        internal static void RegisterLayer(CustomLayerWrapper wrapper, BotOwner owner)
        {
            if (wrapper == null || owner == null || wrapper.Pointer == IntPtr.Zero || owner.Pointer == IntPtr.Zero)
                throw new ArgumentException("Cannot register a custom layer with a missing native pointer or owner.");

            lock (Sync)
            {
                OwnerRegistration registration = GetOrCreateOwner(owner);
                Layers[wrapper.Pointer] = wrapper;
                registration.LayerPointers.Add(wrapper.Pointer);
            }
        }

        internal static void RegisterLogic(CustomLogicWrapper wrapper, BotOwner owner)
        {
            if (wrapper == null || owner == null || wrapper.Pointer == IntPtr.Zero || owner.Pointer == IntPtr.Zero)
                throw new ArgumentException("Cannot register custom logic with a missing native pointer or owner.");

            lock (Sync)
            {
                OwnerRegistration registration = GetOrCreateOwner(owner);
                Logics[wrapper.Pointer] = wrapper;
                registration.LogicPointers.Add(wrapper.Pointer);
            }
        }

        internal static CustomLayerWrapper ResolveLayer(IntPtr? nativePointer)
        {
            if (!nativePointer.HasValue || nativePointer.Value == IntPtr.Zero) return null;
            lock (Sync)
                return Layers.TryGetValue(nativePointer.Value, out CustomLayerWrapper wrapper) ? wrapper : null;
        }

        private sealed class DispatchRecord { internal IntPtr WrapperPointer; internal IntPtr OwnerPointer; internal int ShallUseCalls; internal int GetDecisionCalls; internal int ShallEndCalls; internal int ShallEndHits; internal int ShallEndMisses; internal int LookupHits; internal int LookupMisses; internal bool? LastIsActive; internal string LastDecision; internal int? LastDecisionAction; internal string LastDecisionReason; internal string LastDecisionReadError; internal int? LastShallEndDecisionAction; internal string LastShallEndDecisionReason; internal string LastShallEndWrapperType; internal string LastShallEndWrapperActionType; internal int? LastShallEndWrapperActionId; internal bool? LastShallEndResult; internal string LastShallEndReason; internal string FirstError; }
 private static readonly Dictionary<IntPtr, DispatchRecord> Dispatch = new Dictionary<IntPtr, DispatchRecord>();
 private static int DispatchDropped; private static int ShallEndPrefixCalls; private static int ShallEndPrefixHits; private static int ShallEndPrefixMisses;
 internal static void RecordDispatchPrefix(IntPtr pointer, bool decision, bool hit) { try { lock(Sync) { if(pointer==IntPtr.Zero)return; DispatchRecord r; if(!Dispatch.TryGetValue(pointer,out r)) { if(Dispatch.Count>=128){DispatchDropped++;return;} r=new DispatchRecord { WrapperPointer=pointer }; foreach(var owner in Owners) if(owner.Value.LayerPointers.Contains(pointer)){r.OwnerPointer=owner.Key;break;} Dispatch.Add(pointer,r); } if(decision)r.GetDecisionCalls++;else r.ShallUseCalls++;if(hit)r.LookupHits++;else r.LookupMisses++; } } catch{} }
 internal static void RecordIsActive(IntPtr pointer,bool value) { try { lock(Sync) if(Dispatch.TryGetValue(pointer,out DispatchRecord r))r.LastIsActive=value; } catch{} }
 internal static void RecordDecision(IntPtr pointer,object value) { string a=null,q=null,e=null; try { if(value!=null){var t=value.GetType();var x=t.GetProperty("Action")?.GetValue(value,null);a=x==null?null:Convert.ToInt32(x).ToString();q=t.GetProperty("Reason")?.GetValue(value,null)?.ToString();} } catch(Exception error) { e=error.ToString(); } try { lock(Sync) if(Dispatch.TryGetValue(pointer,out DispatchRecord r)){r.LastDecision=value==null?null:value.ToString();if(int.TryParse(a,out int v))r.LastDecisionAction=v;r.LastDecisionReason=q;r.LastDecisionReadError=e;} } catch{} }
 internal static void RecordDispatchError(IntPtr pointer,Exception error) { try { lock(Sync) if(Dispatch.TryGetValue(pointer,out DispatchRecord r)&&r.FirstError==null)r.FirstError=error==null?null:error.ToString(); } catch{} }
 internal static void RecordShallEndObservation(IntPtr pointer, bool hit, int? decisionAction, string decisionReason, string wrapperType, string wrapperActionType, int? wrapperActionId, bool? shouldEnd, string endReason) { try { lock (Sync) { ShallEndPrefixCalls++; if (hit) ShallEndPrefixHits++; else ShallEndPrefixMisses++; if (pointer == IntPtr.Zero) return; if (!Dispatch.TryGetValue(pointer, out DispatchRecord r)) { if (Dispatch.Count >= 128) { DispatchDropped++; return; } r = new DispatchRecord { WrapperPointer = pointer }; foreach (var owner in Owners) if (owner.Value.LayerPointers.Contains(pointer)) { r.OwnerPointer = owner.Key; break; } Dispatch.Add(pointer, r); } r.ShallEndCalls++; if (hit) r.ShallEndHits++; else r.ShallEndMisses++; r.LastShallEndDecisionAction = decisionAction; r.LastShallEndDecisionReason = decisionReason; r.LastShallEndWrapperType = wrapperType; r.LastShallEndWrapperActionType = wrapperActionType; r.LastShallEndWrapperActionId = wrapperActionId; r.LastShallEndResult = shouldEnd; r.LastShallEndReason = endReason; } } catch { } } internal static object GetDispatchSnapshot() { lock(Sync) { var entries=new object[Dispatch.Count];int i=0;foreach(DispatchRecord r in Dispatch.Values)entries[i++]=new { wrapperPointer=r.WrapperPointer.ToString(),botPointer=r.OwnerPointer.ToString(),shallUsePrefixCalls=r.ShallUseCalls,getDecisionPrefixCalls=r.GetDecisionCalls,lookupHits=r.LookupHits,lookupMisses=r.LookupMisses,lastIsActive=r.LastIsActive,lastGetDecision=r.LastDecision,lastGetDecisionAction=r.LastDecisionAction,lastGetDecisionReason=r.LastDecisionReason,lastGetDecisionReadError=r.LastDecisionReadError,lastShallEndDecisionAction=r.LastShallEndDecisionAction,lastShallEndDecisionReason=r.LastShallEndDecisionReason,lastShallEndWrapperType=r.LastShallEndWrapperType,lastShallEndWrapperActionType=r.LastShallEndWrapperActionType,lastShallEndWrapperActionId=r.LastShallEndWrapperActionId,lastShallEndResult=r.LastShallEndResult,lastShallEndReason=r.LastShallEndReason,lastShallEndReceiverLayerPointer=r.WrapperPointer.ToString(),firstError=r.FirstError }; var agents=new object[Agents.Count];i=0;foreach(AgentRecord r in Agents.Values)agents[i++]=new { agentPointer=r.Pointer.ToString(),currentLayerPointer=r.LayerPointer.ToString(),botPointer=r.BotPointer.ToString(),prefixCalls=r.PrefixCalls,strategyUpdateCalls=r.StrategyCalls,lastAction=r.LastAction,previousAction=r.PreviousAction,lazyGetterCalls=r.LazyCalls,lastLazyGetterInput=r.LastLazyInput,nodePointer=r.NodePointer.ToString(),resolveLogic=r.ResolveLogicResult,startCalls=r.Starts,updateCalls=r.Updates,lastWritebackAction=r.LastWritebackAction,stage=r.Stage,firstError=r.FirstError }; return new { supported=true,available=true,count=entries.Length,dropped=DispatchDropped,entries,agents }; } }


        internal static object GetDispatchSnapshotForBot(IntPtr botPointer)
        {
            lock (Sync)
            {
                var entries = new List<object>();
                foreach (DispatchRecord r in Dispatch.Values)
                    if (botPointer != IntPtr.Zero && r.OwnerPointer == botPointer)
                        entries.Add(new { wrapperPointer = r.WrapperPointer.ToString(), botPointer = r.OwnerPointer.ToString(), shallUsePrefixCalls = r.ShallUseCalls, getDecisionPrefixCalls = r.GetDecisionCalls, lookupHits = r.LookupHits, lookupMisses = r.LookupMisses, lastIsActive = r.LastIsActive, lastGetDecision = r.LastDecision, lastGetDecisionAction = r.LastDecisionAction, lastGetDecisionReason = r.LastDecisionReason, lastGetDecisionReadError = r.LastDecisionReadError, shallEndPrefixCalls = r.ShallEndCalls, shallEndPrefixHits = r.ShallEndHits, shallEndPrefixMisses = r.ShallEndMisses, lastShallEndDecisionAction = r.LastShallEndDecisionAction, lastShallEndDecisionReason = r.LastShallEndDecisionReason, lastShallEndWrapperType = r.LastShallEndWrapperType, lastShallEndWrapperActionType = r.LastShallEndWrapperActionType, lastShallEndWrapperActionId = r.LastShallEndWrapperActionId, lastShallEndResult = r.LastShallEndResult, lastShallEndReason = r.LastShallEndReason, lastShallEndReceiverLayerPointer = r.WrapperPointer.ToString(), firstError = r.FirstError });
                int zeroOwnerAgentCount = 0; foreach (AgentRecord agent in Agents.Values) if (agent.BotPointer == IntPtr.Zero) zeroOwnerAgentCount++;
                var agents = new List<object>();
                foreach (AgentRecord r in Agents.Values)
                    if (botPointer != IntPtr.Zero && r.BotPointer == botPointer)
                        agents.Add(new { agentPointer = r.Pointer.ToString(), currentLayerPointer = r.LayerPointer.ToString(), botPointer = r.BotPointer.ToString(), prefixCalls = r.PrefixCalls, strategyUpdateCalls = r.StrategyCalls, lastAction = r.LastAction, previousAction = r.PreviousAction, lazyGetterCalls = r.LazyCalls, lastLazyGetterInput = r.LastLazyInput, nodePointer = r.NodePointer.ToString(), resolveLogic = r.ResolveLogicResult, startCalls = r.Starts, updateCalls = r.Updates, lastWritebackAction = r.LastWritebackAction, stage = r.Stage, firstError = r.FirstError });
                var logics = new List<object>(); int droppedLogics = 0; foreach (CustomLogicWrapper logic in Logics.Values) if (botPointer != IntPtr.Zero && logic.OwnerPointer == botPointer) { if (logics.Count < 64) logics.Add(logic.GetDiagnosticSnapshot()); else droppedLogics++; }
                return new { supported = true, available = botPointer != IntPtr.Zero, status = botPointer == IntPtr.Zero ? "null-bot-pointer" : entries.Count + agents.Count + logics.Count + droppedLogics == 0 ? "no-records-for-bot" : "available", botPointer = botPointer.ToString(), count = Dispatch.Count, dropped = DispatchDropped, filteredCount = entries.Count, filteredAgentCount = agents.Count, entries = entries.ToArray(), agents = agents.ToArray(), globalAgentCount = Agents.Count, zeroOwnerAgentCount = zeroOwnerAgentCount, globalLogicCount = Logics.Count, filteredLogicCount = logics.Count + droppedLogics, droppedLogicCount = droppedLogics, logics = logics.ToArray(), shallEndPrefixCalls = ShallEndPrefixCalls, shallEndPrefixHits = ShallEndPrefixHits, shallEndPrefixMisses = ShallEndPrefixMisses };
            }
        }

        private sealed class AgentRecord { internal IntPtr Pointer, LayerPointer, BotPointer, NodePointer; internal int PrefixCalls, StrategyCalls, LazyCalls, Starts, Updates; internal string Stage, LastAction, PreviousAction, LastLazyInput, LastWritebackAction, ResolveLogicResult, FirstError; }
        private static readonly Dictionary<IntPtr, AgentRecord> Agents = new Dictionary<IntPtr, AgentRecord>();
        private static string ActionOf(object value) { try { if(value==null)return null; object action=value.GetType().GetProperty("Action")?.GetValue(value,null); return action==null?null:Convert.ToInt32(action).ToString(); } catch { return null; } }
        internal static void RecordAgentPrefix(IntPtr agent, IntPtr layer) { try { lock(Sync) { AgentRecord r; if(agent==IntPtr.Zero)return; if(!Agents.TryGetValue(agent,out r)){if(Agents.Count>=128)return;r=new AgentRecord{Pointer=agent};Agents.Add(agent,r);} r.PrefixCalls++;r.LayerPointer=layer;r.Stage="prefix";r.BotPointer=IntPtr.Zero;foreach(var d in Dispatch)if(d.Key==layer){r.BotPointer=d.Value.OwnerPointer;break;} } } catch{} }
        internal static void RecordStrategy(IntPtr agent, object previous, object result) { try { lock(Sync) if(Agents.TryGetValue(agent,out AgentRecord r)){r.StrategyCalls++;r.PreviousAction=ActionOf(previous);r.LastAction=ActionOf(result);r.Stage=result==null?"strategy-null":"strategy-updated";} } catch{} }
        internal static void RecordLazy(IntPtr agent, object action) { try { lock(Sync) if(Agents.TryGetValue(agent,out AgentRecord r)){r.LazyCalls++;r.LastLazyInput=action==null?null:Convert.ToInt32(action).ToString();r.Stage="lazy-getter";} } catch{} }
        internal static void RecordNode(IntPtr agent, IntPtr node, bool resolved) { try { lock(Sync) if(Agents.TryGetValue(agent,out AgentRecord r)){r.NodePointer=node;r.ResolveLogicResult=resolved?"hit":node==IntPtr.Zero?"null-node":"miss";r.Stage="node-resolved";} } catch{} }
        internal static void RecordAgentStage(IntPtr agent,string stage) { try { lock(Sync) if(Agents.TryGetValue(agent,out AgentRecord r)){r.Stage=stage;if(stage=="start")r.Starts++;if(stage=="update")r.Updates++;} } catch{} }
        internal static void RecordWriteback(IntPtr agent, object result) { try { lock(Sync) if(Agents.TryGetValue(agent,out AgentRecord r)){r.LastWritebackAction=ActionOf(result);r.Stage="writeback";} } catch{} }
        internal static void RecordAgentError(IntPtr agent,string stage,Exception error) { try { lock(Sync) if(Agents.TryGetValue(agent,out AgentRecord r)){r.Stage=stage;if(r.FirstError==null)r.FirstError=error==null?"unknown":error.ToString();} } catch{} }

        internal static CustomLogicWrapper ResolveLogic(IntPtr? nativePointer)
        {
            if (!nativePointer.HasValue || nativePointer.Value == IntPtr.Zero) return null;
            lock (Sync)
                return Logics.TryGetValue(nativePointer.Value, out CustomLogicWrapper wrapper) ? wrapper : null;
        }

        internal static void StopAndClear(BotOwner owner)
        {
            if (owner == null || owner.Pointer == IntPtr.Zero) return;

            IntPtr ownerPointer = owner.Pointer;
            OwnerRegistration registration;
            CustomLayerWrapper[] layers;
            CustomLogicWrapper[] logics;
            lock (Sync)
            {
                if (!Owners.TryGetValue(ownerPointer, out registration)) return;
                layers = new CustomLayerWrapper[registration.LayerPointers.Count];
                int layerIndex = 0;
                foreach (IntPtr pointer in registration.LayerPointers)
                    if (Layers.TryGetValue(pointer, out CustomLayerWrapper layer)) layers[layerIndex++] = layer;
                logics = new CustomLogicWrapper[registration.LogicPointers.Count];
                int logicIndex = 0;
                foreach (IntPtr pointer in registration.LogicPointers)
                    if (Logics.TryGetValue(pointer, out CustomLogicWrapper logic)) logics[logicIndex++] = logic;
            }

            try
            {
                foreach (CustomLayerWrapper layer in layers)
                {
                    if (layer == null) continue;
                    try { layer.Stop(); }
                    catch (Exception ex) { LogStopFailure("layer", ex); }
                }

                foreach (CustomLogicWrapper logic in logics)
                {
                    if (logic == null) continue;
                    try { logic.Stop(); }
                    catch (Exception ex) { LogStopFailure("logic", ex); }
                }
            }
            finally
            {
                lock (Sync)
                {
                    foreach (IntPtr pointer in registration.LayerPointers) { Layers.Remove(pointer); Dispatch.Remove(pointer); }
                    var staleAgents = new List<IntPtr>(); foreach (var agent in Agents) if (agent.Value.BotPointer == ownerPointer) staleAgents.Add(agent.Key); foreach (IntPtr agentPointer in staleAgents) Agents.Remove(agentPointer);
                    foreach (IntPtr pointer in registration.LogicPointers) Logics.Remove(pointer);
                    if (Owners.TryGetValue(ownerPointer, out OwnerRegistration current) && ReferenceEquals(current, registration))
                        Owners.Remove(ownerPointer);
                }
            }
        }

        private static OwnerRegistration GetOrCreateOwner(BotOwner owner)
        {
            IntPtr pointer = owner.Pointer;
            if (!Owners.TryGetValue(pointer, out OwnerRegistration registration))
            {
                registration = new OwnerRegistration(owner);
                Owners.Add(pointer, registration);
            }

            return registration;
        }

        private static void LogStopFailure(string kind, Exception exception)
        {
            try { BigBrainPlugin.BigBrainLogger.LogError("Failed stopping custom " + kind + " during brain disposal: " + exception); }
            catch { }
        }
    }
}
