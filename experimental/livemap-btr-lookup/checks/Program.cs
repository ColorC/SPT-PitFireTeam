using System.Text.Json;
using EFT.Vehicle;
var passed=new List<string>();
void Check(string name,Func<bool> check){if(!check())throw new Exception(name);passed.Add(name);}
BtrController.Instance=null;
Check("no controller",()=>BtrFixture.Host.Lookup()==null);
BtrController.Instance=new BtrController();
Check("controller without vehicle",()=>BtrFixture.Host.Lookup()==null);
var a=new BTRVehicle();BtrController.Instance.BtrVehicle=a;
Check("registered vehicle identity",()=>BtrFixture.Host.Lookup() is {Length:1} rows&&ReferenceEquals(rows[0],a));
a.Destroyed=true;
Check("Unity destroyed vehicle",()=>BtrFixture.Host.Lookup()==null);
var b=new BTRVehicle();BtrController.Instance.BtrVehicle=b;
Check("later vehicle discovered",()=>BtrFixture.Host.Lookup() is {Length:1} rows&&ReferenceEquals(rows[0],b));
b.gameObject.activeInHierarchy=false;
Check("inactive native object excluded",()=>BtrFixture.Host.Lookup()==null);
b.gameObject.activeInHierarchy=true;
Check("later activation discovered",()=>BtrFixture.Host.Lookup() is {Length:1} rows&&ReferenceEquals(rows[0],b));
BtrController.Instance=new BtrController{BtrVehicle=new BTRVehicle()};
Check("new controller identity",()=>BtrFixture.Host.Lookup() is {Length:1} rows&&ReferenceEquals(rows[0],BtrController.Instance.BtrVehicle));
Console.WriteLine(JsonSerializer.Serialize(new{passed=passed.Count,total=8,cases=passed,scope="Exact extracted candidate helper IL; native controller, active flag and Unity equality boundaries substituted. Not native vehicle lifetime acceptance."}));

namespace UnityEngine{
 public class Object{
  public bool Destroyed;
  static bool Null(Object? a)=>ReferenceEquals(a,null)||a.Destroyed;
  public static bool operator ==(Object? a,Object? b)=>Null(a)?Null(b):!Null(b)&&ReferenceEquals(a,b);
  public static bool operator !=(Object? a,Object? b)=>!(a==b);
  public override bool Equals(object? a)=>ReferenceEquals(this,a);
  public override int GetHashCode()=>base.GetHashCode();
 }
 public class GameObject:Object{public bool activeInHierarchy{get;set;}=true;}
 public class Component:Object{public GameObject gameObject{get;}=new GameObject();}
}
namespace EFT.Vehicle{
 public class BTRVehicle:UnityEngine.Component{}
 public class BtrController{
  public static BtrController? Instance{get;set;}
  public BTRVehicle? BtrVehicle{get;set;}
 }
}
namespace BtrFixture{public static class Host{public static BTRVehicle[]? Lookup()=>throw new InvalidOperationException("Actual candidate helper IL must replace this placeholder before running");}}
