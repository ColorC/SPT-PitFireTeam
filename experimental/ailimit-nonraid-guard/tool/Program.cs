using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;

const string Expected="911f90dabf1dac8e4dfcecca867990c0e435f736ec812fbd93c42ccfb3b359c3";
void Need(bool ok,string message){if(!ok)throw new InvalidDataException(message);}
string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
Need(args.Length==3,"input output report");
string input=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]),report=Path.GetFullPath(args[2]);
string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
foreach(string path in new[]{output,report}){Need(path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Output outside experiment root");Need(!File.Exists(path),"Refusing existing output");Directory.CreateDirectory(Path.GetDirectoryName(path)!);}
Need(input!=output&&output!=report&&Hash(input)==Expected,"Exact installed input required; distinct paths required");
using var mod=ModuleDefinition.ReadModule(input);
IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types){foreach(var t in types){yield return t;foreach(var n in Types(t.NestedTypes))yield return n;}}
string Operand(object? v,MethodDefinition m)=>v switch{Instruction i=>"#"+m.Body.Instructions.IndexOf(i),Instruction[] a=>string.Join(",",a.Select(i=>m.Body.Instructions.IndexOf(i))),MemberReference r=>r.FullName,null=>"",_=>v.ToString()??""};
string Body(MethodDefinition m)=>m.HasBody?string.Join("|",m.Body.Instructions.Select(i=>i.OpCode.Code+":"+Operand(i.Operand,m)))+" EH="+m.Body.ExceptionHandlers.Count:"no body";
var before=Types(mod.Types).SelectMany(t=>t.Methods).ToDictionary(m=>m,Body);
string[] Abi()=>Types(mod.Types).SelectMany(t=>t.Methods.Where(m=>m.IsPublic||m.IsFamily||m.IsFamilyOrAssembly).Select(m=>$"{m.Attributes}:{m.FullName}").Concat(t.Fields.Where(f=>f.IsPublic||f.IsFamily||f.IsFamilyOrAssembly).Select(f=>$"{f.Attributes}:{f.FullName}"))).Order().ToArray();
var abi=Abi();var type=mod.Types.Single(t=>t.FullName=="AILimit.AILimitComponent");
var enable=type.Methods.Single(m=>m.Name=="Enable");var start=type.Methods.Single(m=>m.Name=="Start");var setup=type.Methods.Single(m=>m.Name=="SetupBotDistanceForMap");
Need(enable.IsStatic&&enable.Body.ExceptionHandlers.Count==0,"Unknown Enable shape");
var world=type.Fields.Single(f=>f.Name=="gameWorld");
var location=setup.Body.Instructions.Select(i=>i.Operand).OfType<MethodReference>().Single(m=>m.Name=="get_LocationId"&&m.DeclaringType.FullName=="EFT.GameWorld"&&m.ReturnType.FullName=="System.String");
var code=enable.Body.Instructions;
var nullBranch=code.Single(i=>i.OpCode.Code==Code.Brfalse_S&&i.Next.OpCode.Code==Code.Ret&&i.Previous.Operand is MethodReference m&&m.DeclaringType.FullName=="UnityEngine.Object"&&m.Name=="op_Equality");
var oldEntry=(Instruction)nullBranch.Operand;var nullReturn=nullBranch.Next;
Need(nullReturn.OpCode.Code==Code.Ret&&nullReturn.Next==oldEntry&&oldEntry.OpCode.Code==Code.Ldsfld&&((FieldReference)oldEntry.Operand).FullName==world.FullName,"Unknown null-world branch");
Need(code.Count(i=>ReferenceEquals(i.Operand,oldEntry))==1,"Unknown incoming raid path");
string[] clearNames={"playerInfoMapping","botList","pendingEligible","players","playerIds"};
var clearCode=new List<Instruction>();
foreach(string name in clearNames){
 var field=type.Fields.Single(f=>f.Name==name&&f.IsStatic);
 var load=start.Body.Instructions.Single(i=>i.OpCode.Code==Code.Ldsfld&&i.Operand is FieldReference f&&f.FullName==field.FullName&&i.Next.Operand is MethodReference m&&m.Name=="Clear");
 Need(load.Next.OpCode.Code==Code.Callvirt&&((MethodReference)load.Next.Operand).Parameters.Count==0,"Unknown managed collection cleanup");
 clearCode.Add(Instruction.Create(OpCodes.Ldsfld,(FieldReference)load.Operand));clearCode.Add(Instruction.Create(OpCodes.Callvirt,(MethodReference)load.Next.Operand));
}
var il=enable.Body.GetILProcessor();foreach(var instruction in clearCode)il.InsertBefore(nullReturn,instruction);
var isEmpty=new MethodReference("IsNullOrWhiteSpace",mod.TypeSystem.Boolean,mod.TypeSystem.String){HasThis=false};isEmpty.Parameters.Add(new ParameterDefinition(mod.TypeSystem.String));
var guard=new[]{Instruction.Create(OpCodes.Ldsfld,world),Instruction.Create(OpCodes.Callvirt,location),Instruction.Create(OpCodes.Call,isEmpty),Instruction.Create(OpCodes.Brtrue,clearCode[0])};
foreach(var instruction in guard)il.InsertBefore(oldEntry,instruction);
nullBranch.OpCode=OpCodes.Brfalse;nullBranch.Operand=guard[0];
enable.Body.MaxStackSize=Math.Max(enable.Body.MaxStackSize,2);
Need(before.All(p=>p.Key==enable||p.Value==Body(p.Key)),"Unexpected method change");Need(abi.SequenceEqual(Abi()),"Public/protected ABI changed");
mod.Mvid=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(Expected+":nonraid-cache-cleanup-v1"))[..16]);mod.Write(output);Need(Hash(input)==Expected,"Input was modified");
File.WriteAllText(report,JsonSerializer.Serialize(new{inputSha256=Expected,outputSha256=Hash(output),changedMethod=enable.FullName,guard="Unity-null world or null/whitespace LocationId: clear five managed actor caches and return before GetComponent/AddComponent",raidPath="Original nonempty-location component lookup/add path retained",clearedManagedCollections=clearNames,nativeActorCallsDuringCleanup=0,publicProtectedAbiUnchanged=true,allOtherMethodBodiesUnchanged=true,noNewTypeFieldOrMethod=true,inputPreserved=true,status="candidate only; native raid and hideout acceptance required"},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine(File.ReadAllText(report));
