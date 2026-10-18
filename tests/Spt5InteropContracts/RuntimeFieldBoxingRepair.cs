using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
internal static class RuntimeFieldBoxingRepair {
 static MethodDefinition Target(ModuleDefinition mod) {
  var t=mod.Types.SingleOrDefault(x=>x.FullName=="Il2CppInterop.Runtime.IL2CPP")??throw new InvalidDataException("Missing/ambiguous IL2CPP type.");
  var ms=t.Methods.Where(x=>x.Name=="PointerToValueGeneric"&&x.GenericParameters.Count==1&&x.Parameters.Count==3).ToArray();
  if(ms.Length!=1)throw new InvalidDataException("Missing/ambiguous PointerToValueGeneric<T>.");
  var m=ms[0];var p=m.Parameters;
  if(p[0].Name!="objectPointer"||p[1].Name!="isFieldPointer"||p[2].Name!="valueTypeWouldBeBoxed"||p[0].ParameterType.FullName!="System.IntPtr"||p[1].ParameterType.MetadataType!=MetadataType.Boolean||p[2].ParameterType.MetadataType!=MetadataType.Boolean)throw new InvalidDataException("Unexpected method signature.");
  return m;
 }
 static bool Apply(MethodDefinition m) {
  var il=m.Body.Instructions;var bx=il.Where(i=>i.OpCode.Code==Code.Call&&i.Operand is MethodReference r&&r.Name=="il2cpp_value_box").ToArray();
  if(bx.Length!=2)throw new InvalidDataException("Expected exactly two value_box calls.");
  int bi=il.IndexOf(bx[0]);if(bi+2>=il.Count||il[bi+1].OpCode.Code is not(Code.Starg or Code.Starg_S)||il[bi+1].Operand is not ParameterDefinition obj||obj.Index!=0)throw new InvalidDataException("Unknown first-box IL.");
  int si=bi+1;bool done=si+2<il.Count&&il[si+1].OpCode.Code==Code.Ldc_I4_1&&(il[si+2].OpCode.Code is Code.Starg or Code.Starg_S)&&il[si+2].Operand is ParameterDefinition flag&&flag.Index==2;
  if(done)return false;
  if(si+1>=il.Count||il[si+1].OpCode.Code is not(Code.Br or Code.Br_S)||il[si+1].Operand is not Instruction gate||gate.OpCode.Code is not(Code.Ldarg or Code.Ldarg_S or Code.Ldarg_2))throw new InvalidDataException("Unknown second-box gate.");
  int gi=il.IndexOf(gate),second=il.IndexOf(bx[1]);if(gi<0||gi+1>=second||!(gate.OpCode.Code==Code.Ldarg_2||(gate.Operand is ParameterDefinition gp&&gp.Index==2))||il[gi+1].OpCode.Code is not(Code.Brtrue or Code.Brtrue_S))throw new InvalidDataException("Unknown second-box gate condition.");
  var proc=m.Body.GetILProcessor();var one=Instruction.Create(OpCodes.Ldc_I4_1);proc.InsertAfter(il[si],one);proc.InsertAfter(one,Instruction.Create(OpCodes.Starg_S,m.Parameters[2]));return true;
 }
 public static int Run(string[] a) {
  if(a.Length<2||a.Length>4)throw new ArgumentException("repair-runtime-field-boxing <inputDll> <outputDll> [--report <json>]");
  var input=Path.GetFullPath(a[0]);var output=Path.GetFullPath(a[1]);string? report=null;
  if(input.Equals(output,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Input/output must differ.");
  if(a.Length>2){if(a.Length!=4||a[2]!="--report")throw new ArgumentException("Expected --report <json>.");report=Path.GetFullPath(a[3]);}
  using var mod=ModuleDefinition.ReadModule(input);var m=Target(mod);bool changed=Apply(m);mod.Write(output);
  var o=new{command="repair-runtime-field-boxing",input,output,method=m.FullName,changed,status=changed?"repaired":"already-repaired",sourceInputPreserved=true};
  var json=JsonSerializer.Serialize(o,new JsonSerializerOptions{WriteIndented=true});if(report!=null)File.WriteAllText(report,json);Console.WriteLine(json);return 0;
 }
 public static int SelfTest(string[] a) {
  if(a.Length!=1)throw new ArgumentException("repair-runtime-field-boxing-self-test <inputDll>");
  string input=Path.GetFullPath(a[0]);using var mod=ModuleDefinition.ReadModule(input);var m=Target(mod);var il=m.Body.Instructions;
  var original=il.ToArray();var branches=original.Where(i=>i.OpCode.Code is Code.Br or Code.Br_S or Code.Brtrue or Code.Brtrue_S).ToDictionary(i=>i,i=>i.Operand);
  if(!Apply(m))throw new InvalidDataException("First in-memory repair was not applied.");
  var boxes=il.Where(i=>i.OpCode.Code==Code.Call&&i.Operand is MethodReference r&&r.Name=="il2cpp_value_box").ToArray();int b=il.IndexOf(boxes[0]);int s=b+1;
  if(il[s+1].OpCode.Code!=Code.Ldc_I4_1||(il[s+2].OpCode.Code is not(Code.Starg or Code.Starg_S))||((ParameterDefinition)il[s+2].Operand).Index!=2||il[s+3].OpCode.Code is not(Code.Br or Code.Br_S))throw new InvalidDataException("Inserted field-box flag is not immediately before original branch.");
  var injected=new HashSet<Instruction>{il[s+1],il[s+2]};if(!original.SequenceEqual(il.Where(i=>!injected.Contains(i))))throw new InvalidDataException("Repair changed non-inserted IL/control flow.");
  foreach(var pair in branches)if(!ReferenceEquals(pair.Key.Operand,pair.Value))throw new InvalidDataException("Repair changed an original branch target.");
  int count=il.Count;if(Apply(m)||il.Count!=count)throw new InvalidDataException("Second in-memory repair was not idempotent.");
  var starg=il[s];var old=starg.OpCode;starg.OpCode=OpCodes.Nop;bool rejected=false;try{Apply(m);}catch(InvalidDataException){rejected=true;}finally{starg.OpCode=old;}
  if(!rejected||Apply(m))throw new InvalidDataException("Unknown-IL rejection or post-recovery idempotence failed.");
  Console.WriteLine(JsonSerializer.Serialize(new{command="repair-runtime-field-boxing-self-test",input,passed=true,nativeValidation=false,cases=new[]{"actual canonical Cecil shape","same-module idempotence","non-field/already-boxed control flow preserved","unknown first-box IL rejected"}}));return 0;
 }
}