using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Security.Cryptography;
using System.Text.Json;
if(args.Length==4&&args[0]=="--fixture"){
 using var source=ModuleDefinition.ReadModule(args[1]);using var target=ModuleDefinition.ReadModule(args[2]);
 var actual=source.Types.Single(t=>t.FullName=="LiveMap.EntityService").Methods.Single(m=>m.Name=="GetRegisteredBtrVehicles");
 var host=target.Types.Single(t=>t.FullName=="BtrFixture.Host").Methods.Single(m=>m.Name=="Lookup");
 host.Body=new MethodBody(host){InitLocals=actual.Body.InitLocals,MaxStackSize=actual.Body.MaxStackSize};
 TypeReference MapType(TypeReference t)=>target.Types.Single(x=>x.FullName==t.FullName);
 MethodReference MapMethod(MethodReference m)=>target.Types.Single(t=>t.FullName==m.DeclaringType.FullName).Methods.Single(x=>x.Name==m.Name&&x.Parameters.Count==m.Parameters.Count);
 var variables=actual.Body.Variables.ToDictionary(v=>v,v=>new VariableDefinition(MapType(v.VariableType)));foreach(var v in variables.Values)host.Body.Variables.Add(v);
 var instructions=actual.Body.Instructions.ToDictionary(i=>i,i=>Instruction.Create(OpCodes.Nop));
 foreach(var pair in instructions){pair.Value.OpCode=pair.Key.OpCode;pair.Value.Operand=pair.Key.Operand switch{Instruction i=>instructions[i],VariableDefinition v=>variables[v],MethodReference m=>MapMethod(m),TypeReference t=>MapType(t),null=>null,_=>throw new InvalidDataException("Unexpected helper operand")};host.Body.Instructions.Add(pair.Value);}
 if(File.Exists(args[3]))throw new IOException("Fixture output exists");target.Write(args[3]);
 Console.WriteLine(JsonSerializer.Serialize(new{extractedInstructions=instructions.Count,substitutions="Native controller getters and Unity equality to fixture boundaries; opcodes and branches unchanged",originalMethod=actual.FullName}));
 return;
}
const string Expected="7361c23611fd43eca81e12ab5f2f1984de06b6678f1331d2e0d761068d602c6b";
const string ExpectedInterop="b2235cd57c91e92cd60e588053b7f38c02e8e893d82a2ef31b10b05e3113e607";
void Need(bool value,string text){if(!value)throw new InvalidDataException(text);}
string Hash(string p)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant();
Need(args.Length==4,"input LiveMap.dll, matching Assembly-CSharp.dll, fresh output, fresh report required");
string input=Path.GetFullPath(args[0]),interop=Path.GetFullPath(args[1]),output=Path.GetFullPath(args[2]),report=Path.GetFullPath(args[3]);
Need(input!=output&&output!=report&&Hash(input)==Expected,"Exact original input and distinct paths required");
Need(Hash(interop)==ExpectedInterop,"Exact verified EFT interop reference required");
foreach(var p in new[]{output,report}){Need(!File.Exists(p),"Refusing existing output");Directory.CreateDirectory(Path.GetDirectoryName(p)!);}
using var mod=ModuleDefinition.ReadModule(input);using var game=ModuleDefinition.ReadModule(interop);
IEnumerable<TypeDefinition> All(IEnumerable<TypeDefinition> types){foreach(var t in types){yield return t;foreach(var n in All(t.NestedTypes))yield return n;}}
string Operand(object? o,MethodDefinition m)=>o switch{Instruction i=>"#"+m.Body.Instructions.IndexOf(i),Instruction[] a=>string.Join(",",a.Select(i=>m.Body.Instructions.IndexOf(i))),MemberReference r=>r.FullName,null=>"",_=>o.ToString()??""};
string Body(MethodDefinition m)=>m.HasBody?string.Join("|",m.Body.Instructions.Select(i=>i.OpCode.Code+":"+Operand(i.Operand,m)))+" EH="+m.Body.ExceptionHandlers.Count:"no body";
var before=All(mod.Types).SelectMany(t=>t.Methods).ToDictionary(m=>m,Body);
string[] Abi()=>All(mod.Types).SelectMany(t=>t.Methods.Where(m=>m.IsPublic||m.IsFamily||m.IsFamilyOrAssembly).Select(m=>$"{m.Attributes}:{m.FullName}").Concat(t.Fields.Where(f=>f.IsPublic||f.IsFamily||f.IsFamilyOrAssembly).Select(f=>$"{f.Attributes}:{f.FullName}"))).Order().ToArray();
var abi=Abi();var entity=mod.Types.Single(t=>t.FullName=="LiveMap.EntityService");var collect=entity.Methods.Single(m=>m.Name=="CollectBtr");
var search=collect.Body.Instructions.Single(i=>i.Operand is GenericInstanceMethod m&&m.Name=="FindObjectsOfType"&&m.DeclaringType.FullName=="UnityEngine.Object"&&m.GenericArguments.Single().FullName=="EFT.Vehicle.BTRVehicle");
var conversion=search.Next;
Need(search.OpCode.Code==Code.Call&&conversion.OpCode.Code==Code.Call&&conversion.Operand is MethodReference c&&c.Name=="op_Implicit"&&c.ReturnType is ArrayType a&&a.ElementType is GenericParameter g&&g.Position==0&&c.DeclaringType is GenericInstanceType d&&d.GenericArguments.Single().FullName=="EFT.Vehicle.BTRVehicle","Unknown original search/array conversion shape");
var vehicle=mod.ImportReference(game.Types.Single(t=>t.FullName=="EFT.Vehicle.BTRVehicle"));
var controller=game.Types.Single(t=>t.FullName=="EFT.Vehicle.BtrController");
var instance=controller.Properties.Single(p=>p.Name=="Instance").GetMethod;
var getVehicle=controller.Properties.Single(p=>p.Name=="BtrVehicle").GetMethod;
Need(instance.IsPublic&&instance.IsStatic&&getVehicle.IsPublic&&!getVehicle.IsStatic&&getVehicle.ReturnType.FullName==vehicle.FullName,"Matching public native controller getters required");
var equality=collect.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().First(m=>m.DeclaringType.FullName=="UnityEngine.Object"&&m.Name=="op_Equality");
var refs=All(mod.Types).SelectMany(t=>t.Methods).Where(m=>m.HasBody).SelectMany(m=>m.Body.Instructions).Select(i=>i.Operand).OfType<MethodReference>().ToArray();
var getObject=refs.First(m=>m.DeclaringType.FullName=="UnityEngine.Component"&&m.Name=="get_gameObject"&&m.Parameters.Count==0);
var getActive=refs.First(m=>m.DeclaringType.FullName=="UnityEngine.GameObject"&&m.Name=="get_activeInHierarchy"&&m.Parameters.Count==0);
Need(!entity.Methods.Any(m=>m.Name=="GetRegisteredBtrVehicles"),"Helper already exists");
var helper=new MethodDefinition("GetRegisteredBtrVehicles",MethodAttributes.Private|MethodAttributes.Static|MethodAttributes.HideBySig,new ArrayType(vehicle));entity.Methods.Add(helper);
var ctrl=new VariableDefinition(mod.ImportReference(controller));var veh=new VariableDefinition(vehicle);helper.Body.Variables.Add(ctrl);helper.Body.Variables.Add(veh);helper.Body.InitLocals=true;helper.Body.MaxStackSize=4;
var il=helper.Body.GetILProcessor();var have=Instruction.Create(OpCodes.Ldloc,ctrl);var checkActive=Instruction.Create(OpCodes.Ldloc,veh);var valid=Instruction.Create(OpCodes.Ldc_I4_1);
foreach(var i in new[]{
 Instruction.Create(OpCodes.Call,mod.ImportReference(instance)),Instruction.Create(OpCodes.Stloc,ctrl),Instruction.Create(OpCodes.Ldloc,ctrl),Instruction.Create(OpCodes.Brtrue,have),Instruction.Create(OpCodes.Ldnull),Instruction.Create(OpCodes.Ret),
 have,Instruction.Create(OpCodes.Callvirt,mod.ImportReference(getVehicle)),Instruction.Create(OpCodes.Stloc,veh),Instruction.Create(OpCodes.Ldloc,veh),Instruction.Create(OpCodes.Ldnull),Instruction.Create(OpCodes.Call,mod.ImportReference(equality)),Instruction.Create(OpCodes.Brfalse,checkActive),Instruction.Create(OpCodes.Ldnull),Instruction.Create(OpCodes.Ret),
 checkActive,Instruction.Create(OpCodes.Callvirt,mod.ImportReference(getObject)),Instruction.Create(OpCodes.Callvirt,mod.ImportReference(getActive)),Instruction.Create(OpCodes.Brtrue,valid),Instruction.Create(OpCodes.Ldnull),Instruction.Create(OpCodes.Ret),
 valid,Instruction.Create(OpCodes.Newarr,vehicle),Instruction.Create(OpCodes.Dup),Instruction.Create(OpCodes.Ldc_I4_0),Instruction.Create(OpCodes.Ldloc,veh),Instruction.Create(OpCodes.Stelem_Ref),Instruction.Create(OpCodes.Ret)
})il.Append(i);
search.OpCode=OpCodes.Call;search.Operand=helper;conversion.OpCode=OpCodes.Nop;conversion.Operand=null;
Need(before.All(p=>p.Key==collect||p.Value==Body(p.Key)),"Unexpected existing method change");Need(abi.SequenceEqual(Abi()),"Public/protected ABI changed");
Need(!collect.Body.Instructions.Any(i=>i.Operand is MethodReference m&&m.Name=="FindObjectsOfType"),"Global scene search remains");
mod.Mvid=new Guid(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Expected+":btr-controller-active-lookup-v2"))[..16]);mod.Write(output);Need(Hash(input)==Expected,"Original input changed");
File.WriteAllText(report,JsonSerializer.Serialize(new{inputSha256=Expected,outputSha256=Hash(output),interopSha256=Hash(interop),changedExistingMethod=collect.FullName,addedPrivateHelper=helper.FullName,lookup="BtrController.Instance.BtrVehicle with Unity null and activeInHierarchy checks; zero/one registered active native vehicle",noGlobalSceneSearch=true,originalInactiveObjectExclusionPreserved=true,originalEightSecondRetryAndPositionCachePreserved=true,lateSpawnRechecked=true,allOtherExistingMethodBodiesUnchanged=true,publicProtectedAbiUnchanged=true,status="candidate; native empty/late/visible vehicle validation limits must be recorded"},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(File.ReadAllText(report));
