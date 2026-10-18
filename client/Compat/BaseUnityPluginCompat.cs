using System;
using System.Collections;
using System.Reflection;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;
using Il2CppInterop.Runtime.Attributes;
using pitTeam.Compat;

namespace BepInEx
{
    public abstract class BaseUnityPlugin : BasePlugin
    {
        private CompatLifecycleHost _host;
        protected new ManualLogSource Logger => Log;

        public override void Load()
        {
            ManagedFramePump.Initialize();
            Il2CppBoundary.EnsureInjectedComponentRegistered<CompatLifecycleHost>();
            _host = AddComponent<CompatLifecycleHost>();
            InvokeIfPresent("Awake");
            InvokeIfPresent("Start");
            _host.Bind(this);
            Log.LogInfo("IL2CPP lifecycle initialized: " + GetType().FullName);
        }

        public Coroutine StartCoroutine(IEnumerator routine)
        {
            if (_host == null || routine == null) return null;
            return _host.StartCoroutine(routine.WrapToIl2Cpp());
        }

        public void StopCoroutine(Coroutine routine)
        {
            if (routine != null && _host != null) _host.StopCoroutine(routine);
        }

        internal void InvokeIfPresent(string name)
        {
            GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(this, null);
        }
    }

    // IL2CPP requires the callback type to be registered as a Unity component.
    public sealed class CompatLifecycleHost : MonoBehaviour
    {
        private BaseUnityPlugin _owner;

        public CompatLifecycleHost(IntPtr pointer) : base(pointer) { }

        [HideFromIl2Cpp]
        public void Bind(BaseUnityPlugin owner) => _owner = owner;
        private void Update()
        {
            ManagedFramePump.Tick();
            Spt5RuntimeProbe.Tick(_owner != null);
            _owner?.InvokeIfPresent("Update");
        }
        private void LateUpdate() => _owner?.InvokeIfPresent("LateUpdate");
        private void FixedUpdate() => _owner?.InvokeIfPresent("FixedUpdate");
        private void OnGUI() => _owner?.InvokeIfPresent("OnGUI");
        private void OnDestroy() => _owner?.InvokeIfPresent("OnDestroy");
    }
}