using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
internal static class FollowerSpawnTaskOwnershipChecks
{
 public static int Run()
 {
  var checks = new Dictionary<string,bool>();
  string? error = null, faultMessage = null;
  int owner = Thread.CurrentThread.ManagedThreadId, starts = 0;
  bool sameThread = true, successPending = false, successDone = false, faultPending = false, faultDone = false, cancelPending = false, cancelDone = false;
  void Check(string n,bool ok,string why){checks[n]=ok;if(!ok&&error==null)error=why;}
  try
  {
   var a=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var b=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
   Task all=pitTeam.Compat.FollowerSpawnTaskOwnership.StartAndJoin(new[]{1,2},id=>{starts++;sameThread &= Thread.CurrentThread.ManagedThreadId==owner;return id==1?a.Task:b.Task;});
   a.SetResult(true); successPending=!all.IsCompleted;
   Check("parallelStartCount",starts==2,"Expected two synchronous starts; actual startCount="+starts);
   Check("factoryRunsOnOwnerThread",sameThread,"Activation factory thread differs from caller thread.");
   Check("successPendingAfterFirst",successPending,"Aggregate completed after first task while peer pending.");
   b.SetResult(true); all.GetAwaiter().GetResult(); successDone=all.IsCompletedSuccessfully;
   Check("successAllComplete",successDone,"Aggregate not successfully complete after both tasks.");

   var f=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var p=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
   Task ft=pitTeam.Compat.FollowerSpawnTaskOwnership.StartAndJoin(new[]{1,2},id=>id==1?f.Task:p.Task);
   var originalFailure=new InvalidOperationException("fixture activation failure"); f.SetException(originalFailure); faultPending=!ft.IsCompleted;
   Check("faultWaitedForPeer",faultPending,"Fault aggregate completed before pending peer.");
   p.SetResult(true); Exception? observedFailure=null; try{ft.GetAwaiter().GetResult();}catch(Exception ex){observedFailure=ex;faultMessage=ex.Message;}
   faultDone=ft.IsFaulted&&ft.Exception?.GetBaseException().Message=="fixture activation failure"&&ReferenceEquals(observedFailure,originalFailure)&&faultMessage=="fixture activation failure";
   Check("faultObservedAfterPeer",faultDone,"Expected fixture activation failure after peer termination; actual="+(faultMessage??"<none>"));

   var c=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously); var cp=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
   Task ct=pitTeam.Compat.FollowerSpawnTaskOwnership.StartAndJoin(new[]{1,2},id=>id==1?c.Task:cp.Task);
   c.TrySetCanceled(); cancelPending=!ct.IsCompleted;
   Check("cancelWaitedForPeer",cancelPending,"Canceled aggregate completed before pending peer.");
   cp.SetResult(true); bool caught=false; try{ct.GetAwaiter().GetResult();}catch(OperationCanceledException){caught=true;}
   cancelDone=caught&&ct.IsCanceled; Check("cancelObservedAfterPeer",cancelDone,"Expected cancellation after peer termination.");
  }
  catch(Exception ex){if(error==null)error=ex.GetType().FullName+": "+ex.Message;}
  string[] requiredChecks = { "parallelStartCount", "factoryRunsOnOwnerThread", "successPendingAfterFirst", "successAllComplete", "faultWaitedForPeer", "faultObservedAfterPeer", "cancelWaitedForPeer", "cancelObservedAfterPeer" };
  foreach (string name in requiredChecks) if ((!checks.TryGetValue(name, out bool ok) || !ok) && error == null) error = "Missing or failed required check: " + name;
  bool passed = error == null;
  Console.WriteLine(JsonSerializer.Serialize(new {test="follower-task-ownership-self-test",passed,ownerThreadId=owner,startCount=starts,startsOnOwnerThread=sameThread,successPendingAfterFirst=successPending,successAllComplete=successDone,faultWaitedForPeer=faultPending,faultObservedAfterPeer=faultDone,faultMessage,cancelWaitedForPeer=cancelPending,cancelObservedAfterPeer=cancelDone,checks,firstError=error}));
  return passed?0:1;
 }
}