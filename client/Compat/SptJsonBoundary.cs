
using System;
using System.Linq;
using System.Reflection;


using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Il2CppInterop.Runtime.InteropTypes;
using EFT;

namespace pitTeam.Compat
{
    // Managed DTOs use the real managed JSON library; native objects use EFT's own converters.
    internal static class SptJsonBoundary
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            Converters = { new NativeObjectConverter() }
        };

        public static string Serialize(object value)
        {
            ManagedFramePump.RequireOwnerThread();
            return JsonConvert.SerializeObject(value, Settings);
        }

        public static T Deserialize<T>(string json)
        {
            ManagedFramePump.RequireOwnerThread();
            return JsonConvert.DeserializeObject<T>(json, Settings)!;
        }

        public static unsafe Il2CppSystem.Object ParseNativeJObject(string json)
        {
            ManagedFramePump.RequireOwnerThread();
            if (json == null) throw new ArgumentNullException(nameof(json));

            string phase = "native-class-lookup";
            try
            {
                IntPtr nativeClass = Il2CppInterop.Runtime.IL2CPP.GetIl2CppClass(
                    "Newtonsoft.Json.dll", "Newtonsoft.Json.Linq", "JObject");
                if (nativeClass == IntPtr.Zero)
                    throw new InvalidOperationException("IL2CPP could not resolve Newtonsoft.Json.Linq.JObject.");
                Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_class_init(nativeClass);

                phase = "native-parse-method-lookup";
                IntPtr parseMethod = Il2CppInterop.Runtime.IL2CPP.GetIl2CppMethod(
                    nativeClass, false, "Parse", "Newtonsoft.Json.Linq.JObject", "System.String");
                if (parseMethod == IntPtr.Zero)
                    throw new InvalidOperationException("IL2CPP could not resolve JObject.Parse(string).");

                phase = "native-string";
                IntPtr nativeString = Il2CppInterop.Runtime.IL2CPP.ManagedStringToIl2Cpp(json);
                if (nativeString == IntPtr.Zero)
                    throw new InvalidOperationException("ManagedStringToIl2Cpp returned a zero string pointer.");

                phase = "native-parse-invoke";
                IntPtr nativeException = IntPtr.Zero;
                IntPtr* nativeArgs = stackalloc IntPtr[1];
                nativeArgs[0] = nativeString;
                IntPtr nativeResult = Il2CppInterop.Runtime.IL2CPP.il2cpp_runtime_invoke(
                    parseMethod, IntPtr.Zero, (void**)nativeArgs, ref nativeException);
                Il2CppInterop.Runtime.Il2CppException.RaiseExceptionIfNecessary(nativeException);
                if (nativeResult == IntPtr.Zero)
                    throw new InvalidOperationException("Native JObject.Parse(string) returned a zero pointer.");

                phase = "native-object-wrapper-create";
                var wrapper = new Il2CppSystem.Object(nativeResult);
                phase = "native-object-wrapper-pointer";
                if (wrapper.Pointer == IntPtr.Zero)
                    throw new InvalidOperationException("Fresh native Object wrapper resolved to a zero pointer.");
                return wrapper;
            }
            catch (Exception error)
            {
                throw new InvalidOperationException("SptJsonBoundary ParseNativeJObject phase=" + phase + " failed.", error);
            }
        }

        private sealed class NativeObjectConverter : JsonConverter
        {
            public override bool CanConvert(Type type) => typeof(Il2CppObjectBase).IsAssignableFrom(type);

            public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
            {
                if (value == null) { writer.WriteNull(); return; }
                ManagedFramePump.RequireOwnerThread();
                // ProfileInfo is a runtime view; EFT exposes its JSON shape through the public descriptor.
                Il2CppObjectBase nativeValue = value is ProfileInfo profileInfo
                    ? new ProfileInfoDescriptor(profileInfo)
                    : (Il2CppObjectBase)value;
                var native = nativeValue.Cast<Il2CppSystem.Object>();
                writer.WriteRawValue(JsonExtensions.ToJson(native, EftJsonConverters.Converters));
            }

            public override object? ReadJson(JsonReader reader, Type type, object? existing, JsonSerializer serializer)
            {
                if (reader.TokenType == JsonToken.Null) return null;
                ManagedFramePump.RequireOwnerThread();
                string json = JToken.Load(reader).ToString(Formatting.None);
                var parser = typeof(JsonExtensions).GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .Single(m => m.Name == "ParseJsonTo" && m.IsGenericMethodDefinition
                        && m.GetParameters().Length == 2
                        && m.GetParameters()[0].ParameterType == typeof(string)
                        && m.GetParameters()[1].ParameterType.Name.StartsWith("Il2CppReferenceArray"));
                Type nativeType = type == typeof(ProfileInfo) ? typeof(ProfileInfoDescriptor) : type;
                object? parsed = parser.MakeGenericMethod(nativeType).Invoke(null, new object[] { json, EftJsonConverters.Converters });
                if (type != typeof(ProfileInfo)) return parsed;
                if (parsed is not ProfileInfoDescriptor descriptor || string.IsNullOrWhiteSpace(descriptor.Nickname))
                    throw new JsonSerializationException("ProfileInfo JSON did not contain a usable native descriptor nickname.");
                return new ProfileInfo(descriptor);
            }
        }
    }
}
