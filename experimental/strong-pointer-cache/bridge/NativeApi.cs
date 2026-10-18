using System.Collections.Concurrent;
namespace StrongPointerFixture;
public static class NativeApi {
 sealed record Entry(nint Pointer,bool Weak){public nint Target=Pointer;}
 static readonly ConcurrentDictionary<nint,Entry> Handles=new();
 static readonly ConcurrentDictionary<nint,nint> Classes=new();
 static long next=1000;
 public static long Gets,News,Frees,ClassGets,WeakNews;
 public static bool ZeroHandle,ThrowNew,ThrowClass;
 public static Action<nint>? BeforeFree;
 public static void ResetCounters(){Gets=News=Frees=ClassGets=WeakNews=0;ZeroHandle=ThrowNew=ThrowClass=false;BeforeFree=null;}
 public static void Class(nint pointer,nint klass)=>Classes[pointer]=klass;
 public static void ClearWeak(nint handle){if(Handles.TryGetValue(handle,out var e)&&e.Weak)e.Target=0;}
 public static nint il2cpp_gchandle_new(nint obj,bool pinned){Interlocked.Increment(ref News);if(ThrowNew)throw new InvalidOperationException("synthetic strong allocation failure");if(ZeroHandle)return 0;var h=(nint)Interlocked.Increment(ref next);Handles[h]=new(obj,false);return h;}
 public static nint il2cpp_gchandle_new_weakref(nint obj,bool resurrection){Interlocked.Increment(ref WeakNews);var h=(nint)Interlocked.Increment(ref next);Handles[h]=new(obj,true);return h;}
 public static nint il2cpp_gchandle_get_target(nint handle){Interlocked.Increment(ref Gets);return Handles.TryGetValue(handle,out var e)?e.Target:0;}
 public static void il2cpp_gchandle_free(nint handle){BeforeFree?.Invoke(handle);Interlocked.Increment(ref Frees);Handles.TryRemove(handle,out _);}
 public static nint il2cpp_object_get_class(nint obj){Interlocked.Increment(ref ClassGets);if(ThrowClass)throw new InvalidOperationException("synthetic class classification failure");return Classes.GetValueOrDefault(obj);}
}
